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
    /// Log: the first flash and the first steady arrow in full; the [summary] "opening arrow" line (flashes, steady cues, seconds shown).
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
        private bool _firstFlashLogged, _firstFinishLogged;

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
            if (cue != ArrowCue.None && (cue != _lastCue || r.Mask != _lastMask))
            {
                if (cue == ArrowCue.Flash)
                {
                    _flashes++;
                    if (!_firstFlashLogged)
                    {
                        _firstFlashLogged = true;
                        TraxLog.Limited("hud", "opening arrow: first FLASH at " + S1(f.Now) + " s - " + OpeningArrowMath.ArrowsName(r.Mask) + " (the side to hit; fade in "
                            + F2(OpeningArrowMath.FadeInSeconds) + " s to the perfect moment, out " + F2(OpeningArrowMath.FadeOutSeconds) + " s)", "hud-arrow");
                    }
                }
                else
                {
                    _finishes++;
                    if (!_firstFinishLogged)
                    {
                        _firstFinishLogged = true;
                        TraxLog.Limited("hud", "opening arrow: first PERFECT STRIKE side at " + S1(f.Now) + " s - " + OpeningArrowMath.ArrowsName(r.Mask)
                            + ", steady while he reels", "hud-arrow");
                    }
                }
            }
            if (cue == ArrowCue.Flash) _flashSeconds += f.Dt;
            else if (cue == ArrowCue.Finish) _finishSeconds += f.Dt;
            _lastCue = cue;
            _lastMask = cue != ArrowCue.None ? r.Mask : 0;
        }

        protected override bool Refresh(in HudFrame f, bool first)
        {
            if (_vm == null) return false;
            if (first)
                TraxLog.Limited("hud", "opening arrow: layer up at " + S1(f.Now) + " s - vanilla's crosshair arrows (" + OpeningArrowMath.ArrowLength + " x "
                    + OpeningArrowMath.ArrowThickness + " px, " + OpeningArrowMath.ArrowOffset + " px from the centre) in red; the flash with OpeningsEnabled "
                    + (TraxSettings.Shared.OpeningsEnabled ? "on" : "off - only the K test tool's") + ", the perfect-strike side always", "hud-arrow");
            return true;
        }

        internal override void AddSummaryLines(List<string> lines)
        {
            lines.Add("hud: opening arrow (step 106c) - flashes " + _flashes + " (" + F2(_flashSeconds) + " s lit), perfect-strike sides shown " + _finishes
                      + " (" + F2(_finishSeconds) + " s steady)");
        }
    }
}
