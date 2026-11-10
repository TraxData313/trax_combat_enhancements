using System.Collections.Generic;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// Step 106c - THE RED ARROW AT THE CROSSHAIR (Toni 2026-11-09; AI_NOTES "106c - built", DESIGN decision 95 (k)): vanilla's direction-indicator
    /// arrows (Native Crosshair.xml - their sprites, sizes, places and flips) drawn red by our own layer, one cue at a time
    /// (<see cref="AthleticsLogic.TryGetOpeningArrow"/>): the OPENING FLASH when a man goes off balance (with <c>OpeningsEnabled</c>, or the K test
    /// tool) and the PERFECT STRIKE side steady while a man you can finish reels (the defensive game's reel too). Its switch is
    /// <c>OpeningArrowShow</c> (the player page); the strength is read EVERY FRAME (<see cref="OnLayerFrame"/>) - a flash lives 0.2 s.
    ///
    /// Step 112: the colour by the ROLE (<see cref="ArrowColours"/>): the opening to hit red, the perfect-strike side gold - both settings.
    /// Log: the first flash and the first steady arrow in full, each with its role and colour; the [summary] "opening arrow" line (flashes, steady cues, seconds shown).
    /// </summary>
    public sealed class OpeningArrowView : TraxHudView
    {
        /// <summary>The prefab: module\GUI\Prefabs\TraxOpeningArrow.xml.</summary>
        public const string Movie = "TraxOpeningArrow";

        private OpeningArrowVM? _vm;
        private ArrowCue _lastCue;
        private int _lastMask;
        private int _flashes, _finishes;
        private double _flashSeconds, _finishSeconds;
        private bool _firstFlashLogged, _firstFinishLogged, _firstHoldLogged;

        // step 112: the colour shown - its role and the setting's text it was read from (a change of either re-reads it; reference compares, no allocation)
        private ArrowRole _colorRole;
        private string? _colorText;
        private int _roleChanges;
        private bool _badColorLogged;

        public OpeningArrowView()
            : base("opening arrow", Movie, SettingsSchema.OpeningArrowShow)
        {
        }

        /// <summary>The live ViewModel (null while no layer is up) - the offline smoke reads it.</summary>
        internal OpeningArrowVM? CurrentViewModel => _vm;

        internal int FlashesShown => _flashes;

        /// <summary>Step 106f: how long the layer stays up in the walk-about mode after the last opening / reel (s) - so it is not rebuilt between cues.</summary>
        internal const double WalkLingerSeconds = 2.0;

        private double _walkUntil = double.NegativeInfinity;

        /// <summary>
        /// Step 106f (Toni's training ground: "opening arrow: not shown ... not a fight (mission mode) - mode StartUp"): in the walk-about mode
        /// (the training field, a town brawl) the arrow counts as a fight while you are in melee with a man who is off balance or reeling - an
        /// opening pending, a chain on him, a flash noted, a perfect-strike window open - and <see cref="WalkLingerSeconds"/> after. Never in a
        /// conversation, a menu or a film (not the walk mode), never without you on the field (the gate's NoPlayer).
        /// </summary>
        protected override bool FightsInWalkMode(in HudFrame f)
        {
            if (AthleticsLogic.OpeningMeleeNow(f.Now)) _walkUntil = f.Now + WalkLingerSeconds;
            return f.Now < _walkUntil;
        }

        internal int FinishesShown => _finishes;

        protected override ViewModel CreateDataSource(in HudFrame f)
        {
            _vm = new OpeningArrowVM();
            _lastCue = ArrowCue.None;
            _lastMask = 0;
            _colorText = null; // step 112: the colour is read at the first frame
            _colorRole = ArrowRole.Opening;
            return _vm;
        }

        protected override void OnLayerGone()
        {
            _vm = null;
        }

        protected override void OnLayerFrame(in HudFrame f)
        {
            var vm = _vm;
            if (vm == null) return;
            AthleticsLogic.TryGetOpeningArrow(f.Player, f.Now, out var r);
            vm.Set(in r);
            var cue = r.Shows ? r.Cue : ArrowCue.None;
            UpdateColor(vm, cue);
            if (cue != ArrowCue.None && (cue != _lastCue || r.Mask != _lastMask))
            {
                if (cue == ArrowCue.Hold) // step 107: the opening of a hold-block, steady for the hold
                {
                    _flashes++;
                    if (!_firstHoldLogged)
                    {
                        _firstHoldLogged = true;
                        TraxLog.Limited("hud", "opening arrow: first HOLD-BLOCK opening at " + S1(f.Now) + " s - " + OpeningArrowMath.ArrowsName(r.Mask)
                            + " (the side he left open, steady while he holds his block) - " + RoleText(ArrowRole.Opening), "hud-arrow");
                    }
                }
                else if (cue == ArrowCue.Flash)
                {
                    _flashes++;
                    if (!_firstFlashLogged)
                    {
                        _firstFlashLogged = true;
                        TraxLog.Limited("hud", "opening arrow: first FLASH at " + S1(f.Now) + " s - " + OpeningArrowMath.ArrowsName(r.Mask) + " (the side to hit; fade in "
                            + F2(OpeningArrowMath.FadeInSeconds) + " s to the perfect moment, out " + F2(OpeningArrowMath.FadeOutSeconds) + " s) - " + RoleText(ArrowRole.Opening), "hud-arrow");
                    }
                }
                else
                {
                    _finishes++;
                    if (!_firstFinishLogged)
                    {
                        _firstFinishLogged = true;
                        TraxLog.Limited("hud", "opening arrow: first PERFECT STRIKE side at " + S1(f.Now) + " s - " + OpeningArrowMath.ArrowsName(r.Mask)
                            + ", steady while he reels - " + RoleText(ArrowRole.PerfectStrike), "hud-arrow");
                    }
                }
            }
            if (cue == ArrowCue.Flash || cue == ArrowCue.Hold) _flashSeconds += f.Dt; // step 107: a hold's opening counts with the flashes (the side to hit)
            else if (cue == ArrowCue.Finish) _finishSeconds += f.Dt;
            _lastCue = cue;
            _lastMask = cue != ArrowCue.None ? r.Mask : 0;
        }

        protected override bool Refresh(in HudFrame f, bool first)
        {
            if (_vm == null) return false;
            if (first)
                TraxLog.Limited("hud", "opening arrow: layer up at " + S1(f.Now) + " s - vanilla's crosshair arrows (" + OpeningArrowMath.ArrowLength + " x "
                    + OpeningArrowMath.ArrowThickness + " px, " + OpeningArrowMath.ArrowOffset + " px from the centre); " + RoleText(ArrowRole.Opening) + ", " + RoleText(ArrowRole.PerfectStrike) + "; the flash with OpeningsEnabled "
                    + (TraxSettings.Shared.OpeningsEnabled ? "on" : "off - only the K test tool's") + ", the perfect-strike side always", "hud-arrow");
            return true;
        }

        /// <summary>Step 112: the colour follows the ROLE of the cue shown (after step 111's latch - never mid-swing); a hidden arrow keeps its colour.
        /// The setting's text is re-read when it changes (MCM mid-battle): live.</summary>
        private void UpdateColor(OpeningArrowVM vm, ArrowCue cue)
        {
            var role = cue != ArrowCue.None ? ArrowColours.RoleOf(cue) : _colorRole;
            string text = ArrowColours.TextFor(role, TraxSettings.Shared);
            if (_colorText != null && role == _colorRole && ReferenceEquals(text, _colorText)) return;
            if (_colorText != null && role != _colorRole) _roleChanges++;
            _colorRole = role;
            _colorText = text;
            ArrowColours.Resolve(role, TraxSettings.Shared, out float cr, out float cg, out float cb, out float ca, out bool usedDefault);
            vm.SetColor(cr, cg, cb, ca);
            if (usedDefault && !_badColorLogged)
            {
                _badColorLogged = true;
                TraxLog.Limited("hud", "opening arrow: " + ArrowColours.SettingFor(role).Key + " \"" + text + "\" is not a hex colour (RRGGBB or RRGGBBAA) - the default "
                    + ArrowColours.DefaultTextFor(role) + " is used", "hud-arrow");
            }
        }

        /// <summary>"role: PERFECT STRIKE, colour FFD700" - the first-time lines (allocates; log only).</summary>
        private static string RoleText(ArrowRole role) => "role: " + ArrowColours.RoleName(role) + ", colour " + ArrowColours.TextFor(role, TraxSettings.Shared);

        internal override void AddSummaryLines(List<string> lines)
        {
            lines.Add("hud: opening arrow (step 106c) - flashes " + _flashes + " (" + F2(_flashSeconds) + " s lit), perfect-strike sides shown " + _finishes
                      + " (" + F2(_finishSeconds) + " s steady); colour by role (step 112): " + RoleText(ArrowRole.Opening) + ", " + RoleText(ArrowRole.PerfectStrike)
                      + ", the colour changed " + _roleChanges + " times");
        }
    }
}
