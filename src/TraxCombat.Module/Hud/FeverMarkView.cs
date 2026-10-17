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
    /// Step 44 (Anton, 2026-10-17: "when I hold ALT ... see which soldiers are in FEVER"; DESIGN §3 item 3b, AI_NOTES "Step 44") - while the
    /// game shows its markers (step 20's flag: ALT held or the orders menu open), a small bar of OURS above each AI man in a fever (step 43:
    /// armed or burning): bright orange while armed and at the burn's start, through yellow as the store empties (its fill = the fever left),
    /// gone when it is over. Calm men and the player: nothing. Yours and your allies' always; the enemy's with FeverMarksShowEnemy. Vanilla's
    /// own per-man ALT icon (MissionAgentLabelView's 3D banner mesh) is never touched - its mesh list is private and it rewrites the mesh's
    /// arguments on every highlight change; the two would fight.
    ///
    /// WHEN: exactly while vanilla shows its formation markers - its own flag read live (the same read as the ALT labels, so both come and
    /// go together); when it cannot be read, its rule copied (ALT or the orders menu). FeverEnabled off = no marks (nothing to mark). Plus the
    /// common gates (ModEnabled FIRST, AthleticsEnabled, ShowFeverMarks, Hide battle UI, photo mode, a fight). The player need not be on the
    /// field.
    ///
    /// EVERY FRAME (only while shown): the logic's fevers walked (<see cref="AthleticsLogic.TryGetFeverAt"/>, allocation-free), each man
    /// placed for the camera (feet + FeverMarkHeightMetres), the first skips (enemy switch, behind the camera, closer than 1.5 m), projected
    /// (vanilla's own projection - one engine call a man) and dropped when off the screen, then the NEAREST FeverMarkMax picked, sized (the UI
    /// scale, smaller far away), tinted (the fever, vanilla's icon fade) and written into a reused pool of marks. Nothing allocated per frame once the pool has grown to the most marks drawn at once.
    ///
    /// Log: [hud] the first marks drawn per mission in full (men in fever by side, drawn, the cap, skipped by reason, the nearest), FeverEnabled
    /// switched off while shown, the [summary] line (<see cref="FeverMarkStats"/>). Showing and hiding with the key is quiet after the first.
    /// </summary>
    public sealed class FeverMarkView : TraxHudView
    {
        /// <summary>The prefab: module\GUI\Prefabs\TraxFeverMarks.xml.</summary>
        public const string Movie = "TraxFeverMarks";

        /// <summary>The most marks the pool holds (FeverMarkMax's top).</summary>
        private const int PoolMax = 1000;

        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        private readonly FeverMarkFrame _frame = new FeverMarkFrame();
        private IFeverMarkSource? _source;
        private FeverMarksVM? _vm;
        private int _showNo;
        private bool _feverOff;
        private bool _firstLogged;
        private int _drawnNow;

        public FeverMarkView()
            : base("fever marks", Movie, SettingsSchema.ShowFeverMarks)
        {
            Stats.ConditionName = "markers hidden or the fever off";
        }

        /// <summary>The live ViewModel (null while no layer is up) - the offline smoke reads it.</summary>
        internal FeverMarksVM? CurrentViewModel => _vm;

        /// <summary>This mission's numbers ([summary]).</summary>
        internal FeverMarkStats MarkStats { get; } = new FeverMarkStats();

        /// <summary>The last frame (the smoke reads its skips).</summary>
        internal FeverMarkFrame LastFrame => _frame;

        /// <summary>Marks drawn in the last frame.</summary>
        internal int DrawnNow => _drawnNow;

        /// <summary>The engine side (real: the attach; the smoke: a stand-in).</summary>
        internal void UseSource(IFeverMarkSource source) => _source = source;

        protected override bool NeedsPlayer => false;

        protected override bool ReadsIndicatorKey => true;

        protected override bool QuietConditionToggles => true;

        protected override string ViewConditionText => _feverOff
            ? "FeverEnabled is off (no fevers to mark)"
            : "the game's markers are hidden (ALT not held, the orders menu closed)";

        protected override string? ViewConditionWhen => "while the game shows its markers (ALT held or the orders menu open - read live from its own marker layer) and FeverEnabled is on";

        /// <summary>FeverEnabled first (no fevers, no marks), then vanilla's own flag when it can be read; else its rule copied.</summary>
        protected override bool ViewConditionMet(in HudFrame f)
        {
            bool off = !TraxSettings.Shared.FeverEnabled;
            if (off != _feverOff)
            {
                _feverOff = off;
                if (off && IsLayerUp)
                    TraxLog.Limited("hud", ViewName + ": FeverEnabled switched off at " + S1(f.Now) + " s - no fevers, the marks go", "hud-fever");
            }
            if (off) return false;
            if (_source != null && _source.TryReadShown(out bool shown)) return shown;
            return f.ShowIndicatorsKey || f.OrderMenuOpen;
        }

        protected override ViewModel CreateDataSource(in HudFrame f)
        {
            if (_source == null) throw new InvalidOperationException("no fever-mark source - the view was not attached through AthleticsLogic");
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
        }

        internal override void AddSummaryLines(List<string> lines)
        {
            MarkStats.Grown = _frame.Grown;
            lines.Add(MarkStats.SummaryLine(Stats.Errors));
        }

        // ------------------------------------------------------------------ every frame: walk, place, pick, project, draw

        protected override void OnLayerFrame(in HudFrame f)
        {
            var vm = _vm;
            if (vm == null) return;
            long start = Stopwatch.GetTimestamp();
            var r = FeverMarkRules.From(TraxSettings.Shared);
            var frame = _frame;
            frame.Clear();
            int drawn = 0, inFever = 0, yours = 0, enemy = 0;
            if (_source!.BeginFrame(frame))
            {
                float height = (float)r.HeightMetres;
                int n = AthleticsLogic.FeverWalkCount;
                for (int i = 0; i < n; i++)
                {
                    if (!AthleticsLogic.TryGetFeverAt(i, out var agent, out bool armed, out double share)) continue;
                    int index = frame.Count;
                    ref var m = ref frame.Add();
                    m.Armed = armed;
                    m.Share = (float)share;
                    inFever++;
                    if (!_source.Locate(agent!, height, ref m))
                    {
                        frame.SkipAt(index, FeverMarkSkip.NotPlaced);
                        continue;
                    }
                    if (m.Side == (int)MarkerTeam.Enemy) enemy++;
                    else yours++;
                    var skip = FeverMarkMath.PreSkip(in m, r.ShowEnemy);
                    if (skip != FeverMarkSkip.None)
                    {
                        frame.SkipAt(index, skip);
                        continue;
                    }
                    // projected BEFORE the pick: a man off the screen's side never takes a slot from one on it (one engine call each)
                    if (!_source.Project(ref m) || !OnScreen(in m, in r, frame))
                    {
                        frame.SkipAt(index, FeverMarkSkip.OffScreen);
                        continue;
                    }
                    frame.Elect(index);
                }
                int picked = FeverMarkMath.Pick(frame, r.Max);
                int most = Math.Min(r.Max, PoolMax);
                for (int k = 0; k < picked; k++)
                {
                    ref var m = ref frame.Marks[frame.Order[k]];
                    FeverMarkMath.Size(in r, frame.Scale, m.Distance, out float w, out float h);
                    FeverMarkMath.Place(m.ScreenX, m.ScreenY, w, h, out float left, out float top);
                    var mark = vm.MarkAt(drawn, most);
                    if (mark == null) break;
                    float fade = FeverMarkMath.Fade(m.Distance);
                    FeverMarkMath.Tint(m.Armed, m.Share, out float cr, out float cg, out float cb, out float ca);
                    float alpha = ca * fade;
                    mark.Set(left, top, w, h, m.Armed ? 1f : m.Share, new Color(cr, cg, cb, alpha), new Color(FrameGrey, FrameGrey, FrameGrey, FeverMarkMath.FrameAlpha * alpha));
                    drawn++;
                }
            }
            vm.HideFrom(drawn);
            _drawnNow = drawn;
            double ms = (Stopwatch.GetTimestamp() - start) * TicksToMs;
            MarkStats.NoteFrame(f.Dt, drawn, inFever, yours, enemy, frame.Eligible > r.Max, r.Max, frame.Skipped, ms);
            if (!_firstLogged && drawn > 0) LogFirstMarks(in f, in r, inFever, yours, enemy, drawn);
        }

        private static bool OnScreen(in FeverMark m, in FeverMarkRules r, FeverMarkFrame frame)
        {
            FeverMarkMath.Size(in r, frame.Scale, m.Distance, out float w, out float h);
            FeverMarkMath.Place(m.ScreenX, m.ScreenY, w, h, out float left, out float top);
            return FeverMarkMath.OnScreen(left, top, w, h, frame.ScreenWidth, frame.ScreenHeight);
        }

        /// <summary>The frame's grey (#14 = 20 / 255) - <see cref="FeverMarkMath.FrameHex"/>.</summary>
        private const float FrameGrey = 20f / 255f;

        protected override bool Refresh(in HudFrame f, bool first) => _vm != null;

        // ------------------------------------------------------------------ log text (allocates - once per mission)

        private void LogFirstMarks(in HudFrame f, in FeverMarkRules r, int inFever, int yours, int enemy, int drawn)
        {
            _firstLogged = true;
            var nearest = new StringBuilder();
            int shownNearest = 0;
            for (int k = 0; k < _frame.Picked && shownNearest < 3; k++)
            {
                ref var m = ref _frame.Marks[_frame.Order[k]];
                if (m.Skip != FeverMarkSkip.None) continue;
                nearest.Append(nearest.Length > 0 ? " | " : string.Empty).Append(FeverMarkMath.DescribeMark(in m));
                shownNearest++;
            }
            int armed = 0;
            for (int i = 0; i < _frame.Count; i++)
                if (_frame.Marks[i].Armed) armed++;
            TraxLog.Limited("hud", ViewName + ": first marks drawn at " + S1(f.Now) + " s (show #" + _showNo + ") - " + inFever + (inFever == 1 ? " man" : " men")
                + " in a fever (yours and allies " + yours + ", enemy " + enemy + "; armed " + armed + ", burning " + (inFever - armed) + "), " + drawn + " marks drawn"
                + (_frame.Eligible > r.Max ? " (the nearest " + r.Max + " of " + _frame.Eligible + " - FeverMarkMax)" : string.Empty)
                + "; skipped: " + FeverMarkMath.DescribeSkips(_frame.Skipped)
                + "; screen " + OrderStripMath.Px(_frame.ScreenWidth) + " x " + OrderStripMath.Px(_frame.ScreenHeight) + " px, UI scale " + F2(_frame.Scale)
                + "; " + r.Describe() + "; the nearest: " + nearest + "; source: " + _source!.Describe(), "hud-fever");
        }
    }
}
