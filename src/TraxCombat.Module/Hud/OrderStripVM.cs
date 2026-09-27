using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// The orders-menu strip's data (module\GUI\Prefabs\TraxOrderStrip.xml). One fixed list of 8
    /// cells - FormationClass 0..7, the vanilla cards' slots - bound TWICE by the prefab: as the
    /// cells under the cards (placed in screen pixels) and as the rows of the fallback panel. Only one
    /// of the two is drawn (<see cref="StripShown"/> / <see cref="PanelShown"/>). Same BINDING RULE as
    /// the player bar (AI_NOTES "Step 6"): every property EXACTLY the widget property's type - float
    /// for sizes, margins and the Scaled* pixel properties, int for Brush.FontSize, Color, bool,
    /// string; change-checked setters.
    /// </summary>
    public sealed class OrderStripVM : ViewModel
    {
        private bool _stripShown;
        private bool _panelShown;
        private float _panelOffsetTop;
        private float _panelWidth;

        public OrderStripVM()
        {
            Cells = new MBBindingList<OrderStripCellVM>();
            for (int k = 0; k < OrderStripMath.CardsPerSet; k++) Cells.Add(new OrderStripCellVM(k));
        }

        /// <summary>FormationClass 0..7 - the list never changes (the prefab builds 8 cells and 8 rows once).</summary>
        [DataSourceProperty]
        public MBBindingList<OrderStripCellVM> Cells { get; }

        /// <summary>The cells under the cards are drawn.</summary>
        [DataSourceProperty]
        public bool StripShown
        {
            get => _stripShown;
            set
            {
                if (value == _stripShown) return;
                _stripShown = value;
                OnPropertyChangedWithValue(value, nameof(StripShown));
            }
        }

        /// <summary>The fallback panel is drawn.</summary>
        [DataSourceProperty]
        public bool PanelShown
        {
            get => _panelShown;
            set
            {
                if (value == _panelShown) return;
                _panelShown = value;
                OnPropertyChangedWithValue(value, nameof(PanelShown));
            }
        }

        [DataSourceProperty]
        public float PanelOffsetTop
        {
            get => _panelOffsetTop;
            set
            {
                if (value == _panelOffsetTop) return;
                _panelOffsetTop = value;
                OnPropertyChangedWithValue(value, nameof(PanelOffsetTop));
            }
        }

        [DataSourceProperty]
        public float PanelWidth
        {
            get => _panelWidth;
            set
            {
                if (value == _panelWidth) return;
                _panelWidth = value;
                OnPropertyChangedWithValue(value, nameof(PanelWidth));
            }
        }

        /// <summary>The live layout settings (UI pixels; the game's UI scale applies).</summary>
        internal void SetLayout(TraxSettings s, in StripLayout layout)
        {
            PanelOffsetTop = s.OrderPanelOffsetTop;
            PanelWidth = s.OrderPanelWidth;
            for (int k = 0; k < Cells.Count; k++) Cells[k].SetLayout(in layout);
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            foreach (var c in Cells) c.OnFinalize();
        }
    }

    /// <summary>One formation's cell (under its card) and row (in the panel).</summary>
    public sealed class OrderStripCellVM : ViewModel
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
        private float _cellWidth;
        private float _fill;
        private Color _fillColor = BandColors[0];
        private float _bandLow;
        private float _bandHigh;
        private float _peakLine = 0.75f;
        private string _athleticsText = string.Empty;
        private string _healthText = string.Empty;
        private bool _healthShown;
        private int _textSize = 13;
        private float _textMarginTop;
        private float _barMarginTop;
        private float _barHeight = 4;
        private float _sideMargin;
        private int _mean = -1;
        private int _spread = -2;
        private int _health = -2;

        public OrderStripCellVM(int formationIndex)
        {
            FormationIndex = formationIndex;
            Name = OrderStripMath.FormationName(formationIndex);
        }

        /// <summary>FormationClass 0..7 - the card slot.</summary>
        internal int FormationIndex { get; }

        /// <summary>The numbers now shown (-1 none): the view logs them.</summary>
        internal int ShownMean => _mean;

        internal int ShownSpread => _spread;

        internal int ShownHealth => _health;

        internal void SetLayout(in StripLayout layout)
        {
            TextSize = layout.TextSize;
            TextMarginTop = layout.TextMarginTop;
            BarMarginTop = layout.BarMarginTop;
            BarHeight = layout.BarHeight;
            SideMargin = layout.SideMargin;
        }

        /// <summary>Where the cell goes, in screen pixels (bound to the Scaled* widget properties).</summary>
        internal void Place(float x, float y, float width)
        {
            PositionX = x;
            PositionY = y;
            CellWidth = width;
        }

        /// <summary>One formation's values; the texts are rebuilt only when a number changes.</summary>
        internal void SetValues(BarBand band, float fill, float low, float high, float peakLine, int mean, int spread, int health)
        {
            Fill = fill;
            FillColor = BandColors[(int)band];
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

        /// <summary>This formation has a cell (strip) / a row (panel) now.</summary>
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

        /// <summary>"1 Infantry" - the panel's rows.</summary>
        [DataSourceProperty]
        public string Name { get; }

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

        /// <summary>The card's width in screen pixels (ScaledSuggestedWidth).</summary>
        [DataSourceProperty]
        public float CellWidth
        {
            get => _cellWidth;
            set
            {
                if (value == _cellWidth) return;
                _cellWidth = value;
                OnPropertyChangedWithValue(value, nameof(CellWidth));
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

        /// <summary>The ± band's low edge, 0..1 (FillBarWidget InitialAmountAsFloat).</summary>
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

        /// <summary>The ± band's high edge, 0..1 (FillBarWidget CurrentAmountAsFloat - its ChangeWidget
        /// spans low → high).</summary>
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

        /// <summary>OrderStripTextSize (Brush.FontSize is an int).</summary>
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

        [DataSourceProperty]
        public float TextMarginTop
        {
            get => _textMarginTop;
            set
            {
                if (value == _textMarginTop) return;
                _textMarginTop = value;
                OnPropertyChangedWithValue(value, nameof(TextMarginTop));
            }
        }

        [DataSourceProperty]
        public float BarMarginTop
        {
            get => _barMarginTop;
            set
            {
                if (value == _barMarginTop) return;
                _barMarginTop = value;
                OnPropertyChangedWithValue(value, nameof(BarMarginTop));
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
        public float SideMargin
        {
            get => _sideMargin;
            set
            {
                if (value == _sideMargin) return;
                _sideMargin = value;
                OnPropertyChangedWithValue(value, nameof(SideMargin));
            }
        }
    }
}
