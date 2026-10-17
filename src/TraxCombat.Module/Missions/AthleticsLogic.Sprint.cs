using System;
using System.Collections.Generic;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 33 - THE SPRINT (Anton, 2026-10-12: "a short burst of speed, meant to feel a little like a dodge, and for the AI to make
    /// charges sharper"; AI_NOTES "Step 33", DESIGN §2 "The sprint"). Every fighter's <see cref="SprintStore"/> holds seconds =
    /// his CURRENT Athletics points ÷ SprintPointsPerSecond, never below SprintMinSeconds (step 35; clamped when his points drop), refilled every
    /// <see cref="RegenStepSeconds"/> while he is not sprinting (Core <see cref="SprintMath.RefillPerSecond"/>: 7 s standing at full
    /// strength, × the moving factor above a walk, × the tiredness line).
    ///
    /// YOU: holding <c>SprintKey</c> (read every tick through <see cref="ISprintBody.KeyDown"/> - a fight mode, the order menu
    /// closed, your own hands) STARTS a sprint on the press when at least SprintMinStartPercent is stored; letting go ends it,
    /// the rest stays stored. THE AI: every <see cref="SprintMath.OrderPollSeconds"/> every formation's order kind is read (step
    /// 23's read); a kind that differs from the last look is a NEW order - each AI man on foot in that formation sprints once for
    /// up to its share (charge / advance or move), ending early when an enemy is within <see cref="SprintMath.MeleeRangeMetres"/>
    /// or the order changes again. Brace, step back and the pause are not touched.
    ///
    /// SPEED (step 35): the sprint's run factor (<see cref="SprintMath.RunFactor"/> - SprintMax × his FRESH run, then a line to
    /// SprintMin × his CURRENT run; its clock = seconds of MOVING) is quantised in 0.05 steps into <see cref="TrackedAgent.SprintRun"/>;
    /// a step (and the start, and the end) marks him for a properties recompute (the tick's budget queues a charge's hundreds -
    /// <see cref="MaxRecomputesPerTick"/>, the start rotating so nobody starves) and the stat decorator writes it on his run property
    /// in place of his tiredness multiplier, his TopSpeedReachDuration capped at SprintTopSpeedReachSeconds. While sprinting his
    /// Athletics does not refill (RegenPass skips him). The master switch, AthleticsEnabled and SprintEnabled end every sprint at
    /// once (speeds and acceleration back on the next recomputes).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly SprintStats _sprintStats = new SprintStats();
        private readonly SprintFormationRead[] _orderReads = new SprintFormationRead[GameSprintBody.MaxTeams * GameSprintBody.SlotsPerTeam];
        private readonly BraceOrderKind[] _slotKind = new BraceOrderKind[GameSprintBody.MaxTeams * GameSprintBody.SlotsPerTeam];
        private readonly bool[] _slotSeen = new bool[GameSprintBody.MaxTeams * GameSprintBody.SlotsPerTeam];
        private readonly bool[] _slotChanged = new bool[GameSprintBody.MaxTeams * GameSprintBody.SlotsPerTeam];
        private readonly bool[] _slotByPlayer = new bool[GameSprintBody.MaxTeams * GameSprintBody.SlotsPerTeam]; // step 43
        private string _seenSprintText = string.Empty;
        private double _nextOrderPoll = double.NegativeInfinity;
        private double _sprintAccum;
        private int _sprintingNow;
        private bool _playerKeyWasDown;
        private bool _firstYouSprintLogged;
        private bool _firstAiSprintLogged;
        private bool _firstReserveLogged;
        private bool _firstRefusalLogged;
        private bool _firstOrderLogged;
        private bool _ordersSeenAny;

        /// <summary>This mission's sprint numbers (the offline smoke reads them).</summary>
        internal SprintStats SprintStats => _sprintStats;

        /// <summary>The engine side of the sprint (the offline smoke swaps in a stand-in).</summary>
        internal ISprintBody SprintBody { get; set; } = SprintBodyDefault.Current;

        /// <summary>Men sprinting right now.</summary>
        internal int SprintingNow => _sprintingNow;

        private static string SprintText(TraxSettings s) => SprintRules.From(s).Describe(s.SprintKey);

        /// <summary>Mission start: the settings line, read live from now on.</summary>
        private void StartSprint()
        {
            _seenSprintText = SprintText(TraxSettings.Shared);
            TraxLog.Info("sprint", "mission start: " + _seenSprintText + " - read live; formations' orders looked at every " + F2(SprintMath.OrderPollSeconds)
                + " s; the [summary] \"sprint\" lines count your sprints, the AI's by order and the engine's speed against the asked");
        }

        /// <summary>A sprint setting changed (from ApplySettingsChange): one line. Switching it off ends every sprint in the next tick.</summary>
        private void NoteSprintSettings(TraxSettings s)
        {
            string now = SprintText(s);
            if (now == _seenSprintText) return;
            _seenSprintText = now;
            TraxLog.Info("sprint", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now);
            // step 35: a running sprint's acceleration / factor are written by the decorator - recompute every sprinter once
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st.Sprint.Active && !st.Removed) st.SpeedDirty = true;
            }
        }

        /// <summary>
        /// Every tick (from TickAthletics, before the recompute loop): switched off → every sprint ends; else your key, the order
        /// poll, and every <see cref="RegenStepSeconds"/> the sprint pass. No allocation.
        /// </summary>
        internal void TickSprint(float dt, double now, in AthleticsRules r)
        {
            var settings = TraxSettings.Shared;
            var s = SprintRules.From(settings);
            if (!s.Enabled)
            {
                if (_sprintingNow > 0) EndAllSprints(SprintEnd.SwitchedOff, now, aiOnly: false);
                _playerKeyWasDown = true; // back on: a key still held waits for a new press
                _sprintAccum = 0;
                try
                {
                    PollOrdersFor(now, in s, in r, sprintAi: false); // step 43: the fever reads the same poll (else forgotten)
                }
                catch (Exception e)
                {
                    Failed("sprint.orders", e);
                }
                return;
            }
            try
            {
                TickPlayerSprint(now, in s, in r, settings.SprintKeyIndex);
            }
            catch (Exception e)
            {
                Failed("sprint.player", e);
            }
            try
            {
                if (!s.AiOn && _sprintingNow > 0) EndAllSprints(SprintEnd.SwitchedOff, now, aiOnly: true);
                PollOrdersFor(now, in s, in r, sprintAi: s.AiOn);
            }
            catch (Exception e)
            {
                Failed("sprint.orders", e);
            }
            _sprintAccum += dt;
            if (_sprintAccum >= RegenStepSeconds)
            {
                double step = _sprintAccum;
                _sprintAccum = 0;
                SprintPass(step, now, in s, in r);
            }
        }

        // ------------------------------------------------------------------ you

        private TrackedAgent? PlayerRecord() => SmokePlayer != null ? Get(SmokePlayer) : _player;

        private void TickPlayerSprint(double now, in SprintRules s, in AthleticsRules r, int keyIndex)
        {
            var st = PlayerRecord();
            var body = SprintBody;
            if (st == null || st.Removed)
            {
                _playerKeyWasDown = false;
                return;
            }
            var sp = st.Sprint;
            bool you = YouDrive(st);
            bool can = you && body.PlayerCanSprint();
            bool key = can && body.KeyDown(keyIndex);
            if (sp.Active && sp.ByKey)
            {
                if (!key) EndSprint(st, can ? SprintEnd.Released : SprintEnd.ControlChanged, now);
            }
            else if (key && !_playerKeyWasDown && !sp.Active)
            {
                // a PRESS starts one (holding the key through an empty store never restarts by itself)
                if (body.IsMounted(st))
                {
                    _sprintStats.YouRefusedMounted++;
                    LogYouRefused(st, now, "you are on a horse (the sprint is on foot only)", in s, in r);
                }
                else
                {
                    double cap = SprintMath.Capacity(in s, in r, st);
                    var res = sp.TryStart(in s, now, cap, 1.0, true, SprintOrder.None);
                    if (res == SprintStart.Started)
                    {
                        BeginSprint(st, now, in s, in r, cap);
                    }
                    else if (res == SprintStart.BelowMinimum)
                    {
                        _sprintStats.YouRefusedBelowMinimum++;
                        LogYouRefused(st, now, F2(sp.StoredUpTo(cap)) + " s stored of " + F2(cap) + " s - below the " + s.MinStartPercent + "% a start needs ("
                            + F2(cap * s.MinStartShare) + " s)", in s, in r);
                    }
                    else if (res == SprintStart.NoStore)
                    {
                        _sprintStats.YouRefusedEmpty++;
                        LogYouRefused(st, now, "your Athletics bar is empty and SprintMinSeconds is 0 - no sprint store", in s, in r);
                    }
                }
            }
            _playerKeyWasDown = key;
        }

        private void LogYouRefused(TrackedAgent st, double now, string why, in SprintRules s, in AthleticsRules r)
        {
            if (!_firstRefusalLogged)
            {
                _firstRefusalLogged = true;
                TraxLog.Info("sprint", "YOU: first sprint refused this battle at " + Sec(now) + " s - " + why + "; the store refills in "
                    + F1(s.RefillSeconds) + " s from empty standing at full strength (x" + F2(s.RefillMovingFactor) + " moving, down to x"
                    + F2(s.RefillExhaustedFactor) + " at an empty bar)");
            }
            else
            {
                TraxLog.Limited("sprint", "YOU: sprint refused at " + Sec(now) + " s - " + why, "sprint-you");
            }
        }

        // ------------------------------------------------------------------ the AI's orders

        /// <summary>Step 43: the order poll serves the AI sprint AND the fever - it runs while either is on, else the last look is
        /// forgotten (back on, the first look only records).</summary>
        private void PollOrdersFor(double now, in SprintRules s, in AthleticsRules r, bool sprintAi)
        {
            var fr = FeverRules.From(TraxSettings.Shared);
            if (!sprintAi && !fr.Enabled)
            {
                ForgetOrders();
                return;
            }
            PollOrders(now, in s, in r, sprintAi, in fr);
        }

        /// <summary>Every <see cref="SprintMath.OrderPollSeconds"/>: every formation's order kind; a changed kind = a new order → one
        /// pass over the fighters for the men in those formations (the AI sprint while <paramref name="sprintAi"/>, step 43's fever while
        /// it is on). The first look at a formation is not a change.</summary>
        private void PollOrders(double now, in SprintRules s, in AthleticsRules r, bool sprintAi, in FeverRules fr)
        {
            if (now < _nextOrderPoll && now >= _nextOrderPoll - 10 * SprintMath.OrderPollSeconds) return;
            _nextOrderPoll = now + SprintMath.OrderPollSeconds;
            var body = SprintBody;
            int n = body.ReadOrders(_orderReads);
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                int slot = _orderReads[i].Slot;
                if (slot < 0 || slot >= _slotKind.Length) continue;
                var kind = _orderReads[i].Kind;
                _slotByPlayer[slot] = _orderReads[i].ByPlayer;
                if (!_slotSeen[slot])
                {
                    _slotSeen[slot] = true;
                    _ordersSeenAny = true;
                    _slotKind[slot] = kind;
                    continue;
                }
                if (kind == _slotKind[slot]) continue;
                var was = _slotKind[slot];
                _slotKind[slot] = kind;
                _slotChanged[slot] = true;
                any = true;
                var group = SprintMath.Group(kind);
                _sprintStats.AddOrderSeen(group);
                if (!_firstOrderLogged)
                {
                    _firstOrderLogged = true;
                    TraxLog.Info("sprint", "first new order seen this battle at " + Sec(now) + " s: formation slot " + slot + " " + was + " → " + kind + " ("
                        + SprintMath.OrderName(group) + (!SprintMath.AiSprints(group) ? ": no sprint)" : ": its AI men on foot sprint what they hold above a "
                        + F0(100 * SprintMath.AiKeep(in s, group)) + "% reserve)"));
                }
                else if (TraxLog.VerboseWants("sprint-order"))
                {
                    TraxLog.Verbose("sprint", "new order at " + Sec(now) + " s: formation slot " + slot + " " + was + " → " + kind, "sprint-order");
                }
            }
            if (!any) return;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                try
                {
                    int slot = body.FormationSlot(st);
                    if (slot < 0 || slot >= _slotChanged.Length || !_slotChanged[slot]) continue;
                    if (sprintAi) OrderChangedFor(st, _slotKind[slot], now, in s, in r);
                }
                catch (Exception e)
                {
                    Failed("sprint.order-start", e);
                }
                try
                {
                    if (fr.Enabled)
                    {
                        int slot = body.FormationSlot(st);
                        if (slot >= 0 && slot < _slotChanged.Length && _slotChanged[slot]) FeverOrderFor(st, _slotKind[slot], _slotByPlayer[slot], now, in fr, in r);
                    }
                }
                catch (Exception e)
                {
                    Failed("fever.order", e);
                }
            }
            for (int k = 0; k < _slotChanged.Length; k++) _slotChanged[k] = false;
        }

        /// <summary>While the AI sprint is off, the last look is forgotten: back on, the first look only records (an order given
        /// meanwhile is not "new").</summary>
        private void ForgetOrders()
        {
            if (!_ordersSeenAny) return;
            _ordersSeenAny = false;
            for (int k = 0; k < _slotSeen.Length; k++) _slotSeen[k] = false;
            _nextOrderPoll = double.NegativeInfinity;
        }

        private void OrderChangedFor(TrackedAgent st, BraceOrderKind kind, double now, in SprintRules s, in AthleticsRules r)
        {
            if (st.Removed) return;
            var body = SprintBody;
            if (!body.IsActive(st)) return;
            var sp = st.Sprint;
            if (IsPlayer(st) || !body.IsAiControlled(st))
            {
                _sprintStats.AiSkippedPlayer++;
                return;
            }
            if (sp.Active && !sp.ByKey) EndSprint(st, SprintEnd.OrderChanged, now);
            var group = SprintMath.Group(kind);
            if (!SprintMath.AiSprints(group)) return;
            if (body.IsMounted(st))
            {
                _sprintStats.AiSkippedMounted++;
                return;
            }
            // step 37: everything above the order's reserve (stored - keep x his full store) - no SprintMinStartPercent gate for the AI
            double cap = SprintMath.Capacity(in s, in r, st);
            var res = sp.TryStartAi(in s, now, cap, group);
            if (res == SprintStart.Started)
            {
                _sprintStats.AddAiStart(group);
                BeginSprint(st, now, in s, in r, cap);
            }
            else if (res == SprintStart.ReserveKept)
            {
                _sprintStats.AddAiReserveKept(group);
                if (!_firstReserveLogged)
                {
                    _firstReserveLogged = true;
                    TraxLog.Info("sprint", "first AI sprint held back by a reserve this battle: " + Name(st) + " at " + Sec(now) + " s on a new " + SprintMath.OrderName(group)
                        + " order - stored " + F2(sp.StoredUpTo(cap)) + " s of " + F2(cap) + " s (" + F0(100 * sp.Share(cap)) + "%), " + SprintMath.OrderName(group) + " keeps "
                        + F0(100 * SprintMath.AiKeep(in s, group)) + "% - nothing above the reserve, no sprint "
                        + "(it stays for the next order)");
                }
                else if (TraxLog.VerboseWants("sprint-ai"))
                    TraxLog.Verbose("sprint", Name(st) + " keeps his reserve at " + Sec(now) + " s (" + SprintMath.OrderName(group) + ": stored " + F0(100 * sp.Share(cap))
                        + "% of " + F2(cap) + " s, keeps " + F0(100 * SprintMath.AiKeep(in s, group)) + "%) - no sprint", "sprint-ai");
            }
            else if (res != SprintStart.Off && res != SprintStart.Running)
            {
                _sprintStats.AddAiRefused(group);
            }
        }

        // ------------------------------------------------------------------ the pass

        /// <summary>Every <see cref="RegenStepSeconds"/>: each store clamped to his points; a running sprint advanced (its clock and
        /// drain only while moving), checked and its multiplier stepped; a store below full refilled. Native reads only for a man
        /// who sprints or refills.</summary>
        private void SprintPass(double step, double now, in SprintRules s, in AthleticsRules r)
        {
            var body = SprintBody;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                try
                {
                    var sp = st.Sprint;
                    double cap = SprintMath.Capacity(in s, in r, st);
                    if (sp.Clamp(cap)) _sprintStats.Clamps++;
                    if (sp.Active)
                    {
                        RunSprint(st, sp, step, now, in s, in r);
                        continue;
                    }
                    if (sp.Stored >= cap - 1e-9) continue; // full: nothing to do, no native call
                    if (!body.IsActive(st)) continue;
                    body.ReadMotion(st, out float speed, out float top);
                    bool moving = !AthleticsMath.IsWalking(in r, AthleticsMath.Effort(speed, top));
                    double per = SprintMath.RefillPerSecond(in s, cap, moving, AthleticsMath.PeakShare(in r, st));
                    _sprintStats.RefilledSeconds += sp.Refill(step, per, cap);
                }
                catch (Exception e)
                {
                    Failed("sprint.pass", e);
                    if (st.Sprint.Active) SafeEndSprint(st, now);
                }
            }
        }

        private void RunSprint(TrackedAgent st, SprintStore sp, double step, double now, in SprintRules s, in AthleticsRules r)
        {
            var body = SprintBody;
            if (st.Removed || !body.IsActive(st))
            {
                EndSprint(st, SprintEnd.LeftField, now);
                return;
            }
            if (body.IsMounted(st))
            {
                EndSprint(st, SprintEnd.Mounted, now);
                return;
            }
            if (sp.ByKey ? !IsPlayer(st) || !YouDrive(st) : IsPlayer(st) || !body.IsAiControlled(st))
            {
                EndSprint(st, SprintEnd.ControlChanged, now);
                return;
            }
            body.ReadMotion(st, out float speed, out float top);
            // the speed check: the engine's top against his top at the start, while the asked multiplier is in his properties
            // (step 35: both x his FRESH run - his top at the start ÷ his run multiplier then)
            if (!st.SpeedDirty && st.SprintRun > 0f && st.SprintBaseTop > 0f && top > 0f && st.SprintBaseRun > 0f)
                _sprintStats.AddSpeed(st.SprintRun, top / st.SprintBaseTop * st.SprintBaseRun);
            bool moving = speed >= SprintMath.MovingMetresPerSecond;
            if (sp.Advance(step, moving, out var end))
            {
                EndSprint(st, end, now);
                return;
            }
            if (!sp.ByKey && now >= st.SprintNextMelee)
            {
                st.SprintNextMelee = now + SprintMath.MeleeCheckSeconds;
                _sprintStats.MeleeChecks++;
                if (body.EnemyWithin(st, SprintMath.MeleeRangeMetres))
                {
                    EndSprint(st, SprintEnd.InMelee, now);
                    return;
                }
            }
            SetSprintRun(st, SprintMath.Quantize(sp.RunFactor(in s, st.RunSpeedMultiplier)));
        }

        // ------------------------------------------------------------------ start and end

        private void BeginSprint(TrackedAgent st, double now, in SprintRules s, in AthleticsRules r, double cap)
        {
            var sp = st.Sprint;
            _sprintingNow++;
            st.SprintNextMelee = now + SprintMath.MeleeCheckSeconds;
            st.SprintBaseTop = 0f;
            st.SprintBaseRun = st.RunSpeedMultiplier;
            try
            {
                if (!st.SpeedDirty && st.SprintRun == 0f)
                {
                    SprintBody.ReadMotion(st, out _, out float top);
                    st.SprintBaseTop = top;
                }
            }
            catch (Exception e)
            {
                Failed("sprint.base", e);
            }
            // step 35: the floor, the acceleration
            bool floor = SprintMath.OnFloor(in s, in r, st);
            double raw = SprintMath.PointsCapacity(in s, in r, st);
            float gameReach = st.GameReach;
            float ourReach = SprintMath.TopSpeedReach(gameReach, s.TopSpeedReachSeconds);
            if (s.TopSpeedReachSeconds > 0 && gameReach > 0f) _sprintStats.AddReach(gameReach, ourReach);
            float run = st.RunSpeedMultiplier;
            float m = SprintMath.Quantize(sp.RunFactor(in s, run));
            SetSprintRun(st, m);
            string pointsText = F0(AthleticsMath.Points(in r, st)) + " Athletics points / " + F1(s.PointsPerSecond)
                + (floor ? " = " + F2(raw) + " s - the store's floor SprintMinSeconds " + F1(s.MinSeconds) + " s holds it up" : string.Empty);
            string reachText = s.TopSpeedReachSeconds <= 0 ? "the game's own acceleration (SprintTopSpeedReachSeconds 0)"
                : gameReach > 0f ? "top speed reached in " + F2(ourReach) + " s instead of the game's " + F2(gameReach) + " s"
                : "top speed reached in at most " + F2(s.TopSpeedReachSeconds) + " s (the game's value not seen yet)";
            if (sp.ByKey)
            {
                _sprintStats.YouStarted++;
                if (floor) _sprintStats.YouOffFloor++;
                if (!_firstYouSprintLogged)
                {
                    _firstYouSprintLogged = true;
                    TraxLog.Info("sprint", "YOU: first sprint this battle at " + Sec(now) + " s - stored " + F2(sp.StoredAtStart) + " s of " + F2(cap) + " s ("
                        + pointsText + "), so it lasts T " + F2(sp.Length) + " s of moving: x" + F2(s.MaxMultiplier) + " of your FRESH run speed (the run property x"
                        + F2(run) + " → x" + F2(m) + ") until " + F2(sp.Length * s.FullSpeedShare) + " s, then a straight line to x" + F2(s.MinMultiplier)
                        + " of your current run (x" + F2(s.MinMultiplier * run) + ") at " + F2(sp.Length) + " s; " + reachText
                        + "; it drains only while you move, your Athletics does not refill meanwhile; let go to stop (the rest stays stored)");
                }
                else
                {
                    TraxLog.Limited("sprint", "YOU: sprint at " + Sec(now) + " s - stored " + F2(sp.StoredAtStart) + " of " + F2(cap) + " s" + (floor ? " (the floor)" : string.Empty)
                        + ", T " + F2(sp.Length) + " s, x" + F2(m) + " → x" + F2(s.MinMultiplier * run), "sprint-you");
                }
            }
            else
            {
                if (floor) _sprintStats.AiOffFloor++;
                if (!_firstAiSprintLogged)
                {
                    _firstAiSprintLogged = true;
                    TraxLog.Info("sprint", "first AI order sprint this battle: " + Name(st) + " at " + Sec(now) + " s on a new " + SprintMath.OrderName(sp.Order)
                        + " order - stored " + F2(sp.StoredAtStart) + " s of " + F2(cap) + " s (" + F0(100 * sp.StoredAtStart / Math.Max(1e-9, cap)) + "%)"
                        + (floor ? " (the " + F1(s.MinSeconds) + " s floor)" : string.Empty) + ", " + SprintMath.OrderName(sp.Order) + " keeps "
                        + F0(100 * SprintMath.AiKeep(in s, sp.Order)) + "% → asked T " + F2(sp.Length)
                        + " s (stored - keep x " + F2(cap) + "; left after it " + F0(100 * (sp.StoredAtStart - sp.Length) / Math.Max(1e-9, cap))
                        + "%): x" + F2(s.MaxMultiplier) + " of the fresh run (run x" + F2(run) + " → x" + F2(m) + ") until "
                        + F2(sp.Length * s.FullSpeedShare) + " s, then to x" + F2(s.MinMultiplier) + " of his current run (x" + F2(s.MinMultiplier * run) + "); " + reachText
                        + "; ends early on an enemy within " + F1(SprintMath.MeleeRangeMetres) + " m or a new order");
                }
                else if (TraxLog.VerboseWants("sprint-ai"))
                {
                    TraxLog.Verbose("sprint", Name(st) + " sprints at " + Sec(now) + " s (" + SprintMath.OrderName(sp.Order) + ", T " + F2(sp.Length) + " s)", "sprint-ai");
                }
            }
        }

        /// <summary>His applied sprint run factor (0 = none); a change marks him for a recompute (the start, every 0.05 step, the end).</summary>
        private void SetSprintRun(TrackedAgent st, float m)
        {
            if (m == st.SprintRun) return;
            st.SprintRun = m;
            if (st.Removed) return;
            st.SpeedDirty = true;
            _sprintStats.Recomputes++;
        }

        /// <summary>Ends his running sprint (every path): the stats, the multiplier back to 1 (recomputed within the budget).</summary>
        private void EndSprint(TrackedAgent st, SprintEnd end, double now)
        {
            var sp = st.Sprint;
            if (!sp.Active) return;
            double ran = sp.Stop();
            if (_sprintingNow > 0) _sprintingNow--;
            _sprintStats.AddEnd(sp.ByKey, sp.Order, sp.Length, ran, end);
            SetSprintRun(st, 0f);
            if (sp.ByKey)
            {
                TraxLog.Limited("sprint", "YOU: sprint ended at " + Sec(now) + " s (" + SprintMath.EndName(end) + ") after " + F2(ran) + " s of moving - "
                    + F2(Math.Max(0, sp.Stored)) + " s left stored", "sprint-you");
            }
            else if (TraxLog.VerboseWants("sprint-ai"))
            {
                TraxLog.Verbose("sprint", Name(st) + " sprint ended at " + Sec(now) + " s (" + SprintMath.EndName(end) + ") after " + F2(ran) + " s", "sprint-ai");
            }
        }

        private void SafeEndSprint(TrackedAgent st, double now)
        {
            try
            {
                EndSprint(st, SprintEnd.Error, now);
            }
            catch
            {
                st.SprintRun = 0f;
                st.SpeedDirty = true;
            }
        }

        /// <summary>Every running sprint ends (switched off; <paramref name="aiOnly"/> = SprintAiEnabled off - yours runs on).</summary>
        private void EndAllSprints(SprintEnd end, double now, bool aiOnly)
        {
            int ended = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (!st.Sprint.Active || (aiOnly && st.Sprint.ByKey)) continue;
                try
                {
                    EndSprint(st, end, now);
                    ended++;
                }
                catch (Exception e)
                {
                    Failed("sprint.end-all", e);
                    SafeEndSprint(st, now);
                }
            }
            if (ended > 0)
                TraxLog.Info("sprint", (aiOnly ? "SprintAiEnabled" : SprintRules.From(TraxSettings.Shared).OffBecause ?? "the sprint") + " switched off at " + Sec(now)
                    + " s: " + ended + " sprints ended, their speeds back over the next ticks (at most " + MaxRecomputesPerTick + " recomputes a tick)");
            if (!aiOnly) _sprintingNow = 0;
        }

        /// <summary>He left the field: his sprint counted, no engine call.</summary>
        private void SprintLeftField(TrackedAgent st)
        {
            try
            {
                if (st.Sprint.Active) EndSprint(st, SprintEnd.LeftField, SafeNow());
            }
            catch (Exception e)
            {
                Failed("sprint.left", e);
            }
        }

        /// <summary>Step 19's hideout boss fight: a running sprint ends, the store is full again.</summary>
        private void SprintFreshStart(TrackedAgent st, double now)
        {
            try
            {
                if (st.Sprint.Active) EndSprint(st, SprintEnd.FreshStart, now);
                st.Sprint.FillUp();
                _sprintStats.FreshStarts++;
            }
            catch (Exception e)
            {
                Failed("sprint.fresh", e);
            }
        }

        /// <summary>Mission end: every running sprint counted (the properties go with the mission).</summary>
        private void CloseSprints()
        {
            if (_sprintingNow <= 0) return;
            double now = SafeNow();
            for (int i = 0; i < _count; i++)
                if (_dense[i].Sprint.Active) EndSprint(_dense[i], SprintEnd.MissionEnd, now);
            _sprintingNow = 0;
        }

        private void WriteSprintSummary()
        {
            var settings = TraxSettings.Shared;
            var lines = new List<string>(4);
            _sprintStats.AddSummaryLines(lines, SprintRules.From(settings), settings.SprintKey);
            foreach (var line in lines) TraxLog.Info("summary", line);
        }

        // ------------------------------------------------------------------ the HUD

        /// <summary>Your sprint store for the sprint bar (false: no record for him, or the sprint is off). Managed reads only.</summary>
        internal static bool TryGetPlayerSprint(TaleWorlds.MountAndBlade.Agent agent, out SprintReading reading)
        {
            reading = default;
            var logic = _current;
            if (logic == null || agent == null) return false;
            var settings = TraxSettings.Shared;
            var s = SprintRules.From(settings);
            if (!s.Enabled) return false;
            var st = logic.Get(agent);
            if (st == null) return false;
            var r = AthleticsRules.From(settings);
            double cap = SprintMath.Capacity(in s, in r, st);
            var sp = st.Sprint;
            reading = new SprintReading(sp.StoredUpTo(cap), cap, s.MinStartShare, sp.Active, st.SprintRun > 0f ? st.SprintRun : st.RunSpeedMultiplier);
            return true;
        }
    }
}
