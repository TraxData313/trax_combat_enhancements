using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 100b: where a block's TIMING came from (AI_NOTES "Step 100" §1). <see cref="None"/> = not asked / unknown (score 0 - an untimed
    /// block, step 56 exactly).
    /// </summary>
    public enum TimingSource : byte
    {
        /// <summary>No timing read (not a correct block, the switch off, a read the clock cannot answer - a parry reaction, no defend).</summary>
        None,

        /// <summary>The player's guard clock (read every tick, and once more at contact).</summary>
        PlayerClock,

        /// <summary>An AI man's channel-1 ACTIVE defend at contact: age = its progress x its animation length.</summary>
        AiActive,

        /// <summary>An AI man's channel-1 PASSIVE defend at contact - a guard held up: age = held (score 0).</summary>
        AiHeld,

        /// <summary>The engine's chamber block (an attack into the attack - its own timed counter): always perfect.</summary>
        Chamber,
    }

    /// <summary>Step 100b: what channel 1 showed at contact (the diagnostic's answer to AI_NOTES "Step 100" H / H' / C1).</summary>
    public enum GuardRead : byte
    {
        NotRead,
        Active,
        Passive,
        ParryReaction,
        OtherDefend,
        NotDefend,
    }

    /// <summary>Step 100b: the engine's own verdict on the collision (<c>CombatCollisionResult</c>).</summary>
    public enum BlockVerdict : byte
    {
        Blocked,
        Parried,
        Chamber,
        Other,
    }

    /// <summary>
    /// Step 100b: ONE man's guard clock (a struct on his record): the time his guard went UP - the press (0 → a guard), and in a MANUAL
    /// block-direction mode also a switch to another real direction (a late switch to the right side IS the skill); in the game's AUTOMATIC
    /// mode only the press counts (the game turns a held guard to the blow on its own - a direction change there would read "perfect" on every
    /// blow). Pure; the module feeds it the player's reads every tick and once more at contact.
    /// </summary>
    public struct GuardClock
    {
        /// <summary>A guard is up but the engine named no direction (stage Defend / DefendParry, the defend flag 0).</summary>
        public const int UpNoDirection = 1;

        /// <summary>The guard key last noted (0 = no guard; a defend direction flag; <see cref="UpNoDirection"/>).</summary>
        public int Key;

        /// <summary>When the current guard went up (mission seconds).</summary>
        public double RaisedAt;

        /// <summary>Presses noted (a guard raised from none) and manual re-directions (diagnostic counts).</summary>
        public int Presses, Redirects;

        /// <summary>Note the guard now. <paramref name="automatic"/> = the game's automatic block direction (only the press counts).</summary>
        public void Note(int key, double now, bool automatic)
        {
            if (key == 0)
            {
                Key = 0;
                return;
            }
            if (Key == 0)
            {
                RaisedAt = now;
                Presses++;
            }
            else if (!automatic && key != Key && key != UpNoDirection && Key != UpNoDirection)
            {
                RaisedAt = now;
                Redirects++;
            }
            Key = key;
        }

        /// <summary>The guard's age now (s); NaN = no guard up.</summary>
        public double Age(double now) => Key == 0 ? double.NaN : Math.Max(0, now - RaisedAt);

        public void Reset() => this = default;
    }

    /// <summary>
    /// Step 100b: the timed block's settings, read live (DESIGN decision 91). <see cref="Enabled"/> = the master switch FIRST, then
    /// <c>TimedBlocksEnabled</c> (the impact gates - Athletics, ImpactEnabled, the riposte's family - are <see cref="ImpactRules"/>' own).
    /// </summary>
    public readonly struct TimedBlockRules
    {
        public TimedBlockRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            Switch = s.TimedBlocksEnabled;
            Perfect = Math.Max(0, (double)s.TimedBlockPerfectSeconds);
            Window = Math.Max(0, (double)s.TimedBlockWindowSeconds);
            RiposteMax = Clamp01(s.TimedBlockRiposteMaxPercent / 100.0);
            StaggerMax = Math.Max(0, (double)s.TimedBlockStaggerMaxSeconds);
            AgainstYouMax = Math.Max(0, (double)s.TimedBlockAgainstYouMaxSeconds);
            ParryShare = Clamp01(s.TimedBlockParryPercent / 100.0);
            AiShare = Clamp01(s.TimedBlockAiPercent / 100.0);
        }

        public static TimedBlockRules From(TraxSettings s) => new TimedBlockRules(s);

        public bool ModEnabled { get; }
        public bool Switch { get; }
        public bool Enabled => ModEnabled && Switch;
        public double Perfect { get; }
        public double Window { get; }
        public double RiposteMax { get; }
        public double StaggerMax { get; }
        public double AgainstYouMax { get; }
        public double ParryShare { get; }
        public double AiShare { get; }

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Switch ? "TimedBlocksEnabled" : null;

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            if (!Enabled) return "timed blocks OFF (" + OffBecause + ")";
            return "timed blocks ON: perfect within " + Perfect.ToString("0.00", c) + " s, none after " + Window.ToString("0.00", c) + " s; a perfect block's riposte "
                   + (100 * RiposteMax).ToString("0", c) + "%, " + StaggerMax.ToString("0.0", c) + " s (on you at most " + AgainstYouMax.ToString("0.0", c) + " s); a parry counts "
                   + (100 * ParryShare).ToString("0", c) + "%, the AI's timing " + (100 * AiShare).ToString("0", c) + "%";
        }

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }

    /// <summary>
    /// Step 100b: one timed block's result - THE SEAM the later parts hook (100c riders, 100d the push-out, 100e the slow-mo): who blocked,
    /// how sharp (the score and its band), from what read, and what the riposte made of it (step 56's chance / length beside the timed ones).
    /// </summary>
    public readonly struct TimedBlockResult
    {
        public TimedBlockResult(in ImpactDecision d)
        {
            Event = d.Event;
            Score = d.TimedScore;
            Band = TimedBlockMath.Band(d.TimedScore);
            Source = d.TimedSource;
            Age = d.TimedAge;
            BlockerIsPlayer = d.DefenderIsPlayer;
            AttackerIsPlayer = d.AttackerIsPlayer;
            AttackerMounted = d.AttackerMounted;
            RiposteRolled = d.Riposte.Rolled;
            RiposteHappens = d.Riposte.Happens;
            Chance56 = d.RiposteChance56;
            Chance = d.Riposte.InRow ? d.Riposte.Chance : double.NaN;
            Seconds56 = d.RiposteStun56;
            Seconds = d.RiposteStunSeconds;
            CappedForYou = d.TimedCappedForYou;
        }

        public ImpactEvent Event { get; }
        public double Score { get; }
        public int Band { get; }
        public TimingSource Source { get; }
        public double Age { get; }
        public bool BlockerIsPlayer { get; }
        public bool AttackerIsPlayer { get; }
        public bool AttackerMounted { get; }
        public bool RiposteRolled { get; }
        public bool RiposteHappens { get; }

        /// <summary>Step 56's riposte chance (before the blend - after the halving and the limits) and the chance rolled (NaN = no row).</summary>
        public double Chance56 { get; }
        public double Chance { get; }

        /// <summary>Step 56's riposte length and the length asked (NaN = no riposte).</summary>
        public double Seconds56 { get; }
        public double Seconds { get; }

        /// <summary>The timed length was held at <c>TimedBlockAgainstYouMaxSeconds</c> (the attacker is you).</summary>
        public bool CappedForYou { get; }

        /// <summary>A timing above 0 (a timed block at all).</summary>
        public bool Timed => Score > 0;
    }

    /// <summary>
    /// Step 100b (AI_NOTES "Step 100" §1-§2, DESIGN decision 91): the timed block's pure math - the score from the guard's age, the shares, the
    /// riposte's chance and length blended from step 56's toward the timed ceilings, the bands the summary counts in.
    /// </summary>
    public static class TimedBlockMath
    {
        public const int Bands = 5;
        public const int AgeBands = 6;

        /// <summary>The game's <c>BannerlordConfig.DefendDirectionControl</c> value for the AUTOMATIC block direction.</summary>
        public const int DefendDirectionAutomatic = 2;

        /// <summary>The engine's defend direction flags (<c>Agent.MovementControlFlag</c> DefendLeft / Right / Up / Down - the smoke checks them).</summary>
        public const int DefendFlagMask = 0x400 | 0x800 | 0x1000 | 0x2000;

        /// <summary>The engine's <c>Agent.ActionStage</c> Defend (passive) and DefendParry (active and the parry reactions).</summary>
        public const int StageDefend = 5;
        public const int StageDefendParry = 6;

        /// <summary>smoothstep: 0 at 0, 1 at 1, flat at both ends.</summary>
        public static double Smooth(double t)
        {
            if (!(t > 0)) return 0;
            if (t >= 1) return 1;
            return t * t * (3 - 2 * t);
        }

        /// <summary>
        /// THE timing score from the guard's age (s): 1 at or below <paramref name="perfect"/>, 0 at or beyond <paramref name="window"/>, a
        /// smoothstep fall between; NaN / negative / infinite age = 0; a window not above the perfect = a step at the perfect.
        /// </summary>
        public static double Score(double age, double perfect, double window)
        {
            if (double.IsNaN(age) || double.IsInfinity(age) || age < 0) return 0;
            if (age <= perfect) return 1;
            if (window <= perfect || age >= window) return 0;
            return 1 - Smooth((age - perfect) / (window - perfect));
        }

        /// <summary>The guard key from the engine's reads: no guard = 0; a defend stage or a defend flag = the flag's direction bits (or
        /// <see cref="GuardClock.UpNoDirection"/>).</summary>
        public static int GuardKey(int defendFlag, int stage)
        {
            int dir = defendFlag & DefendFlagMask;
            bool up = dir != 0 || stage == StageDefend || stage == StageDefendParry;
            if (!up) return 0;
            return dir != 0 ? dir : GuardClock.UpNoDirection;
        }

        /// <summary>
        /// The block's score s 0-1 (the decision's): 0 unless the rules are on and the event is a CORRECT block (right-side shield / parry) with
        /// a source; a chamber 1, else <see cref="Score"/> of the age; × <c>TimedBlockAiPercent</c> for an AI blocker, × <c>TimedBlockParryPercent</c>
        /// for a parry.
        /// </summary>
        public static double ScoreOf(in TimedBlockRules r, ImpactEvent e, TimingSource source, double age, bool blockerIsPlayer)
        {
            if (!r.Enabled || !ImpactMath.CanRiposte(e) || source == TimingSource.None) return 0;
            double s = source == TimingSource.Chamber ? 1 : source == TimingSource.AiHeld ? 0 : Score(age, r.Perfect, r.Window);
            if (!blockerIsPlayer) s *= r.AiShare;
            if (e == ImpactEvent.Parry) s *= r.ParryShare;
            return Math.Max(0, Math.Min(1, s));
        }

        /// <summary>The riposte's raw chance blended from step 56's toward the timed ceiling: s 0 (or less) = <paramref name="p56"/> EXACTLY;
        /// never below it.</summary>
        public static double Chance(double p56, double max, double s)
        {
            if (!(s > 0)) return p56;
            s = Math.Min(1, s);
            return p56 + Math.Max(0, max - p56) * s;
        }

        /// <summary>
        /// The riposte's length blended from step 56's <paramref name="l56"/> toward <paramref name="max"/> by s, never beyond the stun cap and,
        /// when the attacker is YOU, never beyond <paramref name="youMax"/> - but never shorter than step 56's own; s 0 = <paramref name="l56"/>
        /// EXACTLY. <paramref name="cappedForYou"/> = the you-cap held it back.
        /// </summary>
        public static double Length(double l56, double s, double max, double stunCap, bool toYou, double youMax, out bool cappedForYou)
        {
            cappedForYou = false;
            if (!(s > 0) || double.IsNaN(l56)) return l56;
            s = Math.Min(1, s);
            double target = Math.Min(Math.Max(0, max), Math.Max(0, stunCap));
            if (toYou && target > youMax)
            {
                target = youMax;
                cappedForYou = true;
            }
            if (target <= l56)
            {
                cappedForYou = false;
                return l56;
            }
            return l56 + (target - l56) * s;
        }

        /// <summary>The summary's score band: 0 = none, 1 = (0, 0.25), 2 = [0.25, 0.5), 3 = [0.5, 0.8), 4 = [0.8, 1].</summary>
        public static int Band(double s)
        {
            if (!(s > 0)) return 0;
            if (s < 0.25) return 1;
            if (s < 0.5) return 2;
            if (s < 0.8) return 3;
            return 4;
        }

        public static string BandName(int band) => band switch
        {
            0 => "0",
            1 => "0-0.25",
            2 => "0.25-0.5",
            3 => "0.5-0.8",
            _ => "0.8-1",
        };

        /// <summary>The guard's age band (the verdict table's): 0 none read, 1 ≤ 0.15 s, 2 ≤ 0.3, 3 ≤ 0.5, 4 ≤ 1, 5 longer.</summary>
        public static int AgeBand(double age)
        {
            if (double.IsNaN(age) || age < 0) return 0;
            if (age <= 0.15) return 1;
            if (age <= 0.3) return 2;
            if (age <= 0.5) return 3;
            if (age <= 1) return 4;
            return 5;
        }

        public static string AgeBandName(int band) => band switch
        {
            0 => "no clock",
            1 => "≤0.15 s",
            2 => "0.15-0.3 s",
            3 => "0.3-0.5 s",
            4 => "0.5-1 s",
            _ => ">1 s",
        };

        /// <summary>The engine's collision verdict (<see cref="BlockMath.ResultBlocked"/> / Parried / ChamberBlocked).</summary>
        public static BlockVerdict VerdictOf(int collisionResult) => collisionResult switch
        {
            BlockMath.ResultBlocked => BlockVerdict.Blocked,
            BlockMath.ResultParried => BlockVerdict.Parried,
            BlockMath.ResultChamberBlocked => BlockVerdict.Chamber,
            _ => BlockVerdict.Other,
        };

        public static string SourceName(TimingSource s) => s switch
        {
            TimingSource.PlayerClock => "your clock",
            TimingSource.AiActive => "an active defend at contact",
            TimingSource.AiHeld => "a passive (held) defend",
            TimingSource.Chamber => "a chamber",
            _ => "unknown",
        };

        public static string ReadName(GuardRead g) => g switch
        {
            GuardRead.Active => "active",
            GuardRead.Passive => "passive",
            GuardRead.ParryReaction => "parry reaction",
            GuardRead.OtherDefend => "other defend",
            GuardRead.NotDefend => "not a defend",
            _ => "not read",
        };

        /// <summary>The timing in words for a YOU line ("0.08 s, perfect").</summary>
        public static string TimingWords(in TimedBlockResult t)
        {
            var c = CultureInfo.InvariantCulture;
            string sharp = t.Score >= 0.999 ? "perfect" : t.Score >= 0.8 ? "sharp" : t.Score >= 0.5 ? "good" : t.Score > 0 ? "late" : "untimed";
            if (t.Source == TimingSource.Chamber) return "a chamber, " + sharp;
            return (double.IsNaN(t.Age) || double.IsInfinity(t.Age) ? "held" : t.Age.ToString("0.00", c) + " s") + ", " + sharp + ", score " + t.Score.ToString("0.00", c);
        }
    }

    /// <summary>
    /// Step 100b: one mission's timed blocks - every CORRECT block decided by its score band (shield / parry, you / the AI), the source of its
    /// timing, YOUR guard ages, the engine's verdict × the channel-1 read at contact × your clock's age band (the H / H' / C1 answer), the
    /// active defends' lengths, the ripostes by band (decided / applied / seen, asked → measured), refusals and the you-cap. The [summary] lines.
    /// Main thread only.
    /// </summary>
    public sealed class TimedBlockStats
    {
        private const int Kinds = 2; // 0 shield (right side), 1 parry
        private const int Who = 2; // 0 you, 1 the AI
        private const int Verdicts = 4;
        private const int Reads = 6;
        private const int MaxAges = 512;

        public readonly int[,,] ByBand = new int[Kinds, Who, TimedBlockMath.Bands];
        public readonly double[] ScoreSum = new double[Who];
        public readonly int[] Scored = new int[Who];
        public int Chambers;
        public readonly int[] BySource = new int[5];

        private readonly double[] _ages = new double[MaxAges];
        private int _ageCount;
        public int YourBlocks;
        public double YourBest = double.NaN;

        public readonly int[,,] Verdict = new int[Verdicts, Reads, TimedBlockMath.AgeBands];

        public int ActiveLengths;
        public double ActiveMin = double.NaN, ActiveMax = double.NaN, ActiveSum;
        public int ActiveCyclic, ActiveContinues;

        public readonly int[] RiposteRolled = new int[TimedBlockMath.Bands];
        public readonly int[] RiposteDecided = new int[TimedBlockMath.Bands];
        public readonly int[] RiposteApplied = new int[TimedBlockMath.Bands];
        public readonly int[] RiposteSeen = new int[TimedBlockMath.Bands];
        public readonly double[] AskedSum = new double[TimedBlockMath.Bands];
        public readonly double[] MeasuredSum = new double[TimedBlockMath.Bands];
        public readonly int[] Measured = new int[TimedBlockMath.Bands];
        public readonly int[] RipostesBy = new int[Who];
        public readonly double[] RiposteSecondsBy = new double[Who];
        public double ChanceSum56, ChanceSumTimed;
        public int TimedRolls;
        public int RefusedImmune, RefusedDown, RefusedReplaced, RefusedRider, Halved, CappedForYou;

        /// <summary>One CORRECT block decided (the module's seam, after <see cref="ImpactMath.Decide"/>).</summary>
        public void Note(in TimedBlockResult t, in ImpactDecision d)
        {
            if (!ImpactMath.CanRiposte(t.Event)) return;
            int k = t.Event == ImpactEvent.Parry ? 1 : 0;
            int w = t.BlockerIsPlayer ? 0 : 1;
            ByBand[k, w, t.Band]++;
            if (t.Source != TimingSource.None)
            {
                Scored[w]++;
                ScoreSum[w] += t.Score;
            }
            BySource[(int)t.Source]++;
            if (t.Source == TimingSource.Chamber) Chambers++;
            if (t.Source == TimingSource.PlayerClock && t.BlockerIsPlayer)
            {
                YourBlocks++;
                if (!double.IsNaN(t.Age))
                {
                    if (_ageCount < MaxAges) _ages[_ageCount++] = t.Age;
                    if (double.IsNaN(YourBest) || t.Age < YourBest) YourBest = t.Age;
                }
            }
            if (t.RiposteRolled) RiposteRolled[t.Band]++;
            if (t.RiposteHappens)
            {
                RiposteDecided[t.Band]++;
                RipostesBy[w]++;
                if (!double.IsNaN(t.Seconds)) RiposteSecondsBy[w] += t.Seconds;
            }
            if (t.Timed && d.Riposte.InRow)
            {
                TimedRolls++;
                if (!double.IsNaN(t.Chance56)) ChanceSum56 += t.Chance56;
                ChanceSumTimed += d.Riposte.Chance;
                if (d.Riposte.Halvings > 0) Halved++;
                switch (d.Riposte.Refusal)
                {
                    case ImpactRefusal.StaggerImmune: RefusedImmune++; break;
                    case ImpactRefusal.Down: RefusedDown++; break;
                    case ImpactRefusal.Replaced: RefusedReplaced++; break;
                    case ImpactRefusal.Rider: RefusedRider++; break;
                }
            }
            if (t.CappedForYou && t.RiposteHappens) CappedForYou++;
        }

        /// <summary>The diagnostic: the engine's verdict × the channel-1 read at contact × your clock's age band (0 = not your clock).</summary>
        public void NoteVerdict(BlockVerdict v, GuardRead read, double yourAge, bool yours)
        {
            Verdict[(int)v, (int)read, yours ? TimedBlockMath.AgeBand(yourAge) : 0]++;
        }

        /// <summary>An active defend's animation length (read once per action set and action), cyclic, continues to another action.</summary>
        public void NoteActiveLength(double seconds, bool cyclic, bool continues)
        {
            if (double.IsNaN(seconds)) return;
            ActiveLengths++;
            ActiveSum += seconds;
            if (double.IsNaN(ActiveMin) || seconds < ActiveMin) ActiveMin = seconds;
            if (double.IsNaN(ActiveMax) || seconds > ActiveMax) ActiveMax = seconds;
            if (cyclic) ActiveCyclic++;
            if (continues) ActiveContinues++;
        }

        public void NoteApplied(double score) => RiposteApplied[TimedBlockMath.Band(score)]++;

        public void NoteSeen(double score) => RiposteSeen[TimedBlockMath.Band(score)]++;

        public void NoteMeasured(double score, double asked, double measured)
        {
            int b = TimedBlockMath.Band(score);
            Measured[b]++;
            AskedSum[b] += asked;
            MeasuredSum[b] += measured;
        }

        /// <summary>Your guard ages' median (NaN = none).</summary>
        public double YourMedian()
        {
            if (_ageCount == 0) return double.NaN;
            var a = new double[_ageCount];
            Array.Copy(_ages, a, _ageCount);
            Array.Sort(a);
            return _ageCount % 2 == 1 ? a[_ageCount / 2] : 0.5 * (a[_ageCount / 2 - 1] + a[_ageCount / 2]);
        }

        public bool Any
        {
            get
            {
                foreach (var n in ByBand) if (n > 0) return true;
                return false;
            }
        }

        public string[] SummaryLines(in TimedBlockRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var lines = new List<string>();
            var t = new StringBuilder();
            t.Append("timed blocks (step 100b; ").Append(r.Enabled ? "ON" : "OFF (" + r.OffBecause + ")").Append(") - correct blocks by timing 0 / 0-0.25 / 0.25-0.5 / 0.5-0.8 / 0.8-1: ");
            string[] kind = { "shield", "parry" };
            string[] who = { "YOU", "AI" };
            for (int w = 0; w < Who; w++)
            {
                if (w > 0) t.Append(" | ");
                t.Append(who[w]).Append(": ");
                for (int k = 0; k < Kinds; k++)
                {
                    if (k > 0) t.Append(", ");
                    t.Append(kind[k]).Append(' ');
                    for (int b = 0; b < TimedBlockMath.Bands; b++) t.Append(b > 0 ? "/" : string.Empty).Append(ByBand[k, w, b].ToString(c));
                }
                t.Append(", mean score ").Append(Scored[w] > 0 ? (ScoreSum[w] / Scored[w]).ToString("0.00", c) : "-");
            }
            t.Append("; chambers ").Append(Chambers.ToString(c)).Append("; the timing's source: your clock ").Append(BySource[(int)TimingSource.PlayerClock].ToString(c))
             .Append(", an active defend at contact ").Append(BySource[(int)TimingSource.AiActive].ToString(c)).Append(", a held (passive) defend ")
             .Append(BySource[(int)TimingSource.AiHeld].ToString(c)).Append(", a chamber ").Append(BySource[(int)TimingSource.Chamber].ToString(c))
             .Append(", unknown ").Append(BySource[(int)TimingSource.None].ToString(c));
            double med = YourMedian();
            t.Append("; YOU: ").Append(YourBlocks.ToString(c)).Append(" blocks on your clock, median guard age ")
             .Append(double.IsNaN(med) ? "-" : (1000 * med).ToString("0", c) + " ms").Append(", best ").Append(double.IsNaN(YourBest) ? "-" : (1000 * YourBest).ToString("0", c) + " ms");
            lines.Add(t.ToString());

            var v = new StringBuilder();
            v.Append("timed blocks - the engine's verdict x the channel-1 read at contact (x your clock's age band) - PLAYTEST D64: is an active guard 'Parried' and a held one 'Blocked'? ");
            string[] vn = { "Blocked", "Parried", "Chamber", "other" };
            bool anyV = false;
            for (int vi = 0; vi < Verdicts; vi++)
            {
                for (int ri = 0; ri < Reads; ri++)
                {
                    int sum = 0;
                    for (int ai = 0; ai < TimedBlockMath.AgeBands; ai++) sum += Verdict[vi, ri, ai];
                    if (sum == 0) continue;
                    if (anyV) v.Append(" | ");
                    anyV = true;
                    v.Append(vn[vi]).Append(" + ").Append(TimedBlockMath.ReadName((GuardRead)ri)).Append(' ').Append(sum.ToString(c));
                    var ages = new StringBuilder();
                    for (int ai = 1; ai < TimedBlockMath.AgeBands; ai++)
                        if (Verdict[vi, ri, ai] > 0) ages.Append(ages.Length > 0 ? ", " : string.Empty).Append(TimedBlockMath.AgeBandName(ai)).Append(' ').Append(Verdict[vi, ri, ai].ToString(c));
                    if (ages.Length > 0) v.Append(" (yours: ").Append(ages).Append(')');
                }
            }
            if (!anyV) v.Append("no correct block read");
            v.Append("; the active defends' animation length ").Append(ActiveLengths > 0
                ? (ActiveSum / ActiveLengths).ToString("0.00", c) + " s mean (" + ActiveMin.ToString("0.00", c) + "-" + ActiveMax.ToString("0.00", c) + ", " + ActiveLengths.ToString(c)
                  + " actions; cyclic " + ActiveCyclic.ToString(c) + ", continue to another action " + ActiveContinues.ToString(c) + ")"
                : "not read");
            lines.Add(v.ToString());

            var p = new StringBuilder();
            p.Append("timed ripostes by timing 0 / 0-0.25 / 0.25-0.5 / 0.5-0.8 / 0.8-1: rolled ").Append(Join(RiposteRolled, c)).Append(", decided ").Append(Join(RiposteDecided, c))
             .Append(", applied ").Append(Join(RiposteApplied, c)).Append(", seen ").Append(Join(RiposteSeen, c)).Append("; stagger asked → measured: ");
            for (int b = 0; b < TimedBlockMath.Bands; b++)
            {
                if (b > 0) p.Append(" / ");
                p.Append(Measured[b] > 0 ? (AskedSum[b] / Measured[b]).ToString("0.00", c) + "→" + (MeasuredSum[b] / Measured[b]).ToString("0.00", c) + " s" : "-");
            }
            p.Append(" (targets ").Append(r.StaggerMax.ToString("0.0", c)).Append(" s, ").Append(r.AgainstYouMax.ToString("0.0", c)).Append(" s on you); timed rolls ")
             .Append(TimedRolls.ToString(c)).Append(" - mean chance ").Append(TimedRolls > 0 ? Pct(ChanceSum56 / TimedRolls) + " untimed → " + Pct(ChanceSumTimed / TimedRolls) + " timed" : "-")
             .Append(", halved ").Append(Halved.ToString(c)).Append(", refused: immune ").Append(RefusedImmune.ToString(c)).Append(", down ").Append(RefusedDown.ToString(c))
             .Append(", replaced ").Append(RefusedReplaced.ToString(c)).Append(", a rider ").Append(RefusedRider.ToString(c)).Append(" (100c); held at the you-cap ")
             .Append(CappedForYou.ToString(c)).Append("; ripostes by YOUR blocks ").Append(RipostesBy[0].ToString(c)).Append(" (mean ")
             .Append(RipostesBy[0] > 0 ? (RiposteSecondsBy[0] / RipostesBy[0]).ToString("0.00", c) + " s" : "-").Append("), by the AI's ").Append(RipostesBy[1].ToString(c))
             .Append(" (mean ").Append(RipostesBy[1] > 0 ? (RiposteSecondsBy[1] / RipostesBy[1]).ToString("0.00", c) + " s" : "-").Append(')');
            lines.Add(p.ToString());
            return lines.ToArray();
        }

        private static string Join(int[] a, CultureInfo c)
        {
            var s = new StringBuilder();
            for (int i = 0; i < a.Length; i++) s.Append(i > 0 ? "/" : string.Empty).Append(a[i].ToString(c));
            return s.ToString();
        }

        private static string Pct(double v) => double.IsNaN(v) ? "-" : (100 * v).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }
}
