using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 80 - AI KICKS AND BASHES LAND ON ENEMIES, NOT FRIENDS (Toni's logs since step 68: 314 of 570 landed AI kicks / bashes on a friend in
/// the 17:18 battle, 427 of 668 at 16:17; AI_NOTES "Step 80", DESIGN decision 76): the STRIKE PATH along his facing (<see cref="AiKickPathProbe"/>),
/// the enemy the first body in it, a rear-rank man never; the kick's half-width 0.6 (config format 18), a bash's 0.75, the rear rank 1.2 m.
/// </summary>
public class Step80Tests
{
    private const double Reach = 1.6, Kick = 0.6, Bash = 0.75, Rear = 1.2;

    /// <summary>The kicker at (0, 0) facing +y (or <paramref name="faceX"/>, <paramref name="faceY"/>), his enemy at (tx, ty), his friends.</summary>
    private static AiKickPath V(double tx, double ty, double half, double rear, params (double X, double Y)[] friends) =>
        VF(0, 1, tx, ty, half, rear, friends);

    private static AiKickPath VF(double faceX, double faceY, double tx, double ty, double half, double rear, params (double X, double Y)[] friends)
    {
        var p = AiKickPathProbe.Begin(0, 0, faceX, faceY, tx, ty, Reach, half, rear);
        foreach (var f in friends) p.Add(f.X, f.Y);
        return p.Verdict;
    }

    [Fact]
    public void The_settings_are_placed_live_and_in_the_rules()
    {
        Assert.Same(SettingsSchema.ImpactGroup, SettingsSchema.AiBashPathHalfWidthMetres.Group);
        Assert.Same(SettingsSchema.ImpactGroup, SettingsSchema.AiKickRearRankMetres.Group);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.AiBashPathHalfWidthMetres.Timing);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.AiKickRearRankMetres.Timing);
        var keys = SettingsSchema.All.Select(p => p.Key).ToList();
        int at = keys.IndexOf("AiKickFriendClearMetres");
        Assert.Equal("AiBashPathHalfWidthMetres", keys[at + 1]);
        Assert.Equal("AiKickRearRankMetres", keys[at + 2]);
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiKickFriendClearMetres, Kick, "test");
        s.Set(SettingsSchema.AiBashPathHalfWidthMetres, Bash, "test");
        s.Set(SettingsSchema.AiKickRearRankMetres, Rear, "test");
        var r = AiKickRules.From(s);
        Assert.Equal(Kick, r.PathHalfWidth(AiKickKind.Kick), 6);
        Assert.Equal(Bash, r.PathHalfWidth(AiKickKind.Bash), 6);
        Assert.Equal(Rear, r.RearRank, 6);
        Assert.Contains("a kick's path 0.60 m, a bash's 0.75 m either side", r.Describe(), StringComparison.Ordinal);
        Assert.Contains("; within 1.2 m = a friend in front of him, farther = beyond the rear-rank reach - both refuse", r.Describe(), StringComparison.Ordinal); // step 90's words
        // the master switch still comes first - the path never turns the kicks on
        s.Set(SettingsSchema.ModEnabled, false, "test");
        Assert.False(AiKickRules.From(s).Enabled);
        Assert.Equal("ModEnabled", AiKickRules.From(s).OffBecause);
        Assert.Equal(AiKickMath.SkipCount, Enum.GetValues(typeof(AiKickSkip)).Length); // step 81 rebuilt the reasons (16)
        Assert.Equal(4, AiKickMath.GroupOf(AiKickSkip.TargetOffPath)); // the path's refusals
        Assert.Equal(4, AiKickMath.GroupOf(AiKickSkip.BehindFriend));
        Assert.Equal(7, AiKickMath.EndCount);
        Assert.Equal(AiKickMath.EndCount, Enum.GetValues(typeof(AiKickEnd)).Length);
    }

    [Fact]
    public void A_rear_rank_man_behind_his_front_mate_never_kicks()
    {
        // his front mate straight ahead 0.9 m, the enemy half a file to the side past him
        Assert.Equal(AiKickPath.BehindFriend, V(0.3, 1.5, Kick, Rear, (0.0, 0.9)));
        Assert.Equal(AiKickPath.BehindFriend, V(0.3, 1.5, Bash, Rear, (0.0, 0.9)));
        // the rear-rank distance 0 = the decision only waits
        Assert.Equal(AiKickPath.FriendFirst, V(0.3, 1.5, Kick, 0, (0.0, 0.9)));
        // a friend in the path past the rear-rank distance, still short of the enemy = waits
        Assert.Equal(AiKickPath.FriendFirst, V(0.0, 1.55, Kick, 1.0, (0.1, 1.3)));
    }

    [Fact]
    public void The_path_runs_along_his_facing_not_the_line_to_the_enemy()
    {
        // Step 68's miss: the enemy 21° off his facing at 1.39 m (0.5 m off the facing line - in the kick's path); a mate at (-0.2, 0.9) is 0.51 m
        // from the line to the enemy (step 68 said clear) but 0.2 m off the line his foot goes along
        Assert.True(Math.Abs(-(-0.2) * (1.3 / Math.Sqrt(0.25 + 1.69)) + 0.9 * (0.5 / Math.Sqrt(0.25 + 1.69))) > 0.5);
        Assert.Equal(AiKickPath.BehindFriend, V(0.5, 1.3, Kick, Rear, (-0.2, 0.9)));
        // the enemy 40° off at 1.5 m (inside step 65's facing gate): 0.96 m off his facing line - the kick would pass him
        double a = 40 * Math.PI / 180;
        Assert.Equal(AiKickPath.TargetOffPath, V(1.5 * Math.Sin(a), 1.5 * Math.Cos(a), Kick, Rear));
        Assert.False(AiKickMath.TargetInPath(1.5, 40, Reach, Kick));
        // straight ahead and a little off: in the path
        Assert.Equal(AiKickPath.Clear, V(0.0, 1.4, Kick, Rear));
        Assert.True(AiKickMath.TargetInPath(1.4, 0, Reach, Kick));
        Assert.True(AiKickMath.TargetInPath(1.39, 21, Reach, Kick));
        // facing turned: the same men, the man facing his enemy's file - the mate is no longer in front of his foot
        Assert.Equal(AiKickPath.Clear, VF(0.5, 1.3, 0.5, 1.3, Kick, Rear, (-0.5, 0.6)));
    }

    [Fact]
    public void A_bash_has_a_wider_path_than_a_kick()
    {
        // a mate 0.7 m off the line, 0.8 m ahead: clear of a kick, in a bash's way
        Assert.Equal(AiKickPath.Clear, V(0.0, 1.4, Kick, Rear, (0.7, 0.8)));
        Assert.Equal(AiKickPath.BehindFriend, V(0.0, 1.4, Bash, Rear, (0.7, 0.8)));
        // the enemy 0.7 m off: off a kick's path, in a bash's
        Assert.Equal(AiKickPath.TargetOffPath, V(0.7, 1.2, Kick, Rear));
        Assert.Equal(AiKickPath.Clear, V(0.7, 1.2, Bash, Rear));
    }

    [Fact]
    public void Men_beside_behind_and_beyond_the_enemy_are_not_in_the_way()
    {
        Assert.Equal(AiKickPath.Clear, V(0.0, 1.4, Kick, Rear, (0.65, 0.1)));   // shoulder to shoulder beside him
        Assert.Equal(AiKickPath.Clear, V(0.0, 1.4, Kick, Rear, (-0.5, -0.05))); // beside him, his shoulders not past him
        // a neighbour half a step forward 0.7 m off: clear of a kick, in a bash's sweep (step 68's 0.2 m along let him through)
        Assert.Equal(AiKickPath.Clear, V(0.0, 1.4, Kick, Rear, (0.7, 0.15)));
        Assert.Equal(AiKickPath.BehindFriend, V(0.0, 1.4, Bash, Rear, (0.7, 0.15)));
        Assert.Equal(AiKickPath.Clear, V(0.0, 1.4, Kick, Rear, (0.0, -0.9)));   // his own rear-rank mate behind him
        Assert.Equal(AiKickPath.Clear, V(0.0, 1.0, Kick, Rear, (0.0, 1.5)));    // behind the enemy
        Assert.Equal(AiKickPath.Clear, V(0.0, 1.4, Kick, Rear, (1.2, 1.0)));    // the next file over
    }

    [Fact]
    public void The_enemy_must_be_in_front_and_in_reach_and_zero_turns_the_check_off()
    {
        Assert.Equal(AiKickPath.TargetOffPath, V(0.0, -1.0, Kick, Rear));      // behind him
        Assert.Equal(AiKickPath.TargetOffPath, V(0.0, 1.7, Kick, Rear));       // beyond the reach
        Assert.False(AiKickMath.TargetInPath(1.0, 120, Reach, Kick));
        Assert.False(AiKickMath.TargetInPath(1.7, 0, Reach, Kick));
        Assert.False(AiKickMath.TargetInPath(double.NaN, 0, Reach, Kick));
        // 0 = no check: always clear, the probe never looks at a friend
        var p = AiKickPathProbe.Begin(0, 0, 0, 1, 1.5, -1.0, Reach, 0, Rear);
        p.Add(0, 0.5);
        Assert.Equal(AiKickPath.Clear, p.Verdict);
        Assert.Equal(0, p.Looked);
        Assert.True(AiKickMath.TargetInPath(5, 170, Reach, 0));
        // no facing read: the line to the enemy stands in for it
        Assert.Equal(AiKickPath.BehindFriend, VF(0, 0, 0.6, 1.2, Kick, Rear, (0.3, 0.6)));
    }

    [Fact]
    public void The_probe_and_the_cheap_gate_agree_and_the_nearest_friend_decides()
    {
        var rnd = new Random(80);
        for (int i = 0; i < 2000; i++)
        {
            double d = rnd.NextDouble() * 2.0, off = rnd.NextDouble() * 90;
            double a = off * Math.PI / 180;
            var p = AiKickPathProbe.Begin(0, 0, 0, 1, d * Math.Sin(a), d * Math.Cos(a), Reach, Kick, Rear);
            Assert.Equal(AiKickMath.TargetInPath(d, off, Reach, Kick), p.TargetIn);
        }
        var q = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, Kick, Rear);
        q.Add(0.1, 1.4);
        q.Add(-0.2, 0.7);
        q.Add(0.9, 0.5);
        Assert.Equal(3, q.Looked);
        Assert.Equal(2, q.InPath);
        Assert.Equal(0.7, q.NearestFriend, 6);
        Assert.Equal(AiKickPath.BehindFriend, q.Verdict);
    }

    [Fact]
    public void A_two_deep_line_only_the_front_rank_kicks_and_only_at_the_man_in_front()
    {
        // 10 files 0.9 m apart, the front rank at y = 0, the rear rank at y = -1.0, all facing +y; the enemy line at y = 1.2, offset half a file
        const int files = 10;
        var front = Enumerable.Range(0, files).Select(i => (X: i * 0.9, Y: 0.0)).ToArray();
        var rear = Enumerable.Range(0, files).Select(i => (X: i * 0.9, Y: -1.0)).ToArray();
        var enemy = Enumerable.Range(0, files).Select(i => (X: i * 0.9 + 0.45, Y: 1.2)).ToArray();
        var mates = front.Concat(rear).ToArray();
        int frontClear = 0, rearRefused = 0;
        foreach (var me in mates)
        {
            // his target: the nearest enemy
            var t = enemy.OrderBy(e => (e.X - me.X) * (e.X - me.X) + (e.Y - me.Y) * (e.Y - me.Y)).First();
            var p = AiKickPathProbe.Begin(me.X, me.Y, 0, 1, t.X, t.Y, Reach, Kick, Rear);
            foreach (var f in mates)
                if (f != me) p.Add(f.X, f.Y);
            var v = p.Verdict;
            if (me.Y == 0) { Assert.Equal(AiKickPath.Clear, v); frontClear++; }
            else
            {
                Assert.True(v == AiKickPath.BehindFriend || v == AiKickPath.TargetOffPath, "a rear-rank man at " + me.X + " was " + v);
                rearRefused++;
            }
        }
        Assert.Equal(files, frontClear);
        Assert.Equal(files, rearRefused);
    }

    [Fact]
    public void The_summary_splits_enemy_friend_and_miss_and_counts_the_refusals()
    {
        var st = new AiKickStats();
        for (int i = 0; i < 6; i++) st.NoteOutcome(AiKickKind.Kick, AiKickOutcome.EnemyTarget);
        st.NoteOutcome(AiKickKind.Kick, AiKickOutcome.EnemyOther);
        st.NoteOutcome(AiKickKind.Kick, AiKickOutcome.FriendBody);
        st.NoteOutcome(AiKickKind.Kick, AiKickOutcome.Miss);
        st.NoteOutcome(AiKickKind.Bash, AiKickOutcome.EnemyShield);
        st.NoteOutcome(AiKickKind.Bash, AiKickOutcome.FriendShield);
        st.NotePath(AiKickKind.Kick, AiKickPath.Clear);
        st.NotePath(AiKickKind.Kick, AiKickPath.BehindFriend);
        st.NotePath(AiKickKind.Bash, AiKickPath.TargetOffPath);
        st.NotePath(AiKickKind.Bash, AiKickPath.FriendFirst);
        st.NotePressPath(AiKickPath.FriendFirst);
        st.ContactManSeconds = 600;
        Assert.Equal(7, st.EnemyHits(AiKickKind.Kick));
        Assert.Equal(1, st.FriendHits(AiKickKind.Kick));
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiKickFriendClearMetres, Kick, "test");
        s.Set(SettingsSchema.AiBashPathHalfWidthMetres, Bash, "test");
        s.Set(SettingsSchema.AiKickRearRankMetres, Rear, "test");
        var lines = st.SummaryLines(AiKickRules.From(s)).ToList();
        string line = lines[5];
        Assert.StartsWith("ai kicks - whom they hit and the strike path (step 80): kicks: on an ENEMY 7 (his target 6, another 1, into his shield 0), on a FRIEND 1 (the man 1, his shield 0), a horse / untracked 0, MISSED 1 - friends 12.5% of the hits", line, StringComparison.Ordinal);
        Assert.Contains("| bashes: on an ENEMY 1 (his target 0, another 0, into his shield 1), on a FRIEND 1 (the man 0, his shield 1)", line, StringComparison.Ordinal);
        Assert.Contains("; enemy hits a man-minute in contact 0.800", line, StringComparison.Ordinal);
        Assert.Contains("kicks clear 1, the enemy off his strike path 0, a friend in front, beyond the rear-rank reach 0, a friend in front of him 1; bashes clear 0, the enemy off his strike path 1, a friend in front, beyond the rear-rank reach 1", line, StringComparison.Ordinal);
        Assert.Contains(" - REFUSED 3; a bash's / push's path again after its guard: clear 0, the enemy off his strike path 0, a friend in front, beyond the rear-rank reach 1", line, StringComparison.Ordinal);
        Assert.Equal("path no longer clear after a bash's guard (step 80)", AiKickMath.EndName(AiKickEnd.PathBlocked));
        Assert.Equal("a friend in front of him (step 90)", AiKickMath.SkipName(AiKickSkip.BehindFriend)); // step 90 renamed step 80's "behind a friend - rear rank"
    }

    [Fact]
    public void Format_18_moves_the_kicks_untouched_half_width_once_and_keeps_a_tuned_one()
    {
        static double Shipped(ParamDef p) => p == SettingsSchema.AiKickFriendClearMetres ? 0.6 : p.Default;
        Assert.Equal(23, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 17, \"AiKickFriendClearMetres\": 0.5 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Single(notes);
        Assert.StartsWith("AiKickFriendClearMetres: 0.5 → 0.6 (format 17 → 23, the old default; step 80", notes[0], StringComparison.Ordinal);
        Assert.Equal(0.6, read.Values["AiKickFriendClearMetres"], 6);
        Assert.Single(ConfigFile.Migrate(read, Shipped)); // idempotent: the value moved, no new note
        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 14, \"AiKickFriendClearMetres\": 0.8 }");
        var kept = ConfigFile.Migrate(tuned, Shipped);
        Assert.Single(kept);
        Assert.StartsWith("AiKickFriendClearMetres: 0.8 kept - your own value (format 14 → 23; the new default 0.6", kept[0], StringComparison.Ordinal);
        Assert.Equal(0.8, tuned.Values["AiKickFriendClearMetres"], 6);
        var now = ConfigFile.Read("{ \"ConfigVersion\": 18, \"AiKickFriendClearMetres\": 0.5 }");
        Assert.Empty(ConfigFile.Migrate(now, Shipped));
        Assert.Equal(0.5, now.Values["AiKickFriendClearMetres"], 6);
        // the tests' DESIGN value is the old 0.5: nothing to push
        var design = ConfigFile.Read("{ \"ConfigVersion\": 17, \"AiKickFriendClearMetres\": 0.5 }");
        Assert.Empty(ConfigFile.Migrate(design, p => p.Default));
    }
}
