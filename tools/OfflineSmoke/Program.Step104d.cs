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
    /// Offline check for step 104d - THE PERFECT STRIKE AND THE PUSH-OUT ON HORSEBACK (AI_NOTES "104d - built", DESIGN decision 93 (e)): through
    /// the REAL logic and the decorator's hooks - Toni's tournament, both mounted, your perfect block: his reel in the saddle opens a window and
    /// YOUR guard snaps out (his heavy parry on channel 1 - no bash, the guard back at its window's end); your hit on his HORSE is ignored and
    /// keeps the window; a hit into his shield does not use it; your swing from ANY side, from horseback, lands x3 (the gold line) - step 105
    /// still refuses him ("a rider" - no 80 % knockdown, no knock-off from the hit); a second hit is normal; you ON FOOT blocking a rider: the
    /// window too (any side), no push-out; you mounted blocking a man ON FOOT: the push-out (heavy parry), strict opposites from horseback; a
    /// knock-off = no window; the heavy parry missing = skipped and counted; the AI's mounted block of your blow: its push-out, no window; the
    /// switches (PerfectStrikeOnHorseback, TimedBlockPushOutMounted, the master switch); the [summary] lines.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HorsebackStrikeThroughTheLogic()
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
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockPushOut, true, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockPushOutForYou, true, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockRecoil, false, SettingSources.File);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.File);
                Check(SettingsSchema.PerfectStrikeOnHorseback.Default == 1 && SettingsSchema.PerfectStrikeHorsebackAnySide.Default == 1 && SettingsSchema.TimedBlockPushOutMounted.Default == 1,
                    "the embedded step-104d defaults are not the design's (all on)");
                var inputs = new FakeImpactInputs { Damage = 20 };
                var impact = new FakeImpactBody();
                var guard = new FakeGuardBody();
                var anim = new FakeAnimBody();
                var rb = new FakeRiderReelBody();
                var sink = new FakeMessageSink();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = impact;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.AnimBody = anim;
                logic.RiderReelBody = rb;
                logic.MessageSink = sink;
                logic.ImpactDice = new ScriptDice { Rest = 0.99 };
                var dice = new ScriptDice { Rest = 0.99 };
                logic.RiderShadowDice = dice;
                anim.Known["act_stagger_backward"] = (801, 1.02);
                const string guardName = "act_defend_shield_right_1h_passive", heavyName = "act_defend_shield_right_1h_parry_heavy";
                anim.Known[heavyName] = (901, 0.6);
                double t = 104700;
                logic.TickImpact(t);
                Check(ReferenceEquals(ImpactHooks.Perfect, logic), "the logic is not the decorator's perfect-strike hook");
                int next = 10470;
                var ps = logic.PerfectStrikeStats;
                var ta = logic.TimedAnimStats;

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
                    anim.Guard.Add(i);
                    anim.UpperName[i] = guardName;
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
                TrackedAgent You(bool rides)
                {
                    var y = Man(rides);
                    if (rides) rb.You.Add(y.AgentIndex);
                    logic.SmokePlayer = y.Agent;
                    logic.SmokePlayerAiControlled = false;
                    guard.Contacts[y.Agent.Index] = (GuardRead.Active, double.NaN);
                    return y;
                }
                // your perfect block of a rider's RIGHT swing; the rider dice: the L56 spread, the reel, the knock-off, the rear; the tick starts the reel
                TrackedAgent PerfectBlockOfARider(double knockOffDie = 0.99)
                {
                    Tick(3);
                    YourGuard(0.05);
                    var r = Man(true);
                    dice.Next(0.5, 0.0, knockOffDie, 0.99);
                    logic.ImpactModelHitTaken(r.Agent, logic.SmokePlayer!, Swing(Agent.UsageDirection.AttackRight, true, true, CombatCollisionResult.Blocked));
                    Tick();
                    return r;
                }
                float Hit(TrackedAgent you, TrackedAgent on, Agent.UsageDirection dir, bool mounted, float damage = 20f) =>
                    ImpactHooks.PerfectStrike(you.Agent, on.Agent, mounted, Swing(dir, false, false, CombatCollisionResult.StrikeAgent), damage);

                // ---- 1. Toni's tournament: both mounted, your perfect block - the window on the reeling rider, your guard snaps out
                var you = You(true);
                int plays = anim.Plays.Count;
                var r1 = PerfectBlockOfARider();
                Check(r1.Impact.ReelCause == ReelCause.RiderReel && r1.Impact.Reeling(t + 1.0) && logic.RiderReelsActive == 1, "the rider does not reel in the saddle");
                Check(ps.RiderOpened == 1 && logic.PerfectStrikeWindowsOpen == 1, "his reel opened no perfect-strike window: rider windows " + ps.RiderOpened);
                LogHas("[impact] perfect strike: first window on a RIDER at ");
                LogHas("ANY landed hit on his body counts (never his horse)");
                var yourPush = anim.Plays.Skip(plays).Where(p => p.Man == you.AgentIndex).ToList();
                Check(yourPush.Count == 1 && yourPush[0].Channel == 1 && yourPush[0].Index == 901 && ta.MountedPlayed == 1 && ta.MountedYou == 1 && logic.PushOutsRunning == 1,
                    "your mounted push-out (the heavy parry on channel 1): " + string.Join(", ", yourPush) + ", mounted played " + ta.MountedPlayed);
                Check(anim.Plays.Skip(plays).All(p => p.Channel == 1 && (p.Index == 801 || p.Index == 901)),
                    "something other than the reel / the heavy parry, or a channel other than 1, on a rider: " + string.Join(", ", anim.Plays.Skip(plays)));
                LogHas("live: first MOUNTED PUSH-OUT (step 104d)");
                LogHas("plays his guard's heavy parry " + heavyName + " on channel 1");
                // a hit on his HORSE: ignored, the window kept
                ((IPerfectStrike)logic).HorseHit(you.AgentIndex, r1.AgentIndex);
                Check(ps.HorseHits == 1 && logic.PerfectStrikeWindowsOpen == 1 && ps.RiderLanded == 0, "a hit on his horse used the window / was not counted");
                LogHas("[impact] perfect strike: first hit on a reeling rider's HORSE");
                // into his shield: not on his body - the window kept
                Check(Near(ImpactHooks.PerfectStrike(you.Agent, r1.Agent, true, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked), 5f), 5f)
                      && logic.PerfectStrikeWindowsOpen == 1, "a hit into his shield used the window");
                // step 104 follow-up (Toni 2026-11-09): a COUCHED LANCE on his body is never a perfect strike - normal damage, counted, the window kept
                Check(Near(ImpactHooks.PerfectStrike(you.Agent, r1.Agent, true, Swing(Agent.UsageDirection.AttackLeft, false, false, CombatCollisionResult.StrikeAgent), 20f, passiveAttack: true), 20f)
                      && ps.Couched == 1 && ps.RiderLanded == 0 && logic.PerfectStrikeWindowsOpen == 1, "a couched lance on the reeling rider counted as a perfect strike / used the window");
                // your LEFT swing from horseback (his blocked attack was a RIGHT swing - on foot a wrong side): ANY side on a rider - x3
                Tick(0.3);
                Check(Near(Hit(you, r1, Agent.UsageDirection.AttackLeft, mounted: true), 60f) && ps.RiderLanded == 1 && ps.RiderLandedFromHorseback == 1
                      && ps.WrongSideTotal == 0 && logic.PerfectStrikeWindowsOpen == 0,
                    "your left swing from horseback on the reeling rider did not land x3: rider landed " + ps.RiderLanded + ", wrong " + ps.WrongSideTotal);
                LogHas("YOU: PERFECT STRIKE on ");
                LogHas("your left swing from horseback on him reeling in the saddle (any side) after his blocked right swing");
                // the same hit through the impact model: step 105 refuses a rider - no 80 % knockdown, no knock-off from it
                int dismounts = impact.OwnDismounts.Count;
                logic.ImpactModelHitTaken(you.Agent, r1.Agent, Swing(Agent.UsageDirection.AttackLeft, false, false, CombatCollisionResult.StrikeAgent));
                var hit = logic.LastImpact;
                Check(hit.ReelingHit.Asked && hit.ReelingHit.Refusal == ReelingHitRefusal.Mounted && !hit.ReelingHit.Happens && !hit.Reeling && impact.OwnDismounts.Count == dismounts,
                    "a perfect strike on a reeling rider: step 105 " + hit.ReelingHit.Refusal + ", reeling bonus " + hit.Reeling);
                Check(Near(Hit(you, r1, Agent.UsageDirection.AttackRight, mounted: true), 20f) && ps.RiderLanded == 1, "a second strike on the same reel was perfect too");
                Tick();
                Check(sink.Lines.Any(l => l.Text == "PERFECT STRIKE x3.0" && l.Hex == PerfectStrikeMath.GoldHex), "the gold PERFECT STRIKE x3.0 line was not shown");
                // the push-out's window ends: the guard comes back (our parry cleared if still on his upper body), no lock left
                Tick(1.0);
                Check(logic.PushOutsRunning == 0 && anim.Clears.Contains(you.AgentIndex) && ta.Unstuck + ta.GuardsBack >= 1, "your mounted push-out did not end / clear");
                Tick(1.0);
                Check(logic.RiderReelsActive == 0, "the reel did not end");

                // ---- 2. you ON FOOT block a rider: the window too (any side, from foot); no push-out for a man on foot against a rider
                var foot = You(false);
                int mountedPlayed = ta.MountedPlayed, pushTotal = ta.PushTotal;
                var r2 = PerfectBlockOfARider();
                Check(ps.RiderOpened == 2 && logic.PerfectStrikeWindowsOpen == 1 && ta.PushTotal == pushTotal && ta.MountedPlayed == mountedPlayed,
                    "you on foot vs a rider: rider windows " + ps.RiderOpened + ", push-outs +" + (ta.PushTotal - pushTotal));
                Check(Near(Hit(foot, r2, Agent.UsageDirection.AttackUp, mounted: false), 60f) && ps.RiderLanded == 2 && ps.RiderLandedFromHorseback == 1,
                    "your overhead on foot on the reeling rider did not land x3 (any side)");
                Tick(3);

                // ---- 3. a knock-off (step 104c) = no window - he is down
                var r3 = PerfectBlockOfARider(knockOffDie: 0.0);
                Check(logic.RiderFallStats.KnockOffsApplied >= 1 && ps.RiderKnockedOffNoWindow == 1 && logic.PerfectStrikeWindowsOpen == 0 && ps.RiderOpened == 2,
                    "a knock-off opened a window: knocked off " + ps.RiderKnockedOffNoWindow + ", open " + logic.PerfectStrikeWindowsOpen);
                inputs.Mounted.Remove(r3.AgentIndex);
                anim.Riding.Remove(r3.AgentIndex);
                rb.Riding.Remove(r3.AgentIndex);
                Tick(3);

                // ---- 4. you MOUNTED block a man ON FOOT: the riposte as before + your push-out (heavy parry); from horseback strict opposites
                you = You(true);
                Tick(3);
                YourGuard(0.05);
                var a4 = Man(false);
                mountedPlayed = ta.MountedPlayed;
                int opened = ps.Opened;
                logic.ImpactModelHitTaken(a4.Agent, you.Agent, Swing(Agent.UsageDirection.AttackRight, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.LastTimedBlock.RiposteHappens && ps.Opened == opened + 1 && ps.RiderOpened == 2 && ta.MountedPlayed == mountedPlayed + 1,
                    "your mounted timed block of a man on foot: riposte " + logic.LastTimedBlock.RiposteHappens + ", windows +" + (ps.Opened - opened) + ", mounted push +"
                    + (ta.MountedPlayed - mountedPlayed));
                Check(Near(Hit(you, a4, Agent.UsageDirection.AttackLeft, mounted: true), 20f) && ps.FootWrongSideFromHorseback == 1 && logic.PerfectStrikeWindowsOpen == 1,
                    "your wrong-side swing from horseback on a man on foot was not a wrong side (strict opposites on foot)");
                Check(Near(Hit(you, a4, Agent.UsageDirection.AttackRight, mounted: true), 60f) && ps.FootLandedFromHorseback == 1,
                    "your right swing from horseback after his blocked right swing did not land x3");
                Tick(3);

                // ---- 5. the heavy parry missing for his guard: skipped, counted apart - never a bash on a rider
                anim.UpperName[you.AgentIndex] = "act_horseback_ready_1h"; // no defend action to take it from
                int noParry = ta.MountedSkipped[(int)PushOutSkip.MountedNoParry];
                plays = anim.Plays.Count;
                PerfectBlockOfARider();
                Check(ta.MountedSkipped[(int)PushOutSkip.MountedNoParry] == noParry + 1 && !anim.Plays.Skip(plays).Any(p => p.Man == you.AgentIndex),
                    "a missing heavy parry was not skipped / something else played: " + ta.MountedSkipped[(int)PushOutSkip.MountedNoParry]);
                anim.UpperName[you.AgentIndex] = guardName;
                Tick(3);

                // ---- 6. the AI's mounted block of YOUR mounted blow: its push-out (heavy parry), no window on you
                S.Set(SettingsSchema.TimedBlockAiPercent, 100, SettingSources.File);
                var aiBlocker = Man(true);
                guard.Contacts[aiBlocker.Agent.Index] = (GuardRead.Active, 0.0);
                int openedAi = ps.Opened;
                mountedPlayed = ta.MountedPlayed;
                dice.Next(0.5, 0.0);
                logic.ImpactModelHitTaken(you.Agent, aiBlocker.Agent, Swing(Agent.UsageDirection.AttackUp, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.RiderReelsActive == 1 && you.Impact.Reeling(t) && ta.MountedPlayed == mountedPlayed + 1 && ps.Opened == openedAi,
                    "the AI's mounted block of your blow: reels " + logic.RiderReelsActive + ", its push +" + (ta.MountedPlayed - mountedPlayed) + ", windows +" + (ps.Opened - openedAi));
                S.Set(SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockAiPercent.Default, SettingSources.File);
                Tick(3);

                // ---- 7. the switches: PerfectStrikeOnHorseback off = no rider window, a strike from horseback not counted; off mid-window = closed
                S.Set(SettingsSchema.PerfectStrikeOnHorseback, false, SettingSources.File);
                int riderOpened = ps.RiderOpened;
                var r7 = PerfectBlockOfARider();
                Check(ps.RiderOpened == riderOpened && logic.PerfectStrikeWindowsOpen == 0 && Near(Hit(you, r7, Agent.UsageDirection.AttackLeft, mounted: true), 20f),
                    "PerfectStrikeOnHorseback off still opened a rider window / multiplied");
                S.Set(SettingsSchema.PerfectStrikeOnHorseback, true, SettingSources.File);
                Tick(3);
                var r8 = PerfectBlockOfARider();
                Check(logic.PerfectStrikeWindowsOpen == 1, "no rider window before the mid-window switch test");
                S.Set(SettingsSchema.PerfectStrikeOnHorseback, false, SettingSources.File);
                Check(Near(Hit(you, r8, Agent.UsageDirection.AttackLeft, mounted: true), 20f), "the switch off mid-window still multiplied");
                Tick();
                Check(logic.PerfectStrikeWindowsOpen == 0 && ps.RiderClosedBySwitch == 1, "the switch off did not close the rider window");
                S.Set(SettingsSchema.PerfectStrikeOnHorseback, true, SettingSources.File);
                Tick(3);
                // TimedBlockPushOutMounted off: a riding blocker skipped "mounted"
                S.Set(SettingsSchema.TimedBlockPushOutMounted, false, SettingSources.File);
                int skippedMounted = ta.MountedSkipped[(int)PushOutSkip.Mounted];
                mountedPlayed = ta.MountedPlayed;
                PerfectBlockOfARider();
                Check(ta.MountedSkipped[(int)PushOutSkip.Mounted] == skippedMounted + 1 && ta.MountedPlayed == mountedPlayed, "TimedBlockPushOutMounted off still pushed out a rider");
                S.Set(SettingsSchema.TimedBlockPushOutMounted, true, SettingSources.File);
                Tick(3);
                // the master switch: the window closed at the next tick, no multiplier
                var r9 = PerfectBlockOfARider();
                Check(logic.PerfectStrikeWindowsOpen == 1, "no rider window before the master switch test");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Check(Near(Hit(you, r9, Agent.UsageDirection.AttackLeft, mounted: true), 20f), "the MASTER SWITCH off still multiplied");
                Tick();
                Check(logic.PerfectStrikeWindowsOpen == 0 && logic.RiderReelsActive == 0, "the master switch off left a rider window / a reel");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick(3);

                // ---- 8. the [summary] lines
                string strikeLine = ps.MountedSummaryLine(PerfectStrikeRules.From(S));
                Check(strikeLine.StartsWith("perfect strike on horseback (step 104d; ON, any side on a rider): windows on riders reeling in the saddle after your timed block ", StringComparison.Ordinal)
                      && strikeLine.Contains("PERFECT STRIKES on them 2 (you riding 1, on foot 1)") && strikeLine.Contains("(none - knocked off his horse 1)")
                      && strikeLine.Contains("your hits on such a rider's HORSE (ignored, the window kept) 1")
                      && strikeLine.Contains("your strikes from horseback on men on foot (strict opposites): landed 1, wrong side 1"),
                    "the [summary] perfect strike on horseback line: " + strikeLine);
                string pushLine = ta.SummaryLines(TimedAnimRules.From(S))[0];
                Check(pushLine.Contains("; mounted blockers (step 104d, his guard's heavy parry - on): played ") && pushLine.Contains("mounted - no heavy parry for his guard 1")
                      && pushLine.Contains("mounted (the mounted push-out off) 1"),
                    "the [summary] push-out line's mounted part: " + pushLine);
                Console.WriteLine("        step 104d: " + ps.RiderOpened + " rider windows, " + ps.RiderLanded + " perfect strikes on riders, " + ta.MountedPlayed + " mounted push-outs");
                Console.WriteLine("        " + strikeLine);
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
