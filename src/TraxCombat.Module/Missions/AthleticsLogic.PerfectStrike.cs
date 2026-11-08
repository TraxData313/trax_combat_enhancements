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
        private bool _strikeDisabled, _strikeFirstOpen, _strikeFirstWrong;
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
                _strikeBook.Open(attacker.AgentIndex, dir, score, now, out bool dropped);
                _strike.Opened++;
                if (dropped)
                {
                    _strike.ExpiredUnused++;
                    _strike.Replaced++;
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

        /// <summary>What answers a blocked direction, in words (the first-time line).</summary>
        private static string StrikeWanted(int blocked) => blocked switch
        {
            PerfectStrikeMath.Right => " (it came at your left - answer with YOUR right swing, from your right)",
            PerfectStrikeMath.Left => " (it came at your right - answer with YOUR left swing, from your left)",
            PerfectStrikeMath.Up => " (answer with either side swing)",
            _ => " (answer with any swing but a thrust)",
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
                if (v == null || v.Removed || !PerfectStrikeMath.Open(now, v.Impact.ReelingUntil, r.GraceSeconds))
                {
                    _strikeBook.RemoveAt(i);
                    _strike.ExpiredUnused++;
                    return damage;
                }
                if (attackerMounted)
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
                ref var w = ref _strikeBook[i];
                var verdict = PerfectStrikeMath.Verdict(w.Blocked, strike);
                if (verdict == StrikeVerdict.Unknown)
                {
                    _strike.UnknownDirection++;
                    return damage;
                }
                if (verdict == StrikeVerdict.WrongSide)
                {
                    _strike.NoteWrongSide(strike);
                    w.WrongSide++;
                    if (!_strikeFirstWrong)
                    {
                        _strikeFirstWrong = true;
                        TraxLog.Info("impact", "perfect strike: first WRONG-SIDE hit at " + Sec(now) + " s - your " + PerfectStrikeMath.Name(strike) + " on " + Name(v) + " after his blocked "
                            + PerfectStrikeMath.Name(w.Blocked) + StrikeWanted(w.Blocked) + " - normal damage, the window stays open");
                    }
                    return damage;
                }
                double k = PerfectStrikeMath.Multiplier(w.Score, in r);
                float after = PerfectStrikeMath.Apply(damage, k);
                _strike.NoteLanded(strike, w.Blocked, w.Score, k, damage, after);
                var c = CultureInfo.InvariantCulture;
                TraxLog.Info("impact", "YOU: PERFECT STRIKE on " + Name(v) + " at " + Sec(now) + " s - your " + PerfectStrikeMath.Name(strike) + " after his blocked "
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
                if (!r.Enabled || _strikeDisabled)
                {
                    _strikeBook.RemoveAt(i);
                    _strike.ClosedBySwitch++;
                    continue;
                }
                var v = GetByIndex(_strikeBook[i].Victim);
                if (v == null || v.Removed || !PerfectStrikeMath.Open(now, v.Impact.ReelingUntil, r.GraceSeconds))
                {
                    _strikeBook.RemoveAt(i);
                    _strike.ExpiredUnused++;
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
            _strike.ExpiredUnused += _strikeBook.Count;
            _strikeBook.Clear();
            TraxLog.Info("summary", _strike.SummaryLine(PerfectStrikeRules.From(TraxSettings.Shared)));
        }
    }
}
