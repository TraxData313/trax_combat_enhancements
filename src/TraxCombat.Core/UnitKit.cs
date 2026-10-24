using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 84 (Toni 2026-10-24: "one small touch to make the combat longer ... they all should be able to afford a piece of wood"): what a
    /// spawning tier-1 soldier is given, decided ENGINE-FREE (the module reads the agent into <see cref="KitAgent"/> / <see cref="KitSlot"/>
    /// and plays the <see cref="KitOrder"/>). <see cref="KitSkip.Given"/> = acted on; every other value is why not, in GATE ORDER.
    /// </summary>
    public enum KitSkip
    {
        Given,
        ModOff,
        SwitchOff,
        FixedEquipment,
        Player,
        Hero,
        NotTier1,
        Rider,
        HasShield,
        NoFreeSlot,
        NoCulture,
        NoShieldForCulture,
        Failed,
    }

    /// <summary>One weapon slot (0-3) as the module read it.</summary>
    public struct KitSlot
    {
        public bool Empty;
        public bool Shield;

        /// <summary>A melee weapon (not a shield, not ranged / ammo / thrown).</summary>
        public bool Melee;

        /// <summary>Has a usage usable with one hand (the game's NotUsableWithOneHand absent on some melee usage) - a weapon a shield can go with.</summary>
        public bool OneHand;

        /// <summary>The item id (for the log; the module hands the game's own string - no allocation).</summary>
        public string? Id;
    }

    /// <summary>The spawning man as the module read him.</summary>
    public struct KitAgent
    {
        public bool Player;
        public bool Hero;
        public bool Mounted;

        /// <summary>The game's battle tier (<c>BasicCharacterObject.GetBattleTier()</c> - level 6 = 1; step 73 reads the same).</summary>
        public int Tier;

        /// <summary>His culture's id ("empire"), null when he has none.</summary>
        public string? Culture;
    }

    /// <summary>One item as the game's table describes it (the module fills it).</summary>
    public struct KitItemFacts
    {
        public string Id;
        public string Name;
        public bool Shield;

        /// <summary>A melee weapon with a usage usable with one hand (a spear that works with a shield).</summary>
        public bool OneHandMelee;

        /// <summary>A polearm (a spear) - the fallback's choice for an unknown culture.</summary>
        public bool Polearm;

        public string? Culture;
        public bool Merchandise;
        public int Value;
        public float Weight;

        /// <summary>A shield's hit points (0 for anything else).</summary>
        public int HitPoints;

        public string Describe() =>
            Name + " (" + Id + ", " + Weight.ToString("0.0", CultureInfo.InvariantCulture) + " kg"
            + (Shield ? ", " + HitPoints.ToString(CultureInfo.InvariantCulture) + " hit points" : string.Empty)
            + ", value " + Value.ToString(CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>The game's item table (the module plays it with MBObjectManager; tests and the smoke with stand-ins).</summary>
    public interface IKitItems
    {
        /// <summary>The item with this id, false when there is none.</summary>
        bool TryFind(string id, out object item, out KitItemFacts facts);

        /// <summary>The cheapest MERCHANDISE item of <paramref name="culture"/> (its own culture attribute): a shield, or a one-hand-usable melee
        /// polearm (not thrown). False when the culture has none.</summary>
        bool TryCheapest(string culture, bool shield, out object item, out KitItemFacts facts);
    }

    /// <summary>What to give one man: his culture's shield into a free slot, and a spear in place of his two-hander.</summary>
    public struct KitOrder
    {
        public string Culture;
        public int ShieldSlot;
        public object? Shield;

        /// <summary>The slot of the two-hander to replace, -1 = none.</summary>
        public int SwapSlot;

        /// <summary>The spear - null with <see cref="SwapSlot"/> ≥ 0 = his culture has no usable spear: the two-hander is KEPT.</summary>
        public object? Spear;

        public string? SwappedId;
    }

    /// <summary>The rules - pure functions.</summary>
    public static class UnitKitMath
    {
        /// <summary>The tier that gets the shields (the switch says "Tier 1").</summary>
        public const int GivenTier = 1;

        /// <summary>The weapon slots looked at (Weapon0-3; the 5th is the banner's).</summary>
        public const int Slots = 4;

        /// <summary>The six main cultures with their own settings, in the settings' order.</summary>
        public static readonly string[] Cultures = { "empire", "aserai", "battania", "khuzait", "sturgia", "vlandia" };

        /// <summary>The culture's index in <see cref="Cultures"/> (ignoring case), -1 = a culture without its own settings.</summary>
        public static int CultureIndex(string? culture)
        {
            if (culture == null) return -1;
            for (int i = 0; i < Cultures.Length; i++)
                if (string.Equals(Cultures[i], culture, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>The shield setting of main culture <paramref name="index"/>.</summary>
        public static ParamDef ShieldSetting(int index)
        {
            switch (index)
            {
                case 0: return SettingsSchema.UnitShieldEmpire;
                case 1: return SettingsSchema.UnitShieldAserai;
                case 2: return SettingsSchema.UnitShieldBattania;
                case 3: return SettingsSchema.UnitShieldKhuzait;
                case 4: return SettingsSchema.UnitShieldSturgia;
                case 5: return SettingsSchema.UnitShieldVlandia;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>The spear setting of main culture <paramref name="index"/>; null for the Aserai and Sturgia (their recruits carry no
        /// two-hander - a mod's two-hander falls back to the culture's cheapest spear).</summary>
        public static ParamDef? SpearSetting(int index)
        {
            switch (index)
            {
                case 0: return SettingsSchema.UnitSpearEmpire;
                case 2: return SettingsSchema.UnitSpearBattania;
                case 3: return SettingsSchema.UnitSpearKhuzait;
                case 5: return SettingsSchema.UnitSpearVlandia;
                default: return null;
            }
        }

        /// <summary>A melee weapon with NO usage for one hand (a scythe, a maul, a two-handed axe or sword) - a shield cannot go with it.</summary>
        public static bool TwoHandedOnly(in KitSlot s) => !s.Empty && !s.Shield && s.Melee && !s.OneHand;

        /// <summary>The man's gates, in order: the master switch, the switch, a mission with fixed equipment (tournaments, arenas), the
        /// player, a hero (companions, lords), not tier 1, a rider.</summary>
        public static KitSkip Gate(bool modOn, bool switchOn, bool fixedEquipment, in KitAgent a)
        {
            if (!modOn) return KitSkip.ModOff;
            if (!switchOn) return KitSkip.SwitchOff;
            if (fixedEquipment) return KitSkip.FixedEquipment;
            if (a.Player) return KitSkip.Player;
            if (a.Hero) return KitSkip.Hero;
            if (a.Tier != GivenTier) return KitSkip.NotTier1;
            if (a.Mounted) return KitSkip.Rider;
            return KitSkip.Given;
        }

        /// <summary>His slots: a shield anywhere = <see cref="KitSkip.HasShield"/>; else the first free slot for the shield (none =
        /// <see cref="KitSkip.NoFreeSlot"/>) and the first two-handed-only melee weapon to swap (-1 = none).</summary>
        public static KitSkip Plan(KitSlot[] slots, out int shieldSlot, out int swapSlot)
        {
            shieldSlot = -1;
            swapSlot = -1;
            int n = Math.Min(Slots, slots.Length);
            for (int i = 0; i < n; i++)
                if (!slots[i].Empty && slots[i].Shield) return KitSkip.HasShield;
            for (int i = 0; i < n; i++)
            {
                if (slots[i].Empty)
                {
                    if (shieldSlot < 0) shieldSlot = i;
                }
                else if (swapSlot < 0 && TwoHandedOnly(in slots[i]))
                {
                    swapSlot = i;
                }
            }
            return shieldSlot < 0 ? KitSkip.NoFreeSlot : KitSkip.Given;
        }

        public static string SkipName(KitSkip s)
        {
            switch (s)
            {
                case KitSkip.Given: return "given";
                case KitSkip.ModOff: return "mod off";
                case KitSkip.SwitchOff: return "switched off";
                case KitSkip.FixedEquipment: return "a tournament or arena";
                case KitSkip.Player: return "you";
                case KitSkip.Hero: return "a hero";
                case KitSkip.NotTier1: return "not tier 1";
                case KitSkip.Rider: return "a rider";
                case KitSkip.HasShield: return "has a shield";
                case KitSkip.NoFreeSlot: return "no free slot";
                case KitSkip.NoCulture: return "no culture";
                case KitSkip.NoShieldForCulture: return "no shield for his culture";
                default: return "failed";
            }
        }
    }

    /// <summary>
    /// Step 84: the per-mission brain - resolves each culture's shield and spear (the settings read LIVE: a changed id is resolved again at
    /// the next spawn; the six main cultures from their settings, any other culture from its cheapest merchandise items, once a mission),
    /// decides each spawning man, counts, and writes the first-time lines and the summary. One instance per mission; main thread.
    /// </summary>
    public sealed class UnitKit
    {
        private sealed class Pick
        {
            public bool Resolved;
            public double Key = double.NaN;
            public object? Item;
            public KitItemFacts Facts;
            public string? Why;
        }

        private sealed class Culture
        {
            public readonly string Id;
            public readonly Pick Shield = new Pick();
            public readonly Pick Spear = new Pick();
            public int ShieldsGiven, SpearsSwapped, TwoHandersKept;
            public bool FirstLogged, SkipLogged;

            public Culture(string id) => Id = id;
        }

        private readonly IKitItems _items;
        private readonly Action<string> _log;
        private readonly Culture[] _main = new Culture[UnitKitMath.Cultures.Length];
        private readonly Dictionary<string, Culture> _other = new Dictionary<string, Culture>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Culture> _order = new List<Culture>();
        private readonly int[] _skips = new int[(int)KitSkip.Failed + 1];
        private int _looked;

        public UnitKit(IKitItems items, Action<string> log)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _log = log ?? (_ => { });
            for (int i = 0; i < _main.Length; i++) _main[i] = new Culture(UnitKitMath.Cultures[i]);
        }

        public int Looked => _looked;

        public int Skipped(KitSkip why) => _skips[(int)why];

        public int ShieldsGiven(string culture) => Find(culture)?.ShieldsGiven ?? 0;

        public int SpearsSwapped(string culture) => Find(culture)?.SpearsSwapped ?? 0;

        public int TwoHandersKept(string culture) => Find(culture)?.TwoHandersKept ?? 0;

        private Culture? Find(string culture)
        {
            int i = UnitKitMath.CultureIndex(culture);
            if (i >= 0) return _main[i];
            return _other.TryGetValue(culture, out var c) ? c : null;
        }

        /// <summary>The mission's start: every main culture's shield and spear resolved and said once (an unknown or wrong id = that
        /// culture's shield / spear skipped), each with the cheapest merchandise one of that culture beside it - the proof of "cheap".</summary>
        public void Start(TraxSettings s)
        {
            for (int i = 0; i < _main.Length; i++)
            {
                var c = _main[i];
                ResolveMain(c, i, s, shield: true, log: false);
                ResolveMain(c, i, s, shield: false, log: false);
                _log(c.Id + ": shield " + PickText(c.Shield, UnitKitMath.ShieldSetting(i)) + Cheapest(c.Id, true)
                     + "; spear " + (UnitKitMath.SpearSetting(i) == null ? "none set (no two-hander among its recruits - a two-hander gets the culture's cheapest spear)" : PickText(c.Spear, UnitKitMath.SpearSetting(i)!) + Cheapest(c.Id, false)));
            }
        }

        private string Cheapest(string culture, bool shield)
        {
            if (!_items.TryCheapest(culture, shield, out _, out var f)) return " (no merchandise " + (shield ? "shield" : "one-handed spear") + " of culture " + culture + ")";
            return " (the cheapest merchandise " + (shield ? "shield" : "one-handed spear") + " of culture " + culture + ": " + f.Describe() + ")";
        }

        private static string PickText(Pick p, ParamDef setting) =>
            p.Item != null ? p.Facts.Describe() + " [" + setting.Key + "]" : "SKIPPED - " + p.Why + " [" + setting.Key + "]";

        /// <summary>Resolves (when its setting changed) a main culture's shield or spear. Returns the pick.</summary>
        private Pick ResolveMain(Culture c, int index, TraxSettings s, bool shield, bool log)
        {
            var p = shield ? c.Shield : c.Spear;
            ParamDef? setting = shield ? UnitKitMath.ShieldSetting(index) : UnitKitMath.SpearSetting(index);
            if (setting == null)
            {
                if (!p.Resolved) ResolveCheapest(c, p, shield);
                return p;
            }
            double key = s.Get(setting);
            if (p.Resolved && key == p.Key) return p;
            p.Resolved = true;
            p.Key = key;
            p.Item = null;
            p.Why = null;
            string id = setting.Format(key);
            if (id.Length == 0) p.Why = "none set";
            else if (!_items.TryFind(id, out var item, out var f)) p.Why = "no item \"" + id + "\" in this game";
            else if (shield && !f.Shield) p.Why = "\"" + id + "\" is not a shield";
            else if (!shield && (!f.OneHandMelee || f.Shield)) p.Why = "\"" + id + "\" has no one-handed melee use - a shield cannot go with it";
            else
            {
                p.Item = item;
                p.Facts = f;
            }
            if (log) _log(c.Id + ": " + setting.Key + " now " + PickText(p, setting));
            return p;
        }

        private void ResolveCheapest(Culture c, Pick p, bool shield)
        {
            p.Resolved = true;
            if (_items.TryCheapest(c.Id, shield, out var item, out var f))
            {
                p.Item = item;
                p.Facts = f;
            }
            else
            {
                p.Why = "no merchandise " + (shield ? "shield" : "one-handed spear") + " of culture " + c.Id;
            }
        }

        private Culture CultureOf(string id, out int index)
        {
            index = UnitKitMath.CultureIndex(id);
            if (index >= 0) return _main[index];
            if (!_other.TryGetValue(id, out var c))
            {
                c = new Culture(id);
                ResolveCheapest(c, c.Shield, true);
                ResolveCheapest(c, c.Spear, false);
                _other.Add(id, c);
                _log(id + " (no settings of its own): shield " + (c.Shield.Item != null ? c.Shield.Facts.Describe() + " - its cheapest merchandise shield" : "none - " + c.Shield.Why + ": its tier-1 men are skipped")
                     + "; spear " + (c.Spear.Item != null ? c.Spear.Facts.Describe() + " - its cheapest merchandise one-handed spear" : "none - " + c.Spear.Why));
            }
            return c;
        }

        /// <summary>One spawning man: his gates, his slots, his culture's shield (and spear). <see cref="KitSkip.Given"/> = play
        /// <paramref name="order"/>, then call <see cref="Done"/>.</summary>
        public KitSkip Decide(TraxSettings s, bool fixedEquipment, in KitAgent a, KitSlot[] slots, ref KitOrder order)
        {
            _looked++;
            var gate = UnitKitMath.Gate(s.ModEnabled, s.GiveTier1Shields, fixedEquipment, in a);
            if (gate != KitSkip.Given) return Skip(gate);
            var plan = UnitKitMath.Plan(slots, out int shieldSlot, out int swapSlot);
            if (plan != KitSkip.Given) return Skip(plan);
            if (string.IsNullOrEmpty(a.Culture)) return Skip(KitSkip.NoCulture);
            var c = CultureOf(a.Culture!, out int index);
            var shield = index >= 0 ? ResolveMain(c, index, s, shield: true, log: true) : c.Shield;
            if (shield.Item == null)
            {
                if (!c.SkipLogged)
                {
                    c.SkipLogged = true;
                    _log("first tier-1 man of " + c.Id + " skipped: " + shield.Why + " (once per culture a battle)");
                }
                return Skip(KitSkip.NoShieldForCulture);
            }
            object? spear = null;
            if (swapSlot >= 0)
            {
                var sp = index >= 0 ? ResolveMain(c, index, s, shield: false, log: true) : c.Spear;
                spear = sp.Item;
            }
            order.Culture = c.Id;
            order.ShieldSlot = shieldSlot;
            order.Shield = shield.Item;
            order.SwapSlot = swapSlot;
            order.Spear = spear;
            order.SwappedId = swapSlot >= 0 ? slots[swapSlot].Id : null;
            return KitSkip.Given;
        }

        private KitSkip Skip(KitSkip why)
        {
            _skips[(int)why]++;
            return why;
        }

        /// <summary>After the module played <paramref name="order"/>: counted, and the culture's first man said in full.</summary>
        public void Done(in KitOrder order, bool ok)
        {
            if (!ok)
            {
                _skips[(int)KitSkip.Failed]++;
                return;
            }
            var c = Find(order.Culture);
            if (c == null) return;
            if (c.ShieldsGiven == 0 && c.SpearsSwapped == 0 && c.TwoHandersKept == 0) _order.Add(c);
            c.ShieldsGiven++;
            if (order.SwapSlot >= 0)
            {
                if (order.Spear != null) c.SpearsSwapped++;
                else c.TwoHandersKept++;
            }
            if (!c.FirstLogged || (order.SwapSlot >= 0 && c.SpearsSwapped + c.TwoHandersKept == 1))
            {
                bool first = !c.FirstLogged;
                c.FirstLogged = true;
                string what = "a tier-1 man of " + c.Id + " given " + c.Shield.Facts.Describe() + " in slot " + order.ShieldSlot.ToString(CultureInfo.InvariantCulture);
                if (order.SwapSlot >= 0)
                    what += order.Spear != null
                        ? ", his two-hander " + (order.SwappedId ?? "?") + " swapped for " + c.Spear.Facts.Describe()
                        : ", his two-hander " + (order.SwappedId ?? "?") + " KEPT - " + (c.Spear.Why ?? "no spear for his culture");
                _log((first ? "first " : "first two-hander: ") + what);
            }
        }

        /// <summary>The [summary] line.</summary>
        public string SummaryLine()
        {
            var sb = new StringBuilder(256);
            int shields = 0, spears = 0, kept = 0;
            foreach (var c in _order)
            {
                shields += c.ShieldsGiven;
                spears += c.SpearsSwapped;
                kept += c.TwoHandersKept;
            }
            sb.Append("unit changes (step 84): men looked at ").Append(_looked)
              .Append(", tier-1 men given a shield ").Append(shields);
            AppendBy(sb, c => c.ShieldsGiven);
            sb.Append(", two-handers swapped for a spear ").Append(spears);
            AppendBy(sb, c => c.SpearsSwapped);
            sb.Append(", two-handers kept (no spear) ").Append(kept);
            AppendBy(sb, c => c.TwoHandersKept);
            sb.Append("; skipped:");
            bool any = false;
            for (int i = 1; i < _skips.Length; i++)
            {
                if (_skips[i] == 0) continue;
                sb.Append(any ? ", " : " ").Append(UnitKitMath.SkipName((KitSkip)i)).Append(' ').Append(_skips[i]);
                any = true;
            }
            if (!any) sb.Append(" none");
            return sb.ToString();
        }

        private void AppendBy(StringBuilder sb, Func<Culture, int> count)
        {
            bool open = false;
            foreach (var c in _order)
            {
                int n = count(c);
                if (n == 0) continue;
                sb.Append(open ? ", " : " (").Append(c.Id).Append(' ').Append(n);
                open = true;
            }
            if (open) sb.Append(')');
        }
    }
}
