using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// THE ALT BARS - one view, one stack of bars per man (AI_NOTES "Step 44" and "Step 46"; DESIGN §3 items 3b and 3c). While the game shows its
    /// markers (step 20's flag: ALT held or the orders menu open):
    ///   step 46 (Anton, 2026-10-17: "how tired they are in a bar above that fever bar ... a green bar that gets to red") - a small ATHLETICS BAR
    ///     above every soldier's head: the fill = his points ÷ his FULL pool, the wound cap dark red (the player bar's way), the colour by f
    ///     through BarMath's bands (green / blue / yellow / orange / red). Yours and your allies' always; the enemy's with
    ///     AthleticsMarksShowEnemy. AI men on foot and riders, heroes too; never you (your own bar).
    ///   step 44 (Anton: "see which soldiers are in FEVER") - the FEVER BAR under it for an AI man in a fever (step 43: armed or burning): bright
    ///     orange while armed and at the burn's start, through yellow as the store empties (its fill = the fever left), gone when it is over;
    ///     the enemy's with FeverMarksShowEnemy.
    /// The Athletics bar keeps its height while the fever bar's place is empty (whenever fever bars can show for his side), so it never jumps.
    /// Vanilla's own per-man ALT icon (MissionAgentLabelView's 3D banner mesh) is never touched - its mesh list is private and it rewrites the
    /// mesh's arguments on every highlight change; the two would fight.
    ///
    /// WHEN: exactly while vanilla shows its formation markers - its own flag read live (the same read as the ALT labels, so all come and go
    /// together); when it cannot be read, its rule copied (ALT or the orders menu). Nothing to draw (FeverEnabled off AND the Athletics bars
    /// off) = no layer. Plus the common gates (ModEnabled FIRST, AthleticsEnabled, ShowAthleticsMarks or ShowFeverMarks, Hide battle UI, photo
    /// mode, a fight). The player need not be on the field.
    ///
    /// EVERY FRAME (only while shown): the logic's men walked (<see cref="AthleticsLogic.TryGetMarkAt"/> - every tracked man with the Athletics
    /// bars on, else only the men in a fever; allocation-free), each placed for the camera (feet + FeverMarkHeightMetres, a rider higher), the
    /// first skips (his side's switches, behind the camera, closer than 1.5 m); then the NEAREST FeverMarkMax ON THE SCREEN picked - sorted by
    /// distance only when the cap bites and projected nearest first (vanilla's projection, one engine call a man) until the cap is full, so a man
    /// off the screen never takes a slot and at most about the cap's worth are projected (<see cref="FeverMarkMath.PickOnScreen"/>) -, stacked
    /// and sized (the UI scale, smaller far away), coloured (the band, the fever, vanilla's icon fade) and written into a reused pool of marks.
    /// Nothing allocated per frame once the pool has grown to the most men marked at once.
    ///
    /// STEP 58 (Anton, 2026-10-18: "this ALT view is too cluttered ... only show them on up to 5 soldiers that I'm directly looking at and are
    /// really close, making it work in tournaments too"): by default the NEAR view - the candidates within AltNearMarksMetres of the camera are
    /// ranked by the angle from the camera's forward (= nearest the screen's centre) instead of the distance, the cap is min(AltNearMarksMax,
    /// FeverMarkMax), no distance fade; AltMarksEveryone = the step-46 view above. Spectators (no valid team) are never placed. In a tournament /
    /// arena / duel (no formation markers) the game's ALT key shows the view (the copied rule) - your opponent's bars.
    ///
    /// Log: [hud] the first bars drawn per mission in full (men looked at by side, in a fever, marked, the bars by colour, the cap, skipped by
    /// reason, the nearest), FeverEnabled switched off while shown, the [summary] line (<see cref="FeverMarkStats"/>). Showing and hiding with
    /// the key is quiet after the first.
    /// </summary>
    public sealed class FeverMarkView : TraxHudView
    {
        /// <summary>The prefab: module\GUI\Prefabs\TraxFeverMarks.xml.</summary>
        public const string Movie = "TraxFeverMarks";

        /// <summary>How the log names the view (step 46 renamed it from "fever marks": it draws the Athletics bars too).</summary>
        public const string Name = "ALT bars";

        /// <summary>The most marks the pool holds (FeverMarkMax's top).</summary>
        private const int PoolMax = 1000;

        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        /// <summary>The fever bar's frame grey (#14 = 20 / 255) - <see cref="FeverMarkMath.FrameHex"/>; the Athletics bar's frame and its reachable
        /// part the same grey, a little more opaque (the player bar's reachable part, #141414F0).</summary>
        private const float FrameGrey = 20f / 255f;

        private const float BarFrameAlpha = 0xF0 / 255f;

        private static readonly float WoundR, WoundG, WoundB;

        /// <summary>Step 67: the morale bar's blue (<see cref="MoraleMarkMath.ColorHex"/>).</summary>
        private static readonly float MoraleR, MoraleG, MoraleB;

        private readonly FeverMarkFrame _frame = new FeverMarkFrame();
        private readonly int[] _bands = new int[BarMath.BandCount];
        private IFeverMarkSource? _source;
        private FeverMarksVM? _vm;
        private int _showNo;
        private bool _nothingToDraw;
        private bool _feverWasOn = true;
        private bool _firstLogged;
        private int _drawnNow;
        private int _barsNow;
        private int _feverBarsNow;
        private int _moraleBarsNow;

        static FeverMarkView()
        {
            AthleticsMarkMath.ParseHex(AthleticsMarkMath.WoundHex, out WoundR, out WoundG, out WoundB, out _);
            AthleticsMarkMath.ParseHex(MoraleMarkMath.ColorHex, out MoraleR, out MoraleG, out MoraleB, out _);
        }

        public FeverMarkView()
            : base(Name, Movie, SettingsSchema.ShowFeverMarks, "ShowAthleticsMarks / ShowFeverMarks")
        {
            Stats.ConditionName = "markers hidden or nothing to draw";
        }

        /// <summary>The live ViewModel (null while no layer is up) - the offline smoke reads it.</summary>
        internal FeverMarksVM? CurrentViewModel => _vm;

        /// <summary>This mission's numbers ([summary]).</summary>
        internal FeverMarkStats MarkStats { get; } = new FeverMarkStats();

        /// <summary>The last frame (the smoke reads its skips).</summary>
        internal FeverMarkFrame LastFrame => _frame;

        /// <summary>Men marked in the last frame.</summary>
        internal int DrawnNow => _drawnNow;

        /// <summary>Step 46: Athletics bars / fever bars drawn in the last frame.</summary>
        internal int BarsNow => _barsNow;

        internal int FeverBarsNow => _feverBarsNow;

        /// <summary>Step 67: morale bars drawn in the last frame.</summary>
        internal int MoraleBarsNow => _moraleBarsNow;

        /// <summary>The engine side (real: the attach; the smoke: a stand-in).</summary>
        internal void UseSource(IFeverMarkSource source) => _source = source;

        protected override bool NeedsPlayer => false;

        protected override bool ReadsIndicatorKey => true;

        protected override bool QuietConditionToggles => true;

        /// <summary>Either switch: the Athletics bars (step 46) or the fever bars (step 44).</summary>
        protected override bool ToggleOn(TraxSettings s) => s.ShowAthleticsMarks || s.ShowFeverMarks || s.ShowMoraleMarks; // step 67: or the morale bars

        protected override string ViewConditionText => _nothingToDraw
            ? "nothing to draw (FeverEnabled off and ShowAthleticsMarks off)"
            : "the game's markers are hidden (ALT not held, the orders menu closed)";

        protected override string? ViewConditionWhen => "while the game shows its markers (ALT held or the orders menu open - read live from its own marker layer) and there is something to draw (ShowAthleticsMarks on, or FeverEnabled on)";

        /// <summary>Something to draw first (the Athletics bars, or fevers to mark), then vanilla's own flag when it can be read; else its rule copied.</summary>
        protected override bool ViewConditionMet(in HudFrame f)
        {
            var s = TraxSettings.Shared;
            bool feverOn = s.FeverEnabled;
            if (feverOn != _feverWasOn)
            {
                _feverWasOn = feverOn;
                if (!feverOn && IsLayerUp)
                    TraxLog.Limited("hud", ViewName + ": FeverEnabled switched off at " + S1(f.Now) + " s - no fevers, the fever bars go"
                        + (s.ShowAthleticsMarks ? " (the Athletics bars stay)" : string.Empty), "hud-fever");
            }
            _nothingToDraw = !feverOn && !s.ShowAthleticsMarks && !s.ShowMoraleMarks; // step 67: the morale bars are something to draw
            if (_nothingToDraw) return false;
            if (_source != null && _source.TryReadShown(out bool shown)) return shown;
            return f.ShowIndicatorsKey || f.OrderMenuOpen;
        }

        protected override ViewModel CreateDataSource(in HudFrame f)
        {
            if (_source == null) throw new InvalidOperationException("no ALT-bar source - the view was not attached through AthleticsLogic");
            _source.NewShow();
            _vm = new FeverMarksVM();
            _showNo++;
            MarkStats.NoteShown();
            _drawnNow = 0;
            return _vm;
        }

        protected override void OnLayerGone()
        {
            _vm = null;
            _drawnNow = 0;
            _barsNow = 0;
            _feverBarsNow = 0;
        }

        internal override void AddSummaryLines(List<string> lines)
        {
            MarkStats.Grown = _frame.Grown;
            lines.Add(MarkStats.SummaryLine(Stats.Errors));
        }

        // ------------------------------------------------------------------ every frame: walk, place, pick (projecting), stack, draw

        /// <summary>The pick's projection: the source's, then the man's whole stack on the screen (a struct - no boxing in the generic pick).</summary>
        private struct Projector : IMarkProjector
        {
            public IFeverMarkSource Source;
            public FeverMarkFrame Frame;
            public FeverMarkRules Fever;
            public AthleticsMarkRules Bars;

            public bool ProjectOnScreen(ref FeverMark m)
            {
                if (!Source.Project(ref m)) return false;
                AthleticsMarkMath.Layout(in Fever, in Bars, Frame.Scale, m.Distance, m.HasBar, m.HasFever, m.FeverSlot, m.HasMorale, m.ScreenX, m.ScreenY, out var box);
                return FeverMarkMath.OnScreen(box.Left, box.Top, box.Width, box.Height, Frame.ScreenWidth, Frame.ScreenHeight);
            }
        }

        protected override void OnLayerFrame(in HudFrame f)
        {
            var vm = _vm;
            if (vm == null) return;
            long start = Stopwatch.GetTimestamp();
            var s = TraxSettings.Shared;
            var r = FeverMarkRules.From(s);
            var a = AthleticsMarkRules.From(s);
            var near = AltNearRules.From(s); // step 58: the near view (the men nearest the screen's centre, close to the camera) or every man
            var mo = MoraleMarkRules.From(s); // step 67: the morale bars
            bool everyone = a.On || mo.On;
            bool feverOn = r.Hidden == FeverMarksHidden.None;
            var athletics = AthleticsRules.From(s);
            var bands = BarRules.From(s);
            var frame = _frame;
            frame.Clear();
            Array.Clear(_bands, 0, _bands.Length);
            int drawn = 0, looked = 0, inFever = 0, yours = 0, enemy = 0, bars = 0, feverBars = 0, wounded = 0, moraleBars = 0;
            double moraleSum = 0;
            if ((everyone || feverOn) && _source!.BeginFrame(frame))
            {
                float height = (float)r.HeightMetres;
                int n = AthleticsLogic.MarkWalkCount(everyone);
                for (int i = 0; i < n; i++)
                {
                    int index = frame.Count;
                    ref var m = ref frame.Add();
                    if (!AthleticsLogic.TryGetMarkAt(i, everyone, in athletics, in bands, ref m, out var agent))
                    {
                        frame.DropLast();
                        continue;
                    }
                    looked++;
                    if (m.InFever) inFever++;
                    if (!_source.Locate(agent!, height, ref m))
                    {
                        frame.SkipAt(index, FeverMarkSkip.NotPlaced);
                        continue;
                    }
                    if (m.Side == (int)MarkerTeam.Enemy) enemy++;
                    else yours++;
                    var skip = AthleticsMarkMath.Decide(ref m, in r, in a, in mo);
                    if (skip == FeverMarkSkip.None && near.Near && !AltNearMath.InRange(m.Distance, near.NearMetres)) skip = FeverMarkSkip.TooFar;
                    if (skip != FeverMarkSkip.None)
                    {
                        frame.SkipAt(index, skip);
                        continue;
                    }
                    // the every-man view ranks by the camera distance (step 46), the near view by the angle from the screen's centre (step 58)
                    frame.Elect(index, near.Near ? AltNearMath.CentreKey(m.Facing) : m.Distance);
                }
                // the first by rank ON THE SCREEN: sorted only when the cap bites, projected in rank order until it is full (an off-screen man takes no slot)
                var projector = new Projector { Source = _source, Frame = frame, Fever = r, Bars = a };
                int cap = near.Cap;
                int picked = FeverMarkMath.PickOnScreen(frame, cap, ref projector);
                int most = Math.Min(cap, PoolMax);
                for (int k = 0; k < picked; k++)
                {
                    ref var m = ref frame.Marks[frame.Order[k]];
                    AthleticsMarkMath.Layout(in r, in a, frame.Scale, m.Distance, m.HasBar, m.HasFever, m.FeverSlot, m.HasMorale, m.ScreenX, m.ScreenY, out var box);
                    var mark = vm.MarkAt(drawn, most);
                    if (mark == null) break;
                    float fade = AltNearMath.Fade(near.Near, m.Distance); // the near view: no distance fade (the close man you look at stays bright)
                    Color fill = default, frameColor = default, barColor = default, barFrame = default, barWound = default;
                    if (m.HasFever)
                    {
                        FeverMarkMath.Tint(m.Armed, m.Share, out float cr, out float cg, out float cb, out float ca);
                        float alpha = ca * fade;
                        fill = new Color(cr, cg, cb, alpha);
                        frameColor = new Color(FrameGrey, FrameGrey, FrameGrey, FeverMarkMath.FrameAlpha * alpha);
                        feverBars++;
                    }
                    if (m.HasBar)
                    {
                        AthleticsMarkMath.BandColor(m.Band, out float br, out float bg, out float bb);
                        barColor = new Color(br, bg, bb, fade);
                        barFrame = new Color(FrameGrey, FrameGrey, FrameGrey, BarFrameAlpha * fade);
                        barWound = new Color(WoundR, WoundG, WoundB, fade);
                        bars++;
                        _bands[(int)m.Band]++;
                        if (AthleticsMarkMath.Wounded(in m)) wounded++;
                    }
                    mark.Set(in box, m.HasFever, m.Armed ? 1f : m.Share, fill, frameColor, m.HasBar, m.BarFill, m.BarUsable, barColor, barFrame, barWound);
                    bool moraleShown = m.HasMorale && MoraleMarkMath.Known(m.Morale); // step 67: the place kept when the game keeps no morale
                    if (moraleShown)
                    {
                        moraleBars++;
                        moraleSum += m.Morale;
                    }
                    mark.SetMorale(moraleShown, in box, MoraleMarkMath.Fill(m.Morale), new Color(MoraleR, MoraleG, MoraleB, fade),
                        new Color(FrameGrey, FrameGrey, FrameGrey, BarFrameAlpha * fade));
                    drawn++;
                }
            }
            vm.HideFrom(drawn);
            _drawnNow = drawn;
            _barsNow = bars;
            _feverBarsNow = feverBars;
            double ms = (Stopwatch.GetTimestamp() - start) * TicksToMs;
            MarkStats.NoteBars(bars, _bands, wounded, feverBars, moraleBars, moraleSum);
            _moraleBarsNow = moraleBars;
            MarkStats.NoteMode(near.Near, drawn, near.Cap, near.NearMetres);
            MarkStats.NoteFrame(f.Dt, drawn, looked, inFever, yours, enemy, frame.Skipped[(int)FeverMarkSkip.Capped] > 0, near.Cap, frame.Skipped, ms);
            if (!_firstLogged && drawn > 0) LogFirstMarks(in f, in r, in a, in near, looked, inFever, yours, enemy, drawn, bars, wounded, feverBars);
        }

        protected override bool Refresh(in HudFrame f, bool first) => _vm != null;

        // ------------------------------------------------------------------ log text (allocates - once per mission)

        private void LogFirstMarks(in HudFrame f, in FeverMarkRules r, in AthleticsMarkRules a, in AltNearRules near, int looked, int inFever, int yours, int enemy,
            int drawn, int bars, int wounded, int feverBars)
        {
            _firstLogged = true;
            var nearest = new StringBuilder();
            int shownNearest = 0;
            for (int k = 0; k < _frame.Picked && shownNearest < 3; k++)
            {
                ref var m = ref _frame.Marks[_frame.Order[k]];
                if (m.Skip != FeverMarkSkip.None) continue;
                nearest.Append(nearest.Length > 0 ? " | " : string.Empty).Append(AthleticsMarkMath.DescribeMan(in m));
                if (m.InFever && !m.HasFever) nearest.Append(" (his fever bar off)");
                if (near.Near) nearest.Append(", ").Append(AltNearMath.AngleDegrees(m.Facing).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append("° off the centre");
                shownNearest++;
            }
            int armed = 0;
            for (int i = 0; i < _frame.Count; i++)
                if (_frame.Marks[i].InFever && _frame.Marks[i].Armed) armed++;
            var colours = new StringBuilder();
            for (int b = 0; b < _bands.Length; b++)
                colours.Append(b == 0 ? string.Empty : ", ").Append(BarMath.Name((BarBand)b)).Append(' ').Append(_bands[b]);
            bool capped = _frame.Skipped[(int)FeverMarkSkip.Capped] > 0;
            TraxLog.Limited("hud", ViewName + ": first bars drawn at " + S1(f.Now) + " s (show #" + _showNo + ") - " + looked + (looked == 1 ? " man" : " men")
                + " looked at (yours and allies " + yours + ", enemy " + enemy + "), " + inFever + " in a fever (armed " + armed + ", burning " + (inFever - armed) + "); "
                + drawn + (drawn == 1 ? " man" : " men") + " marked"
                + (!capped ? string.Empty
                   : near.Near ? " (the " + near.Cap + " nearest the screen's centre of " + _frame.Eligible + " candidates within " + S1(near.NearMetres) + " m - AltNearMarksMax)"
                   : " (the nearest " + near.Cap + " on the screen of " + _frame.Eligible + " candidates - FeverMarkMax)")
                + ": " + bars + " Athletics bars (" + colours + "; wounded " + wounded + "), " + feverBars + " fever bars"
                + "; skipped: " + FeverMarkMath.DescribeSkips(_frame.Skipped)
                + "; screen " + OrderStripMath.Px(_frame.ScreenWidth) + " x " + OrderStripMath.Px(_frame.ScreenHeight) + " px, UI scale " + F2(_frame.Scale)
                + "; " + (a.On ? a.Describe() : "Athletics bars off (" + AthleticsMarkMath.HiddenName(a.Hidden) + ")")
                + "; fever " + (r.Hidden == FeverMarksHidden.None ? r.Describe() : "bars off (" + FeverMarkMath.HiddenName(r.Hidden) + ")")
                + "; the first drawn: " + nearest + "; source: " + _source!.Describe() + "; " + near.Describe()
                + (MoraleMarkRules.From(TraxSettings.Shared).On ? "; " + MoraleMarkRules.From(TraxSettings.Shared).Describe() + " - " + _moraleBarsNow + " drawn" : string.Empty), "hud-fever");
        }
    }
}
