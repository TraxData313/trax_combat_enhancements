using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 103 - CROUCH REST (Toni 2026-11-09: "when I crouch (Z) increase the Athletics regeneration x2 (player only, but let this be there)";
    /// AI_NOTES "Step 103", DESIGN decision 92): the settings, read live. <see cref="Enabled"/> = the master switch FIRST, then Athletics, then a
    /// multiplier above 1 and at least one of the two switches. The engine's crouch is <c>Agent.CrouchMode</c> (native <c>GetCrouchMode</c>; the
    /// Z key sends <c>EventControlFlag.Crouch</c> - game key 15 "Crouch", a toggle). The AI never crouches in a battle (only scripted, stealth
    /// missions - <c>Agent.SetCrouchMode</c>), so <see cref="ForAi"/> ships off and would rarely matter.
    /// </summary>
    public readonly struct CrouchRestRules
    {
        public CrouchRestRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            AthleticsEnabled = s.AthleticsEnabled;
            Multiplier = CrouchRestMath.ClampMultiplier(s.CrouchRefillMultiplier);
            ForPlayer = s.CrouchRefillForPlayer;
            ForAi = s.CrouchRefillForAi;
        }

        public static CrouchRestRules From(TraxSettings s) => new CrouchRestRules(s);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }

        /// <summary>The refill rate times this while crouched (1-5; 1 = nothing).</summary>
        public double Multiplier { get; }

        public bool ForPlayer { get; }
        public bool ForAi { get; }

        public bool Enabled => ModEnabled && AthleticsEnabled && Multiplier > 1 && (ForPlayer || ForAi);

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !(Multiplier > 1) ? "CrouchRefillMultiplier 1"
            : !(ForPlayer || ForAi) ? "CrouchRefillForPlayer / CrouchRefillForAi both off" : null;

        public string Describe()
        {
            if (!Enabled) return "crouch rest OFF (" + OffBecause + ")";
            return "crouch rest ON: crouched (Z) the Athletics refill runs x" + Multiplier.ToString("0.0#", CultureInfo.InvariantCulture)
                   + " for " + (ForPlayer && ForAi ? "you and the AI" : ForPlayer ? "you" : "the AI") + " - the rest after a blow, the wounds' cap, the pace and"
                   + " no refill while sprinting all still apply";
        }
    }

    /// <summary>Step 103: the crouch rest's pure rule - the extra refill multiplier for one man this step.</summary>
    public static class CrouchRestMath
    {
        public const double MinMultiplier = 1, MaxMultiplier = 5;

        public static double ClampMultiplier(double v) => double.IsNaN(v) ? MinMultiplier : Math.Max(MinMultiplier, Math.Min(MaxMultiplier, v));

        /// <summary>Does this man's crouch count (the rules on, his switch, on foot)? The crouch state is read only when it does.</summary>
        public static bool Applies(in CrouchRestRules r, bool isPlayer, bool mounted) =>
            r.Enabled && !mounted && (isPlayer ? r.ForPlayer : r.ForAi);

        /// <summary>The refill-rate multiplier: <see cref="CrouchRestRules.Multiplier"/> while he is crouched and it applies, else 1.</summary>
        public static double Multiplier(in CrouchRestRules r, bool isPlayer, bool mounted, bool crouched) =>
            crouched && Applies(in r, isPlayer, mounted) ? r.Multiplier : 1.0;
    }

    /// <summary>Step 103: the crouch rest over one battle - the [summary] "crouch rest" line. Main thread.</summary>
    public sealed class CrouchRestStats
    {
        /// <summary>Refill seconds while crouched, you / the AI.</summary>
        public double PlayerSeconds, AiSeconds;

        /// <summary>Points gained while crouched (with the multiplier), you / the AI.</summary>
        public double PlayerPoints, AiPoints;

        /// <summary>Crouched seconds that refilled nothing (the rest after a blow, already at the top).</summary>
        public double PlayerIdleSeconds;

        /// <summary>Times you crouched (rising edges seen by the refill pass).</summary>
        public int PlayerCrouches;

        public int Errors;

        public void Note(bool isPlayer, double seconds, double points)
        {
            if (isPlayer)
            {
                PlayerSeconds += seconds;
                PlayerPoints += points;
            }
            else
            {
                AiSeconds += seconds;
                AiPoints += points;
            }
        }

        public string SummaryLine(in CrouchRestRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.Append("crouch rest (step 103; ").Append(r.Enabled ? "ON, x" + r.Multiplier.ToString("0.0#", c) : "OFF (" + r.OffBecause + ")")
             .Append("): you crouched ").Append(PlayerCrouches.ToString(c)).Append(" time(s), refilling crouched ").Append(PlayerSeconds.ToString("0.0", c))
             .Append(" s for ").Append(PlayerPoints.ToString("0.0", c)).Append(" points");
            if (r.Multiplier > 1) t.Append(" (").Append((PlayerPoints * (1 - 1 / r.Multiplier)).ToString("0.0", c)).Append(" of them the crouch's extra)");
            if (PlayerIdleSeconds > 0) t.Append(", crouched without a refill (the rest after a blow / full) ").Append(PlayerIdleSeconds.ToString("0.0", c)).Append(" s");
            t.Append("; the AI ").Append(r.ForAi ? "on" : "off");
            if (AiSeconds > 0) t.Append(" - ").Append(AiSeconds.ToString("0.0", c)).Append(" s crouched, ").Append(AiPoints.ToString("0.0", c)).Append(" points");
            else if (r.ForAi) t.Append(" - no AI man crouched (the game's AI crouches only in scripted scenes)");
            if (Errors > 0) t.Append("; ERRORS ").Append(Errors.ToString(c));
            return t.ToString();
        }
    }
}
