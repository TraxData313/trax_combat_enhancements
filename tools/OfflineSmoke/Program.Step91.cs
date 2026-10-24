using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 91 - SHIELD BASHES GET THEIR OWN SHORTER REACH (AI_NOTES "Step 91"). Step 80 / 87 / 90's two grinding lines, the
    /// shipped shares and path, the fronts 1.0 m apart (so the enemy stands on both sides of 1.1 m) and the stand-in engine's SHIELD BASH reaching
    /// only 1.15 m along his facing (the hypothesis from Toni's 19:56 log: the bash's sweep is short) - run twice with the same dice: the bash reach
    /// OFF (step 81: a bash rolled anywhere in the kick's 1.6 m) and the SHIPPED 1.1 m. Bashes only within 1.1 m, the rolls beyond split 41.7 / 58.3,
    /// the friends the bashes hit before / after, every bash binned by the enemy's distance at its decision.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Step 91: the bash reach step 91 shipped and this check measures - 1.1 m, set explicitly (step 91b shipped 0 = off again).</summary>
        private static void Step91Reach() => S.Set(SettingsSchema.AiBashReachMetres, 1.1, SettingSources.File);

        /// <summary>Step 91b: the shipped bash reach (defaults.json - 0, off).</summary>
        private static void ShippedBashReach() => S.Set(SettingsSchema.AiBashReachMetres, SettingsSchema.AiBashReachMetres.Default, SettingSources.File);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BashesOnlyWithinTheirReach()
        {
            var keep = _logic;
            var c = CultureInfo.InvariantCulture;
            const double front = 1.0, bashEngine = 1.15;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                Step88Shares();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                Step90PathDefaults();
                Step91Reach();
                Check(Math.Abs(S.AiBashReachMetres - 1.1) < 1e-6, "step 91's AiBashReachMetres is not 1.1: " + S.AiBashReachMetres);
                Check(Math.Abs(SettingsSchema.AiBashReachMetres.Default) < 1e-9, "step 91b: the shipped AiBashReachMetres is not 0: " + SettingsSchema.AiBashReachMetres.Default);
                var after = FightTwoLines(91, 9200, frontGap: front, bashEngineReach: bashEngine);
                LogHas("[summary] ai kicks - the shield bash's reach (step 91): a shieldman bashes only within 1.10 m (AiBashReachMetres");
                ShippedBashReach(); // step 91b: the shipped 0 - off
                var before = FightTwoLines(91, 9300, frontGap: front, bashEngineReach: bashEngine);
                LogHas("[summary] ai kicks - the shield bash's reach (step 91): the bash reach is OFF - a shieldman bashes at the kick reach, anywhere within 1.6 m");
                var a = after.Stats.BashReach;
                var b = before.Stats.BashReach;
                var r = new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 35, 0.45, 0.5, 1.2, 20, 0.5, 0.4, 1.1);
                Console.WriteLine("        step 91 the bash reach OFF: " + before + "; bashes asked " + before.Stats.AskedCount(AiKickKind.Bash) + ", on a FRIEND "
                    + before.Stats.FriendHits(AiKickKind.Bash) + " of " + BashHits(before.Stats));
                Console.WriteLine("        step 91 the bash reach 1.1 m: " + after + "; bashes asked " + after.Stats.AskedCount(AiKickKind.Bash) + ", on a FRIEND "
                    + after.Stats.FriendHits(AiKickKind.Bash) + " of " + BashHits(after.Stats) + "; ended after the guard beyond the reach " + a.PressBeyond);
                Console.WriteLine("        step 91 line (OFF):  " + b.Line(new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 35, 0.45, 0.5, 1.2, 20, 0.5, 0.4, 0)));
                Console.WriteLine("        step 91 line (1.1):  " + a.Line(r));
                // 1. the grind: bashes only within 1.1 m (none asked in a bin past 1.1, none asked beyond)
                Check(a.AskedBeyond == 0, "step 91: " + a.AskedBeyond + " bashes asked beyond the bash reach");
                for (int bin = 3; bin < AiBashReachStats.BinCount; bin++)
                {
                    Check(a.Asked(bin) == 0 && a.BashRolled(bin) == 0, "step 91: bashes rolled / asked in " + AiBashReachStats.BinName(bin) + ": " + a.BashRolled(bin) + " / " + a.Asked(bin));
                }
                int farBefore = 0;
                for (int bin = 3; bin < AiBashReachStats.BinCount; bin++) farBefore += b.Asked(bin);
                Check(farBefore > 20, "step 91: the stand-in lines never put the enemy beyond 1.1 m with the reach off (" + farBefore + " bashes there) - the test says nothing");
                // 2. the shares: within 25 / 40 / 35, beyond 41.7 / 0 / 58.3 (a few hundred moves each - ±6 points)
                int nIn = a.Moves(false), nOut = a.Moves(true);
                Check(nIn > 150 && nOut > 150, "step 91: too few shieldman moves within / beyond the reach (" + nIn + " / " + nOut + ")");
                Near("within: kick", a.Rolled(false, AiKickKind.Kick), nIn, 25);
                Near("within: bash", a.Rolled(false, AiKickKind.Bash), nIn, 40);
                Near("within: swing", a.Rolled(false, AiKickKind.None), nIn, 35);
                Near("beyond: kick", a.Rolled(true, AiKickKind.Kick), nOut, 100.0 * 25 / 60);
                Check(a.Rolled(true, AiKickKind.Bash) == 0, "step 91: a bash rolled beyond the bash reach");
                Near("beyond: swing", a.Rolled(true, AiKickKind.None), nOut, 100.0 * 35 / 60);
                // 3. every asked bash came to exactly one result, filed by its distance
                foreach (var (st, name) in new[] { (after.Stats, "1.1"), (before.Stats, "OFF") })
                {
                    int results = 0;
                    for (int res = 0; res < (int)AiBashResult.Count; res++) results += st.BashReach.ResultTotal((AiBashResult)res);
                    Check(st.BashReach.AskedTotal() == st.AskedCount(AiKickKind.Bash), "step 91 (" + name + "): bashes binned " + st.BashReach.AskedTotal() + " vs asked " + st.AskedCount(AiKickKind.Bash));
                    Check(results == st.AskedCount(AiKickKind.Bash), "step 91 (" + name + "): bash results " + results + " vs asked " + st.AskedCount(AiKickKind.Bash));
                    Check(st.BashReach.ResultTotal(AiBashResult.Friend) == st.FriendHits(AiKickKind.Bash), "step 91 (" + name + "): friend results " + st.BashReach.ResultTotal(AiBashResult.Friend)
                        + " vs step 80's " + st.FriendHits(AiKickKind.Bash));
                }
                // 4. the friends the bashes hit: fewer with the reach (the far bashes swept past the enemy into the neighbour in the stand-in)
                double fb = FriendShareOfBashHits(before.Stats), fa = FriendShareOfBashHits(after.Stats);
                Console.WriteLine("        step 91 friends' share of the BASH hits: OFF " + (100 * fb).ToString("0.0", c) + "% (" + before.Stats.FriendHits(AiKickKind.Bash) + "), 1.1 m "
                    + (100 * fa).ToString("0.0", c) + "% (" + after.Stats.FriendHits(AiKickKind.Bash) + "); kicks asked " + before.Stats.AskedCount(AiKickKind.Kick) + " → " + after.Stats.AskedCount(AiKickKind.Kick));
                Check(after.Stats.FriendHits(AiKickKind.Bash) < before.Stats.FriendHits(AiKickKind.Bash), "step 91: the bashes hit as many friends with the reach ("
                    + after.Stats.FriendHits(AiKickKind.Bash) + " vs " + before.Stats.FriendHits(AiKickKind.Bash) + ")");
                Check(fa < fb, "step 91: the friends' share of the bash hits did not fall (" + fa + " vs " + fb + ")");
                // the bash's share went to kicks: more kicks with the reach on
                Check(after.Stats.AskedCount(AiKickKind.Kick) > before.Stats.AskedCount(AiKickKind.Kick), "step 91: no more kicks once the far bashes became kicks / swings");

                void Near(string what, int count, int of, double target)
                {
                    double p = 100.0 * count / of;
                    Check(Math.Abs(p - target) <= 6, "step 91: " + what + " " + p.ToString("0.0", c) + "% (target " + target.ToString("0.0", c) + "%)");
                }
            }
            finally
            {
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                AiKickDefaults();
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Step90PathDefaults();
                ShippedBashReach();
                Step65CleanUp(keep);
            }
        }

        private static int BashHits(AiKickStats st) => st.EnemyHits(AiKickKind.Bash) + st.FriendHits(AiKickKind.Bash) + st.OutcomeCount(AiKickKind.Bash, AiKickOutcome.Untracked);

        private static double FriendShareOfBashHits(AiKickStats st)
        {
            int hits = BashHits(st);
            return hits > 0 ? (double)st.FriendHits(AiKickKind.Bash) / hits : 0;
        }
    }
}
