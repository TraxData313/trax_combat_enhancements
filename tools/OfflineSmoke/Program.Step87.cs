using System;
using System.Globalization;
using TraxCombat.Core;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 87 - WHO BLOCKS THE STRIKE PATH (a diagnostic; AI_NOTES "Step 87"). Run inside step 80's two grinding lines (each
    /// side one formation, rank 0 the front, the stand-in body answering the formation place from its Ranks table): every refusal by a friend has
    /// its blocking friend binned, every kick / bash on a friend has the friend located at the hit; the new [summary] line is written.
    /// </summary>
    internal static partial class Program
    {
        private static void Step87WhoBlocks(LineFightResult on, LineFightResult off)
        {
            var c = CultureInfo.InvariantCulture;
            LogHas("[summary] ai kicks - who blocks the path (step 87 diagnostic): friend hits by bashes: "); // step 90: the side's answer first
            LogHas(" | shoulder neighbours are ");
            LogHas("[impact] first AI shield bash REFUSED by a friend - who blocks the path (step 87 diagnostic)");
            var b = on.Stats.Blocks;
            int behind = 0, rel = 0, front = 0, shoulder = 0;
            for (int k = 1; k <= 2; k++)
            {
                var kind = (AiKickKind)k;
                behind += b.Count(AiKickBlockCase.BehindFriend, kind) + b.Count(AiKickBlockCase.FriendFirst, kind);
                for (int r = 0; r < (int)AiKickRelation.Count; r++)
                {
                    rel += b.RelationCount(AiKickBlockCase.BehindFriend, kind, (AiKickRelation)r) + b.RelationCount(AiKickBlockCase.FriendFirst, kind, (AiKickRelation)r);
                }
                front += b.RelationCount(AiKickBlockCase.BehindFriend, kind, AiKickRelation.RankInFront);
                shoulder += b.RelationCount(AiKickBlockCase.BehindFriend, kind, AiKickRelation.ShoulderNeighbour);
                // every refusal the path line counted is binned here too
                Check(b.Count(AiKickBlockCase.BehindFriend, kind) == on.Stats.PathCount(kind, AiKickPath.BehindFriend), "step 87: the behind-a-friend refusals binned "
                    + b.Count(AiKickBlockCase.BehindFriend, kind) + " vs the path's " + on.Stats.PathCount(kind, AiKickPath.BehindFriend) + " (" + kind + ")");
                Check(b.Count(AiKickBlockCase.TargetOffPath, kind) == on.Stats.PathCount(kind, AiKickPath.TargetOffPath), "step 87: the off-path refusals binned differ (" + kind + ")");
                // every friend hit located (the stand-in world places everyone)
                Check(off.Stats.Blocks.Count(AiKickBlockCase.FriendHit, kind) == off.Stats.FriendHits(kind), "step 87: the friend hits binned "
                    + off.Stats.Blocks.Count(AiKickBlockCase.FriendHit, kind) + " vs " + off.Stats.FriendHits(kind) + " (" + kind + ", the path off)");
            }
            Check(behind > 0 && rel == behind, "step 87: refusals by a friend " + behind + ", with a formation place " + rel);
            // the finding the stand-in shows: since step 81 a rear-rank man's enemy stands beyond kick reach (he rolls nothing), so the 'behind a friend'
            // refusals are FRONT-rank men turned toward an enemy half a file over, a shoulder neighbour in the path - the step-87 suspicion
            Check(shoulder > 0 && shoulder >= front, "step 87: the two grinding lines show no shoulder neighbour blocking (shoulder " + shoulder + ", the rank in front " + front + ")");
            Check(off.Stats.Blocks.HitsNotLocated == 0, "step 87: friend hits not located " + off.Stats.Blocks.HitsNotLocated);
            Console.WriteLine("        step 87 ON (the path on) - behind a friend: the rank in front " + front + ", shoulder neighbours " + shoulder + " of " + behind
                + " refusals by a friend; shoulder share " + (100 * b.ShoulderShare(AiKickBlockCase.BehindFriend)).ToString("0", c) + "%");
            Console.WriteLine("        step 87 ON line: " + b.Line());
            Console.WriteLine("        step 87 OFF line (the path off - every friend hit): " + off.Stats.Blocks.Line());
        }
    }
}
