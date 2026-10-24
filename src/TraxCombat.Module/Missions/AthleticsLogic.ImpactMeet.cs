using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 82 - MEETING A BASH OR A KICK THE RIGHT WAY (Toni 2026-10-22; AI_NOTES "Step 82", DESIGN decision 78, IMPACT §3.5's meet rows):
    ///   - THE GUARD AT CONTACT (<see cref="FillMeet"/>): the defender's channel-1 action and its direction, the engine's defend flag
    ///     (<see cref="IMeetBody.Guard"/>) → Core <see cref="ImpactMeetMath.Classify"/> - read in the same decision as the rest (the decorator's
    ///     seams for a landed kick / bash, OnMeleeHit for a bash the shield stopped - <see cref="ImpactModelHitTaken"/>). The engine itself tells
    ///     no direction for an alternative attack: it stops a bash with ANY raised shield and lands every kick and every bash into a weapon guard.
    ///   - THE ROWS applied (<see cref="ApplyMeet"/>): a correct meet's ATTACKER stagger / fall = the riposte's forced stagger / our own blow from
    ///     the tick (step 51 / 62's queue); a landed correct meet = braced (the knock-back refused in the knock-back seam, the reaction cut); a bash
    ///     the shield stopped with a wrong guard = the blocker's stagger / fall / drop from the tick; a landed one = step 50 / 65's live kick.
    ///   - THE AI READS IT (<see cref="MeetReadStart"/>): an AI man targeted by a kick / bash that is starting (any attacker - an AI attempt at its
    ///     start, your kick when the poll sees it) rolls ONCE at the chance he blocks a swing correctly (<see cref="ImpactMeetMath.ReadChance"/> -
    ///     the game's AIBlockOnDecideAbility × the right-direction share of AiRandomizedDefendDirectionChance, step 30's tired guess inside) ×
    ///     <c>MeetAiReadScalePercent</c>; read = after <c>MeetAiReadReactionSeconds</c> his input carries the correct guard (DefendRight / DefendDown,
    ///     step 16's hook - <see cref="AiInputState.MeetGuard"/>) until the hit or <c>MeetAiReadMaxHoldSeconds</c>; a hit sooner than the reaction =
    ///     too late. Never you: your guard is read at the hit, never steered.
    /// Master switch first (<see cref="ImpactMeetRules.Enabled"/>): off = every read guard let go, nothing judged. Main thread; a fixed pool.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>Plumbing: the readers at once (a read lasts at most ~1.5 s - a few dozen in a big battle).</summary>
        internal const int MeetMaxReaders = 128;

        private readonly ImpactMeetStats _meet = new ImpactMeetStats();
        private readonly TrackedAgent?[] _meetReaders = new TrackedAgent?[MeetMaxReaders];
        private int _meetReaderCount;
        private readonly bool[] _meetFirst = new bool[48]; // 1-3 the reads (3 a push's), 16 + row the first meet per row, 30 + row its first outcome

        /// <summary>This mission's meet numbers (the smoke reads them).</summary>
        internal ImpactMeetStats ImpactMeetStats => _meet;

        /// <summary>The reads running now (the smoke).</summary>
        internal int MeetReadersRunning => _meetReaderCount;

        /// <summary>The engine reads (the smoke swaps in a stand-in).</summary>
        internal IMeetBody MeetBody { get; set; } = MeetBodyDefault.Current;

        /// <summary>The read's dice (the smoke seeds its own).</summary>
        internal IRandomSource MeetDice { get; set; } = ThreadSafeRandom.Shared;

        private void StartImpactMeet()
        {
            TraxLog.Info("impact", "mission start, meeting kicks and bashes (step 82): " + ImpactMeetRules.From(TraxSettings.Shared).Describe()
                + " - read live; the [summary] \"impact meets\" lines count each meeting by guard, the outcomes on each man, the AI's reads by bar band (PLAYTEST D47)");
        }

        private bool MeetFirst(int i)
        {
            if (_meetFirst[i]) return false;
            _meetFirst[i] = true;
            return true;
        }

        // ------------------------------------------------------------------ the guard at contact

        /// <summary>The defender's guard read and judged into <paramref name="f"/> (a kick / bash on a man on foot, the rules on - the caller
        /// checks); a correct meet also takes the guarding item's weight and skill. A failing read = counted, NotJudged (step 65's decision).</summary>
        private void FillMeet(Agent victim, ref ImpactFacts f, bool blocked)
        {
            MeetGuardRead g;
            try
            {
                g = MeetBody.Guard(victim);
            }
            catch (Exception e)
            {
                _meet.ReadFailed++;
                Failed("impact.meet-read", e);
                return;
            }
            _meet.NoteGuard(g.Kind, g.Dir);
            f.Meet = ImpactMeetMath.Classify(f.Event == ImpactEvent.Bash, blocked, g.Kind, g.Dir, f.Push); // step 88: a push's correct meet = a guard UP
            f.AltBlocked = blocked;
            if (ImpactMeetMath.AttackerAtRisk(f.Meet))
            {
                f.GuardWeight = g.GuardKg;
                f.MeetGuardSkill = g.GuardSkill;
            }
            int k = (int)f.Meet;
            if (k > 0 && k < ImpactMeetMath.MeetCount && MeetFirst(16 + k))
                TraxLog.Info("impact", "first meet " + ImpactMeetMath.MeetName(f.Meet) + " at " + Sec(SpikeNow) + " s (step 82) - the guard read at contact: "
                    + ImpactMeetMath.GuardName(g.Kind, g.Dir) + " (channel 1 action " + g.ActionType + ", its direction " + g.ActionDirection + ", the defend flag 0x"
                    + g.DefendFlag.ToString("X") + (blocked ? ", the shield STOPPED the " + (f.Push ? "push" : "bash") : ", it landed") + ")");
        }

        // ------------------------------------------------------------------ the decision applied (from ImpactDecide)

        /// <summary>A judged meet: counted (the outcomes on each man); when the kicks' family applies, a correct meet's attacker stagger / fall
        /// and a stopped bash's blocker outcomes queued for the tick (a landed one's defender outcomes are ApplyKick's).</summary>
        private void ApplyMeet(TrackedAgent a, TrackedAgent v, in ImpactDecision d, double now, bool applied, int attackerBand)
        {
            if (d.Meet == ImpactMeet.NotJudged) return;
            bool defStaggered, held;
            if (d.MeetAttacker)
            {
                defStaggered = false;
                held = true;
            }
            else if (d.MeetBlocked)
            {
                defStaggered = d.Stagger.Happens;
                held = !d.Stagger.Happens && !d.Knockdown.Happens;
            }
            else
            {
                held = d.Braced;
                defStaggered = !held && !d.Knockdown.Happens;
            }
            bool reader = v.MeetReading && ReferenceEquals(v.MeetReadBy, a);
            _meet.NoteDecision(d.Meet, d.Riposte.Happens, d.ThrownBack.Happens, defStaggered, d.Knockdown.Happens, d.ShieldDrop.Happens, held,
                d.AttackerIsPlayer || d.DefenderIsPlayer, reader);
            if (applied)
            {
                if (d.MeetAttacker)
                {
                    if (d.ThrownBack.Happens) QueueBlock(ImpactBlockAction.ThrownBack, a, v, in d, now, d.ThrownBackGetUpFactor, attackerBand);
                    else if (d.Riposte.Happens) QueueBlock(ImpactBlockAction.Riposte, a, v, in d, now, d.RiposteStunSeconds, attackerBand);
                }
                else if (d.MeetBlocked)
                {
                    if (d.Knockdown.Happens) QueueBlock(ImpactBlockAction.OwnBlow, v, a, in d, now, d.GetUpFactor, attackerBand);
                    else if (d.Stagger.Happens) QueueBlock(ImpactBlockAction.Stagger, v, a, in d, now, d.StaggerSeconds, attackerBand);
                    if (d.ShieldDrop.Happens)
                    {
                        if (_live.Disabled(ImpactLever.ShieldDrop)) _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.LeverOff);
                        else if (_liveDropCount >= _liveDrops.Length) _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.PoolFull);
                        else _liveDrops[_liveDropCount++] = new ImpactLiveDrop { St = v, By = a, At = now, Bar = d.Bar, Block = true, Event = d.Event };
                    }
                }
            }
            int k = 30 + (int)d.Meet;
            bool any = d.Riposte.Happens || d.ThrownBack.Happens || d.Knockdown.Happens || d.Stagger.Happens || d.ShieldDrop.Happens;
            if (any && k < _meetFirst.Length && MeetFirst(k))
                TraxLog.Info("impact", "first meet outcome - " + ImpactMeetMath.MeetName(d.Meet) + " at " + Sec(now) + " s: " + Name(a) + " (bar " + Pct0(a.Fraction) + ") on "
                    + Name(v) + " (bar " + Pct0(v.Fraction) + ")" + (reader ? ", who READ it" : string.Empty) + " → " + MeetOutcomeText(in d) + (applied ? string.Empty : " (decided, not applied)"));
            if (d.AttackerIsPlayer || d.DefenderIsPlayer)
                TraxLog.Info("impact", "YOU: " + (d.AttackerIsPlayer ? "your " + ImpactMath.EventName(d.Event) + " on " + Name(v) : Name(a) + "'s " + ImpactMath.EventName(d.Event) + " on you")
                    + " - met: " + ImpactMeetMath.MeetName(d.Meet) + " → " + MeetOutcomeText(in d));
        }

        private static string MeetOutcomeText(in ImpactDecision d)
        {
            if (d.MeetAttacker)
                return "the ATTACKER " + (d.ThrownBack.Happens ? "falls (" + Pct0(d.ThrownBack.Chance) + ")" : d.Riposte.Happens ? "staggers (" + Pct0(d.Riposte.Chance) + ", " + F2(d.RiposteStunSeconds) + " s)"
                    : "keeps his feet (stagger " + Pct0(d.Riposte.Chance) + ", fall " + Pct0(d.ThrownBack.Chance) + ", x_r " + F2(d.XRiposte) + ")") + "; the defender holds";
            string s = "the defender " + (d.Knockdown.Happens ? "falls (" + Pct0(d.Knockdown.Chance) + ")" : d.Stagger.Happens ? "staggers (" + Pct0(d.Stagger.Chance) + ")"
                : "holds (stagger " + Pct0(d.Stagger.Chance) + ")");
            if (d.ShieldDrop.Happens) s += ", drops his shield (" + Pct0(d.ShieldDrop.Chance) + ")";
            return s + " (x " + F2(d.X) + ")";
        }

        // ------------------------------------------------------------------ the AI's read

        /// <summary>
        /// A kick / bash of <paramref name="attacker"/> is starting at <paramref name="target"/> (an AI attempt at its start; any other one when the
        /// poll sees its action - you): an AI target rolls ONCE whether he reads it. Never you, a rider, a man down / reeling, in his own kick, or
        /// reading already. Main thread; no engine call but the two property reads (only when he may read).
        /// </summary>
        internal void MeetReadStart(TrackedAgent attacker, TrackedAgent? target, bool bash, double now) => MeetReadStart(attacker, target, bash, now, push: false);

        /// <summary>Step 88: ... a weapon PUSH (<paramref name="push"/>, with <paramref name="bash"/>) is read with the guard UP - its correct meet.</summary>
        internal void MeetReadStart(TrackedAgent attacker, TrackedAgent? target, bool bash, double now, bool push)
        {
            var m = ImpactMeetRules.From(TraxSettings.Shared);
            if (!m.AiReads) return; // the master switch first (inside)
            var s = TraxSettings.Shared;
            if (!s.AthleticsEnabled || !s.ImpactEnabled || !s.ImpactKicksEnabled) return;
            if (target == null || target.Removed || ReferenceEquals(target, attacker) || IsPlayer(target)) return;
            if (ReferenceEquals(target.MeetReadRolledFor, attacker) && !double.IsNaN(target.MeetReadRolledAt) && now - target.MeetReadRolledAt <= m.HoldSeconds) return; // this start rolled once
            target.MeetReadRolledFor = attacker;
            target.MeetReadRolledAt = now;
            if (target.MeetReading || target.AiKickPhase != AiKickPhase.None || target.Impact.Down(now) || target.Impact.Reeling(now) || !MeetBody.CanRead(target))
            {
                _meet.ReadSkippedBusy++;
                return;
            }
            MeetBody.Ability(target, out float block, out float random);
            double chance = ImpactMeetMath.ReadChance(m.ReadScale, block, random);
            bool yes = chance > 0 && MeetDice.NextDouble() < chance;
            int band = ImpactMath.BarBand(target.Fraction);
            _meet.NoteRead(band, chance, yes);
            if (!yes) return;
            if (_meetReaderCount >= _meetReaders.Length)
            {
                _meet.ReadSkippedPool++;
                return;
            }
            target.MeetReading = true;
            target.MeetReadBash = bash;
            target.MeetReadPush = bash && push;
            target.MeetGuardUp = false;
            target.MeetReadStart = now;
            target.MeetReadBy = attacker;
            _meetReaders[_meetReaderCount++] = target;
            if (MeetFirst(bash && push ? 3 : 1))
                TraxLog.Info("impact", "first AI READ of a " + (bash && push ? "weapon push" : bash ? "bash" : "kick") + " at " + Sec(now) + " s (step 82) - " + Name(target) + " (bar " + Pct0(target.Fraction)
                    + ") reads " + Name(attacker) + "'s " + (bash ? "shield bash" : "kick") + ": chance " + Pct0(chance) + " (block decision " + F2(block) + " x the right direction "
                    + Pct0(TiredBlockMath.RightShare(random)) + " x " + Pct0(m.ReadScale) + ") - the guard " + (bash && push ? "UP" : bash ? "RIGHT" : "DOWN") + " from " + F2(m.ReactionSeconds) + " s on");
        }

        /// <summary>Every tick (from TickAthletics, own try there): off = every read let go; a reader's guard raised once the reaction time has
        /// passed (step 16's hook); a read with no hit within the hold let go; gone / taken over = let go.</summary>
        internal void TickMeetReads(double now)
        {
            if (_meetReaderCount == 0) return;
            var m = ImpactMeetRules.From(TraxSettings.Shared);
            if (!m.AiReads)
            {
                EndAllMeetReads(now, engine: true);
                return;
            }
            var body = MeetBody;
            for (int i = 0; i < _meetReaderCount; i++)
            {
                var st = _meetReaders[i]!;
                try
                {
                    if (st.Removed || !body.CanRead(st))
                    {
                        _meet.ReadEndedGone++;
                        EndMeetRead(i--, now, engine: !st.Removed);
                        continue;
                    }
                    if (now - st.MeetReadStart > m.HoldSeconds)
                    {
                        _meet.ReadEndedTimeout++;
                        EndMeetRead(i--, now, engine: true);
                        continue;
                    }
                    if (!st.MeetGuardUp && ImpactMeetMath.GuardUp(now, st.MeetReadStart, m.ReactionSeconds))
                    {
                        st.MeetGuardUp = true;
                        _meet.ReadGuardsRaised++;
                        EnsureInput(st);
                        AiInputHook.SetMeetGuard(st, true, ImpactMeetMath.GuardFlag(st.MeetReadBash, st.MeetReadPush), now); // step 88: UP against a push
                        NoteHooked(st, body.Hook(st));
                    }
                }
                catch (Exception e)
                {
                    Failed("impact.meet-read-tick", e);
                    EndMeetRead(i--, now, engine: false);
                }
            }
        }

        /// <summary>OnMeleeHit (after the decision): a kick / bash of the man he read hit him - in time or too late, met correctly or not; the read ends.</summary>
        private void MeetReadHitTaken(TrackedAgent? a, TrackedAgent? v, double now)
        {
            if (v == null || a == null || !v.MeetReading || !ReferenceEquals(v.MeetReadBy, a)) return;
            var m = ImpactMeetRules.From(TraxSettings.Shared);
            bool late = ImpactMeetMath.TooLate(v.MeetReadStart, now, m.ReactionSeconds);
            bool met = Math.Abs(LastImpact.Now - now) < 1e-6 && ImpactMeetMath.AttackerAtRisk(LastImpact.Meet);
            _meet.NoteReadHit(ImpactMath.BarBand(v.Fraction), late, met);
            if (late && MeetFirst(2))
                TraxLog.Info("impact", "first AI read TOO LATE at " + Sec(now) + " s (step 82) - " + Name(v) + "'s guard was due " + F2(m.ReactionSeconds) + " s after "
                    + Name(a) + "'s " + (v.MeetReadPush ? "push" : v.MeetReadBash ? "bash" : "kick") + " began; it hit after " + F2(now - v.MeetReadStart) + " s");
            for (int i = 0; i < _meetReaderCount; i++)
                if (ReferenceEquals(_meetReaders[i], v))
                {
                    _meet.ReadEndedHit++;
                    EndMeetRead(i, now, engine: true);
                    return;
                }
        }

        private void EndMeetRead(int i, double now, bool engine)
        {
            var st = _meetReaders[i]!;
            st.MeetReading = false;
            st.MeetGuardUp = false;
            st.MeetReadBy = null;
            if (st.Input != null) _meet.ReadFrames += st.Input.MeetFrames;
            if (st.Input != null) st.Input.MeetFrames = 0;
            AiInputHook.SetMeetGuard(st, false, 0u, now);
            if (engine && !st.Removed)
            {
                try
                {
                    MeetBody.Release(st);
                }
                catch (Exception e)
                {
                    Failed("impact.meet-unhook", e);
                }
            }
            _meetReaders[i] = _meetReaders[--_meetReaderCount];
            _meetReaders[_meetReaderCount] = null;
        }

        private void EndAllMeetReads(double now, bool engine)
        {
            while (_meetReaderCount > 0) EndMeetRead(_meetReaderCount - 1, now, engine);
        }

        /// <summary>He left the field (Forget): his read ended with no engine call.</summary>
        private void MeetLeftField(TrackedAgent st)
        {
            if (!st.MeetReading) return;
            for (int i = 0; i < _meetReaderCount; i++)
                if (ReferenceEquals(_meetReaders[i], st))
                {
                    _meet.ReadEndedGone++;
                    EndMeetRead(i, SafeNow(), engine: false);
                    return;
                }
        }

        /// <summary>Step 82: a kick / bash seen starting by the poll (step 18) - not an AI attempt of ours (those rolled at their start): the man it is
        /// aimed at (your target - the nearest enemy in reach in front) may read it.</summary>
        internal void MeetKickSeen(TrackedAgent st, KickBashKind kind, double now)
        {
            if (kind == KickBashKind.None || st.AiKickPhase != AiKickPhase.None || st.Removed) return;
            var m = ImpactMeetRules.From(TraxSettings.Shared);
            if (!m.AiReads) return;
            try
            {
                var s = TraxSettings.Shared;
                var aim = MeetBody.Aim(st, s.AiKickReachMetres, s.AiKickFacingDegrees);
                if (aim == null) return;
                bool push = false;
                if (kind == KickBashKind.Bash)
                {
                    // step 88: your bash with no shield in your off hand is a WEAPON PUSH - the man it is aimed at reads it with the guard UP
                    AiKickBody.Hands(st, out _, out _, out _, out bool shield);
                    push = !shield;
                }
                MeetReadStart(st, Get(aim), kind == KickBashKind.Bash, now, push);
            }
            catch (Exception e)
            {
                Failed("impact.meet-aim", e);
            }
        }

        /// <summary>The summary's turn (after the model's lines): every read ended first (no engine call - the mission ends).</summary>
        private void WriteImpactMeetSummary()
        {
            EndAllMeetReads(SafeNow(), engine: false);
            foreach (var line in _meet.SummaryLines(ImpactMeetRules.From(TraxSettings.Shared))) TraxLog.Info("summary", line);
        }
    }
}
