using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 16's numbers for one mission - the AI holds (the AI timer and the step back) as the A/B playtest reads
    /// them (AI_NOTES "Step 16", docs/PLAYTEST.md): the techniques used; THE fix target - how often a man held by
    /// the timer or stepping back blocks, against everyone else; the input hook's work (men hooked, calls, attack
    /// bits taken out, guards raised, backpedal frames, holds the engine never called us during, errors); and the
    /// timer surviving a step back (overlaps, Legacy deferrals). The module counts the guard and the overlaps as
    /// they happen and adds the hook's per-man counters just before the summary. Main thread. A new instance per mission.
    /// </summary>
    public sealed class AiHoldStats
    {
        // ---- the guard by state: 0 held by the timer (not stepping back), 1 stepping back, 2 stepping back AND held,
        // 3 neither and tired (below the peak line), 4 neither and at full strength
        private readonly int[] _hits = new int[5];
        private readonly int[] _blocked = new int[5];

        // ---- the hook (filled by the module before the summary)
        public int MenHooked;
        public int CallbackAlreadyOn;
        public int CallbackTurnedOn;
        public int CallbackTurnedOff;
        public long CallsWhileActive;
        public double ActiveManSeconds;
        public long AttackCleared;
        public long GuardRaised;
        public long OwnGuardKept;
        public long ReadyCancelled;
        public long BackpedalFrames;
        public long NotAiCalls;
        public int HoldsWithoutACall;
        public int StepsWithoutACall;
        public int HookErrors;

        // ---- the timer survives a step back
        public int HoldsOverlappingAStep;
        public int Deferred;
        public int DeferredStarted;
        public int DeferredCovered;
        public int AttacksWhileHeld;

        // ---- the technique switches seen during the battle
        public bool TimerSwitched;
        public bool StepSwitched;

        /// <summary>A melee hit on an AI fighter on foot: held by the AI timer, stepping back, tired (below the peak
        /// line), blocked (shield, parry, chamber) or landed.</summary>
        public void AddHit(bool held, bool stepping, bool tired, bool blocked)
        {
            int k = stepping ? (held ? 2 : 1) : held ? 0 : tired ? 3 : 4;
            _hits[k]++;
            if (blocked) _blocked[k]++;
        }

        public int Hits(int state) => _hits[state];

        public int Blocked(int state) => _blocked[state];

        /// <summary>Blocked share of hits on men held by the timer (not stepping back) - NaN with none.</summary>
        public double HeldShare => Share(_blocked[0], _hits[0]);

        /// <summary>Blocked share of hits on men stepping back (held by the timer too or not).</summary>
        public double SteppingShare => Share(_blocked[1] + _blocked[2], _hits[1] + _hits[2]);

        /// <summary>Blocked share of hits on everyone else on foot (neither held nor stepping back).</summary>
        public double OthersShare => Share(_blocked[3] + _blocked[4], _hits[3] + _hits[4]);

        /// <summary>The summary lines (docs/PLAYTEST.md L-appendix quotes them).</summary>
        public List<string> SummaryLines(in AttackRateRules rate, in StepBackRules step, int holdsByInput, int holdsByFlag, int stepsByInput, int stepsScripted)
        {
            var lines = new List<string>();
            lines.Add("AI holds - technique: the AI timer " + (rate.PaceHold ? rate.PaceTechnique() : "off (AttackRatePaceHold)")
                      + " - this battle " + holdsByInput + " holds by input, " + holdsByFlag + " by NoAttack"
                      + (TimerSwitched ? " (AttackRatePaceByInput SWITCHED mid-battle: the lines mix both)" : string.Empty)
                      + "; the step back " + (step.StepBackEnabled ? AiInputMath.StepTechnique(step.Backpedal) : "off (StepBackEnabled)")
                      + " - this battle " + stepsByInput + " backpedals, " + stepsScripted + " scripted walks"
                      + (StepSwitched ? " (StepBackBackpedal SWITCHED mid-battle: the lines mix both)" : string.Empty));

            lines.Add("AI holds - GUARD (melee hits on AI fighters on foot that were blocked or parried; THE fix target: held and stepping back close to everyone else): held by the timer "
                      + ShareText(_blocked[0], _hits[0]) + ", stepping back " + ShareText(_blocked[1] + _blocked[2], _hits[1] + _hits[2])
                      + " (of them also held by the timer " + ShareText(_blocked[2], _hits[2]) + "), everyone else " + ShareText(_blocked[3] + _blocked[4], _hits[3] + _hits[4])
                      + " - of them tired (below the peak line) " + ShareText(_blocked[3], _hits[3]) + ", at full strength " + ShareText(_blocked[4], _hits[4])
                      + Verdict());

            var h = new StringBuilder("AI holds - the input hook (AgentComponent.OnAIInputSet): ").Append(MenHooked).Append(" men hooked (a component each, added the first time he was held)");
            if (MenHooked > 0)
            {
                h.Append(", the callback already on for ").Append(CallbackAlreadyOn).Append(" of them (another mod's component - RTS Camera Command System turns it on for every agent), turned on by us for ")
                 .Append(CallbackTurnedOn).Append(", turned off again when idle ").Append(CallbackTurnedOff).Append(" times");
            }
            h.Append("; calls while held ").Append(CallsWhileActive);
            if (ActiveManSeconds > 0) h.Append(" (about ").Append(N1(CallsWhileActive / ActiveManSeconds)).Append(" a second per held man)");
            h.Append("; the attack bits taken out in ").Append(AttackCleared).Append(" calls (a guard raised in ").Append(GuardRaised)
             .Append(", his own guard kept in ").Append(OwnGuardKept).Append(", a ready cancelled in ").Append(ReadyCancelled)
             .Append("), a backpedal written in ").Append(BackpedalFrames).Append(" calls; the player or a non-AI agent passed untouched ").Append(NotAiCalls)
             .Append("; holds / backpedals the engine never called us during ").Append(HoldsWithoutACall).Append(" / ").Append(StepsWithoutACall)
             .Append(" (must be 0 - else the hook is dead: switch the new ways off and tell Claude); errors ").Append(HookErrors);
            lines.Add(h.ToString());

            lines.Add("AI holds - the timer survives a step back: holds that overlapped a step back " + HoldsOverlappingAStep
                      + " (both ran at once, his attacks held until the later of the two ends); NoAttack holds deferred behind a scripted step back "
                      + Deferred + " (set when the step ended " + DeferredStarted + ", covered by the step back " + DeferredCovered
                      + ") | AI attacks that started while a hold or a step back held him anyway " + AttacksWhileHeld + " (must be about 0)");
            return lines;
        }

        /// <summary>" - held close to everyone else" etc.: the gap in points between the held / stepping men and everyone else.</summary>
        private string Verdict()
        {
            double others = OthersShare;
            if (double.IsNaN(others)) return string.Empty;
            var sb = new StringBuilder(" - gap to everyone else: held ");
            sb.Append(Gap(HeldShare, others)).Append(", stepping back ").Append(Gap(SteppingShare, others));
            return sb.ToString();
        }

        private static string Gap(double share, double others) =>
            double.IsNaN(share) ? "n/a" : ((share - others) * 100).ToString("+0;-0;0", CultureInfo.InvariantCulture) + " points";

        private static double Share(int part, int total) => total == 0 ? double.NaN : (double)part / total;

        private static string ShareText(int part, int total) =>
            total == 0 ? "n/a (n 0)" : (100.0 * part / total).ToString("0", CultureInfo.InvariantCulture) + "% (n " + total + ")";

        private static string N1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
