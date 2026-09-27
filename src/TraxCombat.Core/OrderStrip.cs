using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// One vanilla formation card as the orders-menu strip read it from the screen in one frame
    /// (step 9, AI_NOTES "Step 9"): screen PIXELS (the card widget's GlobalPosition and Size), whether
    /// it is drawn (recursively visible), and the men it counts (its CurrentMemberCount - the
    /// formation's units minus the player when he is in it).
    /// </summary>
    public struct OrderCard
    {
        public bool Visible;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public int Members;
    }

    /// <summary>
    /// Everything the strip read from the game's order layer in one frame: the cards in the widget
    /// tree's order (vanilla: 16 = the keyboard columns then the gamepad row; RTS Camera Command
    /// System: 8), the screen's size in pixels and the UI scale (UI pixels → screen pixels). Made once
    /// and refilled every frame - nothing allocated per frame.
    /// </summary>
    public sealed class OrderCardFrame
    {
        public const int MaxCards = 32;

        public readonly OrderCard[] Cards = new OrderCard[MaxCards];

        /// <summary>Cards found (≤ <see cref="MaxCards"/>).</summary>
        public int Count;

        public float ScreenWidth;

        public float ScreenHeight;

        /// <summary>Screen pixels per UI pixel of the 1080p layout (the game's UI scale included).</summary>
        public float Scale = 1f;
    }

    /// <summary>Formation k (0..7 = FormationClass Infantry … HeavyCavalry) of the player's team, as
    /// the strip needs it.</summary>
    public struct StripFormation
    {
        /// <summary>The team has this formation.</summary>
        public bool Exists;

        /// <summary>Men under the player's command in it - what its card counts (its units minus the
        /// player when he is in it).</summary>
        public int Members;

        /// <summary>The Athletics logic's last snapshot (Count 0 = nothing known).</summary>
        public FormationAthleticsStats Stats;

        public bool HasStats => Stats.Count > 0;
    }

    /// <summary>What <see cref="OrderStripMath.Match"/> made of one frame.</summary>
    public enum StripAlignment
    {
        /// <summary>One set of cards is drawn and every card agrees with its formation.</summary>
        Aligned,

        /// <summary>No card drawn or laid out yet (the frame the menu opens) - wait.</summary>
        NotYet,

        /// <summary>A card and its formation disagree - wait a moment (a man just fell), then fall back.</summary>
        Mismatch,

        /// <summary>The cards cannot be used at all (none, not whole sets, two sets at once) - fall back.</summary>
        Problem,
    }

    /// <summary>Why a frame did not align.</summary>
    public enum StripIssue
    {
        None,
        NoCards,
        NotWholeSets,
        TwoSetsVisible,
        NoneVisible,
        NotLaidOut,
        NoFormation,
        CardHidden,
        MembersDisagree,
    }

    /// <summary>Why an open of the orders menu shows the compact panel instead of the strip.</summary>
    public enum StripFallback
    {
        None = 0,

        /// <summary>OrderStripUnderCards is off.</summary>
        SwitchedOff = 1,

        /// <summary>No "MissionOrder" layer and no other layer holding cards.</summary>
        NoOrderLayer = 2,

        /// <summary>The cards found are not whole sets of 8.</summary>
        NotWholeSets = 3,

        /// <summary>Two sets of cards drawn at once.</summary>
        TwoSetsVisible = 4,

        /// <summary>No card drawn / laid out in time after the open.</summary>
        NeverShown = 5,

        /// <summary>A card disagreed with its formation for too long.</summary>
        Mismatch = 6,
    }

    /// <summary>The result of <see cref="OrderStripMath.Match"/> (a struct - no allocation per frame;
    /// <see cref="Describe"/> builds text for the log only).</summary>
    public struct StripMatch
    {
        public StripAlignment Result;
        public StripIssue Issue;

        /// <summary>Cards found.</summary>
        public int Cards;

        /// <summary>Whole sets of 8.</summary>
        public int Sets;

        /// <summary>The set drawn (0-based), -1 when none.</summary>
        public int Set;

        /// <summary>Cards drawn in that set.</summary>
        public int VisibleCards;

        /// <summary>The card slot = formation index of a mismatch / not-laid-out card, -1 when none.</summary>
        public int Slot;

        public int CardMembers;
        public int FormationMembers;

        /// <summary>The fallback an issue leads to.</summary>
        public StripFallback Fallback => Issue switch
        {
            StripIssue.NoCards => StripFallback.NoOrderLayer,
            StripIssue.NotWholeSets => StripFallback.NotWholeSets,
            StripIssue.TwoSetsVisible => StripFallback.TwoSetsVisible,
            StripIssue.NoneVisible or StripIssue.NotLaidOut => StripFallback.NeverShown,
            StripIssue.NoFormation or StripIssue.CardHidden or StripIssue.MembersDisagree => StripFallback.Mismatch,
            _ => StripFallback.None,
        };

        /// <summary>The issue in plain words (log lines only - allocates).</summary>
        public string Describe() => Issue switch
        {
            StripIssue.None => "aligned: set " + (Set + 1) + " of " + Sets + ", " + VisibleCards + " cards drawn, every card agrees with its formation",
            StripIssue.NoCards => "no formation cards found in the order layer",
            StripIssue.NotWholeSets => Cards + " cards found - not whole sets of " + OrderStripMath.CardsPerSet + " (an order-menu mod with its own cards?)",
            StripIssue.TwoSetsVisible => "two sets of cards drawn at once - which one is which formation is unclear",
            StripIssue.NoneVisible => "no card drawn (of " + Cards + " found)",
            StripIssue.NotLaidOut => OrderStripMath.FormationName(Slot) + "'s card is drawn but not laid out yet (size 0)",
            StripIssue.NoFormation => OrderStripMath.FormationName(Slot) + "'s card is drawn but your team has no such formation",
            StripIssue.CardHidden => OrderStripMath.FormationName(Slot) + " has " + FormationMembers + " men but its card is not drawn",
            _ => OrderStripMath.FormationName(Slot) + "'s card counts " + CardMembers + " men, the formation has " + FormationMembers,
        };
    }

    /// <summary>
    /// The strip's cell layout in UI pixels of the 1080p layout (the Advanced OrderStrip* settings,
    /// read live), measured from a card's BOTTOM edge. The cell starts at the higher of the two rows
    /// (<see cref="Top"/>) so the prefab never needs a negative margin.
    /// </summary>
    public readonly struct StripLayout
    {
        public StripLayout(int textSize, int textOffset, int barOffset, int barHeight, int sideMargin)
        {
            TextSize = textSize;
            TextOffset = textOffset;
            BarOffset = barOffset;
            BarHeight = barHeight;
            SideMargin = sideMargin;
        }

        public static StripLayout From(TraxSettings s) =>
            new StripLayout(s.OrderStripTextSize, s.OrderStripTextOffset, s.OrderStripBarOffset, s.OrderStripBarHeight, s.OrderStripSideMargin);

        public int TextSize { get; }
        public int TextOffset { get; }
        public int BarOffset { get; }
        public int BarHeight { get; }
        public int SideMargin { get; }

        /// <summary>Card bottom → the cell's top (the higher row), UI pixels (may be negative).</summary>
        public float Top => Math.Min(TextOffset, BarOffset);

        /// <summary>The numbers row inside the cell.</summary>
        public float TextMarginTop => TextOffset - Top;

        /// <summary>The bar inside the cell.</summary>
        public float BarMarginTop => BarOffset - Top;

        /// <summary>The numbers' line height (the HUD font's line is 1.2 of its size).</summary>
        public float TextHeight => TextSize * OrderStripMath.LineHeightFactor;

        /// <summary>The cell's depth from its top, UI pixels.</summary>
        public float Depth => Math.Max(TextOffset + TextHeight, BarOffset + BarHeight) - Top;

        public string Describe() =>
            "numbers " + TextSize + " px at card bottom " + Signed(TextOffset) + ", bar " + BarHeight + " px at " + Signed(BarOffset)
            + ", " + SideMargin + " px in from the sides (" + Depth.ToString("0", CultureInfo.InvariantCulture) + " px deep; UI pixels - the game's UI scale applies)";

        private static string Signed(int v) => (v >= 0 ? "+" : string.Empty) + v.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// DESIGN §3 item 4 (step 9) - the orders-menu strip's pure logic: which set of the vanilla
    /// cards is drawn and whether slot k really is formation k (<see cref="Match"/>), where a cell
    /// goes (<see cref="PlaceCell"/>), what it says (<see cref="Numbers"/>, <see cref="AthleticsText"/>,
    /// <see cref="HealthText"/>) and its ± band (<see cref="Band"/>). Every card set holds 8 cards in
    /// the game's TroopItem0..7 order = FormationClass 0..7 (MissionOrderTroopControllerVM
    /// .RefreshTroopItemBindings); each card's member count confirms it every frame.
    /// </summary>
    public static class OrderStripMath
    {
        /// <summary>Cards per layout: TroopItem0..7 (engine fact, not a tunable).</summary>
        public const int CardsPerSet = 8;

        /// <summary>A card may count one man fewer or more than its formation (the player joining or
        /// leaving it between two events) - engine plumbing.</summary>
        public const int MemberTolerance = 1;

        /// <summary>Seconds (real time) the cards get to be drawn and laid out after the menu opens
        /// before the panel takes over - plumbing: vanilla lays them out the frame after the open.</summary>
        public const double ShowGraceSeconds = 0.5;

        /// <summary>Seconds (real time) a card may disagree with its formation (a man falling between
        /// two events) before the panel takes over for the rest of the open - plumbing.</summary>
        public const double MismatchGraceSeconds = 1.0;

        /// <summary>The HUD font's line height ÷ its size (FiraSansExtraCondensed: 38 / 32).</summary>
        public const float LineHeightFactor = 1.2f;

        /// <summary>Card slot k's name: the number key and the vanilla formation class.</summary>
        private static readonly string[] Names =
        {
            "1 Infantry", "2 Archers", "3 Cavalry", "4 Horse archers", "5 Skirmishers", "6 Heavy infantry",
            "7 Light cavalry", "8 Heavy cavalry", "General", "Bodyguard",
        };

        public static string FormationName(int index) =>
            index >= 0 && index < Names.Length ? Names[index] : "formation " + index.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// One frame: the drawn set of cards, and whether each drawn card agrees with its formation.
        /// <paramref name="formations"/> holds FormationClass 0..7 (at least <see cref="CardsPerSet"/>
        /// entries). Allocation-free.
        /// </summary>
        public static StripMatch Match(OrderCardFrame frame, StripFormation[] formations)
        {
            var m = new StripMatch { Set = -1, Slot = -1, Cards = frame.Count };
            int n = frame.Count;
            if (n <= 0) return Fail(ref m, StripAlignment.Problem, StripIssue.NoCards);
            if (n % CardsPerSet != 0) return Fail(ref m, StripAlignment.Problem, StripIssue.NotWholeSets);
            m.Sets = n / CardsPerSet;

            int setsDrawn = 0;
            for (int s = 0; s < m.Sets; s++)
            {
                int drawn = 0;
                for (int k = 0; k < CardsPerSet; k++)
                    if (frame.Cards[s * CardsPerSet + k].Visible) drawn++;
                if (drawn == 0) continue;
                setsDrawn++;
                m.Set = s;
                m.VisibleCards = drawn;
            }
            if (setsDrawn == 0) return Fail(ref m, StripAlignment.NotYet, StripIssue.NoneVisible);
            if (setsDrawn > 1) return Fail(ref m, StripAlignment.Problem, StripIssue.TwoSetsVisible);

            for (int k = 0; k < CardsPerSet; k++)
            {
                ref var card = ref frame.Cards[m.Set * CardsPerSet + k];
                var f = k < formations.Length ? formations[k] : default;
                if (card.Visible)
                {
                    if (!(card.Width > 1f && card.Height > 1f))
                    {
                        m.Slot = k;
                        return Fail(ref m, StripAlignment.NotYet, StripIssue.NotLaidOut);
                    }
                    if (!f.Exists)
                    {
                        m.Slot = k;
                        return Fail(ref m, StripAlignment.Mismatch, StripIssue.NoFormation);
                    }
                    if (Math.Abs(card.Members - f.Members) > MemberTolerance)
                    {
                        m.Slot = k;
                        m.CardMembers = card.Members;
                        m.FormationMembers = f.Members;
                        return Fail(ref m, StripAlignment.Mismatch, StripIssue.MembersDisagree);
                    }
                }
                else if (f.Exists && f.Members > 0)
                {
                    m.Slot = k;
                    m.FormationMembers = f.Members;
                    return Fail(ref m, StripAlignment.Mismatch, StripIssue.CardHidden);
                }
            }
            m.Result = StripAlignment.Aligned;
            m.Issue = StripIssue.None;
            return m;
        }

        private static StripMatch Fail(ref StripMatch m, StripAlignment result, StripIssue issue)
        {
            m.Result = result;
            m.Issue = issue;
            return m;
        }

        /// <summary>
        /// Where the cell of <paramref name="card"/> goes, in screen PIXELS: the card's left edge and
        /// width, its top at the card's bottom + <see cref="StripLayout.Top"/> UI pixels × the scale.
        /// A cell that would leave the screen at the bottom is lifted to its edge
        /// (<paramref name="lifted"/>) - vanilla's order icons may then cover the middle of it.
        /// </summary>
        public static void PlaceCell(in OrderCard card, in StripLayout layout, float scale, float screenHeight,
            out float x, out float y, out float width, out bool lifted)
        {
            if (!(scale > 0f)) scale = 1f;
            x = card.X;
            width = card.Width;
            y = card.Y + card.Height + layout.Top * scale;
            float depth = layout.Depth * scale;
            lifted = false;
            if (screenHeight > 0f && y + depth > screenHeight)
            {
                y = Math.Max(0f, screenHeight - depth);
                lifted = true;
            }
        }

        /// <summary>A number that changes when the drawn cards move, appear, vanish or resize (whole
        /// pixels) - to log "the cards changed" without keeping copies. Allocation-free.</summary>
        public static int Signature(OrderCardFrame frame, int set)
        {
            unchecked
            {
                int h = 17 + set;
                if (set < 0) return h;
                for (int k = 0; k < CardsPerSet; k++)
                {
                    int i = set * CardsPerSet + k;
                    if (i >= frame.Count) break;
                    ref var c = ref frame.Cards[i];
                    if (!c.Visible) continue;
                    h = h * 31 + k;
                    h = h * 31 + (int)Math.Round(c.X);
                    h = h * 31 + (int)Math.Round(c.Y);
                    h = h * 31 + (int)Math.Round(c.Width);
                    h = h * 31 + (int)Math.Round(c.Height);
                }
                return h;
            }
        }

        /// <summary>
        /// The strip's two Athletics numbers in whole percent of the bar: the men's mean share of their
        /// own pools, and the band's half-width (<paramref name="stdDevs"/> × the spread) - -1 when the
        /// spread is not shown (switched off, or a single man).
        /// </summary>
        public static void Numbers(in FormationAthleticsStats s, bool showSpread, double stdDevs, out int meanPercent, out int spreadPercent)
        {
            meanPercent = Percent(s.MeanFraction);
            spreadPercent = -1;
            if (!showSpread || s.Count < 2 || double.IsNaN(s.StdFraction)) return;
            double half = Math.Max(0, stdDevs) * s.StdFraction;
            spreadPercent = (int)Math.Round(Math.Min(half, 1.0) * 100, MidpointRounding.AwayFromZero);
        }

        /// <summary>"72% ± 8", or "72%" when <paramref name="spreadPercent"/> is -1.</summary>
        public static string AthleticsText(int meanPercent, int spreadPercent) =>
            meanPercent.ToString(CultureInfo.InvariantCulture) + "%"
            + (spreadPercent >= 0 ? " ± " + spreadPercent.ToString(CultureInfo.InvariantCulture) : string.Empty);

        /// <summary>Average health in whole percent, -1 when unknown.</summary>
        public static int HealthPercent(double meanHealth) =>
            double.IsNaN(meanHealth) ? -1 : Percent(meanHealth);

        /// <summary>"HP 81%" (the HUD font has no heart glyph - AI_NOTES "Step 9"); empty when unknown.</summary>
        public static string HealthText(int healthPercent) =>
            healthPercent < 0 ? string.Empty : "HP " + healthPercent.ToString(CultureInfo.InvariantCulture) + "%";

        /// <summary>
        /// The ± band as shares of the bar (the prefab's FillBarWidget draws from low to high): mean ±
        /// <paramref name="stdDevs"/> standard deviations, clamped to 0..1. No band (low = high = the
        /// mean) when it is not shown.
        /// </summary>
        public static void Band(in FormationAthleticsStats s, bool show, double stdDevs, out float low, out float high)
        {
            float mean = BarMath.Fill(s.MeanFraction);
            if (!show || s.Count < 2 || double.IsNaN(s.StdFraction) || !(stdDevs > 0))
            {
                low = high = mean;
                return;
            }
            low = (float)s.LowFraction(stdDevs);
            high = (float)s.HighFraction(stdDevs);
        }

        /// <summary>"1 Infantry at (20, 833) 131 x 223, 40 men" - log lines only.</summary>
        public static string DescribeCard(int k, in OrderCard c) =>
            FormationName(k) + " at (" + Px(c.X) + ", " + Px(c.Y) + ") " + Px(c.Width) + " x " + Px(c.Height) + ", " + c.Members + (c.Members == 1 ? " man" : " men");

        /// <summary>"1 Infantry 72% ± 8 HP 81% (40 men, f 0.93)" - log lines only.</summary>
        public static string DescribeValues(int k, in FormationAthleticsStats s, int meanPercent, int spreadPercent, int healthPercent) =>
            FormationName(k) + " " + AthleticsText(meanPercent, spreadPercent) + (healthPercent >= 0 ? " " + HealthText(healthPercent) : string.Empty)
            + " (" + s.Count + (s.Count == 1 ? " man" : " men") + ", f " + (double.IsNaN(s.MeanPeakShare) ? "n/a" : s.MeanPeakShare.ToString("0.00", CultureInfo.InvariantCulture))
            + (s.Exhausted > 0 ? ", " + s.Exhausted + " exhausted" : string.Empty) + ")";

        /// <summary>"under the cards" / "the compact panel (why)" for a fallback reason.</summary>
        public static string FallbackName(StripFallback f) => f switch
        {
            StripFallback.SwitchedOff => "OrderStripUnderCards off",
            StripFallback.NoOrderLayer => "no order cards found",
            StripFallback.NotWholeSets => "not whole sets of cards",
            StripFallback.TwoSetsVisible => "two card sets drawn",
            StripFallback.NeverShown => "cards never drawn",
            StripFallback.Mismatch => "a card disagreed with its formation",
            _ => "none",
        };

        internal static string Px(float v) => Math.Round(v).ToString("0", CultureInfo.InvariantCulture);

        private static int Percent(double share) =>
            double.IsNaN(share) ? 0 : (int)Math.Round((share < 0 ? 0 : share > 1 ? 1 : share) * 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// One mission of the orders-menu strip for the [summary]: how often the menu opened with the
    /// strip, under the cards or as the panel, the technique and the card layouts it met, cells placed
    /// and lifted, the card changes, short mismatches, values pushed, every fallback with its reason.
    /// Main thread.
    /// </summary>
    public sealed class OrderStripStats
    {
        private const int MaxLayouts = 6;
        private const int FallbackKinds = 7;

        private readonly int[] _fallbacks = new int[FallbackKinds];
        private readonly string?[] _layouts = new string?[MaxLayouts];
        private int _layoutCount;

        public int Opens { get; private set; }
        public int OpensUnderCards { get; private set; }
        public int OpensPanel { get; private set; }
        public int CellsPlaced { get; private set; }
        public int CellsLifted { get; private set; }
        public int CardChanges { get; private set; }
        public int ShortMismatches { get; private set; }
        public int ValuesPushed { get; private set; }
        public int Rescans { get; private set; }

        /// <summary>"the live vanilla cards (layer MissionOrder)" - set on the first alignment.</summary>
        public string? Technique { get; set; }

        public string? FirstFallbackText { get; private set; }
        public double FirstFallbackAt { get; private set; }

        public void NoteOpen() => Opens++;

        public void NoteUnderCards() => OpensUnderCards++;

        public void NoteCells(int placed, int lifted)
        {
            CellsPlaced += placed;
            CellsLifted += lifted;
        }

        public void NoteCardChange() => CardChanges++;

        public void NoteShortMismatch() => ShortMismatches++;

        public void NoteValuesPushed() => ValuesPushed++;

        public void NoteRescan() => Rescans++;

        /// <summary>One open fell back to the panel; the first one's text is kept for the summary.</summary>
        public void NoteFallback(StripFallback why, string text, double at)
        {
            OpensPanel++;
            int i = (int)why;
            if (i > 0 && i < _fallbacks.Length) _fallbacks[i]++;
            if (FirstFallbackText == null)
            {
                FirstFallbackText = text;
                FirstFallbackAt = at;
            }
        }

        public int Fallbacks(StripFallback why)
        {
            int i = (int)why;
            return i > 0 && i < _fallbacks.Length ? _fallbacks[i] : 0;
        }

        /// <summary>A card layout met ("16 cards in 2 sets, set 1 drawn: 3 cards") - distinct ones
        /// kept, at most <see cref="MaxLayouts"/>. True when it is new.</summary>
        public bool NoteLayout(string text)
        {
            for (int i = 0; i < _layoutCount; i++)
                if (_layouts[i] == text) return false;
            if (_layoutCount < _layouts.Length) _layouts[_layoutCount++] = text;
            return true;
        }

        /// <summary>The [summary] line (without the tag).</summary>
        public string SummaryLine()
        {
            var sb = new StringBuilder("hud: orders strip - opened ").Append(Opens).Append('x');
            if (Opens == 0) return sb.Append(" (the orders menu was never opened with the strip on)").ToString();
            sb.Append(": under the cards in ").Append(OpensUnderCards).Append(", the compact panel in ").Append(OpensPanel);
            sb.Append("; technique: ").Append(Technique ?? "never aligned (the cards were not matched)");
            if (_layoutCount > 0)
            {
                sb.Append("; card layouts seen: ");
                for (int i = 0; i < _layoutCount; i++) sb.Append(i == 0 ? string.Empty : " | ").Append(_layouts[i]);
            }
            sb.Append("; cells placed ").Append(CellsPlaced).Append(" (lifted to the screen's edge ").Append(CellsLifted).Append(')')
              .Append(", card changes ").Append(CardChanges).Append(", short mismatches ").Append(ShortMismatches)
              .Append(" (under ").Append(OrderStripMath.MismatchGraceSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s)")
              .Append(", card re-scans ").Append(Rescans).Append(", values pushed ").Append(ValuesPushed);
            int total = 0;
            for (int i = 1; i < _fallbacks.Length; i++) total += _fallbacks[i];
            if (total == 0) return sb.Append("; fallbacks: none").ToString();
            sb.Append("; fallbacks ").Append(total).Append(" (");
            bool first = true;
            for (int i = 1; i < _fallbacks.Length; i++)
            {
                if (_fallbacks[i] == 0) continue;
                sb.Append(first ? string.Empty : ", ").Append(OrderStripMath.FallbackName((StripFallback)i)).Append(' ').Append(_fallbacks[i]);
                first = false;
            }
            sb.Append(") - the first at ").Append(FirstFallbackAt.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s: ").Append(FirstFallbackText);
            return sb.ToString();
        }
    }
}
