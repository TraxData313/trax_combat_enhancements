using System;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 76 - THE AI PREFERS THE SPEAR + A THRUST PASSES A FRIEND (Toni 2026-10-22: "my shield wall-ers have spears but they keep using their
    /// swords, even though the spear has a longer reach"; RBM fixes it and its second rank is effective; AI_NOTES "Step 76", DESIGN decision 70).
    ///
    /// THE PREFERENCE (RBM's WeaponPreference, its source read; no Harmony): every AI man (both sides, never you) is looked at every ~SpearLookSeconds
    /// (jittered by his index, after deployment). One whose four slots hold a melee polearm AND a melee sidearm (Core <see cref="SpearMath.Classify"/>,
    /// re-read only when a slot's item changes) gets RBM's two timers (<see cref="SpearMath.Step"/>): an enemy within his CLOSE RADIUS (from his
    /// longest polearm's length - <see cref="SpearMath.CloseRadius"/>), not running away and - SpearKeepVsPolearm - not wielding a polearm himself,
    /// for SpearSidearmAfterSeconds = the sidearm favoured; none for SpearPolearmBackAfterSeconds (mounted ...BackMountedSeconds) = the polearm again;
    /// a rider faster than SpearRidingFastMetresPerSecond has nothing close; a BACK-RANK man (rank 1+ with a man of his in front, a wall or a halted
    /// line - <see cref="SpearMath.BackRank"/>) keeps the polearm at once. A flip marks him for a recompute (the tick's budgeted
    /// <c>UpdateAgentProperties</c>); the stat decorator then writes the favor from his flag (<see cref="SpearFavorFor"/>) - nothing near him is ever
    /// queried inside the stat model. THE WIELD FALLBACK (SpearWieldOverride, off): step 16's input hook turns a sidearm Wield into the polearm's.
    ///
    /// THE THRUST (<see cref="ThrustPass"/>, the damage decorator): a friendly contact is counted here by verdict; a pass arms "did the same man hit an
    /// enemy within 0.5 s" and a watch of the friend passed (a hit reaction within 0.5 s = he still flinched); the friendly-hit stun is held / given
    /// back from this tick.
    ///
    /// Master switch FIRST: off = every favour lifted (recomputed), the stun given back; the looks go on and MEASURE (the summary's OFF columns).
    /// Main thread; no allocation per look (a reused slot array, a fixed watch pool).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly SpearStats _spearStats = new SpearStats();
        private readonly ThrustPassStats _thrustStats = new ThrustPassStats();
        private readonly SpearSlot[] _spearSlots = new SpearSlot[4];
        private SpearRules _spearSeen;
        private bool _spearSeenSet;
        private ThrustPassRules _thrustSeen;
        private bool _thrustSeenSet;
        private double _spearLastTick = double.NaN;
        private int _spearFirst; // a bit per first-time line

        private const int FlinchPool = 64;
        private readonly TrackedAgent?[] _flinchMen = new TrackedAgent?[FlinchPool];
        private readonly double[] _flinchUntil = new double[FlinchPool];
        private int _flinchCount;

        /// <summary>This mission's spear numbers (the smoke reads them).</summary>
        internal SpearStats SpearStats => _spearStats;

        internal ThrustPassStats ThrustStats => _thrustStats;

        /// <summary>The engine reads (the smoke swaps in a stand-in).</summary>
        internal ISpearBody SpearBody { get; set; } = SpearBodyDefault.Current;

        /// <summary>The mission's time (the last tick's without a mission - the offline smoke).</summary>
        private double SpearNow => Mission != null ? Mission.CurrentTime : double.IsNaN(_spearLastTick) ? 0 : _spearLastTick;

        private bool FirstTime(int bit)
        {
            if ((_spearFirst & (1 << bit)) != 0) return false;
            _spearFirst |= 1 << bit;
            return true;
        }

        private void StartSpears()
        {
            var s = TraxSettings.Shared;
            var r = SpearRules.From(s);
            var t = ThrustPassRules.From(s);
            _spearSeen = r;
            _spearSeenSet = true;
            _thrustSeen = t;
            _thrustSeenSet = true;
            TraxLog.Info("spears", "mission start, the AI prefers the spear (step 76): " + r.Describe() + " - read live; the [summary] \"spears\" lines split the "
                + "time each man held the polearm / the sidearm by what was close (PLAYTEST D41)");
            TraxLog.Info("spears", "mission start, thrusts past friends (step 76): " + t.Describe() + "; the [summary] \"thrusts past friends\" line counts every "
                + "friendly contact and whether the same man then hit an enemy");
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick (from TickAthletics, own try there): the settings (the master switch first - off lifts every favour), the friendly-hit
        /// stun, the flinch watch, then each man whose look is due.</summary>
        internal void TickSpears(double now)
        {
            _spearLastTick = now;
            var s = TraxSettings.Shared;
            var r = SpearRules.From(s);
            var t = ThrustPassRules.From(s);
            TickThrustRules(in t, now);
            if (!_spearSeenSet || !r.SameAs(in _spearSeen))
            {
                var was = _spearSeen;
                bool had = _spearSeenSet;
                _spearSeen = r;
                _spearSeenSet = true;
                if (had)
                {
                    _spearStats.SwitchChanges++;
                    TraxLog.Info("spears", "spear settings changed mid-mission at " + Sec(now) + " s: " + r.Describe());
                    if (!r.On) UnfavorAll(now);
                    else if (!r.SameFavors(in was)) MarkFavoredDirty();
                    if (!r.WieldOn) ClearWieldWishes(now);
                }
            }
            if (r.On) _spearStats.EverOn = true;
            if (_flinchCount > 0) TickFlinchWatch(now);
            var body = SpearBody;
            if (!body.Ready()) return;
            long start = Stopwatch.GetTimestamp();
            int looks = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st.Removed || now < st.Spear.NextLook) continue;
                try
                {
                    LookSpear(st, now, in r, body);
                    looks++;
                }
                catch (Exception e)
                {
                    _spearStats.Errors++;
                    st.Spear.NextLook = now + r.LookSeconds;
                    Failed("spears.look", e);
                }
            }
            if (looks > 0)
            {
                _spearStats.CostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                _spearStats.CostLooks += looks;
            }
        }

        /// <summary>One man's look: the gate, the loadout (cached), his surroundings, the condition, the time split, RBM's timers, the wield wish.</summary>
        internal void LookSpear(TrackedAgent st, double now, in SpearRules r, ISpearBody body)
        {
            double gap = double.IsNaN(st.Spear.LastLook) || now < st.Spear.LastLook ? 0 : Math.Min(now - st.Spear.LastLook, 2 * r.LookSeconds);
            st.Spear.LastLook = now;
            st.Spear.Looks++;
            st.Spear.NextLook = SpearMath.NextLook(in r, now, st.AgentIndex, st.Spear.Looks);
            if (IsPlayer(st) || !body.Gate(st))
            {
                if (st.Spear.Favored) Unfavor(st, now);
                return;
            }
            _spearStats.Looks++;
            if (body.ReadLoadout(st, _spearSlots) || !st.Spear.Read)
            {
                st.Spear.Loadout = SpearMath.Classify(_spearSlots);
                st.Spear.Read = true;
                _spearStats.LoadoutReads++;
                if (st.Spear.Loadout.Both && !st.SpearCountedBoth)
                {
                    st.SpearCountedBoth = true;
                    _spearStats.MenBoth++;
                    if (st.SpearCounted) _spearStats.MenWithout--;
                    _spearStats.AddRadius(SpearMath.CloseRadius(in r, st.Spear.Loadout.LengthCm), st.Spear.Loadout.LengthCm > 0);
                    if (FirstTime(0))
                        TraxLog.Info("spears", "first AI man with a polearm AND a sidearm this mission: " + Name(st) + " - his polearm in slot "
                            + st.Spear.Loadout.PolearmSlot + " (" + (st.Spear.Loadout.LengthCm > 0 ? F2(st.Spear.Loadout.LengthCm / 100.0) + " m" : "length not known")
                            + "), close radius " + F2(SpearMath.CloseRadius(in r, st.Spear.Loadout.LengthCm)) + " m; polearm slots 0x" + st.Spear.Loadout.PolearmMask.ToString("X")
                            + ", sidearm slots 0x" + st.Spear.Loadout.SidearmMask.ToString("X"));
                }
                else if (!st.SpearCounted && !st.Spear.Loadout.Both) _spearStats.MenWithout++;
                st.SpearCounted = true;
            }
            if (!st.Spear.Loadout.Both)
            {
                if (st.Spear.Favored) Unfavor(st, now);
                return;
            }
            body.Context(st, out var c);
            bool riding = SpearMath.RidingFast(in r, in c);
            bool back = SpearMath.BackRank(in r, in c);
            int other = 0, polearm = 0;
            double radius = SpearMath.CloseRadius(in r, st.Spear.Loadout.LengthCm);
            if (!riding && !back) body.CloseEnemies(st, radius, out other, out polearm);
            var cond = SpearMath.Condition(in r, riding, back, other, polearm);
            var held = SpearMath.HeldOf(body.WieldedSlot(st), in st.Spear.Loadout);
            st.SpearHeldNow = held;
            _spearStats.AddTime(r.On, cond, held, gap);
            st.Spear.Condition = cond;
            if (cond == SpearCondition.BackRank && FirstTime(1))
                TraxLog.Info("spears", "first back-rank man this mission: " + Name(st) + " - rank " + c.Rank + " of a " + c.Arrangement + " under " + c.Order
                    + (c.StandsFirm ? " (standing firm)" : string.Empty) + ", a man of his in front: he keeps the polearm whatever is close (holding " + SpearMath.Name(held) + ")");
            if (cond == SpearCondition.FacingPolearm && FirstTime(2))
                TraxLog.Info("spears", "first man facing only polearms this mission: " + Name(st) + " - " + polearm + " enemy polearm(s) within " + F2(radius)
                    + " m: he keeps his (holding " + SpearMath.Name(held) + ")");
            if (!r.On)
            {
                if (st.Spear.Favored) Unfavor(st, now);
                return;
            }
            if (!st.Spear.Favored)
            {
                st.Spear.Favored = true;
                st.Spear.Close = false;
                st.Spear.CloseSince = double.NaN;
                st.Spear.LastClose = double.NaN;
                st.SpeedDirty = true; // the favor written at his next recompute
                _spearStats.Flip(SpearFlip.Started);
            }
            var flip = SpearMath.Step(in r, ref st.Spear, now, cond, c.Mounted);
            if (flip != SpearFlip.None)
            {
                st.SpeedDirty = true;
                _spearStats.Flip(flip);
                int bit = flip == SpearFlip.ToSidearm ? 3 : flip == SpearFlip.ToPolearm ? 4 : 5;
                if (FirstTime(bit))
                    TraxLog.Info("spears", "first flip " + SpearMath.Name(flip) + " this mission at " + Sec(now) + " s: " + Name(st) + " - " + other + " enemy(ies) close, "
                        + polearm + " with a polearm, radius " + F2(radius) + " m" + (c.Mounted ? ", mounted at " + F1(c.MountSpeed) + " m/s" : string.Empty)
                        + "; holding " + SpearMath.Name(held) + "; the favor now " + (st.Spear.Close ? "the sidearm x" + F0(r.SidearmFavor) : "the polearm x" + F0(r.PolearmFavor)));
            }
            WieldWish(st, r.WieldOn && !st.Spear.Close, now, body);
        }

        /// <summary>The wield fallback's wish on / off for him (hooked when it goes on, released when off).</summary>
        private void WieldWish(TrackedAgent st, bool want, double now, ISpearBody body)
        {
            bool on = st.Input != null && st.Input.SpearWield;
            if (want == on) return;
            AiInputHook.SetSpearWield(st, want, st.Spear.Loadout.PolearmSlot, st.Spear.Loadout.SidearmMask, now);
            if (want)
            {
                _spearStats.WieldWishes++;
                body.Hook(st);
            }
            else body.Release(st);
        }

        private void Unfavor(TrackedAgent st, double now)
        {
            st.Spear.Favored = false;
            st.Spear.Close = false;
            st.Spear.CloseSince = double.NaN;
            st.Spear.LastClose = double.NaN;
            if (!st.Removed) st.SpeedDirty = true; // the game's 1 / 1 / 1 at his next recompute
            _spearStats.Cleared++;
            if (st.Input != null && st.Input.SpearWield)
            {
                AiInputHook.SetSpearWield(st, false, -1, 0, now);
                if (!st.Removed) SpearBody.Release(st);
            }
        }

        private void UnfavorAll(double now)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (!st.Spear.Favored) continue;
                Unfavor(st, now);
                n++;
            }
            TraxLog.Info("spears", "the spear preference OFF at " + Sec(now) + " s (" + (_spearSeen.OffBecause ?? "?") + "): " + n
                + " men's favors lifted (the game's own at their next recompute); the looks go on and measure");
        }

        private void MarkFavoredDirty()
        {
            for (int i = 0; i < _count; i++)
                if (_dense[i].Spear.Favored) _dense[i].SpeedDirty = true;
        }

        private void ClearWieldWishes(double now)
        {
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st.Input == null || !st.Input.SpearWield) continue;
                AiInputHook.SetSpearWield(st, false, -1, 0, now);
                if (!st.Removed) SpearBody.Release(st);
            }
        }

        /// <summary>He left the field: no engine call on him.</summary>
        private void SpearLeftField(TrackedAgent st)
        {
            if (st.Input != null && st.Input.SpearWield) AiInputHook.SetSpearWield(st, false, -1, 0, SafeNow());
            st.Spear.Favored = false;
        }

        /// <summary>
        /// The stat decorator's question (any thread that recomputes him - reads only): is his spear preference live, and which favor. False = the
        /// game's own (not tracked, the switches off, no flag).
        /// </summary>
        internal static bool SpearFavorFor(Agent agent, out bool close, out float favor)
        {
            close = false;
            favor = 1f;
            var logic = _current;
            if (logic == null || agent == null) return false;
            var s = TraxSettings.Shared;
            if (!s.ModEnabled || !s.SpearPreferenceEnabled) return false; // the master switch first
            var st = logic.Get(agent);
            if (st == null || !st.Spear.Favored) return false;
            close = st.Spear.Close;
            if (close)
            {
                favor = s.SpearSidearmFavor;
                Interlocked.Increment(ref logic._spearStats.FavorSidearm);
            }
            else
            {
                favor = s.SpearPolearmFavor;
                Interlocked.Increment(ref logic._spearStats.FavorPolearm);
            }
            return true;
        }

        /// <summary>From StartRelease (Athletics on): an AI attack of a man with both weapons - holding which, and from a back rank.</summary>
        private void NoteSpearAttack(TrackedAgent st, bool mounted)
        {
            if (!st.Spear.Read || !st.Spear.Loadout.Both || IsPlayer(st)) return;
            var held = st.SpearHeldNow;
            if (held == SpearHeld.Polearm) _spearStats.AttacksHoldingPolearm++;
            else if (held == SpearHeld.Sidearm) _spearStats.AttacksHoldingSidearm++;
            if (st.Spear.Condition != SpearCondition.BackRank) return;
            _spearStats.BackRankAttacks++;
            if (held == SpearHeld.Polearm)
            {
                _spearStats.BackRankAttacksPolearm++;
                if (FirstTime(6))
                    TraxLog.Info("spears", "first attack by a back-rank man holding his polearm this mission at " + Sec(SpearNow) + " s: " + Name(st)
                        + " - the native AI attacks with a mate in front of him");
            }
        }

        // ------------------------------------------------------------------ the thrust past a friend

        /// <summary>The damage decorator saw a melee collision with a FRIEND (main thread): counted by verdict; a pass arms the follow-up and the
        /// friend's flinch watch.</summary>
        internal void NoteFriendlyContact(Agent attacker, Agent defender, ThrustVerdict v, bool polearm, bool onShield)
        {
            _thrustStats.Add(v);
            var a = Get(attacker);
            double now = SpearNow;
            if (v == ThrustVerdict.Swing && FirstTime(7))
                TraxLog.Info("spears", "first SWING into a friend this mission at " + Sec(now) + " s" + (a != null ? " by " + Name(a) : string.Empty)
                    + " - it bounces as in the game (only thrusts pass)");
            if (v != ThrustVerdict.Passed) return;
            if (polearm) _thrustStats.PassedPolearm++;
            if (onShield) _thrustStats.PassedOnShield++;
            if (a != null)
            {
                a.ThrustPassAt = now;
                if (IsPlayer(a)) _thrustStats.PassedByPlayer++;
                if (a.Spear.Condition == SpearCondition.BackRank && a.Spear.Favored) _thrustStats.PassedBackRank++;
            }
            var d = Get(defender);
            if (d != null && Rules.Enabled && _flinchCount < FlinchPool)
            {
                _flinchMen[_flinchCount] = d;
                _flinchUntil[_flinchCount] = now + ThrustPassMath.FlinchWatchSeconds;
                _flinchCount++;
                _thrustStats.PassesWatched++;
            }
            if (FirstTime(8))
                TraxLog.Info("spears", "first THRUST past a friend this mission at " + Sec(now) + " s: " + (a != null ? Name(a) : "(untracked)") + " through "
                    + (d != null ? Name(d) : "(untracked)") + (polearm ? " with a polearm" : " (not a polearm)") + (onShield ? ", into his shield" : string.Empty)
                    + (a != null && a.Spear.Condition == SpearCondition.BackRank ? ", from a back rank" : string.Empty) + " - the weapon carries on (ContinueChecking)");
        }

        /// <summary>The damage decorator saw a THRUST hit an enemy (main thread): the same man passed a friend within 0.5 s = the spear reached.</summary>
        internal void NoteEnemyThrust(Agent attacker, bool landed, bool blocked)
        {
            _thrustStats.EnemyThrusts++;
            var a = Get(attacker);
            if (a == null || double.IsNaN(a.ThrustPassAt)) return;
            double now = SpearNow;
            double since = now - a.ThrustPassAt;
            if (since < 0 || since > ThrustPassMath.FollowSeconds)
            {
                if (since > ThrustPassMath.FollowSeconds) a.ThrustPassAt = double.NaN;
                return;
            }
            a.ThrustPassAt = double.NaN;
            _thrustStats.FollowedAny++;
            if (landed) _thrustStats.FollowedLanded++;
            else if (blocked) _thrustStats.FollowedBlocked++;
            if (FirstTime(9))
                TraxLog.Info("spears", "first thrust past a friend that then HIT AN ENEMY this mission at " + Sec(now) + " s: " + Name(a) + ", " + F2(since)
                    + " s after the pass - " + (landed ? "landed" : blocked ? "blocked" : "touched"));
        }

        /// <summary>The friends passed: a hit reaction (a Strike action on either channel - the poll's reads, Athletics on) within 0.5 s = he still
        /// flinched; none = he did not.</summary>
        private void TickFlinchWatch(double now)
        {
            for (int i = _flinchCount - 1; i >= 0; i--)
            {
                var st = _flinchMen[i];
                bool done;
                if (st == null || st.Removed) done = true;
                else if (ImpactActions.IsStrike(st.PrevAction) || ImpactActions.IsStrike(st.PrevLowerAction))
                {
                    _thrustStats.FriendFlinched++;
                    done = true;
                }
                else if (now > _flinchUntil[i])
                {
                    _thrustStats.FriendFine++;
                    done = true;
                }
                else done = false;
                if (!done) continue;
                int last = --_flinchCount;
                _flinchMen[i] = _flinchMen[last];
                _flinchUntil[i] = _flinchUntil[last];
                _flinchMen[last] = null;
            }
        }

        /// <summary>The friendly-hit stun held while the rule is on, the game's own given back when it is off (the master switch first); a line per
        /// change.</summary>
        private void TickThrustRules(in ThrustPassRules t, double now)
        {
            if (_thrustSeenSet && !t.SameAs(in _thrustSeen))
            {
                _thrustStats.SwitchChanges++;
                TraxLog.Info("spears", "thrust settings changed mid-mission at " + Sec(now) + " s: " + t.Describe());
            }
            _thrustSeen = t;
            _thrustSeenSet = true;
            if (t.On) _thrustStats.EverOn = true;
            double want = ThrustPassMath.StunWanted(in t);
            try
            {
                if (!double.IsNaN(want))
                {
                    if (ThrustPass.HoldStun((float)want))
                    {
                        _thrustStats.StunWrites++;
                        _thrustStats.StunSet = want;
                        _thrustStats.StunGame = ThrustPass.GameStun;
                        TraxLog.Info("spears", "the friendly-hit stun (StunPeriodAttackerFriendlyFire) " + F2(ThrustPass.GameStun) + " s → " + F2(want)
                            + " s at " + Sec(now) + " s (FriendlyHitStunSeconds; the game's own back at the battle's end / switched off)");
                    }
                }
                else if (ThrustPass.RestoreStun())
                {
                    _thrustStats.StunRestores++;
                    TraxLog.Info("spears", "the friendly-hit stun given back at " + Sec(now) + " s: " + F2(ThrustPass.StunNow) + " s (" + (t.OffBecause ?? "?") + " off)");
                }
            }
            catch (Exception e)
            {
                _thrustStats.Errors++;
                Failed("spears.stun", e);
            }
        }

        /// <summary>The game's friendly-hit stun back (mission end / teardown) - always safe to call.</summary>
        private void CloseSpears()
        {
            try
            {
                if (ThrustPass.RestoreStun()) _thrustStats.StunRestores++;
            }
            catch (Exception e)
            {
                Failed("spears.stun-restore", e);
            }
        }

        internal void WriteSpearSummary()
        {
            CloseSpears();
            var s = TraxSettings.Shared;
            var r = SpearRules.From(s);
            foreach (var line in _spearStats.SummaryLines(in r)) TraxLog.Info("summary", line);
            TraxLog.Info("summary", _thrustStats.SummaryLine(ThrustPassRules.From(s)));
        }
    }
}
