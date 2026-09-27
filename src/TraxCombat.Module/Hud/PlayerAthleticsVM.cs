using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// The player bar's data (module\GUI\Prefabs\TraxPlayerAthleticsBar.xml binds every property
    /// here by name). BINDING RULE (step 6, verified in the game's source - AI_NOTES "Step 6"):
    /// Gauntlet hands a bound value to the widget's property by reflection and converts only
    /// STRINGS (to a Sprite, Brush, int or Color), so every property's type must be EXACTLY the
    /// widget property's: float for SuggestedWidth / Margin* / InitialAmountAsFloat, Color for Color
    /// and Brush.FontColor, bool for IsVisible, string for Text. A mismatch throws inside the game's
    /// binding - the offline smoke checks every @binding of the prefab against these types.
    /// Setters raise a change only when the value really changed; the number text is rebuilt only
    /// when one of its two numbers moved.
    /// </summary>
    public sealed class PlayerAthleticsVM : ViewModel
    {
        private static readonly Color[] BandColors =
        {
            Color.ConvertStringToColor(BarMath.GreenHex),
            Color.ConvertStringToColor(BarMath.BlueHex),
            Color.ConvertStringToColor(BarMath.YellowHex),
            Color.ConvertStringToColor(BarMath.OrangeHex),
            Color.ConvertStringToColor(BarMath.RedHex),
        };

        private static readonly Color TextColor = Color.ConvertStringToColor(BarMath.TextHex);
        private static readonly Color QuietColor = Color.ConvertStringToColor(BarMath.LabelHex);
        private static readonly Color FrameNormal = Color.ConvertStringToColor(BarMath.FrameHex);
        private static readonly Color AlarmColor = Color.ConvertStringToColor(BarMath.ExhaustedHex);

        private bool _isShown;
        private float _barWidth;
        private float _barHeight;
        private float _offsetRight;
        private float _offsetBottom;
        private float _fill;
        private float _usable = 1f;
        private float _peakLine = 0.75f;
        private Color _fillColor = BandColors[0];
        private Color _frameColor = FrameNormal;
        private Color _numberColor = TextColor;
        private Color _labelColor = QuietColor;
        private string _numberText = string.Empty;
        private string _labelText = BarMath.LabelText;
        private int _shownPoints = -1;
        private int _shownPool = -1;

        /// <summary>The live layout settings (UI pixels; the game's UI scale applies).</summary>
        internal void SetLayout(TraxSettings s)
        {
            BarWidth = s.PlayerBarWidth;
            BarHeight = s.PlayerBarHeight;
            OffsetRight = s.PlayerBarOffsetRight;
            OffsetBottom = s.PlayerBarOffsetBottom;
        }

        /// <summary>One reading, already turned into what the bar shows (Core's BarMath).</summary>
        internal void SetValues(BarBand band, float fill, float usable, float peakLine, int shownPoints, int shownPool, bool exhausted)
        {
            Fill = fill;
            Usable = usable;
            PeakLine = peakLine;
            FillColor = BandColors[(int)band];
            if (shownPoints != _shownPoints || shownPool != _shownPool)
            {
                _shownPoints = shownPoints;
                _shownPool = shownPool;
                NumberText = BarMath.NumberText(shownPoints, shownPool);
            }
            NumberColor = exhausted ? AlarmColor : TextColor;
            LabelColor = exhausted ? AlarmColor : QuietColor;
            FrameColor = exhausted ? AlarmColor : FrameNormal;
            LabelText = exhausted ? BarMath.ExhaustedLabelText : BarMath.LabelText;
        }

        /// <summary>The row is drawn (false until the first reading, or when the player has none).</summary>
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

        /// <summary>Points ÷ pool, 0..1 - the coloured fill (a FillBarWidget, pixel-free).</summary>
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

        /// <summary>The usable share, 0..1 - beyond it the bar is dark (wounds cap it).</summary>
        [DataSourceProperty]
        public float Usable
        {
            get => _usable;
            set
            {
                if (value == _usable) return;
                _usable = value;
                OnPropertyChangedWithValue(value, nameof(Usable));
            }
        }

        /// <summary>The peak line as a share of the bar, 0..1 - the marker's place.</summary>
        [DataSourceProperty]
        public float PeakLine
        {
            get => _peakLine;
            set
            {
                if (value == _peakLine) return;
                _peakLine = value;
                OnPropertyChangedWithValue(value, nameof(PeakLine));
            }
        }

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

        [DataSourceProperty]
        public Color FrameColor
        {
            get => _frameColor;
            set
            {
                if (value == _frameColor) return;
                _frameColor = value;
                OnPropertyChangedWithValue(value, nameof(FrameColor));
            }
        }

        [DataSourceProperty]
        public Color NumberColor
        {
            get => _numberColor;
            set
            {
                if (value == _numberColor) return;
                _numberColor = value;
                OnPropertyChangedWithValue(value, nameof(NumberColor));
            }
        }

        [DataSourceProperty]
        public Color LabelColor
        {
            get => _labelColor;
            set
            {
                if (value == _labelColor) return;
                _labelColor = value;
                OnPropertyChangedWithValue(value, nameof(LabelColor));
            }
        }

        /// <summary>"132 / 180".</summary>
        [DataSourceProperty]
        public string NumberText
        {
            get => _numberText;
            set
            {
                if (value == _numberText) return;
                _numberText = value;
                OnPropertyChangedWithValue(value, nameof(NumberText));
            }
        }

        /// <summary>"Athletics", or "Exhausted" while the bar is empty.</summary>
        [DataSourceProperty]
        public string LabelText
        {
            get => _labelText;
            set
            {
                if (value == _labelText) return;
                _labelText = value;
                OnPropertyChangedWithValue(value, nameof(LabelText));
            }
        }
    }
}
