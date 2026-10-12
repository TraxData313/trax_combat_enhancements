using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 32 - THE SWING STACK (Anton, 2026-10-12: "chain swings stack"; DESIGN §2 "The swing stack", AI_NOTES "Step 32").
    /// Every fighter stores swings - max(SwingStackMinimum, round(current points / SwingStackPointsPerSwing)), half away from
    /// zero, spawn full (Core <see cref="SwingStack"/>). An attack (a melee release, a loose, a throw) spends one at its release;
    /// a kick or shield bash too (KicksUseSwings). While one is stored nobody is held: the pause machinery of steps 13 / 16 / 21
    /// runs ONLY at 0 - and then exactly until the next stored swing comes back. The refill clock: one swing per the pause that
    /// attack would have left (D × (1/m − 1) at his CURRENT m, + the AI's battle-pace share) of time NOT attacking - so the
    /// average rate stays step 13's and a rested man can burst. An AI melee man's own gap after a swing from the stack
    /// (AiMeleeGapSeconds) does not count - his natural rhythm already had it on top of the pause (step 21's measurement), so
    /// without that the stack would undo the battle pace.
    ///
    /// WHERE: the clock advances at every channel-1 action change (before the phases move), every regen pass (0.1 s, everyone),
    /// every tick for a man waiting at 0 (the AI timer's TickPace, your gate's frame). Spends in ObserveAction (releases) and the
    /// kick / bash paths. Clamped to the cap after every charge (points dropped). Filled at a fresh start (ResetPhases: the
    /// switch, the hideout boss fight).
    ///
    /// LOGS: [rate] mission start / settings changes; YOU lines (bucket athletics-stack): the first burst, the stack empty (the
    /// first in full), a kick refused at 0 (rate-limited); the [summary] "swing stack" lines (<see cref="SwingStackStats"/>).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly SwingStackStats _stackStats = new SwingStackStats();
        private SwingStackRules _seenStack;
        private bool _stackSwitched;
        private bool _playerStackWait;
        private bool _firstBurstLogged;
        private bool _firstEmptyLogged;
        private bool _firstKickRefusedLogged;

        /// <summary>This mission's swing-stack numbers (the offline smoke reads them).</summary>
        internal SwingStackStats StackStats => _stackStats;

        /// <summary>Your countdown is a swing-stack wait (it ends when your next stored swing is back).</summary>
        internal bool PlayerStackWait => _playerStackWait;

        private void StartSwingStack()
        {
            _seenStack = SwingStackRules.From(TraxSettings.Shared);
            TraxLog.Info("rate", "swing stack (step 32) at mission start: " + _seenStack.Describe()
                + " - read live; the [summary] \"swing stack\" lines count bursts, waits and kicks");
        }

        /// <summary>He is attacking now (the clock stands still): a ready, a release, the reload after a loose.</summary>
        private static bool StackAttacking(TrackedAgent st) =>
            st.PhaseKind == (int)AttackPhase.WindUp || st.PhaseKind == (int)AttackPhase.Release || st.AwaitReloadEnd;

        /// <summary>Seconds of not attacking per stored swing: the pause his last attack would have left today at his CURRENT m -
        /// you: D × (1/m − 1); the AI: + his class's battle-pace share (<see cref="BattlePaceMath.Plan"/>).</summary>
        private double StackRefillSeconds(TrackedAgent st, in AthleticsRules r, in BattlePaceRules bp)
        {
            double d = st.StackD > 0 ? st.StackD : SwingStackMath.FallbackSwingSeconds;
            float m = AthleticsMath.AttackSpeedMultiplier(in r, st);
            if (IsPlayer(st)) return AttackTimerMath.Pause(d, m);
            return BattlePaceMath.Plan(in bp, st.AttackClass, d, m).Total;
        }

        /// <summary>The clock up to <paramref name="now"/> (managed only - safe in a hit callback). Returns his cap now.</summary>
        private int AdvanceStack(TrackedAgent st, double now, in AthleticsRules r, in SwingStackRules sr, in BattlePaceRules bp)
        {
            int cap = SwingStackMath.Cap(in sr, in r, st);
            int gained = st.Swings.Advance(now, StackRefillSeconds(st, in r, in bp), cap, StackAttacking(st));
            if (gained > 0 && st.Swings.Stored < SwingStackMath.Full) _stackStats.Gained += gained;
            return cap;
        }

        private int AdvanceStack(TrackedAgent st, double now, in AthleticsRules r, in SwingStackRules sr)
        {
            var bp = BattlePaceRules.From(TraxSettings.Shared);
            return AdvanceStack(st, now, in r, in sr, in bp);
        }

        /// <summary>A channel-1 action change, BEFORE the phases move: the clock up to now in the state he was in.</summary>
        private void StackOnAction(TrackedAgent st, double now, in AthleticsRules r)
        {
            var sr = SwingStackRules.From(TraxSettings.Shared);
            if (!sr.Enabled) return;
            try
            {
                AdvanceStack(st, now, in r, in sr);
            }
            catch (Exception e)
            {
                Failed("stack.advance", e);
            }
        }

        /// <summary>
        /// One attack (<paramref name="kick"/> false - a melee release, a loose, a throw) or kick / shield bash spends a stored
        /// swing. At 0 it is counted (a chained blow, a gate miss - the attack cannot be stopped here). You: the stack's first
        /// burst and its empty moments are logged; an empty stack after a kick starts your wait. The AI: an empty stack after a kick
        /// asks his timer for the wait (an attack's end asks it in AiAttackEnded). Managed only.
        /// </summary>
        private void SpendSwing(TrackedAgent st, double now, in AthleticsRules r, bool kick)
        {
            var sr = SwingStackRules.From(TraxSettings.Shared);
            if (!sr.Enabled || (kick && !sr.KicksUseSwings)) return;
            try
            {
                var bp = BattlePaceRules.From(TraxSettings.Shared);
                int cap = AdvanceStack(st, now, in r, in sr, in bp);
                bool you = IsPlayer(st);
                int had = st.Swings.Count(cap);
                var res = st.Swings.Spend(cap, out int run);
                if (run >= 2) _stackStats.AddRun(you, run);
                if (kick)
                {
                    _stackStats.KicksSpent++;
                    if (you) _stackStats.KicksSpentYou++;
                    if (res == SpendResult.AtEmpty) _stackStats.KicksAtEmpty++;
                }
                else
                {
                    _stackStats.AddSpend(you, res);
                }
                if (!you)
                {
                    if (kick && res != SpendResult.AtEmpty && st.Swings.Count(cap) == 0) AskStackWaitAfterKick(st, now, in r, in bp);
                    return;
                }
                if (st.Swings.Run == 2 && !_firstBurstLogged)
                {
                    _firstBurstLogged = true;
                    TraxLog.Info("athletics", "YOU: first burst this battle at " + Sec(now) + " s - a second " + (kick ? "kick / bash" : "attack") + " straight after the last from your stored swings ("
                        + st.Swings.Count(cap) + "/" + cap + " left; one comes back per " + F2(StackRefillSeconds(st, in r, in bp)) + " s you are not attacking)");
                }
                if (res == SpendResult.AtEmpty)
                {
                    TraxLog.Limited("athletics", "YOU: " + (kick ? "a kick / bash" : "an attack") + " at " + Sec(now) + " s started with NO stored swing (a chained blow or a gate miss) - the refill toward the next starts again", "athletics-stack");
                    return;
                }
                if (st.Swings.Count(cap) > 0) return;
                double wait = st.Swings.SecondsToNext(StackRefillSeconds(st, in r, in bp), cap);
                if (!_firstEmptyLogged)
                {
                    _firstEmptyLogged = true;
                    TraxLog.Info("athletics", "YOU: swing stack empty at " + Sec(now) + " s - you spent your last stored swing (" + had + " → 0 of " + cap + " on "
                        + F0(st.Fraction * AthleticsMath.PoolPoints(in r, st)) + " points) on " + (kick ? "a kick / bash" : "an attack") + "; the next comes back in " + F2(wait)
                        + " s of not attacking - until then your attack button" + (sr.KicksUseSwings ? " and kick key do" : " does") + " nothing (held, it attacks the moment one is back)");
                }
                else
                {
                    TraxLog.Limited("athletics", "YOU: swing stack empty at " + Sec(now) + " s (cap " + cap + "), the next in " + F2(wait) + " s", "athletics-stack");
                }
                if (kick) PlayerStackHoldIfEmpty(st, now, in r);
            }
            catch (Exception e)
            {
                Failed("stack.spend", e);
            }
        }

        /// <summary>After every charge (a blow, a kick, a block): his points dropped - the stored count clamped to the new cap.</summary>
        private void StackClamp(TrackedAgent st, in AthleticsRules r)
        {
            var sr = SwingStackRules.From(TraxSettings.Shared);
            if (!sr.Enabled || st.Swings.Stored >= SwingStackMath.Full) return;
            if (st.Swings.Clamp(SwingStackMath.Cap(in sr, in r, st))) _stackStats.Clamps++;
        }

        /// <summary>The regen pass (every 0.1 s, everyone): the clock and the fill sample by f band.</summary>
        private void StackRegenSample(TrackedAgent st, double now, double step, in AthleticsRules r, in SwingStackRules sr, in BattlePaceRules bp)
        {
            int cap = AdvanceStack(st, now, in r, in sr, in bp);
            _stackStats.AddFill(AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st)), (double)st.Swings.Count(cap) / cap, step);
        }

        /// <summary>The AI timer's decision with the stack on (AiAttackEnded): a stored swing left = no pause (an AI melee man's own
        /// gap starts - the clock waits it out); none = the timer holds him until the next comes back. Null = asked.</summary>
        private string? AskStackPause(TrackedAgent st, double now, AttackKind kind, double d, bool known, float m, int bin, int next, in PacePlan plan,
            in AthleticsRules r, in SwingStackRules sr, in BattlePaceRules bp)
        {
            int cap = AdvanceStack(st, now, in r, in sr, in bp);
            if (st.Swings.Count(cap) > 0)
            {
                _rateStats.AddNotHeld(PaceNotHeld.FromStack);
                if (kind == AttackKind.Melee && !IsAttackAction(next)) st.Swings.StartGrace(now + bp.AiGapSeconds);
                return "no pause - a stored swing left (the swing stack)";
            }
            double wait = st.Swings.SecondsToNext(StackRefillSeconds(st, in r, in bp), cap);
            return AskPause(st, now, kind, d, known, m, bin, next, in plan, wait);
        }

        /// <summary>An AI man's kick / bash spent his last stored swing: his timer holds him until the next (no attack ended here).</summary>
        private void AskStackWaitAfterKick(TrackedAgent st, double now, in AthleticsRules r, in BattlePaceRules bp)
        {
            var rr = AttackRateRules.From(TraxSettings.Shared);
            if (!rr.PaceOn || _paceClosed) return;
            var sr = SwingStackRules.From(TraxSettings.Shared);
            int cap = SwingStackMath.Cap(in sr, in r, st);
            double d = st.StackD > 0 ? st.StackD : SwingStackMath.FallbackSwingSeconds;
            float m = AthleticsMath.AttackSpeedMultiplier(in r, st);
            var plan = BattlePaceMath.Plan(in bp, st.AttackClass, d, m);
            double wait = st.Swings.SecondsToNext(plan.Total, cap);
            AskPause(st, now, AttackKind.Melee, d, true, m, AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st)), st.PrevAction, in plan, wait);
        }

        /// <summary>A running AI swing-stack wait (TickPace): the clock moved; True = his next stored swing is back (the hold ends).</summary>
        private bool StackWaitOver(TrackedAgent st, double now)
        {
            var r = Rules;
            var sr = SwingStackRules.From(TraxSettings.Shared);
            int cap = AdvanceStack(st, now, in r, in sr);
            if (st.Swings.Count(cap) == 0) return false;
            st.Swings.MarkWaited();
            return true;
        }

        // ------------------------------------------------------------------ you

        /// <summary>
        /// Your stack is empty and no hold runs (a kick or bash spent the last one, or the tick's safety net): your wait starts -
        /// during your own release the hold from its start, else the countdown to your next stored swing.
        /// </summary>
        private void PlayerStackHoldIfEmpty(TrackedAgent st, double now, in AthleticsRules r)
        {
            if (_playerTimer.Holding) return;
            var rr = AttackRateRules.From(TraxSettings.Shared);
            var sr = SwingStackRules.From(TraxSettings.Shared);
            if (!rr.PlayerTimerOn || !sr.Enabled || !YouDrive(st)) return;
            var bp = BattlePaceRules.From(TraxSettings.Shared);
            int cap = AdvanceStack(st, now, in r, in sr, in bp);
            if (st.Swings.Count(cap) > 0) return;
            double wait = st.Swings.SecondsToNext(StackRefillSeconds(st, in r, in bp), cap);
            if (st.PhaseKind == (int)AttackPhase.WindUp) return; // a ready runs: its release asks (bits vanishing mid-ready = the release)
            if (StackAttacking(st))
            {
                if (_playerTimer.AttackStarted(now, st.PhaseAttack, wait)) _playerTimerOwner = st;
                return;
            }
            if (!_playerTimer.StartCountdown(now, wait)) return;
            _playerTimerOwner = st;
            _playerStackWait = true;
            _firedPending = false;
        }

        /// <summary>
        /// Your swing-stack countdown, every gate frame (and tick): the clock moved, the end follows your next stored swing - now
        /// when it is back (the frame ends the countdown: a held button attacks), else when it will be at your current m. The stack
        /// switched off mid-wait = the estimate stands.
        /// </summary>
        private void RetargetPlayerWait(TrackedAgent st, double now)
        {
            if (!_playerStackWait || !_playerTimer.Running) return;
            var sr = SwingStackRules.From(TraxSettings.Shared);
            if (!sr.Enabled) return;
            var r = Rules;
            var bp = BattlePaceRules.From(TraxSettings.Shared);
            int cap = AdvanceStack(st, now, in r, in sr, in bp);
            if (st.Swings.Count(cap) > 0)
            {
                _playerTimer.Retarget(now);
                return;
            }
            _playerTimer.Retarget(now + st.Swings.SecondsToNext(StackRefillSeconds(st, in r, in bp), cap));
        }

        /// <summary>
        /// The kick gate (managed - the smoke drives it): your kick key this frame (<paramref name="kickPressed"/>, on foot) with
        /// no stored swing and KicksUseSwings on = taken out (counted, logged rate-limited). True = clear the kick bit.
        /// </summary>
        internal bool KickGate(TrackedAgent st, double now, bool kickPressed, bool mounted)
        {
            if (!kickPressed || mounted) return false;
            var sr = SwingStackRules.From(TraxSettings.Shared);
            if (!sr.KicksOn || !AttackRateRules.From(TraxSettings.Shared).PlayerTimerOn) return false;
            var r = Rules;
            int cap = AdvanceStack(st, now, in r, in sr);
            if (st.Swings.Count(cap) > 0) return false;
            _stackStats.KicksRefusedYou++;
            if (!_firstKickRefusedLogged)
            {
                _firstKickRefusedLogged = true;
                TraxLog.Info("athletics", "YOU: kick pressed at " + Sec(now) + " s with no stored swing (0/" + cap + ") - refused: the engine never saw it (KicksUseSwings); "
                    + "a kick or shield bash needs a stored swing like an attack - " + (_playerTimer.Running ? F2(_playerTimer.Remaining(now)) + " s until the next" : "the next comes back after your attack"));
            }
            else
            {
                TraxLog.Limited("athletics", "YOU: kick refused at " + Sec(now) + " s - no stored swing", "athletics-stack");
            }
            return true;
        }

        // ------------------------------------------------------------------ settings, the summary

        /// <summary>A settings change: the switch (on = everyone's stack full - a fresh start for it) and the numbers, logged.</summary>
        private void NoteStackSettings(TraxSettings s)
        {
            var sr = SwingStackRules.From(s);
            var was = _seenStack;
            if (sr.Enabled == was.Enabled && sr.PointsPerSwing.Equals(was.PointsPerSwing) && sr.Minimum == was.Minimum && sr.KicksUseSwings == was.KicksUseSwings) return;
            _seenStack = sr;
            if (sr.Enabled != was.Enabled)
            {
                _stackSwitched = true;
                if (sr.Enabled)
                    for (int i = 0; i < _count; i++) _dense[i].Swings.FillUp();
                TraxLog.Info("rate", "swing stack switched " + (sr.Enabled ? "ON" : "OFF") + " mid-mission at " + Sec(SafeNow()) + " s: "
                    + (sr.Enabled ? "everyone's stack starts full; a pause runs only at 0 from the next attack" : "a pause after every attack below the peak line again from the next attack (a wait running now ends at its estimate)"));
                return;
            }
            TraxLog.Info("rate", "swing stack settings now: " + sr.Describe() + " - every cap follows at the next look (a stack above its new cap is cut at the next charge)");
        }

        /// <summary>The summary's swing-stack lines (bursts still running counted first).</summary>
        private void WriteSwingStackSummary()
        {
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                _stackStats.AddRun(IsPlayer(st), st.Swings.CloseRun());
            }
            foreach (var line in _stackStats.SummaryLines(SwingStackRules.From(TraxSettings.Shared), _stackSwitched))
                TraxLog.Info("summary", line);
        }

        /// <summary>He left the field: his running burst is counted.</summary>
        private void StackLeftField(TrackedAgent st) => _stackStats.AddRun(IsPlayer(st), st.Swings.CloseRun());
    }
}
