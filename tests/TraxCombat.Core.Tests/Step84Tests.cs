using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 84 - TIER-1 MEN GET A CHEAP CULTURE SHIELD (Toni 2026-10-24: "they all should be able to afford a piece of wood"; AI_NOTES "Step 84",
/// DESIGN decision 80): the text setting type, the settings' places, the gates in order (the master switch first), the two-hander test on the
/// game's real usages (a scythe and a maul two-handed only, a pitchfork one-hand usable), the slot plan, the culture lookup with the fallback
/// for a culture without settings, unknown / wrong ids, live changes and the summary line - over a stand-in of the game's item table.
/// </summary>
public class Step84Tests
{
    /// <summary>A stand-in of the game's item table: the agreed items with their real weights / hit points, a few cheaper ones.</summary>
    internal sealed class FakeItems : IKitItems
    {
        public readonly Dictionary<string, KitItemFacts> Items = new(StringComparer.Ordinal);
        public int CheapestAsks;

        public FakeItems()
        {
            Shield("battered_kite_shield", "Simple Kite Shield", null, 2.6f, 220, 120);
            Shield("adarga", "Adarga", "aserai", 1.3f, 160, 80);
            Shield("battania_targe_b", "Targe", "battania", 1.6f, 215, 90);
            Shield("eastern_wicker_shield", "Wicker Square Shield", "khuzait", 2.7f, 260, 110);
            Shield("northern_round_shield", "Round Shield", "sturgia", 3.0f, 300, 130);
            Shield("heater_shield_with_cutout", "Heater Shield with Cutout", "vlandia", 2.1f, 220, 100);
            Shield("nord_cheap_shield", "Nord Plank", "nord", 2.0f, 180, 60);
            Shield("nord_dear_shield", "Nord Fine Shield", "nord", 3.5f, 400, 900);
            Spear("imperial_spear_t2", "Short Militia Spear", "empire", 70);
            Spear("western_spear_2_t2", "Simple Short Spear", "vlandia", 60);
            Spear("western_spear_1_t2", "Simple Commoner Spear", "vlandia", 75);
            Spear("eastern_spear_1_t2", "Jagged Spear", "aserai", 90);
            Spear("nord_spear_1_t2", "Knobbed Short Spear", "nord", 85);
            Items["peasant_polearm_1_t1"] = new KitItemFacts { Id = "peasant_polearm_1_t1", Name = "Scythe", Polearm = true, Merchandise = true, Value = 20, Weight = 2 };
        }

        private void Shield(string id, string name, string? culture, float kg, int hp, int value) =>
            Items[id] = new KitItemFacts { Id = id, Name = name, Shield = true, Culture = culture, Merchandise = true, Weight = kg, HitPoints = hp, Value = value };

        private void Spear(string id, string name, string culture, int value) =>
            Items[id] = new KitItemFacts { Id = id, Name = name, OneHandMelee = true, Polearm = true, Culture = culture, Merchandise = true, Weight = 2, Value = value };

        public bool TryFind(string id, out object item, out KitItemFacts facts)
        {
            bool ok = Items.TryGetValue(id, out facts);
            item = ok ? facts.Id : null!;
            return ok;
        }

        public bool TryCheapest(string culture, bool shield, out object item, out KitItemFacts facts)
        {
            CheapestAsks++;
            var best = Items.Values.Where(f => f.Merchandise && string.Equals(f.Culture, culture, StringComparison.OrdinalIgnoreCase)
                                              && (shield ? f.Shield : f.Polearm && f.OneHandMelee && !f.Shield))
                .OrderBy(f => f.Value).ToList();
            facts = best.FirstOrDefault();
            item = best.Count > 0 ? facts.Id : null!;
            return best.Count > 0;
        }
    }

    private static KitSlot Empty() => new KitSlot { Empty = true };
    private static KitSlot OneHander(string id) => new KitSlot { Melee = true, OneHand = true, Id = id };
    private static KitSlot TwoHander(string id) => new KitSlot { Melee = true, OneHand = false, Id = id };
    private static KitSlot Shield(string id) => new KitSlot { Shield = true, Id = id };
    private static KitSlot[] Slots(params KitSlot[] first)
    {
        var s = new KitSlot[UnitKitMath.Slots];
        for (int i = 0; i < s.Length; i++) s[i] = i < first.Length ? first[i] : Empty();
        return s;
    }

    private static KitAgent Recruit(string? culture, int tier = 1) => new KitAgent { Culture = culture, Tier = tier };

    private static (UnitKit Kit, List<string> Log, FakeItems Items, TraxSettings S) New()
    {
        var log = new List<string>();
        var items = new FakeItems();
        return (new UnitKit(items, log.Add), log, items, new TraxSettings());
    }

    [Fact]
    public void The_switch_has_its_own_section_on_the_player_page_and_the_ten_items_theirs_on_Advanced_before_Advanced_all_live()
    {
        var all = SettingsSchema.All.ToList();
        var sw = SettingsSchema.GiveTier1Shields;
        Assert.Equal(all.IndexOf(SettingsSchema.ImpactShowMessages) + 1, all.IndexOf(sw));
        Assert.Same(SettingsSchema.PlayerUnitsGroup, sw.Group);
        Assert.Equal("Unit changes", sw.Group.Title);
        Assert.Equal("Give Tier 1 units shields", sw.Label);
        Assert.Equal(SettingsPage.Player, sw.Group.Page);
        Assert.Equal(1, sw.Default);
        var items = new[]
        {
            SettingsSchema.UnitShieldEmpire, SettingsSchema.UnitShieldAserai, SettingsSchema.UnitShieldBattania, SettingsSchema.UnitShieldKhuzait,
            SettingsSchema.UnitShieldSturgia, SettingsSchema.UnitShieldVlandia,
            SettingsSchema.UnitSpearEmpire, SettingsSchema.UnitSpearBattania, SettingsSchema.UnitSpearKhuzait, SettingsSchema.UnitSpearVlandia,
        };
        int first = all.IndexOf(SettingsSchema.LineBlockMopUpWinPercent) + 1;
        for (int i = 0; i < items.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(items[i]));
            Assert.Same(SettingsSchema.UnitsGroup, items[i].Group);
            Assert.Equal(SettingsPage.Advanced, items[i].Group.Page);
            Assert.Equal(ParamType.Text, items[i].Type);
            Assert.Equal(ApplyTiming.Live, items[i].Timing);
        }
        var groups = SettingsSchema.Groups.ToList();
        Assert.Equal(groups.Count - 2, groups.IndexOf(SettingsSchema.UnitsGroup)); // right before "Advanced"
        Assert.Equal("Unit changes", SettingsSchema.UnitsGroup.Title);

        var s = new TraxSettings();
        Assert.True(s.GiveTier1Shields);
        Assert.Equal("battered_kite_shield", s.UnitShieldEmpire);
        Assert.Equal("adarga", s.UnitShieldAserai);
        Assert.Equal("battania_targe_b", s.UnitShieldBattania);
        Assert.Equal("eastern_wicker_shield", s.UnitShieldKhuzait);
        Assert.Equal("northern_round_shield", s.UnitShieldSturgia);
        Assert.Equal("heater_shield_with_cutout", s.UnitShieldVlandia);
        Assert.Equal("imperial_spear_t2", s.UnitSpearEmpire);
        Assert.Equal("western_spear_2_t2", s.UnitSpearBattania);
        Assert.Equal("eastern_spear_1_t2", s.UnitSpearKhuzait);
        Assert.Equal("western_spear_2_t2", s.UnitSpearVlandia);
    }

    [Fact]
    public void A_text_setting_stores_its_text_by_index_trimmed_refuses_a_non_id_and_reads_from_files()
    {
        Assert.True(TextValues.TryIndex("  adarga ", out int a));
        Assert.True(TextValues.TryIndex("adarga", out int b));
        Assert.Equal(a, b);
        Assert.Equal("adarga", TextValues.Get(a));
        Assert.True(TextValues.TryIndex("", out int empty));
        Assert.Equal(0, empty);
        Assert.Equal("", TextValues.Get(0));
        Assert.Equal("", TextValues.Get(double.NaN));
        Assert.Equal("", TextValues.Get(TextValues.MaxIndex));
        Assert.False(TextValues.TryIndex("my shield!", out _, out string? why));
        Assert.Contains("' '", why);
        Assert.False(TextValues.TryIndex(new string('a', TextValues.MaxLength + 1), out _));
        Assert.True(TextValues.TryIndex("Adarga", out int upper));
        Assert.NotEqual(a, upper); // ids are case-sensitive in the game

        var p = SettingsSchema.UnitShieldAserai;
        Assert.Equal("\"adarga\"", p.FormatJson(a));
        Assert.StartsWith("default adarga, an item id", p.DefaultAndRangeText);
        Assert.True(DefaultsFile.TryValue(new JValue("heater_shield_with_cutout"), ParamType.Text, p.Min, p.Max, out double v, out string? problem));
        Assert.Null(problem);
        Assert.Equal("heater_shield_with_cutout", TextValues.Get(v));
        Assert.False(DefaultsFile.TryValue(new JValue(3), ParamType.Text, p.Min, p.Max, out _, out problem));
        Assert.False(DefaultsFile.TryValue(new JValue("no good"), ParamType.Text, p.Min, p.Max, out _, out _));

        var read = ConfigFile.Read("{ \"UnitShieldAserai\": \"my_mod_buckler\", \"UnitSpearEmpire\": \"\", \"UnitShieldVlandia\": \"bad id!\" }");
        Assert.True(read.Ok);
        Assert.Equal("my_mod_buckler", TextValues.Get(read.Values["UnitShieldAserai"]));
        Assert.Equal("", TextValues.Get(read.Values["UnitSpearEmpire"]));
        Assert.False(read.Values.ContainsKey("UnitShieldVlandia")); // refused - the default stays
        var s = new TraxSettings();
        ConfigFile.Apply(read, s);
        Assert.Equal("my_mod_buckler", s.UnitShieldAserai);
        Assert.Equal("heater_shield_with_cutout", s.UnitShieldVlandia);
    }

    [Fact]
    public void The_gates_run_in_order_with_the_master_switch_first()
    {
        var r = Recruit("empire");
        Assert.Equal(KitSkip.Given, UnitKitMath.Gate(true, true, false, in r));
        var heroPlayer = new KitAgent { Player = true, Hero = true, Mounted = true, Tier = 7, Culture = "empire" };
        Assert.Equal(KitSkip.ModOff, UnitKitMath.Gate(false, true, true, in heroPlayer));
        Assert.Equal(KitSkip.SwitchOff, UnitKitMath.Gate(true, false, true, in heroPlayer));
        Assert.Equal(KitSkip.FixedEquipment, UnitKitMath.Gate(true, true, true, in heroPlayer));
        Assert.Equal(KitSkip.Player, UnitKitMath.Gate(true, true, false, in heroPlayer));
        var hero = new KitAgent { Hero = true, Tier = 1, Culture = "empire" };
        Assert.Equal(KitSkip.Hero, UnitKitMath.Gate(true, true, false, in hero));
        foreach (int tier in new[] { 0, 2, 3, 6 })
        {
            var t = Recruit("empire", tier);
            Assert.Equal(KitSkip.NotTier1, UnitKitMath.Gate(true, true, false, in t));
        }
        var rider = new KitAgent { Mounted = true, Tier = 1, Culture = "khuzait" };
        Assert.Equal(KitSkip.Rider, UnitKitMath.Gate(true, true, false, in rider));
        Assert.Equal(1, UnitKitMath.GivenTier);
    }

    [Fact]
    public void Two_handed_only_means_a_melee_weapon_with_no_one_handed_usage()
    {
        // the game's own usages (weapon_descriptions + the crafted pieces): the scythe and the maul two-handed only, the pitchfork 1H + 2H
        Assert.True(UnitKitMath.TwoHandedOnly(TwoHander("peasant_polearm_1_t1")));
        Assert.True(UnitKitMath.TwoHandedOnly(TwoHander("peasant_maul_t1_2")));
        Assert.False(UnitKitMath.TwoHandedOnly(OneHander("peasant_pitchfork_2_t1")));
        Assert.False(UnitKitMath.TwoHandedOnly(OneHander("peasant_sickle_1_t1")));
        Assert.False(UnitKitMath.TwoHandedOnly(Shield("adarga")));
        Assert.False(UnitKitMath.TwoHandedOnly(Empty()));
        Assert.False(UnitKitMath.TwoHandedOnly(new KitSlot { Id = "hunting_bow" })); // a bow is not melee: never swapped
    }

    [Fact]
    public void The_plan_puts_the_shield_in_the_first_free_slot_and_swaps_the_first_two_hander()
    {
        Assert.Equal(KitSkip.Given, UnitKitMath.Plan(Slots(TwoHander("scythe")), out int shield, out int swap));
        Assert.Equal(1, shield);
        Assert.Equal(0, swap);
        Assert.Equal(KitSkip.Given, UnitKitMath.Plan(Slots(OneHander("pitchfork")), out shield, out swap));
        Assert.Equal((1, -1), (shield, swap));
        Assert.Equal(KitSkip.Given, UnitKitMath.Plan(Slots(Empty(), TwoHander("maul")), out shield, out swap));
        Assert.Equal((0, 1), (shield, swap));
        Assert.Equal(KitSkip.HasShield, UnitKitMath.Plan(Slots(OneHander("spear"), Empty(), Empty(), Shield("nord_shield")), out _, out _));
        Assert.Equal(KitSkip.NoFreeSlot, UnitKitMath.Plan(Slots(OneHander("a"), OneHander("b"), TwoHander("c"), OneHander("d")), out shield, out _));
        Assert.Equal(-1, shield);
    }

    [Fact]
    public void Each_main_culture_has_its_settings_and_any_other_culture_none()
    {
        Assert.Equal(0, UnitKitMath.CultureIndex("empire"));
        Assert.Equal(3, UnitKitMath.CultureIndex("Khuzait"));
        Assert.Equal(-1, UnitKitMath.CultureIndex("nord"));
        Assert.Equal(-1, UnitKitMath.CultureIndex(null));
        for (int i = 0; i < UnitKitMath.Cultures.Length; i++)
            Assert.EndsWith(char.ToUpperInvariant(UnitKitMath.Cultures[i][0]) + UnitKitMath.Cultures[i].Substring(1), UnitKitMath.ShieldSetting(i).Key);
        Assert.Same(SettingsSchema.UnitSpearEmpire, UnitKitMath.SpearSetting(0));
        Assert.Null(UnitKitMath.SpearSetting(1)); // Aserai: no two-hander among its recruits
        Assert.Same(SettingsSchema.UnitSpearBattania, UnitKitMath.SpearSetting(2));
        Assert.Same(SettingsSchema.UnitSpearKhuzait, UnitKitMath.SpearSetting(3));
        Assert.Null(UnitKitMath.SpearSetting(4)); // Sturgia
        Assert.Same(SettingsSchema.UnitSpearVlandia, UnitKitMath.SpearSetting(5));
    }

    [Theory]
    [InlineData("empire", "peasant_pitchfork_2_t1", false, "battered_kite_shield", null)]
    [InlineData("empire", "peasant_polearm_1_t1", true, "battered_kite_shield", "imperial_spear_t2")]
    [InlineData("khuzait", "peasant_polearm_1_t1", true, "eastern_wicker_shield", "eastern_spear_1_t2")]
    [InlineData("khuzait", "peasant_pitchfork_1_t1", false, "eastern_wicker_shield", null)]
    [InlineData("battania", "peasant_maul_t1_2", true, "battania_targe_b", "western_spear_2_t2")]
    [InlineData("vlandia", "peasant_pitchfork_1_t1", false, "heater_shield_with_cutout", null)]
    [InlineData("aserai", "peasant_hammer_1_t1", false, "adarga", null)]
    [InlineData("sturgia", "sturgia_axe_2_t2", false, "northern_round_shield", null)]
    public void A_recruit_of_each_culture_gets_its_shield_and_a_two_hander_its_spear(string culture, string weapon, bool twoHanded, string shield, string? spear)
    {
        var (kit, log, _, s) = New();
        kit.Start(s);
        var order = default(KitOrder);
        var man = Recruit(culture);
        var slots = Slots(twoHanded ? TwoHander(weapon) : OneHander(weapon));
        Assert.Equal(KitSkip.Given, kit.Decide(s, false, in man, slots, ref order));
        Assert.Equal(shield, order.Shield);
        Assert.Equal(1, order.ShieldSlot);
        Assert.Equal(twoHanded ? 0 : -1, order.SwapSlot);
        Assert.Equal(spear, order.Spear);
        kit.Done(in order, true);
        Assert.Equal(1, kit.ShieldsGiven(culture));
        Assert.Equal(spear != null ? 1 : 0, kit.SpearsSwapped(culture));
        Assert.Contains(log, l => l.StartsWith("first a tier-1 man of " + culture + " given ", StringComparison.Ordinal));
    }

    [Fact]
    public void The_start_says_every_culture_once_with_the_cheapest_merchandise_item_beside_it()
    {
        var (kit, log, _, s) = New();
        kit.Start(s);
        Assert.Equal(6, log.Count);
        Assert.Contains(log, l => l.StartsWith("empire: shield Simple Kite Shield (battered_kite_shield, 2.6 kg, 220 hit points, value 120) [UnitShieldEmpire] (no merchandise shield of culture empire)", StringComparison.Ordinal));
        Assert.Contains(log, l => l.StartsWith("vlandia: shield Heater Shield with Cutout", StringComparison.Ordinal) && l.Contains("spear Simple Short Spear (western_spear_2_t2") && l.Contains("the cheapest merchandise one-handed spear of culture vlandia: Simple Short Spear"));
        Assert.Contains(log, l => l.StartsWith("aserai:", StringComparison.Ordinal) && l.Contains("spear none set"));
    }

    [Fact]
    public void An_unknown_or_wrong_id_skips_that_culture_logged_once_and_a_live_change_is_resolved_again()
    {
        var (kit, log, items, s) = New();
        s.Set(SettingsSchema.UnitShieldSturgia, TextValues.TryIndex("no_such_shield", out int bad) ? bad : -1, SettingSources.Mcm);
        s.Set(SettingsSchema.UnitSpearEmpire, TextValues.TryIndex("peasant_polearm_1_t1", out int scythe) ? scythe : -1, SettingSources.Mcm);
        s.Set(SettingsSchema.UnitShieldAserai, TextValues.TryIndex("imperial_spear_t2", out int notShield) ? notShield : -1, SettingSources.Mcm);
        kit.Start(s);
        Assert.Contains(log, l => l.StartsWith("sturgia: shield SKIPPED - no item \"no_such_shield\" in this game [UnitShieldSturgia]", StringComparison.Ordinal));
        Assert.Contains(log, l => l.Contains("SKIPPED - \"imperial_spear_t2\" is not a shield [UnitShieldAserai]"));
        Assert.Contains(log, l => l.Contains("SKIPPED - \"peasant_polearm_1_t1\" has no one-handed melee use"));
        var order = default(KitOrder);
        var st = Recruit("sturgia");
        Assert.Equal(KitSkip.NoShieldForCulture, kit.Decide(s, false, in st, Slots(OneHander("axe")), ref order));
        Assert.Equal(KitSkip.NoShieldForCulture, kit.Decide(s, false, in st, Slots(OneHander("axe")), ref order));
        Assert.Single(log, l => l.StartsWith("first tier-1 man of sturgia skipped", StringComparison.Ordinal));
        Assert.Equal(2, kit.Skipped(KitSkip.NoShieldForCulture));

        // the Empire's spear has no one-handed use: the shield is given, the scythe KEPT
        var em = Recruit("empire");
        Assert.Equal(KitSkip.Given, kit.Decide(s, false, in em, Slots(TwoHander("peasant_polearm_1_t1")), ref order));
        Assert.Equal(0, order.SwapSlot);
        Assert.Null(order.Spear);
        kit.Done(in order, true);
        Assert.Equal(1, kit.TwoHandersKept("empire"));
        Assert.Contains(log, l => l.Contains("his two-hander peasant_polearm_1_t1 KEPT"));

        // live: MCM sets a real shield - the next spawn resolves it and says so
        s.Set(SettingsSchema.UnitShieldSturgia, TextValues.TryIndex("northern_round_shield", out int good) ? good : -1, SettingSources.Mcm);
        Assert.Equal(KitSkip.Given, kit.Decide(s, false, in st, Slots(OneHander("axe")), ref order));
        Assert.Equal("northern_round_shield", order.Shield);
        Assert.Contains(log, l => l.StartsWith("sturgia: UnitShieldSturgia now Round Shield", StringComparison.Ordinal));
        // empty = none
        s.Set(SettingsSchema.UnitShieldSturgia, 0, SettingSources.Mcm);
        Assert.Equal(KitSkip.NoShieldForCulture, kit.Decide(s, false, in st, Slots(OneHander("axe")), ref order));
        Assert.Contains(log, l => l.Contains("UnitShieldSturgia now SKIPPED - none set"));
        Assert.True(items.CheapestAsks >= 0);
    }

    [Fact]
    public void A_culture_without_settings_takes_its_cheapest_merchandise_shield_and_spear_else_it_is_skipped_once()
    {
        var (kit, log, items, s) = New();
        kit.Start(s);
        int asks = items.CheapestAsks;
        var order = default(KitOrder);
        var nord = Recruit("nord");
        Assert.Equal(KitSkip.Given, kit.Decide(s, false, in nord, Slots(TwoHander("nord_axe_2h")), ref order));
        Assert.Equal("nord_cheap_shield", order.Shield);
        Assert.Equal("nord_spear_1_t2", order.Spear);
        Assert.Contains(log, l => l.StartsWith("nord (no settings of its own): shield Nord Plank", StringComparison.Ordinal));
        Assert.Equal(KitSkip.Given, kit.Decide(s, false, in nord, Slots(OneHander("nord_axe")), ref order));
        Assert.Equal(asks + 2, items.CheapestAsks); // resolved once a mission
        var bandit = Recruit("looters");
        Assert.Equal(KitSkip.NoShieldForCulture, kit.Decide(s, false, in bandit, Slots(OneHander("stone")), ref order));
        Assert.Equal(KitSkip.NoShieldForCulture, kit.Decide(s, false, in bandit, Slots(OneHander("stone")), ref order));
        Assert.Single(log, l => l.StartsWith("first tier-1 man of looters skipped", StringComparison.Ordinal));
        var noCulture = Recruit(null);
        Assert.Equal(KitSkip.NoCulture, kit.Decide(s, false, in noCulture, Slots(OneHander("x")), ref order));
        // an Aserai two-hander (a mod's): the Aserai have no spear setting - their cheapest merchandise spear
        var aserai = Recruit("aserai");
        Assert.Equal(KitSkip.Given, kit.Decide(s, false, in aserai, Slots(TwoHander("aserai_2h")), ref order));
        Assert.Equal("eastern_spear_1_t2", order.Spear);
    }

    [Fact]
    public void Switched_off_gives_nothing_and_a_man_with_a_shield_is_untouched()
    {
        var (kit, _, _, s) = New();
        kit.Start(s);
        var order = default(KitOrder);
        var r = Recruit("empire");
        s.Set(SettingsSchema.GiveTier1Shields, false, SettingSources.Mcm);
        Assert.Equal(KitSkip.SwitchOff, kit.Decide(s, false, in r, Slots(TwoHander("peasant_polearm_1_t1")), ref order));
        s.Set(SettingsSchema.GiveTier1Shields, true, SettingSources.Mcm);
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        Assert.Equal(KitSkip.ModOff, kit.Decide(s, false, in r, Slots(TwoHander("peasant_polearm_1_t1")), ref order));
        s.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
        Assert.Equal(KitSkip.FixedEquipment, kit.Decide(s, true, in r, Slots(TwoHander("peasant_polearm_1_t1")), ref order));
        var nordYoungling = Recruit("nord");
        Assert.Equal(KitSkip.HasShield, kit.Decide(s, false, in nordYoungling, Slots(OneHander("nord_spear"), Shield("nord_shield_tier_2_d")), ref order));
        Assert.Null(order.Shield);
    }

    [Fact]
    public void The_summary_counts_shields_and_spears_by_culture_and_skips_by_reason()
    {
        var (kit, _, _, s) = New();
        kit.Start(s);
        var order = default(KitOrder);
        void Give(string culture, KitSlot weapon)
        {
            var m = Recruit(culture);
            order = default;
            Assert.Equal(KitSkip.Given, kit.Decide(s, false, in m, Slots(weapon), ref order));
            kit.Done(in order, true);
        }
        Give("empire", TwoHander("peasant_polearm_1_t1"));
        Give("empire", OneHander("peasant_pitchfork_2_t1"));
        Give("battania", TwoHander("peasant_maul_t1_2"));
        var vet = Recruit("empire", 3);
        kit.Decide(s, false, in vet, Slots(OneHander("sword")), ref order);
        var withShield = Recruit("vlandia");
        kit.Decide(s, false, in withShield, Slots(OneHander("x"), Shield("y")), ref order);
        order = new KitOrder { Culture = "aserai", ShieldSlot = 1, SwapSlot = -1 };
        kit.Done(in order, false);
        string line = kit.SummaryLine();
        Assert.Equal("unit changes (step 84): men looked at 5, tier-1 men given a shield 3 (empire 2, battania 1), two-handers swapped for a spear 2 (empire 1, battania 1), two-handers kept (no spear) 0; skipped: not tier 1 1, has a shield 1, failed 1", line);
        var (fresh, _, _, s2) = New();
        Assert.Equal("unit changes (step 84): men looked at 0, tier-1 men given a shield 0, two-handers swapped for a spear 0, two-handers kept (no spear) 0; skipped: none", fresh.SummaryLine());
    }
}
