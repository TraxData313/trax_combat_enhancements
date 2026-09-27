using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 16 - the AI holds through the AI's own input (AI_NOTES "Step 16"): the bit rule, the backwards
/// vector in the man's own frame, the distance covered, the later end, and the summary lines that judge it.</summary>
public class AiInputTests
{
    private const uint AttackDown = 0x200, AttackLeft = 0x40, DefendLeft = 0x400, DefendDown = 0x2000, DefendAuto = 0x4000;
    private const uint Forward = 1, StrafeRight = 4, Action = 0x10000;

    [Fact]
    public void A_frame_without_an_attack_wish_is_left_untouched()
    {
        uint flags = Forward | StrafeRight | DefendLeft | Action;
        Assert.Equal(flags, AiInputMath.HoldAttacks(flags, raiseGuard: true, inReady: false, out var edit));
        Assert.Equal(InputEdit.None, edit);
        Assert.Equal(0u, AiInputMath.HoldAttacks(0, raiseGuard: true, inReady: true, out edit));
        Assert.Equal(InputEdit.None, edit);
    }

    [Fact]
    public void Only_the_attack_bits_go__movement_and_his_own_guard_stay()
    {
        // he wants to attack while blocking left: the attack goes, the block and the movement stay - no second guard added
        uint flags = Forward | AttackLeft | DefendLeft;
        Assert.Equal(Forward | DefendLeft, AiInputMath.HoldAttacks(flags, raiseGuard: true, inReady: false, out var edit));
        Assert.Equal(InputEdit.AttackCleared | InputEdit.OwnGuardKept, edit);
        // DefendAuto is a guard of his own too
        Assert.Equal(DefendAuto, AiInputMath.HoldAttacks(AttackDown | DefendAuto, raiseGuard: true, inReady: false, out edit));
        Assert.Equal(InputEdit.AttackCleared | InputEdit.OwnGuardKept, edit);
    }

    [Fact]
    public void A_wish_to_attack_raises_the_guard_instead__or_is_only_dropped_with_the_switch_off()
    {
        Assert.Equal(Forward | DefendDown, AiInputMath.HoldAttacks(Forward | AttackDown, raiseGuard: true, inReady: false, out var edit));
        Assert.Equal(InputEdit.AttackCleared | InputEdit.GuardRaised, edit);
        Assert.Equal(Forward, AiInputMath.HoldAttacks(Forward | AttackDown, raiseGuard: false, inReady: false, out edit));
        Assert.Equal(InputEdit.AttackCleared, edit);
    }

    [Fact]
    public void In_a_ready_the_guard_cancels_it_whatever_the_switch__never_a_release()
    {
        // attack bits vanishing mid-ready = the button let go = the blow released; with a block pressed it is cancelled
        Assert.Equal(DefendDown, AiInputMath.HoldAttacks(AttackLeft, raiseGuard: false, inReady: true, out var edit));
        Assert.Equal(InputEdit.AttackCleared | InputEdit.ReadyCancelled, edit);
        Assert.Equal(DefendDown, AiInputMath.HoldAttacks(AttackLeft, raiseGuard: true, inReady: true, out edit));
        Assert.Equal(InputEdit.AttackCleared | InputEdit.ReadyCancelled, edit);
    }

    [Fact]
    public void The_masks_are_the_engines()
    {
        // Agent.MovementControlFlag v1.4.8 (the offline smoke compares them with the game's enum itself)
        Assert.Equal(0x3C0u, AiInputMath.AttackMask);
        Assert.Equal(0x7C00u, AiInputMath.DefendMask);
        Assert.Equal(0x2000u, AiInputMath.DefendDown);
    }

    [Fact]
    public void While_backpedalling_only_his_move_bits_go()
    {
        const uint Backward = 2, StrafeLeft = 8, TurnLeft = 0x20;
        Assert.Equal(0xFu, AiInputMath.MoveBits);
        Assert.Equal(TurnLeft | DefendLeft | Action, AiInputMath.Backpedal(Forward | Backward | StrafeRight | StrafeLeft | TurnLeft | DefendLeft | Action));
        Assert.Equal(AttackDown, AiInputMath.Backpedal(AttackDown));   // the attack rule is HoldAttacks' job, not this
    }

    [Fact]
    public void Facing_his_enemy_the_backwards_input_is_straight_back()
    {
        // he stands facing north (forward (0, 1), side (1, 0)); his enemy is north, so "away" is south
        Assert.True(AiInputMath.BackpedalVector(0, -1, 1, 0, 0, 1, 1.0, out double x, out double y));
        Assert.Equal(0, x, 9);
        Assert.Equal(-1, y, 9);
        // turned 90° to the right (forward east (1, 0), side south (0, -1)): the same world line is a step to his right
        Assert.True(AiInputMath.BackpedalVector(0, -1, 0, -1, 1, 0, 1.0, out x, out y));
        Assert.Equal(1, x, 9);
        Assert.Equal(0, y, 9);
        // any frame: the written vector has the asked length, and turned back to the world it IS the away line
        double ang = 0.7;
        double fx = -Math.Sin(ang), fy = Math.Cos(ang), sx = fy, sy = -fx;   // the game's convention: s = (f.y, -f.x)
        Assert.True(AiInputMath.BackpedalVector(0.6, -0.8, sx, sy, fx, fy, 0.5, out x, out y));
        Assert.Equal(0.5, Math.Sqrt(x * x + y * y), 9);
        double wx = sx * x + fx * y, wy = sy * x + fy * y;                    // Mat3.TransformToParent
        Assert.Equal(0.3, wx, 9);
        Assert.Equal(-0.4, wy, 9);
    }

    [Fact]
    public void A_degenerate_direction_or_frame_writes_nothing()
    {
        Assert.False(AiInputMath.BackpedalVector(0, 0, 1, 0, 0, 1, 1, out double x, out double y));
        Assert.Equal(0, x);
        Assert.Equal(0, y);
        Assert.False(AiInputMath.BackpedalVector(0, -1, 0, 0, 0, 1, 1, out _, out _));
        Assert.False(AiInputMath.BackpedalVector(0, -1, 1, 0, 0, 0, 1, out _, out _));
        Assert.False(AiInputMath.BackpedalVector(0, -1, 1, 0, 0, 1, double.NaN, out _, out _));
    }

    [Fact]
    public void ToLocal_is_the_games_TransformToLocal_on_the_ground()
    {
        // Mat3.TransformToLocal(v) = (s·v, f·v)
        AiInputMath.ToLocal(3, 4, 1, 0, 0, 1, out double lx, out double ly);
        Assert.Equal(3, lx, 9);
        Assert.Equal(4, ly, 9);
        AiInputMath.ToLocal(3, 4, 0, -1, 1, 0, out lx, out ly);
        Assert.Equal(-4, lx, 9);
        Assert.Equal(3, ly, 9);
    }

    [Fact]
    public void The_distance_covered_counts_only_along_the_line()
    {
        Assert.Equal(1.5, AiInputMath.Covered(10, 10, 10, 8.5, 0, -1), 9);          // 1.5 m back
        Assert.Equal(1.5, AiInputMath.Covered(10, 10, 12, 8.5, 0, -1), 9);          // pushed 2 m sideways: still 1.5
        Assert.Equal(-0.5, AiInputMath.Covered(10, 10, 10, 10.5, 0, -1), 9);        // pushed toward his enemy
        Assert.True(AiInputMath.Arrived(2.0, 2.0));
        Assert.False(AiInputMath.Arrived(1.99, 2.0));
        Assert.False(AiInputMath.Arrived(double.NaN, 2.0));
        Assert.True(AiInputMath.Direction(10, 10, 10, 8, out double dx, out double dy));
        Assert.Equal(0, dx, 9);
        Assert.Equal(-1, dy, 9);
        Assert.False(AiInputMath.Direction(1, 1, 1, 1, out _, out _));
    }

    [Fact]
    public void The_attacks_are_free_again_at_the_later_of_the_two_ends()
    {
        Assert.Equal(12.0, AiInputMath.LaterEnd(12.0, 11.5), 9);    // the timer outlasts the step back
        Assert.Equal(12.5, AiInputMath.LaterEnd(12.0, 12.5), 9);    // the step back outlasts the timer
        Assert.Equal(12.0, AiInputMath.LaterEnd(12.0, double.NaN), 9);
        Assert.Equal(11.5, AiInputMath.LaterEnd(double.NaN, 11.5), 9);
        Assert.True(double.IsNaN(AiInputMath.LaterEnd(double.NaN, double.NaN)));
        // a NoAttack hold deferred behind a scripted step: set only with at least 0.1 s of the pause left
        Assert.True(AiInputMath.DeferredStillWorth(13.0, 12.85));
        Assert.False(AiInputMath.DeferredStillWorth(13.0, 12.95));
    }

    [Fact]
    public void The_three_switches_default_on_and_the_rules_read_them_live()
    {
        var s = new TraxSettings();
        Assert.True(s.AttackRatePaceByInput);
        Assert.True(s.AiHoldRaiseGuard);
        Assert.True(s.StepBackBackpedal);
        var sr = StepBackRules.From(s);
        Assert.True(sr.Backpedal);
        Assert.EndsWith("at most 50 at once; a backpedal (StepBackBackpedal on: a backwards input, facing his enemy, until the distance is covered)", sr.Describe());
        s.Set(SettingsSchema.StepBackBackpedal, false, SettingSources.Mcm);
        sr = StepBackRules.From(s);
        Assert.False(sr.Backpedal);
        Assert.EndsWith("; a scripted walk to the spot (StepBackBackpedal off)", sr.Describe());
    }

    [Fact]
    public void The_guard_by_state_and_the_hook_are_summarised()
    {
        var h = new AiHoldStats();
        for (int i = 0; i < 10; i++) h.AddHit(held: true, stepping: false, tired: true, blocked: i < 3);    // held 30%
        for (int i = 0; i < 4; i++) h.AddHit(held: false, stepping: true, tired: true, blocked: i < 1);     // stepping 1 of 4
        for (int i = 0; i < 4; i++) h.AddHit(held: true, stepping: true, tired: true, blocked: i < 3);      // both 3 of 4
        for (int i = 0; i < 10; i++) h.AddHit(held: false, stepping: false, tired: true, blocked: i < 3);   // tired free 30%
        for (int i = 0; i < 10; i++) h.AddHit(held: false, stepping: false, tired: false, blocked: i < 5);  // fresh 50%
        Assert.Equal(0.3, h.HeldShare, 9);
        Assert.Equal(0.5, h.SteppingShare, 9);
        Assert.Equal(0.4, h.OthersShare, 9);
        h.MenHooked = 5;
        h.CallbackAlreadyOn = 2;
        h.CallbackTurnedOn = 7;
        h.CallbackTurnedOff = 6;
        h.CallsWhileActive = 600;
        h.ActiveManSeconds = 10;
        h.AttackCleared = 40;
        h.GuardRaised = 30;
        h.OwnGuardKept = 9;
        h.ReadyCancelled = 1;
        h.BackpedalFrames = 90;
        h.HoldsOverlappingAStep = 12;
        h.Deferred = 3;
        h.DeferredStarted = 2;
        h.DeferredCovered = 1;
        var lines = h.SummaryLines(new AttackRateRules(true, true, false, true), new StepBackRules(true, 100, 2f, 1.5f, 4f, true, 50), 20, 0, 8, 0);
        Assert.Equal(4, lines.Count);
        Assert.StartsWith("AI holds - technique: the AI timer by input (AttackRatePaceByInput on: only the attack bits taken out of his own input, his guard his own, raised when he wants to attack - AiHoldRaiseGuard on) - this battle 20 holds by input, 0 by NoAttack; the step back backpedal (a backwards input, facing his enemy) - this battle 8 backpedals, 0 scripted walks", lines[0]);
        Assert.Equal("AI holds - GUARD (melee hits on AI fighters on foot that were blocked or parried; THE fix target: held and stepping back close to everyone else): held by the timer 30% (n 10), stepping back 50% (n 8) (of them also held by the timer 75% (n 4)), everyone else 40% (n 20) - of them tired (below the peak line) 30% (n 10), at full strength 50% (n 10) - gap to everyone else: held -10 points, stepping back +10 points", lines[1]);
        Assert.Equal("AI holds - the input hook (AgentComponent.OnAIInputSet): 5 men hooked (a component each, added the first time he was held), the callback already on for 2 of them (another mod's component - RTS Camera Command System turns it on for every agent), turned on by us for 7, turned off again when idle 6 times; calls while held 600 (about 60.0 a second per held man); the attack bits taken out in 40 calls (a guard raised in 30, his own guard kept in 9, a ready cancelled in 1), a backpedal written in 90 calls; the player or a non-AI agent passed untouched 0; holds / backpedals the engine never called us during 0 / 0 (must be 0 - else the hook is dead: switch the new ways off and tell Claude); errors 0", lines[2]);
        Assert.Equal("AI holds - the timer survives a step back: holds that overlapped a step back 12 (both ran at once, his attacks held until the later of the two ends); NoAttack holds deferred behind a scripted step back 3 (set when the step ended 2, covered by the step back 1) | AI attacks that started while a hold or a step back held him anyway 0 (must be about 0)", lines[3]);
    }

    [Fact]
    public void A_switch_mid_battle_is_named_and_nothing_measured_reads_na()
    {
        var h = new AiHoldStats { TimerSwitched = true, StepSwitched = true };
        var lines = h.SummaryLines(new AttackRateRules(true, true, false, true, true, 100, paceByInput: false), new StepBackRules(true, 100, 2f, 1.5f, 4f, true, 50, backpedal: false), 3, 4, 1, 2);
        Assert.Contains("the AI timer NoAttack (AttackRatePaceByInput off: the engine's no-attack flag, step 13's technique) - this battle 3 holds by input, 4 by NoAttack (AttackRatePaceByInput SWITCHED mid-battle: the lines mix both)", lines[0]);
        Assert.Contains("the step back scripted walk (SetScriptedPositionAndDirection) - this battle 1 backpedals, 2 scripted walks (StepBackBackpedal SWITCHED mid-battle: the lines mix both)", lines[0]);
        Assert.Contains("held by the timer n/a (n 0), stepping back n/a (n 0)", lines[1]);
        Assert.DoesNotContain("gap to everyone else", lines[1]);
    }

    [Fact]
    public void The_step_back_summary_names_the_technique_the_arrivals_and_every_sample()
    {
        var st = new StepBackStats();
        var r = new StepBackRules(true, 100, 2f, 1.5f, 4f, true, 50);
        Assert.StartsWith("step back - technique: a backpedal - a backwards movement written into the AI's own input", st.SummaryLines(in r)[0]);
        st.AddStart(1, 0, 2.0, backpedal: true);
        st.AddStart(0, 0, 2.0, backpedal: true);
        st.AddSample(0);
        st.AddSample(0);
        st.AddSample(1);
        st.AddSampledStep(backTurnedAny: false);
        st.AddEnd(StepBackEnd.Arrived, 0.9, 2.0, 0.1, 0);
        st.AddSample(2);
        st.AddSampledStep(backTurnedAny: true);
        st.AddEnd(StepBackEnd.EdgeAhead, 0.5, 0.8, 1.2, 0);
        st.InputReleases = 2;
        var lines = st.SummaryLines(in r);
        Assert.Equal(StepBackStats.BackpedalTechnique + "; settings at the end: " + r.Describe(), lines[0].Substring("step back - technique: ".Length));
        Assert.Equal("step back ends: 2 - completed (time up) 0, arrived (StepBackDistance covered) 1, cut short 1 (the ground ends behind him (edge ahead) 1)", lines[3]);
        Assert.Contains("| every 0.25 s (4 samples): facing his enemy 2 (50%), side-on 1, back turned 1; step backs with the back turned at ANY sample 1 of 2 (50%) (must be about 0)", lines[5]);
        Assert.Contains("- about 2.00 m/s", lines[4]);
        Assert.Contains("; backpedals ended by stopping the input 2 (nothing of ours left in the engine)", lines[7]);
        // a switch mid-battle: both techniques named
        st.AddStart(1, 0, 2.0, backpedal: false);
        Assert.StartsWith("step back - technique: MIXED this battle (StepBackBackpedal switched): 2 backpedals, 1 scripted steps - ", st.SummaryLines(in r)[0]);
    }

    [Fact]
    public void Cycles_with_a_step_back_inside_now_count_and_are_shown_apart__against_the_timers_floor()
    {
        var s = new AttackRateStats();
        for (int i = 0; i < 5; i++) s.AddCycle(AttackKind.Melee, false, 0, 1.7, 1f);                     // the fresh reference 1.7 s
        s.AddTimer(AttackKind.Melee, false, 3, 0.85, 0.2f, 3.4);                                          // D 0.85, m 0.2 → floor 4.25 s
        s.AddTimer(AttackKind.Melee, false, 3, 0.85, 0.2f, 3.4);
        Assert.True(s.AddCycle(AttackKind.Melee, false, 3, 6.0, 0.2f, steppedBack: true));
        Assert.True(s.AddCycle(AttackKind.Melee, false, 3, 7.0, 0.2f, steppedBack: true));
        Assert.True(s.AddCycle(AttackKind.Melee, false, 3, 5.0, 0.2f));
        Assert.Equal(3, s.CycleCount(AttackKind.Melee, false, 3));
        Assert.Equal(2, s.CycleCountWithStepBack(AttackKind.Melee, false, 3));
        Assert.Equal(6.5, s.CycleMeanWithStepBack(AttackKind.Melee, false, 3), 9);
        Assert.Equal(4.25, s.TimerFloorMean(AttackKind.Melee, false, 3), 9);
        Assert.Equal(2, s.SteppedBack[AttackRateStats.Group(AttackKind.Melee, false)]);
        var lines = s.SummaryLines(new AttackRateRules(true, true, false, true), false);
        Assert.Contains(lines, l => l.StartsWith("attack rate, melee, AI, empty (f 0):", StringComparison.Ordinal)
                                    && l.Contains("| cycle 6.00 s (n 3; with a step back inside 6.50 s n 2, without 5.00 s n 1), m 0.20 → target 8.50 s: 71% - too fast"));
        Assert.Contains(lines, l => l.StartsWith("attack rate, melee, AI, empty (f 0) - timer: 2", StringComparison.Ordinal)
                                    && l.EndsWith("; the timer's floor D/m avg 4.25 s - the cycle vs it: 141% (n 3), with a step back inside 153%, without 118% (at least ~100% = held as the spec asks)", StringComparison.Ordinal));
    }
}
