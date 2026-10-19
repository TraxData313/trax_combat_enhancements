using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 62 (AI_NOTES "Step 62"), THE LOST FALLS: the pure rules of making a decided fall really PLAY. Anton's log of 2026-10-19 09:30:
    /// 35 clean-hit knockdowns decided, 7 Falls seen, 13 not seen, 14 answered on a blow the game had SHRUGGED OFF - the engine asks
    /// <c>DecideAgentShrugOffBlow</c> FIRST in <c>Mission.CreateMeleeBlow</c> (Mission.cs 5555, then KnockBack 5571, then KnockDown 5575) and a
    /// weapon blow flagged ShrugOff plays no reaction (a kick's does - <c>Blow.NoIgnore</c> is set for alternative attacks), so our KnockDown
    /// was thrown away. Two fixes: (1) the shrug-off seam decides the blow FIRST and refuses the shrug-off when the model knocks him down or
    /// staggers him (<c>ImpactShrugOffYieldsToImpact</c>; the KnockDown seam then takes that same decision - one per blow); (2) a BACKUP: a
    /// decided fall not seen within <c>ImpactFallBackupSeconds</c> = our own knockdown blow (lever 6, proven 10/10 in the spike) from the tick,
    /// once. A blow that KILLS him is never decided (a dead man plays his death, not our fall - it was counted "not seen" before).
    /// </summary>
    public static class ImpactFallMath
    {
        /// <summary>The longest backup wait a setting may ask (s) - the watch's own window to see a Fall is 1 s, a backup past 3 s would
        /// floor a man who already fought on.</summary>
        public const double MaxBackupSeconds = 3;

        /// <summary>Is the backup due now? A fall watch (a kick's or a clean hit's knockdown) that has not shown a Fall, no backup sent yet,
        /// the setting above 0 and its wait over.</summary>
        public static bool BackupDue(double now, double since, double backupSeconds, bool appeared, bool sent)
        {
            if (appeared || sent || !(backupSeconds > 0) || double.IsNaN(since)) return false;
            return now - since >= backupSeconds;
        }

        /// <summary>A blow that leaves him under 1 hit point kills him (the engine's own rule - <c>Agent.HandleBlow</c> / vanilla's
        /// shrug-off and knock-back questions: health − damage &lt; 1).</summary>
        public static bool Fatal(double health, double damage) => !double.IsNaN(health) && !double.IsNaN(damage) && health - damage < 1;
    }

    /// <summary>
    /// Step 62: one mission's FALLS - every knockdown decided on a LANDED blow (kick / bash / clean hit, on foot) and what became of it:
    /// played by the game (its Fall seen from our answer), played by our BACKUP blow, still not seen, or the man left the field (died) first;
    /// the shrug-offs the model refused for a decided fall / stagger; the blows not decided because they killed; the 0-damage blows (the
    /// engine plays no reaction on them - <c>Agent.HandleBlow</c> returns before <c>HandleBlowAux</c>). The [summary] "impact falls" line:
    /// the target is falls seen ≈ falls decided. Main thread only.
    /// </summary>
    public sealed class ImpactFallStats
    {
        /// <summary>The kinds counted: 0 kicks / bashes, 1 clean hits.</summary>
        public const int KindCount = 2;

        public readonly int[] Decided = new int[KindCount];
        public readonly int[] PlayedByGame = new int[KindCount];
        public readonly int[] PlayedByBackup = new int[KindCount];
        public readonly int[] NotSeen = new int[KindCount];
        public readonly int[] LeftFieldFirst = new int[KindCount];
        public readonly int[] ZeroDamage = new int[KindCount];

        /// <summary>Backups sent (our blow registered) / refused before any engine call (down, a siege engine, water - CanForce) / refused by
        /// the engine or its lever off.</summary>
        public int BackupsSent, BackupsRefused, BackupsFailed;

        /// <summary>The shrug-off seam: the game said "shrugged off", the model decided the blow there and REFUSED it for a knockdown / for a
        /// stagger; decided there with no outcome (the game's answer, then step 52's tired rule).</summary>
        public int ShrugRefusedForFall, ShrugRefusedForStagger, ShrugDecidedNothing;

        /// <summary>The KnockDown seam took the decision the shrug-off seam made for the same blow (one decision per blow).</summary>
        public int TakenFromShrugSeam;

        /// <summary>Landed blows not decided: they killed the man (nothing to fall - he dies).</summary>
        public int Fatal;

        /// <summary>A decided fall's watch taken over by a newer reaction on the same man before the Fall showed.</summary>
        public int Replaced;

        /// <summary>Seconds from the blow to the Fall: the game's, our backup's (from the blow, the wait included).</summary>
        public MeanStd GameDelay, BackupDelay;

        public static int KindOf(ImpactEvent e) => ImpactMath.IsAlternative(e) ? 0 : 1;

        public static string KindName(int k) => k == 0 ? "kicks / bashes" : "clean hits";

        public int Total(int[] row)
        {
            int n = 0;
            foreach (var v in row) n += v;
            return n;
        }

        public void NoteDecided(ImpactEvent e, double damage)
        {
            int k = KindOf(e);
            Decided[k]++;
            if (!(damage > 0)) ZeroDamage[k]++;
        }

        public void NotePlayed(ImpactEvent e, bool backup, double delay)
        {
            int k = KindOf(e);
            if (backup)
            {
                PlayedByBackup[k]++;
                BackupDelay.Add(delay);
            }
            else
            {
                PlayedByGame[k]++;
                GameDelay.Add(delay);
            }
        }

        public void NoteNotSeen(ImpactEvent e) => NotSeen[KindOf(e)]++;

        public void NoteLeftField(ImpactEvent e) => LeftFieldFirst[KindOf(e)]++;

        /// <summary>The [summary] "impact falls" line (one line; "none" when nothing was decided and nothing refused).</summary>
        public string SummaryLine(bool live, double backupSeconds, bool shrugYields)
        {
            var c = CultureInfo.InvariantCulture;
            var s = new StringBuilder("impact falls (step 62 - every knockdown decided on a landed blow and what became of it; the target: seen = decided): ");
            int decided = Total(Decided);
            if (decided == 0 && ShrugRefusedForStagger == 0 && Fatal == 0)
            {
                s.Append(live ? "no knockdown decided on a landed blow this mission" : "not live this mission");
            }
            else
            {
                s.Append("decided ").Append(decided.ToString(c));
                for (int k = 0; k < KindCount; k++)
                {
                    s.Append(k == 0 ? " (" : ", ").Append(KindName(k)).Append(' ').Append(Decided[k].ToString(c));
                }
                s.Append(") → played by the game ").Append(Total(PlayedByGame).ToString(c));
                if (GameDelay.Count > 0) s.Append(" (").Append(GameDelay.Mean.ToString("0.00", c)).Append(" s after the blow)");
                s.Append(", by our backup blow ").Append(Total(PlayedByBackup).ToString(c));
                if (BackupDelay.Count > 0) s.Append(" (").Append(BackupDelay.Mean.ToString("0.00", c)).Append(" s after the blow)");
                s.Append(", STILL NOT SEEN ").Append(Total(NotSeen).ToString(c))
                    .Append(", the man left the field / died first ").Append(Total(LeftFieldFirst).ToString(c))
                    .Append(", its watch taken by a newer reaction ").Append(Replaced.ToString(c))
                    .Append("; by kind - played game / backup / not seen: ");
                for (int k = 0; k < KindCount; k++)
                {
                    if (k > 0) s.Append(" | ");
                    s.Append(KindName(k)).Append(' ').Append(PlayedByGame[k].ToString(c)).Append(" / ").Append(PlayedByBackup[k].ToString(c)).Append(" / ").Append(NotSeen[k].ToString(c));
                }
                s.Append("; on a 0-damage blow (the engine plays no reaction) ").Append(Total(ZeroDamage).ToString(c));
            }
            s.Append("; backups (ImpactFallBackupSeconds ").Append(backupSeconds > 0 ? backupSeconds.ToString("0.0#", c) + " s" : "0 - off")
                .Append(") sent ").Append(BackupsSent.ToString(c)).Append(", refused (down / a siege engine / water) ").Append(BackupsRefused.ToString(c))
                .Append(", failed ").Append(BackupsFailed.ToString(c));
            s.Append("; the game's shrug-off REFUSED for a decided fall ").Append(ShrugRefusedForFall.ToString(c)).Append(" / stagger ").Append(ShrugRefusedForStagger.ToString(c))
                .Append(", decided at the shrug-off with nothing ").Append(ShrugDecidedNothing.ToString(c))
                .Append(" (ImpactShrugOffYieldsToImpact ").Append(shrugYields ? "on" : "OFF").Append("; taken by the KnockDown seam ").Append(TakenFromShrugSeam.ToString(c)).Append(")");
            s.Append("; landed blows not decided because they KILLED ").Append(Fatal.ToString(c));
            return s.ToString();
        }
    }
}
