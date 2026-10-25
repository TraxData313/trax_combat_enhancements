using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 95 - THE STEP-IN BASH + SWING 30% OF THE TIME (AI_NOTES "Step 95"). (1) Through the real logic with a stand-in
    /// world: a shieldman whose enemy stands 1.3 m off steps in during his bash's guard - the input frames through the REAL filter carry the forward
    /// walk toward his enemy in his own frame; the enemy coming within 0.95 m = arrived, the key at the bash's own 0.4 s; never arriving = the guard
    /// held to 0.6 s, then the bash ("the time ran out"); a friend in the walk = no step, the bash from where he stands; a held wall standing firm =
    /// no step; already close = none needed; the switch / the master switch mid-step end the walk; kicks and pushes never step. (2) Step 91's grind
    /// with the fronts 1.3 m apart and the stand-in bash reaching 1.15 m, the shipped shares, the same dice twice - the step-in OFF vs ON: the friends
    /// the bashes hit before / after, the bashes binned at the bash, every input frame of a step toward the enemy, the shares near 27 / 43 / 30 and
    /// 45 / 25 / 30.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Step 95: the shipped step-in (defaults.json - on, 0.95 m, 0.6 s).</summary>
        private static void Step95StepIn()
        {
            foreach (var p in new[] { SettingsSchema.AiBashStepIn, SettingsSchema.AiBashStepInToMetres, SettingsSchema.AiBashStepInMaxSeconds })
                S.Set(p, p.Default, SettingSources.File);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StepInBashThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                Step95Shares();
                Step90PathDefaults();
                Step95StepIn();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                Check(S.AiBashStepIn && Math.Abs(S.AiBashStepInToMetres - 0.95) < 1e-6 && Math.Abs(S.AiBashStepInMaxSeconds - 0.6) < 1e-6,
                    "step 95: the shipped step-in is not on / 0.95 m / 0.6 s");
                Check(S.AiShieldmanKickPercent == 27 && S.AiShieldmanBashPercent == 43 && S.AiNoShieldKickPercent == 45 && S.AiNoShieldPushPercent == 25,
                    "step 95: the shipped shares are not 27 / 43 and 45 / 25");
                var body = new FakeAiKickBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.AiKickBody = body;
                logic.AiKickDice = new FixedDice(0.5); // 0.27 ≤ 0.5 < 0.70: a shieldman BASHES, a man without a shield pushes (0.45 ≤ 0.5 < 0.70)
                var st = logic.AiKickStats;
                double t = 95000;
                TrackedAgent Man(int index, bool shield)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 100;
                    SetBar(m, 1.0);
                    m.PrevAction = m.PrevLowerAction = 35;
                    if (shield) body.Shield.Add(index);
                    return m;
                }
                void Place(TrackedAgent m, double x, double y, int side) => body.Bodies[m.AgentIndex] = (x, y, 0, side == 0 ? 1 : -1, side); // side 0 faces +y, side 1 faces -y
                void Aim(TrackedAgent m, TrackedAgent e)
                {
                    var a = body.Bodies[m.AgentIndex];
                    var b = body.Bodies[e.AgentIndex];
                    body.Targets[m.AgentIndex] = (e, (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)), 0f);
                }
                void Tick(double dt)
                {
                    t += dt;
                    logic.TickAiKicks(t);
                }
                void Over(TrackedAgent m)
                {
                    for (int k = 0; k < 20 && m.AiKickPhase != AiKickPhase.None; k++) Tick(0.2); // never played - NOT played after the watch
                    m.AiKickNext = double.NegativeInfinity;
                }
                (float X, float Y) Frame(TrackedAgent m)
                {
                    uint flags = 0;
                    float vx = 0f, vy = 0f;
                    AiInputHook.Apply(m, m.Input!, true, ref flags, ref vx, ref vy);
                    return (vx, vy);
                }

                var basher = Man(9500, true);
                var enemy = Man(9501, false);
                var friend = Man(9502, true);
                Place(basher, 0, 0, 0);
                Place(enemy, 0, 1.3, 1);
                Place(friend, 5, 0, 0); // far off
                Aim(basher, enemy);

                // ---- 1. he steps: the bash asked, the step-in on, the frames through the real filter walk him toward his enemy (0, 1) in his frame
                DecideAt(logic, basher, t);
                Check(basher.AiKickPhase == AiKickPhase.Guard && basher.AiKickKind == AiKickKind.Bash, "step 95: no bash asked (" + basher.AiKickPhase + " " + basher.AiKickKind + ")");
                Check(basher.AiStepping && basher.Input != null && basher.Input.StepIn, "step 95: a bash at 1.3 m does not step in");
                var f = Frame(basher);
                Check(Math.Abs(f.X) < 1e-4 && Math.Abs(f.Y - 1) < 1e-4, "step 95: the step-in frame is not a straight walk forward: (" + f.X + ", " + f.Y + ")");
                LogHas("[impact] first AI shield bash STEPS IN");
                // turned 90° off his enemy (facing +x), the walk still follows the line to the enemy: (-1, 0) in his frame - to his left
                body.Bodies[basher.AgentIndex] = (0, 0, 1, 0, 0);
                Tick(0.05);
                f = Frame(basher);
                Check(Math.Abs(f.X + 1) < 1e-4 && Math.Abs(f.Y) < 1e-4, "step 95: the step-in frame does not follow the line to the enemy when he is turned: (" + f.X + ", " + f.Y + ")");
                body.Bodies[basher.AgentIndex] = (0, 0, 0, 1, 0);
                // ... he walks in: the enemy at 0.9 m by 0.25 s = ARRIVED; the key at the bash's own 0.4 s, the walk off
                Place(basher, 0, 0.4, 0);
                Aim(basher, enemy);
                Tick(0.2);
                Check(!basher.AiStepping && basher.AiStepIn == AiStepIn.Arrived && !basher.Input!.StepIn, "step 95: no arrival at 0.9 m (" + basher.AiStepIn + ")");
                Check(basher.AiKickPhase == AiKickPhase.Guard, "step 95: an early arrival skipped the bash's own 0.4 s lead");
                Tick(0.16);
                Check(basher.AiKickPhase == AiKickPhase.Press, "step 95: no key at 0.41 s after arriving (" + basher.AiKickPhase + ")");
                Check(Math.Abs(basher.AiKickPressDistance - 0.9) < 1e-4, "step 95: the distance at the bash is not 0.9: " + basher.AiKickPressDistance);
                Over(basher);

                // ---- 2. he never arrives (the enemy steps back as he comes): the guard held to 0.6 s, then the bash - the time ran out
                Place(basher, 0, 0, 0);
                Aim(basher, enemy);
                DecideAt(logic, basher, t);
                double began = t;
                Tick(0.45);
                Check(basher.AiKickPhase == AiKickPhase.Guard && basher.AiStepping, "step 95: the guard was not held past 0.4 s while he walked in");
                Tick(0.16);
                Check(basher.AiKickPhase == AiKickPhase.Press && basher.AiStepIn == AiStepIn.TimeRanOut && !basher.Input!.StepIn,
                    "step 95: no bash after the 0.6 s step guard (" + basher.AiKickPhase + ", " + basher.AiStepIn + ", " + (t - began).ToString("0.00", CultureInfo.InvariantCulture) + " s)");
                Over(basher);

                // ---- 3. a friend in the walk (0.4 m ahead, 0.1 m aside - not in front by step 90's rule, so the bash goes on): no step, the bash from where he stands
                Place(friend, 0.1, 0.4, 0);
                DecideAt(logic, basher, t);
                Check(basher.AiKickPhase == AiKickPhase.Guard && basher.AiStepIn == AiStepIn.RefusedFriend && !basher.AiStepping && !(basher.Input!.StepIn),
                    "step 95: a friend in the walk did not refuse the step (" + basher.AiKickPhase + ", " + basher.AiStepIn + ")");
                LogHas("[impact] first AI shield bash's STEP-IN refused");
                Over(basher);
                Place(friend, 5, 0, 0);

                // ---- 4. a held shield wall standing firm: no step; already close: none needed
                basher.Wall = new WallState { Held = true, Firm = true };
                DecideAt(logic, basher, t);
                Check(basher.AiStepIn == AiStepIn.RefusedWall && !basher.AiStepping, "step 95: a wall standing firm stepped (" + basher.AiStepIn + ")");
                Over(basher);
                basher.Wall = null;
                Place(enemy, 0, 0.9, 1);
                Aim(basher, enemy);
                DecideAt(logic, basher, t);
                Check(basher.AiStepIn == AiStepIn.NotNeeded && !basher.AiStepping && basher.AiKickPhase == AiKickPhase.Guard, "step 95: a bash at 0.9 m stepped (" + basher.AiStepIn + ")");
                Over(basher);
                Place(enemy, 0, 1.3, 1);
                Aim(basher, enemy);

                // ---- 5. a man without a shield PUSHES - never a step
                var pusher = Man(9503, false);
                Place(pusher, 2, 0, 0);
                var enemy2 = Man(9504, false);
                Place(enemy2, 2, 1.3, 1);
                Aim(pusher, enemy2);
                DecideAt(logic, pusher, t);
                Check(pusher.AiKickKind == AiKickKind.Push && !pusher.AiStepping && pusher.Input != null && !pusher.Input.StepIn, "step 95: a push stepped in");
                Over(pusher);

                // ---- 6. the switch and the master switch mid-step: the walk ends (cut short), nothing written after
                DecideAt(logic, basher, t);
                Check(basher.AiStepping, "step 95: no step for the switch check");
                S.Set(SettingsSchema.AiBashStepIn, false, SettingSources.Mcm);
                Check(Frame(basher).Y == 0f, "step 95: the filter still writes the walk with AiBashStepIn off");
                Tick(0.05);
                Check(!basher.AiStepping && basher.AiStepIn == AiStepIn.CutShort && !basher.Input!.StepIn, "step 95: switching the step-in off did not end the walk (" + basher.AiStepIn + ")");
                S.Set(SettingsSchema.AiBashStepIn, true, SettingSources.Mcm);
                Over(basher);
                DecideAt(logic, basher, t);
                Check(basher.AiStepping, "step 95: no step for the master-switch check");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Tick(0.05);
                Check(basher.AiKickPhase == AiKickPhase.None && !basher.AiStepping && !basher.Input!.StepIn, "step 95: the master switch did not end the stepping bash");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Over(basher);

                // ---- the counts: every bash asked has exactly one step-in outcome
                var si = st.StepIn;
                Check(si.Total == st.AskedCount(AiKickKind.Bash), "step 95: step-in outcomes " + si.Total + " vs bashes asked " + st.AskedCount(AiKickKind.Bash));
                Check(si.Count(AiStepIn.Arrived) == 1 && si.Count(AiStepIn.TimeRanOut) == 1 && si.Count(AiStepIn.RefusedFriend) == 1 && si.Count(AiStepIn.RefusedWall) == 1
                      && si.Count(AiStepIn.NotNeeded) == 1 && si.Count(AiStepIn.CutShort) == 2,
                    "step 95: the step-in counts are off: " + si.Line(AiKickRules.From(S)));
                Check(si.Pressed == 2 && Math.Abs(si.FromSum - 2.6) < 1e-3, "step 95: the stepping bashes' gap closed was not noted twice (" + si.Pressed + ", " + si.FromSum + ")");
                Console.WriteLine("        step 95 line (direct): " + si.Line(AiKickRules.From(S)));
                logic.WriteAiKickSummary();
                LogHas("[summary] ai kicks - the step-in bash (step 95): a shieldman steps in during his bash's guard until his enemy is within 0.95 m");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                AiKickDefaults();
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Step90PathDefaults();
                Step65CleanUp(keep);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StepInBashesInAGrind()
        {
            var keep = _logic;
            var c = CultureInfo.InvariantCulture;
            const double front = 1.3, bashEngine = 1.15, walk = 1.2;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                Step95Shares();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                Step90PathDefaults();
                ShippedBashReach();
                // the step-in OFF (step 91's bash from where he stands), then the shipped ON - the same dice
                S.Set(SettingsSchema.AiBashStepIn, false, SettingSources.File);
                var before = FightTwoLines(95, 9500, frontGap: front, bashEngineReach: bashEngine, stepWalkSpeed: walk);
                LogHas("[summary] ai kicks - the step-in bash (step 95): the step-in is OFF (AiBashStepIn off)");
                Step95StepIn();
                var after = FightTwoLines(95, 9600, frontGap: front, bashEngineReach: bashEngine, stepWalkSpeed: walk);
                LogHas("[summary] ai kicks - the step-in bash (step 95): a shieldman steps in");
                var a = after.Stats;
                var b = before.Stats;
                var r = AiKickRules.From(S);
                Console.WriteLine("        step 95 the step-in OFF: " + before + "; bashes asked " + b.AskedCount(AiKickKind.Bash) + ", on a FRIEND " + b.FriendHits(AiKickKind.Bash)
                    + " of " + BashHits(b) + " (" + (100 * FriendShareOfBashHits(b)).ToString("0.0", c) + "%), on an ENEMY " + b.EnemyHits(AiKickKind.Bash));
                Console.WriteLine("        step 95 the step-in ON:  " + after + "; bashes asked " + a.AskedCount(AiKickKind.Bash) + ", on a FRIEND " + a.FriendHits(AiKickKind.Bash)
                    + " of " + BashHits(a) + " (" + (100 * FriendShareOfBashHits(a)).ToString("0.0", c) + "%), on an ENEMY " + a.EnemyHits(AiKickKind.Bash)
                    + "; step frames " + after.StepFrames + " (toward " + after.StepFramesToward + ", away " + after.StepFramesAway + "), the farthest out " + after.FarthestStep.ToString("0.00", c) + " m");
                Console.WriteLine("        step 95 line (OFF): " + b.StepIn.Line(AiKickRules.From(S)));
                Console.WriteLine("        step 95 line (ON):  " + a.StepIn.Line(r));
                Console.WriteLine("        step 95 shares (ON): " + after.SharesLine);
                // 1. the OFF run never steps; every bash has one outcome either way
                Check(b.StepIn.Count(AiStepIn.Off) == b.AskedCount(AiKickKind.Bash) && before.StepFrames == 0, "step 95: the OFF run stepped (" + before.StepFrames + " frames)");
                Check(a.StepIn.Total == a.AskedCount(AiKickKind.Bash), "step 95: step-in outcomes " + a.StepIn.Total + " vs bashes asked " + a.AskedCount(AiKickKind.Bash));
                int binned = a.StepIn.EndedBeforeKey;
                for (int bin = 0; bin < AiBashReachStats.BinCount; bin++)
                    for (int res = 0; res < (int)AiBashResult.Count; res++) binned += a.StepIn.Result(bin, (AiBashResult)res);
                Check(binned == a.AskedCount(AiKickKind.Bash), "step 95: bash results at the key " + binned + " vs asked " + a.AskedCount(AiKickKind.Bash));
                // 2. the step-in happens: most far bashes step, arrive, close the gap; every frame walks toward the enemy
                Check(a.StepIn.Stepped > 50, "step 95: too few step-ins (" + a.StepIn.Stepped + ")");
                Check(a.StepIn.Count(AiStepIn.Arrived) > a.StepIn.Stepped / 2, "step 95: fewer than half the step-ins arrived (" + a.StepIn.Count(AiStepIn.Arrived) + " of " + a.StepIn.Stepped + ")");
                Check(a.StepIn.MeanStep > 0.15, "step 95: the step closed only " + a.StepIn.MeanStep.ToString("0.00", c) + " m on average");
                Check(after.StepFrames > 200 && after.StepFramesAway == 0 && after.StepFramesToward >= 0.9 * after.StepFrames,
                    "step 95: the input frames do not carry the forward step (" + after.StepFrames + " frames, toward " + after.StepFramesToward + ", away " + after.StepFramesAway + ")");
                // 3. the bashes land at the shield's reach: far more of them within 1.1 m at the key
                int nearOn = 0, allOn = 0;
                for (int bin = 0; bin < AiBashReachStats.BinCount; bin++)
                    for (int res = 0; res < (int)AiBashResult.Count; res++)
                    {
                        int k = a.StepIn.Result(bin, (AiBashResult)res);
                        allOn += k;
                        if (bin <= 2) nearOn += k;
                    }
                Check(allOn > 0 && nearOn > 0.6 * allOn, "step 95: only " + nearOn + " of " + allOn + " bashes were within 1.1 m at the key");
                // 4. THE POINT: fewer friends hit by bashes, more enemies (the same dice)
                double fb = FriendShareOfBashHits(b), fa = FriendShareOfBashHits(a);
                Console.WriteLine("        step 95 friends' share of the BASH hits: OFF " + (100 * fb).ToString("0.0", c) + "% (" + b.FriendHits(AiKickKind.Bash) + "), ON "
                    + (100 * fa).ToString("0.0", c) + "% (" + a.FriendHits(AiKickKind.Bash) + "); enemies hit by bashes " + b.EnemyHits(AiKickKind.Bash) + " → " + a.EnemyHits(AiKickKind.Bash));
                Check(fa < fb, "step 95: the friends' share of the bash hits did not fall with the step-in (" + fa + " vs " + fb + ")");
                Check(a.EnemyHits(AiKickKind.Bash) > b.EnemyHits(AiKickKind.Bash), "step 95: no more enemies hit by bashes with the step-in");
                // 5. the shares near 27 / 43 / 30 and 45 / 25 / 30 (a few hundred moves - ±6 points)
                foreach (var (run, name) in new[] { (a, "ON"), (b, "OFF") })
                {
                    Near(name + " shieldman kick", run.Rolled(0, AiKickKind.Kick), run.Moves(0), 27);
                    Near(name + " shieldman bash", run.Rolled(0, AiKickKind.Bash), run.Moves(0), 43);
                    Near(name + " shieldman swing", run.Rolled(0, AiKickKind.None), run.Moves(0), 30);
                    Near(name + " no-shield kick", run.Rolled(1, AiKickKind.Kick), run.Moves(1), 45);
                    Near(name + " no-shield push", run.Rolled(1, AiKickKind.Push), run.Moves(1), 25);
                    Near(name + " no-shield swing", run.Rolled(1, AiKickKind.None), run.Moves(1), 30);
                }

                void Near(string what, int count, int of, double target)
                {
                    Check(of > 150, "step 95: too few moves for " + what + " (" + of + ")");
                    double p = 100.0 * count / Math.Max(1, of);
                    Check(Math.Abs(p - target) <= 6, "step 95: " + what + " " + p.ToString("0.0", c) + "% (target " + target.ToString("0", c) + "%)");
                }
            }
            finally
            {
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                AiKickDefaults();
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Step90PathDefaults();
                ShippedBashReach();
                Step65CleanUp(keep);
            }
        }
    }
}
