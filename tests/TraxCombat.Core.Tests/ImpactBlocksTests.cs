using System;
using System.Linq;
using System.Threading.Tasks;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 51 (IMPACT I4, blocks and the riposte LIVE - AI_NOTES "Step 51"): the block stun by tiredness as the decorator's worker-thread seam
/// computes it (from T alone, no die - and the decision logs the same number), the game's stun held at its own cap before it is lengthened,
/// the riposte with the roles turned (the attacker's T, the defender's guard and f), the riposte alone when only the blocks' switch is off,
/// two EMPTY even men at the ceilings LIVE (exhaustion never cancels - IMPACT §4 A), a wrong-side block never better than a right one at any
/// bar, the forced stagger's speed for the asked length (the animation's own length learned), what a live block writes on the records (once
/// each - courage's shock), the new levers and reasons, and the [summary] "impact live block" lines.
/// </summary>
public class ImpactBlocksTests
{
    private sealed class Dice : IRandomSource
    {
        private readonly double[] _v;
        private int _i;
        public double Rest = 0.999;
        public int Thrown;

        public Dice(params double[] values) => _v = values;

        public double NextDouble()
        {
            Thrown++;
            return _i < _v.Length ? _v[_i++] : Rest;
        }
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    /// <summary>A block: the attacker (pool 100, skill 100, a 1.2 kg sword at 92) at <paramref name="attackerBar"/>, the blocker (pool 100,
    /// skill 100, a 4 kg shield / a 1.2 kg sword for a parry) at <paramref name="bar"/>; the peak line at 75 %; the attacker's tired swing
    /// (step 20b's 0.85 + 0.15 f) as the game reads it.</summary>
    private static ImpactFacts Block(ImpactEvent e, double bar, double attackerBar = 1.0, double attackerPool = 100, double defenderPool = 100,
        double attackerSkill = 100, double defenderSkill = 100)
    {
        double fA = Math.Min(1, attackerBar / 0.75);
        return new ImpactFacts
        {
            Event = e,
            AttackerPoints = attackerPool * attackerBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = attackerSkill,
            DefenderPoints = defenderPool * bar,
            DefenderBar = bar,
            DefenderF = Math.Min(1, bar / 0.75),
            DefenderSkill = defenderSkill,
            WeaponWeight = 1.2,
            SwingSpeed = 92,
            DamageType = ImpactDamageType.Cut,
            GuardWeight = e == ImpactEvent.Parry ? 1.2 : 4.0,
            DefenderHasShield = e != ImpactEvent.Parry,
            DefenderMaxHealth = 100,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null, IRandomSource? dice = null)
    {
        var r = rules ?? Rules();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, dice ?? new Dice());
    }

    private static void Pct(double percent, double chance, string what) =>
        Assert.True(Math.Abs(100 * chance - percent) <= 0.55, what + ": " + (100 * chance).ToString("0.00") + "% vs " + percent + "%");

    // ------------------------------------------------------------------ the stun (the any-thread seam)

    [Fact]
    public void The_block_stun_is_any_thread_math_from_T_alone_x_1_plus_extra_T()
    {
        Assert.Equal(1.0, ImpactMath.BlockStunFactor(1.0, 0), 9);
        Assert.Equal(1.25, ImpactMath.BlockStunFactor(1.0, 0.25), 9); // IMPACT §3.7's column
        Assert.Equal(1.5, ImpactMath.BlockStunFactor(1.0, 0.5), 9);
        Assert.Equal(2.0, ImpactMath.BlockStunFactor(1.0, 1.0), 9);
        Assert.Equal(2.0, ImpactMath.BlockStunFactor(1.0, 3.0), 9); // T clamped
        Assert.Equal(1.0, ImpactMath.BlockStunFactor(1.0, double.NaN), 9);
        Assert.Equal(1.0, ImpactMath.BlockStunFactor(0, 1.0), 9); // BlockStunTiredExtraPercent 0 = the game's stun
        Assert.Equal(1.0, ImpactMath.BlockStunFactor(-1, 1.0), 9);
        var r = Rules();
        Assert.Equal(1.75, ImpactMath.BlockStunFactor(in r, 0.75), 9); // BlockStunTiredExtraPercent 100 (defaults)
        var half = Rules(s => s.Set(SettingsSchema.BlockStunTiredExtraPercent, 50, "test"));
        Assert.Equal(1.5, ImpactMath.BlockStunFactor(in half, 1.0), 9); // read live
    }

    [Fact]
    public void The_lengthened_stun_starts_from_the_games_own_capped_stun_and_holds_at_ours()
    {
        Assert.Equal(0.6f, ImpactMath.LengthenedStun(0.3f, 0.8f, 2.0, 2.0f), 5);
        Assert.Equal(1.2f, ImpactMath.LengthenedStun(1.2f, 0.8f, 1.5, 2.0f), 5); // the game would have played 0.8: 0.8 x 1.5
        Assert.Equal(1.0f, ImpactMath.LengthenedStun(0.9f, 0.8f, 2.0, 1.0f), 5); // ImpactStunCapSeconds 1.0
        Assert.Equal(0.3f, ImpactMath.LengthenedStun(0.3f, 0.8f, 1.0, 2.0f), 5); // a fresh blocker: untouched (the hook clamps it)
        Assert.Equal(0.3f, ImpactMath.LengthenedStun(0.3f, 0.8f, 1.0005, 2.0f), 5);
        Assert.Equal(0f, ImpactMath.LengthenedStun(0f, 0.8f, 2.0, 2.0f), 5);
        Assert.True(float.IsNaN(ImpactMath.LengthenedStun(float.NaN, 0.8f, 2.0, 2.0f)));
        Assert.Equal(0.8f, ImpactMath.LengthenedStun(0.9f, 0.8f, 2.0, 0.5f), 5); // a cap below the game's never shortens what the game plays
    }

    [Fact]
    public void The_decision_logs_the_very_stun_the_seam_applies_and_rolls_no_die_for_it()
    {
        var r = Rules();
        foreach (var bar in new[] { 1.0, 0.6, 0.375, 0.1, 0.0 })
        {
            var dice = new Dice(0.999, 0.999, 0.999, 0.999);
            var d = Decide(Block(ImpactEvent.RightBlock, bar), r, dice);
            Assert.Equal(ImpactMath.BlockStunFactor(in r, d.T), d.StunFactor, 9);
            int rolled = (d.Knockdown.Rolled ? 1 : 0) + (d.ShieldDrop.Rolled ? 1 : 0) + (d.Stagger.Rolled ? 1 : 0) + (d.Riposte.Rolled ? 1 : 0);
            Assert.Equal(rolled, dice.Thrown); // nothing happened: one die per rolled outcome, none for the stun
        }
        Assert.True(double.IsNaN(Decide(Block(ImpactEvent.CleanHit, 0.2)).StunFactor)); // not a block
    }

    // ------------------------------------------------------------------ the riposte

    [Fact]
    public void The_riposte_turns_the_roles_the_attackers_T_the_defenders_guard_times_1_minus_k_T_D()
    {
        // a fresh veteran (200 points, skill 200) blocks a recruit at 40 % (50 points) - IMPACT §3.6: 50 % (DESIGN's initial ceiling)
        var f = Block(ImpactEvent.RightBlock, 1.0, attackerBar: 0.4, attackerPool: 50, defenderPool: 200, attackerSkill: 40, defenderSkill: 200);
        f.AttackerAnimation = 1; // as the doc's script
        var d = Decide(in f);
        Pct(50, d.Riposte.Chance, "the veteran ripostes the recruit");
        Assert.Equal(1 - 0.4 / 0.75, d.TAttacker, 9); // the ATTACKER's tiredness
        Assert.Equal(1.0, d.RiposteBlockerFactor, 9);
        Assert.Equal(0, d.Stagger.Chance); // the fresh veteran himself: nothing
        // the same block by the veteran at 37.5 % (f 0.5): step 56 (RiposteBlockerTiredPercent 0) - his own tiredness does not save the
        // recruit (his smaller body only, and it is still far bigger); step 48 (100) - the chance x his f
        var tired = f;
        tired.DefenderBar = 0.375;
        tired.DefenderF = 0.5;
        tired.DefenderPoints = 75;
        Assert.InRange(Decide(in tired).Riposte.Chance, 0.45, 0.5); // 48 % - his smaller body takes a little off, never × his f
        var step48 = Rules(s => s.Set(SettingsSchema.RiposteBlockerTiredPercent, 100, "test"));
        var tired48 = Decide(in tired, step48);
        Assert.Equal(0.5, tired48.RiposteBlockerFactor, 9);
        Assert.True(tired48.Riposte.Chance < d.Riposte.Chance * 0.75);
        // an exhausted defender: step 56 - an exhausted recruit at 40 % still reels off his guard; step 48 - never; a wrong-side block never;
        // a clean hit never
        var empty = f;
        empty.DefenderBar = 0;
        empty.DefenderF = 0;
        empty.DefenderPoints = 0;
        Assert.True(Decide(in empty).Riposte.Chance > 0.05);
        Assert.Equal(0, Decide(in empty, step48).Riposte.Chance);
        Assert.False(Decide(Block(ImpactEvent.WrongBlock, 1.0, attackerBar: 0)).Riposte.InRow);
        Assert.True(Decide(Block(ImpactEvent.Parry, 1.0, attackerBar: 0)).Riposte.InRow);
        // it happens: the ATTACKER's record reels (once), the blocker's is untouched
        var r = Rules();
        var ds = new ImpactState();
        var at = new ImpactState();
        var dec = ImpactMath.Decide(in r, in f, ref ds, ref at, 100, new Dice(0.0, 0.5)); // the riposte's die, its recoil's
        Assert.True(dec.Riposte.Happens);
        Assert.True(at.Reeling(100.1) && at.Recent(100, 8) == 1);
        Assert.Equal(0, ds.Recent(100, 8));
        Assert.False(ds.Reeling(100.1));
        Assert.InRange(dec.RiposteStunSeconds, 0.5, 1.0 + 1e-9); // RiposteStunMaxSeconds x (0.5 + 0.5 T_A) x the roll
    }

    [Fact]
    public void Blocks_off_and_the_riposte_on_rolls_only_the_riposte_on_a_right_side_block_or_a_parry()
    {
        var r = Rules(s => s.Set(SettingsSchema.ImpactBlocksEnabled, false, "test"));
        Assert.False(r.Applies(ImpactFamily.Blocks));
        Assert.True(r.Applies(ImpactFamily.Riposte));
        Assert.True(r.DecidesEvent(ImpactEvent.RightBlock));
        Assert.True(r.DecidesEvent(ImpactEvent.Parry));
        Assert.False(r.DecidesEvent(ImpactEvent.WrongBlock));
        Assert.True(r.DecidesEvent(ImpactEvent.Kick));
        var d = Decide(Block(ImpactEvent.RightBlock, 0.0, attackerBar: 0.0), r);
        Assert.False(d.Stagger.InRow);
        Assert.False(d.Knockdown.InRow);
        Assert.True(double.IsNaN(d.StunFactor));
        Assert.True(d.Riposte.InRow);
        var none = Rules(s =>
        {
            s.Set(SettingsSchema.ImpactBlocksEnabled, false, "test");
            s.Set(SettingsSchema.ImpactRiposteEnabled, false, "test");
        });
        Assert.False(none.DecidesEvent(ImpactEvent.RightBlock));
        Assert.False(none.DecidesEvent(ImpactEvent.Parry));
        Assert.True(Rules().DecidesEvent(ImpactEvent.WrongBlock));
        Assert.False(Rules(s => s.Set(SettingsSchema.ModEnabled, false, "test")).DecidesEvent(ImpactEvent.RightBlock)); // the master switch first
    }

    // ------------------------------------------------------------------ exhaustion never cancels (IMPACT §4 A, live)

    [Fact]
    public void Two_EMPTY_even_men_stay_at_the_ceilings_LIVE_every_mistake_counts_in_the_step_48_model()
    {
        // step 56: this is the STEP-48 model (a correct block counted the blocker's T in full, the riposte × f_D) - step 56's own
        // answer for two empty men is RightBlocksTests.Two_EMPTY_even_men_a_correct_block_reels_the_ATTACKER_a_wrong_one_breaks_the_blocker
        var r = Rules(s =>
        {
            s.Set(SettingsSchema.CorrectBlockTiredWeightPercent, 100, "test");
            s.Set(SettingsSchema.RiposteBlockerTiredPercent, 100, "test");
        });
        Assert.True(r.Applies(ImpactFamily.Blocks) && r.Applies(ImpactFamily.Riposte)); // live, not shadow
        // both men EMPTY: the attacker's tired swing (85 % animation) against a 2 kg shield, a parrying sword, a 4 kg shield on the wrong side
        var light = Block(ImpactEvent.RightBlock, 0.0, attackerBar: 0.0);
        light.GuardWeight = 2.0;
        var right = Decide(in light, r);
        var parry = Decide(Block(ImpactEvent.Parry, 0.0, attackerBar: 0.0), r);
        var wrong = Decide(Block(ImpactEvent.WrongBlock, 0.0, attackerBar: 0.0), r);
        foreach (var d in new[] { parry, wrong })
        {
            Assert.Equal(1.0, d.X, 9); // T 1, the bodies even (both at the floor), the blow inside the even band: nothing cancels
            Assert.Equal(2.0, d.StunFactor, 9); // DESIGN's initial BlockStunTiredExtraPercent 100 (defaults.json 0 since step 56)
        }
        Assert.True(right.X > 0.98 && right.X <= 1.0, "x " + right.X);
        Pct(80, right.Stagger.Chance, "right-side stagger");
        Pct(40, right.Knockdown.Chance, "right-side down");
        Assert.False(right.ShieldDrop.InRow);
        // a HEAVY 4 kg shield on the right side is a real guard against the tired swing (beyond the even band): x 0.86 - near the
        // ceilings, never the tiredness cancelled (the bodies are even; T stays 1)
        var heavy = Decide(Block(ImpactEvent.RightBlock, 0.0, attackerBar: 0.0), r);
        Assert.InRange(heavy.X, 0.85, 0.87);
        Assert.InRange(heavy.Stagger.Chance, 0.75, 0.77);
        Assert.InRange(heavy.Knockdown.Chance, 0.34, 0.36);
        Assert.Equal(1.0, heavy.T, 9);
        Pct(80, parry.Stagger.Chance, "parry stagger");
        Pct(40, parry.Knockdown.Chance, "parry down");
        Pct(90, wrong.Stagger.Chance, "wrong-side stagger");
        Pct(50, wrong.ShieldDrop.Chance, "wrong-side shield");
        Pct(40, wrong.Knockdown.Chance, "wrong-side down");
        // and no riposte between two empty men (the blocker's f is 0)
        Assert.Equal(0, right.Riposte.Chance);
    }

    [Fact]
    public void A_wrong_side_block_is_never_better_than_a_right_one_at_any_bar_and_clearly_worse_when_tired()
    {
        foreach (var attackerBar in new[] { 1.0, 0.5, 0.0 })
        {
            for (int i = 0; i <= 20; i++)
            {
                double bar = 1.0 - i * 0.05;
                var right = Decide(Block(ImpactEvent.RightBlock, bar, attackerBar));
                var wrong = Decide(Block(ImpactEvent.WrongBlock, bar, attackerBar));
                Assert.True(wrong.X >= right.X - 1e-12, "x at " + bar);
                Assert.True(wrong.Stagger.Chance >= right.Stagger.Chance, "stagger at " + bar);
                Assert.True(wrong.Knockdown.Chance >= right.Knockdown.Chance, "down at " + bar);
                Assert.True(wrong.ShieldDrop.Chance >= 0 && !right.ShieldDrop.InRow, "shield at " + bar);
                if (bar <= 0.5)
                {
                    Assert.True(wrong.Stagger.Chance > right.Stagger.Chance, "a tired man's wrong-side block is not worse (stagger) at " + bar);
                    Assert.True(wrong.ShieldDrop.Chance > 0, "no shield drop on a tired man's wrong-side block at " + bar);
                }
            }
        }
    }

    // ------------------------------------------------------------------ the records (courage's shock: once each)

    [Fact]
    public void A_live_block_writes_the_blockers_record_once_per_outcome()
    {
        var r = Rules();
        var ds = new ImpactState();
        var at = new ImpactState();
        // a wrong-side block on an exhausted man: no knockdown, the shield, the stagger (its length's die at its centre)
        var d = ImpactMath.Decide(in r, Block(ImpactEvent.WrongBlock, 0.0), ref ds, ref at, 100, new Dice(0.999, 0.0, 0.0, 0.5));
        Assert.True(d.ShieldDrop.Happens && d.Stagger.Happens && !d.Knockdown.Happens);
        Assert.Equal(2, ds.Recent(100, 8)); // two outcomes, two shocks - never more
        Assert.True(ds.Reeling(100 + d.StaggerSeconds - 0.01) && !ds.Reeling(100 + d.StaggerSeconds + 0.01));
        Assert.True(ds.StaggerImmune(100 + d.StaggerSeconds + 1.9));
        Assert.Equal(0, at.Recent(100, 8));
        Assert.Equal(1.5, d.StaggerSeconds, 6); // 0.6 x 2.5 at the roll's centre
        // a knockdown REPLACES the stagger: one outcome (+ the shield = two)
        var ks = new ImpactState();
        var k = ImpactMath.Decide(in r, Block(ImpactEvent.WrongBlock, 0.0), ref ks, ref at, 100, new Dice(0.0, 0.999, 0.5));
        Assert.True(k.Knockdown.Happens && k.Stagger.Refusal == ImpactRefusal.Replaced);
        Assert.Equal(1, ks.Recent(100, 8));
        Assert.True(ks.Down(100.5));
        // the measured end (the watch) re-times the immunity: 2 s from what the engine played
        ds.EndReel(101.2, 2);
        Assert.False(ds.Reeling(101.3));
        Assert.True(ds.StaggerImmune(103.1) && !ds.StaggerImmune(103.3));
    }

    // ------------------------------------------------------------------ the forced stagger's speed

    [Fact]
    public void The_stagger_is_played_at_the_speed_for_the_asked_length_the_animations_own_length_learned()
    {
        Assert.Equal(0.6f, ImpactBlockMath.StaggerSpeed(1.5, 0.9), 5);
        Assert.Equal(1.5f, ImpactBlockMath.StaggerSpeed(0.6, 0.9), 5);
        Assert.Equal(ImpactBlockMath.MinStaggerSpeed, ImpactBlockMath.StaggerSpeed(10, 0.9));
        Assert.Equal(ImpactBlockMath.MaxStaggerSpeed, ImpactBlockMath.StaggerSpeed(0.1, 0.9));
        Assert.Equal(1f, ImpactBlockMath.StaggerSpeed(0, 0.9));
        Assert.Equal(1f, ImpactBlockMath.StaggerSpeed(double.NaN, 0.9));
        var n = new StaggerNatural();
        Assert.Equal(ImpactBlockMath.StaggerNaturalGuessSeconds, n.Seconds);
        n.Add(1.5, 0.6); // a 1.5 s stagger at 0.6 = 0.9 s at speed 1
        Assert.Equal(0.9, n.Seconds, 6);
        n.Add(2.2, 0.5); // 1.1 s
        Assert.Equal(1.0, n.Seconds, 6);
        n.Add(SpikeMath.MeasureCapSeconds, 0.5); // a capped watch is not a sample
        n.Add(0.01, 1.0); // nor a flicker
        Assert.Equal(2, n.Count);
        // the lengths of §3.7 through the learned 1.0 s: fresh 0.6 s = x1.67, empty 1.5 s = x0.67
        Assert.Equal(1.0 / 0.6, ImpactBlockMath.StaggerSpeed(0.6, n.Seconds), 4);
        Assert.Equal(1.0 / 1.5, ImpactBlockMath.StaggerSpeed(1.5, n.Seconds), 4);
    }

    // ------------------------------------------------------------------ the levers, the reasons, the summary

    [Fact]
    public void The_new_levers_and_reasons_have_their_names_and_disable_alone()
    {
        Assert.Equal(9, ImpactLiveMath.LeverCount); // step 52: + ShrugOff; step 53: + Dismount
        Assert.Equal(10, ImpactLiveMath.NotAppliedCount); // step 53: + NoHorse
        Assert.Equal("the block stun by tiredness", ImpactLiveMath.LeverName(ImpactLever.BlockStun));
        Assert.Equal("the forced stagger (blocks, the riposte)", ImpactLiveMath.LeverName(ImpactLever.ForcedStagger));
        Assert.Equal("the blocker's knockdown (our own blow)", ImpactLiveMath.LeverName(ImpactLever.OwnBlow));
        Assert.StartsWith("refused before any engine call", ImpactLiveMath.NotAppliedName(ImpactNotApplied.CannotForce));
        Assert.Equal("the engine refused it", ImpactLiveMath.NotAppliedName(ImpactNotApplied.EngineRefused));
        var live = new ImpactLiveStats();
        Assert.True(live.Disable(ImpactLever.ForcedStagger, "impact.live-stagger"));
        Assert.False(live.Disable(ImpactLever.ForcedStagger, "again"));
        Assert.False(live.Disabled(ImpactLever.OwnBlow));
        Assert.Equal("the forced stagger (blocks, the riposte) (at impact.live-stagger)", live.LeversText());
        Assert.Equal(0, ImpactBlockMath.KindOf(ImpactEvent.RightBlock));
        Assert.Equal(1, ImpactBlockMath.KindOf(ImpactEvent.Parry));
        Assert.Equal(2, ImpactBlockMath.KindOf(ImpactEvent.WrongBlock));
        Assert.Equal(-1, ImpactBlockMath.KindOf(ImpactEvent.Kick));
    }

    [Fact]
    public void The_stun_tally_counts_exactly_from_many_threads()
    {
        var stats = new ImpactBlockLiveStats();
        Parallel.For(0, 4000, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            stats.Stun.Call(true);
            if (i % 4 == 0) stats.Stun.NoteUntouched();
            else stats.Stun.NoteLengthened(0.3f, 0.6f, 2.0, i % 8 == 1);
        });
        Assert.Equal(4000, stats.Stun.Calls);
        Assert.Equal(4000, stats.Stun.OffMainThread);
        Assert.Equal(1000, stats.Stun.Untouched);
        Assert.Equal(3000, stats.Stun.Lengthened);
        Assert.Equal(500, stats.Stun.Capped);
        Assert.Equal(2.0, stats.Stun.MeanFactor, 6);
        Assert.Equal(0.3, stats.Stun.MeanBefore, 6);
        Assert.Equal(0.6, stats.Stun.MeanAfter, 6);
    }

    [Fact]
    public void The_summary_reads_each_block_kind_by_band_the_staggers_from_inside_the_riposte_and_the_stun()
    {
        var none = new ImpactBlockLiveStats();
        Assert.Equal("impact live blocks (step 51 - blocks and the riposte): no live block this mission (LIVE)", none.SummaryLines(true, "LIVE", "none").Single());
        var stats = new ImpactBlockLiveStats();
        var r = Rules();
        var ds = new ImpactState();
        var at = new ImpactState();
        var wrong = ImpactMath.Decide(in r, Block(ImpactEvent.WrongBlock, 0.1), ref ds, ref at, 100, new Dice(0.999, 0.0, 0.0, 0.5));
        stats.NoteDecision(in wrong, 0, blocksApplied: true, riposteApplied: true);
        stats.NoteApplied(ImpactEvent.WrongBlock, ImpactOutcome.Stagger, 0.1);
        stats.NoteApplied(ImpactEvent.WrongBlock, ImpactOutcome.ShieldDrop, 0.1);
        stats.NoteSeen(ImpactEvent.WrongBlock, ImpactOutcome.Stagger);
        stats.NoteStagger(1.4, 1.38, 0.64f);
        var ds2 = new ImpactState();
        var at2 = new ImpactState();
        var f = Block(ImpactEvent.Parry, 1.0, attackerBar: 0.2);
        var rip = ImpactMath.Decide(in r, in f, ref ds2, ref at2, 100, new Dice(0.0, 0.5));
        Assert.True(rip.Riposte.Happens);
        stats.NoteDecision(in rip, 3, blocksApplied: true, riposteApplied: true);
        stats.NoteRiposteApplied(3);
        stats.RiposteSeen++;
        stats.NoteNotApplied(ImpactOutcome.Knockdown, ImpactNotApplied.CannotForce);
        stats.NoteGuard(1.5, 0.45);
        stats.NoteGuard(1.0, 0.3);
        stats.Stun.Call(true);
        stats.Stun.NoteLengthened(0.3f, 0.45f, 1.5, false);
        stats.CapGame = 0.8f;
        stats.CapRaisedTo = 2f;
        stats.CapRaises = 1;
        stats.ShockGiven = 3;
        Assert.Equal(2, stats.LiveDecisions);
        var lines = stats.SummaryLines(true, "LIVE", "none");
        Assert.Equal(8, lines.Count);
        var w = lines.Single(l => l.StartsWith("impact live WRONG-side shield blocks by the blocker's bar 100-75% / 75-50% / 50-25% / 25-0% (step 51; LIVE): 1 live (0 / 0 / 0 / 1)"));
        Assert.Contains("staggered decided 0 / 0 / 0 / 1 = 1, applied 0 / 0 / 0 / 1 = 1, seen 1", w);
        Assert.Contains("shield dropped decided 0 / 0 / 0 / 1 = 1, applied 0 / 0 / 0 / 1 = 1, left the hand 0", w);
        Assert.Contains("knocked down (our own blow) decided 0 / 0 / 0 / 0 = 0", w);
        Assert.Single(lines, l => l.StartsWith("impact live parries by the blocker's bar") && l.Contains(": 1 live (1 / 0 / 0 / 0)"));
        Assert.DoesNotContain("shield dropped", lines.Single(l => l.StartsWith("impact live parries")));
        var st = lines.Single(l => l.StartsWith("impact live block staggers: asked 1.40 s (max 1.40 s) (n 1) → measured 1.38 s"));
        Assert.Contains("BLOCKS MADE FROM INSIDE A STAGGER 0 (should be ~0", st);
        Assert.Contains(lines, l => l.StartsWith("impact live blockers knocked down: our own blows registered 0") && l.Contains("refused before any engine call"));
        Assert.Contains(lines, l => l.StartsWith("impact live riposte (a fresh blocker staggers the ATTACKER) by the attacker's bar 100-75% / 75-50% / 50-25% / 25-0%: decided 0 / 0 / 0 / 1 = 1, applied 0 / 0 / 0 / 1 = 1, seen 1"));
        var stun = lines.Single(l => l.StartsWith("impact live block stun: the decorator's calls 1 (off the main thread 1), lengthened 1 (x1.50 mean: 0.30 → 0.45 s"));
        Assert.Contains("the guard measured: asked x1.50 (n 1) → 0.45 s (n 1) vs fresh blockers 0.30 s (n 1) = x1.50 measured", stun);
        Assert.Contains("the game's cap 0.80 → 2.00 s (raised 1x, restored 0x)", stun);
        Assert.Contains(lines, l => l.StartsWith("impact live block limits: ") && l.Contains("outcomes counted as courage's SHOCK 3 (once each)"));
    }
}
