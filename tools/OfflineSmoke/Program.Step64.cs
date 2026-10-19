using System;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 64 - THE WALL'S BLOCKS COUNT, THE CHARGE HITS AT SPEED (AI_NOTES "Step 64"). Anton's looter battles of
    /// 2026-10-19: ~85% of an AI shieldman's blocks read WRONG side even fresh, nobody bounced off the wall. Through the real logic's pieces of
    /// OnMeleeHit (its tracked-attacker part makes native reads a stand-in agent cannot answer): the verdict, the impact model's event, the
    /// guard lines, the summary.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShieldFrontArc()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step63On();
                Step56BlockModel();
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, SettingsSchema.ShieldFrontArcIsCorrect.Default, SettingSources.File);
                S.Set(SettingsSchema.ShieldFrontArcDegrees, SettingsSchema.ShieldFrontArcDegrees.Default, SettingSources.File);
                Check(S.ShieldFrontArcIsCorrect && S.ShieldFrontArcDegrees == 60, "the front arc is not on at 60° by default: " + S.ShieldFrontArcIsCorrect + " / " + S.ShieldFrontArcDegrees);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var facts = new FakeWeaponFacts();
                var logic = NewPaceLogic(new FakePaceBody(), facts);
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                double t = 64000;
                logic.TickImpact(t);
                LogHas("[guard] mission start: shield front arc: on - a shield block whose attacker stands within 60° of the blocker's facing counts as a RIGHT-side block");

                TrackedAgent Man(int index, double bar, int skill)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = skill;
                    SetBar(m, bar);
                    return m;
                }
                var legionary = Man(6400, 1.0, 130);
                inputs.Guards[6400] = (4.0, 130);
                inputs.Shields.Add(6400);
                facts.Shield.Add(6400);
                var looter = Man(6401, 1.0, 30);
                inputs.Weapons[6401] = (1.5, 85, ImpactDamageType.Blunt, 30);
                var arc = logic.ShieldArcStatsForSmoke;
                var guard = logic.TiredBlockStatsForSmoke;

                // ---- 1. the game says WRONG side, the looter 20° off the legionary's facing: a right-side block for every system of ours
                inputs.BlockAngles[6400] = 20;
                var wrong = WrongBlock();
                Check(logic.BlockKindOf(looter.Agent, legionary.Agent, in wrong) == BlockKind.ShieldRightSide, "a wrong-side block from 20° is not counted right");
                logic.ShieldArcHitTaken(looter.Agent, legionary.Agent, in wrong);
                Check(arc.EngineWrong(false) == 1 && arc.OursRight(false) == 1 && arc.Turned(false) == 1, "the arc's counts: engine wrong " + arc.EngineWrong(false) + ", ours right " + arc.OursRight(false));
                LogHas("[guard] first shield block from the front counted RIGHT this battle (step 64): (agent 6400)'s shield block of (agent 6401) at ");
                LogHas("- the attacker 20° off his facing (within ±60°): the game said WRONG side, the mod counts a right-side block");
                logic.ImpactModelHitTaken(looter.Agent, legionary.Agent, in wrong);
                Check(logic.LastImpact.Event == ImpactEvent.RightBlock, "the impact model did not decide a right-side block: " + logic.LastImpact.Event);
                logic.TiredGuardHit(looter.Agent, legionary.Agent, in wrong);
                Check(guard.WrongSide(0) == 0 && guard.Blows(0) == 1, "the guard lines counted the game's wrong side: wrong " + guard.WrongSide(0) + " of " + guard.Blows(0)); // bin 0 = the peak

                // ---- 2. a CHARGING looter into that block (his sprint ended 0.5 s ago): the reflected charge can throw him back
                looter.SprintEndedAt = t - 0.5;
                dice.Rest = 0.0; // every roll with a chance comes up (the fresh legionary's own rows are 0)
                logic.ImpactModelHitTaken(looter.Agent, legionary.Agent, in wrong);
                dice.Rest = 0.999;
                Check(logic.LastImpact.Event == ImpactEvent.RightBlock && logic.LastImpact.Charging && logic.LastImpact.ThrownBack.Happens,
                    "a charging looter into a front-arc block not thrown back: " + logic.LastImpact.Event + ", charging " + logic.LastImpact.Charging + ", thrown " + logic.LastImpact.ThrownBack.Chance);

                // ---- 3. from 100° (the side): the game's WRONG side stands; the engine's right side stays right from behind
                var flank = Man(6402, 1.0, 130);
                inputs.Guards[6402] = (4.0, 130);
                inputs.Shields.Add(6402);
                inputs.BlockAngles[6402] = 100;
                Check(logic.BlockKindOf(looter.Agent, flank.Agent, in wrong) == BlockKind.ShieldWrongSide, "a wrong-side block from 100° counted right");
                logic.ShieldArcHitTaken(looter.Agent, flank.Agent, in wrong);
                logic.ImpactModelHitTaken(looter.Agent, flank.Agent, in wrong);
                Check(logic.LastImpact.Event == ImpactEvent.WrongBlock, "a side block was not decided wrong-side: " + logic.LastImpact.Event);
                int reads = inputs.AngleReads;
                var right = RightBlock();
                inputs.BlockAngles[6402] = 170;
                Check(logic.BlockKindOf(looter.Agent, flank.Agent, in right) == BlockKind.ShieldRightSide && inputs.AngleReads == reads, "the game's right side read the angle / turned wrong");
                logic.ShieldArcHitTaken(looter.Agent, flank.Agent, in right);
                var parry = Parried();
                Check(logic.BlockKindOf(looter.Agent, flank.Agent, in parry) == BlockKind.WeaponParry && inputs.AngleReads == reads, "a parry was looked at");

                // ---- 4. an untracked attacker (a horse, a stand-in): the game's verdict, no native read
                Check(logic.BlockKindOf(FakeAgent(6499), legionary.Agent, in wrong) == BlockKind.ShieldWrongSide && inputs.AngleReads == reads, "an untracked attacker was looked at");
                logic.ShieldArcHitTaken(FakeAgent(6499), legionary.Agent, in wrong);
                Check(arc.NotLooked == 1, "the untracked attacker's block not counted as not looked at: " + arc.NotLooked);

                // ---- 5. live: the arc to 10° (20° is outside), the switch off, the master switch off - the game's verdict at once
                S.Set(SettingsSchema.ShieldFrontArcDegrees, 10, SettingSources.Mcm);
                Check(logic.BlockKindOf(looter.Agent, legionary.Agent, in wrong) == BlockKind.ShieldWrongSide, "ShieldFrontArcDegrees 10 not live");
                S.Set(SettingsSchema.ShieldFrontArcDegrees, 60, SettingSources.Mcm);
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, false, SettingSources.Mcm);
                reads = inputs.AngleReads;
                Check(logic.BlockKindOf(looter.Agent, legionary.Agent, in wrong) == BlockKind.ShieldWrongSide && inputs.AngleReads == reads, "the switch off not live (or the angle still read)");
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, true, SettingSources.Mcm);
                Check(logic.BlockKindOf(looter.Agent, legionary.Agent, in wrong) == BlockKind.ShieldRightSide, "the switch back on not live");

                // ---- 6. the summary line: the game's verdict against ours
                logic.WriteShieldArcSummary();
                LogHas("shield front arc (step 64, on, ±60°): AI men's shield blocks 4: the game right 1 (25%) / WRONG 3 → ours right 2 (50%) / WRONG 2 - 1 wrong-side blocks from the front counted right");
                LogHas("15-30° 1 (1), 90-120° 1, not looked at 1");
                Console.WriteLine("        step 64 front arc: a wrong-side block from 20° → right (the impact model's RightBlock; a charging looter thrown back), from 100° the game's; " + arc.SummaryLine(ShieldArcRules.From(S)));
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
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's step-64 part: off = the game's block verdict at once (no angle read), on = the front arc again.</summary>
        private static void ShieldArcFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, true, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactInputs = inputs;
                var a = logic.Track(FakeAgent(6490))!;
                var v = logic.Track(FakeAgent(6491))!;
                inputs.BlockAngles[6491] = 0;
                var wrong = WrongBlock();
                Check(logic.BlockKindOf(a.Agent, v.Agent, in wrong) == BlockKind.ShieldRightSide, "precondition: the front arc did not turn a block right (step 64)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                int reads = inputs.AngleReads;
                Check(logic.BlockKindOf(a.Agent, v.Agent, in wrong) == BlockKind.ShieldWrongSide && inputs.AngleReads == reads, "mod off: the front arc still turned a block (step 64)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Check(logic.BlockKindOf(a.Agent, v.Agent, in wrong) == BlockKind.ShieldRightSide, "mod back on: the front arc did not come back (step 64)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, false, SettingSources.File);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }
    }
}
