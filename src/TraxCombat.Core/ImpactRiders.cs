using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 53: how a rider's dismount was carried out - the damage decorator's answer for a landed blow, or our own blow (a block).</summary>
    public enum ImpactDismountWay : byte
    {
        /// <summary>A landed kick / bash / clean hit: <c>DecideAgentDismountedByBlow</c> answered yes in the damage decorator (main thread).</summary>
        Seam,

        /// <summary>A block: a blow of our own with <c>CanDismount</c> from the next tick (the engine never registers a blocked blow).</summary>
        OwnBlow,
    }

    /// <summary>
    /// Step 53 - IMPACT I6, RIDERS (docs/IMPACT.md §3.8, §8 q4; AI_NOTES "Step 53"): the pure plumbing of the live riders. A rider's
    /// knockdown is KNOCKED OFF his horse at <c>ImpactRiderDismountPercent</c> of the chance (the model rolls it - <see cref="ImpactDecision.Dismount"/>);
    /// never a stagger (the model refuses it); his shield can drop (a horse does not hold a shield for him); horses untouched.
    /// </summary>
    public static class ImpactRiderMath
    {
        /// <summary>A dismount counts as SEEN when he is off his horse within this many seconds (the engine plays the fall a frame or two later).</summary>
        public const double SeenWithinSeconds = 2.0;

        /// <summary>At most this many dismounts followed / our dismount blows waiting for the tick at once (fixed pools - no allocation).</summary>
        public const int MaxChecks = 32;

        public const int MaxPending = 32;

        /// <summary>The ways (seam / our blow) and the families (kicks / blocks / clean hits) the summary splits by.</summary>
        public const int WayCount = 2;

        public const int FamilyCount = 3;

        /// <summary>0 kicks / bashes, 1 blocks, 2 clean hits (the summary's columns).</summary>
        public static int FamilyIndex(ImpactEvent e) => ImpactMath.IsAlternative(e) ? 0 : ImpactMath.IsBlock(e) ? 1 : 2;

        public static string FamilyName(int i) => i switch
        {
            0 => "kicks / bashes",
            1 => "blocks",
            _ => "clean hits",
        };

        public static string WayName(ImpactDismountWay w) => w == ImpactDismountWay.Seam ? "answered in the damage decorator's dismount seam" : "our own blow with CanDismount from the tick";
    }

    /// <summary>
    /// Step 53: one mission's LIVE riders (IMPACT §6): decisions on mounted men by family, dismounts decided → applied (by way) → SEEN (off his
    /// horse within 2 s) or not, why not, shields dropped from a rider, staggers refused, the riders' ripostes refused; the cost. Main thread.
    /// The [summary] "impact riders" lines.
    /// </summary>
    public sealed class ImpactRiderLiveStats
    {
        private const int F = ImpactRiderMath.FamilyCount;
        private const int W = ImpactRiderMath.WayCount;
        private const int B = ImpactMath.BandCount;

        /// <summary>Live decisions on a mounted defender [family]; dismounts decided [family, band], applied [way], seen / not seen [way].</summary>
        public readonly int[] Decisions = new int[F];
        public readonly int[,] DismountDecided = new int[F, B];
        public readonly int[] Applied = new int[W];
        public readonly int[] Seen = new int[W];
        public readonly int[] NotSeen = new int[W];
        public readonly int[] DismountNotApplied = new int[ImpactLiveMath.NotAppliedCount];

        /// <summary>A rider's shield: the dice said drop [family], queued for the tick, really dropped (the drop path's count), not applied.</summary>
        public readonly int[] ShieldDecided = new int[F];
        public int ShieldQueued, ShieldDropped;
        public readonly int[] ShieldNotApplied = new int[ImpactLiveMath.NotAppliedCount];

        /// <summary>The model's refusals on riders: a stagger (never on a horse) whose curve was above 0; the riposte on a mounted attacker.</summary>
        public int StaggersRefused, RipostesRefused, Halved, RefusedImmune;
        public int YouDismounted, YouDismountedSomeone, ShockGiven, PoolFull, LeftField;
        public double SecondsToFallSum;
        public int SecondsToFallCount;

        public int DecisionsTotal
        {
            get
            {
                int n = 0;
                foreach (var v in Decisions) n += v;
                return n;
            }
        }

        public int DismountsDecided
        {
            get
            {
                int n = 0;
                for (int f = 0; f < F; f++)
                    for (int b = 0; b < B; b++) n += DismountDecided[f, b];
                return n;
            }
        }

        /// <summary>One LIVE decision on a mounted defender (his family applies).</summary>
        public void NoteDecision(in ImpactDecision d)
        {
            int f = ImpactRiderMath.FamilyIndex(d.Event);
            Decisions[f]++;
            if (d.Knockdown.Happens) DismountDecided[f, ImpactMath.BarBand(d.Bar)]++;
            if (d.ShieldDrop.Happens) ShieldDecided[f]++;
            if (d.Stagger.Refusal == ImpactRefusal.Rider && d.Stagger.Raw > 0) StaggersRefused++;
            if (d.Knockdown.Refusal == ImpactRefusal.KnockdownImmune && d.Knockdown.Raw > 0) RefusedImmune++;
            if (d.Knockdown.Halvings > 0 && d.Knockdown.Raw > 0 && d.Knockdown.Refusal == ImpactRefusal.None) Halved++;
            if (d.Knockdown.Happens)
            {
                if (d.DefenderIsPlayer) YouDismounted++;
                if (d.AttackerIsPlayer) YouDismountedSomeone++;
            }
        }

        /// <summary>A riposte refused because the ATTACKER rides (any live block decision - the model's refusal).</summary>
        public void NoteRiposteOnRider(in ImpactDecision d)
        {
            if (d.Riposte.Refusal == ImpactRefusal.Rider && d.Riposte.Raw > 0) RipostesRefused++;
        }

        public void NoteApplied(ImpactDismountWay way) => Applied[(int)way]++;

        public void NoteSeen(ImpactDismountWay way, double seconds)
        {
            Seen[(int)way]++;
            if (!double.IsNaN(seconds) && seconds >= 0)
            {
                SecondsToFallSum += seconds;
                SecondsToFallCount++;
            }
        }

        public void NoteNotSeen(ImpactDismountWay way) => NotSeen[(int)way]++;

        public void NoteNotApplied(ImpactNotApplied why) => DismountNotApplied[(int)why]++;

        public void NoteShieldNotApplied(ImpactNotApplied why) => ShieldNotApplied[(int)why]++;

        /// <summary>The [summary] lines: one when no live decision on a rider happened; else the dismounts and the riders' shields / limits.</summary>
        public IReadOnlyList<string> SummaryLines(bool liveNow, string mode, string levers, int riderPercent)
        {
            var lines = new List<string>();
            if (DecisionsTotal == 0 && RipostesRefused == 0)
            {
                lines.Add("impact riders (step 53): no live decision on a rider this mission (" + mode + "; a rider's knockdown = knocked off his horse at " + I(riderPercent) + "% of it)");
                return lines;
            }
            var s = new StringBuilder("impact riders (step 53 - a knockdown on a rider = KNOCKED OFF his horse at " + I(riderPercent) + "% of its chance, never a stagger; " + mode + "): live decisions on riders ");
            for (int f = 0; f < F; f++)
            {
                if (f > 0) s.Append(" / ");
                s.Append(ImpactRiderMath.FamilyName(f)).Append(' ').Append(I(Decisions[f]));
            }
            s.Append("; dismounts DECIDED ").Append(I(DismountsDecided)).Append(" (by the rider's bar ").Append(Bands()).Append(": ");
            for (int f = 0; f < F; f++)
            {
                if (f > 0) s.Append("; ");
                s.Append(ImpactRiderMath.FamilyName(f)).Append(' ');
                for (int b = 0; b < B; b++)
                {
                    if (b > 0) s.Append(" / ");
                    s.Append(I(DismountDecided[f, b]));
                }
            }
            s.Append(") → APPLIED ").Append(I(Applied[0])).Append(" in the decorator's dismount seam + ").Append(I(Applied[1])).Append(" by our own blow (blocks)")
                .Append(" → SEEN off the horse within ").Append(F1(ImpactRiderMath.SeenWithinSeconds)).Append(" s ").Append(I(Seen[0])).Append(" + ").Append(I(Seen[1]))
                .Append(", still mounted ").Append(I(NotSeen[0])).Append(" + ").Append(I(NotSeen[1]));
            if (SecondsToFallCount > 0) s.Append(" (off after ").Append(F2(SecondsToFallSum / SecondsToFallCount)).Append(" s on average)");
            s.Append(NotText(DismountNotApplied));
            lines.Add(s.ToString());
            int shields = ShieldDecided[0] + ShieldDecided[1] + ShieldDecided[2];
            lines.Add("impact riders' shields and limits: shields knocked from a rider's hand decided " + I(shields) + " (kicks / bashes " + I(ShieldDecided[0]) + ", wrong-side blocks " + I(ShieldDecided[1])
                      + ") → queued " + I(ShieldQueued) + " → dropped " + I(ShieldDropped) + NotText(ShieldNotApplied)
                      + "; staggers REFUSED on riders (never on a horse) " + I(StaggersRefused) + "; ripostes refused on a mounted attacker " + I(RipostesRefused)
                      + "; dismounts refused within the immunity " + I(RefusedImmune) + ", halved by a recent outcome " + I(Halved)
                      + "; YOU: knocked off " + I(YouDismounted) + ", knocked someone off " + I(YouDismountedSomeone) + "; courage's SHOCK " + I(ShockGiven)
                      + "; levers disabled " + levers + "; pool full " + I(PoolFull) + ", left the field " + I(LeftField) + (liveNow ? string.Empty : "; NOT live at the end"));
            return lines;
        }

        private static string NotText(int[] a)
        {
            var not = new StringBuilder();
            for (int n = 0; n < a.Length; n++)
            {
                if (a[n] == 0) continue;
                if (not.Length > 0) not.Append(", ");
                not.Append(ImpactLiveMath.NotAppliedName((ImpactNotApplied)n)).Append(' ').Append(I(a[n]));
            }
            return not.Length > 0 ? " (not applied: " + not + ")" : string.Empty;
        }

        private static string Bands()
        {
            var s = new StringBuilder();
            for (int b = 0; b < B; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(ImpactMath.BandName(b));
            }
            return s.ToString();
        }

        private static string I(long v) => v.ToString(CultureInfo.InvariantCulture);

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 53: what an on-screen impact line says.</summary>
    public enum ImpactMessageKind : byte
    {
        KnockedDown,
        KnockedOff,
        ShieldLost,

        /// <summary>A block broken: the blocker staggered (or his shield knocked from a wrong-side block).</summary>
        GuardBroken,

        /// <summary>The riposte: a fresh blocker staggers the attacker.</summary>
        Riposte,
    }

    /// <summary>Step 53: whose line it is - done to you, done by you, or a notable moment of your men's (the game's colours differ).</summary>
    public enum ImpactMessageRole : byte
    {
        /// <summary>Done TO you (the game's "received" red #D65252).</summary>
        ToYou,

        /// <summary>Done BY you (the game's "delivered" purple #AE4AD9).</summary>
        ByYou,

        /// <summary>A man of your side and an enemy hero, or a hero of your side (white).</summary>
        YourMen,
    }

    /// <summary>
    /// Step 53 (IMPACT §8 q12): the words and colours of the on-screen impact lines - the game's own message colours for combat lines
    /// (Mission.cs: "delivered couched lance damage" #AE4AD9FF, "received" #D65252FF; a neutral line #FFFFFFFF).
    /// </summary>
    public static class ImpactMessageMath
    {
        public const string ReceivedHex = "#D65252FF";
        public const string DeliveredHex = "#AE4AD9FF";
        public const string NeutralHex = "#FFFFFFFF";

        /// <summary>At most this many parts in one merged line; the rest "(+N more)".</summary>
        public const int MaxParts = 3;

        /// <summary>The line's text for one event. <paramref name="attacker"/> = who did it, <paramref name="victim"/> = to whom (names; null =
        /// "someone").</summary>
        public static string Text(ImpactMessageKind kind, ImpactMessageRole role, string? attacker, string? victim)
        {
            string a = string.IsNullOrEmpty(attacker) ? "someone" : attacker!;
            string v = string.IsNullOrEmpty(victim) ? "someone" : victim!;
            switch (role)
            {
                case ImpactMessageRole.ToYou:
                    return kind switch
                    {
                        ImpactMessageKind.KnockedDown => "You are knocked down",
                        ImpactMessageKind.KnockedOff => "You are knocked off your horse",
                        ImpactMessageKind.ShieldLost => "Your shield is knocked from your hand",
                        ImpactMessageKind.GuardBroken => "Your guard breaks",
                        _ => a + " ripostes - you reel",
                    };
                case ImpactMessageRole.ByYou:
                    return kind switch
                    {
                        ImpactMessageKind.KnockedDown => "You knock " + v + " down",
                        ImpactMessageKind.KnockedOff => "You knock " + v + " off his horse",
                        ImpactMessageKind.ShieldLost => "You knock the shield from " + v + "'s hand",
                        ImpactMessageKind.GuardBroken => "You break " + v + "'s guard",
                        _ => "Your riposte - " + v + " reels",
                    };
                default:
                    return kind switch
                    {
                        ImpactMessageKind.KnockedOff => a + " knocks " + v + " off his horse",
                        ImpactMessageKind.ShieldLost => a + " knocks the shield from " + v + "'s hand",
                        ImpactMessageKind.GuardBroken => a + " breaks " + v + "'s guard",
                        ImpactMessageKind.Riposte => v + " reels from " + a + "'s riposte",
                        _ => a + " knocks " + v + " down",
                    };
            }
        }

        /// <summary>The colour of a line: anything done TO you wins (red), then done by you (purple), else your men's (white).</summary>
        public static string Hex(ImpactMessageRole role) => role switch
        {
            ImpactMessageRole.ToYou => ReceivedHex,
            ImpactMessageRole.ByYou => DeliveredHex,
            _ => NeutralHex,
        };

        /// <summary>Which role's colour a merged line takes: to you before by you before your men.</summary>
        public static ImpactMessageRole Stronger(ImpactMessageRole a, ImpactMessageRole b) => (int)a <= (int)b ? a : b;

        /// <summary>Is a moment of YOUR MEN notable enough for a line (<c>ImpactMessagesIncludeYourMen</c>)? Knocked down or off a horse only;
        /// your man on an enemy HERO, or a HERO of yours on the receiving end.</summary>
        public static bool NotableForYourMen(ImpactMessageKind kind, bool attackerYours, bool victimYours, bool victimHero)
        {
            if (kind != ImpactMessageKind.KnockedDown && kind != ImpactMessageKind.KnockedOff) return false;
            if (attackerYours && !victimYours && victimHero) return true;
            return victimYours && victimHero && !attackerYours;
        }
    }

    /// <summary>
    /// Step 53: the on-screen lines' RATE LIMIT and MERGE (IMPACT §8 q12): parts offered while the last line is younger than the minimum gap
    /// wait and are merged into the next line ("You knock Aldric down; your shield is knocked from your hand"), at most
    /// <see cref="ImpactMessageMath.MaxParts"/> parts (the rest "(+N more)"); the same text twice in one line once. Main thread; a fixed array
    /// of parts (the strings are the lines' own - only built for the player's events).
    /// </summary>
    public sealed class ImpactMessageQueue
    {
        private readonly string?[] _parts = new string?[ImpactMessageMath.MaxParts];
        private int _count;
        private int _extra;
        private ImpactMessageRole _role = ImpactMessageRole.YourMen;
        private double _lastShown = double.NegativeInfinity;

        /// <summary>Parts offered, lines shown, parts MERGED into a line beyond its first, parts beyond the cap ("+N more"), duplicates dropped,
        /// parts thrown away unshown (the mission ended / the switch went off).</summary>
        public int Offered, Shown, Merged, Overflow, Duplicates, Discarded;

        /// <summary>Parts offered by role.</summary>
        public readonly int[] ByRole = new int[3];

        /// <summary>Parts offered by kind.</summary>
        public readonly int[] ByKind = new int[5];

        public int Waiting => _count + _extra;

        public void Add(ImpactMessageKind kind, ImpactMessageRole role, string text)
        {
            Offered++;
            ByRole[(int)role]++;
            ByKind[(int)kind]++;
            for (int i = 0; i < _count; i++)
            {
                if (!string.Equals(_parts[i], text, StringComparison.Ordinal)) continue;
                Duplicates++;
                return;
            }
            _role = _count + _extra == 0 ? role : ImpactMessageMath.Stronger(_role, role);
            if (_count < _parts.Length) _parts[_count++] = text;
            else
            {
                _extra++;
                Overflow++;
            }
        }

        /// <summary>The next line, when one waits and the last one is at least <paramref name="minGap"/> seconds old (time going back -
        /// a new mission's clock - counts as old enough).</summary>
        public bool TryTake(double now, double minGap, out string line, out ImpactMessageRole role)
        {
            line = string.Empty;
            role = _role;
            if (_count == 0) return false;
            double gap = double.IsNaN(minGap) || minGap < 0 ? 0 : minGap;
            if (now >= _lastShown && now - _lastShown < gap) return false;
            var s = new StringBuilder();
            for (int i = 0; i < _count; i++)
            {
                string p = _parts[i]!;
                if (i > 0 && p.StartsWith("You", StringComparison.Ordinal)) s.Append("; y").Append(p, 1, p.Length - 1); // "You …" mid-line → "you …"; a name keeps its capital
                else s.Append(i > 0 ? "; " : string.Empty).Append(p);
                _parts[i] = null;
            }
            if (_extra > 0) s.Append(" (+").Append(_extra.ToString(CultureInfo.InvariantCulture)).Append(" more)");
            line = s.ToString();
            Merged += _count - 1 + _extra;
            Shown++;
            _count = 0;
            _extra = 0;
            _role = ImpactMessageRole.YourMen;
            _lastShown = now;
            return true;
        }

        /// <summary>Everything waiting thrown away (counted).</summary>
        public void Clear()
        {
            Discarded += _count + _extra;
            for (int i = 0; i < _count; i++) _parts[i] = null;
            _count = 0;
            _extra = 0;
            _role = ImpactMessageRole.YourMen;
        }

        /// <summary>The [summary] line.</summary>
        public string SummaryLine(bool on, bool yourMen, double minGap)
        {
            var c = CultureInfo.InvariantCulture;
            return "impact messages (step 53 - on-screen lines, " + (on ? "ImpactShowMessages on" : "ImpactShowMessages OFF") + ", " + (yourMen ? "yours + your side's heroes" : "only yours")
                   + ", at most one every " + minGap.ToString("0.0", c) + " s): offered " + Offered + " (to you " + ByRole[0] + ", by you " + ByRole[1] + ", your men " + ByRole[2]
                   + "; knocked down " + ByKind[0] + ", knocked off a horse " + ByKind[1] + ", shields " + ByKind[2] + ", guards broken " + ByKind[3] + ", ripostes " + ByKind[4]
                   + ") → lines SHOWN " + Shown + ", parts MERGED into a line " + Merged + " (beyond " + ImpactMessageMath.MaxParts + " a line: " + Overflow + "), the same part twice " + Duplicates
                   + ", unshown at the end / switched off " + Discarded;
        }
    }
}
