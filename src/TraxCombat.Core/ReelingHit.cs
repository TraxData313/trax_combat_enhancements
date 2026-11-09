using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 105: what put a man into his reel - the last cause that set (or lengthened) his <see cref="ImpactState.ReelingUntil"/>. Only the
    /// reels this mod KNOWS: the impact system's own staggers and lengthened reels. The engine's ordinary flinch on a hit the model did not
    /// stagger, and vanilla's own block stun, never set a reel - they are not "reeling" for step 105.
    /// </summary>
    public enum ReelCause : byte
    {
        /// <summary>Not known (a state written before step 105 / a reel set with no cause).</summary>
        Unknown,

        /// <summary>A landed KICK's lengthened reel (step 50 - or a kick that got through the meet rules, step 82).</summary>
        Kick,

        /// <summary>A landed shield BASH's (or weapon push's) lengthened reel; a bash into his shield that staggered him (step 82).</summary>
        Bash,

        /// <summary>The impact system's rolled stagger on a CLEAN HIT (step 52 - the engine's flinch slowed to the decided length).</summary>
        HitStagger,

        /// <summary>The impact system's rolled stagger on a man who BLOCKED (step 51 - right side, parry or wrong side).</summary>
        BlockStagger,

        /// <summary>An UNTIMED riposte - his blow blocked correctly, he reels (step 56; the AI's held guard).</summary>
        Riposte,

        /// <summary>A TIMED riposte (step 100b - a block with a timing score above 0, the player's or the AI's guard clock).</summary>
        TimedRiposte,

        /// <summary>His kick / bash MET correctly (step 82 - the guard right / down / up): the kicker staggers.</summary>
        MeetRiposte,
    }

    /// <summary>Step 105: what LANDED on the reeling man.</summary>
    public enum ReelingHitKind : byte
    {
        Swing,
        Thrust,
        Kick,
        Bash,
    }

    /// <summary>Step 105: why a landed hit on a reeling man was NOT given the knockdown roll (or its knockdown did not reach the engine).</summary>
    public enum ReelingHitRefusal : byte
    {
        None,

        /// <summary><c>ReelingHitKnockdownEnabled</c> off, or <c>ReelingHitKnockdownPercent</c> 0 - the usual rules only.</summary>
        Off,

        /// <summary>He rides (riders do not reel yet - step 104's seam: the rule for a reeling rider joins there).</summary>
        Mounted,

        /// <summary>He is already on the ground.</summary>
        Down,

        /// <summary>The hit was MET by his guard (step 82's correct meet of a kick / bash - it did not land on him).</summary>
        Guarded,

        /// <summary>He is YOU and <c>ImpactOnPlayerPercent</c> is 0 (never knocked down by this system).</summary>
        YouExempt,

        /// <summary>The blow kills him - he just dies (counted, never decided).</summary>
        Fatal,

        /// <summary>The knockdown came up but was not applied: shadow mode / the family not live, decided outside the decorator's seam, the
        /// KnockDown lever off after an error.</summary>
        NotApplied,
    }

    /// <summary>Step 105: one landed hit on a reeling man - asked or not, the kind, the cause, the chance (NaN = not rolled), the die, the
    /// outcome and why not.</summary>
    public struct ReelingHitResult
    {
        /// <summary>The hit LANDED on a man who reels now (whatever the switch says).</summary>
        public bool Asked;
        public ReelingHitKind Kind;
        public ReelCause Cause;
        public double Chance, Die;
        public bool Happens;

        /// <summary>The usual knockdown roll was higher than the reeling chance and stood (its own die decided).</summary>
        public bool UsualStood;

        public ReelingHitRefusal Refusal;

        public static ReelingHitResult None => new ReelingHitResult { Chance = double.NaN, Die = double.NaN };
    }

    /// <summary>
    /// Step 105 (Toni 2026-11-09: "Make it so that when hitting (swing/kick/bash) an opponent that is staggering has 80% chance to knock him down
    /// no matter what or who caused the stagger"): the settings, read live. Part of <see cref="ImpactRules"/> - the impact system's own switches
    /// (the master switch first, Athletics, <c>ImpactEnabled</c>) gate it there; here only its own switch and the chance.
    /// </summary>
    public readonly struct ReelingHitRules
    {
        public ReelingHitRules(TraxSettings s)
        {
            Switch = s.ReelingHitKnockdownEnabled;
            Chance = ReelingHitMath.ClampChance(s.ReelingHitKnockdownPercent / 100.0);
        }

        public static ReelingHitRules From(TraxSettings s) => new ReelingHitRules(s);

        public bool Switch { get; }

        /// <summary>The knockdown chance on a landed hit while he reels (0-1; ×<c>ImpactOnPlayerPercent</c> on you).</summary>
        public double Chance { get; }

        public bool On => Switch && Chance > 0;

        public string? OffBecause => !Switch ? "ReelingHitKnockdownEnabled" : !(Chance > 0) ? "ReelingHitKnockdownPercent 0" : null;

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            return On
                ? "a hit on a reeling man ON: a swing / thrust / kick / bash that LANDS on a man on foot while he reels knocks him down at " + (100 * Chance).ToString("0", c)
                  + " % (x ImpactOnPlayerPercent on you) - whatever caused the reel; his tiredness, armour, skill, the immunity and the halving ignored"
                : "a hit on a reeling man OFF (" + OffBecause + ") - the usual knockdown roll only";
        }
    }

    /// <summary>
    /// Step 105: the pure decision (AI_NOTES "Step 105", DESIGN decision 94). Called from <see cref="ImpactMath.Decide"/> right after the usual
    /// knockdown roll of a LANDED blow (a clean hit, a kick, a bash) on a man who reels (<see cref="ImpactState.Reeling"/>):
    ///   - not reeling, or not landed (a block, a guard) = not asked - the usual decision exactly;
    ///   - the hard refusals: off, a RIDER (step 104's seam), already DOWN, YOU at <c>ImpactOnPlayerPercent</c> 0 (a fatal blow never gets here);
    ///   - else the chance p = <see cref="ReelingHitRules.Chance"/> x (your share when he is you). "No matter what": his tiredness, the mismatch,
    ///     the brace, the knockdown immunity after his last fall and the halving of recent outcomes are all ignored. The usual roll stands only
    ///     when it came up or its own chance was higher (it can never make a reeling man SAFER); else the knockdown is rolled at p - ONE die.
    /// </summary>
    public static class ReelingHitMath
    {
        public const int KindCount = 4;
        public const int CauseCount = 8;
        public const int RefusalCount = 8;

        public static double ClampChance(double p) => double.IsNaN(p) ? 0 : Math.Max(0, Math.Min(1, p));

        public static ReelingHitKind KindOf(ImpactEvent e, bool thrust) => e switch
        {
            ImpactEvent.Kick => ReelingHitKind.Kick,
            ImpactEvent.Bash => ReelingHitKind.Bash,
            _ => thrust ? ReelingHitKind.Thrust : ReelingHitKind.Swing,
        };

        /// <summary>The cause a reel set by the model gets: a kick / bash event's reel, a clean hit's stagger, a blocker's stagger.</summary>
        public static ReelCause CauseOfDefenderReel(ImpactEvent e) => e switch
        {
            ImpactEvent.Kick => ReelCause.Kick,
            ImpactEvent.Bash => ReelCause.Bash,
            ImpactEvent.CleanHit => ReelCause.HitStagger,
            _ => ReelCause.BlockStagger,
        };

        /// <summary>The refusal for a landed hit on a reeling man (None = roll).</summary>
        public static ReelingHitRefusal Refusal(in ReelingHitRules r, bool mounted, bool down, bool isPlayer, double onPlayer)
        {
            if (!r.On) return ReelingHitRefusal.Off;
            if (mounted) return ReelingHitRefusal.Mounted;
            if (down) return ReelingHitRefusal.Down;
            if (isPlayer && !(onPlayer > 0)) return ReelingHitRefusal.YouExempt;
            return ReelingHitRefusal.None;
        }

        /// <summary>The chance on him: p, x your share when he is you (0-1).</summary>
        public static double ChanceOn(in ReelingHitRules r, bool isPlayer, double onPlayer) =>
            ClampChance(r.Chance * (isPlayer ? Math.Max(0, onPlayer) : 1));

        /// <summary>
        /// The decision for one landed hit: <paramref name="knockdown"/> is the usual roll (made just before) - kept when it came up or its chance
        /// was higher, else REPLACED by the reeling roll (one die). Not reeling / not landed / guarded = <paramref name="knockdown"/> untouched.
        /// </summary>
        public static ReelingHitResult Decide(in ReelingHitRules r, ImpactEvent e, bool thrust, bool landed, bool guarded, bool reeling, ReelCause cause,
            bool mounted, bool down, bool isPlayer, double onPlayer, ref ImpactRoll knockdown, IRandomSource dice)
        {
            var o = ReelingHitResult.None;
            if (!reeling || !ImpactMath.IsLanded(e) || !landed) return o;
            o.Asked = true;
            o.Kind = KindOf(e, thrust);
            o.Cause = cause;
            o.Refusal = guarded ? ReelingHitRefusal.Guarded : Refusal(in r, mounted, down, isPlayer, onPlayer);
            if (o.Refusal != ReelingHitRefusal.None) return o;
            double p = ChanceOn(in r, isPlayer, onPlayer);
            o.Chance = p;
            bool usualCounts = knockdown.InRow && knockdown.Refusal == ImpactRefusal.None;
            if (usualCounts && (knockdown.Happens || knockdown.Chance >= p))
            {
                o.UsualStood = true;
                o.Happens = knockdown.Happens;
                o.Die = knockdown.Die;
                o.Chance = Math.Max(p, knockdown.Chance);
                return o;
            }
            knockdown = new ImpactRoll { InRow = true, Ceiling = p, Raw = p, Halvings = 0, Chance = p, Die = double.NaN, Refusal = ImpactRefusal.None };
            if (p >= 1) knockdown.Happens = true;
            else if (p > 0)
            {
                knockdown.Die = dice.NextDouble();
                knockdown.Happens = knockdown.Die < p;
            }
            o.Die = knockdown.Die;
            o.Happens = knockdown.Happens;
            return o;
        }

        public static string KindName(ReelingHitKind k) => k switch
        {
            ReelingHitKind.Swing => "swing",
            ReelingHitKind.Thrust => "thrust",
            ReelingHitKind.Kick => "kick",
            _ => "bash",
        };

        public static string CauseName(ReelCause c) => c switch
        {
            ReelCause.Kick => "a kick",
            ReelCause.Bash => "a bash",
            ReelCause.HitStagger => "a hit's stagger",
            ReelCause.BlockStagger => "a block's stagger",
            ReelCause.Riposte => "a riposte",
            ReelCause.TimedRiposte => "a timed riposte",
            ReelCause.MeetRiposte => "a met kick / bash",
            _ => "unknown",
        };

        public static string RefusalName(ReelingHitRefusal r) => r switch
        {
            ReelingHitRefusal.Off => "the switch off",
            ReelingHitRefusal.Mounted => "a rider",
            ReelingHitRefusal.Down => "already down",
            ReelingHitRefusal.Guarded => "met by his guard",
            ReelingHitRefusal.YouExempt => "you at 0 %",
            ReelingHitRefusal.Fatal => "killed by it",
            ReelingHitRefusal.NotApplied => "not applied",
            _ => "none",
        };
    }

    /// <summary>Step 105: the mission's count of hits on reeling men (the [summary] line). Main thread; fixed arrays, no allocation per hit.</summary>
    public sealed class ReelingHitStats
    {
        public readonly int[] Hits = new int[ReelingHitMath.KindCount];
        public readonly int[] Downs = new int[ReelingHitMath.KindCount];
        public readonly int[] HitsByCause = new int[ReelingHitMath.CauseCount];
        public readonly int[] DownsByCause = new int[ReelingHitMath.CauseCount];
        public readonly int[] Refused = new int[ReelingHitMath.RefusalCount];
        public int Rolled, RolledUp, UsualStood, OnYou, OnYouDown, ByYou, ByYouDown;

        public int HitsTotal => Hits[0] + Hits[1] + Hits[2] + Hits[3];
        public int DownsTotal => Downs[0] + Downs[1] + Downs[2] + Downs[3];

        /// <summary>A decided hit on a reeling man: <paramref name="applied"/> = its knockdown reached the engine (the seam answered it).</summary>
        public void Note(in ReelingHitResult h, bool applied, bool youVictim, bool youAttacker)
        {
            if (!h.Asked) return;
            int k = (int)h.Kind, c = Math.Min((int)h.Cause, ReelingHitMath.CauseCount - 1);
            Hits[k]++;
            HitsByCause[c]++;
            if (youVictim) OnYou++;
            if (youAttacker) ByYou++;
            if (h.Refusal != ReelingHitRefusal.None)
            {
                Refused[(int)h.Refusal]++;
                return;
            }
            if (h.UsualStood) UsualStood++;
            else if (!double.IsNaN(h.Die) || h.Happens)
            {
                Rolled++;
                if (h.Happens) RolledUp++;
            }
            if (!h.Happens) return;
            if (!applied)
            {
                Refused[(int)ReelingHitRefusal.NotApplied]++;
                return;
            }
            Downs[k]++;
            DownsByCause[c]++;
            if (youVictim) OnYouDown++;
            if (youAttacker) ByYouDown++;
        }

        /// <summary>A blow that KILLS a reeling man - never decided (he dies).</summary>
        public void NoteFatal(ReelingHitKind kind, ReelCause cause)
        {
            Hits[(int)kind]++;
            HitsByCause[Math.Min((int)cause, ReelingHitMath.CauseCount - 1)]++;
            Refused[(int)ReelingHitRefusal.Fatal]++;
        }

        public string SummaryLine(in ImpactRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            var rh = r.ReelingHit;
            t.Append("hits on reeling men (step 105; ").Append(!r.Enabled ? "OFF (" + r.OffBecause + ")" : rh.On ? "ON, " + (100 * rh.Chance).ToString("0", c) + " %" : "OFF (" + rh.OffBecause + ")")
             .Append("): landed ").Append(HitsTotal.ToString(c)).Append(", KNOCKED DOWN ").Append(DownsTotal.ToString(c));
            if (Rolled > 0) t.Append(" (rolled ").Append(Rolled.ToString(c)).Append(" → ").Append(RolledUp.ToString(c)).Append(" came up = ").Append((100.0 * RolledUp / Rolled).ToString("0", c)).Append(" %");
            else t.Append(" (rolled 0");
            t.Append(", the usual roll higher ").Append(UsualStood.ToString(c)).Append(')');
            t.Append("; by hit: ");
            for (int k = 0; k < ReelingHitMath.KindCount; k++)
            {
                if (k > 0) t.Append(", ");
                t.Append(ReelingHitMath.KindName((ReelingHitKind)k)).Append(' ').Append(Downs[k].ToString(c)).Append('/').Append(Hits[k].ToString(c));
            }
            t.Append("; by what made him reel: ");
            bool any = false;
            for (int i = 0; i < ReelingHitMath.CauseCount; i++)
            {
                if (HitsByCause[i] == 0) continue;
                if (any) t.Append(", ");
                any = true;
                t.Append(ReelingHitMath.CauseName((ReelCause)i)).Append(' ').Append(DownsByCause[i].ToString(c)).Append('/').Append(HitsByCause[i].ToString(c));
            }
            if (!any) t.Append('-');
            t.Append("; refused: ");
            any = false;
            for (int i = 1; i < ReelingHitMath.RefusalCount; i++)
            {
                if (Refused[i] == 0) continue;
                if (any) t.Append(", ");
                any = true;
                t.Append(ReelingHitMath.RefusalName((ReelingHitRefusal)i)).Append(' ').Append(Refused[i].ToString(c));
            }
            if (!any) t.Append("none");
            t.Append("; on you ").Append(OnYouDown.ToString(c)).Append('/').Append(OnYou.ToString(c))
             .Append(", by you ").Append(ByYouDown.ToString(c)).Append('/').Append(ByYou.ToString(c))
             .Append(" (knocked down / landed; the engine's own falls and our backup blow: the [summary] \"falls\" line)");
            return t.ToString();
        }
    }
}
