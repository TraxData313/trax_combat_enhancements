using System;
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
    /// Offline check for step 104c - THE REAR AND THE KNOCK-OFF, LIVE (AI_NOTES "104c - built", DESIGN decision 93 (b) / (c)): through the REAL
    /// logic with the engine's stand-ins - your PERFECT block's knock-off: step 53's dismount blow from the tick (owned by you), no reel animation
    /// on top, his record DOWN at once (no reel left), SEEN off, the reel's locks end "dismounted", your hit on the fallen man: no 80 %, no
    /// knockdown ("down"), your line "knocked off his horse!"; he stayed on by the dice: the REAR - our blow on his horse owned by the horse, its
    /// health back (lost 0), no animation, the lock kept to the reel's end, SEEN rearing; a horse at 1 health refused (the reel's animation
    /// plays); a mount that cannot rear refused; the engine keeping him on: not seen, his ground time taken back; the rear lever's error: off
    /// alone, the next refused "its lever off"; the AI's perfect block of YOUR blow: never a rear, never a knock-off; the master switch: no fall;
    /// the [summary] line (the horses' health lost 0).
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RiderFallThroughTheLogic()
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
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.File);
                S.Set(SettingsSchema.ImpactShowMessages, true, SettingSources.File);
                var inputs = new FakeImpactInputs { Damage = 20 };
                var impact = new FakeImpactBody();
                var guard = new FakeGuardBody();
                var anim = new FakeAnimBody();
                var slow = new FakeSlowMoBody();
                var rb = new FakeRiderReelBody();
                var sink = new FakeMessageSink();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = impact;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = slow;
                logic.AnimBody = anim;
                logic.RiderReelBody = rb;
                logic.MessageSink = sink;
                logic.ImpactDice = new ScriptDice { Rest = 0.99 };
                var dice = new ScriptDice { Rest = 0.99 };
                logic.RiderShadowDice = dice;
                anim.Known["act_stagger_backward"] = (801, 1.02);
                double t = 104600;
                logic.TickImpact(t);
                int next = 10460;
                var st = logic.RiderFallStats;

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
                void OffHisHorse(TrackedAgent m) // what the engine's dismount does to the stand-ins (the fake dismount blow takes him out of impact.Mounted)
                {
                    inputs.Mounted.Remove(m.AgentIndex);
                    anim.Riding.Remove(m.AgentIndex);
                    rb.Riding.Remove(m.AgentIndex);
                }
                // your perfect block of a rider's blow; the dice: the L56 spread, the reel, the knock-off, the rear
                TrackedAgent PerfectBlock(params double[] ladder)
                {
                    Tick(3);
                    YourGuard(0.05);
                    var r = Man(true);
                    dice.Next(new[] { 0.5 }.Concat(ladder).ToArray());
                    logic.ImpactModelHitTaken(r.Agent, logic.SmokePlayer!, Swing(Agent.UsageDirection.AttackRight, true, true, CombatCollisionResult.Blocked));
                    return r;
                }
                var you = Man(true);
                rb.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);

                // ---- 1. THE KNOCK-OFF: your perfect block, the knock-off die comes up
                int plays = anim.Plays.Count, dismounts = impact.OwnDismounts.Count, rears = impact.OwnRears.Count;
                var r1 = PerfectBlock(0.0, 0.0);
                Check(logic.LastRiderShadow.Dismount && logic.RiderReelsActive == 1 && impact.OwnDismounts.Count == dismounts, "the knock-off was applied inside the hit callback");
                Tick(); // the reel's start: the knock-off
                Check(impact.OwnDismounts.Count == dismounts + 1 && impact.OwnDismounts.Last().Victim == r1.AgentIndex && impact.OwnDismounts.Last().Attacker == you.AgentIndex,
                    "the knock-off's dismount blow: " + string.Join(", ", impact.OwnDismounts));
                Check(impact.OwnRears.Count == rears && anim.Plays.Count == plays && impact.Staggers.Count == 0,
                    "a knock-off played the reel's animation / a rear / a stagger on top: plays +" + (anim.Plays.Count - plays));
                Check(r1.Impact.Down(t + 0.5) && !r1.Impact.Reeling(t + 0.01), "his record is not DOWN after the knock-off: down until " + r1.Impact.DownUntil + ", reeling until " + r1.Impact.ReelingUntil);
                Check(st.KnockOffsDecided == 1 && st.KnockOffsApplied == 1 && st.KnockOffsYours == 1 && st.AnimSkipped == 1, "the knock-off's counts");
                LogHas("live: first KNOCK-OFF by a perfect timed block (step 104c)");
                OffHisHorse(r1);
                Tick();
                Check(st.KnockOffsSeen == 1 && logic.RiderFallWatches == 0 && logic.RiderReelsActive == 0 && logic.RiderReelStats.EndedBy[(int)RiderReelEnd.Dismounted] == 1
                      && (r1.Input == null || !r1.Input.Reel) && Math.Abs(r1.ReelHorseCheck - 1f) < 1e-6,
                    "the knock-off not seen / his reel's locks not given back (dismounted)");
                // your landed swing on the fallen man: step 105 has no reel to roll on, the usual model refuses "down" - no double-up
                logic.ImpactModelHitTaken(you.Agent, r1.Agent, Swing(Agent.UsageDirection.AttackLeft, false, false, CombatCollisionResult.StrikeAgent));
                var hit = logic.LastImpact;
                Check(!hit.ReelingHit.Happens && !hit.Knockdown.Happens && hit.Knockdown.Refusal == ImpactRefusal.Down && !hit.Stagger.Happens,
                    "a hit on the man just knocked off: 105 " + hit.ReelingHit.Refusal + " happens " + hit.ReelingHit.Happens + ", knockdown " + hit.Knockdown.Refusal);
                Tick(2);
                Check(sink.Lines.Any(l => l.Text.Contains("off his horse")), "your line: " + string.Join(" | ", sink.Lines.Select(l => l.Text)));

                // ---- 2. THE REAR: he stayed on by the dice - our blow on his horse, owned by the horse, its health back
                plays = anim.Plays.Count;
                var r2 = PerfectBlock(0.0, 0.99, 0.0);
                Tick();
                Check(impact.OwnRears.Count == rears + 1 && impact.OwnRears.Last().Rider == r2.AgentIndex && impact.OwnRears.Last().Attacker == -1,
                    "the rear blow (owned by the horse): " + string.Join(", ", impact.OwnRears));
                Check(anim.Plays.Count == plays && impact.OwnDismounts.Count == dismounts + 1, "a rear played the reel's animation on top / knocked him off");
                Check(r2.Input != null && r2.Input.Reel && r2.Impact.Reeling(t + 0.5) && !r2.Impact.Down(t), "the rear dropped the reel's lock");
                Check(Math.Abs(impact.HorseHealth[r2.AgentIndex] - 100) < 1e-9 && st.HorseDamageN == 1 && st.HorseDamageSum == 0, "the horse lost health to our rear blow: " + impact.HorseHealth[r2.AgentIndex]);
                LogHas("live: first REAR by a sharp timed block (step 104c)");
                Tick();
                Check(st.RearsSeen == 1 && st.RearsApplied == 1 && logic.RiderFallWatches == 0, "the rear not seen");
                Tick(1.6);
                Check(logic.RiderReelsActive == 0 && !r2.Input!.Reel, "the rear's reel did not give its lock back at its end");

                // ---- 3. refused: a horse at 1 health, a mount that cannot rear - the reel's animation plays then
                impact.HorseHealth[next] = 1;
                plays = anim.Plays.Count;
                var weak = PerfectBlock(0.0, 0.99, 0.0);
                Tick();
                Check(st.RearRefused[(int)RiderFallRefusal.HorseTooWeak] == 1 && impact.OwnRears.Count == rears + 1 && anim.Plays.Count == plays + 1,
                    "a horse at 1 health: refused " + st.RearRefused[(int)RiderFallRefusal.HorseTooWeak] + ", plays +" + (anim.Plays.Count - plays));
                Tick(1.6);
                impact.CannotRear.Add(next);
                PerfectBlock(0.0, 0.99, 0.0);
                Tick();
                Check(st.RearRefused[(int)RiderFallRefusal.CannotRear] == 1, "a mount that cannot rear was not refused");
                Tick(1.6);

                // ---- 4. the engine keeps him on: not seen, his ground time taken back
                impact.DismountTakesHimOff = false;
                var stays = PerfectBlock(0.0, 0.0);
                Tick();
                Check(stays.Impact.Down(t), "the knock-off did not put him down");
                Tick(2.1);
                Check(st.KnockOffsNotSeen == 1 && !stays.Impact.Down(t) && logic.RiderFallWatches == 0, "a knock-off the engine never played: still down / still followed");
                impact.DismountTakesHimOff = true;
                Tick(1);

                // ---- 5. the rear lever's error: off alone, the next refused
                impact.ThrowOnRear = true;
                PerfectBlock(0.0, 0.99, 0.0);
                Tick();
                impact.ThrowOnRear = false;
                Check(st.RearRefused[(int)RiderFallRefusal.EngineRefused] == 1, "the rear's error not counted");
                Tick(1.6);
                PerfectBlock(0.0, 0.99, 0.0);
                Tick();
                Check(st.RearRefused[(int)RiderFallRefusal.LeverOff] == 1 && impact.OwnRears.Count == rears + 1, "the rear lever not off after its error");
                Tick(1.6);
                var ko = PerfectBlock(0.0, 0.0); // the knock-off's lever is its own
                Tick();
                Check(st.KnockOffsApplied == 3, "the knock-off went off with the rear's lever: " + st.KnockOffsApplied);
                OffHisHorse(ko);
                Tick(2);

                // ---- 6. the AI's perfect block of YOUR mounted blow: never a rear, never a knock-off
                S.Set(SettingsSchema.TimedBlockAiPercent, 100, SettingSources.File);
                var aiBlocker = Man(true);
                guard.Contacts[aiBlocker.Agent.Index] = (GuardRead.Active, 0.0);
                int r6 = impact.OwnRears.Count, d6 = impact.OwnDismounts.Count;
                dice.Next(0.5, 0.0, 0.0, 0.0);
                logic.ImpactModelHitTaken(you.Agent, aiBlocker.Agent, Swing(Agent.UsageDirection.AttackUp, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.RiderReelsActive == 1 && !logic.LastRiderShadow.Rear && !logic.LastRiderShadow.Dismount && impact.OwnRears.Count == r6 && impact.OwnDismounts.Count == d6,
                    "the AI's block reared / unhorsed YOU");
                Tick(1.2);
                S.Set(SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockAiPercent.Default, SettingSources.File);

                // ---- 7. the master switch: no reel, no fall
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                r6 = impact.OwnRears.Count;
                d6 = impact.OwnDismounts.Count;
                PerfectBlock(0.0, 0.0);
                Tick();
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Check(impact.OwnRears.Count == r6 && impact.OwnDismounts.Count == d6 && logic.RiderReelsActive == 0, "the master switch off still knocked a rider off");

                // ---- 8. the [summary] line
                string line = st.SummaryLine(TimedRiderRules.From(S), true);
                Check(line.StartsWith("timed blocks on horseback - rear and knock-off (step 104c): LIVE", StringComparison.Ordinal)
                      && line.Contains("knock-offs decided 3 → applied 3 (yours 3) → SEEN off within 2.0 s 2") && line.Contains("not seen 1")
                      && line.Contains("rears decided 5 → applied 1") && line.Contains("his horse too weak (health below 2 - the blow could kill it) 1")
                      && line.Contains("the horses' health lost to our rear blows (after the give-back) 0 in 1"),
                    "the [summary] line: " + line);
                Console.WriteLine("        step 104c: knock-offs " + st.KnockOffsApplied + " (seen " + st.KnockOffsSeen + "), rears " + st.RearsApplied + " (seen " + st.RearsSeen + "), horse health lost " + st.HorseDamageSum);
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
                S.Set(SettingsSchema.ImpactShowMessages, SettingsSchema.ImpactShowMessages.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}
