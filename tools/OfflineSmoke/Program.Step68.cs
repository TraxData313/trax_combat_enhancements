using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 68 - THE WALL PUSHES AS ONE + KICKS THROUGH THE IMPACT MODEL (Toni's 100 v 100 of 2026-10-19: wall men in contact more
    /// than 1 m ahead of their slots 15.4% under an ADVANCE - F1 then F4; 92 AI kicks / bashes landed, 33 decided; AI_NOTES "Step 68"). Through the
    /// real logic with the stand-in bodies: the paced frame for a held man of a moving order near the enemy (and none for a stop, the march, a slot
    /// within the step), rewritten after vanilla's update, given back on every let-go path, the switch and the master switch live; the walk back after
    /// his own kick; no kick through a friend and whom a kick landed on; every landed kick against the decisions; the cost. What only the game can
    /// tell: that the engine walks a held man to the paced frame and the line moves as one (PLAYTEST D33).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>A held man of a shield wall facing +y on his slot (0, 0) + <paramref name="ahead"/>; the game's order slot <paramref name="lead"/> m
        /// ahead; the enemy's front <paramref name="gap"/> m away.</summary>
        private static WallFacts AdvanceMan(BraceOrderKind order = BraceOrderKind.Advance, double lead = 6, double gap = 2, double ahead = 0.1)
        {
            var f = WallMan(ahead: ahead, order: order);
            f.HasOrderSlot = true;
            f.OrderX = 0;
            f.OrderY = lead;
            f.EnemyGap = gap;
            return f;
        }

        private static void Step68WallOn()
        {
            S.Set(SettingsSchema.ShieldWallHoldsSlots, true, SettingSources.File);
            S.Set(SettingsSchema.ShieldWallStepOutMetres, 1.0, SettingSources.File);
            S.Set(SettingsSchema.ShieldWallPullBackSeconds, 0.5, SettingSources.File);
            foreach (var p in new[] { SettingsSchema.ShieldWallAdvanceAsOne, SettingsSchema.ShieldWallAdvanceStepMetres, SettingsSchema.ShieldWallAdvanceNearMetres, SettingsSchema.ShieldWallKickReturnMetres })
                S.Set(p, p.Default, SettingSources.File);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallAdvancesAsOne()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                Step68WallOn();
                Check(S.ShieldWallAdvanceAsOne && Math.Abs(S.ShieldWallAdvanceStepMetres - 1.0) < 1e-6 && Math.Abs(S.ShieldWallAdvanceNearMetres - 10.0) < 1e-6,
                    "the advance as one is not on at 1 m / 10 m by default");
                var body = new FakeWallBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                var ps = logic.WallStats.Pace;
                LogHas("; step 68: ON (ShieldWallAdvanceAsOne) - a held shield wall / square / circle under a MOVING order (advance - yours or the AI's -, move, fall back, follow) within 10.0 m of the enemy's front walks as ONE line");
                double t = 16800;
                TrackedAgent Man(int i, WallFacts f)
                {
                    var m = logic.Track(FakeAgent(i))!;
                    body.Facts[i] = f;
                    body.Contact.Add(i);
                    return m;
                }
                Man(6800, AdvanceMan());                                          // yours: an advance, the order slot 6 m ahead at the enemy's line
                Man(6801, WallMan());                                             // a stop: the game's own
                Man(6802, AdvanceMan(gap: 25));                                   // far from the enemy: the game's march
                Man(6803, AdvanceMan(order: BraceOrderKind.AiAdvance, gap: 5));   // the AI's advance
                Man(6804, AdvanceMan(order: BraceOrderKind.Move, lead: 0.5));     // a move within the step: nothing to cap
                logic.TickWall(t); // the first call looks at everyone

                // ---- 1. the first look: the two advancing men near the enemy sent 1 m ahead of their slots, nobody else
                Check(logic.WalledNow == 5, "precondition: not every man held: " + logic.WalledNow);
                Check(body.Paced.TryGetValue(6800, out var p0) && Math.Abs(p0.X) < 1e-9 && Math.Abs(p0.Y - 1.0) < 1e-9 && body.Paced.ContainsKey(6803),
                    "the advancing men were not sent 1 m ahead of their slots: " + string.Join(", ", body.Paced.Keys));
                Check(!body.Paced.ContainsKey(6801) && !body.Paced.ContainsKey(6802) && !body.Paced.ContainsKey(6804), "a stop / a march / a slot within the step was paced");
                Check(ps.PacedWrites == 2 && ps.MenPaced == 2 && ps.PacedIn(WallOrderGroup.Advance) == 2 && ps.Skips(WallPaceSkip.Far) == 1 && ps.Skips(WallPaceSkip.WithinStep) == 1
                      && ps.Skips(WallPaceSkip.Standing) == 0,
                    "the first look's counts: paced " + ps.PacedWrites + " on " + ps.MenPaced + ", far " + ps.Skips(WallPaceSkip.Far) + ", within " + ps.Skips(WallPaceSkip.WithinStep));
                LogHas("[wall] first man of a moving wall sent AS ONE LINE (step 68): (agent 6800) at 16800.0 s - his formation in a shield wall under the order advance; the game's order slot lay 6.00 m from his slot in the line (the enemy's front 2.0 m away) - sent 1.00 m toward it instead");

                // ---- 2. vanilla's update on 6800 (his timer moved: the game wrote its own frame) - ours again in the very next frame
                int calls = body.PaceCalls;
                body.Stamps[6800] = 1f;
                WallFrames(logic, ref t, 1.0 / 60);
                Check(body.PaceCalls >= calls + 1, "vanilla's update on an advancing man was not followed by the paced frame");

                // ---- 3. the game's slot comes within the step: its own frame back; out again: paced again
                body.Facts[6800] = AdvanceMan(lead: 0.6);
                WallLook(logic, ref t);
                Check(!body.Paced.ContainsKey(6800) && ps.Restored >= 1 && body.UnpaceCalls >= 1, "the slot within the step: the game's frame not given back");
                body.Facts[6800] = AdvanceMan();
                WallLook(logic, ref t);
                Check(body.Paced.ContainsKey(6800), "out of the step again: not paced again");

                // ---- 4. a man stepping out on the leash keeps the paced frame (the game's would send him on into the enemy)
                var m5 = Man(6805, AdvanceMan());
                body.Enemies[6805] = (0, 1.5);
                WallLook(logic, ref t);
                var ws5 = m5.Wall!;
                Check(ws5.Leash.Free && body.Paced.ContainsKey(6805), "a man stepping out of an advancing wall: free " + ws5.Leash.Free + ", paced " + body.Paced.ContainsKey(6805));
                body.Enemies.Remove(6805);

                // ---- 5. a CHARGE: let go with vanilla's value and the game's own frame back
                int restored = ps.Restored;
                body.Facts[6800] = AdvanceMan(order: BraceOrderKind.Charge);
                WallLook(logic, ref t);
                Check(body.Released.Contains(6800) && !body.Paced.ContainsKey(6800) && ps.Restored == restored + 1, "a charge order: not let go with the game's own frame back");
                body.Facts[6800] = AdvanceMan();
                WallLook(logic, ref t);

                // ---- 6. the switch off LIVE: every paced man given the game's frame back at once; on: paced again at the next look
                Check(body.Paced.Count >= 2, "precondition: nobody paced before the switch");
                S.Set(SettingsSchema.ShieldWallAdvanceAsOne, false, SettingSources.Mcm);
                WallFrames(logic, ref t, 1.0 / 60);
                Check(body.Paced.Count == 0 && logic.WalledNow >= 5, "the switch off: paced frames left (" + body.Paced.Count + ") or the hold let go");
                LogHas("[wall] advance as one (step 68) changed mid-mission at ");
                LogHas(" men given the game's own frame back at once");
                int offCalls = body.PaceCalls;
                WallFrames(logic, ref t, 1.0);
                Check(body.PaceCalls == offCalls && ps.Skips(WallPaceSkip.Off) > 0, "the switch off: still paced, or the looks not filed under 'the pace off'");
                S.Set(SettingsSchema.ShieldWallAdvanceAsOne, true, SettingSources.Mcm);
                WallLook(logic, ref t);
                Check(body.Paced.ContainsKey(6800) && body.Paced.ContainsKey(6803), "the switch on: not paced again at the next look");

                // ---- 7. the step live: 2 m
                S.Set(SettingsSchema.ShieldWallAdvanceStepMetres, 2.0, SettingSources.Mcm);
                WallLook(logic, ref t);
                Check(Math.Abs(body.Paced[6803].Y - 2.0) < 1e-9, "the step 2 m live: " + body.Paced[6803].Y);
                S.Set(SettingsSchema.ShieldWallAdvanceStepMetres, 1.0, SettingSources.Mcm);

                // ---- 8. the samples ON by order group (all in contact)
                WallFrames(logic, ref t, 2);
                Check(ps.ByGroup[(int)WallOrderGroup.Advance].Contact > 0 && ps.ByGroup[(int)WallOrderGroup.Standing].Contact > 0 && ps.ByGroup[(int)WallOrderGroup.Moving].Contact > 0,
                    "the ON samples were not split by order: " + ps.ByOrderLine());

                // ---- 9. the summary
                logic.WriteWallSummary();
                LogHas("[summary] wall slots ON by order (step 68 - the in-contact samples, an enemy within 5 m): standing (stop / hold) ");
                LogHas("| ADVANCE (yours or the AI's) ");
                LogHas("[summary] wall advance as one (step 68): ON (ShieldWallAdvanceAsOne)");
                LogHas(" men (advance ");
                Check(ps.Errors == 0, "pace errors: " + ps.Errors);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                AthleticsDefaults();
                WallDefaults();
            }
        }

        /// <summary>Step 68 for the master-switch step: off = every paced man given the game's frame back at once (with step 41's release), nothing
        /// paced; on = paced again at the next look.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallPaceFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                Step68WallOn();
                var body = new FakeWallBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                logic.Track(FakeAgent(6890));
                body.Facts[6890] = AdvanceMan();
                double t = 16890;
                logic.TickWall(t);
                Check(body.Paced.ContainsKey(6890), "precondition: the advancing man not paced (step 68)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                WallFrames(logic, ref t, 1.0 / 60);
                Check(body.Paced.Count == 0 && logic.WalledNow == 0, "mod off: a paced frame left (step 68)");
                int calls = body.PaceCalls;
                WallFrames(logic, ref t, 1.0);
                Check(body.PaceCalls == calls, "mod off: still paced (step 68)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                WallLook(logic, ref t);
                Check(body.Paced.ContainsKey(6890), "mod back on: not paced again (step 68)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                WallDefaults();
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }

        /// <summary>Step 68's cost: a thousand men of an advancing wall in contact, each one's vanilla update every 0.5 s followed by the flag AND the
        /// paced frame - the pre-tick stays small and allocates nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallPaceCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                Step68WallOn();
                S.Set(SettingsSchema.ShieldWallStepOutMetres, 0.0, SettingSources.File);
                var body = new FakeWallBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                const int men = 1000;
                double t0 = 16900;
                for (int i = 0; i < men; i++)
                {
                    logic.Track(FakeAgent(7000 + i));
                    body.Facts[7000 + i] = AdvanceMan(ahead: (i % 5) * 0.2);
                    body.Stamps[7000 + i] = 0f;
                    if (i % 3 == 0) body.Contact.Add(7000 + i);
                }
                logic.TickWall(t0);
                const int ticks = 600;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = new Stopwatch();
                int calls = body.PaceCalls;
                for (int k = 1; k <= ticks; k++)
                {
                    for (int i = k % 30; i < men; i += 30) body.Stamps[7000 + i] = k;
                    sw.Start();
                    logic.TickWall(t0 + k / 60.0);
                    sw.Stop();
                }
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                int paced = body.PaceCalls - calls;
                Console.WriteLine("        step 68 cost: " + men + " men of an advancing wall paced - the pre-tick " + msPerTick.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                  + " ms a tick, " + allocated + " bytes allocated over " + ticks + " ticks, " + paced + " paced frames (" + (paced / (double)men / 10).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " a man a second)");
                Check(msPerTick < 0.5, "cost: the pre-tick with the pace over " + men + " men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 256 * 1024, "cost: the pace allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(paced >= men * 19, "cost: not a paced frame after each vanilla update (~20 a man in 10 s): " + paced);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                WallDefaults();
            }
        }

        /// <summary>Step 68: a held man's OWN kick carries him 0.8 m out (inside step 63's 1.25 m) - out of place after a kick, never walked while the
        /// kick plays, walked back at once when it is over (no 0.5 s wait), back within 0.25 m; a punish press the same; the setting 0 = step 63.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallWalksBackAfterAKick()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                Step68WallOn();
                Check(Math.Abs(S.ShieldWallKickReturnMetres - 0.5) < 1e-6, "the walk back after a kick is not 0.5 m by default");
                var body = new FakeWallBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                var pull = logic.WallStats.Pull;
                double t = 16850;
                var st = logic.Track(FakeAgent(6850))!;
                st.PrevAction = st.PrevLowerAction = 35;
                body.Facts[6850] = WallMan(ahead: 0.1);
                logic.TickWall(t);
                Check(st.Wall != null && st.Wall.Held, "precondition: the wall man not held");
                // his kick plays (step 18's poll) and carries him 0.8 m forward
                logic.ObserveLowerAction(st, KickBashTracker.ActionKick, t, Rules);
                body.Facts[6850] = WallMan(ahead: 0.8);
                WallFrames(logic, ref t, 0.5);
                Check(st.Wall.Out.Out && st.Wall.Out.AfterOwnKick && pull.SpellsAfterKick == 1, "0.8 m out after his own kick: not out of place after a kick");
                Check(st.Input == null || !st.Input.WallPull, "walked back while his kick plays");
                LogHas("[wall] first held man out of place after his OWN kick / bash or a step-in (step 68): (agent 6850) at ");
                logic.ObserveLowerAction(st, 35, t, Rules); // the kick is over
                WallFrames(logic, ref t, 0.15);
                Check(st.Input != null && st.Input.WallPull, "not walked back at once after his kick");
                body.Facts[6850] = WallMan(ahead: 0.2);
                WallFrames(logic, ref t, 0.15);
                Check(!st.Wall.Out.Out && !st.Input.WallPull && pull.BackAfterKick.Count == 1, "not back within 0.25 m / the wish still on");
                // a punish press (step 54's step-in) the same
                st.Punishing = true;
                body.Facts[6850] = WallMan(ahead: 0.8);
                WallFrames(logic, ref t, 0.5);
                Check(st.Wall.Out.Out && st.Wall.Out.AfterOwnKick && st.Input.WallPull, "0.8 m out pressing a punish window: not walked back at once");
                st.Punishing = false;
                body.Facts[6850] = WallMan(ahead: 0.1);
                WallFrames(logic, ref t, 0.3);
                // the setting 0: step 63 exactly - 0.8 m after a kick is nobody's business
                S.Set(SettingsSchema.ShieldWallKickReturnMetres, 0.0, SettingSources.Mcm);
                logic.ObserveLowerAction(st, KickBashTracker.ActionKick, t, Rules);
                body.Facts[6850] = WallMan(ahead: 0.8);
                WallFrames(logic, ref t, 0.5);
                LogHas("[wall] the walk back after a kick (step 68) changed mid-mission at ");
                logic.ObserveLowerAction(st, 35, t, Rules);
                WallFrames(logic, ref t, 0.5);
                Check(!st.Wall.Out.Out && pull.SpellsAfterKick == 2, "ShieldWallKickReturnMetres 0: 0.8 m after a kick still out of place");
                logic.WriteWallSummary();
                LogHas("; step 68 - after his OWN kick / bash or a punish press 2 (back in ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                WallDefaults();
            }
        }

        /// <summary>Step 68: a friend on the line to the enemy holds the AI's kick (the decision waits for a clear line), the line clear = kicked; the
        /// setting 0 = no check; whom a landed kick hit (a FRIEND, his target) in the "ai kicks" line.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiKickNotThroughAFriend()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.AiKickFriendClearMetres, SettingsSchema.AiKickFriendClearMetres.Default, SettingSources.File);
                Check(Math.Abs(S.AiKickFriendClearMetres - 0.5) < 1e-6, "AiKickFriendClearMetres is not 0.5 by default");
                var body = new FakeAiKickBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.AiKickBody = body;
                logic.AiKickDice = new FixedDice(0.0);
                var st = logic.AiKickStats;
                double t = 68000;
                TrackedAgent Man(int index)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 100;
                    SetBar(m, 1.0);
                    m.PrevAction = m.PrevLowerAction = 35;
                    return m;
                }
                void Look(TrackedAgent m)
                {
                    m.AiKickLastLook = t - AiKickMath.LookSeconds;
                    logic.LookAiKick(m, t, AiKickRules.From(S), Rules);
                }
                var rear = Man(6860);   // a rear-rank man
                var front = Man(6861);  // his front-rank mate
                var enemy = Man(6862);
                body.Friends.Add((6860, 6861));
                body.Targets[6860] = (enemy, 1.4f, 5f);
                body.FriendBlocks.Add(6860);

                // ---- 1. his mate on the line: held, the decision waits for a clear line
                Look(rear);
                Check(rear.AiKickPhase == AiKickPhase.None && st.Skips[(int)AiKickSkip.FriendInTheWay] == 1 && !double.IsNaN(rear.AiKickWaitUntil) && st.FriendChecksBlocked == 1,
                    "a kick through his mate was not held / not waiting: phase " + rear.AiKickPhase + ", skips " + st.Skips[(int)AiKickSkip.FriendInTheWay]);
                LogHas("[impact] first AI kick / bash HELD for a friend in the way at 68000.0 s (step 68) - (agent 6860) at (agent 6862) 1.40 m off: one of his own side stands within 0.50 m of the line between them (AiKickFriendClearMetres) - the decision waits for a clear line");
                t += 0.25;
                Look(rear);
                Check(rear.AiKickPhase == AiKickPhase.None && st.Skips[(int)AiKickSkip.FriendInTheWay] == 2, "still blocked: kicked anyway");

                // ---- 2. the line clear: the waiting decision kicks
                body.FriendBlocks.Remove(6860);
                t += 0.25;
                Look(rear);
                Check(rear.AiKickPhase != AiKickPhase.None && st.WaitsDone == 1 && st.FriendChecksClear >= 1, "the line clear: the waiting kick did not come (phase " + rear.AiKickPhase + ")");

                // ---- 3. whom it landed on: his mate (the check missed him - the engine's own geometry) = a FRIEND; another man's on his target
                logic.AiKickHit(rear, false, front.Agent);
                var other = Man(6863);
                body.Targets[6863] = (enemy, 1.0f, 0f);
                Look(other);
                Check(other.AiKickPhase != AiKickPhase.None, "precondition: the second kicker was not asked");
                logic.AiKickHit(other, false, enemy.Agent);
                Check(st.Victims[(int)AiKickVictim.Friend] == 1 && st.Victims[(int)AiKickVictim.Target] == 1, "whom the kicks hit: friend " + st.Victims[(int)AiKickVictim.Friend] + ", target " + st.Victims[(int)AiKickVictim.Target]);

                // ---- 4. the setting 0 live: no check at all
                S.Set(SettingsSchema.AiKickFriendClearMetres, 0.0, SettingSources.Mcm);
                var third = Man(6864);
                body.Targets[6864] = (enemy, 1.0f, 0f);
                body.FriendBlocks.Add(6864);
                int reads = body.FriendReads;
                Look(third);
                Check(third.AiKickPhase != AiKickPhase.None && body.FriendReads == reads, "AiKickFriendClearMetres 0: the check still ran / held the kick");
                S.Set(SettingsSchema.AiKickFriendClearMetres, 0.5, SettingSources.Mcm);

                logic.WriteAiKickSummary();
                LogHas(" → landed on the man 2 (step 68: on his target 1, on another enemy 0, on a FRIEND (the game cancels it - never decided) 1, on a horse / a man not tracked 0)");
                LogHas("a friend in the way (step 68) 2");
                LogHas("; step 68 - a friend on the line to the enemy (within 0.5 m): held 2, clear ");
            }
            finally
            {
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Step65CleanUp(keep);
            }
        }

        /// <summary>Step 68: every landed kick / bash against the decisions, through the real seams - one on an enemy (decided once), one on a FRIEND
        /// (named, not decided), one into a raised shield (counted apart); the [summary] line with nothing unaccounted.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void KicksLandedVsDecided()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step63On();
                AiKickDefaults();
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.MessageSink = new FakeMessageSink();
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var stats = logic.ImpactModelStats;
                double t = 68500;
                logic.TickImpact(t);
                TrackedAgent Man(int index)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 100;
                    SetBar(m, 1.0);
                    return m;
                }
                void Kick(TrackedAgent a, TrackedAgent v, AttackCollisionData cd)
                {
                    t += 0.05;
                    logic.TickImpact(t);
                    var blow = new Blow(a.AgentIndex);
                    model.DecideAgentKnockedBackByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                    model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                }
                var kicker = Man(6870);
                var enemy = Man(6871);
                var mate = Man(6872);
                int decided = stats.DecidedOf(ImpactEvent.Kick) + stats.DecidedOf(ImpactEvent.Bash);
                dice.Next(0.9);
                Kick(kicker, enemy, KickLands());                                     // on an enemy: decided once
                inputs.NotEnemies = true;
                Kick(kicker, mate, BashLands());                                       // on a FRIEND: named, not decided
                inputs.NotEnemies = false;
                Kick(kicker, enemy, Collision(true, true, false, CombatCollisionResult.Blocked, 3, 2)); // into a raised shield
                Check(stats.KickLanded[0] + stats.KickLanded[1] == 2 && stats.DecidedOf(ImpactEvent.Kick) + stats.DecidedOf(ImpactEvent.Bash) == decided + 1
                      && stats.KickSkipped[(int)ImpactSkip.NotEnemies] == 1 && stats.KickIntoShield[1] == 1,
                    "the landed kicks vs decided: landed " + (stats.KickLanded[0] + stats.KickLanded[1]) + ", decided " + (stats.DecidedOf(ImpactEvent.Kick) + stats.DecidedOf(ImpactEvent.Bash) - decided)
                    + ", friend " + stats.KickSkipped[(int)ImpactSkip.NotEnemies] + ", into a shield " + stats.KickIntoShield[1]);
                logic.WriteImpactSummary();
                LogHas("[summary] impact kicks / bashes landed vs decided (step 68): landed on a man 2 (kicks 1, bashes 1; by you 0) → DECIDED 1 (kicks 1, bashes 0); not decided: on a FRIEND (the game cancels it, nothing plays - step 63) 1");
                LogHas("; unaccounted 0 (every landed kick / bash on an enemy decided) | into a raised shield 1 (kicks 0, bashes 1)");
            }
            finally
            {
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                ImpactHooks.KnockBack = null;
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }
    }
}
