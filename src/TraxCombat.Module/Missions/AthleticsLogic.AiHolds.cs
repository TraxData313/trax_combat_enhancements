using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 16 - the AI holds through the AI's own input (AI_NOTES "Step 16"): the bookkeeping the AI timer
    /// (AthleticsLogic.AttackRate.cs) and the step back (AthleticsLogic.StepBack.cs) share - the per-man input state and
    /// its component (<see cref="AiInputHook"/>), the guard by state (THE fix target), the hook's counts, the component's
    /// swallowed errors (logged here, never inside the callback), the summary's "AI holds" lines and the technique in the
    /// summary header.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>A hold or backpedal that lasted this long without one call from the engine counts as "never called"
        /// (the hook is dead). Log plumbing.</summary>
        private const double NoCallGraceSeconds = 0.3;

        private readonly AiHoldStats _holdStats = new AiHoldStats();
        private readonly List<TrackedAgent> _inputMen = new List<TrackedAgent>(64);

        /// <summary>This mission's AI-hold numbers (the offline smoke reads them).</summary>
        internal AiHoldStats HoldStats => _holdStats;

        /// <summary>His input state, created once (and listed for the summary) - before any body hooks him.</summary>
        private void EnsureInput(TrackedAgent st)
        {
            if (st.Input != null) return;
            st.Input = new AiInputState();
            _inputMen.Add(st);
        }

        /// <summary>What hooking him did - the first hook of a man is counted (callback already on = another mod's).</summary>
        private void NoteHooked(TrackedAgent st, AiInputHook.HookResult r)
        {
            if (r == AiInputHook.HookResult.FirstHookCallbackWasOn)
            {
                _holdStats.MenHooked++;
                _holdStats.CallbackAlreadyOn++;
            }
            else if (r == AiInputHook.HookResult.FirstHookTurnedOn)
            {
                _holdStats.MenHooked++;
            }
        }

        /// <summary>An exception the component swallowed (it must never throw into the engine) - logged now, from the tick.</summary>
        private void DrainInputError(TrackedAgent st)
        {
            var s = st.Input;
            if (s?.PendingError == null) return;
            var e = s.PendingError;
            s.PendingError = null;
            Failed("hold.input-hook", e);
        }

        /// <summary>A melee collision on an AI fighter on foot: held by the AI timer, stepping back, tired - blocked or
        /// landed (the summary's GUARD line - THE fix target of step 16).</summary>
        private void HoldHitTaken(Agent? victim, bool isCanceled, in AttackCollisionData cd)
        {
            if (victim == null || isCanceled || cd.IsHorseCharge) return;
            var vst = Get(victim);
            if (vst == null || victim.MountAgent != null || victim.IsMainAgent) return;
            var r = Rules;
            if (!r.Enabled) return;
            var result = cd.CollisionResult;
            bool blocked = cd.AttackBlockedWithShield || result == CombatCollisionResult.Blocked || result == CombatCollisionResult.Parried
                           || result == CombatCollisionResult.ChamberBlocked;
            var ps = vst.Pace;
            bool held = ps != null && ps.Active;
            var sb = vst.StepBack;
            bool stepping = sb != null && sb.Active;
            bool tired = AthleticsMath.PeakShare(in r, vst) < 1.0 - AthleticsMath.Epsilon;
            _holdStats.AddHit(held, stepping, tired, blocked);
            if (held)
            {
                ps!.HitsTaken++;
                if (blocked) ps.HitsBlocked++;
            }
        }

        /// <summary>The summary's "AI holds" lines: the hook's per-man counts summed first (own try in the caller).</summary>
        private void WriteHoldSummary()
        {
            double now = SafeNow();
            foreach (var st in _inputMen)
            {
                var s = st.Input;
                if (s == null) continue;
                _holdStats.CallsWhileActive += s.Calls;
                _holdStats.ActiveManSeconds += s.ActiveSeconds + (s.ActiveSince >= 0 && now > s.ActiveSince ? now - s.ActiveSince : 0);
                _holdStats.AttackCleared += s.AttackCleared;
                _holdStats.GuardRaised += s.GuardRaised;
                _holdStats.OwnGuardKept += s.OwnGuardKept;
                _holdStats.ReadyCancelled += s.ReadyCancelled;
                _holdStats.BackpedalFrames += s.BackpedalFrames;
                _holdStats.NotAiCalls += s.NotAi;
                _holdStats.HookErrors += s.Errors;
                _holdStats.CallbackTurnedOn += s.TurnedOn;
                _holdStats.CallbackTurnedOff += s.TurnedOff;
                DrainInputError(st);
            }
            foreach (var line in _holdStats.SummaryLines(AttackRateRules.From(TraxSettings.Shared), StepBackRules.From(TraxSettings.Shared),
                         _rateStats.HoldsByInput, _rateStats.HoldsByFlag, _stepStats.StartedByInput, _stepStats.StartedScripted))
                TraxLog.Info("summary", line);
        }

        /// <summary>The technique in the summary header: "AI holds: Input", "AI holds: Legacy (NoAttack + scripted walk)"
        /// or a mix - what this battle used, else the settings at its end.</summary>
        internal string HoldsHeader()
        {
            var s = TraxSettings.Shared;
            int ti = _rateStats.HoldsByInput, tf = _rateStats.HoldsByFlag, si = _stepStats.StartedByInput, ss = _stepStats.StartedScripted;
            string timer = ti > 0 && tf > 0 ? "mixed" : ti > 0 ? "Input" : tf > 0 ? "Legacy" : s.AttackRatePaceByInput ? "Input" : "Legacy";
            string step = si > 0 && ss > 0 ? "mixed" : si > 0 ? "Input" : ss > 0 ? "Legacy" : s.StepBackBackpedal ? "Input" : "Legacy";
            if (timer == step)
                return "AI holds: " + (timer == "Input" ? "Input (the AI's own input: guard up, backpedal)" : timer == "Legacy" ? "Legacy (NoAttack + the scripted walk)" : "mixed (a switch changed mid-battle)");
            return "AI holds: the timer " + timer + ", the step back " + step;
        }

        private static string HookText(AiInputHook.HookResult r) => r switch
        {
            AiInputHook.HookResult.FirstHookCallbackWasOn => "our component added - the engine's input callback was already on (another mod's component, e.g. RTS Camera Command System's)",
            AiInputHook.HookResult.FirstHookTurnedOn => "our component added, the engine's input callback turned on by us",
            AiInputHook.HookResult.TurnedOnAgain => "our component already on him, its callback turned on again",
            _ => "our component already on him and called",
        };

        /// <summary>The captured frames of the mission's first held man / first backpedal: the first call and the first
        /// frame in which he wanted to attack, bits before → after.</summary>
        private static string InputCaptureText(AiInputState? s, string attackWord)
        {
            if (s == null) return "input: no state";
            string first = s.CapturedFirst
                ? "the engine's first call: movement bits " + AiInputHook.FlagNames(s.FirstBefore) + " → " + AiInputHook.FlagNames(s.FirstAfter)
                  + ", input vector (" + F2(s.FirstVecX) + ", " + F2(s.FirstVecY) + ") → (" + F2(s.FirstWroteX) + ", " + F2(s.FirstWroteY) + ")"
                : "the engine NEVER called our input hook (tell Claude - the new way does nothing; switch it off)";
            string attack = s.CapturedAttack
                ? "; " + attackWord + ": " + AiInputHook.FlagNames(s.AttackBefore) + " → " + AiInputHook.FlagNames(s.AttackAfter)
                  + ((s.AttackEdit & InputEdit.ReadyCancelled) != 0 ? " (in a ready: cancelled with a guard, not released)"
                    : (s.AttackEdit & InputEdit.GuardRaised) != 0 ? " (the attack taken out, a guard raised)"
                    : (s.AttackEdit & InputEdit.OwnGuardKept) != 0 ? " (the attack taken out, his own guard kept)"
                    : " (the attack taken out)")
                : "; he never wanted to attack meanwhile";
            return first + attack + "; calls so far " + s.Calls;
        }
    }
}
