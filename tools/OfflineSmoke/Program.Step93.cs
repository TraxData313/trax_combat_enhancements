using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 93 - AN ATHLETICS FLOOR BY TIER (Toni 2026-10-25; AI_NOTES "Step 93", DESIGN decision 85): men carrying a REAL
    /// <c>BasicCharacterObject</c> (a stand-in subclass: its Level gives the game's own <c>GetBattleTier()</c>, its IsHero the hero flag) tracked by
    /// the REAL logic - the tier read at spawn; a T6 rider with Athletics 60 gets a 150-point bar, a hero with 60 keeps 60, a T2 rider with 20 gets
    /// 50, a T4 rider with 60 gets 90; a live floor change re-pools the men on the field and each keeps his share; 0 = no floor; the master switch;
    /// the first-lifted line, the settings line and the [summary] pools line. The game's skill read needs <c>Game.Current</c> (DefaultSkills), which
    /// the smoke has not: the skill is set after Track as in the older smokes, so the spawn-time lines and the summary see skill 0.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>A troop / hero character with only what step 93 reads: Level (→ GetBattleTier) and IsHero.</summary>
        private sealed class TierCharacter : BasicCharacterObject
        {
            private readonly bool _hero;

            public TierCharacter(int level, bool hero)
            {
                Level = level;
                _hero = hero;
            }

            public override bool IsHero => _hero;
        }

        private static Action<Agent, BasicCharacterObject>? _setCharacter;

        private static Agent CharacterAgent(int index, int level, bool hero)
        {
            var a = FakeAgent(index);
            _setCharacter ??= FieldSetter<BasicCharacterObject>("_character"); // reflection's SetValue would run Agent's static constructor (the engine)
            _setCharacter(a, new TierCharacter(level, hero));
            return a;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TierFloorsThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults(); // the shipped floors 50 / 50 / 70 / 90 / 120 / 150 (AthleticsDefaults pins none)
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                Check(new TierCharacter(31, false).GetBattleTier() == 6 && new TierCharacter(21, false).GetBattleTier() == 4
                      && new TierCharacter(11, false).GetBattleTier() == 2 && new TierCharacter(4, false).GetBattleTier() == 0
                      && new TierCharacter(31, true).GetBattleTier() == 7, "the game's GetBattleTier did not give 6 / 4 / 2 / 0 / hero 7 for levels 31 / 21 / 11 / 4");

                TrackedAgent Man(int index, int level, bool hero, int skill)
                {
                    var m = logic.Track(CharacterAgent(index, level, hero))!;
                    m.AthleticsSkill = skill;
                    return m;
                }

                double PoolOf(TrackedAgent m) => AthleticsLogic.TryGetReading(m.Agent, out var r) ? r.Pool : double.NaN;

                var knight = Man(9300, 31, false, 60);  // the T6 Vlandian Banner Knight
                var lord = Man(9301, 31, true, 60);     // a lord with Athletics 60
                var rider = Man(9302, 11, false, 20);   // a T2 rider (median 20)
                var cavalry = Man(9303, 21, false, 60); // Vlandian Cavalry, T4
                var looter = Man(9304, 4, false, 5);    // T0: T1's floor
                var plain = logic.Track(FakeAgent(9305))!; // no character: no tier
                plain.AthleticsSkill = 60;

                Check(knight.Tier == 6 && !knight.IsHero, "the knight's tier: " + knight.Tier);
                Check(lord.Tier == -1 && lord.IsHero, "a hero got a tier: " + lord.Tier);
                Check(rider.Tier == 2 && cavalry.Tier == 4 && looter.Tier == 0 && plain.Tier == -1,
                    "tiers " + rider.Tier + " / " + cavalry.Tier + " / " + looter.Tier + " / " + plain.Tier);
                Check(PoolOf(knight) == 150, "a T6 rider with skill 60 did not get 150: " + PoolOf(knight));
                Check(PoolOf(lord) == 60, "a hero with skill 60 did not keep 60: " + PoolOf(lord));
                Check(PoolOf(rider) == 50, "a T2 rider with skill 20 did not get 50: " + PoolOf(rider));
                Check(PoolOf(cavalry) == 90, "a T4 rider with skill 60 did not get 90: " + PoolOf(cavalry));
                Check(PoolOf(looter) == 50 && PoolOf(plain) == 60, "T0 / no character: " + PoolOf(looter) + " / " + PoolOf(plain));
                LogHas("[athletics] first man lifted by the tier floor: ");
                LogHas(", tier T6, Athletics skill 0 → pool 150 (the skill alone 50 with at least 50; T6's floor 150) - non-hero troops only, heroes and you keep the real skill");

                // a live change re-pools the men already on the field - each keeps his share
                SetBar(knight, 0.6);
                S.Set(SettingsSchema.AthleticsFloorT6, 200, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(AthleticsLogic.TryGetReading(knight.Agent, out var k200) && k200.Pool == 200 && Near(k200.Points, 120) && Near(knight.Fraction, 0.6),
                    "T6 200 live did not re-pool the knight at his 60%: pool " + k200.Pool + ", points " + k200.Points);
                Check(PoolOf(lord) == 60 && PoolOf(cavalry) == 90, "a T6 change touched a hero or a T4 man");
                LogHas("[athletics] pool settings now: the Athletics skill x1.00, at least 50 - pools now min ");
                LogHas("; tier floors (non-heroes) T1-T6 50/50/70/90/120/200, lifted by the tier floor: ");
                S.Set(SettingsSchema.AthleticsFloorT6, 0, SettingSources.Mcm); // 0 = no floor for that tier
                logic.ApplySettingsChange(S);
                Check(PoolOf(knight) == 60 && Near(knight.Fraction, 0.6), "T6 0 did not give the knight his skill back: " + PoolOf(knight));
                LogHas("tier floors (non-heroes) T1-T6 50/50/70/90/120/0");
                S.Set(SettingsSchema.AthleticsFloorT6, 150, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(PoolOf(knight) == 150, "T6 back to 150");

                // the master switch first: off = no tier floor (the bar is vanilla anyway); back on = the floors again
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(PoolOf(knight) == 60, "ModEnabled off kept the tier floor: " + PoolOf(knight));
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(PoolOf(knight) == 150 && PoolOf(lord) == 60, "ModEnabled back on");

                // the summary's pools line (spawn-time skills: 0 here, see the class note)
                Check(logic.Stats.TierLift(AthleticsRules.From(S), 6, out int men6, out _, out double pool6) && men6 == 1 && pool6 == 150,
                    "the summary's T6 count: " + men6);
                logic.WriteAthleticsSummary();
                LogHas("; tier floors (non-heroes) T1-T6 50/50/70/90/120/150, lifted by the tier floor: T4 1 (skill 0.0 → pool 90.0) / T6 1 (skill 0.0 → pool 150.0)");
            }
            finally
            {
                AthleticsDefaults();
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }
    }
}
