using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 100b - TIMED BLOCKS (AI_NOTES "Step 100", DESIGN decision 91): through the REAL logic with a stand-in of the guard
    /// reads (<see cref="IGuardBody"/>) - your clock fed by the tick, a perfect press lifting the riposte to 85 % / 2 s and the stagger made from
    /// the tick; a held guard = step 56; the automatic block direction vs a manual one; the AI's active / held / unknown reads at half strength;
    /// the AI timing YOUR blow held at 1.2 s; the switch and the master switch = no clock reads, untimed; the [summary] lines.
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeGuardBody : IGuardBody
        {
            public readonly Dictionary<int, int> Keys = new Dictionary<int, int>();
            public readonly Dictionary<int, (GuardRead Read, double Age)> Contacts = new Dictionary<int, (GuardRead, double)>();
            public bool Automatic;
            public int KeyReads;

            public int GuardKey(Agent a)
            {
                KeyReads++;
                return Keys.TryGetValue(a.Index, out int k) ? k : 0;
            }

            public bool AutomaticDirection => Automatic;

            public GuardRead ReadContact(Agent victim, out double activeAge, out double length, out bool cyclic, out bool continues)
            {
                activeAge = double.NaN;
                length = double.NaN;
                cyclic = false;
                continues = false;
                if (!Contacts.TryGetValue(victim.Index, out var c)) return GuardRead.NotDefend;
                if (c.Read == GuardRead.Active)
                {
                    activeAge = c.Age;
                    length = 0.4;
                    continues = true;
                }
                return c.Read;
            }

            // step 102: the AI guard clock's watch - who has an enemy near, the hooks and releases asked
            public readonly HashSet<int> Near = new HashSet<int>();
            public int Hooks, Releases, WatchReads;

            public bool WatchWanted(TrackedAgent st, float metres)
            {
                WatchReads++;
                return Near.Contains(st.Agent.Index);
            }

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                Hooks++;
                return AiInputHook.HookResult.FirstHookTurnedOn;
            }

            public void Release(TrackedAgent st) => Releases++;
        }

        /// <summary>Step 100b: every logic built from here on reads the guard through the stand-in (the game's reads native agents).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeGuardBody()
        {
            Check(GuardBodyDefault.Current is GameGuardBody, "the default guard body is not the game's: " + GuardBodyDefault.Current.GetType().Name);
            GuardBodyDefault.Current = new FakeGuardBody();
            Check(new AthleticsLogic().GuardBody == GuardBodyDefault.Current, "a new logic does not start with the default guard body");
            Check(TimedBlockMath.DefendDirectionAutomatic == 2, "the automatic block direction is not 2");
            Check((int)Agent.MovementControlFlag.DefendLeft == 0x400 && (int)Agent.MovementControlFlag.DefendRight == 0x800
                  && (int)Agent.MovementControlFlag.DefendUp == 0x1000 && (int)Agent.MovementControlFlag.DefendDown == 0x2000,
                "the game's defend flags moved");
            Check((int)Agent.ActionStage.Defend == TimedBlockMath.StageDefend && (int)Agent.ActionStage.DefendParry == TimedBlockMath.StageDefendParry,
                "the game's defend stages moved");
            Check((int)CombatCollisionResult.Blocked == BlockMath.ResultBlocked && (int)CombatCollisionResult.Parried == BlockMath.ResultParried
                  && (int)CombatCollisionResult.ChamberBlocked == BlockMath.ResultChamberBlocked, "the game's collision results moved");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TimedBlocksThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                Check(SettingsSchema.TimedBlocksEnabled.Default == 1 && Near((float)SettingsSchema.TimedBlockPerfectSeconds.Default, 0.15f)
                      && Near((float)SettingsSchema.TimedBlockWindowSeconds.Default, 0.5f) && SettingsSchema.TimedBlockRiposteMaxPercent.Default == 85
                      && Near((float)SettingsSchema.TimedBlockStaggerMaxSeconds.Default, 2.0f) && Near((float)SettingsSchema.TimedBlockAgainstYouMaxSeconds.Default, 1.2f)
                      && SettingsSchema.TimedBlockParryPercent.Default == 100 && SettingsSchema.TimedBlockAiPercent.Default == 50,
                    "the embedded timed-block defaults are not the design's");
                foreach (var p in new[] { SettingsSchema.TimedBlocksEnabled, SettingsSchema.TimedBlockPerfectSeconds, SettingsSchema.TimedBlockWindowSeconds,
                             SettingsSchema.TimedBlockRiposteMaxPercent, SettingsSchema.TimedBlockStaggerMaxSeconds, SettingsSchema.TimedBlockAgainstYouMaxSeconds,
                             SettingsSchema.TimedBlockParryPercent, SettingsSchema.TimedBlockAiPercent })
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.RiposteStunMaxSeconds, 1.0, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var guard = new FakeGuardBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                var dice = new ScriptDice { Rest = 0.5 }; // a 0.85 riposte happens, a step-56 ~1 % one does not
                logic.ImpactDice = dice;
                double t = 70000;
                logic.TickImpact(t);
                int next = 6200;

                TrackedAgent Man(double bar, bool shield)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, bar);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (shield ? 4.0 : 1.2, 100);
                    if (shield) inputs.Shields.Add(i);
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                ImpactDecision Hit(TrackedAgent a, TrackedAgent v, AttackCollisionData cd)
                {
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, cd);
                    return logic.LastImpact;
                }

                var you = Man(1.0, true);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN); // the engine's read at contact (the diagnostic's)

                // ---- 1. YOUR perfect press: the guard up 0.05 s before a fresh attacker's blow - 100 % since step 101 (85 + 15 points), 2 s, made from the tick
                Tick(0.5);
                int reads = guard.KeyReads;
                Check(reads > 0, "your clock is not read every tick");
                guard.Keys[you.Agent.Index] = 0x800;
                Tick(0.05);
                var a1 = Man(1.0, false);
                var d = Hit(a1, you, RightBlock());
                Check(d.TimedSource == TimingSource.PlayerClock && Close(d.TimedScore, 1, 1e-9) && Close(d.Riposte.Chance, 1.0, 1e-6) && d.Riposte.Happens
                      && Close(d.RiposteStunSeconds, 2.0, 1e-6) && d.RiposteChance56 < 0.05,
                    "your perfect block: source " + d.TimedSource + ", age " + d.TimedAge + ", score " + d.TimedScore + ", riposte " + d.Riposte.Chance + " (untimed " + d.RiposteChance56 + "), " + d.RiposteStunSeconds + " s");
                Check(logic.LastTimedBlock.Timed && logic.LastTimedBlock.Band == 4 && logic.LastTimedBlock.BlockerIsPlayer, "the timed block's result (the seam) not written");
                Tick();
                Check(body.Staggers.Exists(s => s.Man == a1.Agent.Index), "the timed riposte's stagger not made on the attacker from the tick");
                Check(logic.TimedBlockStats.RiposteApplied[4] == 1, "the accepted timed riposte not counted in its band: " + logic.TimedBlockStats.RiposteApplied[4]);

                // ---- 2. the same guard HELD 1 s: an ordinary block (step 56 exactly)
                Tick(1.0);
                var a2 = Man(1.0, false);
                d = Hit(a2, you, RightBlock());
                Check(d.TimedScore == 0 && d.Riposte.Chance == d.RiposteChance56 && !d.Riposte.Happens, "a HELD guard: score " + d.TimedScore + ", riposte " + d.Riposte.Chance);
                // step 100f: both on screen with their numbers (the riposte's from step 1, this held block's) - the message path, merged by its gap
                S.Set(SettingsSchema.ImpactShowMessages, true, SettingSources.File);
                Tick(2.0);
                LogHas(" reels 2.00 s / 2.00 (guard 0.05 s / 0.15)");
                LogHas(" holds (guard >1 s / 0.15)");

                // ---- 3. a late switch to another side: a new guard in a manual mode, never in the automatic one
                guard.Automatic = true;
                guard.Keys[you.Agent.Index] = 0x1000;
                Tick(0.05);
                d = Hit(Man(1.0, false), you, RightBlock());
                Check(d.TimedScore == 0, "AUTOMATIC block direction: a direction change counted as a new guard (score " + d.TimedScore + ")");
                guard.Automatic = false;
                guard.Keys[you.Agent.Index] = 0x800;
                Tick(0.05);
                d = Hit(Man(1.0, false), you, Parried());
                Check(Close(d.TimedScore, 1, 1e-9) && d.Event == ImpactEvent.Parry && Close(d.Riposte.Chance, 1.0, 1e-6),
                    "a manual switch to the right side 0.05 s before a parry: score " + d.TimedScore + ", riposte " + d.Riposte.Chance);

                // ---- 4. the AI at contact: an active defend 0.05 s old at half strength; a held one 0; a parry reaction unknown
                var ai = Man(1.0, true);
                guard.Contacts[ai.Agent.Index] = (GuardRead.Active, 0.05);
                d = Hit(Man(1.0, false), ai, RightBlock());
                Check(d.TimedSource == TimingSource.AiActive && Close(d.TimedScore, 0.5, 1e-6)
                      && Close(d.Riposte.Chance, Math.Min(1, d.RiposteChance56 + (0.85 - d.RiposteChance56) * 0.5 + 0.15), 1e-6),
                    "the AI's active defend: source " + d.TimedSource + ", score " + d.TimedScore + ", riposte " + d.Riposte.Chance);
                guard.Contacts[ai.Agent.Index] = (GuardRead.Passive, double.NaN);
                d = Hit(Man(1.0, false), ai, RightBlock());
                Check(d.TimedSource == TimingSource.AiHeld && d.TimedScore == 0, "the AI's held defend: " + d.TimedSource + " " + d.TimedScore);
                guard.Contacts[ai.Agent.Index] = (GuardRead.ParryReaction, double.NaN);
                d = Hit(Man(1.0, false), ai, RightBlock());
                Check(d.TimedSource == TimingSource.None && d.TimedScore == 0, "a parry reaction at contact: " + d.TimedSource + " " + d.TimedScore);

                // ---- 5. the AI times its block on YOUR blow: at most 1.2 s
                S.Set(SettingsSchema.TimedBlockAiPercent, 100, SettingSources.File);
                var aiBlocker = Man(1.0, true);
                guard.Contacts[aiBlocker.Agent.Index] = (GuardRead.Active, 0.0);
                logic.SmokePlayer = null;
                var youAttack = Man(1.0, false);
                logic.SmokePlayer = youAttack.Agent;
                d = Hit(youAttack, aiBlocker, RightBlock());
                Check(d.Riposte.Happens && Close(d.RiposteStunSeconds, 1.2, 1e-6) && d.TimedCappedForYou,
                    "the AI's perfect block on YOUR blow: " + d.RiposteStunSeconds + " s (capped " + d.TimedCappedForYou + ")");
                S.Set(SettingsSchema.TimedBlockAiPercent, 50, SettingSources.File);
                logic.SmokePlayer = you.Agent;

                // ---- 6. the switch off: no clock reads, every block untimed; the master switch the same
                S.Set(SettingsSchema.TimedBlocksEnabled, false, SettingSources.File);
                Tick();
                reads = guard.KeyReads;
                guard.Keys[you.Agent.Index] = 0;
                Tick(0.2);
                guard.Keys[you.Agent.Index] = 0x800;
                Tick(0.05);
                Check(guard.KeyReads == reads, "the clock read with timed blocks OFF: " + (guard.KeyReads - reads) + " reads");
                d = Hit(Man(1.0, false), you, RightBlock());
                Check(d.TimedScore == 0 && d.TimedSource == TimingSource.None && d.Riposte.Chance == d.RiposteChance56, "timed blocks OFF: score " + d.TimedScore);
                S.Set(SettingsSchema.TimedBlocksEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                reads = guard.KeyReads;
                Tick(0.2);
                Check(guard.KeyReads == reads, "the clock read with the MASTER SWITCH off: " + (guard.KeyReads - reads) + " reads");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick();

                // ---- 7. the [summary] lines
                var lines = logic.TimedBlockStats.SummaryLines(TimedBlockRules.From(S));
                Check(lines.Length == 3 && lines[0].StartsWith("timed blocks (step 100b; ON)", StringComparison.Ordinal) && lines[1].Contains("Blocked + active")
                      && lines[1].Contains("Parried + active") && lines[2].StartsWith("timed ripostes", StringComparison.Ordinal),
                    "the [summary] timed blocks lines: " + string.Join(" || ", lines));
                Console.WriteLine("        step 100b: your perfect block (0.05 s) - the riposte " + (100 * 1.0).ToString("0", CultureInfo.InvariantCulture) + "% for 2.0 s (step 101: 85 + 15 points); held = step 56; the AI's at half; on you at most 1.2 s");
                foreach (var l in lines) Console.WriteLine("        " + l);
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
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.TimedBlocksEnabled, true, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}
