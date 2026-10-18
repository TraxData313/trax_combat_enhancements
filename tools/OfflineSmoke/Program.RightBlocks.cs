using System;
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
    /// Offline check for step 56 - RIGHT BLOCKS FAVOUR THE DEFENDER (AI_NOTES "Step 56", DESIGN decision 48): the defaults EMBEDDED in the
    /// real DLL (defaults.json: right block / parry 30 / 10, riposte 70, the block stun 0, a correct block counting 25 % of the blocker's T,
    /// the riposte never lowered by it) through the REAL logic and damage decorator with the stand-in body - two EMPTY even men: a right-side
    /// block leaves the blocker standing and the ATTACKER reels (his forced stagger from the tick); the same men on the WRONG side: the
    /// blocker staggered and disarmed at 90 / 50 / 40; two fresh men: nothing either way; the block stun = the game's own.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RightBlocksFavourTheDefender()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                // the embedded defaults.json carries step 56's values (DESIGN's Default column keeps the initial 80 / 40 / 50 / 100)
                Check(SettingsSchema.RightBlockStaggerMaxPercent.Default == 30 && SettingsSchema.RightBlockKnockdownMaxPercent.Default == 10
                      && SettingsSchema.ParryStaggerMaxPercent.Default == 30 && SettingsSchema.ParryKnockdownMaxPercent.Default == 10
                      && SettingsSchema.RiposteStaggerMaxPercent.Default == 70 && SettingsSchema.BlockStunTiredExtraPercent.Default == 0
                      && SettingsSchema.CorrectBlockTiredWeightPercent.Default == 25 && SettingsSchema.RiposteBlockerTiredPercent.Default == 0,
                    "the embedded defaults are not step 56's: right " + SettingsSchema.RightBlockStaggerMaxPercent.Default + " / " + SettingsSchema.RightBlockKnockdownMaxPercent.Default
                    + ", riposte " + SettingsSchema.RiposteStaggerMaxPercent.Default + ", stun " + SettingsSchema.BlockStunTiredExtraPercent.Default);
                Check(ConfigFile.FormatVersion == 10, "the config format is not 10: " + ConfigFile.FormatVersion);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                double t = 60000;
                logic.TickImpact(t);

                TrackedAgent Man(int index, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, bar);
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);

                // ---- 1. two EMPTY even men, a RIGHT-side block: the blocker stands, the attacker reels
                var attacker = Man(6100, 0.0);
                inputs.Weapons[6100] = (1.2, 92, ImpactDamageType.Cut, 100);
                var blocker = Man(6101, 0.0);
                inputs.Guards[6101] = (4.0, 100);
                inputs.Shields.Add(6101);
                Tick();
                Check(Near(StunFromWorker(model, attacker, blocker, 0.1f, 0.3f).Def, 0.3f), "BlockStunTiredExtraPercent 0: an exhausted blocker's stun is not the game's own");
                dice.Next(0.5, 0.0, 0.5); // his stagger (if rolled: no), the riposte (yes), its recoil at the roll's centre
                logic.ImpactModelHitTaken(attacker.Agent, blocker.Agent, RightBlock());
                var d = logic.LastImpact;
                Check(d.Event == ImpactEvent.RightBlock && Close(d.TiredWeight, 0.25, 1e-9) && d.X <= 0.2501, "the right-side block's x: " + d.X + " (T counted x" + d.TiredWeight + ")");
                Check(!d.Stagger.Happens && d.Stagger.Chance <= 0.05 && d.Knockdown.Chance == 0 && Close(d.StunFactor, 1.0, 1e-9),
                    "the EMPTY blocker of a right-side block: stagger " + d.Stagger.Chance + ", down " + d.Knockdown.Chance + ", stun x" + d.StunFactor);
                Check(d.Riposte.Happens && d.Riposte.Chance >= 0.69 && attacker.Impact.Reeling(t + 0.1) && blocker.Impact.Recent(t, 8) == 0,
                    "the EMPTY attacker into a right-side block: riposte " + d.Riposte.Chance);
                double ripost = d.Riposte.Chance, blockerStagger = d.Stagger.Chance;
                Tick();
                Check(body.Staggers.Exists(s => s.Man == 6100) && !body.Staggers.Exists(s => s.Man == 6101) && body.OwnBlows.Count == 0,
                    "the riposte's stagger not made on the ATTACKER from the tick (or the blocker staggered / knocked down)");
                // a parry the same: the attacker's risk
                var parrier = Man(6102, 0.0);
                inputs.Guards[6102] = (1.2, 100);
                dice.Next(0.5, 0.0, 0.5);
                logic.ImpactModelHitTaken(Man(6103, 0.0).Agent, parrier.Agent, Parried());
                d = logic.LastImpact;
                Check(d.Event == ImpactEvent.Parry && d.Stagger.Chance <= 0.05 && d.Knockdown.Chance == 0 && d.Riposte.Chance >= 0.69,
                    "the EMPTY parrier: stagger " + d.Stagger.Chance + ", down " + d.Knockdown.Chance + ", riposte " + d.Riposte.Chance);

                // ---- 2. the same men, a WRONG-side block: his own risk at 90 / 50 / 40, no riposte
                var turtle = Man(6104, 0.0);
                inputs.Guards[6104] = (4.0, 100);
                inputs.Shields.Add(6104);
                body.ShieldInHand[6104] = 3;
                dice.Next(0.999, 0.0, 0.0, 0.5); // no knockdown, the shield, the stagger, its length
                logic.ImpactModelHitTaken(Man(6105, 0.0).Agent, turtle.Agent, WrongBlock());
                d = logic.LastImpact;
                Check(d.Event == ImpactEvent.WrongBlock && Close(d.X, 1.0, 1e-9) && Close(d.Stagger.Chance, 0.9, 1e-6) && Close(d.ShieldDrop.Chance, 0.5, 1e-6)
                      && Close(d.Knockdown.Chance, 0.4, 1e-6) && !d.Riposte.InRow, "the WRONG-side block of two empty men: " + d.Stagger.Chance + " / " + d.ShieldDrop.Chance + " / " + d.Knockdown.Chance);
                Tick();
                Check(body.Drops.Contains((6104, 3)) && body.Staggers.Exists(s => s.Man == 6104), "the wrong-side block's drop / stagger not made from the tick");

                // ---- 3. two FRESH even men: nothing either way
                var a2 = Man(6106, 1.0);
                inputs.Weapons[6106] = (1.2, 92, ImpactDamageType.Cut, 100);
                var b2 = Man(6107, 1.0);
                inputs.Guards[6107] = (4.0, 100);
                inputs.Shields.Add(6107);
                dice.Next(0.999, 0.999, 0.999);
                logic.ImpactModelHitTaken(a2.Agent, b2.Agent, RightBlock());
                d = logic.LastImpact;
                Check(d.Stagger.Chance == 0 && d.Knockdown.Chance == 0 && d.Riposte.Chance <= 0.015, "two FRESH even men: stagger " + d.Stagger.Chance + ", riposte " + d.Riposte.Chance);
                Console.WriteLine("        step 56: two empty even men, a right-side block - the blocker staggered " + (100 * blockerStagger).ToString("0.0", CultureInfo.InvariantCulture)
                                  + "%, the attacker reels " + (100 * ripost).ToString("0", CultureInfo.InvariantCulture) + "%; the wrong side 90 / 50 / 40");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                Step56BlockModel();
            }
        }
    }
}
