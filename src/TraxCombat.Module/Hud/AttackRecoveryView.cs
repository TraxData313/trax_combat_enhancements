using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// Step 13 (Anton: "above that bar add a bar 'attack recovery' that empties when I attack and until
    /// it fills I can't attack; inside it add the secs delay added") - the ATTACK RECOVERY BAR, a slim
    /// row just above your Athletics bar, its right end lined up with it: the word "Attack recovery" and
    /// a bar that is EMPTY while an attack you cannot chain runs, FILLS over your no-attack pause with the
    /// seconds left inside it ("1.3 s", tenths rounded up), and stays FULL (steel, no text) when no pause
    /// runs; it FLASHES twice when you press attack too early (FlashBarOnEarlyAttack). It IS your timer:
    /// the input gate lets an attack through exactly when it is full.
    ///
    /// Shown by the Athletics bar's rules (fights; outside a battle with ShowPlayerBarOutsideBattles) with
    /// its own switch ShowAttackRecoveryBar, and only while the Athletics bar (ShowPlayerBar) and your
    /// pause (AttackRatePlayerTimer) are on. The fill, the seconds and the flash are read EVERY FRAME
    /// (<see cref="OnLayerFrame"/> - a 0.1 s refresh would make the fill and the flash stutter); the
    /// layout at HudRefreshSeconds. Reads <see cref="AthleticsLogic.TryGetPlayerRecovery"/>.
    ///
    /// Log: the first values pushed on each new layer; the first pause and the first flash of the battle
    /// in full; the [summary] "recovery bar" line (pauses shown, flashes, seconds refilling).
    /// </summary>
    public sealed class AttackRecoveryView : TraxHudView
    {
        /// <summary>The prefab: module\GUI\Prefabs\TraxAttackRecoveryBar.xml.</summary>
        public const string Movie = "TraxAttackRecoveryBar";

        private AttackRecoveryVM? _vm;
        private bool _wasRecovering;
        private bool _wasRunning;
        private double _lastFlashStart = double.NaN;
        private int _pausesShown;
        private double _pauseSum;
        private double _pauseMax;
        private int _flashesShown;
        private double _fillingSeconds;
        private bool _firstPauseLogged;
        private bool _firstFlashLogged;

        public AttackRecoveryView()
            : base("recovery bar", Movie, SettingsSchema.ShowAttackRecoveryBar)
        {
            Stats.HasOutsideRule = true;
            Stats.ConditionName = "your Athletics bar or your pause off";
        }

        /// <summary>The live ViewModel (null while no layer is up) - the offline smoke reads it.</summary>
        internal AttackRecoveryVM? CurrentViewModel => _vm;

        internal int PausesShown => _pausesShown;

        internal int FlashesShown => _flashesShown;

        /// <summary>Outside a battle by the Athletics bar's own rule and switch.</summary>
        protected override ParamDef? OutsideToggle => SettingsSchema.ShowPlayerBarOutsideBattles;

        protected override bool ReadOutside(in HudFrame f, out bool belowFull)
        {
            belowFull = false;
            if (f.Player == null || !AthleticsLogic.TryGetReading(f.Player, out var r) || !r.Enabled) return false;
            belowFull = r.BelowFull;
            return true;
        }

        /// <summary>It belongs to the Athletics bar and to your pause.</summary>
        protected override bool ViewConditionMet(in HudFrame f)
        {
            var s = TraxSettings.Shared;
            return s.ShowPlayerBar && s.AttackRatePlayerTimer;
        }

        protected override string ViewConditionText => "your Athletics bar (ShowPlayerBar) or your pause (AttackRatePlayerTimer) is off";

        protected override string? ViewConditionWhen => "your Athletics bar (ShowPlayerBar) and your pause (AttackRatePlayerTimer) are on";

        protected override ViewModel CreateDataSource(in HudFrame f)
        {
            _vm = new AttackRecoveryVM();
            _vm.SetLayout(TraxSettings.Shared);
            return _vm;
        }

        protected override void OnLayerGone()
        {
            _vm = null;
        }

        /// <summary>Every frame on screen: the fill, the seconds and the flash (the VM raises only real changes).</summary>
        protected override void OnLayerFrame(in HudFrame f)
        {
            var vm = _vm;
            if (vm == null) return;
            if (f.Player == null || !AthleticsLogic.TryGetPlayerRecovery(f.Player, f.Now, out var r))
            {
                vm.IsShown = false;
                return;
            }
            bool flash = r.FlashLit && TraxSettings.Shared.FlashBarOnEarlyAttack;
            vm.SetRecovery(r.Share, r.Recovering, r.SecondsText, flash);
            vm.IsShown = true;

            if (r.Running && !_wasRunning)
            {
                _pausesShown++;
                _pauseSum += r.Pause;
                if (r.Pause > _pauseMax) _pauseMax = r.Pause;
                if (!_firstPauseLogged)
                {
                    _firstPauseLogged = true;
                    TraxLog.Limited("hud", "recovery bar: first pause shown at " + S1(f.Now) + " s - empty at your attack, now refilling over " + F2(r.Pause)
                        + " s (D " + F2(r.Duration) + " s at attack speed x" + F2(r.M) + "), \"" + r.SecondsText + "\" inside it, counting down", "hud-recovery");
                }
            }
            if (flash && !double.IsNaN(r.FlashStartedAt) && !r.FlashStartedAt.Equals(_lastFlashStart))
            {
                _lastFlashStart = r.FlashStartedAt;
                _flashesShown++;
                if (!_firstFlashLogged)
                {
                    _firstFlashLogged = true;
                    TraxLog.Limited("hud", "recovery bar: first flash at " + S1(f.Now) + " s - you pressed attack " + (r.Running ? "with " + F2(r.Remaining) + " s of your pause left" : "during your own attack")
                        + " (" + AttackTimerMath.FlashPulses + " pulses of " + F2(AttackTimerMath.FlashOnSeconds) + " s)", "hud-recovery");
                }
            }
            if (r.Recovering) _fillingSeconds += f.Dt;
            _wasRunning = r.Running;
            _wasRecovering = r.Recovering;
        }

        protected override bool Refresh(in HudFrame f, bool first)
        {
            var vm = _vm;
            if (vm == null) return false;
            var s = TraxSettings.Shared;
            vm.SetLayout(s);
            if (f.Player == null || !AthleticsLogic.TryGetPlayerRecovery(f.Player, f.Now, out var r))
            {
                vm.IsShown = false;
                return false;
            }
            if (first)
            {
                TraxLog.Limited("hud", "recovery bar: first values pushed at " + S1(f.Now) + " s - " + (r.Recovering ? "refilling, " + F2(r.Share) + " of it" : "full (no pause running)")
                    + "; bar " + s.RecoveryBarWidth + " x " + s.RecoveryBarHeight + " px, " + s.PlayerBarOffsetRight + " px from the right edge and "
                    + (s.PlayerBarOffsetBottom + s.RecoveryBarOffsetAbove) + " px from the bottom (your Athletics bar's row " + s.PlayerBarOffsetBottom + " + "
                    + s.RecoveryBarOffsetAbove + "; UI pixels - the game's UI scale applies); flash on an early attack " + (s.FlashBarOnEarlyAttack ? "on" : "off"), "hud-recovery");
            }
            return true;
        }

        internal override void AddSummaryLines(List<string> lines)
        {
            var s = TraxSettings.Shared;
            lines.Add("hud: recovery bar - pauses shown " + _pausesShown + (_pausesShown > 0 ? " (avg " + F2(_pauseSum / _pausesShown) + " s, longest " + F2(_pauseMax) + " s)" : string.Empty)
                      + ", " + F1(_fillingSeconds) + " s on screen not full; flashes shown " + _flashesShown + " (FlashBarOnEarlyAttack " + (s.FlashBarOnEarlyAttack ? "on" : "off")
                      + " at the end) - your pauses and swallowed presses are in the attack rate lines");
        }

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
