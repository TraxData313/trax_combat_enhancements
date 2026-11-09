using System;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 106e - the openings' REVIEW fixes (AI_NOTES "106e - review"): (1) no endless re-stagger - a back hit / an unguarded front hit on a man
/// still reeling or inside his stagger immunity opens nothing (a chain earned with a block still staggers); (3) the openings are live only when
/// the impact system can carry the stagger (its riposte live, its hits decided) - else shadow, no flash.
/// </summary>
public class Step106eTests
{
    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, 1, "test");
        s.Set(SettingsSchema.AthleticsEnabled, 1, "test");
        s.Set(SettingsSchema.ImpactEnabled, 1, "test");
        s.Set(SettingsSchema.ImpactShadowMode, 0, "test");
        s.Set(SettingsSchema.ImpactSpike, 0, "test");
        s.Set(SettingsSchema.ImpactHitsEnabled, 1, "test");
        s.Set(SettingsSchema.ImpactRiposteEnabled, 1, "test");
        s.Set(SettingsSchema.OpeningsEnabled, 1, "test");
        s.Set(SettingsSchema.OpeningBackHitStaggers, 1, "test");
        s.Set(SettingsSchema.OpeningUnguardedHitOpens, 1, "test");
        tune?.Invoke(s);
        return s;
    }

    private const int Sword = OpeningMath.AllAttacks;

    // ------------------------------------------------------------------ (1) the stunlock guard

    [Fact]
    public void A_back_hit_on_a_stagger_immune_man_opens_nothing_and_leaves_the_chain()
    {
        var r = OpeningRules.From(Settings());
        var c = OpeningChain.None;
        var hit = c.NoteLanded(7, 10, in r, fromBehind: true, guarding: false, OpeningMath.AttackRight, Sword, 0, out var reset, staggerImmune: true);
        Assert.Equal(OpeningHit.Immune, hit);
        Assert.Equal(OpeningReset.None, reset);
        Assert.False(c.Active);
        // not immune: the back hit still staggers (the player's back hit usually staggers)
        hit = c.NoteLanded(7, 10, in r, fromBehind: true, guarding: false, OpeningMath.AttackRight, Sword, 0, out _, staggerImmune: false);
        Assert.Equal(OpeningHit.Staggers, hit);
    }

    [Fact]
    public void An_unguarded_front_hit_on_a_reeling_man_is_no_opening()
    {
        var r = OpeningRules.From(Settings());
        var c = OpeningChain.None;
        Assert.Equal(OpeningHit.Immune, c.NoteLanded(3, 5, in r, false, guarding: false, OpeningMath.AttackLeft, Sword, 0, out _, staggerImmune: true));
        Assert.False(c.Active);
        Assert.Equal(OpeningHit.OffBalance, c.NoteLanded(3, 5, in r, false, guarding: false, OpeningMath.AttackLeft, Sword, 0, out _, staggerImmune: false));
        // a guarded front hit was nothing either way
        var g = OpeningChain.None;
        Assert.Equal(OpeningHit.Nothing, g.NoteLanded(3, 5, in r, false, guarding: true, OpeningMath.AttackLeft, Sword, 0, out _, staggerImmune: true));
    }

    [Fact]
    public void A_chain_earned_with_a_block_still_staggers_inside_the_immunity()
    {
        var r = OpeningRules.From(Settings());
        var c = OpeningChain.None;
        c.NoteBlock(4, 1.0, false, in r, out _);
        Assert.Equal(OpeningHit.Staggers, c.NoteLanded(4, 1.5, in r, false, true, OpeningMath.AttackDown, Sword, 1, out _, staggerImmune: true));
        var b = OpeningChain.None;
        b.NoteBlock(4, 1.0, false, in r, out _);
        Assert.Equal(OpeningHit.Staggers, b.NoteLanded(4, 1.5, in r, true, true, OpeningMath.AttackDown, Sword, 1, out _, staggerImmune: true));
    }

    [Fact]
    public void The_finish_on_the_reeling_man_is_unchanged_by_the_immunity()
    {
        var r = OpeningRules.From(Settings());
        var c = OpeningChain.None;
        c.NoteLanded(9, 1, in r, true, false, OpeningMath.AttackRight, Sword, 0, out _);
        Assert.Equal(OpeningPhase.Reeling, c.Phase);
        Assert.Equal(OpeningHit.FinishPerfect, c.NoteLanded(9, 1.3, in r, true, false, OpeningMath.AttackLeft, Sword, 0, out _, staggerImmune: true));
    }

    [Fact]
    public void Repeated_back_hits_cannot_keep_a_man_reeling()
    {
        // the review's scenario: hits from behind every 0.6 s; his record reels 0.8 s from each opening stagger and is immune 2 s after it
        var r = OpeningRules.From(Settings());
        var c = OpeningChain.None;
        var st = new ImpactState();
        double immunity = 2.0;
        int staggers = 0;
        for (int i = 0; i < 20; i++)
        {
            double now = i * 0.6;
            bool immune = OpeningMath.ImmuneBeforeHit(st.StaggerImmune(now), false);
            var hit = c.NoteLanded(1, now, in r, true, false, OpeningMath.AttackRight, Sword, 0, out _, immune);
            if (hit == OpeningHit.Staggers)
            {
                staggers++;
                st.Reel(c.ReelUntil, immunity, ReelCause.Opening);
            }
        }
        // 12 s of back hits: a stagger, then 2.8 s of nothing new - at most one every ~3 s (before the fix: every other hit, 10)
        Assert.InRange(staggers, 3, 5);
    }

    [Fact]
    public void Immunity_before_the_hit_ignores_the_decisions_own_stagger()
    {
        Assert.True(OpeningMath.ImmuneBeforeHit(true, false));
        Assert.False(OpeningMath.ImmuneBeforeHit(true, true)); // the decision staggered him - he was not immune before
        Assert.False(OpeningMath.ImmuneBeforeHit(false, false));
    }

    [Fact]
    public void The_summary_counts_the_refusals()
    {
        var st = new OpeningStats { ImmuneRefused = 3 };
        Assert.Contains("stagger-immune (no new opening) 3", st.SummaryLine(OpeningRules.From(Settings())));
    }

    // ------------------------------------------------------------------ (3) live only when the impact system carries the stagger

    [Fact]
    public void The_impact_system_carries_openings_only_with_its_riposte_live_and_hits_decided()
    {
        Assert.True(OpeningMath.ImpactCarries(ImpactRules.From(Settings())));
        Assert.False(OpeningMath.ImpactCarries(ImpactRules.From(Settings(s => s.Set(SettingsSchema.ImpactShadowMode, 1, "test")))));
        Assert.False(OpeningMath.ImpactCarries(ImpactRules.From(Settings(s => s.Set(SettingsSchema.ImpactRiposteEnabled, 0, "test")))));
        Assert.False(OpeningMath.ImpactCarries(ImpactRules.From(Settings(s => s.Set(SettingsSchema.ImpactHitsEnabled, 0, "test")))));
        Assert.False(OpeningMath.ImpactCarries(ImpactRules.From(Settings(s => s.Set(SettingsSchema.ImpactEnabled, 0, "test")))));
        Assert.False(OpeningMath.ImpactCarries(ImpactRules.From(Settings(s => s.Set(SettingsSchema.ImpactSpike, 1, "test")))));
    }

    [Fact]
    public void No_flash_while_the_openings_are_shadow_but_the_test_tool_still_flashes()
    {
        var r = OpeningRules.From(Settings(s => s.Set(SettingsSchema.OpeningArrowShow, 1, "test")));
        Assert.True(OpeningArrowMath.ShowsFlash(in r, false, true));
        Assert.False(OpeningArrowMath.ShowsFlash(in r, false, false));
        Assert.True(OpeningArrowMath.ShowsFlash(in r, true, false));
        Assert.True(OpeningArrowMath.ShowsFlash(in r, false)); // the old call = carried
    }
}
