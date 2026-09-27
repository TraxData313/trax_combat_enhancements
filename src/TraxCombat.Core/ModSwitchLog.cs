using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// The master switch (ModEnabled) over ONE mission, for the log (DESIGN §4 "Master switch"):
    /// whether the mod was on at the start, every toggle with its mission time, and the share of
    /// the mission it was on - so Anton can compare a battle fought with the mod against one
    /// fought without it, or see exactly when he flipped it mid-battle. Pure, main thread, one
    /// bool compare per tick (no allocation unless the switch really moved).
    /// </summary>
    public sealed class ModSwitchLog
    {
        /// <summary>Toggles listed by name in <see cref="Describe"/> (a longer tail is counted).</summary>
        public const int ListedToggles = 10;

        private readonly List<KeyValuePair<double, bool>> _toggles = new List<KeyValuePair<double, bool>>();
        private bool _started;
        private bool _startedOn;
        private bool _on;
        private double _start;
        private double _last;
        private double _onSeconds;

        /// <summary>Mission start: the state the battle began in.</summary>
        public void Start(double now, bool on)
        {
            _started = true;
            _startedOn = on;
            _on = on;
            _start = now;
            _last = now;
            _onSeconds = 0;
            _toggles.Clear();
        }

        /// <summary>Every tick: the switch as it is now. True when it moved since the last call
        /// (the toggle is recorded with <paramref name="now"/>).</summary>
        public bool Observe(double now, bool on)
        {
            if (!_started)
            {
                Start(now, on);
                return false;
            }
            Advance(now);
            if (on == _on) return false;
            _on = on;
            _toggles.Add(new KeyValuePair<double, bool>(now, on));
            return true;
        }

        public bool IsOn => _on;

        public bool StartedOn => _startedOn;

        public int Toggles => _toggles.Count;

        /// <summary>Share of the mission (start → <paramref name="now"/>) the mod was on, 0..1.</summary>
        public double OnShare(double now)
        {
            Advance(now);
            double total = _last - _start;
            if (total <= 0) return _on ? 1 : 0;
            return Math.Max(0, Math.Min(1, _onSeconds / total));
        }

        /// <summary>"mod ON" / "mod OFF" for a battle that never toggled; otherwise
        /// "mod was on for 63% of the battle (started ON; OFF at 40.1 s, ON at 80.3 s)".</summary>
        public string Describe(double now)
        {
            if (_toggles.Count == 0) return _on ? "mod ON" : "mod OFF";
            var sb = new StringBuilder("mod was on for ");
            sb.Append((OnShare(now) * 100).ToString("0", CultureInfo.InvariantCulture)).Append("% of the battle (started ")
              .Append(_startedOn ? "ON" : "OFF").Append(';');
            for (int i = 0; i < _toggles.Count && i < ListedToggles; i++)
                sb.Append(i == 0 ? " " : ", ").Append(_toggles[i].Value ? "ON" : "OFF").Append(" at ").Append(Seconds(_toggles[i].Key)).Append(" s");
            if (_toggles.Count > ListedToggles) sb.Append(", +").Append(_toggles.Count - ListedToggles).Append(" more");
            return sb.Append(')').ToString();
        }

        /// <summary>The [mission] line for one toggle.</summary>
        public static string ToggleText(bool on, double now) =>
            "mod switched " + (on ? "ON" : "OFF") + " (ModEnabled) at " + Seconds(now) + " s - "
            + (on ? "everything back on: damage rolls, Athletics (everyone starts with a full bar), bars"
                  : "vanilla from now on: no damage rolls (hits are recorded unrolled), no Athletics costs, refill or slow attacks (penalties lifted), no bars");

        private void Advance(double now)
        {
            if (now <= _last) return;
            if (_on) _onSeconds += now - _last;
            _last = now;
        }

        private static string Seconds(double t) => t.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
