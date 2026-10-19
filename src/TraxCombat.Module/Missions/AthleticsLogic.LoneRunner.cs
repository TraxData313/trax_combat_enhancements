using System;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 70 - NO LONE RUNNER BEHIND THE ENEMY'S LINE (Toni's 30 v 50 looters, 2026-10-19 15:59: right after the first charge a few looters came
    /// round BEHIND his line, alone, far from their formation and their mates - one stood next to him on his horse; "they will charge, but they
    /// won't break up like that"; AI_NOTES "Step 70", DESIGN decision 64). At courage's look (every AI man on foot in a formation, ~0.5 s - never
    /// you, never riders): ALONE (fewer than 2 mates in courage's circle, an enemy within 8 m) and OUT (more than LoneRunnerAheadMetres ahead
    /// of his own unit's front, or past the enemy's front - the unit table's band frame) = a LONE RUNNER, counted by cause (the sprint into
    /// contact, the punish step-in, a courage surge, the fever press, chasing a target behind the enemy's front, the game) ON and OFF. The fixes
    /// (LoneRunnerEnabled, the master switch first): a man alone is no longer sent in by his fever; a charge's / fever's sprint into contact
    /// ends when he is alone or past the enemy's front; a lone runner walks back to his group - courage's own walk - unless his side wins
    /// clearly around him. No allocation per tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly LoneRunnerStats _loneStats = new LoneRunnerStats();
        private readonly bool[] _firstLoneLogged = new bool[(int)LoneCause.Count];
        private string _seenLoneText = string.Empty;
        private bool _firstForcedWalkLogged;
        private bool _firstLoneSprintEndLogged;
        private bool _firstFeverRefusedLogged;

        /// <summary>This mission's lone-runner numbers (the offline smoke reads them).</summary>
        internal LoneRunnerStats LoneStats => _loneStats;

        private void StartLoneRunners()
        {
            _seenLoneText = LoneRunnerRules.From(TraxSettings.Shared).Describe();
            TraxLog.Info("courage", "mission start: " + _seenLoneText + " - read live at every look; the [summary] \"lone runners\" line counts them by cause");
        }

        private void NoteLoneSettings(TraxSettings s)
        {
            string now = LoneRunnerRules.From(s).Describe();
            if (now == _seenLoneText) return;
            _seenLoneText = now;
            TraxLog.Info("courage", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now);
        }

        /// <summary>
        /// The diagnostic at his look (ON and OFF): alone, out beyond his line (his unit's band frame - his own unit's front, the enemy's front),
        /// the cause and his target. Returns true for a lone runner. <paramref name="feverPresses"/> = his fever would press (the cause's column).
        /// </summary>
        private bool LoneLook(TrackedAgent st, double now, in LoneRunnerRules lr, double px, double py, bool alone, double odds, double shaken, bool feverPresses)
        {
            var m = st.Lone;
            try
            {
                _loneStats.Looks++;
                double gap = m.Gap(now);
                m.Alone = alone;
                m.AloneAt = now;
                m.Lone = false;
                int key = st.Fear.Key;
                if (!alone || key < 0 || !_fearUnits.Fronts(key, now, px, py, out double ahead, out double past)) return false;
                if (!LoneRunnerMath.IsLone(in lr, true, ahead, past)) return false;
                m.Lone = true;
                _loneStats.LoneLooks++;
                // his target (one read - lone men only): behind the enemy's front?
                var target = FearBody.Target(st, out double tx, out double ty);
                _loneStats.TargetReads++;
                double targetPast = target == LoneTarget.None ? double.NaN : _fearUnits.PastEnemyFront(key, now, tx, ty);
                bool behind = !double.IsNaN(targetPast) && targetPast > LoneRunnerMath.PastFrontMetres;
                var sp = st.Sprint;
                var cause = LoneRunnerMath.Cause(sp.Active && sp.IntoContact, st.Punishing, st.Courage.Surging(now), feverPresses, behind);
                m.Cause = cause;
                double add = double.IsNaN(gap) ? 0 : gap;
                if (LoneRunnerMath.Winning(odds, TraxSettings.Shared.FearWinClearOddsPercent, shaken)) _loneStats.WinningSeconds += add;
                _loneStats.Add(m, lr.Enabled, cause, add, behind ? target : LoneTarget.None);
                int c = (int)cause;
                if (!_firstLoneLogged[c])
                {
                    _firstLoneLogged[c] = true;
                    TraxLog.Info("courage", "first LONE RUNNER by " + LoneRunnerMath.CauseName(cause) + " (step 70): " + Name(st) + " at " + Sec(now) + " s - "
                        + (double.IsNaN(ahead) ? "-" : F1(ahead)) + " m ahead of his unit's front, " + (double.IsNaN(past) ? "the enemy's front unknown" : F1(past) + " m past the enemy's front")
                        + ", " + "alone (fewer than " + FearMath.AloneMates + " mates near, an enemy within " + F0(LoneRunnerMath.EnemyMetres) + " m); his target "
                        + LoneRunnerMath.TargetName(target) + (behind ? " BEHIND the enemy's front (" + F1(targetPast) + " m past it)" : target == LoneTarget.None ? string.Empty : " (not behind the enemy's front)")
                        + "; sprint " + (sp.Active ? (sp.IntoContact ? "INTO contact" : "running") : "no") + ", fever " + (feverPresses ? "PRESSING" : "no") + ", surge "
                        + (st.Courage.Surging(now) ? "yes" : "no") + ", punish " + (st.Punishing ? "yes" : "no") + "; the fixes " + (lr.Enabled ? "ON" : "OFF (" + lr.OffBecause + ")"));
                }
                return true;
            }
            catch (Exception e)
            {
                _loneStats.Errors++;
                Failed("lone.look", e);
                m.Lone = false;
                return false;
            }
        }

        /// <summary>FIX 1's count: his fever would have pressed him in, but he is alone - courage decides him.</summary>
        private void NoteFeverRefused(TrackedAgent st, double now)
        {
            _loneStats.FeverRefusedLooks++;
            if (_firstFeverRefusedLogged) return;
            _firstFeverRefusedLogged = true;
            TraxLog.Info("courage", "first fever press REFUSED for a man alone this battle (step 70): " + Name(st) + " at " + Sec(now)
                + " s - fewer than " + FearMath.AloneMates + " mates near with an enemy close: courage's odds and walk back decide him, not his fever");
        }

        /// <summary>FIX 3's count: a lone runner's walk back began (courage's walk, whatever the odds).</summary>
        private void NoteForcedWalk(TrackedAgent st, double now)
        {
            _loneStats.WalksForced++;
            st.Lone.Forced = true;
            if (_firstForcedWalkLogged) return;
            _firstForcedWalkLogged = true;
            TraxLog.Info("courage", "first LONE RUNNER walked back this battle (step 70): " + Name(st) + " at " + Sec(now) + " s - alone out beyond his line ("
                + LoneRunnerMath.CauseName(st.Lone.Cause) + "): courage's walk back to his group, whatever the odds"
                + (st.Sprint.Active && st.Sprint.Regroup ? ", sprinting " + F2(st.Sprint.Length) + " s" : string.Empty));
        }

        /// <summary>FIX 2 (from the sprint pass, every 0.25 s of a sprint INTO contact): alone at the last look, or past the enemy's front now.</summary>
        private bool LoneSprintEnds(TrackedAgent st, double now)
        {
            var lr = LoneRunnerRules.From(TraxSettings.Shared);
            if (!lr.Enabled) return false;
            bool alone = st.Lone.AloneNow(now);
            double past = double.NaN;
            int key = st.Fear.Key;
            if (!alone && key >= 0 && CourageBody.Position(st, out double x, out double y)) past = _fearUnits.PastEnemyFront(key, now, x, y);
            if (!LoneRunnerMath.SprintEnds(in lr, alone, past)) return false;
            _loneStats.SprintsEnded++;
            if (!_firstLoneSprintEndLogged)
            {
                _firstLoneSprintEndLogged = true;
                TraxLog.Info("sprint", "first sprint INTO contact ended for a lone runner this battle (step 70): " + Name(st) + " at " + Sec(now) + " s - "
                    + (alone ? "his mates left behind (fewer than " + FearMath.AloneMates + " near)" : F1(past) + " m past the enemy's front") + ", after " + F2(st.Sprint.Elapsed) + " s");
            }
            return true;
        }
    }
}
