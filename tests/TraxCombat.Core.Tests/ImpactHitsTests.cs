using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 52 (IMPACT I5, clean hits LIVE + the reeling bonus + MOMENTUM - AI_NOTES "Step 52"): the momentum factor (none to a walk, a straight
/// line to the full bonus at a sprint, clamped, never for a rider, off, live) and its place in EVERY family's impact (kicks, bashes, the three
/// blocks, clean hits - the riposte reads it turned round); the speed bands; a fresh sprinting axe-man against a half-tired man vs standing;
/// two EMPTY even men on a clean hit at x = 1 (exhaustion never cancels - IMPACT §4 A); the reeling bonus on every family; the tired man's
/// shrug-off threshold; the flinch factor and its learned length; the weapon classes; and the [summary] lines (momentum + clean hits).
/// </summary>
public class ImpactHitsTests
{
    private sealed class Dice : IRandomSource
    {
        private readonly double[] _v;
        private int _i;
        public double Rest = 0.999;

        public Dice(params double[] values) => _v = values;

        public double NextDouble() => _i < _v.Length ? _v[_i++] : Rest;
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    /// <summary>One event between two men of pool 100 and skill 100 (a 1.2 kg sword at 92, a 4 kg shield / a 1.2 kg parrying sword), the
    /// peak line at 75 %, the attacker's tired swing (0.85 + 0.15 f).</summary>
    private static ImpactFacts Facts(ImpactEvent e, double bar, double attackerBar = 1.0, double speed = 0, double kg = 1.2, double swing = 92,
        ImpactDamageType type = ImpactDamageType.Cut, bool twoHanded = false)
    {
        double fA = Math.Min(1, attackerBar / 0.75);
        return new ImpactFacts
        {
            Event = e,
            AttackerPoints = 100 * attackerBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = 100,
            DefenderPoints = 100 * bar,
            DefenderBar = bar,
            DefenderF = Math.Min(1, bar / 0.75),
            DefenderSkill = 100,
            WeaponWeight = e == ImpactEvent.Bash ? 4.0 : kg,
            SwingSpeed = swing,
            DamageType = type,
            TwoHanded = twoHanded,
            GuardWeight = e == ImpactEvent.Parry ? 1.2 : ImpactMath.IsBlock(e) ? 4.0 : 0,
            DefenderHasShield = e != ImpactEvent.Parry,
            DefenderMaxHealth = 100,
            AttackerSpeed = speed,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null, ImpactState defender = default, double now = 100)
    {
        var r = rules ?? Rules();
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref defender, ref a, now, new Dice());
    }

    private static readonly ImpactEvent[] Every = { ImpactEvent.Kick, ImpactEvent.Bash, ImpactEvent.RightBlock, ImpactEvent.Parry, ImpactEvent.WrongBlock, ImpactEvent.CleanHit };

    // ------------------------------------------------------------------ the settings

    [Fact]
    public void The_four_momentum_settings_sit_after_the_shrug_off_switch_live_with_their_defaults()
    {
        var keys = SettingsSchema.InGroup(SettingsSchema.ImpactGroup).Select(p => p.Key).ToList();
        int i = keys.IndexOf("ImpactTiredNeverShrugsOff");
        Assert.Equal(new[] { "ImpactMomentumEnabled", "ImpactMomentumPercent", "ImpactMomentumWalkMetresPerSecond", "ImpactMomentumSprintMetresPerSecond" }, keys.Skip(i + 1).Take(4));
        var r = Rules();
        Assert.True(r.MomentumOn);
        Assert.Equal(0.6, r.MomentumWeight, 9);
        Assert.Equal(2.0, r.MomentumWalk, 6);
        Assert.Equal(7.0, r.MomentumSprint, 6);
        foreach (var k in keys.Skip(i + 1).Take(4))
        {
            Assert.True(SettingsSchema.TryGet(k, out var p));
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        Assert.Contains("momentum (the attacker's run into the blow) +60% impact at 7.0 m/s and faster, none at 2.0 m/s or slower (riders: step 53)", r.Describe());
        Assert.Contains("tired men always flinch on", r.Describe());
        Assert.EndsWith("momentum (the attacker's run into the blow) OFF", Rules(s => s.Set(SettingsSchema.ImpactMomentumEnabled, false, "test")).Describe());
    }

    // ------------------------------------------------------------------ the momentum factor

    [Fact]
    public void Momentum_is_none_to_a_walk_a_straight_line_to_plus_60_at_a_sprint_and_clamped()
    {
        var r = Rules();
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, 0, false), 9);
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, 1.8, false), 9); // the game's walk limit
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, 2.0, false), 9);
        Assert.Equal(1.3, ImpactMath.MomentumFactor(in r, 4.5, false), 9); // half way: a run
        Assert.Equal(1.6, ImpactMath.MomentumFactor(in r, 7.0, false), 9); // a sprint
        Assert.Equal(1.6, ImpactMath.MomentumFactor(in r, 12.0, false), 9); // clamped
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, -6.0, false), 9); // running AWAY from him adds nothing
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, double.NaN, false), 9);
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, 7.0, true), 9); // a rider's: step 53
        Assert.Equal(1.0, ImpactMath.MomentumFactor(Rules(s => s.Set(SettingsSchema.ImpactMomentumEnabled, false, "test")), 7.0, false), 9);
        Assert.Equal(1.0, ImpactMath.MomentumFactor(Rules(s => s.Set(SettingsSchema.ImpactMomentumPercent, 0, "test")), 7.0, false), 9);
        var big = Rules(s => s.Set(SettingsSchema.ImpactMomentumPercent, 200, "test"));
        Assert.Equal(3.0, ImpactMath.MomentumFactor(in big, 7.0, false), 9); // live: a new percent the next blow
        // the share itself, and a sprint reference at or below the walk's = all or nothing
        Assert.Equal(0.5, ImpactMath.MomentumShare(4.5, 2, 7), 9);
        Assert.Equal(0.0, ImpactMath.MomentumShare(1.0, 2, 2), 9);
        Assert.Equal(1.0, ImpactMath.MomentumShare(2.0, 2, 2), 9);
        Assert.Equal(0.0, ImpactMath.MomentumShare(3.0, 4, 1), 9); // a sprint reference below the walk's: below the walk = none
        Assert.Equal(1.0, ImpactMath.MomentumShare(5.0, 4, 1), 9); // ... at or above it = all
    }

    [Fact]
    public void The_speed_bands_are_standing_walking_running_sprinting()
    {
        var r = Rules();
        Assert.Equal(0, ImpactMath.SpeedBand(in r, 0));
        Assert.Equal(0, ImpactMath.SpeedBand(in r, 0.49));
        Assert.Equal(0, ImpactMath.SpeedBand(in r, -3));
        Assert.Equal(1, ImpactMath.SpeedBand(in r, 1.8));
        Assert.Equal(2, ImpactMath.SpeedBand(in r, 4.4));
        Assert.Equal(3, ImpactMath.SpeedBand(in r, 7.0));
        Assert.Equal(3, ImpactMath.SpeedBand(in r, 9.5));
        Assert.Equal("standing", ImpactMath.SpeedBandName(0));
        Assert.Equal("sprinting", ImpactMath.SpeedBandName(3));
    }

    [Fact]
    public void Momentum_multiplies_the_impact_of_EVERY_family_and_the_riposte_reads_it_turned_round()
    {
        var r = Rules();
        foreach (var e in Every)
        {
            var standing = Decide(Facts(e, 0.5, speed: 0), r);
            var sprinting = Decide(Facts(e, 0.5, speed: 8), r);
            Assert.Equal(1.0, standing.Momentum, 9);
            Assert.Equal(1.6, sprinting.Momentum, 9);
            Assert.Equal(8.0, sprinting.AttackerSpeed, 9);
            Assert.Equal(1.6 * standing.Impact, sprinting.Impact, 9);
            Assert.True(sprinting.X >= standing.X, e + ": x " + sprinting.X + " < " + standing.X);
            Assert.Equal(ImpactMath.BlowImpact(in r, Facts(e, 0.5)), standing.Impact, 9);
        }
        // the riposte: a fresh blocker turns a STANDING tired attacker's blow round more easily than a charging one's
        var still = Decide(Facts(ImpactEvent.Parry, 1.0, attackerBar: 0.3, speed: 0), r);
        var charge = Decide(Facts(ImpactEvent.Parry, 1.0, attackerBar: 0.3, speed: 8), r);
        Assert.True(charge.XRiposte <= still.XRiposte && charge.Riposte.Chance <= still.Riposte.Chance, "the riposte against a charge: " + charge.Riposte.Chance + " vs " + still.Riposte.Chance);
        // a rider's speed never counts (step 53)
        var rider = Facts(ImpactEvent.CleanHit, 0.5, speed: 8);
        rider.AttackerMounted = true;
        Assert.Equal(1.0, Decide(in rider, r).Momentum, 9);
    }

    [Fact]
    public void A_fresh_sprinting_axe_man_throws_a_half_tired_man_around_a_standing_one_hardly()
    {
        var r = Rules();
        // a 3 kg two-handed axe at 80, a fresh attacker, the hit man at half his bar (T 0.33)
        var standing = Decide(Facts(ImpactEvent.CleanHit, 0.5, speed: 0, kg: 3.0, swing: 80, twoHanded: true), r);
        var sprinting = Decide(Facts(ImpactEvent.CleanHit, 0.5, speed: 8, kg: 3.0, swing: 80, twoHanded: true), r);
        Assert.InRange(standing.Knockdown.Chance, 0.005, 0.03);
        Assert.InRange(sprinting.Knockdown.Chance, 0.12, 0.18); // ~10 times the knockdowns
        Assert.InRange(standing.Stagger.Chance, 0.27, 0.33);
        Assert.InRange(sprinting.Stagger.Chance, 0.47, 0.53);
        // ... and the same charge into a FRESH even man is still nothing much: momentum alone does not break a fresh line
        var fresh = Decide(Facts(ImpactEvent.CleanHit, 1.0, speed: 8), r);
        Assert.True(fresh.Knockdown.Chance == 0 && fresh.Stagger.Chance < 0.05, "a sprinting swordsman on a fresh even man: " + fresh.Stagger.Chance + " / " + fresh.Knockdown.Chance);
        Assert.Contains("incl. momentum x1.60 (8.0 m/s into the blow)", ImpactMath.Describe(in sprinting, Facts(ImpactEvent.CleanHit, 0.5, speed: 8, kg: 3.0, swing: 80, twoHanded: true), "A", "D", "LIVE"));
    }

    // ------------------------------------------------------------------ exhaustion never cancels (IMPACT §4 A), the reeling bonus

    [Fact]
    public void Two_EMPTY_even_men_on_a_clean_hit_come_out_at_x_1_and_the_ceilings_LIVE()
    {
        var r = Rules();
        Assert.True(r.Applies(ImpactFamily.Hits)); // live
        var d = Decide(Facts(ImpactEvent.CleanHit, 0.0, attackerBar: 0.0), r);
        Assert.Equal(1.0, d.T, 9);
        Assert.Equal(d.BodyA, d.BodyD, 9); // both at the floor
        Assert.Equal(1.0, d.X, 9); // the tired swing inside the even band: nothing cancels
        Assert.Equal(0.8, d.Stagger.Chance, 9);
        Assert.Equal(0.5, d.Knockdown.Chance, 9);
        Assert.False(d.ShieldDrop.InRow); // a clean hit never drops a shield
        // two FRESH even men: nothing, always
        var fresh = Decide(Facts(ImpactEvent.CleanHit, 1.0), r);
        Assert.Equal(0, fresh.X, 9);
        Assert.Equal(0, fresh.Stagger.Chance);
        Assert.Equal(0, fresh.Knockdown.Chance);
    }

    [Fact]
    public void The_reeling_bonus_adds_0_75_on_every_family_and_a_reeling_man_is_never_staggered_again()
    {
        var r = Rules();
        foreach (var e in Every)
        {
            var calm = Decide(Facts(e, 0.6), r);
            var reeling = default(ImpactState);
            reeling.Reel(101, r.StaggerImmunity); // reeling from an impact stagger until 101 s (any family's: a kick's reel, a block's stagger, a riposte, a clean hit's flinch)
            var d = Decide(Facts(e, 0.6), r, reeling, now: 100);
            Assert.True(d.Reeling, e.ToString());
            Assert.Equal(calm.X + 0.75, d.X, 9);
            if (d.Knockdown.InRow)
            {
                Assert.Equal(ImpactRefusal.None, d.Knockdown.Refusal);
                Assert.True(d.Knockdown.Chance > calm.Knockdown.Chance, e + ": down " + d.Knockdown.Chance + " vs " + calm.Knockdown.Chance);
            }
            if (d.Stagger.InRow) Assert.Equal(ImpactRefusal.StaggerImmune, d.Stagger.Refusal);
        }
        // IMPACT §4 F: a FRESH even man reeling - a kick knocks him down 54 %, a sword blow 34 %
        var st = default(ImpactState);
        st.Reel(101, 2);
        Assert.InRange(Decide(Facts(ImpactEvent.Kick, 1.0), r, st).Knockdown.Chance, 0.53, 0.55);
        Assert.InRange(Decide(Facts(ImpactEvent.CleanHit, 1.0, swing: 90), r, st).Knockdown.Chance, 0.31, 0.37);
        // after the reel ends (the measured end - EndReel) the bonus is gone
        st.EndReel(100, 2);
        Assert.False(Decide(Facts(ImpactEvent.CleanHit, 1.0), r, st, now: 100.5).Reeling);
    }

    [Fact]
    public void A_live_clean_hit_writes_the_stagger_and_the_knockdown_on_his_record()
    {
        var r = Rules();
        var f = Facts(ImpactEvent.CleanHit, 0.0, attackerBar: 0.0);
        var v = default(ImpactState);
        var a = default(ImpactState);
        var d = ImpactMath.Decide(in r, in f, ref v, ref a, 100, new Dice(0.999, 0.0, 0.5)); // no knockdown, the stagger, its length at the centre
        Assert.True(d.Stagger.Happens && !d.Knockdown.Happens);
        Assert.Equal(1.5, d.StaggerSeconds, 6); // 0.6 x 2.5 at T 1
        Assert.True(v.Reeling(101.4) && !v.Reeling(101.6) && v.StaggerImmune(103.4));
        Assert.Equal(1, v.Recent(100, 8));
        var v2 = default(ImpactState);
        var d2 = ImpactMath.Decide(in r, in f, ref v2, ref a, 100, new Dice(0.0, 0.5)); // knocked down (replaces the stagger)
        Assert.True(d2.Knockdown.Happens && !d2.Stagger.Happens && d2.Stagger.Refusal == ImpactRefusal.Replaced);
        Assert.True(v2.Down(100.5) && d2.GetUpFactor >= 1.99);
    }

    // ------------------------------------------------------------------ tired men never shrug off

    [Fact]
    public void A_tired_mans_shrug_off_threshold_falls_with_his_strength()
    {
        Assert.Equal(5.0, ImpactHitMath.ShrugOffThreshold(5, 0), 9); // the green: the game's own
        Assert.Equal(2.5, ImpactHitMath.ShrugOffThreshold(5, 0.5), 9);
        Assert.Equal(0.0, ImpactHitMath.ShrugOffThreshold(5, 1), 9); // empty: he shrugs off nothing
        Assert.Equal(5.0, ImpactHitMath.ShrugOffThreshold(5, -1), 9);
        Assert.Equal(0.0, ImpactHitMath.ShrugOffThreshold(5, 3), 9);
        Assert.True(ImpactHitMath.StillShrugsOff(3, 5, 0));
        Assert.False(ImpactHitMath.StillShrugsOff(3, 5, 0.5)); // a 3-damage blow on a half-tired man: he flinches
        Assert.True(ImpactHitMath.StillShrugsOff(2, 5, 0.5));
        Assert.True(ImpactHitMath.StillShrugsOff(0, 5, 1)); // a 0-damage blow plays nothing anyway
        Assert.False(ImpactHitMath.StillShrugsOff(1, 5, 1));
        // his T: 1 − f; YOU x ImpactOnPlayerPercent
        Assert.Equal(0.5, ImpactHitMath.ShrugTired(0.5, false, 0.0), 9);
        Assert.Equal(0.25, ImpactHitMath.ShrugTired(0.5, true, 0.5), 9);
        Assert.Equal(0.0, ImpactHitMath.ShrugTired(0.5, true, 0.0), 9);
        Assert.Equal(0.0, ImpactHitMath.ShrugTired(1.0, false, 1.0), 9);
        Assert.True(Rules().TiredNeverShrugsOff);
    }

    // ------------------------------------------------------------------ the flinch, the classes

    [Fact]
    public void The_flinch_is_slowed_to_the_decided_length_its_own_length_learned()
    {
        Assert.Equal(0.45, ImpactHitMath.FlinchNaturalGuessSeconds, 9);
        Assert.Equal(0.6 / 0.45, ImpactHitMath.FlinchFactor(0.6, 0.45), 9);
        Assert.Equal(1.5 / 0.45, ImpactHitMath.FlinchFactor(1.5, 0.45), 9);
        Assert.Equal(1.0, ImpactHitMath.FlinchFactor(0.3, 0.45), 9); // never SHORTER than the game's flinch
        Assert.Equal(1.0, ImpactHitMath.FlinchFactor(double.NaN, 0.45), 9);
        Assert.Equal(1.0, ImpactHitMath.FlinchFactor(1.0, 0), 9);
        var n = new FlinchNatural();
        Assert.Equal(0.45, n.Seconds, 9);
        n.Add(0.5, 1.0); // an untouched control
        n.Add(1.2, 0.5); // a slowed one: 0.6 s at speed 1
        Assert.Equal(0.55, n.Seconds, 9);
        n.Add(SpikeMath.MeasureCapSeconds, 1);
        n.Add(0.01, 1);
        Assert.Equal(2, n.Count);
        // the speed the engine is asked: 1 ÷ the factor (step 50's SpeedFor)
        Assert.Equal(0.3f, ImpactLiveMath.SpeedFor(ImpactHitMath.FlinchFactor(1.5, 0.45)), 4);
    }

    [Fact]
    public void The_weapon_classes_are_cut_pierce_blunt_and_two_handed()
    {
        Assert.Equal(0, ImpactHitMath.ClassOf(Facts(ImpactEvent.CleanHit, 1, type: ImpactDamageType.Cut)));
        Assert.Equal(1, ImpactHitMath.ClassOf(Facts(ImpactEvent.CleanHit, 1, type: ImpactDamageType.Pierce)));
        Assert.Equal(2, ImpactHitMath.ClassOf(Facts(ImpactEvent.CleanHit, 1, type: ImpactDamageType.Blunt)));
        Assert.Equal(3, ImpactHitMath.ClassOf(Facts(ImpactEvent.CleanHit, 1, type: ImpactDamageType.Blunt, twoHanded: true)));
        Assert.Equal("two-handed", ImpactHitMath.ClassName(3));
        Assert.Equal("blunt", ImpactHitMath.ClassName(2));
        Assert.Equal(8, ImpactLiveMath.LeverCount);
        Assert.Equal("tired men always flinch (the shrug-off refused)", ImpactLiveMath.LeverName(ImpactLever.ShrugOff));
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_reads_momentum_by_speed_band_and_the_clean_hits_by_class_and_band()
    {
        var r = Rules();
        var stats = new ImpactHitLiveStats();
        Assert.StartsWith("impact momentum (step 52 - the attacker's speed into the blow): no blow decided this mission", stats.SummaryLines(true, "LIVE", "none")[0]);
        Assert.StartsWith("impact live clean hits (step 52): no live clean hit this mission (LIVE)", stats.SummaryLines(true, "LIVE", "none")[1]);
        // a standing blow and a sprinting blow on half-tired men, a sprinting knockdown on an exhausted man, a rider's
        var a = default(ImpactState);
        var v = default(ImpactState);
        var standing = ImpactMath.Decide(in r, Facts(ImpactEvent.CleanHit, 0.4, speed: 0), ref v, ref a, 100, new Dice());
        var v2 = default(ImpactState);
        var down = ImpactMath.Decide(in r, Facts(ImpactEvent.CleanHit, 0.05, speed: 8, kg: 3, swing: 80, twoHanded: true), ref v2, ref a, 100, new Dice(0.0, 0.5));
        Assert.True(down.Knockdown.Happens);
        stats.NoteMomentum(in standing, ImpactMath.SpeedBand(in r, 0), false);
        stats.NoteMomentum(in down, ImpactMath.SpeedBand(in r, 8), false);
        stats.NoteMomentum(in down, 3, true);
        stats.NoteDecision(in standing, 0);
        stats.NoteDecision(in down, 3);
        stats.NoteApplied(3, down.Bar, 0);
        stats.NoteSeen(0);
        stats.NoteShrug(0.05, true);
        stats.NoteShrug(0.9, false);
        stats.NoteFlinch(1.2, 2.5, 1.19, true);
        stats.NoteFlinch(0, 1, 0.47, false);
        var lines = stats.SummaryLines(true, "LIVE", "none");
        Assert.StartsWith("impact momentum (step 52 - every decided kick, bash, block and clean hit by the attacker's speed into the blow", lines[0]);
        Assert.Contains("standing 1 (0.0 m/s, impact x1.00): an outcome 0%, down 0%; on men below half a bar 1: an outcome 0%, down 0%", lines[0]);
        Assert.Contains("sprinting 1 (8.0 m/s, impact x1.60): an outcome 100%, down 100%; on men below half a bar 1: an outcome 100%, down 100%", lines[0]);
        Assert.EndsWith("riders' blows not counted (step 53) 1", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("impact live clean hits, cut, by the hit man's bar 100-75% / 75-50% / 50-25% / 25-0% (step 52; LIVE): 1 live (0 / 0 / 1 / 0)"));
        var two = lines.Single(l => l.StartsWith("impact live clean hits, two-handed,"));
        Assert.Contains("knocked down decided 0 / 0 / 0 / 1 = 1, applied 0 / 0 / 0 / 1 = 1", two);
        Assert.DoesNotContain(lines, l => l.StartsWith("impact live clean hits, pierce,")); // a class with no hit is left out
        Assert.Contains(lines, l => l.StartsWith("impact live clean-hit knockdowns: on men REELING from an impact stagger 0 of 0 hits (-), on the rest 1 of 2 (50%)"));
        Assert.Contains(lines, l => l.StartsWith("impact live clean-hit staggers (the engine's own flinch slowed): asked 1.20 s (n 1) (x2.50 the flinch) → measured 1.19 s (max 1.19 s) (n 1) vs untouched flinches 0.47 s (n 1) = x2.53 measured"));
        var shrug = lines.Single(l => l.StartsWith("impact live shrug-offs (ImpactTiredNeverShrugsOff - the game's threshold x f): the game let a tired man shrug a clean hit off 2 times"));
        Assert.Contains("1 / 0 / 0 / 1 → REFUSED (he flinched) 0 / 0 / 0 / 1 = 1 (50%), kept (light enough even tired) 1", shrug);
        Assert.Contains(lines, l => l.StartsWith("impact live clean-hit limits: knockdown refused - on the ground 0"));
        Assert.Equal(2, stats.LiveDecisions);
        Assert.Equal(1, stats.DecidedOf(0));
    }
}
