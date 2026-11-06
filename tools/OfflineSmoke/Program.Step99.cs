using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 99 - THE ENDS OF A STANDING SHIELD WALL FACE THE FLANKS (Toni 2026-11-06; AI_NOTES "Step 99"). The REAL logic with step
    /// 41's stand-in engine side: who turns, how far, each look's writes, the releases by cause and the [summary] line. What only the game can tell:
    /// that the engine honours a per-man facing in a line formation (the summary's angle - PLAYTEST D63).
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallEndsFaceTheFlanks()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                var body = new FakeWallBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                var fs = logic.WallFlankStats;
                LogHas("[wall] wall flanks (step 99): ON (ShieldWallFlanksFaceOut) - in a held shield wall that STANDS");
                double t = 99000;

                var men = new Dictionary<int, TrackedAgent>();
                void Man(int i, int rank, int files, int left, int right)
                {
                    men[i] = logic.Track(FakeAgent(i))!;
                    body.Facts[i] = WallMan(ahead: 0);
                    body.Ranks[i] = (rank, files, left, right);
                }
                Man(990, 1, 12, 0, 11);  // the left end of rank 2
                Man(991, 0, 12, 11, 0);  // the right front corner
                Man(992, 1, 12, 5, 6);   // the middle
                Man(993, 1, 4, 0, 3);    // the end of a 4-file wall (too narrow)
                body.Looks[990] = (-1, 0);  // the engine obeys: he faces left
                // 991's body keeps facing the front (the engine ignores him): 45 degrees off the asked

                logic.TickWall(t);
                var f990 = men[990].Wall!.Flank;
                var f991 = men[991].Wall!.Flank;
                Check(f990.Turned && f990.Side == WallFlankSide.Left && f990.Degrees == 90 && !f990.Corner, "the left end of rank 2 was not turned 90 degrees left");
                Check(f991.Turned && f991.Side == WallFlankSide.Right && f991.Degrees == 45 && f991.Corner, "the right front corner was not turned 45 degrees right");
                Check(!men[992].Wall!.Flank.Turned && !men[993].Wall!.Flank.Turned, "the middle man or a narrow wall's end was turned");
                Check(body.Turned.TryGetValue(990, out bool on0) && on0 && body.Turned.TryGetValue(991, out bool on1) && on1 && !body.Turned.ContainsKey(992),
                    "the tendency / shield were not written for the turned men only");
                Check(body.FramesTurned >= 2 && fs.MenTurned == 2 && fs.MenCorner == 1, "the turned men's frames were not written: " + body.FramesTurned + ", men " + fs.MenTurned);
                LogHas("[wall] first END MAN TURNED to the flank this mission (step 99): (agent 990) at 99000.0 s - the left end of rank 2 of a standing shield wall 12 files wide, turned 90.0 degrees outward");

                // each look and each vanilla update writes them again
                int ons = body.TurnOnCalls, frames = body.FramesTurned;
                body.Stamps[990] = 1f;
                WallFrames(logic, ref t, 1.0 / 60);
                Check(body.TurnOnCalls > ons && body.FramesTurned > frames, "vanilla's update was not followed by his facing again");

                // settle and sample: one obeys, one does not
                for (int k = 0; k < 10; k++) WallLook(logic, ref t);
                Check(fs.Samples(false) > 0 && fs.Samples(true) > 0, "no angle samples");
                Check(fs.MedianOffTarget(false) < 5 && Math.Abs(fs.MedianOffTarget(true) - 47.5) < 0.1, "the angles: flank " + fs.MedianOffTarget(false) + ", corner " + fs.MedianOffTarget(true));
                LogHas("[wall] first turned end man's facing measured (step 99): ");

                // an ADVANCE: forward (the game's tendency and shield back); the stop again: turned again
                int offs = body.TurnOffCalls;
                body.Facts[990] = WallMan(ahead: 0, order: BraceOrderKind.Advance);
                body.Facts[991] = WallMan(ahead: 0, order: BraceOrderKind.Advance);
                WallLook(logic, ref t);
                Check(!men[990].Wall!.Flank.Turned && !men[991].Wall!.Flank.Turned && body.TurnOffCalls == offs + 2 && fs.Releases(WallFlankVerdict.NotStanding) == 2,
                    "an advance did not turn them forward with the game's values back");
                LogHas("[wall] first turned end man FACES FORWARD again (step 99): ");
                body.Facts[990] = WallMan(ahead: 0);
                body.Facts[991] = WallMan(ahead: 0);
                WallLook(logic, ref t);
                Check(men[990].Wall!.Flank.Turned && men[991].Wall!.Flank.Turned && fs.Turns == 4, "the stop did not turn them out again: turns " + fs.Turns);

                // an enemy on his flank keeps him turned; one 2 m in front turns him forward, and 2 s after it is gone he turns out again
                body.Enemies[990] = (-2.5, 0.5);
                WallLook(logic, ref t);
                Check(men[990].Wall!.Flank.Turned, "an enemy on his flank turned him forward");
                body.Enemies[990] = (0, 2);
                WallLook(logic, ref t);
                Check(!men[990].Wall!.Flank.Turned && fs.Releases(WallFlankVerdict.FrontEnemy) == 1, "an enemy in front did not turn him forward");
                body.Enemies.Remove(990);
                WallLook(logic, ref t);
                Check(!men[990].Wall!.Flank.Turned, "he turned out again within the 2 s hold");
                for (int k = 0; k < 8; k++) WallLook(logic, ref t);
                Check(men[990].Wall!.Flank.Turned, "he did not turn out again after the 2 s hold");

                // the switch off live: forward at the next look; on again: out again
                S.Set(SettingsSchema.ShieldWallFlanksFaceOut, false, SettingSources.Mcm);
                WallLook(logic, ref t);
                Check(!men[990].Wall!.Flank.Turned && !men[991].Wall!.Flank.Turned && fs.Releases(WallFlankVerdict.SwitchedOff) == 2, "the switch off did not turn them forward");
                LogHas("[wall] wall flanks (step 99) changed mid-mission at ");
                S.Set(SettingsSchema.ShieldWallFlanksFaceOut, true, SettingSources.Mcm);
                WallLook(logic, ref t);
                Check(men[990].Wall!.Flank.Turned && men[991].Wall!.Flank.Turned, "the switch on did not turn them out again");

                // the hold off and the master switch: at once
                offs = body.TurnOffCalls;
                S.Set(SettingsSchema.ShieldWallHoldsSlots, false, SettingSources.Mcm);
                WallFrames(logic, ref t, 1.0 / 60);
                Check(!men[990].Wall!.Flank.Turned && !men[991].Wall!.Flank.Turned && body.TurnOffCalls == offs + 2 && fs.Releases(WallFlankVerdict.HoldOff) == 2,
                    "the hold off did not turn them forward at once");
                S.Set(SettingsSchema.ShieldWallHoldsSlots, true, SettingSources.Mcm);
                WallLook(logic, ref t);
                Check(men[990].Wall!.Flank.Turned, "the hold on did not turn him out again");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                WallFrames(logic, ref t, 1.0 / 60);
                Check(!men[990].Wall!.Flank.Turned && fs.Releases(WallFlankVerdict.ModOff) == 2, "the master switch did not turn them forward at once");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                WallLook(logic, ref t);

                logic.WriteWallSummary();
                Check(fs.Errors == 0, "errors: " + fs.Errors);
                LogHas("[summary] wall flanks (step 99 - ShieldWallFlanksFaceOut ON now, 1 file(s) a side, the front corner 45 deg, released by an enemy within 4.0 m in front): end men turned 2 (front corners 1)");
                LogHas("flank men (90 deg) off the asked: median 3 deg");
                LogHas("front corners (45 deg) off the asked: median 48 deg");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.ShieldWallHoldsSlots, true, SettingSources.File);
                S.Set(SettingsSchema.ShieldWallFlanksFaceOut, true, SettingSources.File);
                AthleticsDefaults();
                WallDefaults();
            }
        }
    }
}
