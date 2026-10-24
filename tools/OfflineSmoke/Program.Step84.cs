using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 84 (Toni 2026-10-24: "they all should be able to afford a piece of wood"): TIER-1 MEN GET A CHEAP CULTURE SHIELD through the real
    /// logic's spawn path (<c>AthleticsLogic.StartUnitKit</c> / <c>GiveUnitKit</c> - what OnAgentBuild calls after the game's build) with a
    /// stand-in engine side: every main culture's recruit in each of his four vanilla variants (spnpccharacters.xml), a Nord youngling already
    /// carrying a shield, a hero, a rider, a tier-2 man, the switch and the master switch, a culture without settings, an unknown id, the
    /// lines and the summary. What it cannot check: the engine's own EquipWeaponWithNewEntity and the AI raising the shield (PLAYTEST D49).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The stand-in engine side: each fake agent's read and what was given.</summary>
        private sealed class FakeUnitKitBody : IUnitKitBody
        {
            public sealed class Man
            {
                public KitAgent Facts;
                public readonly string?[] Ids = new string?[UnitKitMath.Slots];
                public readonly bool[] TwoHanded = new bool[UnitKitMath.Slots];
                public readonly bool[] IsShield = new bool[UnitKitMath.Slots];
                public int Given;
            }

            public readonly Dictionary<Agent, Man> Men = new Dictionary<Agent, Man>();
            public string? Fixed;
            public int Reads, Gives;
            public IKitItems Items { get; } = new FakeKitItems();

            public string? FixedEquipment(Mission? mission) => Fixed;

            public bool Read(Agent agent, ref KitAgent man, KitSlot[] slots)
            {
                Reads++;
                if (!Men.TryGetValue(agent, out var m)) return false;
                man = m.Facts;
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i] = default;
                    if (m.Ids[i] == null) { slots[i].Empty = true; continue; }
                    slots[i].Id = m.Ids[i];
                    if (m.IsShield[i]) { slots[i].Shield = true; continue; }
                    slots[i].Melee = true;
                    slots[i].OneHand = !m.TwoHanded[i];
                }
                return true;
            }

            public bool Give(Agent agent, in KitOrder order, Banner? banner)
            {
                Gives++;
                var m = Men[agent];
                m.Given++;
                m.Ids[order.ShieldSlot] = (string)order.Shield!;
                m.IsShield[order.ShieldSlot] = true;
                if (order.SwapSlot >= 0 && order.Spear != null)
                {
                    m.Ids[order.SwapSlot] = (string)order.Spear;
                    m.TwoHanded[order.SwapSlot] = false; // the spear's one-handed usage
                }
                return true;
            }
        }

        /// <summary>The game's item table in miniature: the agreed shields and spears, the real weights / hit points.</summary>
        private sealed class FakeKitItems : IKitItems
        {
            private readonly Dictionary<string, KitItemFacts> _items = new Dictionary<string, KitItemFacts>(StringComparer.Ordinal);

            public FakeKitItems()
            {
                Shield("battered_kite_shield", "Simple Kite Shield", null, 2.6f, 220);
                Shield("adarga", "Adarga", "aserai", 1.3f, 160);
                Shield("battania_targe_b", "Targe", "battania", 1.6f, 215);
                Shield("eastern_wicker_shield", "Wicker Square Shield", "khuzait", 2.7f, 260);
                Shield("northern_round_shield", "Round Shield", "sturgia", 3.0f, 300);
                Shield("heater_shield_with_cutout", "Heater Shield with Cutout", "vlandia", 2.1f, 220);
                Shield("nord_shield_tier_1", "Nord Plank Shield", "nord", 2.4f, 200);
                Spear("imperial_spear_t2", "Short Militia Spear", "empire");
                Spear("western_spear_2_t2", "Simple Short Spear", "vlandia");
                Spear("eastern_spear_1_t2", "Jagged Spear", "aserai");
                Spear("nord_spear_1_t2", "Knobbed Short Spear", "nord");
            }

            private void Shield(string id, string name, string? culture, float kg, int hp) =>
                _items[id] = new KitItemFacts { Id = id, Name = name, Shield = true, Culture = culture, Merchandise = true, Weight = kg, HitPoints = hp, Value = 100 };

            private void Spear(string id, string name, string culture) =>
                _items[id] = new KitItemFacts { Id = id, Name = name, OneHandMelee = true, Polearm = true, Culture = culture, Merchandise = true, Weight = 2.0f, Value = 70 };

            public bool TryFind(string id, out object item, out KitItemFacts facts)
            {
                bool ok = _items.TryGetValue(id, out facts);
                item = ok ? facts.Id : null!;
                return ok;
            }

            public bool TryCheapest(string culture, bool shield, out object item, out KitItemFacts facts)
            {
                var best = _items.Values.Where(f => string.Equals(f.Culture, culture, StringComparison.OrdinalIgnoreCase) && (shield ? f.Shield : f.Polearm && f.OneHandMelee))
                    .OrderBy(f => f.Value).ToList();
                facts = best.FirstOrDefault();
                item = best.Count > 0 ? facts.Id : null!;
                return best.Count > 0;
            }
        }

        // The six main cultures' tier-1 recruits, their four vanilla battle variants (spnpccharacters.xml, Item0), "2h" = no one-handed usage
        // (weapon_descriptions.xml + the crafted pieces: the scythe and the maul; the pitchforks have a OneHandedPolearm usage).
        private static readonly (string Culture, string Troop, string[] Variants)[] Recruits =
        {
            ("empire", "imperial_recruit", new[] { "peasant_pitchfork_2_t1", "2h:peasant_polearm_1_t1", "peasant_sickle_1_t1", "empire_sword_1_t2" }),
            ("aserai", "aserai_recruit", new[] { "aserai_sword_2_t2", "peasant_hammer_1_t1", "peasant_hammer_2_t1", "peasant_hatchet_1_t1" }),
            ("battania", "battanian_volunteer", new[] { "2h:peasant_maul_t1_2", "peasant_hatchet_1_t1", "battania_mace_2_t2", "peasant_pickaxe_1_t1" }),
            ("khuzait", "khuzait_nomad", new[] { "khuzait_mace_1_t2", "peasant_pitchfork_1_t1", "2h:peasant_polearm_1_t1", "peasant_sickle_1_t1" }),
            ("sturgia", "sturgian_recruit", new[] { "sturgia_axe_2_t2", "peasant_hammer_1_t1", "peasant_hammer_2_t1", "peasant_sickle_1_t1" }),
            ("vlandia", "vlandian_recruit", new[] { "vlandia_mace_1_t2", "peasant_pitchfork_1_t1", "peasant_hammer_1_t1", "peasant_pickaxe_1_t1" }),
        };

        private static readonly Dictionary<string, (string Shield, string? Spear)> Expected = new Dictionary<string, (string, string?)>
        {
            ["empire"] = ("battered_kite_shield", "imperial_spear_t2"),
            ["aserai"] = ("adarga", null),
            ["battania"] = ("battania_targe_b", "western_spear_2_t2"),
            ["khuzait"] = ("eastern_wicker_shield", "eastern_spear_1_t2"),
            ["sturgia"] = ("northern_round_shield", null),
            ["vlandia"] = ("heater_shield_with_cutout", "western_spear_2_t2"),
        };

        private static void Step84Defaults()
        {
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.GiveTier1Shields, true, SettingSources.File);
            foreach (var p in SettingsSchema.InGroup(SettingsSchema.UnitsGroup)) S.Set(p, p.Default, SettingSources.File);
        }

        private static (AthleticsLogic Logic, FakeUnitKitBody Body) NewKitLogic(string? fixedGear = null)
        {
            var logic = new AthleticsLogic();
            var body = new FakeUnitKitBody { Fixed = fixedGear };
            logic.UnitKitBody = body;
            logic.StartUnitKit();
            return (logic, body);
        }

        private static Agent KitMan(FakeUnitKitBody body, int index, string? culture, string? weapon, int tier = 1, bool hero = false, bool mounted = false, string? shield = null)
        {
            var a = FakeAgent(index);
            var m = new FakeUnitKitBody.Man { Facts = new KitAgent { Culture = culture, Tier = tier, Hero = hero, Mounted = mounted } };
            if (weapon != null)
            {
                bool two = weapon.StartsWith("2h:", StringComparison.Ordinal);
                m.Ids[0] = two ? weapon.Substring(3) : weapon;
                m.TwoHanded[0] = two;
            }
            if (shield != null)
            {
                m.Ids[1] = shield;
                m.IsShield[1] = true;
            }
            body.Men[a] = m;
            return a;
        }

        private static void TierOneShields()
        {
            try
            {
                Step84Defaults();
                var (logic, body) = NewKitLogic();
                LogHas("[units] tier-1 shields (step 84): on");
                LogHas("empire: shield Simple Kite Shield (battered_kite_shield, 2.6 kg, 220 hit points, value 100) [UnitShieldEmpire]");
                LogHas("khuzait: shield Wicker Square Shield (eastern_wicker_shield, 2.7 kg, 260 hit points, value 100) [UnitShieldKhuzait]");
                LogHas("spear Jagged Spear (eastern_spear_1_t2, 2.0 kg, value 70) [UnitSpearKhuzait]");

                // 1. every culture, every variant: the culture's shield in slot 1; the scythe / maul swapped for the spear, every other weapon kept
                int index = 9000;
                int swapped = 0;
                foreach (var (culture, troop, variants) in Recruits)
                {
                    foreach (var v in variants)
                    {
                        var a = KitMan(body, index++, culture, v);
                        logic.GiveUnitKit(a, null);
                        var m = body.Men[a];
                        bool two = v.StartsWith("2h:", StringComparison.Ordinal);
                        string weapon = two ? v.Substring(3) : v;
                        Check(m.Given == 1, troop + " (" + weapon + "): given " + m.Given + " times");
                        Check(m.IsShield[1] && m.Ids[1] == Expected[culture].Shield, troop + " (" + weapon + "): slot 1 holds " + m.Ids[1] + ", not " + Expected[culture].Shield);
                        string want = two ? Expected[culture].Spear! : weapon;
                        Check(m.Ids[0] == want && !m.TwoHanded[0], troop + " (" + weapon + "): slot 0 holds " + m.Ids[0] + ", not " + want);
                        Check(m.Ids[2] == null && m.Ids[3] == null, troop + ": slots 2-3 not left free");
                        if (two) swapped++;
                    }
                }
                Check(swapped == 3, "precondition: 3 two-handed variants (the Imperial and Khuzait scythe, the Battanian maul), got " + swapped);
                foreach (var c in Expected.Keys)
                    Check(logic.UnitKit!.ShieldsGiven(c) == 4, c + ": " + logic.UnitKit!.ShieldsGiven(c) + " shields, not 4");
                Check(logic.UnitKit!.SpearsSwapped("empire") == 1 && logic.UnitKit.SpearsSwapped("khuzait") == 1 && logic.UnitKit.SpearsSwapped("battania") == 1
                      && logic.UnitKit.SpearsSwapped("vlandia") == 0, "spears swapped by culture wrong");
                LogHas("first a tier-1 man of empire given Simple Kite Shield (battered_kite_shield, 2.6 kg, 220 hit points, value 100) in slot 1");
                LogHas("first two-hander: a tier-1 man of empire given Simple Kite Shield (battered_kite_shield, 2.6 kg, 220 hit points, value 100) in slot 1, his two-hander peasant_polearm_1_t1 swapped for Short Militia Spear (imperial_spear_t2");
                LogHas("first a tier-1 man of battania given Targe (battania_targe_b, 1.6 kg, 215 hit points, value 100) in slot 1, his two-hander peasant_maul_t1_2 swapped for Simple Short Spear (western_spear_2_t2");

                // 2. men left alone: a Nord youngling with his shield, a hero, a rider, a tier-2 man, a tier-0 looter
                int givesBefore = body.Gives;
                var nord = KitMan(body, index++, "nord", "nord_spear_1_t2", shield: "nord_shield_tier_2_d");
                var hero = KitMan(body, index++, "empire", "2h:peasant_polearm_1_t1", tier: 1, hero: true);
                var rider = KitMan(body, index++, "khuzait", "peasant_sickle_1_t1", mounted: true);
                var t2 = KitMan(body, index++, "vlandia", "western_spear_1_t2", tier: 2);
                var looter = KitMan(body, index++, "looters", "stone", tier: 0);
                foreach (var a in new[] { nord, hero, rider, t2, looter }) logic.GiveUnitKit(a, null);
                Check(body.Gives == givesBefore, "a man who should be left alone was given gear (" + (body.Gives - givesBefore) + ")");
                Check(body.Men[nord].Ids[1] == "nord_shield_tier_2_d" && body.Men[nord].Ids[0] == "nord_spear_1_t2", "the Nord youngling's own gear touched");
                Check(body.Men[hero].Ids[0] == "peasant_polearm_1_t1", "a hero's scythe swapped");

                // 3. a culture without settings: its cheapest merchandise shield and spear; one with none skipped and said once
                var nordNoShield = KitMan(body, index++, "nord", "2h:nord_great_axe");
                logic.GiveUnitKit(nordNoShield, null);
                Check(body.Men[nordNoShield].Ids[1] == "nord_shield_tier_1" && body.Men[nordNoShield].Ids[0] == "nord_spear_1_t2", "a shieldless Nord: " + body.Men[nordNoShield].Ids[1] + " / " + body.Men[nordNoShield].Ids[0]);
                LogHas("nord (no settings of its own): shield Nord Plank Shield (nord_shield_tier_1");
                var bandit1 = KitMan(body, index++, "mountain_bandits", "peasant_hatchet_1_t1");
                var bandit2 = KitMan(body, index++, "mountain_bandits", "peasant_hatchet_1_t1");
                logic.GiveUnitKit(bandit1, null);
                logic.GiveUnitKit(bandit2, null);
                Check(body.Men[bandit1].Given == 0 && body.Men[bandit2].Given == 0, "a culture without any shield was given one");
                Check(LogText.Split('\n').Count(l => l.Contains("first tier-1 man of mountain_bandits skipped")) == 1, "the culture without a shield not said exactly once");

                // 4. the switch off: nothing given, no engine read; on again: given
                int reads = body.Reads;
                S.Set(SettingsSchema.GiveTier1Shields, false, SettingSources.Mcm);
                var off = KitMan(body, index++, "empire", "2h:peasant_polearm_1_t1");
                logic.GiveUnitKit(off, null);
                Check(body.Men[off].Given == 0 && body.Reads == reads, "GiveTier1Shields off still gave / read");
                S.Set(SettingsSchema.GiveTier1Shields, true, SettingSources.Mcm);
                logic.GiveUnitKit(off, null);
                Check(body.Men[off].Given == 1 && body.Men[off].Ids[0] == "imperial_spear_t2", "back on: not given");

                // 5. an unknown id mid-battle: that culture skipped and said; a real one again: given
                S.Set(SettingsSchema.UnitShieldAserai, TextValues.TryIndex("my_mod_buckler", out int bad) ? bad : -1, SettingSources.Mcm);
                var asr = KitMan(body, index++, "aserai", "peasant_hammer_1_t1");
                logic.GiveUnitKit(asr, null);
                Check(body.Men[asr].Given == 0, "an unknown Aserai shield id still gave a shield");
                LogHas("aserai: UnitShieldAserai now SKIPPED - no item \"my_mod_buckler\" in this game");
                S.Set(SettingsSchema.UnitShieldAserai, SettingsSchema.UnitShieldAserai.Default, SettingSources.Mcm);
                logic.GiveUnitKit(asr, null);
                Check(body.Men[asr].Ids[1] == "adarga", "the Aserai shield back: not given");

                // 6. a tournament / arena: fixed gear
                var (arena, arenaBody) = NewKitLogic("a tournament or arena fight (TournamentFightMissionController)");
                var fighter = KitMan(arenaBody, index++, "vlandia", "2h:peasant_polearm_1_t1");
                arena.GiveUnitKit(fighter, null);
                Check(arenaBody.Men[fighter].Given == 0, "a tournament fighter was given gear");
                LogHas("not in this mission: a tournament or arena fight (TournamentFightMissionController)");

                // 7. the summary
                var sum = logic.UnitKit!.SummaryLine();
                Check(sum.Contains("tier-1 men given a shield 27 (empire 5, aserai 5, battania 4, khuzait 4, sturgia 4, vlandia 4, nord 1)"), "summary: " + sum);
                Check(sum.Contains("two-handers swapped for a spear 5 (empire 2, battania 1, khuzait 1, nord 1)"), "summary spears: " + sum);
                Check(sum.Contains("has a shield 1") && sum.Contains("a hero 1") && sum.Contains("a rider 1") && sum.Contains("not tier 1 2") && sum.Contains("switched off 1")
                      && sum.Contains("no shield for his culture 3"), "summary skips: " + sum);
                typeof(AthleticsLogic).GetMethod("WriteUnitKitSummary", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(logic, null);
                LogHas("[summary] unit changes (step 84): men looked at");

                // 8. the cost: 1000 spawns (a culture's first man already said) - small, allocating nothing
                Step84Defaults();
                var (costLogic, costBody) = NewKitLogic();
                var men = new Agent[1000];
                for (int i = 0; i < men.Length; i++) men[i] = KitMan(costBody, 20000 + i, Recruits[i % 6].Culture, Recruits[i % 6].Variants[(i / 6) % 4]);
                costLogic.GiveUnitKit(men[0], null);
                for (int i = 1; i < 12; i++) costLogic.GiveUnitKit(men[i], null); // every culture's first lines written
                foreach (var m in costBody.Men.Values) { m.Given = 0; if (m.IsShield[1]) { m.IsShield[1] = false; m.Ids[1] = null; } }
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int i = 12; i < men.Length; i++) costLogic.GiveUnitKit(men[i], null);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                Console.WriteLine("        cost: 988 spawns " + sw.Elapsed.TotalMilliseconds.ToString("0.000") + " ms, " + allocated + " bytes (the stand-in body's boxing excluded: none)");
                Check(allocated < 64 * 1024, "cost: the spawns allocate - " + allocated + " bytes");
            }
            finally
            {
                Step84Defaults();
            }
        }

        /// <summary>The master-switch step's part: off = nothing given, no engine read; on again = given.</summary>
        private static void UnitKitFollowsTheMasterSwitch()
        {
            Step84Defaults();
            var (logic, body) = NewKitLogic();
            var a = KitMan(body, 30000, "khuzait", "2h:peasant_polearm_1_t1");
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            logic.GiveUnitKit(a, null);
            Check(body.Men[a].Given == 0 && body.Reads == 0, "master switch off: a tier-1 man still given a shield / read");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            logic.GiveUnitKit(a, null);
            Check(body.Men[a].Given == 1 && body.Men[a].Ids[0] == "eastern_spear_1_t2", "master switch on: not given");
            Check(logic.UnitKit!.Skipped(KitSkip.ModOff) == 1, "master switch off not counted");
        }
    }
}
