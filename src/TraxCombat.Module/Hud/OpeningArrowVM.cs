using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// Step 106c - the red arrow's data (module\GUI\Prefabs\TraxOpeningArrow.xml binds every property here by name). The step-6 BINDING RULE
    /// holds: each type is EXACTLY the widget property's - the arrows' strength binds the brush's <c>GlobalAlphaFactor</c> (float), their
    /// visibility <c>IsVisible</c> (bool). Setters raise a change only when the value really changed (the flash moves every frame for 0.2 s,
    /// the steady arrow not at all).
    /// </summary>
    public sealed class OpeningArrowVM : ViewModel
    {
        private float _topAlpha, _bottomAlpha, _leftAlpha, _rightAlpha;
        private bool _topShown, _bottomShown, _leftShown, _rightShown;
        private Color _arrowColor = new Color(1f, 1f, 1f, 1f);

        /// <summary>One frame's reading (an empty one hides all four).</summary>
        internal void Set(in ArrowReading r)
        {
            bool on = r.Shows;
            float a = on ? r.Alpha : 0f;
            Apply(on && OpeningArrowMath.Lit(r.Mask, OpeningArrow.Top), a, ref _topShown, ref _topAlpha, nameof(TopShown), nameof(TopAlpha));
            Apply(on && OpeningArrowMath.Lit(r.Mask, OpeningArrow.Bottom), a, ref _bottomShown, ref _bottomAlpha, nameof(BottomShown), nameof(BottomAlpha));
            Apply(on && OpeningArrowMath.Lit(r.Mask, OpeningArrow.Left), a, ref _leftShown, ref _leftAlpha, nameof(LeftShown), nameof(LeftAlpha));
            Apply(on && OpeningArrowMath.Lit(r.Mask, OpeningArrow.Right), a, ref _rightShown, ref _rightAlpha, nameof(RightShown), nameof(RightAlpha));
        }

        /// <summary>Step 112: the arrows' colour (the role's - Core <see cref="ArrowColours"/>); a change only when it really changed.</summary>
        internal void SetColor(float r, float g, float b, float a) => ArrowColor = new Color(r, g, b, a);

        private void Apply(bool lit, float alpha, ref bool shown, ref float stored, string shownName, string alphaName)
        {
            float want = lit ? alpha : 0f;
            if (want != stored)
            {
                stored = want;
                OnPropertyChangedWithValue(want, alphaName);
            }
            if (lit != shown)
            {
                shown = lit;
                OnPropertyChangedWithValue(lit, shownName);
            }
        }

        /// <summary>Step 112: the brush colour of all four arrows (<c>Brush.Color</c>, a Color - multiplied onto vanilla's sprite).</summary>
        [DataSourceProperty]
        public Color ArrowColor
        {
            get => _arrowColor;
            set { if (value != _arrowColor) { _arrowColor = value; OnPropertyChangedWithValue(value, nameof(ArrowColor)); } }
        }

        [DataSourceProperty]
        public float TopAlpha
        {
            get => _topAlpha;
            set { if (value != _topAlpha) { _topAlpha = value; OnPropertyChangedWithValue(value, nameof(TopAlpha)); } }
        }

        [DataSourceProperty]
        public float BottomAlpha
        {
            get => _bottomAlpha;
            set { if (value != _bottomAlpha) { _bottomAlpha = value; OnPropertyChangedWithValue(value, nameof(BottomAlpha)); } }
        }

        [DataSourceProperty]
        public float LeftAlpha
        {
            get => _leftAlpha;
            set { if (value != _leftAlpha) { _leftAlpha = value; OnPropertyChangedWithValue(value, nameof(LeftAlpha)); } }
        }

        [DataSourceProperty]
        public float RightAlpha
        {
            get => _rightAlpha;
            set { if (value != _rightAlpha) { _rightAlpha = value; OnPropertyChangedWithValue(value, nameof(RightAlpha)); } }
        }

        [DataSourceProperty]
        public bool TopShown
        {
            get => _topShown;
            set { if (value != _topShown) { _topShown = value; OnPropertyChangedWithValue(value, nameof(TopShown)); } }
        }

        [DataSourceProperty]
        public bool BottomShown
        {
            get => _bottomShown;
            set { if (value != _bottomShown) { _bottomShown = value; OnPropertyChangedWithValue(value, nameof(BottomShown)); } }
        }

        [DataSourceProperty]
        public bool LeftShown
        {
            get => _leftShown;
            set { if (value != _leftShown) { _leftShown = value; OnPropertyChangedWithValue(value, nameof(LeftShown)); } }
        }

        [DataSourceProperty]
        public bool RightShown
        {
            get => _rightShown;
            set { if (value != _rightShown) { _rightShown = value; OnPropertyChangedWithValue(value, nameof(RightShown)); } }
        }
    }
}
