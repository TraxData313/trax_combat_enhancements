using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 76: one weapon slot as the spear rule sees it (the module reads the item's usages once per loadout).</summary>
    public struct SpearSlot
    {
        /// <summary>Nothing in the slot.</summary>
        public bool Empty;

        /// <summary>A thrown item, or any usage that is consumable or ranged (javelins, throwing axes / spears) - neither a polearm nor a sidearm
        /// (RBM's rule: pushing a favor toward a weapon the AI then throws made men reach for nothing).</summary>
        public bool Throwable;

        /// <summary>Any usage is a melee weapon.</summary>
        public bool Melee;

        /// <summary>Any MELEE usage's relevant skill is Polearm (a spear, a pike, a glaive, a lance on foot).</summary>
        public bool Polearm;

        /// <summary>The longest melee polearm usage's <c>WeaponLength</c> (cm; 0 = not known / not a polearm).</summary>
        public int LengthCm;
    }

    /// <summary>Step 76: what a man carries in his four weapon slots - a melee polearm AND a melee sidearm (RBM's "CarriesPolearmAndSidearm").</summary>
    public struct SpearLoadout
    {
        /// <summary>He carries both: the spear rule applies to him.</summary>
        public bool Both;

        /// <summary>Bits 0-3: the slots holding a melee polearm / a melee non-polearm (a sidearm).</summary>
        public int PolearmMask, SidearmMask;

        /// <summary>The slot of his LONGEST polearm (-1 = none) and its length (cm; 0 = not known).</summary>
        public int PolearmSlot;
        public int LengthCm;
    }

    /// <summary>Step 76: how a man's formation stands around him at a look (the back-rank rule).</summary>
    public enum SpearArrangement
    {
        /// <summary>A shield wall, square or circle (step 41's defensive arrangements).</summary>
        Wall,

        /// <summary>A plain line.</summary>
        Line,

        /// <summary>Anything else (loose, scatter, column, skein, no formation).</summary>
        Other,
    }

    /// <summary>Step 76: one man's surroundings at a look - read by the module (managed + a handful of native reads), judged here.</summary>
    public struct SpearContext
    {
        public bool Mounted;

        /// <summary>His horse's speed (m/s; 0 on foot).</summary>
        public double MountSpeed;

        public SpearArrangement Arrangement;
        public BraceOrderKind Order;

        /// <summary>His rank in the formation's arrangement (0 = the front rank / a square's or circle's outer ring; -1 = not known).</summary>
        public int Rank;

        /// <summary>A man of his formation stands in the rank in front of him, in his file or the next one (the staggered line) - he is truly
        /// behind someone, not the front of a thinned file.</summary>
        public bool ManInFront;

        /// <summary>Step 75: he stands firm in a held wall right now.</summary>
        public bool StandsFirm;
    }

    /// <summary>Step 76: what a look found around a man with a spear and a sidearm (the summary's time split).</summary>
    public enum SpearCondition
    {
        /// <summary>No enemy within his close radius.</summary>
        NotClose,

        /// <summary>An enemy within his close radius (not running away; not holding a polearm while SpearKeepVsPolearm is on).</summary>
        Close,

        /// <summary>A back-rank man of a wall / a standing line - he keeps the spear whatever is close (SpearBackRankKeeps).</summary>
        BackRank,

        /// <summary>Every enemy within his close radius holds a polearm too - he keeps his (SpearKeepVsPolearm).</summary>
        FacingPolearm,

        /// <summary>Mounted faster than SpearRidingFastMetresPerSecond - riding through, nothing counts as close (RBM).</summary>
        Riding,

        Count,
    }

    /// <summary>Step 76: what he held at a look.</summary>
    public enum SpearHeld
    {
        Polearm,
        Sidearm,

        /// <summary>Nothing, a shield alone, a bow, a javelin.</summary>
        Other,

        Count,
    }

    /// <summary>Step 76: why a preference flipped.</summary>
    public enum SpearFlip
    {
        None,

        /// <summary>An enemy close SpearSidearmAfterSeconds - the sidearm is favoured.</summary>
        ToSidearm,

        /// <summary>No enemy close SpearPolearmBackAfterSeconds (mounted: ...BackMountedSeconds) - the polearm again.</summary>
        ToPolearm,

        /// <summary>He stands in a back rank now - the polearm again at once.</summary>
        ToPolearmBackRank,

        /// <summary>The first look of a man with both weapons - his favor begins (the polearm).</summary>
        Started,
    }

    /// <summary>
    /// Step 76: one man's spear preference - RBM's two timers kept per man (no dictionaries), the loadout read last time, his look clock.
    /// A struct on his record (TrackedAgent.Spear).
    /// </summary>
    public struct SpearMind
    {
        /// <summary>The loadout has been read at least once (the module keeps the four item references it was read from).</summary>
        public bool Read;

        public SpearLoadout Loadout;

        /// <summary>His preference is live: the stat decorator writes a favor for him (the switches on, he carries both, an AI man).</summary>
        public bool Favored;

        /// <summary>The sidearm is favoured now (an enemy has been close); false = the polearm.</summary>
        public bool Close;

        /// <summary>When an enemy was first seen close in this spell (NaN = none now) / last seen close (NaN = never).</summary>
        public double CloseSince, LastClose;

        public double NextLook;
        public double LastLook;
        public int Looks;
        public SpearCondition Condition;
        public bool EverCounted;

        /// <summary>A fresh mind: no enemy seen close, the first look due at once.</summary>
        public static SpearMind Fresh => new SpearMind { CloseSince = double.NaN, LastClose = double.NaN, NextLook = 0, LastLook = double.NaN };
    }

    /// <summary>
    /// Step 76's spear-preference settings, read live: the master switch FIRST, then SpearPreferenceEnabled; RBM's favors and timers, the riding
    /// gate, the close radius from the spear's length, the back-rank and facing-a-polearm rules, the look cadence, the wield fallback.
    /// </summary>
    public readonly struct SpearRules
    {
        public SpearRules(bool modEnabled, bool enabled, double polearmFavor, double sidearmFavor, double sidearmAfter, double polearmBackAfter,
            double polearmBackMounted, double ridingFast, bool closeFromLength, double closeBase, double closeLengthPercent, double closeMin,
            double closeMax, double closeFallback, bool backRankKeeps, bool backRankInStandingLine, bool keepVsPolearm, double lookSeconds,
            bool wieldOverride)
        {
            ModEnabled = modEnabled;
            Enabled = enabled;
            PolearmFavor = Clamp(polearmFavor, SpearMath.MinFavor, SpearMath.MaxFavor, 35);
            SidearmFavor = Clamp(sidearmFavor, SpearMath.MinFavor, SpearMath.MaxFavor, 55);
            SidearmAfterSeconds = Clamp(sidearmAfter, 0, SpearMath.MaxDelaySeconds, 2);
            PolearmBackAfterSeconds = Clamp(polearmBackAfter, 0, SpearMath.MaxDelaySeconds, 4);
            PolearmBackMountedSeconds = Clamp(polearmBackMounted, 0, SpearMath.MaxDelaySeconds, 2);
            RidingFastMetresPerSecond = Clamp(ridingFast, 0, SpearMath.MaxRidingMetresPerSecond, 4);
            CloseFromLength = closeFromLength;
            CloseBaseMetres = Clamp(closeBase, 0, SpearMath.MaxCloseBaseMetres, 1.0);
            CloseLengthPercent = Clamp(closeLengthPercent, 0, SpearMath.MaxCloseLengthPercent, 60);
            CloseMinMetres = Clamp(closeMin, SpearMath.MinCloseMetres, SpearMath.MaxCloseMetres, 1.5);
            CloseMaxMetres = Math.Max(CloseMinMetres, Clamp(closeMax, SpearMath.MinCloseMetres, SpearMath.MaxCloseMetres, 3.5));
            CloseFallbackMetres = Clamp(closeFallback, SpearMath.MinCloseMetres, SpearMath.MaxCloseMetres, 2.5);
            BackRankKeeps = backRankKeeps;
            BackRankInStandingLine = backRankInStandingLine;
            KeepVsPolearm = keepVsPolearm;
            LookSeconds = Clamp(lookSeconds, SpearMath.MinLookSeconds, SpearMath.MaxLookSeconds, 0.5);
            WieldOverride = wieldOverride;
        }

        public bool ModEnabled { get; }

        /// <summary>SpearPreferenceEnabled.</summary>
        public bool Enabled { get; }

        /// <summary>The AI's polearm favor while no enemy is close (vanilla 1; RBM 35).</summary>
        public double PolearmFavor { get; }

        /// <summary>The AI's melee (sidearm) favor once an enemy has been close (vanilla 1; RBM 55).</summary>
        public double SidearmFavor { get; }

        public double SidearmAfterSeconds { get; }
        public double PolearmBackAfterSeconds { get; }
        public double PolearmBackMountedSeconds { get; }
        public double RidingFastMetresPerSecond { get; }
        public bool CloseFromLength { get; }
        public double CloseBaseMetres { get; }
        public double CloseLengthPercent { get; }
        public double CloseMinMetres { get; }
        public double CloseMaxMetres { get; }
        public double CloseFallbackMetres { get; }
        public bool BackRankKeeps { get; }
        public bool BackRankInStandingLine { get; }
        public bool KeepVsPolearm { get; }
        public double LookSeconds { get; }
        public bool WieldOverride { get; }

        /// <summary>The favors are written (the master switch first).</summary>
        public bool On => ModEnabled && Enabled;

        /// <summary>The wield fallback runs (the preference on and its own switch).</summary>
        public bool WieldOn => On && WieldOverride;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Enabled ? "SpearPreferenceEnabled" : null;

        public static SpearRules From(TraxSettings s) => new SpearRules(s.ModEnabled, s.SpearPreferenceEnabled, s.SpearPolearmFavor, s.SpearSidearmFavor,
            s.SpearSidearmAfterSeconds, s.SpearPolearmBackAfterSeconds, s.SpearPolearmBackMountedSeconds, s.SpearRidingFastMetresPerSecond,
            s.SpearCloseFromLength, s.SpearCloseBaseMetres, s.SpearCloseLengthPercent, s.SpearCloseMinMetres, s.SpearCloseMaxMetres,
            s.SpearCloseFallbackMetres, s.SpearBackRankKeeps, s.SpearBackRankInStandingLine, s.SpearKeepVsPolearm, s.SpearLookSeconds,
            s.SpearWieldOverride);

        /// <summary>The favors as written (a change re-applies them to every favoured man).</summary>
        public bool SameFavors(in SpearRules o) => On == o.On && PolearmFavor == o.PolearmFavor && SidearmFavor == o.SidearmFavor;

        public bool SameAs(in SpearRules o) => SameFavors(in o) && SidearmAfterSeconds == o.SidearmAfterSeconds && PolearmBackAfterSeconds == o.PolearmBackAfterSeconds
                                               && PolearmBackMountedSeconds == o.PolearmBackMountedSeconds && RidingFastMetresPerSecond == o.RidingFastMetresPerSecond
                                               && CloseFromLength == o.CloseFromLength && CloseBaseMetres == o.CloseBaseMetres && CloseLengthPercent == o.CloseLengthPercent
                                               && CloseMinMetres == o.CloseMinMetres && CloseMaxMetres == o.CloseMaxMetres && CloseFallbackMetres == o.CloseFallbackMetres
                                               && BackRankKeeps == o.BackRankKeeps && BackRankInStandingLine == o.BackRankInStandingLine && KeepVsPolearm == o.KeepVsPolearm
                                               && LookSeconds == o.LookSeconds && WieldOverride == o.WieldOverride;

        /// <summary>The settings in one sentence (the mission-start and change lines, the summary's head).</summary>
        public string Describe()
        {
            string radius = CloseFromLength
                ? "from his longest spear's length: " + F2(CloseBaseMetres) + " m + " + F0(CloseLengthPercent) + "% of it, " + F1(CloseMinMetres) + "-" + F1(CloseMaxMetres)
                  + " m (a 1.5 m spear " + F2(SpearMath.CloseRadius(in this, 150)) + ", 2.0 m " + F2(SpearMath.CloseRadius(in this, 200)) + ", 2.5 m "
                  + F2(SpearMath.CloseRadius(in this, 250)) + ", 3.0 m " + F2(SpearMath.CloseRadius(in this, 300)) + "; a length not known " + F1(CloseFallbackMetres) + " m)"
                : F1(CloseFallbackMetres) + " m for every spear (SpearCloseFromLength off - RBM's 2.5)";
            string head = On ? "ON (SpearPreferenceEnabled)" : "OFF (" + OffBecause + ") - the game's own weapon choice (1 / 1 / 1); the looks still MEASURE";
            return head + " - an AI man (both sides, never you) carrying a melee polearm AND a melee sidearm favours the polearm x" + F0(PolearmFavor)
                   + " while no enemy is close, the sidearm x" + F0(SidearmFavor) + " once one has been close " + F1(SidearmAfterSeconds) + " s, the polearm again "
                   + F1(PolearmBackAfterSeconds) + " s after the last close enemy (" + F1(PolearmBackMountedSeconds) + " s mounted); close = within " + radius
                   + ", not running away" + (KeepVsPolearm ? ", not holding a polearm himself (a man facing only polearms keeps his)" : "")
                   + "; a rider faster than " + F1(RidingFastMetresPerSecond) + " m/s has nothing close"
                   + (BackRankKeeps ? "; a man behind another in a shield wall / square / circle" + (BackRankInStandingLine ? " or a halted line" : "") + " keeps the polearm whatever is close" : "")
                   + "; looked at every ~" + F2(LookSeconds) + " s (jittered)"
                   + (WieldOverride ? "; THE WIELD FALLBACK ON - a sidearm wield while nothing is close is turned into the polearm's" : "");
        }

        private static double Clamp(double v, double min, double max, double fallback) =>
            double.IsNaN(v) ? fallback : v < min ? min : v > max ? max : v;

        internal static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 76 - THE AI PREFERS THE SPEAR (Toni 2026-10-22: "my shield wall-ers have spears but they keep using their swords, even though the spear has
    /// a longer reach"; AI_NOTES "Step 76", DESIGN decision 70). RBM's WeaponPreference (its source read; numbers as parameters) + three additions:
    /// the close radius from the spear's length, a back-rank man of a wall / a halted line keeps the spear, a man facing only polearms keeps his.
    /// Pure: the loadout, the radius, the condition, RBM's two timers, the favors, the wield fallback's bits.
    /// </summary>
    public static class SpearMath
    {
        /// <summary>The settings' ranges. Plumbing.</summary>
        public const double MinFavor = 1;
        public const double MaxFavor = 100;
        public const double MaxDelaySeconds = 20;
        public const double MaxRidingMetresPerSecond = 15;
        public const double MaxCloseBaseMetres = 3;
        public const double MaxCloseLengthPercent = 150;
        public const double MinCloseMetres = 0.5;
        public const double MaxCloseMetres = 6;
        public const double MinLookSeconds = 0.1;
        public const double MaxLookSeconds = 2;

        /// <summary>The look's jitter: x 0.75-1.25 of SpearLookSeconds (RBM's) - an army spawned in one frame never looks in one frame. Plumbing.</summary>
        public const double JitterLow = 0.75, JitterSpan = 0.5;

        /// <summary>The engine's first Wield event bit (<c>Agent.EventControlFlag.Wield0</c> 0x10; Wield1-3 the next bits) - the smoke checks it.</summary>
        public const uint Wield0Bit = 0x10u;

        public const uint WieldMask = 0xF0u;

        /// <summary>The loadout from his four slots (RBM: a thrown / consumable / ranged item is neither; a melee item with a Polearm usage is a
        /// polearm, any other melee item a sidearm). Allocation-free.</summary>
        public static SpearLoadout Classify(SpearSlot[] slots)
        {
            var l = new SpearLoadout { PolearmSlot = -1 };
            int n = slots == null ? 0 : Math.Min(4, slots.Length);
            for (int i = 0; i < n; i++)
            {
                var s = slots![i];
                if (s.Empty || s.Throwable || !s.Melee) continue;
                if (s.Polearm)
                {
                    l.PolearmMask |= 1 << i;
                    if (l.PolearmSlot < 0 || s.LengthCm > l.LengthCm)
                    {
                        l.PolearmSlot = i;
                        l.LengthCm = Math.Max(0, s.LengthCm);
                    }
                }
                else l.SidearmMask |= 1 << i;
            }
            l.Both = l.PolearmMask != 0 && l.SidearmMask != 0;
            return l;
        }

        /// <summary>His close radius (m): SpearCloseBaseMetres + SpearCloseLengthPercent of his longest spear's length, clamped to the min / max; a
        /// length not known (0) or SpearCloseFromLength off = SpearCloseFallbackMetres (RBM's 2.5).</summary>
        public static double CloseRadius(in SpearRules r, int lengthCm)
        {
            if (!r.CloseFromLength || lengthCm <= 0) return r.CloseFallbackMetres;
            double v = r.CloseBaseMetres + r.CloseLengthPercent / 100.0 * (lengthCm / 100.0);
            return v < r.CloseMinMetres ? r.CloseMinMetres : v > r.CloseMaxMetres ? r.CloseMaxMetres : v;
        }

        /// <summary>A back-rank man who keeps his spear whatever is close: SpearBackRankKeeps; rank 1 or deeper with a man of his own in front;
        /// a shield wall / square / circle under any order step 41 holds (not a charge, a retreat, no order), or - SpearBackRankInStandingLine - a
        /// plain line on a STOP (or a man standing firm, step 75).</summary>
        public static bool BackRank(in SpearRules r, in SpearContext c)
        {
            if (!r.BackRankKeeps || c.Rank < 1 || !c.ManInFront) return false;
            if (c.Arrangement == SpearArrangement.Wall) return WallMath.OrderVerdict(c.Order) == WallVerdict.Hold;
            if (c.Arrangement == SpearArrangement.Line && r.BackRankInStandingLine) return c.Order == BraceOrderKind.Stop || c.StandsFirm;
            return false;
        }

        /// <summary>Riding through: mounted faster than SpearRidingFastMetresPerSecond (RBM's 4) - nothing counts as close, nothing is scanned.</summary>
        public static bool RidingFast(in SpearRules r, in SpearContext c) => c.Mounted && c.MountSpeed > r.RidingFastMetresPerSecond;

        /// <summary>The look's condition: riding fast, then the back rank, then the enemies within his radius - <paramref name="otherClose"/>
        /// not holding a polearm, <paramref name="polearmClose"/> holding one (both not running away). With SpearKeepVsPolearm off every one counts.</summary>
        public static SpearCondition Condition(in SpearRules r, bool ridingFast, bool backRank, int otherClose, int polearmClose)
        {
            if (ridingFast) return SpearCondition.Riding;
            if (backRank) return SpearCondition.BackRank;
            if (otherClose > 0) return SpearCondition.Close;
            if (polearmClose > 0) return r.KeepVsPolearm ? SpearCondition.FacingPolearm : SpearCondition.Close;
            return SpearCondition.NotClose;
        }

        /// <summary>
        /// One look of RBM's two timers (its TickWeaponPreference, per man): an enemy close (<see cref="SpearCondition.Close"/>) marks the last
        /// close time and starts the spell; the sidearm once a spell has lasted SpearSidearmAfterSeconds; back to the polearm SpearPolearmBackAfterSeconds
        /// (mounted: ...BackMountedSeconds) after the last close enemy. A back-rank look sends him back to the polearm AT ONCE and clears both
        /// timers. Returns the flip (None = unchanged).
        /// </summary>
        public static SpearFlip Step(in SpearRules r, ref SpearMind m, double now, SpearCondition c, bool mounted)
        {
            if (c == SpearCondition.BackRank)
            {
                m.CloseSince = double.NaN;
                m.LastClose = double.NaN;
                if (!m.Close) return SpearFlip.None;
                m.Close = false;
                return SpearFlip.ToPolearmBackRank;
            }
            bool near = c == SpearCondition.Close;
            if (near)
            {
                m.LastClose = now;
                if (double.IsNaN(m.CloseSince)) m.CloseSince = now;
            }
            else m.CloseSince = double.NaN;
            bool close;
            if (m.Close)
            {
                double back = mounted ? r.PolearmBackMountedSeconds : r.PolearmBackAfterSeconds;
                close = !double.IsNaN(m.LastClose) && now - m.LastClose < back;
            }
            else close = !double.IsNaN(m.CloseSince) && now - m.CloseSince >= r.SidearmAfterSeconds;
            if (close == m.Close) return SpearFlip.None;
            m.Close = close;
            return close ? SpearFlip.ToSidearm : SpearFlip.ToPolearm;
        }

        /// <summary>The favors the stat decorator writes after the base model (vanilla assigns 1 / 1 / 1 every recompute - no compounding):
        /// the sidearm favoured = Melee x SpearSidearmFavor, else Polearm x SpearPolearmFavor (RBM's SetAiRelatedProperties postfix).</summary>
        public static void Favors(in SpearRules r, bool close, out double polearm, out double melee)
        {
            polearm = close ? 1 : r.PolearmFavor;
            melee = close ? r.SidearmFavor : 1;
        }

        /// <summary>What he holds against his loadout (the wielded main-hand slot; -1 = none).</summary>
        public static SpearHeld HeldOf(int slot, in SpearLoadout l)
        {
            if (slot < 0 || slot > 3) return SpearHeld.Other;
            if ((l.PolearmMask & (1 << slot)) != 0) return SpearHeld.Polearm;
            if ((l.SidearmMask & (1 << slot)) != 0) return SpearHeld.Sidearm;
            return SpearHeld.Other;
        }

        /// <summary>The next look: SpearLookSeconds x a jitter of 0.75-1.25 from his index and his look count (deterministic, no dice).</summary>
        public static double NextLook(in SpearRules r, double now, int index, int looks)
        {
            uint h = (uint)(index * 73856093) ^ (uint)(looks * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            double u = (h & 0xFFFF) / 65535.0;
            return now + r.LookSeconds * (JitterLow + JitterSpan * u);
        }

        /// <summary>
        /// THE WIELD FALLBACK (SpearWieldOverride, off by default): the AI's own Wield event for a SIDEARM slot while the polearm is favoured becomes
        /// the Wield of his polearm's slot (the engine's Wield0-3 = 0x10 &lt;&lt; slot). True when it rewrote. Pure bits - the smoke checks them.
        /// </summary>
        public static bool WieldRewrite(ref uint eventFlags, int polearmSlot, int sidearmMask)
        {
            if (polearmSlot < 0 || polearmSlot > 3) return false;
            uint wield = eventFlags & WieldMask;
            if (wield == 0) return false;
            uint sidearmBits = (uint)(sidearmMask & 0xF) << 4;
            if ((wield & sidearmBits) == 0) return false;
            eventFlags = (eventFlags & ~WieldMask) | (Wield0Bit << polearmSlot);
            return true;
        }

        public static string Name(SpearCondition c) => c switch
        {
            SpearCondition.NotClose => "nothing close",
            SpearCondition.Close => "an enemy close",
            SpearCondition.BackRank => "a back rank",
            SpearCondition.FacingPolearm => "facing only polearms",
            SpearCondition.Riding => "riding through",
            _ => c.ToString(),
        };

        public static string Name(SpearHeld h) => h switch
        {
            SpearHeld.Polearm => "the polearm",
            SpearHeld.Sidearm => "the sidearm",
            SpearHeld.Other => "other / nothing",
            _ => h.ToString(),
        };

        public static string Name(SpearFlip f) => f switch
        {
            SpearFlip.ToSidearm => "to the sidearm (an enemy close)",
            SpearFlip.ToPolearm => "back to the polearm (nothing close)",
            SpearFlip.ToPolearmBackRank => "back to the polearm (a back rank)",
            SpearFlip.Started => "began (the polearm)",
            _ => f.ToString(),
        };
    }

    /// <summary>
    /// Step 76: one mission's spear-preference numbers - the men with a polearm and a sidearm, the time each held what by condition ON vs OFF (OFF = the
    /// looks while the preference is off - the game's own choice), the flips, the favors the decorator wrote, the radius used, the wield fallback, the
    /// back-rank men's attacks - and the [summary] "spears" lines. Main thread, except the decorator's counts (Interlocked).
    /// </summary>
    public sealed class SpearStats
    {
        private readonly double[,,] _seconds = new double[2, (int)SpearCondition.Count, (int)SpearHeld.Count];
        private readonly int[] _flips = new int[5];

        public int MenBoth, MenWithout, Looks, LoadoutReads;
        public long FavorPolearm, FavorSidearm;
        public MeanStd Radius;
        public double RadiusMin = double.NaN, RadiusMax = double.NaN;
        public int MenLengthKnown;
        public long WieldRewrites;
        public int WieldWishes;
        public int BackRankAttacks, BackRankAttacksPolearm, AttacksHoldingPolearm, AttacksHoldingSidearm;
        public int Cleared, SwitchChanges, Errors;
        public double CostMs;
        public int CostLooks;
        public bool EverOn;

        public void AddTime(bool on, SpearCondition c, SpearHeld h, double seconds)
        {
            if (!(seconds > 0) || c >= SpearCondition.Count || h >= SpearHeld.Count) return;
            _seconds[on ? 1 : 0, (int)c, (int)h] += seconds;
        }

        public double Seconds(bool on, SpearCondition c, SpearHeld h) => _seconds[on ? 1 : 0, (int)c, (int)h];

        public double Seconds(bool on, SpearCondition c)
        {
            double t = 0;
            for (int h = 0; h < (int)SpearHeld.Count; h++) t += _seconds[on ? 1 : 0, (int)c, h];
            return t;
        }

        public double SecondsAll(bool on)
        {
            double t = 0;
            for (int c = 0; c < (int)SpearCondition.Count; c++) t += Seconds(on, (SpearCondition)c);
            return t;
        }

        public void Flip(SpearFlip f) => _flips[(int)f]++;
        public int Flips(SpearFlip f) => _flips[(int)f];

        public void AddRadius(double r, bool lengthKnown)
        {
            Radius.Add(r);
            if (double.IsNaN(RadiusMin) || r < RadiusMin) RadiusMin = r;
            if (double.IsNaN(RadiusMax) || r > RadiusMax) RadiusMax = r;
            if (lengthKnown) MenLengthKnown++;
        }

        /// <summary>The [summary] lines (2).</summary>
        public string[] SummaryLines(in SpearRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var a = new StringBuilder(900);
            a.Append("spears (step 76 - ").Append(r.On ? "SpearPreferenceEnabled ON" : "OFF (" + r.OffBecause + ")").Append(" now")
             .Append(EverOn ? string.Empty : ", never on this battle").Append(", favors polearm x").Append(SpearRules.F0(r.PolearmFavor)).Append(" / sidearm x")
             .Append(SpearRules.F0(r.SidearmFavor)).Append("): ");
            if (MenBoth + MenWithout == 0)
            {
                a.Append("no AI man was looked at this battle");
                return new[] { a.ToString(), "spears - the flips: none" };
            }
            a.Append(MenBoth).Append(" AI men carry a melee polearm AND a sidearm (").Append(MenWithout).Append(" others looked at, ").Append(LoadoutReads)
             .Append(" loadout reads); their close radius mean ").Append(Radius.Count > 0 ? Radius.Mean.ToString("0.00", c) : "-").Append(" m (")
             .Append(double.IsNaN(RadiusMin) ? "-" : RadiusMin.ToString("0.00", c)).Append("-").Append(double.IsNaN(RadiusMax) ? "-" : RadiusMax.ToString("0.00", c))
             .Append(" m; the spear's length known for ").Append(MenLengthKnown).Append(") | TIME HOLDING - ON ").Append(Split(true)).Append(" | OFF ").Append(Split(false))
             .Append(" (OFF = the same looks with the preference off: the game's own choice)");
            var b = new StringBuilder(700);
            b.Append("spears - the flips: to the sidearm ").Append(Flips(SpearFlip.ToSidearm)).Append(", back to the polearm ").Append(Flips(SpearFlip.ToPolearm))
             .Append(", back at once in a back rank ").Append(Flips(SpearFlip.ToPolearmBackRank)).Append(" (").Append(Flips(SpearFlip.Started)).Append(" men began on the polearm)")
             .Append(" | the favors the decorator wrote: the polearm x").Append(SpearRules.F0(r.PolearmFavor)).Append(' ').Append(FavorPolearm)
             .Append(" recomputes, the sidearm x").Append(SpearRules.F0(r.SidearmFavor)).Append(' ').Append(FavorSidearm)
             .Append(" | attacks begun holding the polearm ").Append(AttacksHoldingPolearm).Append(", the sidearm ").Append(AttacksHoldingSidearm)
             .Append("; by back-rank men ").Append(BackRankAttacks).Append(" (with the polearm ").Append(BackRankAttacksPolearm).Append(", ")
             .Append(PerMinute(BackRankAttacksPolearm, Seconds(true, SpearCondition.BackRank, SpearHeld.Polearm) + Seconds(false, SpearCondition.BackRank, SpearHeld.Polearm)))
             .Append(" a man-minute of a back-rank man holding it - does the native AI thrust past a mate?)")
             .Append(" | the wield fallback (SpearWieldOverride ").Append(r.WieldOverride ? "ON" : "off").Append("): ").Append(WieldWishes).Append(" men, ")
             .Append(WieldRewrites).Append(" sidearm wields turned into the polearm's")
             .Append(" | looks ").Append(Looks).Append(", cost ").Append(CostLooks > 0 ? (CostMs * 1000 / CostLooks).ToString("0.0", c) + " µs a look" : "-")
             .Append(", favors cleared ").Append(Cleared).Append(", switch changes ").Append(SwitchChanges).Append(", errors ").Append(Errors);
            return new[] { a.ToString(), b.ToString() };
        }

        private string Split(bool on)
        {
            double all = SecondsAll(on);
            if (!(all > 0)) return "no time";
            var sb = new StringBuilder(260);
            sb.Append(all.ToString("0", CultureInfo.InvariantCulture)).Append(" man-s");
            for (int ci = 0; ci < (int)SpearCondition.Count; ci++)
            {
                var cond = (SpearCondition)ci;
                double t = Seconds(on, cond);
                if (!(t > 0)) continue;
                sb.Append("; ").Append(SpearMath.Name(cond)).Append(' ').Append(t.ToString("0", CultureInfo.InvariantCulture)).Append(" s: polearm ")
                  .Append(Pct(Seconds(on, cond, SpearHeld.Polearm), t)).Append(", sidearm ").Append(Pct(Seconds(on, cond, SpearHeld.Sidearm), t))
                  .Append(", other ").Append(Pct(Seconds(on, cond, SpearHeld.Other), t));
            }
            return sb.ToString();
        }

        private static string Pct(double k, double n) => n > 0 ? (100.0 * k / n).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";

        private static string PerMinute(int n, double seconds) => seconds > 0 ? (n / (seconds / 60)).ToString("0.0", CultureInfo.InvariantCulture) : "-";
    }

    // ================================================================== thrusts pass a friend

    /// <summary>Step 76: what the damage decorator decided for one melee collision of a man with a FRIEND (a collision with an enemy = NotFriend).</summary>
    public enum ThrustVerdict
    {
        /// <summary>Not a friend (or no defender) - the game's own answer, untouched.</summary>
        NotFriend,

        /// <summary>A thrust into a friend carries on (ContinueChecking) - the spear may reach the enemy behind him.</summary>
        Passed,

        /// <summary>The master switch is off.</summary>
        ModOff,

        /// <summary>ThrustsPassFriends is off.</summary>
        SwitchedOff,

        /// <summary>A kick / shield bash (an alternative attack) - the game's.</summary>
        KickOrBash,

        /// <summary>A swing - it still bounces off the friend.</summary>
        Swing,

        /// <summary>ThrustsPassPolearmsOnly and the weapon is not a polearm.</summary>
        NotPolearm,

        Count,
    }

    /// <summary>Step 76: one collision as the thrust rule sees it.</summary>
    public struct ThrustFacts
    {
        public bool HasDefender;
        public bool Friend;
        public bool Alternative;
        public bool Thrust;
        public bool Polearm;
    }

    /// <summary>Step 76's thrust settings, read live: the master switch FIRST, ThrustsPassFriends, the polearm-only switch, the friendly-hit stun.</summary>
    public readonly struct ThrustPassRules
    {
        public ThrustPassRules(bool modEnabled, bool enabled, bool polearmsOnly, double stunSeconds)
        {
            ModEnabled = modEnabled;
            Enabled = enabled;
            PolearmsOnly = polearmsOnly;
            StunSeconds = double.IsNaN(stunSeconds) ? ThrustPassMath.VanillaStunSeconds : Math.Max(0, Math.Min(ThrustPassMath.MaxStunSeconds, stunSeconds));
        }

        public bool ModEnabled { get; }

        /// <summary>ThrustsPassFriends.</summary>
        public bool Enabled { get; }

        public bool PolearmsOnly { get; }

        /// <summary>FriendlyHitStunSeconds - the game's StunPeriodAttackerFriendlyFire while the rule is on (vanilla 0.4).</summary>
        public double StunSeconds { get; }

        public bool On => ModEnabled && Enabled;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Enabled ? "ThrustsPassFriends" : null;

        public static ThrustPassRules From(TraxSettings s) => new ThrustPassRules(s.ModEnabled, s.ThrustsPassFriends, s.ThrustsPassPolearmsOnly, s.FriendlyHitStunSeconds);

        public bool SameAs(in ThrustPassRules o) => ModEnabled == o.ModEnabled && Enabled == o.Enabled && PolearmsOnly == o.PolearmsOnly && StunSeconds == o.StunSeconds;

        public string Describe() => On
            ? "ON (ThrustsPassFriends) - a THRUST (" + (PolearmsOnly ? "a polearm's only - ThrustsPassPolearmsOnly" : "any weapon's") + ") that touches a man of his own side "
              + "carries on (the game's ContinueChecking) instead of bouncing off him, so a second-rank spear can reach the enemy past its front man; swings, kicks and "
              + "bashes into a friend still bounce; the game's friendly-hit stun of the attacker " + SpearRules.F2(ThrustPassMath.VanillaStunSeconds) + " s → "
              + SpearRules.F2(StunSeconds) + " s (StunPeriodAttackerFriendlyFire, FriendlyHitStunSeconds - given back at the battle's end / switched off)"
            : "OFF (" + OffBecause + ") - a thrust into a friend bounces as in the game (the stun " + SpearRules.F2(ThrustPassMath.VanillaStunSeconds) + " s); the contacts are still counted";
    }

    /// <summary>Step 76 - A THRUST PASSES A FRIEND (RBM's second rank is effective; vanilla's MeleeHitCallback cancels a friend's damage and the base
    /// model's DecideWeaponCollisionReaction then bounces a 0-damage blow). Pure: the decision in gate order, the stun wanted.</summary>
    public static class ThrustPassMath
    {
        /// <summary>The game's StunPeriodAttackerFriendlyFire (Native managed_core_parameters.xml, v1.4.8) - the summary's "the game's".</summary>
        public const double VanillaStunSeconds = 0.4;

        public const double MaxStunSeconds = 1;

        /// <summary>A pass followed by an enemy hit of the same attacker within this = "the spear reached the enemy behind" (measurement only).</summary>
        public const double FollowSeconds = 0.5;

        /// <summary>A friend passed is watched this long for a hit reaction (did he still flinch?). Measurement only.</summary>
        public const double FlinchWatchSeconds = 0.5;

        /// <summary>The engine's thrust strike type (<c>StrikeType.Thrust</c> = 1; the smoke checks it).</summary>
        public const int ThrustStrikeType = 1;

        /// <summary>The decision in gate order: no defender / not a friend → the game's; the master switch; the switch; a kick / bash; a swing; a
        /// polearm only → Passed.</summary>
        public static ThrustVerdict Decide(in ThrustPassRules r, in ThrustFacts f)
        {
            if (!f.HasDefender || !f.Friend) return ThrustVerdict.NotFriend;
            if (!r.ModEnabled) return ThrustVerdict.ModOff;
            if (!r.Enabled) return ThrustVerdict.SwitchedOff;
            if (f.Alternative) return ThrustVerdict.KickOrBash;
            if (!f.Thrust) return ThrustVerdict.Swing;
            if (r.PolearmsOnly && !f.Polearm) return ThrustVerdict.NotPolearm;
            return ThrustVerdict.Passed;
        }

        /// <summary>The stun the game should hold now: ours while the rule is on, else NaN (the game's own - give it back).</summary>
        public static double StunWanted(in ThrustPassRules r) => r.On ? r.StunSeconds : double.NaN;

        public static string Name(ThrustVerdict v) => v switch
        {
            ThrustVerdict.Passed => "a thrust passed",
            ThrustVerdict.ModOff => "the mod off",
            ThrustVerdict.SwitchedOff => "switched off (bounced)",
            ThrustVerdict.KickOrBash => "a kick / bash",
            ThrustVerdict.Swing => "a swing (bounced)",
            ThrustVerdict.NotPolearm => "a thrust, not a polearm (bounced)",
            ThrustVerdict.NotFriend => "not a friend",
            _ => v.ToString(),
        };
    }

    /// <summary>Step 76: one mission's friendly contacts - by verdict, the passes by who and on what, the same attack then hitting an enemy within
    /// <see cref="ThrustPassMath.FollowSeconds"/>, the friend's flinch, the stun set - and the [summary] "thrusts past friends" line. The decorator's
    /// counts come from the main thread (MeleeHitCallback); Interlocked anyway.</summary>
    public sealed class ThrustPassStats
    {
        private readonly long[] _verdicts = new long[(int)ThrustVerdict.Count];
        public long PassedOnShield, PassedByPlayer, PassedPolearm, PassedBackRank;
        public long FollowedLanded, FollowedBlocked, FollowedAny;
        public long PassesWatched, FriendFlinched, FriendFine;
        public long EnemyThrusts;
        public double StunSet = double.NaN, StunGame = double.NaN;
        public int StunWrites, StunRestores, SwitchChanges, Errors;
        public bool EverOn;

        public void Add(ThrustVerdict v) => System.Threading.Interlocked.Increment(ref _verdicts[(int)v]);
        public long Count(ThrustVerdict v) => System.Threading.Interlocked.Read(ref _verdicts[(int)v]);

        public long FriendlyContacts()
        {
            long n = 0;
            for (int i = 0; i < (int)ThrustVerdict.Count; i++)
                if ((ThrustVerdict)i != ThrustVerdict.NotFriend) n += Count((ThrustVerdict)i);
            return n;
        }

        public string SummaryLine(in ThrustPassRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(800);
            sb.Append("thrusts past friends (step 76 - ").Append(r.On ? "ThrustsPassFriends ON" : "OFF (" + r.OffBecause + ")").Append(" now")
              .Append(EverOn ? string.Empty : ", never on this battle").Append("): friendly melee contacts ").Append(FriendlyContacts()).Append(" - ");
            bool first = true;
            for (int i = 0; i < (int)ThrustVerdict.Count; i++)
            {
                var v = (ThrustVerdict)i;
                if (v == ThrustVerdict.NotFriend) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(ThrustPassMath.Name(v)).Append(' ').Append(Count(v));
            }
            long passed = Count(ThrustVerdict.Passed);
            sb.Append(" | passed: with a polearm ").Append(PassedPolearm).Append(", into his shield ").Append(PassedOnShield).Append(", by you ").Append(PassedByPlayer)
              .Append(", by back-rank men ").Append(PassedBackRank)
              .Append(" | THEN AN ENEMY HIT by the same man within ").Append(ThrustPassMath.FollowSeconds.ToString("0.0", c)).Append(" s: ").Append(FollowedAny)
              .Append(" of ").Append(passed).Append(" (").Append(passed > 0 ? (100.0 * FollowedAny / passed).ToString("0", c) + "%" : "-").Append("; landed ")
              .Append(FollowedLanded).Append(", blocked ").Append(FollowedBlocked).Append(") - thrusts into enemies all battle ").Append(EnemyThrusts)
              .Append(" | the friend passed: flinched (a hit reaction within ").Append(ThrustPassMath.FlinchWatchSeconds.ToString("0.0", c)).Append(" s) ").Append(FriendFlinched)
              .Append(", no reaction ").Append(FriendFine).Append(" (of ").Append(PassesWatched).Append(" watched - his actions read while Athletics runs)")
              .Append(" | the friendly-hit stun: ").Append(double.IsNaN(StunSet) ? "never set" : StunSet.ToString("0.00", c) + " s").Append(" (the game's ")
              .Append(double.IsNaN(StunGame) ? ThrustPassMath.VanillaStunSeconds.ToString("0.00", c) : StunGame.ToString("0.00", c)).Append(" s; written ")
              .Append(StunWrites).Append(", given back ").Append(StunRestores).Append(") | switch changes ").Append(SwitchChanges).Append(", errors ").Append(Errors);
            return sb.ToString();
        }
    }
}
