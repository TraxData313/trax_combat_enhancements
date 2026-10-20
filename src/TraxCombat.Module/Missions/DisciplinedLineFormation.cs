using System;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 73 - THE PREVIEW CAP (AI_NOTES "Step 73", DESIGN decision 67 c): the game's own plain line arrangement with ONE difference - its
    /// <see cref="MaximumWidth"/> answers the line discipline's cap. Vanilla's order drag (OrderTroopPlacer → OrderController
    /// .SimulateNewOrderWithPositionAndDirection) and the move it issues (MoveToLineSegment, the same simulation) both clamp each formation's
    /// width to <c>formation.MaximumWidth</c> = <c>Arrangement.MaximumWidth</c> (virtual) - so the preview's men stop at the cap and the order
    /// keeps to it. No Harmony, nothing patched.
    ///
    /// Not a game MODEL (the house rule against subclassing is about AddModel, which replaces by base type): a per-formation object the
    /// game itself swaps on every arrangement order (`Formation.Rearrange`); ours is put in with the game's own `Rearrange` - a line → line
    /// transfer hands the unit tables over by reference (LineFormation.RearrangeTransferUnits), nothing is rebuilt - and the game's next
    /// arrangement order puts its own plain line back (we put ours in again at the next look). Only the player's own formations get it, only
    /// over the game's PLAIN LineFormation (never a column's TransposedLineFormation or another mod's arrangement).
    /// </summary>
    internal sealed class DisciplinedLineFormation : LineFormation
    {
        private long _capReads;

        public DisciplinedLineFormation(IFormation ownerFormation, bool isStaggered)
            : base(ownerFormation, isStaggered)
        {
        }

        /// <summary>The cap (m), NaN = none (a simulation copy, or no cap needed). Written on the main thread between the game's jobs.</summary>
        public float CapWidth = float.NaN;

        /// <summary>How many times the game asked for the maximum width and got the cap (the summary's proof the drag / the move read it).</summary>
        public long CapReads => Interlocked.Read(ref _capReads);

        public override float MaximumWidth
        {
            get
            {
                float game = base.MaximumWidth;
                float cap = CapWidth;
                if (float.IsNaN(cap) || cap <= 0 || cap >= game) return game;
                Interlocked.Increment(ref _capReads);
                return Math.Max(cap, MinimumWidth); // never below the game's own minimum (the cap already is not)
            }
        }

        /// <summary>The game's simulation formations clone the arrangement they preview: the same type (vanilla resets its simulation whenever
        /// the types differ - every unit of every frame), but no cap of its own - the real formation's MaximumWidth already capped the width.</summary>
        public override IFormationArrangement Clone(IFormation formation) => new DisciplinedLineFormation(formation, IsStaggered);
    }
}
