using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// DESIGN §3 item 4 (step 9) - the ORDERS-MENU STRIP: while the orders menu is open, under each
    /// of the game's formation cards a slim Athletics bar (the men's mean share of their pools,
    /// coloured by their average f, a ± FormationSpreadStdDevs band, the peak tick) and the numbers
    /// "72% ± 8" and "HP 81%" (ShowFormationHealth). Anton: "some vision of the state of the troops".
    ///
    /// ALIGNMENT: the vanilla cards are READ LIVE, never patched (AI_NOTES "Step 9"): on each open
    /// the order layer's widget tree is scanned once (<see cref="IOrderCardSource.Scan"/>), then every
    /// frame the drawn card set's pixels are read and each card is checked against its formation
    /// (slot k = FormationClass k, the card's member count must agree -
    /// <see cref="OrderStripMath.Match"/>); each cell goes to its card's bottom edge in screen pixels
    /// (<see cref="OrderStripMath.PlaceCell"/>) - exact at any resolution, UI scale, layout (keyboard
    /// columns, gamepad row) and under RTS Camera Command System's reversed columns.
    ///
    /// FALLBACK: when the cards cannot be trusted (none found, not whole sets of 8, two sets drawn,
    /// none drawn 0.5 s after the open, a card disagreeing with its formation for 1 s) or
    /// OrderStripUnderCards is off, the rest of that open shows a compact panel at the top of the
    /// screen instead (one row per formation, the same numbers and bar). The next open tries again.
    ///
    /// The show/hide rules, the layer and the fail safe are the base's (<see cref="TraxHudView"/>):
    /// ModEnabled first, AthleticsEnabled, ShowInOrderMenu, Hide battle UI, photo mode, a fight, you
    /// on the field, and the orders menu open (its opening and closing are logged quietly).
    ///
    /// Log: the first placement under the cards per mission in full (technique, cards found with
    /// their positions and counts, cells placed); later changes of the drawn cards (rate-limited);
    /// every fallback with its reason (the first of each reason in full); the values once per open
    /// (verbose: every refresh); the [summary] strip line.
    /// </summary>
    public sealed class OrderStripView : TraxHudView
    {
        /// <summary>The prefab: module\GUI\Prefabs\TraxOrderStrip.xml.</summary>
        public const string Movie = "TraxOrderStrip";

        private enum Mode
        {
            Scanning,
            Waiting,
            Cards,
            Panel,
        }

        private readonly OrderCardFrame _frame = new OrderCardFrame();
        private readonly StripFormation[] _forms = new StripFormation[OrderStripMath.CardsPerSet];
        private readonly bool[] _fallbackLogged = new bool[8];
        private IOrderCardSource? _cards;
        private IStripFormations? _formations;
        private OrderStripVM? _vm;
        private Mode _mode;
        private StripFallback _fallback;
        private double _waitSeconds;
        private double _mismatchSeconds;
        private bool _placedThisOpen;
        private bool _underCardsCounted;
        private bool _panelCounted;
        private bool _valuesLoggedThisOpen;
        private int _openNo;
        private int _statsVersion = -1;
        private int _settingsVersion = -1;
        private int _lastSignature;
        private bool _haveSignature;
        private int _loggedSignature;
        private bool _haveLogged;
        private string _scanDetail = string.Empty;

        public OrderStripView()
            : base("orders strip", Movie, SettingsSchema.ShowInOrderMenu)
        {
            Stats.ConditionName = "orders menu closed";
        }

        /// <summary>The live ViewModel (null while no layer is up) - the offline smoke reads it.</summary>
        internal OrderStripVM? CurrentViewModel => _vm;

        /// <summary>This mission's strip numbers ([summary]).</summary>
        internal OrderStripStats StripStats { get; } = new OrderStripStats();

        /// <summary>The cells sit under the cards now.</summary>
        internal bool UnderCards => _mode == Mode.Cards;

        /// <summary>The compact panel is up now.</summary>
        internal bool PanelUp => _mode == Mode.Panel;

        /// <summary>Why the panel is up (None while it is not).</summary>
        internal StripFallback Fallback => _mode == Mode.Panel ? _fallback : StripFallback.None;

        /// <summary>The engine side (real: the attach; the smoke: stand-ins).</summary>
        internal void UseSources(IOrderCardSource cards, IStripFormations formations)
        {
            _cards = cards;
            _formations = formations;
        }

        protected override bool ViewConditionMet(in HudFrame f) => f.OrderMenuOpen;

        protected override string ViewConditionText => "the orders menu is closed";

        protected override string? ViewConditionWhen => "while the orders menu is open";

        protected override bool QuietConditionToggles => true;

        protected override ViewModel CreateDataSource(in HudFrame f)
        {
            if (_cards == null || _formations == null)
                throw new InvalidOperationException("no card / formation source - the view was not attached through AthleticsLogic");
            var s = TraxSettings.Shared;
            _vm = new OrderStripVM();
            _vm.SetLayout(s, StripLayout.From(s));
            _openNo++;
            StripStats.NoteOpen();
            _mode = Mode.Scanning;
            _fallback = StripFallback.None;
            _waitSeconds = 0;
            _mismatchSeconds = 0;
            _placedThisOpen = false;
            _underCardsCounted = false;
            _panelCounted = false;
            _valuesLoggedThisOpen = false;
            _statsVersion = -1;
            _settingsVersion = s.Version;
            _haveSignature = false;
            return _vm;
        }

        protected override void OnLayerGone()
        {
            _vm = null;
        }

        internal override void AddSummaryLines(List<string> lines) => lines.Add(StripStats.SummaryLine());

        // ------------------------------------------------------------------ every frame: alignment

        protected override void OnLayerFrame(in HudFrame f)
        {
            var vm = _vm;
            if (vm == null) return;
            var s = TraxSettings.Shared;
            if (s.Version != _settingsVersion)
            {
                // a setting moved (MCM, mid-battle): the layout now, the values at the next refresh
                _settingsVersion = s.Version;
                vm.SetLayout(s, StripLayout.From(s));
                _statsVersion = -1;
            }
            _formations!.Read(_forms);

            if (!s.OrderStripUnderCards)
            {
                if (_mode != Mode.Panel || _fallback != StripFallback.SwitchedOff)
                    EnterPanel(in f, StripFallback.SwitchedOff, "OrderStripUnderCards is off");
                ShowPanelRows(vm);
                return;
            }
            if (_mode == Mode.Panel)
            {
                if (_fallback != StripFallback.SwitchedOff)
                {
                    ShowPanelRows(vm);
                    return;
                }
                _mode = Mode.Scanning; // switched back on mid-open: try the cards again
                _fallback = StripFallback.None;
                vm.PanelShown = false;
            }
            if (_mode == Mode.Scanning)
            {
                if (!_cards!.Scan(out _scanDetail))
                {
                    EnterPanel(in f, StripFallback.NoOrderLayer, _scanDetail);
                    ShowPanelRows(vm);
                    return;
                }
                _mode = Mode.Waiting;
                _waitSeconds = 0;
            }
            if (!_cards!.Read(_frame))
            {
                // the movie was released / replaced (order type changed, deployment ended): scan once more
                StripStats.NoteRescan();
                if (!_cards.Scan(out _scanDetail) || !_cards.Read(_frame))
                {
                    EnterPanel(in f, StripFallback.NoOrderLayer, "the cards went off the screen and a new scan found none (" + _scanDetail + ")");
                    ShowPanelRows(vm);
                    return;
                }
            }

            var m = OrderStripMath.Match(_frame, _forms);
            switch (m.Result)
            {
                case StripAlignment.Problem:
                    EnterPanel(in f, m.Fallback, m.Describe() + " (" + _scanDetail + ")");
                    ShowPanelRows(vm);
                    return;

                case StripAlignment.NotYet:
                    vm.StripShown = false;
                    _waitSeconds += f.Dt;
                    if (_waitSeconds > OrderStripMath.ShowGraceSeconds)
                    {
                        EnterPanel(in f, StripFallback.NeverShown, m.Describe() + " " + F2(_waitSeconds) + " s after the menu opened (" + _scanDetail + ")");
                        ShowPanelRows(vm);
                    }
                    return;

                case StripAlignment.Mismatch:
                    if (_mismatchSeconds <= 0 && TraxLog.VerboseOn)
                        TraxLog.Verbose("hud", ViewName + ": card and formation disagree at " + S1(f.Now) + " s - " + m.Describe() + " (waiting "
                            + S1(OrderStripMath.MismatchGraceSeconds) + " s before the panel takes over)", "hud-strip-mismatch");
                    _mismatchSeconds += f.Dt;
                    if (_mismatchSeconds > OrderStripMath.MismatchGraceSeconds)
                    {
                        EnterPanel(in f, StripFallback.Mismatch, m.Describe() + " for more than " + S1(OrderStripMath.MismatchGraceSeconds) + " s");
                        ShowPanelRows(vm);
                        return;
                    }
                    if (_mode == Mode.Cards) PlaceCells(in f, in m, s); // keep following the cards meanwhile
                    else vm.StripShown = false;
                    return;

                default:
                    if (_mismatchSeconds > 0)
                    {
                        StripStats.NoteShortMismatch();
                        _mismatchSeconds = 0;
                    }
                    _waitSeconds = 0;
                    PlaceCells(in f, in m, s);
                    return;
            }
        }

        private void PlaceCells(in HudFrame f, in StripMatch m, TraxSettings s)
        {
            var vm = _vm!;
            var layout = StripLayout.From(s);
            int placed = 0, lifted = 0;
            for (int k = 0; k < OrderStripMath.CardsPerSet; k++)
            {
                var cell = vm.Cells[k];
                ref var card = ref _frame.Cards[m.Set * OrderStripMath.CardsPerSet + k];
                bool show = card.Visible && _forms[k].HasStats;
                if (show)
                {
                    OrderStripMath.PlaceCell(in card, in layout, _frame.Scale, _frame.ScreenHeight, out float x, out float y, out float w, out bool up);
                    cell.Place(x, y, w);
                    placed++;
                    if (up) lifted++;
                }
                cell.IsShown = show;
            }
            vm.PanelShown = false;
            vm.StripShown = true;
            _mode = Mode.Cards;
            if (!_underCardsCounted)
            {
                _underCardsCounted = true;
                StripStats.NoteUnderCards();
            }

            int sig = OrderStripMath.Signature(_frame, m.Set);
            if (_placedThisOpen && _haveSignature && sig == _lastSignature) return; // the cells stand where they stood
            _placedThisOpen = true;
            StripStats.NoteCells(placed, lifted); // each (re)placement: the first of an open and every change
            _lastSignature = sig;
            _haveSignature = true;
            if (_haveLogged && sig == _loggedSignature) return; // the same cards as last logged (a reopen)
            bool first = !_haveLogged;
            _haveLogged = true;
            _loggedSignature = sig;
            string layoutText = m.Cards + " cards in " + m.Sets + (m.Sets == 1 ? " set" : " sets") + ", set " + (m.Set + 1) + " drawn";
            StripStats.NoteLayout(layoutText);
            if (first)
            {
                StripStats.Technique = "the live vanilla cards (" + _scanDetail + ")";
                TraxLog.Limited("hud", ViewName + ": first placement under the cards at " + S1(f.Now) + " s (open #" + _openNo + ") - technique: the game's own cards read live, "
                    + _scanDetail + " = " + layoutText + " (" + SetNote(m) + "); screen " + OrderStripMath.Px(_frame.ScreenWidth) + " x " + OrderStripMath.Px(_frame.ScreenHeight)
                    + " px, UI scale " + F2(_frame.Scale) + "; cards drawn: " + DescribeCards(in m) + "; cells (" + layout.Describe() + "): " + DescribeCells(in m, lifted), "hud-strip");
            }
            else
            {
                StripStats.NoteCardChange();
                TraxLog.Limited("hud", ViewName + ": the cards changed at " + S1(f.Now) + " s (open #" + _openNo + ") - " + layoutText + " (" + SetNote(m) + "); cards drawn: "
                    + DescribeCards(in m) + "; cells: " + DescribeCells(in m, lifted), "hud-strip-cards");
            }
        }

        private void ShowPanelRows(OrderStripVM vm)
        {
            for (int k = 0; k < OrderStripMath.CardsPerSet; k++) vm.Cells[k].IsShown = _forms[k].HasStats;
            vm.StripShown = false;
            vm.PanelShown = true;
        }

        private void EnterPanel(in HudFrame f, StripFallback why, string text)
        {
            _mode = Mode.Panel;
            _fallback = why;
            if (!_panelCounted)
            {
                _panelCounted = true;
                StripStats.NotePanelOpen();
            }
            StripStats.NoteFallback(why, text, f.Now);
            var s = TraxSettings.Shared;
            string line = ViewName + ": " + (why == StripFallback.SwitchedOff ? "the compact panel" : "FALLBACK to the compact panel") + " at " + S1(f.Now)
                + " s (open #" + _openNo + ") - " + text + "; the panel lists your formations at the top of the screen (" + s.OrderPanelOffsetTop + " px down, "
                + s.OrderPanelWidth + " px wide) until the menu closes" + (why == StripFallback.SwitchedOff ? string.Empty : " - the next open tries the cards again");
            int i = (int)why;
            if (i >= 0 && i < _fallbackLogged.Length && !_fallbackLogged[i])
            {
                _fallbackLogged[i] = true;
                TraxLog.Limited("hud", line, "hud-strip");
            }
            else if (TraxLog.VerboseOn)
            {
                TraxLog.Verbose("hud", line, "hud-strip-fallback");
            }
        }

        // ------------------------------------------------------------------ every HudRefreshSeconds: values

        protected override bool Refresh(in HudFrame f, bool first)
        {
            var vm = _vm;
            if (vm == null || _formations == null) return false;
            int version = AthleticsLogic.FormationStatsVersion;
            if (!first && version == _statsVersion) return true; // nothing new since the last push
            _formations.Read(_forms);
            var s = TraxSettings.Shared;
            var rules = BarRules.From(s);
            float peak = BarMath.PeakLine(s.AthleticsPeakPercent / 100.0);
            bool spread = s.ShowFormationSpread;
            double k = s.FormationSpreadStdDevs;
            bool health = s.ShowFormationHealth;
            int pushed = 0;
            for (int i = 0; i < OrderStripMath.CardsPerSet; i++)
            {
                if (!_forms[i].HasStats) continue;
                var st = _forms[i].Stats;
                OrderStripMath.Numbers(in st, spread, k, out int mean, out int sp);
                OrderStripMath.Band(in st, spread, k, out float low, out float high);
                int hp = health ? OrderStripMath.HealthPercent(st.MeanHealth) : -1;
                vm.Cells[i].SetValues(BarMath.Band(in rules, st.MeanPeakShare), BarMath.Fill(st.MeanFraction), low, high, peak, mean, sp, hp);
                pushed++;
            }
            _statsVersion = version;
            if (pushed == 0) return false;
            StripStats.NoteValuesPushed();
            if (!_valuesLoggedThisOpen)
            {
                _valuesLoggedThisOpen = true;
                TraxLog.Limited("hud", ViewName + ": values at " + S1(f.Now) + " s (open #" + _openNo + ", " + ModeName() + "): " + DescribeValues(vm)
                    + " - ± is " + (spread ? F2(k) + " std" : "off (ShowFormationSpread)") + ", health " + (health ? "on" : "off (ShowFormationHealth)"), "hud-strip-values");
            }
            else if (TraxLog.VerboseOn)
            {
                TraxLog.Verbose("hud", ViewName + ": values at " + S1(f.Now) + " s: " + DescribeValues(vm), "hud-strip-values");
            }
            return true;
        }

        // ------------------------------------------------------------------ log text (allocates - log lines only)

        private string ModeName() => _mode switch
        {
            Mode.Cards => "under the cards",
            Mode.Panel => "the compact panel",
            _ => "waiting for the cards",
        };

        private static string SetNote(in StripMatch m) =>
            m.Sets == 2 ? (m.Set == 0 ? "the game's side columns - keyboard layout" : "the game's top row - gamepad layout")
            : m.Sets == 1 ? "one layout - an order-menu mod such as RTS Camera Command System"
            : "set " + (m.Set + 1) + " of " + m.Sets;

        private string DescribeCards(in StripMatch m)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < OrderStripMath.CardsPerSet; k++)
            {
                ref var c = ref _frame.Cards[m.Set * OrderStripMath.CardsPerSet + k];
                if (!c.Visible) continue;
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(OrderStripMath.DescribeCard(k, in c)).Append(Math.Abs(c.Members - _forms[k].Members) <= OrderStripMath.MemberTolerance ? " (= formation)" : " (formation " + _forms[k].Members + ")");
            }
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        private string DescribeCells(in StripMatch m, int lifted)
        {
            var vm = _vm!;
            var sb = new StringBuilder();
            for (int k = 0; k < OrderStripMath.CardsPerSet; k++)
            {
                var cell = vm.Cells[k];
                if (!cell.IsShown) continue;
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(cell.Name).Append(" at (").Append(OrderStripMath.Px(cell.PositionX)).Append(", ").Append(OrderStripMath.Px(cell.PositionY)).Append(") w ")
                  .Append(OrderStripMath.Px(cell.CellWidth));
            }
            if (sb.Length == 0) sb.Append("none (no drawn card has tracked men)");
            if (lifted > 0) sb.Append(" - ").Append(lifted).Append(lifted == 1 ? " cell" : " cells").Append(" lifted to the screen's bottom edge");
            return sb.ToString();
        }

        private string DescribeValues(OrderStripVM vm)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < OrderStripMath.CardsPerSet; k++)
            {
                if (!_forms[k].HasStats) continue;
                var cell = vm.Cells[k];
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(OrderStripMath.DescribeValues(k, _forms[k].Stats, cell.ShownMean, cell.ShownSpread, cell.ShownHealth));
            }
            return sb.Length == 0 ? "no formation with tracked men" : sb.ToString();
        }
    }
}
