using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 42: one held man's LEASH (AI_NOTES "Step 42") - stepping out now or held, and whether he is back in his slot since his
    /// last step (only then may he step out again). A struct inside the module's per-man state: no allocation.
    /// </summary>
    public struct WallLeash
    {
        /// <summary>Stepping out now: vanilla's own value is on him (no "keep your slot" write), so the native AI may close on his
        /// enemy - up to the leash.</summary>
        public bool Free;

        /// <summary>Back in his slot (out ≤ <see cref="LeashMath.RearmAt"/>) since his last step: he may step out again.</summary>
        public bool Armed;

        /// <summary>A step ended away from his slot and he is walking back (counted "back in his slot" when he gets there).</summary>
        public bool Returning;

        /// <summary>The running step: its start (mission time), where he stood then and the farthest out at the looks (m out of
        /// his slot).</summary>
        public double StartedAt;
        public double StartOut;
        public double MaxOut;
    }

    /// <summary>Step 42: what one look did to a man's leash.</summary>
    public enum LeashMove
    {
        /// <summary>Nothing changed.</summary>
        None,

        /// <summary>A step out began: an enemy close in front, he stands less than the leash out of his slot.</summary>
        Freed,

        /// <summary>The step ended at the leash: he stood the leash or more out of his slot - held, he walks back.</summary>
        Reined,

        /// <summary>The step ended: no enemy close in front any more - held.</summary>
        EnemyGone,

        /// <summary>The step ended because the leash was set to 0 (the hard hold) mid-step.</summary>
        LeashOff,

        /// <summary>Back in his slot after a step that ended away from it: he may step out again.</summary>
        BackInSlot,
    }

    /// <summary>Step 42: how a step out ended (the summary's counts).</summary>
    public enum LeashEnd
    {
        /// <summary>He reached the leash: held again, walked back.</summary>
        ReachedLeash,

        /// <summary>No enemy close in front any more.</summary>
        EnemyGone,

        /// <summary>The leash set to 0 mid-step.</summary>
        LeashOff,

        /// <summary>Let go to vanilla by a gate (a charge, a retreat, mounted, the switch, the master switch, …).</summary>
        LetGo,

        /// <summary>He fell or left the field.</summary>
        LeftField,

        /// <summary>Still stepping out when the battle ended.</summary>
        StillOut,

        Count,
    }

    /// <summary>
    /// Step 42 - A SMALL STEP OUT OF THE WALL (Anton, 2026-10-17, his playtest of step 41: held men "looked many times like they
    /// were about to reach to hit but stopped, maybe as the enemy took a few steps back, so they very rarely hit" - "give them just
    /// a bit of a step if the enemy moves a small step back, but not as far"; AI_NOTES "Step 42"). A two-state leash with
    /// hysteresis per held man: FREE (vanilla's own value - the native AI may close on his enemy) while he stands less than the
    /// leash L out of his slot AND an enemy is close in front of it; HELD (step 41's "keep your slot") once he is L out, or no
    /// enemy is close; after a step he may step out again only once he is back in his slot. L = 0 is step 41's hard hold exactly.
    /// "Out" = along the formation's facing for a shield wall, away from the formation's centre for a square or circle (their
    /// sides and backs face outwards).
    /// </summary>
    public static class LeashMath
    {
        /// <summary>The setting's range (m) - <c>ShieldWallStepOutMetres</c>.</summary>
        public const double MaxMetres = 3.0;

        /// <summary>A one-handed strike's reach, body centre to body centre (m): his arm ~0.7 + a blade ~0.9 + the target's body
        /// ~0.35. An enemy is "close in front" within L + this of his SLOT - so a step of at most L brings him into reach; an enemy
        /// farther off would only draw him to the leash and back without a blow. Plumbing (Core constant, as Brace's PollSeconds).</summary>
        public const double StrikeReachMetres = 2.0;

        /// <summary>Hysteresis on the enemy's distance: a man stepping out stays free until his enemy is this much farther than the
        /// distance that freed him - an enemy hovering on the edge does not flip him every look. Plumbing.</summary>
        public const double EnemyHysteresisMetres = 0.5;

        /// <summary>"Back in his slot" after a step (m out of it, at most half the leash): step 41's held men stood 0.09 m out on
        /// average in contact, so 0.3 m is in his slot without waiting for a perfect stop. Plumbing.</summary>
        public const double BackInSlotMetres = 0.3;

        /// <summary>A man stepping out is looked at this often (s) - he walks 1-3 m/s, so the leash bites within 0.1-0.3 m of L
        /// (a 0.25 s look would let him 0.75 m past it). They are few: the held men keep step 41's cadence. Plumbing.</summary>
        public const double LookSeconds = 0.1;

        /// <summary>The overshoot mark the summary counts: more than L + this out of his slot (m). Log plumbing.</summary>
        public const double OverMetres = 0.5;

        /// <summary>A square's or circle's man whose slot is nearer than this to the formation's centre (m) steps out along the
        /// formation's facing instead (no outward direction to speak of).</summary>
        public const double MinRadialMetres = 0.5;

        /// <summary>His "out" direction (not normalized; zero = none): the facing for a shield wall (and anything else), the
        /// direction from the formation's centre to his slot for a square or a circle.</summary>
        public static void OutDirection(in WallFacts f, out double dx, out double dy)
        {
            dx = f.DirX;
            dy = f.DirY;
            if (f.Arrangement != WallArrangement.Square && f.Arrangement != WallArrangement.Circle) return;
            double rx = f.SlotX - f.CentreX, ry = f.SlotY - f.CentreY;
            if (rx * rx + ry * ry < MinRadialMetres * MinRadialMetres || double.IsNaN(rx) || double.IsNaN(ry)) return;
            dx = rx;
            dy = ry;
        }

        /// <summary>How far a point stands OUT of his slot along his out direction (m; negative = inside).</summary>
        public static double OutOf(in WallFacts f, double x, double y)
        {
            OutDirection(in f, out double dx, out double dy);
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9 || double.IsNaN(len)) return 0;
            return ((x - f.SlotX) * dx + (y - f.SlotY) * dy) / len;
        }

        /// <summary>How far HE stands out of his slot (m).</summary>
        public static double Out(in WallFacts f) => OutOf(in f, f.AgentX, f.AgentY);

        /// <summary>The enemy at (<paramref name="ex"/>, <paramref name="ey"/>) is close in front: out of his slot's line (in front
        /// of it) and within L + <see cref="StrikeReachMetres"/> of the slot (+ <see cref="EnemyHysteresisMetres"/> while he is
        /// already stepping out).</summary>
        public static bool EnemyCloseInFront(in WallFacts f, double ex, double ey, double leash, bool stepping)
        {
            if (leash <= 0 || !f.HasPositions || double.IsNaN(ex) || double.IsNaN(ey)) return false;
            if (OutOf(in f, ex, ey) <= 0) return false;
            double reach = leash + StrikeReachMetres + (stepping ? EnemyHysteresisMetres : 0);
            double dx = ex - f.SlotX, dy = ey - f.SlotY;
            return dx * dx + dy * dy <= reach * reach;
        }

        /// <summary>"Back in his slot": at most <see cref="BackInSlotMetres"/> out, and at most half the leash (so a short leash
        /// still has a gap between the two edges).</summary>
        public static double RearmAt(double leash) => Math.Min(BackInSlotMetres, leash / 2);

        /// <summary>Whether this look needs the enemy read (a native read in game): only a man who could be freed now or is
        /// stepping out and still inside the leash (a man first seen in his slot is armed by this very look).</summary>
        public static bool NeedsEnemy(in WallLeash s, double leash, double outM) =>
            leash > 0 && outM < leash && (s.Free || s.Armed || (!s.Returning && outM <= RearmAt(leash)));

        /// <summary>
        /// One look: the leash's state machine. L ≤ 0 = the hard hold (a man stepping out is held at once). Stepping out: held at L
        /// or more out (<see cref="LeashMove.Reined"/>), or when no enemy is close (<see cref="LeashMove.EnemyGone"/>). Held: back in
        /// his slot = armed again; armed + an enemy close in front + less than L out = <see cref="LeashMove.Freed"/>.
        /// </summary>
        public static LeashMove Step(ref WallLeash s, double leash, double outM, bool enemyClose, double now)
        {
            if (double.IsNaN(outM)) outM = 0;
            if (leash <= 0)
            {
                bool was = s.Free;
                s = default;
                return was ? LeashMove.LeashOff : LeashMove.None;
            }
            double rearm = RearmAt(leash);
            if (s.Free)
            {
                if (outM > s.MaxOut) s.MaxOut = outM;
                if (outM >= leash)
                {
                    s.Free = false;
                    s.Armed = false;
                    s.Returning = true;
                    return LeashMove.Reined;
                }
                if (!enemyClose)
                {
                    s.Free = false;
                    s.Armed = outM <= rearm;
                    s.Returning = !s.Armed;
                    return LeashMove.EnemyGone;
                }
                return LeashMove.None;
            }
            if (!s.Armed)
            {
                if (outM > rearm) return LeashMove.None;
                s.Armed = true;
                if (s.Returning)
                {
                    s.Returning = false;
                    return LeashMove.BackInSlot; // the next look may free him
                }
            }
            if (!enemyClose || outM >= leash) return LeashMove.None;
            s.Free = true;
            s.Armed = false;
            s.Returning = false;
            s.StartedAt = now;
            s.StartOut = outM;
            s.MaxOut = outM;
            return LeashMove.Freed;
        }

        public static string Name(LeashEnd e) => e switch
        {
            LeashEnd.ReachedLeash => "reached the leash",
            LeashEnd.EnemyGone => "enemy gone",
            LeashEnd.LeashOff => "leash set to 0",
            LeashEnd.LetGo => "let go to vanilla",
            LeashEnd.LeftField => "fell or left",
            LeashEnd.StillOut => "still out at the end",
            _ => e.ToString(),
        };

        /// <summary>The leash in words (the settings sentence).</summary>
        public static string Describe(double leash) => leash <= 0
            ? "no step out (ShieldWallStepOutMetres 0 - the hard hold of step 41)"
            : "a held man may step up to " + F1(leash) + " m out of his slot at an enemy close in front of it (within " + F1(leash) + " + "
              + F1(StrikeReachMetres) + " m of the slot), then is held again and walks back - he may step again once back within "
              + F2(RearmAt(leash)) + " m of his slot (ShieldWallStepOutMetres)";

        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 42 per-mission numbers and the [summary] "wall step out" line: the steps and the men who stepped, a man's steps a
    /// minute while held, how far out (the farthest of each step, at the looks), how each ended, the overshoot past the leash of
    /// those reined, back in the slot after a step, the share of held time spent out, and the in-contact samples past L and past
    /// L + <see cref="LeashMath.OverMetres"/>. Main thread.
    /// </summary>
    public sealed class WallLeashStats
    {
        private readonly int[] _ends = new int[(int)LeashEnd.Count];

        public int Steps;
        public int MenStepped;
        public int BackInSlot;
        public double MaxOutSum;
        public double MaxOutMax;
        public int Ended;
        public double OvershootSum;
        public double OvershootMax;
        public double SecondsOut;
        public double HeldSeconds;
        public int LeashSamples;
        public int OverLeash;
        public int OverLeashFar;
        public bool EverOn;

        public int Ends(LeashEnd e) => _ends[(int)e];

        public void AddStart() => Steps++;

        /// <summary>A step ended: how, its farthest out, how long, and (reined) the leash it was held at.</summary>
        public void AddEnd(LeashEnd how, double maxOut, double seconds, double leash)
        {
            _ends[(int)how]++;
            Ended++;
            double m = maxOut > 0 && !double.IsNaN(maxOut) ? maxOut : 0;
            MaxOutSum += m;
            if (m > MaxOutMax) MaxOutMax = m;
            if (seconds > 0 && !double.IsNaN(seconds)) SecondsOut += seconds;
            if (how != LeashEnd.ReachedLeash || leash <= 0) return;
            double over = m - leash;
            if (over < 0) over = 0;
            OvershootSum += over;
            if (over > OvershootMax) OvershootMax = over;
        }

        /// <summary>An in-contact sample while the leash is on (L &gt; 0): his out of his slot against L.</summary>
        public void AddSample(double outM, double leash)
        {
            if (leash <= 0) return;
            LeashSamples++;
            if (outM > leash) OverLeash++;
            if (outM > leash + LeashMath.OverMetres) OverLeashFar++;
        }

        public double StepsPerManMinute => HeldSeconds > 0 ? Steps / (HeldSeconds / 60) : 0;

        /// <summary>The [summary] line.</summary>
        public string SummaryLine(double leash)
        {
            string head = "wall step out (step 42): leash " + LeashMath.F1(leash) + " m now";
            if (!EverOn) return head + " - the leash was 0 (the hard hold of step 41) the whole battle: no steps";
            if (Steps == 0)
                return head + " - no man stepped out (no enemy came within the leash + " + LeashMath.F1(LeashMath.StrikeReachMetres) + " m in front of a held man's slot); "
                       + LeashText();
            int reined = Ends(LeashEnd.ReachedLeash);
            return head + " - " + Steps + " steps out by " + MenStepped + " men (" + LeashMath.F2(StepsPerManMinute) + " a man a minute while held"
                   + (HeldSeconds > 0 ? ", out of their slots " + Pct(SecondsOut / HeldSeconds) + " of the held time" : string.Empty) + "); each step's farthest out: mean "
                   + (Ended > 0 ? LeashMath.F2(MaxOutSum / Ended) : "-") + " m, the farthest " + LeashMath.F2(MaxOutMax) + " m; ended: " + EndsText()
                   + (reined > 0 ? "; reined at the leash " + LeashMath.F2(OvershootSum / reined) + " m past it on average, at most " + LeashMath.F2(OvershootMax) + " m (the "
                                   + LeashMath.F1(LeashMath.LookSeconds) + " s look)" : string.Empty)
                   + "; back in their slots after a step " + BackInSlot + "; " + LeashText();
        }

        private string LeashText() =>
            LeashSamples == 0
                ? "no in-contact sample while the leash was on"
                : "in contact past the leash " + Pct((double)OverLeash / LeashSamples) + ", past it + " + LeashMath.F1(LeashMath.OverMetres) + " m "
                  + Pct((double)OverLeashFar / LeashSamples) + " (of " + LeashSamples + " in-contact samples with the leash on - the overshoot: should be near 0)";

        private string EndsText()
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < _ends.Length; i++)
                if (_ends[i] > 0) parts.Add(LeashMath.Name((LeashEnd)i) + " " + _ends[i]);
            return parts.Count == 0 ? "none yet" : string.Join(", ", parts);
        }

        private static string Pct(double v) => (v * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }
}
