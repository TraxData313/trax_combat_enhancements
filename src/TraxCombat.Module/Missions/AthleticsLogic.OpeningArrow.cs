using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 106c - THE RED ARROW's state (AI_NOTES "106c - built", DESIGN decision 95 (k)); drawn by <c>Hud.OpeningArrowView</c>, which reads
    /// <see cref="TryGetOpeningArrow"/> every frame. Two cues, one at a time (never several men's):
    ///   - THE OPENING FLASH: noted when he goes off balance (<see cref="BeginFollowUp"/> - his block of your attack or your unguarded front hit,
    ///     the side by <see cref="OpeningPlan.Opening"/>, so <c>OpeningSidesCrossed</c> holds) peaking at the plan's flash; and by the K test tool's
    ///     forced guard (the side it leaves open, peaking <c>OpeningFlashDelaySeconds</c> after the key). Shown only with <c>OpeningsEnabled</c> -
    ///     or for the test tool - (<see cref="OpeningArrowMath.ShowsFlash"/>); it wins over the steady arrow while it is lit.
    ///   - THE PERFECT STRIKE SIDE: an opening's reel (the chain's finishing sides, with <c>OpeningsEnabled</c>), else your newest open perfect-strike
    ///     window (step 100g's book: the defensive game's timed-block reel, a rider's reel without any-side) - his blocked direction's answer by your
    ///     wielded weapon (<see cref="OpeningArrowMath.WindowFinishMask"/>), while the window is open (his reel + the grace).
    /// Main thread; no allocation per frame (the reads are struct reads; the weapon read only while a window is open).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private double _arrowFlashPeak = double.NaN;
        private int _arrowFlashAttack = OpeningMath.Unknown;
        private bool _arrowFlashTest, _arrowFailed;

        /// <summary>The flash last noted: its attack and peak (the smoke reads them).</summary>
        internal int ArrowFlashAttack => _arrowFlashAttack;

        internal double ArrowFlashPeak => _arrowFlashPeak;

        /// <summary>A flash of <paramref name="attack"/>'s side peaking at <paramref name="peakAt"/> (mission time); <paramref name="test"/> = the K tool's.</summary>
        private void NoteArrowFlash(int attack, double peakAt, bool test)
        {
            if (!OpeningMath.IsAttack(attack)) return;
            _arrowFlashAttack = attack;
            _arrowFlashPeak = peakAt;
            _arrowFlashTest = test;
        }

        /// <summary>Step 106f: you are in melee with a man who is off balance or reeling - an opening pending, a chain, a flash noted, a perfect-strike
        /// window open (the HUD's walk-about rule for the red arrow). Main thread.</summary>
        internal static bool OpeningMeleeNow(double now)
        {
            var logic = _current;
            return logic != null && (logic._openPending || logic._openChain.Active || logic._hold.Active || (!double.IsNaN(logic._arrowFlashPeak) && !OpeningArrowMath.FlashOver(now, logic._arrowFlashPeak)) || logic._strikeBook.Count > 0);
        }

        /// <summary>The red arrow this frame for the HUD (main thread). False = nothing to show (or no logic running).</summary>
        internal static bool TryGetOpeningArrow(Agent? player, double now, out ArrowReading reading)
        {
            reading = default;
            var logic = _current;
            return logic != null && logic.ReadArrow(player, now, out reading);
        }

        private bool ReadArrow(Agent? player, double now, out ArrowReading a)
        {
            a = default;
            if (_arrowFailed) return false;
            try
            {
                var live = ReadArrowLive(player, now);
                a = LatchArrow(now, in live); // step 111: never a turn while your swing is in the air
                return a.Shows;
            }
            catch (Exception e)
            {
                _arrowFailed = true; // off for the battle - the arrow is display only
                Failed("hud.opening-arrow", e);
                return false;
            }
        }

        /// <summary>What the arrow would show this frame, before step 111's latch (none = default).</summary>
        private ArrowReading ReadArrowLive(Agent? player, double now)
        {
            ArrowReading a = default;
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!double.IsNaN(_arrowFlashPeak))
                {
                    if (OpeningArrowMath.FlashOver(now, _arrowFlashPeak)) _arrowFlashPeak = double.NaN;
                    else if (OpeningArrowMath.ShowsFlash(in r, _arrowFlashTest, _arrowFlashTest || OpeningsLive(in r))) // step 106e: no flash while the openings are shadow
                    {
                        float alpha = OpeningArrowMath.FlashAlpha(now, _arrowFlashPeak);
                        if (alpha > 0)
                        {
                            a.Cue = ArrowCue.Flash;
                            a.Mask = OpeningMath.Bit(_arrowFlashAttack);
                            a.Alpha = alpha;
                            return a;
                        }
                    }
                }
                if (!OpeningArrowMath.ShowsFinish(in r)) return default;
                int hold = HoldArrowMask(); // step 107: the opening of a hold, steady while he holds
                if (hold != 0 && OpeningsLive(in r))
                {
                    a.Cue = ArrowCue.Hold;
                    a.Mask = hold;
                    a.Alpha = 1f;
                    return a;
                }
                int mask = FinishMaskNow(player, now, in r);
                if (mask == 0) return default;
                a.Cue = ArrowCue.Finish;
                a.Mask = mask;
                a.Alpha = 1f;
                return a;
            }
        }

        /// <summary>The finishing sides of the one reeling man you can finish now, else 0.</summary>
        private int FinishMaskNow(Agent? player, double now, in OpeningRules r)
        {
            // step 106d: an opening's reel is a window in the book like the defensive game's (the damage reads the same mask) - one source
            if (_strikeDisabled || _strikeBook.Count == 0) return 0;
            var pr = PerfectStrikeRules.From(TraxSettings.Shared);
            int best = -1;
            double bestAt = double.NegativeInfinity;
            for (int i = 0; i < _strikeBook.Count; i++)
            {
                ref var w = ref _strikeBook[i];
                if (w.Opening ? !pr.OpeningOn || w.FinishMask == 0 : !pr.Enabled) continue;
                if (w.Rider && !pr.MountedOn) continue;
                if ((!w.Opening && !PerfectStrikeMath.IsAttack(w.Blocked)) || w.OpenedAt < bestAt) continue;
                var v = GetByIndex(w.Victim);
                if (v == null || v.Removed || !PerfectStrikeMath.OpenFor(now, v.Impact.ReelingUntil, pr.GraceSeconds, w.FlightUntil)) continue; // step 111: the flight keeps it
                best = i;
                bestAt = w.OpenedAt;
            }
            if (best < 0) return 0;
            ref var b = ref _strikeBook[best];
            if (b.Opening) return b.FinishMask;
            int weapon = player != null ? ReadWeaponMask(player) : OpeningMath.AllAttacks;
            return OpeningArrowMath.WindowFinishMask(b.Rider, pr.HorsebackAnySide, b.Blocked, weapon, r.WeaponFinishes);
        }
    }
}
