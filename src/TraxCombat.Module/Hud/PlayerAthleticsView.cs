using System;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// DESIGN §3.1 + additions - the player's own Athletics bar (step 6), under the vanilla
    /// health bar, bottom right: the word "Athletics", the NUMBER (current / pool) and a slim bar
    /// - the fill coloured by f (green in the peak zone, blue just below the line, then yellow /
    /// orange / red at BarYellow/Orange/RedBelowPercent), a white marker at the peak line
    /// (AthleticsPeakPercent of the FULL pool), and the part of the bar his wounds hold drawn dark
    /// (HealthCapsAthletics), so a wounded player sees why it will not fill. Empty (exhausted):
    /// the label reads "Exhausted" and label, number and frame turn red.
    ///
    /// Reads <see cref="AthleticsLogic.TryGetReading"/> for the main agent every HudRefreshSeconds
    /// (live); the colours and numbers come from Core's <see cref="BarMath"/>. The show/hide rules,
    /// the layer and the fail safe are the base's (<see cref="TraxHudView"/>).
    ///
    /// Log: the first values pushed on each new layer; the FIRST time each colour, the empty bar
    /// and a wound show in a battle (a snapshot line each); later colour changes only with
    /// VerboseLogging (rate-limited); the [summary] counts all of it.
    /// </summary>
    public sealed class PlayerAthleticsView : TraxHudView
    {
        /// <summary>The prefab: module\GUI\Prefabs\TraxPlayerAthleticsBar.xml.</summary>
        public const string Movie = "TraxPlayerAthleticsBar";

        private PlayerAthleticsVM? _vm;
        private bool _hasValues;
        private BarBand _band;
        private bool _exhausted;
        private double _usable = 1.0;
        private int _lastBand = -1;

        public PlayerAthleticsView()
            : base("player bar", Movie, SettingsSchema.ShowPlayerBar)
        {
        }

        /// <summary>The live ViewModel (null while no layer is up) - the offline smoke reads it.</summary>
        internal PlayerAthleticsVM? CurrentViewModel => _vm;

        protected override ViewModel CreateDataSource(in HudFrame f)
        {
            _vm = new PlayerAthleticsVM();
            _vm.SetLayout(TraxSettings.Shared);
            _hasValues = false;
            return _vm;
        }

        protected override void OnLayerGone()
        {
            _vm = null;
            _hasValues = false;
        }

        protected override void OnVisibleFrame(float dt)
        {
            if (_hasValues) Stats.AddBarTime(dt, _band, _exhausted, _usable);
        }

        protected override bool Refresh(in HudFrame f, bool first)
        {
            var vm = _vm;
            if (vm == null) return false;
            var s = TraxSettings.Shared;
            vm.SetLayout(s);
            if (f.Player == null || !AthleticsLogic.TryGetReading(f.Player, out var r) || !r.Enabled)
            {
                // not tracked (yet), or Athletics went off between the gate and here: draw nothing
                vm.IsShown = false;
                _hasValues = false;
                return false;
            }

            var rules = BarRules.From(s);
            var band = BarMath.Band(in rules, r.PeakShare);
            BarMath.DisplayNumbers(r.Points, r.Pool, out int shown, out int pool);
            float fill = BarMath.Fill(r.Fraction);
            float usable = BarMath.Usable(r.UsableFraction);
            float peak = BarMath.PeakLine(r.PeakFraction);
            vm.SetValues(band, fill, usable, peak, shown, pool, r.Exhausted);
            vm.IsShown = true;
            _band = band;
            _exhausted = r.Exhausted;
            _usable = usable;
            _hasValues = true;
            LogWhatIsNew(in f, in r, in rules, band, shown, pool, fill, usable, first);
            return true;
        }

        private void LogWhatIsNew(in HudFrame f, in AthleticsReading r, in BarRules rules, BarBand band, int shown, int pool, float fill, float usable, bool first)
        {
            if (first)
            {
                var s = TraxSettings.Shared;
                TraxLog.Limited("hud", "player bar: first values pushed at " + S1(f.Now) + " s - " + shown + " / " + pool + ", fill " + F2(fill)
                    + ", usable " + F2(usable) + ", colour " + BarMath.Describe(band) + ", f " + F2(r.PeakShare) + ", peak marker at "
                    + Pct(r.PeakFraction) + " of the bar" + (r.Exhausted ? ", EXHAUSTED" : string.Empty)
                    + "; bar " + s.PlayerBarWidth + " x " + s.PlayerBarHeight + " px, " + s.PlayerBarOffsetRight + " px from the right edge and "
                    + s.PlayerBarOffsetBottom + " px from the bottom (UI pixels - the game's UI scale applies); colours: " + rules.Describe(), "hud-first");
            }

            int b = (int)band;
            bool firstOfColour = Stats.NoteBand(band);
            if (firstOfColour)
                TraxLog.Limited("hud", "player bar: " + BarMath.Name(band).ToUpperInvariant() + " for the first time this battle at " + S1(f.Now)
                    + " s - f " + F2(r.PeakShare) + ", " + shown + " / " + pool + " (fill " + F2(fill) + ")", "hud-band");
            else if (_lastBand >= 0 && _lastBand != b && TraxLog.VerboseWants("hud-band"))
                TraxLog.Verbose("hud", "player bar: colour " + BarMath.Name((BarBand)_lastBand) + " → " + BarMath.Name(band) + " at " + S1(f.Now)
                    + " s (f " + F2(r.PeakShare) + ", " + shown + " / " + pool + ")", "hud-band");
            _lastBand = b;

            if (Stats.NoteExhausted(r.Exhausted))
                TraxLog.Limited("hud", "player bar: EXHAUSTED shown at " + S1(f.Now) + " s - " + shown + " / " + pool
                    + ": the label reads \"" + BarMath.ExhaustedLabelText + "\", label, number and frame red", "hud-exhausted");

            if (Stats.NoteUsable(r.UsableFraction))
                TraxLog.Limited("hud", "player bar: wounded at " + S1(f.Now) + " s - the last " + Pct(BarMath.Capped(r.UsableFraction))
                    + " of the bar shown dark: usable " + Math.Round(r.UsablePool) + " of " + pool + " (health caps the bar); f can reach at most "
                    + F2(Math.Min(1.0, r.PeakFraction > 0 ? r.UsableFraction / r.PeakFraction : 1.0)) + " now", "hud-wound");
        }

        private static string Pct(double share) => Math.Round(share * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}
