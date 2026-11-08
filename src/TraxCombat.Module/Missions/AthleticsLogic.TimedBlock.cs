using System;
using System.Globalization;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 100b - TIMED BLOCKS (Toni 2026-11-08; AI_NOTES "Step 100", DESIGN decision 91): the GUARD CLOCK and the timing at contact.
    ///   - YOUR clock (<see cref="TrackedAgent.Guard"/>): every tick your guard key (<see cref="IGuardBody.GuardKey"/>) is noted - the press
    ///     starts it; in a manual block-direction mode a switch to another side starts it again; in the game's AUTOMATIC mode only the press
    ///     counts. Read once more at contact (a guard raised this very frame).
    ///   - THE AI at contact: its channel-1 defend - an ACTIVE one's age = progress × its length, a PASSIVE one = held (score 0), a parry
    ///     reaction or anything else = unknown (score 0). A chamber block = perfect.
    ///   - THE DIAGNOSTIC (part 1 of 100b, never a gate): every correct block's engine verdict (Blocked / Parried / Chamber) × the channel-1
    ///     read × your clock's age band, the active defends' lengths - the [summary] "timed blocks - the engine's verdict" line.
    ///   - THE SEAM: every decided correct block becomes a <see cref="TimedBlockResult"/> (<see cref="TimedBlockDecided"/>) and a timed
    ///     riposte the engine took calls <see cref="TimedRiposteAccepted"/> - 100d (the push-out) and 100e (the slow-mo, built) hook there;
    ///     100c (riders) was dropped by Toni 2026-11-08 - riders keep the refused riposte.
    /// Master switch first (through <see cref="ImpactRules.Enabled"/> and <see cref="TimedBlockRules.Enabled"/>): off = no clock reads, the
    /// clock forgotten, every block untimed (step 56 exactly). Main thread; no allocation per tick or event.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly TimedBlockStats _timed = new TimedBlockStats();
        private bool _timedClockOn;
        private int _timedYouLines;

        /// <summary>Step 100b: the guard reads (the smoke swaps in its stand-in).</summary>
        internal IGuardBody GuardBody { get; set; } = GuardBodyDefault.Current;

        internal TimedBlockStats TimedBlockStats => _timed;

        /// <summary>The last decided correct block's result (the smoke reads it).</summary>
        internal TimedBlockResult LastTimedBlock { get; private set; }

        /// <summary>Every tick (from <see cref="TickImpact"/>): your guard key into your clock while timed blocks can apply; off = forgotten.</summary>
        private void TickTimedBlocks(double now)
        {
            var ir = ImpactRules.From(TraxSettings.Shared);
            bool on = ir.Enabled && ir.Timed.Enabled;
            var you = PlayerRecord();
            if (!on)
            {
                if (_timedClockOn && you != null) you.Guard.Reset();
                if (_timedClockOn) TraxLog.Info("impact", "timed blocks: the guard clock stopped at " + Sec(now) + " s (" + (ir.Enabled ? ir.Timed.OffBecause : "the impact system off") + ")");
                _timedClockOn = false;
                return;
            }
            if (!_timedClockOn)
            {
                _timedClockOn = true;
                TraxLog.Info("impact", "timed blocks: the guard clock runs from " + Sec(now) + " s - " + ir.Timed.Describe() + "; block direction "
                    + (GuardBody.AutomaticDirection ? "AUTOMATIC (only the press counts)" : "manual (a switch to another side counts as a new guard)"));
            }
            if (you == null || you.Removed || !YouDrive(you)) return;
            you.Guard.Note(GuardBody.GuardKey(you.Agent), now, GuardBody.AutomaticDirection);
        }

        /// <summary>
        /// At contact (the hit callback, a CORRECT block only): the timing into <paramref name="f"/> - a chamber, your clock (read once more
        /// now), or the AI's channel-1 defend - and the diagnostic's counts. A failing read leaves the block untimed.
        /// </summary>
        private void FillTiming(TrackedAgent? v, Agent victim, in AttackCollisionData cd, double now, ref ImpactFacts f)
        {
            if (v == null || !ImpactMath.CanRiposte(f.Event)) return;
            var tr = TimedBlockRules.From(TraxSettings.Shared);
            if (!tr.Enabled) return;
            var verdict = TimedBlockMath.VerdictOf((int)cd.CollisionResult);
            var read = GuardBody.ReadContact(victim, out double activeAge, out double length, out bool cyclic, out bool continues);
            if (read == GuardRead.Active) _timed.NoteActiveLength(length, cyclic, continues);
            bool yours = IsPlayer(v) && YouDrive(v);
            double yourAge = double.NaN;
            if (yours)
            {
                v.Guard.Note(GuardBody.GuardKey(victim), now, GuardBody.AutomaticDirection);
                yourAge = v.Guard.Age(now);
            }
            _timed.NoteVerdict(verdict, read, yourAge, yours);
            if (verdict == BlockVerdict.Chamber)
            {
                f.TimingSource = TimingSource.Chamber;
                f.GuardAge = 0;
            }
            else if (yours)
            {
                f.TimingSource = double.IsNaN(yourAge) ? TimingSource.None : TimingSource.PlayerClock;
                f.GuardAge = yourAge;
            }
            else if (read == GuardRead.Active && !double.IsNaN(activeAge))
            {
                f.TimingSource = TimingSource.AiActive;
                f.GuardAge = activeAge;
            }
            else if (read == GuardRead.Passive)
            {
                f.TimingSource = TimingSource.AiHeld;
                f.GuardAge = double.PositiveInfinity;
            }
        }

        /// <summary>THE SEAM: one decided correct block (after <see cref="ImpactMath.Decide"/>) - counted, your lines; 100c / 100d / 100e hook
        /// here. <paramref name="live"/> = the riposte really applies (not shadow).</summary>
        private void TimedBlockDecided(TrackedAgent a, TrackedAgent v, in ImpactDecision d, bool live)
        {
            if (!ImpactMath.CanRiposte(d.Event)) return;
            var t = new TimedBlockResult(in d);
            LastTimedBlock = t;
            _timed.Note(in t, in d);
            YourBlockHeldMessage(a, v, in t, live); // step 100f: your block's timing on screen, even when he holds
            if (!t.Timed || !(t.BlockerIsPlayer || t.AttackerIsPlayer)) return;
            var c = CultureInfo.InvariantCulture;
            string words = TimedBlockMath.TimingWords(in t);
            string s;
            if (t.BlockerIsPlayer)
                s = "your timed " + ImpactMath.EventName(d.Event) + " (" + words + ") of " + Name(a) + ": "
                    + (t.RiposteHappens ? "he reels " + t.Seconds.ToString("0.00", c) + " s (untimed " + F2(t.Seconds56) + " s)" : "he holds")
                    + " - the riposte " + PctOrDash(t.Chance) + " (untimed " + PctOrDash(t.Chance56) + ")";
            else
                s = Name(v) + " timed his " + ImpactMath.EventName(d.Event) + " on you (" + words + "): "
                    + (t.RiposteHappens ? "you reel " + t.Seconds.ToString("0.00", c) + " s" + (t.CappedForYou ? " (held at the you-cap)" : string.Empty) : "you hold")
                    + " - the riposte " + PctOrDash(t.Chance) + " (untimed " + PctOrDash(t.Chance56) + ")";
            if (!live) s += " (shadow - nothing applied)";
            _timedYouLines++;
            TraxLog.Info("impact", "YOU: " + s);
        }

        /// <summary>
        /// Step 100f (Toni's playtest: learn the timing from the screen): YOUR correct block that brought no riposte - its own on-screen line
        /// with the numbers ("Your parry - X holds (guard 0.30 s / 0.15)"), behind <c>TimedBlockShowTiming</c> and step 53's
        /// <c>ImpactShowMessages</c>; a riposte speaks when the engine takes it (<see cref="RiposteTimingText"/>). Live blocks only.
        /// </summary>
        private void YourBlockHeldMessage(TrackedAgent a, TrackedAgent v, in TimedBlockResult t, bool live)
        {
            if (!live || !t.BlockerIsPlayer || t.RiposteHappens || !TraxSettings.Shared.TimedBlockShowTiming) return;
            var tr = TimedBlockRules.From(TraxSettings.Shared);
            string? text = TimedBlockMath.YourBlockLine(MessageName(a), t.Event, false, double.NaN, tr.StaggerMax, t.Source, t.Age, tr.Perfect);
            if (text != null) ImpactMessage(ImpactMessageKind.BlockHeld, v, a, text);
        }

        /// <summary>Step 100f: the words of YOUR riposte the engine took, with its numbers ("Your riposte - X reels 1.46 s / 2.00 (guard 0.30 s /
        /// 0.15)"); null = the plain line (not yours, the switch off, no timing read).</summary>
        private string? RiposteTimingText(in ImpactBlockPending p)
        {
            if (p.Other == null || p.Other.Removed || !IsPlayer(p.Other) || !TraxSettings.Shared.TimedBlockShowTiming) return null;
            var tr = TimedBlockRules.From(TraxSettings.Shared);
            if (!tr.Enabled) return null;
            return TimedBlockMath.YourBlockLine(MessageName(p.St), p.Event, true, p.Asked, tr.StaggerMax, p.Source, p.GuardAge, tr.Perfect);
        }

        /// <summary>THE SEAM (the tick): the engine took a riposte stagger this block decided - counted by its timing band. 100d's push-out and
        /// 100e's slow-mo start from here (a timed one, the score in <paramref name="score"/>).</summary>
        private void TimedRiposteAccepted(TrackedAgent attacker, TrackedAgent? blocker, double score, ImpactEvent ev, double now)
        {
            _timed.NoteApplied(score);
            MaybeStartSlowMo(blocker, score); // step 100e: the slow motion on your best blocks (its own try)
            MaybePushOut(blocker, ev, score, now); // step 100d: the blocker's shield / weapon goes out (its own try)
            MaybeOpenPerfectStrike(attacker, blocker, score, now); // step 100g: YOUR timed block opens a perfect-strike window on him (its own try)
            if (score > 0 && LiveFirst(LiveFirstTimedRiposte))
                TraxLog.Info("impact", "live: first TIMED riposte at " + Sec(now) + " s - " + Name(attacker) + " staggered after " + (blocker != null ? Name(blocker) : "(someone)")
                    + "'s block, timing score " + F2(score));
        }

        /// <summary>The summary's turn (after the model's lines).</summary>
        private void WriteTimedBlockSummary()
        {
            foreach (var line in _timed.SummaryLines(TimedBlockRules.From(TraxSettings.Shared))) TraxLog.Info("summary", line);
        }

        private static string PctOrDash(double c) => double.IsNaN(c) ? "-" : Pct0(c);

        /// <summary>The live part's first-time slot of the first timed riposte (step 51's slots end at 21).</summary>
        private const int LiveFirstTimedRiposte = 22;
    }
}
