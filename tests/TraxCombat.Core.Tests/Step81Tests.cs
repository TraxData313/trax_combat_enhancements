using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 81 - KICKS AND BASHES AS A SHARE OF THE MELEE MOVES (Toni 2026-10-22: "since they add good disturbance to the battle and also dont do much
/// damage I want to try increase them: shielded men kick 20% of the time, shield bash 35% and swing the rest. If they have a spear prefer holding
/// ground with it like now, and if the enemy comes close then use the other skills. Unshielded - 35% kick and the rest swing ... make the min swing
/// stack from 2 to 3 ... Then make all those moves take 8 athletics each"; AI_NOTES "Step 81", DESIGN decision 77): the three shares and their
/// validation, the seeded rolls, the spear rule (nothing rolled beyond kick reach), the refusal groups, the swing stack's minimum 3, the cost 8,
/// config format 19, the summary's shares line.
/// </summary>
public class Step81Tests
{
    private static AiKickRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiNoShieldPushPercent, 0, "t"); // step 81's rules: no weapon push (step 88's share pinned off)
        tune?.Invoke(s);
        return AiKickRules.From(s);
    }

    /// <summary>defaults.json's step-81 values (the tests run on DESIGN's initial 2 / 3 / 10 for the moved keys).</summary>
    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.SwingStackMinimum ? 3 : p == SettingsSchema.CostPerKickOrBash ? 8.0 : p == SettingsSchema.AiKickCooldownSeconds ? 2.0 : p.Default;

    private sealed class Seeded : IRandomSource
    {
        private readonly Random _r;

        public Seeded(int seed) => _r = new Random(seed);

        public double NextDouble() => _r.NextDouble();
    }

    [Fact]
    public void The_three_shares_follow_the_switch_on_the_Advanced_page_live_and_shipped_20_35_35()
    {
        var all = SettingsSchema.All.ToList();
        int on = all.IndexOf(SettingsSchema.AiKicksEnabled);
        var keys = new[] { SettingsSchema.AiShieldmanKickPercent, SettingsSchema.AiShieldmanBashPercent, SettingsSchema.AiNoShieldKickPercent };
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Equal(on + 1 + i, all.IndexOf(keys[i]));
            Assert.Same(SettingsSchema.ImpactGroup, keys[i].Group);
            Assert.Equal(SettingsPage.Advanced, keys[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, keys[i].Timing);
            Assert.Equal(0, keys[i].Min, 9);
            Assert.Equal(100, keys[i].Max, 9);
        }
        var r = Rules();
        Assert.Equal(0.20, r.ShieldKick, 9);
        Assert.Equal(0.35, r.ShieldBash, 9);
        Assert.Equal(0.45, r.SwingShare(true), 9);
        Assert.Equal(0.35, r.NoShieldKick, 9);
        Assert.Equal(0.65, r.SwingShare(false), 9);
        Assert.False(r.ShieldSharesCut);
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiNoShieldKickPercent, 50, SettingSources.Mcm);
        Assert.Equal(0.5, AiKickRules.From(s).NoShieldKick, 9); // live
        string text = DefaultsFile.ReadEmbeddedText();
        // step 88 moved the shieldman's shipped shares to 25 / 40 (Step88Tests), step 95 to 27 / 43 and the no-shield kick to 45 (Step95Tests); the rest as step 81 shipped them
        foreach (var line in new[] { "\"AiShieldmanKickPercent\": 27,", "\"AiShieldmanBashPercent\": 43,", "\"AiNoShieldKickPercent\": 45,", "\"SwingStackMinimum\": 3,",
                     "\"CostPerKickOrBash\": 6.0,", "\"AiKickCooldownSeconds\": 1.0,", "\"CostPerBlow\": 7.0," }) // step 94: 8 → 6, 10 → 7; step 97: the cooldown 2 → 1
            Assert.Contains(line, text, StringComparison.Ordinal);
        foreach (var gone in ConfigFile.RetiredKickKeys) Assert.DoesNotContain("\"" + gone + "\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_shieldmans_kick_and_bash_never_go_over_100_the_bash_gives_way()
    {
        var over = Rules(s => { s.Set(SettingsSchema.AiShieldmanKickPercent, 70, "t"); s.Set(SettingsSchema.AiShieldmanBashPercent, 60, "t"); });
        Assert.True(over.ShieldSharesCut);
        Assert.Equal(0.70, over.ShieldKick, 9);
        Assert.Equal(0.30, over.ShieldBash, 9);
        Assert.Equal(0, over.SwingShare(true), 9);
        Assert.Contains("kick 70% / shield bash 30% / swing 0% (kick + bash set over 100% - the bash cut to what is left)", over.Describe(), StringComparison.Ordinal);
        var exact = Rules(s => { s.Set(SettingsSchema.AiShieldmanKickPercent, 40, "t"); s.Set(SettingsSchema.AiShieldmanBashPercent, 60, "t"); });
        Assert.False(exact.ShieldSharesCut);
        Assert.Equal(0.60, exact.ShieldBash, 9);
        // the constructor clamps out-of-range numbers too
        var wild = new AiKickRules(true, true, true, 1.6, 40, 2, true, 150, -5, 300);
        Assert.Equal(1, wild.ShieldKick, 9);
        Assert.Equal(0, wild.ShieldBash, 9);
        Assert.Equal(1, wild.NoShieldKick, 9);
        Assert.Contains("a SHIELDMAN kick 20% / shield bash 35% / swing 45%, a man with NO shield kick 35% / weapon push 0% / swing 65%", Rules().Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_roll_splits_the_die_kick_then_bash_then_swing()
    {
        var r = Rules();
        Assert.Equal(AiKickKind.Kick, AiKickMath.Roll(in r, true, 0.0));
        Assert.Equal(AiKickKind.Kick, AiKickMath.Roll(in r, true, 0.1999));
        Assert.Equal(AiKickKind.Bash, AiKickMath.Roll(in r, true, 0.2));
        Assert.Equal(AiKickKind.Bash, AiKickMath.Roll(in r, true, 0.5499));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, true, 0.55));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, true, 0.9999));
        Assert.Equal(AiKickKind.Kick, AiKickMath.Roll(in r, false, 0.3499));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, false, 0.35)); // no shield never bashes
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, false, double.NaN));
        var none = Rules(s => { s.Set(SettingsSchema.AiShieldmanKickPercent, 0, "t"); s.Set(SettingsSchema.AiShieldmanBashPercent, 0, "t"); });
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in none, true, 0.0));
    }

    [Fact]
    public void Seeded_rolls_come_out_near_20_35_45_for_shieldmen_and_35_65_without_a_shield()
    {
        var r = Rules();
        var dice = new Seeded(81);
        const int n = 100000;
        int[] shield = new int[AiKickMath.KindCount], bare = new int[AiKickMath.KindCount];
        for (int i = 0; i < n; i++)
        {
            shield[(int)AiKickMath.Roll(in r, true, dice.NextDouble())]++;
            bare[(int)AiKickMath.Roll(in r, false, dice.NextDouble())]++;
        }
        Assert.InRange(shield[1] / (double)n, 0.195, 0.205);
        Assert.InRange(shield[2] / (double)n, 0.345, 0.355);
        Assert.InRange(shield[0] / (double)n, 0.445, 0.455);
        Assert.InRange(bare[1] / (double)n, 0.345, 0.355);
        Assert.InRange(bare[0] / (double)n, 0.645, 0.655);
        Assert.Equal(0, bare[2]);
        Assert.Equal(0, bare[3]); // step 88's push share pinned 0 here
        // the stats count them the same way: rolled = done when nothing is refused
        var st = new AiKickStats();
        var d2 = new Seeded(7);
        for (int i = 0; i < 2000; i++)
        {
            var k = AiKickMath.Roll(in r, true, d2.NextDouble());
            st.NoteMove(0, k);
            if (k != AiKickKind.None) st.Asked(k, 0);
        }
        Assert.Equal(2000, st.Moves(0));
        Assert.Equal(st.Rolled(0, AiKickKind.None), st.Done(0, AiKickKind.None));
        Assert.Equal(st.Rolled(0, AiKickKind.Bash), st.Done(0, AiKickKind.Bash));
    }

    [Fact]
    public void A_spearman_keeps_his_ground_beyond_kick_reach_and_the_shares_start_once_the_enemy_is_close_in_front()
    {
        var r = Rules();
        Assert.Equal(1.6, r.Reach, 6);
        // a spearman's enemy at a spear's length: nothing rolled, he thrusts (the game's own move, step 76's preference)
        Assert.False(AiKickMath.InReach(in r, 2.5, 0, out var why));
        Assert.Equal(AiKickSkip.OutOfReach, why);
        Assert.False(AiKickMath.InReach(in r, 1.61, 0, out why));
        Assert.False(AiKickMath.InReach(in r, double.NaN, 0, out why));
        Assert.Equal(AiKickSkip.OutOfReach, why);
        // the enemy comes close: in reach and in front = the shares
        Assert.True(AiKickMath.InReach(in r, 1.6, 0, out _));
        Assert.True(AiKickMath.InReach(in r, 1.2, 40, out _));
        Assert.False(AiKickMath.InReach(in r, 1.2, 41, out why)); // beside him: not in front
        Assert.Equal(AiKickSkip.NotFacing, why);
        // both kinds of "not a move" are group 0 - never a refusal
        Assert.Equal(0, AiKickMath.GroupOf(AiKickSkip.OutOfReach));
        Assert.Equal(0, AiKickMath.GroupOf(AiKickSkip.NotFacing));
        // a raised shield facing the shieldman: his rolled bash becomes a kick (KindFor - step 65's rule kept)
        Assert.Equal(AiKickKind.Kick, AiKickMath.KindFor(in r, true, true));
    }

    [Fact]
    public void The_refusals_fall_into_safety_stack_cooldown_and_path()
    {
        Assert.Equal(16, AiKickMath.SkipCount);
        Assert.Equal(AiKickMath.SkipCount, Enum.GetValues(typeof(AiKickSkip)).Length);
        foreach (var s in new[] { AiKickSkip.Staggered, AiKickSkip.Busy, AiKickSkip.NoAthletics, AiKickSkip.PoolFull }) Assert.Equal("safety", AiKickMath.GroupName(AiKickMath.GroupOf(s)));
        Assert.Equal("stack empty", AiKickMath.GroupName(AiKickMath.GroupOf(AiKickSkip.NoSwing)));
        Assert.Equal("cooldown", AiKickMath.GroupName(AiKickMath.GroupOf(AiKickSkip.Cooldown)));
        foreach (var s in new[] { AiKickSkip.TargetOffPath, AiKickSkip.FriendInTheWay, AiKickSkip.BehindFriend }) Assert.Equal("path", AiKickMath.GroupName(AiKickMath.GroupOf(s)));
        foreach (var s in new[] { AiKickSkip.OutOfReach, AiKickSkip.NotFacing, AiKickSkip.NoTarget, AiKickSkip.Mounted, AiKickSkip.Routing, AiKickSkip.Ranged, AiKickSkip.ReadyOver })
            Assert.Equal(0, AiKickMath.GroupOf(s));
        Assert.Equal(2.0, Rules(s => s.Set(SettingsSchema.AiKickCooldownSeconds, 2.0, "t")).Cooldown, 9);
    }

    [Fact]
    public void The_swing_stack_holds_at_least_3_and_a_kick_or_bash_costs_8_with_the_hero_and_leader_multipliers()
    {
        // the minimum 3: an empty man still saves up three; a full 150 bar 8 as before
        Assert.Equal(3, SwingStackMath.Cap(0, 20, 3));
        Assert.Equal(3, SwingStackMath.Cap(40, 20, 3));
        Assert.Equal(4, SwingStackMath.Cap(80, 20, 3));
        Assert.Equal(8, SwingStackMath.Cap(150, 20, 3));
        var s = new TraxSettings();
        s.Set(SettingsSchema.SwingStackMinimum, 3, "t");
        Assert.Equal(3, SwingStackRules.From(s).Minimum);
        Assert.True(SwingStackRules.From(s).KicksOn); // kicks and bashes spend from it (KicksUseSwings, on)
        // the cost: 8 a common man, x 0.75 a hero, x 0.75 again a party leader; swings stay 10
        s.Set(SettingsSchema.CostPerKickOrBash, 8.0, "t");
        s.Set(SettingsSchema.CostPerBlow, 10.0, "t");
        var r = AthleticsRules.From(s);
        var man = new Fighter();
        var hero = new Fighter { IsHero = true };
        var leader = new Fighter { IsHero = true, IsLeader = true };
        Assert.Equal(8.0, AthleticsMath.KickOrBashCostPoints(in r, man), 6);
        Assert.Equal(6.0, AthleticsMath.KickOrBashCostPoints(in r, hero), 6);
        Assert.Equal(4.5, AthleticsMath.KickOrBashCostPoints(in r, leader), 6);
        Assert.Equal(10.0, AthleticsMath.BlowCostPoints(in r, man), 6);
    }

    [Fact]
    public void Format_19_moves_the_untouched_stack_minimum_cost_and_cooldown_keeps_hand_set_ones_and_drops_the_retired_keys()
    {
        Assert.Equal(26, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 18, \"SwingStackMinimum\": 2, \"CostPerKickOrBash\": 6.0, \"AiKickCooldownSeconds\": 8.0, \"AiKicksPerMinute\": 1.2, "
                                   + "\"AiBashShareOfSwingsPercent\": 100, \"AiKickMinBarPercent\": 35 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(6, notes.Count);
        Assert.StartsWith("SwingStackMinimum: 2 → 3 (format 18 → 26, the old default; step 81, Toni: kicks and bashes as a share of the melee moves - the swing stack holds at least 3", notes[0], StringComparison.Ordinal);
        Assert.StartsWith("CostPerKickOrBash: 6.0 → 8.0 (format 18 → 26, the old default; step 81", notes[1], StringComparison.Ordinal);
        Assert.StartsWith("AiKickCooldownSeconds: 8.0 → 2.0 (format 18 → 26, the old default; step 81", notes[2], StringComparison.Ordinal);
        Assert.Contains(notes, n => n.StartsWith("AiKicksPerMinute 1.2 dropped (format 18 → 26; step 81", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("AiBashShareOfSwingsPercent 100 dropped (format 18 → 26; step 81", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("AiKickMinBarPercent 35 dropped (format 18 → 26; step 81", StringComparison.Ordinal));
        Assert.Equal(3, read.Values["SwingStackMinimum"], 9);
        Assert.Equal(8, read.Values["CostPerKickOrBash"], 9);
        Assert.Equal(2, read.Values["AiKickCooldownSeconds"], 9);
        Assert.Empty(read.Unknown);
        // hand-set values are kept and said once
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 18, \"SwingStackMinimum\": 5, \"CostPerKickOrBash\": 4.0, \"AiKickCooldownSeconds\": 6.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(3, kept.Count);
        Assert.StartsWith("SwingStackMinimum: 5 kept - your own value (format 18 → 26; the new default 3", kept[0], StringComparison.Ordinal);
        Assert.StartsWith("CostPerKickOrBash: 4.0 kept - your own value (format 18 → 26; the new default 8.0", kept[1], StringComparison.Ordinal);
        Assert.StartsWith("AiKickCooldownSeconds: 6.0 kept - your own value (format 18 → 26; the new default 2.0", kept[2], StringComparison.Ordinal);
        Assert.Equal(5, mine.Values["SwingStackMinimum"], 9);
        // a format-19 file is never touched (but a retired key pasted in is still dropped); a file without the keys gets the defaults (no note)
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 19, \"SwingStackMinimum\": 2, \"CostPerKickOrBash\": 6.0 }"), Shipped));
        Assert.Single(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 19, \"AiKickWaitSeconds\": 3.0 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 18 }"), Shipped));
        // the cooldown's 8 only since format 13, the cost's 6 only since format 15: an older file's value went through steps 66 / 71
        var twelve = ConfigFile.Read("{ \"ConfigVersion\": 12, \"AiKickCooldownSeconds\": 10.0, \"CostPerKickOrBash\": 3.0 }");
        var chain = ConfigFile.Migrate(twelve, Shipped);
        Assert.Equal(2, read.Values["AiKickCooldownSeconds"], 9);
        Assert.Equal(2, twelve.Values["AiKickCooldownSeconds"], 9);
        Assert.Equal(8, twelve.Values["CostPerKickOrBash"], 9);
        Assert.Equal(2, chain.Count);
        Assert.Equal(14, ConfigFile.RetiredKickKeys.Length);
    }

    [Fact]
    public void The_shares_line_gives_rolled_and_done_per_man_type_the_rate_in_contact_the_refusals_and_the_cost()
    {
        var r = Rules();
        var st = new AiKickStats();
        // 100 shieldmen's moves: rolled 20 kicks, 35 bashes, 45 swings; 5 bashes refused (3 stack, 2 path) → done 20 / 30 / 50
        for (int i = 0; i < 20; i++) { st.NoteMove(0, AiKickKind.Kick); st.Asked(AiKickKind.Kick, 0); }
        for (int i = 0; i < 35; i++) st.NoteMove(0, AiKickKind.Bash);
        for (int i = 0; i < 30; i++) st.Asked(AiKickKind.Bash, 0);
        for (int i = 0; i < 45; i++) st.NoteMove(0, AiKickKind.None);
        for (int i = 0; i < 3; i++) { st.Skips[(int)AiKickSkip.NoSwing]++; st.NoteRefused(0, AiKickSkip.NoSwing); }
        for (int i = 0; i < 2; i++) { st.Skips[(int)AiKickSkip.FriendInTheWay]++; st.NoteRefused(0, AiKickSkip.FriendInTheWay); }
        // 20 moves without a shield: 7 kicks rolled, 1 refused by the cooldown
        for (int i = 0; i < 7; i++) st.NoteMove(1, AiKickKind.Kick);
        for (int i = 0; i < 6; i++) st.Asked(AiKickKind.Kick, 1);
        for (int i = 0; i < 13; i++) st.NoteMove(1, AiKickKind.None);
        st.Skips[(int)AiKickSkip.Cooldown]++;
        st.NoteRefused(1, AiKickSkip.Cooldown);
        st.ContactManSeconds = 600;
        st.PointsSpent = 448;
        st.RaisedShieldKicks = 2;
        string line = st.SharesLine(in r);
        Assert.Equal("ai kicks - shares of the melee moves in kick reach (step 81): shieldmen 100 moves - rolled kick 20.0% / bash 35.0% / swing 45.0% (target 20% / 35% / 45%) "
                     + "→ DONE kick 20.0% / bash 30.0% / swing 50.0%; refused safety 0, stack empty 3, cooldown 0, path 2 | no shield 20 moves - rolled kick 35.0% / push 0.0% / swing 65.0% "
                     + "(target 35% / 0% / 65%) → DONE kick 30.0% / push 0.0% / swing 70.0%; refused safety 0, stack empty 0, cooldown 1, path 0 | kicks 26, bashes 30 and pushes 0 asked = 5.60 a man-minute "
                     + "in contact (10.0 man-minutes); a raised shield kicked instead of bashed 2; refused by reason: no stored swing 3, cooldown 1, a friend in front, beyond the rear-rank reach (step 90) 2; "
                     + "Athletics spent on them 448", line);
        Assert.Equal(line, st.SummaryLines(in r).ToList()[4]);
    }
}
