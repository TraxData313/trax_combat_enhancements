using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 66 - MORE AI KICKS AND BASHES, EACH COSTING MORE (AI_NOTES "Step 66"): a shieldman's swings earn his bashes
    /// (AiBashShareOfSwingsPercent), a decision with the enemy close waits for reach (AiKickWaitSeconds / AiKickWaitMetres), a kick costs 5 -
    /// through the REAL logic's look (the step-65 stand-in body answers the engine's reads). Whether the engine plays them is PLAYTEST D31's.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>
        /// The bash credit and the wait through the real look: nothing earned = no bash; a shield-infantry swing (NoteAiBashSwing at the release)
        /// earns one, the bash is asked with AiBashCooldownSeconds and the credit spent; a decision at 2.5 m waits and becomes a kick at 1.2 m; a
        /// wait that runs out; a wait dropped (the enemy gone); 6 m never waited for; the kick's price 5; the summary line.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiBashesAndTheWait()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.CostPerKickOrBash, SettingsSchema.CostPerKickOrBash.Default, SettingSources.File); // AthleticsDefaults pins step 18's 3
                Check(Math.Abs(S.CostPerKickOrBash - 5) < 1e-6 && S.AiBashShareOfSwingsPercent == 100 && Math.Abs(S.AiKickCooldownSeconds - 8) < 1e-6
                      && Math.Abs(S.AiBashCooldownSeconds - 2) < 1e-6 && Math.Abs(S.AiKickWaitSeconds - 3) < 1e-6,
                    "step 66's defaults: cost " + S.CostPerKickOrBash + ", share " + S.AiBashShareOfSwingsPercent + ", cooldowns " + S.AiKickCooldownSeconds + " / " + S.AiBashCooldownSeconds
                    + ", wait " + S.AiKickWaitSeconds);
                var body = new FakeAiKickBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.AiKickBody = body;
                logic.AiKickDice = new FixedDice(0.0);
                var st = logic.AiKickStats;
                double t = 66100;
                TrackedAgent Man(int index, int skill = 100)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = skill;
                    SetBar(m, 1.0);
                    m.PrevAction = m.PrevLowerAction = 35;
                    return m;
                }
                void Look(TrackedAgent m)
                {
                    m.AiKickLastLook = t - AiKickMath.LookSeconds;
                    logic.LookAiKick(m, t, AiKickRules.From(S), Rules);
                }
                int Skips(AiKickSkip s) => st.Skips[(int)s];

                // ---- 1. a shieldman: nothing earned = no bash (his die is the earned bash's, never the kick rate)
                var shieldman = Man(6610);
                body.Shield.Add(6610);
                body.Targets[6610] = (Man(6611), 1.2f, 0f);
                int dice = Skips(AiKickSkip.Dice);
                Look(shieldman);
                Check(shieldman.AiKickPhase == AiKickPhase.None && Skips(AiKickSkip.Dice) == dice + 1, "a shieldman with no bash earned was asked one");
                // ---- 2. his swing earns it (a shield-infantry release), the bash is asked, the credit spent, the bash's cooldown
                shieldman.AttackClass = AttackClass.ShieldInfantry;
                logic.NoteAiBashSwing(shieldman, mounted: false);
                Check(Math.Abs(shieldman.BashCredit - 1) < 1e-9 && st.ShieldSwings == 1, "a shield-infantry swing did not earn a bash: " + shieldman.BashCredit);
                logic.NoteAiBashSwing(shieldman, mounted: true); // a rider's swing earns nothing
                Check(st.ShieldSwings == 1, "a mounted swing earned a bash");
                Look(shieldman);
                Check(shieldman.AiKickPhase == AiKickPhase.Guard && shieldman.AiKickKind == AiKickKind.Bash && shieldman.BashCredit < 1e-9
                      && Math.Abs(shieldman.AiKickNext - (t + 2)) < 1e-6,
                    "the earned bash: phase " + shieldman.AiKickPhase + ", kind " + shieldman.AiKickKind + ", credit " + shieldman.BashCredit + ", next " + (shieldman.AiKickNext - t));
                var footman = Man(6612);
                footman.AttackClass = AttackClass.FootMelee;
                logic.NoteAiBashSwing(footman, mounted: false);
                Check(footman.BashCredit == 0 && st.ShieldSwings == 1, "a swing without a shield earned a bash");

                // ---- 3. the wait: the enemy at 2.5 m - decided, no attempt, then a kick the look he stands at 1.2 m
                var kicker = Man(6620);
                var foe = Man(6621);
                body.Targets[6620] = (foe, 2.5f, 0f);
                Look(kicker);
                Check(kicker.AiKickPhase == AiKickPhase.None && !double.IsNaN(kicker.AiKickWaitUntil) && st.WaitsStarted == 1 && Skips(AiKickSkip.Waiting) == 1,
                    "a decision at 2.5 m did not wait (phase " + kicker.AiKickPhase + ", waits " + st.WaitsStarted + ")");
                logic.AiKickDice = new FixedDice(0.999); // no die decides a waiting man
                t += 0.5;
                body.Targets[6620] = (foe, 2.0f, 70f);
                Look(kicker);
                Check(kicker.AiKickPhase == AiKickPhase.None && Skips(AiKickSkip.Waiting) == 2, "a waiting man kicked an enemy not in front");
                t += 0.5;
                body.Targets[6620] = (foe, 1.2f, 5f);
                Look(kicker);
                Check(kicker.AiKickPhase == AiKickPhase.Press && kicker.AiKickKind == AiKickKind.Kick && st.WaitsDone == 1 && double.IsNaN(kicker.AiKickWaitUntil)
                      && Math.Abs(kicker.AiKickNext - (t + 8)) < 1e-6,
                    "the waiting man did not kick once in reach (phase " + kicker.AiKickPhase + ", done " + st.WaitsDone + ")");
                Check(Math.Abs(st.WaitSeconds - 1.0) < 1e-6, "the wait's time: " + st.WaitSeconds);
                Check(Math.Abs(AthleticsMath.KickOrBashCostPoints(Rules, kicker) - 5) < 1e-6, "a common man's kick does not cost 5: " + AthleticsMath.KickOrBashCostPoints(Rules, kicker));

                // ---- 4. a wait that runs out, one dropped, an enemy too far to wait for
                logic.AiKickDice = new FixedDice(0.0);
                var slow = Man(6630);
                body.Targets[6630] = (Man(6631), 3.0f, 0f);
                Look(slow);
                Check(!double.IsNaN(slow.AiKickWaitUntil), "precondition: no wait at 3 m");
                t += 3.1;
                logic.AiKickDice = new FixedDice(0.999);
                Look(slow);
                Check(st.WaitsExpired == 1 && double.IsNaN(slow.AiKickWaitUntil) && slow.AiKickPhase == AiKickPhase.None, "a wait did not run out after 3 s");
                logic.AiKickDice = new FixedDice(0.0);
                var lost = Man(6632);
                body.Targets[6632] = (Man(6633), 2.0f, 0f);
                Look(lost);
                body.Targets.Remove(6632);
                t += 0.25;
                Look(lost);
                Check(st.WaitsDropped == 1 && double.IsNaN(lost.AiKickWaitUntil), "a wait with the enemy gone was not dropped");
                var far = Man(6634);
                body.Targets[6634] = (Man(6635), 6.0f, 0f);
                int tooFar = Skips(AiKickSkip.TooFar), started = st.WaitsStarted;
                Look(far);
                Check(Skips(AiKickSkip.TooFar) == tooFar + 1 && st.WaitsStarted == started, "an enemy at 6 m was waited for");

                // ---- 5. the summary line
                logic.WriteAiKickSummary();
                LogHas("[summary] ai kicks - bashes per swing and the wait for reach (step 66): the shieldmen's melee swings (AI, on foot) 1 earned 1.0 bashes (AiBashShareOfSwingsPercent 100%;");
                LogHas("; decisions that waited for reach 3 → kicked / bashed 1 (1.00 s after the decision), ran out (3.0 s) 1, dropped (the enemy gone or beyond 5.0 m, mounted, a ranged weapon, routing) 1");
                Console.WriteLine("        step 66: " + st.SummaryLines(AiKickRules.From(S)).ToArrayLast());
            }
            finally
            {
                Step65CleanUp(keep);
            }
        }

        /// <summary>
        /// A CLASH (the stand-in world, seeded, the real tick): 20 shieldmen in reach swinging 8 times a minute for 10 minutes - their bashes about
        /// one a swing; 20 kickers whose enemy stands 2.5 m off and steps into reach for one second in every four - the waits turn into kicks at
        /// about the kick rate; nothing stuck, 0 bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiBashesInAClash()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                var body = new FakeAiKickBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.AiKickBody = body;
                logic.AiKickDice = new SeededRandom(66);
                var swingDice = new SeededRandom(6601);
                var shieldmen = new List<TrackedAgent>();
                var kickers = new List<TrackedAgent>();
                for (int i = 0; i < 40; i++)
                {
                    var m = logic.Track(FakeAgent(6700 + i))!;
                    m.AthleticsSkill = 100;
                    m.PrevAction = m.PrevLowerAction = 35;
                    if (i < 20)
                    {
                        body.Shield.Add(6700 + i);
                        m.AttackClass = AttackClass.ShieldInfantry;
                        shieldmen.Add(m);
                    }
                    else
                    {
                        m.AttackClass = AttackClass.FootMelee;
                        kickers.Add(m);
                    }
                }
                for (int i = 0; i < 40; i++) body.Targets[6700 + i] = (i < 20 ? kickers[i] : shieldmen[i - 20], 1.2f, 10f);
                double t = 67000;
                int swings = 0;
                for (int k = 0; k < 600 * 30; k++)
                {
                    t += 1.0 / 30;
                    // the kickers' enemy in reach one second in four, 2.5 m off the rest
                    float d = (int)(t % 4) == 0 ? 1.2f : 2.5f;
                    for (int i = 0; i < 20; i++) body.Targets[6720 + i] = (shieldmen[i], d, 10f);
                    foreach (var m in shieldmen)
                    {
                        SetBar(m, 1.0);
                        if (m.AiKickPhase == AiKickPhase.None && swingDice.NextDouble() < 8.0 / 60 / 30) // 8 swings a minute while free
                        {
                            swings++;
                            logic.NoteAiBashSwing(m, mounted: false);
                        }
                    }
                    foreach (var m in kickers) SetBar(m, 1.0);
                    logic.TickAiKicks(t);
                }
                var st = logic.AiKickStats;
                int bashes = st.AskedCount(AiKickKind.Bash), kicks = st.AskedCount(AiKickKind.Kick);
                double perSwing = swings > 0 ? (double)bashes / swings : 0;
                var r = AiKickRules.From(S);
                double kickModel = 20 * AiKickMath.WithCooldown(AiKickMath.PerMinute(in r, 100, 1.0, false, 0), r.Cooldown);
                Console.WriteLine("        step 66 a clash: 20 shieldmen swung " + swings + " times in 10 minutes and were asked " + bashes + " bashes ("
                                  + perSwing.ToString("0.00", CultureInfo.InvariantCulture) + " a swing); 20 kickers " + (kicks / 10.0).ToString("0.0", CultureInfo.InvariantCulture)
                                  + " kicks a minute (the model's " + kickModel.ToString("0.0", CultureInfo.InvariantCulture) + " with the enemy always in reach) - waits "
                                  + st.WaitsStarted + " → " + st.WaitsDone + " kicked, " + st.WaitsExpired + " ran out, " + st.WaitsDropped + " dropped");
                Check(perSwing > 0.8 && perSwing < 1.05, "a clash: " + perSwing + " bashes a swing - not about one a swing");
                Check(kicks / 10.0 > 0.6 * kickModel && kicks / 10.0 < 1.2 * kickModel, "a clash: " + kicks / 10.0 + " kicks a minute against the model's " + kickModel);
                Check(st.WaitsDone > st.WaitsExpired, "a clash: most waits ran out (" + st.WaitsExpired + " of " + st.WaitsStarted + ")");
                Check(st.Skips[(int)AiKickSkip.PoolFull] == 0, "a clash: the attempt pool filled");
            }
            finally
            {
                Step65CleanUp(keep);
            }
        }

        private static string ToArrayLast(this IEnumerable<string> lines)
        {
            string last = string.Empty;
            foreach (var l in lines) last = l;
            return last;
        }
    }
}
