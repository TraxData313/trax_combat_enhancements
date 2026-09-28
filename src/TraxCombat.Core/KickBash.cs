namespace TraxCombat.Core
{
    /// <summary>What a kick / shield bash action is (step 18).</summary>
    public enum KickBashKind
    {
        None = 0,

        /// <summary>A kick (the kick key) - the engine's Kick, KickContinue, KickHit actions.</summary>
        Kick = 1,

        /// <summary>A shield bash (attack while blocking; staff and two-hander bashes too) - WeaponBash.</summary>
        Bash = 2,
    }

    /// <summary>
    /// Step 18 (Anton, 2026-09-28 - DESIGN §2 "What is a blow", AI_NOTES "Step 18"): ONE decision per kick
    /// or shield bash - "is this a new one, to be charged now?" - for one fighter, pure (the module feeds it
    /// the engine's action codes). Kicks and bashes cost <c>CostPerKickOrBash</c> once each, when they
    /// start; they never start the attack timer (that is the caller's rule).
    ///
    /// Where a kick or bash is seen: the poll of action channel 1 (the upper body - a shield bash showed
    /// there in the playtest log of 2026-09-27) and of channel 0 (the whole body - the game's own
    /// <c>StandingPoint</c> reads kicks there); the channel a kick plays on is not proven in game, so both
    /// are read and the summary counts which one saw it. One action on both channels is ONE kick (the
    /// channels are OR-ed: it starts when the first shows it, ends when neither does). The hit
    /// (<c>IsAlternativeAttack</c> in OnMeleeHit) is the fallback: a kick or bash that lands while no
    /// channel shows one is charged at the hit, and the poll's later sight of the same action (within
    /// <see cref="SameActionSeconds"/>) is not charged again - so a bash that also lands is charged once.
    /// </summary>
    public sealed class KickBashTracker
    {
        /// <summary>The engine's <c>Agent.ActionCodeType</c> values (the offline smoke checks them against
        /// the game's enum).</summary>
        public const int ActionKick = 28, ActionKickContinue = 29, ActionKickHit = 30, ActionWeaponBash = 31;

        /// <summary>A kick / bash hit and a poll sighting (or two hits) this close together are the same
        /// action - shorter than any kick or bash animation, longer than the frame or two between a hit and
        /// the poll. Engine plumbing, not a gameplay number.</summary>
        public const double SameActionSeconds = 1.0;

        /// <summary>The kind of kick / bash an engine action code is (None for every other action).</summary>
        public static KickBashKind KindOf(int action) => action switch
        {
            ActionKick => KickBashKind.Kick,
            ActionKickContinue => KickBashKind.Kick,
            ActionKickHit => KickBashKind.Kick,
            ActionWeaponBash => KickBashKind.Bash,
            _ => KickBashKind.None,
        };

        /// <summary>What channel 1 (the upper body) shows now, as last seen.</summary>
        public KickBashKind Upper { get; private set; }

        /// <summary>What channel 0 (the whole body) shows now, as last seen.</summary>
        public KickBashKind Lower { get; private set; }

        /// <summary>A kick or bash is running on either channel.</summary>
        public bool Running => Upper != KickBashKind.None || Lower != KickBashKind.None;

        /// <summary>When the last kick / bash was decided (charged, or free by the rules) - −∞ = never.</summary>
        public double DecidedAt { get; private set; } = double.NegativeInfinity;

        /// <summary>The last one was decided at its HIT, before any channel showed it; the first sighting
        /// within <see cref="SameActionSeconds"/> is that same action.</summary>
        public bool DecidedByHit { get; private set; }

        /// <summary>
        /// A channel's action changed to <paramref name="action"/>. Returns the kind of a NEW kick or bash that
        /// starts here - to be decided (charged) now - or None: not a kick / bash, the same one going on (Kick →
        /// KickHit), the other channel already shows it, or its hit was charged first.
        /// </summary>
        public KickBashKind Observe(bool lowerChannel, int action, double now)
        {
            var kind = KindOf(action);
            var before = lowerChannel ? Lower : Upper;
            bool wasRunning = Running;
            if (lowerChannel) Lower = kind;
            else Upper = kind;
            if (kind == KickBashKind.None || kind == before) return KickBashKind.None;
            if (before == KickBashKind.None && wasRunning) return KickBashKind.None; // the other channel shows it already
            // new: a rising edge (nothing ran), or this channel went straight from one kind to the other
            if (DecidedByHit && now - DecidedAt < SameActionSeconds)
            {
                DecidedByHit = false; // the one its hit already paid for
                return KickBashKind.None;
            }
            DecidedByHit = false;
            DecidedAt = now;
            return kind;
        }

        /// <summary>
        /// A kick or bash LANDED (a hit, a block, a shield). True = decide (charge) it now: no channel shows a
        /// kick or bash and none was decided within <see cref="SameActionSeconds"/> - the poll missed it. False
        /// = the running or just-ended one, already decided at its start.
        /// </summary>
        public bool Hit(double now)
        {
            if (Running) return false;
            if (now - DecidedAt < SameActionSeconds) return false;
            DecidedAt = now;
            DecidedByHit = true;
            return true;
        }
    }
}
