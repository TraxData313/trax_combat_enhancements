using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 68: the held orders in three groups - how the [summary] splits the wall's samples and what the pace applies to.</summary>
    public enum WallOrderGroup
    {
        /// <summary>Stop / hold: the game's order slot is where the line stands.</summary>
        Standing,

        /// <summary>Advance (yours) or the AI's advance: the game's order slot lies at the enemy's line.</summary>
        Advance,

        /// <summary>Move, fall back, follow: the order slot moves with the order.</summary>
        Moving,

        Count,
    }

    /// <summary>
    /// Step 68's pace settings, read live: the master switch FIRST, then step 41's hold (<c>ShieldWallHoldsSlots</c> - the pace rides the
    /// hold), then <c>ShieldWallAdvanceAsOne</c>; the step (m) a held man of a moving wall is sent ahead of the line's own slots, and how near
    /// the enemy's front the pace begins (m).
    /// </summary>
    public readonly struct WallPaceRules
    {
        public WallPaceRules(bool modEnabled, bool holdsSlots, bool asOne, double stepMetres, double nearMetres)
        {
            ModEnabled = modEnabled;
            HoldsSlots = holdsSlots;
            AsOne = asOne;
            StepMetres = double.IsNaN(stepMetres) ? WallPaceMath.MinStepMetres : Math.Max(WallPaceMath.MinStepMetres, Math.Min(WallPaceMath.MaxStepMetres, stepMetres));
            NearMetres = double.IsNaN(nearMetres) || nearMetres < 0 ? 0 : nearMetres;
        }

        public static WallPaceRules From(TraxSettings s) =>
            new WallPaceRules(s.ModEnabled, s.ShieldWallHoldsSlots, s.ShieldWallAdvanceAsOne, s.ShieldWallAdvanceStepMetres, s.ShieldWallAdvanceNearMetres);

        public bool ModEnabled { get; }
        public bool HoldsSlots { get; }
        public bool AsOne { get; }
        public double StepMetres { get; }
        public double NearMetres { get; }

        /// <summary>The master switch first, the hold, the switch.</summary>
        public bool Enabled => ModEnabled && HoldsSlots && AsOne;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !HoldsSlots ? "ShieldWallHoldsSlots" : !AsOne ? "ShieldWallAdvanceAsOne" : null;

        public bool SameAs(in WallPaceRules o) => ModEnabled == o.ModEnabled && HoldsSlots == o.HoldsSlots && AsOne == o.AsOne && StepMetres == o.StepMetres && NearMetres == o.NearMetres;

        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - a held wall under an advance walks to the game's own slots at the enemy's line (step 41's flag only)";
            return "ON (ShieldWallAdvanceAsOne) - a held shield wall / square / circle under a MOVING order (advance - yours or the AI's -, move, fall back, follow) "
                   + "within " + WallPaceMath.F1(NearMetres) + " m of the enemy's front walks as ONE line: each man is sent at most " + WallPaceMath.F2(StepMetres)
                   + " m ahead of his slot in the line as it stands (ShieldWallAdvanceStepMetres) instead of to the game's order slot at the enemy's line, so the "
                   + "whole wall pushes forward together and nobody runs ahead of it; a charge still lets them go, a stop / hold is the game's own";
        }
    }

    /// <summary>
    /// Step 68 - THE WALL PUSHES AS ONE (Toni's 100 v 100 of 2026-10-19: a shield wall ordered forward with F1 → F4 = ADVANCE; in contact 15.4% of
    /// held men more than 1 m ahead of their slots, the looter battle with the wall standing 0.8%; "they need to hold the wall and push the whole
    /// wall, not to break up"; AI_NOTES "Step 68", DESIGN decision 61). THE CAUSE (the decompile): under an advance the game's ORDER slot of each
    /// man (<c>MovementOrder.GetPositionAux</c>: the closest enemy formation's median position less 2 m) lies AT the enemy's line, and every AI man
    /// walks to his order slot (<c>HumanAIComponent.GetFormationFrame</c> → <c>Agent.TrySetFormationFrame</c>, every ~0.5 s); step 41's flag and the
    /// "ahead" measure use the slot in the line AS IT STANDS (<c>GetCurrentGlobalPositionOfUnit</c> = the men's average + his place) - so in
    /// contact the men who are free to move keep walking into the enemy while those who fight stand: the line stretches and bulges. Under a stop
    /// the two slots are the same point (0.6-0.8% in step 41-67's battles). THE FIX: for a held man of a moving order near the enemy, the frame the
    /// engine walks him to is his slot in the line as it stands + at most <see cref="WallPaceRules.StepMetres"/> toward the game's order slot -
    /// every man the same step, so the line advances as one body at the pace its slowest part allows, and the leash (step 42) still lets a man
    /// step out to strike. Pure: the group, the gap, the paced point.
    /// </summary>
    public static class WallPaceMath
    {
        /// <summary>The setting's range (m) - <c>ShieldWallAdvanceStepMetres</c> (below ~0.3 m the engine's own stop tolerance may never walk him).</summary>
        public const double MinStepMetres = 0.3;
        public const double MaxStepMetres = 3.0;

        /// <summary>The setting's range (m) - <c>ShieldWallAdvanceNearMetres</c>.</summary>
        public const double MaxNearMetres = 60.0;

        public static WallOrderGroup GroupOf(BraceOrderKind k) => k switch
        {
            BraceOrderKind.Advance => WallOrderGroup.Advance,
            BraceOrderKind.AiAdvance => WallOrderGroup.Advance,
            BraceOrderKind.Move => WallOrderGroup.Moving,
            BraceOrderKind.FallBack => WallOrderGroup.Moving,
            BraceOrderKind.Follow => WallOrderGroup.Moving,
            BraceOrderKind.FollowEntity => WallOrderGroup.Moving,
            _ => WallOrderGroup.Standing,
        };

        /// <summary>A held order the pace applies to: every one but a stop / hold (the charge and the retreat are never held - step 41).</summary>
        public static bool Paces(BraceOrderKind k) => GroupOf(k) != WallOrderGroup.Standing;

        /// <summary>The gap between two formations' fronts: their centres' distance less half of each one's depth (never below 0).</summary>
        public static double FrontGap(double centreDistance, double depth, double enemyDepth)
        {
            if (double.IsNaN(centreDistance)) return double.NaN;
            double d = centreDistance - 0.5 * (Pos(depth) + Pos(enemyDepth));
            return d < 0 ? 0 : d;
        }

        /// <summary>Near the enemy: a known gap within <paramref name="nearMetres"/>.</summary>
        public static bool Engaged(double gap, double nearMetres) => !double.IsNaN(gap) && gap <= nearMetres;

        /// <summary>How far the game's order slot lies from his slot in the line (m).</summary>
        public static double OrderLead(in WallFacts f)
        {
            if (!f.HasOrderSlot) return 0;
            double dx = f.OrderX - f.SlotX, dy = f.OrderY - f.SlotY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Does this held man walk to a PACED point now (the rules on, a moving order, his order slot known and more than the step from his
        /// slot, near the enemy)? <paramref name="px"/> / <paramref name="py"/> = his slot in the line + the step toward the order slot.
        /// </summary>
        public static bool Pace(in WallPaceRules r, in WallFacts f, out double px, out double py)
        {
            px = f.SlotX;
            py = f.SlotY;
            if (!r.Enabled || !f.HasPositions || !f.HasOrderSlot || !Paces(f.Order) || !Engaged(f.EnemyGap, r.NearMetres)) return false;
            double dx = f.OrderX - f.SlotX, dy = f.OrderY - f.SlotY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (!(len > r.StepMetres)) return false; // the game's own slot is within the step: nothing to cap
            px = f.SlotX + dx * r.StepMetres / len;
            py = f.SlotY + dy * r.StepMetres / len;
            return true;
        }

        /// <summary>Why a held man of a moving order was not paced at a look (the summary).</summary>
        public static WallPaceSkip Why(in WallPaceRules r, in WallFacts f)
        {
            if (!r.Enabled) return WallPaceSkip.Off;
            if (!Paces(f.Order)) return WallPaceSkip.Standing;
            if (!f.HasOrderSlot || !f.HasPositions) return WallPaceSkip.NoOrderSlot;
            if (!Engaged(f.EnemyGap, r.NearMetres)) return WallPaceSkip.Far;
            return OrderLead(in f) > r.StepMetres ? WallPaceSkip.None : WallPaceSkip.WithinStep;
        }

        private static double Pos(double v) => double.IsNaN(v) || v < 0 ? 0 : v;

        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        public static string Name(WallOrderGroup g) => g switch
        {
            WallOrderGroup.Standing => "standing (stop / hold)",
            WallOrderGroup.Advance => "ADVANCE (yours or the AI's)",
            _ => "moving (move / fall back / follow)",
        };
    }

    /// <summary>Step 68: a held man of a moving order at a look - paced, or why not.</summary>
    public enum WallPaceSkip
    {
        None,

        /// <summary>The pace is off (a switch).</summary>
        Off,

        /// <summary>A stop / hold - the game's slot is the line's.</summary>
        Standing,

        /// <summary>No order slot read (the game gave none).</summary>
        NoOrderSlot,

        /// <summary>The enemy's front farther than <c>ShieldWallAdvanceNearMetres</c> (or unknown): the game's march.</summary>
        Far,

        /// <summary>The game's order slot within the step: nothing to cap.</summary>
        WithinStep,

        Count,
    }

    /// <summary>
    /// Step 68 per mission: the wall's slot samples by ORDER GROUP (ON - the in-contact leak by order: standing vs advance vs moving), the paced
    /// writes and men, how far ahead the game's order slot lay, the looks not paced by reason, the frames given back to the game + the two
    /// [summary] lines.
    /// </summary>
    public sealed class WallPaceStats
    {
        public readonly WallAheadSamples[] ByGroup = { new WallAheadSamples(), new WallAheadSamples(), new WallAheadSamples() };
        private readonly int[] _skips = new int[(int)WallPaceSkip.Count];
        private readonly int[] _pacedByGroup = new int[(int)WallOrderGroup.Count];

        public int PacedWrites;
        public int MenPaced;
        public int Restored;
        public int Errors;
        public bool EverOn;
        public MeanStd Lead;
        public double LeadMax;
        public MeanStd Gap;

        public int Skips(WallPaceSkip s) => _skips[(int)s];
        public int PacedIn(WallOrderGroup g) => _pacedByGroup[(int)g];

        public void AddSample(BraceOrderKind order, double ahead, bool contact) => ByGroup[(int)WallPaceMath.GroupOf(order)].Add(ahead, contact);

        public void Paced(WallOrderGroup g, double lead, double gap)
        {
            PacedWrites++;
            _pacedByGroup[(int)g]++;
            if (!double.IsNaN(lead))
            {
                Lead.Add(lead);
                if (lead > LeadMax) LeadMax = lead;
            }
            if (!double.IsNaN(gap)) Gap.Add(gap);
        }

        public void Skip(WallPaceSkip s) => _skips[(int)s]++;

        /// <summary>The [summary] "wall slots ON by order" line: the in-contact leak per order group (the step-68 question).</summary>
        public string ByOrderLine()
        {
            var c = CultureInfo.InvariantCulture;
            var parts = new List<string>();
            for (int g = 0; g < (int)WallOrderGroup.Count; g++)
            {
                var s = ByGroup[g];
                string name = WallPaceMath.Name((WallOrderGroup)g);
                if (s.Contact == 0)
                {
                    parts.Add(name + " - (" + s.Samples + " samples, none in contact)");
                    continue;
                }
                parts.Add(name + " " + s.Contact + ": more than 1 m ahead " + (100.0 * s.ContactShareNear).ToString("0.0", c) + "%, more than 2 m "
                          + (100.0 * s.ContactOverFar / s.Contact).ToString("0.0", c) + "%, mean " + (s.ContactAheadSum / s.Contact).ToString("0.00", c) + " m, the farthest "
                          + s.ContactMax.ToString("0.0", c) + " m");
            }
            return "wall slots ON by order (step 68 - the in-contact samples, an enemy within " + WallMath.ContactMetres.ToString("0", c) + " m): " + string.Join(" | ", parts);
        }

        /// <summary>The [summary] "wall advance as one" line.</summary>
        public string PaceLine(in WallPaceRules r)
        {
            var c = CultureInfo.InvariantCulture;
            string head = "wall advance as one (step 68): " + r.Describe() + " - ";
            if (!EverOn && PacedWrites == 0) return head + "never on this battle";
            return head + PacedWrites + " paced writes on " + MenPaced + " men (advance " + PacedIn(WallOrderGroup.Advance) + ", move / fall back / follow "
                   + PacedIn(WallOrderGroup.Moving) + "); the game's order slot lay "
                   + (Lead.Count > 0 ? Lead.Mean.ToString("0.00", c) + " m on average from his slot in the line (the farthest " + LeadMax.ToString("0.0", c) + " m)" : "-")
                   + ", the enemy's front " + (Gap.Count > 0 ? Gap.Mean.ToString("0.0", c) + " m away on average" : "-") + "; held looks of a moving order not paced: "
                   + "the enemy's front farther than " + WallPaceMath.F1(r.NearMetres) + " m (the game's march) " + Skips(WallPaceSkip.Far)
                   + ", the game's slot within the step " + Skips(WallPaceSkip.WithinStep) + ", no order slot " + Skips(WallPaceSkip.NoOrderSlot)
                   + ", the pace off " + Skips(WallPaceSkip.Off) + "; frames given back to the game's " + Restored + "; errors " + Errors;
        }
    }
}
