using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 73 - LINE DISCIPLINE BY TIER (Toni 2026-10-20: 60 tier-5 men against 120 tier-1 recruits - the recruits came on a very broad
    /// front about 2 deep, wider than his whole army, and wrapped round him; "a low tier untrained troop cannot keep that disciplined line
    /// all the way and execute that encircling"; AI_NOTES "Step 73", DESIGN decision 67). The settings, read live: the master switch FIRST,
    /// the system's switch (every AI formation), the player's own switch (his formations and his order drag), the map tier → minimum ranks
    /// (T0 6, T1 5, T2 4, T3 3, T4 2, T5 1, T6 1), the tier heroes count as, ranged formations too, sieges, the preview cap, the on-screen
    /// line and how often the men's mean depth is recomputed.
    /// </summary>
    public readonly struct LineDisciplineRules
    {
        public LineDisciplineRules(bool modEnabled, bool enabled, bool forPlayer,
            double t0 = 6, double t1 = 5, double t2 = 4, double t3 = 3, double t4 = 2, double t5 = 1, double t6 = 1,
            int heroTier = 6, bool rangedToo = true, bool inSieges = false, bool previewCap = true, bool showMessage = true, double pollSeconds = 1)
        {
            ModEnabled = modEnabled;
            Enabled = enabled;
            ForPlayer = forPlayer;
            T0 = Ranks(t0);
            T1 = Ranks(t1);
            T2 = Ranks(t2);
            T3 = Ranks(t3);
            T4 = Ranks(t4);
            T5 = Ranks(t5);
            T6 = Ranks(t6);
            HeroTier = heroTier < 0 ? 0 : heroTier > LineDisciplineMath.MaxTier ? LineDisciplineMath.MaxTier : heroTier;
            RangedToo = rangedToo;
            InSieges = inSieges;
            PreviewCap = previewCap;
            ShowMessage = showMessage;
            PollSeconds = double.IsNaN(pollSeconds) ? 1 : pollSeconds < LineDisciplineMath.MinPollSeconds ? LineDisciplineMath.MinPollSeconds
                : pollSeconds > LineDisciplineMath.MaxPollSeconds ? LineDisciplineMath.MaxPollSeconds : pollSeconds;
        }

        public static LineDisciplineRules From(TraxSettings s) =>
            new LineDisciplineRules(s.ModEnabled, s.LineDisciplineEnabled, s.LineDisciplineForPlayer,
                s.LineDisciplineRanksT0, s.LineDisciplineRanksT1, s.LineDisciplineRanksT2, s.LineDisciplineRanksT3,
                s.LineDisciplineRanksT4, s.LineDisciplineRanksT5, s.LineDisciplineRanksT6,
                s.LineDisciplineHeroTier, s.LineDisciplineRangedToo, s.LineDisciplineInSieges, s.LineDisciplinePreviewCap,
                s.LineDisciplineShowMessage, s.LineDisciplinePollSeconds);

        public bool ModEnabled { get; }

        /// <summary>LineDisciplineEnabled - the system (every AI formation that counts).</summary>
        public bool Enabled { get; }

        /// <summary>LineDisciplineForPlayer - the player's own formations (the ones he orders) too.</summary>
        public bool ForPlayer { get; }

        /// <summary>The map: the minimum ranks a man of each tier holds (LineDisciplineRanksT0..T6), each 1-12.</summary>
        public double T0 { get; }
        public double T1 { get; }
        public double T2 { get; }
        public double T3 { get; }
        public double T4 { get; }
        public double T5 { get; }
        public double T6 { get; }

        /// <summary>LineDisciplineHeroTier: a hero (a lord, a companion, you) counts as a man of this tier.</summary>
        public int HeroTier { get; }

        /// <summary>LineDisciplineRangedToo: archers' and skirmishers' formations count (infantry always does).</summary>
        public bool RangedToo { get; }

        /// <summary>LineDisciplineInSieges: sieges and sally-outs too (the walls and streets set the width there - off by default).</summary>
        public bool InSieges { get; }

        /// <summary>LineDisciplinePreviewCap: the player's formation carries its cap as the game's own maximum width, so the order drag's
        /// preview stops growing at it.</summary>
        public bool PreviewCap { get; }

        /// <summary>LineDisciplineShowMessage: a short on-screen line when the player's line order asked wider than his men hold.</summary>
        public bool ShowMessage { get; }

        /// <summary>LineDisciplinePollSeconds: the men's mean depth is recomputed this often (and at once when the formation's count changes).</summary>
        public double PollSeconds { get; }

        /// <summary>The master switch FIRST, then the system's switch: every AI formation that counts.</summary>
        public bool On => ModEnabled && Enabled;

        /// <summary>The player's own formations: the system on AND his switch.</summary>
        public bool PlayerOn => On && ForPlayer;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Enabled ? "LineDisciplineEnabled" : null;

        /// <summary>The minimum ranks of a man of this tier (clamped 0-6).</summary>
        public double RanksOf(int tier)
        {
            switch (LineDisciplineMath.ClampTier(tier))
            {
                case 0: return T0;
                case 1: return T1;
                case 2: return T2;
                case 3: return T3;
                case 4: return T4;
                case 5: return T5;
                default: return T6;
            }
        }

        public string MapText()
        {
            var sb = new StringBuilder();
            for (int t = 0; t <= LineDisciplineMath.MaxTier; t++)
            {
                if (t > 0) sb.Append(" / ");
                sb.Append('T').Append(t).Append(' ').Append(LineDisciplineMath.F(RanksOf(t)));
            }
            return sb.ToString();
        }

        public string Describe()
        {
            string what = "line discipline by tier (step 73): an infantry" + (RangedToo ? ", archer or skirmisher" : "")
                          + " formation in a line, shield wall, loose or scatter arrangement holds no thinner than its men's mean minimum ranks ("
                          + MapText() + "; heroes as T" + HeroTier + ") - at most ceil(men / depth) files wide; cavalry, horse archers, squares, circles, "
                          + "columns and wedges untouched; " + (InSieges ? "sieges too" : "not in sieges") + ", never naval battles";
            if (!On) return what + " - OFF (" + OffBecause + "): the game's own widths";
            return what + " - ON for the AI; your own formations " + (ForPlayer
                ? "too (" + (PreviewCap ? "the order drag's preview stops at the cap" : "capped on the order, the preview not capped - LineDisciplinePreviewCap off")
                  + (ShowMessage ? ", a line on screen when you ask wider" : "") + ")"
                : "NOT (LineDisciplineForPlayer off)");
        }

        private static double Ranks(double v) =>
            double.IsNaN(v) ? 1 : v < LineDisciplineMath.MinRanks ? LineDisciplineMath.MinRanks : v > LineDisciplineMath.MaxRanks ? LineDisciplineMath.MaxRanks : v;
    }

    /// <summary>Step 73: a formation's arrangement as the line discipline sees it.</summary>
    public enum LineShape
    {
        Line,
        ShieldWall,
        Loose,
        Scatter,
        Column,
        Square,
        Circle,
        Skein,
        Other,
    }

    /// <summary>Step 73: the battle's kind.</summary>
    public enum LineMission
    {
        /// <summary>A field battle (custom battles, village raids, hideouts - anything not below).</summary>
        Field,

        /// <summary>A siege or a sally-out (LineDisciplineInSieges).</summary>
        Siege,

        /// <summary>A naval battle or raid (War Sails' decks) - never.</summary>
        Naval,
    }

    /// <summary>Step 73: why a formation is not held to its depth this look (None = it is).</summary>
    public enum LineSkip
    {
        None,
        ModOff,
        SystemOff,
        Naval,
        Siege,
        Empty,
        Shape,
        Mounted,
        Ranged,
        PlayerOff,
    }

    /// <summary>Step 73: what the game shows of one formation this look (the module fills it; the smoke stands it in).</summary>
    public struct LineFacts
    {
        /// <summary>The men in its arrangement (detached men - on a siege engine, a ladder - are not in the line).</summary>
        public int Men;

        /// <summary>The player's own (his team, not left to the AI) - LineDisciplineForPlayer.</summary>
        public bool PlayerOrdered;

        /// <summary>On the player's side (his team or an ally's) - the summary's side.</summary>
        public bool PlayerSide;

        public LineShape Shape;

        /// <summary>A cavalry or horse-archer formation (the game's own query).</summary>
        public bool Mounted;

        /// <summary>An archer / skirmisher formation (the game's own query).</summary>
        public bool Ranged;

        /// <summary>Its frontage now (m), the space between files (m) and a man's width (m) - the game's own.</summary>
        public double Width;
        public double Interval;
        public double Diameter;
    }

    /// <summary>
    /// Step 73's pure rule. A man's ranks = the map at his tier (the game's battle tier: ceil((level − 5) / 5), 0 to 6 - the campaign's
    /// troop tier; a 7 counts as 6; a hero as LineDisciplineHeroTier). The formation's minimum depth D = the MEAN of its men's ranks. Its
    /// line is at most F = ceil(men ÷ D) files wide (never fewer than the game's own minimum √men files, never fewer than 1), so its depth
    /// men ÷ F is D, short by less than one man a file: a fractional D gives a partly filled last rank (100 men at D 3.5 → 29 files, 3 full
    /// ranks + 13 men - the 4th rank 45 % full). The width of F files is the game's own: (F − 1) × (the interval + a man's width) + a
    /// man's width, at the formation's current spacing.
    /// </summary>
    public static class LineDisciplineMath
    {
        public const int MaxTier = 6;
        public const double MinRanks = 1;
        public const double MaxRanks = 12;
        public const double MinPollSeconds = 0.25;
        public const double MaxPollSeconds = 10;

        /// <summary>Added to a cap's width so the game's own floor ((w − d) ÷ (i + d) + 1e-5, + 1) gives exactly F files, not F − 1.</summary>
        public const double WidthSlackMetres = 0.05;

        public static int ClampTier(int tier) => tier < 0 ? 0 : tier > MaxTier ? MaxTier : tier;

        /// <summary>The tier a man counts as: a hero = LineDisciplineHeroTier, else his battle tier clamped to 0-6.</summary>
        public static int TierOf(int battleTier, bool hero, in LineDisciplineRules rules) => hero ? rules.HeroTier : ClampTier(battleTier);

        /// <summary>D = the mean of the men's ranks (1 for no men).</summary>
        public static double MinDepth(double sumRanks, int men) => men <= 0 ? 1 : Math.Max(MinRanks, sumRanks / men);

        /// <summary>ceil(men ÷ D), at least 1 (a hair under a whole number counts as it: 120 ÷ 5 = 24, not 25).</summary>
        public static int MaxFiles(int men, double depth)
        {
            if (men <= 0) return 1;
            if (double.IsNaN(depth) || depth < MinRanks) depth = MinRanks;
            return Math.Max(1, (int)Math.Ceiling(men / depth - 1e-9));
        }

        /// <summary>The game's own narrowest line (LineFormation.MinimumFileCount): √men files, rounded down, at least 1.</summary>
        public static int GameMinFiles(int men) => Math.Max(1, (int)Math.Sqrt(Math.Max(0, men)));

        /// <summary>The cap: <see cref="MaxFiles"/>, never below the game's own minimum (the game would not form narrower anyway).</summary>
        public static int CapFiles(int men, double depth) => Math.Max(MaxFiles(men, depth), GameMinFiles(men));

        /// <summary>The game's width of a line of this many files (LineFormation.FlankWidth).</summary>
        public static double Width(int files, double interval, double diameter) => Math.Max(0, files - 1) * (interval + diameter) + diameter;

        /// <summary>The width handed to the game for a cap of this many files (with the slack - the game floors it to exactly these files).</summary>
        public static double CapWidth(int files, double interval, double diameter) => Width(files, interval, diameter) + WidthSlackMetres;

        /// <summary>The game's own files of a width (LineFormation.GetFileCountFromWidth without its minimum).</summary>
        public static int FilesOf(double width, double interval, double diameter)
        {
            double step = interval + diameter;
            if (double.IsNaN(width) || step <= 0) return 1;
            return Math.Max(0, (int)((width - diameter) / step + 1e-5)) + 1;
        }

        /// <summary>The ranks a line of this many files has (the last one partly filled).</summary>
        public static int Ranks(int men, int files) => men <= 0 || files <= 0 ? 0 : (men + files - 1) / files;

        /// <summary>The men in the last rank.</summary>
        public static int LastRankMen(int men, int files)
        {
            int ranks = Ranks(men, files);
            return ranks == 0 ? 0 : men - (ranks - 1) * files;
        }

        /// <summary>The line's depth men ÷ files.</summary>
        public static double Depth(int men, int files) => files <= 0 ? 0 : (double)men / files;

        /// <summary>A line, shield wall, loose or scatter arrangement (squares, circles, columns, wedges are left out).</summary>
        public static bool Counts(LineShape shape) => shape == LineShape.Line || shape == LineShape.ShieldWall || shape == LineShape.Loose || shape == LineShape.Scatter;

        /// <summary>Is this formation held to its depth now? The master switch FIRST, the system, the battle, then the formation.</summary>
        public static LineSkip Verdict(in LineDisciplineRules rules, LineMission mission, in LineFacts f)
        {
            if (!rules.ModEnabled) return LineSkip.ModOff;
            if (!rules.Enabled) return LineSkip.SystemOff;
            if (mission == LineMission.Naval) return LineSkip.Naval;
            if (mission == LineMission.Siege && !rules.InSieges) return LineSkip.Siege;
            if (f.Men <= 0) return LineSkip.Empty;
            if (!Counts(f.Shape)) return LineSkip.Shape;
            if (f.Mounted) return LineSkip.Mounted;
            if (f.Ranged && !rules.RangedToo) return LineSkip.Ranged;
            if (f.PlayerOrdered && !rules.ForPlayer) return LineSkip.PlayerOff;
            return LineSkip.None;
        }

        /// <summary>"5 ranks, the last 13 of 29" - a line's shape in words.</summary>
        public static string ShapeText(int men, int files)
        {
            int ranks = Ranks(men, files);
            int last = LastRankMen(men, files);
            if (ranks <= 0) return "empty";
            return files + " files x " + ranks + " ranks" + (last < files ? " (the last " + last + " of " + files + ")" : "") + ", " + F(Depth(men, files)) + " deep";
        }

        public static string F(double v) => v.ToString("0.0#", CultureInfo.InvariantCulture);

        public static string M(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 73: why the line discipline narrowed a formation.</summary>
    public enum LineClampCause
    {
        /// <summary>Its width grew since the last look past the cap - an order, the AI's behaviour (FormOrder Wide, envelop, the line spreading).</summary>
        Widened,

        /// <summary>Its width did not grow but the cap fell - losses, a man moved out, the mean depth changed: the line shrinks back.</summary>
        AutoShrink,
    }

    /// <summary>Step 73: one formation's record for the summary (the first time it was capped and the widest asked).</summary>
    public sealed class LineRecord
    {
        public string Name = "";
        public bool PlayerOrdered;
        public int MenAtFirstCap;
        public double DepthAtFirstCap;
        public int CapFilesAtFirstCap;
        public double CapWidthAtFirstCap;
        public int WidestAskedFiles;
        public double WidestAskedWidth;
        public int Clamps;
        public int AutoShrinks;
        public int LastMen;
        public double LastDepth;
        public int LastCapFiles;
    }

    /// <summary>Step 73's numbers for one side of the battle (the player's / the other).</summary>
    public sealed class LineSideStats
    {
        public int Counted;
        public int Capped;
        public int AiWidenClamps;
        public int PlayerWidenClamps;
        public int AiAutoShrinks;
        public int PlayerAutoShrinks;
        public int Errors;
        public readonly List<LineRecord> Records = new List<LineRecord>();
    }

    /// <summary>Step 73's measurement - the [summary] "line discipline" lines.</summary>
    public sealed class LineDisciplineStats
    {
        public const int MaxRecordsShown = 6;

        public readonly LineSideStats Player = new LineSideStats();
        public readonly LineSideStats Other = new LineSideStats();

        /// <summary>The preview cap: the player's formations given the capped arrangement, its re-installs after the game swapped its own
        /// back in (a new arrangement order), the ones given back, the times the game asked the capped arrangement for its maximum width and
        /// got the cap (the order drag's preview and the move both ask), and the arrangements the install had to skip (not the game's plain line).</summary>
        public int PreviewInstalls;
        public int PreviewReinstalls;
        public int PreviewRemovals;
        public long PreviewCapReads;
        public int PreviewSkipped;

        /// <summary>The player's line orders (a click or a drag): seen, asked wider than the cap, came out at the cap (the preview / the move
        /// held it), still too wide after the order (the next look clamped it), the on-screen lines shown.</summary>
        public int PlayerLineOrders;
        public int PlayerAskedWider;
        public int PlayerOrderAtCap;
        public int PlayerOrderTooWide;
        public int Messages;

        /// <summary>Widths given back when the switch went off (the game's own form order restored).</summary>
        public int GivenBack;

        /// <summary>Looks where the game had not taken our last clamp yet (retried after a second).</summary>
        public int ClampsNotTaken;

        public long Ticks;
        public double TickMs;
        public double MaxTickMs;
        public int Errors;

        public LineSideStats Side(bool playerSide) => playerSide ? Player : Other;

        public IList<string> SummaryLines(in LineDisciplineRules rules)
        {
            var lines = new List<string>();
            string state = rules.On ? "ON" + (rules.ForPlayer ? "" : " for the AI only (LineDisciplineForPlayer off)") : "OFF (" + rules.OffBecause + ")";
            lines.Add("line discipline (step 73): " + state + " at the end - the map " + rules.MapText() + ", heroes as T" + rules.HeroTier
                      + (rules.RangedToo ? ", ranged formations too" : ", infantry only") + (rules.InSieges ? ", sieges too" : "")
                      + "; looks " + Ticks + ", cost avg " + Ms(Ticks > 0 ? TickMs / Ticks : 0) + " ms a tick (max " + Ms(MaxTickMs) + "), clamps the game had not taken yet " + ClampsNotTaken + ", errors " + Errors);
            lines.Add(SideLine("your side", Player, true));
            lines.Add(SideLine("the other side", Other, false));
            lines.Add("line discipline - your orders (step 73): " + PlayerLineOrders + " line orders (a click or a drag), " + PlayerAskedWider
                      + " asked wider than the men hold → " + PlayerOrderAtCap + " came out at the cap, " + PlayerOrderTooWide + " still too wide (clamped at the next look); "
                      + Messages + " on-screen lines; PREVIEW " + PreviewVerdict() + " (capped arrangement given " + PreviewInstalls + " times, again after the game's own "
                      + PreviewReinstalls + ", given back " + PreviewRemovals + ", skipped " + PreviewSkipped + " - not the game's plain line; the game asked it for its maximum width and got the cap "
                      + PreviewCapReads + " times); widths given back at a switch-off " + GivenBack);
            return lines;
        }

        /// <summary>"capped" when the game read the cap, else why not.</summary>
        public string PreviewVerdict()
        {
            if (PreviewCapReads > 0) return "CAPPED (the drag stops growing at the cap)";
            if (PreviewInstalls == 0) return "not installed (no formation of yours needed it, or the switch is off)";
            return "installed but never read at a cap (no drag wider than the cap yet)";
        }

        private static string SideLine(string who, LineSideStats s, bool player)
        {
            var sb = new StringBuilder();
            sb.Append("line discipline, ").Append(who).Append(" (step 73): ").Append(s.Counted).Append(" formations counted, ").Append(s.Capped).Append(" capped");
            int shown = 0;
            foreach (var r in s.Records)
            {
                if (shown == MaxRecordsShown)
                {
                    sb.Append("; … ").Append(s.Records.Count - shown).Append(" more");
                    break;
                }
                sb.Append(shown == 0 ? " - " : "; ");
                sb.Append(r.Name).Append(r.PlayerOrdered ? " (yours)" : "").Append(": ").Append(r.MenAtFirstCap).Append(" men, D ").Append(LineDisciplineMath.F(r.DepthAtFirstCap))
                  .Append(" → max ").Append(r.CapFilesAtFirstCap).Append(" files ").Append(LineDisciplineMath.M(r.CapWidthAtFirstCap)).Append(" m")
                  .Append(" vs asked up to ").Append(r.WidestAskedFiles).Append(" files ").Append(LineDisciplineMath.M(r.WidestAskedWidth)).Append(" m")
                  .Append(", ").Append(r.Clamps).Append(" clamps (").Append(r.AutoShrinks).Append(" auto-shrinks); at the end ").Append(r.LastMen).Append(" men D ")
                  .Append(LineDisciplineMath.F(r.LastDepth)).Append(" max ").Append(r.LastCapFiles).Append(" files");
                shown++;
            }
            sb.Append(" | clamps applied: by the AI's own widening ").Append(s.AiWidenClamps).Append(", on your orders ").Append(s.PlayerWidenClamps)
              .Append(" | auto-shrinks (losses / the depth changed): AI ").Append(s.AiAutoShrinks).Append(", yours ").Append(s.PlayerAutoShrinks)
              .Append(" | errors ").Append(s.Errors);
            if (!player && (s.PlayerWidenClamps != 0 || s.PlayerAutoShrinks != 0)) sb.Append(" (the other side has no formation of yours - must be 0)");
            return sb.ToString();
        }

        private static string Ms(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
