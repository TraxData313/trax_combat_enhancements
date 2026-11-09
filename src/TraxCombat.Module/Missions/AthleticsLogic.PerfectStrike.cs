using System;
using System.Globalization;
using System.Threading;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 100g - THE PERFECT STRIKE (Toni 2026-11-08: "if I attack in the opposite way (say I blocked his right attack, coming from my left and
    /// then swing from my right to left) after I blocked him and he is staggered I do 2-3x damage against him and I see some notification like
    /// PERFECT STRIKE"; AI_NOTES "Step 100" - "100g - built", DESIGN decision 91 (j)):
    ///   - OPENED from <see cref="TimedRiposteAccepted"/> (the engine took the riposte stagger) when the blocker is YOU and the block was timed
    ///     (score > 0): a window on the staggered man with his blocked attack's direction (read at contact for 100d's recoil -
    ///     <see cref="TrackedAgent.RecoilDirection"/>) and your timing score.
    ///   - OPEN while he reels - his <see cref="ImpactState.ReelingUntil"/> (set by the decision, re-timed by the measured stagger, recoil +
    ///     backward stagger alike) - plus <c>PerfectStrikeGraceSeconds</c>.
    ///   - USED by the damage decorator's last question (<see cref="IPerfectStrike"/>): your first melee swing that lands on his BODY (not
    ///     blocked, not a shield, not a kick / bash, you on foot) from the opposite side (<see cref="PerfectStrikeMath.Verdict"/>) lands
    ///     x <see cref="PerfectStrikeMath.Multiplier(double, in PerfectStrikeRules)"/>; one per stagger. A WRONG-SIDE hit is counted and the
    ///     window STAYS open (a wrong guess is punished by the lost tempo, not by losing the chance).
    ///   - SHOWN from the next tick: a gold "PERFECT STRIKE x2.8" in the message feed and (behind <c>PerfectStrikeCentreMessage</c>) in the
    ///     middle of the screen; a YOU line in the log.
    /// Master switch first (through <see cref="PerfectStrikeRules.Enabled"/>): off = every window closed at the next tick, no multiplier. Main
    /// thread; no allocation per tick (the text only when a strike lands).
    /// </summary>
    public sealed partial class AthleticsLogic : IPerfectStrike
    {
        private readonly PerfectStrikeBook _strikeBook = new PerfectStrikeBook();
        private readonly PerfectStrikeStats _strike = new PerfectStrikeStats();
        private bool _strikeDisabled, _strikeFirstOpen, _strikeFirstWrong, _strikeFirstBlockedUnknown, _strikeFirstRider, _strikeFirstHorse;
        private string? _strikeShow;
        private bool _strikeShowCentre;

        internal PerfectStrikeStats PerfectStrikeStats => _strike;

        internal int PerfectStrikeWindowsOpen => _strikeBook.Count;

        /// <summary>The last perfect strike's on-screen text (the smoke reads it).</summary>
        internal string? LastPerfectStrikeText { get; private set; }

        /// <summary>From <see cref="TimedRiposteAccepted"/>: YOUR timed block's riposte stagger was taken - a window opens on him.</summary>
        private void MaybeOpenPerfectStrike(TrackedAgent attacker, TrackedAgent? blocker, double score, double now)
        {
            if (_strikeDisabled || blocker == null || !(score > 0)) return;
            try
            {
                var r = PerfectStrikeRules.From(TraxSettings.Shared);
                if (!r.Enabled || !IsPlayer(blocker) || !YouDrive(blocker) || attacker.Removed) return;
                int dir = !double.IsNaN(attacker.RecoilDirectionAt) && now - attacker.RecoilDirectionAt <= TimedRecoilMath.DirectionMaxAge + 1e-9 ? attacker.RecoilDirection : -1;
                _strikeBook.Open(attacker.AgentIndex, dir, score, now, false, out bool dropped, out bool droppedRider);
                _strike.Opened++;
                if (dropped)
                {
                    StrikeExpired(droppedRider);
                    _strike.Replaced++;
                    if (droppedRider) _strike.RiderReplaced++;
                }
                if (!_strikeFirstOpen)
                {
                    _strikeFirstOpen = true;
                    TraxLog.Info("impact", "perfect strike: first window at " + Sec(now) + " s - " + Name(attacker) + " reels after your timed block (timing " + F2(score)
                        + ") of his " + PerfectStrikeMath.Name(dir) + StrikeWanted(dir) + "; open until his stagger ends + " + F2(r.GraceSeconds) + " s, x"
                        + PerfectStrikeMath.Multiplier(score, in r).ToString("0.00", CultureInfo.InvariantCulture) + " - " + r.Describe());
                }
            }
            catch (Exception e)
            {
                StrikeFailed("impact.perfect-open", e);
            }
        }

        /// <summary>
        /// Step 104d - from <see cref="TimedRiderReelAccepted"/> (the tick took a rider's REEL IN THE SADDLE): YOUR timed block (score > 0, you
        /// riding or on foot) opens a window on the reeling RIDER - <see cref="PerfectStrikeRules.MountedOn"/> (the master switch first ...
        /// <c>TimedBlocksOnHorseback</c>, <c>PerfectStrikeOnHorseback</c>). Open while he reels (his record's <see cref="ImpactState.ReelingUntil"/>,
        /// the reel's own end) + the grace, like on foot. Knocked off his horse (step 104c) = no window (he is down - step 54's punish covers him).
        /// Never on you (the AI's block of your blow opens nothing). Its own try.
        /// </summary>
        private void MaybeOpenRiderStrike(in RiderReelRec p, double now)
        {
            if (_strikeDisabled || !(p.Score > 0) || p.You) return;
            try
            {
                var blocker = p.Blocker;
                var a = p.St;
                if (blocker == null || blocker.Removed || a.Removed || !IsPlayer(blocker) || !YouDrive(blocker)) return;
                var r = PerfectStrikeRules.From(TraxSettings.Shared);
                if (!r.MountedOn) return;
                if (p.Fell)
                {
                    _strike.RiderKnockedOffNoWindow++;
                    return;
                }
                int dir = !double.IsNaN(a.RecoilDirectionAt) && now - a.RecoilDirectionAt <= TimedRecoilMath.DirectionMaxAge + 1e-9 ? a.RecoilDirection : -1;
                _strikeBook.Open(a.AgentIndex, dir, p.Score, now, true, out bool dropped, out bool droppedRider);
                _strike.Opened++;
                _strike.RiderOpened++;
                if (dropped)
                {
                    StrikeExpired(droppedRider);
                    _strike.Replaced++;
                    if (droppedRider) _strike.RiderReplaced++;
                }
                if (!_strikeFirstRider)
                {
                    _strikeFirstRider = true;
                    TraxLog.Info("impact", "perfect strike: first window on a RIDER at " + Sec(now) + " s - " + Name(a) + " reels in the saddle after your timed block (timing " + F2(p.Score)
                        + ") of his " + PerfectStrikeMath.Name(dir) + (r.HorsebackAnySide ? " - ANY landed hit on his body counts (never his horse)" : StrikeWanted(dir))
                        + "; open until his reel ends (" + F2(p.Until - now) + " s) + " + F2(r.GraceSeconds) + " s, x"
                        + PerfectStrikeMath.Multiplier(p.Score, in r).ToString("0.00", CultureInfo.InvariantCulture) + " - " + r.Describe());
                }
            }
            catch (Exception e)
            {
                StrikeFailed("impact.perfect-open-rider", e);
            }
        }

        /// <summary>A window gone unused (expired, replaced, still open at the end) - counted, a rider's apart.</summary>
        private void StrikeExpired(bool rider)
        {
            _strike.ExpiredUnused++;
            if (rider) _strike.RiderExpiredUnused++;
        }

        /// <summary>Step 104d - the decorator's question for a hit on a HORSE (main thread): YOUR hit on the horse of a rider in an open window
        /// is counted and ignored - the window stays open (only his body counts).</summary>
        void IPerfectStrike.HorseHit(int attackerIndex, int riderIndex)
        {
            if (_strikeDisabled || _strikeBook.Count == 0 || riderIndex < 0) return;
            try
            {
                int i = _strikeBook.IndexOf(riderIndex);
                if (i < 0 || !_strikeBook[i].Rider) return;
                var you = GetByIndex(attackerIndex);
                if (you == null || !IsPlayer(you) || !YouDrive(you)) return;
                if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                {
                    _strike.OffMainThread++;
                    return;
                }
                _strike.HorseHits++;
                if (!_strikeFirstHorse)
                {
                    _strikeFirstHorse = true;
                    var v = GetByIndex(riderIndex);
                    TraxLog.Info("impact", "perfect strike: first hit on a reeling rider's HORSE at " + Sec(SpikeNow) + " s - " + (v != null ? Name(v) : "(a rider)")
                        + "'s horse: normal damage, the window stays open (only his body counts)");
                }
            }
            catch (Exception e)
            {
                StrikeFailed("impact.perfect-horse", e);
            }
        }

        /// <summary>What answers a blocked direction, in words (the first-time line).</summary>
        private static string StrikeWanted(int blocked) => blocked switch
        {
            PerfectStrikeMath.Right => " (it came at your left - answer with YOUR right swing, from your right)",
            PerfectStrikeMath.Left => " (it came at your right - answer with YOUR left swing, from your left)",
            PerfectStrikeMath.Up => " (it came from above - answer from BELOW: YOUR thrust / stab)",
            PerfectStrikeMath.Down => " (it came from below - answer from ABOVE: YOUR overhead)",
            _ => " (its direction unknown - nothing counts)",
        };

        /// <summary>THE DECORATOR'S LAST QUESTION (main thread): the damage this hit lands.</summary>
        float IPerfectStrike.PerfectStrike(Agent attacker, Agent victim, bool attackerMounted, in AttackCollisionData cd, float damage)
        {
            if (_strikeDisabled || _strikeBook.Count == 0) return damage;
            try
            {
                int i = _strikeBook.IndexOf(victim.Index);
                if (i < 0) return damage;
                var you = Get(attacker);
                if (you == null || !IsPlayer(you) || !YouDrive(you)) return damage;
                if (_mainThreadId != -1 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                {
                    _strike.OffMainThread++;
                    return damage;
                }
                var r = PerfectStrikeRules.From(TraxSettings.Shared);
                if (!r.Enabled) return damage; // the tick closes the windows
                double now = SpikeNow;
                var v = GetByIndex(victim.Index);
                ref var w = ref _strikeBook[i];
                if (v == null || v.Removed || !PerfectStrikeMath.Open(now, v.Impact.ReelingUntil, r.GraceSeconds))
                {
                    StrikeExpired(w.Rider);
                    _strikeBook.RemoveAt(i);
                    return damage;
                }
                if (w.Rider && !r.MountedOn) return damage; // step 104d's switch off: the tick closes it
                if (attackerMounted && !r.MountedOn) // step 104d: from horseback only with its switch
                {
                    _strike.Mounted++;
                    return damage;
                }
                if (cd.IsAlternativeAttack || cd.AttackBlockedWithShield || cd.CollisionResult != CombatCollisionResult.StrikeAgent || !(damage > 0))
                {
                    _strike.NotOnBody++;
                    return damage;
                }
                int strike = (int)cd.AttackDirection;
                var verdict = PerfectStrikeMath.Verdict(w.Rider, r.HorsebackAnySide, w.Blocked, strike); // step 104d: a reeling rider - any side
                if (verdict == StrikeVerdict.Unknown)
                {
                    _strike.UnknownDirection++;
                    return damage;
                }
                if (verdict == StrikeVerdict.BlockedUnknown) // step 103: his direction unknown - nothing counts, said once
                {
                    _strike.BlockedUnknown++;
                    if (!_strikeFirstBlockedUnknown)
                    {
                        _strikeFirstBlockedUnknown = true;
                        TraxLog.Info("impact", "perfect strike: first hit after a blocked attack of UNKNOWN direction at " + Sec(now) + " s - your " + PerfectStrikeMath.Name(strike)
                            + " on " + Name(v) + " - nothing counts (strict opposites need his direction), normal damage");
                    }
                    return damage;
                }
                if (verdict == StrikeVerdict.WrongSide)
                {
                    _strike.NoteWrongSide(strike, w.Blocked);
                    w.WrongSide++;
                    if (w.Rider) _strike.RiderWrongSide++;
                    else if (attackerMounted) _strike.FootWrongSideFromHorseback++;
                    // step 103: every mismatch of yours is logged (yours only, inside a ~2 s window - a handful a battle), so a playtest shows what you hit
                    {
                        string head = _strikeFirstWrong ? "perfect strike: WRONG-SIDE hit at " : "perfect strike: first WRONG-SIDE hit at ";
                        _strikeFirstWrong = true;
                        TraxLog.Info("impact", head + Sec(now) + " s - your " + PerfectStrikeMath.Name(strike) + " on " + Name(v) + " after his blocked "
                            + PerfectStrikeMath.Name(w.Blocked) + StrikeWanted(w.Blocked) + " - normal damage, the window stays open");
                    }
                    return damage;
                }
                double k = PerfectStrikeMath.Multiplier(w.Score, in r);
                float after = PerfectStrikeMath.Apply(damage, k);
                _strike.NoteLanded(strike, w.Blocked, w.Score, k, damage, after);
                if (w.Rider) _strike.NoteRiderLanded(w.Score, k, damage, after, attackerMounted);
                else if (attackerMounted) _strike.FootLandedFromHorseback++;
                var c = CultureInfo.InvariantCulture;
                TraxLog.Info("impact", "YOU: PERFECT STRIKE on " + Name(v) + " at " + Sec(now) + " s - your " + PerfectStrikeMath.Name(strike) + (attackerMounted ? " from horseback" : string.Empty)
                    + (w.Rider ? " on him reeling in the saddle" + (r.HorsebackAnySide ? " (any side)" : string.Empty) : string.Empty) + " after his blocked "
                    + PerfectStrikeMath.Name(w.Blocked) + ", " + F2(now - w.OpenedAt) + " s into his stagger, timing " + F2(w.Score) + " → x" + k.ToString("0.00", c) + ": "
                    + damage.ToString("0.#", c) + " → " + after.ToString("0.#", c) + " damage" + (w.WrongSide > 0 ? " (after " + w.WrongSide.ToString(c) + " wrong-side hit(s))" : string.Empty));
                _strikeBook.RemoveAt(i);
                _strikeShow = PerfectStrikeMath.Text(k);
                _strikeShowCentre = r.CentreMessage;
                LastPerfectStrikeText = _strikeShow;
                return after;
            }
            catch (Exception e)
            {
                StrikeFailed("impact.perfect-strike", e);
                return damage;
            }
        }

        /// <summary>Every tick (from <see cref="TickImpact"/>): the line of a strike that landed, then the windows closed when he stopped reeling or
        /// the rules went off.</summary>
        internal void TickPerfectStrike(double now)
        {
            if (_strikeShow != null)
            {
                string text = _strikeShow;
                _strikeShow = null;
                MessageSink.Show(text, PerfectStrikeMath.GoldHex);
                if (_strikeShowCentre) MessageSink.Centre(text);
            }
            if (_strikeBook.Count == 0) return;
            var r = PerfectStrikeRules.From(TraxSettings.Shared);
            for (int i = _strikeBook.Count - 1; i >= 0; i--)
            {
                bool rider = _strikeBook[i].Rider;
                if (!r.Enabled || _strikeDisabled || (rider && !r.MountedOn)) // step 104d: a rider's window also closes with its switch
                {
                    _strikeBook.RemoveAt(i);
                    _strike.ClosedBySwitch++;
                    if (rider) _strike.RiderClosedBySwitch++;
                    continue;
                }
                var v = GetByIndex(_strikeBook[i].Victim);
                if (v == null || v.Removed || !PerfectStrikeMath.Open(now, v.Impact.ReelingUntil, r.GraceSeconds))
                {
                    _strikeBook.RemoveAt(i);
                    StrikeExpired(rider);
                }
            }
        }

        private void StrikeFailed(string site, Exception e)
        {
            _strikeDisabled = true;
            _strike.Errors++;
            Failed(site, e);
            TraxLog.Info("impact", "the perfect strike is OFF for this battle after an error (" + site + ")");
        }

        /// <summary>The summary's turn (after the timed animations' lines): a window still open at the end counts as expired unused.</summary>
        private void WritePerfectStrikeSummary()
        {
            for (int i = 0; i < _strikeBook.Count; i++) StrikeExpired(_strikeBook[i].Rider);
            _strikeBook.Clear();
            var r = PerfectStrikeRules.From(TraxSettings.Shared);
            TraxLog.Info("summary", _strike.SummaryLine(in r));
            TraxLog.Info("summary", _strike.MountedSummaryLine(in r)); // step 104d
        }
    }
}
