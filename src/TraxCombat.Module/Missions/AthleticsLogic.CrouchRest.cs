using System;
using System.Globalization;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 103 - CROUCH REST (Toni 2026-11-09: "when I crouch (Z) increase the Athletics regeneration x2 (player only, but let this be
    /// there)"; AI_NOTES "Step 103", DESIGN decision 92). Inside the regen pass: a man the rule applies to (Core
    /// <see cref="CrouchRestMath.Applies"/> - you by <c>CrouchRefillForPlayer</c>, the AI by <c>CrouchRefillForAi</c>, never mounted) has his
    /// crouch read (<c>Agent.CrouchMode</c> - the engine's state the Z key toggles; one native read a pass, for you alone by default) and his
    /// refill rate multiplied by <c>CrouchRefillMultiplier</c> (<see cref="AthleticsMath.Regen"/>'s extra multiplier - the delay after a
    /// blow, the wounds' cap, the pace and no refill while sprinting all still apply). Your bar's label reads "Resting" while it refills
    /// crouched. Nothing allocated per tick; the [athletics] first-time line and the [summary] "crouch rest" line.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly CrouchRestStats _crouchStats = new CrouchRestStats();
        private bool _crouchFirst, _crouchWas;

        internal CrouchRestStats CrouchRestStats => _crouchStats;

        /// <summary>The crouch read (the engine's <c>Agent.CrouchMode</c>); the offline smoke hands in a stand-in.</summary>
        internal Func<Agent, bool> CrouchRead { get; set; } = GameCrouchRead;

        private static bool GameCrouchRead(Agent a) => a.CrouchMode;

        /// <summary>The refill multiplier for this man this pass (1 = none). Reads his crouch only when the rule applies to him.</summary>
        internal double CrouchMultiplierFor(TrackedAgent st, bool mounted, in CrouchRestRules cr, double now)
        {
            if (!cr.Enabled) return 1.0;
            bool player = IsPlayer(st);
            if (!CrouchRestMath.Applies(in cr, player, mounted))
            {
                if (player) _crouchWas = false;
                return 1.0;
            }
            bool crouched;
            try
            {
                crouched = CrouchRead(st.Agent);
            }
            catch (Exception e)
            {
                _crouchStats.Errors++;
                if (_crouchStats.Errors == 1) Failed("athletics.crouch", e);
                return 1.0;
            }
            if (player)
            {
                if (crouched && !_crouchWas)
                {
                    _crouchStats.PlayerCrouches++;
                    if (!_crouchFirst)
                    {
                        _crouchFirst = true;
                        TraxLog.Info("athletics", "YOU: first crouch at " + now.ToString("0.0", CultureInfo.InvariantCulture) + " s - " + cr.Describe());
                    }
                }
                _crouchWas = crouched;
            }
            return CrouchRestMath.Multiplier(in cr, player, mounted, crouched);
        }

        /// <summary>After the regen step: the crouched refill counted (points with the multiplier in them) and your bar's "Resting".</summary>
        internal void NoteCrouchRefill(TrackedAgent st, double crouch, double step, in RegenOutcome o, in AthleticsRules r)
        {
            if (!(crouch > 1.0)) return;
            bool player = IsPlayer(st);
            if (o.Seconds > 0)
            {
                _crouchStats.Note(player, o.Seconds, o.Gained * AthleticsMath.PoolPoints(in r, st));
                if (player) st.CrouchResting = o.Gained > 0;
            }
            else if (player) _crouchStats.PlayerIdleSeconds += step;
        }

        /// <summary>The summary's turn (after the hideout's).</summary>
        private void WriteCrouchRestSummary() => TraxLog.Info("summary", _crouchStats.SummaryLine(CrouchRestRules.From(TraxSettings.Shared)));
    }
}
