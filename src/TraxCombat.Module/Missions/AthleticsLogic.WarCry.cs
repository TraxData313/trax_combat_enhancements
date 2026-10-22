using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 79 - WAR CRIES ONLY (Toni 2026-10-22: battles that sound alive; AI_NOTES "Step 79", DESIGN decision 75). SOUND ONLY - nothing here touches
    /// movement, readiness, the rest marks, morale or orders. Three causes queue a cry: a rested man going back in by his own mark (<see cref="GoIn"/>), a
    /// mate going in with him by contagion (an answer), a formation's NEW charge order (the order poll - <see cref="NoteChargeOrder"/> /
    /// <see cref="ChargeRoars"/>). The queue is drained on the mission tick (<see cref="TickWarCries"/>), never in an engine callback; each cry passes the
    /// man's own limit, his unit's cap and the battle's cap (Core <see cref="WarCryLimiter"/>) right before it plays. Fixed arrays - nothing allocated at
    /// a thousand men. The master switch FIRST, then WarCriesEnabled, read live.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private const int WarCryUnits = GameSprintBody.MaxTeams * GameSprintBody.SlotsPerTeam;

        private readonly WarCryStats _cryStats = new WarCryStats();
        private readonly WarCryLimiter _cryLimiter = new WarCryLimiter(WarCryUnits);
        private readonly WarCryQueue<TrackedAgent> _cryQueue = new WarCryQueue<TrackedAgent>();
        private readonly bool[] _cryChargeNew = new bool[WarCryUnits];
        private readonly bool[] _cryChargeRoar = new bool[WarCryUnits];
        private readonly int[] _cryChargeChosen = new int[WarCryUnits];
        private readonly double[] _cryChargeLast = new double[WarCryUnits];
        private bool _cryAnyCharge, _cryWasOn;
        private string _seenCryText = string.Empty;
        private readonly bool[] _firstCry = new bool[(int)WarCryCause.Count];
        private readonly bool[] _firstCryDrop = new bool[(int)WarCryGate.Count];
        private bool _firstChargeRoar;

        internal WarCryStats WarCryStats => _cryStats;
        internal int WarCriesQueued => _cryQueue.Count;

        /// <summary>The engine side (the smoke swaps in a counting stand-in).</summary>
        internal IWarCryBody WarCryBody { get; set; } = WarCryBodyDefault.Current;

        /// <summary>The cries' dice - the chances and the stagger (the smoke seeds it).</summary>
        internal IRandomSource WarCryDice { get; set; } = ThreadSafeRandom.Shared;

        private void StartWarCries()
        {
            _cryQueue.Clear();
            _cryLimiter.Reset();
            for (int k = 0; k < WarCryUnits; k++) _cryChargeLast[k] = double.NegativeInfinity;
            var r = WarCryRules.From(TraxSettings.Shared);
            _cryWasOn = r.On;
            _seenCryText = r.Describe();
            TraxLog.Info("warcry", "mission start: " + _seenCryText + " - read live; the [warcry] lines log the first cry of each cause and the first cry each limit "
                + "drops; the [summary] \"war cries\" line (cries by cause, dropped by each limit, the peak in any 1 s)");
        }

        private void NoteWarCrySettings(TraxSettings s)
        {
            string now = WarCryRules.From(s).Describe();
            if (now == _seenCryText) return;
            _seenCryText = now;
            TraxLog.Info("warcry", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now);
        }

        // ------------------------------------------------------------------ the causes

        /// <summary>A rested man went back in (<see cref="WarCryCause.BackIn"/>) or a mate went with him (<see cref="WarCryCause.Answer"/>): his roll, and if
        /// it wins his cry queued (back in at once, an answer 0..AnswerDelaySeconds later). Never changes anything but the queue.</summary>
        private void WarCryEvent(TrackedAgent st, WarCryCause cause, double now)
        {
            try
            {
                var r = WarCryRules.From(TraxSettings.Shared);
                if (!r.On) return;
                _cryStats.Events[(int)cause]++;
                if (!Eligible(st, cause)) return;
                if (!ManFree(st, cause, now, in r)) return;
                if (!WarCryMath.Rolls(in r, cause, WarCryDice.NextDouble())) return;
                _cryStats.RollsWon[(int)cause]++;
                Enqueue(st, cause, now + WarCryMath.Delay(in r, cause, WarCryDice.NextDouble()), UnitOf(st));
            }
            catch (Exception e)
            {
                _cryStats.Errors++;
                Failed("warcry.event", e);
            }
        }

        /// <summary>From the order poll: a formation's kind became a charge (from anything that is not one) - marked; <see cref="ChargeRoars"/> runs
        /// after the poll's read.</summary>
        private void NoteChargeOrder(int slot, double now)
        {
            if (slot < 0 || slot >= WarCryUnits) return;
            if (!WarCryRules.From(TraxSettings.Shared).On) return;
            _cryChargeNew[slot] = true;
            _cryAnyCharge = true;
        }

        /// <summary>The units marked by <see cref="NoteChargeOrder"/> roar: each unit at most once per its cooldown; one pass over the fighters from a
        /// random start (so the cap does not always pick the same men), each eligible man of a roaring unit rolls the share, the unit's first
        /// ChargeMaxMen winners queued 0..ChargeStaggerSeconds after the order. Mounted men too.</summary>
        private void ChargeRoars(double now)
        {
            if (!_cryAnyCharge) return;
            _cryAnyCharge = false;
            var r = WarCryRules.From(TraxSettings.Shared);
            bool any = false;
            int roaring = 0;
            for (int k = 0; k < WarCryUnits; k++)
            {
                _cryChargeRoar[k] = false;
                _cryChargeChosen[k] = 0;
                if (!_cryChargeNew[k]) continue;
                _cryChargeNew[k] = false;
                if (!r.On) continue;
                _cryStats.ChargeOrders++;
                if (!WarCryMath.ChargeRoars(in r, _cryChargeLast[k], now))
                {
                    _cryStats.ChargeCooled++;
                    continue;
                }
                _cryChargeLast[k] = now;
                _cryChargeRoar[k] = true;
                _cryStats.ChargeRoars++;
                roaring++;
                any = true;
            }
            if (!any || _count == 0) return;
            int cap = WarCryMath.ChargeShoutersPerUnit(in r, roaring);
            var body = SprintBody;
            int start = (int)(WarCryDice.NextDouble() * _count);
            if (start >= _count) start = 0;
            for (int i = 0; i < _count; i++)
            {
                int j = i + start;
                if (j >= _count) j -= _count;
                var st = _dense[j];
                if (st.Removed) continue;
                int slot = body.FormationSlot(st);
                if (slot < 0 || slot >= WarCryUnits || !_cryChargeRoar[slot]) continue;
                _cryStats.Events[(int)WarCryCause.Charge]++;
                if (!Eligible(st, WarCryCause.Charge)) continue;
                if (!WarCryMath.Rolls(in r, WarCryCause.Charge, WarCryDice.NextDouble())) continue;
                if (_cryChargeChosen[slot] >= cap)
                {
                    _cryStats.ChargeOverMax++;
                    continue;
                }
                if (!ManFree(st, WarCryCause.Charge, now, in r)) continue;
                _cryStats.RollsWon[(int)WarCryCause.Charge]++;
                _cryChargeChosen[slot]++;
                Enqueue(st, WarCryCause.Charge, now + WarCryMath.Delay(in r, WarCryCause.Charge, WarCryDice.NextDouble()), slot);
            }
            if (_firstChargeRoar) return;
            for (int k = 0; k < WarCryUnits; k++)
            {
                if (!_cryChargeRoar[k] || _cryChargeChosen[k] == 0) continue;
                _firstChargeRoar = true;
                TraxLog.Info("warcry", "first CHARGE ROAR at " + Sec(now) + " s: formation slot " + k + " got a new charge order - " + _cryChargeChosen[k]
                    + " of its men queued to shout (" + r.ChargeSharePercent + "% each, at most " + cap + " a unit with " + roaring + " units roaring at once) over 0-" + F1(r.ChargeStaggerSeconds)
                    + " s; sound only");
                break;
            }
        }

        private bool Eligible(TrackedAgent st, WarCryCause cause)
        {
            if (!st.Removed && !IsPlayer(st) && WarCryBody.MayShout(st)) return true;
            _cryStats.NotEligible[(int)cause]++;
            return false;
        }

        /// <summary>His own limit at the event: a cry of his already waiting, or one less than ManCooldownSeconds ago = dropped (the man's limit).</summary>
        private bool ManFree(TrackedAgent st, WarCryCause cause, double now, in WarCryRules r)
        {
            if (!st.WarCryQueued && WarCryMath.ManReady(st.WarCryNext, now)) return true;
            Drop(st, cause, WarCryGate.Man, now);
            return false;
        }

        private int UnitOf(TrackedAgent st)
        {
            int slot = SprintBody.FormationSlot(st);
            return slot >= 0 && slot < WarCryUnits ? slot : -1;
        }

        private void Enqueue(TrackedAgent st, WarCryCause cause, double at, int unit)
        {
            if (!_cryQueue.Add(st, at, cause, unit))
            {
                Drop(st, cause, WarCryGate.Queue, at);
                return;
            }
            st.WarCryQueued = true;
            _cryStats.Queued[(int)cause]++;
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick (TickAthletics, after the order poll): switched off → every waiting cry dropped at once; else each cry due now passes the
        /// three limits and plays (<see cref="IWarCryBody.Shout"/>) or is dropped. No allocation.</summary>
        internal void TickWarCries(double now)
        {
            if (_cryQueue.Count == 0 && !_cryWasOn) // nothing waiting and was off: just watch the switch
            {
                _cryWasOn = WarCryRules.From(TraxSettings.Shared).On;
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var r = WarCryRules.From(TraxSettings.Shared);
                if (!r.On)
                {
                    int n = _cryQueue.Count;
                    for (int i = n - 1; i >= 0; i--)
                    {
                        var st = _cryQueue.Man(i);
                        st.WarCryQueued = false;
                        _cryStats.Dropped[(int)_cryQueue.Cause(i), (int)WarCryGate.SwitchedOff]++;
                    }
                    _cryQueue.Clear();
                    if (_cryWasOn)
                        TraxLog.Info("warcry", "war cries switched OFF at " + Sec(now) + " s (" + r.OffBecause + "): " + n + " waiting cries dropped");
                    _cryWasOn = false;
                    return;
                }
                _cryWasOn = true;
                var body = WarCryBody;
                for (int i = _cryQueue.Count - 1; i >= 0; i--)
                {
                    if (i >= _cryQueue.Count) continue;
                    if (_cryQueue.At(i) > now) continue;
                    var st = _cryQueue.Man(i);
                    var cause = _cryQueue.Cause(i);
                    int unit = _cryQueue.UnitOf(i);
                    _cryQueue.RemoveAt(i);
                    st.WarCryQueued = false;
                    if (st.Removed || IsPlayer(st) || !body.MayShout(st))
                    {
                        Drop(st, cause, WarCryGate.Gone, now);
                        continue;
                    }
                    var gate = _cryLimiter.Check(in r, now, unit, st.WarCryNext);
                    if (gate != WarCryGate.Ok)
                    {
                        Drop(st, cause, gate, now);
                        continue;
                    }
                    bool mounted = body.IsMounted(st);
                    body.Shout(st, mounted, now);
                    _cryLimiter.Note(now, unit);
                    _cryStats.PeakBattle = _cryLimiter.PeakBattle;
                    _cryStats.PeakUnit = _cryLimiter.PeakUnit;
                    _cryStats.Cried[(int)cause]++;
                    if (mounted) _cryStats.CriedMounted[(int)cause]++;
                    if (!double.IsNaN(st.WarCryLast)) _cryStats.NoteGap(now - st.WarCryLast);
                    st.WarCryLast = now;
                    st.WarCryNext = WarCryMath.NextAllowed(in r, now);
                    if (!_firstCry[(int)cause])
                    {
                        _firstCry[(int)cause] = true;
                        TraxLog.Info("warcry", "first WAR CRY (" + WarCryMath.CauseName(cause) + ") at " + Sec(now) + " s: " + Name(st) + (mounted ? " on a horse (the game's rally shout)" : "")
                            + " - " + (cause == WarCryCause.BackIn ? "going back in after his rest" : cause == WarCryCause.Answer ? "answering a mate going back in" : "his unit's new charge order")
                            + "; sound only, his next cry no earlier than " + F1(r.ManCooldownSeconds) + " s");
                    }
                }
            }
            catch (Exception e)
            {
                _cryStats.Errors++;
                Failed("warcry.tick", e);
            }
            _cryStats.CostMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        private void Drop(TrackedAgent st, WarCryCause cause, WarCryGate gate, double now)
        {
            _cryStats.Dropped[(int)cause, (int)gate]++;
            if (_firstCryDrop[(int)gate]) return;
            _firstCryDrop[(int)gate] = true;
            TraxLog.Info("warcry", "first cry dropped by " + WarCryMath.GateName(gate) + " at " + Sec(now) + " s: " + Name(st) + " (" + WarCryMath.CauseName(cause) + ")");
        }

        internal void WriteWarCrySummary()
        {
            // what still waits at the end is dropped as switched off (it never played)
            for (int i = _cryQueue.Count - 1; i >= 0; i--)
            {
                _cryQueue.Man(i).WarCryQueued = false;
                _cryStats.Dropped[(int)_cryQueue.Cause(i), (int)WarCryGate.SwitchedOff]++;
            }
            _cryQueue.Clear();
            var lines = new List<string>(1);
            _cryStats.AddSummaryLines(lines, WarCryRules.From(TraxSettings.Shared));
            foreach (var line in lines) TraxLog.Info("summary", line);
        }
    }
}
