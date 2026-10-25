using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 96 (Toni 2026-10-25: "more defencive - bump up the defencive stuff"): THE AI GUARDS BETTER through the REAL stat decorator and logic - the
    /// game's guess chance × AiGuardGuessPercent BEFORE step 30's tired raise, the block- / parry-decide abilities × AiBlockDecidePercent clamped to
    /// 0.99 / 0.95, never you, live, the master switch first (AI_NOTES "Step 96").
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiGuardThroughTheDecorator()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                S.Set(SettingsSchema.TiredBlockRandomPercent, 100, SettingSources.File);
                S.Set(SettingsSchema.AiGuardGuessPercent, 60, SettingSources.File); // the shipped values
                S.Set(SettingsSchema.AiBlockDecidePercent, 120, SettingSources.File);
                Check((int)DrivenProperty.AIBlockOnDecideAbility == 5 && (int)DrivenProperty.AIParryOnDecideAbility == 6, "the game's DrivenProperty numbers for the decide abilities moved");
                var probe = new AgentDrivenProperties();
                probe.SetStat(DrivenProperty.AIBlockOnDecideAbility, 0.37f);
                probe.SetStat(DrivenProperty.AIParryOnDecideAbility, 0.41f);
                Check(Near(probe.AIBlockOnDecideAbility, 0.37f) && Near(probe.AIParryOnDecideAbility, 0.41f), "AgentDrivenProperties' decide abilities do not read their own slots");

                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                LogHas("[guard] mission start: AI guard (step 96): on - AI men on foot and riders (never you): the game's block-direction guess chance x0.60 (AiGuardGuessPercent 60");
                var baseModel = new FreshStatsModel { BlockChance = 1f, BlockDecide = 0.62f, ParryDecide = 0.53f }; // a recruit's
                var top = new TraxAgentStatModel(new AgentStatCalculateModel[] { baseModel });
                top.Initialize(baseModel);
                var r = Rules;
                double t = 7000;

                // a FRESH AI man: the guard from his first recompute (tracking marks him)
                var man = logic.Track(FakeAgent(9600))!;
                man.AthleticsSkill = 100;
                Check(Near(man.GuardGuess, 0.6f) && Near(man.GuardDecide, 1.2f) && man.SpeedDirty, "a fresh man was not given the guard factors at tracking: " + man.GuardGuess + " / " + man.GuardDecide);
                var p = man.Agent.AgentDrivenProperties;
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AiRandomizedDefendDirectionChance, 0.6f), "a fresh man's guess chance is not 1.00 x 0.6: " + p.AiRandomizedDefendDirectionChance);
                Check(Near(p.AIBlockOnDecideAbility, 0.744f) && Near(p.AIParryOnDecideAbility, 0.636f), "a fresh man's decide abilities are not x1.2: "
                    + p.AIBlockOnDecideAbility + " / " + p.AIParryOnDecideAbility);
                LogHas("[guard] first AI guard this mission (step 96): (agent 9600) at ");
                LogHas(" s, f 1.00 - the game's block-direction guess chance 1.00 → 0.60 (x0.60, AiGuardGuessPercent; before step 30's tired raise), the block-decide ability 0.62 → 0.74 and the parry-decide ability 0.53 → 0.64 (x1.20, AiBlockDecidePercent; at most 0.99 / 0.95)");
                top.UpdateAgentStats(man.Agent, p); // no compounding: the base assigns fresh
                Check(Near(p.AiRandomizedDefendDirectionChance, 0.6f) && Near(p.AIBlockOnDecideAbility, 0.744f), "the guard compounded over two recomputes");

                // TIRED f 0.52: step 30's raise on top of the cut - 0.6 + 0.4 x 0.48 = 0.792 (vanilla + step 30 alone: 1.00)
                for (int i = 0; i < 6; i++) AthleticsMath.Charge(man, in r, t);
                logic.BlockTaken(man, BlockKind.ShieldRightSide, 99, 1, t, in r, mounted: false);
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(man.BlockRandomWeight, 0.48f) && Near(p.AiRandomizedDefendDirectionChance, 0.792f), "f 0.52: the guess chance is not the cut then the raise (0.792): "
                    + p.AiRandomizedDefendDirectionChance + " (w " + man.BlockRandomWeight + ")");
                Check(Near(p.AIBlockOnDecideAbility, 0.744f), "f 0.52: the block-decide ability moved with tiredness");
                // a game value of 0.5 (a better man): 0.3 → 0.3 + 0.7 x 0.48 = 0.636 - still more guesses than fresh
                baseModel.BlockChance = 0.5f;
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AiRandomizedDefendDirectionChance, 0.636f), "own 0.5 at f 0.52: not 0.636: " + p.AiRandomizedDefendDirectionChance);
                baseModel.BlockChance = 1f;
                // empty: every pick a guess
                for (int i = 0; i < 8; i++) logic.BlockTaken(man, BlockKind.ShieldWrongSide, 99, 2 + i, t + i * 0.1, in r, mounted: false);
                top.UpdateAgentStats(man.Agent, p);
                Check(man.Fraction == 0 && Near(p.AiRandomizedDefendDirectionChance, 1f), "an empty man's guess chance is not 1: " + p.AiRandomizedDefendDirectionChance);

                // a veteran: block 0.90 → 1.08 clamped to 0.99, parry 0.79 → 0.948
                var vet = logic.Track(FakeAgent(9602))!;
                vet.AthleticsSkill = 100;
                baseModel.BlockDecide = 0.9f;
                baseModel.ParryDecide = 0.79f;
                var vp = vet.Agent.AgentDrivenProperties;
                top.UpdateAgentStats(vet.Agent, vp);
                Check(Near(vp.AIBlockOnDecideAbility, 0.99f) && Near(vp.AIParryOnDecideAbility, 0.948f), "the veteran's clamp: " + vp.AIBlockOnDecideAbility + " / " + vp.AIParryOnDecideAbility);
                baseModel.BlockDecide = 0.62f;
                baseModel.ParryDecide = 0.53f;

                // never YOU: the re-target gives you 1 / 1 and the decorator leaves the game's values
                var me = logic.Track(FakeAgent(9601))!;
                me.AthleticsSkill = 100;
                logic.SmokePlayer = me.Agent;
                logic.ApplySettingsChange(S); // everyone re-targeted (you are known now)
                Check(me.GuardGuess == 1f && me.GuardDecide == 1f, "your guard factors are not 1: " + me.GuardGuess + " / " + me.GuardDecide);
                var mp = me.Agent.AgentDrivenProperties;
                top.UpdateAgentStats(me.Agent, mp);
                Check(mp.AiRandomizedDefendDirectionChance == 1f && mp.AIBlockOnDecideAbility == 0.62f && mp.AIParryOnDecideAbility == 0.53f, "your values were touched");

                // LIVE: 80 → 0.80; 100 / 100 → the game's values, no write
                var fresh = logic.Track(FakeAgent(9603))!;
                fresh.AthleticsSkill = 100;
                var fp = fresh.Agent.AgentDrivenProperties;
                S.Set(SettingsSchema.AiGuardGuessPercent, 80, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[guard] settings changed mid-mission at ");
                LogHas(" s: AI guard (step 96): on - AI men on foot and riders (never you): the game's block-direction guess chance x0.80 (AiGuardGuessPercent 80");
                Check(Near(fresh.GuardGuess, 0.8f) && fresh.SpeedDirty, "AiGuardGuessPercent 80: not re-targeted (or no recompute asked)");
                top.UpdateAgentStats(fresh.Agent, fp);
                Check(Near(fp.AiRandomizedDefendDirectionChance, 0.8f), "AiGuardGuessPercent 80: not 0.80: " + fp.AiRandomizedDefendDirectionChance);
                S.Set(SettingsSchema.AiGuardGuessPercent, 100, SettingSources.Mcm);
                S.Set(SettingsSchema.AiBlockDecidePercent, 100, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas(" s: AI guard (step 96): off (AiGuardGuessPercent 100 and AiBlockDecidePercent 100) - the game's own block-direction guesses and block decisions");
                Check(fresh.GuardGuess == 1f && fresh.GuardDecide == 1f && fresh.SpeedDirty, "100 / 100: the factors not back to 1 (or no recompute asked)");
                int before = logic.GuardStatsForSmoke.Recomputes;
                top.UpdateAgentStats(fresh.Agent, fp);
                Check(fp.AiRandomizedDefendDirectionChance == 1f && fp.AIBlockOnDecideAbility == 0.62f && logic.GuardStatsForSmoke.Recomputes == before, "100 / 100: the game's values did not come back untouched");
                S.Set(SettingsSchema.AiGuardGuessPercent, 60, SettingSources.Mcm);
                S.Set(SettingsSchema.AiBlockDecidePercent, 120, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                top.UpdateAgentStats(fresh.Agent, fp);
                Check(Near(fp.AiRandomizedDefendDirectionChance, 0.6f) && Near(fp.AIBlockOnDecideAbility, 0.744f), "back at 60 / 120: not written again");

                // THE MASTER SWITCH: off = the game's values at the next recompute even with stale factors; AthleticsEnabled off the same
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                top.UpdateAgentStats(fresh.Agent, fp);
                Check(fp.AiRandomizedDefendDirectionChance == 1f && fp.AIBlockOnDecideAbility == 0.62f && fp.AIParryOnDecideAbility == 0.53f, "ModEnabled off: the guard was still written");
                logic.ApplySettingsChange(S);
                Check(fresh.GuardGuess == 1f && fresh.GuardDecide == 1f, "ModEnabled off: the factors not back to 1 by the re-target");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(Near(fresh.GuardGuess, 0.6f), "back on: the factors not given again");
                S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
                top.UpdateAgentStats(fresh.Agent, fp);
                Check(fp.AiRandomizedDefendDirectionChance == 1f && fp.AIBlockOnDecideAbility == 0.62f, "AthleticsEnabled off: the guard was still written");
                S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);

                var gs = logic.GuardStatsForSmoke;
                Check(gs.Men == 3 && gs.Count(0) > 0 && gs.Count(3) > 0, "the guard's counts by band are off: men " + gs.Men + ", peak " + gs.Count(0) + ", empty " + gs.Count(3));
                typeof(AthleticsLogic).GetMethod("WriteTiredBlockSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
                LogHas("[summary] AI guard factors (step 96 - AiGuardGuessPercent 60, AiBlockDecidePercent 120; AI men on foot and riders, never you): written for 3 AI men (");
                LogHas("; the game's → ours, avg by f band - the block-direction guess chance (before step 30's tired raise) / the block-decide ability / the parry-decide ability: peak (f 1) ");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                S.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
            }
        }
    }
}
