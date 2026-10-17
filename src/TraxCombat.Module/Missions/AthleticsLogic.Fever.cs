using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 43 - AGGRESSION AND THE FEVER (Anton, 2026-10-17: "a man at work doesn't hold a pace until he drops; the more tired he is,
    /// the less he swings" … "when the general turns to them and says NOW!, everybody surges … the surge fades"; AI_NOTES "Step 43",
    /// DESIGN §2 "Aggression and the fever").
    ///
    /// AGGRESSION: every AI man's <see cref="TrackedAgent.DecisionFactor"/> = his share in 0.05 steps - AiAggressionFreshPercent at a
    /// full bar, a line to AiAggressionExhaustedPercent at an empty one (the bar = points ÷ his full pool); <see cref="RetargetSpeed"/>
    /// carries it, so every charge and refill step moves it; the stat decorator multiplies the game's two melee attack-decision chances
    /// by it on every recompute (AI on foot only - the decorator checks the player and the horse).
    /// FEVER: a NEW charge / advance order on his formation (step 33's order poll - <see cref="FeverOrderFor"/>) arms his
    /// <see cref="FeverStore"/> with his CURRENT points ÷ FeverPointsPerSecond seconds; every man is looked at about every
    /// <see cref="FeverMath.PollSeconds"/> (staggered like the brace): an enemy within FeverContactMetres (one native proximity read)
    /// starts the burn, the burn reads the shield in his hand live (step 21's weapon read), his share is lifted to FeverAggressionPercent
    /// and fades back to his tired share (a 0.05 step marks him for a recompute under the tick's budget). A hold / move / retreat order
    /// ends it. The fever sprint: every FeverSprintCheckSeconds a roll at FeverSprintChancePercent × fever left. No allocation per tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly FeverStats _feverStats = new FeverStats();
        private string _seenFeverText = string.Empty;
        private double _feverLastTick = double.NaN;
        private double _feverCarry;
        private int _feverCursor;
        private int _feverRunning;
        private bool _firstArmedLogged;
        private bool _firstBurnLogged;
        private bool _firstFadedLogged;
        private bool _firstFeverSprintLogged;
        private bool _firstValuesLogged;

        /// <summary>This mission's fever numbers (the offline smoke reads them).</summary>
        internal FeverStats FeverStats => _feverStats;

        /// <summary>The fever sprint's dice (the smoke swaps in a seeded one).</summary>
        internal IRandomSource FeverDice { get; set; } = ThreadSafeRandom.Shared;

        /// <summary>Fevers running now (armed or burning).</summary>
        internal int FeversRunning => _feverRunning;

        private static string FeverText(TraxSettings s) => FeverRules.From(s).Describe();

        private void StartFever()
        {
            _seenFeverText = FeverText(TraxSettings.Shared);
            TraxLog.Info("fever", "mission start: " + _seenFeverText + " - read live; every man looked at about every " + F2(FeverMath.PollSeconds)
                + " s; the [summary] \"fever\" lines count fevers by order, contact, burns, the fade, fever sprints and AI attacks per man-minute in contact by bar band, without and in a fever and in no formation");
        }

        /// <summary>An aggression / fever setting changed (from ApplySettingsChange - its RetargetSpeed loop re-targets every man's factor).</summary>
        private void NoteFeverSettings(TraxSettings s)
        {
            string now = FeverText(s);
            if (now == _seenFeverText) return;
            _seenFeverText = now;
            TraxLog.Info("fever", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - every man's decision values follow at his next recompute");
        }

        /// <summary>His aggression share (0.05 steps) from the live rules, his bar and his fever; a change marks him for a recompute (true).</summary>
        private bool RetargetDecision(TrackedAgent st)
        {
            var fr = FeverRules.From(TraxSettings.Shared);
            var fv = st.Fever;
            float want = FeverMath.DecisionFactor(in fr, fv.Phase, fv.Left, fv.Full, st.Fraction, IsPlayer(st));
            if (want == st.DecisionFactor) return false;
            st.DecisionFactor = want;
            _feverStats.DecisionRecomputes++;
            if (fv.Running) fv.Recomputes++;
            if (!st.Removed) st.SpeedDirty = true;
            return true;
        }

        // ------------------------------------------------------------------ the order

        /// <summary>A new order on his formation (the poll): a charge / advance arms (or re-arms) his fever; any other ends it.</summary>
        internal void FeverOrderFor(TrackedAgent st, BraceOrderKind kind, bool byPlayer, double now, in FeverRules fr, in AthleticsRules r)
        {
            if (st.Removed) return;
            var body = SprintBody;
            if (!body.IsActive(st)) return;
            var fv = st.Fever;
            var group = FeverMath.Group(kind);
            if (group == SprintOrder.None)
            {
                if (fv.Running) EndFever(st, FeverEnd.OrderEnded, now);
                return;
            }
            if (IsPlayer(st) || !body.IsAiControlled(st))
            {
                _feverStats.NotArmedPlayer++;
                return;
            }
            if (body.IsMounted(st))
            {
                _feverStats.NotArmedMounted++;
                return;
            }
            bool wasRunning = fv.Running;
            double points = AthleticsMath.Points(in r, st);
            var res = fv.Arm(in fr, group, points, now, byPlayer);
            switch (res)
            {
                case FeverArm.Armed:
                    _feverRunning++;
                    _feverStats.AddArmed(group, byPlayer);
                    if (!_firstArmedLogged)
                    {
                        _firstArmedLogged = true;
                        TraxLog.Info("fever", "first fever armed this battle: " + Name(st) + " at " + Sec(now) + " s on a new " + SprintMath.OrderName(group) + " order ("
                            + (byPlayer ? "yours" : "the AI general's") + ", " + kind + ") - " + F0(points) + " Athletics points / " + F1(fr.PointsPerSecond) + " = "
                            + F2(fv.Full) + " s of fever; his attack decisions x" + F2(FeverMath.DecisionFactor(in fr, fv.Phase, fv.Left, fv.Full, st.Fraction, false))
                            + " the game's (his tired share at a " + F0(100 * st.Fraction) + "% bar: x" + F2(FeverMath.TiredShare(in fr, st.Fraction)) + ") until an enemy comes within "
                            + F1(fr.ContactMetres) + " m, then the clock runs");
                    }
                    else if (TraxLog.VerboseWants("fever-arm"))
                        TraxLog.Verbose("fever", Name(st) + " armed at " + Sec(now) + " s (" + SprintMath.OrderName(group) + ", " + F2(fv.Full) + " s)", "fever-arm");
                    break;
                case FeverArm.Rearmed:
                    _feverStats.Rearmed++; // not a new fever: the summary's "armed" counts fevers
                    break;
                case FeverArm.KeptLarger:
                    _feverStats.KeptLarger++;
                    break;
                case FeverArm.Repeat:
                    _feverStats.Repeats++;
                    break;
                case FeverArm.NoStore:
                    _feverStats.NotArmedNoStore++;
                    break;
            }
            if (!wasRunning && fv.Running) RetargetDecision(st);
            else if (res == FeverArm.Rearmed) RetargetDecision(st);
        }

        // ------------------------------------------------------------------ the look

        /// <summary>Every tick (from TickAthletics): fever off → every fever ends; the master switch or Athletics off → nothing to look at; else a
        /// staggered slice so each man is looked at about every <see cref="FeverMath.PollSeconds"/>.</summary>
        internal void TickFever(double now)
        {
            var fr = FeverRules.From(TraxSettings.Shared);
            if (!fr.Enabled && _feverRunning > 0) EndAllFevers(FeverEnd.SwitchedOff, now, fr.OffBecause);
            if (!fr.AggressionOn)
            {
                _feverLastTick = now;
                return;
            }
            double dt = double.IsNaN(_feverLastTick) || now < _feverLastTick ? FeverMath.PollSeconds : now - _feverLastTick;
            _feverLastTick = now;
            if (dt > FeverMath.PollSeconds) dt = FeverMath.PollSeconds;
            _feverCarry += _count * dt / FeverMath.PollSeconds;
            int n = (int)(_feverCarry + 1e-9);
            _feverCarry -= n;
            if (_feverCarry < 0) _feverCarry = 0;
            if (n > _count) n = _count;
            if (n == 0) return;
            var ar = AthleticsRules.From(TraxSettings.Shared);
            long start = Stopwatch.GetTimestamp();
            for (int k = 0; k < n; k++)
            {
                if (_feverCursor >= _count) _feverCursor = 0;
                var st = _dense[_feverCursor++];
                try
                {
                    PollFever(st, now, in fr, in ar);
                }
                catch (Exception e)
                {
                    Failed("fever.poll", e);
                    if (st.Fever.Running) SafeEndFever(st, now);
                }
            }
            _feverStats.Polls += n;
            _feverStats.PollCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        private void PollFever(TrackedAgent st, double now, in FeverRules fr, in AthleticsRules ar)
        {
            if (st.Removed) return;
            var body = SprintBody;
            if (!body.IsActive(st)) return;
            var fv = st.Fever;
            double gap = double.IsNaN(st.FeverLastLook) || now < st.FeverLastLook ? 0 : now - st.FeverLastLook;
            if (gap > 4 * FeverMath.PollSeconds) gap = 0; // a longer gap = the feature was off
            if (double.IsNaN(st.FeverLastLook)) _feverStats.MenLooked++;
            st.FeverLastLook = now;
            if (IsPlayer(st) || !body.IsAiControlled(st))
            {
                if (fv.Running) EndFever(st, FeverEnd.PlayerControl, now);
                st.FeverContact = false;
                RetargetDecision(st);
                return;
            }
            if (body.IsMounted(st))
            {
                if (fv.Running) EndFever(st, FeverEnd.Mounted, now);
                st.FeverContact = false;
                RetargetDecision(st);
                return;
            }
            // the summary's time in contact: since the last look, by the state then
            if (gap > 0) _feverStats.AiManSeconds += gap;
            if (st.FeverContact) _feverStats.AddContactTime(st.NoFormation ? FeverStats.NoFormation : fv.Running ? FeverStats.InFever : FeverStats.Calm, FeverStats.BarBand(st.Fraction), gap);
            st.NoFormation = body.FormationSlot(st) < 0;
            bool contact = body.EnemyWithin(st, (float)fr.ContactMetres);
            st.FeverContact = contact;
            if (fv.Phase == FeverPhase.Armed && contact && fv.StartBurn(now))
            {
                _feverStats.AddBurnStart(now - fv.ArmedAt);
                if (!_firstBurnLogged)
                {
                    _firstBurnLogged = true;
                    TraxLog.Info("fever", "first fever burning this battle: " + Name(st) + " met an enemy within " + F1(fr.ContactMetres) + " m at " + Sec(now) + " s, "
                        + F1(now - fv.ArmedAt) + " s after the " + SprintMath.OrderName(fv.Order) + " order - " + F2(fv.Left) + " s of fever burn from now (x"
                        + F2(FeverMath.BurnRate(in fr, true)) + " with a shield in hand), his attack decisions fading from x"
                        + F2(FeverMath.DecisionFactor(in fr, FeverPhase.Armed, 1, 1, st.Fraction, false)) + " to his tired share (x" + F2(FeverMath.TiredShare(in fr, st.Fraction))
                        + " now, sinking as he fights) in 0.05 steps");
                }
                gap = 0; // the clock starts now
            }
            if (fv.Phase == FeverPhase.Burning)
            {
                bool shield = false;
                try
                {
                    WeaponFacts.Read(st, true, false, out shield, out _);
                }
                catch (Exception e)
                {
                    Failed("fever.shield", e); // counted without
                }
                if (fv.Burn(in fr, gap, shield))
                {
                    EndFever(st, FeverEnd.Faded, now);
                    return;
                }
            }
            if (fv.Running && fr.SprintOn && fv.SprintDue(in fr, now)) FeverSprintCheck(st, now, in fr, in ar);
            RetargetDecision(st);
        }

        // ------------------------------------------------------------------ the fever sprint

        /// <summary>One roll (every FeverSprintCheckSeconds): the cheap gates first, the two proximity reads, then the dice at
        /// FeverSprintChancePercent × fever left; a start is step 36's AI sprint (everything above his order's reserve).</summary>
        private void FeverSprintCheck(TrackedAgent st, double now, in FeverRules fr, in AthleticsRules ar)
        {
            _feverStats.SprintChecks++;
            var fv = st.Fever;
            var s = SprintRules.From(TraxSettings.Shared);
            if (!s.Enabled || !s.AiOn)
            {
                _feverStats.AddRefusal(FeverSprintRefusal.SprintOff);
                return;
            }
            var sp = st.Sprint;
            if (sp.Active)
            {
                _feverStats.AddRefusal(FeverSprintRefusal.Sprinting);
                return;
            }
            double cap = SprintMath.Capacity(in s, in ar, st);
            if (!(cap > SprintMath.EmptySeconds))
            {
                _feverStats.AddRefusal(FeverSprintRefusal.NoStore);
                return;
            }
            sp.Clamp(cap);
            if (!(SprintMath.AiAmount(in s, fv.Order, sp.StoredUpTo(cap), cap) > 0))
            {
                _feverStats.AddRefusal(FeverSprintRefusal.ReserveKept);
                return;
            }
            var body = SprintBody;
            if (fr.SprintMinMetres > 0 && body.EnemyWithin(st, (float)fr.SprintMinMetres))
            {
                _feverStats.AddRefusal(FeverSprintRefusal.InMelee);
                return;
            }
            if (!body.EnemyWithin(st, (float)fr.SprintMaxMetres))
            {
                _feverStats.AddRefusal(FeverSprintRefusal.NoEnemyInRange);
                return;
            }
            double chance = FeverMath.SprintChance(in fr, fv.Phase, fv.Left, fv.Full);
            if (!FeverMath.SprintRoll(chance, FeverDice.NextDouble()))
            {
                _feverStats.AddRefusal(FeverSprintRefusal.MissedRoll);
                return;
            }
            var res = sp.TryStartAi(in s, now, cap, fv.Order);
            if (res != SprintStart.Started)
            {
                _feverStats.AddRefusal(res == SprintStart.ReserveKept ? FeverSprintRefusal.ReserveKept : res == SprintStart.Running ? FeverSprintRefusal.Sprinting
                    : res == SprintStart.Off ? FeverSprintRefusal.SprintOff : FeverSprintRefusal.NoStore);
                return;
            }
            double share = fv.Phase == FeverPhase.Armed ? 1 : fv.Share;
            fv.Sprints++;
            _feverStats.AddSprint(share);
            _sprintStats.AddAiStart(fv.Order);
            BeginSprint(st, now, in s, in ar, cap);
            if (!_firstFeverSprintLogged)
            {
                _firstFeverSprintLogged = true;
                TraxLog.Info("fever", "first fever sprint this battle: " + Name(st) + " at " + Sec(now) + " s - an enemy " + F1(fr.SprintMinMetres) + "-" + F1(fr.SprintMaxMetres)
                    + " m away, fever " + F0(100 * share) + "% left → chance " + F0(100 * chance) + "% (FeverSprintChancePercent " + fr.SprintChancePercent
                    + " x the fever left); sprints " + F2(sp.Length) + " s - what he held above the " + SprintMath.OrderName(fv.Order) + " reserve ("
                    + F0(100 * SprintMath.AiKeep(in s, fv.Order)) + "%)");
            }
            else if (TraxLog.VerboseWants("fever-sprint"))
                TraxLog.Verbose("fever", Name(st) + " fever sprint at " + Sec(now) + " s (" + F0(100 * share) + "% left, " + F2(sp.Length) + " s)", "fever-sprint");
        }

        // ------------------------------------------------------------------ the end

        /// <summary>His fever ends (every path): counted, the factor back to his tired share (a recompute within the budget).</summary>
        private void EndFever(TrackedAgent st, FeverEnd end, double now)
        {
            var fv = st.Fever;
            if (!fv.Running) return;
            bool burned = fv.Phase == FeverPhase.Burning;
            double full = fv.FullAtBurn;
            int recomputes = fv.Recomputes;
            fv.Stop();
            if (_feverRunning > 0) _feverRunning--;
            if (end != FeverEnd.LeftField && end != FeverEnd.MissionEnd)
            {
                if (RetargetDecision(st)) recomputes++; // back to his tired share
            }
            fv.Recomputes = recomputes;
            _feverStats.AddEnd(end, burned, fv.BurnShieldSeconds, fv.BurnPlainSeconds, full, fv.ShieldSwitched, recomputes);
            if (end == FeverEnd.Faded && !_firstFadedLogged)
            {
                _firstFadedLogged = true;
                var fr = FeverRules.From(TraxSettings.Shared);
                TraxLog.Info("fever", "first fever faded this battle: " + Name(st) + " at " + Sec(now) + " s - " + F2(full) + " s of store burned in "
                    + F2(fv.BurnShieldSeconds + fv.BurnPlainSeconds) + " s (" + F2(fv.BurnShieldSeconds) + " s with a shield in hand, " + F2(fv.BurnPlainSeconds)
                    + " s without), " + fv.Recomputes + " decision recomputes, " + fv.Sprints + " fever sprints; back to his tired share (x" + F2(st.DecisionFactor) + " the game's at a " + F0(100 * st.Fraction) + "% bar)");
            }
            else if (TraxLog.VerboseWants("fever-end"))
                TraxLog.Verbose("fever", Name(st) + " fever ended at " + Sec(now) + " s (" + FeverMath.EndName(end) + ")", "fever-end");
        }

        private void SafeEndFever(TrackedAgent st, double now)
        {
            try
            {
                EndFever(st, FeverEnd.Error, now);
            }
            catch
            {
                st.Fever.Stop();
                st.DecisionFactor = 1f;
                st.SpeedDirty = true;
            }
        }

        private void EndAllFevers(FeverEnd end, double now, string? because)
        {
            int ended = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (!st.Fever.Running) continue;
                try
                {
                    EndFever(st, end, now);
                    ended++;
                }
                catch (Exception e)
                {
                    Failed("fever.end-all", e);
                    SafeEndFever(st, now);
                }
            }
            _feverRunning = 0;
            if (ended > 0)
                TraxLog.Info("fever", (because ?? "the fever") + " switched off at " + Sec(now) + " s: " + ended + " fevers ended, their men back to their tired share over the next ticks");
        }

        private void FeverLeftField(TrackedAgent st)
        {
            try
            {
                if (st.Fever.Running) EndFever(st, FeverEnd.LeftField, SafeNow());
            }
            catch (Exception e)
            {
                Failed("fever.left", e);
            }
        }

        private void CloseFevers()
        {
            if (_feverRunning <= 0) return;
            double now = SafeNow();
            for (int i = 0; i < _count; i++)
                if (_dense[i].Fever.Running) EndFever(_dense[i], FeverEnd.MissionEnd, now);
            _feverRunning = 0;
        }

        // ------------------------------------------------------------------ the measurements

        /// <summary>A melee release (StartRelease): an AI man on foot in contact at his last look - counted by his group (no fever / in a
        /// fever / no formation) and his bar band.</summary>
        internal void NoteFeverAttack(TrackedAgent st, bool mounted)
        {
            if (mounted || !st.FeverContact || IsPlayer(st)) return;
            _feverStats.AddAttack(st.NoFormation ? FeverStats.NoFormation : st.Fever.Running ? FeverStats.InFever : FeverStats.Calm, FeverStats.BarBand(st.Fraction));
        }

        /// <summary>The stat decorator wrote the two values (any thread the game recomputes on; managed only).</summary>
        internal static void NoteDecisionValues(TaleWorlds.MountAndBlade.Agent agent, float gameAttack, float attack, float gameRiposte, float riposte)
        {
            var logic = _current;
            if (logic == null) return;
            var st = logic.Get(agent);
            if (st == null) return;
            var fv = st.Fever;
            var fr = FeverRules.From(TraxSettings.Shared);
            int state = fv.Running && fr.Enabled ? FeverStats.BarBands : FeverStats.BarBand(st.Fraction);
            logic._feverStats.AddValues(state, gameAttack, attack, gameRiposte, riposte);
            if (!logic._firstValuesLogged)
            {
                logic._firstValuesLogged = true;
                TraxLog.Info("fever", "first decision values written this mission: " + Name(st) + " (" + (state == FeverStats.BarBands ? "in a fever" : "bar " + F0(100 * st.Fraction) + "%")
                    + ", x" + F2(st.DecisionFactor) + ") - attack chance " + F3(gameAttack) + " → " + F3(attack) + ", riposte chance " + F3(gameRiposte) + " → " + F3(riposte));
            }
        }

        internal void WriteFeverSummary()
        {
            var lines = new List<string>(9);
            _feverStats.AddSummaryLines(lines, FeverRules.From(TraxSettings.Shared));
            foreach (var line in lines) TraxLog.Info("summary", line);
        }
    }
}
