using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 84b (Toni 2026-10-24, his F12 of the Sturgian Recruit in the encyclopedia with no shield: "make the troops' gear show the shields ...
    /// as long as nothing goes into the save"): why a troop's own battle gear was or was not edited in memory. <see cref="TroopSkip.Edited"/> =
    /// edited; every other value is why not, in GATE ORDER (the master switch first).
    /// </summary>
    public enum TroopSkip
    {
        Edited,
        ModOff,
        SwitchOff,
        Player,
        Hero,
        NotSoldier,
        NotTier1,
        Rider,
        HeroTemplate,
        NoBattleSet,
        SharedSet,
        HasShield,
        NoCommonSlot,
        NoShieldForCulture,
    }

    /// <summary>One battle equipment set of a troop as the module read it. <see cref="Handle"/> = the game's Equipment; <see cref="Originals"/> =
    /// each weapon slot's element as the game had it (boxed - written back by <see cref="ITroopKitWorld.Write"/> on a restore).</summary>
    public sealed class KitSet
    {
        public object Handle = null!;
        public readonly KitSlot[] Slots = new KitSlot[UnitKitMath.Slots];
        public readonly object?[] Originals = new object?[UnitKitMath.Slots];

        /// <summary>A horse in it (a rider's set - step 84 never gives a rider a shield).</summary>
        public bool Horse;

        /// <summary>The set belongs to this troop alone: no other character and no other registered roster holds the same object (a template
        /// such as <c>*_troop_civilian_template_t1</c>, or a hero made from this troop, would share it).</summary>
        public bool OwnedAlone = true;
    }

    /// <summary>One character (a troop) as the module read it.</summary>
    public sealed class KitTroop
    {
        public object Handle = null!;
        public string Id = string.Empty;
        public string? Culture;
        public int Tier;
        public bool Hero;
        public bool Player;

        /// <summary>The game's IsSoldier (occupation "Soldier") - templates of wanderers, notables and lords are not.</summary>
        public bool Soldier;

        /// <summary>A hero of this campaign was made from this troop (its Template / origin) - its gear is copied into the save at creation.</summary>
        public bool HeroTemplate;

        public readonly List<KitSet> Sets = new List<KitSet>();
    }

    /// <summary>The game's side of step 84b (the module plays it with MBObjectManager; tests and the smoke with stand-ins).</summary>
    public interface ITroopKitWorld
    {
        IKitItems Items { get; }

        /// <summary>Every character worth a look, read fresh (the module reads the game's objects; nothing kept between reads).</summary>
        void ReadTroops(List<KitTroop> into);

        /// <summary>Writes <paramref name="value"/> into weapon slot <paramref name="slot"/> of <paramref name="set"/>: an item from
        /// <see cref="Items"/> (a new element), or an <see cref="KitSet.Originals"/> entry (the game's own element back). False = refused.</summary>
        bool Write(object set, int slot, object? value);

        /// <summary>The slot still holds <paramref name="item"/> (a restore puts the original back only over what we put there).</summary>
        bool Holds(object set, int slot, object item);
    }

    /// <summary>A copy of one of a troop's battle sets made by the game (a hero created from the troop - Hero.BattleEquipment).</summary>
    public interface IKitCopy
    {
        /// <summary>The copy equals <paramref name="set"/> slot by slot as it stands now.</summary>
        bool SameAs(object set);

        /// <summary>The copy's slot holds <paramref name="item"/>.</summary>
        bool Holds(int slot, object item);

        void Write(int slot, object? value);
    }

    /// <summary>One slot we changed: what was there, what we put.</summary>
    public struct TroopEdit
    {
        public object Troop;
        public object Set;
        public int Slot;
        public object? Original;
        public object Given;
        public bool Shield;
    }

    /// <summary>The rules - pure functions.</summary>
    public static class TroopKitMath
    {
        /// <summary>What the edit depends on: "off" while the master switch or the switch is off, else the 10 item settings. The same signature
        /// twice = nothing to do (apply twice = once); a change = restore, then apply again.</summary>
        public static string Signature(TraxSettings s)
        {
            if (!s.ModEnabled) return "off (mod off)";
            if (!s.GiveTier1Shields) return "off (GiveTier1Shields)";
            var sb = new StringBuilder("on", 96);
            for (int i = 0; i < UnitKitMath.Cultures.Length; i++)
            {
                sb.Append('|').Append(s.Get(UnitKitMath.ShieldSetting(i)).ToString(CultureInfo.InvariantCulture));
                var sp = UnitKitMath.SpearSetting(i);
                if (sp != null) sb.Append('/').Append(s.Get(sp).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static bool IsOn(string signature) => signature.StartsWith("on", StringComparison.Ordinal);

        /// <summary>The troop's gates in order (the master switch first), then its sets: none a battle set, one shared, one with a shield, no
        /// weapon slot free in EVERY set. <paramref name="shieldSlot"/> = the first slot free in all of them - the game mixes slots 0-1 of one set
        /// with 2-3 of another at spawn (Equipment.GetRandomEquipmentElements), so the shield must sit in the same slot in every set: then any mix
        /// carries exactly one.</summary>
        public static TroopSkip Plan(bool modOn, bool switchOn, KitTroop t, out int shieldSlot)
        {
            shieldSlot = -1;
            if (!modOn) return TroopSkip.ModOff;
            if (!switchOn) return TroopSkip.SwitchOff;
            if (t.Player) return TroopSkip.Player;
            if (t.Hero) return TroopSkip.Hero;
            if (!t.Soldier) return TroopSkip.NotSoldier;
            if (t.Tier != UnitKitMath.GivenTier) return TroopSkip.NotTier1;
            if (t.Sets.Count == 0) return TroopSkip.NoBattleSet;
            foreach (var set in t.Sets)
                if (set.Horse) return TroopSkip.Rider;
            if (t.HeroTemplate) return TroopSkip.HeroTemplate;
            foreach (var set in t.Sets)
                if (!set.OwnedAlone) return TroopSkip.SharedSet;
            foreach (var set in t.Sets)
                for (int i = 0; i < UnitKitMath.Slots; i++)
                    if (!set.Slots[i].Empty && set.Slots[i].Shield) return TroopSkip.HasShield;
            for (int i = 0; i < UnitKitMath.Slots; i++)
            {
                bool free = true;
                foreach (var set in t.Sets)
                    if (!set.Slots[i].Empty) { free = false; break; }
                if (free)
                {
                    shieldSlot = i;
                    return TroopSkip.Edited;
                }
            }
            return TroopSkip.NoCommonSlot;
        }

        /// <summary>The set's first two-handed-only melee weapon (step 84's rule), -1 = none.</summary>
        public static int SwapSlot(KitSet set)
        {
            for (int i = 0; i < UnitKitMath.Slots; i++)
                if (UnitKitMath.TwoHandedOnly(in set.Slots[i])) return i;
            return -1;
        }

        public static string SkipName(TroopSkip s)
        {
            switch (s)
            {
                case TroopSkip.Edited: return "edited";
                case TroopSkip.ModOff: return "mod off";
                case TroopSkip.SwitchOff: return "switched off";
                case TroopSkip.Player: return "you";
                case TroopSkip.Hero: return "a hero";
                case TroopSkip.NotSoldier: return "not a soldier";
                case TroopSkip.NotTier1: return "not tier 1";
                case TroopSkip.Rider: return "a rider";
                case TroopSkip.HeroTemplate: return "a hero was made from it";
                case TroopSkip.NoBattleSet: return "no battle set";
                case TroopSkip.SharedSet: return "a battle set shared with another character";
                case TroopSkip.HasShield: return "has a shield";
                case TroopSkip.NoCommonSlot: return "no slot free in every set";
                default: return "no shield for its culture";
            }
        }
    }

    /// <summary>
    /// Step 84b: the BOOKKEEPING of the in-memory edit of the tier-1 troops' own battle gear - the same rules and items as step 84's spawn
    /// (<see cref="UnitKit"/>), every changed slot remembered with the game's own element so it can be put back exactly. <see cref="Sync"/> is the
    /// one entry: the same signature = nothing (apply twice = once); another = restore everything, then apply under the new one. Main thread.
    /// </summary>
    public sealed class TroopKit
    {
        private readonly Action<string> _log;
        private readonly List<TroopEdit> _edits = new List<TroopEdit>();
        private readonly List<KitTroop> _troops = new List<KitTroop>();
        private readonly HashSet<object> _excluded = new HashSet<object>(ReferenceComparer.Instance);
        private readonly int[] _skips = new int[(int)TroopSkip.NoShieldForCulture + 1];
        private string? _signature;

        public TroopKit(Action<string> log) => _log = log ?? (_ => { });

        /// <summary>The signature last synced (null = nothing synced since the last <see cref="Restore"/>).</summary>
        public string? Signature => _signature;

        public IReadOnlyList<TroopEdit> Edits => _edits;

        public int TroopsEdited { get; private set; }

        public int SetsEdited { get; private set; }

        public int ShieldsPut { get; private set; }

        public int SpearsSwapped { get; private set; }

        public int TwoHandersKept { get; private set; }

        public int Skipped(TroopSkip why) => _skips[(int)why];

        /// <summary>Restores how many slots in all (the last restore).</summary>
        public int LastRestored { get; private set; }

        /// <summary>Slots the last restore left alone because something else had changed them since.</summary>
        public int LastRestoreChangedSince { get; private set; }

        public bool IsEdited(object troop)
        {
            foreach (var e in _edits)
                if (ReferenceEquals(e.Troop, troop)) return true;
            return false;
        }

        /// <summary>Brings the troops' gear in line with the settings: nothing when the signature is the one applied; else restore, then (on)
        /// apply. Returns true when it changed anything.</summary>
        public bool Sync(ITroopKitWorld world, TraxSettings s, string why)
        {
            string sig = TroopKitMath.Signature(s);
            if (_signature == sig) return false;
            bool had = _edits.Count > 0;
            if (had) RestoreAll(world, why + " - settings changed");
            _signature = sig;
            if (!TroopKitMath.IsOn(sig))
            {
                if (had) _log("troop gear (step 84b): " + sig + " - the troops' own gear is the game's");
                return had;
            }
            Apply(world, s, why);
            return had || _edits.Count > 0;
        }

        /// <summary>Puts every changed slot back (the game ends, the mod unloads, a different game starts) and forgets the signature, so the next
        /// <see cref="Sync"/> applies afresh. The troops excluded by a hero copy are forgotten too (another game).</summary>
        public void Restore(ITroopKitWorld world, string why)
        {
            if (_edits.Count > 0) RestoreAll(world, why);
            _signature = null;
            _excluded.Clear();
        }

        /// <summary>The game copied one of <paramref name="troop"/>'s sets (a hero made from it): the copy gets the game's own elements back in
        /// the slots we changed, and the troop is restored and left alone from now on (Hero.ResetEquipments copies its template again later).
        /// Returns the copy's slots put back (0 = not our troop, or the copy matches no edited set).</summary>
        public int UndoCopy(ITroopKitWorld world, object troop, IKitCopy copy, string who)
        {
            if (!IsEdited(troop)) return 0;
            object? match = null;
            foreach (var e in _edits)
                if (ReferenceEquals(e.Troop, troop) && copy.SameAs(e.Set)) { match = e.Set; break; }
            int undone = 0;
            if (match != null)
            {
                foreach (var e in _edits)
                {
                    if (!ReferenceEquals(e.Set, match)) continue;
                    copy.Write(e.Slot, e.Original);
                    undone++;
                }
            }
            else
            {
                // changed after the copy (the game's own hero setup): every slot still holding what we gave gets an original of that slot - the
                // shield's slot was free in every set, a spear's original is one of the troop's own two-handers
                var done = new bool[UnitKitMath.Slots];
                foreach (var e in _edits)
                {
                    if (!ReferenceEquals(e.Troop, troop) || e.Slot < 0 || e.Slot >= done.Length || done[e.Slot]) continue;
                    if (!copy.Holds(e.Slot, e.Given)) continue;
                    copy.Write(e.Slot, e.Original);
                    done[e.Slot] = true;
                    undone++;
                }
            }
            int restored = 0;
            for (int i = _edits.Count - 1; i >= 0; i--)
            {
                var e = _edits[i];
                if (!ReferenceEquals(e.Troop, troop)) continue;
                if (world.Holds(e.Set, e.Slot, e.Given) && world.Write(e.Set, e.Slot, e.Original)) restored++;
                _edits.RemoveAt(i);
            }
            _excluded.Add(troop);
            TroopsEdited = Math.Max(0, TroopsEdited - 1);
            _log("troop gear (step 84b): a hero (" + who + ") was made from an edited troop - " + undone + " slot(s) of his copy given the game's gear"
                 + (match == null ? " (his copy matched no edited set)" : string.Empty) + ", the troop's " + restored
                 + " slot(s) put back and the troop left alone this game");
            return undone;
        }

        private void Apply(ITroopKitWorld world, TraxSettings s, string why)
        {
            Array.Clear(_skips, 0, _skips.Length);
            TroopsEdited = SetsEdited = ShieldsPut = SpearsSwapped = TwoHandersKept = 0;
            _troops.Clear();
            world.ReadTroops(_troops);
            var kit = new UnitKit(world.Items, _log);
            var cultures = new List<string>();
            foreach (var t in _troops)
            {
                var plan = TroopKitMath.Plan(s.ModEnabled, s.GiveTier1Shields, t, out int slot);
                if (plan == TroopSkip.Edited && _excluded.Contains(t.Handle)) plan = TroopSkip.HeroTemplate;
                if (plan != TroopSkip.Edited)
                {
                    _skips[(int)plan]++;
                    continue;
                }
                if (!kit.ItemsFor(s, t.Culture ?? string.Empty, out var shield, out var spear, out _))
                {
                    _skips[(int)TroopSkip.NoShieldForCulture]++;
                    continue;
                }
                int setsHere = 0;
                foreach (var set in t.Sets)
                {
                    if (!world.Write(set.Handle, slot, shield!)) continue;
                    _edits.Add(new TroopEdit { Troop = t.Handle, Set = set.Handle, Slot = slot, Original = set.Originals[slot], Given = shield!, Shield = true });
                    ShieldsPut++;
                    setsHere++;
                    int swap = TroopKitMath.SwapSlot(set);
                    if (swap < 0) continue;
                    if (spear != null && world.Write(set.Handle, swap, spear))
                    {
                        _edits.Add(new TroopEdit { Troop = t.Handle, Set = set.Handle, Slot = swap, Original = set.Originals[swap], Given = spear });
                        SpearsSwapped++;
                    }
                    else
                    {
                        TwoHandersKept++;
                    }
                }
                if (setsHere == 0) continue;
                TroopsEdited++;
                SetsEdited += setsHere;
                string c = t.Culture ?? "?";
                if (!cultures.Contains(c)) cultures.Add(c);
            }
            _skips[(int)TroopSkip.Edited] = TroopsEdited;
            var sb = new StringBuilder(256);
            sb.Append("troop gear (step 84b, ").Append(why).Append("): ").Append(TroopsEdited).Append(" tier-1 troops, ").Append(SetsEdited)
              .Append(" battle sets edited IN MEMORY (nothing saved) - shields put ").Append(ShieldsPut).Append(", two-handers swapped for a spear ")
              .Append(SpearsSwapped).Append(", two-handers kept (no spear) ").Append(TwoHandersKept).Append("; troops looked at ").Append(_troops.Count)
              .Append(", skipped:");
            bool any = false;
            for (int i = 1; i < _skips.Length; i++)
            {
                if (_skips[i] == 0) continue;
                sb.Append(any ? ", " : " ").Append(TroopKitMath.SkipName((TroopSkip)i)).Append(' ').Append(_skips[i]);
                any = true;
            }
            if (!any) sb.Append(" none");
            if (cultures.Count > 0)
            {
                sb.Append("; by culture:");
                foreach (var c in cultures)
                    sb.Append(' ').Append(c).Append(" = ").Append(kit.Describe(c, true)).Append(" / spear ").Append(kit.Describe(c, false)).Append(';');
            }
            _log(sb.ToString());
            _troops.Clear();
        }

        private void RestoreAll(ITroopKitWorld world, string why)
        {
            int restored = 0, changed = 0;
            var troops = new HashSet<object>(ReferenceComparer.Instance);
            var sets = new HashSet<object>(ReferenceComparer.Instance);
            for (int i = _edits.Count - 1; i >= 0; i--)
            {
                var e = _edits[i];
                troops.Add(e.Troop);
                sets.Add(e.Set);
                bool ok = false;
                try
                {
                    ok = world.Holds(e.Set, e.Slot, e.Given) && world.Write(e.Set, e.Slot, e.Original);
                }
                catch
                {
                    ok = false; // the game's objects may be gone at its end - nothing left to put back
                }
                if (ok) restored++;
                else changed++;
            }
            _edits.Clear();
            LastRestored = restored;
            LastRestoreChangedSince = changed;
            TroopsEdited = SetsEdited = ShieldsPut = SpearsSwapped = TwoHandersKept = 0;
            _log("troop gear (step 84b): restored " + restored + " slot(s) in " + sets.Count + " battle sets of " + troops.Count + " troops (" + why + ")"
                 + (changed > 0 ? "; " + changed + " slot(s) left as they were - changed since by something else" : string.Empty));
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
