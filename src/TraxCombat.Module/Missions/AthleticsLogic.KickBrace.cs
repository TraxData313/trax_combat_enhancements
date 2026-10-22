using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 65 - THE KICKED MAN BRACES (Anton 2026-10-19: "make sure a looter kicking a heavy shielded troop doesn't do too much - maybe now and
    /// then a lucky kick can stagger him for a fast bit"; AI_NOTES "Step 65", DESIGN decision 58).
    ///
    /// The brace is part of the impact model's ONE decision per landed kick / bash (Core <see cref="ImpactMath.Decide"/> - the same pressure x
    /// as the staggers and knockdowns: his tiredness, both men's points, rank / armour (step 62's toughness), skill, a heavy shield for a bash,
    /// a blow from behind): failure chance S((x − from) ÷ (to − from)); a SPENT man (below <c>KickBraceMinBarPercent</c>) never braces. What it
    /// changes in the game:
    ///   - THE KNOCK-BACK: the engine asks <c>DecideAgentKnockedBackByBlow</c> (Mission.CreateMeleeBlow 5571 - after the shrug-off question,
    ///     before the knockdown one); a kick / bash is always YES in vanilla (MissionCombatMechanicsHelper 61). The damage decorator now asks
    ///     <see cref="IKnockBackDecider"/>: the model decides the blow HERE (the same <see cref="ImpactModelOnLandedBlow"/> - the KnockDown seam
    ///     then takes this decision) and a BRACED man's knock-back is refused (<c>KickBraceRefusesKnockBack</c>);
    ///   - THE REACTION: a kick's blow is NoIgnore (Mission.cs 5513) - the engine plays SOME reaction whatever the flags (step 62's finding), so
    ///     what shows within 0.6 s is played FASTER - 1 ÷ <c>KickBraceReelPercent</c> for a braced man, 1 ÷ the failed brace's cut for a lucky
    ///     kick (short + (1 − short) × T) - a watch of its own (<see cref="ImpactWatchPurpose.Brace"/>) through step 50's ReactionWatch and
    ///     SetCurrentActionSpeed, at most 8 x (<see cref="ImpactLiveMath.MaxSpeed"/>);
    ///   - no knockdown, no shield drop on a braced man (<see cref="ImpactRefusal.Braced"/>); a failed brace = step 50's rolls as before (0 on a
    ///     fresh man - the thresholds 0.2 / 0.35 need pressure).
    /// You and the AI alike (your share <c>ImpactOnPlayerPercent</c> scales the failure against you). Measured: the [summary] "kick brace"
    /// lines; an on-screen line for yours (braced / a lucky kick). Master switch FIRST (KickBraceRules.On), then the model's own gates.
    /// </summary>
    public sealed partial class AthleticsLogic : IKnockBackDecider
    {
        private readonly KickBraceStats _kickBraceStats = new KickBraceStats();
        private readonly bool[] _braceFirst = new bool[8];
        private string _seenBraceText = string.Empty;

        /// <summary>This mission's brace numbers (the smoke reads them).</summary>
        internal KickBraceStats KickBraceStats => _kickBraceStats;

        private void StartKickBrace()
        {
            _seenBraceText = KickBraceRules.From(TraxSettings.Shared).Describe();
            TraxLog.Info("impact", "mission start, the kicked man braces (step 65): " + _seenBraceText + " - read live; the [summary] \"kick brace\" lines count braced / short reel / "
                + "full reel / knocked down by the kicked man's tier (toughness) and bar (PLAYTEST D31)");
        }

        /// <summary>A brace setting changed (from ApplySettingsChange): one line.</summary>
        private void NoteKickBraceSettings(TraxSettings s)
        {
            string now = KickBraceRules.From(s).Describe();
            if (now == _seenBraceText) return;
            _seenBraceText = now;
            TraxLog.Info("impact", "brace settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now);
        }

        // ------------------------------------------------------------------ the knock-back seam (MAIN thread)

        bool IKnockBackDecider.KnockBackKept(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow) => KnockBackKept(attacker, victim, in collision);

        /// <summary>
        /// The game said KNOCKED BACK: a landed kick / bash on a tracked man on foot is decided now (or the shrug-off seam's decision of the same
        /// blow taken) - a BRACED man's knock-back refused while <c>KickBraceRefusesKnockBack</c> (counted either way). Everything else (a horse
        /// charge, a weapon knock-back, the brace off, the master switch off) keeps the game's YES. Own try: an error = the game's answer.
        /// </summary>
        internal bool KnockBackKept(Agent attacker, Agent victim, in AttackCollisionData cd)
        {
            try
            {
                if (!cd.IsAlternativeAttack || !_spikeStarted || _spikeClosed) return true;
                var br = KickBraceRules.From(TraxSettings.Shared);
                if (!br.On && !ImpactMeetRules.From(TraxSettings.Shared).Enabled) return true; // the master switch first (inside On / Enabled); step 82: a meet holds without the brace
                var v = Get(victim);
                if (v == null || v.Removed || ImpactBody.IsMounted(v)) return true;
                _kickBraceStats.KnockBackAsked++;
                _inKnockBackSeam = true;
                try
                {
                    ImpactModelOnLandedBlow(attacker, victim, in cd);
                }
                finally
                {
                    _inKnockBackSeam = false;
                }
                if (!_pendingDecided || !_pendingBraced) return true;
                if (!br.RefusesKnockBack && !_pendingMeetHeld)
                {
                    _kickBraceStats.KnockBackKeptBraceOff++;
                    return true;
                }
                _kickBraceStats.KnockBackRefused++;
                if (BraceFirst(0))
                    TraxLog.Info("impact", "live: first knock-back REFUSED for a braced man at " + Sec(SpikeNow) + " s - " + Name(v) + " braced against "
                        + (Get(attacker) is TrackedAgent a ? Name(a) : "someone") + "'s " + ImpactMath.EventName(LastImpact.Event) + " (failure chance " + Pct0(LastImpact.BraceFail)
                        + "): the game's knock-back flag refused in the damage decorator; what reaction the engine still plays is sped up x" + F2(ImpactLiveMath.FastSpeedFor(LastImpact.ReelFactor)));
                return false;
            }
            catch (Exception e)
            {
                _inKnockBackSeam = false;
                Failed("impact.brace-knockback", e);
                return true;
            }
        }

        // ------------------------------------------------------------------ the decision applied (from ApplyKick)

        /// <summary>A LIVE kick / bash decision whose brace was asked: counted by tier and bar band, the first of each outcome in full, the
        /// on-screen line (yours only - <see cref="ImpactMessage"/>'s rules).</summary>
        private void NoteBrace(TrackedAgent a, TrackedAgent v, in ImpactDecision d)
        {
            if (d.Brace == KickBraceOutcome.NotAsked) return;
            _kickBraceStats.Note(d.Brace, d.DefenderToughness, d.Bar, d.BraceFail, d.BraceSpent, d.Event == ImpactEvent.Bash, d.DefenderIsPlayer, d.AttackerIsPlayer);
            int k = 1 + (int)d.Brace; // 2..5
            if (BraceFirst(k))
                TraxLog.Info("impact", "live: first kick / bash " + KickBraceMath.OutcomeName(d.Brace).ToUpperInvariant() + (d.BraceSpent ? " (a spent man - no brace)" : string.Empty)
                    + " at " + Sec(d.Now) + " s - " + Name(a) + "'s " + ImpactMath.EventName(d.Event) + " on " + Name(v) + " (bar " + Pct0(d.Bar) + ", toughness "
                    + F2(d.DefenderToughness) + " - " + KickBraceMath.TierName(KickBraceMath.TierBand(d.DefenderToughness)) + ", x " + F2(d.X) + ", failure " + Pct0(d.BraceFail)
                    + (double.IsNaN(d.ReelFactor) ? string.Empty : ", his reaction x" + F2(d.ReelFactor) + " of the game's") + ")");
            bool bash = d.Event == ImpactEvent.Bash;
            if (d.Braced) ImpactMessage(bash ? ImpactMessageKind.BashBraced : ImpactMessageKind.Braced, a, v);
            else if (d.Brace == KickBraceOutcome.ShortReel && !d.BraceSpent) ImpactMessage(bash ? ImpactMessageKind.BashShortReel : ImpactMessageKind.ShortReel, a, v);
        }

        // ------------------------------------------------------------------ the cut reaction's watch (from TickLiveWatches)

        private void BraceWatchAppeared(ref ImpactLiveWatch w, double now)
        {
            if (w.Braced) _kickBraceStats.BracedSeen++;
            else _kickBraceStats.ShortSeen++;
            if (BraceFirst(6))
                TraxLog.Info("impact", "live: first cut reaction seen at " + Sec(now) + " s - " + Name(w.St) + "'s " + ImpactActions.Name(w.W.FirstType) + " on channel " + w.W.Channel + ", "
                    + F2(now - w.W.Since) + " s after the kick (" + (w.Braced ? "BRACED" : "a short reel") + ", played at speed " + F2(ImpactLiveMath.FastSpeedFor(w.Factor)) + ")");
        }

        private void BraceWatchMeasured(ref ImpactLiveWatch w, double length, double now)
        {
            if (w.Braced)
            {
                _kickBraceStats.BracedLength.Add(length);
                _kickBraceStats.BracedAsked.Add(w.Factor);
            }
            else
            {
                _kickBraceStats.ShortLength.Add(length);
                _kickBraceStats.ShortAsked.Add(w.Factor);
            }
            if (BraceFirst(7))
                TraxLog.Info("impact", "live: first cut reaction measured at " + Sec(now) + " s - " + Name(w.St) + "'s lasted " + F2(length) + " s (asked x" + F2(w.Factor)
                    + " of the game's; untouched reels so far " + (_live.ReelControl.Count > 0 ? F2(_live.ReelControl.Mean) + " s" : "none") + ")");
        }

        private void BraceWatchNeverAppeared(ref ImpactLiveWatch w, double now)
        {
            if (w.Braced) _kickBraceStats.BracedNotSeen++; // the knock-back refused: the engine may play nothing at all - the best brace
            else _kickBraceStats.ShortNotSeen++;
        }

        private bool BraceFirst(int i)
        {
            if (_braceFirst[i]) return false;
            _braceFirst[i] = true;
            return true;
        }

        /// <summary>The summary's turn (after the punish lines).</summary>
        internal void WriteKickBraceSummary()
        {
            foreach (var line in _kickBraceStats.SummaryLines(KickBraceRules.From(TraxSettings.Shared))) TraxLog.Info("summary", line);
        }
    }
}
