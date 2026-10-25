using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 97 - THE STEP-IN THROUGH THE PRESS CLAMP + A SHORTER KICK COOLDOWN (AI_NOTES "Step 97"). Through the real logic and the
    /// REAL input filter with a stand-in world where step 77's press clamp is active (a stand-in of SteerPresser: the front clamp into the enemy line,
    /// his distance to its front refreshed every 0.1 s): a shieldman whose enemy stands 1.3 m off, 0.7 m behind the line's front, steps in and ARRIVES at
    /// 0.95 m (the clamp lets the step through); without step 97's read the clamp eats the step (the time runs out, he never moves); with the enemy 1 m
    /// behind the front he is stopped short of the front, never past it; a stronger wish (the step back's backpedal) is filed; a stand-in engine that
    /// never moves him = every wish clear and the summary says IT'S THE ENGINE. The cooldown ships 1 s.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Step 97: the shipped kick cooldown (defaults.json - 1 s).</summary>
        private static void Step97Cooldown() => S.Set(SettingsSchema.AiKickCooldownSeconds, SettingsSchema.AiKickCooldownSeconds.Default, SettingSources.File);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StepInThroughThePressClamp()
        {
            var keep = _logic;
            var c = CultureInfo.InvariantCulture;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                Step95Shares();
                Step90PathDefaults();
                Step95StepIn();
                Step97Cooldown();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.PressDontPass, true, SettingSources.File);
                Check(Math.Abs(S.AiKickCooldownSeconds - 1) < 1e-6, "step 97: the shipped cooldown is not 1 s (" + S.AiKickCooldownSeconds + ")");
                var body = new FakeAiKickBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.AiKickBody = body;
                logic.AiKickDice = new FixedDice(0.5); // a shieldman BASHES (0.27 ≤ 0.5 < 0.70)
                var hold = logic.AiKickStats.StepIn.Hold;
                var si = logic.AiKickStats.StepIn;
                double t = 97000;
                TrackedAgent Man(int index, bool shield)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 100;
                    SetBar(m, 1.0);
                    m.PrevAction = m.PrevLowerAction = 35;
                    if (shield) body.Shield.Add(index);
                    return m;
                }
                var basher = Man(9700, true);
                var enemy = Man(9701, false);
                double y = 0;
                void Place()
                {
                    body.Bodies[basher.AgentIndex] = (0, y, 0, 1, 0);
                    body.Bodies[enemy.AgentIndex] = (0, 1.3, 0, -1, 1);
                    body.Targets[basher.AgentIndex] = (enemy, (float)(1.3 - y), 0f);
                }

                // one bash: the press stand-in (the front clamp into the line - his frame's +y - while he stands within 0.6 m of the front, its distance
                // refreshed every 0.1 s like SteerPresser; frontReads off = no distance read, the clamp as step 77 alone), the real filter, the stand-in
                // engine walking him at `speed` along the vector it writes. Returns where he got to.
                double Bash(double frontY, double speed, bool press, bool frontReads, bool backpedal = false)
                {
                    y = 0;
                    Place();
                    basher.AiKickNext = double.NegativeInfinity;
                    DecideAt(logic, basher, t);
                    Check(basher.AiStepping, "step 97: no step-in for the bash");
                    var inp = basher.Input!;
                    if (backpedal) AiInputHook.SetBackpedal(basher, true, false, t);
                    double nextSteer = t;
                    for (int k = 0; k < 60 && basher.AiKickPhase == AiKickPhase.Guard; k++)
                    {
                        if (press && t >= nextSteer - 1e-9)
                        {
                            nextSteer = t + 0.1;
                            AiInputHook.SetPress(basher, true, t);
                            double fd = frontY - y;
                            inp.PressFront = fd <= PressMath.LookaheadMetres;
                            inp.PressNX = 0f;
                            inp.PressNY = 1f;
                            inp.PressFrontDistance = inp.PressFront && frontReads ? (float)fd : float.NaN;
                        }
                        t += 0.02;
                        logic.TickAiKicks(t);
                        if (inp.StepIn && basher.AiKickPhase == AiKickPhase.Guard)
                        {
                            uint flags = 0;
                            float vx = 0f, vy = 0f;
                            AiInputHook.Apply(basher, inp, true, ref flags, ref vx, ref vy);
                            y += speed * 0.02 * vy;
                            Place();
                        }
                    }
                    if (backpedal) AiInputHook.SetBackpedal(basher, false, false, t);
                    AiInputHook.SetPress(basher, false, t);
                    for (int k = 0; k < 20 && basher.AiKickPhase != AiKickPhase.None; k++)
                    {
                        t += 0.2;
                        logic.TickAiKicks(t);
                    }
                    return y;
                }

                // ---- 1. BEFORE (the clamp as step 77 alone - no front distance read): the clamp eats the step, he never moves, the time runs out
                long cutBefore = hold.Frames(StepInHold.PressCut);
                double got = Bash(0.6, 1.2, press: true, frontReads: false);
                Check(got < 0.01 && basher.AiStepIn == AiStepIn.TimeRanOut, "step 97: without the exemption the step was not eaten (" + got.ToString("0.00", c) + " m, " + basher.AiStepIn + ")");
                Check(hold.Frames(StepInHold.PressCut) > cutBefore + 10, "step 97: the press clamp's cut was not filed (" + hold.Frames(StepInHold.PressCut) + ")");
                Console.WriteLine("        step 97 the clamp alone: walked " + got.ToString("0.00", c) + " m, " + AiBashStepInMath.Name(basher.AiStepIn) + ", press-cut frames " + (hold.Frames(StepInHold.PressCut) - cutBefore));

                // ---- 2. WITH step 97: the enemy 1.3 m off, 0.7 m behind the line's front (the clamp active from the start) - the step let through, ARRIVED at 0.95 m
                long letBefore = hold.Frames(StepInHold.PressLetThrough);
                int arrivedBefore = si.Count(AiStepIn.Arrived);
                double covBefore = hold.Covered;
                got = Bash(0.6, 1.2, press: true, frontReads: true);
                Check(si.Count(AiStepIn.Arrived) == arrivedBefore + 1, "step 97: the step-in through the press clamp did not arrive (" + basher.AiStepIn + ", " + got.ToString("0.00", c) + " m)");
                Check(got >= 0.34 && got < 0.45 && Math.Abs(basher.AiKickPressDistance - (1.3 - got)) < 1e-3 && basher.AiKickPressDistance <= 0.96,
                    "step 97: he did not stop at 0.95 m from his enemy (walked " + got.ToString("0.00", c) + ", at the bash " + basher.AiKickPressDistance.ToString("0.00", c) + ")");
                Check(hold.Frames(StepInHold.PressLetThrough) > letBefore + 5, "step 97: no frame let through by the press clamp");
                Check(Math.Abs(hold.Covered - covBefore - got) < 0.05, "step 97: the walk measured from his positions " + (hold.Covered - covBefore).ToString("0.00", c) + " vs " + got.ToString("0.00", c));
                LogHas("[impact] first AI step-in MEASURED");
                Console.WriteLine("        step 97 through the clamp: walked " + got.ToString("0.00", c) + " m, the enemy " + basher.AiKickPressDistance.ToString("0.00", c) + " m off at the bash, let-through frames "
                    + (hold.Frames(StepInHold.PressLetThrough) - letBefore));

                // ---- 3. NEVER PAST THE FRONT: the enemy 1 m behind the line's front (0.3 m ahead of him) - stopped short of the front, the time runs out
                cutBefore = hold.Frames(StepInHold.PressCut);
                got = Bash(0.3, 1.2, press: true, frontReads: true);
                Check(got < 0.3 && got > 0.1 && basher.AiStepIn == AiStepIn.TimeRanOut,
                    "step 97: the step went past / did not reach toward the line's front at 0.3 m (" + got.ToString("0.00", c) + " m, " + basher.AiStepIn + ")");
                Check(hold.Frames(StepInHold.PressCut) > cutBefore, "step 97: the clamp's cut at the front was not filed");
                Console.WriteLine("        step 97 the front 0.3 m ahead: walked " + got.ToString("0.00", c) + " m (never past it), press-cut frames " + (hold.Frames(StepInHold.PressCut) - cutBefore));

                // ---- 4. a stronger wish: the step back's backpedal replaces the step - filed by name
                long backBefore = hold.Frames(StepInHold.Backpedal);
                got = Bash(5, 1.2, press: false, frontReads: false, backpedal: true);
                Check(hold.Frames(StepInHold.Backpedal) > backBefore + 5 && got <= 0, "step 97: the backpedal over the step was not filed (" + hold.Frames(StepInHold.Backpedal) + ", " + got.ToString("0.00", c) + ")");

                // ---- 5. THE ENGINE: no press, nothing of ours in the way, a stand-in engine that never moves him - three step-ins all clear, 0 m walked
                int clearBefore = hold.ClearSteps;
                double clearCov = hold.ClearCovered, clearSec = hold.ClearSeconds, clearPeak = hold.ClearPeakSum;
                for (int k = 0; k < 3; k++) got = Bash(5, 0, press: false, frontReads: false);
                Check(hold.ClearSteps == clearBefore + 3 && Math.Abs(hold.ClearCovered - clearCov) < 1e-6, "step 97: the engine's step-ins not all clear / moved (" + hold.ClearSteps + ")");
                string verdict = AiStepInHoldStats.EngineVerdict(3, hold.ClearSeconds - clearSec, hold.ClearCovered - clearCov, hold.ClearPeakSum - clearPeak);
                Check(verdict.StartsWith("IT'S THE ENGINE", StringComparison.Ordinal), "step 97: the stand-in engine's verdict: " + verdict);
                Console.WriteLine("        step 97 a stand-in engine that never moves him: " + verdict);
                // the moving clear step (2.) vs the frozen engine (5.) together: the summary clause and its verdict
                logic.WriteAiKickSummary();
                LogHas("[summary] ai kicks - the step-in bash (step 95): a shieldman steps in");
                LogHas("| what held the step back (step 97 - each step-in frame's forward stick toward his enemy): frames ");
                Console.WriteLine("        step 97 clause: " + hold.Clause());
                Check(hold.Started == si.Stepped && hold.Steps >= 6, "step 97: step-ins started " + hold.Started + " vs stepped " + si.Stepped + ", measured " + hold.Steps);
            }
            finally
            {
                S.Set(SettingsSchema.PressDontPass, false, SettingSources.File);
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                AiKickDefaults();
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Step90PathDefaults();
                Step65CleanUp(keep);
            }
        }
    }
}
