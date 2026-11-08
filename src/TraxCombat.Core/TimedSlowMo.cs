using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 100e (Toni 2026-11-08: "Maybe even try a slow mo when the player does it nicely"; AI_NOTES "Step 100" §5, DESIGN decision 91 (e)):
    /// the slow motion's settings, read live. <see cref="Enabled"/> = the master switch FIRST, then Athletics and the impact system (the timed
    /// riposte lives there), then <c>TimedBlocksEnabled</c>, then <c>TimedBlockSlowMo</c>.
    /// </summary>
    public readonly struct TimedSlowMoRules
    {
        public TimedSlowMoRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            ImpactOn = s.AthleticsEnabled && s.ImpactEnabled;
            TimedOn = s.TimedBlocksEnabled;
            Switch = s.TimedBlockSlowMo;
            MinScore = Clamp01(F(s.TimedBlockSlowMoMinScore));
            Speed = Math.Max(TimedSlowMoMath.MinSpeed, Math.Min(1, Nan0(F(s.TimedBlockSlowMoSpeed))));
            RealSeconds = Math.Max(TimedSlowMoMath.MinRealSeconds, Nan0(F(s.TimedBlockSlowMoRealSeconds)));
            Cooldown = Math.Max(0, Nan0(F(s.TimedBlockSlowMoCooldownSeconds)));
        }

        public static TimedSlowMoRules From(TraxSettings s) => new TimedSlowMoRules(s);

        public bool ModEnabled { get; }
        public bool ImpactOn { get; }
        public bool TimedOn { get; }
        public bool Switch { get; }
        public bool Enabled => ModEnabled && ImpactOn && TimedOn && Switch;

        /// <summary>The timing score your block needs (0-1).</summary>
        public double MinScore { get; }

        /// <summary>The game speed at the slow motion's deepest (0.05-1).</summary>
        public double Speed { get; }

        /// <summary>The whole slow motion in REAL seconds, the ease back included.</summary>
        public double RealSeconds { get; }

        /// <summary>REAL seconds from one slow motion's start to the next.</summary>
        public double Cooldown { get; }

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !ImpactOn ? "the impact system off" : !TimedOn ? "TimedBlocksEnabled" : !Switch ? "TimedBlockSlowMo" : null;

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            if (!Enabled) return "slow motion OFF (" + OffBecause + ")";
            return "slow motion ON: your timed block scoring " + MinScore.ToString("0.00", c) + "+ whose riposte the engine took - the game at "
                   + Speed.ToString("0.00", c) + " speed, back to 1 over " + RealSeconds.ToString("0.00", c) + " real s, at most one every "
                   + Cooldown.ToString("0.0", c) + " real s";
        }

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));

        private static double Nan0(double v) => double.IsNaN(v) ? 0 : v;

        /// <summary>A float setting as the decimal it was written as (0.8f is 0.80000001 - a score of exactly 0.8 must pass).</summary>
        private static double F(float v) => float.IsNaN(v) ? double.NaN : Math.Round((double)v, 5);
    }

    /// <summary>Step 100e: why a timed riposte did NOT start a slow motion (<see cref="Start"/> = it did). In the order asked.</summary>
    public enum SlowMoSkip : byte
    {
        Start,

        /// <summary>The rules off (the master switch, the impact system, timed blocks, the slow-mo switch).</summary>
        Off,

        /// <summary>The blocker is not you (the AI's timed blocks never slow the game).</summary>
        NotYou,

        /// <summary>Your block's timing below <c>TimedBlockSlowMoMinScore</c>.</summary>
        LowScore,

        /// <summary>A multiplayer session (the game ignores time speed there).</summary>
        Multiplayer,

        /// <summary>The game paused (a menu).</summary>
        Paused,

        /// <summary>One of ours is already playing.</summary>
        Playing,

        /// <summary>Within the cooldown of the last one (real seconds).</summary>
        Cooldown,

        /// <summary>Another time-speed request is active (the order menu's slow-down, the radial / equip menus, another mod): we never fight it.</summary>
        OtherRequest,
    }

    /// <summary>Step 100e: how a slow motion ended - every way gives the request back.</summary>
    public enum SlowMoEnd : byte
    {
        /// <summary>The ease back reached 1.</summary>
        Finished,

        /// <summary>The switch, timed blocks, the impact system or the master switch went off.</summary>
        SwitchedOff,

        /// <summary>The game paused (a menu) - or a frame gap long enough to be one.</summary>
        Paused,

        /// <summary>Another time-speed request came in under ours - it wins, we leave.</summary>
        OtherRequest,

        /// <summary>You are no longer on the field (down, dead, a camera mode without you).</summary>
        YouGone,

        /// <summary>The mission ended / the behaviour was removed.</summary>
        MissionEnd,

        /// <summary>A caught error (the slow motion is disabled for the rest of the battle).</summary>
        Error,
    }

    /// <summary>
    /// Step 100e: the slow motion's pure math - the speed profile over REAL time, the trigger decision, the "is someone else slowing the game"
    /// test. Real time is the engine's application clock (it never slows); the mission's dt is already scaled by the speed we ask.
    /// </summary>
    public static class TimedSlowMoMath
    {
        /// <summary>Our time-speed request id (vanilla uses 624 equip / 813 radial / 864 order menu / 1121 the developer slow-mo).</summary>
        public const int RequestId = 31100;

        /// <summary>The vanilla request ids we know of - any of them present = someone else is slowing the game.</summary>
        public static readonly int[] VanillaRequestIds = { 624, 813, 864, 1121 };

        /// <summary>The ease IN to the floor, real seconds (at most a tenth of the whole).</summary>
        public const double EaseInSeconds = 0.05;

        public const double MinSpeed = 0.05;
        public const double MinRealSeconds = 0.1;

        /// <summary>A real-time gap between two of our ticks this long = the game was paused / stalled: the slow motion ends.</summary>
        public const double StallSeconds = 0.25;

        /// <summary>A scene speed this far below the one we asked = another request is lower than ours.</summary>
        public const double SpeedTolerance = 0.01;

        /// <summary>
        /// The game speed <paramref name="t"/> real seconds after the start: 1 before 0; a smoothstep down to <paramref name="floor"/> over the ease in
        /// (min(0.05, total / 10)); then a smoothstep back up to 1 at <paramref name="total"/> - flat at both ends, so the floor is held a while and the
        /// return is gentle; 1 from <paramref name="total"/> on. Never below the floor, never above 1, continuous.
        /// </summary>
        public static double Speed(double t, double floor, double total)
        {
            if (double.IsNaN(t) || t <= 0) return 1;
            floor = double.IsNaN(floor) ? 1 : Math.Max(0, Math.Min(1, floor));
            if (!(total > 0) || t >= total) return 1;
            double ein = Math.Min(EaseInSeconds, total / 10);
            if (t < ein) return 1 - (1 - floor) * TimedBlockMath.Smooth(t / ein);
            return floor + (1 - floor) * TimedBlockMath.Smooth((t - ein) / (total - ein));
        }

        /// <summary>The slow motion is over at <paramref name="t"/> real seconds.</summary>
        public static bool Done(double t, double total) => !(t < total);

        /// <summary>The mission time a whole slow motion lasts (the integral of <see cref="Speed"/> - for the log and the tests).</summary>
        public static double MissionSeconds(double floor, double total, int steps = 400)
        {
            if (!(total > 0)) return 0;
            double h = total / steps, sum = 0;
            for (int i = 0; i < steps; i++) sum += Speed((i + 0.5) * h, floor, total) * h;
            return sum;
        }

        /// <summary>
        /// THE trigger: an accepted timed riposte starts a slow motion only for YOUR block scoring at least the minimum, single player, not
        /// paused, none playing, past the cooldown (real seconds since the last start; NaN = never), and nobody else slowing the game.
        /// </summary>
        public static SlowMoSkip Decide(in TimedSlowMoRules r, bool blockerIsYou, double score, bool multiplayer, bool paused, bool playing,
            double nowReal, double lastStartReal, bool otherRequest)
        {
            if (!r.Enabled) return SlowMoSkip.Off;
            if (!blockerIsYou) return SlowMoSkip.NotYou;
            if (double.IsNaN(score) || score <= 0 || score < r.MinScore) return SlowMoSkip.LowScore;
            if (multiplayer) return SlowMoSkip.Multiplayer;
            if (paused) return SlowMoSkip.Paused;
            if (playing) return SlowMoSkip.Playing;
            if (!double.IsNaN(lastStartReal) && nowReal - lastStartReal < r.Cooldown) return SlowMoSkip.Cooldown;
            if (otherRequest) return SlowMoSkip.OtherRequest;
            return SlowMoSkip.Start;
        }

        /// <summary>Someone else slows the game: a known vanilla request is present, or the scene runs slower than the speed WE asked last
        /// (1 when we ask nothing) by more than <see cref="SpeedTolerance"/>.</summary>
        public static bool OtherSlowing(bool vanillaRequestPresent, double sceneSpeed, double ourAsked)
        {
            if (vanillaRequestPresent) return true;
            if (double.IsNaN(sceneSpeed)) return false;
            if (double.IsNaN(ourAsked)) ourAsked = 1;
            return sceneSpeed < Math.Min(1, ourAsked) - SpeedTolerance;
        }

        public static string SkipName(SlowMoSkip s) => s switch
        {
            SlowMoSkip.Start => "started",
            SlowMoSkip.Off => "off",
            SlowMoSkip.NotYou => "not your block",
            SlowMoSkip.LowScore => "timing below the minimum",
            SlowMoSkip.Multiplayer => "multiplayer",
            SlowMoSkip.Paused => "paused",
            SlowMoSkip.Playing => "one already playing",
            SlowMoSkip.Cooldown => "the cooldown",
            SlowMoSkip.OtherRequest => "another slow-down active",
            _ => s.ToString(),
        };

        public static string EndName(SlowMoEnd e) => e switch
        {
            SlowMoEnd.Finished => "finished",
            SlowMoEnd.SwitchedOff => "switched off",
            SlowMoEnd.Paused => "paused",
            SlowMoEnd.OtherRequest => "another slow-down came in",
            SlowMoEnd.YouGone => "you left the field",
            SlowMoEnd.MissionEnd => "the mission ended",
            SlowMoEnd.Error => "an error",
            _ => e.ToString(),
        };
    }

    /// <summary>
    /// Step 100e: one battle's slow motion - at most one playing. <see cref="Asked"/> = the speed our request holds now (NaN = no request of ours in
    /// the game: the module mirrors it exactly - every Add / Remove goes through here). Main thread.
    /// </summary>
    public sealed class TimedSlowMo
    {
        public bool Playing { get; private set; }
        public double StartReal { get; private set; } = double.NaN;
        public double LastStartReal { get; private set; } = double.NaN;
        public double LastTickReal { get; private set; } = double.NaN;
        public double Asked { get; private set; } = double.NaN;
        public double Score { get; private set; }

        /// <summary>A caught error disabled the slow motion for the rest of the battle.</summary>
        public bool Disabled { get; set; }

        public void Start(double nowReal, double score)
        {
            Playing = true;
            StartReal = nowReal;
            LastStartReal = nowReal;
            LastTickReal = nowReal;
            Score = score;
        }

        /// <summary>The speed to ask now (the module sets it as our request); NaN = the slow motion is over (give the request back).</summary>
        public double Step(double nowReal, in TimedSlowMoRules r)
        {
            if (!Playing) return double.NaN;
            LastTickReal = nowReal;
            double t = nowReal - StartReal;
            if (TimedSlowMoMath.Done(t, r.RealSeconds)) return double.NaN;
            return TimedSlowMoMath.Speed(t, r.Speed, r.RealSeconds);
        }

        /// <summary>The real gap since our last tick of this slow motion is a stall / pause.</summary>
        public bool Stalled(double nowReal) => Playing && !double.IsNaN(LastTickReal) && nowReal - LastTickReal > TimedSlowMoMath.StallSeconds;

        /// <summary>Our request now holds <paramref name="speed"/> (NaN = removed).</summary>
        public void NoteAsked(double speed) => Asked = speed;

        public void Stop()
        {
            Playing = false;
            StartReal = double.NaN;
        }
    }

    /// <summary>Step 100e: the slow motion over one battle - the [summary] "timed slow-mo" line.</summary>
    public sealed class TimedSlowMoStats
    {
        public const int Skips = 9;
        public const int Ends = 7;

        public readonly int[] Skipped = new int[Skips];
        public readonly int[] Ended = new int[Ends];
        public int Played;
        public double RealSum, ScoreSum, LowestAsked = double.NaN;
        public int RemoveRefused, Errors;

        /// <summary>Requests of ours still in the game after the mission end's give-back - must be 0.</summary>
        public int LeftOver;

        /// <summary>The mission end's give-back looked (the line says so - a battle torn down without it says "not checked").</summary>
        public bool CheckedAtEnd;

        public void NoteSkip(SlowMoSkip s)
        {
            if (s == SlowMoSkip.Start) Played++;
            else Skipped[(int)s]++;
        }

        public void NoteStart(double score) => ScoreSum += score;

        public void NoteAsked(double speed)
        {
            if (double.IsNaN(speed)) return;
            if (double.IsNaN(LowestAsked) || speed < LowestAsked) LowestAsked = speed;
        }

        public void NoteEnd(SlowMoEnd e, double realSeconds)
        {
            Ended[(int)e]++;
            if (!double.IsNaN(realSeconds) && realSeconds >= 0) RealSum += realSeconds;
        }

        public int EndedTotal
        {
            get
            {
                int n = 0;
                foreach (int e in Ended) n += e;
                return n;
            }
        }

        public string SummaryLine(in TimedSlowMoRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.Append("timed slow-mo (step 100e; ").Append(r.Enabled ? "ON" : "OFF (" + r.OffBecause + ")").Append("): played ").Append(Played.ToString(c));
            int ended = EndedTotal;
            t.Append(" (mean real ").Append(ended > 0 ? (RealSum / ended).ToString("0.00", c) + " s" : "-").Append(", mean score ")
             .Append(Played > 0 ? (ScoreSum / Played).ToString("0.00", c) : "-").Append(", the deepest speed asked ")
             .Append(double.IsNaN(LowestAsked) ? "-" : LowestAsked.ToString("0.00", c)).Append("); skipped:");
            bool any = false;
            for (int i = 1; i < Skips; i++)
            {
                if (Skipped[i] == 0) continue;
                t.Append(any ? ", " : " ").Append(TimedSlowMoMath.SkipName((SlowMoSkip)i)).Append(' ').Append(Skipped[i].ToString(c));
                any = true;
            }
            if (!any) t.Append(" none");
            t.Append("; ended:");
            any = false;
            for (int i = 0; i < Ends; i++)
            {
                if (Ended[i] == 0) continue;
                t.Append(any ? ", " : " ").Append(TimedSlowMoMath.EndName((SlowMoEnd)i)).Append(' ').Append(Ended[i].ToString(c));
                any = true;
            }
            if (!any) t.Append(" none");
            t.Append("; requests left over at the mission end ").Append(CheckedAtEnd ? LeftOver.ToString(c) + " (must be 0)" : "not checked yet");
            if (RemoveRefused > 0) t.Append("; a give-back found no request ").Append(RemoveRefused.ToString(c));
            if (Errors > 0) t.Append("; ERRORS ").Append(Errors.ToString(c)).Append(" (disabled for the battle)");
            return t.ToString();
        }
    }
}
