using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 54 (IMPACT I7, §8 q14 - AI_NOTES "Step 54"): THE AI PUNISHES A WINDOW - the rules, read live. A window = a man REELING or DOWN
    /// from a live impact outcome (his <see cref="ImpactState"/>) + the grace. Windows exist only while an impact family really applies
    /// (the master switch FIRST, Athletics, ImpactEnabled, not shadow, not the spike, a family on) - <see cref="WindowsLive"/>; the
    /// measurement runs then, with the punish ON or OFF, so the two compare. <see cref="Enabled"/> = + ImpactPunishEnabled: the AI presses.
    /// </summary>
    public readonly struct PunishRules
    {
        public PunishRules(bool modEnabled, bool athleticsEnabled, bool impactEnabled, bool shadow, bool spike, bool anyFamily, bool punishEnabled,
            int percent, double graceSeconds, double rangeMetres)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            ImpactEnabled = impactEnabled;
            Shadow = shadow;
            Spike = spike;
            AnyFamily = anyFamily;
            PunishEnabled = punishEnabled;
            Percent = percent < 0 ? 0 : percent > PunishMath.MaxPercent ? PunishMath.MaxPercent : percent;
            GraceSeconds = double.IsNaN(graceSeconds) || graceSeconds < 0 ? 0 : graceSeconds;
            RangeMetres = double.IsNaN(rangeMetres) || rangeMetres < 0 ? 0 : rangeMetres;
        }

        public static PunishRules From(TraxSettings s) => new PunishRules(s.ModEnabled, s.AthleticsEnabled, s.ImpactEnabled, s.ImpactShadowMode, s.ImpactSpike,
            s.ImpactKicksEnabled || s.ImpactBlocksEnabled || s.ImpactHitsEnabled || s.ImpactRiposteEnabled, s.ImpactPunishEnabled,
            s.ImpactPunishAggressionPercent, s.ImpactPunishGraceSeconds, s.ImpactPunishRangeMetres);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }
        public bool ImpactEnabled { get; }
        public bool Shadow { get; }
        public bool Spike { get; }
        public bool AnyFamily { get; }
        public bool PunishEnabled { get; }

        /// <summary>The aggression share in a window, % of the game's AI (0-200).</summary>
        public int Percent { get; }

        public double GraceSeconds { get; }
        public double RangeMetres { get; }

        /// <summary>Windows really happen (an impact family applies - never in shadow / the spike): the master switch FIRST.</summary>
        public bool WindowsLive => ModEnabled && AthleticsEnabled && ImpactEnabled && !Shadow && !Spike && AnyFamily;

        /// <summary>The AI presses the windows.</summary>
        public bool Enabled => WindowsLive && PunishEnabled;

        /// <summary>The share in a window, in 0.05 steps (<see cref="FeverMath.Quantize"/>).</summary>
        public double Share => FeverMath.Quantize(Percent / 100.0);

        /// <summary>Why the AI does not press (the master switch first), or null when it does.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !ImpactEnabled ? "ImpactEnabled"
            : Shadow ? "ImpactShadowMode" : Spike ? "ImpactSpike" : !AnyFamily ? "every impact family off" : !PunishEnabled ? "ImpactPunishEnabled" : null;

        /// <summary>The settings in one sentence (the mission-start line, the summary).</summary>
        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ")" + (WindowsLive ? " - the windows are still measured" : string.Empty);
            return "ON - an AI man on foot whose enemy reels or lies down (a live impact outcome, you too) decides to attack at " + Percent
                   + "% of the game's AI (never lower than his own share) until the window closes + " + PunishMath.F1(GraceSeconds)
                   + " s; a timid man within " + PunishMath.F1(RangeMetres) + " m steps in for it; never you, never a rider";
        }
    }

    /// <summary>Step 54 pure: the window, the raised share, the timid man's press.</summary>
    public static class PunishMath
    {
        /// <summary>The percent's upper bound (as the fever's).</summary>
        public const int MaxPercent = 200;

        /// <summary>Plumbing: the battle-wide "any window open" mark runs this long past the last window's end (a measured end may come a
        /// little after the asked one).</summary>
        public const double AnyWindowSlackSeconds = 1.0;

        /// <summary>The end of his reeling / time on the ground (0 = never happened).</summary>
        public static double WindowEnd(in ImpactState s) => Math.Max(s.ReelingUntil, s.DownUntil);

        /// <summary>He is in a window at <paramref name="now"/>: reeling or down, or within the grace after.</summary>
        public static bool InWindow(in ImpactState s, double now, double grace)
        {
            double end = WindowEnd(in s);
            return end > 0 && now < end + (grace > 0 ? grace : 0);
        }

        /// <summary>The factor the decorator writes: his own share raised to the window's (never lowered); the player, not punishing or the
        /// rules off = his own untouched.</summary>
        public static float Raise(float factor, in PunishRules r, bool punishing, bool isPlayer)
        {
            if (isPlayer || !punishing || !r.Enabled) return factor;
            float p = (float)r.Share;
            return p > factor ? p : factor;
        }

        /// <summary>A TIMID man presses the window like a surge: the rules on, a range above 0, the target within it, not in a panic.</summary>
        public static bool TimidPresses(in PunishRules r, bool panic, double distance) =>
            r.Enabled && r.RangeMetres > 0 && !panic && !double.IsNaN(distance) && distance <= r.RangeMetres;

        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 54 per mission: the windows (opened on AI men / on you, seen by an AI enemy in contact, PUNISHED - an AI melee attack started
    /// inside -, wasted), THE PROOF - AI melee attacks per man-minute in contact against a target in a window vs not, with the punish ON and
    /// OFF -, the presses (raised, ended, timid men sent in), the target reads and the look's cost + the [summary] "impact punish" lines.
    /// Main thread.
    /// </summary>
    public sealed class PunishStats
    {
        /// <summary>The proof's rows: the punish on / off at the look; the columns: the target in a window / not.</summary>
        public const int Off = 0, On = 1, NotInWindow = 0, InWindow = 1;

        private readonly double[,] _manSeconds = new double[2, 2];
        private readonly int[,] _attacks = new int[2, 2];

        public int WindowsOnAi, WindowsOnPlayer, SeenOnAi, SeenOnPlayer, PunishedOnAi, PunishedOnPlayer;
        public int Raised, Ended, TimidPresses, PlayerAttacksInWindow;
        public long TargetReads, Looks;
        public double LookCostMs;
        public bool EverLive;

        public void AddTime(bool on, bool inWindow, double seconds)
        {
            if (!(seconds > 0)) return;
            _manSeconds[on ? On : Off, inWindow ? InWindow : NotInWindow] += seconds;
        }

        public void AddAttack(bool on, bool inWindow) => _attacks[on ? On : Off, inWindow ? InWindow : NotInWindow]++;

        public double ManSeconds(bool on, bool inWindow) => _manSeconds[on ? On : Off, inWindow ? InWindow : NotInWindow];

        public int Attacks(bool on, bool inWindow) => _attacks[on ? On : Off, inWindow ? InWindow : NotInWindow];

        /// <summary>Attacks per man-minute (NaN without time).</summary>
        public double Rate(bool on, bool inWindow)
        {
            double s = ManSeconds(on, inWindow);
            return s > 0 ? Attacks(on, inWindow) * 60.0 / s : double.NaN;
        }

        public void Opened(bool onPlayer)
        {
            if (onPlayer) WindowsOnPlayer++;
            else WindowsOnAi++;
        }

        public void Seen(bool onPlayer)
        {
            if (onPlayer) SeenOnPlayer++;
            else SeenOnAi++;
        }

        public void Punished(bool onPlayer)
        {
            if (onPlayer) PunishedOnPlayer++;
            else PunishedOnAi++;
        }

        private static string Rate(double attacks, double seconds) =>
            seconds > 0 ? PunishMath.F2(attacks * 60.0 / seconds) + " (" + attacks.ToString(CultureInfo.InvariantCulture) + " attacks / " + PunishMath.F2(seconds / 60.0) + " man-min)"
                        : "- (no time)";

        private string Row(bool on) =>
            "target reeling / down " + Rate(Attacks(on, true), ManSeconds(on, true)) + ", target not " + Rate(Attacks(on, false), ManSeconds(on, false));

        /// <summary>The [summary] lines (one when windows were never live).</summary>
        public void AddSummaryLines(List<string> lines, in PunishRules r)
        {
            lines.Add("impact punish settings (step 54): " + r.Describe());
            if (!EverLive)
            {
                lines.Add("impact punish: no window this battle could be live (impact off, in shadow or the spike) - nothing measured");
                return;
            }
            int opened = WindowsOnAi + WindowsOnPlayer;
            int punished = PunishedOnAi + PunishedOnPlayer;
            lines.Add("impact punish windows: opened " + opened + " (on AI men " + WindowsOnAi + ", on you " + WindowsOnPlayer + ") - seen by an AI enemy in contact "
                      + (SeenOnAi + SeenOnPlayer) + " (on you " + SeenOnPlayer + "), PUNISHED (an AI melee attack started inside) " + punished + " (on you "
                      + PunishedOnPlayer + " of " + WindowsOnPlayer + "), wasted " + (opened - punished) + (opened > 0 ? " (" + PunishMath.F1(100.0 * punished / opened) + "% punished)" : string.Empty));
            lines.Add("impact punish proof: AI melee attacks per man-minute in contact (on foot) - punish ON: " + Row(true) + "; punish OFF: " + Row(false));
            lines.Add("impact punish men: raised to the window's share " + Raised + " times, ended " + Ended + ", timid men sent in " + TimidPresses
                      + "; AI attacks inside a window on you " + PlayerAttacksInWindow + "; target reads " + TargetReads + " in " + Looks + " looks, cost "
                      + PunishMath.F2(LookCostMs) + " ms");
        }
    }
}
