using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 109 (keep your distance - AI_NOTES "Step 109"): the engine side of the reads a look needs, asked only for a man who cannot attack now (a
    /// handful of managed checks first - his pause, his stack, his brace). The walk itself goes through courage's body (<c>ToLocal</c>, <c>GroundOk</c>,
    /// the hook). The offline smoke plays a stand-in.
    /// </summary>
    internal interface IKeepBody
    {
        bool IsActive(TrackedAgent st);

        /// <summary>The AI drives him (not the player, not a man the player took over).</summary>
        bool IsAiControlled(TrackedAgent st);

        /// <summary>What he is and holds: on a horse, running away (a rout), a bow / crossbow / throwing weapon in his main hand; his wielded weapon's
        /// length (m; NaN = none / unknown) and whether it is a polearm (a spear - the summary's weapon class).</summary>
        void Kit(TrackedAgent st, out bool mounted, out bool routing, out bool ranged, out double lengthMetres, out bool spear);

        /// <summary>The man he fights - <c>Agent.GetTargetAgent()</c>, else <c>Agent.ImmediateEnemy</c>; an active enemy on foot only - and both men's
        /// places on the ground; null with none.</summary>
        Agent? Target(TrackedAgent st, out double px, out double py, out double ex, out double ey);
    }

    /// <summary>The game's side: native reads (the AI's own target, positions), managed equipment reads; main thread.</summary>
    internal sealed class GameKeepBody : IKeepBody
    {
        public bool IsActive(TrackedAgent st) => st.Agent.IsActive();

        public bool IsAiControlled(TrackedAgent st) => st.Agent.IsAIControlled && !st.Agent.IsMainAgent;

        public void Kit(TrackedAgent st, out bool mounted, out bool routing, out bool ranged, out double lengthMetres, out bool spear)
        {
            var a = st.Agent;
            mounted = a.MountAgent != null;
            routing = a.IsRunningAway || a.IsRetreating();
            ranged = false;
            spear = false;
            lengthMetres = double.NaN;
            var w = a.WieldedWeapon;
            if (w.IsEmpty) return;
            var u = w.CurrentUsageItem;
            if (u == null) return;
            ranged = u.IsRangedWeapon;
            spear = GameSpearBody.IsPolearmClass(u.WeaponClass);
            if (u.WeaponLength > 0) lengthMetres = u.WeaponLength / 100.0; // the engine's WeaponLength is in cm
        }

        public Agent? Target(TrackedAgent st, out double px, out double py, out double ex, out double ey)
        {
            px = py = ex = ey = double.NaN;
            var a = st.Agent;
            var e = a.GetTargetAgent();
            if (!IsEnemyOnFoot(a, e)) e = a.ImmediateEnemy;
            if (!IsEnemyOnFoot(a, e)) return null;
            var p = a.Position.AsVec2;
            var q = e!.Position.AsVec2;
            px = p.x;
            py = p.y;
            ex = q.x;
            ey = q.y;
            return e;
        }

        private static bool IsEnemyOnFoot(Agent a, Agent? e) => e != null && e.IsActive() && e.IsHuman && e.MountAgent == null && a.IsEnemyOf(e);
    }

    /// <summary>The body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in.</summary>
    internal static class KeepBodyDefault
    {
        internal static IKeepBody Current = new GameKeepBody();
    }
}
