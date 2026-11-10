using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 110 (Toni 2026-11-10, in the training field: "upon resetting my health after a fight with a training master, reset my [Athletics] while I
    /// test more"; AI_NOTES "Step 110", DESIGN decision 99). In the game's training field (StoryMode's <c>TrainingFieldMissionController</c>, found by
    /// NAME - the module does not reference StoryMode) every fighter starts fresh - the bar to the top his wounds allow, the swing stack and the sprint
    /// full, any pause / step back / brace ended (step 19's fresh start) - at each of the controller's own moments, read from public state only:
    /// <list type="bullet">
    /// <item>YOUR HEALTH RESET: the controller restores your health after a bout (at once after you beat the rookie, 1.5 s after a loss or the veteran's
    /// fall) and writes it full at a bout's start - your health rising by <see cref="TrainingFieldRefillRules.HealthJumpPercent"/> or more of your
    /// maximum between two ticks (in the training field nothing else heals you);</item>
    /// <item>A BOUT BEGAN / ENDED: the controller moves its trainers onto the enemy team at "Defeat the trainer!" and back onto yours when the bout ends
    /// (won, lost or left) - the count of men hostile to you turning from 0 to some or back to 0. This catches a bout you won without a scratch, when
    /// your health never moved.</item>
    /// </list>
    /// The master switch FIRST, then Athletics, then <c>TrainingFieldRefill</c> - read live at each moment; the moment is logged either way.
    /// </summary>
    public readonly struct TrainingFieldRefillRules
    {
        public TrainingFieldRefillRules(bool modEnabled, bool athleticsEnabled, bool refillEnabled, double healthJumpPercent = 5)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            RefillEnabled = refillEnabled;
            HealthJumpPercent = double.IsNaN(healthJumpPercent) ? 5 : healthJumpPercent < 1 ? 1 : healthJumpPercent > 100 ? 100 : healthJumpPercent;
        }

        public static TrainingFieldRefillRules From(TraxSettings s) =>
            new TrainingFieldRefillRules(s.ModEnabled, s.AthleticsEnabled, s.TrainingFieldRefill, s.TrainingFieldRefillHealthJumpPercent);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }

        /// <summary>TrainingFieldRefill (Advanced, Refill).</summary>
        public bool RefillEnabled { get; }

        /// <summary>TrainingFieldRefillHealthJumpPercent: a rise of your health by this much of your maximum between two ticks = the game reset it.</summary>
        public double HealthJumpPercent { get; }

        public bool On => ModEnabled && AthleticsEnabled && RefillEnabled;

        /// <summary>The gate, master switch FIRST (null = it runs).</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !RefillEnabled ? "TrainingFieldRefill" : null;

        public string Describe() =>
            "training field fresh start (step 110): when the game resets your health (a rise of " + F(HealthJumpPercent) + "% of your maximum or more in a "
            + "tick) or a trainer's bout begins or ends (the trainers moved onto the enemy's team or back), everyone in the training field refills - the "
            + "Athletics bar to the top his wounds allow, the swing stack and the sprint full, any pause, step back or brace ended - "
            + (On ? "ON" : "OFF (" + OffBecause + "): the moments only logged");

        internal static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 110: what made the training field's fresh start.</summary>
    public enum TrainingRefillWhy
    {
        /// <summary>Your health rose by the jump or more in one tick - the controller reset it.</summary>
        HealthReset,

        /// <summary>Men hostile to you appeared (0 → some): a bout began.</summary>
        BoutBegan,

        /// <summary>No man hostile to you any more (some → 0): the bout ended.</summary>
        BoutEnded,

        /// <summary>None this tick.</summary>
        None,
    }

    /// <summary>Step 110 - the pure part: the controller's name, the health jump, the watch over both signals.</summary>
    public static class TrainingFieldMath
    {
        /// <summary>The game's training field controller (StoryMode.Missions) - matched by its type NAME.</summary>
        public const string ControllerName = "TrainingFieldMissionController";

        public static bool IsController(string? typeName) => string.Equals(typeName, ControllerName, StringComparison.Ordinal);

        /// <summary>Your health (0..1 of its maximum) rose by the jump or more since the last tick. NaN either side = no.</summary>
        public static bool HealthJumped(double before, double now, in TrainingFieldRefillRules r) =>
            !double.IsNaN(before) && !double.IsNaN(now) && now - before >= r.HealthJumpPercent / 100.0 - 1e-9;

        public static string WhyText(TrainingRefillWhy why) => why switch
        {
            TrainingRefillWhy.HealthReset => "the game reset your health",
            TrainingRefillWhy.BoutBegan => "a trainer's bout began (a man turned hostile to you)",
            TrainingRefillWhy.BoutEnded => "the trainer's bout ended (nobody hostile to you any more)",
            _ => "none",
        };
    }

    /// <summary>
    /// Step 110: one training field's watch - fed every tick with your health (0..1; NaN = no player now) and the count of active men hostile to you.
    /// The first reading is only the baseline (a mission never starts with a refill). The health jump wins when both happen in one tick (one refill).
    /// </summary>
    public sealed class TrainingFieldWatch
    {
        public double LastHealth { get; private set; } = double.NaN;
        public int LastHostile { get; private set; } = -1;

        /// <summary>The health before the last jump (the log's "54% → 100%").</summary>
        public double HealthBefore { get; private set; } = double.NaN;

        public TrainingRefillWhy Observe(double health, int hostile, in TrainingFieldRefillRules r)
        {
            var why = TrainingRefillWhy.None;
            if (TrainingFieldMath.HealthJumped(LastHealth, health, in r))
            {
                why = TrainingRefillWhy.HealthReset;
                HealthBefore = LastHealth;
            }
            else if (LastHostile >= 0 && hostile >= 0)
            {
                if (LastHostile == 0 && hostile > 0) why = TrainingRefillWhy.BoutBegan;
                else if (LastHostile > 0 && hostile == 0) why = TrainingRefillWhy.BoutEnded;
            }
            if (!double.IsNaN(health)) LastHealth = health; // no player this tick: keep the last reading (a jump spans it)
            if (hostile >= 0) LastHostile = hostile;
            return why;
        }
    }

    /// <summary>Step 110 per mission: the moments, the refills, the [athletics] line and the [summary] line.</summary>
    public sealed class TrainingFieldRefillStats
    {
        /// <summary>The training field's controller was found at the first tick.</summary>
        public bool IsTrainingField { get; set; }

        /// <summary>Moments seen by why (refilled or not).</summary>
        public readonly int[] Moments = new int[(int)TrainingRefillWhy.None];

        /// <summary>Fresh starts that ran (all switches on), men refilled in them, of them you; moments with a switch off.</summary>
        public int Refills, MenRefilled, YouRefilled, Off;

        /// <summary>Your bar before each refill (0..1) - the sum and the lowest (how tired the bouts left you).</summary>
        public double YouBeforeSum, YouBeforeMin = double.NaN;

        /// <summary>Pauses, step backs, braces, sprints a fresh start ended.</summary>
        public int Released;

        public int Errors;

        public void AddMoment(TrainingRefillWhy why)
        {
            if (why < TrainingRefillWhy.None) Moments[(int)why]++;
        }

        public void AddYou(double before)
        {
            YouRefilled++;
            if (double.IsNaN(before)) return;
            YouBeforeSum += before;
            if (double.IsNaN(YouBeforeMin) || before < YouBeforeMin) YouBeforeMin = before;
        }

        /// <summary>The [athletics] line of one moment.</summary>
        public static string MomentLine(TrainingRefillWhy why, double now, in TrainingFieldRefillRules r, double healthBefore, double healthNow, int men,
            double youBefore, double youAfter, double youPool, string others)
        {
            var sb = new StringBuilder(320);
            sb.Append("training field (step 110) at ").Append(F1(now)).Append(" s: ").Append(TrainingFieldMath.WhyText(why));
            if (why == TrainingRefillWhy.HealthReset) sb.Append(" (").Append(P0(healthBefore)).Append(" → ").Append(P0(healthNow)).Append(')');
            if (!r.On)
            {
                sb.Append(" - nobody refilled (").Append(r.OffBecause).Append(" is off)");
                return sb.ToString();
            }
            sb.Append(" - a fresh start for ").Append(men).Append(men == 1 ? " man" : " men");
            if (!double.IsNaN(youBefore))
                sb.Append(": YOU ").Append(F1(youBefore * youPool)).Append(" → ").Append(F1(youAfter * youPool)).Append(" of ").Append(F1(youPool))
                  .Append(" (swings and sprint full)");
            if (!string.IsNullOrEmpty(others)) sb.Append("; ").Append(others);
            return sb.ToString();
        }

        /// <summary>The [summary] line (only in the training field).</summary>
        public string SummaryLine(in TrainingFieldRefillRules r)
        {
            var sb = new StringBuilder(320);
            sb.Append("training field fresh start (step 110) ").Append(r.On ? "ON" : "OFF (" + r.OffBecause + ")").Append(": moments - your health reset ")
              .Append(Moments[(int)TrainingRefillWhy.HealthReset]).Append(", bouts began ").Append(Moments[(int)TrainingRefillWhy.BoutBegan]).Append(", bouts ended ")
              .Append(Moments[(int)TrainingRefillWhy.BoutEnded]).Append("; fresh starts ").Append(Refills).Append(" (men refilled ").Append(MenRefilled)
              .Append(", you ").Append(YouRefilled).Append("), with a switch off ").Append(Off).Append("; your bar before them mean ")
              .Append(P0(YouRefilled > 0 && !double.IsNaN(YouBeforeMin) ? YouBeforeSum / YouRefilled : double.NaN)).Append(", lowest ").Append(P0(YouBeforeMin))
              .Append("; released ").Append(Released).Append(" (pauses, step backs, braces, sprints); errors ").Append(Errors);
            return sb.ToString();
        }

        public static string F1(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);

        public static string P0(double v) => double.IsNaN(v) ? "-" : (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
