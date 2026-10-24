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

        /// <summary>Step 33: a keyboard / mouse key by NAME (<see cref="KeyNames"/>) - stored as its index in that
        /// list (a whole number like an Int), written and read as the name ("LeftControl").</summary>
        Key,

        /// <summary>Step 84: a short TEXT - an item id ("adarga") - stored as its index in <see cref="TextValues"/> (a whole number like an
        /// Int, the same text always the same index), written and read as the text itself. "" = none.</summary>
        Text,
    }

    /// <summary>
    /// Step 33: the keys a <see cref="ParamType.Key"/> setting may name (SprintKey) - the game's own
    /// <c>TaleWorlds.InputSystem.InputKey</c> names (the offline smoke checks each parses as one), in the order MCM and the
    /// file's comment list them. Most keys the game binds in a fight by default are left out (WASD, LMB / RMB, Space jump,
    /// E kick, F action, R camera, X weapon mode, G drop, U, V, O, Z crouch, the number row's orders); Left Shift (zoom),
    /// Left Alt (the markers) and Caps Lock (walk) stay in the list for players who rebound those. A name is matched
    /// ignoring case, spaces, '-' and '_' ("left ctrl" too).
    /// </summary>
    public static class KeyNames
    {
        /// <summary>Every name, in order; a Key setting's value is an index here.</summary>
        public static readonly string[] All =
        {
            "LeftControl", "RightControl", "LeftAlt", "RightAlt", "LeftShift", "RightShift", "CapsLock", "Tab",
            "Q", "T", "Y", "H", "J", "K", "L", "B", "N", "M", "C",
            "MiddleMouseButton", "X1MouseButton", "X2MouseButton",
        };

        /// <summary>The name at <paramref name="index"/> (rounded, clamped into the list).</summary>
        public static string Name(double index)
        {
            if (double.IsNaN(index)) return All[0];
            long i = (long)Math.Round(index, MidpointRounding.AwayFromZero);
            if (i < 0) i = 0;
            if (i >= All.Length) i = All.Length - 1;
            return All[i];
        }

        /// <summary>The index of <paramref name="name"/>, ignoring case, spaces, '-' and '_'; "ctrl" / "control" and "lctrl"
        /// style short forms of the Ctrl, Alt and Shift keys are accepted too. False when it is none of <see cref="All"/>.</summary>
        public static bool TryIndex(string? name, out int index)
        {
            index = -1;
            if (name == null) return false;
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
                if (c != ' ' && c != '-' && c != '_') sb.Append(char.ToLowerInvariant(c));
            string n = sb.ToString().Replace("ctrl", "control");
            if (n == "lcontrol") n = "leftcontrol";
            else if (n == "rcontrol") n = "rightcontrol";
            else if (n == "lalt") n = "leftalt";
            else if (n == "ralt") n = "rightalt";
            else if (n == "lshift") n = "leftshift";
            else if (n == "rshift") n = "rightshift";
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], n, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>"LeftControl, RightControl, …" - the file's comment and the MCM hint.</summary>
        public static string ListText => string.Join(", ", All);
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

    /// <summary>Step 59 (Anton, 2026-10-18: "leave a few options for me and the players"): which MCM page a group sits on.
    /// <see cref="Player"/> = the short main page a player reads ("Trax Combat Enhancements"); <see cref="Advanced"/> = every
    /// other setting, grouped by system ("Trax Combat Enhancements - Advanced"). The config file follows the same split:
    /// the player groups first. A NEW setting goes to Advanced unless it is one a player needs (CLAUDE.md).</summary>
    public enum SettingsPage
    {
        Player,
        Advanced,
    }

    /// <summary>A block of related settings: one MCM group, one section of the config file.</summary>
    public sealed class ParamGroup
    {
        public ParamGroup(int order, string title, SettingsPage page = SettingsPage.Advanced)
        {
            Order = order;
            Title = title;
            Page = page;
        }

        /// <summary>Position in MCM and in the file (0 first). The player page's groups come first (step 59).</summary>
        public int Order { get; }

        /// <summary>Step 59: the MCM page this group is on (the config file's section too).</summary>
        public SettingsPage Page { get; }

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
                case ParamType.Key:
                case ParamType.Text:
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
                case ParamType.Key:
                    return KeyNames.Name(value);
                case ParamType.Text:
                    return TextValues.Get(value);
                default:
                    return value.ToString("0.0###", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>The value as a JSON value in the config file and defaults.json: <see cref="Format"/>, a key's name in
        /// quotes ("LeftControl").</summary>
        public string FormatJson(double value) =>
            Type == ParamType.Key ? "\"" + Format(value) + "\""
            : Type == ParamType.Text ? Newtonsoft.Json.JsonConvert.ToString(Format(value)) // step 84: the text, quoted and escaped
            : Format(value);

        /// <summary>"default 50, range 0 to 100" - the tail of the config-file comment.</summary>
        public string DefaultAndRangeText =>
            Type == ParamType.Bool
                ? "default " + Format(Default)
                : Type == ParamType.Key
                    ? "default " + Format(Default) + ", one of " + KeyNames.ListText
                    : Type == ParamType.Text
                        ? "default " + Format(Default) + ", " + TextValues.RuleText
                        : "default " + Format(Default) + ", range " + Format(Min) + " to " + Format(Max);

        /// <summary>The MCM hint: the description, when a change applies, the default. (The config
        /// file's comment says only "Applies from the next battle." for such a setting: a hand edit
        /// of ANY setting is read at the next battle start - its header says so.)</summary>
        public string HintText =>
            Description
            + (Timing == ApplyTiming.NextBattle ? " Applies from the next battle." : " Applies at once, even mid-battle.")
            + " Default: " + Format(Default) + ".";

        public override string ToString() => Key;
    }
}
