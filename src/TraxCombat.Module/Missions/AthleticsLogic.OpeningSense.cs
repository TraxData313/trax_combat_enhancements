using System;
using System.Globalization;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 111 - THE OPENING CHAIN MAKES SENSE (Toni's training field 2026-11-10, the veteran trainer with a shield; AI_NOTES "Step 111", DESIGN
    /// decision 100). One place for the rules that tie steps 106-108 together:
    ///   - THE HOLD GATE (<see cref="HoldGate"/>): no hold by ANY route (your hit, his shield block, a link) while he reels from our stagger or is still
    ///     inside the stagger immunity after it, nor while YOU reel (<c>OpeningHoldEndsWhenYouReel</c>) - Core <see cref="OpeningHoldMath.Refuse"/>.
    ///   - YOUR SWING IN THE AIR (<see cref="YourSwingBegan"/> at your melee wind-up, <see cref="YourSwingResolved"/> at its contact / miss): the red
    ///     arrow never turns meanwhile (Core <see cref="ArrowLatch"/>), the "Unbalanced" line waits for it.
    ///   - THE PERFECT STRIKE'S FLIGHT: a swing you begin while a perfect-strike window is open keeps that window open and HIS GUARD DOWN (the forced
    ///     guard record, kind <see cref="OpeningForceKind.Strike"/>: every guard bit out, 'can defend' off) until it lands / misses, at most
    ///     <c>PerfectStrikeFlightSeconds</c> past the window.
    ///   - NO KICK / BASH UNDER A HOLD: step 81's roll refused for a held / forced man (<see cref="OpeningNoKick"/>); a running attempt cut when a
    ///     forced guard begins on him (<see cref="CutAiKickFor"/>).
    /// Main thread; no allocation per tick (structs, lines only on events of your own fights).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private ArrowLatch _arrowLatch;
        private int _flightVictim = -1;
        private bool _senseFirstRefused, _senseFirstYouReel, _senseFirstArrowHeld, _senseFirstKick, _senseFirstFlight, _holdTextDeferred;

        /// <summary>The arrow's latch (the smoke reads it).</summary>
        internal ArrowLatch OpeningArrowLatch => _arrowLatch;

        /// <summary>The man a perfect-strike swing of yours is in the air at (-1 none; the smoke reads it).</summary>
        internal int PerfectStrikeFlightVictim => _flightVictim;

        // ------------------------------------------------------------------ the hold gate

        /// <summary>
        /// The one gate every route a hold starts by passes (Core <see cref="OpeningHoldMath.Refuse"/>): YOU reeling or down (or
        /// <paramref name="youRiposted"/> - this block's riposte on you), then HIM reeling (his record, an opening's reel, an open perfect-strike window
        /// or its flight), then him inside the stagger immunity after it.
        /// </summary>
        private HoldRefusal HoldGate(TrackedAgent v, bool youRiposted, double now, in OpeningHoldRules hr)
        {
            var you = PlayerRecord();
            bool youReel = youRiposted || (you != null && !you.Removed && (you.Impact.Reeling(now) || you.Impact.Down(now)));
            bool heReels = v.Impact.Reeling(now) || v.Impact.Down(now) || (_openChain.Phase == OpeningPhase.Reeling && _openChain.Target == v.AgentIndex && _openChain.Reeling(now))
                           || StrikeWindowOn(v, now);
            return OpeningHoldMath.Refuse(heReels, v.Impact.StaggerImmune(now), youReel, hr.EndsWhenYouReel);
        }

        /// <summary>A perfect-strike window on <paramref name="v"/> is open now (his reel + the grace, or a swing of yours in the air at it).</summary>
        private bool StrikeWindowOn(TrackedAgent v, double now)
        {
            if (_strikeDisabled || _strikeBook.Count == 0) return false;
            int i = _strikeBook.IndexOf(v.AgentIndex);
            if (i < 0) return false;
            var pr = PerfectStrikeRules.From(TraxSettings.Shared);
            return PerfectStrikeMath.OpenFor(now, v.Impact.ReelingUntil, pr.GraceSeconds, _strikeBook[i].FlightUntil);
        }

        /// <summary>A hold refused: counted, a line (the first one with the rule spelled out); a chain still on him ends.</summary>
        private void NoteHoldRefused(TrackedAgent v, HoldRefusal why, bool byBlock, double now, string what)
        {
            _openHoldStats.NoteRefused(why, byBlock);
            if (_hold.Active && _hold.Target == v.AgentIndex) EndHoldChain(why == HoldRefusal.YouReel ? HoldEnd.YouReel : HoldEnd.Off, now, true);
            if (!_senseFirstRefused)
            {
                _senseFirstRefused = true;
                TraxLog.Info("impact", "openings: first hold REFUSED at " + Sec(now) + " s (step 111) - " + Name(v) + ": " + OpeningHoldMath.RefusalName(why)
                                       + " - no hold by any route (your hit, his shield block, a link) while he reels or is not recovered (the stagger immunity), nor while you reel");
            }
            OpeningLine("openings (hold-block): " + what + " - NO hold on " + Name(v) + ": " + OpeningHoldMath.RefusalName(why)
                        + (why == HoldRefusal.HeImmune ? " (" + F2(Math.Max(0, v.Impact.StaggerImmuneUntil - now)) + " s left)" : string.Empty));
        }

        /// <summary>From <see cref="TickHolds"/>: you reel or lie down - his hold chain ends, the arrow goes (<c>OpeningHoldEndsWhenYouReel</c>).</summary>
        private bool HoldEndsYouReel(TrackedAgent v, double now, in OpeningHoldRules hr)
        {
            if (!hr.EndsWhenYouReel) return false;
            var you = PlayerRecord();
            if (you == null || you.Removed || !(you.Impact.Reeling(now) || you.Impact.Down(now))) return false;
            string phase = _hold.Phase == HoldPhase.Recovering ? "recovering" : _hold.Phase == HoldPhase.Holding ? "holding" : "after the hold";
            EndHoldChain(HoldEnd.YouReel, now, true);
            if (!_senseFirstYouReel)
            {
                _senseFirstYouReel = true;
                TraxLog.Info("impact", "openings: first hold ENDED because YOU reel at " + Sec(now) + " s (step 111) - " + Name(v) + " is no longer off balance; the arrow goes");
            }
            OpeningLine("openings (hold-block): you reel - " + Name(v) + "'s hold ends (he was " + phase + "); the arrow goes");
            return true;
        }

        // ------------------------------------------------------------------ your swing in the air

        /// <summary>From <see cref="OpeningWindUp"/>: your melee wind-up - the arrow latched on what it shows now, a perfect-strike flight begun.</summary>
        private void YourSwingBegan(TrackedAgent you, double now)
        {
            if (!YouDrive(you)) return;
            try
            {
                var live = ReadArrowLive(you.Agent, now);
                _arrowLatch.Begin(now, in live);
                StrikeFlightBegan(you, now);
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        /// <summary>From <see cref="OpeningContactSeen"/> (your blow's contact - <paramref name="victim"/>) and <see cref="OpeningMiss"/> (null): the
        /// swing resolved - the arrow free again, the flight over.</summary>
        private void YourSwingResolved(TrackedAgent? victim, bool landed, double now)
        {
            _arrowLatch.End();
            if (_flightVictim < 0) return;
            try
            {
                ResolveFlight(victim == null ? 2 : victim.AgentIndex != _flightVictim ? 1 : landed ? 0 : 1, now);
            }
            catch (Exception e)
            {
                StrikeFailed("impact.perfect-strike-flight", e);
            }
        }

        /// <summary>The arrow through the latch (the reading of this frame for <paramref name="live"/>).</summary>
        private ArrowReading LatchArrow(double now, in ArrowReading live)
        {
            double max = OpeningHoldRules.From(TraxSettings.Shared).ArrowSwingMax;
            bool before = _arrowLatch.Held;
            var shown = _arrowLatch.Filter(now, max, in live, out bool held);
            if (held && !before)
            {
                _openHoldStats.ArrowSwingsHeld++;
                if (!_senseFirstArrowHeld)
                {
                    _senseFirstArrowHeld = true;
                    TraxLog.Info("impact", "openings: first ARROW HELD during your swing at " + Sec(now) + " s (step 111) - it showed " + OpeningArrowMath.ArrowsName(_arrowLatch.Mask)
                                           + " at your wind-up and would have turned to " + OpeningArrowMath.ArrowsName(live.Mask) + " in the air; it turns only when your swing lands or misses");
                }
            }
            return shown;
        }

        /// <summary>The "Unbalanced" line waits while your swing is in the air (counted once a line).</summary>
        private bool HoldTextWaits(double now)
        {
            if (!_arrowLatch.InSwing(now, OpeningHoldRules.From(TraxSettings.Shared).ArrowSwingMax)) return false;
            if (!_holdTextDeferred)
            {
                _holdTextDeferred = true;
                _openHoldStats.MessagesDeferred++;
            }
            return true;
        }

        // ------------------------------------------------------------------ the perfect strike's flight

        /// <summary>Your wind-up while a perfect-strike window is open: the window held open and his guard down until the swing resolves.</summary>
        private void StrikeFlightBegan(TrackedAgent you, double now)
        {
            if (_strikeDisabled || _strikeBook.Count == 0) return;
            var pr = PerfectStrikeRules.From(TraxSettings.Shared);
            if (!(pr.FlightSeconds > 0)) return;
            int best = -1;
            double bestAt = double.NegativeInfinity;
            for (int i = 0; i < _strikeBook.Count; i++)
            {
                ref var w = ref _strikeBook[i];
                if ((w.Opening ? !pr.OpeningOn : !pr.Enabled) || w.Rider || w.OpenedAt < bestAt) continue; // a rider: never forced (on foot only)
                var v = GetByIndex(w.Victim);
                if (v == null || v.Removed || !PerfectStrikeMath.Open(now, v.Impact.ReelingUntil, pr.GraceSeconds)) continue;
                best = i;
                bestAt = w.OpenedAt;
            }
            if (best < 0) return;
            ref var b = ref _strikeBook[best];
            var man = GetByIndex(b.Victim)!;
            if (_flightVictim >= 0 && _flightVictim != b.Victim) ResolveFlight(3, now); // a new swing at another man: the old flight over
            double until = PerfectStrikeMath.FlightEnd(now, man.Impact.ReelingUntil, pr.GraceSeconds, pr.FlightSeconds);
            if (!(until > 0)) return;
            bool fresh = _flightVictim != b.Victim || !(b.FlightUntil > 0);
            b.FlightUntil = until;
            _flightVictim = b.Victim;
            if (fresh) _strike.Flights++;
            bool forced = false;
            if (!(_forceKind == OpeningForceKind.Test && ReferenceEquals(_forceSt, man)) && StartForce(man, OpeningForceKind.Strike, OpeningMath.Unknown, true, until + 0.05, now))
            {
                forced = true;
                if (!_forceFlagOff && OpeningBody.CanDefend(man))
                {
                    OpeningBody.SetCanDefend(man, false);
                    _forceFlagOff = true;
                    _force.FlagsOff++;
                }
            }
            if (!_senseFirstFlight)
            {
                _senseFirstFlight = true;
                TraxLog.Info("impact", "perfect strike: first swing begun in a window at " + Sec(now) + " s (step 111) - " + Name(man) + " reels "
                                       + F2(Math.Max(0, man.Impact.ReelingUntil - now)) + " s more; the window and his guard stay down until your swing lands or misses, at most "
                                       + F2(pr.FlightSeconds) + " s past the window (PerfectStrikeFlightSeconds)");
            }
            OpeningLine("perfect strike: your swing began in the window on " + Name(man) + " (" + F2(Math.Max(0, man.Impact.ReelingUntil - now)) + " s of his reel left) - "
                        + (forced ? "his guard down and 'can defend' off" : "his guard NOT taken (" + (_forceKind == OpeningForceKind.Test ? "the test tool has him" : "not his to drive") + ")")
                        + " until it lands or misses, at most to " + Sec(until) + " s");
        }

        /// <summary>The flight over - <paramref name="how"/>: 0 landed on him, 1 blocked / not on his body / another man, 2 a miss, 3 its time ran out.</summary>
        private void ResolveFlight(int how, double now)
        {
            int victim = _flightVictim;
            if (victim < 0) return;
            _flightVictim = -1;
            var pr = PerfectStrikeRules.From(TraxSettings.Shared);
            var v = GetByIndex(victim);
            int i = _strikeBook.IndexOf(victim);
            if (i >= 0) _strikeBook[i].FlightUntil = 0;
            bool outlasted = v != null && PerfectStrikeMath.Outlasted(now, v.Impact.ReelingUntil, pr.GraceSeconds);
            if (outlasted) _strike.FlightsOutlasted++;
            switch (how)
            {
                case 0: _strike.FlightsLanded++; break;
                case 1: _strike.FlightsBlocked++; break;
                case 2: _strike.FlightsMissed++; break;
                default: _strike.FlightsExpired++; break;
            }
            if (_forceKind == OpeningForceKind.Strike && _forceSt != null && _forceSt.AgentIndex == victim) EndForce(OpeningForceEnd.Resolved, now, !_forceSt.Removed);
            if (v != null)
                OpeningLine("perfect strike: your swing at " + Name(v) + " " + (how == 0 ? "LANDED" : how == 1 ? "was stopped / met another" : how == 2 ? "missed" : "ran out of time")
                            + (outlasted ? " after his reel ended (the flight kept his guard down)" : " inside his reel") + "; his guard is his own again");
        }

        /// <summary>The flight's own switches: the master switch first (inside the rules), a perfect strike live in either game, the flight's seconds.</summary>
        private static bool StrikeFlightRulesOn()
        {
            var pr = PerfectStrikeRules.From(TraxSettings.Shared);
            return pr.ModEnabled && (pr.Enabled || pr.OpeningOn) && pr.FlightSeconds > 0;
        }

        /// <summary>The flight's force on <paramref name="st"/> still wanted (the backstop in <see cref="TickOpeningForce"/>).</summary>
        private bool FlightOn(TrackedAgent st, double now)
        {
            if (_flightVictim != st.AgentIndex) return false;
            int i = _strikeBook.IndexOf(st.AgentIndex);
            return i >= 0 && _strikeBook[i].FlightUntil > 0 && now <= _strikeBook[i].FlightUntil + 0.05;
        }

        /// <summary>The window of <paramref name="i"/> closes in the tick / the decorator: its flight (if any) ran out first.</summary>
        private void FlightWindowClosing(int victim, double now)
        {
            if (victim == _flightVictim) ResolveFlight(3, now);
        }

        // ------------------------------------------------------------------ no kick / bash under a hold

        /// <summary>Step 81's roll at his wind-up: refused while he is held (a hold - recovering or holding - or any forced guard of ours).</summary>
        private bool OpeningNoKick(TrackedAgent st, double now)
        {
            bool forced = ReferenceEquals(_forceSt, st) && _forceKind != OpeningForceKind.None;
            if (!forced && !_hold.Forced(st.AgentIndex)) return false;
            _openHoldStats.KickRollsRefused++;
            if (!_senseFirstKick)
            {
                _senseFirstKick = true;
                TraxLog.Info("impact", "openings: first kick / bash roll REFUSED for a held man at " + Sec(now) + " s (step 111) - " + Name(st) + " ("
                                       + (forced ? "forced guard: " + _forceKind : "hold " + _hold.Phase) + ") - a man under a hold, a forced guard or the recovery never kicks or bashes");
            }
            return true;
        }

        /// <summary>A forced guard begins on <paramref name="st"/>: a kick / bash attempt of his still running is cut (never played over it).</summary>
        private void CutAiKickFor(TrackedAgent st, double now)
        {
            if (st.AiKickPhase == AiKickPhase.None || _aiKickCount == 0) return;
            for (int i = 0; i < _aiKickCount; i++)
            {
                if (!ReferenceEquals(_aiKickers[i], st)) continue;
                EndAiKick(i, AiKickEnd.Interrupted, now);
                _openHoldStats.KicksCut++;
                OpeningLine("openings: " + Name(st) + "'s running kick / bash CUT - a forced guard began on him");
                return;
            }
        }
    }
}
