using System;
using System.Collections.Generic;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 84: the texts a <see cref="ParamType.Text"/> setting may hold (the culture shields' and spears' item ids). Every setting value is a
    /// double (<see cref="TraxSettings"/> keeps one array), so a text is stored as its INDEX here: an append-only table, the same text always
    /// the same index (ordinal - the game's item ids are case-sensitive), index 0 = "" (none). A text is trimmed and must look like an id:
    /// letters, digits, '_', '.', '-', at most <see cref="MaxLength"/> characters. Thread-safe; a setting change mid-battle adds at most one
    /// entry per new text, nothing per read.
    /// </summary>
    public static class TextValues
    {
        /// <summary>The longest text accepted.</summary>
        public const int MaxLength = 100;

        /// <summary>The highest index a Text setting may hold (its ParamDef's Max) - far more than any player will ever type.</summary>
        public const int MaxIndex = 1000000;

        private static readonly object Gate = new object();
        private static readonly List<string> Texts = new List<string> { string.Empty };
        private static readonly Dictionary<string, int> Indexes = new Dictionary<string, int>(StringComparer.Ordinal) { { string.Empty, 0 } };

        /// <summary>"an item id (letters, digits, _ . -; empty = none)" - the file's comment and the MCM hint's rule.</summary>
        public const string RuleText = "an item id (letters, digits, _ . -; empty = none)";

        /// <summary>The text at <paramref name="index"/> ("" for 0, NaN or an index never handed out).</summary>
        public static string Get(double index)
        {
            if (double.IsNaN(index) || double.IsInfinity(index)) return string.Empty;
            long i = (long)Math.Round(index, MidpointRounding.AwayFromZero);
            lock (Gate)
                return i > 0 && i < Texts.Count ? Texts[(int)i] : string.Empty;
        }

        /// <summary>The index of <paramref name="text"/> (trimmed), added when new. False (index -1, <paramref name="why"/> set) when it is
        /// not an id: a character outside letters, digits, '_', '.', '-', or longer than <see cref="MaxLength"/>. Null = "".</summary>
        public static bool TryIndex(string? text, out int index, out string? why)
        {
            index = -1;
            why = null;
            string t = (text ?? string.Empty).Trim();
            if (t.Length > MaxLength)
            {
                why = "longer than " + MaxLength + " characters";
                return false;
            }
            foreach (char c in t)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-') continue;
                why = "'" + c + "' is not allowed in an item id";
                return false;
            }
            lock (Gate)
            {
                if (Indexes.TryGetValue(t, out index)) return true;
                if (Texts.Count > MaxIndex)
                {
                    why = "too many different texts this session";
                    index = -1;
                    return false;
                }
                index = Texts.Count;
                Texts.Add(t);
                Indexes.Add(t, index);
                return true;
            }
        }

        /// <summary><see cref="TryIndex(string?, out int, out string?)"/> without the reason.</summary>
        public static bool TryIndex(string? text, out int index) => TryIndex(text, out index, out _);
    }
}
