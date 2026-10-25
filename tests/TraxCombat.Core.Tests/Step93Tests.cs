using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 93 - AN ATHLETICS FLOOR BY TIER (Toni 2026-10-25, his Vlandia playthrough: the T6 Vlandian Banner Knight has Athletics 60 - the game
/// gives riders Riding instead; AI_NOTES "Step 93", DESIGN decision 85): the six settings placed / shipped / live, the pool = max(the skill ×
/// the per-skill multiplier, the "at least" floor, his tier's floor), T0 = T1's floor, a hero (and the player) untouched, 0 = no floor, the
/// master switch first, a live change keeps the share, the summary's tier text.
/// </summary>
public class Step93Tests
{
    private static readonly ParamDef[] Floors =
    {
        SettingsSchema.AthleticsFloorT1, SettingsSchema.AthleticsFloorT2, SettingsSchema.AthleticsFloorT3,
        SettingsSchema.AthleticsFloorT4, SettingsSchema.AthleticsFloorT5, SettingsSchema.AthleticsFloorT6,
    };

    private static readonly int[] Shipped = { 50, 50, 70, 90, 120, 150 };

    private static AthleticsRules Shipping() => AthleticsRules.From(OldPool.Settings());

    private static Fighter Troop(int skill, int tier, bool hero = false) => new() { AthleticsSkill = skill, Tier = tier, IsHero = hero };

    [Fact]
    public void The_six_floors_sit_on_the_Advanced_page_after_the_old_floor_ship_Tonis_numbers_and_are_live()
    {
        var all = SettingsSchema.All.ToList();
        int at = all.IndexOf(SettingsSchema.AthleticsPoolFloor);
        string defaults = DefaultsFile.ReadEmbeddedText();
        for (int t = 0; t < 6; t++)
        {
            var p = Floors[t];
            Assert.Equal("AthleticsFloorT" + (t + 1), p.Key);
            Assert.Equal(at + 1 + t, all.IndexOf(p));
            Assert.Same(SettingsSchema.AthleticsGroup, p.Group);
            Assert.Equal(SettingsPage.Advanced, p.Group.Page);
            Assert.Equal(ApplyTiming.Live, p.Timing);
            Assert.Equal(ParamType.Int, p.Type);
            Assert.Equal(0, p.Min, 9);
            Assert.Equal(1000, p.Max, 9);
            Assert.Equal(Shipped[t], p.Default, 9);
            Assert.Contains("\"AthleticsFloorT" + (t + 1) + "\": " + Shipped[t] + ",", defaults, StringComparison.Ordinal);
            Assert.Contains("keep the bar of your real skill", p.Description, StringComparison.Ordinal);
        }
        Assert.Equal(SettingsSchema.AthleticsPoolBase, all[at + 7]);     // step 94 sits between them
        Assert.Equal(SettingsSchema.AthleticsPoolPerSkill, all[at + 8]);
        Assert.Equal(25, ConfigFile.FormatVersion); // new keys: no migration

        var s = OldPool.Settings();
        var r = AthleticsRules.From(s);
        for (int t = 1; t <= 6; t++) Assert.Equal(Shipped[t - 1], AthleticsMath.TierFloor(in r, t));
        s.Set(SettingsSchema.AthleticsFloorT6, 200, SettingSources.Mcm);
        Assert.Equal(200, AthleticsMath.TierFloor(AthleticsRules.From(s), 6)); // live: read at use
        Assert.Equal(200.0, AthleticsMath.PoolPoints(AthleticsRules.From(s), Troop(60, 6)), 9);
    }

    [Theory]
    [InlineData(60, 6, 150)]  // the T6 Vlandian Banner Knight / Elite Cataphract / Khan's Guard
    [InlineData(60, 4, 90)]   // Vlandian Cavalry
    [InlineData(80, 5, 120)]  // a T5 rider (median 80)
    [InlineData(20, 2, 50)]   // a T2 rider: the old floor and T2's are both 50
    [InlineData(40, 3, 70)]   // a T3 rider
    [InlineData(100, 4, 100)] // a T4 infantryman: his skill is above the floor
    [InlineData(130, 5, 130)] // a T5 legionary
    [InlineData(170, 6, 170)] // a Fian champion
    [InlineData(10, 1, 50)]   // a recruit
    [InlineData(5, 0, 50)]    // T0 (a looter) takes T1's floor
    [InlineData(60, 9, 150)]  // above 6 counts as 6
    public void The_pool_is_the_biggest_of_the_skill_the_old_floor_and_his_tiers_floor(int skill, int tier, double pool)
    {
        var r = Shipping();
        Assert.Equal(pool, AthleticsMath.PoolPoints(in r, Troop(skill, tier)), 9);
        Assert.Equal(pool, AthleticsMath.PoolPointsFor(in r, skill, tier), 9);
    }

    [Fact]
    public void Each_of_the_three_wins_where_it_is_biggest()
    {
        var s = OldPool.Settings();
        s.Set(SettingsSchema.AthleticsPoolPerSkill, 2.0, SettingSources.Mcm);
        s.Set(SettingsSchema.AthleticsPoolFloor, 100, SettingSources.Mcm);
        var r = AthleticsRules.From(s);
        Assert.Equal(160.0, AthleticsMath.PoolPoints(in r, Troop(80, 6)), 9); // 2 × 80 = 160 > 150 > 100: the skill
        Assert.Equal(150.0, AthleticsMath.PoolPoints(in r, Troop(60, 6)), 9); // 120 < 150: the tier
        Assert.Equal(100.0, AthleticsMath.PoolPoints(in r, Troop(30, 4)), 9); // 60, T4 90 < 100: the old floor
        Assert.True(AthleticsMath.IsLiftedByTier(in r, 60, 6));
        Assert.False(AthleticsMath.IsLiftedByTier(in r, 80, 6));
        Assert.False(AthleticsMath.IsLiftedByTier(in r, 30, 4));
        Assert.True(AthleticsMath.IsAtFloor(in r, 30, 4));
        Assert.False(AthleticsMath.IsAtFloor(in r, 40, 6)); // the tier lifted him, not the old floor
    }

    [Fact]
    public void A_hero_the_player_or_a_man_of_no_tier_keeps_his_real_skill()
    {
        var r = Shipping();
        Assert.Equal(60.0, AthleticsMath.PoolPoints(in r, Troop(60, 6, hero: true)), 9); // a hero's tier never counts
        Assert.Equal(60.0, AthleticsMath.PoolPoints(in r, Troop(60, -1)), 9);            // the module's −1: a hero / the player / unknown
        Assert.Equal(60.0, AthleticsMath.PoolPoints(in r, new Fighter { AthleticsSkill = 60 }), 9); // the default: no tier
        Assert.Equal(-1, new Fighter().Tier);
        Assert.Equal(-1, AthleticsMath.FloorTier(Troop(60, 6, hero: true)));
        Assert.Equal(50.0, AthleticsMath.PoolPoints(in r, Troop(10, 6, hero: true)), 9); // the old floor still holds a hero
    }

    [Fact]
    public void Zero_is_no_floor_for_that_tier_and_the_master_switch_comes_first()
    {
        var s = OldPool.Settings();
        s.Set(SettingsSchema.AthleticsFloorT6, 0, SettingSources.Mcm);
        var r = AthleticsRules.From(s);
        Assert.Equal(0, AthleticsMath.TierFloor(in r, 6));
        Assert.Equal(60.0, AthleticsMath.PoolPoints(in r, Troop(60, 6)), 9);
        Assert.Equal(90.0, AthleticsMath.PoolPoints(in r, Troop(60, 4)), 9); // the others stay
        Assert.False(AthleticsMath.IsLiftedByTier(in r, 60, 6));

        s.Set(SettingsSchema.AthleticsFloorT1, 0, SettingSources.Mcm);
        s.Set(SettingsSchema.AthleticsPoolFloor, 0, SettingSources.Mcm);
        Assert.Equal(5.0, AthleticsMath.PoolPoints(AthleticsRules.From(s), Troop(5, 0)), 9); // T0 = T1's: off too

        var off = OldPool.Settings();
        off.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        var ro = AthleticsRules.From(off);
        Assert.Equal(0, AthleticsMath.TierFloor(in ro, 6));
        Assert.Equal(60.0, AthleticsMath.PoolPoints(in ro, Troop(60, 6)), 9);

        // the constructor's default (the older tests): no tier floors
        var plain = new AthleticsRules(true, 50, 1f, 75, true, 10, true, 0.75f, 0.75f, 0, 20, 0.7f, 1f, true, 2, 1.5f, 60, 0.5f, 0.4f, 50);
        Assert.False(plain.AnyTierFloor);
        Assert.Equal(60.0, AthleticsMath.PoolPoints(in plain, Troop(60, 6)), 9);
    }

    [Fact]
    public void A_live_change_re_pools_him_and_he_keeps_his_share()
    {
        var s = OldPool.Settings();
        var knight = Troop(60, 6);
        var r = AthleticsRules.From(s);
        AthleticsMath.Charge(knight, in r, 0); // 10 of 150
        double share = knight.Fraction;
        Assert.Equal(140.0 / 150.0, share, 9);
        Assert.Equal(140.0, AthleticsMath.Points(in r, knight), 9);
        s.Set(SettingsSchema.AthleticsFloorT6, 300, SettingSources.Mcm);
        var r2 = AthleticsRules.From(s);
        Assert.Equal(share, knight.Fraction, 12);                       // the fraction stays
        Assert.Equal(300.0 * share, AthleticsMath.Points(in r2, knight), 9); // the pool follows at once
        Assert.NotEqual(r.TierFloorsKey, r2.TierFloorsKey);             // the settings-changed check sees it
    }

    [Fact]
    public void The_summary_counts_the_lifted_men_by_tier_with_the_mean_skill_and_pool()
    {
        var stats = new AthleticsStats();
        for (int i = 0; i < 3; i++) stats.AddFighter(60, true, 6);   // Banner Knights
        stats.AddFighter(60, true, 4);                                 // Vlandian Cavalry
        stats.AddFighter(100, true, 4);                                // a T4 infantryman - not lifted
        stats.AddFighter(20, true, 2);                                 // T2 rider: 50 = the old floor, not lifted
        stats.AddFighter(60, true, -1);                                // a hero
        var r = Shipping();
        Assert.True(stats.TierLift(in r, 6, out int men, out double skill, out double pool));
        Assert.Equal(3, men);
        Assert.Equal(60, skill, 9);
        Assert.Equal(150, pool, 9);
        Assert.False(stats.TierLift(in r, 2, out _, out _, out _));
        Assert.Equal("tier floors (non-heroes) T1-T6 50/50/70/90/120/150, lifted by the tier floor: T4 1 (skill 60.0 → pool 90.0) / T6 3 (skill 60.0 → pool 150.0)",
            stats.TierLiftText(in r));
        Assert.True(stats.Pools(in r, out double min, out double mean, out double max, out int atFloor));
        Assert.Equal(50, min);
        Assert.Equal(150, max);
        Assert.Equal((150 * 3 + 90 + 100 + 50 + 60) / 7.0, mean, 9);
        Assert.Equal(1, atFloor); // the T2 rider
        var line = stats.SummaryLines(in r, c => "a" + c, new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, FormationAthleticsStats>>())
            .Single(l => l.StartsWith("Athletics pools", StringComparison.Ordinal));
        Assert.Contains("7 fighters - min 50 / avg 107.1 / max 150; 1 at the floor; tier floors (non-heroes) T1-T6 50/50/70/90/120/150, lifted by the tier floor: T4 1", line);

        var s = OldPool.Settings();
        foreach (var p in Floors) s.Set(p, 0, SettingSources.Mcm);
        Assert.Equal("tier floors (non-heroes) T1-T6 0/0/0/0/0/0 - off", stats.TierLiftText(AthleticsRules.From(s)));
        Assert.Equal("tier floors (non-heroes) T1-T6 50/50/70/90/120/150, nobody lifted by the tier floor", new AthleticsStats().TierLiftText(in r));
    }
}
