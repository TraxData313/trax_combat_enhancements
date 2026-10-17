using Newtonsoft.Json.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 33 (Anton, 2026-10-12): the sprint - a store in seconds = current points / 20, refilled in 7 s standing at full
/// strength (x0.5 moving, down to x0.5 at an empty bar), 20% needed to start, 1.6 x the current run speed for the first 30% of
/// the sprint then a straight line to 1.1 x; the AI sprints on a new order (step 37: everything above the order's reserve - charge keeps 0, advance 50, move 80).</summary>
public class SprintTests
{
    private static SprintRules Sprint(bool on = true, bool ai = true, bool mod = true, bool athletics = true, double pps = 20, int minStart = 20,
        double max = 1.6, double min = 1.1, int fullShare = 30, double refill = 7, double moving = 0.5, double exhausted = 0.5,
        double floor = 0, double reach = 0, int chargeKeep = 0, int advanceKeep = 50, int moveKeep = 80) =>
        new(mod, athletics, on, ai, pps, minStart, max, min, fullShare, refill, moving, exhausted, chargeKeep, advanceKeep, moveKeep, floor, reach);

    private static AthleticsRules Athletics() => AthleticsRules.From(new TraxSettings());

    private static void Near(double expected, double actual, double eps = 1e-6) =>
        Assert.True(Math.Abs(expected - actual) <= eps, "expected " + expected + ", got " + actual);

    // ------------------------------------------------------------------ the store's size

    [Fact]
    public void The_store_is_points_over_20_seconds_and_empty_has_none()
    {
        Near(7.5, SprintMath.Capacity(150, 20));
        Near(2.5, SprintMath.Capacity(50, 20));
        Near(5, SprintMath.Capacity(100, 20));
        Near(10, SprintMath.Capacity(200, 20));
        Near(15, SprintMath.Capacity(300, 20));
        Assert.Equal(0, SprintMath.Capacity(0, 20));
        Assert.Equal(0, SprintMath.Capacity(-3, 20));
        Near(15, SprintMath.Capacity(150, 10));
    }

    [Fact]
    public void The_store_follows_his_CURRENT_points()
    {
        var s = Sprint();
        var r = Athletics();
        var f = new Fighter { AthleticsSkill = 150 };
        Near(7.5, SprintMath.Capacity(in s, in r, f));
        f.Fraction = 0.5;
        Near(3.75, SprintMath.Capacity(in s, in r, f));
        f.Fraction = 0;
        Assert.Equal(0, SprintMath.Capacity(in s, in r, f));
    }

    [Fact]
    public void A_new_store_is_full_until_the_first_clamp_and_a_points_drop_clamps_it()
    {
        var st = new SprintStore();
        Assert.False(st.Clamp(7.5)); // the first look: +inf → 7.5, not counted as a drop
        Near(7.5, st.Stored);
        Assert.True(st.Clamp(5));    // a blow: 150 → 100 points
        Near(5, st.Stored);
        Assert.False(st.Clamp(6));   // points back up: the store does not jump, it refills
        Near(5, st.Stored);
        Assert.True(st.Clamp(0));    // empty: no sprint at all
        Assert.Equal(0, st.Stored);
        st.FillUp();                  // a fresh start
        st.Clamp(7.5);
        Near(7.5, st.Stored);
    }

    // ------------------------------------------------------------------ the refill

    [Fact]
    public void Refill_times_standing_moving_and_tired_combos()
    {
        var s = Sprint();
        Near(7, SprintMath.SecondsToFull(in s, moving: false, peakShare: 1));
        Near(14, SprintMath.SecondsToFull(in s, moving: true, peakShare: 1));
        Near(14, SprintMath.SecondsToFull(in s, moving: false, peakShare: 0));
        Near(28, SprintMath.SecondsToFull(in s, moving: true, peakShare: 0));
        Near(7 / 0.75, SprintMath.SecondsToFull(in s, moving: false, peakShare: 0.5)); // half way to the peak line: x0.75
        Near(7 / 0.375, SprintMath.SecondsToFull(in s, moving: true, peakShare: 0.5));
        Near(1, SprintMath.TiredFactor(in s, 1.3)); // above the line it is 1
        Near(0.5, SprintMath.TiredFactor(in s, -1));
    }

    [Fact]
    public void Refill_is_capacity_over_7_per_second_and_never_while_sprinting_or_above_the_top()
    {
        var s = Sprint();
        Near(7.5 / 7, SprintMath.RefillPerSecond(in s, 7.5, false, 1));
        Assert.Equal(0, SprintMath.RefillPerSecond(in s, 0, false, 1));
        var st = new SprintStore();
        st.Clamp(0);
        st.Clamp(7.5);
        double per = SprintMath.RefillPerSecond(in s, 7.5, false, 1);
        for (int i = 0; i < 35; i++) st.Refill(0.1, per, 7.5);
        Near(3.75, st.Stored, 1e-9); // 3.5 s of 7: half full
        for (int i = 0; i < 100; i++) st.Refill(0.1, per, 7.5);
        Near(7.5, st.Stored); // never above the top
        Assert.Equal(SprintStart.Started, st.TryStart(in s, 0, 7.5, 1, true, SprintOrder.None));
        st.Advance(1, true, out _);
        double before = st.Stored;
        Assert.Equal(0, st.Refill(1, per, 7.5)); // sprinting: nothing
        Assert.Equal(before, st.Stored);
    }

    // ------------------------------------------------------------------ the start gate

    [Fact]
    public void A_start_needs_20_percent_of_the_store_and_an_empty_bar_has_no_sprint()
    {
        var s = Sprint();
        Assert.True(SprintMath.CanStart(in s, 1.5, 7.5));
        Assert.False(SprintMath.CanStart(in s, 1.49, 7.5));
        Assert.False(SprintMath.CanStart(in s, 0, 0));
        var st = new SprintStore();
        st.Clamp(7.5);
        st.Clamp(1.4);
        Assert.Equal(SprintStart.BelowMinimum, st.TryStart(in s, 0, 7.5, 1, true, SprintOrder.None));
        Assert.Equal(SprintStart.NoStore, new SprintStore().TryStart(in s, 0, 0, 1, true, SprintOrder.None));
        var any = Sprint(minStart: 0);
        Assert.Equal(SprintStart.Started, st.TryStart(in any, 0, 7.5, 1, true, SprintOrder.None));
        Assert.Equal(SprintStart.Running, st.TryStart(in any, 0, 7.5, 1, true, SprintOrder.None));
    }

    // ------------------------------------------------------------------ the speed profile

    [Fact]
    public void A_full_sprint_runs_1_6_until_2_25_s_then_a_straight_line_to_1_1_at_7_5_s()
    {
        var s = Sprint();
        Near(1.6, SprintMath.Multiplier(in s, 0, 7.5));
        Near(1.6, SprintMath.Multiplier(in s, 2.25, 7.5));
        Near(1.35, SprintMath.Multiplier(in s, 4.875, 7.5));
        Near(1.1, SprintMath.Multiplier(in s, 7.5, 7.5));
        Near(1.1, SprintMath.Multiplier(in s, 9, 7.5));
        Assert.True(SprintMath.Multiplier(in s, 2.3, 7.5) < 1.6);
    }

    [Fact]
    public void A_30_percent_store_is_2_25_s_long_and_starts_at_full_speed_for_0_675_s()
    {
        var s = Sprint();
        var st = new SprintStore();
        st.Clamp(7.5);
        st.Clamp(2.25); // 30% of a 7.5 s store
        Assert.Equal(SprintStart.Started, st.TryStart(in s, 10, 7.5, 1, true, SprintOrder.None));
        Near(2.25, st.Length);
        Near(1.6, st.Multiplier(in s)); // the dodge: full speed from a partial store
        st.Advance(0.675, true, out _);
        Near(1.6, st.Multiplier(in s));
        st.Advance(0.7875, true, out _);
        Near(1.35, st.Multiplier(in s), 1e-9);
        Assert.True(st.Advance(1, true, out var end));
        Assert.Equal(SprintEnd.StoreEmpty, end);
        Assert.Equal(0, st.Stored);
    }

    [Fact]
    public void The_applied_multiplier_moves_in_0_05_steps()
    {
        Assert.Equal(1.6f, SprintMath.Quantize(1.6));
        Assert.Equal(1.6f, SprintMath.Quantize(1.58));
        Assert.Equal(1.55f, SprintMath.Quantize(1.56));
        Assert.Equal(1.1f, SprintMath.Quantize(1.1));
        Assert.Equal(1f, SprintMath.Quantize(1.0));
        Assert.Equal(0.9f, SprintMath.Quantize(0.9));   // step 35: a tired man's fade ends below 1
        Assert.Equal(0.35f, SprintMath.Quantize(0.33));
        Assert.Equal(0.05f, SprintMath.Quantize(0));     // never 0 (0 = no sprint on his record)
        Assert.Equal(0.05f, SprintMath.Quantize(double.NaN));
    }

    [Fact]
    public void Full_speed_100_percent_stays_at_the_start_speed_and_an_end_speed_above_it_is_capped()
    {
        var all = Sprint(fullShare: 100);
        Near(1.6, SprintMath.Multiplier(in all, 7.4, 7.5));
        var inverted = Sprint(max: 1.3, min: 2);
        Near(1.3, inverted.MinMultiplier);
        Near(1.3, SprintMath.Multiplier(in inverted, 7, 7.5));
        var none = Sprint(fullShare: 0);
        Near(1.35, SprintMath.Multiplier(in none, 3.75, 7.5));
    }

    // ------------------------------------------------------------------ draining, releasing

    [Fact]
    public void The_store_drains_only_while_moving_and_letting_go_keeps_the_rest()
    {
        var s = Sprint();
        var st = new SprintStore();
        st.Clamp(7.5);
        st.TryStart(in s, 0, 7.5, 1, true, SprintOrder.None);
        st.Advance(2, false, out _); // standing still holding the key
        Near(7.5, st.Stored);
        Near(0, st.Elapsed);
        st.Advance(3, true, out _);
        Near(4.5, st.Stored);
        Near(3, st.Elapsed);
        Near(3, st.Stop());
        Assert.False(st.Active);
        Near(4.5, st.Stored); // the rest stays stored
        Assert.Equal(1.0, st.Multiplier(in s));
    }

    // ------------------------------------------------------------------ the AI

    [Fact]
    public void AI_reserve_by_order_kind_and_the_kinds_grouped()
    {
        var s = Sprint();
        Near(0, SprintMath.AiKeep(in s, SprintOrder.Charge));
        Near(0.5, SprintMath.AiKeep(in s, SprintOrder.Advance));
        Near(0.8, SprintMath.AiKeep(in s, SprintOrder.Move));
        Assert.Equal(0, SprintMath.AiKeep(in s, SprintOrder.None));
        Assert.True(SprintMath.AiSprints(SprintOrder.Charge));
        Assert.True(SprintMath.AiSprints(SprintOrder.Advance));
        Assert.True(SprintMath.AiSprints(SprintOrder.Move));
        Assert.False(SprintMath.AiSprints(SprintOrder.None));
        Assert.Equal(SprintOrder.Charge, SprintMath.Group(BraceOrderKind.Charge));
        Assert.Equal(SprintOrder.Charge, SprintMath.Group(BraceOrderKind.ChargeToTarget));
        Assert.Equal(SprintOrder.Charge, SprintMath.Group(BraceOrderKind.AttackEntity));
        Assert.Equal(SprintOrder.Charge, SprintMath.Group(BraceOrderKind.AiCharge));
        Assert.Equal(SprintOrder.Advance, SprintMath.Group(BraceOrderKind.Advance));
        Assert.Equal(SprintOrder.Advance, SprintMath.Group(BraceOrderKind.AiAdvance));
        Assert.Equal(SprintOrder.Move, SprintMath.Group(BraceOrderKind.Move));
        foreach (var k in new[] { BraceOrderKind.Stop, BraceOrderKind.Retreat, BraceOrderKind.FallBack, BraceOrderKind.Follow, BraceOrderKind.FollowEntity,
                     BraceOrderKind.NoOrder, BraceOrderKind.NoFormation })
            Assert.Equal(SprintOrder.None, SprintMath.Group(k));
        Assert.Equal("advance", SprintMath.OrderName(SprintOrder.Advance));
        Assert.Equal("move", SprintMath.OrderName(SprintOrder.Move));
    }

    [Fact]
    public void An_AI_advance_sprint_uses_half_its_FULL_store_and_keeps_the_rest()
    {
        var s = Sprint();
        var st = new SprintStore();
        st.Clamp(7.5);
        Assert.Equal(SprintStart.Started, st.TryStartAi(in s, 0, 7.5, SprintOrder.Advance));
        Assert.Equal(SprintOrder.Advance, st.Order);
        Assert.False(st.ByKey);
        Near(3.75, st.Length);
        Near(1.6, st.Multiplier(in s));
        Assert.False(st.Advance(3.7, true, out _));
        Assert.True(st.Advance(1, true, out var end));
        Assert.Equal(SprintEnd.LengthDone, end);
        Near(3.75, st.Stored);
        Near(1.1, st.Multiplier(in s), 1e-9);
    }

    [Fact]
    public void The_master_switch_Athletics_and_the_sprint_switches_gate_every_start()
    {
        var st = new SprintStore();
        st.Clamp(7.5);
        Assert.Equal(SprintStart.Off, st.TryStart(Sprint(mod: false), 0, 7.5, 1, true, SprintOrder.None));
        Assert.Equal(SprintStart.Off, st.TryStart(Sprint(athletics: false), 0, 7.5, 1, true, SprintOrder.None));
        Assert.Equal(SprintStart.Off, st.TryStart(Sprint(on: false), 0, 7.5, 1, true, SprintOrder.None));
        Assert.Equal(SprintStart.Off, st.TryStart(Sprint(ai: false), 0, 7.5, 1, false, SprintOrder.Charge)); // the AI's own switch
        Assert.Equal(SprintStart.Off, st.TryStartAi(Sprint(ai: false), 0, 7.5, SprintOrder.Charge));
        Assert.Equal(SprintStart.Off, st.TryStartAi(Sprint(mod: false), 0, 7.5, SprintOrder.Charge));
        Assert.Equal(SprintStart.Started, st.TryStart(Sprint(ai: false), 0, 7.5, 1, true, SprintOrder.None)); // yours still works
        Assert.Equal("ModEnabled", Sprint(mod: false).OffBecause);
        Assert.Equal("SprintEnabled", Sprint(on: false).OffBecause);
        Assert.Contains("OFF (ModEnabled)", Sprint(mod: false).Describe("LeftControl"));
        var on = Sprint().Describe("LeftControl");
        Assert.Contains("hold LeftControl", on);
        Assert.Contains("150 points = 7.5 s", on);
        Assert.Contains("x1.60 until 2.25 s", on);
    }

    [Fact]
    public void Settings_read_live_from_the_shared_object()
    {
        var t = new TraxSettings();
        var s = SprintRules.From(t);
        Assert.True(s.Enabled);
        Near(20, s.PointsPerSecond);
        Near(1.6, s.MaxMultiplier, 1e-6);
        Assert.Equal(30, s.FullSpeedSharePercent);
        t.Set(SettingsSchema.SprintAiMoveKeepPercent, 60, "test");
        Assert.Equal(60, SprintRules.From(t).AiMoveKeepPercent);
        t.Set(SettingsSchema.ModEnabled, false, "test");
        Assert.False(SprintRules.From(t).Enabled);
    }

    // ------------------------------------------------------------------ the key setting

    [Fact]
    public void Key_names_match_loosely_and_format_back()
    {
        Assert.True(KeyNames.TryIndex("LeftControl", out int lc));
        Assert.Equal(0, lc);
        Assert.True(KeyNames.TryIndex("left ctrl", out int a));
        Assert.Equal(lc, a);
        Assert.True(KeyNames.TryIndex("LCtrl", out int b));
        Assert.Equal(lc, b);
        Assert.True(KeyNames.TryIndex("x1_mouse_button", out int x1));
        Assert.Equal("X1MouseButton", KeyNames.Name(x1));
        Assert.False(KeyNames.TryIndex("W", out _)); // a movement key is not offered
        Assert.False(KeyNames.TryIndex("", out _));
        Assert.False(KeyNames.TryIndex(null, out _));
        Assert.Equal("LeftControl", SettingsSchema.SprintKey.Format(0));
        Assert.Equal("\"RightAlt\"", SettingsSchema.SprintKey.FormatJson(3));
        Assert.Contains("one of LeftControl, RightControl", SettingsSchema.SprintKey.DefaultAndRangeText);
        Assert.Equal("LeftControl", new TraxSettings().SprintKey);
    }

    [Fact]
    public void The_key_round_trips_through_the_config_file_and_a_bad_name_falls_back_with_a_note()
    {
        var t = new TraxSettings();
        KeyNames.TryIndex("RightAlt", out int ra);
        t.Set(SettingsSchema.SprintKey, ra, "test");
        var text = ConfigFile.Write(t.Snapshot());
        Assert.Contains("\"SprintKey\": \"RightAlt\"", text);
        var read = ConfigFile.Read(text);
        Assert.True(read.Ok);
        Assert.Equal(ra, read.Values["SprintKey"]);

        var loose = ConfigFile.Read("{ \"SprintKey\": \"left alt\" }");
        KeyNames.TryIndex("LeftAlt", out int la);
        Assert.Equal(la, loose.Values["SprintKey"]);

        var bad = ConfigFile.Read("{ \"SprintKey\": \"Banana\" }");
        Assert.False(bad.Values.ContainsKey("SprintKey") && bad.Values["SprintKey"] != SettingsSchema.SprintKey.Default);
        Assert.Contains(bad.Issues, i => i.Key == "SprintKey");
    }

    [Fact]
    public void Defaults_file_reads_a_key_by_name_only()
    {
        Assert.True(DefaultsFile.TryValue(new JValue("CapsLock"), ParamType.Key, 0, KeyNames.All.Length - 1, out double v, out string? problem));
        Assert.Null(problem);
        Assert.Equal("CapsLock", KeyNames.Name(v));
        Assert.False(DefaultsFile.TryValue(new JValue(3), ParamType.Key, 0, KeyNames.All.Length - 1, out _, out problem));
        Assert.Contains("not a key name", problem);
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_names_your_sprints_the_AI_by_order_and_the_speed_check()
    {
        var stats = new SprintStats();
        stats.YouStarted = 2;
        stats.YouRefusedBelowMinimum = 1;
        stats.AddEnd(true, SprintOrder.None, 7.5, 5, SprintEnd.Released);
        stats.AddEnd(true, SprintOrder.None, 2, 2, SprintEnd.StoreEmpty);
        stats.AddOrderSeen(SprintOrder.Charge);
        for (int i = 0; i < 3; i++)
        {
            stats.AddAiStart(SprintOrder.Charge);
            stats.AddEnd(false, SprintOrder.Charge, 6, 4, SprintEnd.InMelee);
        }
        for (int i = 0; i < 25; i++) stats.AddSpeed(1.6, 1.58);
        var lines = new List<string>();
        stats.AddSummaryLines(lines, Sprint(), "LeftControl");
        string all = string.Join("\n", lines);
        Assert.Equal(4, lines.Count);
        Assert.Contains("sprint - you: 2 sprints", all);
        Assert.Contains("below 20% stored 1", all);
        Assert.Contains("key released 1", all);
        Assert.Contains("charge (keeps 0%) 3 men (12.0 s in all, avg 4.00 s)", all);
        Assert.Contains("move (keeps 80%) 0 men (0.0 s in all)", all);
        Assert.Contains("an enemy within 3 m 3", all);
        Assert.Contains("follows the sprint - no cap", all);
        Assert.True(stats.EngineFollows);
        var capped = new SprintStats();
        for (int i = 0; i < 25; i++) capped.AddSpeed(1.6, 1.0);
        Assert.False(capped.EngineFollows);
    }

    // ------------------------------------------------------------------ step 35 (Anton, 2026-10-12, after two tournaments)

    [Fact]
    public void Step35_the_peak_is_on_the_FRESH_run_and_fades_to_1_1_of_the_CURRENT_run()
    {
        var s = Sprint(max: 2.0, min: 1.1);
        // a fresh man: 2.0 → 1.1
        Near(2.0, SprintMath.RunFactor(in s, 0, 7.5, 1));
        Near(2.0, SprintMath.RunFactor(in s, 2.25, 7.5, 1));
        Near(1.55, SprintMath.RunFactor(in s, 4.875, 7.5, 1));
        Near(1.1, SprintMath.RunFactor(in s, 7.5, 7.5, 1));
        // an exhausted man (run x0.3): the same peak, the fade to 1.1 x 0.3 = 0.33
        Near(2.0, SprintMath.RunFactor(in s, 0, 2, 0.3));
        Near(2.0, SprintMath.RunFactor(in s, 0.6, 2, 0.3));
        Near((2.0 + 0.33) / 2, SprintMath.RunFactor(in s, 1.3, 2, 0.3));
        Near(0.33, SprintMath.RunFactor(in s, 2, 2, 0.3));
        Near(0.33, SprintMath.RunFactor(in s, 5, 2, 0.3));
        Assert.Equal(0.35f, SprintMath.Quantize(SprintMath.RunFactor(in s, 2, 2, 0.3)));
        // half way to the peak line (run 0.65 at the floor 0.3): 2.0 → 0.715
        Near(0.715, SprintMath.RunFactor(in s, 5, 5, 0.65));
        // the fresh-man profile is the old relative one
        Near(SprintMath.Multiplier(in s, 4, 7.5), SprintMath.RunFactor(in s, 4, 7.5, 1));
        // the store's factor: his run when no sprint runs
        var st = new SprintStore();
        st.Clamp(2);
        Near(0.3, st.RunFactor(in s, 0.3));
        Assert.Equal(SprintStart.Started, st.TryStart(in s, 0, 2, 1, true, SprintOrder.None));
        Near(2.0, st.RunFactor(in s, 0.3));
        st.Advance(1.3, true, out _);
        Near((2.0 + 0.33) / 2, st.RunFactor(in s, 0.3));
        st.Stop();
        Near(0.3, st.RunFactor(in s, 0.3));
    }

    [Fact]
    public void Step35_the_store_never_drops_below_its_floor()
    {
        var s = Sprint(floor: 2);
        var r = Athletics();
        Near(2, SprintMath.Capacity(0, 20, 2));
        Near(7.5, SprintMath.Capacity(150, 20, 2));
        Near(2, SprintMath.Capacity(30, 20, 2));
        Near(0, SprintMath.Capacity(0, 20, 0));
        var empty = new Fighter { AthleticsSkill = 150, Fraction = 0 };
        Near(2, SprintMath.Capacity(in s, in r, empty));
        Assert.True(SprintMath.OnFloor(in s, in r, empty));
        Near(0, SprintMath.PointsCapacity(in s, in r, empty));
        var small = new Fighter { AthleticsSkill = 50, Fraction = 0.5 };   // pool 50 at 50%: 25 points = 1.25 s
        Near(2, SprintMath.Capacity(in s, in r, small));
        Assert.True(SprintMath.OnFloor(in s, in r, small));
        var big = new Fighter { AthleticsSkill = 150 };                     // pool 150 full: 7.5 s
        Near(7.5, SprintMath.Capacity(in s, in r, big));
        Assert.False(SprintMath.OnFloor(in s, in r, big));
        var noFloor = Sprint();
        Assert.False(SprintMath.OnFloor(in noFloor, in r, empty));          // no floor: never "on" it
        // an exhausted man sprints off his floor - no "empty bar" refusal
        var st = new SprintStore();
        Assert.Equal(SprintStart.Started, st.TryStart(in s, 0, SprintMath.Capacity(in s, in r, empty), 1, true, SprintOrder.None));
        Near(2, st.Length);
        // floor 0: step 33's rule - an empty bar has no store
        Assert.Equal(SprintStart.NoStore, new SprintStore().TryStart(in noFloor, 0, SprintMath.Capacity(in noFloor, in r, empty), 1, true, SprintOrder.None));
    }

    [Fact]
    public void Step35_the_floor_refills_at_the_usual_rule_slowly_when_tired()
    {
        var s = Sprint(floor: 2);
        // capacity / 7 x the factors: an exhausted man standing 14 s, moving 28 s - for 2 s of store
        Near(2 / 7.0 * 0.5, SprintMath.RefillPerSecond(in s, 2, false, 0));
        Near(2 / 7.0 * 0.25, SprintMath.RefillPerSecond(in s, 2, true, 0));
        var st = new SprintStore();
        st.Clamp(2);
        st.TryStart(in s, 0, 2, 1, true, SprintOrder.None);
        Assert.True(st.Advance(3, true, out var end));
        Assert.Equal(SprintEnd.StoreEmpty, end);
        st.Stop();
        // a used-up store with a floor: a press is below the minimum, not "no store"
        Assert.Equal(SprintStart.BelowMinimum, st.TryStart(in s, 0, 2, 1, true, SprintOrder.None));
        double per = SprintMath.RefillPerSecond(in s, 2, false, 0);
        for (int i = 0; i < 70; i++) st.Refill(0.1, per, 2);
        Near(1, st.Stored, 1e-9);           // 7 s of 14: half
        for (int i = 0; i < 70; i++) st.Refill(0.1, per, 2);
        Near(2, st.Stored, 1e-9);           // 14 s: full
    }

    [Fact]
    public void Step35_acceleration_is_capped_while_sprinting_and_0_leaves_the_game_alone()
    {
        Assert.Equal(1.0f, SprintMath.TopSpeedReach(2.6f, 1.0));
        Assert.Equal(2.6f, SprintMath.TopSpeedReach(2.6f, 0));
        Assert.Equal(2.6f, SprintMath.TopSpeedReach(2.6f, double.NaN));
        Assert.Equal(0.8f, SprintMath.TopSpeedReach(0.8f, 1.0));            // a game value already quicker stays
        Assert.Equal(0.1f, SprintMath.TopSpeedReach(2.6f, 0.02));           // never below 0.1 s
        Near(0.1, Sprint(reach: 0.05).TopSpeedReachSeconds);
        Assert.Equal(0, Sprint(reach: -1).TopSpeedReachSeconds);
        Assert.Equal(0, Sprint(reach: 0).TopSpeedReachSeconds);
        Near(1, Sprint(reach: 1).TopSpeedReachSeconds);
    }

    [Fact]
    public void Step35_settings_live_and_the_master_switch_first()
    {
        var t = new TraxSettings();
        var s = SprintRules.From(t);
        Near(2, s.MinSeconds, 1e-6);                 // DESIGN's initial values
        Near(1, s.TopSpeedReachSeconds, 1e-6);
        t.Set(SettingsSchema.SprintMinSeconds, 3.5, "test");
        t.Set(SettingsSchema.SprintTopSpeedReachSeconds, 0.6, "test");
        t.Set(SettingsSchema.SprintMaxMultiplier, 2.0, "test");
        s = SprintRules.From(t);
        Near(3.5, s.MinSeconds, 1e-6);
        Near(0.6, s.TopSpeedReachSeconds, 1e-6);
        Near(2.0, s.MaxMultiplier, 1e-6);
        t.Set(SettingsSchema.SprintTopSpeedReachSeconds, 0, "test");
        Assert.Equal(0, SprintRules.From(t).TopSpeedReachSeconds);
        // the master switch: no sprint even off a floor
        var st = new SprintStore();
        Assert.Equal(SprintStart.Off, st.TryStart(Sprint(mod: false, floor: 2), 0, 2, 1, true, SprintOrder.None));
        Assert.Equal(SprintStart.Off, st.TryStart(Sprint(athletics: false, floor: 2), 0, 2, 1, true, SprintOrder.None));
        var on = Sprint(max: 2.0, floor: 2, reach: 1).Describe("LeftControl");
        Assert.Contains("never below 2.0 s (an empty bar still holds 2.0 s)", on);
        Assert.Contains("speed x2.00 of his FRESH run speed (whatever his Athletics)", on);
        Assert.Contains("a straight line to x1.10 of his CURRENT run speed", on);
        Assert.Contains("top speed reached in at most 1.0 s while sprinting", on);
        Assert.Contains("the game's own acceleration (SprintTopSpeedReachSeconds 0)", Sprint().Describe("LeftControl"));
        Assert.Contains("empty = 0", Sprint().Describe("LeftControl"));
    }

    [Fact]
    public void Step35_the_summary_counts_floor_starts_and_the_acceleration()
    {
        var stats = new SprintStats();
        stats.YouStarted = 3;
        stats.YouOffFloor = 2;
        stats.AiOffFloor = 7;
        stats.AddReach(2.6, 1.0);
        stats.AddReach(2.4, 1.0);
        for (int i = 0; i < 25; i++) stats.AddSpeed(2.0, 2.0);
        var lines = new List<string>();
        stats.AddSummaryLines(lines, Sprint(max: 2.0, floor: 2, reach: 1), "LeftControl");
        string all = string.Join("\n", lines);
        Assert.Equal(4, lines.Count);
        Assert.Contains("; 2 started off the 2.0 s floor (his points alone stored less)", all);
        Assert.Contains("; off the 2.0 s floor 7", all);
        Assert.Contains("sprint speed check (x his FRESH run speed = his top at the sprint's start ÷ his run multiplier then, while it ran)", all);
        Assert.Contains("acceleration while sprinting: the game's top-speed reach avg 2.50 s → ours 1.00 s (sprint starts: 2)", all);
        var none = new List<string>();
        new SprintStats().AddSummaryLines(none, Sprint(), "LeftControl");
        Assert.DoesNotContain("floor", string.Join("\n", none));
        Assert.Contains("acceleration: the game's own (SprintTopSpeedReachSeconds 0)", string.Join("\n", none));
    }

    // ------------------------------------------------------------------ step 36 / 37 (Anton, 2026-10-12): the AI sprint by order -
    // one number per order, the reserve he keeps; he sprints everything above it

    private const double Full = 5; // Anton's examples: a full store of 5 s

    [Theory]
    [InlineData(1.0, 1.0)]   // move from 100%: 20% (1 s), down to 80%
    [InlineData(0.9, 0.5)]   // Anton's example - from 90%: 10% until he drops to 80%
    [InlineData(0.6, 0.0)]   // from 60%: none - below the reserve
    [InlineData(0.8, 0.0)]   // exactly at the reserve: none
    [InlineData(0.83, 0.0)]  // 0.15 s above it: below the 0.2 s minimum
    public void Step37_a_MOVE_keeps_80_percent_and_sprints_the_rest(double storedShare, double seconds)
    {
        Near(seconds, SprintMath.AiAmount(Sprint(), SprintOrder.Move, storedShare * Full, Full), 1e-9);
    }

    [Theory]
    [InlineData(1.0, 2.5)]   // advance from 100%: 50%
    [InlineData(0.9, 2.0)]   // from 90%: 40%
    [InlineData(0.6, 0.5)]   // from 60%: 10%
    [InlineData(0.45, 0.0)]  // from 45%: none - below the reserve
    public void Step37_an_ADVANCE_keeps_half_and_sprints_the_rest(double storedShare, double seconds)
    {
        Near(seconds, SprintMath.AiAmount(Sprint(), SprintOrder.Advance, storedShare * Full, Full), 1e-9);
    }

    [Theory]
    [InlineData(1.0, 5.0)]   // from 100%: all of it
    [InlineData(0.6, 3.0)]
    [InlineData(0.1, 0.5)]   // 10%: below step 33's 20% start gate, yet the AI sprints - its gate is the reserve and the 0.2 s minimum
    public void Step37_a_CHARGE_keeps_nothing_and_sprints_everything(double storedShare, double seconds)
    {
        Near(seconds, SprintMath.AiAmount(Sprint(), SprintOrder.Charge, storedShare * Full, Full), 1e-9);
    }

    [Fact]
    public void Step37_other_orders_never_and_a_keep_above_the_store_is_none()
    {
        var s = Sprint();
        Assert.Equal(0, SprintMath.AiAmount(in s, SprintOrder.None, Full, Full));
        var keepAll = Sprint(chargeKeep: 100);
        Assert.Equal(0, SprintMath.AiAmount(in keepAll, SprintOrder.Charge, Full, Full));
        Assert.True(SprintMath.AiReserveStops(in keepAll, SprintOrder.Charge, Full, Full));
        Assert.Equal(0, SprintMath.AiAmount(Sprint(moveKeep: 90), SprintOrder.Move, 0.8 * Full, Full));
        Assert.False(SprintMath.AiReserveStops(in s, SprintOrder.Charge, 0.01, Full)); // keep 0: empty is not "the reserve"
        Assert.True(SprintMath.AiReserveStops(in s, SprintOrder.Advance, 0.45 * Full, Full));
        Assert.Equal(0, SprintMath.AiAmount(in s, SprintOrder.Move, Full, 0));
        Near(1.0, SprintMath.AiAmount(in s, SprintOrder.Move, double.PositiveInfinity, Full)); // a new store (full until clamped)
    }

    [Fact]
    public void Step37_a_move_then_a_second_move_sprints_only_down_to_the_reserve_and_the_charge_takes_it()
    {
        var s = Sprint();
        var st = new SprintStore();
        st.Clamp(Full);
        Assert.Equal(SprintStart.Started, st.TryStartAi(in s, 0, Full, SprintOrder.Move));
        Near(1.0, st.Length);
        Assert.True(st.Advance(2, true, out var end));
        Assert.Equal(SprintEnd.LengthDone, end);
        st.Stop();
        Near(4.0, st.Stored);       // left 80%
        Assert.Equal(SprintStart.ReserveKept, st.TryStartAi(in s, 3, Full, SprintOrder.Move));
        Assert.False(st.Active);
        Near(4.0, st.Stored);
        // the reserve is there for the real charge: all of it
        Assert.Equal(SprintStart.Started, st.TryStartAi(in s, 6, Full, SprintOrder.Charge));
        Near(4.0, st.Length);
        st.Stop();
        // an advance from full: half
        var adv = new SprintStore();
        adv.Clamp(Full);
        Assert.Equal(SprintStart.Started, adv.TryStartAi(in s, 0, Full, SprintOrder.Advance));
        Near(2.5, adv.Length);
        // an empty store with no reserve: below the minimum, not "reserve kept"
        var empty = new SprintStore();
        empty.Clamp(Full);
        empty.TryStartAi(in s, 0, Full, SprintOrder.Charge);
        empty.Advance(10, true, out _);
        empty.Stop();
        Assert.Equal(SprintStart.BelowMinimum, empty.TryStartAi(in s, 1, Full, SprintOrder.Charge));
        Assert.Equal(SprintStart.ReserveKept, empty.TryStartAi(in s, 1, Full, SprintOrder.Advance));
        Assert.Equal(SprintStart.Off, empty.TryStartAi(in s, 1, Full, SprintOrder.None));
        Assert.Equal(SprintStart.NoStore, new SprintStore().TryStartAi(in s, 1, 0, SprintOrder.Charge));
    }

    [Fact]
    public void Step37_settings_read_live_the_master_switch_first_and_the_sentence_names_each_reserve()
    {
        var t = new TraxSettings();
        var s = SprintRules.From(t);
        Assert.Equal(0, s.AiChargeKeepPercent);
        Assert.Equal(50, s.AiAdvanceKeepPercent);
        Assert.Equal(80, s.AiMoveKeepPercent);
        Assert.Contains("on a NEW order he sprints what he holds above the order's reserve (stored - keep x his full store), nothing below 0.2 s", s.Describe("LeftControl"));
        Assert.Contains("charge keeps 0%, advance keeps 50%, move keeps 80%; hold, retreat, fall back, follow none", s.Describe("LeftControl"));
        Assert.Contains("no 20% start gate for the AI", s.Describe("LeftControl"));
        t.Set(SettingsSchema.SprintAiMoveKeepPercent, 40, "test");
        t.Set(SettingsSchema.SprintAiChargeKeepPercent, 30, "test");
        t.Set(SettingsSchema.SprintAiAdvanceKeepPercent, 100, "test");
        var now = SprintRules.From(t);
        Near(0.2 * Full, SprintMath.AiAmount(in now, SprintOrder.Move, 0.6 * Full, Full)); // 60% now above a 40% reserve
        Near(0.7 * Full, SprintMath.AiAmount(in now, SprintOrder.Charge, Full, Full));
        Assert.Equal(0, SprintMath.AiAmount(in now, SprintOrder.Advance, Full, Full));     // 100 = never on an advance
        Assert.Contains("charge keeps 30%, advance keeps 100%, move keeps 40%", now.Describe("LeftControl"));
        t.Set(SettingsSchema.ModEnabled, false, "test");
        var off = SprintRules.From(t);
        Assert.False(off.AiOn);
        var st = new SprintStore();
        st.Clamp(Full);
        Assert.Equal(SprintStart.Off, st.TryStartAi(in off, 0, Full, SprintOrder.Charge));
        Assert.Equal(SprintStart.Off, st.TryStartAi(in off, 0, Full, SprintOrder.Move));
        Assert.DoesNotContain("keeps", off.Describe("LeftControl"));
        t.Set(SettingsSchema.ModEnabled, true, "test");
        Assert.Equal(SprintStart.Started, st.TryStartAi(SprintRules.From(t), 0, Full, SprintOrder.Move));
    }

    [Fact]
    public void Step37_the_summary_counts_the_kinds_and_the_reserve_kept()
    {
        var stats = new SprintStats();
        stats.AddOrderSeen(SprintOrder.Move);
        stats.AddOrderSeen(SprintOrder.Advance);
        stats.AddAiStart(SprintOrder.Move);
        stats.AddEnd(false, SprintOrder.Move, 1, 1, SprintEnd.LengthDone);
        stats.AddAiStart(SprintOrder.Advance);
        stats.AddEnd(false, SprintOrder.Advance, 2.5, 2, SprintEnd.InMelee);
        for (int i = 0; i < 3; i++) stats.AddAiReserveKept(SprintOrder.Move);
        stats.AddAiRefused(SprintOrder.Charge);
        var lines = new List<string>();
        stats.AddSummaryLines(lines, Sprint(), "LeftControl");
        string ai = lines[2];
        Assert.StartsWith("sprint - AI: new orders seen - charge 0, advance 1, move 1, other 0; sprints - charge (keeps 0%) 0 men (0.0 s in all), "
                          + "advance (keeps 50%) 1 men (2.0 s in all, avg 2.00 s), move (keeps 80%) 1 men (1.0 s in all, avg 1.00 s)"
                          + "; not sprinted: reserve kept 3 (charge 0, advance 0, move 3), less than 0.2 s or empty: charge 1, advance 0, move 0", ai);
        Assert.Equal(3, stats.AiReserveKeptTotal);
    }

    [Fact]
    public void Step37_a_format_6_config_drops_the_use_keys_and_moves_the_old_reserve_defaults_once()
    {
        Assert.Equal(8, ConfigFile.FormatVersion);
        // a step 36 file at its defaults
        string six = "{ \"ConfigVersion\": 6, \"SprintAiChargeUsePercent\": 100, \"SprintAiChargeKeepPercent\": 0, \"SprintAiAdvanceUsePercent\": 50, "
                     + "\"SprintAiAdvanceKeepPercent\": 0, \"SprintAiMoveUsePercent\": 20, \"SprintAiMoveKeepPercent\": 70, \"Typo\": 1 }";
        var read = ConfigFile.Read(six);
        Assert.Equal(4, read.Unknown.Count);
        var notes = ConfigFile.Migrate(read);
        Assert.Equal(5, notes.Count);
        Assert.Contains("SprintAiChargeUsePercent 100 dropped (format 6 → 8; step 37, Anton: one number per order - he sprints everything above the order's reserve (its KeepPercent))", notes);
        Assert.Contains(notes, n => n.StartsWith("SprintAiAdvanceUsePercent 50 dropped (format 6 → 8; step 37", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("SprintAiMoveUsePercent 20 dropped (format 6 → 8; step 37", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("SprintAiAdvanceKeepPercent: 0 → 50 (format 6 → 8, the old default; step 37", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("SprintAiMoveKeepPercent: 70 → 80 (format 6 → 8, the old default; step 37", StringComparison.Ordinal));
        Assert.Equal(0, read.Values["SprintAiChargeKeepPercent"]);     // the charge's reserve was already 0
        Assert.Equal(50, read.Values["SprintAiAdvanceKeepPercent"]);
        Assert.Equal(80, read.Values["SprintAiMoveKeepPercent"]);
        Assert.Equal("Typo", Assert.Single(read.Unknown).Key);         // the typo stays reported
        var rewritten = ConfigFile.Write(read.Values, read.Unknown);
        Assert.DoesNotContain("UsePercent", rewritten);
        Assert.Contains("\"ConfigVersion\": 8", rewritten);
        Assert.Contains("\"SprintAiMoveKeepPercent\": 80", rewritten);
        // idempotent: the rewritten file migrates to nothing
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read(rewritten)));

        // user values kept: a reserve he set himself stays, his use keys still go
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 6, \"SprintAiAdvanceKeepPercent\": 20, \"SprintAiMoveKeepPercent\": 60, \"SprintAiMoveUsePercent\": 35 }");
        var mineNotes = ConfigFile.Migrate(mine);
        Assert.Equal("SprintAiMoveUsePercent 35 dropped (format 6 → 8; step 37, Anton: one number per order - he sprints everything above the order's reserve (its KeepPercent))",
            Assert.Single(mineNotes));
        Assert.Equal(20, mine.Values["SprintAiAdvanceKeepPercent"]);
        Assert.Equal(60, mine.Values["SprintAiMoveKeepPercent"]);
        Assert.Empty(mine.Unknown);
        // a format-7 file's 0 / 70 are his own now: kept
        var seven = ConfigFile.Read("{ \"ConfigVersion\": 7, \"SprintAiAdvanceKeepPercent\": 0, \"SprintAiMoveKeepPercent\": 70 }");
        Assert.Empty(ConfigFile.Migrate(seven));
        Assert.Equal(0, seven.Values["SprintAiAdvanceKeepPercent"]);
        Assert.Equal(70, seven.Values["SprintAiMoveKeepPercent"]);
    }

    [Fact]
    public void Step36_37_an_older_config_drops_step_33s_two_shares_once()
    {
        // Anton's config.json of 2026-10-12 morning: format 5 with step 33's two keys
        string old = "{ \"ConfigVersion\": 5, \"SprintAiChargeSharePercent\": 90, \"SprintAiMoveSharePercent\": 50, \"Typo\": 1 }";
        var read = ConfigFile.Read(old);
        Assert.Equal(3, read.Unknown.Count);
        var notes = ConfigFile.Migrate(read);
        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, n => n.StartsWith("SprintAiChargeSharePercent 90 dropped (format 5 → 8; step 33's charge share", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("SprintAiMoveSharePercent 50 dropped (format 5 → 8; step 33's advance-and-move share", StringComparison.Ordinal));
        Assert.Equal("Typo", Assert.Single(read.Unknown).Key);
        Assert.False(read.Values.ContainsKey("SprintAiMoveKeepPercent")); // the reserves take their defaults
        Assert.Equal(2, ConfigFile.Migrate(read).Count);                 // idempotent: nothing new the second time
        var rewritten = ConfigFile.Write(read.Values, read.Unknown);
        Assert.DoesNotContain("SprintAiChargeSharePercent", rewritten);
        Assert.DoesNotContain("SprintAiMoveSharePercent", rewritten);
        // a non-number goes the same way
        Assert.Contains("dropped", Assert.Single(ConfigFile.Migrate(ConfigFile.Read("{ \"SprintAiChargeSharePercent\": \"lots\" }"))));
        // a current file: nothing
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 7, \"SprintAiMoveKeepPercent\": 60 }")));
    }
}
