using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 81 - KICKS AND BASHES AS A SHARE OF THE MELEE MOVES (Toni 2026-10-22; AI_NOTES "Step 81"): the real decision point
    /// (a melee wind-up seen by step 13's phases, queued, rolled by the tick) in step 80's two grinding lines with the swing stack ON at its
    /// minimum 3 and the cost 8 - the measured shares per man type against 20 / 35 / 45 and 35 / 65, what the stack, the cooldown and the
    /// strike path take off them; a kick / bash never started at an empty stack.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>
        /// THE SHARES IN A GRIND: step 80's two two-deep lines (12 files a side, half shieldmen, shuffling and turning) for 3 minutes, every man
        /// swinging on his own rhythm, the swing stack on (min 3, 20 points a swing, kicks use swings), a kick / bash 8, a swing 10 - the shares
        /// ROLLED at the decision point near the targets; DONE = what the stack, the cooldown and the strike path leave; no kick / bash at 0.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SharesInAGrind()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                foreach (var p in new[]
                         {
                             SettingsSchema.AiKickFriendClearMetres, SettingsSchema.AiBashPathHalfWidthMetres, SettingsSchema.AiKickRearRankMetres, SettingsSchema.SwingStackEnabled,
                             SettingsSchema.SwingStackMinimum, SettingsSchema.SwingStackPointsPerSwing, SettingsSchema.KicksUseSwings, SettingsSchema.CostPerKickOrBash, SettingsSchema.CostPerBlow,
                         })
                    S.Set(p, p.Default, SettingSources.File);
                Check(S.SwingStackEnabled && S.SwingStackMinimum == 3 && S.KicksUseSwings && Math.Abs(S.CostPerKickOrBash - 8) < 1e-6 && Math.Abs(S.CostPerBlow - 10) < 1e-6
                      && Math.Abs(S.AiKickCooldownSeconds - 2) < 1e-6,
                    "the step-81 defaults are not the stack on at least 3 / kicks use swings / a kick 8, a swing 10 / 2 s between two: " + S.SwingStackMinimum + " / " + S.CostPerKickOrBash
                    + " / " + S.AiKickCooldownSeconds);
                var g = FightTwoLines(81, 8200, stack: true);
                var st = g.Stats;
                var r = AiKickRules.From(S);
                double Share(int type, AiKickKind k, bool done)
                {
                    int n = st.Moves(type);
                    return n > 0 ? (double)(done ? st.Done(type, k) : st.Rolled(type, k)) / n : 0;
                }
                string Pct(double v) => (100 * v).ToString("0.0", CultureInfo.InvariantCulture) + "%";
                Console.WriteLine("        step 81 two grinding lines, 3 minutes, the stack on (min 3) - " + g.SharesLine);
                Console.WriteLine("        step 81 swings released: shieldmen " + g.Swings[0] + ", no shield " + g.Swings[1] + "; kicks / bashes spent from the stack " + g.KicksSpent
                                  + ", at an empty stack " + g.KicksAtEmpty + "; the fewest stored at a wind-up " + g.FewestAtWindUp + "; " + g);
                Check(st.Moves(0) >= 300 && st.Moves(1) >= 300, "too few moves in kick reach to measure: shieldmen " + st.Moves(0) + ", no shield " + st.Moves(1));
                // the ROLL: near the targets (the dice alone)
                Check(Math.Abs(Share(0, AiKickKind.Kick, false) - 0.20) < 0.05 && Math.Abs(Share(0, AiKickKind.Bash, false) - 0.35) < 0.05 && Math.Abs(Share(0, AiKickKind.None, false) - 0.45) < 0.05,
                    "a shieldman's rolled shares are not near 20 / 35 / 45: " + Pct(Share(0, AiKickKind.Kick, false)) + " / " + Pct(Share(0, AiKickKind.Bash, false)) + " / " + Pct(Share(0, AiKickKind.None, false)));
                Check(Math.Abs(Share(1, AiKickKind.Kick, false) - 0.35) < 0.05 && st.Rolled(1, AiKickKind.Bash) == 0,
                    "a man without a shield's rolled shares are not near 35 / 65: " + Pct(Share(1, AiKickKind.Kick, false)) + ", bashes " + st.Rolled(1, AiKickKind.Bash));
                // DONE: never more than rolled - the limits only take away (a raised shield turns a bash into a kick: within those)
                for (int t = 0; t < 2; t++)
                {
                    Check(st.Done(t, AiKickKind.Kick) + st.Done(t, AiKickKind.Bash) <= st.Rolled(t, AiKickKind.Kick) + st.Rolled(t, AiKickKind.Bash), "more kicks / bashes done than rolled for " + AiKickStats.TypeName(t));
                    Check(st.Done(t, AiKickKind.None) >= st.Rolled(t, AiKickKind.None), "fewer swings done than rolled for " + AiKickStats.TypeName(t));
                }
                Check(st.Done(0, AiKickKind.Kick) + st.Done(0, AiKickKind.Bash) > 0.25 * st.Moves(0), "the limits took nearly every shieldman's kick / bash: " + g.SharesLine);
                Check(st.Done(1, AiKickKind.Kick) > 0.15 * st.Moves(1), "the limits took nearly every kick of the men without a shield: " + g.SharesLine);
                // the swing stack: kicks / bashes spend from it and never start at 0
                Check(g.KicksSpent > 0 && g.KicksAtEmpty == 0, "a kick / bash started at an empty stack (" + g.KicksAtEmpty + " of " + g.KicksSpent + ")");
                Check(st.Refused(0, 2) + st.Refused(1, 2) + st.Refused(0, 3) + st.Refused(1, 3) + st.Refused(0, 4) + st.Refused(1, 4) > 0, "nothing refused by the stack, the cooldown or the path in a grind");
                Check(g.FriendShare < 0.05, "the strike path: friends " + Pct(g.FriendShare) + " of the hits (step 80 - under 5%)");
                LogHas("[summary] ai kicks - shares of the melee moves in kick reach (step 81): shieldmen ");
                // the same lines TIRING (no refill at all): the cost and the stack take more of the shares as the bars empty
                var tired = FightTwoLines(81, 8300, stack: true, keepFresh: false);
                Console.WriteLine("        step 81 the same lines TIRING (no refill) - " + tired.SharesLine);
                Console.WriteLine("        step 81 tiring: swings released shieldmen " + tired.Swings[0] + ", no shield " + tired.Swings[1] + "; kicks / bashes at an empty stack " + tired.KicksAtEmpty);
                Check(tired.KicksAtEmpty == 0, "tiring: a kick / bash started at an empty stack");
                Check(tired.Stats.Refused(0, 1) + tired.Stats.Refused(1, 1) > 0, "tiring: no kick / bash refused for want of Athletics as the bars emptied");
                LogHas("[impact] mission start, the AI kicks and bashes (steps 65 / 81): ON - each melee move an AI man on foot is about to make (his wind-up)");
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
        /// THE STACK GATES THE KICK (step 81 on the new path): a man with the stack on at its minimum 3 - an empty man still holds 3; a rolled kick spends
        /// one when it plays (step 18's poll); at 0 stored the next rolled kick is REFUSED (he swings) and counted "stack empty"; one back = kicked.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TheStackGatesTheKick()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                foreach (var p in new[] { SettingsSchema.SwingStackEnabled, SettingsSchema.SwingStackMinimum, SettingsSchema.SwingStackPointsPerSwing, SettingsSchema.KicksUseSwings, SettingsSchema.CostPerKickOrBash })
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.AiKickCooldownSeconds, 0.0, SettingSources.File); // the stack alone here
                var body = new FakeAiKickBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.AiKickBody = body;
                logic.AiKickDice = new FixedDice(0.0);
                var st = logic.AiKickStats;
                var ar = Rules;
                var kicker = logic.Track(FakeAgent(8150))!;
                var enemy = logic.Track(FakeAgent(8151))!;
                kicker.AthleticsSkill = enemy.AthleticsSkill = 100;
                SetBar(kicker, 0.3); // 30% of his bar: round(points / 20) is below 3 - the minimum holds the cap at 3
                kicker.PrevAction = kicker.PrevLowerAction = enemy.PrevAction = enemy.PrevLowerAction = 35;
                body.Targets[8150] = (enemy, 1.0f, 0f);
                var sr = SwingStackRules.From(S);
                int cap = SwingStackMath.Cap(sr, ar, kicker);
                Check(cap == 3, "the swing stack's cap at 30% of a bar is not the minimum 3: " + cap);
                double t = 81500;
                logic.StackPass(t, 0.1);
                Check(kicker.Swings.Count(cap) == 3, "a fresh man's stack is not full at 3: " + kicker.Swings.Count(cap));
                // 1. a rolled kick spends one stored swing when the engine plays it (step 18's poll - ChargeKickOrBash)
                DecideAt(logic, kicker, t);
                Check(kicker.AiKickPhase != AiKickPhase.None, "a fresh man's rolled kick was not asked");
                logic.ObserveLowerAction(kicker, KickBashTracker.ActionKick, t, ar);
                Check(kicker.Swings.Count(cap) == 2 && logic.StackStats.KicksSpent == 1, "the played kick did not spend one stored swing: " + kicker.Swings.Count(cap) + " left");
                logic.ObserveLowerAction(kicker, 35, t, ar);
                foreach (double dt in new[] { 0.2, 0.35, 0.4, 0.45 }) logic.TickAiKicks(t + dt);
                Check(kicker.AiKickPhase == AiKickPhase.None, "the played kick did not end");
                // 2. the last two spent (swings): at 0 the next rolled kick is REFUSED - he swings - counted "stack empty"
                kicker.Swings.Spend(cap, out _);
                kicker.Swings.Spend(cap, out _);
                Check(kicker.Swings.Count(cap) == 0, "precondition: the stack not empty");
                kicker.PrevAction = 35;
                DecideAt(logic, kicker, t + 0.5);
                Check(kicker.AiKickPhase == AiKickPhase.None && st.Skips[(int)AiKickSkip.NoSwing] == 1 && st.Refused(1, 2) == 1 && st.Done(1, AiKickKind.None) == 1,
                    "a rolled kick at an empty stack was not refused as 'stack empty' (phase " + kicker.AiKickPhase + ", refused " + st.Skips[(int)AiKickSkip.NoSwing] + ")");
                Check(logic.StackStats.KicksAtEmpty == 0, "a kick started at an empty stack");
                int kicks = 1;
                Console.WriteLine("        step 81 the stack gates the kick: cap " + cap + " at 30% of a bar, a played kick spent 1 of 3, at 0 the rolled kick refused (stack empty - he swings), one back = kicked (" + kicks + " + 1)");
            }
            finally
            {
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Step65CleanUp(keep);
            }
        }
    }
}
