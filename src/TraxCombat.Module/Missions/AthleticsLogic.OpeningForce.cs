using System;
using System.Globalization;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 106b - OPENINGS: THE FORCED GUARD (AI_NOTES "Step 106" §106b, DESIGN decision 95). One record (only YOUR chain opens anything): the AI
    /// man you put OFF BALANCE gets his guard written to the balance side through step 16's input hook (<see cref="AiInputHook.SetOpening"/> - in
    /// the filter after step 82's read guard, before a kick; step 104b's reel lock stays last), his attack bits out. At YOUR follow-up's wind-up into
    /// the balance side the LIVE BLOCK ROLL (<see cref="OpeningMath.BlockChance"/> by its timing score and link, the openings' dice): he wins = his
    /// own AI guards again (and blocks as it will); he fails = the guard KEPT away from your side until the blow resolves (your contact with him, a
    /// miss) or <c>OpeningHoldMaxSeconds</c> - a shieldman from link <c>OpeningShieldMinChain</c> (or after an unguarded hit) with his guard LOWERED
    /// (every guard bit out: a raised shield blocks any side). Behind <c>OpeningsEnabled</c> (the master switch first); never you or a man you drive,
    /// never a rider. THE TEST TOOL (<c>OpeningGuardTest</c>, off; <c>OpeningGuardTestKey</c> K): the nearest AI man on foot within 8 m is forced into
    /// the next guard (low / high / left / right / lowered / lowered by the CanDefend flag) for <c>OpeningGuardTestSeconds</c>, his pose read back
    /// before, after 0.5 s and at the end - on screen and in the log. Main thread; one record, no allocation per tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>When the test tool reads his pose back the first time (s after the key).</summary>
        internal const double OpeningTestReadSeconds = 0.5;

        private readonly OpeningForceStats _force = new OpeningForceStats();
        private TrackedAgent? _forceSt;
        private OpeningForceKind _forceKind;
        private OpeningAttackLock _attackLock; // step 106 follow-up: the off-balance attack lock, at most 1 s a run
        private double _forceUntil, _forceStart;
        private uint _forceFlag;
        private bool _forceLower, _forceFlagOff, _forceRead1, _openLiveRolled, _openLiveThrough;
        private int _forceGuard = OpeningMath.Unknown, _forceTestMode = -1;
        private OpeningTestMode _forceMode;
        private OpeningPose _forceBefore;
        private bool _forceTestFirstLine;

        // step 106f: the LIVE pose read-back (one pending read at a time - the forced man's), and what a held follow-up forced (for a BLOCKED ANYWAY)
        private bool _poseReading, _poseFirstDone, _poseShield, _poseLowered;
        private int _poseGuard = OpeningMath.Unknown, _poseLines, _anywayLines;
        private double _poseStart;
        private int _heldDir = OpeningMath.Unknown, _heldGuard = OpeningMath.Unknown;
        private bool _heldLowered;

        /// <summary>Step 106f: live read-back / blocked-anyway lines at Info per mission (then verbose).</summary>
        internal const int OpeningPoseLines = 20;

        internal IOpeningBody OpeningBody { get; set; } = OpeningBodyDefault.Current;

        internal OpeningForceStats OpeningForceStats => _force;

        /// <summary>The man forced now (null none) and why (the smoke reads them).</summary>
        internal TrackedAgent? OpeningForced => _forceSt;
        internal OpeningForceKind OpeningForceKind => _forceKind;
        internal bool OpeningForceLowered => _forceSt != null && _forceLower;

        // ------------------------------------------------------------------ the live route

        /// <summary>Step 106e: the openings are LIVE - their switch (the master switch first) AND the impact system can carry the stagger
        /// (<see cref="OpeningMath.ImpactCarries"/>, the ForcedStagger lever not lost to an error). Else shadow: nothing forced, nothing flashed.</summary>
        private bool OpeningsLive(in OpeningRules r) =>
            r.Enabled && OpeningMath.ImpactCarries(ImpactRules.From(TraxSettings.Shared)) && !_live.Disabled(ImpactLever.ForcedStagger);

        /// <summary>From <see cref="BeginFollowUp"/>: he is OFF BALANCE - with the live switch on, his guard goes to the balance side until your
        /// wind-up (at most the off-balance time). Its own try.</summary>
        private void ForceBalance(TrackedAgent v, in OpeningPlan plan, double now)
        {
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!plan.Opens || !OpeningsLive(in r)) return;
                if (_forceKind == OpeningForceKind.Test && ReferenceEquals(_forceSt, v)) return; // the test tool has him
                _openLiveRolled = false;
                // step 106 follow-up (Toni 2026-11-09): the attack lock at most 1 s a run - spamming swings into his guard no longer freezes him
                if (_forceSt != null && !ReferenceEquals(_forceSt, v)) EndForce(OpeningForceEnd.ChainEnded, now, true); // the old man's run ends first
                if (!_attackLock.Begin(v.AgentIndex, now, r.ChainReset, out double until))
                {
                    _force.LockSpent++;
                    if (ReferenceEquals(_forceSt, v) && _forceKind == OpeningForceKind.Balance) EndForce(OpeningForceEnd.Timeout, now, true);
                    return;
                }
                if (until < now + r.ChainReset - 1e-9) _force.LockCapped++;
                if (!StartForce(v, OpeningForceKind.Balance, plan.Guard, false, until, now))
                {
                    _attackLock.End(now);
                    return;
                }
                _force.Balance++;
            }
            catch (Exception e)
            {
                ForceFailed(e, now);
            }
        }

        /// <summary>From <see cref="JudgeFollowUp"/> once your follow-up's direction is COMMITTED (step 106f: its release, was its wind-up): THE
        /// LIVE BLOCK ROLL. Until then his guard stays forced to the balance side (the pose shown). Its own try.</summary>
        private string ForceAtCommit(TrackedAgent you, double now)
        {
            _openLiveRolled = false;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (_forceKind != OpeningForceKind.Balance || _forceSt == null || _forceSt.AgentIndex != _openTarget || !OpeningsLive(in r)) return string.Empty;
                var v = _forceSt;
                if (_openVerdict != OpeningVerdict.Opened)
                {
                    _force.WrongSide++;
                    EndForce(OpeningForceEnd.WrongSide, now, true);
                    return " - LIVE: another side, his own guard back";
                }
                double chance = OpeningMath.BlockChance(in r, _openScore, _openPlan.K, _openPlan.ShieldHolds);
                double die = OpeningDice.NextDouble();
                _openLiveRolled = true;
                _openLiveThrough = OpeningForceMath.Through(die, chance);
                if (!_openLiveThrough)
                {
                    _force.RollsBlocked++;
                    EndForce(OpeningForceEnd.HeBlocks, now, true);
                    return " - LIVE: he wins the block roll (" + Pct0(chance) + "), his own guard back";
                }
                _force.RollsThrough++;
                bool shield = _openPlan.Shield || SafeShield(v);
                bool lower = OpeningForceMath.Lowers(shield, in _openPlan);
                if (!StartForce(v, OpeningForceKind.Held, _openPlan.Guard, lower, OpeningMath.HoldEnds(in r, now), now)) return string.Empty;
                _force.Held++;
                _heldDir = _openDir;
                _heldGuard = _openPlan.Guard;
                _heldLowered = lower;
                // step 106f: the game's own "can defend" flag off too for the blow's flight (given back by EndForce on every path)
                string flag = string.Empty;
                if (TraxSettings.Shared.OpeningHoldTakesDefend && !_forceFlagOff && OpeningBody.CanDefend(v))
                {
                    OpeningBody.SetCanDefend(v, false);
                    _forceFlagOff = true;
                    _force.FlagsOff++;
                    _force.HoldFlagsOff++;
                    flag = ", his 'can defend' flag off until the blow";
                }
                if (lower)
                {
                    _force.ShieldLowerings++;
                    if (shield) NoteGuardBroken(); // step 106d: "GUARD BROKEN" on screen - his shield comes down for your follow-up
                }
                return " - LIVE: he FAILS the block roll (" + Pct0(chance) + "): his guard held at the " + OpeningMath.GuardName(_openPlan.Guard)
                       + (lower ? " with his " + (shield ? "shield" : "guard") + " LOWERED" : string.Empty) + flag + " until the blow (at most " + F2(r.HoldMax) + " s)";
            }
            catch (Exception e)
            {
                ForceFailed(e, now);
                return string.Empty;
            }
        }

        private bool SafeShield(TrackedAgent v)
        {
            try
            {
                return AnimBody.ShieldInHand(v);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Your blow met him (OnMeleeHit) or a swing missed: a HELD guard is let go (the blow resolved); <paramref name="landed"/> counted.</summary>
        private void ForceBlowResolved(int victim, bool landed, double now)
        {
            if (_forceKind != OpeningForceKind.Held || _forceSt == null) return;
            if (victim >= 0 && _forceSt.AgentIndex != victim) return;
            if (landed) _force.HeldLanded++;
            EndForce(OpeningForceEnd.Resolved, now, true);
        }

        /// <summary>From the block seam: he blocked your follow-up - after a LIVE roll he lost, that is "blocked ANYWAY" (the force did not hold him).
        /// Step 106f: its CAUSE read at the block - <paramref name="contact"/> your swing's collision direction, <paramref name="shieldBlock"/> his
        /// shield stopped it, <paramref name="guardAtContact"/> his channel-1 guard - counted and logged (Info, its own budget).</summary>
        private string ForceBlockedText(TrackedAgent v, int contact, bool shieldBlock, int guardAtContact, double now)
        {
            if (!_openLiveRolled) return string.Empty;
            _openLiveRolled = false;
            if (!_openLiveThrough) return " - the LIVE roll at your follow-up gave him the block";
            var cause = OpeningForceMath.AnywayCause(_heldDir, contact, shieldBlock, _heldLowered, guardAtContact, _heldGuard);
            _force.NoteAnyway(cause);
            string why = " - BLOCKED ANYWAY: the live roll said through and his guard was forced - cause: " + OpeningForceMath.AnywayName(cause) + " (judged "
                         + OpeningMath.AttackName(_heldDir) + ", at contact " + OpeningMath.AttackName(contact) + "; forced the " + OpeningMath.GuardName(_heldGuard)
                         + (_heldLowered ? " LOWERED" : string.Empty) + ", his guard at contact " + OpeningMath.GuardName(guardAtContact) + (shieldBlock ? ", his SHIELD" : ", a weapon")
                         + ")";
            ForceInfoLine(ref _anywayLines, "openings: " + Name(v) + why);
            return why;
        }

        private static void ForceInfoLine(ref int budget, string text)
        {
            if (budget < OpeningPoseLines)
            {
                budget++;
                TraxLog.Info("impact", text + (budget == OpeningPoseLines ? " (the last such line at Info this battle - the rest verbose)" : string.Empty));
            }
            else if (TraxLog.VerboseWants("openings")) TraxLog.Verbose("impact", text, "openings");
        }

        // ------------------------------------------------------------------ step 106f: the live pose read back

        /// <summary>A live force began / changed: the pose to read back (a held guard that keeps the balance pose keeps the read already running).</summary>
        private void BeginPoseRead(TrackedAgent st, OpeningForceKind kind, int guard, bool lower, double now)
        {
            if (kind == OpeningForceKind.Test) return;
            if (_poseReading && !lower && !_poseLowered && guard == _poseGuard) return; // the same pose goes on
            if (_poseReading) FinishPose(OpeningPoseResult.EndedFirst, st, null);
            _poseReading = true;
            _poseFirstDone = false;
            _poseGuard = guard;
            _poseLowered = lower;
            _poseShield = SafeShield(st);
            _poseStart = now;
        }

        /// <summary>From <see cref="TickOpeningForce"/>: the reads at 0.2 s and (when not shown yet) 0.5 s.</summary>
        private void TickPoseRead(TrackedAgent st, double now)
        {
            if (!_poseReading) return;
            bool first = !_poseFirstDone;
            double at = _poseStart + (first ? OpeningForceMath.PoseFirstReadSeconds : OpeningForceMath.PoseLastReadSeconds);
            if (now < at - 1e-9) return;
            var p = OpeningBody.Pose(st);
            _poseFirstDone = true;
            if (!p.Read)
            {
                if (!first) FinishPose(OpeningPoseResult.EndedFirst, st, null);
                return;
            }
            var verdict = OpeningForceMath.PoseVerdict(_poseGuard, _poseLowered, p.Direction, p.Defending, first);
            if (verdict.HasValue) FinishPose(verdict.Value, st, p);
        }

        private void FinishPose(OpeningPoseResult result, TrackedAgent st, OpeningPose? p)
        {
            _poseReading = false;
            _force.NoteLivePose(result, _poseShield, _poseLowered);
            string what = _poseLowered ? "his guard LOWERED" : "his " + (_poseShield ? "shield" : "guard") + " to the " + OpeningMath.GuardName(_poseGuard);
            ForceInfoLine(ref _poseLines, "openings: the balance pose read back - " + Name(st) + ": " + what + " -> " + OpeningForceMath.PoseName(result)
                                          + (p.HasValue ? " (" + PoseText(p.Value) + ")" : string.Empty));
        }

        // ------------------------------------------------------------------ the record

        private bool StartForce(TrackedAgent st, OpeningForceKind kind, int guard, bool lower, double until, double now)
        {
            var body = OpeningBody;
            if (st.Removed || !body.OnFoot(st) || !body.AiDriven(st))
            {
                _force.NotAi++;
                return false;
            }
            if (_forceSt != null && !ReferenceEquals(_forceSt, st)) EndForce(OpeningForceEnd.ChainEnded, now, true);
            if (_forceFlagOff && ReferenceEquals(_forceSt, st) && !(kind == OpeningForceKind.Test && _forceMode == OpeningTestMode.LoweredByFlag)) GiveFlagBack(st);
            EnsureInput(st);
            AiInputHook.SetOpening(st, true, OpeningForceMath.GuardFlag(guard), lower, now);
            NoteHooked(st, body.Hook(st));
            _forceSt = st;
            _forceKind = kind;
            _forceGuard = guard;
            _forceFlag = OpeningForceMath.GuardFlag(guard);
            _forceLower = lower;
            _forceUntil = until;
            _forceStart = now;
            BeginPoseRead(st, kind, guard, lower, now); // step 106f: read back at 0.2 s / 0.5 s
            return true;
        }

        /// <summary>The force let go: his input wish off (the frames summed), the CanDefend flag given back, the callback released when idle.
        /// <paramref name="engine"/> false = no engine call (he left the field).</summary>
        private void EndForce(OpeningForceEnd why, double now, bool engine)
        {
            var st = _forceSt;
            if (st == null) return;
            _forceSt = null;
            var kind = _forceKind;
            _forceKind = OpeningForceKind.None;
            if (kind != OpeningForceKind.Test) _attackLock.End(now); // step 106 follow-up: his free time counts from here
            if (_poseReading) FinishPose(OpeningPoseResult.EndedFirst, st, null); // step 106f: let go before the last read
            _force.NoteEnd(why);
            try
            {
                var s = st.Input;
                if (s != null)
                {
                    _force.NoteFrames(s.OpeningFrames, s.OpeningAttacksOut, s.OpeningGuardsWritten, s.OpeningGuardsCleared, s.OpeningLoweredFrames);
                    s.OpeningFrames = s.OpeningAttacksOut = s.OpeningGuardsWritten = s.OpeningGuardsCleared = s.OpeningLoweredFrames = 0;
                }
                AiInputHook.SetOpening(st, false, 0u, false, now);
                if (kind == OpeningForceKind.Test && engine && !st.Removed) TestReadBack(st, "at the end", now);
                if (_forceFlagOff)
                {
                    if (engine && !st.Removed) GiveFlagBack(st);
                    else _forceFlagOff = false;
                }
                if (engine && !st.Removed) OpeningBody.Release(st);
            }
            catch (Exception e)
            {
                _force.Errors++;
                if (_force.Errors == 1) Failed("impact.opening-force", e);
            }
        }

        private void GiveFlagBack(TrackedAgent st)
        {
            _forceFlagOff = false;
            OpeningBody.SetCanDefend(st, true);
            _force.FlagsBack++;
        }

        /// <summary>From <see cref="TickOpenings"/>: the switches (the master switch first; the live switch for a live force, the test tool for its
        /// own), the man gone, the time, the chain moved on.</summary>
        private void TickOpeningForce(double now)
        {
            TickOpeningTest(now);
            var st = _forceSt;
            if (st == null) return;
            var r = OpeningRules.From(TraxSettings.Shared);
            if (st.Removed)
            {
                EndForce(OpeningForceEnd.Off, now, false);
                return;
            }
            bool on = _forceKind == OpeningForceKind.Test ? r.ModEnabled && TraxSettings.Shared.OpeningGuardTest : OpeningsLive(in r); // step 106e: the impact system too
            if (!on)
            {
                EndForce(OpeningForceEnd.Off, now, true);
                return;
            }
            if (!OpeningBody.OnFoot(st) || !OpeningBody.AiDriven(st))
            {
                EndForce(OpeningForceEnd.Off, now, true);
                return;
            }
            if (_forceKind != OpeningForceKind.Test) TickPoseRead(st, now); // step 106f: the live pose read back
            if (_forceKind == OpeningForceKind.Test)
            {
                if (!_forceRead1 && now >= _forceStart + Math.Min(OpeningTestReadSeconds, (_forceUntil - _forceStart) * 0.5))
                {
                    _forceRead1 = true;
                    TestReadBack(st, "after " + F2(now - _forceStart) + " s", now);
                }
                if (now >= _forceUntil) EndForce(OpeningForceEnd.Timeout, now, true);
                return;
            }
            if (now >= _forceUntil)
            {
                EndForce(OpeningForceEnd.Timeout, now, true);
                return;
            }
            // step 106f: the balance pose is held through your wind-up until your follow-up COMMITS (then the roll keeps it or gives his guard back)
            if (_forceKind == OpeningForceKind.Balance && (!_openPending || _openJudged || _openTarget != st.AgentIndex || !_openChain.Active))
                EndForce(_openChain.Phase == OpeningPhase.Reeling ? OpeningForceEnd.Reels : OpeningForceEnd.ChainEnded, now, true);
            else if (_forceKind == OpeningForceKind.Held && _openChain.Phase == OpeningPhase.Reeling) EndForce(OpeningForceEnd.Reels, now, true);
        }

        private void ForceFailed(Exception e, double now)
        {
            _force.Errors++;
            if (_force.Errors == 1) Failed("impact.opening-force", e);
            EndForce(OpeningForceEnd.Off, now, true);
        }

        /// <summary>The mission's end (before the summary): the force let go, the flag given back.</summary>
        private void CloseOpeningForce(double now) => EndForce(OpeningForceEnd.Off, now, true);

        private void WriteOpeningForceSummary() =>
            TraxLog.Info("summary", _force.SummaryLine(OpeningRules.From(TraxSettings.Shared), TraxSettings.Shared.OpeningGuardTest));

        // ------------------------------------------------------------------ the test tool

        private void TickOpeningTest(double now)
        {
            var settings = TraxSettings.Shared;
            if (!settings.ModEnabled || !settings.OpeningGuardTest) return;
            if (!AnimBody.KeyPressed(settings.OpeningGuardTestKey, out bool back)) return;
            try
            {
                var you = PlayerRecord();
                if (you == null || you.Removed) return;
                var near = OpeningBody.NearestFoe(you.Agent, TestNearestMetres);
                var st = near != null ? Get(near) : null;
                if (st == null || ReferenceEquals(st, you) || st.Removed || !OpeningBody.OnFoot(st) || !OpeningBody.AiDriven(st))
                {
                    AnimBody.Say("[guard test] nobody to force - stand within " + TestNearestMetres.ToString("0", CultureInfo.InvariantCulture) + " m of an enemy on foot");
                    TraxLog.Info("impact", "guard test: nobody to force (no enemy on foot within " + TestNearestMetres.ToString("0", CultureInfo.InvariantCulture) + " m)");
                    return;
                }
                if (!_forceTestFirstLine)
                {
                    _forceTestFirstLine = true;
                    TraxLog.Info("impact", "guard test tool ON (OpeningGuardTest): " + settings.OpeningGuardTestKey + " = the next guard on the nearest AI man on foot, Left Shift + "
                        + settings.OpeningGuardTestKey + " = the one before; low / high / left / right / lowered / lowered by the CanDefend flag, held "
                        + F2(settings.OpeningGuardTestSeconds) + " s; his pose read back before, after " + F2(OpeningTestReadSeconds) + " s and at the end");
                }
                var mode = OpeningForceMath.Next(_forceTestMode, back);
                _forceTestMode = (int)mode;
                if (_forceSt != null) EndForce(OpeningForceEnd.ChainEnded, now, true);
                _forceBefore = OpeningBody.Pose(st);
                int guard = OpeningForceMath.TestGuard(mode);
                bool lower = OpeningForceMath.TestLowers(mode);
                _forceMode = mode;
                double secs = Math.Max(0.1, settings.OpeningGuardTestSeconds);
                if (!StartForce(st, OpeningForceKind.Test, guard, lower, now + secs, now)) return;
                _forceRead1 = false;
                _force.Tests++;
                // step 106c: the red arrow flashes the side this guard leaves open, as an opening would (the tool shows it with the openings off)
                var orules = OpeningRules.From(settings);
                int flash = OpeningArrowMath.TestFlashAttack(guard, orules.SidesCrossed);
                NoteArrowFlash(flash, now + orules.FlashDelay, true);
                if (mode == OpeningTestMode.LoweredByFlag && OpeningBody.CanDefend(st))
                {
                    OpeningBody.SetCanDefend(st, false);
                    _forceFlagOff = true;
                    _force.FlagsOff++;
                }
                string line = "[guard test " + ((int)mode + 1).ToString(CultureInfo.InvariantCulture) + "/" + ((int)OpeningTestMode.Count).ToString(CultureInfo.InvariantCulture) + "] "
                              + Name(st) + " (" + (SafeShield(st) ? "shield" : "no shield") + ") - " + OpeningForceMath.TestName(mode) + " for " + F2(secs) + " s; before: " + PoseText(in _forceBefore)
                              + (OpeningMath.IsAttack(flash) ? "; the red arrow flashes " + OpeningMath.ArrowName(OpeningMath.ArrowFor(flash)) + " at " + F2(orules.FlashDelay) + " s" : string.Empty);
                AnimBody.Say(line);
                TraxLog.Info("impact", "guard test: " + line);
            }
            catch (Exception e)
            {
                ForceFailed(e, now);
            }
        }

        /// <summary>The test's read-back: his pose now against the wanted guard and against his pose before the key.</summary>
        private void TestReadBack(TrackedAgent st, string when, double now)
        {
            var p = OpeningBody.Pose(st);
            if (!p.Read) return;
            bool lowered = OpeningForceMath.TestLowers(_forceMode);
            bool matched = OpeningForceMath.PoseMatches(_forceGuard, lowered, p.Direction, p.Defending);
            bool changed = p.Direction != _forceBefore.Direction || p.Defending != _forceBefore.Defending || !string.Equals(p.Action, _forceBefore.Action, StringComparison.Ordinal);
            _force.NotePose(matched, changed);
            string line = "[guard test] " + Name(st) + " " + when + ": " + PoseText(in p) + " -> " + (matched ? "AS WANTED" : "NOT as wanted") + " ("
                          + OpeningForceMath.TestName(_forceMode) + ")" + (changed ? ", changed from before" : ", SAME as before");
            AnimBody.Say(line);
            TraxLog.Info("impact", "guard test: " + line);
        }

        private static string PoseText(in OpeningPose p)
        {
            if (!p.Read) return "unread";
            return (string.IsNullOrEmpty(p.Action) ? "no action" : p.Action) + ", direction " + DirectionName(p.Direction) + (p.Defending ? ", DEFENDING" : ", not defending")
                   + ", defend flag " + (p.DefendFlag == 0 ? "none" : "0x" + p.DefendFlag.ToString("X", CultureInfo.InvariantCulture));
        }

        private static string DirectionName(int d) => OpeningMath.IsGuard(d) ? OpeningMath.GuardName(d) : OpeningMath.IsAttack(d) ? OpeningMath.AttackName(d) : "none";
    }
}
