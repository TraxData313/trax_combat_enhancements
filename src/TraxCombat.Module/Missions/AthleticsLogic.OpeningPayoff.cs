using System;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 106d - OPENINGS, THE PAYOFF LIVE (AI_NOTES "Step 106" - "106d - built", DESIGN decision 95 (d), (l)). With <c>OpeningsEnabled</c> (the master
    /// switch first) YOUR landed hit that <see cref="OpeningChain.NoteLanded"/> calls a stagger (him off balance, or from behind) is played:
    ///   - THE REEL: his record reels (<see cref="ImpactState.Reel(double, double, ReelCause)"/>, <see cref="ReelCause.Opening"/> - so step 105's 80 %
    ///     knockdown meets any other hit / kick on him) for the chain's reel (0.8 s + 0.3 a link, at most 2 s) and his forced backward stagger is
    ///     queued for the tick - the riposte's path (<see cref="ImpactBlockAction.Opening"/>: its family's switch, the ForcedStagger lever, never a
    ///     rider), watched (<see cref="ImpactWatchPurpose.Opening"/>), its measured end written back. The impact system's own stagger on the same hit
    ///     carries it instead (no second one); a knockdown on the same hit, or a man already down, gets none.
    ///   - THE PERFECT STRIKE WINDOW: an opening window in step 100g's book (<see cref="PerfectStrikeBook.OpenOpening"/>) with the chain's finishing
    ///     sides (the opposite of the staggering swing, by the weapon) and the follow-up's timing - the damage decorator applies it, the red arrow
    ///     shows it (both read the SAME mask), open while he reels + the grace.
    ///   - THE NOTICES: "OPENING!" gold in the feed and the middle of the screen (like PERFECT STRIKE) from the next tick; "GUARD BROKEN" in the feed
    ///     when a won block roll lowers a shield (106b's route).
    /// The forced guard of 106b is let go as he reels (<see cref="OpeningForceEnd.Reels"/>). Main thread; no allocation per tick (texts only when
    /// shown, constant strings).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        internal const string OpeningText = "OPENING!";
        internal const string GuardBrokenText = "GUARD BROKEN";

        /// <summary>The feed colour of GUARD BROKEN (#RRGGBBAA, orange).</summary>
        internal const string GuardBrokenHex = "#FF8C1AFF";

        private bool _openShowOpening, _openShowGuardBroken, _openFirstLive, _openFirstPlayed;

        /// <summary>Step 113b: the man whose impact stagger on THIS hit the opening's longer reel replaced (ApplyHit, right after, skips its flinch
        /// watch for him); null otherwise. Set and read inside one impact decision.</summary>
        private TrackedAgent? _openTookImpactStagger;

        /// <summary>The last opening stagger's asked seconds (NaN none; the smoke reads it).</summary>
        internal double LastOpeningReelAsked { get; private set; } = double.NaN;

        /// <summary>
        /// From <see cref="OpeningShadowHit"/>'s stagger (after the forced guard was let go): the reel, the window, the notice - with the live switch.
        /// <paramref name="modStagger"/> / <paramref name="modDown"/> = the impact decision's own stagger / knockdown on this same hit. Its own try there.
        /// </summary>
        private string OpeningPayoff(TrackedAgent a, TrackedAgent v, bool modStagger, bool modDown, double now)
        {
            var r = OpeningRules.From(TraxSettings.Shared);
            if (!r.Enabled) return string.Empty;
            var ir = ImpactRules.From(TraxSettings.Shared);
            bool hitsLive = ir.Applies(ImpactFamily.Hits);
            double reel = Math.Max(0, _openChain.ReelUntil - now);
            if (modDown && hitsLive)
            {
                _open.LiveKnockedDown++;
                EndChain(OpeningReset.ReelOver);
                return " - LIVE: knocked DOWN by this same hit (no stagger, no window)";
            }
            if (v.Impact.Down(now))
            {
                _open.LiveAlreadyDown++;
                EndChain(OpeningReset.ReelOver);
                return " - LIVE: he is down already (no stagger)";
            }
            if (!ir.Applies(ImpactFamily.Riposte) || _live.Disabled(ImpactLever.ForcedStagger))
            {
                _open.LiveNotLive++;
                EndChain(OpeningReset.Off);
                return " - LIVE: not played - the impact system's riposte is not live (" + ir.ModeText() + ")";
            }
            string how;
            double impactReel = Math.Max(0, v.Impact.ReelingUntil - now);
            if (modStagger && hitsLive && v.Impact.Reeling(now) && OpeningMath.OpeningOutlastsImpact(reel, impactReel) && QueueOpeningStagger(a, v, reel, now))
            {
                // step 113b: the impact system's own stagger on this hit is SHORTER than the opening's reel - ONE stagger, ours (act_stagger_backward
                // slowed to the opening's length from the tick); ApplyHit skips its flinch watch for him (no second animation, no early EndReel)
                _openTookImpactStagger = v;
                _open.LiveLengthenedImpact++;
                _open.LiveLengthenedFromSum += impactReel;
                how = " - LIVE: he reels " + F2(reel) + " s (the opening's reel outlasts the impact system's own " + F2(impactReel) + " s stagger on this hit - ours replaces it, from the next tick)";
            }
            else if (modStagger && hitsLive && v.Impact.Reeling(now))
            {
                _open.LiveOnModStagger++;
                how = " - LIVE: the impact system's own stagger carries it (" + F2(v.Impact.ReelingUntil - now) + " s)";
            }
            else if (!QueueOpeningStagger(a, v, reel, now))
            {
                _open.LiveNotApplied++;
                EndChain(OpeningReset.Off);
                return " - LIVE: not played (the queue full / the lever off)";
            }
            else how = " - LIVE: he reels " + F2(reel) + " s (our stagger from the next tick)";
            OpenOpeningWindow(v, now);
            _openShowOpening = true;
            if (!_openFirstLive)
            {
                _openFirstLive = true;
                TraxLog.Info("impact", "openings: first LIVE stagger at " + Sec(now) + " s - " + Name(v) + how + "; the PERFECT STRIKE side " + OpeningMath.MaskName(_openChain.FinishMask)
                    + " for the reel (the red arrow and the damage read the same sides); step 105's knockdown on any other hit; \"" + OpeningText + "\" on screen");
            }
            return how;
        }

        /// <summary>His reel written (cause Opening) and our forced stagger queued for the tick; false = not queued (his reel left as it was).</summary>
        private bool QueueOpeningStagger(TrackedAgent a, TrackedAgent v, double reel, double now)
        {
            if (_live.Disabled(ImpactLever.ForcedStagger) || _blockPendingCount >= _blockPending.Length || !(reel > 0)) return false;
            var ir = ImpactRules.From(TraxSettings.Shared);
            v.Impact.Reel(now + reel, ir.StaggerImmunity, ReelCause.Opening);
            _blockPending[_blockPendingCount++] = new ImpactBlockPending
            {
                Action = ImpactBlockAction.Opening, St = v, Other = a, Event = ImpactEvent.CleanHit, At = now, Asked = reel, Bar = v.Fraction,
                Source = TimingSource.None, GuardAge = double.NaN, Perfect = double.NaN,
            };
            _open.LiveAsked++;
            _open.NoteReelAsked(reel); // step 113
            LastOpeningReelAsked = reel;
            return true;
        }

        /// <summary>The tick plays the queued opening stagger (RunBlockPending - CanForce passed): act_stagger_backward at the speed for its length,
        /// with the engine's priority, then over it; watched.</summary>
        private void RunOpeningStagger(in ImpactBlockPending p, double now, ref int calls)
        {
            float speed = ImpactBlockMath.StaggerSpeed(p.Asked, _staggerNatural.Seconds);
            calls++;
            bool over = false;
            bool ok = ImpactBody.ForceStagger(p.St, StaggerKind.Backward, speed, false, out int index);
            if (!ok && calls < ImpactLiveMath.MaxActionsPerTick)
            {
                calls++;
                ok = ImpactBody.ForceStagger(p.St, StaggerKind.Backward, speed, true, out index);
                over = ok;
            }
            if (!ok)
            {
                _open.LiveNotApplied++;
                FixState(p.Action, p.St, now);
                return;
            }
            _open.LivePlayed++;
            AddLiveWatch(p.St, ImpactWatchPurpose.Opening, WatchKind.Stagger, now, ImpactBlockMath.StaggerAppearSeconds, 1,
                expectIndex: index, ev: p.Event, asked: p.Asked, speed: speed);
            if (!_openFirstPlayed)
            {
                _openFirstPlayed = true;
                TraxLog.Info("impact", "openings: first opening stagger PLAYED at " + Sec(now) + " s - " + Name(p.St) + "'s act_stagger_backward from the tick, "
                    + (over ? "OVER the engine's priority" : "with the engine's priority") + ", speed " + F2(speed) + " for " + F2(p.Asked) + " s asked");
            }
        }

        /// <summary>The perfect-strike window of an opening's reel (step 100g's book, an opening window): the chain's finishing sides and timing.</summary>
        private void OpenOpeningWindow(TrackedAgent v, double now)
        {
            if (_strikeDisabled) return;
            try
            {
                var pr = PerfectStrikeRules.From(TraxSettings.Shared);
                if (!pr.OpeningOn || _openChain.FinishMask == 0) return;
                _strikeBook.OpenOpening(v.AgentIndex, _openChain.FinishMask, _openChain.Score, now, out bool dropped, out bool droppedRider);
                _strike.Opened++;
                _strike.OpeningOpened++;
                _open.LiveWindows++;
                if (dropped)
                {
                    StrikeExpired(droppedRider);
                    _strike.Replaced++;
                    if (droppedRider) _strike.RiderReplaced++;
                }
            }
            catch (Exception e)
            {
                StrikeFailed("impact.perfect-open-opening", e);
            }
        }

        /// <summary>From <see cref="ForceAtCommit"/>: a won roll LOWERED his shield - "GUARD BROKEN" from the next tick.</summary>
        private void NoteGuardBroken() => _openShowGuardBroken = true;

        /// <summary>From <see cref="TickOpenings"/> (before anything else): the queued notices; while he reels from an opening the chain's reel follows
        /// his record's (re-timed by the measured stagger) + the grace, so the chain and the window end together.</summary>
        private void TickOpeningPayoff(double now)
        {
            if (_openShowOpening)
            {
                _openShowOpening = false;
                _open.OpeningShown++;
                MessageSink.Show(OpeningText, PerfectStrikeMath.GoldHex);
                if (PerfectStrikeRules.From(TraxSettings.Shared).CentreMessage) MessageSink.Centre(OpeningText);
            }
            if (_openShowGuardBroken)
            {
                _openShowGuardBroken = false;
                _open.GuardBrokenShown++;
                MessageSink.Show(GuardBrokenText, GuardBrokenHex);
            }
            if (_openChain.Phase != OpeningPhase.Reeling || !OpeningRules.From(TraxSettings.Shared).Enabled) return;
            var t = GetByIndex(_openChain.Target);
            if (t == null || t.Removed) return;
            double end = t.Impact.ReelingUntil + PerfectStrikeRules.From(TraxSettings.Shared).GraceSeconds;
            _openChain.ReelUntil = end;
        }
    }
}
