using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 50 - IMPACT I3, KICKS AND BASHES LIVE (AI_NOTES "Step 50"): the REAL damage decorator's KnockDown seam and
    /// the REAL <see cref="AthleticsLogic"/> with the stand-in body (<c>FakeImpactBody</c> - it plays the reactions the engine would: a Fall
    /// and its _continue, a StrikeKnockBack) - a kick on an exhausted man knocked down, his fall-and-rise slowed x2 and measured, the get-up
    /// written into his record (5 s immunity from it - a second kick refused); a bash on a shielded tired man: the shield dropped FROM THE
    /// TICK (never inside the callback), left his hand, picked up again (the pickup lock); his reel slowed and measured; you vs a fresh king
    /// (nothing - the YOU line); a rider (nothing applied); shadow mode = nothing applied; a failing lever disabled alone; courage's shock; the
    /// summary - and the cost.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Plays one reaction on the stand-in body: its type on channel 0 from <paramref name="from"/> for <paramref name="seconds"/>
        /// (a _continue at <paramref name="continueAt"/> - a new action index of the same type), then idle; ticks the logic every 1/60 s.
        /// Returns the time after.</summary>
        private static double PlayReaction(AthleticsLogic logic, FakeImpactBody body, TrackedAgent st, int type, double from, double seconds, double continueAt = double.NaN, int index = 900)
        {
            double t = from;
            double end = from + seconds;
            while (t < end + 0.1)
            {
                int i = !double.IsNaN(continueAt) && t >= from + continueAt ? index + 1 : index;
                body.Actions[st.AgentIndex] = t < end ? new ActionRead(type, i, 35, 200) : new ActionRead(35, 100, 35, 200);
                logic.TickImpact(t);
                t += 1.0 / 60;
            }
            body.Actions.Remove(st.AgentIndex);
            return t;
        }

        private static void TickFor(AthleticsLogic logic, ref double t, double seconds)
        {
            double end = t + seconds;
            while (t < end)
            {
                logic.TickImpact(t);
                t += 1.0 / 60;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactKicksLiveThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                S.Set(SettingsSchema.AthleticsPoolPerSkill, 0.8, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var live = logic.ImpactLiveStats;
                var model = SpikeDamageModel();
                double t = 30000;
                logic.TickImpact(t);
                LogHas("LIVE - kicks / bashes, blocks, clean hits, the riposte applied"); // step 52: every family live

                TrackedAgent Man(int index, int athletics, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = athletics;
                    SetBar(m, bar);
                    return m;
                }
                bool Kick(TrackedAgent a, TrackedAgent v, bool bash = false)
                {
                    var cd = bash ? BashLands() : KickLands();
                    var blow = new Blow(a.AgentIndex);
                    bool down = model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow); // the engine's question, then
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);                                        // its OnMeleeHit
                    return down;
                }

                // ---- 1. a KICK on an EXHAUSTED man with a shield: knocked down (the decorator's answer), the shield queued, his fall-and-rise x2
                var kicker = Man(5000, 125, 1.0);
                var tired = Man(5001, 125, 0.0);
                inputs.Shields.Add(5001);
                body.ShieldInHand[5001] = 3;
                dice.Next(0.0, 0.0, 0.5); // down, shield, the get-up roll at its centre (T 1: x2.00)
                bool down = Kick(kicker, tired);
                var d = logic.LastImpact;
                Check(down && d.Knockdown.Happens && d.ShieldDrop.Happens && Math.Abs(d.GetUpFactor - 2.0) < 1e-9,
                    "the kick on the exhausted man: KnockDown " + down + ", shield " + d.ShieldDrop.Happens + ", get-up x" + d.GetUpFactor);
                Check(body.Drops.Count == 0 && logic.ImpactLiveDrops == 1, "the shield was dropped INSIDE the hit callback (or not queued): drops " + body.Drops.Count + ", queued " + logic.ImpactLiveDrops);
                Check(tired.Impact.Down(t + 0.5) && tired.Impact.Recent(t, 6) == 2, "the knockdown + the drop not on his record");
                LogHas("[impact] first kick decided at 30000.0 s - (agent 5000)'s kick on (agent 5001) (LIVE)");
                // the next tick: the drop made from the tick (his slot 3), the engine plays the Fall (+ its _continue = the rise)
                double fallAt = t + 1.0 / 60;
                logic.TickImpact(fallAt);
                Check(body.Drops.Count == 1 && body.Drops[0] == (5001, 3) && logic.ImpactLiveDrops == 0 && live.DropsQueued == 1, "the shield drop was not made from the tick");
                body.SlotEmptied.Add(5001); // the engine's queued DropItem ran: the slot is empty from here
                LogHas("[impact] live: first shield drop made from the tick at 30000.0 s - (agent 5001)'s shield in slot 3");
                int slowsBefore = body.Slows.Count;
                double after = PlayReaction(logic, body, tired, ImpactActions.Fall, fallAt + 1.0 / 60, 3.0, continueAt: 1.0);
                var slows = body.Slows.Skip(slowsBefore).Where(x => x.Man == 5001).ToList();
                Check(slows.Count == 2 && slows.All(x => x.Channel == 0 && Math.Abs(x.Speed - 0.5f) < 1e-6), "the fall-and-rise was not slowed to 0.5 the tick it showed and again on its _continue: " + string.Join(", ", slows));
                Check(live.GetUpMeasured.Count == 1 && Math.Abs(live.GetUpMeasured.Mean - 3.0) < 0.05 && Math.Abs(live.GetUpAsked.Mean - 2.0) < 1e-9 && live.Seen[0] == 1,
                    "the get-up not measured: " + live.GetUpMeasured.Count + " / " + live.GetUpMeasured.Mean);
                double up = after - 0.1;
                Check(!tired.Impact.Down(up + 0.05) && tired.Impact.KnockdownImmune(up + 4.8) && !tired.Impact.KnockdownImmune(up + 5.2),
                    "the measured get-up was not written into his record (down " + tired.Impact.DownUntil + ", immune " + tired.Impact.KnockdownImmuneUntil + ", up ~" + up + ")");
                LogHas("[impact] live: first knockdown seen at ");
                LogHas("[impact] live: first fall-and-rise slowed at ");
                LogHas("[impact] live: first get-up measured at ");
                // the shield left his hand at a look during the fall; the pickup lock (AI) while ShieldPickupLockSeconds runs; picked up again
                t = after;
                TickFor(logic, ref t, 0.6);
                Check(live.Dropped == 1 && !double.IsNaN(tired.ShieldDroppedAt), "the shield never left his hand in the count");
                LogHas("[impact] live: first shield left the hand at ");
                Check(!logic.ShieldPickupLocked(tired, t), "ShieldPickupLockSeconds 0 locked a pickup (vanilla = never)");
                S.Set(SettingsSchema.ShieldPickupLockSeconds, 15, SettingSources.Mcm);
                Check(logic.ShieldPickupLocked(tired, t) && ImpactHooks.ShieldPickupLocked(tired.Agent), "the pickup lock did not hold within 15 s (logic / the decorator's hook)");
                Check(!logic.ShieldPickupLocked(tired, tired.ShieldDroppedAt + 15.1), "the pickup lock outlived its 15 s");
                S.Set(SettingsSchema.ShieldPickupLockSeconds, 0, SettingSources.Mcm);
                body.ShieldAgain.Add(5001);
                TickFor(logic, ref t, 0.6);
                Check(live.PickedUp == 1 && live.PickupSeconds.Count == 1, "the shield picked up again was not counted");
                inputs.Shields.Remove(5001); // (the stand-in's inputs: no shield in his hand for the next kick)

                // ---- 2. immunity: a second kick within 5 s of getting up - refused, nothing answered (the reel's roll at its centre)
                dice.Next(0.5);
                down = Kick(kicker, tired);
                d = logic.LastImpact;
                Check(!down && d.Knockdown.Refusal == ImpactRefusal.KnockdownImmune && live.RefusedKnockdownImmune == 1, "a kick within the get-up immunity knocked him down: " + d.Knockdown.Refusal);
                // its reel (no knockdown) is watched: the StrikeKnockBack slowed by the reel factor, measured, written as his reel's end
                double reelFactor = d.ReelFactor;
                Check(Math.Abs(reelFactor - 2.5) < 1e-9, "the exhausted man's reel factor (x2.5 at the roll's centre): x" + reelFactor);
                slowsBefore = body.Slows.Count;
                double reelEnd = PlayReaction(logic, body, tired, ImpactActions.StrikeKnockBack, t + 1.0 / 60, 0.42 * reelFactor, index: 950);
                slows = body.Slows.Skip(slowsBefore).Where(x => x.Man == 5001).ToList();
                Check(slows.Count == 1 && Math.Abs(slows[0].Speed - (float)(1 / reelFactor)) < 1e-4, "the reel was not slowed to 1 / x" + reelFactor + ": " + string.Join(", ", slows));
                Check(live.ReelMeasured.Count == 1 && Math.Abs(live.ReelMeasured.Mean - 0.42 * reelFactor) < 0.04, "the reel not measured: " + live.ReelMeasured.Mean);
                Check(!tired.Impact.Reeling(reelEnd) && tired.Impact.StaggerImmune(reelEnd + 1.5) && !tired.Impact.StaggerImmune(reelEnd + 2.1),
                    "the reel's measured end not in his record (reeling " + tired.Impact.ReelingUntil + ", immune " + tired.Impact.StaggerImmuneUntil + ")");
                LogHas("[impact] live: first slowed reel measured at ");
                t = reelEnd;

                // ---- 3. a BASH on a SHIELDED TIRED man (bar 20%): the shield dropped from the tick, the reel slowed and measured
                var basher = Man(5010, 125, 1.0);
                inputs.Weapons[5010] = (4.0, 0, ImpactDamageType.Blunt, 100);
                var turtle = Man(5011, 125, 0.2);
                inputs.Shields.Add(5011);
                body.ShieldInHand[5011] = 2;
                dice.Next(0.999, 0.0, 0.5); // no knockdown, the shield, the reel roll at its centre
                down = Kick(basher, turtle, bash: true);
                d = logic.LastImpact;
                Check(!down && d.Event == ImpactEvent.Bash && d.ShieldDrop.Happens && d.ReelFactor > 1, "the bash on the tired turtle: " + d.Event + ", shield " + d.ShieldDrop.Happens + ", reel x" + d.ReelFactor);
                // he is in his own bash at the first tick: the drop waits (vanilla's rule), then goes
                body.Actions[5011] = new ActionRead(35, 100, ImpactActions.WeaponBash, 300);
                logic.TickImpact(t += 1.0 / 60);
                Check(logic.ImpactLiveDrops == 1 && !body.Drops.Contains((5011, 2)), "the shield dropped during his own bash");
                body.Actions.Remove(5011);
                double bashReelStart = t + 1.0 / 60;
                slowsBefore = body.Slows.Count;
                t = PlayReaction(logic, body, turtle, ImpactActions.StrikeKnockBack, bashReelStart, 0.42 * d.ReelFactor, index: 980);
                Check(body.Drops.Contains((5011, 2)), "the bash's shield drop never made from the tick");
                Check(live.ReelMeasured.Count == 2 && body.Slows.Skip(slowsBefore).Any(x => x.Man == 5011), "the bash's reel not slowed / measured");

                // ---- 4. YOU vs a FRESH KING: your kick does nothing (x below 0), the YOU line says why
                var you = Man(5020, 100, 1.0);
                logic.SmokePlayer = you.Agent;
                var king = Man(5021, 275, 1.0); // a 220-point pool, fresh
                inputs.Shields.Add(5021);
                down = Kick(you, king);
                d = logic.LastImpact;
                Check(!down && d.Knockdown.Chance == 0 && d.ShieldDrop.Chance == 0 && d.X <= 0 && d.AttackerIsPlayer, "your kick on a fresh king did something: x " + d.X);
                LogHas("[impact] YOU: your kick on (agent 5021) (his bar 100%, x ");
                LogHas("): no knockdown (0%), shield kept (0%)");
                // ... and on him exhausted: down
                SetBar(king, 0.05);
                dice.Next(0.0, 0.999, 0.5);
                down = Kick(you, king);
                Check(down && logic.LastImpact.Knockdown.Happens, "your kick on an exhausted king did not knock him down");
                LogHas("[impact] YOU: your kick on (agent 5021) (his bar 5%, x ");
                LogHas("): knocked down (80%) - gets up x");
                logic.SmokePlayer = null;

                // ---- 5. a RIDER: step 53 - the KnockDown seam never answers a rider (the engine asks his dismount instead - Program.ImpactRiders.cs),
                // nothing of the kicks' own path (no reel watched, no KnockDown), counted in the riders' stats
                var rider = Man(5030, 125, 0.0);
                inputs.Mounted.Add(5030);
                dice.Next(0.0, 0.999, 0.5);
                int watchesBefore = logic.ImpactLiveWatches;
                down = Kick(kicker, rider);
                Check(!down && logic.ImpactLiveWatches == watchesBefore && live.NotApplied[0, (int)ImpactNotApplied.Rider] == 0 && logic.ImpactRiderStats.Decisions[0] == 1
                      && logic.ImpactRiderStats.DismountNotApplied[(int)ImpactNotApplied.OutsideSeam] == 1,
                    "a kick on a rider through the KnockDown seam: answered, watched or not counted as a rider's (step 53)");

                // ---- 6. courage's SHOCK: a live outcome counts (the kicks apply); step 51: a live block's outcome counts too - ONCE each
                var impactLive = (bool)typeof(AthleticsLogic).GetMethod("ImpactLive", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
                Check(impactLive && tired.Impact.Recent(30001, 6) >= 1, "courage does not count the live kicks' outcomes as shock");
                var blocker = Man(5040, 125, 0.0);
                inputs.Shields.Add(5040);
                inputs.Guards[5040] = (4.0, 100);
                dice.Next(0.999, 0.0, 0.0, 0.5); // no knockdown, the shield, the stagger, its length
                logic.ImpactModelHitTaken(kicker.Agent, blocker.Agent, WrongBlock());
                Check(logic.LastImpact.ShieldDrop.Happens && logic.LastImpact.Stagger.Happens && blocker.Impact.Recent(t, 6) == 2 && blocker.Impact.Reeling(t + 0.1),
                    "a live wrong-side block's two outcomes are not on his record once each (shock " + blocker.Impact.Recent(t, 6) + ")");
                logic.TickImpact(t += 1.0 / 60); // the stagger and the drop made from the tick (step 51's checks are Program.ImpactBlocks.cs)

                // ---- 7. SHADOW mode: decided and logged, nothing applied, nothing queued or watched
                S.Set(SettingsSchema.ImpactShadowMode, true, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);
                var shadowed = Man(5050, 125, 0.0);
                inputs.Shields.Add(5050);
                body.ShieldInHand[5050] = 3;
                int watches = logic.ImpactLiveWatches;
                dice.Next(0.0, 0.0, 0.5);
                down = Kick(kicker, shadowed);
                Check(!down && logic.LastImpact.Knockdown.Happens && logic.ImpactLiveDrops == 0 && logic.ImpactLiveWatches == watches, "shadow mode applied a kick");
                S.Set(SettingsSchema.ImpactShadowMode, false, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);

                // ---- 8. a failing lever: SetCurrentActionSpeed throws - that lever off, the knockdown still answered and watched
                body.ThrowOnSlow = true;
                var victim = Man(5060, 125, 0.0);
                dice.Next(0.0, 0.999, 0.5);
                down = Kick(kicker, victim);
                t = PlayReaction(logic, body, victim, ImpactActions.Fall, t + 1.0 / 60, 1.5, index: 1200);
                body.ThrowOnSlow = false;
                Check(down && live.Disabled(ImpactLever.SlowReaction) && !live.Disabled(ImpactLever.KnockDown) && live.GetUpControl.Count == 1,
                    "a throwing slow did not disable that lever alone (the fall measured untouched)");
                LogHas("smoke: SetCurrentActionSpeed failed");
                LogHas("[impact] live lever the slowed reel / get-up disabled for this mission after an error at impact.live-slow");
                var victim2 = Man(5061, 125, 0.0);
                dice.Next(0.0, 0.999, 0.5);
                Check(Kick(kicker, victim2), "after the slow lever failed the knockdown is no longer answered");
                // the drop throws: that lever off; the next shield drop decided is not applied (lever off), the knockdown still answered
                body.ThrowOnDrop = true;
                var victim3 = Man(5062, 125, 0.0);
                inputs.Shields.Add(5062);
                body.ShieldInHand[5062] = 3;
                dice.Next(0.999, 0.0, 0.5);
                Kick(kicker, victim3);
                logic.TickImpact(t += 1.0 / 60);
                body.ThrowOnDrop = false;
                Check(live.Disabled(ImpactLever.ShieldDrop), "a throwing drop did not disable the drop lever");
                var victim4 = Man(5063, 125, 0.0);
                inputs.Shields.Add(5063);
                body.ShieldInHand[5063] = 3;
                dice.Next(0.0, 0.0, 0.5);
                Check(Kick(kicker, victim4) && logic.ImpactLiveDrops == 0 && live.NotApplied[1, (int)ImpactNotApplied.LeverOff] >= 1, "the drop lever off: the knockdown not answered or a drop still queued");

                // ---- 9. the master switch: off = nothing answered, every watch / drop let go at once
                var victim5 = Man(5070, 125, 0.0);
                dice.Next(0.0, 0.999, 0.5);
                Kick(kicker, victim5);
                Check(logic.ImpactLiveWatches >= 1, "no watch to let go");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);
                Check(logic.ImpactLiveWatches == 0 && logic.ImpactLiveDrops == 0, "mod off: the live watches / drops were not let go");
                LogHas("[impact] kicks no longer live at ");
                dice.Next(0.0, 0.0, 0.5);
                Check(!Kick(kicker, Man(5071, 125, 0.0)), "mod off: a kick was still knocked down");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);
                dice.Next(0.0, 0.999, 0.5);
                Check(Kick(kicker, Man(5072, 125, 0.0)), "mod back on: the kick's knockdown not answered");

                // ---- the summary
                logic.WriteImpactSummary();
                LogHas("[summary] impact live (step 50 - kicks and bashes; LIVE - kicks / bashes, blocks, clean hits, the riposte applied");
                LogHas("knocked down decided ");
                LogHas("[summary] impact live lengths: the reel asked x");
                LogHas("[summary] impact live shields: drops queued from the tick ");
                LogHas("[summary] impact live limits: knockdown refused - on the ground ");
                LogHas("levers disabled the shield drop (at impact.live-drop), the slowed reel / get-up (at impact.live-slow)");
                LogHas("[summary] impact kicks by the kicked man's bar 100-75% / 75-50% / 50-25% / 25-0%: ");
                LogHas("did knock down");
                Console.WriteLine("        step 50: get-up asked x" + live.GetUpAsked.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " → measured "
                                  + live.GetUpMeasured.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s (untouched " + live.GetUpControl.Mean.ToString("0.00", CultureInfo.InvariantCulture)
                                  + " s); reels asked x" + live.ReelAsked.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " (max x" + live.ReelAskedMax.ToString("0.00", CultureInfo.InvariantCulture)
                                  + ") → measured " + live.ReelMeasured.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s (max " + live.ReelMeasuredMax.ToString("0.00", CultureInfo.InvariantCulture) + " s)");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: a thousand men at every bar, twenty LIVE kicks a tick through the decorator's seam (the watch pool and the drop
        /// queue busy all the time, the stand-in's reactions never showing - every watch runs to its window) - small and allocating nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactKicksLiveCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(50);
                var model = SpikeDamageModel();
                const int men = 1000;
                var all = new TrackedAgent[men];
                for (int i = 0; i < men; i++)
                {
                    var m = logic.Track(FakeAgent(9000 + i))!;
                    m.AthleticsSkill = 50 + (i % 200);
                    SetBar(m, (i % 101) / 100.0);
                    if (i % 2 == 0)
                    {
                        inputs.Shields.Add(9000 + i);
                        body.ShieldInHand[9000 + i] = 3;
                    }
                    all[i] = m;
                }
                var kick = KickLands();
                int k = 0;
                void Frame(int f)
                {
                    double t = 40000 + f / 60.0;
                    logic.TickImpact(t);
                    for (int e = 0; e < 20; e++, k++)
                    {
                        var a = all[(k * 7) % men];
                        var v = all[(k * 13 + 1) % men];
                        var blow = new Blow(a.AgentIndex);
                        model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in kick, null!, in blow);
                        logic.ImpactModelHitTaken(a.Agent, v.Agent, in kick);
                    }
                }
                for (int f = 0; f < 600; f++) Frame(f); // warm-up: every first line written
                const int ticks = 1200;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int f = 600; f < 600 + ticks; f++) Frame(f);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double ms = sw.Elapsed.TotalMilliseconds / ticks;
                var live = logic.ImpactLiveStats;
                Console.WriteLine("        step 50 cost: " + men + " men, 20 live kicks a tick - " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, "
                                  + allocated + " bytes over " + ticks + " ticks; " + live.LiveDecisions + " live decisions, knocked down " + live.AppliedOf(ImpactOutcome.Knockdown)
                                  + ", shields dropped " + live.DropsQueued + ", watches full " + live.WatchesFull + ", live tick " + (live.TickMs / Math.Max(1, live.Ticks)).ToString("0.000", CultureInfo.InvariantCulture) + " ms");
                Check(ms < 0.5, "cost: 20 live kicks a tick over " + men + " men took " + ms + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the live kicks allocate - " + allocated + " bytes over " + ticks + " ticks");
                Check(live.AppliedOf(ImpactOutcome.Knockdown) > 100 && live.DropsQueued > 100, "cost: too few live outcomes - " + live.AppliedOf(ImpactOutcome.Knockdown) + " / " + live.DropsQueued);
                logic.WriteImpactSummary();
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }
    }
}
