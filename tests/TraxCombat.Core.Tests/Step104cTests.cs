using System;
using System.Collections.Generic;
using TraxCombat.Core;
using Xunit.Abstractions;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 104c - THE REAR AND THE KNOCK-OFF, LIVE (DESIGN decision 93 (b) / (c), AI_NOTES "104c - built"): the design's numbers (a perfect block's
/// knock-off ~9 % at a canter, 30 % for a galloping poor rider; the rear 50 % x the timing from 0.8 - 47 % at a 0.20 s guard), never on you; the
/// rear blow's safety math (a horse below 2 health refused, its 1 health given back - net 0); the reel's animation skipped under a fall / rear;
/// a man just knocked off is DOWN - step 105's 80 % and the usual model refuse him while he falls; the lever, the words, the [summary] line, the
/// settings as designed; no allocation.
/// </summary>
public class Step104cTests
{
    private readonly ITestOutputHelper _out;

    public Step104cTests(ITestOutputHelper output) => _out = output;

    private sealed class Script : IRandomSource
    {
        private readonly Queue<double> _q;
        public int Calls;
        public Script(params double[] v) => _q = new Queue<double>(v);
        public double NextDouble()
        {
            Calls++;
            return _q.Count > 0 ? _q.Dequeue() : 0.5;
        }
    }

    private static TimedRiderRules Rules() => TimedRiderRules.From(new TraxSettings());

    private static TimedRiderFacts Facts(double s, bool youBlock = true, bool youRide = false, double speed = 6.5, double xr = 0, ImpactEvent e = ImpactEvent.RightBlock) => new TimedRiderFacts
    {
        Event = e,
        BlockerMounted = true,
        AttackerMounted = true,
        BlockerIsPlayer = youBlock,
        AttackerIsPlayer = youRide,
        Score = s,
        RowInRow = true,
        RowChance = 1,
        RowRefusal = ImpactRefusal.Rider,
        Length56 = 0.6,
        StunCap = 2.0,
        XRiposte = xr,
        HorseSpeed = speed,
        TiredMountMultiplier = 1,
    };

    // ------------------------------------------------------------------ A. the design's numbers

    [Fact]
    public void The_knock_off_is_9_percent_at_a_canter_and_30_for_a_galloping_poor_rider_perfect_only()
    {
        var r = Rules();
        var canter = TimedRiderMath.Decide(in r, Facts(1, speed: 6.5), double.NaN, new Script(0.0, 0.99, 0.99));
        Assert.Equal(0.09375, canter.DismountChance, 6); // 30 % x 0.625 x 0.5
        var gallopWeak = TimedRiderMath.Decide(in r, Facts(1, speed: 12, xr: 2), double.NaN, new Script(0.0, 0.99, 0.99));
        Assert.Equal(0.30, gallopWeak.DismountChance, 6);
        var sharp = TimedRiderMath.Decide(in r, Facts(0.95), double.NaN, new Script(0.0, 0.99));
        Assert.Equal(0, sharp.DismountChance); // a sharp but not perfect block never knocks off
        _out.WriteLine("knock-off: canter " + (100 * canter.DismountChance).ToString("0.0") + "%, a galloping poor rider " + (100 * gallopWeak.DismountChance).ToString("0") + "%");
    }

    [Theory]
    [InlineData(1.00, 0.50)]
    [InlineData(0.94, 0.47)]
    [InlineData(0.80, 0.40)]
    [InlineData(0.79, 0.00)]
    [InlineData(0.50, 0.00)]
    public void The_rear_is_50_percent_at_perfect_falling_with_the_score_none_below_0_8(double s, double chance)
    {
        var r = Rules();
        Assert.Equal(chance, TimedRiderMath.RearChance(s, r.RearMinScore, r.RearShare), 2);
        Assert.Equal(0.47, TimedRiderMath.RearChance(TimedBlockMath.Score(0.20, 0.15, 0.5), r.RearMinScore, r.RearShare), 2); // a 0.20 s guard at skill 100
    }

    [Fact]
    public void A_knock_off_is_rolled_first_and_the_rear_only_if_he_stayed_on_never_on_you()
    {
        var r = Rules();
        var off = TimedRiderMath.Decide(in r, Facts(1), double.NaN, new Script(0.0, 0.0, 0.0));
        Assert.True(off.Dismount);
        Assert.False(off.Rear);
        Assert.True(double.IsNaN(off.RearDie)); // no rear die once he is off
        var rear = TimedRiderMath.Decide(in r, Facts(1), double.NaN, new Script(0.0, 0.99, 0.0));
        Assert.False(rear.Dismount);
        Assert.True(rear.Rear);
        // the AI's block of YOUR blow: never, whatever the dice
        var you = TimedRiderMath.Decide(in r, Facts(1, youBlock: false, youRide: true), double.NaN, new Script(0.0, 0.0, 0.0));
        Assert.True(you.Reel);
        Assert.False(you.Dismount);
        Assert.False(you.Rear);
        // an AI blocker's half-strength timing (≤ 0.5) never reaches either
        var ai = TimedRiderMath.Decide(in r, Facts(0.5, youBlock: false), double.NaN, new Script(0.0, 0.0, 0.0));
        Assert.False(ai.Dismount);
        Assert.False(ai.Rear);
    }

    [Fact]
    public void The_settings_are_the_designs()
    {
        Assert.Equal(0.8, SettingsSchema.TimedRiderRearMinScore.Default, 9);
        Assert.Equal(50, SettingsSchema.TimedRiderRearPercent.Default, 9);
        Assert.Equal(30, SettingsSchema.TimedRiderDismountPercent.Default, 9);
        Assert.Equal(50, SettingsSchema.TimedRiderDismountWeaponPercent.Default, 9);
        var r = Rules();
        Assert.Equal(0.8, r.RearMinScore, 9);
        Assert.Equal(0.5, r.RearShare, 9);
        Assert.Equal(0.3, r.DismountShare, 9);
        Assert.Equal(0.5, r.DismountWeaponShare, 9);
        Assert.Contains("the rear and the knock-off LIVE (steps 104b / 104c", TimedRiderRules.From(LiveSettings()).Describe());
    }

    private static TraxSettings LiveSettings()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.TimedBlocksOnHorseback, 1, "test");
        return s;
    }

    // ------------------------------------------------------------------ B. the rear blow's safety

    [Fact]
    public void The_rear_blow_never_kills_a_horse_and_gives_its_health_back()
    {
        Assert.False(TimedRiderFallMath.RearBlowSafe(1));
        Assert.False(TimedRiderFallMath.RearBlowSafe(0));
        Assert.False(TimedRiderFallMath.RearBlowSafe(double.NaN));
        Assert.True(TimedRiderFallMath.RearBlowSafe(2));
        // the blow takes 1, the give-back writes it again: net 0
        double back = TimedRiderFallMath.Restored(120, 119, 150);
        Assert.Equal(120, back, 9);
        Assert.Equal(0, TimedRiderFallMath.NetDamage(120, back), 9);
        // never above the horse's limit; never below what the blow left
        Assert.Equal(100, TimedRiderFallMath.Restored(120, 99, 100), 9);
        Assert.Equal(119, TimedRiderFallMath.Restored(110, 119, 150), 9);
        Assert.True(double.IsNaN(TimedRiderFallMath.NetDamage(double.NaN, 1)));
        Assert.Equal(1, TimedRiderFallMath.NetDamage(10, 9), 9);
    }

    [Fact]
    public void The_reel_animation_is_skipped_under_a_fall_or_a_rear()
    {
        Assert.True(TimedRiderFallMath.SkipsReelAnimation(true, false));
        Assert.True(TimedRiderFallMath.SkipsReelAnimation(false, true));
        Assert.False(TimedRiderFallMath.SkipsReelAnimation(false, false));
        Assert.Equal(2.0, TimedRiderFallMath.WatchSeconds(RiderFallKind.KnockOff), 9);
        Assert.Equal(1.5, TimedRiderFallMath.WatchSeconds(RiderFallKind.Rear), 9);
    }

    [Fact]
    public void The_rear_has_its_own_lever()
    {
        Assert.Equal(10, ImpactLiveMath.LeverCount);
        Assert.Equal("a rider's horse reared (our blow)", ImpactLiveMath.LeverName(ImpactLever.Rear));
        Assert.Equal(TimedRiderFallMath.RefusalCount, Enum.GetValues(typeof(RiderFallRefusal)).Length);
        foreach (RiderFallRefusal x in Enum.GetValues(typeof(RiderFallRefusal))) Assert.NotEqual("?", TimedRiderFallMath.RefusalName(x));
    }

    // ------------------------------------------------------------------ C. step 105 and the usual model on a man just knocked off

    private static ImpactFacts HitOnFoot() => new ImpactFacts
    {
        Event = ImpactEvent.CleanHit,
        AttackerPoints = 100, AttackerF = 1, AttackerAnimation = 1, AttackerSkill = 100,
        DefenderPoints = 100, DefenderBar = 1, DefenderF = 1, DefenderSkill = 100,
        WeaponWeight = 1.2, SwingSpeed = 92, DamageType = ImpactDamageType.Cut, GuardWeight = 1.2, DefenderMaxHealth = 100, Damage = 30,
        DefenderMounted = false, // off his horse now
    };

    [Fact]
    public void A_man_just_knocked_off_is_down_no_80_percent_no_new_outcome_while_he_falls()
    {
        var ir = ImpactRules.From(new TraxSettings());
        // the module's knock-off: his reel in the saddle, then down at once (the game's fall-and-rise)
        var st = default(ImpactState);
        st.Reel(101.5, ir.StaggerImmunity, ReelCause.RiderReel);
        st.KnockedDown(100, 100 + ImpactMath.VanillaFallRiseSeconds, ir.KnockdownImmunity);
        Assert.False(st.Reeling(100.01)); // his reel ended with the fall - step 105 has nothing to roll on
        Assert.True(st.Down(100.5));
        var a = default(ImpactState);
        var d = ImpactMath.Decide(in ir, HitOnFoot(), ref st, ref a, 100.5, new Script(0.0, 0.0, 0.0, 0.0, 0.0, 0.0));
        Assert.False(d.ReelingHit.Happens);
        Assert.False(d.Knockdown.Happens);
        Assert.Equal(ImpactRefusal.Down, d.Knockdown.Refusal);
        Assert.False(d.Stagger.Happens);
        Assert.False(d.Reeling);
        // up again after the fall-and-rise: an ordinary man (no reel, no down) - the usual rolls, never the 80 %
        var a2 = default(ImpactState);
        var up = ImpactMath.Decide(in ir, HitOnFoot(), ref st, ref a2, 102, new Script(0.99, 0.99, 0.99, 0.99, 0.99, 0.99));
        Assert.False(up.ReelingHit.Happens);
        Assert.NotEqual(ImpactRefusal.Down, up.Knockdown.Refusal);
    }

    // ------------------------------------------------------------------ D. the words and the [summary] line

    [Fact]
    public void Your_line_says_knocked_off_or_rears()
    {
        Assert.Equal(" - knocked off his horse!", TimedRiderLines.FallSuffix(true, false));
        Assert.Equal(" - his horse rears!", TimedRiderLines.FallSuffix(false, true));
        Assert.Equal(string.Empty, TimedRiderLines.FallSuffix(false, false));
        var o = TimedRiderMath.Decide(Rules(), Facts(1), double.NaN, new Script(0.0, 0.0));
        Assert.Contains("a knock-off / rear above applied from the tick (step 104c)", TimedRiderMath.Describe(in o, "Rider", "you", true));
    }

    [Fact]
    public void The_summary_line()
    {
        var st = new TimedRiderFallStats();
        st.NoteDecided(RiderFallKind.KnockOff);
        st.NoteDecided(RiderFallKind.KnockOff);
        st.NoteApplied(RiderFallKind.KnockOff, true);
        st.NoteSeen(RiderFallKind.KnockOff, 0.1);
        st.NoteRefused(RiderFallKind.KnockOff, RiderFallRefusal.NotRiding);
        st.NoteDecided(RiderFallKind.Rear);
        st.NoteDecided(RiderFallKind.Rear);
        st.NoteDecided(RiderFallKind.Rear);
        st.NoteApplied(RiderFallKind.Rear, false);
        st.NoteApplied(RiderFallKind.Rear, true);
        st.NoteHorseDamage(0);
        st.NoteHorseDamage(0);
        st.NoteSeen(RiderFallKind.Rear, 0.2);
        st.NoteNotSeen(RiderFallKind.Rear);
        st.NoteRefused(RiderFallKind.Rear, RiderFallRefusal.CannotRear);
        st.AnimSkipped = 3;
        string line = st.SummaryLine(TimedRiderRules.From(LiveSettings()), true);
        _out.WriteLine(line);
        Assert.StartsWith("timed blocks on horseback - rear and knock-off (step 104c): LIVE (a perfect block 30% x the speed band x the match knocks off; a sharp one from 0.80 50% x the timing rears; never on you) - ", line);
        Assert.Contains("knock-offs decided 2 → applied 1 (yours 1) → SEEN off within 2.0 s 1 (mean 0.10 s), not seen 0, refused: no horse under him by the tick 1", line);
        Assert.Contains("rears decided 3 → applied 2 (yours 1) → SEEN rearing within 1.5 s 1 (mean 0.20 s), not seen 1, refused: his mount cannot rear (no CanRear flag) 1", line);
        Assert.Contains("the horses' health lost to our rear blows (after the give-back) 0 in 2 (the most one horse 0)", line);
        Assert.Contains("the reel's animation skipped under a fall / rear 3", line);
        Assert.StartsWith("timed blocks on horseback - rear and knock-off (step 104c): OFF (TimedBlocksOnHorseback)", new TimedRiderFallStats().SummaryLine(Rules(), true));
    }

    [Fact]
    public void The_counts_allocate_nothing()
    {
        var st = new TimedRiderFallStats();
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                st.NoteDecided(RiderFallKind.Rear);
                st.NoteApplied(RiderFallKind.Rear, i % 2 == 0);
                st.NoteHorseDamage(0);
                st.NoteSeen(RiderFallKind.Rear, 0.1);
                st.NoteRefused(RiderFallKind.KnockOff, RiderFallRefusal.LeverOff);
                _ = TimedRiderFallMath.Restored(100, 99, 120);
                _ = TimedRiderFallMath.RearBlowSafe(100);
            }
        });
        Assert.Equal(0, bytes);
    }
}
