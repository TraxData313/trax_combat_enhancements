using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 63 - FALLS ON THE HIT, A STRONG REAR BLOW, THE WALL PULLS MEN BACK (AI_NOTES "Step 63"). Anton's battle of
    /// 2026-10-19 11:02: 198 falls decided, 71 played by the game, 126 by our backup 0.53 s later - every one of the 126 on a 0-DAMAGE blow:
    /// a FRIEND's (the engine cancels a friend's damage and a 0-damage blow plays no reaction). Through the real shrug-off hook, the real
    /// decorator's KnockDown seam and the real logic with the stand-in body.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FallsOnTheHit()
        {
            var keep = _logic;
            float thresholdKeep = ManagedParameters.GetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut);
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step63On();
                Check(Math.Abs(TraxSettings.Shared.ImpactFallBackupSeconds - 0.2) < 1e-6, "ImpactFallBackupSeconds is not 0.2 by default: " + TraxSettings.Shared.ImpactFallBackupSeconds);
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, 5f);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var game = new CustomAgentApplyDamageModel();
                var falls = logic.ImpactFallStats;
                double t = 63000;
                logic.TickImpact(t);

                TrackedAgent Man(int index, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, bar);
                    return m;
                }
                (bool Kept, bool Down) Blow(TrackedAgent a, TrackedAgent v, in AttackCollisionData cd)
                {
                    var blow = new Blow(a.AgentIndex);
                    bool kept = ImpactHooks.ShrugOffKept(v.Agent, in cd, in blow, game);
                    if (kept) blow.BlowFlag |= BlowFlags.ShrugOff;
                    bool down = model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                    return (kept, down);
                }
                void Tick(double seconds) => TickFor(logic, ref t, seconds);
                var sword = Man(6300, 1.0);
                inputs.Weapons[6300] = (1.2, 92, ImpactDamageType.Cut, 125);

                // ---- 1. a FRIEND's blow (the rear rank's swing into his own front man): the engine cancels its damage (0) - never decided, no
                // fall, no stagger, no backup; the game's own shrug-off answer stands
                var front = Man(6301, 0.0);
                inputs.NotEnemies = true;
                int decisions = logic.ImpactModelStats.Decisions;
                dice.Next(0.0, 0.0, 0.5);
                var friendly = CleanHitDealing(0);
                var r1 = Blow(sword, front, in friendly);
                Check(!r1.Down && logic.ImpactModelStats.Decisions == decisions, "a friend's blow was decided: down " + r1.Down + ", decisions +" + (logic.ImpactModelStats.Decisions - decisions));
                Check(falls.Friendly == 1 && logic.ImpactModelStats.Skipped[(int)ImpactSkip.NotEnemies] >= 1 && falls.Total(falls.Decided) == 0,
                    "a friend's blow not counted: friendly " + falls.Friendly + ", skipped " + logic.ImpactModelStats.Skipped[(int)ImpactSkip.NotEnemies]);
                Tick(1.0);
                Check(body.OwnBlows.Count == 0, "a backup blow on a man hit by a friend: " + string.Join(", ", body.OwnBlows));
                inputs.NotEnemies = false;

                // ---- 2. an ENEMY's blow the engine plays at once: played by the game, no backup
                var hit = Man(6302, 0.0);
                dice.Next(0.0, 0.5);
                var light = CleanHitDealing(3);
                Check(Blow(sword, hit, in light).Down, "an enemy's decided fall not answered");
                t = PlayReaction(logic, body, hit, ImpactActions.Fall, t + 1.0 / 60, 1.5, index: 6302);
                Check(falls.PlayedByGame[1] == 1 && body.OwnBlows.Count == 0, "the game's own Fall not counted (or a backup sent): game " + falls.PlayedByGame[1]);

                // ---- 3. a fall the engine does NOT play: our blow after 0.2 s (not 0.5), once
                var lost = Man(6303, 0.0);
                dice.Next(0.0, 0.5);
                Check(Blow(sword, lost, in light).Down, "the third fall not answered");
                Tick(0.15);
                Check(body.OwnBlows.Count == 0, "the backup came before its 0.2 s");
                Tick(0.1);
                Check(body.OwnBlows.Count == 1 && falls.BackupsSent == 1, "no backup blow 0.25 s after the blow: " + string.Join(", ", body.OwnBlows));
                t = PlayReaction(logic, body, lost, ImpactActions.Fall, t, 1.5, index: 6303);
                Check(falls.PlayedByBackup[1] == 1 && falls.BackupDelay.Mean < 0.3, "the backup's Fall: " + falls.PlayedByBackup[1] + " after " + falls.BackupDelay.Mean + " s");

                logic.WriteImpactSummary();
                LogHas("a FRIEND's blow (step 63 - the game cancels its damage, nothing plays) 1");
                LogHas("because a FRIEND struck them (step 63 - the game cancels a friend's damage: nothing to play) 1");
                LogHas("backups (ImpactFallBackupSeconds 0.2 s) sent 1");
                Console.WriteLine("        step 63 falls: a friend's blow decided " + (falls.Friendly == 1 ? "never" : "?") + "; enemy falls decided " + falls.Total(falls.Decided)
                                  + " - played by the game " + falls.Total(falls.PlayedByGame) + " (" + falls.GameDelay.Mean.ToString("0.00") + " s), by the backup "
                                  + falls.Total(falls.PlayedByBackup) + " (" + falls.BackupDelay.Mean.ToString("0.00") + " s after the blow)");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactHooks.ShrugOff = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, thresholdKeep);
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }
    }
}
