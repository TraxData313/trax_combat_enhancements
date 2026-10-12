using System;
using System.Diagnostics;
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
    /// Step 30 (Anton, 2026-10-12): a tired AI man's block direction is partly random - the real stat decorator writes the
    /// game's own AiRandomizedDefendDirectionChance = own + (1 − own) × w, w = TiredBlockRandomPercent / 100 × (1 − f),
    /// carried by the real logic's re-target (AI_NOTES "Step 30").
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TiredBlockDirection()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                S.Set(SettingsSchema.TiredBlockRandomPercent, 100, SettingSources.File); // DESIGN's value
                Check((int)DrivenProperty.AiRandomizedDefendDirectionChance == 40, "the game's DrivenProperty for the block direction chance is not 40");
                var probe = new AgentDrivenProperties();
                probe.SetStat(DrivenProperty.AiRandomizedDefendDirectionChance, 0.37f);
                Check(Near(probe.AiRandomizedDefendDirectionChance, 0.37f), "AgentDrivenProperties.AiRandomizedDefendDirectionChance does not read its own slot");

                var facts = new FakeWeaponFacts();
                var logic = NewPaceLogic(new FakePaceBody(), facts);
                LogHas("[guard] mission start: tired block direction: on - below the peak line an AI man's block direction is partly random: weight w = 100% × (1 − f) (TiredBlockRandomPercent), so at f 0.5 50% of his picks and empty 100% are a random direction");
                var baseModel = new FreshStatsModel { BlockChance = -1.1f }; // a final-round tournament fighter: never random in vanilla
                var top = new TraxAgentStatModel(new AgentStatCalculateModel[] { baseModel });
                top.Initialize(baseModel);
                var r = Rules;
                double t = 5000;

                // a fresh man: the game's value untouched
                var man = logic.Track(FakeAgent(70))!;
                facts.Shield.Add(70); // step 34: his recomputes count "with a shield in hand"
                man.AthleticsSkill = 100; // pool 100, the peak line 75
                var p = man.Agent.AgentDrivenProperties;
                top.UpdateAgentStats(man.Agent, p);
                Check(man.BlockRandomWeight == 0f && p.AiRandomizedDefendDirectionChance == -1.1f, "a fresh man's chance was touched: " + p.AiRandomizedDefendDirectionChance);

                // f 0.52 after six swings and a right-side block: w 0.48 rides on the block's re-target, the decorator writes 0.48
                for (int i = 0; i < 6; i++) AthleticsMath.Charge(man, in r, t);
                logic.BlockTaken(man, BlockKind.ShieldRightSide, 99, 1, t, in r, mounted: false);
                Check(Near(man.BlockRandomWeight, 0.48f) && man.SpeedDirty, "the block's re-target did not carry w 0.48: " + man.BlockRandomWeight);
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AiRandomizedDefendDirectionChance, 0.48f), "f 0.52 over a game value of -1.10: the chance is not 0.48: " + p.AiRandomizedDefendDirectionChance);
                LogHas("[guard] first tired block direction this mission: (agent 70) at ");
                LogHas(" s, f 0.52 - the game's random-direction chance for him -1.10 (below 0: never random - counted as 0) → 0.48 = own 0.00 + (1 − own) × w, w = 100% × (1 − 0.52) = 0.48 - that share of his blocks goes in a random direction (right 1 in 4 by luck); his blocks right about 64.0% of the time");
                top.UpdateAgentStats(man.Agent, p); // a second recompute (a weapon switch): the base resets it, written once
                Check(Near(p.AiRandomizedDefendDirectionChance, 0.48f), "the chance compounded over two recomputes: " + p.AiRandomizedDefendDirectionChance);
                // the game's own chance counts: 0.2 + 0.8 × 0.48
                baseModel.BlockChance = 0.2f;
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AiRandomizedDefendDirectionChance, 0.584f), "own 0.2 at w 0.48: not 0.584: " + p.AiRandomizedDefendDirectionChance);

                // empty: eight wrong-side blocks (5 each) - w 1, every pick random
                for (int i = 0; i < 8; i++) logic.BlockTaken(man, BlockKind.ShieldWrongSide, 99, 2 + i, t + i * 0.1, in r, mounted: false);
                Check(man.Fraction == 0 && man.BlockRandomWeight == 1f, "eight wrong-side blocks did not empty him to w 1: " + man.Fraction + " / " + man.BlockRandomWeight);
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AiRandomizedDefendDirectionChance, 1f), "an empty man's chance is not 1: " + p.AiRandomizedDefendDirectionChance);

                // never you: emptied, your w stays 0 and the decorator leaves the game's value
                var me = logic.Track(FakeAgent(71))!;
                me.AthleticsSkill = 100;
                logic.SmokePlayer = me.Agent;
                for (int i = 0; i < 10; i++) AthleticsMath.Charge(me, in r, t);
                logic.BlockTaken(me, BlockKind.WeaponParry, 98, 1, t, in r, mounted: false);
                Check(me.Fraction == 0 && me.BlockRandomWeight == 0f, "your tired-block weight is not 0 at empty: " + me.BlockRandomWeight);
                top.UpdateAgentStats(me.Agent, me.Agent.AgentDrivenProperties);
                Check(me.Agent.AgentDrivenProperties.AiRandomizedDefendDirectionChance == 0.2f, "your chance was touched");

                // the slider live: 50 → w 0.5 for the empty man (0.2 + 0.8 × 0.5 = 0.6), 0 → the game's value
                S.Set(SettingsSchema.TiredBlockRandomPercent, 50, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[guard] settings changed mid-mission at ");
                LogHas(" s: tired block direction: on - below the peak line an AI man's block direction is partly random: weight w = 50% × (1 − f)");
                Check(Near(man.BlockRandomWeight, 0.5f), "TiredBlockRandomPercent 50: w is not 0.5 at empty: " + man.BlockRandomWeight);
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AiRandomizedDefendDirectionChance, 0.6f), "TiredBlockRandomPercent 50: the chance is not 0.6: " + p.AiRandomizedDefendDirectionChance);
                S.Set(SettingsSchema.TiredBlockRandomPercent, 0, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas(" s: tired block direction: off (TiredBlockRandomPercent 0) - every AI man blocks as the game decides");
                Check(man.BlockRandomWeight == 0f && man.SpeedDirty, "TiredBlockRandomPercent 0: w not back to 0 (or no recompute asked)");
                top.UpdateAgentStats(man.Agent, p);
                Check(p.AiRandomizedDefendDirectionChance == 0.2f, "TiredBlockRandomPercent 0: the game's value did not come back: " + p.AiRandomizedDefendDirectionChance);
                S.Set(SettingsSchema.TiredBlockRandomPercent, 100, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(man.BlockRandomWeight == 1f, "back at 100: w not 1 at empty");

                // AthleticsEnabled off and the master switch: the game's value (the decorator's gate even with a stale w)
                S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
                top.UpdateAgentStats(man.Agent, p);
                Check(p.AiRandomizedDefendDirectionChance == 0.2f, "AthleticsEnabled off: the chance was still raised");
                S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                top.UpdateAgentStats(man.Agent, p);
                Check(p.AiRandomizedDefendDirectionChance == 0.2f, "ModEnabled off: the chance was still raised (stale w)");
                logic.ApplySettingsChange(S); // the switch seen: everyone full, re-targeted
                Check(man.BlockRandomWeight == 0f && man.Fraction == 1, "ModEnabled off: w not back to 0 by the reset");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                top.UpdateAgentStats(man.Agent, p);
                Check(p.AiRandomizedDefendDirectionChance == 0.2f && man.BlockRandomWeight == 0f, "back on: a fresh start must leave the game's value");

                // the input frame: a braced empty man's own guard DIRECTION passes untouched (the randomness is the game's own)
                var braced = logic.Track(FakeAgent(72))!;
                braced.Input = new AiInputState { BraceHoldAttacks = true };
                uint own = (uint)Agent.MovementControlFlag.DefendLeft | (uint)Agent.MovementControlFlag.Forward;
                float vx = 0f, vy = 1f;
                uint flags = own;
                AiInputHook.Apply(braced, braced.Input, true, ref flags, ref vx, ref vy, false);
                Check(flags == own, "the input frame changed a braced man's own guard direction: " + flags.ToString("X"));

                // the guard by f band (before a block's charge), never you, nothing while Athletics is off; step 34: split by what
                // the DEFENDER holds as the blow arrives (the weapon read - a shield taken up or dropped mid-fight moves him over)
                r = Rules;
                var fresh = logic.Track(FakeAgent(73))!;
                facts.Shield.Add(73);
                for (int i = 0; i < 8; i++) logic.GuardTaken(fresh, BlockKind.ShieldRightSide, in r);
                facts.Shield.Remove(73); // the shield gone: the next two blows count "without"
                logic.GuardTaken(fresh, BlockKind.WeaponParry, in r);
                logic.GuardTaken(fresh, BlockKind.None, in r);
                var empty = logic.Track(FakeAgent(74))!;
                for (int i = 0; i < 10; i++) AthleticsMath.Charge(empty, in r, t);
                facts.Shield.Add(74);
                logic.GuardTaken(empty, BlockKind.ShieldWrongSide, in r);
                logic.GuardTaken(empty, BlockKind.ShieldRightSide, in r);
                facts.Shield.Remove(74);
                logic.GuardTaken(empty, BlockKind.None, in r);
                logic.GuardTaken(empty, BlockKind.None, in r);
                int readsBefore = facts.Reads;
                logic.GuardTaken(me, BlockKind.None, in r); // you: never counted
                S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
                var off = Rules;
                logic.GuardTaken(fresh, BlockKind.None, in off);
                S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);
                Check(facts.Reads == readsBefore, "the weapon was read for a blow that is not counted (you / Athletics off)");
                var ts = logic.TiredBlockStatsForSmoke;
                Check(ts.Blows(0) == 10 && ts.Blows(3) == 4 && ts.Landed(3) == 2 && ts.WrongSide(3) == 1, "the guard by f band is off: " + ts.Blows(0) + " / " + ts.Blows(3));
                Check(ts.Blows(0, true) == 8 && ts.Blows(0, false) == 2 && ts.Landed(0, false) == 1 && ts.Parried(0, false) == 1,
                    "step 34: the peak band's split by a shield in hand is off: " + ts.Blows(0, true) + " with / " + ts.Blows(0, false) + " without");
                Check(ts.Blows(3, true) == 2 && ts.WrongSide(3, true) == 1 && ts.Landed(3, true) == 0 && ts.Blows(3, false) == 2 && ts.Landed(3, false) == 2,
                    "step 34: the empty band's split by a shield in hand is off: " + ts.Blows(3, true) + " with / " + ts.Blows(3, false) + " without");
                Check(ts.Recomputes > 0 && ts.RecomputesWithShield == ts.Recomputes, "step 34: the shield man's recomputes were not counted with a shield: "
                    + ts.RecomputesWithShield + " of " + ts.Recomputes);
                Check(ts.Men == 1 && ts.Raised(1) >= 1 && ts.Raised(3) >= 1, "the raised chance was not counted by band (men " + ts.Men + ")");

                typeof(AthleticsLogic).GetMethod("WriteTiredBlockSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
                LogHas("[summary] tired block direction (TiredBlockRandomPercent 100, step 30): the game's random-direction chance raised for 1 AI men (");
                LogHas(" recomputes - " + ts.Recomputes + " with a shield in hand, 0 without; ");
                LogHas("; his own → written, avg by f band: f 0.5-1 ");
                LogHas("[summary] AI guard by f (melee blows that reached AI men; kicks, bashes, charges and missiles left out - step 30: tired men should block worse): peak (f 1) 10 blows - blocked 90.0% (shield right side 80.0%, WRONG side 0.0%, parry 10.0%), landed 10.0%; f 0.5-1 none; f below 0.5 none; empty (f 0) 4 blows - blocked 50.0% (shield right side 25.0%, WRONG side 25.0%, parry 0.0%), landed 50.0%");
                LogHas("[summary] AI guard by f WITH a shield in hand (when the blow came - step 34): peak (f 1) 8 blows - blocked 100.0% (shield right side 100.0%, WRONG side 0.0%, parry 0.0%), landed 0.0%; f 0.5-1 none; f below 0.5 none; empty (f 0) 2 blows - blocked 100.0% (shield right side 50.0%, WRONG side 50.0%, parry 0.0%), landed 0.0% (a wrong direction with a shield = a WRONG-side block: the shield still stops the blow)");
                LogHas("[summary] AI guard by f WITHOUT a shield (when the blow came - step 34): peak (f 1) 2 blows - blocked 50.0% (shield right side 0.0%, WRONG side 0.0%, parry 50.0%), landed 50.0%; f 0.5-1 none; f below 0.5 none; empty (f 0) 2 blows - blocked 0.0% (shield right side 0.0%, WRONG side 0.0%, parry 0.0%), landed 100.0% (a wrong direction without a shield = the blow lands: empty should land far more than peak)");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                S.Set(SettingsSchema.VerboseLogging, true, SettingSources.File); // as step 29 left it
            }
        }

        /// <summary>The cost: a thousand tired AI men each blocking (the re-target carries w) and recomputed through the real
        /// decorator (the chance written) - per blow, not per tick: nothing of step 30 runs every frame.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TiredBlockCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                S.Set(SettingsSchema.TiredBlockRandomPercent, 100, SettingSources.File);
                S.Set(SettingsSchema.CostPerShieldBlock, 0.5, SettingSources.File);
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                var baseModel = new FreshStatsModel();
                var top = new TraxAgentStatModel(new AgentStatCalculateModel[] { baseModel });
                top.Initialize(baseModel);
                const int men = 1000;
                var all = new TrackedAgent[men];
                var r = Rules;
                double t = 9000;
                for (int i = 0; i < men; i++)
                {
                    var st = logic.Track(FakeAgent(4000 + i))!;
                    st.AthleticsSkill = 100;
                    for (int k = 0; k < 3 + i % 4; k++) AthleticsMath.Charge(st, in r, t); // 70 down to 40: all below the peak line
                    all[i] = st;
                }
                // warm up (the first raised chance's line, the JIT)
                for (int i = 0; i < men; i++)
                {
                    logic.BlockTaken(all[i], BlockKind.ShieldRightSide, 1, 1, t, in r, mounted: false);
                    top.UpdateAgentStats(all[i].Agent, all[i].Agent.AgentDrivenProperties);
                }
                const int rounds = 40;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                int raised = 0;
                for (int k = 1; k <= rounds; k++)
                    for (int i = 0; i < men; i++)
                    {
                        var st = all[i];
                        logic.BlockTaken(st, BlockKind.ShieldRightSide, 1, 1 + k, t + k * 1.5, in r, mounted: false);
                        top.UpdateAgentStats(st.Agent, st.Agent.AgentDrivenProperties);
                        if (st.Agent.AgentDrivenProperties.AiRandomizedDefendDirectionChance > 0.2f) raised++;
                    }
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double usPerBlow = sw.Elapsed.TotalMilliseconds * 1000.0 / (rounds * men);
                Console.WriteLine("        step 30 cost: " + men + " tired men x " + rounds + " blocks, each re-targeted and recomputed through the decorator - "
                                  + usPerBlow.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " µs a blow, " + allocated + " bytes allocated");
                Check(raised == rounds * men, "cost: not every tired man's chance was raised: " + raised);
                Check(usPerBlow < 20, "cost: a block + recompute took " + usPerBlow + " µs (budget 20)");
                Check(allocated < 256 * 1024, "cost: step 30 allocates per blow - " + allocated + " bytes over " + rounds * men + " blows");
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
