using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// The ALT bars' data (module\GUI\Prefabs\TraxFeverMarks.xml - step 44's fever marks, step 46's Athletics bars): a GROW-ONLY POOL of marks
    /// for one show - mark i is the i-th MAN marked this frame (not keyed by man: a mark is a stack of bars placed anew every frame, so reusing
    /// slot i for another man costs nothing and keeps the pool as small as the most men marked at once). Marks past this frame's count are
    /// only hidden. Same BINDING RULE as the other HUD views (AI_NOTES "Step 6"): every property EXACTLY the widget property's type;
    /// change-checked setters.
    /// </summary>
    public sealed class FeverMarksVM : ViewModel
    {
        public FeverMarksVM()
        {
            Marks = new MBBindingList<FeverMarkVM>();
        }

        /// <summary>The pool (the prefab builds one widget per item when it is added).</summary>
        [DataSourceProperty]
        public MBBindingList<FeverMarkVM> Marks { get; }

        /// <summary>Mark <paramref name="index"/>, added when the pool is that small (null past <paramref name="most"/>).</summary>
        internal FeverMarkVM? MarkAt(int index, int most)
        {
            if (index < Marks.Count) return Marks[index];
            if (index >= most || index != Marks.Count) return null;
            var mark = new FeverMarkVM();
            Marks.Add(mark);
            return mark;
        }

        /// <summary>Hides every mark from <paramref name="first"/> on (this frame drew fewer).</summary>
        internal void HideFrom(int first)
        {
            for (int i = first < 0 ? 0 : first; i < Marks.Count; i++) Marks[i].IsShown = false;
        }

        /// <summary>Marks drawn now (the smoke and the log).</summary>
        internal int ShownCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Marks.Count; i++)
                    if (Marks[i].IsShown) n++;
                return n;
            }
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            foreach (var m in Marks) m.OnFinalize();
        }
    }

    /// <summary>
    /// One man's mark: a box placed in screen pixels (its bottom centre on his point) holding, centred, his ATHLETICS BAR at the top (step 46: a
    /// dark frame, the wounded part dark red, the reachable part dark, the fill = his Athletics left in the band's colour) and his FEVER BAR at the
    /// bottom (step 44: a dark frame, the fill = the fever left, orange → yellow). Either may be hidden; the fever bar's place is kept (its
    /// widget hidden) while he has no fever, so his Athletics bar never jumps.
    /// </summary>
    public sealed class FeverMarkVM : ViewModel
    {
        private static readonly Color Hidden = new Color(0f, 0f, 0f, 0f);

        private bool _isShown;
        private float _positionX;
        private float _positionY;
        private float _boxWidth;
        private float _boxHeight;
        private bool _feverShown;
        private float _width;
        private float _height;
        private float _fill;
        private Color _fillColor = Hidden;
        private Color _frameColor = Hidden;
        private bool _barShown;
        private float _barWidth;
        private float _barHeight;
        private float _barFill;
        private float _barUsable = 1f;
        private Color _barColor = Hidden;
        private Color _barFrameColor = Hidden;
        private Color _barWoundColor = Hidden;
        private bool _moraleShown;
        private float _moraleWidth = 1f;
        private float _moraleHeight = 1f;
        private float _moraleFill;
        private Color _moraleColor = Hidden;
        private Color _moraleFrameColor = Hidden;
        private float _feverOffsetY;

        /// <summary>Step 67: this frame's morale bar (at the box's bottom - the fever bar lifted above it by <paramref name="feverLift"/>).
        /// <paramref name="shown"/> false with a place kept = no morale known for him (a tournament).</summary>
        internal void SetMorale(bool shown, in MarkBox box, float fill, Color color, Color frameColor)
        {
            MoraleShown = shown;
            MoraleWidth = box.MoraleWidth > 0f ? box.MoraleWidth : 1f;
            MoraleHeight = box.MoraleHeight > 0f ? box.MoraleHeight : 1f;
            FeverOffsetY = -box.FeverLift;
            if (!shown) return;
            MoraleFill = fill;
            MoraleColor = color;
            MoraleFrameColor = frameColor;
        }

        /// <summary>One frame's values (the colours with the fade's alpha baked in).</summary>
        internal void Set(in MarkBox box, bool hasFever, float fill, Color fillColor, Color frameColor,
            bool hasBar, float barFill, float barUsable, Color barColor, Color barFrameColor, Color barWoundColor)
        {
            PositionX = box.Left;
            PositionY = box.Top;
            BoxWidth = box.Width;
            BoxHeight = box.Height;
            FeverShown = hasFever;
            Width = box.FeverWidth > 0f ? box.FeverWidth : 1f;
            Height = box.FeverHeight > 0f ? box.FeverHeight : 1f;
            if (hasFever)
            {
                Fill = fill;
                FillColor = fillColor;
                FrameColor = frameColor;
            }
            BarShown = hasBar;
            BarWidth = box.BarWidth > 0f ? box.BarWidth : 1f;
            BarHeight = box.BarHeight > 0f ? box.BarHeight : 1f;
            if (hasBar)
            {
                BarFill = barFill;
                BarUsable = barUsable;
                BarColor = barColor;
                BarFrameColor = barFrameColor;
                BarWoundColor = barWoundColor;
            }
            IsShown = true;
        }

        /// <summary>Drawn this frame.</summary>
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

        /// <summary>The box's left, screen pixels (ScaledPositionXOffset).</summary>
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

        /// <summary>The box's top, screen pixels (ScaledPositionYOffset).</summary>
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

        /// <summary>Step 46: the box, screen pixels (ScaledSuggestedWidth / Height): the wider bar × the stack's height.</summary>
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

        [DataSourceProperty]
        public float BoxHeight
        {
            get => _boxHeight;
            set
            {
                if (value == _boxHeight) return;
                _boxHeight = value;
                OnPropertyChangedWithValue(value, nameof(BoxHeight));
            }
        }

        // ------------------------------------------------------------------ the fever bar (step 44)

        /// <summary>Step 46: the fever bar drawn (he is in a fever and its switches let it show) - its place may be kept without it.</summary>
        [DataSourceProperty]
        public bool FeverShown
        {
            get => _feverShown;
            set
            {
                if (value == _feverShown) return;
                _feverShown = value;
                OnPropertyChangedWithValue(value, nameof(FeverShown));
            }
        }

        /// <summary>The fever bar, screen pixels (ScaledSuggestedWidth): FeverMarkWidth × the UI scale × the distance's size.</summary>
        [DataSourceProperty]
        public float Width
        {
            get => _width;
            set
            {
                if (value == _width) return;
                _width = value;
                OnPropertyChangedWithValue(value, nameof(Width));
            }
        }

        /// <summary>The fever bar, screen pixels (ScaledSuggestedHeight).</summary>
        [DataSourceProperty]
        public float Height
        {
            get => _height;
            set
            {
                if (value == _height) return;
                _height = value;
                OnPropertyChangedWithValue(value, nameof(Height));
            }
        }

        /// <summary>The fever left ÷ full, 0..1 (1 while armed).</summary>
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

        /// <summary>Orange → yellow by the fever, its alpha × vanilla's distance fade (<see cref="FeverMarkMath.Tint"/>).</summary>
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

        /// <summary>The fever bar's dark frame, faded with the fill.</summary>
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

        // ------------------------------------------------------------------ the Athletics bar (step 46)

        /// <summary>His Athletics bar drawn.</summary>
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

        /// <summary>The Athletics bar, screen pixels (ScaledSuggestedWidth): AthleticsMarkWidth × the UI scale × the distance's size.</summary>
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

        /// <summary>His points ÷ his FULL pool, 0..1 (never past <see cref="BarUsable"/>).</summary>
        [DataSourceProperty]
        public float BarFill
        {
            get => _barFill;
            set
            {
                if (value == _barFill) return;
                _barFill = value;
                OnPropertyChangedWithValue(value, nameof(BarFill));
            }
        }

        /// <summary>The usable share (the wound cap): dark up to it, dark red beyond.</summary>
        [DataSourceProperty]
        public float BarUsable
        {
            get => _barUsable;
            set
            {
                if (value == _barUsable) return;
                _barUsable = value;
                OnPropertyChangedWithValue(value, nameof(BarUsable));
            }
        }

        /// <summary>The band's colour (BarMath - green / blue / yellow / orange / red), alpha = vanilla's distance fade.</summary>
        [DataSourceProperty]
        public Color BarColor
        {
            get => _barColor;
            set
            {
                if (value == _barColor) return;
                _barColor = value;
                OnPropertyChangedWithValue(value, nameof(BarColor));
            }
        }

        /// <summary>The dark frame and the reachable empty part, faded.</summary>
        [DataSourceProperty]
        public Color BarFrameColor
        {
            get => _barFrameColor;
            set
            {
                if (value == _barFrameColor) return;
                _barFrameColor = value;
                OnPropertyChangedWithValue(value, nameof(BarFrameColor));
            }
        }

        // ------------------------------------------------------------------ the morale bar (step 67)

        /// <summary>Step 67: his morale bar drawn (ShowMoraleMarks, his side, a morale known).</summary>
        [DataSourceProperty]
        public bool MoraleShown
        {
            get => _moraleShown;
            set
            {
                if (value == _moraleShown) return;
                _moraleShown = value;
                OnPropertyChangedWithValue(value, nameof(MoraleShown));
            }
        }

        /// <summary>The morale bar, screen pixels - the Athletics bar's size.</summary>
        [DataSourceProperty]
        public float MoraleWidth
        {
            get => _moraleWidth;
            set
            {
                if (value == _moraleWidth) return;
                _moraleWidth = value;
                OnPropertyChangedWithValue(value, nameof(MoraleWidth));
            }
        }

        [DataSourceProperty]
        public float MoraleHeight
        {
            get => _moraleHeight;
            set
            {
                if (value == _moraleHeight) return;
                _moraleHeight = value;
                OnPropertyChangedWithValue(value, nameof(MoraleHeight));
            }
        }

        /// <summary>His morale ÷ 100, 0..1.</summary>
        [DataSourceProperty]
        public float MoraleFill
        {
            get => _moraleFill;
            set
            {
                if (value == _moraleFill) return;
                _moraleFill = value;
                OnPropertyChangedWithValue(value, nameof(MoraleFill));
            }
        }

        /// <summary>The morale blue, faded like the others.</summary>
        [DataSourceProperty]
        public Color MoraleColor
        {
            get => _moraleColor;
            set
            {
                if (value == _moraleColor) return;
                _moraleColor = value;
                OnPropertyChangedWithValue(value, nameof(MoraleColor));
            }
        }

        [DataSourceProperty]
        public Color MoraleFrameColor
        {
            get => _moraleFrameColor;
            set
            {
                if (value == _moraleFrameColor) return;
                _moraleFrameColor = value;
                OnPropertyChangedWithValue(value, nameof(MoraleFrameColor));
            }
        }

        /// <summary>The fever bar lifted above the morale bar (screen pixels, negative = up; 0 without a morale bar).</summary>
        [DataSourceProperty]
        public float FeverOffsetY
        {
            get => _feverOffsetY;
            set
            {
                if (value == _feverOffsetY) return;
                _feverOffsetY = value;
                OnPropertyChangedWithValue(value, nameof(FeverOffsetY));
            }
        }

        /// <summary>The wounded part (the player bar's dark red), faded.</summary>
        [DataSourceProperty]
        public Color BarWoundColor
        {
            get => _barWoundColor;
            set
            {
                if (value == _barWoundColor) return;
                _barWoundColor = value;
                OnPropertyChangedWithValue(value, nameof(BarWoundColor));
            }
        }
    }
}
