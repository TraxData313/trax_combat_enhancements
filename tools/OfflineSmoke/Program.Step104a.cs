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
    /// Offline check for step 104a - TIMED BLOCKS ON HORSEBACK, THE EYES FIRST (AI_NOTES "Step 104", DESIGN decision 93): through the REAL logic -
    /// your perfect block of a rider's blow while you ride: the live decision still refuses the riposte (a rider), NOTHING is applied (no stagger,
    /// no state on him, no action), the SHADOW says he WOULD reel 1.5 s with the horse at half speed and rolls the knock-off / the rear from its own
    /// dice; a man on foot blocking a rider; an AI rider's timed block of YOU (1.0 s cap, never a rear / knock-off); a rider blocking a man on foot
    /// (104d's notes); an untimed block = today's refusal, no die; the master switch; the J test tool on a RIDER - the nearest man rides / you ride
    /// alone: the 16 rider candidates, every action on channel 1 only, the rear through our blow on his horse, followed (SEEN rearing); the
    /// [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TimedRiderShadowThroughTheLogic()
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
                                                             || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.File);
                // step 104b turned the switch ON (defaults.json): 104a's shadow is checked with it OFF - nothing applied
                S.Set(SettingsSchema.TimedBlocksOnHorseback, false, SettingSources.File);
                Check(!TraxSettings.Shared.TimedBlocksOnHorseback, "TimedBlocksOnHorseback did not turn off for the shadow's check");
                var inputs = new FakeImpactInputs { Damage = 20 };
                var impact = new FakeImpactBody();
                var guard = new FakeGuardBody();
                var anim = new FakeAnimBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = impact;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.AnimBody = anim;
                var live = new ScriptDice { Rest = 0.5 };
                logic.ImpactDice = live;
                var shadowDice = new ScriptDice { Rest = 0.5 };
                logic.RiderShadowDice = shadowDice;
                Check(!ReferenceEquals(logic.RiderShadowDice, logic.ImpactDice), "the shadow shares the live dice");
                anim.Known["act_stagger_backward"] = (801, 1.02);
                anim.Known["act_blocked_overswing_horseback"] = (802, 0.55);
                anim.Known["act_strike2_chest_front"] = (803, 0.6);
                anim.Known["act_shield_bash"] = (601, 1.0);
                double t = 104000;
                logic.TickImpact(t);
                int next = 10400;
                var st = logic.RiderShadowStats;

                TrackedAgent Man(bool shield, bool rides)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, 1.0);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (shield ? 4.0 : 1.2, 100);
                    if (shield)
                    {
                        inputs.Shields.Add(i);
                        anim.Shields.Add(i);
                    }
                    anim.Usage[i] = "onehanded_shield_swing";
                    if (rides)
                    {
                        inputs.Mounted.Add(i);
                        inputs.Speeds[i] = 6.5; // a canter
                        impact.Mounted.Add(i);
                        anim.Riding.Add(i);
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
                var you = Man(true, true);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);

                // ---- 1. Toni's tournament: both mounted, your PERFECT block - live refused (a rider), the shadow WOULD reel him 1.5 s
                YourGuard(0.05);
                var r1 = Man(false, true);
                int staggers = impact.Staggers.Count, plays = anim.Plays.Count, rears = impact.OwnRears.Count, dismounts = impact.OwnDismounts.Count;
                shadowDice.Next(0.5, 0.9, 0.9, 0.2); // the L56 spread, the reel (≤ 1), the knock-off (0.9 > ~9 %), the rear (0.2 < 50 %)
                logic.ImpactModelHitTaken(r1.Agent, you.Agent, Swing(Agent.UsageDirection.AttackRight, true, true, CombatCollisionResult.Blocked));
                Tick();
                var d = logic.LastImpact;
                Check(d.TimedScore > 0.999 && d.Riposte.InRow && d.Riposte.Refusal == ImpactRefusal.Rider && !d.Riposte.Happens,
                    "the live decision on a rider is not today's refusal: score " + d.TimedScore + ", " + d.Riposte.Refusal);
                Check(impact.Staggers.Count == staggers && anim.Plays.Count == plays && impact.OwnRears.Count == rears && impact.OwnDismounts.Count == dismounts && !r1.Impact.Reeling(t),
                    "the shadow APPLIED something on the rider (a stagger, an action, a rear, a dismount or a reel on his record)");
                var o = logic.LastRiderShadow;
                Check(o.Combo == TimedRiderCombo.RiderVsRider && o.Reel && Math.Abs(o.ReelSeconds - 1.5) < 1e-9 && Math.Abs(o.HorseCheck - 0.5) < 1e-9
                      && Math.Abs(o.AnimSeconds - 1.0) < 1e-9 && o.DismountChance > 0.05 && o.DismountChance < 0.15 && !o.Dismount && o.Rear
                      && Math.Abs(o.StrikeWindow - 1.65) < 1e-9,
                    "the shadow of your perfect block: " + TimedRiderMath.Describe(in o, "r1", "you"));
                LogHas("[impact] first rider shadow at ");
                LogHas("WOULD reel 1.50 s");

                // ---- 2. a man ON FOOT (you) blocks a rider - the same branch (TimedRiderFromFoot on); off = refused
                Tick(3);
                inputs.Mounted.Remove(you.AgentIndex);
                impact.Mounted.Remove(you.AgentIndex);
                anim.Riding.Remove(you.AgentIndex);
                YourGuard(0.05);
                var r2 = Man(false, true);
                shadowDice.Next(0.5, 0.5, 0.9, 0.9);
                logic.ImpactModelHitTaken(r2.Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.LastRiderShadow.Combo == TimedRiderCombo.FootVsRider && logic.LastRiderShadow.Reel, "a man on foot's perfect block of a rider would not reel him");
                S.Set(SettingsSchema.TimedRiderFromFoot, false, SettingSources.File);
                Tick(3);
                YourGuard(0.05);
                logic.ImpactModelHitTaken(Man(false, true).Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.LastRiderShadow.Refusal == TimedRiderRefusal.FromFootOff, "TimedRiderFromFoot off did not refuse a foot blocker");
                S.Set(SettingsSchema.TimedRiderFromFoot, true, SettingSources.File);

                // ---- 3. an untimed (held) block = today's refusal, no shadow die
                Tick(3);
                guard.Keys[you.Agent.Index] = 0x800;
                Tick(2.0); // held 2 s
                int calls = shadowDice.Calls;
                logic.ImpactModelHitTaken(Man(false, true).Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.LastRiderShadow.Refusal == TimedRiderRefusal.NotTimed && shadowDice.Calls == calls, "a held block rolled the shadow");

                // ---- 4. an AI's timed block of YOUR mounted blow: the reel at most 1.0 s, never a rear, never knocked off
                Tick(3);
                inputs.Mounted.Add(you.AgentIndex);
                impact.Mounted.Add(you.AgentIndex);
                anim.Riding.Add(you.AgentIndex);
                inputs.Speeds[you.AgentIndex] = 9;
                S.Set(SettingsSchema.TimedBlockAiPercent, 100, SettingSources.File); // even a perfect AI block
                var aiBlocker = Man(true, true);
                guard.Contacts[aiBlocker.Agent.Index] = (GuardRead.Active, 0.0);
                shadowDice.Next(0.5, 0.0);
                logic.ImpactModelHitTaken(you.Agent, aiBlocker.Agent, Swing(Agent.UsageDirection.AttackUp, true, true, CombatCollisionResult.Blocked));
                Tick();
                o = logic.LastRiderShadow;
                Check(o.Reel && o.ReelSeconds <= 1.0 + 1e-9 && o.CappedForYou && !o.Rear && !o.Dismount && double.IsNaN(o.RearChance) && double.IsNaN(o.DismountChance),
                    "the AI's block on you: " + TimedRiderMath.Describe(in o, "you", "AI"));
                S.Set(SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockAiPercent.Default, SettingSources.File);

                // ---- 5. a rider blocking a man on foot: today's riposte stands; the shadow notes 104d (the mounted push-out)
                Tick(3);
                var footA = Man(false, false);
                logic.ImpactModelHitTaken(footA.Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.LastRiderShadow.Combo == TimedRiderCombo.RiderVsFoot, "a rider's block of a man on foot was not noted");

                // ---- 6. the master switch: no shadow at all
                Tick(3);
                int asked = st.ByCombo.Sum();
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                YourGuard(0.05);
                logic.ImpactModelHitTaken(Man(false, true).Agent, you.Agent, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked));
                Tick();
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Check(st.ByCombo.Sum() == asked, "the MASTER SWITCH off still ran the rider shadow");

                // ---- 7. the J test tool on a RIDER: the nearest man rides - the 16 rider candidates, channel 1 only; the rear followed
                Tick(3);
                S.Set(SettingsSchema.TimedBlockAnimTest, true, SettingSources.File);
                var rider = Man(true, true);
                anim.Near = rider.Agent;
                int playsBefore = anim.Plays.Count;
                int n = TimedRiderAnimMath.Candidates.Length;
                for (int k = 0; k < n; k++)
                {
                    anim.Presses = 1;
                    Tick();
                }
                var said = anim.Said.Where(x => x.StartsWith("[anim test RIDER ", StringComparison.Ordinal)).ToList();
                Check(said.Count >= n && said[0].StartsWith("[anim test RIDER 1/16]", StringComparison.Ordinal) && said[0].Contains("act_stagger_backward") && said[0].Contains("taken"),
                    "the first rider candidate: " + string.Join(" | ", said.Take(2)));
                Check(said.Any(x => x.Contains("act_blocked_overswing_horseback") && x.Contains("taken")), "the mounted overhead recoil was not played: " + string.Join(" | ", said));
                Check(said.Any(x => x.Contains("act_strike2_chest_front") && x.Contains("taken")), "the strike flinch was not played");
                var riderPlays = anim.Plays.Skip(playsBefore).Where(p => p.Man == rider.AgentIndex).ToList();
                Check(riderPlays.Count >= 4 && riderPlays.All(p => p.Channel == TimedRiderAnimMath.RiderChannel), "an action played on a rider's channel 0: " + string.Join(", ", riderPlays));
                Check(anim.Plays.Skip(playsBefore).All(p => p.Channel == 1), "the rider tool touched a channel other than 1");
                Check(impact.OwnRears.Count == rears + 1 && impact.OwnRears.Last().Rider == rider.AgentIndex, "the rear candidate did not register our blow on his horse");
                Tick(0.1);
                Check(anim.Said.Any(x => x.Contains("SEEN rearing")), "the rear was not followed: " + anim.Said.Last());
                LogHas("anim test tool, RIDER candidates (step 104a)");
                // Left Shift goes back; the foot list stays the foot list for a man on foot
                anim.Shift = true;
                anim.Presses = 1;
                Tick();
                Check(anim.Said.Last().StartsWith("[anim test RIDER 15/16]", StringComparison.Ordinal), "Left Shift did not go back on the rider list: " + anim.Said.Last());
                anim.Shift = false;
                anim.Near = Man(true, false).Agent;
                anim.Presses = 1;
                Tick();
                Check(anim.Said.Last().StartsWith("[anim test 1/26]", StringComparison.Ordinal), "a man on foot did not get the foot candidates: " + anim.Said.Last());
                // you ride, nobody near: the rider candidates on you
                anim.Near = null;
                anim.Presses = 1;
                Tick();
                Check(anim.Said.Last().StartsWith("[anim test RIDER 16/16]", StringComparison.Ordinal) && anim.Said.Last().Contains("(you) on his horse"), "you mounted alone did not get the rider candidates: " + anim.Said.Last());
                S.Set(SettingsSchema.TimedBlockAnimTest, false, SettingSources.File);

                // ---- 8. the [summary] line
                string line = st.SummaryLine(TimedRiderRules.From(S));
                Check(line.StartsWith("timed blocks on horseback (shadow, step 104a): the switch OFF - NOTHING APPLIED; correct blocks of a rider's blow ", StringComparison.Ordinal)
                      && line.Contains("rider vs rider 2 / foot vs rider 3") && line.Contains("untimed (today's refusal) 1") && line.Contains("from foot off 1")
                      && line.Contains("a rider blocking a man on foot 1"),
                    "the [summary] line: " + line);
                Console.WriteLine("        step 104a: " + st.Reels + " shadow reels of " + st.Rolled + " rolled, " + st.Rears + " rears, " + st.Dismounts + " knocked off - nothing applied");
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
                                                             || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, SettingsSchema.ImpactOnPlayerPercent.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}
