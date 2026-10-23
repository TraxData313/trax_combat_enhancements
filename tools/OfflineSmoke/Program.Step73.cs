using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 73 (Toni 2026-10-20: 120 tier-1 recruits came at his 60 tier-5 men on a front ~2 deep and wrapped round him): LINE DISCIPLINE BY
    /// TIER through the real <see cref="LineDiscipline"/> with a stand-in world of formations (the game's own floor of a width to files, its
    /// FormOrder Wide, the order drag clamped to the formation's maximum width), and the REAL <see cref="DisciplinedLineFormation"/> - the
    /// game's own LineFormation code - over a stand-in owner. What it cannot check: the engine's formations, the AI's behaviours, the real
    /// order placer's drag (PLAYTEST D38).
    /// </summary>
    internal static partial class Program
    {
        private const double LineInterval = 0.6;
        private const double LineDiameter = 0.4;

        private sealed class FakeLine
        {
            public string Name = "a line";
            public readonly List<int> Tiers = new List<int>(); // −1 = a hero
            public double Width;
            public LineShape Shape = LineShape.Line;
            public bool Mounted, Ranged, PlayerOrdered, PlayerSide;
            public bool Custom;
            public double Saved = double.NaN;
            public bool Refuses;
            public bool Preview, EverPreview, Plain = true;
            public double PreviewCap = double.NaN;
            public int Clamps;

            public int Files => LineDisciplineMath.FilesOf(Width, LineInterval, LineDiameter);

            public FakeLine Men(int tier, int count)
            {
                for (int i = 0; i < count; i++) Tiers.Add(tier);
                return this;
            }

            /// <summary>The game's own FormOrder Wide (FormOrder.GetMaxFileCountAux): round(√(men ÷ 16)) × 16 files, at most one man a file.</summary>
            public FakeLine Wide()
            {
                int files = Math.Max((int)Math.Round(Math.Sqrt(Tiers.Count / 16.0), MidpointRounding.ToEven), 1) * 16;
                files = Math.Min(files, Math.Max(1, Tiers.Count));
                Width = LineDisciplineMath.Width(files, LineInterval, LineDiameter);
                Custom = false;
                return this;
            }

            public FakeLine FilesWide(int files)
            {
                Width = LineDisciplineMath.Width(files, LineInterval, LineDiameter);
                return this;
            }

            /// <summary>The game's order drag (OrderController's simulation): the dragged width, clamped to the formation's maximum width.</summary>
            public void Drag(double asked)
            {
                double w = Preview && !double.IsNaN(PreviewCap) ? Math.Min(asked, PreviewCap) : asked;
                Width = LineDisciplineMath.Width(LineDisciplineMath.FilesOf(w, LineInterval, LineDiameter), LineInterval, LineDiameter);
                Custom = true;
            }

            public void Lose(int count) => Tiers.RemoveRange(Tiers.Count - count, count);
        }

        private sealed class FakeLineWorld : ILineWorld
        {
            public readonly List<FakeLine> Lines = new List<FakeLine>();
            public readonly List<string> Messages = new List<string>();
            public LineMission Mission { get; set; } = LineMission.Field;
            public long PreviewCapReads { get; set; }
            public int SkippedSignals;

            public int BeginLook() => Lines.Count;

            public bool Read(int slot, ref LineFacts facts)
            {
                if (slot < 0 || slot >= Lines.Count) return false;
                var l = Lines[slot];
                if (l.Tiers.Count == 0) return false;
                facts.Men = l.Tiers.Count;
                facts.PlayerOrdered = l.PlayerOrdered;
                facts.PlayerSide = l.PlayerSide;
                facts.Shape = l.Shape;
                facts.Mounted = l.Mounted;
                facts.Ranged = l.Ranged;
                facts.Width = l.Width;
                facts.Interval = LineInterval;
                facts.Diameter = LineDiameter;
                return true;
            }

            public void Ranks(int slot, in LineDisciplineRules rules, int[] tiers, out double sum, out int men)
            {
                sum = 0;
                men = 0;
                foreach (int t in Lines[slot].Tiers)
                {
                    bool hero = t < 0;
                    int tier = LineDisciplineMath.TierOf(hero ? 0 : t, hero, in rules);
                    sum += rules.RanksOf(tier);
                    men++;
                    tiers[hero ? LineDisciplineMath.MaxTier + 1 : tier]++;
                }
            }

            public bool Clamp(int slot, double width)
            {
                var l = Lines[slot];
                l.Clamps++;
                if (l.Refuses) return false;
                if (!l.Custom || double.IsNaN(l.Saved)) l.Saved = l.Width;
                l.Custom = true;
                l.Width = LineDisciplineMath.Width(LineDisciplineMath.FilesOf(width, LineInterval, LineDiameter), LineInterval, LineDiameter);
                return true;
            }

            public bool GiveBack(int slot)
            {
                var l = Lines[slot];
                if (double.IsNaN(l.Saved)) return false;
                l.Width = l.Saved;
                l.Saved = double.NaN;
                l.Custom = false;
                return true;
            }

            public PreviewChange SetPreview(int slot, double capWidth)
            {
                var l = Lines[slot];
                if (double.IsNaN(capWidth))
                {
                    if (!l.Preview) return PreviewChange.None;
                    l.Preview = false;
                    l.EverPreview = false;
                    return PreviewChange.Removed;
                }
                if (l.Preview)
                {
                    l.PreviewCap = capWidth;
                    return PreviewChange.None;
                }
                if (!l.Plain)
                {
                    if (SkippedSignals++ > 0) return PreviewChange.None;
                    return PreviewChange.Skipped;
                }
                l.Preview = true;
                l.PreviewCap = capWidth;
                bool again = l.EverPreview;
                l.EverPreview = true;
                return again ? PreviewChange.Reinstalled : PreviewChange.Installed;
            }

            public string Name(int slot) => Lines[slot].Name;

            public void Show(string text) => Messages.Add(text);
        }

        private static void Step73Defaults()
        {
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.LineDisciplineEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.LineDisciplineForPlayer, true, SettingSources.File);
            double[] map = { 6, 5, 4, 3, 2, 1, 1 };
            var keys = new[]
            {
                SettingsSchema.LineDisciplineRanksT0, SettingsSchema.LineDisciplineRanksT1, SettingsSchema.LineDisciplineRanksT2, SettingsSchema.LineDisciplineRanksT3,
                SettingsSchema.LineDisciplineRanksT4, SettingsSchema.LineDisciplineRanksT5, SettingsSchema.LineDisciplineRanksT6,
            };
            for (int i = 0; i < keys.Length; i++) S.Set(keys[i], map[i], SettingSources.File);
            S.Set(SettingsSchema.LineDisciplineHeroTier, 6, SettingSources.File);
            S.Set(SettingsSchema.LineDisciplineRangedToo, true, SettingSources.File);
            S.Set(SettingsSchema.LineDisciplineInSieges, false, SettingSources.File);
            S.Set(SettingsSchema.LineDisciplinePreviewCap, true, SettingSources.File);
            S.Set(SettingsSchema.LineDisciplineShowMessage, true, SettingSources.File);
            S.Set(SettingsSchema.LineDisciplinePollSeconds, 1.0, SettingSources.File);
            S.Set(SettingsSchema.LineMinWidthToDepthRatio, 0.0, SettingSources.File); // step 83's small-unit cap off: step 73 as built
        }

        private static string M(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static void LineDisciplineThroughTheLogic()
        {
            Step73Defaults();
            var w = new FakeLineWorld();
            // Toni's battle: the enemy's 120 recruits on the AI's advance (FormOrder Wide = 48 files, 2.5 deep)
            var recruits = new FakeLine { Name = "the enemy's Infantry" }.Men(1, 120).Wide();
            // his 60 tier-5 men, his own (the game's Wide for 60 = 32 files)
            var veterans = new FakeLine { Name = "your Infantry", PlayerOrdered = true, PlayerSide = true }.Men(5, 60).Wide();
            // his own recruits, a second unit of his
            var mine = new FakeLine { Name = "your Skirmisher", PlayerOrdered = true, PlayerSide = true, Ranged = true }.Men(1, 60).FilesWide(12);
            // left alone: cavalry, a square, a column
            var horse = new FakeLine { Name = "the enemy's Cavalry", Mounted = true }.Men(0, 40).FilesWide(40);
            var square = new FakeLine { Name = "the enemy's HeavyInfantry", Shape = LineShape.Square }.Men(0, 40).FilesWide(40);
            var column = new FakeLine { Name = "the ally's Infantry", Shape = LineShape.Column, PlayerSide = true }.Men(0, 40).FilesWide(40);
            // the mixed unit: 70 T1 + 30 T3 + 20 T5 → D 3.83 → 32 files, 3 ranks + 24
            var mixed = new FakeLine { Name = "the enemy's Ranged", Ranged = true }.Men(1, 70).Men(3, 30).Men(5, 20).FilesWide(64);
            w.Lines.AddRange(new[] { recruits, veterans, mine, horse, square, column, mixed });
            Check(recruits.Files == 48 && veterans.Files == 32, "precondition: the game's Wide gives " + recruits.Files + " / " + veterans.Files + " files");
            var lines = new LineDiscipline(w);
            lines.Start();
            double t = 10;

            // 1. the first look: the recruits narrowed to 24 files (5 deep) at once, the veterans untouched, the mixed unit to 32
            lines.Tick(t);
            Check(recruits.Files == 24, "step 73: the recruits' 48-file line was not narrowed to 24 (" + recruits.Files + ")");
            Check(LineDisciplineMath.Ranks(120, recruits.Files) == 5, "step 73: the recruits are not 5 deep");
            Check(veterans.Files == 32 && veterans.Clamps == 0, "step 73: the veterans (D 1) were touched");
            Check(mixed.Files == 32 && LineDisciplineMath.LastRankMen(120, mixed.Files) == 24, "step 73: the mixed unit is " + mixed.Files + " files, not 32 (3 ranks + 24)");
            Check(horse.Clamps == 0 && square.Clamps == 0 && column.Clamps == 0 && horse.Files == 40 && square.Files == 40, "step 73: cavalry / a square / a column touched");
            Check(mine.Files == 12 && mine.Clamps == 0, "step 73: your 12-file recruits (inside their 12-file cap) touched");
            Check(mine.Preview && !veterans.Preview, "step 73: the capped arrangement not given to your recruits only (D 1 needs none)");
            Check(Math.Abs(mine.PreviewCap - LineDisciplineMath.CapWidth(12, LineInterval, LineDiameter)) < 1e-9, "step 73: your recruits' cap is " + mine.PreviewCap);
            var other = lines.Stats.Other;
            Check(other.AiWidenClamps == 2 && other.AiAutoShrinks == 0, "step 73: the first look's AI clamps " + other.AiWidenClamps + " / shrinks " + other.AiAutoShrinks);

            // 2. nothing changes: no clamp, the depth not recounted before its second
            int clamps = recruits.Clamps;
            for (int k = 0; k < 30; k++) lines.Tick(t += 1 / 60.0);
            Check(recruits.Clamps == clamps && recruits.Files == 24, "step 73: a narrowed line clamped again with nothing changed");

            // 3. the recruits lose 20 men: the cap falls to 20 files - AUTO-SHRINK at once (the count changed: recounted now)
            recruits.Lose(20);
            lines.Tick(t += 1 / 60.0);
            Check(recruits.Files == 20, "step 73: the recruits' line did not shrink back to 20 files after losses (" + recruits.Files + ")");
            Check(other.AiAutoShrinks == 1, "step 73: the shrink not counted as an auto-shrink");

            // 4. the AI's behaviour widens again (Wide at 100 men: 32 files): narrowed again, a widening clamp
            recruits.Wide();
            Check(recruits.Files == 32, "precondition: Wide at 100 = " + recruits.Files);
            lines.Tick(t += 1 / 60.0);
            Check(recruits.Files == 20 && other.AiWidenClamps == 3, "step 73: the AI's second widening not narrowed (" + recruits.Files + " files, widen clamps " + other.AiWidenClamps + ")");

            // 5. the mean depth changes as men of another tier fall: the mixed unit loses its 20 veterans → D 4.43, 100 men → 23 files
            mixed.Lose(20);
            lines.Tick(t += 1 / 60.0);
            Check(mixed.Files == 23, "step 73: the mixed unit after losing its veterans is " + mixed.Files + " files, not 23 (D 4.4)");

            // 6. YOUR drag: 40 m asked for your 60 recruits (D 5 → 12 files, 7.05 m): the drag stops at the cap, the order keeps to it, a line on screen
            double asked = 40;
            mine.Drag(asked);
            Check(mine.Files == 12, "step 73: the drag with the capped maximum width gave " + mine.Files + " files, not the cap 12");
            lines.NotePlayerLineOrder(2, asked, t += 0.1);
            Check(lines.Stats.PlayerLineOrders == 1 && lines.Stats.PlayerAskedWider == 1 && lines.Stats.PlayerOrderAtCap == 1, "step 73: your capped drag not counted at the cap");
            Check(w.Messages.Count == 1 && w.Messages[0] == "your Skirmisher: these men hold no thinner than 5 deep - 12 men wide at most", "step 73: the on-screen line: " + string.Join(" | ", w.Messages));
            lines.Tick(t += 1 / 60.0);
            Check(mine.Clamps == 0, "step 73: a capped drag clamped after");

            // 7. the preview cap off: the drag shows any width, the line is narrowed at the next look (a player clamp), said once in 2 s
            S.Set(SettingsSchema.LineDisciplinePreviewCap, false, SettingSources.Mcm);
            lines.Tick(t += 1 / 60.0);
            Check(!mine.Preview, "step 73: LineDisciplinePreviewCap off did not take the capped arrangement out");
            mine.Drag(asked);
            Check(mine.Files > 12, "precondition: the uncapped drag is " + mine.Files + " files");
            lines.NotePlayerLineOrder(2, asked, t += 0.1);
            Check(lines.Stats.PlayerOrderTooWide == 1, "step 73: an uncapped drag not counted too wide");
            lines.Tick(t += 1 / 60.0);
            Check(mine.Files == 12 && lines.Stats.Player.PlayerWidenClamps == 1, "step 73: your too-wide line not narrowed at the next look (" + mine.Files + ")");
            Check(w.Messages.Count == 1, "step 73: the on-screen line repeated within 2 s");
            S.Set(SettingsSchema.LineDisciplinePreviewCap, true, SettingSources.Mcm);
            lines.Tick(t += 1 / 60.0);
            Check(mine.Preview, "step 73: the preview cap not back");

            // 8. the game's arrangement order puts its own plain line back: ours again at the next look (a re-install)
            mine.Preview = false;
            lines.Tick(t += 1 / 60.0);
            Check(mine.Preview && lines.Stats.PreviewReinstalls == 1, "step 73: the capped arrangement not put in again after the game's own");

            // 9. YOUR switch off: your lines let go (the clamped width given back, the preview out), the AI's kept
            S.Set(SettingsSchema.LineDisciplineForPlayer, false, SettingSources.Mcm);
            lines.Tick(t += 1 / 60.0);
            Check(!mine.Preview, "step 73: LineDisciplineForPlayer off kept the capped arrangement");
            Check(recruits.Files == 20, "step 73: LineDisciplineForPlayer off let the AI's line go");
            mine.Drag(asked);
            lines.Tick(t += 1 / 60.0);
            Check(mine.Files > 12, "step 73: LineDisciplineForPlayer off still narrowed your line");
            S.Set(SettingsSchema.LineDisciplineForPlayer, true, SettingSources.Mcm);
            lines.Tick(t += 1 / 60.0);
            Check(mine.Files == 12 && mine.Preview, "step 73: your switch back on did not hold your line again");

            // 10. a clamp the game does not take: retried after a second, not every frame; a widening right after a TAKEN clamp is narrowed at once
            var stuckWorld = new FakeLineWorld();
            var stuck = new FakeLine { Name = "stuck" }.Men(1, 120).Wide();
            stuck.Refuses = true;
            stuckWorld.Lines.Add(stuck);
            var stuckLines = new LineDiscipline(stuckWorld);
            for (int k = 0; k <= 60; k++) stuckLines.Tick(k / 60.0);
            Check(stuck.Clamps == 2 && stuckLines.Stats.ClampsNotTaken == 2, "step 73: a clamp the game did not take was tried " + stuck.Clamps + " times in a second (expected 2)");
            stuck.Refuses = false;
            stuckLines.Tick(2.5);
            Check(stuck.Files == 24, "step 73: the clamp not taken once the game could");
            stuck.Wide();
            stuckLines.Tick(2.5 + 1 / 60.0);
            Check(stuck.Files == 24, "step 73: a re-widening right after a taken clamp waited");

            // 11. not in a naval battle, not in a siege (by default); a siege with the switch: yes
            var naval = new FakeLineWorld { Mission = LineMission.Naval };
            var navalLine = new FakeLine { Name = "naval" }.Men(1, 120).Wide();
            naval.Lines.Add(navalLine);
            var nl = new LineDiscipline(naval);
            nl.Tick(1);
            Check(navalLine.Files == 48 && navalLine.Clamps == 0, "step 73: a naval battle's line narrowed");
            var siege = new FakeLineWorld { Mission = LineMission.Siege };
            var siegeLine = new FakeLine { Name = "siege" }.Men(1, 120).Wide();
            siege.Lines.Add(siegeLine);
            var sl = new LineDiscipline(siege);
            sl.Tick(1);
            Check(siegeLine.Files == 48, "step 73: a siege's line narrowed with LineDisciplineInSieges off");
            S.Set(SettingsSchema.LineDisciplineInSieges, true, SettingSources.Mcm);
            sl.Tick(2);
            Check(siegeLine.Files == 24, "step 73: LineDisciplineInSieges on did not narrow the siege's line");
            S.Set(SettingsSchema.LineDisciplineInSieges, false, SettingSources.Mcm);

            // 12. a formation whose arrangement is not the game's plain line: skipped once, said once
            var odd = new FakeLine { Name = "your HorseArcher", PlayerOrdered = true, PlayerSide = true, Plain = false }.Men(1, 60).FilesWide(60);
            w.Lines.Add(odd);
            lines.Tick(t += 1 / 60.0);
            lines.Tick(t += 1 / 60.0);
            Check(lines.Stats.PreviewSkipped == 1 && odd.Files == 12, "step 73: a non-plain arrangement - skipped " + lines.Stats.PreviewSkipped + ", " + odd.Files + " files");

            // 13. the summary
            w.PreviewCapReads = 42;
            var summary = lines.SummaryLines();
            Check(summary.Count == 4, "step 73: " + summary.Count + " summary lines");
            Check(summary[0].StartsWith("line discipline (step 73): ON at the end - the map T0 6.0 / T1 5.0 / T2 4.0 / T3 3.0 / T4 2.0 / T5 1.0 / T6 1.0, heroes as T6, ranged formations too", StringComparison.Ordinal), "step 73: " + summary[0]);
            Check(summary[2].Contains("the enemy's Infantry: 120 men, D 5.0 → max 24 files 23.05 m".Replace("23.05", M(LineDisciplineMath.CapWidth(24, LineInterval, LineDiameter)))), "step 73: the other side's line: " + summary[2]);
            Check(summary[2].Contains("vs asked up to 48 files"), "step 73: the widest asked not in: " + summary[2]);
            Check(summary[1].Contains("your Skirmisher (yours): 60 men, D 5.0 → max 12 files"), "step 73: your side's line: " + summary[1]);
            Check(summary[3].Contains("PREVIEW CAPPED") && summary[3].Contains("got the cap 42 times"), "step 73: the orders line: " + summary[3]);
            foreach (var line in summary) TraxLog.Info("summary", line);
            Check(lines.Stats.Errors == 0, "step 73: " + lines.Stats.Errors + " errors");

            LogHas("[lines] mission start: line discipline by tier (step 73)");
            LogHas("first formation counted at 10.0 s: the enemy's Infantry - 120 men (120 T1) → D 5.0 → at most 24 files");
            LogHas("first clamp of an AI line at 10.0 s: the enemy's Infantry (the other side) stood 48 files");
            LogHas("narrowed to " + M(LineDisciplineMath.CapWidth(24, LineInterval, LineDiameter)) + " m: 24 files x 5 ranks, 5.0 deep (it was made wider");
            LogHas("first AUTO-SHRINK at");
            LogHas("first capped arrangement at 10.0 s: your Skirmisher now tells the game its maximum width is");
            LogHas("first line order of yours wider than the men hold at");
            LogHas("- AT THE CAP (the game's own drag / move kept to the capped maximum width)");
            LogHas("first clamp of YOUR line at");
            LogHas("settings changed mid-mission at");
            LogHas("your formations let go at");
            LogHas("the capped arrangement could not go into your HorseArcher");
            LogHas("[summary] line discipline, the other side (step 73):");
        }

        private static void LinesFollowTheMasterSwitch()
        {
            try
            {
                Step73Defaults();
                var w = new FakeLineWorld();
                var recruits = new FakeLine { Name = "the enemy's Infantry" }.Men(1, 120).Wide();
                var mine = new FakeLine { Name = "your Infantry", PlayerOrdered = true, PlayerSide = true }.Men(0, 60).Wide(); // 60 T0: D 6 → 10 files
                w.Lines.Add(recruits);
                w.Lines.Add(mine);
                var lines = new LineDiscipline(w);
                double t = 500;
                lines.Tick(t);
                Check(recruits.Files == 24 && mine.Files == 10 && mine.Preview, "precondition: the lines not held (" + recruits.Files + " / " + mine.Files + ")");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                lines.Tick(t += 0.1);
                Check(recruits.Files == 48 && mine.Files == 32, "mod off: the game's own widths not given back at once (step 73: " + recruits.Files + " / " + mine.Files + ")");
                Check(!mine.Preview, "mod off: the capped arrangement not taken out (step 73)");
                recruits.Wide();
                int c = recruits.Clamps;
                lines.Tick(t += 0.1);
                Check(recruits.Clamps == c && recruits.Files == 48, "mod off: a line still narrowed (step 73)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                lines.Tick(t += 0.1);
                Check(recruits.Files == 24 && mine.Files == 10 && mine.Preview, "mod back on: the lines not held again (step 73)");
                LogHas("switched OFF at");
                LogHas("formations given the game's own width back, every capped arrangement taken out");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Step73Defaults();
            }
        }

        /// <summary>The game's IFormation for the real arrangement, stood in (the interval, a man's width, the men).</summary>
        private sealed class FakeLineOwner : IFormation
        {
            public int Men;
            public float Interval => (float)LineInterval;
            public float Distance => 0.4f;
            public float UnitDiameter => (float)LineDiameter;
            public float MinimumInterval => 0.4f;
            public float MaximumInterval => (float)LineInterval;
            public float MinimumDistance => 0.4f;
            public float MaximumDistance => 0.4f;
            public int? OverridenUnitCount => Men;
            public bool GetIsLocalPositionAvailable(Vec2 localPosition, Vec2? nearestAvailableUnitPositionLocal) => true;
            public bool BatchUnitPositions(MBArrayList<Vec2i> orderedPositionIndices, MBArrayList<Vec2> orderedLocalPositions, MBList2D<int> availabilityTable, MBList2D<WorldPosition> globalPositionTable, int fileCount, int rankCount) => false;
            public IFormationUnit? GetClosestUnitTo(Vec2 localPosition, MBList<IFormationUnit>? unitsWithSpaces = null, float? maxDistance = null) => null;
            public IFormationUnit? GetClosestUnitTo(IFormationUnit targetUnit, MBList<IFormationUnit>? unitsWithSpaces = null, float? maxDistance = null) => null;
            public void OnUnitAddedOrRemoved() { }
            public void SetUnitToFollow(IFormationUnit unit, IFormationUnit toFollow, Vec2 vector) { }
        }

        private static void DisciplinedLineIsTheGamesLine()
        {
            var owner = new FakeLineOwner { Men = 120 };
            var plain = new LineFormation(owner);
            var mine = new DisciplinedLineFormation(owner, true);
            float game = plain.MaximumWidth;
            Check(Math.Abs(game - (float)LineDisciplineMath.Width(120, LineInterval, LineDiameter)) < 1e-3, "step 73: the game's maximum width for 120 men is " + game);
            Check(Math.Abs(mine.MaximumWidth - game) < 1e-6 && mine.CapReads == 0, "step 73: no cap = the game's own maximum width");
            float cap = (float)LineDisciplineMath.CapWidth(24, LineInterval, LineDiameter);
            mine.CapWidth = cap;
            Check(Math.Abs(mine.MaximumWidth - cap) < 1e-6 && mine.CapReads == 1, "step 73: the capped maximum width is " + mine.MaximumWidth + " (" + mine.CapReads + " reads)");
            // the game's own floor of the cap width (LineFormation.GetFileCountFromWidth) gives exactly the cap's files - the drag stops at 24 men
            Check(mine.GetFileCountFromWidth(cap) == 24, "step 73: the game floors the cap width to " + mine.GetFileCountFromWidth(cap) + " files, not 24");
            Check(mine.GetFileCountFromWidth(Math.Min(60f, mine.MaximumWidth)) == 24, "step 73: a 60 m drag clamped by the game's own rule is not 24 files");
            // never below the game's own minimum
            mine.CapWidth = 0.5f;
            Check(Math.Abs(mine.MaximumWidth - mine.MinimumWidth) < 1e-6, "step 73: a cap below the game's minimum width was not raised to it");
            // a cap wider than the game's own maximum changes nothing
            mine.CapWidth = game + 10;
            long reads = mine.CapReads;
            Check(Math.Abs(mine.MaximumWidth - game) < 1e-6 && mine.CapReads == reads, "step 73: a cap above the game's maximum answered");
            // the simulation copy keeps the type, with no cap of its own
            var clone = mine.Clone(owner);
            Check(clone is DisciplinedLineFormation d && float.IsNaN(d.CapWidth) && clone.GetType() == mine.GetType(), "step 73: the clone is " + clone.GetType().Name);
            IFormationArrangement asGame = mine;
            Check(asGame is LineFormation && !(asGame is TransposedLineFormation), "step 73: ours is not a plain line to the game");
            // the game's own copy of the staggering
            var flat = new LineFormation(owner, false);
            var copy = new DisciplinedLineFormation(owner, true);
            copy.DeepCopyFrom(flat);
            Check(!copy.IsStaggered, "step 73: DeepCopyFrom did not carry the staggering");
        }

        private static void LineDisciplineCostStaysFlat()
        {
            Step73Defaults();
            var w = new FakeLineWorld();
            // 1000 men: 20 formations of 50 on two sides, every kind, half of them wider than their cap each second (the AI re-widening)
            for (int i = 0; i < 20; i++)
            {
                var l = new FakeLine { Name = "f" + i, PlayerSide = i < 10, PlayerOrdered = i < 3, Shape = i % 7 == 6 ? LineShape.Square : LineShape.Line, Mounted = i % 9 == 8 }
                    .Men(i % 7, 50).Wide();
                w.Lines.Add(l);
            }
            var lines = new LineDiscipline(w);
            double t = 1000;
            lines.Tick(t);
            const int ticks = 600;
            AppDomain.MonitoringIsEnabled = true;
            long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            var sw = new Stopwatch();
            for (int k = 1; k <= ticks; k++)
            {
                if (k % 60 == 0)
                    for (int i = 0; i < w.Lines.Count; i += 2) w.Lines[i].Wide();
                sw.Start();
                lines.Tick(t + k / 60.0);
                sw.Stop();
            }
            long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
            double ms = sw.Elapsed.TotalMilliseconds / ticks;
            Console.WriteLine("        step 73 cost: 20 formations / 1000 men looked at every frame, recounted every second, re-widened each second - "
                              + (ms * 1000).ToString("0.0", CultureInfo.InvariantCulture) + " µs a tick, " + allocated + " bytes over " + ticks + " ticks (the on-screen lines of your 3 units, 2 s apart); clamps "
                              + (lines.Stats.Player.AiWidenClamps + lines.Stats.Player.PlayerWidenClamps + lines.Stats.Other.AiWidenClamps) + " widenings, "
                              + (lines.Stats.Player.AiAutoShrinks + lines.Stats.Player.PlayerAutoShrinks + lines.Stats.Other.AiAutoShrinks) + " shrinks");
            Check(ms < 0.2, "cost: the line discipline's look took " + ms + " ms (budget 0.2 ms)");
            Check(allocated < 64 * 1024, "cost: the line discipline allocates - " + allocated + " bytes over " + ticks + " ticks");
            Check(lines.Stats.Errors == 0, "cost: errors " + lines.Stats.Errors);
        }
    }
}
