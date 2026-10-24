using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 87 - WHO BLOCKS THE STRIKE PATH (a diagnostic, Toni's 5 battles of 2026-10-24: 511 of 752 refused AI kicks / bashes "behind a friend",
/// 56% of the bashes that passed the path still on a friend; AI_NOTES "Step 87"): the probe names the nearest friend in the path with his along /
/// side, the bins, the formation relation, the [summary] line - measurement only, no behaviour change.
/// </summary>
public class Step87Tests
{
    private const double Reach = 1.6, Kick = 0.6, Bash = 0.75, Rear = 1.2;

    [Fact]
    public void The_probe_reports_the_nearest_friend_in_the_path_with_his_along_side_and_index()
    {
        // he faces +y, the enemy 1.5 m ahead; a man beside his shoulder 0.1 m forward and 0.5 m over (in a bash's path), one 0.9 m ahead in the
        // rank in front, one out of the path to the side, one behind him
        var p = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, Bash, Rear);
        p.Add(0.9, 0.1, 10);   // 0.9 m over: out of the path
        p.Add(0.0, 0.9, 11);   // the rank in front
        p.Add(-0.5, 0.1, 12);  // the shoulder neighbour, half a step forward - the nearest in the path
        p.Add(0.1, -0.8, 13);  // behind him
        Assert.Equal(AiKickPath.BehindFriend, p.Verdict);
        Assert.Equal(12, p.NearestFriendIndex);
        Assert.Equal(0.1, p.NearestFriend, 9);
        Assert.Equal(0.5, p.NearestFriendSide, 9);
        Assert.Equal(2, p.InPath);
        // a kick's narrower path: the neighbour 0.5 m over is in it too (0.6); 0.65 m over is not
        var k = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, Kick, Rear);
        k.Add(0.65, 0.1, 1);
        k.Add(0.2, 0.7, 2);
        Assert.Equal(2, k.NearestFriendIndex);
        Assert.Equal(0.7, k.NearestFriend, 9);
        Assert.Equal(0.2, k.NearestFriendSide, 9);
        // nobody in the path: no index, NaNs; the old two-argument Add still works (no index)
        var none = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, Kick, Rear);
        none.Add(1.0, 0.5);
        Assert.Equal(-1, none.NearestFriendIndex);
        Assert.True(double.IsNaN(none.NearestFriend) && double.IsNaN(none.NearestFriendSide));
        var noIndex = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, Kick, Rear);
        noIndex.Add(0.1, 0.5);
        Assert.Equal(-1, noIndex.NearestFriendIndex);
        Assert.Equal(0.5, noIndex.NearestFriend, 9);
        // a facing turned 30° off his target: the frame is the facing's
        double a = 30 * Math.PI / 180;
        var t = AiKickPathProbe.Begin(0, 0, Math.Sin(a), Math.Cos(a), 0, 1.2, Reach, Bash, Rear);
        t.Add(0.6 * Math.Sin(a) + 0.2 * Math.Cos(a), 0.6 * Math.Cos(a) - 0.2 * Math.Sin(a), 7);
        Assert.Equal(7, t.NearestFriendIndex);
        Assert.Equal(0.6, t.NearestFriend, 6);
        Assert.Equal(0.2, t.NearestFriendSide, 6);
    }

    [Fact]
    public void The_bins_have_their_edges()
    {
        Assert.Equal(0, AiKickBlockMath.AlongBin(-0.3));
        Assert.Equal(0, AiKickBlockMath.AlongBin(0));
        Assert.Equal(1, AiKickBlockMath.AlongBin(0.05));
        Assert.Equal(1, AiKickBlockMath.AlongBin(0.2));
        Assert.Equal(2, AiKickBlockMath.AlongBin(0.3));
        Assert.Equal(3, AiKickBlockMath.AlongBin(0.8));
        Assert.Equal(4, AiKickBlockMath.AlongBin(1.1));
        Assert.Equal(5, AiKickBlockMath.AlongBin(1.25));
        Assert.Equal(0, AiKickBlockMath.SideBin(0.3));
        Assert.Equal(1, AiKickBlockMath.SideBin(-0.45));
        Assert.Equal(2, AiKickBlockMath.SideBin(0.75));
        Assert.Equal(3, AiKickBlockMath.SideBin(0.9));
        Assert.Equal(0, AiKickBlockMath.AngleBin(10));
        Assert.Equal(1, AiKickBlockMath.AngleBin(12));
        Assert.Equal(2, AiKickBlockMath.AngleBin(25));
        Assert.Equal(3, AiKickBlockMath.AngleBin(40));
        Assert.Equal(4, AiKickBlockMath.AngleBin(41));
        Assert.Equal("0.2-0.4", AiKickBlockMath.AlongName(2));
        Assert.Equal("0.6-0.75", AiKickBlockMath.SideName(2));
        Assert.Equal("30-40°", AiKickBlockMath.AngleName(3));
        AiKickBlockMath.Locate(0, 0, 0, 2, 0.5, 0.3, out double along, out double side);
        Assert.Equal(0.3, along, 9);
        Assert.Equal(0.5, side, 9);
    }

    [Fact]
    public void The_relation_names_a_shoulder_neighbour_the_rank_in_front_and_the_rest()
    {
        Assert.Equal(AiKickRelation.ShoulderNeighbour, AiKickBlockMath.RelationOf(true, true, 1, 1));
        Assert.Equal(AiKickRelation.ShoulderNeighbour, AiKickBlockMath.RelationOf(true, true, 0, 0));
        Assert.Equal(AiKickRelation.RankInFront, AiKickBlockMath.RelationOf(true, true, 1, 0));
        Assert.Equal(AiKickRelation.OtherRank, AiKickBlockMath.RelationOf(true, true, 0, 1));
        Assert.Equal(AiKickRelation.OtherRank, AiKickBlockMath.RelationOf(true, true, 3, 1));
        Assert.Equal(AiKickRelation.OtherFormation, AiKickBlockMath.RelationOf(false, true, 1, 1));
        Assert.Equal(AiKickRelation.OtherFormation, AiKickBlockMath.RelationOf(false, false, -1, -1));
        Assert.Equal(AiKickRelation.Unknown, AiKickBlockMath.RelationOf(true, false, 1, 1)); // a column
        Assert.Equal(AiKickRelation.Unknown, AiKickBlockMath.RelationOf(true, true, -1, 0)); // detached
        Assert.Equal(-1, AiKickBlocker.None.FriendIndex);
        Assert.False(AiKickBlocker.None.Valid);
        Assert.Equal(AiKickRelation.Unknown, AiKickBlocker.None.Relation);
    }

    private static AiKickBlocker B(double along, double side, AiKickRelation rel) =>
        new AiKickBlocker { Valid = true, Along = along, Side = side, Relation = rel, FriendIndex = 5, File = -1, Rank = -1, FriendFile = -1, FriendRank = -1 };

    [Fact]
    public void The_stats_bin_refusals_and_friend_hits_by_kind()
    {
        var s = new AiKickBlockStats();
        s.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Bash, B(0.1, 0.55, AiKickRelation.ShoulderNeighbour), 1.3, 25);
        s.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Bash, B(0.15, 0.65, AiKickRelation.ShoulderNeighbour), 1.4, 35);
        s.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Bash, B(0.9, 0.1, AiKickRelation.RankInFront), 1.5, 5);
        s.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Kick, B(1.0, 0.2, AiKickRelation.RankInFront), 1.5, 8);
        s.NoteRefusal(AiKickPath.Clear, AiKickKind.Kick, B(1.0, 0.2, AiKickRelation.RankInFront), 1.5, 8); // clear: nothing
        s.NoteRefusal(AiKickPath.TargetOffPath, AiKickKind.Kick, AiKickBlocker.None, 1.5, 36.87); // the enemy 1.2 ahead, 0.9 over
        s.NoteFriendHit(AiKickKind.Bash, B(0.3, 0.7, AiKickRelation.ShoulderNeighbour), 22);
        s.NoteFriendHit(AiKickKind.Bash, AiKickBlocker.None, 15); // not located: the angle only
        Assert.Equal(3, s.Count(AiKickBlockCase.BehindFriend, AiKickKind.Bash));
        Assert.Equal(1, s.Count(AiKickBlockCase.BehindFriend, AiKickKind.Kick));
        Assert.Equal(2, s.RelationCount(AiKickBlockCase.BehindFriend, AiKickKind.Bash, AiKickRelation.ShoulderNeighbour));
        Assert.Equal(1, s.RelationCount(AiKickBlockCase.BehindFriend, AiKickKind.Bash, AiKickRelation.RankInFront));
        Assert.Equal(2, s.AlongCount(AiKickBlockCase.BehindFriend, AiKickKind.Bash, 1));
        Assert.Equal(1, s.SideCount(AiKickBlockCase.BehindFriend, AiKickKind.Bash, 1));
        Assert.Equal(1, s.SideCount(AiKickBlockCase.BehindFriend, AiKickKind.Bash, 2));
        Assert.Equal(1, s.AngleCount(AiKickBlockCase.BehindFriend, AiKickKind.Bash, 2));
        Assert.Equal(1, s.AngleCount(AiKickBlockCase.BehindFriend, AiKickKind.Bash, 3));
        Assert.Equal(1, s.Count(AiKickBlockCase.TargetOffPath, AiKickKind.Kick));
        Assert.Equal(1, s.AlongCount(AiKickBlockCase.TargetOffPath, AiKickKind.Kick, 4)); // 1.2 ahead
        Assert.Equal(1, s.SideCount(AiKickBlockCase.TargetOffPath, AiKickKind.Kick, 3));  // 0.9 over
        Assert.Equal(2, s.Count(AiKickBlockCase.FriendHit, AiKickKind.Bash));
        Assert.Equal(1, s.HitsNotLocated);
        Assert.Equal(1, s.RelationCount(AiKickBlockCase.FriendHit, AiKickKind.Bash, AiKickRelation.ShoulderNeighbour));
        Assert.Equal(0.5, s.ShoulderShare(AiKickBlockCase.BehindFriend), 9);
        Assert.Equal(1.0, s.ShoulderShare(AiKickBlockCase.FriendHit), 9);
        Assert.True(double.IsNaN(s.ShoulderShare(AiKickBlockCase.FriendFirst)));
    }

    [Fact]
    public void The_summary_line_follows_the_strike_path_line()
    {
        var st = new AiKickStats();
        st.Blocks.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Bash, B(0.1, 0.55, AiKickRelation.ShoulderNeighbour), 1.3, 25);
        st.Blocks.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Kick, B(1.0, 0.2, AiKickRelation.RankInFront), 1.5, 8);
        st.Blocks.NoteFriendHit(AiKickKind.Bash, B(0.3, 0.7, AiKickRelation.ShoulderNeighbour), 22);
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiKickFriendClearMetres, Kick, "test");
        s.Set(SettingsSchema.AiBashPathHalfWidthMetres, Bash, "test");
        s.Set(SettingsSchema.AiKickRearRankMetres, Rear, "test");
        var lines = st.SummaryLines(AiKickRules.From(s)).ToList();
        Assert.StartsWith("ai kicks - whom they hit and the strike path (step 80)", lines[5], StringComparison.Ordinal);
        string line = lines[6];
        Assert.Equal(7, lines.Count);
        // step 90: the answer first (B() leaves Lateral 0 - on the line counts as his weapon side), the refusal renamed, the side split after the side bins
        Assert.StartsWith("ai kicks - who blocks the path (step 87 diagnostic): friend hits by bashes: 0% on his shield side (left; pushes n/a, kicks n/a) | "
            + "shoulder neighbours are 50% of the 'a friend in front of him' refusals and 100% of the friends hit", line, StringComparison.Ordinal);
        Assert.Contains("| REFUSED a friend in front of him (step 90; step 80's 'behind a friend') - kicks 1: ahead ≤0 (beside / behind his shoulders) 0, 0-0.2 0, 0.2-0.4 0, 0.4-0.8 0, 0.8-1.2 1, >1.2 0; to the side 0-0.3 1, 0.3-0.6 0, 0.6-0.75 0, >0.75 0; "
            + "his shield side (left) 0, his weapon side (right) 1; his SHOULDER NEIGHBOUR (same rank) 0, the rank in front 1 (along 1.00 / side 0.20), another rank 0, another formation / none 0, unknown (no line ranks) 0; angle 0-10° 1, 10-20° 0, 20-30° 0, 30-40° 0, >40° 0; bashes 1: ahead", line, StringComparison.Ordinal);
        Assert.Contains("his SHOULDER NEIGHBOUR (same rank) 1 (along 0.10 / side 0.55)", line, StringComparison.Ordinal);
        Assert.Contains("| REFUSED a friend in front beyond the rear-rank reach - kicks 0; bashes 0; pushes 0 | REFUSED the enemy off his path (the ENEMY's place) - kicks 0; bashes 0; pushes 0 | LANDED ON A FRIEND (his place at the hit, the angle at the decision) - kicks 0; bashes 1: ahead", line, StringComparison.Ordinal);
        Assert.DoesNotContain("not located", line, StringComparison.Ordinal);
        Assert.StartsWith("ai kicks - who blocks the path (step 87 diagnostic): friend hits by bashes: n/a on his shield side (left; pushes n/a, kicks n/a) | "
            + "shoulder neighbours are n/a of the 'a friend in front of him' refusals and n/a of the friends hit", new AiKickBlockStats().Line(), StringComparison.Ordinal);
    }

    [Fact]
    public void Noting_allocates_nothing()
    {
        var s = new AiKickBlockStats();
        var b = B(0.3, 0.4, AiKickRelation.ShoulderNeighbour);
        s.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Bash, b, 1.2, 20); // warm up
        s.NoteFriendHit(AiKickKind.Kick, b, 10);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            var p = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, Bash, Rear);
            p.Add(0.3, 0.4, i);
            p.Add(0.0, 0.9, i + 1);
            s.NoteRefusal(p.Verdict, AiKickKind.Bash, b, 1.2, 20);
            s.NoteFriendHit(AiKickKind.Kick, b, 10);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
