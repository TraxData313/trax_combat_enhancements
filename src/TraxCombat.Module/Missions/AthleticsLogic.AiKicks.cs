using System;
using System.Diagnostics;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 65 - THE AI KICKS AND BASHES (Anton 2026-10-19: "make the AI do a few shield bashes and kicks now and then - the game has it but the
    /// NPCs never use them"; AI_NOTES "Step 65", DESIGN decision 57). Step 81 - AS A SHARE OF THE MELEE MOVES (Toni 2026-10-22: "shielded men kick
    /// 20% of the time, shield bash 35% and swing the rest ... Unshielded - 35% kick and the rest swing"; AI_NOTES "Step 81", DESIGN decision 77).
    ///
    /// THE LEVER (the decompile + RBM's AiKickBash, its public source): the engine performs a kick from the agent's EVENT flags -
    /// <c>Agent.EventControlFlag.Kick</c> (0x8000), the bit vanilla's own <c>MissionMainAgentController</c> sets for your kick key; the input
    /// is CONTEXTUAL - the kick key while the guard is up is a BASH (shield or weapon), else a kick. The native AI's kick tendency
    /// (<c>AgentDrivenProperties.AiKick</c>, written by <c>SetAiRelatedProperties</c>) does nothing in game (RBM's finding; Anton: "the NPCs
    /// never use them"). So the kick is INJECTED through step 16's input hook (<see cref="AiInputComponent"/> - <c>OnAIInputSet</c>, the AI's
    /// own input before the engine uses it): the guard up (DefendUp) - 0.4 s for a bash, 0.15 s for a kick -, then the kick bit for 0.15 s (+ DefendUp
    /// for a bash), then COMMIT - his own attack / guard / event input held off and his movement stopped until the action has played out (or 1.5 s).
    ///
    /// THE DECISION POINT (step 81): the AI's own decision to attack - the wind-up of his melee attack, which step 13's phases see on the poll of his
    /// channel-1 action (<see cref="NoteAiMeleeReady"/>: queued only - the phases may run inside a hit callback). The tick rolls it
    /// (<see cref="DecideAiMove"/>): an AI man on foot whose enemy (his target, else his immediate enemy) stands within kick reach in front of him
    /// rolls his SHARES - a kick / a bash takes the place of that swing (the guard of the attempt's first phase cancels the wind-up - step 16's ready
    /// cancel: his attack bits out WITH a guard); a swing goes on untouched. Beyond kick reach nothing is rolled (a spearman keeps his ground and thrusts).
    /// A rolled kick / bash is REFUSED - he swings - when staggered / down / his kick still playing / without the Athletics for it / the pool full
    /// (safety), with no stored swing (step 32's stack - KicksUseSwings), within the cooldown of his last, or when the enemy is not the first body in
    /// his strike path (step 80). The rate, its factors, the bar gate, the wait for reach and the bash credit (steps 65 / 66) are gone.
    ///
    /// THE COST: the kick / bash is SEEN by step 18's action poll like yours and charged there - <c>CostPerKickOrBash</c> and a stored swing
    /// (step 32) - nothing of our own.
    ///
    /// Measured: the [summary] "ai kicks" lines - asked → PLAYED (the action seen) → landed / into a shield, by kind and man type, the ends, the
    /// decision point (wind-ups, not a move in reach), THE SHARES rolled and done per man type, the refusals, the Athletics spent. Master switch FIRST
    /// (<see cref="AiKickRules.Enabled"/>): off = every attempt's wish cleared at once, nothing queued. Main thread; fixed pools, no allocation per tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly AiKickStats _aiKickStats = new AiKickStats();
        private readonly TrackedAgent?[] _aiKickers = new TrackedAgent?[AiKickMath.MaxAttempts];
        private int _aiKickCount;
        private readonly TrackedAgent?[] _aiMoves = new TrackedAgent?[AiKickMath.MaxPendingMoves];
        private int _aiMoveCount;
        private int _aiKickCursor;
        private double _aiKickCarry;
        private double _aiKickLastTick = double.NaN;
        private string _seenAiKickText = string.Empty;
        private readonly bool[] _aiKickFirst = new bool[12]; // 7 / 8: step 87's first refusal by a friend - a kick / a bash; 9 / 10: its first friend hit

        /// <summary>This mission's AI kick numbers (the smoke reads them).</summary>
        internal AiKickStats AiKickStats => _aiKickStats;

        /// <summary>Attempts running now (the smoke).</summary>
        internal int AiKicksRunning => _aiKickCount;

        /// <summary>Wind-ups waiting for the tick's roll (the smoke).</summary>
        internal int AiMovesPending => _aiMoveCount;

        /// <summary>The engine reads (the smoke swaps in a stand-in).</summary>
        internal IAiKickBody AiKickBody { get; set; } = AiKickBodyDefault.Current;

        /// <summary>The roll's dice (the smoke seeds its own).</summary>
        internal IRandomSource AiKickDice { get; set; } = ThreadSafeRandom.Shared;

        private void StartAiKicks()
        {
            _seenAiKickText = AiKickRules.From(TraxSettings.Shared).Describe();
            TraxLog.Info("impact", "mission start, the AI kicks and bashes (steps 65 / 81): " + _seenAiKickText + " - read live; rolled at each AI melee wind-up; the [summary] \"ai kicks\" lines count "
                + "asked → played → landed and the shares rolled / done per man type (PLAYTEST D31 / D46)");
        }

        /// <summary>An AI kick setting changed (from ApplySettingsChange): one line (switching off ends every attempt in the next tick).</summary>
        private void NoteAiKickSettings(TraxSettings s)
        {
            string now = AiKickRules.From(s).Describe();
            if (now == _seenAiKickText) return;
            _seenAiKickText = now;
            TraxLog.Info("impact", "AI kick settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now);
        }

        // ------------------------------------------------------------------ the decision point

        /// <summary>Step 81: an AI man's melee WIND-UP began (step 13's phases - may run inside a hit callback): queued for the tick's roll, once.</summary>
        internal void NoteAiMeleeReady(TrackedAgent st)
        {
            if (st.AiMoveQueued || st.AiKickPhase != AiKickPhase.None || IsPlayer(st)) return;
            if (!AiKickRules.From(TraxSettings.Shared).Enabled) return;
            if (_aiMoveCount >= _aiMoves.Length)
            {
                _aiKickStats.QueueFull++;
                return;
            }
            st.AiMoveQueued = true;
            _aiMoves[_aiMoveCount++] = st;
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick (from TickAthletics, own try there): off (the master switch first) = every attempt ended at once and the queue
        /// dropped; the running attempts advanced; the queued wind-ups rolled; a staggered slice of contact samples (the man-minutes).</summary>
        internal void TickAiKicks(double now)
        {
            var r = AiKickRules.From(TraxSettings.Shared);
            if (!r.Enabled)
            {
                if (_aiKickCount > 0) EndAllAiKicks(AiKickEnd.SwitchedOff, now, r.OffBecause);
                ClearAiMoves();
                _aiKickLastTick = now;
                return;
            }
            long start = Stopwatch.GetTimestamp();
            if (_aiKickCount > 0) AdvanceAiKicks(now, in r);
            if (_aiMoveCount > 0)
            {
                var ar = Rules;
                for (int i = 0; i < _aiMoveCount; i++)
                {
                    var st = _aiMoves[i]!;
                    _aiMoves[i] = null;
                    st.AiMoveQueued = false;
                    try
                    {
                        DecideAiMove(st, now, in r, in ar);
                    }
                    catch (Exception e)
                    {
                        Failed("aikick.decide", e);
                    }
                }
                _aiMoveCount = 0;
            }
            // the contact samples (the summary's man-minutes in contact): a staggered slice, each man about every 0.25 s - our own records only
            double dt = double.IsNaN(_aiKickLastTick) || now < _aiKickLastTick ? AiKickMath.LookSeconds : now - _aiKickLastTick;
            _aiKickLastTick = now;
            if (dt > AiKickMath.LookSeconds) dt = AiKickMath.LookSeconds;
            _aiKickCarry += _count * dt / AiKickMath.LookSeconds;
            int n = (int)(_aiKickCarry + 1e-9);
            _aiKickCarry -= n;
            if (_aiKickCarry < 0) _aiKickCarry = 0;
            if (n > _count) n = _count;
            for (int k = 0; k < n; k++)
            {
                if (_aiKickCursor >= _count) _aiKickCursor = 0;
                var st = _dense[_aiKickCursor++];
                if (st.Removed || IsPlayer(st)) continue;
                double last = st.AiKickLastLook;
                st.AiKickLastLook = now;
                if (st.FeverContact) _aiKickStats.ContactManSeconds += double.IsNaN(last) || now < last || now - last > 4 * AiKickMath.LookSeconds ? AiKickMath.LookSeconds : now - last;
            }
            _aiKickStats.CostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        private void ClearAiMoves()
        {
            for (int i = 0; i < _aiMoveCount; i++)
            {
                var st = _aiMoves[i];
                if (st != null) st.AiMoveQueued = false;
                _aiMoves[i] = null;
            }
            _aiMoveCount = 0;
        }

        /// <summary>
        /// Step 81, ONE melee move at its wind-up: not his to roll (gone, you, not AI, the wind-up over) = nothing; a rider / a routing man / a ranged
        /// weapon / no enemy / the enemy beyond kick reach or not in front = not a move in reach (counted; he swings or thrusts as the game wants);
        /// else THE ROLL by his shares (a shieldman kick / bash / swing, no shield kick / swing) - a swing goes on; a kick / bash is refused (he
        /// swings) by safety, the stack, the cooldown or the strike path, else BEGUN - its guard cancels the wind-up.
        /// </summary>
        internal void DecideAiMove(TrackedAgent st, double now, in AiKickRules r, in AthleticsRules ar)
        {
            if (st.Removed || st.AiKickPhase != AiKickPhase.None) return;
            var body = AiKickBody;
            if (IsPlayer(st) || !body.IsActive(st) || !body.IsAiControlled(st)) return;
            _aiKickStats.Looks++;
            if (st.PrevAction != ActionReadyMelee)
            {
                Skip(AiKickSkip.ReadyOver); // released / cancelled before the tick looked - nothing left to replace
                return;
            }
            _aiKickStats.NativeReads++;
            body.Hands(st, out bool mounted, out bool routing, out bool ranged, out bool shield);
            if (mounted || routing || ranged)
            {
                Skip(mounted ? AiKickSkip.Mounted : routing ? AiKickSkip.Routing : AiKickSkip.Ranged);
                return;
            }
            var targetAgent = body.Target(st, out float distance, out float off);
            var target = targetAgent != null ? Get(targetAgent) : null;
            if (target == null || target.Removed || target.Impact.Down(now) || ImpactActions.IsFall(target.PrevLowerAction))
            {
                Skip(AiKickSkip.NoTarget);
                return;
            }
            if (!AiKickMath.InReach(in r, distance, off, out var notInReach))
            {
                Skip(notInReach); // a spearman with the enemy beyond his kick keeps his ground and thrusts
                return;
            }
            // a move in kick reach: THE ROLL
            int type = AiKickStats.TypeOf(shield);
            var kind = AiKickMath.Roll(in r, shield, AiKickDice.NextDouble());
            _aiKickStats.NoteMove(type, kind);
            if (kind == AiKickKind.None) return; // a swing: his wind-up goes on untouched
            if (kind == AiKickKind.Bash && r.KicksARaisedShield && target.PrevAction == ImpactActions.DefendShield && body.FacingTo(target, st) <= AiKickMath.RaisedShieldDegrees)
            {
                // step 82: with the meet rules a raised shield guarding RIGHT meets a bash correctly (the basher pays) - kick it instead; guarding
                // any other way a bash is the better move (the blocker is at risk) - keep it. Without the rules: step 65's swap
                bool swap = true;
                if (ImpactMeetRules.From(TraxSettings.Shared).Enabled)
                {
                    var g = MeetBody.Guard(target.Agent);
                    swap = g.Kind == MeetGuardKind.Shield && g.Dir == MeetDir.Right;
                    if (swap) _meet.SwapsToKick++;
                    else _meet.BashesKeptIntoWrongShield++;
                }
                if (swap)
                {
                    kind = AiKickKind.Kick; // a raised shield facing him stops a bash, not a kick
                    _aiKickStats.RaisedShieldKicks++;
                }
            }
            // the refusals - he swings instead: safety, the stack, the cooldown, the strike path (the engine's positions last)
            if (st.Impact.Reeling(now) || st.Impact.Down(now) || ImpactActions.IsStrike(st.PrevAction) || ImpactActions.IsFall(st.PrevLowerAction)
                || ImpactActions.IsStrike(st.PrevLowerAction))
            {
                Refuse(type, AiKickSkip.Staggered);
                return;
            }
            if (st.KickBash.Running)
            {
                Refuse(type, AiKickSkip.Busy);
                return;
            }
            if (AthleticsMath.Points(in ar, st) < AthleticsMath.KickOrBashCostPoints(in ar, st))
            {
                Refuse(type, AiKickSkip.NoAthletics);
                return;
            }
            if (_aiKickCount >= _aiKickers.Length)
            {
                Refuse(type, AiKickSkip.PoolFull);
                return;
            }
            if (NoStoredSwing(st, in ar))
            {
                Refuse(type, AiKickSkip.NoSwing);
                return;
            }
            if (now < st.AiKickNext)
            {
                Refuse(type, AiKickSkip.Cooldown);
                return;
            }
            // step 80: the strike path along his facing - the enemy in it and the first body there
            var path = StrikePathOf(st, targetAgent!, kind, distance, off, in r, out var blocker);
            if (r.PathHalfWidth(kind) > 0)
            {
                _aiKickStats.NotePath(kind, path);
                _aiKickStats.Blocks.NoteRefusal(path, kind, in blocker, distance, off); // step 87 (a diagnostic): who blocks it
                if (path == AiKickPath.BehindFriend || path == AiKickPath.FriendFirst) BlockFirstLine(st, target, kind, path, in blocker, distance, off, now);
            }
            if (path != AiKickPath.Clear)
            {
                bool rear = path == AiKickPath.BehindFriend;
                if (path != AiKickPath.TargetOffPath) _aiKickStats.FriendChecksBlocked++;
                Refuse(type, path == AiKickPath.TargetOffPath ? AiKickSkip.TargetOffPath : rear ? AiKickSkip.BehindFriend : AiKickSkip.FriendInTheWay);
                if (path != AiKickPath.TargetOffPath && AiKickFirst(rear ? 5 : 4))
                    TraxLog.Info("impact", "first AI " + AiKickMath.KindName(kind) + " REFUSED - " + AiKickMath.PathName(path) + " at " + Sec(now) + " s (step 80) - " + Name(st)
                        + " at " + Name(target) + " " + F2(distance) + " m off, " + F1(off) + "° off his facing: one of his own side stands in his strike path (along his facing, "
                        + F2(r.PathHalfWidth(kind)) + " m either side) closer than the enemy" + (rear ? " - within " + F1(r.RearRank) + " m of him (AiKickRearRankMetres): a rear-rank man" : string.Empty)
                        + " - he swings instead (step 81)");
                return;
            }
            if (r.PathHalfWidth(kind) > 0) _aiKickStats.FriendChecksClear++;
            BeginAiKick(st, target, kind, type, now, r.Cooldown, off);
        }

        /// <summary>Step 87: the first strike-path refusal by a friend of each kind, in full - who blocked and where he stood.</summary>
        private void BlockFirstLine(TrackedAgent st, TrackedAgent target, AiKickKind kind, AiKickPath path, in AiKickBlocker b, float distance, float off, double now)
        {
            if (!AiKickFirst(kind == AiKickKind.Bash ? 8 : 7)) return;
            var friend = b.FriendIndex >= 0 ? GetByIndex(b.FriendIndex) : null;
            TraxLog.Info("impact", "first AI " + AiKickMath.KindName(kind) + " REFUSED by a friend - who blocks the path (step 87 diagnostic) at " + Sec(now) + " s: "
                + Name(st) + BlockRanks(b.File, b.Rank) + " at " + Name(target) + " " + F2(distance) + " m off, " + F1(off) + "° off his facing - " + AiKickMath.PathName(path)
                + (b.Valid
                    ? ": " + (friend != null ? Name(friend) : "a friend (untracked)") + BlockRanks(b.FriendFile, b.FriendRank) + " " + F2(b.Along) + " m ahead along his facing, "
                      + F2(b.Side) + " m to the side - " + AiKickBlockMath.RelationName(b.Relation)
                    : " (the blocking friend not named by the probe)")
                + " - the [summary] \"who blocks the path\" line bins every one");
        }

        private static string BlockRanks(int file, int rank) => rank >= 0 ? " (file " + file + ", rank " + rank + ")" : string.Empty;

        /// <summary>Step 81: a rolled kick / bash refused - he swings instead (counted by reason and by group).</summary>
        private void Refuse(int type, AiKickSkip why)
        {
            Skip(why);
            _aiKickStats.NoteRefused(type, why);
        }

        /// <summary>Step 32 / 81: KicksUseSwings - a kick / bash cannot start with no stored swing (a man the stack holds, or his stack at 0 now).</summary>
        private static bool NoStoredSwing(TrackedAgent st, in AthleticsRules ar)
        {
            var inp = st.Input;
            if (inp != null && inp.HoldKicks && inp.HoldAttacks) return true;
            var sr = SwingStackRules.From(TraxSettings.Shared);
            if (!sr.KicksOn) return false;
            return st.Swings.Count(SwingStackMath.Cap(in sr, in ar, st)) < 1;
        }

        private void Skip(AiKickSkip why) => _aiKickStats.Skips[(int)why]++;

        /// <summary>Step 80: the strike path for a <paramref name="kind"/> at the enemy <paramref name="distance"/> m / <paramref name="off"/>° off his
        /// facing - off = Clear; the cheap gate on the reads he has (the enemy in the path at all), then the engine's positions and his side's men
        /// near (<see cref="IAiKickBody.StrikePath"/>).</summary>
        private AiKickPath StrikePathOf(TrackedAgent st, Agent target, AiKickKind kind, float distance, float off, in AiKickRules r, out AiKickBlocker blocker)
        {
            blocker = AiKickBlocker.None;
            double half = r.PathHalfWidth(kind);
            if (!(half > 0)) return AiKickPath.Clear;
            if (!AiKickMath.TargetInPath(distance, off, r.Reach, half)) return AiKickPath.TargetOffPath;
            _aiKickStats.PathProbes++;
            var v = AiKickBody.StrikePath(st, target, r.Reach, half, r.RearRank, out int looked, out blocker);
            _aiKickStats.PathFriendsLooked += looked;
            return v;
        }

        /// <summary>Step 80: a bash's guard is over - its strike path read again just before the key (the enemy he fights now, in reach, in front,
        /// in the path, no friend first): anything but clear ends the attempt with no key.</summary>
        private AiKickPath PressPath(TrackedAgent st, in AiKickRules r)
        {
            if (!(r.PathHalfWidth(st.AiKickKind) > 0)) return AiKickPath.Clear;
            var targetAgent = AiKickBody.Target(st, out float distance, out float off);
            if (targetAgent == null || !(distance <= r.Reach) || !(off <= r.Facing)) return AiKickPath.TargetOffPath;
            return StrikePathOf(st, targetAgent, st.AiKickKind, distance, off, in r, out _);
        }

        private void BeginAiKick(TrackedAgent st, TrackedAgent target, AiKickKind kind, int type, double now, double cooldown, double angle)
        {
            st.AiKickAngle = angle; // step 87: the facing-to-target angle of the decision (a friend hit is binned by it)
            st.AiKickKind = kind;
            st.AiKickPhase = AiKickMath.FirstPhase(kind);
            st.AiKickStart = now;
            st.AiKickPhaseEnd = AiKickMath.PhaseEnd(st.AiKickPhase, now, kind);
            st.AiKickReason = type;
            st.AiKickSeen = false;
            st.AiKickLanded = false;
            st.AiKickNext = now + cooldown;
            st.AiKickTarget = target;
            _aiKickers[_aiKickCount++] = st;
            EnsureInput(st);
            AiInputHook.SetKick(st, st.AiKickPhase, kind, now);
            NoteHooked(st, AiKickBody.Hook(st));
            _aiKickStats.Asked(kind, type);
            MeetReadStart(st, target, kind == AiKickKind.Bash, now); // step 82: the target may read it from its start
            if (AiKickFirst(0))
                TraxLog.Info("impact", "first AI " + AiKickMath.KindName(kind) + " asked at " + Sec(now) + " s - " + Name(st) + " (bar " + Pct0(st.Fraction) + ", "
                    + AiKickStats.TypeName(type) + ") on " + Name(target) + " in place of the swing he was winding up (step 81) - through his own input: the guard up "
                    + F2(kind == AiKickKind.Bash ? AiKickMath.BashLeadSeconds : AiKickMath.KickLeadSeconds) + " s (his wind-up cancelled), then the kick key "
                    + F2(AiKickMath.PressSeconds) + " s" + (kind == AiKickKind.Bash ? " with the guard up" : string.Empty) + ", then his own input held off until it plays out ("
                    + F1(AiKickMath.WatchSeconds) + " s at most)");
        }

        /// <summary>The running attempts, every tick: gone / struck = ended; the action seen (step 18's tracker - the poll of both channels) =
        /// PLAYED; the phases by time; the commit ends when the action is over or the watch runs out.</summary>
        private void AdvanceAiKicks(double now, in AiKickRules r)
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
                        if (next == AiKickPhase.Press && st.AiKickKind == AiKickKind.Bash)
                        {
                            // step 80: the bash's guard is over - its path read again before the key
                            var pv = PressPath(st, in r);
                            if (r.PathHalfWidth(AiKickKind.Bash) > 0) _aiKickStats.NotePressPath(pv);
                            if (pv != AiKickPath.Clear)
                            {
                                if (AiKickFirst(6))
                                    TraxLog.Info("impact", "first AI shield bash ENDED before its key at " + Sec(now) + " s (step 80) - " + Name(st) + ": after the "
                                        + F2(AiKickMath.BashLeadSeconds) + " s guard his strike path is no longer clear (" + AiKickMath.PathName(pv) + ") - no bash");
                                EndAiKick(i--, AiKickEnd.PathBlocked, now);
                                continue;
                            }
                        }
                        st.AiKickPhase = next;
                        st.AiKickPhaseEnd = AiKickMath.PhaseEnd(next, now, st.AiKickKind);
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
            if (why == AiKickEnd.Played && st.AiKickSeen && !st.AiKickLanded) _aiKickStats.NoteOutcome(st.AiKickKind, AiKickOutcome.Miss); // step 80
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
        internal void AiKickHit(TrackedAgent st, bool intoShield) => AiKickHit(st, intoShield, null);

        /// <summary>Step 68: ... and WHOM it landed on (his target, another enemy, a FRIEND - the game cancels it, a horse / a man not tracked).</summary>
        internal void AiKickHit(TrackedAgent st, bool intoShield, Agent? victim)
        {
            if (st.AiKickPhase == AiKickPhase.None || st.AiKickLanded) return;
            st.AiKickLanded = true;
            _aiKickStats.Landed(st.AiKickKind, st.AiKickReason, intoShield);
            var who = victim != null ? AiKickVictimOf(st, victim) : AiKickVictim.Untracked;
            if (!intoShield && victim != null) _aiKickStats.NoteVictim(who);
            if (who == AiKickVictim.Friend && victim != null) FriendHitBlock(st, victim, intoShield); // step 87
            // step 80: whom it hit first - an enemy (his body / his shield), a friend (body / shield), a horse / untracked
            _aiKickStats.NoteOutcome(st.AiKickKind, who == AiKickVictim.Untracked ? AiKickOutcome.Untracked
                : who == AiKickVictim.Friend ? (intoShield ? AiKickOutcome.FriendShield : AiKickOutcome.FriendBody)
                : intoShield ? AiKickOutcome.EnemyShield : who == AiKickVictim.Target ? AiKickOutcome.EnemyTarget : AiKickOutcome.EnemyOther);
            if (!intoShield && AiKickFirst(3))
                TraxLog.Info("impact", "first AI " + AiKickMath.KindName(st.AiKickKind) + " LANDED at " + Sec(SafeNow()) + " s - " + Name(st)
                    + (st.AiKickTarget != null ? " on " + Name(st.AiKickTarget) : string.Empty) + " (the impact model and the brace decide what it does)");
        }

        /// <summary>Step 87 (a diagnostic): a kick / bash whose first collision was a friend - his place in the kicker's frame at the hit and in
        /// the formation, the decision's angle; the first of each kind in full.</summary>
        private void FriendHitBlock(TrackedAgent st, Agent victim, bool intoShield)
        {
            var b = AiKickBlocker.None;
            try
            {
                b = AiKickBody.Locate(st, victim);
            }
            catch (Exception e)
            {
                Failed("aikick.locate", e);
            }
            _aiKickStats.Blocks.NoteFriendHit(st.AiKickKind, in b, st.AiKickAngle);
            if (!AiKickFirst(st.AiKickKind == AiKickKind.Bash ? 10 : 9)) return;
            var friend = Get(victim);
            TraxLog.Info("impact", "first AI " + AiKickMath.KindName(st.AiKickKind) + " on a FRIEND - where he stood (step 87 diagnostic) at " + Sec(SafeNow()) + " s: "
                + Name(st) + BlockRanks(b.File, b.Rank) + (intoShield ? " hit the shield of " : " hit ") + (friend != null ? Name(friend) : "a friend") + BlockRanks(b.FriendFile, b.FriendRank)
                + (b.Valid ? " " + F2(b.Along) + " m ahead along his facing, " + F2(b.Side) + " m to the side - " + AiKickBlockMath.RelationName(b.Relation) : " (not located)")
                + "; at the decision his target stood " + F1(st.AiKickAngle) + "° off his facing");
        }

        /// <summary>Step 68: whom a landed kick / bash hit - the victim against the kicker (a native IsEnemyOf, once per landed kick).</summary>
        private AiKickVictim AiKickVictimOf(TrackedAgent st, Agent victim)
        {
            var v = Get(victim);
            if (v == null || v.Removed) return AiKickVictim.Untracked;
            if (ReferenceEquals(v, st.AiKickTarget)) return AiKickVictim.Target;
            return AiKickBody.IsEnemy(st, v) ? AiKickVictim.OtherEnemy : AiKickVictim.Friend;
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
            ClearAiMoves();
            foreach (var line in _aiKickStats.SummaryLines(AiKickRules.From(TraxSettings.Shared))) TraxLog.Info("summary", line);
        }
    }
}
