using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// YOUR no-attack timer (step 13, DESIGN §2 PAUSE ONLY; research in AI_NOTES "Step 13"). After each
    /// of your attacks below the peak line (D = its wind-up + release, ranged + its reload; m when it
    /// ends) your attack button does nothing for D × (1/m − 1); held, it starts the next attack the
    /// moment the pause ends. Blocking, kicks, moving and weapon switches are never touched.
    ///
    /// HOW: <see cref="PlayerAttackGate"/> sits FIRST in the mission's behaviour list, so it pre-ticks
    /// last - right after MissionMainAgentController wrote this frame's input into your agent's
    /// MovementFlags, before the engine reads them - and asks <see cref="GatePlayerInput"/>, which clears
    /// ONLY the attack bits (AttackMask) while your hold is on. The hold begins at your release's START
    /// when its pause is sure to be worth it (a click during your own swing cannot chain a blow past it)
    /// and its countdown at the attack's end; the pure state is Core's <see cref="PlayerAttackTimer"/>.
    /// Never while a ready runs (bits vanishing mid-ready = the release). Fail safe: an exception
    /// releases the hold (vanilla input) and logs [error].
    ///
    /// LOGS ([athletics] YOU …): the first pause in full (D, m, the pause, where the hold began), its end
    /// (the button held or not) and the first time a held button fired; later pauses, their ends and
    /// swallowed presses rate-limited (buckets athletics-timer, athletics-press); releases (switched off,
    /// not you). The summary's "your timer" line counts all of it.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private const uint AttackMaskBits = (uint)Agent.MovementControlFlag.AttackMask;

        /// <summary>A countdown the gate has not ended this long after its end is ended by the tick (the
        /// gate is not running - another mod reordered the behaviours?) - the recovery bar must never
        /// stick. Plumbing, counted in the log.</summary>
        private const double GateMissingSeconds = 0.25;

        private readonly PlayerAttackTimer _playerTimer = new PlayerAttackTimer();
        private TrackedAgent? _playerTimerOwner;
        private PlayerAttackGate? _gate;
        private bool _firstPlayerTimerLogged;
        private bool _firstPlayerEndLogged;
        private bool _firstPressLogged;
        private bool _firstFiredLogged;
        private bool _firedPending;
        private bool _gateOrderChecked;
        private int _gateMissedEnds;

        /// <summary>The smoke's stand-in for Mission.MainAgent (null offline, and always null in game).</summary>
        internal Agent? SmokePlayer { get; set; }

        /// <summary>Your timer's state (the offline smoke and the HUD read it).</summary>
        internal PlayerAttackTimer PlayerTimer => _playerTimer;

        /// <summary>The gate attached for this mission (null until SubModule attached it).</summary>
        internal PlayerAttackGate? Gate
        {
            get => _gate;
            set => _gate = value;
        }

        /// <summary>This fighter is the one you control.</summary>
        internal bool IsPlayer(TrackedAgent st) => st.Agent.IsMainAgent || (SmokePlayer != null && ReferenceEquals(st.Agent, SmokePlayer));

        // ------------------------------------------------------------------ the attack's start and end

        /// <summary>
        /// Your release began (a swing after its charge, a loose, a throw): when the pause it will leave is
        /// sure to be worth it - (this wind-up + your last rest of this kind) × (1/m − 1) ≥ the minimum -
        /// the hold begins NOW, so a press during your own swing never chains a blow past the pause.
        /// </summary>
        private void PlayerAttackStarting(TrackedAgent st, double now, AttackKind kind, in AthleticsRules r)
        {
            if (!IsPlayer(st)) return;
            try
            {
                if (!AttackRateRules.From(TraxSettings.Shared).PlayerTimerOn) return;
                float m = AthleticsMath.AttackSpeedMultiplier(in r, st);
                double rest = kind == AttackKind.Melee ? st.LastRestMelee : st.LastRestRanged;
                double expected = AttackTimerMath.Pause(st.AttackDuration + Math.Max(0, rest), m);
                if (_playerTimer.AttackStarted(now, kind, expected))
                {
                    _playerTimerOwner = st;
                    _rateStats.PlayerEarlyHolds++;
                }
            }
            catch (Exception e)
            {
                Failed("rate.player-start", e);
            }
        }

        /// <summary>Your attack ended: the countdown D × (1/m − 1) starts - never when the next ready has
        /// already begun (a chained blow: clearing the bits in a ready would release it).</summary>
        private void PlayerAttackEnded(TrackedAgent st, double now, AttackKind kind, double d, float m, int bin, int next)
        {
            var rr = AttackRateRules.From(TraxSettings.Shared);
            if (!rr.PlayerTimerOn)
            {
                ReleasePlayerTimer(PlayerTimerEnd.SwitchedOff, now);
                return;
            }
            if (IsAttackAction(next))
            {
                // chained (the hold was not on during the swing): no countdown can start on a readied blow
                _playerTimer.Release();
                return;
            }
            bool earlyHold = _playerTimer.InAttack;
            bool started = _playerTimer.AttackEnded(now, kind, d, m, out bool endedEarly);
            _playerTimerOwner = st;
            if (endedEarly) _rateStats.PlayerEarlyHoldsTooShort++;
            if (!started) return;
            double pause = _playerTimer.Pause;
            _rateStats.AddTimer(kind, true, bin, d, m, pause);
            st.TimerPending = true;
            st.TimerStart = now;
            st.TimerAsked = pause;
            st.TimerM = m;
            st.TimerBin = bin;
            st.TimerKind = kind;
            _firedPending = false;

            if (!_firstPlayerTimerLogged)
            {
                _firstPlayerTimerLogged = true;
                TraxLog.Info("athletics", "YOU: first attack pause this battle at " + Sec(now) + " s - your " + (kind == AttackKind.Melee ? "melee" : "ranged") + " attack (wind-up "
                    + F2(st.CurrentWindUp) + " + " + (kind == AttackKind.Melee ? "swing " : "loose and reload ") + F2(Math.Max(0, d - st.CurrentWindUp)) + " = D " + F2(d)
                    + " s) ended at attack speed x" + F2(m) + " (f " + F2(AthleticsMath.PeakShare(Rules, st)) + ") → no new attack for " + F2(pause)
                    + " s = D x (1/m - 1), until " + Sec(_playerTimer.TimerEnd) + " s; the hold began " + (earlyHold ? "at your release's start (expected " + F2(_playerTimer.ExpectedPause) + " s)" : "at the attack's end")
                    + "; your attack button does nothing until then (held, it attacks the moment it ends); blocking, kicks, moving and weapon switches stay free; the Attack recovery bar empties and refills over it");
            }
            else
            {
                TraxLog.Limited("athletics", "YOU: attack pause " + F2(pause) + " s at " + Sec(now) + " s (D " + F2(d) + " s at x" + F2(m) + ", " + (kind == AttackKind.Melee ? "melee" : "ranged")
                    + ") until " + Sec(_playerTimer.TimerEnd) + " s", "athletics-timer");
            }
        }

        /// <summary>One of your attacks began (a ready, or a release no ready was seen for): while a hold
        /// is on it is one the gate missed - counted, the hold released (a readied blow must never lose its
        /// bits); after a countdown that ended with the button held, how soon it came.</summary>
        private void PlayerAttackBegan(TrackedAgent st, double now)
        {
            if (_playerTimer.Holding && ReferenceEquals(_playerTimerOwner, st))
            {
                _rateStats.PlayerStartedAnyway++;
                TraxLog.Limited("athletics", "YOU: an attack began at " + Sec(now) + " s while your pause held (" + F2(_playerTimer.Remaining(now))
                    + " s left) - the input gate did not stop it; released (tell Claude: the gate's order or the engine's input timing)", "athletics-press");
                _playerTimer.Release();
            }
            if (!_firedPending) return;
            _firedPending = false;
            double after = now - _playerTimer.EndedAt;
            _rateStats.AddFiredHeld(after);
            if (_firstFiredLogged) return;
            _firstFiredLogged = true;
            TraxLog.Info("athletics", "YOU: your held attack button started the next attack at " + Sec(now) + " s, " + F2(after)
                + " s after the pause ended (hold-to-attack: near 0 = it fires the moment the recovery bar is full)");
        }

        /// <summary>He left the field: his timer ends without an engine call.</summary>
        private void PlayerTimerLeftField(TrackedAgent st)
        {
            if (ReferenceEquals(_playerTimerOwner, st)) ReleasePlayerTimer(PlayerTimerEnd.NotYou, SafeNow());
        }

        // ------------------------------------------------------------------ every frame: the gate

        /// <summary>
        /// The gate's pre-tick (<see cref="PlayerAttackGate"/>, right after the player controller wrote
        /// this frame's input): while your hold is on, clear the attack bits of your MovementFlags. No
        /// native call at all while no hold runs. Never throws: an exception releases the hold.
        /// </summary>
        internal void GatePlayerInput()
        {
            if (!_playerTimer.Holding) return;
            try
            {
                var mission = Mission;
                var a = mission?.MainAgent;
                double now = mission?.CurrentTime ?? 0;
                var st = _playerTimerOwner;
                if (st == null || st.Removed || a == null || !ReferenceEquals(a, st.Agent))
                {
                    ReleasePlayerTimer(PlayerTimerEnd.NotYou, now);
                    return;
                }
                if (!AttackRateRules.From(TraxSettings.Shared).PlayerTimerOn)
                {
                    ReleasePlayerTimer(PlayerTimerEnd.SwitchedOff, now);
                    return;
                }
                if (!a.IsActive() || a.IsAIControlled) return; // not driven by the controller now
                if (IsReadyAction(st.PrevAction))
                {
                    // a ready runs (the poll saw it): bits vanishing now would release his blow - let go
                    PlayerAttackBegan(st, now);
                    return;
                }
                CheckGateOrderOnce(mission!);
                uint flags = (uint)a.MovementFlags;
                var f = GateFrame(st, now, (flags & AttackMaskBits) != 0);
                if (f.Clear) a.MovementFlags = (Agent.MovementControlFlag)(flags & ~AttackMaskBits);
            }
            catch (Exception e)
            {
                Failed("rate.player-gate", e);
                try { _playerTimer.Release(); } catch { /* the fallback must not fail */ }
            }
        }

        /// <summary>One frame's decision (managed - the offline smoke drives it): the countdown's end, a
        /// new press swallowed (counted, logged, the flash), the answer "clear the bits".</summary>
        internal PlayerGateFrame GateFrame(TrackedAgent st, double now, bool pressing)
        {
            var f = _playerTimer.Frame(now, pressing);
            if (f.Ended) PlayerCountdownEnded(now, f.HeldAtEnd);
            if (!f.Swallowed) return f;
            if (f.InAttack) _rateStats.SwallowedInAttack++;
            else _rateStats.SwallowedInTimer++;
            bool flash = f.FlashStarted && TraxSettings.Shared.FlashBarOnEarlyAttack;
            if (flash) _rateStats.Flashes++;
            string where = f.InAttack ? "during your own attack (no chained blow below the peak line)" : "with " + F2(_playerTimer.Remaining(now)) + " s of your pause left";
            if (!_firstPressLogged)
            {
                _firstPressLogged = true;
                TraxLog.Info("athletics", "YOU: attack pressed at " + Sec(now) + " s " + where + " - swallowed: the engine never saw it (no wind-up)"
                    + (flash ? "; the Attack recovery bar flashes" : TraxSettings.Shared.FlashBarOnEarlyAttack ? "" : " (FlashBarOnEarlyAttack off: no flash)")
                    + "; keep it held and the attack starts the moment the pause ends");
            }
            else
            {
                TraxLog.Limited("athletics", "YOU: attack pressed early at " + Sec(now) + " s " + where + " - swallowed" + (flash ? ", bar flashed" : string.Empty), "athletics-press");
            }
            return f;
        }

        private void PlayerCountdownEnded(double now, bool held)
        {
            _firedPending = held;
            if (!_firstPlayerEndLogged)
            {
                _firstPlayerEndLogged = true;
                TraxLog.Info("athletics", "YOU: first attack pause ended at " + Sec(now) + " s after " + F2(now - _playerTimer.TimerStart) + " s (asked " + F2(_playerTimer.Pause) + " s) - "
                    + (held ? "your attack button was held: the next attack starts now" : "your attack button was not held: attack whenever you like")
                    + "; presses swallowed during it: " + _playerTimer.SwallowedThisHold);
            }
            else
            {
                TraxLog.Limited("athletics", "YOU: attack pause ended at " + Sec(now) + " s" + (held ? " - button held, attacking now" : string.Empty), "athletics-timer");
            }
        }

        /// <summary>Once, at the first frame the gate holds: is it still pre-ticking AFTER the player
        /// controller (a lower index than it - the loop runs from the end)? Logged either way.</summary>
        private void CheckGateOrderOnce(Mission mission)
        {
            if (_gateOrderChecked) return;
            _gateOrderChecked = true;
            var list = mission.MissionBehaviors;
            int gate = _gate != null ? list.IndexOf(_gate) : -1;
            int controller = list.FindIndex(b => b is TaleWorlds.MountAndBlade.View.MissionViews.MissionMainAgentController);
            if (gate >= 0 && controller > gate)
                TraxLog.Info("rate", "your attack gate holds for the first time: it pre-ticks after MissionMainAgentController (gate " + gate + ", controller "
                    + controller + " of " + list.Count + " behaviours - the loop runs from the end), so it clears your attack input after it is written");
            else
                TraxLog.Info("rate", "WARNING: your attack gate's order is off (gate " + gate + ", MissionMainAgentController " + controller + " of " + list.Count
                    + ") - it may clear your input before the controller writes it; the summary's \"attacks that started while held anyway\" will show it (tell Claude)");
        }

        // ------------------------------------------------------------------ every tick: the safety net

        /// <summary>Every tick: switched off or not you any more → released at once; a countdown the gate
        /// never ended (it is not running) is ended here so the recovery bar cannot stick.</summary>
        internal void TickPlayerTimer(double now)
        {
            if (!_playerTimer.Holding) return;
            if (!AttackRateRules.From(TraxSettings.Shared).PlayerTimerOn)
            {
                ReleasePlayerTimer(PlayerTimerEnd.SwitchedOff, now);
                return;
            }
            var st = _playerTimerOwner;
            if (st == null || st.Removed || !IsPlayer(st))
            {
                ReleasePlayerTimer(PlayerTimerEnd.NotYou, now);
                return;
            }
            if (_playerTimer.Running && now >= _playerTimer.TimerEnd + GateMissingSeconds)
            {
                _gateMissedEnds++;
                var f = _playerTimer.Frame(now, _playerTimer.Pressing);
                if (f.Ended) PlayerCountdownEnded(now, held: false);
                TraxLog.Limited("rate", "your attack pause was ended by the tick, not the gate, at " + Sec(now) + " s (" + _gateMissedEnds
                    + " so far) - the gate is not running in this mission (tell Claude)", "rate-gate");
            }
        }

        /// <summary>Ends your hold at once (switched off, not you, the gate failed). Logged, counted.</summary>
        private void ReleasePlayerTimer(PlayerTimerEnd why, double now)
        {
            if (!_playerTimer.Release()) return;
            _rateStats.AddPlayerEnd(why);
            TraxLog.Limited("athletics", "YOU: your attack pause released at " + Sec(now) + " s - " + (why == PlayerTimerEnd.SwitchedOff
                ? "switched off (ModEnabled, AthleticsEnabled or AttackRatePlayerTimer): attack at once" : "you no longer control that fighter"), "athletics-timer");
        }

        /// <summary>The mission is over: your timer is released (nothing to lift in the engine - the gate
        /// simply stops clearing).</summary>
        private void ClosePlayerTimer(double now)
        {
            if (!_playerTimer.Holding) return;
            _rateStats.PlayerHeldAtMissionEnd++;
            _rateStats.AddPlayerEnd(PlayerTimerEnd.MissionEnd);
            _playerTimer.Release();
            _ = now;
        }

        // ------------------------------------------------------------------ the HUD's read

        /// <summary>
        /// What the Attack recovery bar shows for <paramref name="agent"/> (the player) at
        /// <paramref name="now"/>: empty during an attack a pause will follow, filling over the pause with
        /// the seconds left, full otherwise. False for an agent we do not track or between missions.
        /// Allocation-free, main thread.
        /// </summary>
        public static bool TryGetPlayerRecovery(Agent agent, double now, out AttackRecoveryReading reading)
        {
            reading = default;
            var logic = _current;
            if (logic == null || agent == null) return false;
            var st = logic.Get(agent);
            if (st == null) return false;
            bool on = AttackRateRules.From(TraxSettings.Shared).PlayerTimerOn;
            reading = on && ReferenceEquals(logic._playerTimerOwner, st)
                ? AttackRecoveryReading.From(logic._playerTimer, now, on)
                : AttackRecoveryReading.Full(on);
            return true;
        }
    }
}
