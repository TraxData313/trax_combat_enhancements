using System;

namespace TraxCombat.Core
{
    /// <summary>Step 106c: what the red arrow at the crosshair is showing.</summary>
    public enum ArrowCue : byte
    {
        None,

        /// <summary>THE OPENING FLASH - he went off balance: the side to hit, peaking at the perfect moment to start your follow-up.</summary>
        Flash,

        /// <summary>THE PERFECT STRIKE SIDE - a man you can finish reels: the side(s) to finish him from, steady for the whole reel.</summary>
        Finish,

        /// <summary>Step 107: THE OPENING OF A HOLD - he holds a forced block: the side it leaves open, steady for the whole hold.</summary>
        Hold,
    }

    /// <summary>Step 106c: one frame of the red arrow - the sides lit (attack bits, <see cref="OpeningMath.Bit"/>) and how strongly. A struct: no
    /// allocation per frame.</summary>
    public struct ArrowReading
    {
        public ArrowCue Cue;

        /// <summary>The sides lit, as attack bits: AttackUp = the TOP arrow, AttackDown = the BOTTOM, AttackLeft = the LEFT, AttackRight = the RIGHT
        /// (vanilla's crosshair mapping - <see cref="OpeningMath.ArrowFor"/>).</summary>
        public int Mask;

        /// <summary>0-1.</summary>
        public float Alpha;

        public bool Shows => Cue != ArrowCue.None && Mask != 0 && Alpha > 0;
    }

    /// <summary>
    /// Step 111 (Toni's training field 2026-11-10, pic 4: "while I'm swinging from the top down, the indicator changes to from the left"): the red
    /// arrow NEVER TURNS while your swing is in the air. <see cref="Begin"/> at your melee wind-up notes what it shows; until the swing resolves
    /// (<see cref="End"/> at its contact / miss) or <c>OpeningArrowSwingHoldMaxSeconds</c> passes, a DIFFERENT direction is held back - the noted one
    /// stays (<see cref="Filter"/>); the arrow may still appear (none was shown) or go (its hold / window ended). The 106 flash is never held. A struct:
    /// no allocation.
    /// </summary>
    public struct ArrowLatch
    {
        public bool Active;
        public double SwingAt;
        public ArrowCue Cue;
        public int Mask;

        /// <summary>A change was held back during this swing (counted once a swing).</summary>
        public bool Held;

        public bool InSwing(double now, double max) => Active && max > 0 && now - SwingAt <= max + 1e-9;

        /// <summary>Your swing's wind-up at <paramref name="now"/>, the arrow showing <paramref name="live"/> (none = nothing noted yet).</summary>
        public void Begin(double now, in ArrowReading live)
        {
            Active = true;
            SwingAt = now;
            bool keep = live.Shows && live.Cue != ArrowCue.Flash;
            Cue = keep ? live.Cue : ArrowCue.None;
            Mask = keep ? live.Mask : 0;
            Held = false;
        }

        /// <summary>Your swing resolved (its contact, a miss) - the arrow follows the game again.</summary>
        public void End()
        {
            Active = false;
            Cue = ArrowCue.None;
            Mask = 0;
            Held = false;
        }

        /// <summary>The reading to show this frame for the <paramref name="live"/> one; <paramref name="heldNow"/> = a different direction was held back.</summary>
        public ArrowReading Filter(double now, double max, in ArrowReading live, out bool heldNow)
        {
            heldNow = false;
            if (!InSwing(now, max))
            {
                if (Active) End(); // the cap ran out (a feint, a held swing): the latch let go
                return live;
            }
            if (!live.Shows || live.Cue == ArrowCue.Flash) return live; // the arrow goes (its hold / window ended) - never a turn
            if (Mask == 0 || Cue == ArrowCue.None)
            {
                Cue = live.Cue; // none was shown: it may appear - and from now on it stays
                Mask = live.Mask;
                return live;
            }
            if (live.Mask == Mask) return live;
            heldNow = true;
            Held = true;
            return new ArrowReading { Cue = Cue, Mask = Mask, Alpha = 1f };
        }
    }

    /// <summary>
    /// Step 106c - THE RED ARROW, the pure part (AI_NOTES "106c - built", DESIGN decision 95 (k)). Drawn by the module's TraxOpeningArrow.xml at
    /// vanilla's direction-indicator places (Native Crosshair.xml: <c>crosshair_arrow_vertical</c> top / bottom - the bottom one vertically
    /// flipped -, <c>crosshair_arrow</c> left / right - the right one horizontally flipped -, 131 x 31 px, 350 px from the centre), in red. Two cues:
    ///   - THE OPENING FLASH: a linear fade in over <see cref="FadeInSeconds"/> to full AT the flash (the perfect moment to start your follow-up), out
    ///     over <see cref="FadeOutSeconds"/>, then gone - shown with <c>OpeningsEnabled</c> (or for the K test tool's forced guard);
    ///   - THE PERFECT STRIKE SIDE: steady while a man you can finish reels - an opening's reel (its chain's finishing sides) or a perfect-strike
    ///     window (the defensive game's timed-block reel - the strict opposite of his blocked attack, by your weapon: <see cref="DefensiveFinishMask"/>).
    /// Every side of a finishing mask is lit (an axe's left AND right after his blocked overhead; a spear's two stabs after a side swing).
    /// </summary>
    public static class OpeningArrowMath
    {
        /// <summary>The flash's fade in to its peak and out after it (s, mission time - the slow motion slows it too).</summary>
        public const double FadeInSeconds = 0.05, FadeOutSeconds = 0.15;

        /// <summary>Vanilla's arrow (Crosshair.xml constants): its long side, its short side, its distance from the screen's centre (UI px).</summary>
        public const int ArrowLength = 131, ArrowThickness = 31, ArrowOffset = 350;

        /// <summary>The flash's strength at <paramref name="now"/> for a flash peaking at <paramref name="peakAt"/>: 0 before the fade in, a line up to 1
        /// at the peak, a line down to 0 <see cref="FadeOutSeconds"/> after it; NaN = 0.</summary>
        public static float FlashAlpha(double now, double peakAt)
        {
            if (double.IsNaN(now) || double.IsNaN(peakAt)) return 0f;
            double dt = now - peakAt;
            if (dt < -FadeInSeconds || dt > FadeOutSeconds) return 0f;
            double a = dt <= 0 ? 1 + dt / FadeInSeconds : 1 - dt / FadeOutSeconds;
            return (float)Math.Max(0, Math.Min(1, a));
        }

        /// <summary>The flash is spent (its fade out over, or none).</summary>
        public static bool FlashOver(double now, double peakAt) => double.IsNaN(peakAt) || now > peakAt + FadeOutSeconds + 1e-9;

        /// <summary>The flash is shown: the master switch, <c>OpeningArrowShow</c>, and the live openings (<c>OpeningsEnabled</c>) - or the K test
        /// tool forcing a guard (<paramref name="testTool"/>), so the arrow can be seen while testing with the openings off. Step 106e:
        /// <paramref name="impactCarries"/> false (<see cref="OpeningMath.ImpactCarries"/> - the impact system in shadow, its riposte or hits off)
        /// = the openings are shadow only, no flash.</summary>
        public static bool ShowsFlash(in OpeningRules r, bool testTool, bool impactCarries = true) => r.ModEnabled && r.ArrowShow && ((r.Enabled && impactCarries) || testTool);

        /// <summary>The perfect-strike side is shown: the master switch and <c>OpeningArrowShow</c> (display only - the windows decide the rest).</summary>
        public static bool ShowsFinish(in OpeningRules r) => r.ModEnabled && r.ArrowShow;

        /// <summary>The K test tool's flash for a forced <paramref name="guard"/>: the side it leaves open (<see cref="OpeningMath.OpeningFor"/>); a
        /// lowered mode (no guard) = <see cref="OpeningMath.Unknown"/>, no flash.</summary>
        public static int TestFlashAttack(int guard, bool sidesCrossed) => OpeningMath.IsGuard(guard) ? OpeningMath.OpeningFor(guard, sidesCrossed) : OpeningMath.Unknown;

        /// <summary>
        /// THE DEFENSIVE GAME's finishing sides (step 100g's window): YOUR strict answer to his blocked attack (<see cref="PerfectStrikeMath.Opposite"/>
        /// - his overhead ↔ your thrust, his side swing ↔ the same value, your swing from the other side of your body), by your weapon
        /// (<see cref="OpeningMath.FinishMask"/> - 106a2's no-thrust and spear rules with <c>PerfectStrikeWeaponFinishes</c>). His direction unknown = 0.
        /// </summary>
        public static int DefensiveFinishMask(int blocked, int weaponMask, bool weaponFinishes) =>
            OpeningMath.FinishMask(PerfectStrikeMath.Opposite(blocked), weaponMask, weaponFinishes);

        /// <summary>A reeling RIDER's window (step 104d): any side counts with <c>PerfectStrikeHorsebackAnySide</c> - nothing to point at (0); else
        /// the defensive game's sides.</summary>
        public static int WindowFinishMask(bool rider, bool anySide, int blocked, int weaponMask, bool weaponFinishes) =>
            rider && anySide ? 0 : DefensiveFinishMask(blocked, weaponMask, weaponFinishes);

        /// <summary>The attack whose side <paramref name="arrow"/> is (the inverse of <see cref="OpeningMath.ArrowFor"/>).</summary>
        public static int AttackOf(OpeningArrow arrow) => arrow switch
        {
            OpeningArrow.Top => OpeningMath.AttackUp,
            OpeningArrow.Bottom => OpeningMath.AttackDown,
            OpeningArrow.Left => OpeningMath.AttackLeft,
            OpeningArrow.Right => OpeningMath.AttackRight,
            _ => OpeningMath.Unknown,
        };

        /// <summary><paramref name="arrow"/> is lit in <paramref name="mask"/>.</summary>
        public static bool Lit(int mask, OpeningArrow arrow) => (mask & OpeningMath.Bit(AttackOf(arrow))) != 0;

        /// <summary>The lit arrows in words ("top", "left + right") - log lines only (allocates).</summary>
        public static string ArrowsName(int mask)
        {
            string s = string.Empty;
            if (Lit(mask, OpeningArrow.Top)) s = "top";
            if (Lit(mask, OpeningArrow.Bottom)) s += (s.Length > 0 ? " + " : string.Empty) + "bottom";
            if (Lit(mask, OpeningArrow.Left)) s += (s.Length > 0 ? " + " : string.Empty) + "left";
            if (Lit(mask, OpeningArrow.Right)) s += (s.Length > 0 ? " + " : string.Empty) + "right";
            return s.Length > 0 ? s : "none";
        }
    }
}
