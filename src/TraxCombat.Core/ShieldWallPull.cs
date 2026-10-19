using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 63: one held man's time OUT OF PLACE - standing beyond the step-out leash ahead of his slot while he is held (thrown forward by a
    /// blow, pushed by the press, reined late). A struct in the module's per-man wall state: no allocation.
    /// </summary>
    public struct WallOutOfPlace
    {
        /// <summary>Out of place now (since <see cref="Since"/>, mission time).</summary>
        public bool Out;
        public double Since;

        /// <summary>It began within <see cref="WallPullMath.ImpactWindowSeconds"/> of an impact outcome on him (down, reeling, or one in his
        /// record) - the step-63 question.</summary>
        public bool AfterImpact;

        /// <summary>Step 68: it began within <see cref="WallPullMath.KickWindowSeconds"/> of his OWN kick / bash or while he pressed a punish window
        /// (step 54) - walked back at once, from <c>ShieldWallKickReturnMetres</c> out.</summary>
        public bool AfterOwnKick;

        /// <summary>The farthest out (m) at the looks.</summary>
        public double MaxOut;

        /// <summary>Walked back by our wish now (since <see cref="PullSince"/>); <see cref="Pulled"/> = at any time in this spell.</summary>
        public bool Pulling;
        public double PullSince;
        public bool Pulled;

        /// <summary>Seconds of pulling spent in this spell (the cap <see cref="WallPullMath.MaxPullSeconds"/>).</summary>
        public double PullSeconds;

        /// <summary>Able to move (not down, not reeling) since (NaN = not now) - the wait before our walk counts from here.</summary>
        public double MovableSince;
    }

    /// <summary>Step 63: how a spell out of place ended (the summary's counts).</summary>
    public enum WallOutEnd
    {
        /// <summary>Back within half the leash (at least 0.5 m) of his slot.</summary>
        Back,

        /// <summary>Freed on the leash again, let go to vanilla by a gate, the switch / master switch.</summary>
        LetGo,

        /// <summary>He fell or left the field.</summary>
        LeftField,

        /// <summary>Still out when the battle ended.</summary>
        StillOut,

        Count,
    }

    /// <summary>What one look did to a man's spell out of place.</summary>
    public enum WallOutMove
    {
        None,

        /// <summary>A spell began: held, and more than <see cref="WallPullMath.Beyond"/> ahead.</summary>
        Began,

        /// <summary>Back (within <see cref="WallPullMath.BackAt"/>).</summary>
        Back,

        /// <summary>Our walk back began (the spell lasted <c>ShieldWallPullBackSeconds</c>, he can move).</summary>
        PullStarted,

        /// <summary>Our walk back paused - he went down / reels again, or it ran its <see cref="WallPullMath.MaxPullSeconds"/>.</summary>
        PullStopped,
    }

    /// <summary>
    /// Step 63 - THE WALL PULLS MEN BACK (Anton's battle of 2026-10-19 11:02: wall men in contact more than 1 m ahead of their slots 11.1%,
    /// step 42's battles 0.6-2.5%; AI_NOTES "Step 63"). A held man (the "keep your slot" flag on him, not stepping out on the leash) who
    /// stands more than <see cref="Beyond"/> ahead of his slot is OUT OF PLACE: looked at every <see cref="LeashMath.LookSeconds"/> (the flag
    /// written at each look - held again at once, not at vanilla's next ~0.5 s update), and once the spell has lasted
    /// <c>ShieldWallPullBackSeconds</c> while he can move (not down, not reeling from an impact) he is WALKED BACK - a backpedal toward his slot,
    /// facing the enemy, through step 16's input hook (the courage walk's channel) - until he is back within <see cref="BackAt"/>.
    /// </summary>
    public static class WallPullMath
    {
        /// <summary>The setting's range (s) - <c>ShieldWallPullBackSeconds</c>.</summary>
        public const double MaxSeconds = 3.0;

        /// <summary>Out of place = more than max(L, this) + <see cref="BeyondMarginMetres"/> ahead (with the leash at 0, step 41's
        /// "more than 1 m ahead" mark). Plumbing.</summary>
        public const double MinLeashMetres = 1.0;

        /// <summary>A man the leash reins stands a few centimetres past L (step 42: 0.04 m on average, at most 0.27) - not out of place.
        /// Plumbing.</summary>
        public const double BeyondMarginMetres = 0.25;

        /// <summary>An impact outcome on him within this many seconds before the spell began = "after an impact" (a fall lasts ~3 s).
        /// Plumbing.</summary>
        public const double ImpactWindowSeconds = 3.0;

        /// <summary>Step 68: his own kick / bash (its start - step 18's tracker) at most this long ago makes a spell "after his own kick" (a kick /
        /// bash plays ~1-1.5 s). Plumbing.</summary>
        public const double KickWindowSeconds = 2.5;

        /// <summary>Step 68: the setting's range (m) - <c>ShieldWallKickReturnMetres</c>.</summary>
        public const double MaxKickReturnMetres = 3.0;

        /// <summary>One spell's walk back stops after this many seconds of pulling (a man who cannot get back - a wall of bodies - is left
        /// to the game's flag). Plumbing.</summary>
        public const double MaxPullSeconds = 4.0;

        /// <summary>The leash the measure uses: L, never below <see cref="MinLeashMetres"/>.</summary>
        public static double LeashOf(double leash) => double.IsNaN(leash) || leash < MinLeashMetres ? MinLeashMetres : leash;

        /// <summary>Out of place beyond this (m ahead of his slot).</summary>
        public static double Beyond(double leash) => LeashOf(leash) + BeyondMarginMetres;

        /// <summary>Back within this (m) - half the leash (0.5 m at L 1).</summary>
        public static double BackAt(double leash) => LeashOf(leash) / 2;

        /// <summary>
        /// One look at a HELD man (not stepping out): <paramref name="outM"/> m out of his slot, <paramref name="canMove"/> = not down and not
        /// reeling, <paramref name="impact"/> = an impact outcome on him lately, <paramref name="pullSeconds"/> = the setting (0 = never pull).
        /// <paramref name="dt"/> = seconds since the last look (the pull's budget). Returns what changed.
        /// </summary>
        public static WallOutMove Step(ref WallOutOfPlace s, double leash, double outM, bool canMove, bool impact, double pullSeconds, double now, double dt) =>
            Step(ref s, leash, outM, canMove, impact, pullSeconds, now, dt, 0, false);

        /// <summary>
        /// Step 68: the same with THE RETURN AFTER HIS OWN KICK: <paramref name="ownKick"/> = his own kick / bash began within
        /// <see cref="KickWindowSeconds"/> (or he presses a punish window - step 54's step-in); <paramref name="kickReturnMetres"/> =
        /// <c>ShieldWallKickReturnMetres</c> (0 = off). Such a man is out of place from that many metres ahead (not the leash's 1.25), back within
        /// half of it, and walked back AT ONCE once he can move (no <c>ShieldWallPullBackSeconds</c> wait - it works with that setting at 0 too).
        /// </summary>
        public static WallOutMove Step(ref WallOutOfPlace s, double leash, double outM, bool canMove, bool impact, double pullSeconds, double now, double dt,
            double kickReturnMetres, bool ownKick)
        {
            if (double.IsNaN(outM)) return WallOutMove.None;
            bool kickOn = kickReturnMetres > 0;
            if (!s.Out)
            {
                bool kick = kickOn && ownKick && outM > kickReturnMetres;
                if (!kick && !(outM > Beyond(leash))) return WallOutMove.None;
                s = new WallOutOfPlace { Out = true, Since = now, AfterImpact = impact, AfterOwnKick = kickOn && ownKick, MaxOut = outM, MovableSince = canMove ? now : double.NaN };
                return WallOutMove.Began;
            }
            if (outM > s.MaxOut) s.MaxOut = outM;
            if (!canMove) s.MovableSince = double.NaN;
            else if (double.IsNaN(s.MovableSince)) s.MovableSince = now;
            if (s.Pulling && dt > 0 && !double.IsNaN(dt)) s.PullSeconds += dt;
            bool kickSpell = s.AfterOwnKick && kickOn;
            if (outM <= (kickSpell ? Math.Min(BackAt(leash), kickReturnMetres / 2) : BackAt(leash)))
            {
                s.Out = false;
                s.Pulling = false;
                return WallOutMove.Back;
            }
            bool want = (kickSpell || pullSeconds > 0) && canMove && now - s.MovableSince >= (kickSpell ? 0 : pullSeconds) && s.PullSeconds < MaxPullSeconds;
            if (want && !s.Pulling)
            {
                s.Pulling = true;
                s.Pulled = true;
                s.PullSince = now;
                return WallOutMove.PullStarted;
            }
            if (!want && s.Pulling)
            {
                s.Pulling = false;
                return WallOutMove.PullStopped;
            }
            return WallOutMove.None;
        }

        public static string Name(WallOutEnd e) => e switch
        {
            WallOutEnd.Back => "back in place",
            WallOutEnd.LetGo => "freed / let go",
            WallOutEnd.LeftField => "fell or left",
            WallOutEnd.StillOut => "still out at the end",
            _ => e.ToString(),
        };

        /// <summary>The setting in words (the mission-start sentence).</summary>
        public static string Describe(double pullSeconds, double leash) =>
            "a held man more than " + LeashMath.F2(Beyond(leash)) + " m ahead of his slot (beyond the leash - thrown forward by a blow, pushed by the press) is looked at every "
            + LeashMath.F1(LeashMath.LookSeconds) + " s and held again at each look"
            + (pullSeconds > 0
                ? ", and walked back to within " + LeashMath.F2(BackAt(leash)) + " m of his slot (backing off, facing the enemy) once he has stood out " + LeashMath.F1(pullSeconds)
                  + " s able to move (ShieldWallPullBackSeconds)"
                : " - never walked back by us (ShieldWallPullBackSeconds 0: the game's own pull only; the extra looks off too)");
    }

    /// <summary>
    /// Step 63: one mission's spells out of place - how many began (after an impact / other), how they ended, the time back (mean / max, by
    /// cause, pulled or by the game's flag alone), the farthest, the walks back - and the [summary] "wall pulled back" line.
    /// </summary>
    public sealed class WallPullStats
    {
        private readonly int[] _ends = new int[(int)WallOutEnd.Count];

        public int Spells, SpellsAfterImpact;

        /// <summary>Step 68: spells after his own kick / bash or a punish press, and their time back.</summary>
        public int SpellsAfterKick;
        public MeanStd BackAfterKick;
        public int Pulls, PullFrames;
        public MeanStd BackAfterImpact, BackOther, BackPulled, BackUnpulled;
        public double BackMax;
        public double MaxOut;

        public int Ends(WallOutEnd e) => _ends[(int)e];

        public void Began(bool afterImpact) => Began(afterImpact, false);

        /// <summary>Step 68: a spell began - after an impact on him, after his own kick / bash (a punish press), or other.</summary>
        public void Began(bool afterImpact, bool afterOwnKick)
        {
            Spells++;
            if (afterImpact) SpellsAfterImpact++;
            if (afterOwnKick) SpellsAfterKick++;
        }

        /// <summary>A spell ended - how, and (back) after how long.</summary>
        public void Ended(in WallOutOfPlace s, WallOutEnd how, double now)
        {
            _ends[(int)how]++;
            if (s.MaxOut > MaxOut) MaxOut = s.MaxOut;
            if (how != WallOutEnd.Back) return;
            double secs = now - s.Since;
            if (double.IsNaN(secs) || secs < 0) return;
            if (s.AfterImpact) BackAfterImpact.Add(secs); // MeanStd is a struct: never through a ternary (a copy)
            else BackOther.Add(secs);
            if (s.AfterOwnKick) BackAfterKick.Add(secs); // step 68
            if (s.Pulled) BackPulled.Add(secs);
            else BackUnpulled.Add(secs);
            if (secs > BackMax) BackMax = secs;
        }

        /// <summary>The [summary] line.</summary>
        public string SummaryLine(double pullSeconds, double leash)
        {
            var c = CultureInfo.InvariantCulture;
            string head = "wall pulled back (step 63 - a held man more than " + LeashMath.F2(WallPullMath.Beyond(leash)) + " m ahead of his slot; ShieldWallPullBackSeconds "
                          + (pullSeconds > 0 ? pullSeconds.ToString("0.0#", c) + " s" : "0 - the game's pull only") + "): ";
            if (Spells == 0) return head + "no held man stood out of place this battle";
            return head + Spells + " spells out of place (after an impact " + SpellsAfterImpact + ", other " + (Spells - SpellsAfterImpact) + "), the farthest "
                   + LeashMath.F2(MaxOut) + " m; ended: back " + Ends(WallOutEnd.Back) + ", freed / let go " + Ends(WallOutEnd.LetGo) + ", fell or left " + Ends(WallOutEnd.LeftField)
                   + ", still out at the end " + Ends(WallOutEnd.StillOut) + "; THE TIME BACK - after an impact " + Secs(in BackAfterImpact) + ", other " + Secs(in BackOther)
                   + ", the longest " + BackMax.ToString("0.00", c) + " s; walked back by us " + Pulls + " times (" + PullFrames + " input frames) - back with our walk "
                   + Secs(in BackPulled) + ", by the game's flag alone " + Secs(in BackUnpulled)
                   + "; step 68 - after his OWN kick / bash or a punish press " + SpellsAfterKick + " (back in " + Secs(in BackAfterKick) + ")";
        }

        private static string Secs(in MeanStd m) =>
            m.Count > 0 ? m.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s (n " + m.Count + ")" : "- (n 0)";
    }
}
