using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 66 - MORE AI KICKS AND BASHES, EACH COSTING MORE (Anton's looter battle 2026-10-19 14:16: "ai kicks: asked 0" in 49.4 man-minutes of
/// contact; "not a bad idea for men to kick and bash more - a defensive supplement to all that killing, disturbing"; "make the kick/bash also a
/// bit more taking of the energy, say 5 pts each"; "and shield bashes, maybe make them as frequent as swings when the man has a shield"; AI_NOTES
/// "Step 66", DESIGN decision 59): the five new settings, a decision that waits for reach, a shieldman's swings earning his bashes, the shipped
/// rates (format 13) and THE WORKED EXAMPLES - kicks a minute for a looter / a legionary, and a shieldman's swings and bashes by bar band.
/// </summary>
public class Step66Tests
{
    private static AiKickRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return AiKickRules.From(s);
    }

    /// <summary>defaults.json's step-66 values (the tests run on DESIGN's initial 3 / 0.1 / 10).</summary>
    internal static void Ship(TraxSettings s)
    {
        s.Set(SettingsSchema.CostPerKickOrBash, 5.0, "test");
        s.Set(SettingsSchema.AiKicksPerMinute, 1.2, "test");
        s.Set(SettingsSchema.AiKickCooldownSeconds, 8.0, "test");
    }

    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.CostPerKickOrBash ? 5.0 : p == SettingsSchema.AiKicksPerMinute ? 1.2 : p == SettingsSchema.AiKickCooldownSeconds ? 8.0 : p.Default;

    [Fact]
    public void The_five_settings_follow_the_brace_on_the_Advanced_page_and_live()
    {
        var all = SettingsSchema.All.ToList();
        int last = all.IndexOf(SettingsSchema.KickBraceRefusesKnockBack);
        var keys = new[]
        {
            SettingsSchema.AiKickWaitSeconds, SettingsSchema.AiKickWaitMetres, SettingsSchema.AiBashShareOfSwingsPercent, SettingsSchema.AiBashDelaySeconds,
            SettingsSchema.AiBashCooldownSeconds,
        };
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Equal(last + 1 + i, all.IndexOf(keys[i]));
            Assert.Same(SettingsSchema.ImpactGroup, keys[i].Group);
            Assert.Equal(SettingsPage.Advanced, keys[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, keys[i].Timing);
        }
        var r = Rules();
        Assert.Equal(3.0, r.WaitSeconds, 6);
        Assert.Equal(5.0, r.WaitMetres, 6);
        Assert.Equal(1.0, r.BashShare, 6);
        Assert.Equal(1.5, r.BashDelay, 6);
        Assert.Equal(2.0, r.BashCooldown, 6);
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiBashShareOfSwingsPercent, 50, SettingSources.Mcm);
        Assert.Equal(0.5, AiKickRules.From(s).BashShare, 6); // live
    }

    [Fact]
    public void The_bashes_keep_the_feature_on_without_kicks_and_the_sentence_names_both()
    {
        var bashOnly = Rules(s => s.Set(SettingsSchema.AiKicksPerMinute, 0.0, "t"));
        Assert.True(bashOnly.Enabled);
        Assert.Contains("never kicks a man without a shield (AiKicksPerMinute 0)", bashOnly.Describe(), StringComparison.Ordinal);
        var kicksOnly = Rules(s => s.Set(SettingsSchema.AiBashShareOfSwingsPercent, 0, "t"));
        Assert.True(kicksOnly.Enabled);
        Assert.Contains("a shieldman never bashes (AiBashShareOfSwingsPercent 0)", kicksOnly.Describe(), StringComparison.Ordinal);
        Assert.False(Rules(s => s.Set(SettingsSchema.ModEnabled, false, "t")).Enabled); // the master switch first
        var d = Rules(Ship).Describe();
        Assert.StartsWith("ON - an AI man on foot within 1.6 m of the enemy he fights (within 40° of his facing) KICKS him (no shield in hand) at 1.20 a minute", d, StringComparison.Ordinal);
        Assert.Contains("at least 8.0 s between two; a SHIELDMAN bashes him 100% as often as he swings (each of his melee swings earns 1.00 of a bash, at most 2 saved, "
                        + "spent within ~1.5 s at a full bar", d, StringComparison.Ordinal);
        Assert.Contains("at least 2.0 s after a bash; a decision with that enemy within 5.0 m but not yet in reach waits up to 3.0 s for it", d, StringComparison.Ordinal);
    }

    [Fact]
    public void A_shieldmans_swings_earn_his_bashes_at_most_two_and_the_bash_comes_sooner_the_fresher_he_is()
    {
        var r = Rules();
        double c = AiKickMath.AddBashCredit(in r, 0);
        Assert.Equal(1, c, 9);
        c = AiKickMath.AddBashCredit(in r, c);
        Assert.Equal(2, c, 9);
        Assert.Equal(2, AiKickMath.AddBashCredit(in r, c), 9); // the cap
        Assert.Equal(1, AiKickMath.AddBashCredit(in r, double.NaN), 9);
        var half = Rules(s => s.Set(SettingsSchema.AiBashShareOfSwingsPercent, 50, "t"));
        Assert.Equal(0.5, AiKickMath.AddBashCredit(in half, 0), 9); // one bash per two swings
        // the rate a minute with a whole bash earned: 60 × the bar factor ÷ 1.5 s
        Assert.Equal(0, AiKickMath.BashPerMinute(in r, 0.99, 1.0), 9);
        Assert.Equal(40, AiKickMath.BashPerMinute(in r, 1, 1.0), 9);
        Assert.Equal(20, AiKickMath.BashPerMinute(in r, 2, 0.55), 9);   // half the bar factor - twice the wait
        Assert.Equal(0, AiKickMath.BashPerMinute(in r, 2, 0.34), 9);    // below the bar gate: never
        var none = Rules(s => s.Set(SettingsSchema.AiBashShareOfSwingsPercent, 0, "t"));
        Assert.Equal(0, AiKickMath.BashPerMinute(in none, 2, 1.0), 9);
        // the cooldown by kind
        Assert.Equal(2.0, AiKickMath.CooldownFor(in r, AiKickKind.Bash), 9);
        Assert.Equal(10.0, AiKickMath.CooldownFor(in r, AiKickKind.Kick), 9);
    }

    [Fact]
    public void A_decision_with_the_enemy_close_waits_for_reach_and_a_rate_with_a_cooldown_is_capped()
    {
        var r = Rules();
        Assert.True(AiKickMath.Waits(in r, 2.5));
        Assert.True(AiKickMath.Waits(in r, 5.0));
        Assert.False(AiKickMath.Waits(in r, 5.1));
        var noWait = Rules(s => s.Set(SettingsSchema.AiKickWaitSeconds, 0.0, "t"));
        Assert.False(AiKickMath.Waits(in noWait, 2.5)); // 0 = step 65
        Assert.Equal(0, AiKickMath.WithCooldown(0, 8), 9);
        Assert.Equal(1.2, AiKickMath.WithCooldown(1.2, 0), 9);
        Assert.Equal(1.0 / (8.0 / 60 + 1 / 1.2), AiKickMath.WithCooldown(1.2, 8), 9);
        Assert.True(AiKickMath.WithCooldown(1000, 8) < 7.5 + 1e-9); // never above one per cooldown
    }

    [Fact]
    public void Format_13_moves_the_old_kick_cost_rate_and_cooldown_once_and_keeps_a_tuned_value()
    {
        Assert.Equal(17, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 12, \"CostPerKickOrBash\": 3.0, \"AiKicksPerMinute\": 0.1, \"AiKickCooldownSeconds\": 10.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(3, notes.Count);
        Assert.StartsWith("CostPerKickOrBash: 3.0 → 5.0 (format 12 → 17, the old default; step 66, Anton: a kick or a shield bash takes more of the energy", notes[0], StringComparison.Ordinal);
        Assert.StartsWith("AiKicksPerMinute: 0.1 → 1.2 (format 12 → 17, the old default; step 66, Anton's looter battle: no AI kick was ever seen", notes[1], StringComparison.Ordinal);
        Assert.StartsWith("AiKickCooldownSeconds: 10.0 → 8.0 (format 12 → 17, the old default; step 66", notes[2], StringComparison.Ordinal);
        Assert.Equal(5, read.Values["CostPerKickOrBash"], 9);
        Assert.Equal(1.2, read.Values["AiKicksPerMinute"], 9);
        Assert.Equal(8, read.Values["AiKickCooldownSeconds"], 9);
        // a hand-set value is kept and said once; an older file's 3 moves too (the key since step 18)
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 12, \"CostPerKickOrBash\": 4.0, \"AiKicksPerMinute\": 0.5 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(2, kept.Count);
        Assert.StartsWith("CostPerKickOrBash: 4.0 kept - your own value (format 12 → 17; the new default 5.0", kept[0], StringComparison.Ordinal);
        Assert.StartsWith("AiKicksPerMinute: 0.5 kept - your own value (format 12 → 17; the new default 1.2", kept[1], StringComparison.Ordinal);
        Assert.Equal(4, mine.Values["CostPerKickOrBash"], 9);
        var old = ConfigFile.Read("{ \"ConfigVersion\": 5, \"CostPerKickOrBash\": 3.0 }");
        Assert.Contains(ConfigFile.Migrate(old, Shipped), n => n.StartsWith("CostPerKickOrBash: 3.0 → 5.0 (format 5 → 17", StringComparison.Ordinal));
        // a format-13 file is never touched; a file without the keys gets the defaults (no note)
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 13, \"CostPerKickOrBash\": 3.0, \"AiKicksPerMinute\": 0.1 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 12 }"), Shipped));
    }

    [Fact]
    public void The_summary_line_counts_the_bashes_per_swing_and_the_waits()
    {
        var st = new AiKickStats();
        st.ShieldSwings = 40;
        st.CreditEarned = 38;
        st.CreditLost = 2;
        for (int i = 0; i < 30; i++) st.Asked(AiKickKind.Bash, 3);
        st.WaitsStarted = 12;
        st.WaitsDone = 9;
        st.WaitSeconds = 9 * 0.8;
        st.WaitsExpired = 2;
        st.WaitsDropped = 1;
        var lines = st.SummaryLines(Rules()).ToList();
        Assert.Equal(5, lines.Count);
        Assert.Equal("ai kicks - bashes per swing and the wait for reach (step 66): the shieldmen's melee swings (AI, on foot) 40 earned 38.0 bashes "
                     + "(AiBashShareOfSwingsPercent 100%; lost at the cap of 2 2.0) → bashes asked 30 = 0.75 a swing; decisions that waited for reach 12 → kicked / bashed 9 "
                     + "(0.80 s after the decision), ran out (3.0 s) 2, dropped (the enemy gone or beyond 5.0 m, mounted, a ranged weapon, routing) 1", lines[4]);
        Assert.Equal("waiting for reach", AiKickMath.SkipName(AiKickSkip.Waiting));
        Assert.Equal(AiKickMath.SkipCount, Enum.GetValues(typeof(AiKickSkip)).Length);
    }
}

/// <summary>
/// Step 66 - THE WORKED EXAMPLES on the shipped values (MECHANICS §11c "The AI kicks and bashes", PLAYTEST D31): the kicks a minute of a man
/// WITHOUT a shield in steady contact (the 8 s cooldown in), and a SHIELDMAN's swings and bashes a minute by bar band - a seeded walk through
/// the real per-look rules (his swings from Anton's 14:16 fever proof, each swing 1.3 s of his time, a bash 1.2 s).
/// </summary>
public class Step66ExamplesTests
{
    private static AiKickRules Shipped()
    {
        var s = new TraxSettings();
        Step66Tests.Ship(s);
        return AiKickRules.From(s);
    }

    private static double Kicks(in AiKickRules r, double skill, double bar, bool fever, double turtle) =>
        AiKickMath.WithCooldown(AiKickMath.PerMinute(in r, skill, bar, fever, turtle), r.Cooldown);

    [Fact]
    public void A_fresh_average_man_kicks_about_once_a_minute_a_looter_less_a_legionary_more()
    {
        var r = Shipped();
        Assert.Equal(1.03, Kicks(in r, 100, 1.0, false, 0), 2);  // "about once a minute"
        Assert.Equal(0.71, Kicks(in r, 30, 1.0, false, 0), 2);   // a fresh looter (Athletics 30: x0.65)
        Assert.Equal(1.17, Kicks(in r, 130, 1.0, false, 0), 2);  // a fresh legionary (130: x1.15)
        Assert.Equal(0.28, Kicks(in r, 30, 0.5, false, 0), 2);   // half a bar: x0.375
        Assert.Equal(0.48, Kicks(in r, 130, 0.5, false, 0), 2);
        Assert.Equal(1.78, Kicks(in r, 30, 1.0, false, 1), 2);   // against a full turtle (x3)
        Assert.Equal(2.67, Kicks(in r, 130, 1.0, false, 1), 2);
        Assert.Equal(1.29, Kicks(in r, 30, 1.0, true, 0), 2);    // in a fever (x2)
        Assert.Equal(2.02, Kicks(in r, 130, 1.0, true, 0), 2);
        Assert.Equal(2.88, Kicks(in r, 30, 1.0, true, 1), 2);    // both
        Assert.Equal(3.94, Kicks(in r, 130, 1.0, true, 1), 2);
        Assert.Equal(0, Kicks(in r, 130, 0.34, true, 1), 9);     // tired men stop (below 35%)
        // step 65's 0.1 a minute for the same fresh looter: 0.064 - one kick in 16 minutes of contact
        Assert.Equal(0.064, AiKickMath.WithCooldown(AiKickMath.PerMinute(in r, 30, 1.0, false, 0) / 12, 10), 3);
    }

    /// <summary>One shieldman in steady contact for <paramref name="minutes"/>: his own swings at <paramref name="swingsWithout"/> a minute
    /// (what he did without bashes - the 1.3 s of each swing out), a look every 0.25 s, his credit and the bash's die through the Core rules.</summary>
    private static (double Swings, double Bashes) Shieldman(in AiKickRules r, double swingsWithout, double bar, int seed, double minutes = 300)
    {
        const double look = AiKickMath.LookSeconds, swingSeconds = 1.3, bashSeconds = 1.2;
        var dice = new SeededRandom(seed);
        double free = 1.0 / (60.0 / swingsWithout - swingSeconds); // his swings a second while free
        double t = 0, busyUntil = 0, next = 0, credit = 0;
        int swings = 0, bashes = 0;
        while (t < minutes * 60)
        {
            t += look;
            if (t < busyUntil) continue;
            if (dice.NextDouble() < 1 - Math.Exp(-free * look))
            {
                swings++;
                credit = AiKickMath.AddBashCredit(in r, credit);
                busyUntil = t + swingSeconds;
                continue;
            }
            if (t < next) continue;
            if (dice.NextDouble() < AiKickMath.LookChance(AiKickMath.BashPerMinute(in r, credit, bar), look))
            {
                bashes++;
                credit -= 1;
                busyUntil = t + bashSeconds;
                next = t + AiKickMath.CooldownFor(in r, AiKickKind.Bash);
            }
        }
        return (swings / minutes, bashes / minutes);
    }

    private static (double Swings, double Bashes) Band(in AiKickRules r, double swingsWithout, params double[] bars)
    {
        double s = 0, b = 0;
        for (int i = 0; i < bars.Length; i++)
        {
            var (sw, ba) = Shieldman(in r, swingsWithout, bars[i], 66 + i);
            s += sw;
            b += ba;
        }
        return (s / bars.Length, b / bars.Length);
    }

    [Fact]
    public void A_shieldman_bashes_about_once_a_swing_fresh_less_as_he_tires_and_never_starves_his_swings()
    {
        var r = Shipped();
        // the 14:16 battle's fever proof (in a formation, no fever): 8.07 / 16.47 / 11.35 / 3.67 attacks a man-minute in contact by bar band
        var top = Band(in r, 8.07, 0.8, 0.875, 0.95, 1.0);
        Assert.InRange(top.Bashes / top.Swings, 0.85, 1.05);      // ~0.95 a swing: "as often as he swings"
        Assert.InRange(top.Swings, 0.8 * 8.07, 8.07);             // ~7.1: the bashes' own time takes ~12% of his swings
        var upper = Band(in r, 16.47, 0.52, 0.58, 0.625, 0.67, 0.72);
        Assert.InRange(upper.Bashes / upper.Swings, 0.45, 0.8);   // ~0.6
        Assert.InRange(upper.Swings, 0.75 * 16.47, 16.47);        // ~13.8
        var lower = Band(in r, 11.35, 0.27, 0.32, 0.37, 0.42, 0.47);
        Assert.InRange(lower.Bashes / lower.Swings, 0.1, 0.35);   // ~0.2 (none below 35%)
        var spent = Band(in r, 3.67, 0.05, 0.12, 0.2);
        Assert.Equal(0, spent.Bashes, 9);                         // a spent man never bashes
        // a fresh WALL man swings less (step 21: 30%) - the bashes follow him
        var wall = Band(in r, 6.0, 1.0);
        Assert.InRange(wall.Bashes / wall.Swings, 0.85, 1.05);
        Assert.InRange(wall.Swings, 5.0, 6.0);
        // the knob: half as often
        var halfRules = AiKickRules.From(new Func<TraxSettings>(() => { var s = new TraxSettings(); Step66Tests.Ship(s); s.Set(SettingsSchema.AiBashShareOfSwingsPercent, 50, "t"); return s; })());
        var halfTop = Band(in halfRules, 8.07, 0.8, 0.875, 0.95, 1.0);
        Assert.InRange(halfTop.Bashes / halfTop.Swings, 0.4, 0.55);
    }
}
