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

        /// <summary>The reading to show this frame for the <paramref name="live"/> one; <paramref name="heldNow"/> = a different direction (or, step 112,
        /// a different role on the same side - its colour) was held back.</summary>
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
            if (live.Mask == Mask && live.Cue == Cue) return live;
            heldNow = true; // step 112: a ROLE change on the same side (the hold's red opening -> the gold perfect-strike side) is held back too - the colour never changes mid-swing
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

    /// <summary>Step 112 (Toni 2026-11-10: "Make the PERFECT STRIKE [arrow] gold"): what the arrow at the crosshair is FOR - its colour follows it.</summary>
    public enum ArrowRole : byte
    {
        /// <summary>THE OPENING - the side to hit: step 107's hold (he holds an unbalanced block) and step 106's flash. Red by default.</summary>
        Opening,

        /// <summary>THE PERFECT STRIKE SIDE - he staggers (an opening's reel, a timed block's riposte, a rider's reel): the side to finish him from.
        /// Gold by default, like the "PERFECT STRIKE x2.8" line.</summary>
        PerfectStrike,
    }

    /// <summary>
    /// Step 112 - THE ARROW'S COLOUR BY ITS ROLE, the pure part (AI_NOTES "Step 112", DESIGN decision 101). The role is the cue's
    /// (<see cref="RoleOf"/>: <see cref="ArrowCue.Finish"/> = the perfect strike, a hold / a flash = the opening); the cue the HUD draws is the one
    /// after step 111's <see cref="ArrowLatch"/>, which now holds a role change on the same side too, so the colour changes only with the role and
    /// never while your swing is in the air. The colours are settings (<c>OpeningArrowColor</c> / <c>PerfectStrikeArrowColor</c>, hex RRGGBB or
    /// RRGGBBAA, '#' optional); a text that is not one falls back to the setting's default. No allocation (<see cref="TryParse"/> walks the characters).
    /// </summary>
    public static class ArrowColours
    {
        /// <summary>The role of a cue: the perfect-strike side for <see cref="ArrowCue.Finish"/>, the opening for everything else.</summary>
        public static ArrowRole RoleOf(ArrowCue cue) => cue == ArrowCue.Finish ? ArrowRole.PerfectStrike : ArrowRole.Opening;

        /// <summary>"opening" / "PERFECT STRIKE" - log lines.</summary>
        public static string RoleName(ArrowRole role) => role == ArrowRole.PerfectStrike ? "PERFECT STRIKE" : "opening";

        /// <summary>The setting that colours <paramref name="role"/>.</summary>
        public static ParamDef SettingFor(ArrowRole role) => role == ArrowRole.PerfectStrike ? SettingsSchema.PerfectStrikeArrowColor : SettingsSchema.OpeningArrowColor;

        /// <summary>The colour text set for <paramref name="role"/> (as typed - <see cref="TryParse"/> reads it).</summary>
        public static string TextFor(ArrowRole role, TraxSettings s) => role == ArrowRole.PerfectStrike ? s.PerfectStrikeArrowColor : s.OpeningArrowColor;

        /// <summary>The default colour text of <paramref name="role"/> (defaults.json) - the fallback for a text that is not a colour.</summary>
        public static string DefaultTextFor(ArrowRole role)
        {
            var p = SettingFor(role);
            return p.Format(p.Default);
        }

        /// <summary>
        /// The colour of <paramref name="role"/> as 0-1 channels: the set text, else (not a colour) the default's, else opaque white.
        /// <paramref name="usedDefault"/> = the set text was not a colour.
        /// </summary>
        public static void Resolve(ArrowRole role, TraxSettings s, out float r, out float g, out float b, out float a, out bool usedDefault)
        {
            usedDefault = !TryParse(TextFor(role, s), out r, out g, out b, out a);
            if (usedDefault && !TryParse(DefaultTextFor(role), out r, out g, out b, out a))
                r = g = b = a = 1f;
        }

        /// <summary>
        /// A hex colour: RRGGBB (opaque) or RRGGBBAA, an optional leading '#', spaces around ignored, either case. False (white, opaque) for anything
        /// else.
        /// </summary>
        public static bool TryParse(string? text, out float r, out float g, out float b, out float a)
        {
            r = g = b = a = 1f;
            if (text == null) return false;
            int start = 0, end = text.Length;
            while (start < end && char.IsWhiteSpace(text[start])) start++;
            while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
            if (start < end && text[start] == '#') start++;
            int n = end - start;
            if (n != 6 && n != 8) return false;
            uint v = 0;
            for (int i = start; i < end; i++)
            {
                int d = HexDigit(text[i]);
                if (d < 0) return false;
                v = (v << 4) | (uint)d;
            }
            if (n == 6) v = (v << 8) | 0xFF;
            r = ((v >> 24) & 0xFF) / 255f;
            g = ((v >> 16) & 0xFF) / 255f;
            b = ((v >> 8) & 0xFF) / 255f;
            a = (v & 0xFF) / 255f;
            return true;
        }

        private static int HexDigit(char c) =>
            c >= '0' && c <= '9' ? c - '0'
            : c >= 'a' && c <= 'f' ? c - 'a' + 10
            : c >= 'A' && c <= 'F' ? c - 'A' + 10
            : -1;
    }
}
