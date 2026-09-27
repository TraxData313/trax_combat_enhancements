using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// One mission's step-back numbers (DESIGN §2, step 5d) for the <c>[summary]</c> block - built so
    /// ONE playtest proves or disproves the feature: the chance rolled after every AI swing on foot,
    /// by f (0% in the peak zone, rising as f falls); what the dice said and what the game's safety
    /// checks refused; how each step back ended (completed / cut short by reason); how far and how
    /// long they really moved; whether they faced their enemy (the risk to watch - a turned back)
    /// and kept their guard (hits taken blocked vs landed, against everyone else's); and the release
    /// checks - no scripted movement left behind (0 at mission end). A new instance per mission.
    /// Main thread only. Plain counters are public fields: the module just counts.
    /// </summary>
    public sealed class StepBackStats
    {
        /// <summary>The technique, named in the summary header (AI_NOTES "Step 5d").</summary>
        public const string Technique =
            "a scripted step - the engine's SetScriptedPositionAndDirection to the spot straight away from his target, facing him, at a walk "
            + "(+ NoAttack while StepBackHoldAttacks), then DisableScriptedMovement - his formation takes him back";

        private static readonly int Refusals = Enum.GetValues(typeof(StepBackRefusal)).Length;
        private static readonly int Ends = Enum.GetValues(typeof(StepBackEnd)).Length;

        private readonly int[] _swings = new int[AthleticsMath.PeakBins];
        private readonly double[] _chanceSum = new double[AthleticsMath.PeakBins];
        private readonly int[] _yes = new int[AthleticsMath.PeakBins];
        private readonly int[] _notRolled = new int[4];
        private readonly int[] _refused = new int[Refusals];
        private readonly int[] _ended = new int[Ends];
        private readonly int[] _startFacing = new int[4];
        private readonly int[] _midFacing = new int[4];
        private readonly int[] _midMotion = new int[4];
        private readonly int[] _endFacing = new int[4];

        // ---- starts
        public int Started;
        public int StartedHolding;
        public int StartedCharging;
        public int StartedNoFormation;
        public int NowStepping;
        public int PeakAtOnce;

        // ---- how they went
        public MeanStd Moved;
        public double MovedMin = double.MaxValue;
        public double MovedMax;
        public MeanStd AskedDistance;
        public int ReachedSpot;
        public MeanStd Seconds;
        public int MidSamples;
        public int EndSamples;

        // ---- the guard
        public int HitsWhileStepping;
        public int BlockedWhileStepping;
        public int HitsOthersOnFoot;
        public int BlockedOthersOnFoot;
        public int SwingsWhileStepping;

        // ---- release checks
        public int Releases;
        public int StillScriptedAfterRelease;
        public int FlagsClearedByHand;
        public int MidStepAtMissionEnd;
        public int OverdueAtMissionEnd;
        public int StillScriptedAtMissionEnd;
        public int SwitchedOffReleases;

        // ------------------------------------------------------------------ counting

        /// <summary>One roll after an AI swing on foot: f then, the chance, what the dice said.</summary>
        public void AddRoll(double peakShare, double chance, bool yes)
        {
            int bin = AthleticsMath.PeakBin(peakShare);
            _swings[bin]++;
            _chanceSum[bin] += chance;
            if (yes) _yes[bin]++;
        }

        public void AddNotRolled(StepBackNotRolled why) => _notRolled[(int)why]++;

        public void AddRefused(StepBackRefusal why) => _refused[(int)why]++;

        /// <param name="movementState">His formation's movement state at the start: 0 charge, 1 hold,
        /// 3 stand ground, −1 no formation (MovementOrder.MovementStateEnum).</param>
        public void AddStart(int movementState, int facingBin, double askedDistance)
        {
            Started++;
            if (movementState < 0) StartedNoFormation++;
            else if (movementState == 0) StartedCharging++;
            else StartedHolding++;
            _startFacing[Clamp(facingBin)]++;
            AskedDistance.Add(askedDistance);
            NowStepping++;
            if (NowStepping > PeakAtOnce) PeakAtOnce = NowStepping;
        }

        public void AddMidSample(int facingBin, int motionBin)
        {
            MidSamples++;
            _midFacing[Clamp(facingBin)]++;
            _midMotion[Clamp(motionBin)]++;
        }

        /// <param name="moved">Metres he really moved (NaN when not read - a removed man).</param>
        /// <param name="toSpot">Metres from the spot at the end (NaN when not read).</param>
        /// <param name="endFacingBin">Facing at the end, or −1 when not read.</param>
        public void AddEnd(StepBackEnd why, double seconds, double moved, double toSpot, int endFacingBin)
        {
            _ended[(int)why]++;
            if (NowStepping > 0) NowStepping--;
            if (seconds >= 0) Seconds.Add(seconds);
            if (!double.IsNaN(moved))
            {
                Moved.Add(moved);
                if (moved < MovedMin) MovedMin = moved;
                if (moved > MovedMax) MovedMax = moved;
            }
            if (!double.IsNaN(toSpot) && toSpot <= StepBackMath.ReachedSlack) ReachedSpot++;
            if (endFacingBin >= 0)
            {
                EndSamples++;
                _endFacing[Clamp(endFacingBin)]++;
            }
        }

        /// <summary>A melee hit on a fighter on foot: stepping back or not, blocked (shield, parry,
        /// chamber) or landed.</summary>
        public void AddHitTaken(bool stepping, bool blocked)
        {
            if (stepping)
            {
                HitsWhileStepping++;
                if (blocked) BlockedWhileStepping++;
            }
            else
            {
                HitsOthersOnFoot++;
                if (blocked) BlockedOthersOnFoot++;
            }
        }

        public int Swings(int bin) => _swings[bin];

        public int Yes(int bin) => _yes[bin];

        public double MeanChance(int bin) => _swings[bin] > 0 ? _chanceSum[bin] / _swings[bin] : double.NaN;

        public int Rolls
        {
            get
            {
                int n = 0;
                foreach (var s in _swings) n += s;
                return n;
            }
        }

        public int DiceYes
        {
            get
            {
                int n = 0;
                foreach (var s in _yes) n += s;
                return n;
            }
        }

        public int NotRolled(StepBackNotRolled why) => _notRolled[(int)why];

        public int Refused(StepBackRefusal why) => _refused[(int)why];

        public int RefusedTotal
        {
            get
            {
                int n = 0;
                foreach (var s in _refused) n += s;
                return n;
            }
        }

        public int Ended(StepBackEnd why) => _ended[(int)why];

        public int EndedTotal
        {
            get
            {
                int n = 0;
                foreach (var s in _ended) n += s;
                return n;
            }
        }

        /// <summary>Everything that ended other than by its time running out.</summary>
        public int CutShort => EndedTotal - Ended(StepBackEnd.TimeUp);

        public int StartFacing(int bin) => _startFacing[bin];

        public int MidFacing(int bin) => _midFacing[bin];

        public int MidMotion(int bin) => _midMotion[bin];

        public int EndFacing(int bin) => _endFacing[bin];

        // ------------------------------------------------------------------ wording

        public static string RefusalText(StepBackRefusal why) => why switch
        {
            StepBackRefusal.NotAiControlled => "not AI-controlled",
            StepBackRefusal.NoAiComponent => "no AI component",
            StepBackRefusal.Busy => "busy (the game's own check: detached, ladder, siege engine, an object, running away or already scripted)",
            StepBackRefusal.Routing => "routing",
            StepBackRefusal.HoldingArrangement => "shield wall/square/circle",
            StepBackRefusal.RetreatOrder => "formation retreating",
            StepBackRefusal.NoEnemy => "no enemy target",
            StepBackRefusal.EnemyTooFar => "enemy beyond StepBackEnemyRange",
            StepBackRefusal.OffNavMesh => "spot off the navmesh",
            StepBackRefusal.NotLevel => "spot not level (wall edge, stairs)",
            StepBackRefusal.Blocked => "no straight way back (wall, fence, gap)",
            StepBackRefusal.AtOnceCap => "StepBackMaxAtOnce reached",
            StepBackRefusal.TickBudget => "tick budget",
            StepBackRefusal.Gone => "gone before the tick (left the field or switched off)",
            StepBackRefusal.NoLongerEligible => "no longer eligible at the tick (mounted, mission kind)",
            StepBackRefusal.EngineError => "engine call failed",
            StepBackRefusal.EngineIgnored => "engine did not take the scripted position",
            _ => why.ToString(),
        };

        public static string EndText(StepBackEnd why) => why switch
        {
            StepBackEnd.TimeUp => "time up",
            StepBackEnd.LeftField => "left the field",
            StepBackEnd.MissionEnd => "mission end",
            StepBackEnd.SwitchedOff => "switched off",
            StepBackEnd.OrderChanged => "formation order changed",
            StepBackEnd.FormationChanged => "formation changed",
            StepBackEnd.Detached => "detached from his formation",
            StepBackEnd.PlayerControl => "the player took him",
            StepBackEnd.Mounted => "mounted",
            StepBackEnd.Routing => "routing",
            StepBackEnd.HandedOver => "handed over to the game (not disabled)",
            StepBackEnd.ClearedByGame => "cleared by the game first",
            StepBackEnd.NotActive => "no longer active",
            StepBackEnd.Error => "error",
            _ => why.ToString(),
        };

        /// <summary>
        /// The <c>[summary]</c> step-back lines, plain words (docs/PLAYTEST.md "Step back" quotes them).
        /// </summary>
        public List<string> SummaryLines(in StepBackRules r)
        {
            var lines = new List<string>();
            lines.Add("step back - technique: " + Technique + "; settings at the end: " + r.Describe());

            var sb = new StringBuilder("step back rolls after AI melee swings on foot, by f (the chance must be 0% at full strength and rise as f falls): ");
            for (int bin = 0; bin < AthleticsMath.PeakBins; bin++)
            {
                if (bin > 0) sb.Append(" | ");
                sb.Append(AthleticsMath.PeakBinName(bin)).Append(' ').Append(_swings[bin]).Append(" swings");
                if (_swings[bin] > 0)
                    sb.Append(", chance avg ").Append(P0(MeanChance(bin))).Append(", dice yes ").Append(_yes[bin])
                      .Append(" (").Append(P0((double)_yes[bin] / _swings[bin])).Append(')');
            }
            sb.Append("; not rolled: you ").Append(NotRolled(StepBackNotRolled.Player)).Append(", riders ").Append(NotRolled(StepBackNotRolled.Mounted))
              .Append(", not a field battle (tournament, arena, duel, naval, deployment, ending) ").Append(NotRolled(StepBackNotRolled.MissionKind));
            lines.Add(sb.ToString());

            sb = new StringBuilder("step back starts: dice yes ").Append(DiceYes).Append(" → started ").Append(Started)
                .Append(" (holding a line ").Append(StartedHolding).Append(", charging ").Append(StartedCharging).Append(", no formation ")
                .Append(StartedNoFormation).Append("), most at once ").Append(PeakAtOnce).Append("; not started ").Append(RefusedTotal);
            AppendCounts(sb, _refused, i => RefusalText((StepBackRefusal)i), skipZeroIndex: true);
            lines.Add(sb.ToString());

            sb = new StringBuilder("step back ends: ").Append(EndedTotal).Append(" - completed (time up) ").Append(Ended(StepBackEnd.TimeUp))
                .Append(", cut short ").Append(CutShort);
            var cut = (int[])_ended.Clone();
            cut[(int)StepBackEnd.TimeUp] = 0;
            AppendCounts(sb, cut, i => EndText((StepBackEnd)i), skipZeroIndex: true);
            if (NowStepping > 0) sb.Append("; still stepping back when the summary was written ").Append(NowStepping);
            lines.Add(sb.ToString());

            lines.Add("step back moves: " + (Moved.Count == 0
                ? "none measured"
                : "avg " + N2(Moved.Mean) + " m of " + N2(AskedDistance.Mean) + " asked (min " + N2(MovedMin) + ", max " + N2(MovedMax)
                  + ", reached the spot " + ReachedSpot + " of " + Moved.Count + ")")
                + ", lasted avg " + (Seconds.Count == 0 ? "n/a" : N2(Seconds.Mean) + " s (n " + Seconds.Count + ")"));

            lines.Add("step back facing (THE risk: a turned back) - at the start: " + Facing(_startFacing)
                + " | mid-step (" + MidSamples + " sampled): " + Facing(_midFacing) + "; " + Motion(_midMotion)
                + " | at the end (" + EndSamples + "): " + Facing(_endFacing));

            lines.Add("step back guard: hits taken while stepping back " + HitsWhileStepping + " - blocked " + BlockedWhileStepping
                + (HitsWhileStepping > 0 ? " (" + P0((double)BlockedWhileStepping / HitsWhileStepping) + ")" : "")
                + ", landed " + (HitsWhileStepping - BlockedWhileStepping) + " | everyone else on foot: " + HitsOthersOnFoot + " hits, blocked "
                + BlockedOthersOnFoot + (HitsOthersOnFoot > 0 ? " (" + P0((double)BlockedOthersOnFoot / HitsOthersOnFoot) + ")" : "")
                + " | swings started while stepping back " + SwingsWhileStepping + (r.HoldAttacks ? " (0 expected: StepBackHoldAttacks is on)" : " (StepBackHoldAttacks is off: allowed)"));

            lines.Add("step back release check: " + Releases + " released through the engine - scripted movement still on right after "
                + StillScriptedAfterRelease + " (must be 0), our flags (NoAttack, DoNotRun) cleared by hand " + FlagsClearedByHand
                + " | at mission end: " + MidStepAtMissionEnd + " were mid-step (released then), overdue (past their time) " + OverdueAtMissionEnd
                + " (must be 0), scripted movement still on after that release " + StillScriptedAtMissionEnd + " (must be 0)"
                + (SwitchedOffReleases > 0 ? " | switched off mid-battle " + SwitchedOffReleases + " time(s), everyone released at once" : ""));
            return lines;
        }

        private static string Facing(int[] bins)
        {
            int n = bins[0] + bins[1] + bins[2] + bins[3];
            if (n == 0) return "none";
            return StepBackMath.FacingBinName(0) + " " + bins[0] + " (" + P0((double)bins[0] / n) + "), side-on " + bins[1]
                   + ", back turned " + bins[2] + (bins[3] > 0 ? ", no enemy to face " + bins[3] : "");
        }

        private static string Motion(int[] bins)
        {
            int n = bins[0] + bins[1] + bins[2] + bins[3];
            if (n == 0) return "motion none";
            return "moving away " + bins[0] + ", standing or sideways " + bins[1] + ", moving toward him " + bins[2]
                   + (bins[3] > 0 ? ", unknown " + bins[3] : "");
        }

        private static void AppendCounts(StringBuilder sb, int[] counts, Func<int, string> name, bool skipZeroIndex)
        {
            bool first = true;
            for (int i = skipZeroIndex ? 1 : 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                sb.Append(first ? " (" : ", ").Append(name(i)).Append(' ').Append(counts[i]);
                first = false;
            }
            if (!first) sb.Append(')');
        }

        private static int Clamp(int bin) => bin < 0 ? 3 : bin > 3 ? 3 : bin;

        private static string N2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string P0(double share) => double.IsNaN(share) ? "n/a" : (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
