using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 96 (Toni 2026-10-25: "more defencive - bump up the defencive stuff"): THE AI GUARDS BETTER. Each tracked man's two factors
    /// (<see cref="TrackedAgent.GuardGuess"/> / <see cref="TrackedAgent.GuardDecide"/>) are re-targeted at tracking and with the speeds
    /// (<see cref="RetargetSpeed"/> - so a settings change marks everyone for a recompute); the stat decorator writes the game's guess chance × g
    /// (before step 30's tired raise) and the block- / parry-decide abilities × b (<see cref="AiGuardMath"/>). This part: the [guard] lines, the
    /// first write in full, the counts by f band and the [summary] line. AI_NOTES "Step 96".
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly AiGuardStats _guardStats = new AiGuardStats();
        private AiGuardRules _seenGuard;
        private bool _guardFirstLogged;

        /// <summary>For the smoke: the step-96 counters of this mission.</summary>
        internal AiGuardStats GuardStatsForSmoke => _guardStats;

        /// <summary>His factors now (1 / 1 for the player and while off); a change marks him for a recompute. True when they moved.</summary>
        private bool RetargetGuard(TrackedAgent st)
        {
            var gr = AiGuardRules.From(TraxSettings.Shared);
            AiGuardMath.Factors(in gr, IsPlayer(st), out float g, out float b);
            if (g == st.GuardGuess && b == st.GuardDecide) return false;
            st.GuardGuess = g;
            st.GuardDecide = b;
            if (!st.Removed) st.SpeedDirty = true;
            return true;
        }

        private void StartGuard()
        {
            _seenGuard = AiGuardRules.From(TraxSettings.Shared);
            TraxLog.Info("guard", "mission start: " + _seenGuard.Describe() + " - read live; the [summary] \"AI guard factors\" line says what was written by f band");
        }

        /// <summary>A setting or a switch changed (from NoteTiredBlockSettings): one line; everyone is re-targeted by the same handler.</summary>
        private void NoteGuardSettings(TraxSettings s)
        {
            var now = AiGuardRules.From(s);
            if (now.GuessPercent == _seenGuard.GuessPercent && now.DecidePercent == _seenGuard.DecidePercent && now.Enabled == _seenGuard.Enabled
                && now.GuessOn == _seenGuard.GuessOn && now.DecideOn == _seenGuard.DecideOn) return;
            _seenGuard = now;
            TraxLog.Info("guard", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now.Describe()
                + " - every AI man gets the new values at his next recompute (at most " + MaxRecomputesPerTick + " recomputes a tick)");
        }

        /// <summary>The stat decorator wrote the guard for <paramref name="agent"/>: counted by his f band, the mission's first in full. Never throws
        /// (the decorator's own try around it as well); no allocation past the first line.</summary>
        internal static void NoteGuard(Agent agent, float gameGuess, float guess, float gameBlock, float block, float gameParry, float parry, float g, float b)
        {
            var logic = _current;
            if (logic == null) return;
            var st = logic.Get(agent);
            if (st == null) return;
            logic.GuardApplied(st, gameGuess, guess, gameBlock, block, gameParry, parry, g, b);
        }

        internal void GuardApplied(TrackedAgent st, float gameGuess, float guess, float gameBlock, float block, float gameParry, float parry, float g, float b)
        {
            var r = Rules;
            double f = AthleticsMath.PeakShare(in r, st);
            _guardStats.Add(AthleticsMath.PeakBin(f), gameGuess, guess, gameBlock, block, gameParry, parry);
            if (!st.GuardSeen)
            {
                st.GuardSeen = true;
                _guardStats.Men++;
            }
            if (_guardFirstLogged) return;
            _guardFirstLogged = true;
            TraxLog.Info("guard", "first AI guard this mission (step 96): " + Name(st) + " at " + Sec(SafeNow()) + " s, f " + F2(f)
                + " - the game's block-direction guess chance " + F2(gameGuess) + " → " + F2(guess) + " (x" + F2(g) + ", AiGuardGuessPercent; before step 30's tired raise)"
                + ", the block-decide ability " + F2(gameBlock) + " → " + F2(block) + " and the parry-decide ability " + F2(gameParry) + " → " + F2(parry)
                + " (x" + F2(b) + ", AiBlockDecidePercent; at most " + F2(AiGuardMath.BlockDecideMax) + " / " + F2(AiGuardMath.ParryDecideMax) + ") - the engine reads all three itself");
        }

        /// <summary>The summary's step-96 line (own try in the caller).</summary>
        private void WriteGuardSummary() => TraxLog.Info("summary", _guardStats.SummaryLine(AiGuardRules.From(TraxSettings.Shared)));
    }
}
