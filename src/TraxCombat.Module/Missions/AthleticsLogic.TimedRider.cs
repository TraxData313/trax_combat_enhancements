using System;
using System.Globalization;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 104a - TIMED BLOCKS ON HORSEBACK, THE SHADOW (Toni 2026-11-09: "can you add the reel vs mounted troops as well somehow, like that 3rd
    /// round, we were on horsebacks fighting, I blocked"; AI_NOTES "Step 104", DESIGN decision 93). Every correct block (right-side shield,
    /// parry) whose attacker RIDES - rider vs rider, a man on foot vs a rider - and every one a rider makes against a man on foot is handed to Core
    /// <see cref="TimedRiderMath.Decide"/> right after the impact decision: what 104b-104d WOULD do (the reel's chance and length, the horse's
    /// check, the knock-off and the rear, your perfect strike window and the slow motion). NOTHING IS APPLIED - not the reel, not a state, not an
    /// action; the shadow rolls ITS OWN dice (<see cref="RiderShadowDice"/>), so the live decisions' dice never move. The [impact] "rider shadow"
    /// lines (the first in full, yours always, the rest verbose) and the [summary] "timed blocks on horseback (shadow)" line. Main thread (the hit
    /// callback); no allocation unless a line is written.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly TimedRiderStats _riderShadow = new TimedRiderStats();
        private bool _riderShadowFirstLine, _riderShadowDisabled;

        /// <summary>Step 104a: the shadow's own dice (the smoke seeds its own) - never the impact model's.</summary>
        internal IRandomSource RiderShadowDice { get; set; } = new ThreadSafeRandom();

        /// <summary>Step 104a: this mission's rider shadow decisions (the smoke reads them).</summary>
        internal TimedRiderStats RiderShadowStats => _riderShadow;

        /// <summary>Step 104a: the last shadow decision (the smoke).</summary>
        internal TimedRiderShadow LastRiderShadow { get; private set; }

        private void StartRiderShadow()
        {
            TraxLog.Info("impact", "mission start: " + TimedRiderRules.From(TraxSettings.Shared).Describe()
                + " (step 104a; read live; the [impact] \"rider shadow\" lines and the [summary] \"timed blocks on horseback (shadow)\" line)");
        }

        /// <summary>From ImpactDecide, after the decision, for a correct block with a rider on either end. <paramref name="attacker"/> = his
        /// impact state as the decision left it (a rider's riposte is refused, so it is the state before). Step 104b: returns true when the
        /// reel it rolled is APPLIED (<see cref="QueueRiderReel"/> - <c>TimedBlocksOnHorseback</c> on, the riposte live); the rear / knock-off
        /// it rolls after the reel stay shadow (104c).</summary>
        private bool RiderShadow(TrackedAgent a, TrackedAgent v, in ImpactFacts f, in ImpactDecision d, in ImpactRules r, ImpactState attacker, double now)
        {
            if (_riderShadowDisabled) return false;
            bool live = false;
            try
            {
                var rr = TimedRiderRules.From(TraxSettings.Shared);
                if (!rr.ShadowOn) return false;
                var dice = RiderShadowDice;
                var tf = new TimedRiderFacts
                {
                    Event = f.Event,
                    BlockerMounted = f.DefenderMounted,
                    AttackerMounted = f.AttackerMounted,
                    BlockerIsPlayer = f.DefenderIsPlayer,
                    AttackerIsPlayer = f.AttackerIsPlayer,
                    AttackerPassive = f.AttackerPassive,
                    Score = d.TimedScore,
                    RowInRow = d.Riposte.InRow,
                    RowChance = d.Riposte.InRow ? d.Riposte.Chance : double.NaN,
                    RowRefusal = d.Riposte.Refusal,
                    StunCap = r.StunCap,
                    XRiposte = d.XRiposte,
                    HorseSpeed = f.AttackerSpeed,
                    AttackerReeling = attacker.Reeling(now),
                    AttackerImmune = attacker.StaggerImmune(now),
                    TiredMountMultiplier = a.MountSpeedMultiplier,
                    FootRiposteHappened = d.Riposte.Happens,
                    Length56 = double.NaN,
                };
                double natural = double.NaN;
                if (f.AttackerMounted)
                {
                    if (d.Riposte.InRow && d.TimedScore > 0) tf.Length56 = ImpactMath.RiposteStun56(in r, in d, dice.NextDouble()); // its own die
                    if (!TimedRiderMath.IsNone(rr.ReelAction) && AnimBody.Find(a, rr.ReelAction, out _, out double len)) natural = len;
                }
                var o = TimedRiderMath.Decide(in rr, in tf, natural, dice);
                LastRiderShadow = o;
                _riderShadow.Note(in o, f.DefenderIsPlayer, f.AttackerIsPlayer);
                if (!o.Asked) return false;
                live = QueueRiderReel(a, v, in o, in rr, in r, in d, now); // step 104b: the reel APPLIED (the tick starts it)
                bool yours = f.DefenderIsPlayer || f.AttackerIsPlayer;
                if (!_riderShadowFirstLine)
                {
                    _riderShadowFirstLine = true;
                    TraxLog.Info("impact", "first rider shadow at " + Sec(now) + " s (step 104a): " + TimedRiderMath.Describe(in o, Name(a), Name(v), live) + "; " + rr.Describe());
                }
                else if (yours) TraxLog.Info("impact", "rider shadow: " + TimedRiderMath.Describe(in o, Name(a), Name(v), live));
                else if (TraxLog.VerboseWants("rider-shadow")) TraxLog.Verbose("impact", "rider shadow: " + TimedRiderMath.Describe(in o, Name(a), Name(v), live), "rider-shadow");
            }
            catch (Exception e)
            {
                _riderShadowDisabled = true;
                Failed("impact.rider-shadow", e);
                TraxLog.Info("impact", "the rider shadow (step 104a) is OFF for this battle after an error" + (live ? " - the reel already queued runs on (the tick gives every lock back)" : " - nothing of it was applied"));
            }
            return live;
        }

        // ------------------------------------------------------------------ the J test tool's rider candidates (step 104a)

        private int _animTestRiderStep = -1;
        private bool _animTestRiderFirstLine;
        private TrackedAgent? _rearWatch;
        private double _rearWatchUntil;
        private const double RearWatchSeconds = 1.5;

        /// <summary>
        /// The test tool on a RIDER (you mounted with nobody near, or the nearest man rides): the next rider candidate of Core
        /// <see cref="TimedRiderAnimMath.Candidates"/> - every action on his CHANNEL 1 only (his upper body; never channel 0 on a rider, never an
        /// action on the horse - RBM's crash), the mounted push-outs through step 100d's own push (channel 1, his bash guarded), the rear through
        /// the engine's own rear flag on a blow (followed: SEEN rearing within 1.5 s or not). Named on screen and in the log, taken / refused.
        /// </summary>
        private void RiderAnimTest(TrackedAgent st, TrackedAgent you, bool back, double now, in TimedAnimRules r)
        {
            var body = AnimBody;
            var cands = TimedRiderAnimMath.Candidates;
            int count = cands.Length;
            _animTestRiderStep = back ? (_animTestRiderStep <= 0 ? count - 1 : _animTestRiderStep - 1) : (_animTestRiderStep + 1) % count;
            int k = _animTestRiderStep;
            var c = cands[k];
            string who = Name(st) + (ReferenceEquals(st, you) ? " (you)" : string.Empty) + " on his horse";
            string text = RiderCandidate(st, you, c, now, in r);
            _anim.TestPlays++;
            string line = "[anim test RIDER " + (k + 1).ToString(CultureInfo.InvariantCulture) + "/" + count.ToString(CultureInfo.InvariantCulture) + "] " + who + " - " + c.Label + ": " + text;
            body.Say(line);
            if (!_animTestRiderFirstLine)
            {
                _animTestRiderFirstLine = true;
                TraxLog.Info("impact", "anim test tool, RIDER candidates (step 104a): " + r.TestKey + " on a rider (the nearest within 8 m, else a rider within 15 m, else you when mounted) = the next, Left Shift + "
                    + r.TestKey + " = the one before; " + count.ToString(CultureInfo.InvariantCulture) + " candidates - every action on the rider's channel 1 (upper body) only, never his legs, never the horse; the rear through the game's own rear flag");
            }
            TraxLog.Info("impact", "anim test: " + line);
        }

        private string RiderCandidate(TrackedAgent st, TrackedAgent you, in RiderAnimCandidate c, double now, in TimedAnimRules r)
        {
            var body = AnimBody;
            const int ch = TimedRiderAnimMath.RiderChannel; // channel 1 ONLY on a rider
            switch (c.Kind)
            {
                case RiderAnimKind.Configured:
                {
                    var rr = TimedRiderRules.From(TraxSettings.Shared);
                    if (TimedRiderMath.IsNone(rr.ReelAction)) return "none set (the lock only - nothing to see)";
                    return PlayRiderAction(st, rr.ReelAction, (float)rr.AnimSpeed);
                }
                case RiderAnimKind.Action:
                    return PlayRiderAction(st, c.Action, c.Speed);
                case RiderAnimKind.Recoil:
                {
                    string usage = body.UsageOf(st);
                    var cls = TimedRecoilMath.ClassOf(usage, body.ShieldInHand(st));
                    var names = TimedRiderAnimMath.MountedRecoilNames(cls, c.Direction, usage);
                    if (names.Length == 0)
                    {
                        _anim.TestMissing++;
                        return "none for his " + TimedRecoilMath.ClassName(cls) + " (" + (usage.Length > 0 ? usage : "no weapon") + ")";
                    }
                    foreach (var n in names)
                    {
                        if (!body.Find(st, n, out int index, out double len)) continue;
                        bool ok = body.Play(st, ch, index, c.Speed, 0f, 0.05f, 0.2f);
                        return n + " x" + F2(c.Speed) + " (" + F2(len) + " s own length) - " + (ok ? "taken" : "REFUSED by the engine");
                    }
                    _anim.TestMissing++;
                    return names[0] + " - not in his action set";
                }
                case RiderAnimKind.PushParryHeavy:
                case RiderAnimKind.PushAction:
                {
                    var kind = TimedPushOutMath.KindOf(body.ShieldInHand(st), body.UsageOf(st));
                    if (c.Kind == RiderAnimKind.PushParryHeavy && TimedPushOutMath.ParryHeavyOf(body.CurrentName(st, ch)) == null)
                        return "he is not guarding now (his heavy parry comes from his guard - raise it / wait for his guard and press again)";
                    int skippedBefore = _anim.PushSkipped[(int)PushOutSkip.NoAction];
                    bool ok = PlayPushOut(st, kind, c.Action, in r, now, IsPlayer(st), out string played, out float speed, out double seconds);
                    if (!ok && _anim.PushSkipped[(int)PushOutSkip.NoAction] > skippedBefore) _anim.TestMissing++;
                    return ok ? played + " x" + F2(speed) + " for " + F2(seconds) + " s (" + TimedPushOutMath.KindName(kind) + ") - taken" : "not played (not in his action set / refused)";
                }
                case RiderAnimKind.Rear:
                {
                    // step 104c: exactly the live rear's blow (owned by the horse itself, its 1 health given back)
                    if (!ImpactBody.OwnRearBlow(st, out var why, out double lost)) return "not asked - " + TimedRiderFallMath.RefusalName(why);
                    _rearWatch = st;
                    _rearWatchUntil = now + RearWatchSeconds;
                    return "our 1-damage blow flagged MakesRear registered on his horse (its health back - lost " + F2(lost) + ") - watching for the rear (" + F2(RearWatchSeconds) + " s)";
                }
                default:
                    return "?";
            }
        }

        /// <summary>A named action on the rider's channel 1 at <paramref name="speed"/> - found in his set first.</summary>
        private string PlayRiderAction(TrackedAgent st, string action, float speed)
        {
            var body = AnimBody;
            if (!body.Find(st, action, out int index, out double len))
            {
                _anim.TestMissing++;
                return action + " - not in his action set";
            }
            bool ok = body.Play(st, TimedRiderAnimMath.RiderChannel, index, speed, 0f, 0.05f, 0.2f);
            return action + " x" + F2(speed) + " on his upper body (" + F2(len) + " s own length, " + F2(double.IsNaN(len) ? double.NaN : len / Math.Max(0.05, speed)) + " s played) - "
                   + (ok ? "taken" : "REFUSED by the engine");
        }

        /// <summary>The rear preview, followed: his horse's channel 0 type Rear within the watch = SEEN (a read only).</summary>
        private void TickRearWatch(double now)
        {
            var st = _rearWatch!;
            try
            {
                bool seen = !st.Removed && ImpactBody.IsActive(st) && ImpactBody.MountRearing(st);
                if (!seen && now < _rearWatchUntil) return;
                _rearWatch = null;
                string line = "[anim test RIDER] the rear on " + Name(st) + "'s horse: " + (seen ? "SEEN rearing" : "NOT seen within " + F2(RearWatchSeconds) + " s (the engine did not rear him - standing still? a mount that cannot?)");
                AnimBody.Say(line);
                TraxLog.Info("impact", "anim test: " + line);
            }
            catch (Exception e)
            {
                _rearWatch = null;
                AnimFailed("impact.anim-test-rear", e);
            }
        }

        private void WriteRiderShadowSummary()
        {
            WriteRiderReelSummary(); // step 104b: the live reel's line first
            WriteRiderFallSummary(); // step 104c: the rear and the knock-off
            TraxLog.Info("summary", _riderShadow.SummaryLine(TimedRiderRules.From(TraxSettings.Shared)));
        }
    }
}
