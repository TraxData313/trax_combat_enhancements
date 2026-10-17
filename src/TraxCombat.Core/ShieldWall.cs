using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 41: a formation's arrangement as the shield wall's hold reads it - the game's three "defensive arrangements"
    /// (<c>ArrangementOrder.GetArrangementOrderDefensiveness</c> = 1: shield wall, square, circle), the column apart (vanilla never
    /// writes the flag for a column), everything else one group.</summary>
    public enum WallArrangement
    {
        ShieldWall,
        Square,
        Circle,
        Column,
        Other,
        Count,
    }

    /// <summary>
    /// Step 41: one look at one man - held in his slot (<see cref="Hold"/>), or why he is left to vanilla. The values are in GATE
    /// ORDER (<see cref="WallMath.Decide"/> answers the first that applies): the master switch first, then the switch, then the
    /// mission, the man, his formation, its order, himself, and last the escape (far from his slot and behind the line).
    /// </summary>
    public enum WallVerdict
    {
        /// <summary>Held: keep with the formation instead of walking at his target.</summary>
        Hold,

        /// <summary>The master switch (ModEnabled) is off.</summary>
        ModOff,

        /// <summary>ShieldWallHoldsSlots is off.</summary>
        SwitchedOff,

        /// <summary>A naval battle or a naval raid (War Sails): ship decks and boarding are left to the game.</summary>
        Naval,

        /// <summary>The attacking side of a siege: its wall-climb and gate orders are Moves (RBM's lesson).</summary>
        SiegeAttacker,

        /// <summary>The player, or a man the player drives (RTS Camera) - not his own AI.</summary>
        NotAi,

        /// <summary>On a horse.</summary>
        Mounted,

        /// <summary>In no formation (tournaments, arenas, duels).</summary>
        NoFormation,

        /// <summary>Detached from his formation (a siege engine, a ladder, a banner job, a detachment's standing point).</summary>
        Detached,

        /// <summary>His formation is not in a shield wall, square or circle (line, loose, scatter, skein, column).</summary>
        Arrangement,

        /// <summary>A charge order: charge, charge a target, the AI's tactical charge, attack a gate.</summary>
        ChargeOrder,

        /// <summary>A retreat order.</summary>
        RetreatOrder,

        /// <summary>No order the game names.</summary>
        NoOrder,

        /// <summary>He himself is fleeing (the engine's IsRetreating) - vanilla writes nothing for him either.</summary>
        Retreating,

        /// <summary>The escape (RBM's, vanilla's catch-up radius): far from his slot AND behind the line - left free to rejoin.</summary>
        FarBehind,

        Count,
    }

    /// <summary>
    /// Step 41: one man as the shield wall's hold reads him - the gates (the game side fills them in gate order; what a gate
    /// before it already decides may stay unread) and, for a man in a defensive arrangement, the positions on the ground (metres,
    /// the scene's x / y): him, his slot, his formation's facing and average position, and the formation's integrity numbers
    /// vanilla hands the engine with the same call. A struct: no allocation per look.
    /// </summary>
    public struct WallFacts
    {
        public bool Naval;
        public bool SiegeAttacker;
        public bool Ai;
        public bool Mounted;
        public bool HasFormation;
        public bool Detached;
        public WallArrangement Arrangement;
        public BraceOrderKind Order;
        public bool Retreating;

        /// <summary>The positions were read (a man in a defensive arrangement, not detached).</summary>
        public bool HasPositions;

        public double AgentX, AgentY;
        public double SlotX, SlotY;
        public double DirX, DirY;
        public double CentreX, CentreY;

        /// <summary>The formation's cached integrity numbers (<c>Formation.CachedFormationIntegrityData</c>): the average top
        /// speed of its men near their slots (m/s), their average velocity, the deviation of their positions (m).</summary>
        public double AvgSpeed;
        public double VelX, VelY;
        public double Deviation;
    }

    /// <summary>
    /// Step 41's settings, read live from <see cref="TraxSettings"/>: the master switch first, then ShieldWallHoldsSlots. Not tied
    /// to AthleticsEnabled - the hold is formation discipline, not tiredness.
    /// </summary>
    public readonly struct WallRules
    {
        public WallRules(bool modEnabled, bool holdsSlots)
        {
            ModEnabled = modEnabled;
            HoldsSlots = holdsSlots;
        }

        public bool ModEnabled { get; }
        public bool HoldsSlots { get; }

        public bool Enabled => ModEnabled && HoldsSlots;

        /// <summary>The switch that holds it off (the master switch first), or null when on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !HoldsSlots ? "ShieldWallHoldsSlots" : null;

        public static WallRules From(TraxSettings s) => new WallRules(s.ModEnabled, s.ShieldWallHoldsSlots);

        public bool SameAs(in WallRules o) => ModEnabled == o.ModEnabled && HoldsSlots == o.HoldsSlots;

        /// <summary>The settings in one sentence (the mission-start and change lines, the summary).</summary>
        public string Describe()
        {
            if (!Enabled)
                return "OFF (" + OffBecause + ") - vanilla: an AI man in a shield wall, square or circle may walk out of his slot at the enemy he fights";
            return "ON (ShieldWallHoldsSlots) - every AI man on foot in a shield wall, square or circle keeps his slot instead of walking at the enemy "
                   + "he fights (he still fights what reaches him), under any order but a charge or a retreat; not a man detached from his formation, "
                   + "not one fleeing, not one far (" + F0(WallMath.CatchUpSpeeds) + " x the formation's speed) from his slot AND behind the line (left free "
                   + "to rejoin); never you, riders, a siege's attackers or naval battles";
        }

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 41 - THE SHIELD WALL HOLDS ITS SLOTS (Anton, 2026-10-17: a fresh shield wall waiting for the enemy - men step 2-3 m out
    /// of it to swing and walk back; AI_NOTES "Step 41", RESEARCH §O). Pure: the gates in order, the escape, the "ahead of his
    /// slot" measure on any facing, the names. Vanilla (<c>HumanAIComponent.ParallelUpdateFormationMovement</c>) tells the engine
    /// "keep with the formation instead of moving to the agent" only for a shield man in a mostly ranged formation; the mod tells
    /// it for every man held here.
    /// </summary>
    public static class WallMath
    {
        /// <summary>Every man is looked at this often (s), staggered over the ticks - his gates, and a held man's write. Plumbing
        /// (the backstop: the write that matters follows vanilla's own update, see RESEARCH §O).</summary>
        public const double PollSeconds = 0.25;

        /// <summary>A man in a holding defensive arrangement is sampled for the log this often (s) - where he stands against his
        /// slot. Log plumbing.</summary>
        public const double SampleSeconds = 0.5;

        /// <summary>Only when vanilla's update timer cannot be read (a game update renamed it): a held man is rewritten this
        /// often (s) instead of right after vanilla's own update. Plumbing.</summary>
        public const double FallbackRewriteSeconds = 0.1;

        /// <summary>"In contact" for the log: an enemy within this many metres of him at the sample. Log plumbing.</summary>
        public const double ContactMetres = 5.0;

        /// <summary>The two "ahead of his slot" marks the summary counts (m). Log plumbing.</summary>
        public const double AheadNearMetres = 1.0;
        public const double AheadFarMetres = 2.0;

        /// <summary>Vanilla's catch-up radius in the formation's speeds: a man this far from his slot (6 x the average top speed of
        /// its men near their slots, ~20 m at a walk-run 3.5 m/s) is "far" - vanilla's <c>ShouldCatchUpWithFormation</c> test
        /// (3 x the speed x 2). The game's own number, not ours.</summary>
        public const double CatchUpSpeeds = 6.0;

        /// <summary>The game's three defensive arrangements (shield wall, square, circle).</summary>
        public static bool IsDefensive(WallArrangement a) => a == WallArrangement.ShieldWall || a == WallArrangement.Square || a == WallArrangement.Circle;

        /// <summary>The order gate: a charge (any kind, attacking a gate too) and a retreat are left to vanilla, as is no order;
        /// hold / stop, move, advance, the AI's advance, fall back and follow are held.</summary>
        public static WallVerdict OrderVerdict(BraceOrderKind k) => k switch
        {
            BraceOrderKind.Charge => WallVerdict.ChargeOrder,
            BraceOrderKind.ChargeToTarget => WallVerdict.ChargeOrder,
            BraceOrderKind.AiCharge => WallVerdict.ChargeOrder,
            BraceOrderKind.AttackEntity => WallVerdict.ChargeOrder,
            BraceOrderKind.Retreat => WallVerdict.RetreatOrder,
            BraceOrderKind.NoOrder => WallVerdict.NoOrder,
            BraceOrderKind.NoFormation => WallVerdict.NoFormation,
            _ => WallVerdict.Hold,
        };

        /// <summary>
        /// Whether he stands in a HOLDING defensive arrangement - every gate but the two switches and the escape (the log samples
        /// these men whichever way the switch stands, so ON and OFF compare). <see cref="WallVerdict.Hold"/> = yes.
        /// </summary>
        public static WallVerdict Eligible(in WallFacts f)
        {
            if (f.Naval) return WallVerdict.Naval;
            if (f.SiegeAttacker) return WallVerdict.SiegeAttacker;
            if (!f.Ai) return WallVerdict.NotAi;
            if (f.Mounted) return WallVerdict.Mounted;
            if (!f.HasFormation) return WallVerdict.NoFormation;
            if (f.Detached) return WallVerdict.Detached;
            if (!IsDefensive(f.Arrangement)) return WallVerdict.Arrangement;
            var o = OrderVerdict(f.Order);
            if (o != WallVerdict.Hold) return o;
            if (f.Retreating) return WallVerdict.Retreating;
            return WallVerdict.Hold;
        }

        /// <summary>The whole decision, gates in order: the master switch FIRST, the switch, <see cref="Eligible"/>, then the
        /// escape (<see cref="Escapes"/>).</summary>
        public static WallVerdict Decide(in WallRules r, in WallFacts f)
        {
            if (!r.ModEnabled) return WallVerdict.ModOff;
            if (!r.HoldsSlots) return WallVerdict.SwitchedOff;
            var e = Eligible(in f);
            if (e != WallVerdict.Hold) return e;
            if (Escapes(in f)) return WallVerdict.FarBehind;
            return WallVerdict.Hold;
        }

        /// <summary>His distance to his slot on the ground (m).</summary>
        public static double DistanceToSlot(in WallFacts f)
        {
            double dx = f.AgentX - f.SlotX, dy = f.AgentY - f.SlotY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Far from his slot: at or beyond <see cref="CatchUpSpeeds"/> x the formation's average speed. No speed yet (the
        /// game's cache still empty) = not far.</summary>
        public static bool FarFromSlot(double distance, double avgSpeed) => avgSpeed > 0 && distance >= CatchUpSpeeds * avgSpeed;

        /// <summary>Behind the line: behind his formation's average position along its facing.</summary>
        public static bool BehindLine(in WallFacts f) => (f.AgentX - f.CentreX) * f.DirX + (f.AgentY - f.CentreY) * f.DirY < 0;

        /// <summary>RBM's escape on vanilla's radius: far from his slot AND behind the line - a man who fell back (or was pushed
        /// back) may run to rejoin as vanilla lets him; a man far AHEAD of his slot is held (walked back into the wall).</summary>
        public static bool Escapes(in WallFacts f) => f.HasPositions && FarFromSlot(DistanceToSlot(in f), f.AvgSpeed) && BehindLine(in f);

        /// <summary>How far he stands AHEAD of his slot along the formation's facing (m; negative = behind it). The facing need
        /// not be a unit vector; a zero facing reads 0.</summary>
        public static double Ahead(in WallFacts f)
        {
            double len = Math.Sqrt(f.DirX * f.DirX + f.DirY * f.DirY);
            if (len < 1e-9 || double.IsNaN(len)) return 0;
            return ((f.AgentX - f.SlotX) * f.DirX + (f.AgentY - f.SlotY) * f.DirY) / len;
        }

        public static string Name(WallVerdict v) => v switch
        {
            WallVerdict.Hold => "held",
            WallVerdict.ModOff => "the mod off",
            WallVerdict.SwitchedOff => "switched off",
            WallVerdict.Naval => "a naval battle",
            WallVerdict.SiegeAttacker => "a siege attacker",
            WallVerdict.NotAi => "you or a man you drive",
            WallVerdict.Mounted => "mounted",
            WallVerdict.NoFormation => "no formation",
            WallVerdict.Detached => "detached",
            WallVerdict.Arrangement => "another arrangement",
            WallVerdict.ChargeOrder => "a charge order",
            WallVerdict.RetreatOrder => "a retreat order",
            WallVerdict.NoOrder => "no order",
            WallVerdict.Retreating => "fleeing",
            WallVerdict.FarBehind => "far from his slot and behind the line",
            _ => v.ToString(),
        };

        public static string Name(WallArrangement a) => a switch
        {
            WallArrangement.ShieldWall => "shield wall",
            WallArrangement.Square => "square",
            WallArrangement.Circle => "circle",
            WallArrangement.Column => "column",
            _ => "another arrangement",
        };
    }
}
