using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 79 (Toni 2026-10-22: battles that sound alive; AI_NOTES "Step 79", DESIGN decision 75) - WAR CRIES ONLY: the settings (the switch on the player
/// page after the rest's, ten Advanced after the rest's numbers), the master switch first, THE CHANCES (back in / answer / the charge share) and the
/// delays, EVERY RATE LIMIT (the man's, the unit's, the battle's - sliding 1 s windows), the charge roar's unit cooldown, the queue, the summary line.
/// </summary>
public class Step79Tests
{
    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.WarCryBackInPercent, SettingsSchema.WarCryAnswerPercent, SettingsSchema.WarCryAnswerDelaySeconds, SettingsSchema.WarCryChargeSharePercent,
        SettingsSchema.WarCryChargeMaxMen, SettingsSchema.WarCryChargeStaggerSeconds, SettingsSchema.WarCryChargeUnitCooldownSeconds,
        SettingsSchema.WarCryManCooldownSeconds, SettingsSchema.WarCryUnitPerSecond, SettingsSchema.WarCryBattlePerSecond,
    };

    private static WarCryRules On() => new WarCryRules(true, true);

    [Fact]
    public void The_switch_follows_the_rest_on_the_player_page_and_the_numbers_follow_the_rest_numbers()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.RestEnabled) + 1, all.IndexOf(SettingsSchema.WarCriesEnabled));
        Assert.Contains(SettingsSchema.WarCriesEnabled, SettingsSchema.PlayerPage);
        Assert.Equal(1, SettingsSchema.WarCriesEnabled.Default, 9);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.WarCriesEnabled.Description);
        int first = all.IndexOf(SettingsSchema.RestGapDiagnosticMetres) + 1;
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(Advanced[i]));
            Assert.Same(SettingsSchema.CourageGroup, Advanced[i].Group);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
        }
        var r = WarCryRules.From(new TraxSettings());
        Assert.True(r.On);
        Assert.Equal(35, r.BackInPercent);
        Assert.Equal(20, r.AnswerPercent);
        Assert.Equal(1.0, r.AnswerDelaySeconds, 9);
        Assert.Equal(40, r.ChargeSharePercent);
        Assert.Equal(12, r.ChargeMaxMen);
        Assert.Equal(1.5, r.ChargeStaggerSeconds, 9);
        Assert.Equal(15, r.ChargeUnitCooldownSeconds, 9);
        Assert.Equal(8, r.ManCooldownSeconds, 9);
        Assert.Equal(8, r.UnitPerSecond);
        Assert.Equal(16, r.BattlePerSecond);
        var s = new TraxSettings();
        s.Set(SettingsSchema.WarCryBattlePerSecond, 5, SettingSources.Mcm);
        Assert.Equal(5, WarCryRules.From(s).BattlePerSecond); // live
    }

    [Fact]
    public void The_master_switch_comes_first()
    {
        Assert.False(new WarCryRules(false, true).On);
        Assert.Equal("ModEnabled", new WarCryRules(false, false).OffBecause);
        Assert.Equal("WarCriesEnabled", new WarCryRules(true, false).OffBecause);
        Assert.Null(On().OffBecause);
        Assert.Contains("OFF (WarCriesEnabled)", new WarCryRules(true, false).Describe());
        Assert.Contains("sound only", On().Describe());
    }

    [Fact]
    public void The_chances_are_each_causes_own_and_a_seeded_thousand_rolls_land_near_them()
    {
        var r = On();
        Assert.Equal(0.35, WarCryMath.Chance(in r, WarCryCause.BackIn), 9);
        Assert.Equal(0.20, WarCryMath.Chance(in r, WarCryCause.Answer), 9);
        Assert.Equal(0.40, WarCryMath.Chance(in r, WarCryCause.Charge), 9);
        Assert.True(WarCryMath.Rolls(in r, WarCryCause.BackIn, 0.349));
        Assert.False(WarCryMath.Rolls(in r, WarCryCause.BackIn, 0.35));
        Assert.False(WarCryMath.Rolls(in r, WarCryCause.Answer, 0.25));
        var rng = new SeededRandom(79);
        foreach (var cause in new[] { WarCryCause.BackIn, WarCryCause.Answer, WarCryCause.Charge })
        {
            int won = 0;
            for (int i = 0; i < 10000; i++) if (WarCryMath.Rolls(in r, cause, rng.NextDouble())) won++;
            Assert.InRange(won / 10000.0, WarCryMath.Chance(in r, cause) - 0.02, WarCryMath.Chance(in r, cause) + 0.02);
        }
        var none = new WarCryRules(true, true, backInPercent: 0, answerPercent: 0, chargeSharePercent: 0);
        Assert.False(WarCryMath.Rolls(in none, WarCryCause.BackIn, 0));
        var all = new WarCryRules(true, true, backInPercent: 100);
        Assert.True(WarCryMath.Rolls(in all, WarCryCause.BackIn, 0.999999));
        Assert.Equal(100, new WarCryRules(true, true, backInPercent: 250).BackInPercent); // clamped
    }

    [Fact]
    public void Back_in_cries_at_once_answers_and_charges_are_staggered_within_their_spans()
    {
        var r = On();
        Assert.Equal(0, WarCryMath.Delay(in r, WarCryCause.BackIn, 0.9), 9);
        Assert.Equal(0.5, WarCryMath.Delay(in r, WarCryCause.Answer, 0.5), 9);
        Assert.Equal(1.5 * 0.999, WarCryMath.Delay(in r, WarCryCause.Charge, 0.999), 9);
        Assert.Equal(0, WarCryMath.Delay(in r, WarCryCause.Charge, -1), 9);
        Assert.Equal(1.5, WarCryMath.Delay(in r, WarCryCause.Charge, 7), 9); // clamped to the span
    }

    [Fact]
    public void The_man_shouts_at_most_once_in_his_cooldown()
    {
        var r = On();
        double next = WarCryMath.NextAllowed(in r, 100);
        Assert.Equal(108, next, 9);
        Assert.False(WarCryMath.ManReady(next, 107.99));
        Assert.True(WarCryMath.ManReady(next, 108));
        Assert.True(WarCryMath.ManReady(double.NegativeInfinity, 0));
        var lim = new WarCryLimiter(4);
        Assert.Equal(WarCryGate.Man, lim.Check(in r, 104, 0, next));
        Assert.Equal(WarCryGate.Ok, lim.Check(in r, 108.5, 0, next));
    }

    [Fact]
    public void A_unit_never_cries_more_than_its_cap_in_any_second()
    {
        var r = new WarCryRules(true, true, unitPerSecond: 3, battlePerSecond: 60);
        var lim = new WarCryLimiter(4);
        int played = 0;
        for (int i = 0; i < 10; i++)
        {
            double t = 10 + i * 0.05;
            if (lim.Check(in r, t, 1, double.NegativeInfinity) != WarCryGate.Ok)
            {
                Assert.Equal(WarCryGate.Unit, lim.Check(in r, t, 1, double.NegativeInfinity));
                continue;
            }
            lim.Note(t, 1);
            played++;
        }
        Assert.Equal(3, played);
        Assert.Equal(WarCryGate.Ok, lim.Check(in r, 10.4, 2, double.NegativeInfinity)); // another unit is free
        Assert.Equal(WarCryGate.Ok, lim.Check(in r, 11.0, 1, double.NegativeInfinity)); // 1 s after the first: the window slid
        Assert.Equal(3, lim.PeakUnit);
    }

    [Fact]
    public void The_battle_never_cries_more_than_its_cap_in_any_second_however_many_units()
    {
        var r = new WarCryRules(true, true, unitPerSecond: 30, battlePerSecond: 16);
        var lim = new WarCryLimiter(80);
        var times = new List<double>();
        var rng = new SeededRandom(7);
        for (int i = 0; i < 2000; i++) // 1000 men in 80 units asking within 3 s, each twice
        {
            double t = 50 + i * 0.0015;
            int unit = (int)(rng.NextDouble() * 80);
            if (lim.Check(in r, t, unit, double.NegativeInfinity) != WarCryGate.Ok) continue;
            lim.Note(t, unit);
            times.Add(t);
        }
        int peak = 0;
        for (int i = 0, j = 0; i < times.Count; i++)
        {
            while (times[i] - times[j] >= 1.0) j++;
            peak = Math.Max(peak, i - j + 1);
        }
        Assert.Equal(16, peak);
        Assert.Equal(16, lim.PeakBattle);
        Assert.InRange(times.Count, 16 * 3, 16 * 4); // ~3 s of cries
        // unit numbers outside the table share one spare bucket
        Assert.Equal(lim.Unit(-1), lim.Unit(999));
    }

    [Fact]
    public void The_window_slides_and_a_new_mission_clears_it()
    {
        var w = new CryWindow(4);
        w.Add(1.0);
        w.Add(1.5);
        Assert.Equal(2, w.CountAt(1.9));
        Assert.Equal(1, w.CountAt(2.0)); // the 1.0 cry is out at exactly 1 s
        Assert.True(w.Allows(2.0, 2));
        Assert.False(w.Allows(2.0, 1));
        Assert.Equal(0, w.CountAt(0.5)); // time went backwards
        for (int i = 0; i < 10; i++) w.Add(5 + i * 0.01);
        Assert.Equal(4, w.CountAt(5.1)); // never more than its capacity
        Assert.False(w.Allows(5.1, 100)); // the cap is bounded by the capacity
    }

    [Fact]
    public void A_units_charge_roar_cools_down()
    {
        var r = On();
        Assert.True(WarCryMath.ChargeRoars(in r, double.NegativeInfinity, 10));
        Assert.False(WarCryMath.ChargeRoars(in r, 10, 24.9));
        Assert.True(WarCryMath.ChargeRoars(in r, 10, 25));
        Assert.False(WarCryMath.ChargeRoars(new WarCryRules(true, true, chargeMaxMen: 0), double.NegativeInfinity, 10));
        Assert.False(WarCryMath.ChargeRoars(new WarCryRules(true, true, chargeSharePercent: 0), double.NegativeInfinity, 10));
    }

    [Fact]
    public void A_charge_by_many_units_shares_the_battles_cap_over_the_stagger()
    {
        var r = On(); // 16 a second x 1.5 s = 24 cries the battle can play
        Assert.Equal(12, WarCryMath.ChargeShoutersPerUnit(in r, 1));
        Assert.Equal(12, WarCryMath.ChargeShoutersPerUnit(in r, 2));
        Assert.Equal(8, WarCryMath.ChargeShoutersPerUnit(in r, 3));
        Assert.Equal(3, WarCryMath.ChargeShoutersPerUnit(in r, 10));
        Assert.Equal(1, WarCryMath.ChargeShoutersPerUnit(in r, 40));
        Assert.Equal(12, WarCryMath.ChargeShoutersPerUnit(in r, 0));
        var quick = new WarCryRules(true, true, chargeStaggerSeconds: 0.2); // under 1 s the battle's one second counts
        Assert.Equal(4, WarCryMath.ChargeShoutersPerUnit(in quick, 4));
        Assert.Equal(0, WarCryMath.ChargeShoutersPerUnit(new WarCryRules(true, true, chargeMaxMen: 0), 3));
    }

    [Fact]
    public void The_queue_never_allocates_past_its_capacity_and_removes_from_the_end()
    {
        var q = new WarCryQueue<string>(3);
        Assert.True(q.Add("a", 1, WarCryCause.BackIn, 0));
        Assert.True(q.Add("b", 2, WarCryCause.Answer, 1));
        Assert.True(q.Add("c", 3, WarCryCause.Charge, 2));
        Assert.False(q.Add("d", 4, WarCryCause.Charge, 2));
        q.RemoveAt(0);
        Assert.Equal(2, q.Count);
        Assert.Equal("c", q.Man(0));
        Assert.Equal(WarCryCause.Charge, q.Cause(0));
        Assert.Equal(2, q.UnitOf(0));
        q.Clear();
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void The_summary_line_counts_by_cause_and_by_limit()
    {
        var s = new WarCryStats();
        s.Cried[(int)WarCryCause.BackIn] = 4;
        s.Cried[(int)WarCryCause.Charge] = 30;
        s.CriedMounted[(int)WarCryCause.Charge] = 6;
        s.Dropped[(int)WarCryCause.Charge, (int)WarCryGate.Battle] = 12;
        s.Dropped[(int)WarCryCause.Answer, (int)WarCryGate.Man] = 2;
        s.PeakBattle = 16;
        s.PeakUnit = 8;
        s.NoteGap(9.5);
        s.NoteGap(8.2);
        var lines = new List<string>();
        s.AddSummaryLines(lines, On());
        Assert.Single(lines);
        Assert.StartsWith("war cries (step 79) - ON: cries 34", lines[0]);
        Assert.Contains("back in 4 of 0 events", lines[0]);
        Assert.Contains("charge order 30 (riders 6)", lines[0]);
        Assert.Contains("the battle's cap 12", lines[0]);
        Assert.Contains("the man's own limit 2", lines[0]);
        Assert.Contains("the battle 16 (cap 16), one unit 8 (cap 8)", lines[0]);
        Assert.Contains("shortest gap between two cries 8.2 s (limit 8 s)", lines[0]);
    }
}
