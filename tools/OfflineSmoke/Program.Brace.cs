using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 23 - BRACE BY ORDERS (AI_NOTES "Step 23"): the REAL <see cref="AthleticsLogic"/> with a stand-in
    /// for the brace's engine side (<see cref="IBraceBody"/>: the order, the hands, the wield, the hook). What runs for real: the
    /// staggered look at every man, the band by his formation's order (and his own margin, and his wound cap), entering and
    /// leaving, every end path, the input frame the component writes (melee attacks out, a guard, the shield up, ranged let
    /// through), the brace surviving the AI timer's end and the timer surviving the brace's, the shield steps, the logs and the
    /// summary - and the cost of looking at a thousand men. What only the game can tell: that the order read is the one you
    /// gave, that the engine takes the shield out and the AI keeps it (PLAYTEST, the [summary] "brace" lines).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for the brace's engine side: an order and hands per agent index, the wield calls
        /// recorded (a wield of the shield slot puts it in his other hand; of a one-hander, frees his hands for it).</summary>
        private sealed class FakeBraceBody : IBraceBody
        {
            public readonly Dictionary<int, BraceOrderKind> Orders = new Dictionary<int, BraceOrderKind>();
            public readonly Dictionary<int, HandFacts> Hands = new Dictionary<int, HandFacts>();
            public readonly HashSet<int> NotAi = new HashSet<int>();
            public readonly HashSet<int> Gone = new HashSet<int>();
            public readonly List<(int, int)> Wields = new List<(int, int)>();
            public readonly List<int> Hooked = new List<int>();
            public readonly List<int> Released = new List<int>();
            public bool WieldTakes = true;

            public bool IsActive(TrackedAgent st) => !Gone.Contains(st.AgentIndex);

            public bool IsAiControlled(TrackedAgent st) => !NotAi.Contains(st.AgentIndex);

            public BraceOrderKind ReadOrder(TrackedAgent st) => Orders.TryGetValue(st.AgentIndex, out var k) ? k : BraceOrderKind.Stop;

            public HandFacts ReadHands(TrackedAgent st) => Hands.TryGetValue(st.AgentIndex, out var h) ? h : HandFacts.None;

            public void Wield(TrackedAgent st, int slot)
            {
                Wields.Add((st.AgentIndex, slot));
                if (!WieldTakes || !Hands.TryGetValue(st.AgentIndex, out var h)) return;
                if (slot == h.ShieldSlot) h.ShieldInOffHand = true;
                else if (slot == h.OneHandedSlot) h.MainNeedsBothHands = false;
                Hands[st.AgentIndex] = h;
            }

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                Hooked.Add(st.AgentIndex);
                return AiInputHook.HookResult.FirstHookTurnedOn;
            }

            public void Release(TrackedAgent st) => Released.Add(st.AgentIndex);
        }

        /// <summary>Step 23: every logic built from here on reads through a stand-in brace body (the game's calls the engine).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeBraceBody()
        {
            Check(BraceBodyDefault.Current is GameBraceBody, "the default brace body is not the game's: " + BraceBodyDefault.Current.GetType().Name);
            BraceBodyDefault.Current = new FakeBraceBody();
            Check(new AthleticsLogic().BraceBody == BraceBodyDefault.Current, "a new logic does not start with the default brace body");
        }

        /// <summary>DESIGN's initial brace numbers (tuning defaults.json never breaks the checks).</summary>
        private static void BraceDefaults(int spread = 0)
        {
            S.Set(SettingsSchema.BraceEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.BraceFloorChargePercent, 20, SettingSources.File);
            S.Set(SettingsSchema.BraceFloorAdvancePercent, 40, SettingSources.File);
            S.Set(SettingsSchema.BraceFloorHoldPercent, 60, SettingSources.File);
            S.Set(SettingsSchema.BraceRecoverPercent, 20, SettingSources.File);
            S.Set(SettingsSchema.BraceRecoverSpreadPercent, spread, SettingSources.File);
            S.Set(SettingsSchema.BraceWieldShield, true, SettingSources.File);
            S.Set(SettingsSchema.BraceRaiseShield, true, SettingSources.File);
        }

        /// <summary>A 100-skill soldier: a pool of 100 points, a blow (10) = 10% of his bar.</summary>
        private static TrackedAgent Soldier(AthleticsLogic logic, FakeBraceBody body, int index, BraceOrderKind order)
        {
            var st = logic.Track(FakeAgent(index))!;
            st.AthleticsSkill = 100;
            body.Orders[index] = order;
            return st;
        }

        private static void Blows(TrackedAgent st, int n, double t)
        {
            var r = Rules;
            for (int i = 0; i < n; i++) AthleticsMath.Charge(st, in r, t);
        }

        /// <summary>Refills him in a straight line (RegenRateNearFullPercent 100: 1/60 of the bar a second at a walk) to
        /// <paramref name="to"/>, after the refill delay.</summary>
        private static void RefillTo(TrackedAgent st, double to, ref double t)
        {
            var r = Rules;
            double need = (to - st.Fraction) * r.FullRegenSecondsStanding;
            if (need <= 0) return;
            double start = Math.Max(t, st.LastBlowTime + r.RegenDelaySeconds);
            t = start + need;
            AthleticsMath.Regen(st, in r, t, need, 0f, 1f);
        }

        /// <summary>One look at everyone (the brace's tick after a gap of a poll or more).</summary>
        private static void Look(AthleticsLogic logic, ref double t)
        {
            t += 0.3;
            logic.TickBrace(t);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BraceThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                StepBackDefaults();
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.AttackRatePaceByInput, true, SettingSources.File);
                S.Set(SettingsSchema.RegenRateNearFullPercent, 100, SettingSources.File); // a straight refill: easy numbers
                BraceDefaults();
                var body = new FakeBraceBody();
                var pace = new FakePaceBody();
                var logic = NewPaceLogic(pace, new FakeWeaponFacts());
                logic.BraceBody = body;
                logic.BraceDice = new SeededRandom(23);
                var bs = logic.BraceStats;
                LogHas("[brace] mission start: ON - an AI man whose Athletics bar (points ÷ his full pool, what the bar shows) is at or below his formation's order floor stops attacking in melee and only defends (guard up) until he refills to floor + 20% (BraceRecoverPercent) exactly (BraceRecoverSpreadPercent 0): charge 20% → 40% (BraceFloorChargePercent), advance 40% → 60% (BraceFloorAdvancePercent), hold / halt / retreat / move / follow / anything else 60% → 80% (BraceFloorHoldPercent)");
                double t = 11000;

                // ---- A: a CHARGING man braces at 20% and attacks again at 40%
                var a = Soldier(logic, body, 700, BraceOrderKind.Charge);
                Blows(a, 7, t);                                     // 30%: above the charge floor
                Look(logic, ref t);
                Check(a.Brace != null && !a.Brace.Active && logic.BracingNow == 0 && bs.MenPolled == 1, "A: a charging man at 30% braced (his floor is 20)");
                Blows(a, 1, t);                                     // 20%: at the floor
                Look(logic, ref t);
                Check(a.Brace!.Active && a.Input != null && a.Input.BraceHoldAttacks && body.Hooked.Contains(700) && bs.Braces == 1
                      && bs.ByKind(BraceOrderKind.Charge) == 1 && logic.BracingNow == 1, "A: a charging man at 20% did not brace");
                LogHas("[brace] first brace this mission: (agent 700) at ");
                LogHas(" s - order charge (the charge floor 20%), his bar 20.0% of his full pool (20.0 points) → no melee attacks, guard up, until 40.0% = floor 20.0% + his margin 20.0 points (BraceRecoverPercent 20 + 0.0, his own roll); shield: no shield; our component added, the engine's input callback turned on by us");
                Check(InputFrame(a, MvAttackDown) == MvDefendDown, "A: a bracing man's attack wish was not replaced by a guard");
                Check(InputFrame(a, MvForward) == MvForward, "A: a bracing man without a shield got a guard he did not ask for");
                Check(InputFrame(a, MvAttackLeft | MvDefendLeft) == MvDefendLeft, "A: a bracing man's own guard was not kept");
                RefillTo(a, 0.39, ref t);
                Look(logic, ref t);
                Check(a.Brace.Active, "A: he stopped bracing at 39% (his target is 40)");
                RefillTo(a, 0.40, ref t);
                Look(logic, ref t);
                Check(!a.Brace.Active && !a.Input.BraceHoldAttacks && !a.Input.Active && body.Released.Contains(700) && bs.Ends(BraceEnd.Refilled) == 1,
                    "A: he did not stop bracing at 40%");
                Check(InputFrame(a, MvAttackDown) == MvAttackDown, "A: his attacks are still held after the brace");
                LogHas("[brace] first brace ended at ");
                LogHas(" - refilled to floor + recover; bar now 40.0%, order now charge; melee hits taken 0, blocked 0; his attacks taken out 2 frames, the shield held up 0 frames, engine calls 3");

                // ---- B: a HOLDING man braces at 60% and attacks again at 80%
                var b = Soldier(logic, body, 701, BraceOrderKind.Stop);
                Blows(b, 3, t);
                Look(logic, ref t);
                Check(b.Brace != null && !b.Brace.Active, "B: a holding man braced at 70%");
                Blows(b, 1, t);
                Look(logic, ref t);
                Check(b.Brace!.Active && b.Brace.KindAtStart == BraceOrderKind.Stop, "B: a holding man at 60% did not brace");
                RefillTo(b, 0.79, ref t);
                Look(logic, ref t);
                Check(b.Brace.Active, "B: he stopped at 79%");
                RefillTo(b, 0.80, ref t);
                Look(logic, ref t);
                Check(!b.Brace.Active && bs.Ends(BraceEnd.Refilled) == 2, "B: he did not stop at 80%");

                // ---- C: the order changes mid-brace (hold → charge: out at once) and before one (charge → hold: in at once)
                var c = Soldier(logic, body, 702, BraceOrderKind.Stop);
                Blows(c, 4, t);
                Look(logic, ref t);
                Check(c.Brace!.Active, "C: precondition - he does not brace at 60% under hold");
                body.Orders[702] = BraceOrderKind.Charge;
                Look(logic, ref t);
                Check(!c.Brace.Active && bs.Ends(BraceEnd.OrderChanged) == 1, "C: a charge order did not end his brace at 60% (the charge target is 40)");
                var c2 = Soldier(logic, body, 703, BraceOrderKind.Charge);
                Blows(c2, 7, t);                                    // 30%
                Look(logic, ref t);
                Check(!c2.Brace!.Active, "C: a charging man braced at 30%");
                body.Orders[703] = BraceOrderKind.Stop;
                Look(logic, ref t);
                Check(c2.Brace.Active && c2.Brace.KindAtStart == BraceOrderKind.Stop, "C: a hold order did not start his brace at once");
                body.Orders[703] = BraceOrderKind.Charge;           // back to charge: 30% is under the charge target 40 - he keeps bracing
                Look(logic, ref t);
                Check(c2.Brace.Active, "C: back to charge at 30% ended his brace (the charge target is 40)");

                // the AI's own advance (a Move order under an advance behaviour) is the advance floor
                var d = Soldier(logic, body, 704, BraceOrderKind.AiAdvance);
                Blows(d, 6, t);                                     // 40%
                Look(logic, ref t);
                Check(d.Brace!.Active && bs.ByKind(BraceOrderKind.AiAdvance) == 1 && bs.ByGroup(BraceOrder.Advance) == 1, "D: the AI's advance did not brace at 40%");

                // ---- E: a WOUNDED man (health 50%): never at his cap, from 30% to his cap 50% - never stuck
                var w = Wounded(logic, 705, 50f, t);                // no skill: a pool of 50, a blow = 20%
                body.Orders[705] = BraceOrderKind.Stop;
                Look(logic, ref t);
                Check(!w.Brace!.Active, "E: a wounded man full to his cap (50%) braced under the hold floor 60");
                Blows(w, 1, t);                                     // 30%
                Look(logic, ref t);
                Check(w.Brace.Active && w.Brace.BandAtStart.Capped && Near(w.Brace.BandAtStart.Target, 0.5) && Near(w.Brace.BandAtStart.Floor, 0.3),
                    "E: the wounded man's band is not 30 → 50 (capped)");
                RefillTo(w, 0.5, ref t);
                Look(logic, ref t);
                Check(!w.Brace.Active && bs.Ends(BraceEnd.WoundCap) == 1, "E: the wounded man did not stop at his cap 50%");

                // ---- F: never the player; a man the player takes over (RTS Camera) stops bracing
                var me = Soldier(logic, body, 706, BraceOrderKind.Stop);
                logic.SmokePlayer = me.Agent;
                Blows(me, 10, t);
                Look(logic, ref t);
                Check(me.Brace == null && (me.Input == null || !me.Input.BraceHoldAttacks), "F: the player braced");
                logic.SmokePlayer = null;
                var taken = Soldier(logic, body, 707, BraceOrderKind.Stop);
                Blows(taken, 5, t);
                Look(logic, ref t);
                Check(taken.Brace!.Active, "F: precondition - 707 does not brace");
                body.NotAi.Add(707);
                Look(logic, ref t);
                Check(!taken.Brace.Active && bs.Ends(BraceEnd.PlayerControl) == 1, "F: a man the player took over kept bracing");

                // ---- G: ranged goes on - a bow in hand is let through, a ranged release counted; a melee ready is cancelled
                var g = Soldier(logic, body, 708, BraceOrderKind.Charge);
                body.Hands[708] = new HandFacts { ShieldSlot = -1, OneHandedSlot = -1, MainRanged = true };
                Blows(g, 8, t);
                Look(logic, ref t);
                Check(g.Brace!.Active && g.Input!.BraceRanged && bs.ShieldAtStart(ShieldStep.NoShield) >= 1, "G: the bowman did not brace, or his bow was not seen");
                Check(InputFrame(g, MvAttackDown) == MvAttackDown && g.Input.BraceRangedPassed == 1, "G: a bracing bowman's shot was held");
                var gr = Rules;
                logic.ObserveAction(g, ActReleaseRangedCode, t, in gr);
                logic.ObserveAction(g, ActIdle, t + 0.2, in gr);
                Check(bs.RangedWhileBracing == 1 && bs.MeleeWhileBracing == 0, "G: the ranged release while bracing was not counted apart");
                body.Hands[708] = HandFacts.None;                   // he switched to his sword
                Look(logic, ref t);
                g.PrevAction = ActReadyMeleeCode;                   // a melee ready under way: cancelled with a guard, never released
                Check(InputFrame(g, MvAttackDown) == MvDefendDown, "G: a melee ready of a bracing man was not cancelled");
                g.PrevAction = ActReleaseRangedCode;                // a ranged action under way is let through even with the sword read
                Check(InputFrame(g, MvAttackDown) == MvAttackDown, "G: a ranged action under way was held");
                g.PrevAction = ActIdle;

                // ---- H: the shield taken out - a two-hander's man: the one-hander first, then the shield; kept up; put away; re-asked
                var h = Soldier(logic, body, 709, BraceOrderKind.Stop);
                body.Hands[709] = new HandFacts { ShieldSlot = 1, OneHandedSlot = 0, MainNeedsBothHands = true };
                Blows(h, 4, t);
                Look(logic, ref t);
                Check(h.Brace!.Active && body.Wields.Count == 1 && body.Wields[0] == (709, 0) && bs.OneHandedCalls == 1 && bs.ShieldAtStart(ShieldStep.WieldOneHanded) == 1,
                    "H: the one-hander was not asked first");
                Look(logic, ref t);                                 // 0.3 s later: not yet (checked every 1 s)
                Check(body.Wields.Count == 1, "H: wielded again before the check interval");
                t += 1.0;
                Look(logic, ref t);
                Check(body.Wields.Count == 2 && body.Wields[1] == (709, 1) && bs.ShieldCalls == 1, "H: the shield was not asked after the one-hander");
                Look(logic, ref t);
                Check(bs.ShieldHeld == 1 && h.Input!.BraceShieldInHand, "H: the shield in his hand was not seen");
                Check(InputFrame(h, MvForward) == (MvForward | MvDefendDown) && h.Input.BraceShieldRaised == 1, "H: the shield was not held up");
                Check(InputFrame(h, MvForward | MvDefendLeft) == (MvForward | MvDefendLeft), "H: his own guard direction was overwritten");
                S.Set(SettingsSchema.BraceRaiseShield, false, SettingSources.Mcm);
                Check(InputFrame(h, MvForward) == MvForward, "H: BraceRaiseShield off still raised the shield (live)");
                S.Set(SettingsSchema.BraceRaiseShield, true, SettingSources.Mcm);
                var hh = body.Hands[709];
                hh.ShieldInOffHand = false;                         // the AI put it away
                body.Hands[709] = hh;
                Look(logic, ref t);
                Check(bs.SwitchedBack == 1 && !h.Input.BraceShieldInHand, "H: the AI putting the shield away was not counted");
                t += 1.0;
                Look(logic, ref t);
                Check(body.Wields.Count == 3 && body.Wields[2] == (709, 1), "H: the shield was not asked again");
                hh = body.Hands[709];
                hh.ShieldInOffHand = false;
                body.Hands[709] = hh;
                t += 1.0;
                Look(logic, ref t);
                Check(body.Wields.Count == 3, "H: more than " + BraceMath.MaxWieldCalls + " wield calls in one brace");
                // a shield already in hand needs nothing; the switch off asks nothing
                var h2 = Soldier(logic, body, 710, BraceOrderKind.Stop);
                body.Hands[710] = new HandFacts { ShieldSlot = 1, OneHandedSlot = 0, ShieldInOffHand = true };
                S.Set(SettingsSchema.BraceWieldShield, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[brace] settings changed mid-mission at ");
                var h3 = Soldier(logic, body, 711, BraceOrderKind.Stop);
                body.Hands[711] = new HandFacts { ShieldSlot = 1, OneHandedSlot = 0 };
                Blows(h3, 4, t);
                Look(logic, ref t);
                Check(h3.Brace!.Active && bs.ShieldOffSwitch == 1 && body.Wields.Count == 3, "H: BraceWieldShield off still asked for the shield");
                S.Set(SettingsSchema.BraceWieldShield, true, SettingSources.Mcm);
                Blows(h2, 4, t);
                Look(logic, ref t);
                Check(h2.Brace!.Active && bs.ShieldAtStart(ShieldStep.InHand) == 1 && body.Wields.Count == 3, "H: a shield already in hand was asked for");

                // ---- I: one hold's end never lifts another - the AI timer and the brace on the same man, both ways round
                var i1 = Soldier(logic, body, 712, BraceOrderKind.Stop);
                Blows(i1, 4, t);
                Look(logic, ref t);
                Check(i1.Brace!.Active, "I: precondition - 712 does not brace");
                AiInputHook.SetHold(i1, true, t);                   // the AI timer's wish on top
                AiInputHook.SetHold(i1, false, t + 0.5);            // ... and its end
                Check(i1.Input!.Active && InputFrame(i1, MvAttackDown) == MvDefendDown, "I: the timer's end lifted the brace");
                i1.Input.CallbackOurs = true;                       // the callback flag stays on while he braces
                Check(!AiInputHook.UnhookIfIdle(i1) && i1.Input.CallbackOurs, "I: the callback went off while he still braced");
                i1.Input.CallbackOurs = false;
                AiInputHook.SetHold(i1, true, t);
                RefillTo(i1, 0.8, ref t);
                Look(logic, ref t);
                Check(!i1.Brace.Active && i1.Input.HoldAttacks && InputFrame(i1, MvAttackDown) == MvDefendDown, "I: the brace's end lifted the timer");
                AiInputHook.SetHold(i1, false, t);

                // ---- the guard while bracing, through the real OnMeleeHit
                MeleeHitOn(logic, h, blocked: true);
                MeleeHitOn(logic, h, blocked: true);
                MeleeHitOn(logic, h, blocked: false);
                Check(bs.HitsWhileBracing == 3 && bs.BlockedWhileBracing == 2 && h.Brace.HitsTaken == 3, "the guard while bracing was not counted");

                // ---- hot swap: the hold floor to 70 - a man at 65% braces at his next look
                var k = Soldier(logic, body, 713, BraceOrderKind.Stop);
                Blows(k, 3, t);
                AthleticsMath.Regen(k, Rules, t + 100, 3, 0f, 1f); // 70% → 75%... then one blow: 65%
                Blows(k, 1, t + 100);
                Look(logic, ref t);
                Check(!k.Brace!.Active, "hot swap: precondition - 713 braced at 65% under 60");
                S.Set(SettingsSchema.BraceFloorHoldPercent, 70, SettingSources.Mcm);
                Look(logic, ref t);
                Check(k.Brace.Active, "hot swap: the new hold floor 70 did not apply at his next look");
                S.Set(SettingsSchema.BraceFloorHoldPercent, 60, SettingSources.Mcm);

                // ---- the per-man margin: rolled once, kept; the slider rescales it live
                S.Set(SettingsSchema.BraceRecoverSpreadPercent, 5, SettingSources.Mcm);
                double unit = h.Brace.Unit;
                Look(logic, ref t);
                Check(h.Brace.Unit == unit && bs.UnitsRolled == bs.MenPolled, "the margin was rolled again (or not once per man)");
                Check(Near(BraceMath.Band(BraceRules.From(S), BraceOrder.Hold, 1.0, unit).Target, 0.8 + 0.05 * unit), "the spread does not move his target by his own roll");
                double lo = double.MaxValue, hi = double.MinValue;
                var dice = new SeededRandom(7);
                for (int n = 0; n < 500; n++)
                {
                    double u = BraceMath.RollUnit(dice);
                    lo = Math.Min(lo, u);
                    hi = Math.Max(hi, u);
                }
                Check(lo >= -1 && hi < 1 && lo < -0.9 && hi > 0.9, "the rolls do not spread over [-1, 1)");

                // ---- leaving the field: the brace ends, no engine call
                int released = body.Released.Count;
                typeof(AthleticsLogic).GetMethod("Untrack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, new object[] { d.Agent });
                Check(!d.Brace!.Active && !d.Input!.BraceHoldAttacks && body.Released.Count == released && bs.Ends(BraceEnd.LeftField) == 1,
                    "leaving the field: the brace did not end, or the engine was called on a removed man");

                // ---- the switches lift every brace at once: BraceEnabled, then the master switch
                int bracing = logic.BracingNow;
                Check(bracing >= 3, "precondition: several men brace (" + bracing + ")");
                S.Set(SettingsSchema.BraceEnabled, false, SettingSources.Mcm);
                logic.TickBrace(t + 0.01);
                Check(logic.BracingNow == 0 && bs.Ends(BraceEnd.SwitchedOff) == bracing && !h.Input.BraceHoldAttacks, "BraceEnabled off: the braces were not lifted at once");
                LogHas("[brace] BraceEnabled switched OFF mid-mission at ");
                S.Set(SettingsSchema.BraceEnabled, true, SettingSources.Mcm);
                Look(logic, ref t);
                int again = logic.BracingNow;
                Check(again >= 3, "back on: the tired men did not brace again (" + again + ")");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickBrace(t + 0.01);
                Check(logic.BracingNow == 0, "ModEnabled off: the braces were not lifted at once");
                LogHas("[brace] the whole mod (ModEnabled) switched OFF mid-mission at ");
                Check(InputFrame(h, MvAttackDown) == MvAttackDown, "ModEnabled off: a man is still held");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Look(logic, ref t);

                // ---- the formation read API counts the men bracing (the next step's "ready men")
                var fs = new FormationAthleticsStats(10, 50, 5, 0.5, 0.1, 0, 0.7, 2, 0.9, 3);
                Check(fs.Ready == 7 && fs.Bracing == 3 && fs.Total == 10, "the formation stats' Ready / Bracing / Total");

                // ---- the summary: the battle ends with men still bracing
                int still = logic.BracingNow;
                logic.WriteAthleticsSummary();
                Check(logic.BracingNow == 0 && bs.Ends(BraceEnd.MissionEnd) == still, "mission end: the braces were not all closed");
                LogHas("[summary] brace by orders (step 23): ON - ");
                LogHas("its settings CHANGED during this battle (the lines mix both)");
                LogHas("[summary] brace: ");
                LogHas(" AI fighters braced, ");
                LogHas("by order: charge ");
                LogHas("the AI's advance 1");
                LogHas("[summary] brace lengths: under 5 s ");
                Check(bs.Ends(BraceEnd.Refilled) == 3 && bs.Ends(BraceEnd.SwitchedOff) == bracing + again, "the ends by reason: refilled " + bs.Ends(BraceEnd.Refilled)
                      + " (3), switched off " + bs.Ends(BraceEnd.SwitchedOff) + " (" + (bracing + again) + ")");
                LogHas("[summary] brace ends: refilled to floor + recover 3, the order changed 1, refilled to his wound cap 1, switched off " + (bracing + again) + ", fell or left the field 1, the player took him 1, the hideout boss fight's fresh start 0, the battle ended " + still);
                LogHas("[summary] brace margins (BraceRecoverSpreadPercent 5): ");
                // (the switches above ended and restarted several braces: the counts come from the stats, whose meaning the steps above checked)
                LogHas("[summary] brace shield (BraceWieldShield on): at the brace's start - already in hand " + bs.ShieldAtStart(ShieldStep.InHand) + ", taken out "
                       + bs.ShieldAtStart(ShieldStep.WieldShield) + ", a one-hander first 1, no shield " + bs.ShieldAtStart(ShieldStep.NoShield));
                LogHas("; wield calls: the shield " + bs.ShieldCalls + ", a one-hander 1; the shield seen in his hand after we asked " + bs.ShieldHeld + ", the AI put it away again while bracing 1");
                Check(bs.ShieldAtStart(ShieldStep.InHand) >= 1 && bs.ShieldCalls >= 2 && bs.ShieldHeld >= 1 && bs.ShieldOffSwitch == 1, "the shield counts");
                LogHas("[summary] brace GUARD: melee hits taken while bracing 3, blocked 2 (66.7%");
                LogHas("melee attacks that started while bracing anyway 0 (should be about 0), ranged shots while bracing 1 (allowed)");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                StepBackDefaults();
                BraceDefaults();
            }
        }

        /// <summary>Step 23's cost: a thousand men looked at about four times a second - the tick stays small and allocates
        /// nothing once every man has his record.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BraceTickCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                BraceDefaults(spread: 5);
                var body = new FakeBraceBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.BraceBody = body;
                logic.BraceDice = new SeededRandom(1);
                const int men = 1000;
                double t0 = 12000;
                for (int i = 0; i < men; i++)
                {
                    var st = Soldier(logic, body, 3000 + i, i % 3 == 0 ? BraceOrderKind.Charge : i % 3 == 1 ? BraceOrderKind.Advance : BraceOrderKind.Stop);
                    Blows(st, i % 10, t0);                          // 100% down to 10%: about half of them brace
                    if (i % 4 == 0) body.Hands[3000 + i] = new HandFacts { ShieldSlot = 1, OneHandedSlot = 0, ShieldInOffHand = true };
                }
                logic.TickBrace(t0);                                // first look: every record made, the margins rolled
                int bracing = logic.BracingNow;
                Check(bracing > 200 && bracing < 800, "cost: an odd number of men brace: " + bracing);
                const int ticks = 600;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickBrace(t0 + k / 60.0);  // ten seconds at 60 fps: each man looked at ~40 times
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                Console.WriteLine("        step 23 cost: " + men + " men, " + bracing + " bracing - the brace's tick " + msPerTick.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                  + " ms a tick, " + allocated + " bytes allocated over " + ticks + " ticks");
                Check(logic.BracingNow == bracing, "cost: braces changed with nothing refilling");
                Check(msPerTick < 0.5, "cost: the brace's tick over " + men + " men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 256 * 1024, "cost: the brace's tick allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.BraceStats.AiManSeconds > men * 9 && logic.BraceStats.AiManSeconds < men * 11, "cost: the AI fighter-time is not ~10 s per man: " + logic.BraceStats.AiManSeconds);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                BraceDefaults();
            }
        }
    }
}
