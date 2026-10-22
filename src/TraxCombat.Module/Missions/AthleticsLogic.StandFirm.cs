using System;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 75 - A STANDING SHIELD WALL NEVER LEAVES ITS SLOT (Toni 2026-10-22: "the men in the shield wall still step forward even when on a standing
    /// order, they take a few steps to strike at an enemy, then fall back"; AI_NOTES "Step 75", DESIGN decision 69). Rides step 41's look
    /// (<see cref="LookWall"/>): a man step 41 holds whose formation is on a Stop, or on a Move at its place (<see cref="StandFirmMath.Conditions"/>),
    /// STANDS FIRM - the leash 0 for him (each step it would have given counted as refused), a running AI sprint ended, the engine's walk target pinned
    /// to his slot instead of the game's frame (a Stop's "where he stands", step 68's pace), the tolerance it is told tightened to the slack, and more
    /// than the slack ahead of his slot he is walked straight back (step 63's backpedal, at once - <see cref="OutOfPlaceLook"/>). His attack input is
    /// never touched: he guards, blocks, swings and kicks at what is in his reach. No allocation per look.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly StandFirmStats _firm = new StandFirmStats();
        private StandFirmRules _seenFirm;
        private bool _firstFirmLogged, _firstFirmStrikeLogged, _firstFirmBackLogged;
        private readonly bool[] _firstLeverLogged = new bool[(int)StandFirmLever.Count];

        /// <summary>This mission's stand-firm numbers (the offline smoke reads them).</summary>
        internal StandFirmStats StandFirmStats => _firm;

        private void StartStandFirm()
        {
            _seenFirm = StandFirmRules.From(TraxSettings.Shared);
            if (_seenFirm.Enabled) _firm.EverOn = true;
            _firm.SampleSeconds = WallMath.SampleSeconds;
            TraxLog.Info("wall", "stand firm (step 75): " + _seenFirm.Describe() + " - read live; the [summary] \"wall stands firm\" line compares the same men ON vs OFF");
        }

        /// <summary>From <see cref="TickWall"/>: a settings change logged (the men follow at their next look, within 0.25 s).</summary>
        private void TickStandFirmSettings(double now)
        {
            var fr = StandFirmRules.From(TraxSettings.Shared);
            if (fr.Enabled) _firm.EverOn = true;
            if (fr.SameAs(in _seenFirm)) return;
            bool was = _seenFirm.Enabled;
            _seenFirm = fr;
            if (was != fr.Enabled) _firm.SwitchChanges++;
            TraxLog.Info("wall", "stand firm (step 75) changed mid-mission at " + Sec(now) + " s: " + fr.Describe()
                + (was != fr.Enabled ? (fr.Enabled ? " - every man of a standing wall stands firm within " : " - the standing walls' men back to the leash and the game's frame within ") + F2(WallMath.PollSeconds) + " s" : string.Empty));
        }

        /// <summary>
        /// One look (from <see cref="LookWall"/>, after step 41's verdict <paramref name="v"/>): the order conditions (ON and OFF - the latch kept either
        /// way), the decision, the transitions, a running sprint ended. True = he stands firm now.
        /// </summary>
        private bool StandFirmLook(TrackedAgent st, WallState ws, in WallFacts f, WallVerdict v, double now)
        {
            var fr = StandFirmRules.From(TraxSettings.Shared);
            var cond = StandFirmVerdict.NotHeld;
            if (f.HasPositions && WallMath.Eligible(in f) == WallVerdict.Hold) cond = StandFirmMath.Conditions(in fr, in f, ref ws.FirmLatch);
            else ws.FirmLatch = default;
            ws.FirmCond = cond == StandFirmVerdict.Stands;
            bool held = v == WallVerdict.Hold;
            var d = StandFirmMath.Decide(in fr, held, cond);
            if (held) _firm.AddLook(d);
            bool firm = d == StandFirmVerdict.Stands;
            if (firm && !ws.Firm)
            {
                ws.Firm = true;
                ws.FirmPaceRefused = false;
                if (!ws.EverFirm)
                {
                    ws.EverFirm = true;
                    _firm.MenStood++;
                }
                if (!_firstFirmLogged)
                {
                    _firstFirmLogged = true;
                    TraxLog.Info("wall", "first man STANDING FIRM this mission (step 75): " + Name(st) + " at " + Sec(now) + " s - his formation in a " + WallMath.Name(f.Arrangement)
                        + " under the order " + BraceMath.Name(f.Order)
                        + (f.Order == BraceOrderKind.Move ? " (at its place: " + F2(StandFirmMath.FromPlace(in f)) + " m from its order position, its men at " + F2(StandFirmMath.Speed(in f)) + " m/s)" : " (a stop stands at once)")
                        + "; he stands " + F2(LeashMath.Out(in f)) + " m out of his slot - no step out, no sprint, walked back beyond " + F2(fr.SlackMetres) + " m"
                        + (fr.PinFrame ? ", the engine's walk target his slot" : string.Empty) + (fr.TightTolerance ? ", the tolerance " + F2(StandFirmMath.Tolerance(in fr, true, f.Deviation)) + " m (the formation's " + F2(f.Deviation) + " m)" : string.Empty));
                }
            }
            else if (!firm && ws.Firm)
            {
                ws.Firm = false;
                ws.FirmLeash = default;
                if (!held) _firm.LetGo(v);
            }
            if (!firm) ws.FirmLeash = default;
            if (firm && st.Sprint.Active && !st.Sprint.ByKey)
            {
                EndSprint(st, SprintEnd.StandFirm, now);
                if (Refused(StandFirmLever.Sprint)) FirstLever(st, StandFirmLever.Sprint, now, "a running sprint ended");
            }
            return firm;
        }

        /// <summary>A man standing firm: the step the leash (<paramref name="leash"/> = step 42's L) WOULD give him now - counted as refused (the leash
        /// itself is 0 for him). The same enemy reads the leash makes for a held man in his slot.</summary>
        private void FirmWouldStep(TrackedAgent st, WallState ws, in WallFacts f, double leash, double now)
        {
            if (leash <= 0 || !f.HasPositions)
            {
                ws.FirmLeash = default;
                return;
            }
            double outM = LeashMath.Out(in f);
            bool close = false;
            if (LeashMath.NeedsEnemy(in ws.FirmLeash, leash, outM) && WallBody.TryEnemy(st, out double ex, out double ey))
                close = LeashMath.EnemyCloseInFront(in f, ex, ey, leash, ws.FirmLeash.Free);
            if (LeashMath.Step(ref ws.FirmLeash, leash, outM, close, now) == LeashMove.Freed)
                if (Refused(StandFirmLever.StepOut)) FirstLever(st, StandFirmLever.StepOut, now, "an enemy close in front of his slot (within the leash " + F1(leash) + " + " + F1(LeashMath.StrikeReachMetres) + " m) - the step out refused, he strikes from his slot");
        }

        /// <summary>A man standing firm, at each hold write: the engine's walk target = his slot (or the game's own frame back when the pin is off); step
        /// 68's pace that would have applied counted as refused (once a spell).</summary>
        private void FirmFrameLook(TrackedAgent st, WallState ws, in WallFacts f, double now)
        {
            var fr = StandFirmRules.From(TraxSettings.Shared);
            try
            {
                if (fr.PinFrame)
                {
                    if (WallBody.Pace(st, f.SlotX, f.SlotY))
                    {
                        ws.Paced = true;
                        _firm.FramesPinned++;
                    }
                }
                else if (ws.Paced)
                {
                    Unpace(st, ws);
                }
                if (!ws.FirmPaceRefused && WallPaceMath.Pace(WallPaceRules.From(TraxSettings.Shared), in f, out _, out _))
                {
                    ws.FirmPaceRefused = true;
                    if (Refused(StandFirmLever.Pace)) FirstLever(st, StandFirmLever.Pace, now, "step 68's advance pace would have sent him toward the game's order slot " + F2(WallPaceMath.OrderLead(in f)) + " m ahead");
                }
            }
            catch (Exception e)
            {
                _firm.Errors++;
                Failed("wall.firm-frame", e);
            }
        }

        /// <summary>The tolerance handed to the engine with the flag for this man (counted when tightened).</summary>
        private double FirmTolerance(in WallFacts f)
        {
            var fr = StandFirmRules.From(TraxSettings.Shared);
            double t = StandFirmMath.Tolerance(in fr, true, f.Deviation);
            if (t != f.Deviation) _firm.ToleranceWrites++;
            return t;
        }

        /// <summary>A lever refused - counted (the walks back are counted by <see cref="StandFirmStats.Began"/>); true = the first of its kind (the
        /// caller builds its line only then - no string per refusal).</summary>
        private bool Refused(StandFirmLever lever)
        {
            if (lever != StandFirmLever.KickOrPunish && lever != StandFirmLever.Pushed) _firm.Refuse(lever);
            return !_firstLeverLogged[(int)lever];
        }

        /// <summary>The first refusal of a kind, in full.</summary>
        private void FirstLever(TrackedAgent st, StandFirmLever lever, double now, string what)
        {
            _firstLeverLogged[(int)lever] = true;
            TraxLog.Info("wall", "first lever refused for a man standing firm (step 75) - " + StandFirmMath.Name(lever) + ": " + Name(st) + " at " + Sec(now) + " s, " + what);
        }

        /// <summary>From <see cref="OutOfPlaceLook"/>: a spell began / ended while he stood firm.</summary>
        private void FirmSpellBegan(TrackedAgent st, WallState ws, double outM, bool impact, double now)
        {
            _firm.Began(impact, ws.Out.OwnKick);
            var lever = ws.Out.OwnKick ? StandFirmLever.KickOrPunish : StandFirmLever.Pushed;
            if (Refused(lever)) FirstLever(st, lever, now, F2(outM) + " m ahead of his slot"
                + (impact ? " after an impact" : string.Empty) + " - walked straight back as soon as he can move");
        }

        private void FirmSpellBack(TrackedAgent st, WallState ws, double now)
        {
            double secs = now - ws.Out.Since;
            if (!double.IsNaN(secs) && secs >= 0) _firm.Back.Add(secs);
            if (_firstFirmBackLogged) return;
            _firstFirmBackLogged = true;
            TraxLog.Info("wall", "first man standing firm back in his slot (step 75): " + Name(st) + " at " + Sec(now) + " s, " + F2(secs) + " s after he stood out (the farthest "
                + F2(ws.Out.MaxOut) + " m)" + (ws.Out.Pulled ? " - walked back by us" : " - by the game's flag alone"));
        }

        /// <summary>From StartRelease: an AI melee release on foot by a man in stand-firm conditions - ON (standing firm) or OFF, from his slot (within the
        /// slack at his last look) or out of it.</summary>
        internal void NoteFirmAttack(TrackedAgent st, bool mounted, double now)
        {
            if (mounted || IsPlayer(st)) return;
            var ws = st.Wall;
            if (ws == null || !ws.Held || !ws.FirmCond) return;
            var fr = StandFirmRules.From(TraxSettings.Shared);
            bool fromSlot = double.IsNaN(ws.FirmOut) || ws.FirmOut <= fr.SlackMetres;
            _firm.AddStrike(ws.Firm, fromSlot);
            if (!ws.Firm || _firstFirmStrikeLogged) return;
            _firstFirmStrikeLogged = true;
            TraxLog.Info("wall", "first strike by a man standing firm (step 75): " + Name(st) + " at " + Sec(now) + " s, " + F2(double.IsNaN(ws.FirmOut) ? 0 : ws.FirmOut)
                + " m out of his slot - " + (fromSlot ? "from his slot" : "OUT of it (beyond the slack " + F2(fr.SlackMetres) + " m)"));
        }

        /// <summary>A let-go path without a look (the switch / master switch, he left, an error, the mission's end): no longer standing firm.</summary>
        private static void FirmForget(WallState ws)
        {
            ws.Firm = false;
            ws.FirmLeash = default;
        }

        private void WriteStandFirmSummary() => TraxLog.Info("summary", _firm.SummaryLine(StandFirmRules.From(TraxSettings.Shared)));
    }
}
