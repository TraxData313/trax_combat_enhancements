using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 30 (Anton, 2026-10-12): a tired AI man's block DIRECTION is partly left to chance. The weight w =
    /// TiredBlockRandomPercent / 100 × (1 − f) is re-targeted with the speed multipliers (<see cref="RetargetSpeed"/>, the same
    /// recompute steps and budget) and written by the stat decorator into the game's own
    /// <c>AiRandomizedDefendDirectionChance</c> = own + (1 − own) × w (<see cref="TiredBlockMath.Chance"/>). This part: the
    /// settings lines, the first raised chance in full, the GUARD by f band (OnMeleeHit) and the 2 [summary] lines.
    /// AI_NOTES "Step 30", RESEARCH §M.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly TiredBlockStats _tiredBlockStats = new TiredBlockStats();
        private TiredBlockRules _seenTiredBlock;
        private bool _tiredBlockFirstLogged;

        /// <summary>For the smoke: the step-30 counters of this mission.</summary>
        internal TiredBlockStats TiredBlockStatsForSmoke => _tiredBlockStats;

        /// <summary>Mission start: the settings sentence (read live from now on).</summary>
        private void StartTiredBlock()
        {
            _seenTiredBlock = TiredBlockRules.From(TraxSettings.Shared);
            TraxLog.Info("guard", "mission start: " + _seenTiredBlock.Describe() + " - read live; the [summary] \"tired block direction\" and"
                + " \"AI guard by f\" lines say how much it was raised and how well the AI blocked by f band");
        }

        /// <summary>The setting or a switch changed (from ApplySettingsChange): one line; everyone is re-targeted by the same
        /// handler and recomputed over the next ticks.</summary>
        private void NoteTiredBlockSettings(TraxSettings s)
        {
            var now = TiredBlockRules.From(s);
            if (now.Percent == _seenTiredBlock.Percent && now.Enabled == _seenTiredBlock.Enabled) return;
            _seenTiredBlock = now;
            TraxLog.Info("guard", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now.Describe()
                + " - every tired AI man gets the new chance over the next ticks (at most " + MaxRecomputesPerTick + " recomputes a tick)");
        }

        /// <summary>The w to apply to <paramref name="st"/> now (0 for the player - his mouse picks his direction).</summary>
        private float TiredBlockWeight(TrackedAgent st, in AthleticsRules r) =>
            TiredBlockMath.Weight(TiredBlockRules.From(TraxSettings.Shared), AthleticsMath.PeakShare(in r, st), IsPlayer(st));

        /// <summary>
        /// The stat decorator wrote <paramref name="chance"/> over the game's <paramref name="game"/> for <paramref name="agent"/>
        /// (w = <paramref name="weight"/>): counted by his f band at the re-target; the mission's first in full, the rest verbose.
        /// Never throws (the decorator's own try around it as well).
        /// </summary>
        internal static void NoteTiredBlock(Agent agent, float game, float chance, float weight)
        {
            var logic = _current;
            if (logic == null) return;
            var st = logic.Get(agent);
            if (st == null) return;
            logic.TiredBlockApplied(st, game, chance, weight);
        }

        /// <summary>The bookkeeping of one written chance (the smoke calls it through the real decorator).</summary>
        internal void TiredBlockApplied(TrackedAgent st, float game, float chance, float weight)
        {
            double f = st.BlockRandomPeakShare;
            _tiredBlockStats.AddRaised(AthleticsMath.PeakBin(f), game, chance);
            if (!st.BlockRandomSeen)
            {
                st.BlockRandomSeen = true;
                _tiredBlockStats.Men++;
            }
            if (!_tiredBlockFirstLogged)
            {
                _tiredBlockFirstLogged = true;
                TraxLog.Info("guard", "first tired block direction this mission: " + Name(st) + " at " + Sec(SafeNow()) + " s, f " + F2(f)
                    + " - the game's random-direction chance for him " + F2(game) + (game < 0f ? " (below 0: never random - counted as 0)" : string.Empty)
                    + " → " + F2(chance) + " = own " + F2(TiredBlockMath.OwnChance(game)) + " + (1 − own) × w, w = "
                    + _seenTiredBlock.Percent + "% × (1 − " + F2(f) + ") = " + F2(weight) + " - that share of his blocks goes in a random direction"
                    + " (right 1 in 4 by luck); his blocks right about " + Pct(TiredBlockMath.RightShare(chance)) + " of the time if his own AI is always right");
            }
            else if (TraxLog.VerboseWants("guard-chance"))
            {
                TraxLog.Verbose("guard", "tired block direction: " + Name(st) + " f " + F2(f) + ", w " + F2(weight) + " - chance "
                    + F2(game) + " → " + F2(chance), "guard-chance");
            }
        }

        /// <summary>OnMeleeHit, BEFORE the defender's block charge (so f is his bar when the blow came): a melee blow that reached
        /// an AI man - blocked by kind or landed - by his f band. Kicks, bashes, horse charges and missiles left out; the player
        /// never; a hit that struck neither him nor his guard (the world) left out.</summary>
        private void TiredGuardHit(Agent? victim, in AttackCollisionData cd)
        {
            if (victim == null || cd.IsMissile || cd.IsAlternativeAttack || cd.IsHorseCharge) return;
            var st = Get(victim);
            if (st == null) return;
            var kind = BlockMath.KindOf(cd.IsMissile, cd.IsAlternativeAttack, cd.IsHorseCharge, cd.CollidedWithShieldOnBack,
                cd.AttackBlockedWithShield, cd.CorrectSideShieldBlock, (int)cd.CollisionResult);
            if (kind == BlockKind.None && cd.CollisionResult != CombatCollisionResult.StrikeAgent && !cd.CollidedWithShieldOnBack) return;
            var r = Rules;
            GuardTaken(st, kind, in r);
        }

        /// <summary>One blow at an AI man counted by his f band (<see cref="BlockKind.None"/> = landed). Athletics off (f
        /// meaningless) or the player → not counted. The smoke drives it.</summary>
        internal void GuardTaken(TrackedAgent st, BlockKind kind, in AthleticsRules r)
        {
            if (!r.Enabled || IsPlayer(st)) return;
            _tiredBlockStats.AddGuard(AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st)), kind);
        }

        /// <summary>The summary's 2 step-30 lines (own try in the caller).</summary>
        private void WriteTiredBlockSummary()
        {
            foreach (var line in _tiredBlockStats.SummaryLines(TiredBlockRules.From(TraxSettings.Shared)))
                TraxLog.Info("summary", line);
        }
    }
}
