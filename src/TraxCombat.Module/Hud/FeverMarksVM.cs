using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// The fever marks' data (module\GUI\Prefabs\TraxFeverMarks.xml, step 44): a GROW-ONLY POOL of marks for one show - mark i is the i-th
    /// mark drawn this frame (they are not keyed by man: a mark is a bar placed anew every frame, so reusing slot i for another man costs
    /// nothing and keeps the pool as small as the most marks drawn at once). Marks past this frame's count are only hidden. Same BINDING
    /// RULE as the other HUD views (AI_NOTES "Step 6"): every property EXACTLY the widget property's type; change-checked setters.
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

    /// <summary>One fever mark: a small bar (a dark frame, the fill = the fever left in its colour) placed in screen pixels.</summary>
    public sealed class FeverMarkVM : ViewModel
    {
        private static readonly Color Hidden = new Color(0f, 0f, 0f, 0f);

        private bool _isShown;
        private float _positionX;
        private float _positionY;
        private float _width;
        private float _height;
        private float _fill;
        private Color _fillColor = Hidden;
        private Color _frameColor = Hidden;

        /// <summary>One frame's values (the colours with the fade's alpha baked in).</summary>
        internal void Set(float left, float top, float width, float height, float fill, Color fillColor, Color frameColor)
        {
            PositionX = left;
            PositionY = top;
            Width = width;
            Height = height;
            Fill = fill;
            FillColor = fillColor;
            FrameColor = frameColor;
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

        /// <summary>Screen pixels (ScaledSuggestedWidth): FeverMarkWidth × the UI scale × the distance's size.</summary>
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

        /// <summary>Screen pixels (ScaledSuggestedHeight).</summary>
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

        /// <summary>The dark frame, faded with the fill.</summary>
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
    }
}
