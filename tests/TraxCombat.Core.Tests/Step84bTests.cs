using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 84b - THE TIER-1 SHIELDS SHOW IN THE TROOPS' OWN GEAR (Toni 2026-10-24: "make the troops' gear show the shields ... as long as nothing
/// goes into the save"; AI_NOTES "Step 84b", DESIGN decision 80b): the bookkeeping of the in-memory edit over a stand-in of the game's characters -
/// apply twice = once, restore = the exact originals, a settings change re-applies, the gates in order (the master switch first), shared / horse /
/// shield sets left alone, the common slot (the game mixes slots 0-1 of one set with 2-3 of another), the hero copy.
/// </summary>
public class Step84bTests
{
    /// <summary>A set: 4 weapon slots holding item ids (the stand-in items are their ids), the originals as boxed tokens.</summary>
    internal sealed class FakeSet
    {
        public readonly object?[] Slots = new object?[4];
        public readonly bool[] TwoHanded = new bool[4];
        public readonly bool[] Shield = new bool[4];
        public bool Horse;
        public bool Shared;
        public int Writes;
    }

    internal sealed class FakeTroop
    {
        public string Id = "";
        public string? Culture;
        public int Tier = 1;
        public bool Hero, Player, HeroTemplate;
        public bool Soldier = true;
        public readonly List<FakeSet> Sets = new();
    }

    internal sealed class FakeWorld : ITroopKitWorld
    {
        public readonly List<FakeTroop> Troops = new();
        public IKitItems Items { get; } = new Step84Tests.FakeItems();
        public int Reads;

        public void ReadTroops(List<KitTroop> into)
        {
            Reads++;
            foreach (var t in Troops)
            {
                var k = new KitTroop { Handle = t, Id = t.Id, Culture = t.Culture, Tier = t.Tier, Hero = t.Hero, Player = t.Player, Soldier = t.Soldier, HeroTemplate = t.HeroTemplate };
                foreach (var s in t.Sets)
                {
                    var ks = new KitSet { Handle = s, Horse = s.Horse, OwnedAlone = !s.Shared };
                    for (int i = 0; i < 4; i++)
                    {
                        ks.Originals[i] = s.Slots[i] == null ? null : new Token((string)s.Slots[i]!);
                        if (s.Slots[i] == null) { ks.Slots[i].Empty = true; continue; }
                        ks.Slots[i].Id = s.Slots[i] is Token tk ? tk.Id : (string)s.Slots[i]!;
                        if (s.Shield[i]) { ks.Slots[i].Shield = true; continue; }
                        ks.Slots[i].Melee = true;
                        ks.Slots[i].OneHand = !s.TwoHanded[i];
                    }
                    k.Sets.Add(ks);
                }
                into.Add(k);
            }
        }

        public bool Write(object set, int slot, object? value)
        {
            var s = (FakeSet)set;
            s.Writes++;
            s.Slots[slot] = value is Token t ? t.Id : value; // an original back = the game's own element, an item = ours
            return true;
        }

        public bool Holds(object set, int slot, object item) => Equals(((FakeSet)set).Slots[slot], item);
    }

    /// <summary>The game's own element (an EquipmentElement in the module), boxed.</summary>
    internal sealed record Token(string Id);

    internal sealed class FakeCopy : IKitCopy
    {
        public readonly object?[] Slots = new object?[4];
        public FakeCopy(FakeSet from) => Array.Copy(from.Slots, Slots, 4);
        public bool SameAs(object set) => ((FakeSet)set).Slots.SequenceEqual(Slots);
        public bool Holds(int slot, object item) => Equals(Slots[slot], item);
        public void Write(int slot, object? value) => Slots[slot] = value is Token t ? t.Id : value;
    }

    private static FakeSet Set(string weapon, bool twoHanded = false)
    {
        var s = new FakeSet();
        s.Slots[0] = weapon;
        s.TwoHanded[0] = twoHanded;
        return s;
    }

    /// <summary>Toni's Sturgian Recruit (spnpccharacters.xml: 4 sets, Item0 only) and an Imperial Recruit with the scythe in one set.</summary>
    private static (FakeWorld World, FakeTroop Sturgian, FakeTroop Imperial) Recruits()
    {
        var w = new FakeWorld();
        var st = new FakeTroop { Id = "sturgian_recruit", Culture = "sturgia" };
        foreach (var v in new[] { "sturgia_axe_2_t2", "peasant_hammer_1_t1", "peasant_hammer_2_t1", "peasant_sickle_1_t1" }) st.Sets.Add(Set(v));
        var im = new FakeTroop { Id = "imperial_recruit", Culture = "empire" };
        im.Sets.Add(Set("peasant_pitchfork_2_t1"));
        im.Sets.Add(Set("peasant_polearm_1_t1", twoHanded: true));
        im.Sets.Add(Set("peasant_sickle_1_t1"));
        im.Sets.Add(Set("empire_sword_1_t2"));
        w.Troops.Add(st);
        w.Troops.Add(im);
        return (w, st, im);
    }

    private static (TroopKit Kit, List<string> Log, TraxSettings S) New()
    {
        var log = new List<string>();
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
        s.Set(SettingsSchema.GiveTier1Shields, true, SettingSources.File);
        return (new TroopKit(log.Add), log, s);
    }

    private static int Idx(string id) => TextValues.TryIndex(id, out int i) ? i : -1;

    private static object?[][] Snapshot(FakeWorld w) => w.Troops.SelectMany(t => t.Sets).Select(s => (object?[])s.Slots.Clone()).ToArray();

    [Fact]
    public void Applies_the_culture_shield_into_the_same_slot_of_every_set_and_swaps_the_two_hander()
    {
        var (w, st, im) = Recruits();
        var (kit, log, s) = New();
        Assert.True(kit.Sync(w, s, "test"));
        foreach (var set in st.Sets)
        {
            Assert.Equal("northern_round_shield", set.Slots[1]);
            Assert.Null(set.Slots[2]);
        }
        Assert.Equal("peasant_pitchfork_2_t1", im.Sets[0].Slots[0]);
        Assert.Equal("imperial_spear_t2", im.Sets[1].Slots[0]);
        Assert.All(im.Sets, set => Assert.Equal("battered_kite_shield", set.Slots[1]));
        Assert.Equal(2, kit.TroopsEdited);
        Assert.Equal(8, kit.SetsEdited);
        Assert.Equal(8, kit.ShieldsPut);
        Assert.Equal(1, kit.SpearsSwapped);
        Assert.Contains(log, l => l.Contains("2 tier-1 troops, 8 battle sets edited IN MEMORY (nothing saved)", StringComparison.Ordinal));
        Assert.Contains(log, l => l.Contains("sturgia = Round Shield (northern_round_shield", StringComparison.Ordinal));
    }

    [Fact]
    public void Applying_twice_is_applying_once()
    {
        var (w, st, _) = Recruits();
        var (kit, log, s) = New();
        kit.Sync(w, s, "game ready");
        var after = Snapshot(w);
        int writes = st.Sets.Sum(x => x.Writes);
        int lines = log.Count;
        Assert.False(kit.Sync(w, s, "mission start"));
        Assert.False(kit.Sync(w, s, "setting UnitShieldEmpire")); // the same values: nothing
        Assert.Equal(after, Snapshot(w));
        Assert.Equal(writes, st.Sets.Sum(x => x.Writes));
        Assert.Equal(1, w.Reads);
        Assert.Equal(lines, log.Count);
        Assert.Equal(9, kit.Edits.Count);
    }

    [Fact]
    public void Restore_puts_back_the_exact_originals()
    {
        var (w, _, _) = Recruits();
        var before = Snapshot(w);
        var (kit, log, s) = New();
        kit.Sync(w, s, "game ready");
        Assert.NotEqual(before, Snapshot(w));
        kit.Restore(w, "the game ended");
        Assert.Equal(before, Snapshot(w));
        Assert.Empty(kit.Edits);
        Assert.Null(kit.Signature);
        Assert.Equal(9, kit.LastRestored);
        Assert.Contains(log, l => l.Contains("restored 9 slot(s) in 8 battle sets of 2 troops (the game ended)", StringComparison.Ordinal));
        // after a restore the next game applies afresh
        Assert.True(kit.Sync(w, s, "next game"));
        Assert.Equal(9, kit.Edits.Count);
    }

    [Fact]
    public void Switching_off_restores_and_on_applies_again_master_switch_first()
    {
        var (w, st, _) = Recruits();
        var before = Snapshot(w);
        var (kit, log, s) = New();
        kit.Sync(w, s, "ready");
        s.Set(SettingsSchema.GiveTier1Shields, false, SettingSources.Mcm);
        Assert.True(kit.Sync(w, s, "setting GiveTier1Shields"));
        Assert.Equal(before, Snapshot(w));
        Assert.Contains(log, l => l.Contains("off (GiveTier1Shields)", StringComparison.Ordinal));
        s.Set(SettingsSchema.GiveTier1Shields, true, SettingSources.Mcm);
        kit.Sync(w, s, "setting GiveTier1Shields");
        Assert.Equal("northern_round_shield", st.Sets[0].Slots[1]);
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        kit.Sync(w, s, "setting ModEnabled");
        Assert.Equal(before, Snapshot(w));
        Assert.Equal("off (mod off)", TroopKitMath.Signature(s));
        s.Set(SettingsSchema.GiveTier1Shields, false, SettingSources.Mcm);
        Assert.Equal("off (mod off)", TroopKitMath.Signature(s)); // the master switch is read first
    }

    [Fact]
    public void An_item_setting_change_re_applies_with_the_new_item()
    {
        var (w, st, _) = Recruits();
        var (kit, _, s) = New();
        kit.Sync(w, s, "ready");
        var p = SettingsSchema.UnitShieldSturgia;
        s.Set(p, Idx("adarga"), SettingSources.Mcm);
        Assert.True(kit.Sync(w, s, "setting " + p.Key));
        Assert.All(st.Sets, set => Assert.Equal("adarga", set.Slots[1]));
        Assert.Equal(9, kit.Edits.Count); // not 18 - restored first
        // an unknown id = that culture left alone, the originals back
        s.Set(p, Idx("no_such_shield"), SettingSources.Mcm);
        kit.Sync(w, s, "setting " + p.Key);
        Assert.All(st.Sets, set => Assert.Null(set.Slots[1]));
        Assert.Equal(1, kit.Skipped(TroopSkip.NoShieldForCulture));
    }

    [Fact]
    public void The_gates_in_order()
    {
        var t = new KitTroop { Soldier = true, Tier = 1 };
        t.Sets.Add(new KitSet { Handle = new object() });
        for (int i = 0; i < 4; i++) t.Sets[0].Slots[i].Empty = true;
        Assert.Equal(TroopSkip.ModOff, TroopKitMath.Plan(false, false, t, out _));
        Assert.Equal(TroopSkip.SwitchOff, TroopKitMath.Plan(true, false, t, out _));
        Assert.Equal(TroopSkip.Edited, TroopKitMath.Plan(true, true, t, out int slot));
        Assert.Equal(0, slot);
        t.Player = true; Assert.Equal(TroopSkip.Player, TroopKitMath.Plan(true, true, t, out _)); t.Player = false;
        t.Hero = true; Assert.Equal(TroopSkip.Hero, TroopKitMath.Plan(true, true, t, out _)); t.Hero = false;
        t.Soldier = false; Assert.Equal(TroopSkip.NotSoldier, TroopKitMath.Plan(true, true, t, out _)); t.Soldier = true;
        t.Tier = 2; Assert.Equal(TroopSkip.NotTier1, TroopKitMath.Plan(true, true, t, out _)); t.Tier = 1;
        t.Sets[0].Horse = true; Assert.Equal(TroopSkip.Rider, TroopKitMath.Plan(true, true, t, out _)); t.Sets[0].Horse = false;
        t.HeroTemplate = true; Assert.Equal(TroopSkip.HeroTemplate, TroopKitMath.Plan(true, true, t, out _)); t.HeroTemplate = false;
        t.Sets[0].OwnedAlone = false; Assert.Equal(TroopSkip.SharedSet, TroopKitMath.Plan(true, true, t, out _)); t.Sets[0].OwnedAlone = true;
        t.Sets[0].Slots[2] = new KitSlot { Shield = true }; Assert.Equal(TroopSkip.HasShield, TroopKitMath.Plan(true, true, t, out _));
        var empty = new KitTroop { Soldier = true, Tier = 1 };
        Assert.Equal(TroopSkip.NoBattleSet, TroopKitMath.Plan(true, true, empty, out _));
    }

    [Fact]
    public void The_shield_goes_into_a_slot_free_in_every_set_or_nowhere()
    {
        // the game mixes slots 0-1 of one set with 2-3 of another: a shield in slot 1 of one set and slot 2 of another could give a man two
        var (w, _, _) = Recruits();
        var mod = new FakeTroop { Id = "mod_levy", Culture = "vlandia" };
        var a = Set("vlandia_mace_1_t2"); a.Slots[1] = "javelin"; mod.Sets.Add(a);
        var b = Set("vlandia_mace_1_t2"); mod.Sets.Add(b);
        w.Troops.Add(mod);
        var (kit, _, s) = New();
        kit.Sync(w, s, "ready");
        Assert.Equal("heater_shield_with_cutout", a.Slots[2]);
        Assert.Equal("heater_shield_with_cutout", b.Slots[2]);
        Assert.Null(b.Slots[1]);
        var full = new FakeTroop { Id = "full", Culture = "vlandia" };
        var c = Set("x"); c.Slots[1] = "y"; full.Sets.Add(c);
        var d = Set("x"); d.Slots[2] = "y"; d.Slots[3] = "z"; full.Sets.Add(d);
        w.Troops.Add(full);
        kit.Restore(w, "test");
        kit.Sync(w, s, "again");
        Assert.Equal(1, kit.Skipped(TroopSkip.NoCommonSlot));
        Assert.Null(c.Slots[2]);
    }

    [Fact]
    public void Shared_sets_heroes_riders_and_shield_carriers_are_never_touched()
    {
        var (w, _, _) = Recruits();
        var shared = new FakeTroop { Id = "shares_a_template", Culture = "sturgia" };
        var ss = Set("axe"); ss.Shared = true; shared.Sets.Add(ss);
        var nord = new FakeTroop { Id = "nord_youngling", Culture = "nord" };
        var ns = Set("axe"); ns.Slots[1] = "nord_shield"; ns.Shield[1] = true; nord.Sets.Add(ns);
        var hero = new FakeTroop { Id = "lord", Culture = "sturgia", Hero = true }; hero.Sets.Add(Set("sword"));
        var rider = new FakeTroop { Id = "rider", Culture = "khuzait" }; var rs = Set("mace"); rs.Horse = true; rider.Sets.Add(rs);
        var templ = new FakeTroop { Id = "made_a_hero", Culture = "sturgia", HeroTemplate = true }; templ.Sets.Add(Set("axe"));
        var t2 = new FakeTroop { Id = "warrior", Culture = "sturgia", Tier = 2 }; t2.Sets.Add(Set("axe"));
        w.Troops.AddRange(new[] { shared, nord, hero, rider, templ, t2 });
        var (kit, log, s) = New();
        kit.Sync(w, s, "ready");
        foreach (var t in new[] { shared, hero, rider, templ, t2 }) Assert.All(t.Sets, x => Assert.Null(x.Slots[1]));
        Assert.Equal("nord_shield", ns.Slots[1]);
        Assert.Equal(0, ss.Writes + ns.Writes);
        Assert.Equal(1, kit.Skipped(TroopSkip.SharedSet));
        Assert.Equal(1, kit.Skipped(TroopSkip.HasShield));
        Assert.Equal(1, kit.Skipped(TroopSkip.Hero));
        Assert.Equal(1, kit.Skipped(TroopSkip.Rider));
        Assert.Equal(1, kit.Skipped(TroopSkip.HeroTemplate));
        Assert.Equal(1, kit.Skipped(TroopSkip.NotTier1));
        Assert.Contains(log, l => l.Contains("a battle set shared with another character 1", StringComparison.Ordinal));
    }

    [Fact]
    public void A_restore_leaves_a_slot_something_else_changed()
    {
        var (w, st, _) = Recruits();
        var (kit, log, s) = New();
        kit.Sync(w, s, "ready");
        st.Sets[0].Slots[1] = "someone_elses_item";
        kit.Restore(w, "end");
        Assert.Equal("someone_elses_item", st.Sets[0].Slots[1]);
        Assert.Equal(1, kit.LastRestoreChangedSince);
        Assert.Contains(log, l => l.Contains("1 slot(s) left as they were", StringComparison.Ordinal));
    }

    [Fact]
    public void A_hero_made_from_an_edited_troop_gets_the_games_gear_and_the_troop_is_left_alone()
    {
        var (w, _, im) = Recruits();
        var before = im.Sets.Select(x => (object?[])x.Slots.Clone()).ToArray();
        var (kit, log, s) = New();
        kit.Sync(w, s, "ready");
        var copy = new FakeCopy(im.Sets[1]); // the scythe set, edited: spear + shield
        Assert.Equal(2, kit.UndoCopy(w, im, copy, "Kostis"));
        Assert.Equal("peasant_polearm_1_t1", copy.Slots[0]);
        Assert.Null(copy.Slots[1]);
        Assert.Equal(before, im.Sets.Select(x => x.Slots).ToArray());
        Assert.False(kit.IsEdited(im));
        Assert.Contains(log, l => l.Contains("a hero (Kostis) was made from an edited troop", StringComparison.Ordinal));
        // left alone for this game even when the settings change, back after a restore (another game)
        s.Set(SettingsSchema.UnitShieldEmpire, Idx("adarga"), SettingSources.Mcm);
        kit.Sync(w, s, "setting");
        Assert.All(im.Sets, x => Assert.Null(x.Slots[1]));
        kit.Restore(w, "game ended");
        kit.Sync(w, s, "next game");
        Assert.All(im.Sets, x => Assert.Equal("adarga", x.Slots[1]));
        // a copy changed after it was made: the slots still holding what we gave get an original
        var changed = new FakeCopy(im.Sets[1]);
        changed.Slots[3] = "a_gift";
        Assert.Equal(2, kit.UndoCopy(w, im, changed, "Arenicos"));
        Assert.Null(changed.Slots[1]);
        Assert.Equal("peasant_polearm_1_t1", changed.Slots[0]);
        // not our troop: nothing
        Assert.Equal(0, kit.UndoCopy(w, new object(), new FakeCopy(im.Sets[0]), "nobody"));
    }
}
