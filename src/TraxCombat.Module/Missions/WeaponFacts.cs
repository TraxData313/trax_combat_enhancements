using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 21: what a fighter holds at an attack's RELEASE - the battle-pace class is read from it (a shield in the other
    /// hand, the ranged weapon), never from his formation or troop class. Behind one seam (the step-5d pattern): the real one
    /// reads the agent (<see cref="GameWeaponFacts"/>); the offline smoke plays a stand-in (its agents have no native side).
    /// </summary>
    internal interface IWeaponFacts
    {
        /// <summary>At a release: <paramref name="shieldInOffHand"/> for a melee attack, the <paramref name="ranged"/> weapon for a
        /// ranged one (<paramref name="throwing"/> = the engine's throwing release). May run inside an engine hit callback.</summary>
        void Read(TrackedAgent st, bool melee, bool throwing, out bool shieldInOffHand, out RangedWeaponKind ranged);
    }

    /// <summary>
    /// The game side: <c>Agent.WieldedOffhandWeapon</c> / <c>Agent.WieldedWeapon</c> - each is the wielded slot's index (the
    /// agent's own memory, read through a pointer the game keeps - no engine call) and the managed equipment item, so it is
    /// safe inside a hit callback. A shield = the off-hand item's current usage is a shield (<c>WeaponComponentData.IsShield</c>);
    /// the ranged weapon = the main-hand item's <c>WeaponClass</c> (Bow, Crossbow; anything else - a sling, a musket - "other").
    /// </summary>
    internal sealed class GameWeaponFacts : IWeaponFacts
    {
        public void Read(TrackedAgent st, bool melee, bool throwing, out bool shieldInOffHand, out RangedWeaponKind ranged)
        {
            shieldInOffHand = false;
            ranged = RangedWeaponKind.None;
            var a = st.Agent;
            if (melee)
            {
                var off = a.WieldedOffhandWeapon.CurrentUsageItem;
                shieldInOffHand = off != null && off.IsShield;
                return;
            }
            if (throwing)
            {
                ranged = RangedWeaponKind.Other;
                return;
            }
            var main = a.WieldedWeapon.CurrentUsageItem;
            ranged = main == null ? RangedWeaponKind.Other
                : main.WeaponClass == WeaponClass.Bow ? RangedWeaponKind.Bow
                : main.WeaponClass == WeaponClass.Crossbow ? RangedWeaponKind.Crossbow
                : RangedWeaponKind.Other;
        }
    }

    /// <summary>The weapon read every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its
    /// stand-in once, before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class WeaponFactsDefault
    {
        internal static IWeaponFacts Current = new GameWeaponFacts();
    }
}
