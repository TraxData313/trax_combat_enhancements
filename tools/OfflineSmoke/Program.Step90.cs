using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 90 - ONLY A MAN REALLY IN FRONT BLOCKS A KICK / BASH / PUSH + WHICH SIDE BASHES HIT FRIENDS (AI_NOTES "Step 90").
    /// Step 80 / 87's two grinding lines (the stand-in engine's own reach and widths, each side one formation), the shipped shares, run twice with
    /// the same dice: STEP 80's RULE (0.6 / 0.75, anyone in the path blocks) and the SHIPPED step-90 values (0.45 / 0.5, a friend blocks only 0.5 m+
    /// ahead within 0.4 m of the line). The refusals by shoulder neighbours drop to ~0; every friend hit is counted by side; the asks are counted by
    /// the friends tight beside him.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Step 90: the older smokes assert step 80's numbers - they run step 80's strike path (0.6 / 0.75 / 1.2, no block rule).</summary>
        private static void Step80PathDefaults()
        {
            S.Set(SettingsSchema.AiKickFriendClearMetres, 0.6, SettingSources.File);
            S.Set(SettingsSchema.AiBashPathHalfWidthMetres, 0.75, SettingSources.File);
            S.Set(SettingsSchema.AiKickRearRankMetres, 1.2, SettingSources.File);
            S.Set(SettingsSchema.AiKickBlockAheadMetres, 0.0, SettingSources.File);
            S.Set(SettingsSchema.AiKickBlockSideMetres, 0.0, SettingSources.File);
        }

        /// <summary>Step 90: the shipped strike path (defaults.json).</summary>
        private static void Step90PathDefaults()
        {
            foreach (var p in new[]
                     {
                         SettingsSchema.AiKickFriendClearMetres, SettingsSchema.AiBashPathHalfWidthMetres, SettingsSchema.AiKickRearRankMetres,
                         SettingsSchema.AiKickBlockAheadMetres, SettingsSchema.AiKickBlockSideMetres,
                     })
                S.Set(p, p.Default, SettingSources.File);
        }

        private static int ShoulderRefusals(AiKickBlockStats b)
        {
            int n = 0;
            for (int k = 1; k < AiKickMath.KindCount; k++)
                n += b.RelationCount(AiKickBlockCase.BehindFriend, (AiKickKind)k, AiKickRelation.ShoulderNeighbour)
                     + b.RelationCount(AiKickBlockCase.FriendFirst, (AiKickKind)k, AiKickRelation.ShoulderNeighbour);
            return n;
        }

        private static int FriendRefusals(AiKickBlockStats b)
        {
            int n = 0;
            for (int k = 1; k < AiKickMath.KindCount; k++)
                n += b.Count(AiKickBlockCase.BehindFriend, (AiKickKind)k) + b.Count(AiKickBlockCase.FriendFirst, (AiKickKind)k);
            return n;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnlyAManInFrontBlocks()
        {
            var keep = _logic;
            var c = CultureInfo.InvariantCulture;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                Step88Shares();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                Step90PathDefaults();
                Check(Math.Abs(S.AiKickFriendClearMetres - 0.45) < 1e-6 && Math.Abs(S.AiBashPathHalfWidthMetres - 0.5) < 1e-6 && Math.Abs(S.AiKickRearRankMetres - 1.2) < 1e-6
                      && Math.Abs(S.AiKickBlockAheadMetres - 0.5) < 1e-6 && Math.Abs(S.AiKickBlockSideMetres - 0.4) < 1e-6,
                    "the shipped step-90 path is not 0.45 / 0.5 / 1.2 / 0.5 / 0.4: " + S.AiKickFriendClearMetres + " / " + S.AiBashPathHalfWidthMetres + " / " + S.AiKickRearRankMetres
                    + " / " + S.AiKickBlockAheadMetres + " / " + S.AiKickBlockSideMetres);
                var after = FightTwoLines(90, 9000);
                LogHas("[summary] ai kicks - who blocks the path (step 87 diagnostic): friend hits by bashes: ");
                LogHas("step 90: a friend blocks only 0.50 m+ ahead and within 0.40 m of the line");
                Step80PathDefaults();
                var before = FightTwoLines(90, 9100);
                var a = after.Stats.Blocks;
                var b = before.Stats.Blocks;
                int shBefore = ShoulderRefusals(b), shAfter = ShoulderRefusals(a);
                Console.WriteLine("        step 90 the two lines, step 80's rule: " + before + "; refusals by a friend " + FriendRefusals(b) + ", by a SHOULDER NEIGHBOUR " + shBefore);
                Console.WriteLine("        step 90 the same, the shipped rule:   " + after + "; refusals by a friend " + FriendRefusals(a) + ", by a SHOULDER NEIGHBOUR " + shAfter
                    + "; let through " + after.Stats.PathFriendsLetThrough);
                Console.WriteLine("        step 90 line (shipped): " + a.Line());
                // 1. the shoulder neighbours no longer refuse the move
                Check(shBefore > 20, "step 90: the stand-in lines do not show the shoulder-neighbour refusals under step 80's rule (" + shBefore + ")");
                Check(shAfter <= Math.Max(2, shBefore / 20), "step 90: shoulder neighbours still refuse " + shAfter + " (step 80's rule " + shBefore + ")");
                Check(after.Stats.PathFriendsLetThrough > 0, "step 90: no friend in a path was let through");
                // 2. THE TRADE (measured, not wished for): the narrower path refuses an enemy standing to the side (the 'enemy off his path' refusals
                // take the neighbours' place - about as many moves are made) and misses fewer, but a bash's sweep in the stand-in engine (0.7 m either
                // side) still reaches the neighbour it no longer waits for - more friends are hit. The enemy hits must hold up.
                Check(after.EnemyHits > 0.85 * before.EnemyHits, "step 90: the enemy hits fell more than 15% (" + after.EnemyHits + " vs " + before.EnemyHits + ")");
                Check(after.Misses <= before.Misses, "step 90: the narrower path misses more (" + after.Misses + " vs " + before.Misses + ")");
                Check(after.FriendShare < 0.2, "step 90: friends " + (100 * after.FriendShare).ToString("0.0", c) + "% of the hits in the stand-in (guard: under 20%)");
                // 3. every friend hit counted by side, every ask and every bash / push friend hit by the friends tight beside him
                for (int k = 1; k < AiKickMath.KindCount; k++)
                {
                    var kind = (AiKickKind)k;
                    int hits = a.Count(AiKickBlockCase.FriendHit, kind);
                    Check(hits == after.Stats.FriendHits(kind), "step 90: friend hits binned " + hits + " vs " + after.Stats.FriendHits(kind) + " (" + kind + ")");
                    Check(a.BySide(AiKickBlockCase.FriendHit, kind, 0) + a.BySide(AiKickBlockCase.FriendHit, kind, 1) == hits,
                        "step 90: the friend hits by side do not add up (" + kind + ")");
                    int asked = 0;
                    for (int nc = 0; nc < AiKickBlockMath.NearCases; nc++) asked += a.AskedNear(kind, nc);
                    Check(asked == after.Stats.AskedCount(kind), "step 90: the asks by neighbours " + asked + " vs asked " + after.Stats.AskedCount(kind) + " (" + kind + ")");
                    Check(a.HitNearCounted(kind) == hits, "step 90: the friend hits with their neighbours counted " + a.HitNearCounted(kind) + " vs " + hits + " (" + kind + ")");
                    int refusals = a.Count(AiKickBlockCase.BehindFriend, kind) + a.Count(AiKickBlockCase.FriendFirst, kind);
                    int bySide = a.BySide(AiKickBlockCase.BehindFriend, kind, 0) + a.BySide(AiKickBlockCase.BehindFriend, kind, 1)
                                 + a.BySide(AiKickBlockCase.FriendFirst, kind, 0) + a.BySide(AiKickBlockCase.FriendFirst, kind, 1);
                    Check(bySide == refusals, "step 90: the refusals by side " + bySide + " vs " + refusals + " (" + kind + ")");
                }
                // 4. in the stand-in the lines are symmetric and men turn either way: friends get hit on both sides (the real answer is Toni's log)
                int bashHits = a.Count(AiKickBlockCase.FriendHit, AiKickKind.Bash) + b.Count(AiKickBlockCase.FriendHit, AiKickKind.Bash);
                Console.WriteLine("        step 90 friend hits by bashes on his shield side: shipped " + Pct(a.ShieldSideShare(AiKickKind.Bash)) + " of "
                    + a.Count(AiKickBlockCase.FriendHit, AiKickKind.Bash) + ", step 80's rule " + Pct(b.ShieldSideShare(AiKickKind.Bash)) + " of " + b.Count(AiKickBlockCase.FriendHit, AiKickKind.Bash)
                    + "; friends' share of all hits " + (100 * after.FriendShare).ToString("0.0", c) + "% (step 80's rule " + (100 * before.FriendShare).ToString("0.0", c) + "%)");
                Check(bashHits > 0, "step 90: no bash landed on a friend in either run - the side split is untested");

                string Pct(double v) => double.IsNaN(v) ? "n/a" : (100 * v).ToString("0", c) + "%";
            }
            finally
            {
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                AiKickDefaults();
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Step90PathDefaults();
                Step65CleanUp(keep);
            }
        }
    }
}
