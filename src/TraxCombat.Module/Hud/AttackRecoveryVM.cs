using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// The Attack recovery bar's data (module\GUI\Prefabs\TraxAttackRecoveryBar.xml binds every property
    /// here by name). The step-6 BINDING RULE holds: every property's type is EXACTLY the widget
    /// property's (float / Color / bool / string - Gauntlet converts only strings); the offline smoke
    /// checks every @binding. Setters raise a change only when the value really changed - the fill moves
    /// every frame while the bar refills, everything else rarely.
    /// </summary>
    public sealed class AttackRecoveryVM : ViewModel
    {
        private static readonly Color RecoveringColor = Color.ConvertStringToColor(AttackTimerMath.RecoveringHex);
        private static readonly Color ReadyColor = Color.ConvertStringToColor(AttackTimerMath.ReadyHex);

        private bool _isShown;
        private float _barWidth;
        private float _barHeight;
        private float _offsetRight;
        private float _offsetBottom;
        private float _fill = 1f;
        private Color _fillColor = ReadyColor;
        private string _secondsText = string.Empty;
        private bool _secondsShown;
        private bool _flashOn;

        /// <summary>The live layout: its own length and thickness, lined up with your Athletics bar's right
        /// end, RecoveryBarOffsetAbove over its row (UI pixels; the game's UI scale applies).</summary>
        internal void SetLayout(TraxSettings s)
        {
            BarWidth = s.RecoveryBarWidth;
            BarHeight = s.RecoveryBarHeight;
            OffsetRight = s.PlayerBarOffsetRight;
            OffsetBottom = s.PlayerBarOffsetBottom + s.RecoveryBarOffsetAbove;
        }

        /// <summary>One frame's reading, already turned into what the bar shows.</summary>
        internal void SetRecovery(float share, bool recovering, string secondsText, bool flash)
        {
            Fill = share;
            FillColor = recovering ? RecoveringColor : ReadyColor;
            SecondsText = secondsText;
            SecondsShown = secondsText.Length > 0;
            FlashOn = flash;
        }

        /// <summary>The row is drawn (false until the first reading).</summary>
        [DataSourceProperty]
        public bool IsShown
        {
            get => _isShown;
            set
            {
                if (value == _isShown) return;
                _isShown = value;
                OnPropertyChangedWithValue(value, nameof(IsShown));
            }
        }

        [DataSourceProperty]
        public float BarWidth
        {
            get => _barWidth;
            set
            {
                if (value == _barWidth) return;
                _barWidth = value;
                OnPropertyChangedWithValue(value, nameof(BarWidth));
            }
        }

        [DataSourceProperty]
        public float BarHeight
        {
            get => _barHeight;
            set
            {
                if (value == _barHeight) return;
                _barHeight = value;
                OnPropertyChangedWithValue(value, nameof(BarHeight));
            }
        }

        [DataSourceProperty]
        public float OffsetRight
        {
            get => _offsetRight;
            set
            {
                if (value == _offsetRight) return;
                _offsetRight = value;
                OnPropertyChangedWithValue(value, nameof(OffsetRight));
            }
        }

        [DataSourceProperty]
        public float OffsetBottom
        {
            get => _offsetBottom;
            set
            {
                if (value == _offsetBottom) return;
                _offsetBottom = value;
                OnPropertyChangedWithValue(value, nameof(OffsetBottom));
            }
        }

        /// <summary>The recovered share, 0..1 - empty during your attack, filling over the pause, full otherwise.</summary>
        [DataSourceProperty]
        public float Fill
        {
            get => _fill;
            set
            {
                if (value == _fill) return;
                _fill = value;
                OnPropertyChangedWithValue(value, nameof(Fill));
            }
        }

        /// <summary>Amber while it refills, steel when full.</summary>
        [DataSourceProperty]
        public Color FillColor
        {
            get => _fillColor;
            set
            {
                if (value == _fillColor) return;
                _fillColor = value;
                OnPropertyChangedWithValue(value, nameof(FillColor));
            }
        }

        /// <summary>"1.3 s" - the pause left, inside the bar.</summary>
        [DataSourceProperty]
        public string SecondsText
        {
            get => _secondsText;
            set
            {
                if (value == _secondsText) return;
                _secondsText = value;
                OnPropertyChangedWithValue(value, nameof(SecondsText));
            }
        }

        [DataSourceProperty]
        public bool SecondsShown
        {
            get => _secondsShown;
            set
            {
                if (value == _secondsShown) return;
                _secondsShown = value;
                OnPropertyChangedWithValue(value, nameof(SecondsShown));
            }
        }

        /// <summary>The flash overlay is lit (an early attack press - two quick pulses).</summary>
        [DataSourceProperty]
        public bool FlashOn
        {
            get => _flashOn;
            set
            {
                if (value == _flashOn) return;
                _flashOn = value;
                OnPropertyChangedWithValue(value, nameof(FlashOn));
            }
        }
    }
}
