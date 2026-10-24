using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// Step 33 - YOUR SPRINT BAR: a slim row above the Attack recovery bar, its right end lined up with your Athletics bar -
    /// "Sprint 6.2 s" and a bar filled with your sprint store (its share of its size now), a white mark at
    /// SprintMinStartPercent; grey below the mark, blue when a sprint can start, yellow while you sprint. Read EVERY FRAME
    /// (<see cref="OnLayerFrame"/> - a sprint drains 1 s a second), the layout at HudRefreshSeconds. Reads
    /// <see cref="AthleticsLogic.TryGetPlayerSprint"/>.
    ///
    /// Shown by the HUD gate in fights (the master switch first) with its own switch ShowSprintBar, and only while your
    /// Athletics bar (ShowPlayerBar) and the sprint (SprintEnabled) are on. Outside a battle (walking about a town or village -
    /// the sprint works there too since 2026-10-14) by the Athletics bar's own rule and switch (ShowPlayerBarOutsideBattles): a
    /// weapon drawn, or your Athletics or your sprint store below full, then the grace.
    ///
    /// Log: the first values on each new layer, the first sprint shown; the [summary] "sprint bar" line.
    /// </summary>
    public sealed class SprintBarView : TraxHudView
    {
        /// <summary>The prefab: module\GUI\Prefabs\TraxSprintBar.xml.</summary>
        public const string Movie = "TraxSprintBar";

        private SprintBarVM? _vm;
        private bool _wasActive;
        private int _sprintsShown;
        private double _sprintingSeconds;
        private double _belowMarkSeconds;
        private bool _firstSprintLogged;

        // Step 86: the key hint's inputs at its last build - rebuilt only when one of them changes (never per frame).
        private bool _hintBuilt;
        private bool _hintMod;
        private bool _hintShow;
        private bool _hintPad;
        private int _hintPadType;
        private int _hintKey;
        private int _hintPadKey;
        private SprintKeyHint _hint = SprintKeyHint.Nothing;
        private bool _glyphPending;
        private int _hintBuilds;
        private bool _firstHintLogged;
        private bool _firstPadHintLogged;

        /// <summary>Step 86: does the game have this controller key's glyph sprite? The smoke swaps it (no sprite data offline); the default
        /// asks the game's sprite data once per change (General\InputKeys\… sits in ui_group1, always loaded).</summary>
        internal static Func<string, bool>? GlyphProbe = GameGlyphProbe;

        private static bool GameGlyphProbe(string inputKeyName)
        {
            var data = UIResourceManager.SpriteData;
            return data != null && data.GetSprite(SprintKeyHints.GlyphSprite(inputKeyName)) != null;
        }

        /// <summary>How many times the hint was rebuilt (the smoke checks it stays put frame to frame).</summary>
        internal int HintBuilds => _hintBuilds;

        internal SprintKeyHint CurrentHint => _hint;

        public SprintBarView()
            : base("sprint bar", Movie, SettingsSchema.ShowSprintBar)
        {
            Stats.HasOutsideRule = true;
            Stats.ConditionName = "your Athletics bar or the sprint off";
        }

        /// <summary>Outside a battle by the Athletics bar's own rule and switch; "below full" also while you sprint or your
        /// sprint store refills.</summary>
        protected override ParamDef? OutsideToggle => SettingsSchema.ShowPlayerBarOutsideBattles;

        protected override bool ReadOutside(in HudFrame f, out bool belowFull)
        {
            belowFull = false;
            if (f.Player == null || !AthleticsLogic.TryGetReading(f.Player, out var r) || !r.Enabled) return false;
            if (!AthleticsLogic.TryGetPlayerSprint(f.Player, out var sp)) return false;
            belowFull = r.BelowFull || sp.Active || sp.Share < 1.0 - 1e-6;
            return true;
        }

        /// <summary>The live ViewModel (null while no layer is up) - the offline smoke reads it.</summary>
        internal SprintBarVM? CurrentViewModel => _vm;

        internal int SprintsShown => _sprintsShown;

        /// <summary>It belongs to your Athletics bar and to the sprint.</summary>
        protected override bool ViewConditionMet(in HudFrame f)
        {
            var s = TraxSettings.Shared;
            return s.ShowPlayerBar && s.SprintEnabled;
        }

        protected override string ViewConditionText => "your Athletics bar (ShowPlayerBar) or the sprint (SprintEnabled) is off";

        protected override string? ViewConditionWhen => "your Athletics bar (ShowPlayerBar) and the sprint (SprintEnabled) are on";

        protected override ViewModel CreateDataSource(in HudFrame f)
        {
            _vm = new SprintBarVM();
            _vm.SetLayout(TraxSettings.Shared, TraxSettings.Shared.SprintMinStartPercent / 100.0);
            _hintBuilt = false; // a new layer: the hint pushed again
            _glyphPending = false;
            UpdateKeyHint(in f, _vm);
            return _vm;
        }

        protected override void OnLayerGone()
        {
            _vm = null;
        }

        /// <summary>Every frame on screen: the fill, the colour and the seconds (the VM raises only real changes).</summary>
        protected override void OnLayerFrame(in HudFrame f)
        {
            var vm = _vm;
            if (vm == null) return;
            if (f.Player == null || !AthleticsLogic.TryGetPlayerSprint(f.Player, out var r))
            {
                vm.IsShown = false;
                return;
            }
            vm.SetReading(in r);
            UpdateKeyHint(in f, vm);
            vm.IsShown = true;
            if (r.Active)
            {
                _sprintingSeconds += f.Dt;
                if (!_wasActive)
                {
                    _sprintsShown++;
                    if (!_firstSprintLogged)
                    {
                        _firstSprintLogged = true;
                        TraxLog.Limited("hud", "sprint bar: first sprint shown at " + S1(f.Now) + " s - \"" + r.LabelText + "\" of " + F2(r.Capacity)
                            + " s, the fill yellow and draining while you move (x" + F2(r.Multiplier) + " of your FRESH run speed now)", "hud-sprint");
                    }
                }
            }
            else if (!r.Ready)
            {
                _belowMarkSeconds += f.Dt;
            }
            _wasActive = r.Active;
        }

        /// <summary>
        /// Step 86 - THE KEY HINT: "Sprint (Ctrl) 6.2 s" on mouse and keyboard, the game's own glyph for the controller button on a pad
        /// (<see cref="HudFrame.GamepadActive"/> = the game's Input.IsGamepadActive). The master switch first, then SprintShowKeyHint. Its
        /// inputs (both switches, the device, the pad's type, both keys) are compared every frame - plain reads, nothing allocated - and the
        /// hint is rebuilt only when one changed. A glyph is pushed EMPTY first and set the next frame: the game's InputKeyVisualWidget
        /// picks its sprite (and the PlayStation twin) only when its KeyID changes.
        /// </summary>
        private void UpdateKeyHint(in HudFrame f, SprintBarVM vm)
        {
            var s = TraxSettings.Shared;
            bool mod = s.ModEnabled;
            bool show = mod && s.SprintShowKeyHint;
            int key = s.SprintKeyIndex;
            int padKey = s.SprintControllerKeyIndex;
            bool pad = f.GamepadActive;
            int padType = f.ControllerType;
            if (_hintBuilt && mod == _hintMod && show == _hintShow && pad == _hintPad && padType == _hintPadType && key == _hintKey && padKey == _hintPadKey)
            {
                if (_glyphPending)
                {
                    _glyphPending = false;
                    vm.SetKeyHint(in _hint, true);
                }
                return;
            }
            bool wasBuilt = _hintBuilt;
            _hintBuilt = true;
            _hintMod = mod;
            _hintShow = show;
            _hintPad = pad;
            _hintPadType = padType;
            _hintKey = key;
            _hintPadKey = padKey;
            _hint = SprintKeyHints.Decide(mod, show, pad, key, padKey, GlyphProbe);
            _hintBuilds++;
            _glyphPending = _hint.Kind == SprintHintKind.Glyph;
            vm.SetKeyHint(in _hint, false);
            if (!_firstHintLogged || (pad && !_firstPadHintLogged))
            {
                _firstHintLogged = true;
                if (pad) _firstPadHintLogged = true;
                TraxLog.Limited("hud", "sprint bar: key hint " + _hint.Describe() + " at " + S1(f.Now) + " s - " + (pad ? "on a pad (the game's Input.IsGamepadActive)" : "on mouse and keyboard")
                    + ", " + s.SprintKeysText + (show ? string.Empty : mod ? " (SprintShowKeyHint off)" : " (the mod is off)"), "hud-sprint");
            }
            else if (wasBuilt && TraxLog.VerboseWants("hud-sprint"))
            {
                TraxLog.Verbose("hud", "sprint bar: key hint now " + _hint.Describe() + (pad ? " (pad)" : " (keyboard)"), "hud-sprint");
            }
        }

        protected override bool Refresh(in HudFrame f, bool first)
        {
            var vm = _vm;
            if (vm == null) return false;
            var s = TraxSettings.Shared;
            if (f.Player == null || !AthleticsLogic.TryGetPlayerSprint(f.Player, out var r))
            {
                vm.SetLayout(s, s.SprintMinStartPercent / 100.0);
                vm.IsShown = false;
                return false;
            }
            vm.SetLayout(s, r.MinShare);
            if (first)
            {
                TraxLog.Limited("hud", "sprint bar: first values pushed at " + S1(f.Now) + " s - \"" + r.LabelText + "\" of " + F2(r.Capacity) + " s ("
                    + (r.Active ? "sprinting" : r.Ready ? "ready" : "below the start mark") + "), the mark at " + s.SprintMinStartPercent + "%; bar " + s.RecoveryBarWidth
                    + " x " + s.RecoveryBarHeight + " px, " + s.PlayerBarOffsetRight + " px from the right edge and " + (s.PlayerBarOffsetBottom + s.SprintBarOffsetAbove)
                    + " px from the bottom (your Athletics bar's row " + s.PlayerBarOffsetBottom + " + " + s.SprintBarOffsetAbove + "; UI pixels); key " + s.SprintKeysText, "hud-sprint");
            }
            return true;
        }

        internal override void AddSummaryLines(List<string> lines)
        {
            lines.Add("hud: sprint bar - sprints shown " + _sprintsShown + ", " + F1(_sprintingSeconds) + " s on screen sprinting, " + F1(_belowMarkSeconds)
                      + " s below the start mark - your sprints themselves are in the sprint lines");
        }

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
