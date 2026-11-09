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
    /// Offline check for step 105 - A HIT ON A REELING MAN KNOCKS HIM DOWN (AI_NOTES "Step 105", DESIGN decision 94): through the REAL logic and
    /// the damage decorator's KnockDown hook - Toni's case (your perfect block, he reels, your swing lands: KnockDown answered, "You knock X
    /// down" from the tick; the die above 80 % - he stays up); an AI man's kick on a reeling AI man; a man who does not reel is decided as
    /// before; you as the victim (x ImpactOnPlayerPercent); a rider refused; a killing blow just kills (never decided); a blow into his shield
    /// never counts; the switch and the master switch; the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReelingHitThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("TimedBlock", StringComparison.Ordinal) || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal)
                                                             || p.Key.StartsWith("ReelingHit", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, false, SettingSources.File); // the kick's guard read is the engine's (step 82 has its own smoke)
                S.Set(SettingsSchema.ImpactShowMessages, true, SettingSources.File);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.File);
                Check(SettingsSchema.ReelingHitKnockdownEnabled.Default == 1 && SettingsSchema.ReelingHitKnockdownPercent.Default == 80,
                    "the embedded reeling-hit defaults are not the design's (on, 80 %)");
                var inputs = new FakeImpactInputs { Damage = 20 };
                var guard = new FakeGuardBody();
                var sink = new FakeMessageSink();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.MessageSink = sink;
                var dice = new ScriptDice { Rest = 0.5 };
                logic.ImpactDice = dice;
                double t = 105000;
                logic.TickImpact(t);
                Check(ReferenceEquals(ImpactHooks.KnockDown, logic), "the logic is not the decorator's KnockDown hook");
                int next = 10500;
                var st = logic.ReelingHitStats;

                TrackedAgent Man(bool shield)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, 1.0);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (shield ? 4.0 : 1.2, 100);
                    if (shield) inputs.Shields.Add(i);
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                // a landed blow as the engine runs it: the decorator's KnockDown question, then OnMeleeHit's part
                bool Lands(TrackedAgent a, TrackedAgent v, AttackCollisionData cd)
                {
                    bool down = ImpactHooks.KnockDownOnBlow(a.Agent, v.Agent, in cd, default, false);
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                    return down;
                }
                var you = Man(true);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);

                // ---- 1. Toni's case: your perfect block makes him reel; your swing lands - he is knocked down (the die 0.5 < 0.8)
                guard.Keys[you.Agent.Index] = 0;
                Tick(0.1);
                guard.Keys[you.Agent.Index] = 0x800;
                Tick(0.05);
                var a1 = Man(false);
                logic.ImpactModelHitTaken(a1.Agent, you.Agent, Swing(Agent.UsageDirection.AttackRight, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.LastTimedBlock.RiposteHappens && a1.Impact.Reeling(t), "your perfect block did not make him reel");
                Check(a1.Impact.ReelCause == ReelCause.TimedRiposte, "his reel's cause is not the timed riposte: " + a1.Impact.ReelCause);
                Tick(0.2);
                dice.Rest = 0.5;
                Check(Lands(you, a1, CleanHit()), "your swing on the reeling man was not answered KnockDown (Toni's case)");
                Check(st.DownsTotal == 1 && st.Downs[(int)ReelingHitKind.Swing] == 1 && st.DownsByCause[(int)ReelCause.TimedRiposte] == 1 && st.ByYouDown == 1,
                    "the knockdown was not counted as your swing on a man reeling from a timed riposte: " + st.DownsTotal);
                Check(logic.LastImpact.Knockdown.Happens && Math.Abs(logic.LastImpact.ReelingHit.Chance - 0.8) < 1e-9, "the decision is not the 80 % roll");
                LogHas("[impact] first REELING man knocked down at ");
                Check(a1.Impact.Down(t) && !a1.Impact.Reeling(t), "the fallen man is not on the ground in his record");
                Tick(2.0); // the on-screen lines: one per ImpactMessageMinSeconds 1.5 (the riposte's line went first)
                Check(sink.Lines.Any(l => l.Text.StartsWith("You knock ", StringComparison.Ordinal) && l.Text.EndsWith(" down", StringComparison.Ordinal)),
                    "no 'You knock X down' line: " + string.Join(" | ", sink.Lines.Select(l => l.Text)));
                // a second hit on the man now down: not a reeling hit (Down - the usual decision)
                int hitsBefore = st.HitsTotal;
                Lands(you, a1, CleanHit());
                Check(st.HitsTotal == hitsBefore, "a hit on the man already DOWN counted as a hit on a reeling man");

                // ---- 2. the die above 80 %: he stays up (the usual chance of a fresh even man is far below it)
                Tick(6);
                var a2 = Man(false);
                a2.Impact.Reel(t + 1.0, 2, ReelCause.HitStagger);
                dice.Rest = 0.9;
                Check(!Lands(you, a2, CleanHit()) && st.Rolled == 2 && st.RolledUp == 1, "the die 0.9 knocked the reeling man down / was not rolled: rolled " + st.Rolled);

                // ---- 3. an AI man's KICK on a reeling AI man - anyone on anyone; his knockdown immunity and recent outcomes ignored
                var k1 = Man(false);
                var k2 = Man(true);
                k2.Impact.Reel(t + 1.0, 2, ReelCause.Bash);
                k2.Impact.KnockdownImmuneUntil = t + 10;
                k2.Impact.NoteOutcome(t - 0.5);
                dice.Rest = 0.5;
                Check(Lands(k1, k2, KickLands()) && st.Downs[(int)ReelingHitKind.Kick] == 1 && st.DownsByCause[(int)ReelCause.Bash] == 1,
                    "an AI man's kick on a reeling, knockdown-immune AI man did not knock him down");

                // ---- 4. a man who does not reel: the usual decision (no reeling roll)
                var n1 = Man(false);
                int asked = st.HitsTotal;
                Check(!Lands(k1, n1, CleanHit()) && st.HitsTotal == asked && !logic.LastImpact.ReelingHit.Asked, "a hit on a man who does not reel was given the reeling roll");

                // ---- 5. YOU as the victim, x ImpactOnPlayerPercent: 50 % → 40 % (the die 0.45 keeps you up, 0.35 knocks you down); 0 % = never
                Tick(6);
                var ai = Man(false);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 50, SettingSources.File);
                you.Impact.Clear();
                you.Impact.Reel(t + 1.0, 2, ReelCause.Riposte);
                dice.Rest = 0.45;
                Check(!Lands(ai, you, CleanHit()) && Math.Abs(logic.LastImpact.ReelingHit.Chance - 0.4) < 1e-9, "you at 50 % were not rolled at 40 %: " + logic.LastImpact.ReelingHit.Chance);
                dice.Rest = 0.35;
                Check(Lands(ai, you, CleanHit()) && st.OnYouDown == 1, "you, reeling, were not knocked down at 40 % by the die 0.35");
                Tick(2.0);
                Check(sink.Lines.Any(l => l.Text.StartsWith("You are knocked down", StringComparison.Ordinal)), "no 'You are knocked down' line");
                Tick(8);
                you.Impact.Clear();
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 0, SettingSources.File);
                you.Impact.Reel(t + 1.0, 2, ReelCause.Riposte);
                dice.Rest = 0.0;
                Check(!Lands(ai, you, CleanHit()) && st.Refused[(int)ReelingHitRefusal.YouExempt] == 1, "you at 0 % were knocked down by a reeling hit");
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.File);
                you.Impact.Clear();

                // ---- 6. a RIDER: refused (step 104's seam); a killing blow: he just dies; a blow into his shield: never counted
                var r1 = Man(false);
                inputs.Mounted.Add(r1.Agent.Index);
                r1.Impact.Reel(t + 1.0, 2, ReelCause.Kick);
                logic.ImpactModelHitTaken(k1.Agent, r1.Agent, CleanHit());
                Check(st.Refused[(int)ReelingHitRefusal.Mounted] == 1, "a reeling rider was not refused");
                inputs.Mounted.Remove(r1.Agent.Index);
                var f1 = Man(false);
                f1.Impact.Reel(t + 1.0, 2, ReelCause.HitStagger);
                inputs.Fatal = true;
                Check(!Lands(k1, f1, CleanHit()) && st.Refused[(int)ReelingHitRefusal.Fatal] == 1, "a killing blow on a reeling man was decided (he must just die)");
                inputs.Fatal = false;
                var s1 = Man(true);
                s1.Impact.Reel(t + 1.0, 2, ReelCause.HitStagger);
                int hits = st.HitsTotal;
                logic.ImpactModelHitTaken(k1.Agent, s1.Agent, RightBlock());
                Check(st.HitsTotal == hits, "a blow into his shield counted as a hit on a reeling man");

                // ---- 7. the switch and the master switch
                var o1 = Man(false);
                o1.Impact.Reel(t + 1.0, 2, ReelCause.Kick);
                S.Set(SettingsSchema.ReelingHitKnockdownEnabled, false, SettingSources.File);
                dice.Rest = 0.5;
                Check(!Lands(k1, o1, CleanHit()) && st.Refused[(int)ReelingHitRefusal.Off] == 1, "the switch off still knocked the reeling man down");
                S.Set(SettingsSchema.ReelingHitKnockdownEnabled, true, SettingSources.File);
                Tick(6);
                var o2 = Man(false);
                o2.Impact.Reel(t + 1.0, 2, ReelCause.Kick);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                int before = st.HitsTotal;
                Check(!Lands(k1, o2, CleanHit()) && st.HitsTotal == before, "the MASTER SWITCH off still decided a reeling hit");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);

                // ---- 8. the [summary] line
                string line = st.SummaryLine(ImpactRules.From(S));
                Check(line.StartsWith("hits on reeling men (step 105; ON, 80 %): ", StringComparison.Ordinal) && line.Contains("KNOCKED DOWN 3")
                      && line.Contains("refused: the switch off 1, a rider 1, you at 0 % 1, killed by it 1") && line.Contains("a timed riposte 1/1"),
                    "the [summary] line: " + line);
                Console.WriteLine("        step 105: " + st.HitsTotal + " hits on reeling men, " + st.DownsTotal + " knocked down");
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
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("TimedBlock", StringComparison.Ordinal) || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal)
                                                             || p.Key.StartsWith("ReelingHit", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, SettingsSchema.ImpactMeetRulesEnabled.Default, SettingSources.File);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, SettingsSchema.ImpactOnPlayerPercent.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}
