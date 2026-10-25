using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 90 - ONLY A MAN REALLY IN FRONT BLOCKS A KICK / BASH / PUSH + WHICH SIDE BASHES HIT FRIENDS (Toni's 17:39 battle of 2026-10-24: of 95
/// 'behind a friend - rear rank' refusals only 10 were the rank in front, 53 the SHOULDER NEIGHBOUR ~0.35 m ahead / 0.64 m aside; 45 of 66 bash hits
/// on a friend; AI_NOTES "Step 90", DESIGN decision 83): the two settings placed / shipped / live, config format 21, the shoulder neighbour no
/// longer blocks a kick, a bash or a push, a man 0.8 m straight ahead does, the narrower paths, the side's sign (+ = his left, Vec2.LeftVec), the
/// side split and the neighbours in the step-87 line, 0 bytes.
/// </summary>
public class Step90Tests
{
    private const double Reach = 1.6, Rear = 1.2;

    /// <summary>defaults.json's step-90 values (the tests run on DESIGN's initial ones: 0.5 / 0.75 / 0.5 / 0.4).</summary>
    private static AiKickRules Shipped() => new AiKickRules(true, true, true, Reach, 40, 2, true, 25, 40, 35, 0.45, 0.5, Rear, 20, 0.5, 0.4);

    /// <summary>Step 80's rule exactly (the old widths, no block rule).</summary>
    private static AiKickRules Step80() => new AiKickRules(true, true, true, Reach, 40, 2, true, 25, 40, 35, 0.6, 0.75, Rear, 20);

    private static double ShippedDefault(ParamDef p) =>
        p == SettingsSchema.AiKickFriendClearMetres ? 0.45 : p == SettingsSchema.AiBashPathHalfWidthMetres ? 0.5 : p.Default;

    /// <summary>The verdict for a man at the origin facing +y, his enemy at (0, 1.5), with friends at the given (side, along) - side + = his RIGHT
    /// in the world's x (facing +y, his left is −x).</summary>
    private static AiKickPath V(in AiKickRules r, AiKickKind kind, params (double X, double Y)[] friends)
    {
        var p = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, r.Reach, r.PathHalfWidth(kind), r.RearRank, r.BlockAhead, r.BlockSide);
        foreach (var f in friends) p.Add(f.X, f.Y);
        return p.Verdict;
    }

    // ------------------------------------------------------------------ the settings

    [Fact]
    public void The_two_settings_sit_after_the_rear_rank_on_the_Advanced_page_live_and_the_narrower_paths_ship()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.AiKickRearRankMetres) + 1, all.IndexOf(SettingsSchema.AiKickBlockAheadMetres));
        Assert.Equal(all.IndexOf(SettingsSchema.AiKickBlockAheadMetres) + 1, all.IndexOf(SettingsSchema.AiKickBlockSideMetres));
        foreach (var p in new[] { SettingsSchema.AiKickBlockAheadMetres, SettingsSchema.AiKickBlockSideMetres })
        {
            Assert.Same(SettingsSchema.ImpactGroup, p.Group);
            Assert.Equal(SettingsPage.Advanced, p.Group.Page);
            Assert.Equal(ApplyTiming.Live, p.Timing);
            Assert.Equal(ParamType.Float, p.Type);
            Assert.Equal(0, p.Min, 9);
            Assert.Equal(2, p.Max, 9);
        }
        Assert.Equal(0.5, SettingsSchema.AiKickBlockAheadMetres.Default, 9); // DESIGN's initial values
        Assert.Equal(0.4, SettingsSchema.AiKickBlockSideMetres.Default, 9);
        string text = DefaultsFile.ReadEmbeddedText();
        foreach (var line in new[] { "\"AiKickFriendClearMetres\": 0.45,", "\"AiBashPathHalfWidthMetres\": 0.5,", "\"AiKickRearRankMetres\": 1.2,", "\"AiKickBlockAheadMetres\": 0.5,", "\"AiKickBlockSideMetres\": 0.4," })
            Assert.Contains(line, text, StringComparison.Ordinal);
        // live: read at use time through the rules
        var s = new TraxSettings();
        Assert.Equal(0.5, AiKickRules.From(s).BlockAhead, 6);
        Assert.Equal(0.4, AiKickRules.From(s).BlockSide, 6);
        s.Set(SettingsSchema.AiKickBlockAheadMetres, 0.7, "test");
        s.Set(SettingsSchema.AiKickBlockSideMetres, 0.25, "test");
        Assert.Equal(0.7, AiKickRules.From(s).BlockAhead, 6);
        Assert.Equal(0.25, AiKickRules.From(s).BlockSide, 6);
        Assert.Contains("step 90: a friend blocks only when really in front - 0.70 m+ ahead and within 0.25 m of the line", AiKickRules.From(s).Describe(), StringComparison.Ordinal);
        // the master switch still comes first - the block rule never turns the kicks on
        s.Set(SettingsSchema.ModEnabled, false, "test");
        Assert.False(AiKickRules.From(s).Enabled);
        Assert.Equal("ModEnabled", AiKickRules.From(s).OffBecause);
        // the old constructor (no block arguments) = step 80's rule
        Assert.Equal(0, Step80().BlockAhead, 9);
        Assert.Equal(0, Step80().BlockSide, 9);
    }

    [Fact]
    public void Format_21_narrows_an_untouched_path_keeps_a_tuned_one_and_leaves_format_21_alone()
    {
        Assert.Equal(25, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 20, \"AiKickFriendClearMetres\": 0.6, \"AiBashPathHalfWidthMetres\": 0.75, \"AiKickRearRankMetres\": 1.2 }");
        var notes = ConfigFile.Migrate(read, ShippedDefault);
        Assert.Equal(0.45, read.Values["AiKickFriendClearMetres"], 9);
        Assert.Equal(0.5, read.Values["AiBashPathHalfWidthMetres"], 9);
        Assert.Equal(1.2, read.Values["AiKickRearRankMetres"], 9);
        Assert.False(read.Values.ContainsKey("AiKickBlockAheadMetres")); // new keys: the defaults fill them like any missing key
        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, n => n.StartsWith("AiBashPathHalfWidthMetres: 0.75 → 0.5 (format 20 → 25, the old default; step 90, Toni: only a man really in front blocks", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("AiKickFriendClearMetres: 0.6 → 0.45 (format 20 → 25, the old default; step 90", StringComparison.Ordinal));
        // a tuned value is kept and said once
        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 18, \"AiKickFriendClearMetres\": 0.6, \"AiBashPathHalfWidthMetres\": 0.9 }");
        var tn = ConfigFile.Migrate(tuned, ShippedDefault);
        Assert.Equal(0.9, tuned.Values["AiBashPathHalfWidthMetres"], 9);
        Assert.Equal(0.45, tuned.Values["AiKickFriendClearMetres"], 9);
        Assert.Contains(tn, n => n.StartsWith("AiBashPathHalfWidthMetres: 0.9 kept - your own value (format 18 → 25; the new default 0.5", StringComparison.Ordinal));
        // a format-17 file's 0.5 goes through step 80's block straight to today's 0.45; the bash key did not exist yet
        var old = ConfigFile.Read("{ \"ConfigVersion\": 17, \"AiKickFriendClearMetres\": 0.5 }");
        ConfigFile.Migrate(old, ShippedDefault);
        Assert.Equal(0.45, old.Values["AiKickFriendClearMetres"], 9);
        // format 21 untouched; the rewritten file is format 21 and idempotent
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 21, \"AiKickFriendClearMetres\": 0.6, \"AiBashPathHalfWidthMetres\": 0.75 }"), ShippedDefault));
        string written = ConfigFile.Write(read.Values, read.Unknown);
        Assert.Contains("\"ConfigVersion\": 25", written, StringComparison.Ordinal);
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read(written), ShippedDefault));
        // on DESIGN's initial values (0.5 / 0.75) the move is silent for the bash; nothing pushed over a value that is already today's
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 20, \"AiBashPathHalfWidthMetres\": 0.75 }"), p => p.Default));
    }

    // ------------------------------------------------------------------ the rule

    [Fact]
    public void A_shoulder_neighbour_035_ahead_064_aside_no_longer_blocks_a_kick_a_bash_or_a_push()
    {
        var r = Shipped();
        foreach (var kind in new[] { AiKickKind.Kick, AiKickKind.Bash, AiKickKind.Push })
        {
            // Toni's mean bash neighbour, either shoulder
            Assert.Equal(AiKickPath.Clear, V(in r, kind, (0.64, 0.35)));
            Assert.Equal(AiKickPath.Clear, V(in r, kind, (-0.64, 0.35)));
            // his mean kick neighbour 0.50 ahead / 0.53 aside: outside the 0.4 m line
            Assert.Equal(AiKickPath.Clear, V(in r, kind, (0.53, 0.50)));
            // a neighbour half a step forward INSIDE the narrower path but not 0.5 m ahead: let through, counted
            var p = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, r.Reach, r.PathHalfWidth(kind), r.RearRank, r.BlockAhead, r.BlockSide);
            p.Add(0.3, 0.35);
            Assert.Equal(AiKickPath.Clear, p.Verdict);
            Assert.Equal(1, p.LetThrough);
            Assert.Equal(0, p.InPath);
        }
        // step 80's rule refused the bash for that same neighbour (the problem the log showed)
        var old = Step80();
        Assert.Equal(AiKickPath.BehindFriend, V(in old, AiKickKind.Bash, (0.64, 0.35)));
        Assert.Equal(AiKickPath.BehindFriend, V(in old, AiKickKind.Push, (-0.64, 0.35)));
    }

    [Fact]
    public void A_man_08_straight_ahead_blocks_and_a_farther_one_too()
    {
        var r = Shipped();
        foreach (var kind in new[] { AiKickKind.Kick, AiKickKind.Bash, AiKickKind.Push })
        {
            Assert.Equal(AiKickPath.BehindFriend, V(in r, kind, (0.0, 0.8)));       // the rank in front - "a friend in front of him"
            Assert.Equal(AiKickPath.BehindFriend, V(in r, kind, (0.35, 0.5)));      // the corner of the box: 0.5 ahead, 0.35 aside
            Assert.Equal(AiKickPath.FriendFirst, V(in r, kind, (0.1, 1.3)));        // in front, beyond the rear-rank reach, closer than the enemy
            Assert.Equal(AiKickPath.Clear, V(in r, kind, (0.0, 1.55)));             // beyond the enemy
            Assert.Equal(AiKickPath.Clear, V(in r, kind, (0.0, 0.45)));             // too close to count as "in front" (beside his shoulders)
        }
        // the nearest blocking friend decides; a neighbour let through does not hide the man in front
        var p = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, 0.5, Rear, 0.5, 0.4);
        p.Add(0.3, 0.3, 7);
        p.Add(-0.1, 0.9, 8);
        Assert.Equal(AiKickPath.BehindFriend, p.Verdict);
        Assert.Equal(8, p.NearestFriendIndex);
        Assert.Equal(0.9, p.NearestFriend, 9);
        // the block side never wider than the path: 0.4 set over a 0.3 path = 0.3
        var narrow = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, 0.3, Rear, 0.5, 0.4);
        narrow.Add(0.35, 0.8);
        Assert.Equal(AiKickPath.Clear, narrow.Verdict);
        // 0 / 0 = step 80's rule exactly
        var s80 = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, 0.75, Rear, 0, 0);
        s80.Add(0.7, 0.1);
        Assert.Equal(AiKickPath.BehindFriend, s80.Verdict);
    }

    [Fact]
    public void The_paths_are_narrower_bash_and_push_05_kick_045()
    {
        var r = Shipped();
        Assert.Equal(0.45, r.PathHalfWidth(AiKickKind.Kick), 9);
        Assert.Equal(0.5, r.PathHalfWidth(AiKickKind.Bash), 9);
        Assert.Equal(0.5, r.PathHalfWidth(AiKickKind.Push), 9);
        // an enemy 0.6 m off the line at 1.2 m along: in step 80's bash path (0.75), off the new one
        double d = Math.Sqrt(1.2 * 1.2 + 0.6 * 0.6), a = Math.Atan2(0.6, 1.2) * 180 / Math.PI;
        Assert.True(AiKickMath.TargetInPath(d, a, Reach, 0.75));
        Assert.False(AiKickMath.TargetInPath(d, a, Reach, r.PathHalfWidth(AiKickKind.Bash)));
        // a kick: 0.5 m off - in step 80's 0.6, off the 0.45
        double dk = Math.Sqrt(1.2 * 1.2 + 0.5 * 0.5), ak = Math.Atan2(0.5, 1.2) * 180 / Math.PI;
        Assert.True(AiKickMath.TargetInPath(dk, ak, Reach, 0.6));
        Assert.False(AiKickMath.TargetInPath(dk, ak, Reach, r.PathHalfWidth(AiKickKind.Kick)));
        Assert.Contains("kicks 0.45 m, bashes 0.50 m (pushes too) either side, rear rank 1.2 m; step 90: a friend blocks only 0.50 m+ ahead and within 0.40 m of the line",
            new AiKickStats().PathLine(in r), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the side

    [Fact]
    public void The_side_sign_is_plus_on_his_left_his_shield_side_as_the_games_LeftVec()
    {
        // facing +y: the game's Vec2.LeftVec() = (−y, x) = (−1, 0) - a friend at −x is on his LEFT
        var p = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, 0.5, Rear, 0.5, 0.4);
        p.Add(-0.2, 0.8);
        Assert.True(p.NearestFriendLateral > 0);
        Assert.Equal(0.2, p.NearestFriendLateral, 9);
        Assert.Equal(0.2, p.NearestFriendSide, 9);
        var q = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, 0.5, Rear, 0.5, 0.4);
        q.Add(0.2, 0.8);
        Assert.Equal(-0.2, q.NearestFriendLateral, 9);
        // facing +x: LeftVec = (0, 1) - a friend at +y is on his left; Locate agrees with the probe
        AiKickBlockMath.Locate(0, 0, 1, 0, 0.3, 0.5, out double along, out double side, out double lateral);
        Assert.Equal(0.3, along, 9);
        Assert.Equal(0.5, side, 9);
        Assert.Equal(0.5, lateral, 9);
        Assert.Equal(0, AiKickBlockMath.SideOf(lateral));
        Assert.Equal("his shield side (left)", AiKickBlockMath.SideOfName(AiKickBlockMath.SideOf(lateral)));
        Assert.Equal(1, AiKickBlockMath.SideOf(-0.1));
        Assert.Equal(1, AiKickBlockMath.SideOf(0));     // on the line: counted on his weapon side
        Assert.Equal(-1, AiKickBlockMath.SideOf(double.NaN));
        // the old Locate (no sign) unchanged
        AiKickBlockMath.Locate(0, 0, 1, 0, 0.3, -0.5, out double a2, out double s2);
        Assert.Equal(0.3, a2, 9);
        Assert.Equal(0.5, s2, 9);
        // the neighbours within 0.9 m: two on his left (one behind his shoulder), one on his right, one 1.0 m off not counted, every one fed counts
        var n = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, 0.5, Rear, 0.5, 0.4);
        n.Add(-0.7, 0.1);
        n.Add(-0.6, -0.3);
        n.Add(0.8, 0.2);
        n.Add(1.0, 0.0);
        Assert.Equal(2, n.LeftNear);
        Assert.Equal(1, n.RightNear);
        Assert.Equal(AiKickPath.Clear, n.Verdict);
        Assert.Equal(-1, AiKickBlocker.None.LeftNear);
        Assert.True(double.IsNaN(AiKickBlocker.None.Lateral));
        Assert.Equal(0, AiKickBlockMath.NearCase(0, 0));
        Assert.Equal(1, AiKickBlockMath.NearCase(2, 0));
        Assert.Equal(2, AiKickBlockMath.NearCase(0, 1));
        Assert.Equal(3, AiKickBlockMath.NearCase(1, 1));
        Assert.Equal(-1, AiKickBlockMath.NearCase(-1, -1));
    }

    private static AiKickBlocker B(double along, double lateral, AiKickRelation rel = AiKickRelation.ShoulderNeighbour) => new AiKickBlocker
    {
        Valid = true, Along = along, Side = Math.Abs(lateral), Lateral = lateral, Relation = rel, FriendIndex = 5, File = -1, Rank = -1, FriendFile = -1,
        FriendRank = -1, LeftNear = -1, RightNear = -1,
    };

    [Fact]
    public void The_step_87_line_splits_refusals_and_friend_hits_by_side_and_counts_the_neighbours()
    {
        var st = new AiKickStats();
        var b = st.Blocks;
        b.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Bash, B(0.8, 0.1, AiKickRelation.RankInFront), 1.4, 5);
        b.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Bash, B(0.6, -0.2, AiKickRelation.RankInFront), 1.4, 5);
        b.NoteRefusal(AiKickPath.BehindFriend, AiKickKind.Bash, B(0.7, -0.3, AiKickRelation.RankInFront), 1.4, 5);
        Assert.Equal(1, b.BySide(AiKickBlockCase.BehindFriend, AiKickKind.Bash, 0));
        Assert.Equal(2, b.BySide(AiKickBlockCase.BehindFriend, AiKickKind.Bash, 1));
        // the bashes asked: 4 with a friend tight on his shield side only, 2 with none
        for (int i = 0; i < 4; i++) b.NoteAsked(AiKickKind.Bash, 1, 0);
        for (int i = 0; i < 2; i++) b.NoteAsked(AiKickKind.Bash, 0, 0);
        b.NoteAsked(AiKickKind.Bash, -1, -1); // not counted
        Assert.Equal(4, b.AskedNear(AiKickKind.Bash, 1));
        Assert.Equal(2, b.AskedNear(AiKickKind.Bash, 0));
        // the friend hits: 3 on his shield side (two with a friend tight there), 1 on his weapon side; a push on his right
        b.NoteFriendHit(AiKickKind.Bash, B(0.0, 0.64), 15, 1, 0);
        b.NoteFriendHit(AiKickKind.Bash, B(-0.1, 0.7), 15, 1, 0);
        b.NoteFriendHit(AiKickKind.Bash, B(0.2, 0.5), 15, 0, 0);
        b.NoteFriendHit(AiKickKind.Bash, B(0.1, -0.6), 15, 0, 0);
        b.NoteFriendHit(AiKickKind.Push, B(0.3, -0.5), 15, 0, 1);
        b.NoteFriendHit(AiKickKind.Kick, B(0.6, 0.1), 15); // a kick: no neighbours counted
        Assert.Equal(0.75, b.ShieldSideShare(AiKickKind.Bash), 9);
        Assert.Equal(0.0, b.ShieldSideShare(AiKickKind.Push), 9);
        Assert.Equal(1.0, b.ShieldSideShare(AiKickKind.Kick), 9);
        Assert.Equal(2, b.HitNear(AiKickKind.Bash, 1, 0));
        Assert.Equal(1, b.HitNear(AiKickKind.Bash, 0, 0));
        Assert.Equal(1, b.HitNear(AiKickKind.Bash, 0, 1));
        Assert.Equal(4, b.HitNearCounted(AiKickKind.Bash));
        Assert.Equal(0.5, b.HitMeanLeft(AiKickKind.Bash), 9);
        Assert.Equal(0, b.HitNearCounted(AiKickKind.Kick));
        var s = new TraxSettings();
        var lines = st.SummaryLines(AiKickRules.From(s)).ToList();
        Assert.Equal(9, lines.Count); // step 95: + the step-in bash (the 9th); step 91: + the shield bash's reach (the 8th)
        string line = lines[6];
        Assert.StartsWith("ai kicks - who blocks the path (step 87 diagnostic): friend hits by bashes: 75% on his shield side (left; pushes 0%, kicks 100%) | shoulder neighbours are ", line, StringComparison.Ordinal);
        Assert.Contains("| REFUSED a friend in front of him (step 90; step 80's 'behind a friend') - kicks 0; bashes 3: ahead", line, StringComparison.Ordinal);
        Assert.Contains("; his shield side (left) 1, his weapon side (right) 2; his SHOULDER NEIGHBOUR (same rank) 0, the rank in front 3", line, StringComparison.Ordinal);
        Assert.Contains("; his shield side (left) 3, his weapon side (right) 1; his SHOULDER NEIGHBOUR (same rank) 4", line, StringComparison.Ordinal);
        Assert.Contains("| step 90 - friends within 0.9 m of him at the decision (the sign: + = his left, Vec2.LeftVec): bashes asked / hit a friend (on his shield side - weapon side) "
            + "none 2 / 2 (1 - 1) 100%, on his shield side only 4 / 2 (2 - 0) 50%, on his weapon side only 0 / 0 (0 - 0), on both sides 0 / 0 (0 - 0); the men who hit a friend had on average 0.50 left / 0.00 right; "
            + "pushes asked / hit a friend (on his shield side - weapon side) none 0 / 0 (0 - 0), on his shield side only 0 / 0 (0 - 0), on his weapon side only 0 / 1 (0 - 1), on both sides 0 / 0 (0 - 0); "
            + "the men who hit a friend had on average 0.00 left / 1.00 right", line, StringComparison.Ordinal);
    }

    [Fact]
    public void The_probe_and_the_side_notes_allocate_nothing()
    {
        var b = new AiKickBlockStats();
        var blk = B(0.6, 0.2);
        b.NoteFriendHit(AiKickKind.Bash, blk, 10, 1, 1); // warm up
        b.NoteAsked(AiKickKind.Bash, 1, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 10000; i++)
        {
            var p = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.5, Reach, 0.5, Rear, 0.5, 0.4);
            p.Add(0.64, 0.35, 1);
            p.Add(-0.64, 0.35, 2);
            p.Add(0.0, 0.8, 3);
            sum += p.LeftNear + p.RightNear + p.NearestFriendLateral;
            b.NoteRefusal(p.Verdict, AiKickKind.Bash, blk, 1.5, 10);
            b.NoteAsked(AiKickKind.Bash, p.LeftNear, p.RightNear);
            b.NoteFriendHit(AiKickKind.Bash, blk, 10, p.LeftNear, p.RightNear);
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, bytes);
        Assert.True(sum > 0);
    }
}
