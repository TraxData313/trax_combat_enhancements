using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat
{
    /// <summary>
    /// Step 84b (Toni 2026-10-24: "make the troops' gear show the shields ... as long as nothing goes into the save"; AI_NOTES "Step 84b", DESIGN
    /// decision 80b): the tier-1 troops' OWN battle sets get step 84's culture shield (and the two-hander → spear swap) IN MEMORY, so the
    /// encyclopedia, the party and the recruit screens show them. The spike: a non-hero troop's equipment is never written into a save
    /// (BasicCharacterObject has no saveable member, CharacterObject saves only its hero and origin, MBEquipmentRoster is no save type - Equipment
    /// is saved only inside a Hero). When: the game is initialized (a campaign loaded or new, a custom battle), each relevant setting change (live),
    /// a mission start (the safety net); put back at the game's end and at unload. A hero made from an edited troop gets the game's gear in his copy
    /// and the troop is left alone (CampaignEvents.HeroCreated, a NON-serialized listener). Every entry wrapped: an error = [error], the game goes on.
    /// </summary>
    internal static class TroopGear
    {
        private static readonly TroopKit Book = new TroopKit(line => TraxLog.Info("units", line));
        private static ITroopKitWorld? _world;
        private static object? _game;
        private static bool _subscribed;

        /// <summary>The game's side; the smoke swaps it.</summary>
        internal static Func<ITroopKitWorld> WorldFactory = () => new GameTroopKitWorld();

        internal static TroopKit Kit => Book;

        internal static bool Attached => _world != null;

        /// <summary>Once, at load: listen to the settings (a change syncs live while a game runs).</summary>
        internal static void Initialize()
        {
            if (_subscribed) return;
            _subscribed = true;
            TraxSettings.Shared.Changed += OnSettingChanged;
        }

        /// <summary>The game's objects are ready (OnAfterGameInitializationFinished) - or the first mission of a game we missed.</summary>
        internal static void GameReady(object game, string why)
        {
            try
            {
                if (_world != null && ReferenceEquals(_game, game))
                {
                    Book.Sync(_world, TraxSettings.Shared, why);
                    return;
                }
                if (_world != null) Detach("another game - " + why);
                Attach(WorldFactory(), game, why);
            }
            catch (Exception e)
            {
                TraxLog.Error("units.troopgear.ready", e);
            }
        }

        /// <summary>Starts on a world (the smoke calls it with its stand-in).</summary>
        internal static void Attach(ITroopKitWorld world, object game, string why)
        {
            _world = world;
            _game = game;
            if (world is GameTroopKitWorld) TroopGearCampaign.Hook();
            Book.Sync(world, TraxSettings.Shared, why);
        }

        /// <summary>The game ends / the mod unloads: every changed slot put back.</summary>
        internal static void Detach(string why)
        {
            var world = _world;
            _world = null;
            _game = null;
            if (world == null) return;
            try
            {
                if (world is GameTroopKitWorld) TroopGearCampaign.Unhook();
                Book.Restore(world, why);
            }
            catch (Exception e)
            {
                TraxLog.Error("units.troopgear.restore", e);
            }
        }

        private static void OnSettingChanged(SettingChange change)
        {
            var world = _world;
            if (world == null || change?.Param == null || !Relevant(change.Param)) return;
            try
            {
                Book.Sync(world, TraxSettings.Shared, "setting " + change.Param.Key);
            }
            catch (Exception e)
            {
                TraxLog.Error("units.troopgear.setting", e);
            }
        }

        internal static bool Relevant(ParamDef p)
        {
            if (ReferenceEquals(p, SettingsSchema.ModEnabled) || ReferenceEquals(p, SettingsSchema.GiveTier1Shields)) return true;
            for (int i = 0; i < UnitKitMath.Cultures.Length; i++)
                if (ReferenceEquals(p, UnitKitMath.ShieldSetting(i)) || ReferenceEquals(p, UnitKitMath.SpearSetting(i))) return true;
            return false;
        }

        /// <summary>A hero was made from <paramref name="template"/> and copied one of its sets (Hero.BattleEquipment): give the copy the game's
        /// gear back and leave the troop alone.</summary>
        internal static void HeroMade(object? template, IKitCopy copy, string who)
        {
            var world = _world;
            if (world == null || template == null) return;
            try
            {
                Book.UndoCopy(world, template, copy, who);
            }
            catch (Exception e)
            {
                TraxLog.Error("units.troopgear.hero", e);
            }
        }
    }

    /// <summary>Step 84b: the campaign's part (CampaignSystem types in method bodies only; Campaign.Current is null outside a campaign).</summary>
    internal static class TroopGearCampaign
    {
        private static readonly object Owner = new object();
        private static bool _hooked;

        internal static void Hook()
        {
            if (Campaign.Current == null) return;
            if (_hooked) Unhook();
            CampaignEvents.HeroCreated.AddNonSerializedListener(Owner, new Action<Hero, bool>(OnHeroCreated));
            _hooked = true;
        }

        internal static void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            try
            {
                if (Campaign.Current != null) CampaignEvents.HeroCreated.ClearListeners(Owner);
            }
            catch
            {
                // the campaign is gone with its events
            }
        }

        /// <summary>Every troop a hero of this campaign was made from (its Template / origin).</summary>
        internal static void HeroTemplates(HashSet<object> into)
        {
            if (Campaign.Current == null) return;
            Add(Hero.AllAliveHeroes, into);
            Add(Hero.DeadOrDisabledHeroes, into);
        }

        private static void Add(IReadOnlyList<Hero>? heroes, HashSet<object> into)
        {
            if (heroes == null) return;
            for (int i = 0; i < heroes.Count; i++)
            {
                var t = heroes[i]?.CharacterObject?.OriginalCharacter;
                if (t != null) into.Add(t);
            }
        }

        private static void OnHeroCreated(Hero hero, bool isOffspring)
        {
            var template = hero?.CharacterObject?.OriginalCharacter;
            if (hero == null || template == null) return;
            TroopGear.HeroMade(template, new HeroCopy(hero), hero.Name?.ToString() ?? hero.StringId);
        }

        private sealed class HeroCopy : IKitCopy
        {
            private readonly Hero _hero;

            public HeroCopy(Hero hero) => _hero = hero;

            public bool SameAs(object set) => _hero.BattleEquipment != null && _hero.BattleEquipment.IsEquipmentEqualTo(set as Equipment);

            public bool Holds(int slot, object item) => _hero.BattleEquipment != null && ReferenceEquals(_hero.BattleEquipment[slot].Item, item);

            public void Write(int slot, object? value)
            {
                if (_hero.BattleEquipment == null) return;
                _hero.BattleEquipment[slot] = value is EquipmentElement e ? e : EquipmentElement.Invalid;
            }
        }
    }

    /// <summary>Step 84b: the game's side - every character of MBObjectManager, its battle sets, who else holds each set.</summary>
    internal sealed class GameTroopKitWorld : ITroopKitWorld
    {
        public IKitItems Items { get; } = new GameKitItems();

        public void ReadTroops(List<KitTroop> into)
        {
            var om = MBObjectManager.Instance;
            if (om == null) return;
            var characters = om.GetObjectTypeList<BasicCharacterObject>();
            var rosters = om.GetObjectTypeList<MBEquipmentRoster>();
            if (characters == null) return;
            // who holds each Equipment object: the registered rosters (a template such as *_troop_civilian_template_t1 and the troop that
            // names it in an EquipmentSet both hold it) and the non-hero characters; a hero made from a troop (CharacterObject.CreateFrom
            // shares its template's roster) is caught by HeroTemplate - the game shows only his own Hero.BattleEquipment
            var inRosters = new Dictionary<Equipment, int>(RefEq.Instance);
            if (rosters != null)
            {
                for (int r = 0; r < rosters.Count; r++)
                {
                    var all = rosters[r]?.AllEquipments;
                    if (all == null) continue;
                    var seen = new HashSet<Equipment>(RefEq.Instance);
                    for (int i = 0; i < all.Count; i++)
                        if (all[i] != null && seen.Add(all[i])) inRosters[all[i]] = inRosters.TryGetValue(all[i], out int n) ? n + 1 : 1;
                }
            }
            var inChars = new Dictionary<Equipment, int>(RefEq.Instance);
            for (int c = 0; c < characters.Count; c++)
            {
                var ch = characters[c];
                if (ch == null || ch.IsHero) continue; // a hero's sets are his own (Hero.BattleEquipment); his template is caught below
                var seen = new HashSet<Equipment>(RefEq.Instance);
                foreach (var eq in SetsOf(ch))
                    if (seen.Add(eq)) inChars[eq] = inChars.TryGetValue(eq, out int n) ? n + 1 : 1;
            }
            var templates = new HashSet<object>(RefObj.Instance);
            TroopGearCampaign.HeroTemplates(templates);
            var player = Game.Current?.PlayerTroop;
            for (int c = 0; c < characters.Count; c++)
            {
                var ch = characters[c];
                if (ch == null || ch.IsObsolete) continue;
                var t = new KitTroop
                {
                    Handle = ch,
                    Id = ch.StringId ?? "?",
                    Culture = ch.Culture?.StringId,
                    Hero = ch.IsHero,
                    Player = ReferenceEquals(ch, player),
                    Soldier = ch.IsSoldier,
                    HeroTemplate = templates.Contains(ch),
                };
                t.Tier = t.Soldier && !t.Hero ? ch.GetBattleTier() : -1;
                if (t.Soldier && !t.Hero && !t.Player && t.Tier == UnitKitMath.GivenTier)
                {
                    foreach (var eq in SetsOf(ch))
                    {
                        if (!eq.IsBattle) continue;
                        var set = new KitSet { Handle = eq };
                        for (int s = 0; s < UnitKitMath.Slots; s++)
                        {
                            var el = eq[s];
                            GameUnitKitBody.ReadSlot(el.IsEmpty ? null : el.Item, ref set.Slots[s]);
                            set.Originals[s] = el;
                        }
                        set.Horse = !eq[EquipmentIndex.Horse].IsEmpty;
                        set.OwnedAlone = (inRosters.TryGetValue(eq, out int nr) ? nr : 0) <= 1 && (inChars.TryGetValue(eq, out int nc) ? nc : 0) <= 1;
                        t.Sets.Add(set);
                    }
                }
                into.Add(t);
            }
        }

        /// <summary>A non-hero character's sets (its roster's battle and civilian sets - AllEquipments is protected).</summary>
        private static IEnumerable<Equipment> SetsOf(BasicCharacterObject ch)
        {
            var battle = ch.BattleEquipments;
            if (battle != null)
                foreach (var eq in battle)
                    if (eq != null) yield return eq;
            var civilian = ch.CivilianEquipments;
            if (civilian != null)
                foreach (var eq in civilian)
                    if (eq != null) yield return eq;
        }

        public bool Write(object set, int slot, object? value)
        {
            if (!(set is Equipment eq) || slot < 0 || slot >= UnitKitMath.Slots) return false;
            if (value is ItemObject item) eq[slot] = new EquipmentElement(item);
            else if (value is EquipmentElement e) eq[slot] = e;
            else if (value == null) eq[slot] = EquipmentElement.Invalid;
            else return false;
            return true;
        }

        public bool Holds(object set, int slot, object item) =>
            set is Equipment eq && slot >= 0 && slot < UnitKitMath.Slots && ReferenceEquals(eq[slot].Item, item);

        private sealed class RefEq : IEqualityComparer<Equipment>
        {
            public static readonly RefEq Instance = new RefEq();
            public bool Equals(Equipment? x, Equipment? y) => ReferenceEquals(x, y);
            public int GetHashCode(Equipment obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private sealed class RefObj : IEqualityComparer<object>
        {
            public static readonly RefObj Instance = new RefObj();
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
