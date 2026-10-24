using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 82 - MEETING A BASH OR A KICK THE RIGHT WAY (Toni 2026-10-22; AI_NOTES "Step 82"): the engine's codes behind the
    /// Core's guard read, the meet rows through the real decorator's seams and OnMeleeHit with a stand-in engine (a bash the shield stopped with
    /// a guard right / wrong, a bash on a weapon guard right with the brace OFF, a kick on a guard down, a kick met any other way, you), the AI's
    /// read through the real logic (rolled once, the guard after the reaction time, in time / too late, a miss's hold running out, never you, the
    /// master switch), shieldmen bashing fresh vs tired defenders, and the cost at 1000 men.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for the meet's engine reads: each man's guard (or, while his read guard is up, the guard he raised - the
        /// engine honouring the input), his block ability, a player's aim.</summary>
        private sealed class FakeMeetBody : IMeetBody
        {
            public readonly Dictionary<int, MeetGuardRead> Guards = new Dictionary<int, MeetGuardRead>();
            public readonly Dictionary<int, TrackedAgent> Men = new Dictionary<int, TrackedAgent>();
            public readonly HashSet<int> Shields = new HashSet<int>();
            public readonly Dictionary<int, (float Block, float Random)> Abilities = new Dictionary<int, (float, float)>();
            public readonly Dictionary<int, TrackedAgent> Aims = new Dictionary<int, TrackedAgent>();
            public readonly HashSet<int> NotAi = new HashSet<int>();
            public int Hooks, Releases, GuardReads;

            public MeetGuardRead Guard(Agent victim)
            {
                GuardReads++;
                int i = victim.Index;
                if (Men.TryGetValue(i, out var st) && st.Input != null && st.Input.MeetGuard)
                    return new MeetGuardRead
                    {
                        Kind = Shields.Contains(i) ? MeetGuardKind.Shield : MeetGuardKind.Weapon,
                        Dir = st.Input.MeetGuardFlag == ImpactMeetMath.FlagDefendRight ? MeetDir.Right : st.Input.MeetGuardFlag == ImpactMeetMath.FlagDefendUp ? MeetDir.Up : MeetDir.Down, // step 88: UP against a push
                        ActionType = Shields.Contains(i) ? 2 : 9, ActionDirection = -1, DefendFlag = st.Input.MeetGuardFlag,
                        GuardKg = Shields.Contains(i) ? 4.0 : 1.2, GuardSkill = Guards.TryGetValue(i, out var own) ? own.GuardSkill : 130,
                    };
                return Guards.TryGetValue(i, out var g) ? g : new MeetGuardRead { Kind = MeetGuardKind.None, ActionType = 35, ActionDirection = -1 };
            }

            public void Ability(TrackedAgent st, out float blockOnDecide, out float randomizedDirection)
            {
                if (!Abilities.TryGetValue(st.AgentIndex, out var a)) a = (0.84f, 0.89f);
                blockOnDecide = a.Block;
                randomizedDirection = a.Random;
            }

            public Agent? Aim(TrackedAgent st, double reach, double facing) => Aims.TryGetValue(st.AgentIndex, out var t) ? t.Agent : null;

            public bool CanRead(TrackedAgent st) => !NotAi.Contains(st.AgentIndex);

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                Hooks++;
                return AiInputHook.HookResult.FirstHookTurnedOn;
            }

            public void Release(TrackedAgent st) => Releases++;
        }

        private static MeetGuardRead ShieldGuard(MeetDir d) => new MeetGuardRead
        {
            Kind = MeetGuardKind.Shield, Dir = d, ActionType = 2, ActionDirection = d == MeetDir.Right ? 7 : d == MeetDir.Down ? 5 : -1, GuardKg = 4.0, GuardSkill = 130,
        };

        private static MeetGuardRead WeaponGuard(MeetDir d) => new MeetGuardRead
        {
            Kind = MeetGuardKind.Weapon, Dir = d, ActionType = d == MeetDir.Right ? 9 : d == MeetDir.Down ? 7 : 8, ActionDirection = -1, GuardKg = 1.2, GuardSkill = 130,
        };

        /// <summary>Step 82: every logic built from here on reads the meet's engine side through the stand-in; the engine's action codes, usage
        /// directions and defend bits behind the Core's guard read.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeMeetBody()
        {
            Check(MeetBodyDefault.Current is GameMeetBody, "the default meet body is not the game's: " + MeetBodyDefault.Current.GetType().Name);
            MeetBodyDefault.Current = new FakeMeetBody();
            Check(new AthleticsLogic().MeetBody == MeetBodyDefault.Current, "a new logic does not start with the default meet body");
            Check(ImpactMeetMath.ActDefendFist == (int)Agent.ActionCodeType.DefendFist && ImpactMeetMath.ActDefendShield == (int)Agent.ActionCodeType.DefendShield
                  && ImpactMeetMath.ActDefendLeftStaff == (int)Agent.ActionCodeType.DefendLeftStaff && ImpactMeetMath.ActReadyRanged == (int)Agent.ActionCodeType.ReadyRanged
                  && ImpactMeetMath.ActReadyMelee == (int)Agent.ActionCodeType.ReadyMelee && ImpactMeetMath.ActReleaseMelee == (int)Agent.ActionCodeType.ReleaseMelee
                  && ImpactMeetMath.ActKick == (int)Agent.ActionCodeType.Kick && ImpactMeetMath.ActWeaponBash == (int)Agent.ActionCodeType.WeaponBash,
                "the engine's ActionCodeType moved under the guard read");
            Check(ImpactMeetMath.GuardOf((int)Agent.ActionCodeType.DefendForward1h, -1, 0, out var d1) == MeetGuardKind.Weapon && d1 == MeetDir.Down
                  && ImpactMeetMath.GuardOf((int)Agent.ActionCodeType.DefendRight2h, -1, 0, out var d2) == MeetGuardKind.Weapon && d2 == MeetDir.Right
                  && ImpactMeetMath.GuardOf((int)Agent.ActionCodeType.DefendUpStaff, -1, 0, out var d3) == MeetGuardKind.Weapon && d3 == MeetDir.Up
                  && ImpactMeetMath.GuardOf((int)Agent.ActionCodeType.DefendLeft1h, -1, 0, out var d4) == MeetGuardKind.Weapon && d4 == MeetDir.Left,
                "the weapon guards' names do not map onto their directions");
            Check(ImpactMeetMath.DirDefendUp == (int)Agent.UsageDirection.DefendUp && ImpactMeetMath.DirDefendDown == (int)Agent.UsageDirection.DefendDown
                  && ImpactMeetMath.DirDefendLeft == (int)Agent.UsageDirection.DefendLeft && ImpactMeetMath.DirDefendRight == (int)Agent.UsageDirection.DefendRight,
                "the engine's UsageDirection moved");
            Check(ImpactMeetMath.FlagDefendLeft == (uint)Agent.MovementControlFlag.DefendLeft && ImpactMeetMath.FlagDefendRight == (uint)Agent.MovementControlFlag.DefendRight
                  && ImpactMeetMath.FlagDefendUp == (uint)Agent.MovementControlFlag.DefendUp && ImpactMeetMath.FlagDefendDown == (uint)Agent.MovementControlFlag.DefendDown,
                "the engine's MovementControlFlag defend bits moved");
            Check((uint)Agent.MovementControlFlag.DefendMask == AiInputMath.DefendMask && (uint)Agent.MovementControlFlag.AttackMask == AiInputMath.AttackMask, "the masks moved");
        }

        /// <summary>The meet rules at the embedded defaults (the older steps pin them off - AthleticsDefaults).</summary>
        private static void MeetDefaults()
        {
            foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("Meet", StringComparison.Ordinal) || p == SettingsSchema.ImpactMeetRulesEnabled))
                S.Set(p, p.Default, SettingSources.File);
        }

        private sealed class MeetWorld
        {
            public AthleticsLogic Logic = null!;
            public FakeImpactInputs Inputs = null!;
            public FakeImpactBody Body = null!;
            public FakeMeetBody Meet = null!;
            public ScriptDice Dice = null!;
            public TraxCombat.Models.TraxDamageModel Model = null!;
            public double T;
        }

        private static MeetWorld NewMeetWorld(double t)
        {
            AthleticsDefaults();
            ImpactModelDefaults();
            Step63On();
            Step56BlockModel();
            AiKickDefaults();
            MeetDefaults();
            S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
            var w = new MeetWorld
            {
                Inputs = new FakeImpactInputs(),
                Body = new FakeImpactBody(),
                Meet = new FakeMeetBody(),
                Dice = new ScriptDice(),
                Model = SpikeDamageModel(),
                T = t,
            };
            w.Logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
            w.Logic.ImpactBody = w.Body;
            w.Logic.ImpactInputs = w.Inputs;
            w.Logic.MeetBody = w.Meet;
            w.Logic.MessageSink = new FakeMessageSink();
            w.Logic.ImpactDice = w.Dice;
            w.Logic.TickImpact(t);
            return w;
        }

        private static TrackedAgent MeetMan(MeetWorld w, int index, int skill, double bar, bool shield)
        {
            var m = w.Logic.Track(FakeAgent(index))!;
            m.AthleticsSkill = skill;
            SetBar(m, bar);
            w.Inputs.Kits[index] = (60, 15);
            if (shield)
            {
                w.Inputs.Shields.Add(index);
                w.Meet.Shields.Add(index);
                w.Inputs.Weapons[index] = (4.0, 0, ImpactDamageType.Blunt, skill); // a bash = the shield
            }
            w.Meet.Men[index] = m;
            return m;
        }

        private static AttackCollisionData BashIntoShield() => Collision(true, true, false, CombatCollisionResult.Blocked, 3, 2);

        /// <summary>One kick / bash collision through the decorator's knock-back and KnockDown questions, then OnMeleeHit's part; the knock-back answer.</summary>
        private static bool MeetHit(MeetWorld w, TrackedAgent a, TrackedAgent v, AttackCollisionData cd, out bool down)
        {
            w.T += 0.05;
            w.Logic.TickImpact(w.T);
            var blow = new Blow(a.AgentIndex);
            bool back = w.Model.DecideAgentKnockedBackByBlow(a.Agent, v.Agent, in cd, null!, in blow);
            down = w.Model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);
            w.Logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
            return back;
        }

        private static void MeetCleanUp(AthleticsLogic? keep)
        {
            S.Set(SettingsSchema.ImpactMeetRulesEnabled, false, SettingSources.File);
            Step65CleanUp(keep);
        }

        /// <summary>
        /// THE MEET ROWS through the real decorator's seams and OnMeleeHit: a bash the shield stopped guarding RIGHT - a tired basher staggered
        /// from the tick (the riposte's lever), the blocker untouched; a bash on a weapon guard RIGHT with the BRACE OFF - the knock-back refused,
        /// the basher at risk; a bash the shield stopped guarding DOWN on an exhausted blocker - his stagger from the tick; a kick on a guard DOWN -
        /// a tired kicker thrown down by our own blow; a kick met with no guard - step 65's brace as before; a kick the shield stopped met any other
        /// way - nothing; YOU met correctly; the master switch / the rules' switch off - step 65 exactly.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MeetRowsThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                var w = NewMeetWorld(82000);
                var logic = w.Logic;
                LogHas("[impact] mission start, meeting kicks and bashes (step 82): ON - a bash met by a RIGHT guard staggers / fells the BASHER (shield 70% / 30%");
                var basher = MeetMan(w, 8200, 60, 0.1, true);      // a spent basher
                var legionary = MeetMan(w, 8201, 130, 1.0, true);  // a fresh legionary
                var sworder = MeetMan(w, 8202, 130, 1.0, false);   // a fresh swordsman
                var spentWall = MeetMan(w, 8203, 130, 0.05, true); // an exhausted shieldman

                // ---- 1. a bash the shield stopped, guarding RIGHT: the basher at risk - fall no (0.99), stagger yes (0.01), its length
                w.Meet.Guards[legionary.AgentIndex] = ShieldGuard(MeetDir.Right);
                w.Dice.Next(0.99, 0.01, 0.5);
                int staggers = w.Body.Staggers.Count;
                MeetHit(w, basher, legionary, BashIntoShield(), out _);
                var d = logic.LastImpact;
                Check(d.Meet == ImpactMeet.BashShieldRight && d.MeetAttacker && d.MeetBlocked && d.Riposte.Happens && !d.Knockdown.InRow && !d.Stagger.InRow,
                    "a bash on a right-guarded shield: " + d.Meet + ", attacker " + d.MeetAttacker + ", riposte " + d.Riposte.Happens + " (" + d.Riposte.Chance + ")");
                Check(Math.Abs(d.Riposte.Chance - 0.70) < 0.02, "the spent basher's stagger is not the ceiling 70%: " + d.Riposte.Chance);
                w.Logic.TickImpact(w.T + 0.02);
                Check(w.Body.Staggers.Skip(staggers).Any(s => s.Man == basher.AgentIndex) && !w.Body.Staggers.Skip(staggers).Any(s => s.Man == legionary.AgentIndex),
                    "the basher was not staggered from the tick (or the blocker was): " + string.Join(", ", w.Body.Staggers.Skip(staggers)));
                Check(logic.ImpactMeetStats.Decided[(int)ImpactMeet.BashShieldRight] == 1 && logic.ImpactMeetStats.BlockedBashes == 1, "the meet not counted");
                LogHas("[impact] first meet bash - shield, guard right at ");
                LogHas("the guard read at contact: shield right (channel 1 action 2, its direction 7");
                LogHas("[impact] first meet outcome - bash - shield, guard right at ");

                // ---- 2. a bash on a weapon guard RIGHT, the brace OFF: the engine lands it, the knock-back refused all the same
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.Mcm);
                w.Meet.Guards[sworder.AgentIndex] = WeaponGuard(MeetDir.Right);
                w.T += 3;
                w.Dice.Next(0.99, 0.99);
                bool back = MeetHit(w, basher, sworder, BashLands(), out bool down);
                d = logic.LastImpact;
                Check(!back && !down && d.Meet == ImpactMeet.BashWeaponRight && d.Braced && Math.Abs(d.ReelFactor - 0.2) < 1e-6,
                    "a bash on a weapon guard right: knock-back " + back + ", down " + down + ", " + d.Meet + ", braced " + d.Braced + ", reel x" + d.ReelFactor);
                S.Set(SettingsSchema.KickBraceEnabled, true, SettingSources.Mcm);

                // ---- 3. a bash the shield stopped guarding DOWN on an exhausted blocker: his rows - fall no, drop no, stagger yes
                w.Meet.Guards[spentWall.AgentIndex] = ShieldGuard(MeetDir.Down);
                var fresh = MeetMan(w, 8204, 100, 1.0, true);
                staggers = w.Body.Staggers.Count;
                w.Dice.Next(0.99, 0.99, 0.01, 0.5);
                MeetHit(w, fresh, spentWall, BashIntoShield(), out _);
                d = logic.LastImpact;
                Check(d.Meet == ImpactMeet.BashShieldWrong && !d.MeetAttacker && d.Stagger.Happens && d.StaggerSeconds > 0 && Math.Abs(d.Stagger.Chance - 0.85) < 0.02,
                    "a bash on a shield guarding down: " + d.Meet + ", the blocker staggered " + d.Stagger.Happens + " (" + d.Stagger.Chance + ")");
                w.Logic.TickImpact(w.T + 0.02);
                Check(w.Body.Staggers.Skip(staggers).Any(s => s.Man == spentWall.AgentIndex), "the blocker was not staggered from the tick");

                // ---- 4. a kick on a shield guarding DOWN: the spent kicker thrown down (our own blow on him), the kicked man held
                var spentKicker = MeetMan(w, 8205, 60, 0.05, false);
                var wall2 = MeetMan(w, 8206, 130, 1.0, true);
                w.Meet.Guards[wall2.AgentIndex] = ShieldGuard(MeetDir.Down);
                int blows = w.Body.OwnBlows.Count;
                w.Dice.Next(0.01, 0.5);
                back = MeetHit(w, spentKicker, wall2, KickLands(), out down);
                d = logic.LastImpact;
                Check(!back && !down && d.Meet == ImpactMeet.KickShieldDown && d.ThrownBack.Happens && d.Riposte.Refusal == ImpactRefusal.Replaced,
                    "a kick on a shield guarding down: knock-back " + back + ", " + d.Meet + ", the kicker down " + d.ThrownBack.Happens);
                w.Logic.TickImpact(w.T + 0.02);
                Check(w.Body.OwnBlows.Skip(blows).Any(b => b.Victim == spentKicker.AgentIndex), "the kicker was not knocked down by our blow from the tick");

                // ---- 5. a kick met with NO guard: step 65's brace as before (the dice above the failure - braced)
                var open = MeetMan(w, 8207, 130, 1.0, true);
                var kicker2 = MeetMan(w, 8208, 30, 1.0, false);
                w.Dice.Next(0.99);
                MeetHit(w, kicker2, open, KickLands(), out _);
                d = logic.LastImpact;
                Check(d.Meet == ImpactMeet.KickOther && d.Brace != KickBraceOutcome.NotAsked && !d.MeetAttacker, "a kick with no guard: " + d.Meet + ", brace " + d.Brace);

                // ---- 6. a kick the shield stopped, met any other way: nothing (as before)
                int notJudged = logic.ImpactMeetStats.BlockedKicksNotJudged;
                MeetHit(w, kicker2, open, Collision(true, true, false, CombatCollisionResult.Blocked, -1, 2), out _);
                Check(logic.ImpactMeetStats.BlockedKicksNotJudged == notJudged + 1, "a kick the shield stopped, no guard down, was judged");

                // ---- 7. YOU: your bash met by a right-guarded shield - the YOU line
                var you = MeetMan(w, 8209, 100, 0.2, true);
                var vet = MeetMan(w, 8210, 200, 1.0, true);
                w.Meet.Guards[vet.AgentIndex] = ShieldGuard(MeetDir.Right);
                logic.SmokePlayer = you.Agent;
                w.T += 3;
                w.Dice.Next(0.99, 0.99);
                MeetHit(w, you, vet, BashIntoShield(), out _);
                LogHas("[impact] YOU: your shield bash on (agent 8210) - met: bash - shield, guard right → the ATTACKER");
                logic.SmokePlayer = null;

                // ---- 8. the rules off / the master switch off: step 65 exactly (a bash into a raised shield decides nothing)
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, false, SettingSources.Mcm);
                int decided = logic.ImpactMeetStats.Total;
                var before = logic.LastImpact;
                MeetHit(w, basher, legionary, BashIntoShield(), out _);
                Check(logic.ImpactMeetStats.Total == decided && logic.LastImpact.Now == before.Now, "ImpactMeetRulesEnabled off: a stopped bash was still decided");
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                MeetHit(w, basher, legionary, BashIntoShield(), out _);
                Check(logic.ImpactMeetStats.Total == decided, "the master switch off: a stopped bash was decided");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);

                logic.WriteImpactSummary();
                LogHas("[summary] impact meets (step 82 / 88) - each kick / bash / push by how it was met (ON): bash - shield, guard right 2 → the ATTACKER staggered 1");
                LogHas("[summary] impact meets - the guard read at contact (step 82; PLAYTEST D47 - does the engine tell the direction?)");
                LogHas("[summary] impact meets - the AI reads an incoming kick / bash (step 82)");
            }
            finally
            {
                MeetCleanUp(keep);
            }
        }

        /// <summary>
        /// THE AI READS IT through the real logic: rolled ONCE per start (the chance from his block ability - a sure read at 1.0 / 0.0, none for a
        /// looter's die above 1 in 8); the guard RIGHT for a bash only after the reaction time; the stopped bash then meets it correctly; the read
        /// ends at the hit (in time, met); a hit sooner than the reaction = TOO LATE; a kick read gets the guard DOWN; no hit = let go after the
        /// hold; never you; a man in his own kick never; the poll's sight of YOUR bash = the man you aim at reads it; the master switch off ends the
        /// reads at the next tick.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiReadsTheMeetThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                var w = NewMeetWorld(82200);
                var logic = w.Logic;
                logic.MeetDice = new ScriptDice();
                var dice = (ScriptDice)logic.MeetDice;
                var basher = MeetMan(w, 8220, 60, 0.1, true);
                var reader = MeetMan(w, 8221, 250, 1.0, true);
                w.Meet.Guards[reader.AgentIndex] = ShieldGuard(MeetDir.Down); // his own raised guard (the AI's DefendDown) when he does not read
                w.Meet.Abilities[reader.AgentIndex] = (1.0f, 0.0f);
                var st = logic.ImpactMeetStats;
                double t = w.T;

                // ---- 1. a sure read: the guard RIGHT after 0.15 s, the stopped bash met correctly, the read ended at the hit
                dice.Next(0.5);
                logic.MeetReadStart(basher, reader, true, t);
                Check(reader.MeetReading && !reader.MeetGuardUp && logic.MeetReadersRunning == 1 && st.ReadYes[0] == 1, "a sure read was not rolled yes");
                logic.MeetReadStart(basher, reader, true, t + 0.02); // the poll's sight of the same bash: no second roll
                Check(st.ReadRolled[0] == 1, "the same start rolled twice");
                logic.TickMeetReads(t + 0.10);
                Check(!reader.MeetGuardUp && (reader.Input == null || !reader.Input.MeetGuard), "the guard went up before the reaction time");
                logic.TickMeetReads(t + 0.16);
                Check(reader.MeetGuardUp && reader.Input!.MeetGuard && reader.Input.MeetGuardFlag == ImpactMeetMath.FlagDefendRight && w.Meet.Hooks >= 1,
                    "the read guard is not RIGHT after the reaction time");
                uint frame = ImpactMeetMath.GuardFrame((uint)(Agent.MovementControlFlag.AttackLeft | Agent.MovementControlFlag.DefendDown), reader.Input.MeetGuardFlag);
                Check(frame == (uint)Agent.MovementControlFlag.DefendRight, "the input frame is not the right guard alone: 0x" + frame.ToString("X"));
                w.T = t + 0.45;
                w.Dice.Next(0.99, 0.01, 0.5);
                MeetHit(w, basher, reader, BashIntoShield(), out _);
                Check(logic.LastImpact.Meet == ImpactMeet.BashShieldRight && logic.LastImpact.Riposte.Happens, "the read guard did not meet the bash: " + logic.LastImpact.Meet);
                Check(!reader.MeetReading && logic.MeetReadersRunning == 0 && !reader.Input.MeetGuard && st.ReadInTime[0] == 1 && st.ReadMet[0] == 1 && st.ReadEndedHit == 1,
                    "the read did not end at the hit in time and met");
                Check(st.ByReader[(int)ImpactMeet.BashShieldRight] == 1, "the meet by a reader not counted");
                LogHas("[impact] first AI READ of a bash at ");

                // ---- 2. too late: the hit 0.1 s after the start
                t = w.T + 3;
                dice.Next(0.5);
                logic.MeetReadStart(basher, reader, true, t);
                w.T = t + 0.05;
                MeetHit(w, basher, reader, BashIntoShield(), out _);
                Check(st.ReadTooLate[0] == 1 && logic.LastImpact.Meet == ImpactMeet.BashShieldWrong, "a hit before the reaction time is not TOO LATE / met wrong: " + logic.LastImpact.Meet);
                LogHas("[impact] first AI read TOO LATE at ");

                // ---- 3. a kick read: the guard DOWN; no hit = let go after the hold
                t = w.T + 3;
                dice.Next(0.5);
                logic.MeetReadStart(basher, reader, false, t);
                logic.TickMeetReads(t + 0.2);
                Check(reader.Input!.MeetGuardFlag == ImpactMeetMath.FlagDefendDown, "a kick read is not the guard DOWN");
                logic.TickMeetReads(t + 1.6);
                Check(!reader.MeetReading && !reader.Input.MeetGuard && st.ReadEndedTimeout == 1 && w.Meet.Releases >= 1, "a read with no hit was not let go after the hold");

                // ---- 4. a looter's ability (0.5 / 1.0 = one in eight): the die 0.9 - no read, his guard left alone
                var looter = MeetMan(w, 8222, 20, 1.0, true);
                w.Meet.Abilities[looter.AgentIndex] = (0.5f, 1.0f);
                dice.Next(0.9);
                logic.MeetReadStart(basher, looter, true, t + 2);
                Check(!looter.MeetReading && st.ReadRolled[0] == 4 && Math.Abs(st.ChanceSum[0] - 3.125) < 1e-6, "a looter's read: rolled " + st.ReadRolled[0] + ", chances " + st.ChanceSum[0]);

                // ---- 5. never you; never a man in his own kick
                logic.SmokePlayer = reader.Agent;
                logic.MeetReadStart(basher, reader, true, t + 5);
                Check(!reader.MeetReading, "you read a bash");
                logic.SmokePlayer = null;
                reader.AiKickPhase = AiKickPhase.Guard;
                int busy = st.ReadSkippedBusy;
                logic.MeetReadStart(looter, reader, true, t + 6);
                Check(!reader.MeetReading && st.ReadSkippedBusy == busy + 1, "a man in his own kick read one");
                reader.AiKickPhase = AiKickPhase.None;

                // ---- 6. YOUR bash seen by the poll: the man you aim at reads it
                var you = MeetMan(w, 8223, 100, 1.0, true);
                w.Meet.Aims[you.AgentIndex] = reader;
                logic.SmokePlayer = you.Agent;
                dice.Next(0.1);
                logic.MeetKickSeen(you, KickBashKind.Bash, t + 8);
                Check(reader.MeetReading && ReferenceEquals(reader.MeetReadBy, you) && reader.MeetReadBash, "the man you bash did not read it");
                logic.SmokePlayer = null;

                // ---- 7. the master switch off: the read let go at the next tick
                logic.TickMeetReads(t + 8.2);
                Check(reader.Input!.MeetGuard, "the guard is not up before the switch");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickMeetReads(t + 8.3);
                Check(!reader.MeetReading && !reader.Input.MeetGuard && logic.MeetReadersRunning == 0, "the master switch off left a read guard up");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.WriteImpactSummary();
                LogHas("100-75%: rolled 5");
            }
            finally
            {
                MeetCleanUp(keep);
            }
        }

        /// <summary>
        /// SHIELDMEN BASHING FRESH VS TIRED DEFENDERS (seeded, 400 bashes each): an average shieldman at half his bar (skill 80) bashes a skilled
        /// shieldman (skill 250, a 4 kg shield - the game's block ability on realistic: 0.94 / 0.49) whose own raised guard is DOWN (the AI's) unless
        /// he READS the bash (then RIGHT) - FRESH (100 %) vs TIRED (10 %, step 30's guess on his direction): fresh defenders punish many bashes,
        /// tired ones are staggered.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BashesOnFreshAndTiredDefenders()
        {
            var keep = _logic;
            try
            {
                const int n = 400;
                string Pct(int k) => (100.0 * k / n).ToString("0.0", CultureInfo.InvariantCulture) + "%";
                (int read, int bashersHit, int bashersDown, int defStag, int defDown, int defDrop) Run(double bar, int seed)
                {
                    var w = NewMeetWorld(82400 + seed);
                    var logic = w.Logic;
                    logic.ImpactDice = new SeededRandom(seed);
                    logic.MeetDice = new SeededRandom(seed + 1);
                    var tb = new TiredBlockRules(true, true, 100);
                    float random = TiredBlockMath.Chance(0.488f, TiredBlockMath.Weight(in tb, Math.Min(1, bar / 0.75), false));
                    int r = 0, bh = 0, bd = 0, ds = 0, dd = 0, dr = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var a = MeetMan(w, 30000 + 2 * i, 80, 0.5, true);
                        var v = MeetMan(w, 30001 + 2 * i, 200, bar, true);
                        w.Inputs.Weapons[a.AgentIndex] = (4.0, 0, ImpactDamageType.Blunt, 80);
                        w.Meet.Guards[v.AgentIndex] = new MeetGuardRead { Kind = MeetGuardKind.Shield, Dir = MeetDir.Down, ActionType = 2, ActionDirection = 5, GuardKg = 4.0, GuardSkill = 250 };
                        w.Meet.Abilities[v.AgentIndex] = (0.938f, random);
                        double t = w.T + 1;
                        logic.MeetReadStart(a, v, true, t);
                        if (v.MeetReading) r++;
                        logic.TickMeetReads(t + 0.2);
                        w.T = t + 0.4; // the AI's bash: 0.4 s of guard, then the key
                        MeetHit(w, a, v, BashIntoShield(), out _);
                        var d = logic.LastImpact;
                        if (d.Riposte.Happens) bh++;
                        if (d.ThrownBack.Happens) bd++;
                        if (d.Stagger.Happens) ds++;
                        if (d.Knockdown.Happens) dd++;
                        if (d.ShieldDrop.Happens) dr++;
                        logic.TickMeetReads(w.T + 2);
                    }
                    return (r, bh, bd, ds, dd, dr);
                }
                var fresh = Run(1.0, 821);
                var tired = Run(0.1, 822);
                Console.WriteLine("        step 82 an average shieldman at half his bar bashing a skilled shieldman, " + n + " each - FRESH: read " + Pct(fresh.read) + ", the basher staggered "
                                  + Pct(fresh.bashersHit) + " / down " + Pct(fresh.bashersDown) + ", the defender staggered " + Pct(fresh.defStag) + " / down " + Pct(fresh.defDown) + " / shield "
                                  + Pct(fresh.defDrop) + " | TIRED (10%): read " + Pct(tired.read) + ", the basher staggered " + Pct(tired.bashersHit) + " / down " + Pct(tired.bashersDown)
                                  + ", the defender staggered " + Pct(tired.defStag) + " / down " + Pct(tired.defDown) + " / shield " + Pct(tired.defDrop));
                int freshPunished = fresh.bashersHit + fresh.bashersDown, tiredBroken = tired.defStag + tired.defDown;
                Check(freshPunished >= n / 5, "fresh skilled defenders punished too few bashes: " + Pct(freshPunished));
                Check(fresh.defStag + fresh.defDown <= n / 20, "fresh skilled defenders were staggered too often: " + Pct(fresh.defStag + fresh.defDown));
                Check(tiredBroken >= n * 2 / 5 && tiredBroken > 4 * (fresh.defStag + fresh.defDown), "tired defenders were not staggered: " + Pct(tiredBroken));
                Check(fresh.read > tired.read, "tired men read as often as fresh: " + fresh.read + " vs " + tired.read);
            }
            finally
            {
                MeetCleanUp(keep);
            }
        }

        /// <summary>THE COST at 1000 men: ~8 kicks / bashes starting a tick (each target reads or not), every reader ticked, 20 stopped bashes a tick
        /// decided through OnMeleeHit with the guard read - the time and 0 bytes.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MeetCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var w = NewMeetWorld(82600);
                var logic = w.Logic;
                w.Body.Record = false; // the stand-in's own lists never grow inside the count
                logic.ImpactDice = new SeededRandom(826);
                logic.MeetDice = new SeededRandom(827);
                const int many = 1000;
                var all = new List<TrackedAgent>(many);
                for (int i = 0; i < many; i++)
                {
                    var m = MeetMan(w, 40000 + i, 100, 0.3 + 0.7 * (i % 7) / 6.0, true);
                    m.Input = new AiInputState(); // made once, at his first hook - not the tick's cost
                    w.Meet.Guards[m.AgentIndex] = ShieldGuard(i % 2 == 0 ? MeetDir.Down : MeetDir.Right);
                    all.Add(m);
                }
                var cd = BashIntoShield();
                int cursor = 0;
                void Tick()
                {
                    w.T += 1.0 / 60;
                    for (int k = 0; k < 8; k++)
                    {
                        var a = all[cursor % many];
                        var v = all[(cursor + 1) % many];
                        cursor++;
                        logic.MeetReadStart(a, v, k % 2 == 0, w.T);
                    }
                    logic.TickMeetReads(w.T);
                    for (int k = 0; k < 20; k++)
                    {
                        var a = all[(cursor * 7 + k) % many];
                        var v = all[(cursor * 7 + k + 3) % many];
                        logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                    }
                    logic.TickImpact(w.T);
                }
                for (int k = 0; k < 300; k++) Tick(); // the first-time lines written before the count
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 0; k < ticks; k++) Tick();
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double ms = sw.Elapsed.TotalMilliseconds / ticks;
                var st = logic.ImpactMeetStats;
                Console.WriteLine("        step 82 cost: " + many + " men, 8 kicks / bashes starting and 20 stopped bashes decided a tick - " + ms.ToString("0.000", CultureInfo.InvariantCulture)
                                  + " ms a tick, " + allocated + " bytes over " + ticks + " ticks; reads rolled " + st.ReadRolled.Sum() + ", read " + st.ReadYes.Sum() + ", running "
                                  + logic.MeetReadersRunning + ", meets decided " + st.Total);
                Check(st.Total > 1000 && st.ReadYes.Sum() > 0, "cost: nothing was decided / read");
                Check(ms < 1.0, "cost: the meet's tick took " + ms + " ms (budget 1.0)");
                Check(allocated < 16 * 1024, "cost: the meet allocates - " + allocated + " bytes");
            }
            finally
            {
                MeetCleanUp(keep);
            }
        }
    }
}
