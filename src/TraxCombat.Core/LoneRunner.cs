using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 70: why a lone runner is out there - the diagnostic's columns, in priority order (the first that holds).</summary>
    public enum LoneCause
    {
        /// <summary>A charge order's or a fever's sprint INTO contact (step 64) is carrying him.</summary>
        SprintIntoContact,

        /// <summary>Step 54's punish: his target is reeling / down and he steps in for it.</summary>
        PunishStepIn,

        /// <summary>Step 49b's surge: a timid man brave for a few seconds.</summary>
        CourageSurge,

        /// <summary>Step 49b's fever press: his fever (no shock) sends him in whatever the odds.</summary>
        FeverPress,

        /// <summary>His AI's target stands behind the enemy's front - he is chasing it.</summary>
        ChasingTarget,

        /// <summary>None of ours - the game's own AI.</summary>
        Game,

        Count,
    }

    /// <summary>Step 70: whom a lone runner's AI target is (when it stands behind the enemy's front).</summary>
    public enum LoneTarget
    {
        None,

        /// <summary>You - the player's own man.</summary>
        Player,

        /// <summary>A hero (a lord, a companion).</summary>
        Hero,

        /// <summary>Anyone else.</summary>
        Other,

        Count,
    }

    /// <summary>
    /// Step 70's lone-runner settings, read live (AI_NOTES "Step 70", DESIGN decision 63): the master switch FIRST, LoneRunnerEnabled (the
    /// fixes - the diagnostic measures either way), LoneRunnerAheadMetres.
    /// </summary>
    public readonly struct LoneRunnerRules
    {
        public LoneRunnerRules(bool modEnabled, bool enabled, double aheadMetres = 4)
        {
            ModEnabled = modEnabled;
            FixEnabled = enabled;
            AheadMetres = double.IsNaN(aheadMetres) || aheadMetres < 0 ? 0 : aheadMetres > 30 ? 30 : aheadMetres;
        }

        public static LoneRunnerRules From(TraxSettings s) => new LoneRunnerRules(s.ModEnabled, s.LoneRunnerEnabled, s.LoneRunnerAheadMetres);

        public bool ModEnabled { get; }

        /// <summary>LoneRunnerEnabled (the switch itself).</summary>
        public bool FixEnabled { get; }

        /// <summary>The master switch FIRST, then LoneRunnerEnabled.</summary>
        public bool Enabled => ModEnabled && FixEnabled;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !FixEnabled ? "LoneRunnerEnabled" : null;

        /// <summary>A man more than this (m) ahead of his own unit's front (toward the enemy) - or past the enemy's front - is OUT.</summary>
        public double AheadMetres { get; }

        public string Describe()
        {
            string what = "a LONE RUNNER (step 70) = an AI man on foot more than " + AheadMetres.ToString("0.#", CultureInfo.InvariantCulture)
                          + " m ahead of his own unit's front or " + LoneRunnerMath.PastFrontMetres.ToString("0.#", CultureInfo.InvariantCulture)
                          + " m past the enemy's front, fewer than " + FearMath.AloneMates + " mates within courage's radius and an enemy within "
                          + LoneRunnerMath.EnemyMetres.ToString("0", CultureInfo.InvariantCulture) + " m";
            return Enabled
                ? what + "; a man alone is no longer sent in by his fever, a charge / fever sprint ends when he is alone or past the enemy's front, and a lone runner "
                  + "walks back to his group (courage's walk) unless he chases a beaten enemy (his side 2 : 1 around him, the enemy near him broken)"
                : what + " - measured only (" + OffBecause + " off)";
        }
    }

    /// <summary>Step 70 pure rules: alone, beyond, the lone runner, the cause, the three fixes.</summary>
    public static class LoneRunnerMath
    {
        /// <summary>"In contact or with an enemy within": courage's scan reach (step 49b's 8 m).</summary>
        public const double EnemyMetres = CourageMath.SurgeEnemyMetres;

        /// <summary>Past the enemy's front by more than this (m) - behind the enemy's line.</summary>
        public const double PastFrontMetres = 1.0;

        /// <summary>A longer gap between two looks = not watched meanwhile (his time out starts again).</summary>
        public const double MaxGapSeconds = 2;

        /// <summary>The sprint reads the last look's "alone" while it is at most this old (s).</summary>
        public const double AloneStaleSeconds = 1.0;

        /// <summary>Alone: fewer than <see cref="FearMath.AloneMates"/> mates in courage's circle, an enemy within <see cref="EnemyMetres"/>.</summary>
        public static bool Alone(int mates, double nearestEnemy) =>
            mates < FearMath.AloneMates && !double.IsNaN(nearestEnemy) && nearestEnemy <= EnemyMetres;

        /// <summary>Out beyond his line: more than AheadMetres ahead of his own unit's front, or past the enemy's front (NaN = unknown).</summary>
        public static bool Beyond(in LoneRunnerRules r, double aheadOfOwn, double pastEnemy) =>
            (!double.IsNaN(aheadOfOwn) && aheadOfOwn > r.AheadMetres) || (!double.IsNaN(pastEnemy) && pastEnemy > PastFrontMetres);

        public static bool IsLone(in LoneRunnerRules r, bool alone, double aheadOfOwn, double pastEnemy) => alone && Beyond(in r, aheadOfOwn, pastEnemy);

        /// <summary>The enemy near him counts as BEATEN at this shaken share (the toughness-weighted morale share on the fear's lines - routing = 1).</summary>
        public const double BeatenShaken = 0.5;

        /// <summary>A lone chaser of the BEATEN is left alone: his side's local odds at FearWinClearOddsPercent or more AND the enemy near him broken
        /// (shaken at least <see cref="BeatenShaken"/>). Courage's odds weigh an enemy at the edge of its radius at a few per cent - the odds alone
        /// would excuse a looter standing 5 m from one legionary.</summary>
        public static bool Winning(double odds, int clearOddsPercent, double enemyShaken) =>
            !double.IsNaN(odds) && odds >= Math.Max(1, clearOddsPercent) / 100.0 && !double.IsNaN(enemyShaken) && enemyShaken >= BeatenShaken;

        /// <summary>The cause, the first that holds.</summary>
        public static LoneCause Cause(bool sprintIntoContact, bool punish, bool surge, bool feverPress, bool chasing) =>
            sprintIntoContact ? LoneCause.SprintIntoContact : punish ? LoneCause.PunishStepIn : surge ? LoneCause.CourageSurge
            : feverPress ? LoneCause.FeverPress : chasing ? LoneCause.ChasingTarget : LoneCause.Game;

        /// <summary>FIX 1: the fever presses against bad odds - but no longer for a man ALONE (courage's isolation decides him).</summary>
        public static bool FeverPresses(in LoneRunnerRules r, bool feverPresses, bool alone) => feverPresses && !(r.Enabled && alone);

        /// <summary>FIX 3: a lone runner walks back to his group (courage's walk, whatever the odds) - unless his side wins clearly around him.</summary>
        public static bool ForcesWalk(in LoneRunnerRules r, bool lone, bool winning) => r.Enabled && lone && !winning;

        /// <summary>FIX 2: a sprint INTO contact ends when he is alone (his mates left behind) or past the enemy's front.</summary>
        public static bool SprintEnds(in LoneRunnerRules r, bool alone, double pastEnemy) =>
            r.Enabled && (alone || (!double.IsNaN(pastEnemy) && pastEnemy > PastFrontMetres));

        public static string CauseName(LoneCause c) => c switch
        {
            LoneCause.SprintIntoContact => "the sprint into contact (step 64)",
            LoneCause.PunishStepIn => "the punish step-in (step 54)",
            LoneCause.CourageSurge => "a courage surge (step 49b)",
            LoneCause.FeverPress => "the fever press (step 49b)",
            LoneCause.ChasingTarget => "chasing a target behind the enemy's front",
            LoneCause.Game => "the game itself",
            _ => c.ToString(),
        };

        public static string TargetName(LoneTarget t) => t switch
        {
            LoneTarget.Player => "you (the player)",
            LoneTarget.Hero => "a hero",
            LoneTarget.Other => "another man",
            _ => "none",
        };
    }

    /// <summary>Step 70: one man's lone-runner watch (on his record; the logic owns it).</summary>
    public sealed class LoneMind
    {
        /// <summary>His last look's time (the seconds between looks).</summary>
        public double LastT = double.NaN;

        /// <summary>The last look: alone (and when), a lone runner, the cause.</summary>
        public bool Alone;
        public double AloneAt = double.NaN;
        public bool Lone;
        public LoneCause Cause = LoneCause.Count;

        /// <summary>The causes / targets he was counted under this battle (a bit each - the summary's men).</summary>
        public int CauseMask, TargetMask;
        public bool Counted;

        /// <summary>A walk back forced by the lone-runner rule runs.</summary>
        public bool Forced;

        /// <summary>The last look said "alone" recently enough for the sprint to use it.</summary>
        public bool AloneNow(double now) => Alone && !double.IsNaN(AloneAt) && now - AloneAt <= LoneRunnerMath.AloneStaleSeconds && now >= AloneAt;

        /// <summary>The seconds since his last look (NaN = first look or too long ago); the time kept.</summary>
        public double Gap(double now)
        {
            double gap = double.IsNaN(LastT) || now <= LastT ? double.NaN : now - LastT;
            LastT = now;
            return gap > 0 && gap <= LoneRunnerMath.MaxGapSeconds ? gap : double.NaN;
        }
    }

    /// <summary>Step 70: the lone-runner diagnostic and its fixes per mission - man-seconds and men by cause (fixes ON / OFF), the targets behind
    /// the enemy's front by whom, the winners left alone, the fixes' counts - the [summary] "lone runners" line.</summary>
    public sealed class LoneRunnerStats
    {
        private const int Sides = 2;
        private readonly double[] _seconds = new double[Sides * (int)LoneCause.Count];
        private readonly int[] _men = new int[(int)LoneCause.Count];
        private readonly double[] _targetSeconds = new double[(int)LoneTarget.Count];
        private readonly int[] _targetMen = new int[(int)LoneTarget.Count];
        private readonly int[] _sideMen = new int[Sides];

        public long Looks;
        public long LoneLooks;
        public double WinningSeconds;
        public long FeverRefusedLooks;
        public int WalksForced;
        public int SurgesEnded;
        public int SprintsEnded;
        public long TargetReads;
        public int Errors;

        /// <summary>One look's time out: <paramref name="seconds"/> under <paramref name="cause"/> (fixes on / off); a target behind the enemy's
        /// front by whom; his first time under a cause / target = one man more.</summary>
        public void Add(LoneMind m, bool on, LoneCause cause, double seconds, LoneTarget behind)
        {
            int c = (int)cause;
            if (c < 0 || c >= (int)LoneCause.Count) return;
            if (seconds > 0) _seconds[(on ? 1 : 0) * (int)LoneCause.Count + c] += seconds;
            int bit = 1 << c;
            if ((m.CauseMask & bit) == 0)
            {
                m.CauseMask |= bit;
                _men[c]++;
            }
            if (!m.Counted)
            {
                m.Counted = true;
                _sideMen[on ? 1 : 0]++;
            }
            int t = (int)behind;
            if (t <= 0 || t >= (int)LoneTarget.Count) return;
            if (seconds > 0) _targetSeconds[t] += seconds;
            int tb = 1 << t;
            if ((m.TargetMask & tb) != 0) return;
            m.TargetMask |= tb;
            _targetMen[t]++;
        }

        public double SecondsOf(LoneCause c, bool on) => _seconds[(on ? 1 : 0) * (int)LoneCause.Count + (int)c];

        public double Seconds(bool on)
        {
            double s = 0;
            for (int c = 0; c < (int)LoneCause.Count; c++) s += SecondsOf((LoneCause)c, on);
            return s;
        }

        public int MenOf(LoneCause c) => _men[(int)c];

        public int Men(bool on) => _sideMen[on ? 1 : 0];

        public double TargetSeconds(LoneTarget t) => _targetSeconds[(int)t];

        public int TargetMen(LoneTarget t) => _targetMen[(int)t];

        private static string N(double v, string f = "0.0") => double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.InvariantCulture);

        public void AddSummaryLines(List<string> lines, in LoneRunnerRules r)
        {
            var sb = new StringBuilder();
            sb.Append("lone runners (step 70 - ").Append(r.Enabled ? "the fixes ON" : "the fixes OFF (" + r.OffBecause + "), measured only").Append("; more than ")
              .Append(N(r.AheadMetres, "0.#")).Append(" m ahead of his unit's front or ").Append(N(LoneRunnerMath.PastFrontMetres, "0.#")).Append(" m past the enemy's front, fewer than ")
              .Append(FearMath.AloneMates).Append(" mates near, an enemy within ").Append(N(LoneRunnerMath.EnemyMetres, "0")).Append(" m): fixes ON ").Append(N(Seconds(true)))
              .Append(" man-s (").Append(Men(true)).Append(" men), OFF ").Append(N(Seconds(false))).Append(" man-s (").Append(Men(false)).Append(" men) - by cause: ");
            for (int c = 0; c < (int)LoneCause.Count; c++)
            {
                if (c > 0) sb.Append(", ");
                var cause = (LoneCause)c;
                sb.Append(LoneRunnerMath.CauseName(cause)).Append(' ').Append(N(SecondsOf(cause, true) + SecondsOf(cause, false))).Append(" man-s (").Append(MenOf(cause)).Append(" men)");
            }
            sb.Append("; his target behind the enemy's front: ");
            for (int t = 1; t < (int)LoneTarget.Count; t++)
            {
                if (t > 1) sb.Append(", ");
                var target = (LoneTarget)t;
                sb.Append(LoneRunnerMath.TargetName(target)).Append(' ').Append(N(TargetSeconds(target))).Append(" man-s (").Append(TargetMen(target)).Append(" men)");
            }
            sb.Append("; his side winning clearly around him (left alone) ").Append(N(WinningSeconds)).Append(" man-s; THE FIXES: the fever's press refused for a man alone ")
              .Append(FeverRefusedLooks).Append(" looks, walks back forced ").Append(WalksForced).Append(", surges / punish step-ins ended ").Append(SurgesEnded)
              .Append(", sprints into contact ended ").Append(SprintsEnded).Append("; looks ").Append(Looks).Append(" (lone ").Append(LoneLooks).Append("), target reads ")
              .Append(TargetReads).Append("; errors ").Append(Errors);
            lines.Add(sb.ToString());
        }
    }
}
