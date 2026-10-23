using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 83 (Toni 2026-10-23: 20 T1 recruits at the minimum depth 6 stood 3-4 files wide - "a column"): SMALL UNITS KEEP A WIDE LINE through
    /// the real <see cref="LineDiscipline"/> with step 73's stand-in world, and the REAL <see cref="DisciplinedLineFormation"/> holding the same cap
    /// for the order drag. What it cannot check: the engine's own formations and the real order placer (PLAYTEST D48).
    /// </summary>
    internal static partial class Program
    {
        private static void Step83Defaults()
        {
            Step73Defaults();
            double[] map = { 7, 6, 5, 4, 3, 2, 1 }; // the shipped map (step 74)
            var keys = new[]
            {
                SettingsSchema.LineDisciplineRanksT0, SettingsSchema.LineDisciplineRanksT1, SettingsSchema.LineDisciplineRanksT2, SettingsSchema.LineDisciplineRanksT3,
                SettingsSchema.LineDisciplineRanksT4, SettingsSchema.LineDisciplineRanksT5, SettingsSchema.LineDisciplineRanksT6,
            };
            for (int i = 0; i < keys.Length; i++) S.Set(keys[i], map[i], SettingSources.File);
            S.Set(SettingsSchema.LineMinWidthToDepthRatio, 2.0, SettingSources.File);
        }

        private static void SmallUnitsKeepAWideLine()
        {
            try
            {
                Step83Defaults();
                var w = new FakeLineWorld();
                // Toni's 20 recruits, his own, on the game's Wide (16 files for 20 men); an AI copy of them; 8 looters; a 100-man unit that will shrink
                var mine = new FakeLine { Name = "your Infantry", PlayerOrdered = true, PlayerSide = true }.Men(1, 20).Wide();
                var theirs = new FakeLine { Name = "the enemy's Infantry" }.Men(1, 20).Wide();
                var looters = new FakeLine { Name = "the enemy's Ranged" }.Men(0, 8).Wide();
                var big = new FakeLine { Name = "the enemy's HeavyInfantry" }.Men(1, 100).Wide();
                w.Lines.AddRange(new[] { mine, theirs, looters, big });
                Check(mine.Files == 16 && looters.Files == 8 && big.Files == 32, "precondition: the game's Wide gives " + mine.Files + " / " + looters.Files + " / " + big.Files);
                var lines = new LineDiscipline(w);
                lines.Start();
                double t = 10;
                lines.Tick(t);

                // 1. 20 T1: D 6 capped at floor(√10) = 3 → 7 files x 3 ranks - wider than twice its depth, the remainder too
                int ranks = LineDisciplineMath.Ranks(20, mine.Files);
                Check(mine.Files == 7 && ranks == 3, "step 83: your 20 recruits stand " + mine.Files + " files x " + ranks + " ranks, not 7 x 3");
                Check(mine.Files >= 2 * ranks, "step 83: your 20 recruits are not twice as wide as deep (" + mine.Files + " x " + ranks + ")");
                Check(theirs.Files == 7, "step 83: the AI's 20 recruits stand " + theirs.Files + " files, not 7");
                // the drag preview's cap is the same 7 files
                double cap7 = LineDisciplineMath.CapWidth(7, LineInterval, LineDiameter);
                Check(mine.Preview && Math.Abs(mine.PreviewCap - cap7) < 1e-9, "step 83: the preview cap is " + mine.PreviewCap + " m, not 7 files' " + cap7);
                mine.Drag(40);
                Check(mine.Files == 7, "step 83: a 40 m drag gave " + mine.Files + " files, not the cap 7");
                // 8 T0: D 7 capped at 2 → 4 files x 2 ranks
                Check(looters.Files == 4 && LineDisciplineMath.Ranks(8, looters.Files) == 2, "step 83: 8 looters stand " + looters.Files + " files, not 4 x 2");
                // 100 T1: floor(√50) = 7 ≥ 6 - the tier's own 6: 17 files
                Check(big.Files == 17 && LineDisciplineMath.Ranks(100, big.Files) == 6, "step 83: 100 recruits stand " + big.Files + " files, not 17 (6 deep)");

                // 2. the 100 lose 80: the cap follows - 20 men → 7 files (an auto-shrink), twice as wide as deep
                big.Lose(80);
                lines.Tick(t += 1 / 60.0);
                Check(big.Files == 7 && big.Files >= 2 * LineDisciplineMath.Ranks(20, big.Files), "step 83: the 100 shrunk to 20 stand " + big.Files + " files, not 7");

                // 3. the ratio 0 live: step 73 as before - the AI's re-widened 20 held 6 deep (4 files, the game's √20 floor)
                S.Set(SettingsSchema.LineMinWidthToDepthRatio, 0.0, SettingSources.Mcm);
                theirs.Wide();
                lines.Tick(t += 1 / 60.0);
                Check(theirs.Files == 4, "step 83: the ratio 0 left the AI's 20 recruits " + theirs.Files + " files, not step 73's 4");
                Check(Math.Abs(mine.PreviewCap - LineDisciplineMath.CapWidth(4, LineInterval, LineDiameter)) < 1e-9, "step 83: the ratio 0 left the preview cap at " + mine.PreviewCap);
                S.Set(SettingsSchema.LineMinWidthToDepthRatio, 2.0, SettingSources.Mcm);
                theirs.Wide();
                lines.Tick(t += 1 / 60.0);
                Check(theirs.Files == 7 && Math.Abs(mine.PreviewCap - cap7) < 1e-9, "step 83: the ratio back at 2 - " + theirs.Files + " files, the preview cap " + mine.PreviewCap);
                Check(lines.Stats.Errors == 0, "step 83: " + lines.Stats.Errors + " errors");

                // 4. the REAL arrangement over 20 men holds the same cap: the game's own floor of the capped maximum width = 7 files
                var owner = new FakeLineOwner { Men = 20 };
                var real = new DisciplinedLineFormation(owner, true);
                real.CapWidth = (float)mine.PreviewCap;
                int files = real.GetFileCountFromWidth(Math.Min(40f, real.MaximumWidth));
                Check(files == 7 && files >= 2 * LineDisciplineMath.Ranks(20, files), "step 83: the real arrangement's drag gives " + files + " files for 20 men, not 7");

                LogHas("first SMALL UNIT at 10.0 s (step 83): your Infantry - 20 men at D 6.0 would stand 4 files x 5 ranks, 5.0 deep; held to 3.0 deep so the line is at least 2.0x as wide as deep: at most 7 files - 7 files x 3 ranks (the last 6 of 7), 2.86 deep");
                LogHas("a small unit: held 3.0 deep - LineMinWidthToDepthRatio 2.0");
                LogHas("a small unit's line at least 2.0 times as wide as deep");
            }
            finally
            {
                Step73Defaults();
            }
        }
    }
}
