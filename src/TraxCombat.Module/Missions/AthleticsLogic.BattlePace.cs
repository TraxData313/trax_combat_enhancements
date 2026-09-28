using System;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 21 - BATTLE PACE (Anton, 2026-09-28: "make the infantry more defensive, especially the guys with the shields, so
    /// that maybe they swing 30% less" and "make the archers a bit slower ... about 30% slower overall"; AI_NOTES "Step 21",
    /// DESIGN §2 "Battle pace"). Rides on the AI timer (AthleticsLogic.AttackRate.cs): after each AI attack the pause is the
    /// tired part D × (1/m − 1) PLUS a share by the attack's CLASS, read at its release from what he holds
    /// (<see cref="IWeaponFacts"/>): shield infantry and other foot melee wait so their expected cycle stretches by
    /// 1 / (1 − the %) (<see cref="BattlePaceMath.Plan"/>) - at full strength too, so fresh men are held now; bowmen and
    /// crossbowmen wait a flat number of seconds more; riders' melee, thrown weapons, slings and you: tiredness only. Guard up
    /// (the input hook), the master switch and every lift path exactly as the tired pause's. This file: the class read, the
    /// per-class bookkeeping (<see cref="BattlePaceStats"/>), the model's cycle for the summary, the first-of-each-class lines.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly BattlePaceStats _paceStats = new BattlePaceStats();
        private readonly bool[] _classLogged = new bool[(int)AttackClass.Count];
        private BattlePaceRules _seenPace;
        private bool _paceSettingsChanged;

        /// <summary>This mission's battle-pace numbers (the offline smoke reads them).</summary>
        internal BattlePaceStats PaceStats => _paceStats;

        /// <summary>What a fighter holds at a release (the offline smoke swaps in a stand-in).</summary>
        internal IWeaponFacts WeaponFacts { get; set; } = WeaponFactsDefault.Current;

        /// <summary>Mission start: the settings line, read live from now on.</summary>
        private void StartBattlePace(in AttackRateRules rr)
        {
            _seenPace = BattlePaceRules.From(TraxSettings.Shared);
            TraxLog.Info("rate", "battle pace (step 21) at mission start: " + _seenPace.Describe(rr.PaceOn, rr.PaceOffBecause)
                + " - the [summary] \"battle pace\" lines give each class's attacks a minute per man");
        }

        /// <summary>A release began (melee, a loose, a throw): the class of this attack from what he holds now - AI only (you
        /// are never slowed by it). A failing read logs once and falls back to the plain class of its kind (no share it was not
        /// read for).</summary>
        private void ReadAttackClass(TrackedAgent st, bool melee, bool throwing)
        {
            if (IsPlayer(st)) return;
            bool mounted = st.Agent.MountAgent != null;
            try
            {
                WeaponFacts.Read(st, melee, throwing, out bool shield, out var ranged);
                st.AttackClass = BattlePaceMath.Classify(melee, mounted, shield, ranged);
            }
            catch (Exception e)
            {
                Failed("rate.class", e);
                st.AttackClass = BattlePaceMath.Classify(melee, mounted, false, RangedWeaponKind.Other);
            }
        }

        /// <summary>An AI attack ENDED: its class (checked against its kind), counted with the man; the next cycle's facts are
        /// reset (the pause decision fills them in).</summary>
        private AttackClass NoteClassAttack(TrackedAgent st, AttackKind kind)
        {
            var cls = BattlePaceMath.Consistent(st.AttackClass, kind, st.Agent.MountAgent != null);
            int bit = 1 << (int)cls;
            bool newMan = (st.PaceClassesSeen & bit) == 0;
            st.PaceClassesSeen |= bit;
            _paceStats.AddAttack(cls, newMan);
            st.PaceLastValid = true;
            st.PaceLastClass = cls;
            st.PaceLastKind = kind;
            st.PaceLastShare = 0;
            st.PaceModelAt0 = st.PaceModelWith = double.NaN;
            return cls;
        }

        /// <summary>The model's cycle after this attack (foot melee only - the summary's check): his attack as played + the pause
        /// + his own gap AiMeleeGapSeconds - with the setting at 0 (the tired pause alone) and with it (the pause he got).</summary>
        private static void SetPaceModel(TrackedAgent st, in BattlePaceRules bp, in PacePlan plan, bool known, bool paused)
        {
            st.PaceLastShare = paused ? plan.Extra : 0;
            if (!known || (plan.Class != AttackClass.ShieldInfantry && plan.Class != AttackClass.FootMelee)) return;
            double played = st.EndedPlayed > 0 ? st.EndedPlayed : st.EndedDuration;
            st.PaceModelAt0 = BattlePaceMath.ModelCycle(played, plan.Tired, bp.AiGapSeconds);
            st.PaceModelWith = BattlePaceMath.ModelCycle(played, paused ? plan.Total : 0, bp.AiGapSeconds);
        }

        /// <summary>A pause asked for was not started after all (refused by the tick, or his next attack began first): counted as
        /// "no pause", and the model of this cycle is dropped (it would claim a pause that never ran).</summary>
        private void PaceNotStarted(TrackedAgent st, PaceState ps)
        {
            _paceStats.AddNoPause(ps.Class);
            st.PaceLastShare = 0;
            st.PaceModelAt0 = st.PaceModelWith = double.NaN;
        }

        /// <summary>One cycle (release to release, shot to shot) of an AI fighter: filed under the class of the attack whose
        /// pause is inside it - a fighting rhythm only (the attack-rate cap at his m, + the share inside it).</summary>
        private void NotePaceCycle(TrackedAgent st, AttackKind kind, double seconds, float asked)
        {
            if (!st.PaceLastValid || st.PaceLastKind != kind) return;
            st.PaceLastValid = false;
            if (double.IsNaN(seconds) || seconds < 0 || seconds > AttackRateMath.CycleCap(kind, asked) + st.PaceLastShare) return;
            _paceStats.AddCycle(st.PaceLastClass, seconds, st.PaceLastShare, st.PaceModelAt0, st.PaceModelWith);
        }

        /// <summary>His next attack began <paramref name="afterEnd"/> s after his last AI timer ended (the AI's own gap after a
        /// pause) - filed under that timer's class (a fighting rhythm only).</summary>
        private void NotePaceGap(TrackedAgent st, AttackKind kind, double afterEnd)
        {
            if (double.IsNaN(afterEnd) || afterEnd > AttackRateMath.CycleCap(kind, 1f)) return;
            _paceStats.AddGapAfterPause(st.TimerClass, Math.Max(0, afterEnd));
        }

        /// <summary>A battle-pace setting changed (from NoteRateSettings): logged once per change; the summary says the battle
        /// mixed both. It applies at every fighter's next attack; a pause running now keeps its length.</summary>
        private void NoteBattlePaceSettings(TraxSettings s)
        {
            var now = BattlePaceRules.From(s);
            if (now.SameAs(in _seenPace)) return;
            _seenPace = now;
            _paceSettingsChanged = true;
            var rr = AttackRateRules.From(s);
            TraxLog.Info("rate", "battle pace changed mid-mission at " + Sec(SafeNow()) + " s: " + now.Describe(rr.PaceOn, rr.PaceOffBecause)
                + " - applies at each fighter's next attack; a pause running now keeps its length");
        }

        /// <summary>Once per class and mission: the first AI attack of that class and what its pause became - the proof that the
        /// class was read and the share asked (the playtest's single run).</summary>
        private void FirstOfClass(TrackedAgent st, AttackClass cls, double now, in PacePlan plan, in BattlePaceRules bp, string? why)
        {
            int i = (int)cls;
            if (i < 0 || i >= _classLogged.Length || _classLogged[i]) return;
            _classLogged[i] = true;
            string head = "battle pace - first " + BattlePaceMath.Name(cls) + " attack this mission: " + Name(st) + " at " + Sec(now) + " s ("
                          + BattlePaceMath.What(cls) + "; " + bp.SettingText(cls) + ") → ";
            if (why != null)
            {
                TraxLog.Info("rate", head + why);
                return;
            }
            string text = head + "a pause of " + F2(plan.Total) + " s asked = the tired pause " + F2(plan.Tired) + " s + the battle-pace share " + F2(plan.Extra) + " s";
            if (plan.Percent > 0)
            {
                double played = st.EndedPlayed > 0 ? st.EndedPlayed : st.EndedDuration;
                double at0 = BattlePaceMath.ModelCycle(played, plan.Tired, bp.AiGapSeconds);
                text += " - his expected cycle (his attack " + F2(played) + " s + his own gap " + F2(bp.AiGapSeconds) + " s + the tired pause " + F2(plan.Tired)
                        + " s) " + F2(at0) + " s ÷ (1 - " + F2(plan.Percent / 100.0) + ") = " + F2(BattlePaceMath.ModelCycle(played, plan.Total, bp.AiGapSeconds)) + " s: "
                        + plan.Percent + "% fewer swings";
            }
            else if (plan.Extra > 0)
            {
                text += " - the archer's extra seconds on top of the tired pause";
            }
            else
            {
                text += " - no battle-pace share (none for this class, or its setting at 0)";
            }
            TraxLog.Info("rate", text);
        }

        /// <summary>The summary's battle-pace lines (own try in the caller).</summary>
        private void WriteBattlePaceSummary()
        {
            var s = TraxSettings.Shared;
            var rr = AttackRateRules.From(s);
            foreach (var line in _paceStats.SummaryLines(BattlePaceRules.From(s), rr.PaceOn, rr.PaceOffBecause, _paceSettingsChanged))
                TraxLog.Info("summary", line);
        }
    }
}
