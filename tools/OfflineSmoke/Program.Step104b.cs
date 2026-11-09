using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 104b - THE REEL IN THE SADDLE, LIVE (AI_NOTES "104b - built", DESIGN decision 93): through the REAL logic with
    /// stand-ins of the engine (the rider's body, the animation calls) - Toni's tournament: both mounted, your perfect block: the riposte still
    /// refused "a rider", the REEL applied - his record reels (cause a reel in the saddle), from the tick the lock (his attack + guard bits out of
    /// the REAL filter, his steering kept), act_stagger_backward x1.0 on his CHANNEL 1 only (nothing on channel 0, no stagger, no action on a
    /// horse), his horse x0.5, the slow motion, your on-screen line; your landed hit while he reels is refused by step 105 ("a rider" - no 80 %
    /// knockdown); 1.5 s later every lock given back; the AI's timed block of YOUR mounted blow: you reel at most 1.0 s, your gate frame; every
    /// exit - dismounted, removed, the switch, the master switch, the mission end, an error - releases every lock; the [summary] line (left over 0).
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeRiderReelBody : IRiderReelBody
        {
            public readonly HashSet<int> Riding = new HashSet<int>();
            public readonly HashSet<int> You = new HashSet<int>();
            public readonly Dictionary<int, double> Speeds = new Dictionary<int, double>();
            public int Hooks, Releases;
            public bool Throw;

            bool IRiderReelBody.Riding(TrackedAgent st)
            {
                if (Throw) throw new InvalidOperationException("smoke: the rider body throws");
                return Riding.Contains(st.AgentIndex);
            }

            public bool AiDriven(TrackedAgent st) => !You.Contains(st.AgentIndex);
            public double HorseSpeed(TrackedAgent st) => Speeds.TryGetValue(st.AgentIndex, out double v) ? v : double.NaN;

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                Hooks++;
                return AiInputHook.HookResult.AlreadyHooked;
            }

            public void Release(TrackedAgent st) => Releases++;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeRiderReelBody()
        {
            Check(RiderReelBodyDefault.Current is GameRiderReelBody, "the default rider reel body is not the game's: " + RiderReelBodyDefault.Current.GetType().Name);
            RiderReelBodyDefault.Current = new FakeRiderReelBody();
            Check(new AthleticsLogic().RiderReelBody == RiderReelBodyDefault.Current, "a new logic does not start with the default rider reel body");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RiderReelThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("TimedBlock", StringComparison.Ordinal) || p.Key.StartsWith("TimedRider", StringComparison.Ordinal)
                                                             || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal) || p.Key.StartsWith("ReelingHit", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.TimedBlocksOnHorseback, true, SettingSources.File); // as shipped (defaults.json)
                S.Set(SettingsSchema.ReelingHitKnockdownEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockSlowMo, true, SettingSources.File);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.File);
                var inputs = new FakeImpactInputs { Damage = 20 };
                var impact = new FakeImpactBody();
                var guard = new FakeGuardBody();
                var anim = new FakeAnimBody();
                var slow = new FakeSlowMoBody();
                var rb = new FakeRiderReelBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = impact;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = slow;
                logic.AnimBody = anim;
                logic.RiderReelBody = rb;
                logic.ImpactDice = new ScriptDice { Rest = 0.5 };
                var dice = new ScriptDice { Rest = 0.5 };
                logic.RiderShadowDice = dice;
                anim.Known["act_stagger_backward"] = (801, 1.02);
                double t = 104500;
                logic.TickImpact(t);
                int next = 10450;
                var st = logic.RiderReelStats;

                TrackedAgent Man(bool rides)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, 1.0);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (4.0, 100);
                    inputs.Shields.Add(i);
                    anim.Shields.Add(i);
                    anim.Usage[i] = "onehanded_shield_swing";
                    if (rides)
                    {
                        inputs.Mounted.Add(i);
                        inputs.Speeds[i] = 6.5;
                        impact.Mounted.Add(i);
                        anim.Riding.Add(i);
                        rb.Riding.Add(i);
                        rb.Speeds[i] = 6.5;
                    }
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                void YourGuard(double age)
                {
                    guard.Keys[logic.SmokePlayer!.Index] = 0;
                    Tick(0.1);
                    guard.Keys[logic.SmokePlayer!.Index] = 0x800;
                    Tick(age);
                }
                var you = Man(true);
                rb.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);
                const uint move = 0x1 | 0x10; // Forward | TurnRight - his steering
                const uint attackDefend = 0x40 | 0x1000; // AttackLeft | DefendRight

                // ---- 1. Toni's tournament: both mounted, your PERFECT block - the reel APPLIED
                YourGuard(0.05);
                var r1 = Man(true);
                int staggers = impact.Staggers.Count, plays = anim.Plays.Count, rears = impact.OwnRears.Count, dismounts = impact.OwnDismounts.Count;
                dice.Next(0.5, 0.9, 0.9, 0.9); // the L56 spread, the reel (≤ 1), the knock-off (missed), the rear (missed - 104c's anyway)
                logic.ImpactModelHitTaken(r1.Agent, you.Agent, Swing(Agent.UsageDirection.AttackRight, true, true, CombatCollisionResult.Blocked));
                var d = logic.LastImpact;
                Check(d.TimedScore > 0.999 && d.Riposte.Refusal == ImpactRefusal.Rider && !d.Riposte.Happens, "the live riposte on a rider is not today's refusal: " + d.Riposte.Refusal);
                Check(r1.Impact.Reeling(t + 1.4) && r1.Impact.ReelCause == ReelCause.RiderReel && r1.RiderReelSlot >= 0 && logic.RiderReelsActive == 1,
                    "the reel is not on his record / not queued: until " + r1.Impact.ReelingUntil + ", cause " + r1.Impact.ReelCause);
                Check(st.Decided == 1 && st.Started == 0 && anim.Plays.Count == plays, "the reel started inside the hit callback");
                Tick(); // the tick starts it
                Check(st.Started == 1 && st.LockAi == 1 && rb.Hooks == 1 && r1.Input != null && r1.Input.Reel && r1.Input.ReelNoGuard,
                    "the AI rider's lock did not start: started " + st.Started + ", hooks " + rb.Hooks);
                var newPlays = anim.Plays.Skip(plays).ToList();
                Check(newPlays.Count == 1 && newPlays[0].Man == r1.AgentIndex && newPlays[0].Channel == TimedRiderAnimMath.RiderChannel && newPlays[0].Index == 801
                      && Math.Abs(newPlays[0].Speed - 1f) < 1e-6,
                    "the reel's animation: " + string.Join(", ", newPlays));
                Check(anim.Plays.Skip(plays).All(p => p.Channel == 1), "an action of the reel touched a channel other than 1");
                Check(impact.Staggers.Count == staggers && impact.OwnRears.Count == rears && impact.OwnDismounts.Count == dismounts,
                    "the reel staggered him on channel 0 / reared / dismounted (104c's)");
                Check(Math.Abs(r1.ReelHorseCheck - 0.5f) < 1e-6 && r1.MountSpeedMultiplier <= 0.5f + 1e-6 && st.HorsesChecked == 1,
                    "his horse not checked: x" + r1.MountSpeedMultiplier);
                Check(slow.Ours && st.SlowMoAsked == 1, "your perfect block's reel did not slow the world");
                LogHas("live: first REEL IN THE SADDLE (step 104b)");
                LogHas("REELS (live) 1.50 s");
                // the REAL filter's frame: attack + guard out, his steering untouched
                uint f = AiInputHook.ApplyReel(r1, r1.Input!, move | attackDefend);
                Check(f == move && r1.Input!.ReelAttacksOut == 1 && r1.Input.ReelGuardsOut == 1, "the AI lock frame: " + f.ToString("X"));
                // your landed swing while he reels: step 105 refuses a rider (no 80 % knockdown), no reeling bonus on his knock-off
                int kd = impact.OwnDismounts.Count;
                logic.ImpactModelHitTaken(you.Agent, r1.Agent, Swing(Agent.UsageDirection.AttackLeft, false, false, CombatCollisionResult.StrikeAgent));
                var hit = logic.LastImpact;
                Check(hit.ReelingHit.Asked && hit.ReelingHit.Refusal == ReelingHitRefusal.Mounted && !hit.ReelingHit.Happens && !hit.Reeling,
                    "a hit on the reeling rider: " + hit.ReelingHit.Refusal + ", reeling bonus " + hit.Reeling);
                // a second timed block while he reels: refused "already reeling" - never a second reel on top
                YourGuard(0.05);
                logic.ImpactModelHitTaken(r1.Agent, you.Agent, Swing(Agent.UsageDirection.AttackRight, true, true, CombatCollisionResult.Blocked));
                Check(logic.LastRiderShadow.Refusal == TimedRiderRefusal.Reeling && logic.RiderReelsActive == 1, "a second reel on a reeling rider");
                // the reel's end: every lock given back
                Tick(1.6);
                Check(logic.RiderReelsActive == 0 && !r1.Input!.Reel && Math.Abs(r1.ReelHorseCheck - 1f) < 1e-6 && r1.MountSpeedMultiplier > 0.99f && r1.RiderReelSlot == -1
                      && !r1.Impact.Reeling(t) && st.EndedBy[(int)RiderReelEnd.Ended] == 1 && rb.Releases == 1,
                    "the reel's end did not give every lock back: active " + logic.RiderReelsActive + ", horse x" + r1.MountSpeedMultiplier);
                Check(st.Measured == 1 && Math.Abs(st.MeasuredSum - 1.5) < 0.05 && Math.Abs(st.AskedSum - 1.5) < 1e-9, "asked 1.5 → measured " + st.MeasuredSum);
                Check(st.AnimCleared == 1 && anim.Clears.Contains(r1.AgentIndex), "the animation was not cut at its 1.0 s part (the lock alone after it)");

                // ---- 2. the AI's timed block of YOUR mounted blow: you reel at most 1.0 s, your gate frame
                Tick(3);
                S.Set(SettingsSchema.TimedBlockAiPercent, 100, SettingSources.File); // even a perfect AI block
                var aiBlocker = Man(true);
                guard.Contacts[aiBlocker.Agent.Index] = (GuardRead.Active, 0.0);
                dice.Next(0.5, 0.0);
                logic.ImpactModelHitTaken(you.Agent, aiBlocker.Agent, Swing(Agent.UsageDirection.AttackUp, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.RiderReelsActive == 1 && st.LockYou == 1 && you.Impact.Reeling(t) && you.Impact.ReelingUntil - t <= 1.0 + 1e-6,
                    "the AI's block on you: active " + logic.RiderReelsActive + ", until +" + (you.Impact.ReelingUntil - t));
                uint yf = logic.YourReelFrame(you, move | attackDefend);
                Check(yf == move && st.YourAttacksSwallowed == 1 && st.YourGuardsSwallowed == 1, "your gate frame: " + yf.ToString("X"));
                Check(you.Input == null || !you.Input.Reel, "YOUR reel went through the AI's hook");
                Tick(1.1);
                Check(logic.RiderReelsActive == 0 && logic.YourReelFrame(you, move | attackDefend) == (move | attackDefend), "your gate still holds after your reel");
                S.Set(SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockAiPercent.Default, SettingSources.File);

                // ---- 3. every exit gives every lock back
                TrackedAgent Reeling()
                {
                    Tick(3);
                    YourGuard(0.05);
                    var r = Man(true);
                    dice.Next(0.5, 0.0, 0.9, 0.9);
                    logic.ImpactModelHitTaken(r.Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                    Tick();
                    Check(r.Input != null && r.Input.Reel && r.ReelHorseCheck < 1f, "a reel did not start for the exit check");
                    return r;
                }
                bool AllBack(TrackedAgent r) => logic.RiderReelsActive == 0 && (r.Input == null || !r.Input.Reel) && Math.Abs(r.ReelHorseCheck - 1f) < 1e-6 && r.RiderReelSlot == -1;
                // dismounted
                var x = Reeling();
                rb.Riding.Remove(x.AgentIndex);
                Tick();
                Check(AllBack(x) && st.EndedBy[(int)RiderReelEnd.Dismounted] == 1 && !x.Impact.Reeling(t), "dismounted: a lock left");
                // removed (no engine call)
                x = Reeling();
                int rel = rb.Releases;
                CallPrivate(logic, "Untrack", x.Agent);
                Check(AllBack(x) && st.EndedBy[(int)RiderReelEnd.Removed] == 1 && rb.Releases == rel, "removed: a lock left / an engine call");
                // TimedBlocksOnHorseback off mid-reel
                x = Reeling();
                S.Set(SettingsSchema.TimedBlocksOnHorseback, false, SettingSources.File);
                Tick();
                Check(AllBack(x) && st.EndedBy[(int)RiderReelEnd.SwitchedOff] == 1, "the switch off: a lock left");
                // ... and off = no new reel (the shadow still says what it would do)
                YourGuard(0.05);
                var y = Man(true);
                dice.Next(0.5, 0.0, 0.9, 0.9);
                logic.ImpactModelHitTaken(y.Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                Check(logic.LastRiderShadow.Reel && logic.RiderReelsActive == 0 && !y.Impact.Reeling(t + 0.1), "the switch off still applied a reel");
                S.Set(SettingsSchema.TimedBlocksOnHorseback, true, SettingSources.File);
                // the master switch mid-reel
                x = Reeling();
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Tick();
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Check(AllBack(x) && st.EndedBy[(int)RiderReelEnd.SwitchedOff] == 2, "the master switch: a lock left");
                // the mission end (before the summary)
                x = Reeling();
                CallPrivate(logic, "CloseRiderReels", "the mission end");
                Check(AllBack(x) && st.EndedBy[(int)RiderReelEnd.MissionEnd] == 1, "the mission end: a lock left");
                // an error: every lock given back at once, the reel off for the battle
                x = Reeling();
                rb.Throw = true;
                Tick();
                rb.Throw = false;
                Check(AllBack(x) && st.EndedBy[(int)RiderReelEnd.Error] == 1, "an error: a lock left");
                YourGuard(0.05);
                var z = Man(true);
                dice.Next(0.5, 0.0, 0.9, 0.9);
                logic.ImpactModelHitTaken(z.Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                Check(logic.RiderReelsActive == 0 && !z.Impact.Reeling(t + 0.1), "the reel ran on after its error");

                // ---- 4. an untimed (held) block = today's refusal: no reel, no die
                Tick(3);
                guard.Keys[you.Agent.Index] = 0x800;
                Tick(2.0);
                int decided = st.Decided, calls = dice.Calls;
                logic.ImpactModelHitTaken(Man(true).Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                Check(st.Decided == decided && dice.Calls == calls, "a held block reeled a rider");

                // ---- 5. the [summary] line: every release counted, nothing left over
                st.LeftOver = logic.RiderReelsActive;
                string line = st.SummaryLine(TimedRiderRules.From(S), true);
                Check(line.StartsWith("timed blocks on horseback (step 104b): the reel LIVE - reels decided ", StringComparison.Ordinal)
                      && line.Contains("at its end 2") && line.Contains("death / removal 1") && line.Contains("dismounted 1") && line.Contains("a switch off 2")
                      && line.Contains("the mission end 1") && line.Contains("an error 1") && line.Contains("left over at the mission end 0"),
                    "the [summary] line: " + line);
                Console.WriteLine("        step 104b: " + st.Started + " reels in the saddle started, every lock released (" + string.Join(" / ", st.EndedBy) + "), left over " + st.LeftOver);
                Console.WriteLine("        " + line);
            }
            finally
            {
                if (_logic != null) _logic.SmokePlayer = null;
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactHooks.PushOut = null;
                ImpactHooks.Perfect = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("TimedBlock", StringComparison.Ordinal) || p.Key.StartsWith("TimedRider", StringComparison.Ordinal)
                                                             || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal) || p.Key.StartsWith("ReelingHit", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, SettingsSchema.ImpactOnPlayerPercent.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}
