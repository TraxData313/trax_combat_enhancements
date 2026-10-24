using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// Step 33: the sprint bar's data (module\GUI\Prefabs\TraxSprintBar.xml binds every property here by name). The step-6
    /// BINDING RULE holds: every property's type is EXACTLY the widget property's (float / Color / bool / string - Gauntlet
    /// converts only strings); the offline smoke checks every @binding. Setters raise a change only when the value really
    /// changed; the label is rebuilt only when its tenth of a second moves.
    /// </summary>
    public sealed class SprintBarVM : ViewModel
    {
        private static readonly Color BelowStartColor = Color.ConvertStringToColor(SprintMath.BelowStartHex);
        private static readonly Color ReadyColor = Color.ConvertStringToColor(SprintMath.ReadyHex);
        private static readonly Color SprintingColor = Color.ConvertStringToColor(SprintMath.SprintingHex);

        private bool _isShown;
        private float _barWidth;
        private float _barHeight;
        private float _offsetRight;
        private float _offsetBottom;
        private float _fill;
        private Color _fillColor = ReadyColor;
        private float _markOffset;
        private bool _markShown = true;
        private string _secondsText = string.Empty;
        private int _labelTenths = -1;
        private string _keyText = string.Empty;
        private bool _keyTextShown;
        private string _keyGlyph = string.Empty;
        private bool _keyGlyphShown;

        /// <summary>The live layout: as long and thick as the Attack recovery bar, lined up with your Athletics bar's right end,
        /// SprintBarOffsetAbove over its row (UI pixels; the game's UI scale applies).</summary>
        internal void SetLayout(TraxSettings s, double minShare)
        {
            BarWidth = s.RecoveryBarWidth;
            BarHeight = s.RecoveryBarHeight;
            OffsetRight = s.PlayerBarOffsetRight;
            OffsetBottom = s.PlayerBarOffsetBottom + s.SprintBarOffsetAbove;
            SetMark(minShare);
        }

        /// <summary>The start mark: at the start share of the bar's inside (hidden at 0 - any amount starts).</summary>
        internal void SetMark(double minShare)
        {
            MarkShown = minShare > 0.001 && minShare < 0.999;
            MarkOffset = (float)((BarWidth - 2) * minShare);
        }

        /// <summary>One frame's reading.</summary>
        internal void SetReading(in SprintReading r)
        {
            Fill = (float)r.Share;
            FillColor = r.Active ? SprintingColor : r.Ready ? ReadyColor : BelowStartColor;
            int tenths = (int)System.Math.Floor(r.Stored * 10 + 1e-9);
            if (tenths != _labelTenths)
            {
                _labelTenths = tenths;
                SecondsText = SprintMath.SecondsText(r.Stored);
            }
        }

        /// <summary>Step 86: the key hint - "(Ctrl)" as text, or the game's glyph by its InputKey name ("" = none drawn: the game's widget
        /// shows no sprite). The view calls it only when the key, the device or the switch changed.</summary>
        internal void SetKeyHint(in SprintKeyHint hint, bool glyphNow)
        {
            KeyText = hint.Kind == SprintHintKind.Text ? hint.Text : string.Empty;
            KeyTextShown = hint.Kind == SprintHintKind.Text;
            KeyGlyph = hint.Kind == SprintHintKind.Glyph && glyphNow ? hint.GlyphKey : string.Empty;
            KeyGlyphShown = hint.Kind == SprintHintKind.Glyph && glyphNow;
        }

        /// <summary>"Sprint 6.2 s" - the words as before step 86 (the log and the smoke read it; built only when read).</summary>
        internal string LabelText => WordText + " " + _secondsText;

        /// <summary>The bar's word, "Sprint".</summary>
        [DataSourceProperty]
        public string WordText => SprintMath.LabelWord;

        /// <summary>"6.2 s" - after the key hint (step 86 split the label: the word, the hint, the seconds).</summary>
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

        /// <summary>Step 86: "(Ctrl)" - the keyboard key, or the controller button's short name when its glyph cannot be drawn.</summary>
        [DataSourceProperty]
        public string KeyText
        {
            get => _keyText;
            set
            {
                if (value == _keyText) return;
                _keyText = value;
                OnPropertyChangedWithValue(value, nameof(KeyText));
            }
        }

        [DataSourceProperty]
        public bool KeyTextShown
        {
            get => _keyTextShown;
            set
            {
                if (value == _keyTextShown) return;
                _keyTextShown = value;
                OnPropertyChangedWithValue(value, nameof(KeyTextShown));
            }
        }

        /// <summary>Step 86: the controller button's InputKey name for the game's InputKeyVisualWidget ("ControllerLThumb"), "" = none.</summary>
        [DataSourceProperty]
        public string KeyGlyph
        {
            get => _keyGlyph;
            set
            {
                if (value == _keyGlyph) return;
                _keyGlyph = value;
                OnPropertyChangedWithValue(value, nameof(KeyGlyph));
            }
        }

        [DataSourceProperty]
        public bool KeyGlyphShown
        {
            get => _keyGlyphShown;
            set
            {
                if (value == _keyGlyphShown) return;
                _keyGlyphShown = value;
                OnPropertyChangedWithValue(value, nameof(KeyGlyphShown));
            }
        }

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

        /// <summary>The store's share, 0..1.</summary>
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

        /// <summary>Grey below the start mark, blue ready, yellow while sprinting.</summary>
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

        /// <summary>The start mark's distance from the inside's left edge, UI pixels.</summary>
        [DataSourceProperty]
        public float MarkOffset
        {
            get => _markOffset;
            set
            {
                if (value == _markOffset) return;
                _markOffset = value;
                OnPropertyChangedWithValue(value, nameof(MarkOffset));
            }
        }

        [DataSourceProperty]
        public bool MarkShown
        {
            get => _markShown;
            set
            {
                if (value == _markShown) return;
                _markShown = value;
                OnPropertyChangedWithValue(value, nameof(MarkShown));
            }
        }
    }
}
