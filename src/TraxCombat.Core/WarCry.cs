using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 79 - WAR CRIES ONLY (Toni 2026-10-22: battles that sound alive; AI_NOTES "Step 79", DESIGN decision 75). SOUND ONLY: a cry never changes
    /// movement, readiness, the rest marks, morale or orders. Three causes - a rested man going back in by his own ready mark (step 78c), a mate who goes
    /// in with him by contagion answering, and a NEW charge order on his formation (the player's F1 F3 or the AI general's switch to a charge) - and
    /// three rate limits: per man, per unit (a cap in any 1 s) and per battle (a cap in any 1 s), so a thousand men never make a choir or flood the
    /// sound engine. The settings, read live: the master switch FIRST, then <c>WarCriesEnabled</c>, then the numbers.
    /// </summary>
    public readonly struct WarCryRules
    {
        public WarCryRules(bool modEnabled, bool warCriesEnabled, int backInPercent = 35, int answerPercent = 20, double answerDelaySeconds = 1.0,
            int chargeSharePercent = 40, int chargeMaxMen = 12, double chargeStaggerSeconds = 1.5, double chargeUnitCooldownSeconds = 15,
            double manCooldownSeconds = 8, int unitPerSecond = 8, int battlePerSecond = 16)
        {
            ModEnabled = modEnabled;
            WarCriesEnabled = warCriesEnabled;
            BackInPercent = ClampI(backInPercent, 0, 100);
            AnswerPercent = ClampI(answerPercent, 0, 100);
            AnswerDelaySeconds = Clamp(answerDelaySeconds, 0, 3);
            ChargeSharePercent = ClampI(chargeSharePercent, 0, 100);
            ChargeMaxMen = ClampI(chargeMaxMen, 0, WarCryMath.ChargeMaxMenMax);
            ChargeStaggerSeconds = Clamp(chargeStaggerSeconds, 0, 5);
            ChargeUnitCooldownSeconds = Clamp(chargeUnitCooldownSeconds, 0, 120);
            ManCooldownSeconds = Clamp(manCooldownSeconds, 0, 60);
            UnitPerSecond = ClampI(unitPerSecond, 1, WarCryMath.UnitCapMax);
            BattlePerSecond = ClampI(battlePerSecond, 1, WarCryMath.BattleCapMax);
        }

        public static WarCryRules From(TraxSettings s) =>
            new WarCryRules(s.ModEnabled, s.WarCriesEnabled, s.WarCryBackInPercent, s.WarCryAnswerPercent, s.WarCryAnswerDelaySeconds, s.WarCryChargeSharePercent,
                s.WarCryChargeMaxMen, s.WarCryChargeStaggerSeconds, s.WarCryChargeUnitCooldownSeconds, s.WarCryManCooldownSeconds, s.WarCryUnitPerSecond,
                s.WarCryBattlePerSecond);

        public bool ModEnabled { get; }

        /// <summary>WarCriesEnabled - men shout (sound only).</summary>
        public bool WarCriesEnabled { get; }

        /// <summary>WarCryBackInPercent: the chance a rested man going back in by his own ready mark shouts.</summary>
        public int BackInPercent { get; }

        /// <summary>WarCryAnswerPercent: the chance a mate who goes in with him by contagion answers.</summary>
        public int AnswerPercent { get; }

        /// <summary>WarCryAnswerDelaySeconds: an answer comes 0 to this many seconds after (never in sync with the first).</summary>
        public double AnswerDelaySeconds { get; }

        /// <summary>WarCryChargeSharePercent: on a new charge order each of the unit's men rolls this to be one of its shouters.</summary>
        public int ChargeSharePercent { get; }

        /// <summary>WarCryChargeMaxMen: a unit's shouters on one charge order at most (0 = no charge roar).</summary>
        public int ChargeMaxMen { get; }

        /// <summary>WarCryChargeStaggerSeconds: each shouter's cry comes 0 to this many seconds after the order (staggered, not in sync).</summary>
        public double ChargeStaggerSeconds { get; }

        /// <summary>WarCryChargeUnitCooldownSeconds: a unit roars on a charge order at most once in this many seconds (an AI's charge that flickers
        /// on and off is one roar).</summary>
        public double ChargeUnitCooldownSeconds { get; }

        /// <summary>WarCryManCooldownSeconds: one man shouts at most once in this many seconds, whatever the cause.</summary>
        public double ManCooldownSeconds { get; }

        /// <summary>WarCryUnitPerSecond: one unit's cries in any 1 s at most.</summary>
        public int UnitPerSecond { get; }

        /// <summary>WarCryBattlePerSecond: the whole battle's cries in any 1 s at most.</summary>
        public int BattlePerSecond { get; }

        /// <summary>Cries play: the master switch FIRST, then its own switch.</summary>
        public bool On => ModEnabled && WarCriesEnabled;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !WarCriesEnabled ? "WarCriesEnabled" : null;

        public string Describe() =>
            "war cries (step 79, sound only - never movement, readiness, the rest marks, morale or orders): a rested man going back in by his own ready mark shouts at "
            + BackInPercent + "%, a mate going in with him by contagion answers at " + AnswerPercent + "% (0-" + F(AnswerDelaySeconds) + " s after); a NEW charge order "
            + "(yours or the AI general's) - each man of the unit rolls " + ChargeSharePercent + "% to shout, at most " + ChargeMaxMen + " a unit, staggered over 0-"
            + F(ChargeStaggerSeconds) + " s, one roar a unit in " + F(ChargeUnitCooldownSeconds) + " s; limits: one cry a man in " + F(ManCooldownSeconds) + " s, "
            + UnitPerSecond + " a unit and " + BattlePerSecond + " a battle in any 1 s; never you, the routed or fleeing, the dead; riders on a charge (the game's "
            + "rally shout) - " + (On ? "ON" : "OFF (" + OffBecause + ")");

        private static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        private static int ClampI(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        internal static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 79: why a man shouts.</summary>
    public enum WarCryCause
    {
        /// <summary>A rested man going back in by his own ready mark (step 78c).</summary>
        BackIn,

        /// <summary>A mate who went in with him by contagion answers.</summary>
        Answer,

        /// <summary>His formation got a new charge order.</summary>
        Charge,

        Count,
    }

    /// <summary>Step 79: what stopped a cry (or let it through).</summary>
    public enum WarCryGate
    {
        /// <summary>It plays.</summary>
        Ok,

        /// <summary>He shouted less than <see cref="WarCryRules.ManCooldownSeconds"/> ago (or a cry of his is already queued).</summary>
        Man,

        /// <summary>His unit is at its cap in the last 1 s.</summary>
        Unit,

        /// <summary>The battle is at its cap in the last 1 s.</summary>
        Battle,

        /// <summary>When it came due he could not shout any more (dead, routed, fleeing, the player took him).</summary>
        Gone,

        /// <summary>War cries (or the master switch) went off while it waited.</summary>
        SwitchedOff,

        /// <summary>The queue was full (plumbing - <see cref="WarCryMath.QueueMax"/>).</summary>
        Queue,

        Count,
    }

    public static class WarCryMath
    {
        /// <summary>The caps' ranges (the windows are sized for them; plumbing).</summary>
        public const int UnitCapMax = 30, BattleCapMax = 60, ChargeMaxMenMax = 60;

        /// <summary>The rate limits' window (s) - "a cap per second".</summary>
        public const double WindowSeconds = 1.0;

        /// <summary>Cries waiting at once at most (plumbing; a thousand-man charge queues ~ units x ChargeMaxMen).</summary>
        public const int QueueMax = 512;

        /// <summary>The chance of a cry for this cause (0..1): back in, the answer, a man's roll to be one of the charge's shouters.</summary>
        public static double Chance(in WarCryRules r, WarCryCause cause) => cause switch
        {
            WarCryCause.BackIn => r.BackInPercent / 100.0,
            WarCryCause.Answer => r.AnswerPercent / 100.0,
            WarCryCause.Charge => r.ChargeSharePercent / 100.0,
            _ => 0,
        };

        /// <summary>The roll: <paramref name="die"/> in [0, 1) under the chance.</summary>
        public static bool Rolls(in WarCryRules r, WarCryCause cause, double die) => die < Chance(in r, cause);

        /// <summary>How long after the event the cry comes: back in at once; an answer 0..AnswerDelaySeconds; a charge 0..ChargeStaggerSeconds.</summary>
        public static double Delay(in WarCryRules r, WarCryCause cause, double die)
        {
            double d = die < 0 ? 0 : die >= 1 ? 1 : die;
            return cause switch
            {
                WarCryCause.Answer => d * r.AnswerDelaySeconds,
                WarCryCause.Charge => d * r.ChargeStaggerSeconds,
                _ => 0,
            };
        }

        /// <summary>His own limit: he may shout again from <paramref name="nextAllowed"/> on.</summary>
        public static bool ManReady(double nextAllowed, double now) => !(now < nextAllowed);

        /// <summary>After a cry: his next one no earlier than this.</summary>
        public static double NextAllowed(in WarCryRules r, double now) => now + r.ManCooldownSeconds;

        /// <summary>A unit may roar on a new charge order: its share above 0, at least one shouter allowed, its last roar long enough ago.</summary>
        public static bool ChargeRoars(in WarCryRules r, double lastRoar, double now) =>
            r.ChargeMaxMen > 0 && r.ChargeSharePercent > 0 && !(now - lastRoar < r.ChargeUnitCooldownSeconds);

        /// <summary>
        /// One charge poll's shouters per roaring unit: ChargeMaxMen, but never more than the battle's cap can play over the stagger, shared by the units
        /// roaring at once - ceil(BattlePerSecond x max(1 s, the stagger) / units), at least 1. Without it a thousand-man charge would queue 120 cries for a
        /// 16-a-second battle: the first 16 would play in the first 0.2 s and the rest be dropped - a burst, not a staggered roar (the smoke saw it).
        /// </summary>
        public static int ChargeShoutersPerUnit(in WarCryRules r, int roaringUnits)
        {
            if (roaringUnits <= 0) return r.ChargeMaxMen;
            double budget = r.BattlePerSecond * Math.Max(1.0, r.ChargeStaggerSeconds);
            int share = Math.Max(1, (int)Math.Ceiling(budget / roaringUnits - 1e-9));
            return Math.Min(r.ChargeMaxMen, share);
        }

        public static string CauseName(WarCryCause c) => c switch
        {
            WarCryCause.BackIn => "back in",
            WarCryCause.Answer => "contagion answer",
            WarCryCause.Charge => "charge order",
            _ => c.ToString(),
        };

        public static string GateName(WarCryGate g) => g switch
        {
            WarCryGate.Man => "the man's own limit",
            WarCryGate.Unit => "the unit's cap",
            WarCryGate.Battle => "the battle's cap",
            WarCryGate.Gone => "gone (dead / routed / fleeing / yours)",
            WarCryGate.SwitchedOff => "switched off",
            WarCryGate.Queue => "the queue full",
            _ => g.ToString(),
        };
    }

    /// <summary>
    /// Step 79: the cries of the last <see cref="WarCryMath.WindowSeconds"/> - a fixed ring of times, never allocating. Times must not go backwards
    /// (a time earlier than the newest clears it - a new mission).
    /// </summary>
    public sealed class CryWindow
    {
        private readonly double[] _times;
        private int _head, _count;

        public CryWindow(int capacity) => _times = new double[Math.Max(1, capacity)];

        public int Capacity => _times.Length;

        /// <summary>Cries in (now - 1 s, now].</summary>
        public int CountAt(double now)
        {
            Prune(now);
            return _count;
        }

        /// <summary>One more cry is allowed under <paramref name="cap"/> now.</summary>
        public bool Allows(double now, int cap) => CountAt(now) < Math.Min(cap, _times.Length);

        public void Add(double now)
        {
            Prune(now);
            if (_count == _times.Length)
            {
                _head = (_head + 1) % _times.Length; // full: the oldest goes (never happens under a cap within the capacity)
                _count--;
            }
            _times[(_head + _count) % _times.Length] = now;
            _count++;
        }

        public void Clear() => _head = _count = 0;

        private void Prune(double now)
        {
            if (_count > 0 && now < _times[(_head + _count - 1) % _times.Length])
            {
                Clear(); // time went backwards
                return;
            }
            while (_count > 0 && !(now - _times[_head] < WarCryMath.WindowSeconds))
            {
                _head = (_head + 1) % _times.Length;
                _count--;
            }
        }
    }

    /// <summary>
    /// Step 79: the three rate limits in one gate - his own (the time he may shout again), his unit's cap and the battle's cap in any 1 s - and the
    /// peaks (the most cries in any 1 s, the battle's and one unit's). Units are 0..units-1; anything else counts as one spare "no formation" unit.
    /// </summary>
    public sealed class WarCryLimiter
    {
        private readonly CryWindow[] _units;
        private readonly CryWindow _battle = new CryWindow(WarCryMath.BattleCapMax);

        public WarCryLimiter(int units)
        {
            _units = new CryWindow[Math.Max(0, units) + 1];
            for (int i = 0; i < _units.Length; i++) _units[i] = new CryWindow(WarCryMath.UnitCapMax);
        }

        /// <summary>The most cries the battle played in any 1 s, and one unit.</summary>
        public int PeakBattle { get; private set; }
        public int PeakUnit { get; private set; }

        public int Unit(int unit) => unit >= 0 && unit < _units.Length - 1 ? unit : _units.Length - 1;

        /// <summary>Would a cry of this man of this unit play now? His own limit first, then the unit's, then the battle's.</summary>
        public WarCryGate Check(in WarCryRules r, double now, int unit, double manNextAllowed)
        {
            if (!WarCryMath.ManReady(manNextAllowed, now)) return WarCryGate.Man;
            if (!_units[Unit(unit)].Allows(now, r.UnitPerSecond)) return WarCryGate.Unit;
            if (!_battle.Allows(now, r.BattlePerSecond)) return WarCryGate.Battle;
            return WarCryGate.Ok;
        }

        /// <summary>A cry played: into both windows, the peaks.</summary>
        public void Note(double now, int unit)
        {
            var w = _units[Unit(unit)];
            w.Add(now);
            _battle.Add(now);
            int u = w.CountAt(now), b = _battle.CountAt(now);
            if (u > PeakUnit) PeakUnit = u;
            if (b > PeakBattle) PeakBattle = b;
        }

        public void Reset()
        {
            foreach (var w in _units) w.Clear();
            _battle.Clear();
            PeakBattle = PeakUnit = 0;
        }
    }

    /// <summary>
    /// Step 79: the cries waiting for their moment (the stagger, an answer's delay) - drained on the mission tick, never in an engine callback. Fixed
    /// arrays: adding and taking never allocate; full = the cry is dropped (<see cref="WarCryGate.Queue"/>).
    /// </summary>
    public sealed class WarCryQueue<T> where T : class
    {
        private readonly T?[] _men;
        private readonly double[] _at;
        private readonly WarCryCause[] _cause;
        private readonly int[] _unit;

        public WarCryQueue(int capacity = WarCryMath.QueueMax)
        {
            int n = Math.Max(1, capacity);
            _men = new T?[n];
            _at = new double[n];
            _cause = new WarCryCause[n];
            _unit = new int[n];
        }

        public int Count { get; private set; }

        public bool Add(T man, double at, WarCryCause cause, int unit)
        {
            if (Count >= _men.Length) return false;
            _men[Count] = man;
            _at[Count] = at;
            _cause[Count] = cause;
            _unit[Count] = unit;
            Count++;
            return true;
        }

        public T Man(int i) => _men[i]!;
        public double At(int i) => _at[i];
        public WarCryCause Cause(int i) => _cause[i];
        public int UnitOf(int i) => _unit[i];

        /// <summary>Removes entry <paramref name="i"/> (the last one moves into its place - walk the queue from the end).</summary>
        public void RemoveAt(int i)
        {
            int last = Count - 1;
            if (i < 0 || i > last) return;
            if (i != last)
            {
                _men[i] = _men[last];
                _at[i] = _at[last];
                _cause[i] = _cause[last];
                _unit[i] = _unit[last];
            }
            _men[last] = null;
            Count = last;
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++) _men[i] = null;
            Count = 0;
        }
    }

    /// <summary>Step 79: the [summary] "war cries" numbers.</summary>
    public sealed class WarCryStats
    {
        /// <summary>Events that could bring a cry, rolls won, cries queued and played - by cause.</summary>
        public readonly long[] Events = new long[(int)WarCryCause.Count];
        public readonly long[] RollsWon = new long[(int)WarCryCause.Count];
        public readonly long[] Queued = new long[(int)WarCryCause.Count];
        public readonly long[] Cried = new long[(int)WarCryCause.Count];
        public readonly long[] CriedMounted = new long[(int)WarCryCause.Count];

        /// <summary>Men who could not shout at the event (routed, fleeing, dead, you) - by cause.</summary>
        public readonly long[] NotEligible = new long[(int)WarCryCause.Count];

        /// <summary>Cries dropped - by cause and gate.</summary>
        public readonly long[,] Dropped = new long[(int)WarCryCause.Count, (int)WarCryGate.Count];

        /// <summary>Charge orders seen (a formation's NEW charge), roared, cooled down (its unit roared lately), men past the unit's shouters cap.</summary>
        public long ChargeOrders, ChargeRoars, ChargeCooled, ChargeOverMax;

        /// <summary>The most cries in any 1 s - the battle's, one unit's (from the limiter) - and the shortest gap between one man's two cries.</summary>
        public int PeakBattle, PeakUnit;
        public double ShortestManGap = double.NaN;

        public long Errors;
        public double CostMs;

        public long DroppedBy(WarCryGate g)
        {
            long n = 0;
            for (int c = 0; c < (int)WarCryCause.Count; c++) n += Dropped[c, (int)g];
            return n;
        }

        public long TotalCried()
        {
            long n = 0;
            foreach (var v in Cried) n += v;
            return n;
        }

        public void NoteGap(double gap)
        {
            if (double.IsNaN(gap)) return;
            if (double.IsNaN(ShortestManGap) || gap < ShortestManGap) ShortestManGap = gap;
        }

        public void AddSummaryLines(List<string> lines, in WarCryRules r)
        {
            var sb = new System.Text.StringBuilder(512);
            sb.Append("war cries (step 79) - ").Append(r.On ? "ON" : "OFF (" + r.OffBecause + ")").Append(": cries ").Append(TotalCried());
            for (int c = 0; c < (int)WarCryCause.Count; c++)
            {
                var cause = (WarCryCause)c;
                sb.Append(c == 0 ? " - " : "; ").Append(WarCryMath.CauseName(cause)).Append(' ').Append(Cried[c]);
                if (CriedMounted[c] > 0) sb.Append(" (riders ").Append(CriedMounted[c]).Append(')');
                sb.Append(" of ").Append(Events[c]).Append(cause == WarCryCause.Charge ? " men in charging units" : " events")
                    .Append(" (rolls won ").Append(RollsWon[c]).Append(", queued ").Append(Queued[c]).Append(", not eligible ").Append(NotEligible[c]).Append(')');
            }
            sb.Append("; DROPPED by");
            for (int g = 1; g < (int)WarCryGate.Count; g++)
                sb.Append(g == 1 ? " " : ", ").Append(WarCryMath.GateName((WarCryGate)g)).Append(' ').Append(DroppedBy((WarCryGate)g));
            sb.Append("; charge orders ").Append(ChargeOrders).Append(" (roared ").Append(ChargeRoars).Append(", the unit's roar cooling ").Append(ChargeCooled)
                .Append(", men past the unit's shouter cap (").Append(r.ChargeMaxMen).Append(", shared out to the battle's cap x the stagger) ").Append(ChargeOverMax).Append(')');
            sb.Append("; PEAK cries in any 1 s: the battle ").Append(PeakBattle).Append(" (cap ").Append(r.BattlePerSecond).Append("), one unit ").Append(PeakUnit)
                .Append(" (cap ").Append(r.UnitPerSecond).Append("); one man's shortest gap between two cries ")
                .Append(double.IsNaN(ShortestManGap) ? "-" : ShortestManGap.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s (limit ")
                .Append(WarCryRules.F(r.ManCooldownSeconds)).Append(" s); errors ").Append(Errors).Append(", cost ")
                .Append(CostMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms");
            lines.Add(sb.ToString());
        }
    }
}
