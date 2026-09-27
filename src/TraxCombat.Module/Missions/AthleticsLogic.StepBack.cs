using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Tired fighters step back (DESIGN §2, step 5d; research and choice in AI_NOTES "Step 5d").
    ///
    /// THE FLOW: a melee swing ENDS (the falling edge out of ReleaseMelee, seen by the poll or inside
    /// a hit) → an AI fighter on foot in a field battle rolls StepBackMaxChancePercent × (1 − f) (no
    /// dice in the peak zone: the chance is 0) → a yes is QUEUED → the next pass of the tick (never
    /// inside an engine callback) runs the game's safety checks and starts a scripted step
    /// (<see cref="IStepBackBody"/>) → every tick checks each running one: time up (StepBackSeconds,
    /// read live) or any reason to cut it short → released (<c>DisableScriptedMovement</c>) and his
    /// formation takes him back.
    ///
    /// ALWAYS RELEASED: time up, left the field (no engine call), mission end (through the engine,
    /// before the summary), switched off (ModEnabled, AthleticsEnabled or StepBackEnabled - everyone at
    /// once, next tick), formation order / arrangement / formation changed, detached, the player took
    /// him, mounted, routing, an exception. Handed over to a game job (object, ladder queue) = our
    /// record dropped WITHOUT disabling (the game's job must not be cancelled).
    ///
    /// LOGS: [stepback] mission start (rules, technique), the mission kind (first tick), on/off and
    /// settings changes, the FIRST step back of the mission in full (start and end, always), each
    /// start / end / refusal in verbose buckets (stepback, stepback-refused); the [summary] lines of
    /// <see cref="StepBackStats"/>.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly List<TrackedAgent> _stepPending = new List<TrackedAgent>(64);
        private readonly List<TrackedAgent> _stepping = new List<TrackedAgent>(64);
        private readonly StepBackStats _stepStats = new StepBackStats();
        private bool _stepSeenEnabled;
        private string? _stepOffBecause;
        private int _stepSeenVersion = -1;
        private string? _stepSeenText;
        private bool _stepFirstStarted;
        private bool _stepClosed;

        /// <summary>This mission's step-back numbers (the offline smoke reads them).</summary>
        internal StepBackStats StepStats => _stepStats;

        /// <summary>The engine side - the offline smoke swaps in a stand-in (its agents have no native side).</summary>
        internal IStepBackBody StepBackBody { get; set; } = new GameStepBackBody();

        /// <summary>Step 16: the engine side of the backpedal (StepBackBackpedal) - the smoke swaps it too.</summary>
        internal IStepBackBody StepBackInputBody { get; set; } = new InputStepBackBody();

        private bool _stepSeenBackpedal;

        private IStepBackBody BodyOf(StepBackState sb) => sb.ByInput ? StepBackInputBody : StepBackBody;

        /// <summary>The dice (the smoke makes them repeatable).</summary>
        internal IRandomSource StepBackDice { get; set; } = ThreadSafeRandom.Shared;

        /// <summary>Fighters stepping back right now (the smoke checks it).</summary>
        internal int SteppingNow => _stepping.Count;

        private void StartStepBacks()
        {
            var sr = StepBackRules.From(TraxSettings.Shared);
            _stepSeenEnabled = sr.Enabled;
            _stepOffBecause = sr.OffBecause;
            _stepSeenVersion = TraxSettings.Shared.Version;
            _stepSeenText = sr.Describe();
            _stepSeenBackpedal = sr.Backpedal;
            TraxLog.Info("stepback", "mission start: " + _stepSeenText + " - read live; technique: "
                + (sr.Backpedal ? StepBackStats.BackpedalTechnique : StepBackStats.Technique));
        }

        /// <summary>First tick: what kind of mission this is for the step back.</summary>
        private void NoteStepBackMission()
        {
            try
            {
                TraxLog.Info("stepback", StepBackBody.MissionNote(Mission));
            }
            catch (Exception e)
            {
                Failed("stepback.mission", e);
            }
        }

        // ------------------------------------------------------------------ the swing

        /// <summary>A counted melee swing started: while he steps back it is a swing the scripted step
        /// did not hold (0 expected with StepBackHoldAttacks on).</summary>
        private void StepBackSwingStarted(TrackedAgent st)
        {
            var sb = st.StepBack;
            if (sb == null || !sb.Active) return;
            sb.Swings++;
            _stepStats.SwingsWhileStepping++;
        }

        /// <summary>
        /// A counted melee swing ENDED ("after each melee swing"): roll the chance from his f (after the
        /// swing's charge) and queue a yes for the next tick. May run inside an engine hit callback, so
        /// it only reads managed state and never moves anyone. Internal: the offline smoke drives it.
        /// </summary>
        internal void StepBackSwingEnded(TrackedAgent st, double now, in AthleticsRules r)
        {
            try
            {
                var sr = StepBackRules.From(TraxSettings.Shared);
                if (!sr.Enabled || _stepClosed) return;
                var sb = st.StepBack;
                if (sb != null && (sb.Active || sb.Pending))
                {
                    _stepStats.AddNotRolled(StepBackNotRolled.AlreadyStepping);
                    return;
                }
                var a = st.Agent;
                if (a.IsMainAgent)
                {
                    _stepStats.AddNotRolled(StepBackNotRolled.Player);
                    return;
                }
                if (a.MountAgent != null)
                {
                    _stepStats.AddNotRolled(StepBackNotRolled.Mounted);
                    return;
                }
                if (!StepBackBody.MissionAllows(Mission))
                {
                    _stepStats.AddNotRolled(StepBackNotRolled.MissionKind);
                    return;
                }
                double f = AthleticsMath.PeakShare(in r, st);
                double chance = StepBackMath.Chance(in sr, f);
                bool yes = chance > 0 && StepBackMath.Roll(chance, StepBackDice.NextDouble()); // no dice at 0
                _stepStats.AddRoll(f, chance, yes);
                if (!yes) return;
                sb ??= st.StepBack = new StepBackState();
                sb.Pending = true;
                sb.PendingAt = now;
                sb.PendingF = f;
                sb.PendingChance = chance;
                _stepPending.Add(st);
            }
            catch (Exception e)
            {
                Failed("stepback.roll", e);
            }
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick, whatever the switches (so switching off releases at once): settings
        /// changes, the running step backs (end checks, the mid-step sample), then the queued starts.
        /// Internal: the offline smoke drives it with its stand-in body.</summary>
        internal void TickStepBacks(double now)
        {
            var settings = TraxSettings.Shared;
            var sr = StepBackRules.From(settings);
            if (settings.Version != _stepSeenVersion) NoteStepBackSettings(in sr);

            if (!sr.Enabled)
            {
                if (_stepping.Count > 0)
                {
                    int n = _stepping.Count;
                    _stepStats.SwitchedOffReleases++;
                    FinishAll(StepBackEnd.SwitchedOff, now, native: true);
                    TraxLog.Info("stepback", (sr.OffBecause == "StepBackEnabled" ? "StepBackEnabled" : sr.OffBecause == "AthleticsEnabled" ? "AthleticsEnabled" : "the whole mod (ModEnabled)")
                        + " switched OFF mid-mission at " + Sec(now) + " s: " + n + " fighters stepping back released to their formations at once");
                }
                DropPending(StepBackRefusal.Gone);
                return;
            }

            // the running ones (backwards: Finish removes by index)
            for (int i = _stepping.Count - 1; i >= 0; i--)
            {
                if (i >= _stepping.Count) continue;
                var st = _stepping[i];
                var sb = st.StepBack!;
                try
                {
                    // the game's own reasons FIRST: a man the game just gave a job of its own must not be
                    // released over it, even when his time is up in the same tick
                    var body = BodyOf(sb);
                    StepBackEnd why = body.Check(st, in sb.Plan);
                    if (why == StepBackEnd.None && StepBackMath.TimeUp(in sr, sb.StartedAt, now)) why = StepBackEnd.TimeUp;
                    if (why == StepBackEnd.None)
                    {
                        // step 16: a backpedal measures the distance covered, checks the ground behind him and turns
                        // the backwards line into his own frame for this frame's input
                        why = body.Steer(st, ref sb.Plan, in sr, now, out float lx, out float ly);
                        if (why == StepBackEnd.None && sb.ByInput && st.Input != null)
                        {
                            st.Input.BackX = lx;
                            st.Input.BackY = ly;
                        }
                    }
                    if (why == StepBackEnd.None)
                    {
                        if (now >= sb.NextSampleAt) SampleEvery(st, sb, now);
                        if (!sb.MidSampled && now - sb.StartedAt >= 0.5 * sr.Seconds) SampleMid(st, sb);
                        continue;
                    }
                    Finish(st, why, now, native: why != StepBackEnd.HandedOver && why != StepBackEnd.ClearedByGame && why != StepBackEnd.NotActive);
                }
                catch (Exception e)
                {
                    Failed("stepback.tick", e);
                    SafeFinish(st, StepBackEnd.Error, now);
                }
            }

            // the queued starts
            if (_stepPending.Count == 0) return;
            int budget = StepBackMath.MaxStartsPerTick;
            for (int i = 0; i < _stepPending.Count; i++)
            {
                var st = _stepPending[i];
                var sb = st.StepBack!;
                sb.Pending = false;
                if (st.Removed || _stepClosed)
                {
                    Refuse(st, sb, StepBackRefusal.Gone);
                    continue;
                }
                if (_stepping.Count >= sr.MaxAtOnce)
                {
                    Refuse(st, sb, StepBackRefusal.AtOnceCap);
                    continue;
                }
                if (budget <= 0)
                {
                    Refuse(st, sb, StepBackRefusal.TickBudget);
                    continue;
                }
                budget--;
                TryStart(st, sb, in sr, now);
            }
            _stepPending.Clear();
        }

        private void TryStart(TrackedAgent st, StepBackState sb, in StepBackRules sr, double now)
        {
            var plan = default(StepBackPlan);
            StepBackRefusal why;
            // step 16: the technique is read at the START; a running step back keeps the one it began with
            var body = sr.Backpedal ? StepBackInputBody : StepBackBody;
            if (sr.Backpedal) EnsureInput(st);
            try
            {
                why = body.MissionAllows(Mission) ? body.Probe(st, in sr, ref plan) : StepBackRefusal.NoLongerEligible;
            }
            catch (Exception e)
            {
                Failed("stepback.probe", e);
                why = StepBackRefusal.EngineError;
            }
            if (why != StepBackRefusal.None)
            {
                Refuse(st, sb, why);
                return;
            }

            bool taken;
            try
            {
                taken = body.Start(st, ref plan, in sr);
            }
            catch (Exception e)
            {
                Failed("stepback.start", e);
                // whatever the engine got, take it back (a scripted frame nobody tracks would never end)
                try { body.Release(st, in plan); } catch { /* already failing - logged above */ }
                Refuse(st, sb, StepBackRefusal.EngineError);
                return;
            }
            if (!taken)
            {
                Refuse(st, sb, StepBackRefusal.EngineIgnored);
                return;
            }

            sb.Active = true;
            sb.StartedAt = now;
            st.SteppedBackThisCycle = true; // step 5e: this cycle has a step back inside (step 16: counted, shown apart)
            sb.Plan = plan;
            sb.ByInput = sr.Backpedal;
            sb.HoldAttacks = sr.HoldAttacks;
            sb.MidSampled = false;
            sb.Mid = default;
            sb.HitsTaken = sb.HitsBlocked = sb.Swings = 0;
            sb.NextSampleAt = now + AiInputMath.SampleSeconds;
            sb.SampledAny = sb.BackTurnedAny = false;
            sb.First = !_stepFirstStarted;
            sb.Detail = sb.First ? new System.Text.StringBuilder() : null;
            _stepFirstStarted = true;
            _stepping.Add(st);
            _stepStats.AddStart(plan.MovementState, StepBackMath.FacingBin(plan.StartFacingCos), sr.Distance, sb.ByInput);
            if (sb.ByInput)
            {
                // the backpedal from this frame: its wish on, the first vector for his current frame, the call count
                sb.Plan.NextEdgeCheck = now + AiInputMath.EdgeCheckSeconds;
                var s = st.Input!;
                AiInputHook.SetBackpedal(st, true, sr.HoldAttacks, now);
                try
                {
                    if (body.Steer(st, ref sb.Plan, in sr, now, out float lx, out float ly) == StepBackEnd.None)
                    {
                        s.BackX = lx;
                        s.BackY = ly;
                    }
                }
                catch (Exception e)
                {
                    Failed("stepback.steer", e);
                }
                sb.CallsAtStart = s.Calls;
                NoteHooked(st, sb.Plan.Hook);
                if (sb.First)
                {
                    s.Capture = true;
                    s.CapturedFirst = s.CapturedAttack = false;
                }
            }
            // the AI timer survives the step back (step 16): a hold running now overlaps it
            if (st.Pace != null && st.Pace.Active) st.Pace.Overlapped = true;
            if (sb.First) LogFirstStart(st, sb, in sr, now);
            else if (TraxLog.VerboseWants("stepback")) LogStart(st, sb, now);
        }

        private void Refuse(TrackedAgent st, StepBackState sb, StepBackRefusal why)
        {
            sb.Pending = false;
            _stepStats.AddRefused(why);
            if (TraxLog.VerboseWants("stepback-refused"))
            {
                TraxLog.Verbose("stepback", "step back not started: " + Name(st) + " (f " + F2(sb.PendingF) + ", chance " + P0(sb.PendingChance) + ") - "
                    + StepBackStats.RefusalText(why), "stepback-refused");
            }
        }

        private void DropPending(StepBackRefusal why)
        {
            for (int i = 0; i < _stepPending.Count; i++)
            {
                var sb = _stepPending[i].StepBack;
                if (sb != null && sb.Pending) Refuse(_stepPending[i], sb, why);
            }
            _stepPending.Clear();
        }

        /// <summary>Step 16: every 0.25 s of every step back (both techniques) - his facing (a turned back at ANY sample
        /// is counted) and, for the mission's first, the distance and facing written down for its end line.</summary>
        private void SampleEvery(TrackedAgent st, StepBackState sb, double now)
        {
            sb.NextSampleAt = now + AiInputMath.SampleSeconds;
            if (!BodyOf(sb).Sample(st, in sb.Plan, out var s) || !s.Valid) return;
            int bin = StepBackMath.FacingBin(s.FacingCos);
            _stepStats.AddSample(bin);
            sb.SampledAny = true;
            if (bin == 2) sb.BackTurnedAny = true;
            var d = sb.Detail;
            if (d == null || d.Length > 1500) return;
            double back = sb.ByInput ? sb.Plan.Covered : StepBackMath.Distance2D(s.Position.x, s.Position.y, sb.Plan.From.x, sb.Plan.From.y);
            d.Append(d.Length == 0 ? "" : " | ").Append("+").Append(Sec(now - sb.StartedAt)).Append(" s ").Append(F2(back)).Append(" m, ")
             .Append(Deg(s.FacingCos)).Append(" off, ").Append(double.IsNaN(s.SpeedAway) ? "n/a" : F2(s.SpeedAway)).Append(" m/s away");
        }

        private void SampleMid(TrackedAgent st, StepBackState sb)
        {
            sb.MidSampled = true;
            if (!BodyOf(sb).Sample(st, in sb.Plan, out var s)) return;
            sb.Mid = s;
            _stepStats.AddMidSample(StepBackMath.FacingBin(s.FacingCos), StepBackMath.MotionBin(s.SpeedAway));
        }

        // ------------------------------------------------------------------ the end

        /// <summary>Ends one step back: out of the running list, released through the engine when
        /// <paramref name="native"/> (never for a removed man, never over a game job), counted, logged.</summary>
        private void Finish(TrackedAgent st, StepBackEnd why, double now, bool native)
        {
            var sb = st.StepBack;
            if (sb == null || !sb.Active) return;
            sb.Active = false;
            int slot = _stepping.IndexOf(st);
            if (slot >= 0)
            {
                int last = _stepping.Count - 1;
                _stepping[slot] = _stepping[last];
                _stepping.RemoveAt(last);
            }

            // step 16: a backpedal's wish goes off first - on EVERY path (managed only), so the callback writes nothing
            // from the next frame even when no engine call follows (he left, the game took him)
            if (sb.ByInput)
            {
                AiInputHook.SetBackpedal(st, false, false, now);
                var s = st.Input;
                if (s != null)
                {
                    if (s.Calls == sb.CallsAtStart && now - sb.StartedAt >= NoCallGraceSeconds && why != StepBackEnd.LeftField) _holdStats.StepsWithoutACall++;
                    if (sb.First) s.Capture = false;
                    DrainInputError(st);
                }
            }
            if (sb.SampledAny) _stepStats.AddSampledStep(sb.BackTurnedAny);

            var rel = default(StepBackRelease);
            if (native)
            {
                try
                {
                    rel = BodyOf(sb).Release(st, in sb.Plan);
                }
                catch (Exception e)
                {
                    Failed("stepback.release", e);
                }
                if (rel.Released && sb.ByInput)
                {
                    _stepStats.InputReleases++;
                }
                else if (rel.Released)
                {
                    _stepStats.Releases++;
                    if (rel.StillScripted) _stepStats.StillScriptedAfterRelease++;
                    if (rel.FlagsCleared) _stepStats.FlagsClearedByHand++;
                    if (why == StepBackEnd.MissionEnd && rel.StillScripted) _stepStats.StillScriptedAtMissionEnd++;
                }
            }

            double moved = double.NaN, toSpot = double.NaN;
            int endFacing = -1;
            if (rel.End.Valid)
            {
                var p = rel.End.Position;
                moved = StepBackMath.Distance2D(p.x, p.y, sb.Plan.From.x, sb.Plan.From.y);
                toSpot = StepBackMath.Distance2D(p.x, p.y, sb.Plan.Spot.x, sb.Plan.Spot.y);
                endFacing = StepBackMath.FacingBin(rel.End.FacingCos);
            }
            _stepStats.AddEnd(why, now - sb.StartedAt, moved, toSpot, endFacing);
            if (sb.First) LogFirstEnd(st, sb, why, now, in rel, moved, toSpot);
            else if (TraxLog.VerboseWants("stepback")) LogEnd(st, sb, why, now, moved, rel.End);
            sb.Plan = default; // drop the references (enemy, formation)
        }

        /// <summary>Finish that cannot throw (the error path).</summary>
        private void SafeFinish(TrackedAgent st, StepBackEnd why, double now)
        {
            try
            {
                Finish(st, why, now, native: true);
            }
            catch (Exception e)
            {
                Failed("stepback.finish", e);
                var sb = st.StepBack;
                if (sb != null) sb.Active = false;
                _stepping.Remove(st);
                StopBackpedalQuietly(st, now);
            }
        }

        /// <summary>The error paths: whatever failed, a backpedal's wish must not outlive its record (managed only).</summary>
        private static void StopBackpedalQuietly(TrackedAgent st, double now)
        {
            try
            {
                if (st.Input != null && st.Input.Backpedal) AiInputHook.SetBackpedal(st, false, false, now);
            }
            catch
            {
                // nothing more to do - the record is gone and the next hold rewrites the wishes
            }
        }

        private void FinishAll(StepBackEnd why, double now, bool native)
        {
            for (int i = _stepping.Count - 1; i >= 0; i--)
            {
                if (i >= _stepping.Count) continue;
                var st = _stepping[i];
                try
                {
                    Finish(st, why, now, native);
                }
                catch (Exception e)
                {
                    Failed("stepback.finish", e);
                    var sb = st.StepBack;
                    if (sb != null) sb.Active = false;
                    _stepping.Remove(st);
                    StopBackpedalQuietly(st, now);
                }
            }
        }

        /// <summary>He left the field (OnAgentRemoved - an engine callback): no engine call on him.</summary>
        private void StepBackLeftField(TrackedAgent st)
        {
            var sb = st.StepBack;
            if (sb == null) return;
            sb.Pending = false; // a queued start is refused as "gone" by the tick (st.Removed)
            if (sb.Active) Finish(st, StepBackEnd.LeftField, SafeNow(), native: false);
        }

        /// <summary>The mission is over (before the summary): everyone still stepping back is released -
        /// through the engine while the agents still live (<paramref name="agentsAlive"/>), else only
        /// the records are dropped (the teardown fallback: the native side is gone). Counts who was
        /// mid-step and who was OVERDUE (past his time by more than a second - the tick should have
        /// released him).</summary>
        private void CloseStepBacks(bool agentsAlive)
        {
            if (_stepClosed) return;
            _stepClosed = true;
            double now = SafeNow();
            var sr = StepBackRules.From(TraxSettings.Shared);
            _stepStats.MidStepAtMissionEnd += _stepping.Count;
            foreach (var st in _stepping)
                if (now - st.StepBack!.StartedAt > sr.Seconds + 1.0) _stepStats.OverdueAtMissionEnd++;
            FinishAll(StepBackEnd.MissionEnd, now, native: agentsAlive);
            DropPending(StepBackRefusal.Gone);
        }

        // ------------------------------------------------------------------ the guard

        /// <summary>A melee collision on a tracked fighter on foot (OnMeleeHit): blocked or landed,
        /// stepping back or not - the summary compares the two ("did he keep his guard up?").</summary>
        private void StepBackHitTaken(Agent? victim, bool isCanceled, in AttackCollisionData cd)
        {
            if (victim == null || isCanceled || cd.IsHorseCharge) return;
            var vst = Get(victim);
            if (vst == null || victim.MountAgent != null) return;
            if (!StepBackRules.From(TraxSettings.Shared).Enabled) return;
            var result = cd.CollisionResult;
            bool blocked = cd.AttackBlockedWithShield || result == CombatCollisionResult.Blocked || result == CombatCollisionResult.Parried
                           || result == CombatCollisionResult.ChamberBlocked;
            var sb = vst.StepBack;
            bool stepping = sb != null && sb.Active;
            _stepStats.AddHitTaken(stepping, blocked);
            if (stepping)
            {
                sb!.HitsTaken++;
                if (blocked) sb.HitsBlocked++;
            }
        }

        // ------------------------------------------------------------------ logs

        private void NoteStepBackSettings(in StepBackRules sr)
        {
            _stepSeenVersion = TraxSettings.Shared.Version;
            if (sr.Enabled != _stepSeenEnabled)
            {
                _stepSeenEnabled = sr.Enabled;
                if (sr.Enabled)
                    TraxLog.Info("stepback", (_stepOffBecause ?? "StepBackEnabled") + " switched ON mid-mission: tired fighters step back again from their next swing");
            }
            _stepOffBecause = sr.OffBecause;
            if (sr.Backpedal != _stepSeenBackpedal)
            {
                _stepSeenBackpedal = sr.Backpedal;
                _holdStats.StepSwitched = true;
                TraxLog.Info("stepback", "StepBackBackpedal switched " + (sr.Backpedal ? "ON" : "OFF") + " mid-mission at " + Sec(SafeNow()) + " s: new step backs are "
                    + AiInputMath.StepTechnique(sr.Backpedal) + "; the " + _stepping.Count + " running now finish the way they began");
            }
            string text = sr.Describe();
            if (text != _stepSeenText)
            {
                _stepSeenText = text;
                TraxLog.Info("stepback", "settings now: " + text);
            }
        }

        private void LogFirstStart(TrackedAgent st, StepBackState sb, in StepBackRules sr, double now)
        {
            try
            {
                var r = AthleticsRules.From(TraxSettings.Shared);
                ref var p = ref sb.Plan;
                double pool = AthleticsMath.PoolPoints(in r, st);
                TraxLog.Info("stepback", "first step back this mission: " + Name(st) + " at " + Sec(now) + " s (f " + F2(sb.PendingF)
                    + ", Athletics " + F1(st.Fraction * pool) + " of " + F0(pool) + ", chance " + P0(sb.PendingChance) + ", swing ended at " + Sec(sb.PendingAt)
                    + " s) - from " + V(p.From) + " to " + V(p.Spot) + " (" + F2(sr.Distance) + " m straight away from " + EnemyName(p.Enemy) + ", "
                    + F1(p.EnemyDistance) + " m off; asked to face him: " + F0(p.Radians * 180.0 / Math.PI) + "°); facing before: " + Deg(p.StartFacingCos)
                    + " off his enemy; " + FormationText(in p) + "; "
                    + (sb.ByInput
                        ? "technique: a BACKPEDAL through his own input (" + HookText(p.Hook) + "; the line away from him ("
                          + F2(p.DirX) + ", " + F2(p.DirY) + ") in his own frame now (" + F2(st.Input?.BackX ?? 0) + ", " + F2(st.Input?.BackY ?? 0)
                          + ") - (0, -1) = straight back; his attacks " + (sb.HoldAttacks ? "held (only the attack bits out)" : "free (StepBackHoldAttacks off)")
                          + "; scripted flags " + Flags(p.FlagsBefore) + " (none of ours))"
                        : "technique: the scripted walk; scripted flags " + Flags(p.FlagsBefore) + " → " + Flags(p.FlagsAfter)
                          + ((p.FlagsAfter & (int)Agent.AIScriptedFrameFlags.GoToPosition) != 0 ? " (GoToPosition set: the engine took it)" : " (GoToPosition NOT set)")));
            }
            catch (Exception e)
            {
                Failed("stepback.log", e);
            }
        }

        private void LogFirstEnd(TrackedAgent st, StepBackState sb, StepBackEnd why, double now, in StepBackRelease rel, double moved, double toSpot)
        {
            try
            {
                string mid = sb.MidSampled && sb.Mid.Valid
                    ? "mid-step " + StepBackMath.FacingBinName(StepBackMath.FacingBin(sb.Mid.FacingCos)) + " (" + Deg(sb.Mid.FacingCos) + " off), "
                      + StepBackMath.MotionBinName(StepBackMath.MotionBin(sb.Mid.SpeedAway)) + " (" + F2(sb.Mid.SpeedAway) + " m/s away)"
                    : "no mid-step sample";
                string end = rel.End.Valid
                    ? "now at " + V(rel.End.Position) + ", moved " + F2(moved) + " m (" + F2(toSpot) + " m from the spot), "
                      + StepBackMath.FacingBinName(StepBackMath.FacingBin(rel.End.FacingCos)) + " (" + Deg(rel.End.FacingCos) + " off, "
                      + F1(rel.End.EnemyDistance) + " m from him)"
                    : "not read (" + (why == StepBackEnd.LeftField ? "he left the field" : "no engine read") + ")";
                string release = rel.Released && sb.ByInput
                    ? "the backpedal input stopped - nothing of ours left in the engine (scripted flags " + Flags(rel.FlagsAfter) + "); his own AI and formation take him back"
                    : rel.Released
                    ? "released: scripted movement " + (rel.StillScripted ? "STILL ON - tell Claude" : "off") + ", flags now " + Flags(rel.FlagsAfter)
                      + (rel.FlagsCleared ? " (our flags had to be cleared by hand)" : "") + " - his formation takes him back"
                    : why == StepBackEnd.HandedOver ? "handed over to the game's own job - left alone"
                    : why == StepBackEnd.ClearedByGame ? "the game had already cleared it"
                    : "not released through the engine";
                string samples = sb.Detail != null && sb.Detail.Length > 0 ? "; every " + F2(AiInputMath.SampleSeconds) + " s (metres back, facing): " + sb.Detail : "; no 0.25 s sample";
                string input = sb.ByInput ? "; " + InputCaptureText(st.Input, attackWord: "an attack wish while stepping back") : string.Empty;
                TraxLog.Info("stepback", "first step back ended (" + StepBackStats.EndText(why) + ") after " + Sec(now - sb.StartedAt) + " s: " + end + "; "
                    + mid + samples + "; hits taken " + sb.HitsTaken + " (blocked " + sb.HitsBlocked + "), swings " + sb.Swings + input + "; " + release);
            }
            catch (Exception e)
            {
                Failed("stepback.log", e);
            }
        }

        private void LogStart(TrackedAgent st, StepBackState sb, double now)
        {
            try
            {
                ref var p = ref sb.Plan;
                TraxLog.Verbose("stepback", "step back: " + Name(st) + " at " + Sec(now) + " s (f " + F2(sb.PendingF) + ", chance " + P0(sb.PendingChance)
                    + ") from " + EnemyName(p.Enemy) + " " + F1(p.EnemyDistance) + " m off, facing " + Deg(p.StartFacingCos) + " off; "
                    + FormationText(in p), "stepback");
            }
            catch (Exception e)
            {
                Failed("stepback.log", e);
            }
        }

        private void LogEnd(TrackedAgent st, StepBackState sb, StepBackEnd why, double now, double moved, in StepBackSnapshot end)
        {
            try
            {
                TraxLog.Verbose("stepback", "step back ended (" + StepBackStats.EndText(why) + "): " + Name(st) + " after " + Sec(now - sb.StartedAt)
                    + " s - moved " + (double.IsNaN(moved) ? "n/a" : F2(moved) + " m") + (end.Valid ? ", " + Deg(end.FacingCos) + " off his enemy" : "")
                    + ", hits taken " + sb.HitsTaken + " (blocked " + sb.HitsBlocked + "), swings " + sb.Swings, "stepback");
            }
            catch (Exception e)
            {
                Failed("stepback.log", e);
            }
        }

        private static string FormationText(in StepBackPlan p)
        {
            if (p.Formation == null) return "no formation";
            string name;
            try
            {
                name = FormationName((int)p.Formation.FormationIndex);
            }
            catch
            {
                name = "formation";
            }
            return "formation " + name + " (" + Enum.GetName(typeof(MovementOrder.MovementOrderEnum), p.MovementOrder) + ", "
                   + Enum.GetName(typeof(ArrangementOrder.ArrangementOrderEnum), p.Arrangement) + ", "
                   + (p.MovementState == 0 ? "charging" : p.MovementState < 0 ? "?" : "holding") + ")";
        }

        private static string EnemyName(Agent? enemy)
        {
            if (enemy == null) return "(no enemy)";
            return SafeName(enemy);
        }

        private static string Flags(int flags)
        {
            if (flags == 0) return "none";
            var parts = new List<string>();
            foreach (Agent.AIScriptedFrameFlags f in Enum.GetValues(typeof(Agent.AIScriptedFrameFlags)))
                if (f != Agent.AIScriptedFrameFlags.None && (flags & (int)f) != 0) parts.Add(f.ToString());
            return string.Join("|", parts);
        }

        private static string Deg(double cosine)
        {
            double d = StepBackMath.Degrees(cosine);
            return double.IsNaN(d) ? "n/a" : d.ToString("0", CultureInfo.InvariantCulture) + "°";
        }

        private static string V(Vec3 v) =>
            "(" + v.x.ToString("0.0", CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.0", CultureInfo.InvariantCulture) + ", "
            + v.z.ToString("0.0", CultureInfo.InvariantCulture) + ")";

        private double SafeNow()
        {
            try
            {
                return Mission?.CurrentTime ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
