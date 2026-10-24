using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TraxCombat.Core;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 84b (Toni 2026-10-24: "make the troops' gear show the shields ... as long as nothing goes into the save"): THE TROOPS' OWN GEAR edited
    /// in memory through the real TroopGear (Attach / the settings' Changed event / Detach) and the module's real write path
    /// (GameTroopKitWorld.Write / Holds) on the game's own Equipment objects - a stand-in troop of 4 battle sets (Toni's Sturgian Recruit) and an
    /// Imperial Recruit with the scythe; then step 84's spawn path on a man built from an edited set: he already has the shield, nothing given
    /// twice. What it cannot check: MBObjectManager's real characters and the encyclopedia (PLAYTEST D49b).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>A stand-in reader of one troop list over REAL Equipment objects; writes go through the module's real GameTroopKitWorld.</summary>
        private sealed class SmokeTroopWorld : ITroopKitWorld, IKitItems
        {
            public readonly Dictionary<string, ItemObject> ItemsById = new Dictionary<string, ItemObject>(StringComparer.Ordinal);
            public readonly Dictionary<ItemObject, KitItemFacts> Facts = new Dictionary<ItemObject, KitItemFacts>();
            public readonly List<(string Id, string Culture, Equipment[] Sets)> Troops = new List<(string, string, Equipment[])>();
            private readonly ITroopKitWorld _game = new GameTroopKitWorld();
            public int Reads;

            public IKitItems Items => this;

            public ItemObject Item(string id, bool shield = false, bool twoHanded = false, bool spear = false, string? culture = null)
            {
                if (ItemsById.TryGetValue(id, out var it)) return it;
                it = (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject));
                it.Type = shield ? ItemObject.ItemTypeEnum.Shield : spear ? ItemObject.ItemTypeEnum.Polearm : ItemObject.ItemTypeEnum.OneHandedWeapon;
                ItemsById[id] = it;
                Facts[it] = new KitItemFacts
                {
                    Id = id, Name = id, Shield = shield, OneHandMelee = !shield && !twoHanded, Polearm = spear, Culture = culture, Merchandise = true,
                    Value = 50, Weight = 2,
                };
                return it;
            }

            public bool TryFind(string id, out object item, out KitItemFacts facts)
            {
                facts = default;
                item = null!;
                if (!ItemsById.TryGetValue(id, out var it)) return false;
                item = it;
                facts = Facts[it];
                return true;
            }

            public bool TryCheapest(string culture, bool shield, out object item, out KitItemFacts facts)
            {
                foreach (var kv in Facts)
                {
                    if (!string.Equals(kv.Value.Culture, culture, StringComparison.Ordinal) || kv.Value.Shield != shield) continue;
                    item = kv.Key;
                    facts = kv.Value;
                    return true;
                }
                item = null!;
                facts = default;
                return false;
            }

            public void ReadTroops(List<KitTroop> into)
            {
                Reads++;
                foreach (var (id, culture, sets) in Troops)
                {
                    var t = new KitTroop { Handle = sets, Id = id, Culture = culture, Tier = 1, Soldier = true };
                    foreach (var eq in sets)
                    {
                        var ks = new KitSet { Handle = eq, Horse = !eq[EquipmentIndex.Horse].IsEmpty };
                        for (int i = 0; i < UnitKitMath.Slots; i++)
                        {
                            var el = eq[i];
                            ks.Originals[i] = el;
                            if (el.IsEmpty) { ks.Slots[i].Empty = true; continue; }
                            var f = Facts[el.Item];
                            ks.Slots[i].Id = f.Id;
                            ks.Slots[i].Shield = f.Shield;
                            ks.Slots[i].Melee = !f.Shield;
                            ks.Slots[i].OneHand = f.OneHandMelee;
                        }
                        t.Sets.Add(ks);
                    }
                    into.Add(t);
                }
            }

            public bool Write(object set, int slot, object? value) => _game.Write(set, slot, value);

            public bool Holds(object set, int slot, object item) => _game.Holds(set, slot, item);

            public string? IdAt(Equipment eq, int slot) => eq[slot].IsEmpty ? null : Facts[eq[slot].Item].Id;
        }

        private static Equipment BattleSet(SmokeTroopWorld w, string weapon, bool twoHanded = false)
        {
            var eq = new Equipment(Equipment.EquipmentType.Battle);
            eq[0] = new EquipmentElement(w.Item(weapon, twoHanded: twoHanded));
            return eq;
        }

        private static void TroopGearInMemory()
        {
            try
            {
                Step84Defaults();
                TroopGear.Initialize();
                var w = new SmokeTroopWorld();
                w.Item("northern_round_shield", shield: true, culture: "sturgia");
                w.Item("battered_kite_shield", shield: true);
                w.Item("imperial_spear_t2", spear: true, culture: "empire");
                w.Item("adarga", shield: true, culture: "aserai");
                var sturgian = new[]
                {
                    BattleSet(w, "sturgia_axe_2_t2"), BattleSet(w, "peasant_hammer_1_t1"), BattleSet(w, "peasant_hammer_2_t1"), BattleSet(w, "peasant_sickle_1_t1"),
                };
                var imperial = new[]
                {
                    BattleSet(w, "peasant_pitchfork_2_t1"), BattleSet(w, "peasant_polearm_1_t1", twoHanded: true), BattleSet(w, "peasant_sickle_1_t1"), BattleSet(w, "empire_sword_1_t2"),
                };
                w.Troops.Add(("sturgian_recruit", "sturgia", sturgian));
                w.Troops.Add(("imperial_recruit", "empire", imperial));
                var originals = sturgian.Concat(imperial).Select(e => Enumerable.Range(0, 12).Select(i => e[i]).ToArray()).ToArray();

                // 1. the game is ready: every set gets its culture's shield in slot 1, the scythe the spear - in the game's own Equipment objects
                var game = new object();
                TroopGear.Attach(w, game, "smoke game ready");
                Check(TroopGear.Attached, "TroopGear not attached");
                foreach (var eq in sturgian)
                    Check(w.IdAt(eq, 1) == "northern_round_shield" && w.IdAt(eq, 2) == null, "a Sturgian set's slot 1 holds " + w.IdAt(eq, 1));
                Check(w.IdAt(imperial[1], 0) == "imperial_spear_t2", "the scythe set's slot 0 holds " + w.IdAt(imperial[1], 0));
                Check(w.IdAt(imperial[0], 0) == "peasant_pitchfork_2_t1", "the pitchfork was swapped");
                Check(imperial.All(e => w.IdAt(e, 1) == "battered_kite_shield"), "an Imperial set without the kite shield");
                Check(TroopGear.Kit.TroopsEdited == 2 && TroopGear.Kit.SetsEdited == 8, "edited " + TroopGear.Kit.TroopsEdited + " troops / " + TroopGear.Kit.SetsEdited + " sets");
                LogHas("[units] troop gear (step 84b, smoke game ready): 2 tier-1 troops, 8 battle sets edited IN MEMORY (nothing saved)");

                // 2. a mission start / an unrelated setting = nothing (apply twice = once)
                int reads = w.Reads;
                TroopGear.GameReady(game, "mission start");
                S.Set(SettingsSchema.CostPerBlow, S.Get(SettingsSchema.CostPerBlow) + 1, SettingSources.Mcm);
                S.Set(SettingsSchema.CostPerBlow, S.Get(SettingsSchema.CostPerBlow) - 1, SettingSources.Mcm);
                Check(w.Reads == reads, "re-read on an unchanged signature");
                Check(TroopGear.Kit.Edits.Count == 9, "edits " + TroopGear.Kit.Edits.Count + ", not 9");

                // 3. THE SPAWN PATH: a man built from an edited set already carries the shield - step 84 gives nothing
                var (logic, body) = NewKitLogic();
                var man = KitMan(body, 9800, "sturgia", w.IdAt(sturgian[0], 0), shield: w.IdAt(sturgian[0], 1));
                logic.GiveUnitKit(man, null);
                Check(body.Men[man].Given == 0 && body.Gives == 0, "a man from an edited set was given a second shield");
                Check(logic.UnitKit!.Skipped(KitSkip.HasShield) == 1, "the spawn path did not skip him as 'has a shield'");
                var swapped = KitMan(body, 9801, "empire", w.IdAt(imperial[1], 0), shield: w.IdAt(imperial[1], 1));
                logic.GiveUnitKit(swapped, null);
                Check(body.Men[swapped].Given == 0 && body.Men[swapped].Ids[0] == "imperial_spear_t2", "the scythe man's spear / shield touched again");

                // 4. an item setting live: re-applied with the new shield, never twice
                S.Set(SettingsSchema.UnitShieldSturgia, TextValues.TryIndex("adarga", out int adarga) ? adarga : -1, SettingSources.Mcm);
                Check(sturgian.All(e => w.IdAt(e, 1) == "adarga" && w.IdAt(e, 2) == null), "the Sturgian sets not re-applied with the Adarga");
                Check(TroopGear.Kit.Edits.Count == 9, "after the item change edits " + TroopGear.Kit.Edits.Count);
                LogHas("(setting UnitShieldSturgia - settings changed)");

                // 5. SWITCH OFF live: the exact originals back in the game's Equipment objects; the master switch too; on again = edited
                S.Set(SettingsSchema.GiveTier1Shields, false, SettingSources.Mcm);
                var now = sturgian.Concat(imperial).Select(e => Enumerable.Range(0, 12).Select(i => e[i]).ToArray()).ToArray();
                bool same = true;
                for (int s = 0; s < originals.Length; s++)
                    for (int i = 0; i < 12; i++)
                        if (!now[s][i].IsEqualTo(originals[s][i])) same = false;
                Check(same, "switched off: a set is not the game's original");
                LogHas("[units] troop gear (step 84b): restored 9 slot(s) in 8 battle sets of 2 troops (setting GiveTier1Shields - settings changed)");
                LogHas("troop gear (step 84b): off (GiveTier1Shields)");
                S.Set(SettingsSchema.GiveTier1Shields, true, SettingSources.Mcm);
                Check(w.IdAt(sturgian[2], 1) == "adarga", "on again: not edited");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Check(sturgian.All(e => e[1].IsEmpty) && w.IdAt(imperial[1], 0) == "peasant_polearm_1_t1", "the master switch off left an edit");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Check(w.IdAt(sturgian[3], 1) == "adarga", "the master switch on again: not edited");

                // 6. the game ends: everything back, nothing more on a setting change
                TroopGear.Detach("smoke game ended");
                Check(!TroopGear.Attached && sturgian.All(e => e[1].IsEmpty) && imperial.All(e => e[1].IsEmpty), "the game's end left an edit");
                LogHas("(smoke game ended)");
                S.Set(SettingsSchema.UnitShieldSturgia, TextValues.TryIndex("northern_round_shield", out int round) ? round : -1, SettingSources.Mcm);
                Check(sturgian.All(e => e[1].IsEmpty), "a setting change after the game's end edited a set");
            }
            finally
            {
                TroopGear.Detach("smoke cleanup");
                Step84Defaults();
            }
        }
    }
}
