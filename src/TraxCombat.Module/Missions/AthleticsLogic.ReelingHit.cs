using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 105 - A HIT ON A REELING MAN KNOCKS HIM DOWN (Toni 2026-11-09: "when hitting (swing/kick/bash) an opponent that is staggering has
    /// 80% chance to knock him down no matter what or who caused the stagger"; AI_NOTES "Step 105", DESIGN decision 94). The decision is Core's
    /// (<see cref="ReelingHitMath.Decide"/>, inside <see cref="ImpactMath.Decide"/> right after the usual knockdown roll): it only REPLACES the
    /// knockdown roll of a landed blow, so the knockdown goes through the same proven path as every other one of the impact system - the
    /// damage decorator's KnockDown seam (step 50 / 52: ApplyKick / ApplyHit answer it, the fall watched and slowed, our own blow as the backup
    /// when no fall shows - step 62), the on-screen YOU lines (step 53's ImpactMessage), the punish window (step 54). The blow's damage is the
    /// engine's (computed before the KnockDown question - Mission.CreateMeleeBlow) and the PERFECT STRIKE's multiplier rides in the damage
    /// decorator as before; a killing blow is never decided (he just dies - counted here).
    /// Here: the count for the [summary] line and the first lines. Main thread; no allocation per hit (lines built only when written).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly ReelingHitStats _reelingHits = new ReelingHitStats();
        private bool _reelingFirstAsked, _reelingFirstDown, _reelingFirstNotApplied;

        /// <summary>Step 105: this mission's hits on reeling men (the smoke reads them).</summary>
        internal ReelingHitStats ReelingHitStats => _reelingHits;

        private void StartReelingHits()
        {
            TraxLog.Info("impact", "mission start: " + ImpactRules.From(TraxSettings.Shared).ReelingHit.Describe() + " (step 105; read live; the [summary] \"hits on reeling men\" line)");
        }

        /// <summary>A decided landed blow on a reeling man (from ImpactDecide, after the apply): counted; its first lines.</summary>
        private void NoteReelingHit(TrackedAgent a, TrackedAgent v, in ImpactDecision d, bool applied, bool knockDown, double now)
        {
            var h = d.ReelingHit;
            bool reached = h.Happens && applied && knockDown;
            _reelingHits.Note(in h, reached, d.DefenderIsPlayer, d.AttackerIsPlayer);
            if (reached && !_reelingFirstDown)
            {
                _reelingFirstDown = true;
                TraxLog.Info("impact", "first REELING man knocked down at " + Sec(now) + " s (step 105) - " + Name(a) + "'s " + ReelingHitMath.KindName(h.Kind) + " on " + Name(v)
                    + " (reeling from " + ReelingHitMath.CauseName(h.Cause) + ", " + Pct0(h.Chance) + (h.UsualStood ? " - the usual roll was higher" : string.Empty)
                    + "): KnockDown answered in the decorator's seam - his fall watched, our own blow if none shows");
            }
            else if (h.Happens && !reached && !_reelingFirstNotApplied)
            {
                _reelingFirstNotApplied = true;
                TraxLog.Info("impact", "first reeling-man knockdown NOT applied at " + Sec(now) + " s (step 105) - " + Name(a) + "'s " + ReelingHitMath.KindName(h.Kind) + " on " + Name(v)
                    + ": " + (!applied ? "its family not live (shadow / off / a rider)" : "decided outside the decorator's seam or the KnockDown lever off"));
            }
            else if (!_reelingFirstAsked)
            {
                _reelingFirstAsked = true;
                TraxLog.Info("impact", "first landed hit on a REELING man at " + Sec(now) + " s (step 105) - " + Name(a) + "'s " + ReelingHitMath.KindName(h.Kind) + " on " + Name(v)
                    + " (reeling from " + ReelingHitMath.CauseName(h.Cause) + "): " + (h.Refusal != ReelingHitRefusal.None ? "refused - " + ReelingHitMath.RefusalName(h.Refusal)
                       : h.Happens ? "knocked down" : "rolled " + Pct0(h.Chance) + ", stays up"));
            }
            else if (TraxLog.VerboseWants("impact"))
                TraxLog.Verbose("impact", "reeling hit: " + Name(a) + "'s " + ReelingHitMath.KindName(h.Kind) + " on " + Name(v) + " (from " + ReelingHitMath.CauseName(h.Cause) + "): "
                    + (h.Refusal != ReelingHitRefusal.None ? "refused - " + ReelingHitMath.RefusalName(h.Refusal) : (reached ? "DOWN" : h.Happens ? "down NOT applied" : "stays up") + " (" + Pct0(h.Chance) + ")"));
        }
    }
}
