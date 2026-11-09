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
        private double _forceUntil, _forceStart;
        private uint _forceFlag;
        private bool _forceLower, _forceFlagOff, _forceRead1, _openLiveRolled, _openLiveThrough;
        private int _forceGuard = OpeningMath.Unknown, _forceTestMode = -1;
        private OpeningTestMode _forceMode;
        private OpeningPose _forceBefore;
        private bool _forceTestFirstLine;

        internal IOpeningBody OpeningBody { get; set; } = OpeningBodyDefault.Current;

        internal OpeningForceStats OpeningForceStats => _force;

        /// <summary>The man forced now (null none) and why (the smoke reads them).</summary>
        internal TrackedAgent? OpeningForced => _forceSt;
        internal OpeningForceKind OpeningForceKind => _forceKind;
        internal bool OpeningForceLowered => _forceSt != null && _forceLower;

        // ------------------------------------------------------------------ the live route

        /// <summary>From <see cref="BeginFollowUp"/>: he is OFF BALANCE - with the live switch on, his guard goes to the balance side until your
        /// wind-up (at most the off-balance time). Its own try.</summary>
        private void ForceBalance(TrackedAgent v, in OpeningPlan plan, double now)
        {
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.Enabled || !plan.Opens) return;
                if (_forceKind == OpeningForceKind.Test && ReferenceEquals(_forceSt, v)) return; // the test tool has him
                _openLiveRolled = false;
                if (!StartForce(v, OpeningForceKind.Balance, plan.Guard, false, now + r.ChainReset, now)) return;
                _force.Balance++;
            }
            catch (Exception e)
            {
                ForceFailed(e, now);
            }
        }

        /// <summary>From <see cref="OpeningWindUp"/> after the verdict and the score: THE LIVE BLOCK ROLL. Its own try.</summary>
        private string ForceAtWindUp(TrackedAgent you, double now)
        {
            _openLiveRolled = false;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.Enabled || _forceKind != OpeningForceKind.Balance || _forceSt == null || _forceSt.AgentIndex != _openTarget) return string.Empty;
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
                if (lower) _force.ShieldLowerings++;
                return " - LIVE: he FAILS the block roll (" + Pct0(chance) + "): his guard held at the " + OpeningMath.GuardName(_openPlan.Guard)
                       + (lower ? " with his " + (shield ? "shield" : "guard") + " LOWERED" : string.Empty) + " until the blow (at most " + F2(r.HoldMax) + " s)";
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

        /// <summary>From the block seam: he blocked your follow-up - after a LIVE roll he lost, that is "blocked ANYWAY" (the force did not hold him).</summary>
        private string ForceBlockedText(double now)
        {
            if (!_openLiveRolled) return string.Empty;
            _openLiveRolled = false;
            if (!_openLiveThrough) return " - the LIVE roll at your wind-up gave him the block";
            _force.BlockedAnyway++;
            return " - BLOCKED ANYWAY: the live roll at your wind-up said through and his guard was forced (an engine finding - PLAYTEST D78)";
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
            bool on = _forceKind == OpeningForceKind.Test ? r.ModEnabled && TraxSettings.Shared.OpeningGuardTest : r.Enabled;
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
            if (_forceKind == OpeningForceKind.Balance && (!_openPending || _openWound || _openTarget != st.AgentIndex || !_openChain.Active))
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
