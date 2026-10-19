using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 64, THE WALL'S BLOCKS COUNT (AI_NOTES "Step 64", Core <see cref="ShieldArcMath"/>): one block kind per melee collision for
    /// every system of ours - the block's cost and the refill rule (steps 22 / 29), the guard lines (steps 30 / 34) and the impact model's
    /// event (a right-side block can riposte / throw a charger back): the engine's <c>CorrectSideShieldBlock</c>, turned RIGHT for a
    /// wrong-side SHIELD block whose attacker stands within <c>ShieldFrontArcDegrees</c> of the blocker's facing
    /// (<see cref="IImpactInputs.FacingAngle"/> - step 62's angle read). Both men must be tracked (an untracked attacker - a horse, a
    /// stand-in - keeps the engine's verdict, and no native read is made for him). The impact spike (a test tool) keeps the engine's. Both
    /// verdicts counted once per collision (OnMeleeHit) → the [summary] "shield front arc" line.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly ShieldArcStats _shieldArc = new ShieldArcStats();
        private ShieldArcRules _seenShieldArc;
        private bool _shieldArcFirstLogged;

        /// <summary>For the smoke: this mission's step-64 counters.</summary>
        internal ShieldArcStats ShieldArcStatsForSmoke => _shieldArc;

        /// <summary>OUR block kind of one melee collision (<see cref="BlockKind.None"/> = not a block) - see the class.</summary>
        internal BlockKind BlockKindOf(Agent? attacker, Agent? victim, in AttackCollisionData cd) => ResolveBlock(attacker, victim, in cd, out _, out _);

        /// <summary>The engine's kind and ours; <paramref name="angle"/> = the attacker's angle off the blocker's facing when it was read, else NaN.</summary>
        private BlockKind ResolveBlock(Agent? attacker, Agent? victim, in AttackCollisionData cd, out BlockKind engine, out double angle)
        {
            angle = double.NaN;
            engine = BlockMath.KindOf(cd.IsMissile, cd.IsAlternativeAttack, cd.IsHorseCharge, cd.CollidedWithShieldOnBack,
                cd.AttackBlockedWithShield, cd.CorrectSideShieldBlock, (int)cd.CollisionResult);
            var r = ShieldArcRules.From(TraxSettings.Shared);
            if (!ShieldArcMath.NeedsAngle(in r, engine) || attacker == null || victim == null) return engine;
            if (Get(attacker) == null || Get(victim) == null) return engine; // a horse, a stand-in: no native read, the game's verdict
            try
            {
                angle = ImpactInputs.FacingAngle(victim, attacker);
            }
            catch (Exception e)
            {
                Failed("shieldarc.angle", e);
                angle = double.NaN;
            }
            return ShieldArcMath.Resolve(in r, engine, angle);
        }

        /// <summary>OnMeleeHit (first, its own try): both verdicts of a SHIELD block on a tracked man, counted once per collision; the
        /// mission's first block the arc turned right in full, the rest verbose.</summary>
        internal void ShieldArcHitTaken(Agent? attacker, Agent? victim, in AttackCollisionData cd)
        {
            if (victim == null || !cd.AttackBlockedWithShield || cd.IsMissile) return;
            var ours = ResolveBlock(attacker, victim, in cd, out var engine, out double angle);
            if (engine != BlockKind.ShieldRightSide && engine != BlockKind.ShieldWrongSide) return;
            var st = Get(victim);
            if (st == null) return;
            bool you = IsPlayer(st);
            _shieldArc.Note(you, engine, ours, angle);
            if (engine == ours) return;
            if (!_shieldArcFirstLogged)
            {
                _shieldArcFirstLogged = true;
                TraxLog.Info("guard", "first shield block from the front counted RIGHT this battle (step 64): " + (you ? "YOUR" : Name(st) + "'s") + " shield block of "
                    + (attacker != null && Get(attacker) is { } a ? Name(a) : "a blow") + " at " + Sec(SafeNow()) + " s - the attacker " + F0(angle)
                    + "° off his facing (within ±" + F0(_seenShieldArc.Degrees) + "°): the game said WRONG side, the mod counts a right-side block (its cost, the refill"
                    + " running on, the impact model's correct block - the riposte, a charger thrown back)");
            }
            else if (TraxLog.VerboseWants("guard-arc"))
            {
                TraxLog.Verbose("guard", (you ? "YOUR" : Name(st) + "'s") + " shield block at " + F0(angle) + "° off the facing: the game WRONG side → right (front arc)", "guard-arc");
            }
        }

        /// <summary>Mission start (from StartTiredBlock): the rule's sentence.</summary>
        private void StartShieldArc()
        {
            _seenShieldArc = ShieldArcRules.From(TraxSettings.Shared);
            TraxLog.Info("guard", "mission start: " + _seenShieldArc.Describe() + " - read live; the [summary] \"shield front arc\" line counts the game's verdict"
                + " against ours");
        }

        /// <summary>A setting changed (from NoteTiredBlockSettings): one line when the rule moved.</summary>
        private void NoteShieldArcSettings(TraxSettings s)
        {
            var now = ShieldArcRules.From(s);
            if (now.Enabled == _seenShieldArc.Enabled && Math.Abs(now.Degrees - _seenShieldArc.Degrees) < 1e-9) return;
            _seenShieldArc = now;
            TraxLog.Info("guard", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now.Describe() + " - from the next block");
        }

        /// <summary>The summary's line (from WriteTiredBlockSummary).</summary>
        internal void WriteShieldArcSummary() => TraxLog.Info("summary", _shieldArc.SummaryLine(ShieldArcRules.From(TraxSettings.Shared)));
    }
}
