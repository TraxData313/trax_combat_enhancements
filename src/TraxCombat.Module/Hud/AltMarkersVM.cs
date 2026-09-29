using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// The ALT labels' data (module\GUI\Prefabs\TraxAltMarkers.xml, step 20): a GROW-ONLY list of labels, one per
    /// formation that got one during this show, keyed by team + formation (<see cref="AltMarker.Key"/>) - so a label
    /// never swaps formations when vanilla re-sorts its markers, and nothing is added or removed while the set of
    /// marked formations stays the same (a label that has no marker this frame is only hidden). Same BINDING RULE as
    /// the other HUD views (AI_NOTES "Step 6"): every property EXACTLY the widget property's type; change-checked.
    /// </summary>
    public sealed class AltMarkersVM : ViewModel
    {
        private readonly int[] _keys = new int[AltMarkerFrame.MaxMarkers];

        public AltMarkersVM()
        {
            Labels = new MBBindingList<AltMarkerLabelVM>();
        }

        /// <summary>The labels (the prefab builds one widget per item when it is added).</summary>
        [DataSourceProperty]
        public MBBindingList<AltMarkerLabelVM> Labels { get; }

        /// <summary>The label of <paramref name="key"/>, added the first time it is asked for (null past
        /// <see cref="AltMarkerFrame.MaxMarkers"/>). <paramref name="added"/>: it is new.</summary>
        internal AltMarkerLabelVM? LabelFor(int key, in AltMarkerLayout layout, float scale, out bool added)
        {
            added = false;
            int n = Labels.Count;
            for (int i = 0; i < n; i++)
                if (_keys[i] == key) return Labels[i];
            if (n >= _keys.Length) return null;
            var label = new AltMarkerLabelVM(n);
            label.SetLayout(in layout, scale);
            _keys[n] = key;
            Labels.Add(label);
            added = true;
            return label;
        }

        /// <summary>The key of the label at <paramref name="index"/> (log lines).</summary>
        internal int KeyAt(int index) => index >= 0 && index < Labels.Count ? _keys[index] : int.MinValue;

        /// <summary>The live layout settings (UI pixels; the box width in screen pixels).</summary>
        internal void SetLayout(in AltMarkerLayout layout, float scale)
        {
            for (int i = 0; i < Labels.Count; i++) Labels[i].SetLayout(in layout, scale);
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            foreach (var l in Labels) l.OnFinalize();
        }
    }

    /// <summary>One formation's label: "72% ± 8" (in the colour of the men's f) and "HP 81%", then the slim bar; step 24: then
    /// "ready 34/50" (the men not bracing), plain / yellow / red.</summary>
    public sealed class AltMarkerLabelVM : ViewModel
    {
        private static readonly Color[] BandColors =
        {
            Color.ConvertStringToColor(BarMath.GreenHex),
            Color.ConvertStringToColor(BarMath.BlueHex),
            Color.ConvertStringToColor(BarMath.YellowHex),
            Color.ConvertStringToColor(BarMath.OrangeHex),
            Color.ConvertStringToColor(BarMath.RedHex),
        };

        private bool _isShown;
        private float _positionX;
        private float _positionY;
        private float _boxWidth = AltMarkerMath.BoxWidth;
        private string _athleticsText = string.Empty;
        private Color _athleticsColor = BandColors[0];
        private string _healthText = string.Empty;
        private bool _healthShown;
        private int _textSize = 16;
        private bool _barShown = true;
        private float _barWidth = 60;
        private float _barHeight = 3;
        private float _fill;
        private Color _fillColor = BandColors[0];
        private float _bandLow;
        private float _bandHigh;
        private float _peakLine = 0.75f;
        private int _mean = -1;
        private int _spread = -2;
        private int _health = -2;
        private string _readyText = string.Empty;
        private Color _readyColor = OrderStripCellVM.ReadyColors[0];
        private bool _readyShown;
        private int _ready = -1;
        private int _total = -1;

        public AltMarkerLabelVM(int index)
        {
            Index = index;
        }

        /// <summary>Its place in the list.</summary>
        internal int Index { get; }

        /// <summary>The stats version / settings version its values were pushed at (-1 = never).</summary>
        internal int PushedStats { get; set; } = -1;

        internal int PushedSettings { get; set; } = -1;

        /// <summary>The numbers now shown (-1 none): the view logs them.</summary>
        internal int ShownMean => _mean;

        internal int ShownSpread => _spread;

        internal int ShownHealth => _health;

        /// <summary>Step 24: the ready count now shown (-1 while hidden).</summary>
        internal int ShownReady => _readyShown ? _ready : -1;

        internal int ShownTotal => _readyShown ? _total : -1;

        /// <summary>Seen with a marker this frame (the view hides the rest).</summary>
        internal bool SeenThisFrame { get; set; }

        internal void SetLayout(in AltMarkerLayout layout, float scale)
        {
            TextSize = layout.TextSize;
            BarShown = layout.Bar;
            BarWidth = layout.BarWidth;
            BarHeight = layout.Bar ? layout.BarHeight : 1;
            BoxWidth = AltMarkerMath.BoxWidth * (scale > 0f ? scale : 1f);
        }

        /// <summary>The centring box's top-left in screen pixels (Scaled* widget properties).</summary>
        internal void Place(float x, float y, float boxWidth)
        {
            PositionX = x;
            PositionY = y;
            BoxWidth = boxWidth;
        }

        /// <summary>One formation's values; the texts are rebuilt only when a number changes.</summary>
        internal void SetValues(BarBand band, float fill, float low, float high, float peakLine, int mean, int spread, int health)
        {
            var colour = BandColors[(int)band];
            AthleticsColor = colour;
            FillColor = colour;
            Fill = fill;
            BandLow = low;
            BandHigh = high;
            PeakLine = peakLine;
            if (mean != _mean || spread != _spread)
            {
                _mean = mean;
                _spread = spread;
                AthleticsText = OrderStripMath.AthleticsText(mean, spread);
            }
            if (health != _health)
            {
                _health = health;
                HealthText = OrderStripMath.HealthText(health);
            }
            HealthShown = health >= 0;
        }

        /// <summary>Step 24: the ready count ("ready 34/50") - the text rebuilt only when a number changes.</summary>
        internal void SetReady(bool shown, int ready, int total, ReadyBand band)
        {
            ReadyShown = shown;
            if (!shown) return;
            ReadyColor = OrderStripCellVM.ReadyColors[(int)band];
            if (ready != _ready || total != _total)
            {
                _ready = ready;
                _total = total;
                ReadyText = ReadyMath.Text(ready, total);
            }
        }

        /// <summary>This label is drawn now (its marker is shown).</summary>
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

        /// <summary>Screen pixels from the left (ScaledPositionXOffset).</summary>
        [DataSourceProperty]
        public float PositionX
        {
            get => _positionX;
            set
            {
                if (value == _positionX) return;
                _positionX = value;
                OnPropertyChangedWithValue(value, nameof(PositionX));
            }
        }

        /// <summary>Screen pixels from the top (ScaledPositionYOffset).</summary>
        [DataSourceProperty]
        public float PositionY
        {
            get => _positionY;
            set
            {
                if (value == _positionY) return;
                _positionY = value;
                OnPropertyChangedWithValue(value, nameof(PositionY));
            }
        }

        /// <summary>The centring box's width in screen pixels (ScaledSuggestedWidth).</summary>
        [DataSourceProperty]
        public float BoxWidth
        {
            get => _boxWidth;
            set
            {
                if (value == _boxWidth) return;
                _boxWidth = value;
                OnPropertyChangedWithValue(value, nameof(BoxWidth));
            }
        }

        /// <summary>"72% ± 8".</summary>
        [DataSourceProperty]
        public string AthleticsText
        {
            get => _athleticsText;
            set
            {
                if (value == _athleticsText) return;
                _athleticsText = value;
                OnPropertyChangedWithValue(value, nameof(AthleticsText));
            }
        }

        /// <summary>The men's average f as the bar's colour band (Brush.FontColor).</summary>
        [DataSourceProperty]
        public Color AthleticsColor
        {
            get => _athleticsColor;
            set
            {
                if (value == _athleticsColor) return;
                _athleticsColor = value;
                OnPropertyChangedWithValue(value, nameof(AthleticsColor));
            }
        }

        /// <summary>"HP 81%".</summary>
        [DataSourceProperty]
        public string HealthText
        {
            get => _healthText;
            set
            {
                if (value == _healthText) return;
                _healthText = value;
                OnPropertyChangedWithValue(value, nameof(HealthText));
            }
        }

        /// <summary>ShowFormationHealth on and the health known.</summary>
        [DataSourceProperty]
        public bool HealthShown
        {
            get => _healthShown;
            set
            {
                if (value == _healthShown) return;
                _healthShown = value;
                OnPropertyChangedWithValue(value, nameof(HealthShown));
            }
        }

        /// <summary>Step 24: "ready 34/50" - men not bracing of the formation.</summary>
        [DataSourceProperty]
        public string ReadyText
        {
            get => _readyText;
            set
            {
                if (value == _readyText) return;
                _readyText = value;
                OnPropertyChangedWithValue(value, nameof(ReadyText));
            }
        }

        /// <summary>Step 24: plain, yellow or red by the share ready (Brush.FontColor).</summary>
        [DataSourceProperty]
        public Color ReadyColor
        {
            get => _readyColor;
            set
            {
                if (value == _readyColor) return;
                _readyColor = value;
                OnPropertyChangedWithValue(value, nameof(ReadyColor));
            }
        }

        /// <summary>Step 24: ShowReadyCount on and bracing possible (ReadyRules).</summary>
        [DataSourceProperty]
        public bool ReadyShown
        {
            get => _readyShown;
            set
            {
                if (value == _readyShown) return;
                _readyShown = value;
                OnPropertyChangedWithValue(value, nameof(ReadyShown));
            }
        }

        /// <summary>AltMarkerTextSize (Brush.FontSize is an int).</summary>
        [DataSourceProperty]
        public int TextSize
        {
            get => _textSize;
            set
            {
                if (value == _textSize) return;
                _textSize = value;
                OnPropertyChangedWithValue(value, nameof(TextSize));
            }
        }

        /// <summary>AltMarkerBarHeight above 0.</summary>
        [DataSourceProperty]
        public bool BarShown
        {
            get => _barShown;
            set
            {
                if (value == _barShown) return;
                _barShown = value;
                OnPropertyChangedWithValue(value, nameof(BarShown));
            }
        }

        /// <summary>AltMarkerBarWidth, UI pixels (SuggestedWidth).</summary>
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

        /// <summary>AltMarkerBarHeight, UI pixels (SuggestedHeight).</summary>
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

        /// <summary>The men's mean share of their pools, 0..1.</summary>
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

        /// <summary>By the men's average f (BarMath bands).</summary>
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

        /// <summary>The ± band's low edge, 0..1.</summary>
        [DataSourceProperty]
        public float BandLow
        {
            get => _bandLow;
            set
            {
                if (value == _bandLow) return;
                _bandLow = value;
                OnPropertyChangedWithValue(value, nameof(BandLow));
            }
        }

        /// <summary>The ± band's high edge, 0..1.</summary>
        [DataSourceProperty]
        public float BandHigh
        {
            get => _bandHigh;
            set
            {
                if (value == _bandHigh) return;
                _bandHigh = value;
                OnPropertyChangedWithValue(value, nameof(BandHigh));
            }
        }

        /// <summary>The peak line (AthleticsPeakPercent of the pool), 0..1.</summary>
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
    }
}
