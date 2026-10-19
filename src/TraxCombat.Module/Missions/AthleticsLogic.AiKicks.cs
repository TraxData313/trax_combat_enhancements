using System;
using System.Diagnostics;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 65 - THE AI KICKS AND BASHES (Anton 2026-10-19: "make the AI do a few shield bashes and kicks now and then - the game has it but the
    /// NPCs never use them"; AI_NOTES "Step 65", DESIGN decision 57).
    ///
    /// THE LEVER (the decompile + RBM's AiKickBash, its public source): the engine performs a kick from the agent's EVENT flags -
    /// <c>Agent.EventControlFlag.Kick</c> (0x8000), the bit vanilla's own <c>MissionMainAgentController</c> sets for your kick key; the input
    /// is CONTEXTUAL - the kick key while the guard is up is a BASH (shield or weapon), else a kick. The native AI's kick tendency
    /// (<c>AgentDrivenProperties.AiKick</c>, written by <c>SetAiRelatedProperties</c>) does nothing in game (RBM's finding; Anton: "the NPCs
    /// never use them"). So the kick is INJECTED through step 16's input hook (<see cref="AiInputComponent"/> - <c>OnAIInputSet</c>, the AI's
    /// own input before the engine uses it): a bash holds DefendUp for 0.4 s, then the kick bit for 0.15 s (+ DefendUp), then COMMIT - his
    /// own attack / guard / event input held off and his movement stopped until the action has played out (or 1.5 s) - the native AI's own
    /// input would cancel it before the hit frame (RBM's phases, <see cref="AiKickMath"/>'s plumbing). Nothing else is touched.
    ///
    /// WHO / HOW OFTEN: every AI man is looked at about every 0.25 s (staggered, the fever's pattern); the cheap gates on our own records
    /// first (the cooldown, his bar, the Athletics for the cost, a stored swing, not staggered / down, not busy in his own attack / kick /
    /// step back), then ONE die against the most he could get (a turtle target) - only a man whose die is under it reads the engine (his
    /// horse, a rout, his hands, his target, its distance and angle) and the target's turtling (our own records: his blocks of the last
    /// seconds, faded, + his guard up now) gives the exact chance for the same die. Never you; never a rider; a ranged weapon, a rout, an
    /// enemy on a horse / on the ground / out of reach / not in front = none.
    ///
    /// THE COST: the kick / bash is SEEN by step 18's action poll like yours and charged there - <c>CostPerKickOrBash</c> and a stored swing
    /// (step 32) - nothing of our own.
    ///
    /// Measured: the [summary] "ai kicks" lines - asked → PLAYED (the action seen) → landed / into a shield, by kind and reason (turtle, fever),
    /// the ends, why not, the Athletics spent, the look's cost. Master switch FIRST (<see cref="AiKickRules.Enabled"/>): off = every attempt's
    /// wish cleared at once. Main thread; a fixed pool of attempts, no allocation per tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly AiKickStats _aiKickStats = new AiKickStats();
        private readonly TrackedAgent?[] _aiKickers = new TrackedAgent?[AiKickMath.MaxAttempts];
        private int _aiKickCount;
        private int _aiKickCursor;
        private double _aiKickCarry;
        private double _aiKickLastTick = double.NaN;
        private string _seenAiKickText = string.Empty;
        private readonly bool[] _aiKickFirst = new bool[6];

        /// <summary>This mission's AI kick numbers (the smoke reads them).</summary>
        internal AiKickStats AiKickStats => _aiKickStats;

        /// <summary>Attempts running now (the smoke).</summary>
        internal int AiKicksRunning => _aiKickCount;

        /// <summary>The engine reads (the smoke swaps in a stand-in).</summary>
        internal IAiKickBody AiKickBody { get; set; } = AiKickBodyDefault.Current;

        /// <summary>The look's dice (the smoke seeds its own).</summary>
        internal IRandomSource AiKickDice { get; set; } = ThreadSafeRandom.Shared;

        private void StartAiKicks()
        {
            _seenAiKickText = AiKickRules.From(TraxSettings.Shared).Describe();
            TraxLog.Info("impact", "mission start, the AI kicks and bashes (step 65): " + _seenAiKickText + " - read live; looked at every ~" + F2(AiKickMath.LookSeconds)
                + " s a man; the [summary] \"ai kicks\" lines count asked → played → landed (PLAYTEST D31)");
        }

        /// <summary>An AI kick setting changed (from ApplySettingsChange): one line (switching off ends every attempt in the next tick).</summary>
        private void NoteAiKickSettings(TraxSettings s)
        {
            string now = AiKickRules.From(s).Describe();
            if (now == _seenAiKickText) return;
            _seenAiKickText = now;
            TraxLog.Info("impact", "AI kick settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now);
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick (from TickAthletics, own try there): off (the master switch first) = every attempt ended at once; the running
        /// attempts advanced; a staggered slice of looks.</summary>
        internal void TickAiKicks(double now)
        {
            var r = AiKickRules.From(TraxSettings.Shared);
            if (!r.Enabled)
            {
                if (_aiKickCount > 0) EndAllAiKicks(AiKickEnd.SwitchedOff, now, r.OffBecause);
                _aiKickLastTick = now;
                return;
            }
            long start = Stopwatch.GetTimestamp();
            if (_aiKickCount > 0) AdvanceAiKicks(now);
            double dt = double.IsNaN(_aiKickLastTick) || now < _aiKickLastTick ? AiKickMath.LookSeconds : now - _aiKickLastTick;
            _aiKickLastTick = now;
            if (dt > AiKickMath.LookSeconds) dt = AiKickMath.LookSeconds;
            _aiKickCarry += _count * dt / AiKickMath.LookSeconds;
            int n = (int)(_aiKickCarry + 1e-9);
            _aiKickCarry -= n;
            if (_aiKickCarry < 0) _aiKickCarry = 0;
            if (n > _count) n = _count;
            if (n > 0)
            {
                var ar = Rules;
                for (int k = 0; k < n; k++)
                {
                    if (_aiKickCursor >= _count) _aiKickCursor = 0;
                    var st = _dense[_aiKickCursor++];
                    try
                    {
                        LookAiKick(st, now, in r, in ar);
                    }
                    catch (Exception e)
                    {
                        Failed("aikick.look", e);
                    }
                }
            }
            _aiKickStats.CostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>One man's look: the gates in order (cheap first), ONE die, the engine's reads only under the die's best case.</summary>
        internal void LookAiKick(TrackedAgent st, double now, in AiKickRules r, in AthleticsRules ar)
        {
            if (st.Removed || st.AiKickPhase != AiKickPhase.None) return;
            var body = AiKickBody;
            if (IsPlayer(st) || !body.IsActive(st) || !body.IsAiControlled(st)) return;
            double gap = AiKickMath.LookGap(now, st.AiKickLastLook);
            st.AiKickLastLook = now;
            _aiKickStats.Looks++;
            if (st.FeverContact) _aiKickStats.ContactManSeconds += gap;
            if (!double.IsNaN(st.AiKickWaitUntil) && now > st.AiKickWaitUntil)
            {
                _aiKickStats.WaitsExpired++; // step 66: the enemy never came in reach in time
                ClearAiKickWait(st);
            }
            if (now < st.AiKickNext)
            {
                Skip(AiKickSkip.Cooldown);
                return;
            }
            double barF = AiKickMath.BarFactor(in r, st.Fraction);
            if (!(barF > 0))
            {
                Skip(AiKickSkip.Tired);
                return;
            }
            if (AthleticsMath.Points(in ar, st) < AthleticsMath.KickOrBashCostPoints(in ar, st))
            {
                Skip(AiKickSkip.NoAthletics);
                return;
            }
            var inp = st.Input;
            if (inp != null && inp.HoldKicks && inp.HoldAttacks)
            {
                Skip(AiKickSkip.NoSwing);
                return;
            }
            if (st.Impact.Reeling(now) || st.Impact.Down(now) || ImpactActions.IsStrike(st.PrevAction) || ImpactActions.IsFall(st.PrevLowerAction)
                || ImpactActions.IsStrike(st.PrevLowerAction))
            {
                Skip(AiKickSkip.Staggered);
                return;
            }
            if (AiKickMath.IsBusyAction(st.PrevAction) || st.KickBash.Running || (inp != null && (inp.Backpedal || inp.Regroup || inp.WallPull)))
            {
                Skip(AiKickSkip.Busy);
                return;
            }
            // step 66: a decision already taken waits for reach - no die, the engine read every look until he is in reach or it runs out
            bool waiting = !double.IsNaN(st.AiKickWaitUntil);
            bool fever = st.Fever.Running;
            double u = 0, bashChance = 0;
            if (!waiting)
            {
                // ONE die against his best case: a kick against a full turtle (a man without a shield) or the bash his swings earned (a shieldman)
                double kickBest = r.PerMinute > 0 ? AiKickMath.LookChance(AiKickMath.PerMinute(in r, st.AthleticsSkill, st.Fraction, fever, 1.0), gap) : 0;
                bashChance = AiKickMath.LookChance(AiKickMath.BashPerMinute(in r, st.BashCredit, st.Fraction), gap);
                u = AiKickDice.NextDouble();
                if (u >= Math.Max(kickBest, bashChance))
                {
                    Skip(AiKickSkip.Dice);
                    return;
                }
            }
            // under the best case (or waiting): the engine's reads
            _aiKickStats.NativeReads++;
            body.Hands(st, out bool mounted, out bool routing, out bool ranged, out bool shield);
            if (mounted || routing || ranged)
            {
                DropAiKickWait(st);
                Skip(mounted ? AiKickSkip.Mounted : routing ? AiKickSkip.Routing : AiKickSkip.Ranged);
                return;
            }
            var targetAgent = body.Target(st, out float distance, out float off);
            var target = targetAgent != null ? Get(targetAgent) : null;
            if (target == null || target.Removed || target.Impact.Down(now) || ImpactActions.IsFall(target.PrevLowerAction))
            {
                DropAiKickWait(st);
                Skip(AiKickSkip.NoTarget);
                return;
            }
            bool guardUp = ImpactActions.IsDefend(target.PrevAction);
            double turtle = AiKickMath.TurtleShare(in r, AiKickMath.Faded(target.TurtleBlocks, target.TurtleBlocksAt, now, r.TurtleSeconds), guardUp);
            double chance;
            if (!waiting)
            {
                // the exact chance for the same die: a shieldman's earned bash, else the kick by the target's turtling
                chance = shield ? bashChance : AiKickMath.LookChance(AiKickMath.PerMinute(in r, st.AthleticsSkill, st.Fraction, fever, turtle), gap);
                if (u >= chance)
                {
                    Skip(AiKickSkip.Dice);
                    return;
                }
                if (shield) st.BashCredit = Math.Max(0, st.BashCredit - 1); // the earned bash is spent on the decision
            }
            else chance = st.AiKickWaitChance;
            if (!(distance <= r.Reach) || !(off <= r.Facing))
            {
                if (waiting)
                {
                    if (!(distance <= r.WaitMetres))
                    {
                        DropAiKickWait(st);
                        Skip(AiKickSkip.TooFar);
                    }
                    else Skip(AiKickSkip.Waiting);
                    return;
                }
                if (AiKickMath.Waits(in r, distance))
                {
                    st.AiKickWaitUntil = now + r.WaitSeconds;
                    st.AiKickWaitSince = now;
                    st.AiKickWaitChance = chance;
                    _aiKickStats.WaitsStarted++;
                    Skip(AiKickSkip.Waiting);
                    return;
                }
                Skip(!(distance <= r.Reach) ? AiKickSkip.TooFar : AiKickSkip.NotFacing);
                return;
            }
            bool raisedFacing = false;
            if (shield && r.KicksARaisedShield && target.PrevAction == ImpactActions.DefendShield) raisedFacing = body.FacingTo(target, st) <= AiKickMath.RaisedShieldDegrees;
            var kind = AiKickMath.KindFor(in r, shield, raisedFacing);
            if (_aiKickCount >= _aiKickers.Length)
            {
                Skip(AiKickSkip.PoolFull);
                return;
            }
            if (waiting)
            {
                _aiKickStats.WaitsDone++;
                _aiKickStats.WaitSeconds += now - st.AiKickWaitSince;
                ClearAiKickWait(st);
            }
            BeginAiKick(st, target, kind, AiKickStats.Reason(turtle >= 0.5, fever), now, AiKickMath.CooldownFor(in r, kind), turtle, chance);
        }

        private void Skip(AiKickSkip why) => _aiKickStats.Skips[(int)why]++;

        /// <summary>Step 66: a waiting decision dropped (the enemy gone or too far, mounted, a ranged weapon, routing) - counted once.</summary>
        private void DropAiKickWait(TrackedAgent st)
        {
            if (double.IsNaN(st.AiKickWaitUntil)) return;
            _aiKickStats.WaitsDropped++;
            ClearAiKickWait(st);
        }

        private static void ClearAiKickWait(TrackedAgent st)
        {
            st.AiKickWaitUntil = double.NaN;
            st.AiKickWaitSince = double.NaN;
        }

        /// <summary>Step 66 (StartRelease, after the class read): an AI shieldman's melee swing on foot earns AiBashShareOfSwingsPercent of a bash
        /// (at most AiKickMath.BashCreditCap saved) - "as often as he swings".</summary>
        internal void NoteAiBashSwing(TrackedAgent st, bool mounted)
        {
            if (mounted || st.AttackClass != AttackClass.ShieldInfantry || IsPlayer(st)) return;
            var r = AiKickRules.From(TraxSettings.Shared);
            if (!r.Enabled || !(r.BashShare > 0)) return;
            double before = st.BashCredit;
            st.BashCredit = AiKickMath.AddBashCredit(in r, before);
            _aiKickStats.ShieldSwings++;
            _aiKickStats.CreditEarned += st.BashCredit - before;
            _aiKickStats.CreditLost += Math.Max(0, r.BashShare - (st.BashCredit - before));
        }

        private void BeginAiKick(TrackedAgent st, TrackedAgent target, AiKickKind kind, int reason, double now, double cooldown, double turtle, double chance)
        {
            st.AiKickKind = kind;
            st.AiKickPhase = AiKickMath.FirstPhase(kind);
            st.AiKickStart = now;
            st.AiKickPhaseEnd = AiKickMath.PhaseEnd(st.AiKickPhase, now);
            st.AiKickReason = reason;
            st.AiKickSeen = false;
            st.AiKickLanded = false;
            st.AiKickNext = now + cooldown;
            st.AiKickTarget = target;
            _aiKickers[_aiKickCount++] = st;
            EnsureInput(st);
            AiInputHook.SetKick(st, st.AiKickPhase, kind, now);
            NoteHooked(st, AiKickBody.Hook(st));
            _aiKickStats.Asked(kind, reason);
            if (AiKickFirst(0))
                TraxLog.Info("impact", "first AI " + AiKickMath.KindName(kind) + " asked at " + Sec(now) + " s - " + Name(st) + " (bar " + Pct0(st.Fraction) + ", Athletics skill "
                    + st.AthleticsSkill + (st.Fever.Running ? ", in a fever" : string.Empty) + ") on " + Name(target) + " (turtle " + Pct0(turtle) + ", his chance this look "
                    + (100 * chance).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%) - through his own input: "
                    + (kind == AiKickKind.Bash ? "the guard up " + F2(AiKickMath.BashLeadSeconds) + " s, then " : string.Empty) + "the kick key " + F2(AiKickMath.PressSeconds)
                    + " s, then his own input held off until it plays out (" + F1(AiKickMath.WatchSeconds) + " s at most)");
        }

        /// <summary>The running attempts, every tick: gone / struck = ended; the action seen (step 18's tracker - the poll of both channels) =
        /// PLAYED; the phases by time; the commit ends when the action is over or the watch runs out.</summary>
        private void AdvanceAiKicks(double now)
        {
            var body = AiKickBody;
            for (int i = 0; i < _aiKickCount; i++)
            {
                var st = _aiKickers[i]!;
                try
                {
                    if (st.Removed || !body.IsActive(st) || IsPlayer(st) || !body.IsAiControlled(st))
                    {
                        EndAiKick(i--, AiKickEnd.Gone, now);
                        continue;
                    }
                    var kb = st.KickBash;
                    if (!st.AiKickSeen && (kb.Running || kb.DecidedAt >= st.AiKickStart - 1e-6))
                    {
                        st.AiKickSeen = true;
                        _aiKickStats.Played(st.AiKickKind, st.AiKickReason, now - st.AiKickStart);
                        var shown = kb.Upper != KickBashKind.None ? kb.Upper : kb.Lower;
                        if (shown != KickBashKind.None && (shown == KickBashKind.Bash) != (st.AiKickKind == AiKickKind.Bash)) _aiKickStats.PlayedOtherKind++;
                        if (AiKickFirst(1))
                            TraxLog.Info("impact", "first AI " + AiKickMath.KindName(st.AiKickKind) + " PLAYED at " + Sec(now) + " s - " + Name(st) + "'s action showed "
                                + F2(now - st.AiKickStart) + " s after the ask (" + (shown == KickBashKind.Bash ? "a bash" : shown == KickBashKind.Kick ? "a kick" : "seen at its hit")
                                + ") - the engine took the injected input");
                    }
                    if (!st.AiKickSeen && (st.Impact.Reeling(now) || st.Impact.Down(now) || ImpactActions.IsStrike(st.PrevAction) || ImpactActions.IsFall(st.PrevLowerAction)))
                    {
                        EndAiKick(i--, AiKickEnd.Interrupted, now);
                        continue;
                    }
                    if (st.AiKickPhase == AiKickPhase.Commit && st.AiKickSeen && !kb.Running)
                    {
                        EndAiKick(i--, AiKickEnd.Played, now);
                        continue;
                    }
                    if (now >= st.AiKickPhaseEnd)
                    {
                        var next = AiKickMath.Next(st.AiKickPhase);
                        if (next == AiKickPhase.None)
                        {
                            EndAiKick(i--, st.AiKickSeen ? AiKickEnd.Played : AiKickEnd.NotPlayed, now);
                            continue;
                        }
                        st.AiKickPhase = next;
                        st.AiKickPhaseEnd = AiKickMath.PhaseEnd(next, now);
                        AiInputHook.SetKick(st, next, st.AiKickKind, now);
                    }
                }
                catch (Exception e)
                {
                    Failed("aikick.advance", e);
                    EndAiKick(i--, AiKickEnd.Gone, now);
                }
            }
        }

        /// <summary>One attempt over: the wish off (the callback released if idle - never for a man gone), counted, out of the pool.</summary>
        private void EndAiKick(int i, AiKickEnd why, double now, bool engine = true)
        {
            var st = _aiKickers[i]!;
            _aiKickStats.Ends[(int)why]++;
            if (why == AiKickEnd.NotPlayed && AiKickFirst(2))
                TraxLog.Info("impact", "first AI " + AiKickMath.KindName(st.AiKickKind) + " NOT played at " + Sec(now) + " s - " + Name(st) + ": no kick / bash showed within "
                    + F1(AiKickMath.WatchSeconds) + " s of the press (his channel 1 " + ImpactActions.Name(st.PrevAction) + ", channel 0 " + ImpactActions.Name(st.PrevLowerAction)
                    + ") - tell Claude if the summary's played count stays near 0");
            st.AiKickPhase = AiKickPhase.None;
            st.AiKickTarget = null;
            AiInputHook.SetKick(st, AiKickPhase.None, AiKickKind.None, now);
            if (engine && !st.Removed)
            {
                try
                {
                    AiKickBody.Release(st);
                }
                catch (Exception e)
                {
                    Failed("aikick.unhook", e);
                }
            }
            _aiKickers[i] = _aiKickers[--_aiKickCount];
            _aiKickers[_aiKickCount] = null;
        }

        private void EndAllAiKicks(AiKickEnd why, double now, string? because)
        {
            int n = _aiKickCount;
            while (_aiKickCount > 0) EndAiKick(_aiKickCount - 1, why, now, engine: why != AiKickEnd.MissionEnd);
            if (n > 0 && why == AiKickEnd.SwitchedOff)
                TraxLog.Info("impact", "the AI kicks switched off at " + Sec(now) + " s (" + because + "): " + n + " running attempt(s) ended, their input given back");
        }

        /// <summary>He left the field (Forget): his attempt ended with no engine call.</summary>
        private void AiKickLeftField(TrackedAgent st)
        {
            if (st.AiKickPhase == AiKickPhase.None) return;
            for (int i = 0; i < _aiKickCount; i++)
                if (ReferenceEquals(_aiKickers[i], st))
                {
                    EndAiKick(i, AiKickEnd.Gone, SafeNow(), engine: false);
                    return;
                }
        }

        // ------------------------------------------------------------------ the hits (OnMeleeHit)

        /// <summary>A kick / bash of his LANDED or hit a shield (OnMeleeHit's attacker part): counted once per attempt.</summary>
        internal void AiKickHit(TrackedAgent st, bool intoShield)
        {
            if (st.AiKickPhase == AiKickPhase.None || st.AiKickLanded) return;
            st.AiKickLanded = true;
            _aiKickStats.Landed(st.AiKickKind, st.AiKickReason, intoShield);
            if (!intoShield && AiKickFirst(3))
                TraxLog.Info("impact", "first AI " + AiKickMath.KindName(st.AiKickKind) + " LANDED at " + Sec(SafeNow()) + " s - " + Name(st)
                    + (st.AiKickTarget != null ? " on " + Name(st.AiKickTarget) : string.Empty) + " (the impact model and the brace decide what it does)");
        }

        /// <summary>A block by <paramref name="st"/> (BlockHitTaken - a paid block): the turtle memory one more.</summary>
        private void NoteTurtleBlock(TrackedAgent st, double now)
        {
            AiKickMath.NoteBlock(ref st.TurtleBlocks, ref st.TurtleBlocksAt, now, TraxSettings.Shared.AiKickTurtleSeconds);
        }

        /// <summary>The Athletics an AI kick cost (ChargeKickOrBash, while his attempt runs).</summary>
        private void NoteAiKickCost(TrackedAgent st, double points)
        {
            if (st.AiKickPhase != AiKickPhase.None && points > 0) _aiKickStats.PointsSpent += points;
        }

        private bool AiKickFirst(int i)
        {
            if (_aiKickFirst[i]) return false;
            _aiKickFirst[i] = true;
            return true;
        }

        /// <summary>The summary's turn (after the brace's lines): the running attempts ended first (no engine call - the mission ends).</summary>
        internal void WriteAiKickSummary()
        {
            EndAllAiKicks(AiKickEnd.MissionEnd, SafeNow(), null);
            foreach (var line in _aiKickStats.SummaryLines(AiKickRules.From(TraxSettings.Shared))) TraxLog.Info("summary", line);
        }
    }
}
