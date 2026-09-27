using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>The value kind of a setting. Every value is stored as a double internally
    /// (bool = 0/1, int = a whole number) so one array holds them all.</summary>
    public enum ParamType
    {
        Bool,
        Int,
        Float,
    }

    /// <summary>When a changed value takes effect. The target for EVERY setting is
    /// <see cref="Live"/> (CLAUDE.md, hot swap); <see cref="NextBattle"/> is the fallback a
    /// feature may use only when live truly is impossible - and then its description says so.
    /// There is deliberately no "after a restart".</summary>
    public enum ApplyTiming
    {
        Live,
        NextBattle,
    }

    /// <summary>A block of related settings: one MCM group, one section of the config file.</summary>
    public sealed class ParamGroup
    {
        public ParamGroup(int order, string title)
        {
            Order = order;
            Title = title;
        }

        /// <summary>Position in MCM and in the file (0 first).</summary>
        public int Order { get; }

        /// <summary>The MCM group name and the config-file section heading. No '/' - MCM reads
        /// that as a sub-group delimiter.</summary>
        public string Title { get; }

        public override string ToString() => Title;
    }

    /// <summary>
    /// One setting of the mod: its key (the config-file key and the MCM property id), range,
    /// group and the plain-words description shown beside it in the file and as the MCM hint -
    /// and its default, which comes from defaults.json (<see cref="DefaultsFile"/>, the one truth
    /// for default values), never from code. Immutable; the live VALUE lives in
    /// <see cref="TraxSettings"/>.
    /// </summary>
    public sealed class ParamDef
    {
        internal ParamDef(int index, string key, ParamType type, double min, double max,
            ParamGroup group, string label, string description, ApplyTiming timing)
        {
            if (min > max) throw new ArgumentException(key + ": min > max");
            Index = index;
            Key = key;
            Type = type;
            Min = min;
            Max = max;
            Group = group;
            Label = label;
            Description = description;
            Timing = timing;
            // The one truth: the embedded defaults.json (a problem there is recorded, never thrown).
            Default = DefaultsFile.DefaultFor(key, type, min, max);
        }

        /// <summary>Slot in <see cref="SettingsSchema.All"/> and in the settings value array.</summary>
        public int Index { get; }

        /// <summary>The config-file key, the MCM property id, the name in every log line.</summary>
        public string Key { get; }

        public ParamType Type { get; }

        /// <summary>The default - from defaults.json (see <see cref="DefaultsFile"/>).</summary>
        public double Default { get; }

        public double Min { get; }

        public double Max { get; }

        public ParamGroup Group { get; }

        /// <summary>Short MCM label ("Spread (± %)"). Unique within its group - MCM keys its
        /// property table by this name.</summary>
        public string Label { get; }

        /// <summary>What it does, in player terms. Written above the key in the config file and
        /// used as the MCM hint.</summary>
        public string Description { get; }

        public ApplyTiming Timing { get; }

        /// <summary>Digits kept for a Float - so MCM's float slider (0.1f = 0.1000000015) and
        /// the file never disagree about "the same" value.</summary>
        public const int FloatDecimals = 4;

        /// <summary>Rounds to the type's precision and clamps into [Min, Max]. NaN → the default.</summary>
        public double Normalize(double value) => Normalize(value, out _, out _);

        /// <summary>Rounds to the type's precision and clamps into [Min, Max], saying which
        /// happened (for the "clamped" / "rounded" notes in the log). NaN → the default.</summary>
        public double Normalize(double value, out bool clamped, out bool rounded)
        {
            clamped = false;
            rounded = false;
            if (double.IsNaN(value)) return Default;
            double v = value;
            switch (Type)
            {
                case ParamType.Bool:
                    v = v != 0 ? 1 : 0;
                    break;
                case ParamType.Int:
                    v = Math.Round(v, MidpointRounding.AwayFromZero);
                    rounded = Math.Abs(v - value) > 1e-9;
                    break;
                case ParamType.Float:
                    if (!double.IsInfinity(v))
                        v = Math.Round(v, FloatDecimals, MidpointRounding.AwayFromZero);
                    break;
            }
            if (v < Min) { v = Min; clamped = true; }
            else if (v > Max) { v = Max; clamped = true; }
            return v;
        }

        /// <summary>The value as the config file and the log write it: true/false, 50, 0.75, 1.0.</summary>
        public string Format(double value)
        {
            switch (Type)
            {
                case ParamType.Bool:
                    return value != 0 ? "true" : "false";
                case ParamType.Int:
                    return ((long)Math.Round(value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                default:
                    return value.ToString("0.0###", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>"default 50, range 0 to 100" - the tail of the config-file comment.</summary>
        public string DefaultAndRangeText =>
            Type == ParamType.Bool
                ? "default " + Format(Default)
                : "default " + Format(Default) + ", range " + Format(Min) + " to " + Format(Max);

        /// <summary>The MCM hint: the description, when it applies (only if not live), the default.</summary>
        public string HintText =>
            Description
            + (Timing == ApplyTiming.NextBattle ? " Applies from the next battle." : string.Empty)
            + " Default: " + Format(Default) + ".";

        public override string ToString() => Key;
    }
}
