using System;
using System.Globalization;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 106a - OPENINGS, THE SHADOW (Toni 2026-11-09; AI_NOTES "Step 106", DESIGN decision 95). NOTHING is applied: every correct block
    /// (right-side shield / parry) of YOUR melee attack by an AI man on foot - seen at the impact decision's block seam, next to
    /// <see cref="TimedBlockDecided"/> - is planned by Core <see cref="OpeningMath.Plan"/> (the chain's link, the guard that WOULD be forced, the
    /// window) and followed:
    ///   - your next melee WIND-UP (step 13's phases - <see cref="OpeningWindUp"/>): its delay from the block and its direction (channel 1's action
    ///     direction, read once) → <see cref="OpeningMath.Verdict"/> (into the opening / wrong direction / too early / too late);
    ///   - its CONTACT (OnMeleeHit, before the impact decision - <see cref="OpeningContact"/>): the time from the wind-up, what it met, and whether the
    ///     wind-up's direction agreed with the contact's (an engine fact for 106b);
    ///   - the chain's resets (your hit landed on him, the gap, his hit on you, another target, a miss by the setting).
    /// Also counted: your attack's direction × his guard read at contact - the engine's SIDE PAIRING (<c>OpeningSidesCrossed</c>), and AI vs AI
    /// correct blocks on foot. Master switch first (through <see cref="OpeningRules.ShadowOn"/>): off = the chain and the follow-up dropped at the
    /// next tick. Main thread; one record, no allocation per tick (lines only for your blocks, the first <see cref="OpeningFullLines"/> at Info).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>Your openings logged at Info per mission; the rest verbose.</summary>
        internal const int OpeningFullLines = 40;

        private readonly OpeningStats _open = new OpeningStats();
        private OpeningChain _openChain = OpeningChain.None;
        private OpeningPlan _openPlan;
        private bool _openPending, _openWound;
        private int _openTarget = -1, _openWindUpDir = OpeningMath.Unknown, _openLines;
        private double _openBlockAt, _openWindUpAt;
        private OpeningVerdict _openVerdict;

        internal OpeningStats OpeningStats => _open;

        /// <summary>The chain as it stands (the smoke reads it).</summary>
        internal OpeningChain OpeningChain => _openChain;

        /// <summary>The last opening planned (the smoke reads it).</summary>
        internal OpeningPlan LastOpening { get; private set; }

        /// <summary>The last verdict on a swing after an opening (the smoke reads it).</summary>
        internal OpeningVerdict LastOpeningVerdict { get; private set; }

        /// <summary>An opening waits for your swing or its contact (the smoke reads it).</summary>
        internal bool OpeningPending => _openPending;

        /// <summary>The openings' dice (the guard picked); the smoke seeds its own.</summary>
        internal IRandomSource OpeningDice { get; set; } = ThreadSafeRandom.Shared;

        /// <summary>His guard at contact - channel 1's action direction (DefendUp 4 .. DefendRight 7; anything else = unread). The smoke's stand-in.</summary>
        internal Func<Agent, int> OpeningGuardRead { get; set; } = GameActionDirection;

        /// <summary>Your swing's direction at its wind-up - channel 1's action direction (AttackUp 0 .. AttackRight 3), the engine's
        /// <c>AttackDirection</c> when that is not an attack. The smoke's stand-in.</summary>
        internal Func<Agent, int> OpeningSwingRead { get; set; } = GameSwingDirection;

        /// <summary>The attacks your wielded weapon can make (<see cref="OpeningMath.WeaponMask"/>). The smoke's stand-in.</summary>
        internal Func<Agent, int> OpeningWeaponRead { get; set; } = GameWeaponMask;

        private static int GameActionDirection(Agent a) => (int)a.GetCurrentActionDirection(1);

        private static int GameSwingDirection(Agent a)
        {
            int d = (int)a.GetCurrentActionDirection(1);
            return OpeningMath.IsAttack(d) ? d : (int)a.AttackDirection;
        }

        private static int GameWeaponMask(Agent a)
        {
            var w = a.WieldedWeapon;
            if (w.IsEmpty) return OpeningMath.AllAttacks;
            var u = w.CurrentUsageItem;
            if (u == null) return OpeningMath.AllAttacks;
            return OpeningMath.WeaponMask(u.SwingDamage > 0, u.ThrustDamage > 0);
        }

        /// <summary>From StartImpactModel: the mission-start line.</summary>
        private void StartOpenings()
        {
            _openChain = OpeningChain.None;
            _openPending = false;
            TraxLog.Info("impact", "mission start: " + OpeningRules.From(TraxSettings.Shared).Describe()
                                   + " - step 106a is a SHADOW: nothing is applied; the [summary] \"openings\" line (PLAYTEST D77)");
        }

        // ------------------------------------------------------------------ the block (the impact decision's seam)

        /// <summary>
        /// From <see cref="ImpactDecide"/> right after <see cref="TimedBlockDecided"/>: a decided block. A correct block of YOUR attack by an AI man =
        /// the chain's next link and an opening planned; AI vs AI on foot counted. Its own try.
        /// </summary>
        private void OpeningShadowBlock(TrackedAgent a, TrackedAgent v, in ImpactFacts f, in ImpactDecision d, double now)
        {
            if (!ImpactMath.CanRiposte(d.Event)) return;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.ShadowOn) return;
                bool you = f.AttackerIsPlayer && YouDrive(a);
                if (!you)
                {
                    if (!f.AttackerIsPlayer && !f.DefenderIsPlayer && !f.AttackerMounted && !f.DefenderMounted) _open.AiVsAi++;
                    return;
                }
                if (f.DefenderIsPlayer) return;
                CloseOpening(now, Core.OpeningContact.None); // a block before the last opening's swing was seen - judged now
                int k = _openChain.NoteBlock(v.AgentIndex, now, in r, out var reset);
                _open.NoteReset(reset);
                int guardAtContact = OpeningMath.Unknown;
                try
                {
                    guardAtContact = OpeningGuardRead(v.Agent);
                }
                catch (Exception)
                {
                    _open.GuardReadFailed++;
                }
                if (!OpeningMath.IsGuard(guardAtContact)) guardAtContact = OpeningMath.Unknown;
                int yourAttack = !double.IsNaN(a.RecoilDirectionAt) && Math.Abs(a.RecoilDirectionAt - now) < 1e-6 ? a.RecoilDirection : OpeningMath.Unknown;
                _open.NotePair(yourAttack, guardAtContact);
                int mask = OpeningMath.AllAttacks;
                try
                {
                    mask = OpeningWeaponRead(a.Agent);
                }
                catch (Exception)
                {
                    mask = OpeningMath.AllAttacks;
                }
                bool shield = d.Event == ImpactEvent.RightBlock;
                var plan = OpeningMath.Plan(in r, k, f.AttackerSkill, shield, f.AttackerMounted || f.DefenderMounted, guardAtContact, yourAttack, mask, OpeningDice);
                LastOpening = plan;
                _open.NoteBlock(in plan);
                if (plan.Opens)
                {
                    _openPending = true;
                    _openWound = false;
                    _openTarget = v.AgentIndex;
                    _openPlan = plan;
                    _openBlockAt = now;
                    _openWindUpAt = double.NaN;
                    _openWindUpDir = OpeningMath.Unknown;
                }
                OpeningLine("openings (shadow): YOUR " + OpeningMath.AttackName(yourAttack) + " blocked by " + Name(v) + " (" + (shield ? "shield" : "weapon") + ", his "
                            + OpeningMath.GuardName(guardAtContact) + " at contact) at " + Sec(now) + " s - link " + plan.K.ToString(CultureInfo.InvariantCulture)
                            + (reset != OpeningReset.None ? " (the chain restarted: " + OpeningMath.ResetName(reset) + ")" : string.Empty) + ": "
                            + (plan.Opens
                                ? "his guard WOULD go to the " + OpeningMath.GuardName(plan.Guard) + " (of " + plan.Candidates.ToString(CultureInfo.InvariantCulture) + ") -> opening: your "
                                  + OpeningMath.AttackName(plan.Opening) + ", arrow " + OpeningMath.ArrowName(plan.Arrow) + "; start it " + F2(r.ForceDelay) + "-" + F2(r.ForceDelay + plan.Window)
                                  + " s after the block (skill " + SkillText(f.AttackerSkill) + "); a hit would reel him " + F2(plan.Reel) + " s"
                                  + (shield ? ", guard break " + Pct0(plan.BreakChance) : string.Empty)
                                : "no opening (" + SkipWords(plan.Skip, in r) + ")"));
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        private static string SkillText(double skill) => double.IsNaN(skill) ? "?" : skill.ToString("0", CultureInfo.InvariantCulture);

        private static string SkipWords(OpeningSkip s, in OpeningRules r) => s switch
        {
            OpeningSkip.Mounted => "a rider - on foot only",
            OpeningSkip.ShieldTooEarly => "a shield opens from link " + r.ShieldMinChain.ToString(CultureInfo.InvariantCulture),
            OpeningSkip.NoOpeningForWeapon => "no other guard opens an attack your weapon can make",
            _ => "-",
        };

        // ------------------------------------------------------------------ your next swing

        /// <summary>
        /// Your melee WIND-UP began (step 13's phases, the tick's poll or the hit callback): the chain's gap checked, and the waiting opening judged
        /// by this swing's start and direction (one read). Its own try.
        /// </summary>
        internal void OpeningWindUp(TrackedAgent st, double now)
        {
            if (!_openPending && !_openChain.Active) return;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.ShadowOn) return;
                if (_openChain.Expired(now, in r))
                {
                    CloseOpening(now, Core.OpeningContact.None);
                    ResetOpeningChain(OpeningReset.Gap);
                    return;
                }
                if (!_openPending || _openWound || !YouDrive(st)) return;
                int dir = OpeningMath.Unknown;
                try
                {
                    dir = OpeningSwingRead(st.Agent);
                }
                catch (Exception)
                {
                    dir = OpeningMath.Unknown;
                }
                if (!OpeningMath.IsAttack(dir)) dir = OpeningMath.Unknown;
                _openWound = true;
                _openWindUpAt = now;
                _openWindUpDir = dir;
                double since = now - _openBlockAt;
                _openVerdict = OpeningMath.Verdict(in r, in _openPlan, since, dir);
                LastOpeningVerdict = _openVerdict;
                _open.NoteVerdict(_openVerdict, _openPlan.K, since);
                OpeningLine("openings (shadow): your next swing - " + OpeningMath.AttackName(dir) + " started " + F2(since) + " s after the block (window "
                            + F2(r.ForceDelay) + "-" + F2(r.ForceDelay + _openPlan.Window) + " s, opening: " + OpeningMath.AttackName(_openPlan.Opening) + ", link "
                            + _openPlan.K.ToString(CultureInfo.InvariantCulture) + "): " + OpeningMath.VerdictName(_openVerdict));
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        /// <summary>Your melee release ended without a hit (step 13's phases): the swing after an opening met nothing; a miss in a chain counted
        /// (and with <c>OpeningMissKeepsChain</c> off the chain starts over).</summary>
        internal void OpeningMiss(double now)
        {
            if (!_openPending && !_openChain.Active) return;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.ShadowOn) return;
                if (_openPending && _openWound) CloseOpening(now, Core.OpeningContact.None);
                if (!_openChain.Active) return;
                _open.Misses++;
                if (!r.MissKeepsChain) ResetOpeningChain(OpeningReset.Miss);
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        /// <summary>
        /// OnMeleeHit (before the impact decision - its block makes the NEXT opening): your blow's contact closes the waiting opening (what it met,
        /// the time from its wind-up, the wind-up's direction against the contact's); the chain's resets - your hit landed on him, your blow on
        /// another man, his hit on you. Kicks / bashes, missiles and horse charges are not attacks here. Its own try.
        /// </summary>
        internal void OpeningContactSeen(Agent attacker, Agent victim, in AttackCollisionData cd)
        {
            if (!_openPending && !_openChain.Active) return;
            if (cd.IsMissile || cd.IsHorseCharge || cd.IsAlternativeAttack || attacker == null || victim == null) return;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.ShadowOn) return;
                var a = Get(attacker);
                var v = Get(victim);
                if (a == null || v == null) return;
                double now = SpikeNow;
                bool landed = !cd.AttackBlockedWithShield && cd.CollisionResult == CombatCollisionResult.StrikeAgent;
                if (IsPlayer(v) && YouDrive(v))
                {
                    if (landed && _openChain.Active && a.AgentIndex == _openChain.Target)
                    {
                        CloseOpening(now, Core.OpeningContact.None);
                        ResetOpeningChain(OpeningReset.HeHitYou);
                    }
                    return;
                }
                if (!IsPlayer(a) || !YouDrive(a)) return;
                bool onTarget = _openChain.Active ? v.AgentIndex == _openChain.Target : v.AgentIndex == _openTarget;
                if (_openPending && _openWound)
                {
                    var c = v.AgentIndex != _openTarget ? Core.OpeningContact.OtherTarget
                        : landed ? Core.OpeningContact.Landed
                        : cd.AttackBlockedWithShield && !cd.CorrectSideShieldBlock ? Core.OpeningContact.WrongBlock
                        : Core.OpeningContact.BlockedAgain;
                    _open.NoteDirection(_openWindUpDir, TimedRecoilMath.DirectionOf((int)cd.AttackDirection));
                    CloseOpening(now, c);
                }
                else if (_openPending) // its wind-up unseen (Athletics' phases off, or a release no ready was seen for): the direction unread
                {
                    _openPending = false;
                    LastOpeningVerdict = OpeningVerdict.UnknownDirection;
                    _open.NoteVerdict(OpeningVerdict.UnknownDirection, _openPlan.K, double.NaN);
                }
                if (_openChain.Active && !onTarget) ResetOpeningChain(OpeningReset.TargetSwitch);
                else if (_openChain.Active && landed) ResetOpeningChain(OpeningReset.HitLanded);
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        /// <summary>The waiting opening ends: its verdict (no swing seen = <see cref="OpeningVerdict.NoSwing"/>), the contact counted for a judged swing.</summary>
        private void CloseOpening(double now, OpeningContact contact)
        {
            if (!_openPending) return;
            _openPending = false;
            if (!_openWound)
            {
                LastOpeningVerdict = OpeningVerdict.NoSwing;
                _open.NoteVerdict(OpeningVerdict.NoSwing, _openPlan.K, double.NaN);
                return;
            }
            _open.NoteContact(contact, contact == Core.OpeningContact.None ? double.NaN : now - _openWindUpAt, _openVerdict == OpeningVerdict.Opened);
            if (contact != Core.OpeningContact.None && _openLines < OpeningFullLines)
                OpeningLine("openings (shadow): that swing met " + OpeningMath.ContactName(contact) + " " + F2(now - _openWindUpAt) + " s after its start");
        }

        private void ResetOpeningChain(OpeningReset why)
        {
            if (!_openChain.Active) return;
            _open.NoteReset(why);
            _openChain.Reset();
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>From <see cref="TickImpact"/> (own try there): the switches (the master switch first) - off = everything dropped; a swing that
        /// never came (the chain's gap) and a contact that never came (the hold's time + 0.5 s) closed; the chain's man gone.</summary>
        private void TickOpenings(double now)
        {
            if (!_openPending && !_openChain.Active) return;
            var r = OpeningRules.From(TraxSettings.Shared);
            if (!r.ShadowOn)
            {
                CloseOpening(now, Core.OpeningContact.None);
                ResetOpeningChain(OpeningReset.Off);
                return;
            }
            if (_openPending && !_openWound && now - _openBlockAt > r.ChainReset + 1e-9)
            {
                CloseOpening(now, Core.OpeningContact.None);
                ResetOpeningChain(OpeningReset.Gap);
            }
            else if (_openPending && _openWound && now > OpeningMath.HoldEnds(in r, _openWindUpAt) + 0.5) CloseOpening(now, Core.OpeningContact.None);
            if (_openChain.Active)
            {
                var t = GetByIndex(_openChain.Target);
                if (t == null || t.Removed)
                {
                    CloseOpening(now, Core.OpeningContact.None);
                    ResetOpeningChain(OpeningReset.Off);
                }
                else if (!_openPending && _openChain.Expired(now, in r)) ResetOpeningChain(OpeningReset.Gap);
            }
        }

        /// <summary>The mission's end (before the summary): a waiting opening closed.</summary>
        private void CloseOpenings(double now)
        {
            CloseOpening(now, Core.OpeningContact.None);
            _openChain.Reset();
        }

        /// <summary>The summary's turn (after step 105's line).</summary>
        private void WriteOpeningSummary()
        {
            CloseOpenings(SpikeNow);
            TraxLog.Info("summary", _open.SummaryLine(OpeningRules.From(TraxSettings.Shared)));
        }

        private void OpeningLine(string text)
        {
            if (_openLines < OpeningFullLines)
            {
                _openLines++;
                TraxLog.Info("impact", "YOU: " + text + (_openLines == OpeningFullLines ? " (the last at Info this battle - the rest verbose)" : string.Empty));
            }
            else if (TraxLog.VerboseWants("openings")) TraxLog.Verbose("impact", "YOU: " + text, "openings");
        }

        private void OpeningFailed(Exception e)
        {
            _open.Errors++;
            if (_open.Errors == 1) Failed("impact.openings", e);
            _openPending = false;
            _openChain.Reset();
        }
    }
}
