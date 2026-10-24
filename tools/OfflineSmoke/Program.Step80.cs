using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 80 - AI KICKS AND BASHES LAND ON ENEMIES, NOT FRIENDS (AI_NOTES "Step 80"). Two two-deep lines facing each other
    /// in a stand-in world on a plane (FakeAiKickBody.Bodies): the REAL logic's looks and attempts decide, a stand-in ENGINE plays each kick /
    /// bash and finds the first body along the man's facing at the moment it lands (its own reach and width - a foot 1.25 m out plus a body's
    /// 0.4 m; a foot's 0.12 m / a shield's 0.3 m either side plus a body's 0.4 m - not the mod's numbers), with the men shuffling and turning all
    /// the while. The same battle with the strike path off is the "before" (step 68 measured only reach and front - and its line check is gone).
    /// </summary>
    internal static partial class Program
    {
        private sealed class LineFightResult
        {
            public int Asked, Played, EnemyHits, FriendHits, Misses, Refused, PressBlocked;
            public double ContactMinutes;
            public string Summary = string.Empty;

            /// <summary>Step 81: the logic's own counts, the swings released by man type (0 a shieldman, 1 none), the kicks / bashes spent at an empty
            /// stack, the fewest stored swings any man had when he wound up.</summary>
            public AiKickStats Stats = new AiKickStats();
            public readonly int[] Swings = new int[2];
            public int KicksAtEmpty, KicksSpent, FewestAtWindUp = int.MaxValue;
            public string SharesLine = string.Empty;

            public double FriendShare => EnemyHits + FriendHits > 0 ? (double)FriendHits / (EnemyHits + FriendHits) : 0;
            public double EnemyPerManMinute => ContactMinutes > 0 ? EnemyHits / ContactMinutes : 0;
            public double AskedPerManMinute => ContactMinutes > 0 ? Asked / ContactMinutes : 0;

            public override string ToString() => "asked " + Asked + " (" + AskedPerManMinute.ToString("0.00", CultureInfo.InvariantCulture) + " a man-minute), played " + Played
                + ", on an ENEMY " + EnemyHits + " (" + EnemyPerManMinute.ToString("0.000", CultureInfo.InvariantCulture) + " a man-minute), on a FRIEND " + FriendHits
                + " (" + (100 * FriendShare).ToString("0.0", CultureInfo.InvariantCulture) + "% of the hits), missed " + Misses + "; refused by the path " + Refused
                + ", a bash ended after its guard " + PressBlocked + "; " + ContactMinutes.ToString("0.0", CultureInfo.InvariantCulture) + " man-minutes in contact";
        }

        /// <summary>
        /// Step 80: two two-deep lines of 12 files (0.9 m apart, the enemy's half a file over, 1.3 m between the fronts), half the files
        /// shieldmen, every man shuffling ±0.15 m across / ±0.2 m along his file and turning up to ±30° off his target; 3 minutes ON (the defaults) and OFF (the paths 0) with the same
        /// dice - ON: friends under 5% of the hits and the enemy still kicked; OFF: the problem the logs showed. Since step 81 the kicks and bashes come
        /// from the men's own swings (the shares at the decision point), not from a rate.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void KicksLandOnEnemiesNotFriends()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                foreach (var p in new[] { SettingsSchema.AiKickFriendClearMetres, SettingsSchema.AiBashPathHalfWidthMetres, SettingsSchema.AiKickRearRankMetres })
                    S.Set(p, p.Default, SettingSources.File);
                Check(Math.Abs(S.AiKickFriendClearMetres - 0.6) < 1e-6 && Math.Abs(S.AiBashPathHalfWidthMetres - 0.75) < 1e-6 && Math.Abs(S.AiKickRearRankMetres - 1.2) < 1e-6,
                    "the step-80 defaults are not 0.6 / 0.75 / 1.2: " + S.AiKickFriendClearMetres + " / " + S.AiBashPathHalfWidthMetres + " / " + S.AiKickRearRankMetres);

                var on = FightTwoLines(80, 8000);
                LogHas("[summary] ai kicks - whom they hit and the strike path (step 80): kicks: on an ENEMY ");
                LogHas("[impact] first AI ");
                S.Set(SettingsSchema.AiKickFriendClearMetres, 0.0, SettingSources.Mcm);
                S.Set(SettingsSchema.AiBashPathHalfWidthMetres, 0.0, SettingSources.Mcm);
                var off = FightTwoLines(80, 8100);
                Console.WriteLine("        step 80 two two-deep lines, 3 minutes - the strike path ON: " + on);
                Console.WriteLine("        step 80 the same, the path OFF (before): " + off);
                Console.WriteLine("        step 80 ON summary: " + on.Summary);
                // step 81: the kicks come from the men's own wind-ups now - a rear-rank man's enemy stands beyond kick reach, so he rolls nothing and the
                // path OFF hits fewer friends than step 80's rate did (27%); the path still cuts them to under a third
                Check(off.FriendShare > 0.08, "the stand-in battle does not show the problem with the path off: friends " + off.FriendShare.ToString("0.000", CultureInfo.InvariantCulture));
                Check(on.FriendShare < off.FriendShare / 3, "the strike path ON did not cut the friends' share to under a third: " + on.FriendShare + " vs " + off.FriendShare);
                Check(on.FriendShare < 0.05, "the strike path ON: friends " + (100 * on.FriendShare).ToString("0.0", CultureInfo.InvariantCulture) + "% of the hits (target under 5%)");
                Check(on.EnemyHits >= 30, "the strike path ON: only " + on.EnemyHits + " hits on an enemy in 3 minutes - the kicks stopped");
                Check(on.Refused > 0, "the strike path refused nothing");
                Check(on.EnemyPerManMinute > 0.5 * off.EnemyPerManMinute, "the strike path ON lost more than half the enemy hits: " + on.EnemyPerManMinute + " vs " + off.EnemyPerManMinute);
                Step87WhoBlocks(on, off);
            }
            finally
            {
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                foreach (var p in new[] { SettingsSchema.AiKickFriendClearMetres, SettingsSchema.AiBashPathHalfWidthMetres, SettingsSchema.AiKickRearRankMetres })
                    S.Set(p, p.Default, SettingSources.File);
                Step65CleanUp(keep);
            }
        }

        /// <summary>
        /// Step 80's two two-deep lines, since step 81 driven by the men's own SWINGS: each man winds up (step 13's phases see the engine's ReadyMelee
        /// - the real decision point queues it, the real tick rolls it) about 1.0-1.6 s after his last move ends, releases 0.5 s later, is over 0.4 s
        /// after that; a rolled kick / bash cancels his wind-up (the stand-in engine drops it to idle) and plays in its commit. With
        /// <paramref name="stack"/> the swing stack is on (step 32 - the stand-in AI waits at 0 like the stack's hold, the stack's regen pass every
        /// 0.1 s): swings and kicks / bashes spend from it. <paramref name="keepFresh"/> off = no refill at all (every swing 10, every kick / bash 8 drain
        /// the bar to empty - a tired line).
        /// </summary>
        private static LineFightResult FightTwoLines(int seed, int baseIndex, bool stack = false, bool keepFresh = true)
        {
            const int files = 12;
            const double spacing = 0.9, rankGap = 1.0, frontGap = 1.3, seconds = 180, dt = 1.0 / 30;
            // the stand-in ENGINE's kick / bash: a foot 1.25 m out + a body's 0.4; 0.12 m (a foot) / 0.3 m (a shield) either side + a body's 0.4
            const double engineReach = 1.25 + 0.4, engineKickSide = 0.12 + 0.4, engineBashSide = 0.3 + 0.4;
            var body = new FakeAiKickBody();
            var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
            logic.AiKickBody = body;
            logic.AiKickDice = new SeededRandom(seed);
            var rnd = new Random(seed);
            int n = 4 * files;
            var men = new TrackedAgent[n];
            var bx = new double[n];
            var by = new double[n];
            var side = new int[n];
            var ox = new double[n];
            var oy = new double[n];
            var turn = new double[n];
            var shield = new bool[n];
            var x = new double[n];
            var y = new double[n];
            var fx = new double[n];
            var fy = new double[n];
            var target = new int[n];
            var hitAt = new double[n];
            var endAt = new double[n];
            var swingPhase = new int[n];   // step 81: 0 idle, 1 his wind-up, 2 his release
            var phaseEnd = new double[n];
            var nextReady = new double[n];
            var inAttempt = new bool[n];
            double t = 80000 + baseIndex;
            for (int i = 0; i < n; i++)
            {
                int s = i / (2 * files), rank = (i / files) % 2, file = i % files;
                side[i] = s;
                bx[i] = file * spacing + (s == 1 ? spacing / 2 : 0);
                by[i] = s == 0 ? -rank * rankGap : frontGap + rank * rankGap;
                shield[i] = file % 2 == 0;
                var m = logic.Track(FakeAgent(baseIndex + i))!;
                m.AthleticsSkill = 100;
                SetBar(m, 1.0);
                m.PrevAction = m.PrevLowerAction = 35;
                m.FeverContact = true;
                if (shield[i]) body.Shield.Add(baseIndex + i);
                body.Ranks[baseIndex + i] = (s, file, rank); // step 87: each side one formation, rank 0 the front
                men[i] = m;
                hitAt[i] = endAt[i] = double.NaN;
                nextReady[i] = t + rnd.NextDouble() * 1.5;
            }
            var res = new LineFightResult();
            var ar = Rules;
            int steps = (int)(seconds / dt);
            double nextStackPass = t;
            for (int k = 0; k < steps; k++)
            {
                t += dt;
                // the men shuffle and turn
                for (int i = 0; i < n; i++)
                {
                    ox[i] = Math.Max(-0.15, Math.Min(0.15, ox[i] + (rnd.NextDouble() - 0.5) * 0.04)); // never closer than 0.6 m to a neighbour (two bodies)
                    oy[i] = Math.Max(-0.2, Math.Min(0.2, oy[i] + (rnd.NextDouble() - 0.5) * 0.04));
                    turn[i] = Math.Max(-30, Math.Min(30, turn[i] + (rnd.NextDouble() - 0.5) * 3));
                    x[i] = bx[i] + ox[i];
                    y[i] = by[i] + oy[i];
                }
                for (int i = 0; i < n; i++)
                {
                    int best = -1;
                    double bd = double.MaxValue;
                    for (int j = 0; j < n; j++)
                    {
                        if (side[j] == side[i]) continue;
                        double d2 = (x[j] - x[i]) * (x[j] - x[i]) + (y[j] - y[i]) * (y[j] - y[i]);
                        if (d2 < bd)
                        {
                            bd = d2;
                            best = j;
                        }
                    }
                    target[i] = best;
                    double dx = x[best] - x[i], dy = y[best] - y[i], d = Math.Sqrt(bd);
                    double a = turn[i] * Math.PI / 180, c = Math.Cos(a), sn = Math.Sin(a);
                    fx[i] = (dx * c - dy * sn) / d;
                    fy[i] = (dx * sn + dy * c) / d;
                    body.Targets[baseIndex + i] = (men[best], (float)d, (float)Math.Abs(turn[i]));
                    body.Bodies[baseIndex + i] = (x[i], y[i], fx[i], fy[i], side[i]);
                }
                if (keepFresh && k % 240 == 0)
                    for (int i = 0; i < n; i++) SetBar(men[i], 1.0); // the rest of the fight keeps them fresh (step 81's tiring run: no refill at all)
                if (stack && t >= nextStackPass)
                {
                    logic.StackPass(t, 0.1); // the regen pass's stack part (the game runs it every 0.1 s)
                    nextStackPass = t + 0.1;
                }
                // step 81: the men's own swings - a wind-up when his last move is over (with the stack on: not while it holds him at 0)
                for (int i = 0; i < n; i++)
                {
                    var m = men[i];
                    if (m.AiKickPhase != AiKickPhase.None || inAttempt[i]) continue;
                    if (swingPhase[i] == 0 && t >= nextReady[i])
                    {
                        if (stack)
                        {
                            int cap = SwingStackMath.Cap(SwingStackRules.From(S), ar, m);
                            int have = m.Swings.Count(cap);
                            if (have < 1) continue; // the stack's hold: no attack at 0
                            res.FewestAtWindUp = Math.Min(res.FewestAtWindUp, have);
                        }
                        logic.ObserveAction(m, ReadyMeleeAction, t, ar); // the phases queue the decision
                        swingPhase[i] = 1;
                        phaseEnd[i] = t + 0.5;
                    }
                    else if (swingPhase[i] == 1 && t >= phaseEnd[i])
                    {
                        logic.ObserveAction(m, (int)Agent.ActionCodeType.ReleaseMelee, t, ar);
                        res.Swings[shield[i] ? 0 : 1]++;
                        swingPhase[i] = 2;
                        phaseEnd[i] = t + 0.4;
                    }
                    else if (swingPhase[i] == 2 && t >= phaseEnd[i])
                    {
                        logic.ObserveAction(m, 35, t, ar);
                        swingPhase[i] = 0;
                        nextReady[i] = t + 1.0 + 0.6 * rnd.NextDouble();
                    }
                }
                logic.TickAiKicks(t);
                for (int i = 0; i < n; i++)
                {
                    var m = men[i];
                    if (m.AiKickPhase != AiKickPhase.None && !inAttempt[i])
                    {
                        // a rolled kick / bash began: its guard cancels his wind-up (the stand-in engine drops it)
                        inAttempt[i] = true;
                        if (swingPhase[i] == 1) logic.ObserveAction(m, 35, t, ar);
                        swingPhase[i] = 0;
                    }
                    else if (m.AiKickPhase == AiKickPhase.None && inAttempt[i])
                    {
                        inAttempt[i] = false;
                        nextReady[i] = t + 1.0 + 0.6 * rnd.NextDouble();
                    }
                }
                // the stand-in ENGINE: a kick / bash in its commit plays now, lands 0.25 s later on the first body along his facing then, over at 0.6 s
                for (int i = 0; i < n; i++)
                {
                    var m = men[i];
                    if (m.AiKickPhase == AiKickPhase.Commit && double.IsNaN(hitAt[i]) && double.IsNaN(endAt[i]))
                    {
                        if (AiKickMath.GuardUp(m.AiKickKind)) logic.ObserveAction(m, KickBashTracker.ActionWeaponBash, t, ar); // a shield bash / a weapon push (step 88): the engine's WeaponBash
                        else logic.ObserveLowerAction(m, KickBashTracker.ActionKick, t, ar);
                        hitAt[i] = t + 0.25;
                        endAt[i] = t + 0.6;
                    }
                    if (!double.IsNaN(hitAt[i]) && t >= hitAt[i])
                    {
                        hitAt[i] = double.NaN;
                        double sideLimit = AiKickMath.GuardUp(m.AiKickKind) ? engineBashSide : engineKickSide; // step 88: a push sweeps like a bash
                        int first = -1;
                        double fa = double.MaxValue;
                        for (int j = 0; j < n; j++)
                        {
                            if (j == i) continue;
                            double dx = x[j] - x[i], dy = y[j] - y[i];
                            double along = dx * fx[i] + dy * fy[i], across = Math.Abs(-dx * fy[i] + dy * fx[i]);
                            if (along > 0 && along <= engineReach && across <= sideLimit && along < fa)
                            {
                                fa = along;
                                first = j;
                            }
                        }
                        if (first >= 0) logic.AiKickHit(m, false, men[first].Agent);
                    }
                    if (!double.IsNaN(endAt[i]) && t >= endAt[i])
                    {
                        endAt[i] = double.NaN;
                        if (AiKickMath.GuardUp(m.AiKickKind)) logic.ObserveAction(m, 35, t, ar);
                        else logic.ObserveLowerAction(m, 35, t, ar);
                    }
                }
            }
            for (int s = 0; s < 3; s++) logic.TickAiKicks(t += 0.5);
            var st = logic.AiKickStats;
            res.Stats = st;
            for (int kd = 1; kd < AiKickMath.KindCount; kd++) // step 88: the pushes too
            {
                var kk = (AiKickKind)kd;
                res.Asked += st.AskedCount(kk);
                res.Played += st.PlayedCount(kk);
                res.EnemyHits += st.EnemyHits(kk);
                res.FriendHits += st.FriendHits(kk);
                res.Misses += st.OutcomeCount(kk, AiKickOutcome.Miss);
            }
            for (int kd = 1; kd < AiKickMath.KindCount; kd++)
                for (int p = 1; p < (int)AiKickPath.Count; p++) res.Refused += st.PathCount((AiKickKind)kd, (AiKickPath)p);
            res.PressBlocked = st.Ends[(int)AiKickEnd.PathBlocked];
            res.ContactMinutes = st.ContactManSeconds / 60.0;
            res.KicksAtEmpty = logic.StackStats.KicksAtEmpty;
            res.KicksSpent = logic.StackStats.KicksSpent;
            logic.WriteAiKickSummary();
            res.Summary = st.PathLine(AiKickRules.From(S));
            res.SharesLine = st.SharesLine(AiKickRules.From(S));
            return res;
        }
    }
}
