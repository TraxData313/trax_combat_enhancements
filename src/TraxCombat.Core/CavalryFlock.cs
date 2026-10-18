using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 55: what a rider fights with - read once from his kit; each kind flocks with its own kind of his formation.</summary>
    public enum RiderKind
    {
        /// <summary>Lance / sword / mace, no ranged weapon with ammo.</summary>
        Melee = 0,

        /// <summary>A bow, crossbow or sling with ammo.</summary>
        HorseArcher = 1,

        /// <summary>Throwing weapons (javelins, throwing axes / knives) with some left, no bow.</summary>
        Skirmisher = 2,
    }

    /// <summary>Step 55: why a rider is not part of the flocking (in the order asked). The logic adds the player; a man on foot is not counted.</summary>
    public enum FlockSkip
    {
        NotAi,
        NoFormation,

        /// <summary>Detached, on a machine, in a ladder queue, running away, or scripted by somebody else (the game's own scripted walk).</summary>
        Busy,

        /// <summary>His formation's order is not a charge (charge / charge to target) - a hold, move, follow or advance keeps riders in their slots.</summary>
        NotCharging,

        Count,
    }

    /// <summary>Step 55: a rider's state.</summary>
    public enum RiderFlockState
    {
        /// <summary>Eager (eagerness at or above CavalryFlockFreeEagerPercent): the game's own rider - the vanilla dance, untouched.</summary>
        Free,

        /// <summary>Below the free line: he rides with his kind's flock until it is big enough for his tiredness and its most rested rider leads it in.</summary>
        Flocking,

        /// <summary>Released by his flock's FLIP: charging with it for CavalryFlockWaveSeconds - the game's own rider, no new flock meanwhile.</summary>
        Wave,
    }

    /// <summary>Step 55: why a rider started flocking.</summary>
    public enum FlockReason
    {
        /// <summary>His Athletics bar put his eagerness below the free line.</summary>
        Tired,

        /// <summary>Courage's odds (step 49's toughness sums) within CavalryFlockTimidRadiusMetres halve his eagerness - a lone rider among many enemies.</summary>
        Timid,

        Count,
    }

    /// <summary>Step 55: how a flocking spell ended.</summary>
    public enum FlockEnd
    {
        /// <summary>His flock FLIPPED: every flocking rider of it released at once toward the goal - the wave.</summary>
        Wave,

        /// <summary>Rested above the free line (+ the hysteresis) - free again, the dance.</summary>
        Rested,

        /// <summary>CavalryFlockMaxSeconds passed without a flip - released on his own (his wave window still runs).</summary>
        GaveUp,

        SwitchedOff,

        /// <summary>No longer eligible (dismounted, his order no longer a charge, taken over by the player or a game job, his formation gone).</summary>
        Excluded,

        LeftField,
        Error,
        MissionEnd,
        Count,
    }

    /// <summary>
    /// Step 55 - CAVALRY FLOCKING, the settings read live (Anton, 2026-10-18: fresh, eager riders dance as in vanilla; the more tired a rider, the
    /// bigger the group of his own kind he wants before he attacks again; the flock is always on the move, pulled toward the unit's centre by its
    /// tired riders and toward the goal by its rested ones, and FLIPS into a charge as a flock - AI_NOTES "Step 55").
    /// </summary>
    public readonly struct FlockRules
    {
        public FlockRules(bool modEnabled, bool flockEnabled, bool athleticsEnabled, bool courageEnabled, int freeEagerPercent, int hysteresisPercent,
            int midEagerPercent, int midGroupSize, int exhaustedSharePercent, bool timid, double timidRadiusMetres, double gatherRadiusMetres,
            double separationMetres, double leadSeconds, bool tightenWeights, double waveSeconds, double maxSeconds)
        {
            ModEnabled = modEnabled;
            FlockEnabled = flockEnabled;
            AthleticsEnabled = athleticsEnabled;
            CourageEnabled = courageEnabled;
            FreeEagerPercent = ClampInt(freeEagerPercent, 10, 100);
            HysteresisPercent = ClampInt(hysteresisPercent, 0, 30);
            MidEagerPercent = ClampInt(midEagerPercent, 5, 95);
            if (MidEagerPercent >= FreeEagerPercent) MidEagerPercent = FreeEagerPercent - 1;
            MidGroupSize = ClampInt(midGroupSize, 1, 20);
            ExhaustedSharePercent = ClampInt(exhaustedSharePercent, 0, 100);
            TimidSwitch = timid;
            TimidRadiusMetres = Clamp(timidRadiusMetres, 5, 40);
            GatherRadiusMetres = Clamp(gatherRadiusMetres, 5, 100);
            SeparationMetres = Clamp(separationMetres, 1, 10);
            LeadSeconds = Clamp(leadSeconds, 0, 5);
            TightenWeights = tightenWeights;
            WaveSeconds = Clamp(waveSeconds, 0, 60);
            MaxSeconds = Clamp(maxSeconds, 5, 180);
        }

        private static double Clamp(double v, double min, double max) => double.IsNaN(v) || v < min ? min : v > max ? max : v;

        private static int ClampInt(int v, int min, int max) => v < min ? min : v > max ? max : v;

        public bool ModEnabled { get; }

        /// <summary>CavalryFlockEnabled (the switch itself).</summary>
        public bool FlockEnabled { get; }

        public bool AthleticsEnabled { get; }
        public bool CourageEnabled { get; }

        /// <summary>The master switch FIRST, then CavalryFlockEnabled. Courage's switch does not hold it (its own switch).</summary>
        public bool Enabled => ModEnabled && FlockEnabled;

        /// <summary>Which switch holds it off (null = on).</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !FlockEnabled ? "CavalryFlockEnabled" : null;

        /// <summary>CavalryFlockFreeEagerPercent - at or above this eagerness a rider is FREE (vanilla's dance).</summary>
        public int FreeEagerPercent { get; }

        /// <summary>CavalryFlockHysteresisPercent - a flocker is free again only at free + this.</summary>
        public int HysteresisPercent { get; }

        /// <summary>CavalryFlockMidEagerPercent / CavalryFlockMidGroupSize - the group-size curve's middle point: at this eagerness he wants this many of
        /// his kind (him included) with him before he attacks; 1 at the free line, a line between.</summary>
        public int MidEagerPercent { get; }
        public int MidGroupSize { get; }

        /// <summary>CavalryFlockExhaustedSharePercent - at eagerness 0 he wants this share of his kind's riders (never fewer than the middle size), a
        /// line from the middle point.</summary>
        public int ExhaustedSharePercent { get; }

        public bool TimidSwitch { get; }
        public double TimidRadiusMetres { get; }

        /// <summary>CavalryFlockGatherRadiusMetres - the flocking riders of his kind within this distance are "with me".</summary>
        public double GatherRadiusMetres { get; }

        public double SeparationMetres { get; }
        public double LeadSeconds { get; }
        public bool TightenWeights { get; }
        public double WaveSeconds { get; }
        public double MaxSeconds { get; }

        /// <summary>The timid factor: its own switch AND courage on (courage's odds are courage's).</summary>
        public bool TimidOn => TimidSwitch && CourageEnabled;

        public static FlockRules From(TraxSettings s) => new FlockRules(s.ModEnabled, s.CavalryFlockEnabled, s.AthleticsEnabled, s.CourageEnabled,
            s.CavalryFlockFreeEagerPercent, s.CavalryFlockHysteresisPercent, s.CavalryFlockMidEagerPercent, s.CavalryFlockMidGroupSize,
            s.CavalryFlockExhaustedSharePercent, s.CavalryFlockTimid, s.CavalryFlockTimidRadiusMetres, s.CavalryFlockGatherRadiusMetres,
            s.CavalryFlockSeparationMetres, s.CavalryFlockLeadSeconds, s.CavalryFlockTightenWeights, s.CavalryFlockWaveSeconds, s.CavalryFlockMaxSeconds);

        private static string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>The settings sentence for the mission-start line, a change and the summary.</summary>
        public string Describe()
        {
            if (!Enabled)
                return "cavalry flocking OFF (" + OffBecause + ") - the game's own riders, every one charging on his own; the census still MEASURES (spread, lone passes, time between passes) for the ON / OFF comparison";
            return "cavalry flocking: every AI rider under a CHARGE order (both sides, never you) has an EAGERNESS = his Athletics bar" + (AthleticsEnabled ? "" : " (the Athletics system is off: 100%)")
                   + (TimidOn ? " x " + N(100 * FlockMath.TimidEagerFactor, "0") + "% when courage's odds within " + N(TimidRadiusMetres, "0") + " m say he is alone among enemies" : "")
                   + "; at " + FreeEagerPercent + "% or more he is FREE (the game's dance); below it he FLOCKS with his own kind of his formation (melee riders, horse archers, skirmishers apart) "
                   + "and wants a group before he attacks again - 1 at " + FreeEagerPercent + "%, " + MidGroupSize + " at " + MidEagerPercent + "%, " + ExhaustedSharePercent
                   + "% of his kind at 0 - counting the flocking riders of his kind within " + N(GatherRadiusMetres, "0") + " m; the flock never sits: it rides at its centre led "
                   + N(LeadSeconds, "0.0") + " s ahead along a heading pulled toward the formation's goal by its rested riders and toward the unit's centre by its tired ones, each rider in his own slot "
                   + N(SeparationMetres, "0.0") + " m from the next (Agent.SetScriptedPosition - vanilla's way of riding an AI horse" + (TightenWeights ? "; his horseback weights lowered" : "")
                   + "); when the group is big enough and its most rested rider is ready, the flock FLIPS - all of it released at once, a WAVE (" + N(WaveSeconds, "0")
                   + " s free); free again above " + (FreeEagerPercent + HysteresisPercent) + "%; a flocker who never flips is released after " + N(MaxSeconds, "0") + " s";
        }
    }

    /// <summary>Step 55: the pure rules of cavalry flocking (AI_NOTES "Step 55").</summary>
    public static class FlockMath
    {
        // ---- plumbing (documented constants, not settings)

        /// <summary>The census runs every this many seconds: one pass over every tracked AI rider (positions, the flocks, the decisions).</summary>
        public const double CensusSeconds = 0.5;

        /// <summary>Teams × formations × kinds: the flock table's size (a team index or formation index beyond = no flock).</summary>
        public const int MaxTeams = 8;
        public const int MaxFormations = 16;
        public const int KindCount = 3;
        public const int Keys = MaxTeams * MaxFormations * KindCount;

        /// <summary>A timid rider's eagerness × this (courage's odds say he is alone among enemies).</summary>
        public const double TimidEagerFactor = 0.5;

        /// <summary>The FLIP needs the group's LEADER (its most eager rider) at least this ready (his eagerness ÷ the free line) - a flock of
        /// exhausted riders keeps riding until somebody among them has rested a little.</summary>
        public const double LeaderReadyShare = 0.5;

        /// <summary>A rider flocks at least this long before his group may flip (the flip's hysteresis: no join-and-go in the same census).</summary>
        public const double MinFlockSeconds = 3;

        /// <summary>The flock's lead never slower than this (m/s - a canter): the flock is always on the move.</summary>
        public const double MinLeadSpeed = 4;

        /// <summary>The LONE-PASS measure: an AI rider's pass with fewer than <see cref="LonePassMates"/> riders of his kind within
        /// <see cref="LonePassMetres"/> (their census positions) - the proof against "one by one".</summary>
        public const double LonePassMetres = 30;
        public const int LonePassMates = 3;

        /// <summary>A rider's attack this long after his last one starts a new PASS (the time between passes is measured pass to pass).</summary>
        public const double PassGapSeconds = 4;

        /// <summary>A rider's scripted point is sent again when it moved this far since the last one, or after <see cref="ReissueSeconds"/>.</summary>
        public const double ReissueMetres = 2;
        public const double ReissueSeconds = 2;

        /// <summary>A rider's timid look (one proximity query) at most every this many seconds.</summary>
        public const double TimidLookSeconds = 1;

        /// <summary>A census gap longer than this is no velocity (a teleport, a pause).</summary>
        public const double MaxVelocityGapSeconds = 2;

        /// <summary>The fastest velocity a census believes (m/s) - a horse at a full gallop is ~10-12.</summary>
        public const double MaxSpeed = 20;

        /// <summary>The eagerness bands of the measurements: below 25 / 25-50 / 50-75 / 75 and above.</summary>
        public const int EagerBands = 4;

        /// <summary>The flocking rider's horseback weights (<c>HumanAIComponent.OverrideBehaviorParams</c>, y1 at 0 m, a line to y2 at x2, to y3
        /// at x3, y3 beyond): ChargeHorseback strikes what is beside him and never rides off for a target (vanilla's charge: 2 at 0 m, 5 at 25-30 m
        /// and beyond - a far enemy is the most attractive); RangedHorseback keeps a little shooting (vanilla's charge: 0 / 3 at 10 m / 6 at 20 m).</summary>
        public const float FlockChargeY1 = 2f, FlockChargeX2 = 4f, FlockChargeY2 = 0.5f, FlockChargeX3 = 15f, FlockChargeY3 = 0.01f;
        public const float FlockRangedY1 = 1f, FlockRangedX2 = 15f, FlockRangedY2 = 0.5f, FlockRangedX3 = 30f, FlockRangedY3 = 0.1f;

        /// <summary>The golden angle (radians) - the sunflower's slots.</summary>
        public static readonly double GoldenAngle = Math.PI * (3 - Math.Sqrt(5));

        // ---- who he is

        public static RiderKind KindOf(bool rangedWithAmmo, bool thrown) => rangedWithAmmo ? RiderKind.HorseArcher : thrown ? RiderKind.Skirmisher : RiderKind.Melee;

        /// <summary>The flock's slot in the table: (team, formation, kind); −1 = none (out of range).</summary>
        public static int Key(int team, int formation, RiderKind kind)
        {
            if (team < 0 || team >= MaxTeams || formation < 0 || formation >= MaxFormations) return -1;
            int k = (int)kind;
            if (k < 0 || k >= KindCount) return -1;
            return (team * MaxFormations + formation) * KindCount + k;
        }

        public static RiderKind KindOfKey(int key) => (RiderKind)(key % KindCount);

        // ---- eagerness

        /// <summary>His eagerness 0..1: his bar (points ÷ FULL pool - wounds count through the cap; 1 with Athletics off) × the timid factor.</summary>
        public static double Eagerness(in FlockRules r, double bar, bool timid)
        {
            double e = r.AthleticsEnabled ? (double.IsNaN(bar) ? 1 : bar < 0 ? 0 : bar > 1 ? 1 : bar) : 1;
            return timid ? e * TimidEagerFactor : e;
        }

        /// <summary>Free (the dance) at or above the free line; a flocker only at free + the hysteresis.</summary>
        public static bool IsFree(in FlockRules r, double eagerness, bool flocking) =>
            eagerness * 100 >= r.FreeEagerPercent + (flocking ? r.HysteresisPercent : 0) - 1e-9;

        /// <summary>His readiness 0..1 = eagerness ÷ the free line: the pull toward the goal (1 − it = the pull toward the unit's centre).</summary>
        public static double Readiness(in FlockRules r, double eagerness)
        {
            double v = eagerness * 100 / r.FreeEagerPercent;
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }

        /// <summary>THE GROUP-SIZE CURVE: how many of his kind (him included) he wants with him before he attacks - 1 at the free line, the middle
        /// size at the middle eagerness, the exhausted share of his kind's riders at 0 (never below the middle size), straight lines between,
        /// rounded up, never more than his kind has.</summary>
        public static int GroupNeeded(in FlockRules r, double eagerness, int kindCount)
        {
            double e = eagerness * 100;
            double free = r.FreeEagerPercent, mid = r.MidEagerPercent;
            double need;
            if (e >= free) need = 1;
            else if (e >= mid) need = 1 + (r.MidGroupSize - 1) * (free - e) / (free - mid);
            else
            {
                double big = Math.Max(r.MidGroupSize, Math.Ceiling(r.ExhaustedSharePercent / 100.0 * kindCount - 1e-9));
                double t = e <= 0 ? 1 : (mid - e) / mid;
                need = r.MidGroupSize + (big - r.MidGroupSize) * t;
            }
            int n = (int)Math.Ceiling(need - 1e-9);
            if (n < 1) n = 1;
            if (kindCount > 0 && n > kindCount) n = kindCount;
            return n;
        }

        /// <summary>THE HEADING BLEND: the rested riders' readiness pulls toward the goal, the tired riders' (1 − readiness) toward the unit's
        /// centre; both directions unit vectors (0, 0 = unknown); the result a unit vector (false = no pull at all).</summary>
        public static bool Heading(double readySum, double tiredSum, double goalX, double goalY, double centreX, double centreY, out double hx, out double hy)
        {
            hx = readySum * goalX + tiredSum * centreX;
            hy = readySum * goalY + tiredSum * centreY;
            double len = Math.Sqrt(hx * hx + hy * hy);
            if (!(len > 1e-9))
            {
                hx = hy = 0;
                return false;
            }
            hx /= len;
            hy /= len;
            return true;
        }

        /// <summary>A unit vector from (x0, y0) to (x1, y1) (0, 0 for a NaN or a point on top).</summary>
        public static void Unit(double x0, double y0, double x1, double y1, out double ux, out double uy)
        {
            ux = x1 - x0;
            uy = y1 - y0;
            double len = Math.Sqrt(ux * ux + uy * uy);
            if (double.IsNaN(len) || len < 1e-6)
            {
                ux = uy = 0;
                return;
            }
            ux /= len;
            uy /= len;
        }

        /// <summary>The share of the group's pull that points at the goal (readySum ÷ (readySum + tiredSum)) - the vote.</summary>
        public static double GoalVote(double readySum, double tiredSum) => readySum + tiredSum > 1e-9 ? readySum / (readySum + tiredSum) : 0;

        /// <summary>THE FLIP: the group (flocking riders of his kind within the gather radius, him included) has the size its MEAN eagerness
        /// demands, its leader (its most eager rider) is ready, and he has flocked long enough.</summary>
        public static bool Flips(int withMe, int needed, double leaderReadiness, double flockedSeconds) =>
            withMe >= needed && leaderReadiness >= LeaderReadyShare - 1e-9 && flockedSeconds >= MinFlockSeconds - 1e-9;

        /// <summary>A rider's place in the flock: the sunflower (Vogel's spiral) - slot i at radius c × √i, angle i × the golden angle; with c =
        /// separation ÷ √π the neighbours stand about one separation apart and the disc fills evenly whatever the count. Slot 0 is the centre.
        /// Fixed per rider for his spell - the offsets never jitter (no per-frame steering noise).</summary>
        public static void SlotOffset(int slot, double separation, out double dx, out double dy)
        {
            if (slot <= 0 || !(separation > 0))
            {
                dx = dy = 0;
                return;
            }
            double rad = separation / Math.Sqrt(Math.PI) * Math.Sqrt(slot);
            double a = slot * GoldenAngle;
            dx = rad * Math.Cos(a);
            dy = rad * Math.Sin(a);
        }

        /// <summary>The point a flocker rides to: his group's centre, led along the heading at the group's speed (never below
        /// <see cref="MinLeadSpeed"/>) × the lead - the flock moves instead of converging on a dot - + his slot.</summary>
        public static void SteerPoint(double cx, double cy, double hx, double hy, double speed, double leadSeconds, int slot, double separation, out double tx, out double ty)
        {
            SlotOffset(slot, separation, out double ox, out double oy);
            double v = double.IsNaN(speed) || speed < MinLeadSpeed ? MinLeadSpeed : speed;
            tx = cx + hx * v * leadSeconds + ox;
            ty = cy + hy * v * leadSeconds + oy;
        }

        public static bool GaveUp(in FlockRules r, double since, double now) => now - since >= r.MaxSeconds;

        /// <summary>The scripted point is sent again: never sent, moved more than <see cref="ReissueMetres"/>, or older than <see cref="ReissueSeconds"/>.</summary>
        public static bool Reissue(double issuedAt, double issuedX, double issuedY, double tx, double ty, double now)
        {
            if (double.IsNaN(issuedAt) || now < issuedAt) return true;
            if (now - issuedAt >= ReissueSeconds) return true;
            double dx = tx - issuedX, dy = ty - issuedY;
            return dx * dx + dy * dy > ReissueMetres * ReissueMetres;
        }

        /// <summary>A velocity from two census positions (0 for a long gap; clamped to <see cref="MaxSpeed"/>).</summary>
        public static void Velocity(double x0, double y0, double t0, double x1, double y1, double t1, out double vx, out double vy)
        {
            vx = vy = 0;
            double dt = t1 - t0;
            if (double.IsNaN(t0) || double.IsNaN(x0) || !(dt > 1e-6) || dt > MaxVelocityGapSeconds) return;
            vx = (x1 - x0) / dt;
            vy = (y1 - y0) / dt;
            double v = Math.Sqrt(vx * vx + vy * vy);
            if (v > MaxSpeed)
            {
                vx *= MaxSpeed / v;
                vy *= MaxSpeed / v;
            }
        }

        public static bool LonePass(int mates) => mates < LonePassMates;

        public static int EagerBand(double eagerness)
        {
            double e = eagerness * 100;
            return e < 25 ? 0 : e < 50 ? 1 : e < 75 ? 2 : 3;
        }

        // ---- names

        public static string BandName(int band) => band switch
        {
            0 => "<25%",
            1 => "25-50%",
            2 => "50-75%",
            _ => "75%+",
        };

        public static string KindName(RiderKind k) => k switch
        {
            RiderKind.Melee => "melee riders",
            RiderKind.HorseArcher => "horse archers",
            RiderKind.Skirmisher => "skirmishers",
            _ => k.ToString(),
        };

        public static string SkipName(FlockSkip s) => s switch
        {
            FlockSkip.NotAi => "not AI (you / taken over)",
            FlockSkip.NoFormation => "no formation",
            FlockSkip.Busy => "busy (detached / machine / ladder / routing / the game's scripted walk)",
            FlockSkip.NotCharging => "not on a charge (hold / move / follow / advance - slots keep them)",
            _ => s.ToString(),
        };

        public static string ReasonName(FlockReason r) => r switch
        {
            FlockReason.Tired => "tired",
            FlockReason.Timid => "timid (alone among enemies)",
            _ => r.ToString(),
        };

        public static string EndName(FlockEnd e) => e switch
        {
            FlockEnd.Wave => "the wave (flipped)",
            FlockEnd.Rested => "rested (free again)",
            FlockEnd.GaveUp => "gave up (no flip in time)",
            FlockEnd.SwitchedOff => "switched off",
            FlockEnd.Excluded => "no longer eligible",
            FlockEnd.LeftField => "left the field",
            FlockEnd.Error => "error",
            FlockEnd.MissionEnd => "battle over",
            _ => e.ToString(),
        };
    }

    /// <summary>
    /// Step 55: one rider's flocking state - created once per tracked man (like his courage), reused; no per-tick allocation. Main thread.
    /// </summary>
    public sealed class RiderFlock
    {
        public bool KindKnown;
        public RiderKind Kind;

        /// <summary>His flock's key at the last census (−1 = none).</summary>
        public int Key = -1;

        /// <summary>Seen by a census (counted once).</summary>
        public bool Seen;

        /// <summary>Eligible at the last census (in the rider list) and his place there.</summary>
        public bool Eligible;
        public int ListIndex = -1;

        public RiderFlockState State { get; private set; }
        public bool Flocking => State == RiderFlockState.Flocking;
        public FlockReason Reason { get; private set; } = FlockReason.Count;
        public double Since { get; private set; } = double.NaN;

        /// <summary>His sunflower slot in the flock for this spell.</summary>
        public int Slot { get; private set; }

        /// <summary>The wave runs until then (after a flip / a give-up - the pass goes in; no new flock meanwhile).</summary>
        public double WaveUntil = double.NegativeInfinity;

        /// <summary>His eagerness and timid flag at the last census.</summary>
        public double Eagerness = 1;
        public bool Timid;

        /// <summary>His census position, its time and the velocity from the last two.</summary>
        public double X = double.NaN, Y = double.NaN, T = double.NaN, VelX, VelY;

        /// <summary>The scripted point last sent (NaN = none in this spell) and whether OUR scripted walk is on him.</summary>
        public double IssuedX = double.NaN, IssuedY = double.NaN, IssuedAt = double.NaN;
        public bool Moving;

        /// <summary>His horseback weights are ours.</summary>
        public bool WeightsSet;

        /// <summary>Decided in this census already (a flip released him).</summary>
        public bool Done;

        public double NextTimidLook = double.NegativeInfinity;

        /// <summary>The last timid look's numbers (the log).</summary>
        public double Odds = double.NaN, Required = double.NaN;

        /// <summary>His last attack and his last PASS (an attack after <see cref="FlockMath.PassGapSeconds"/> of none).</summary>
        public double LastAttack = double.NaN, LastPass = double.NaN;

        public void Begin(FlockReason reason, double now, int slot)
        {
            State = RiderFlockState.Flocking;
            Reason = reason;
            Since = now;
            Slot = slot < 0 ? 0 : slot;
            IssuedAt = double.NaN;
        }

        /// <summary>The spell ends: its length (seconds); a wave window when <paramref name="waveSeconds"/> > 0.</summary>
        public double End(double now, double waveSeconds)
        {
            double len = State == RiderFlockState.Flocking && !double.IsNaN(Since) && now >= Since ? now - Since : 0;
            if (waveSeconds > 0)
            {
                State = RiderFlockState.Wave;
                WaveUntil = now + waveSeconds;
            }
            else State = RiderFlockState.Free;
            IssuedAt = double.NaN;
            return len;
        }

        /// <summary>The wave is over: free (re-evaluated from his eagerness at once).</summary>
        public bool WaveOver(double now)
        {
            if (State != RiderFlockState.Wave || now < WaveUntil) return false;
            State = RiderFlockState.Free;
            return true;
        }

        /// <summary>Back to free at once (the switch, the master switch, the battle's end, an exclusion during a wave).</summary>
        public void Reset()
        {
            State = RiderFlockState.Free;
            IssuedAt = double.NaN;
            WaveUntil = double.NegativeInfinity;
        }

        /// <summary>A census saw him at (x, y) at <paramref name="now"/>: the velocity from the last sight.</summary>
        public void See(double x, double y, double now)
        {
            FlockMath.Velocity(X, Y, T, x, y, now, out VelX, out VelY);
            X = x;
            Y = y;
            T = now;
        }
    }

    /// <summary>
    /// Step 55: the flocks of one census - per (team, formation, kind) the riders' count, centre (the spread's), the formation's goal and centre of
    /// mass (read once a census per flock) and the next sunflower slot. Fixed arrays (<see cref="FlockMath.Keys"/>) and a list of the keys touched -
    /// no allocation per census.
    /// </summary>
    public sealed class FlockTable
    {
        private readonly double[] _sumX = new double[FlockMath.Keys];
        private readonly double[] _sumY = new double[FlockMath.Keys];
        private readonly int[] _flocking = new int[FlockMath.Keys];
        private readonly int[] _touched = new int[FlockMath.Keys];
        private readonly bool[] _isTouched = new bool[FlockMath.Keys];
        private int _touchedCount;

        public readonly int[] Riders = new int[FlockMath.Keys];
        public readonly double[] CentreX = new double[FlockMath.Keys];
        public readonly double[] CentreY = new double[FlockMath.Keys];

        /// <summary>The formation's goal (its target) and centre of mass this census: 0 = not read yet, 1 = read and valid, −1 = read, none.</summary>
        public readonly sbyte[] GoalRead = new sbyte[FlockMath.Keys];
        public readonly double[] GoalX = new double[FlockMath.Keys];
        public readonly double[] GoalY = new double[FlockMath.Keys];
        public readonly sbyte[] MassRead = new sbyte[FlockMath.Keys];
        public readonly double[] MassX = new double[FlockMath.Keys];
        public readonly double[] MassY = new double[FlockMath.Keys];

        /// <summary>The next sunflower slot of each flock (back to 0 when nobody flocks in it).</summary>
        public readonly int[] NextSlot = new int[FlockMath.Keys];

        public int TouchedCount => _touchedCount;

        public int TouchedKey(int i) => _touched[i];

        /// <summary>A new census: the last one's keys back to 0.</summary>
        public void Begin()
        {
            for (int i = 0; i < _touchedCount; i++)
            {
                int k = _touched[i];
                _isTouched[k] = false;
                Riders[k] = _flocking[k] = 0;
                _sumX[k] = _sumY[k] = 0;
                GoalRead[k] = MassRead[k] = 0;
            }
            _touchedCount = 0;
        }

        /// <summary>One rider of the flock <paramref name="key"/> (any state).</summary>
        public void Add(int key, double x, double y, bool flocking)
        {
            if (key < 0 || key >= FlockMath.Keys) return;
            if (!_isTouched[key])
            {
                _isTouched[key] = true;
                _touched[_touchedCount++] = key;
            }
            Riders[key]++;
            _sumX[key] += x;
            _sumY[key] += y;
            if (flocking) _flocking[key]++;
        }

        /// <summary>The centres; a flock with nobody flocking restarts its slots; a flock not seen any more too.</summary>
        public void Finish()
        {
            for (int i = 0; i < _touchedCount; i++)
            {
                int k = _touched[i];
                CentreX[k] = _sumX[k] / Riders[k];
                CentreY[k] = _sumY[k] / Riders[k];
                if (_flocking[k] == 0) NextSlot[k] = 0;
            }
        }

        /// <summary>The next slot of the flock (a rider joining).</summary>
        public int TakeSlot(int key) => key < 0 || key >= FlockMath.Keys ? 0 : NextSlot[key]++;

        /// <summary>A rider's distance from his kind's centre this census (NaN = none).</summary>
        public double DistanceFromCentre(int key, double x, double y)
        {
            if (key < 0 || key >= FlockMath.Keys || !_isTouched[key]) return double.NaN;
            double dx = x - CentreX[key], dy = y - CentreY[key];
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Touched(int key) => key >= 0 && key < FlockMath.Keys && _isTouched[key];
    }

    /// <summary>
    /// Step 55: "who is with me" - a hash grid over the flocking riders of one census (cell = the gather radius), queried for the flocking riders of
    /// the same flock within a radius. Fixed bucket heads; the per-rider arrays grow (doubling) only when a battle has more riders than ever before -
    /// no allocation per census.
    /// </summary>
    public sealed class FlockGrid
    {
        public const int Buckets = 1024;
        private readonly int[] _head = new int[Buckets];
        private int[] _next = new int[64];
        private int[] _key = new int[64];
        private int[] _cx = new int[64];
        private int[] _cy = new int[64];
        private double[] _x = new double[64];
        private double[] _y = new double[64];
        private int[] _id = new int[64];
        private int _count;
        private double _cell = 25;

        public int Count => _count;

        public FlockGrid()
        {
            for (int i = 0; i < Buckets; i++) _head[i] = -1;
        }

        /// <summary>Empty, with this cell size (≥ 1 m).</summary>
        public void Clear(double cell)
        {
            for (int i = 0; i < _count; i++) _head[Bucket(_key[i], _cx[i], _cy[i])] = -1;
            _count = 0;
            _cell = cell < 1 ? 1 : cell;
        }

        private static int Bucket(int key, int cx, int cy)
        {
            unchecked
            {
                int h = key * 73856093 ^ cx * 19349663 ^ cy * 83492791;
                return h & (Buckets - 1);
            }
        }

        private int CellOf(double v) => (int)Math.Floor(v / _cell);

        /// <summary>One flocking rider (<paramref name="id"/> = the caller's index for him).</summary>
        public void Add(int id, int key, double x, double y)
        {
            if (_count == _next.Length) Grow();
            int i = _count++;
            _id[i] = id;
            _key[i] = key;
            _x[i] = x;
            _y[i] = y;
            _cx[i] = CellOf(x);
            _cy[i] = CellOf(y);
            int b = Bucket(key, _cx[i], _cy[i]);
            _next[i] = _head[b];
            _head[b] = i;
        }

        private void Grow()
        {
            int n = _next.Length * 2;
            Array.Resize(ref _next, n);
            Array.Resize(ref _key, n);
            Array.Resize(ref _cx, n);
            Array.Resize(ref _cy, n);
            Array.Resize(ref _x, n);
            Array.Resize(ref _y, n);
            Array.Resize(ref _id, n);
        }

        /// <summary>The ids of the riders of flock <paramref name="key"/> within <paramref name="radius"/> (≤ the cell) of (x, y) into
        /// <paramref name="into"/> (him included when he is in the grid); how many (capped at the buffer's length).</summary>
        public int Query(int key, double x, double y, double radius, int[] into)
        {
            int n = 0;
            int cx = CellOf(x), cy = CellOf(y);
            double r2 = radius * radius;
            int reach = radius <= _cell ? 1 : (int)Math.Ceiling(radius / _cell);
            for (int gx = cx - reach; gx <= cx + reach; gx++)
                for (int gy = cy - reach; gy <= cy + reach; gy++)
                {
                    for (int i = _head[Bucket(key, gx, gy)]; i >= 0; i = _next[i])
                    {
                        if (_key[i] != key || _cx[i] != gx || _cy[i] != gy) continue;
                        double dx = _x[i] - x, dy = _y[i] - y;
                        if (dx * dx + dy * dy > r2) continue;
                        if (n < into.Length) into[n++] = _id[i];
                    }
                }
            return n;
        }
    }
}
