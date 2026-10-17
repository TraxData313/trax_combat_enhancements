using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 54 - IMPACT I7, THE AI PUNISHES A WINDOW (docs/IMPACT.md §7, §8 q14; AI_NOTES "Step 54"; DESIGN decision 46).
    ///
    /// A WINDOW = a man reeling or on the ground from a LIVE impact outcome (his <see cref="ImpactState"/>, written by <see cref="ImpactDecide"/>;
    /// the player's too) + <c>ImpactPunishGraceSeconds</c>. Every window is noted where the decision writes the state
    /// (<see cref="NotePunishWindow"/>: counted once, the battle-wide "any window open" mark moved). At the FEVER's look (~0.25 s, staggered -
    /// <see cref="PollFever"/>) an AI man on foot in contact reads his target (<see cref="IPunishBody"/> - one native read, and only while some
    /// window is open anywhere); a target in a window = he PUNISHES: <see cref="RetargetDecision"/> raises his aggression share to
    /// <c>ImpactPunishAggressionPercent</c> (step 43's path: 0.05 steps, a recompute under the tick's budget) until a look sees the window
    /// closed; a timid man (step 49) within <c>ImpactPunishRangeMetres</c> steps in like a surge, brave until it closes. Never the player as
    /// the attacker (his hands punish), never a rider (the decorator keeps riders' values), never in shadow. Measured with the punish ON and
    /// OFF alike: attacks per man-minute against targets in a window vs not, windows punished vs wasted. No allocation per tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly PunishStats _punish = new PunishStats();
        private double _punishAnyUntil = double.NegativeInfinity;
        private string _seenPunishText = string.Empty;
        private bool _firstWindowLogged;
        private bool _firstRaiseLogged;
        private bool _firstPunishedLogged;
        private bool _firstPlayerPunishedLogged;
        private bool _firstTimidPressLogged;

        /// <summary>This mission's punish numbers (the offline smoke reads them).</summary>
        internal PunishStats PunishStats => _punish;

        /// <summary>The target read (the smoke swaps in a stand-in).</summary>
        internal IPunishBody PunishBody { get; set; } = PunishBodyDefault.Current;

        private void StartPunish()
        {
            _seenPunishText = PunishRules.From(TraxSettings.Shared).Describe();
            TraxLog.Info("impact", "mission start, the AI punishing a window (step 54): " + _seenPunishText + " - read live; looked at with the fever's look (every man about every "
                + F2(FeverMath.PollSeconds) + " s); the [summary] \"impact punish\" lines count windows opened / seen / punished and AI attacks per man-minute against a target in a window vs not, punish ON and OFF");
        }

        /// <summary>A punish setting changed (from ApplySettingsChange - its RetargetSpeed loop re-targets every man's factor).</summary>
        private void NotePunishSettings(TraxSettings s)
        {
            string now = PunishRules.From(s).Describe();
            if (now == _seenPunishText) return;
            _seenPunishText = now;
            TraxLog.Info("impact", "punish settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now);
        }

        // ------------------------------------------------------------------ the windows (from the decision)

        /// <summary>The decision wrote <paramref name="victim"/>'s state (live, not shadow): a NEW window counted (opened on an AI man / on
        /// you), the battle-wide mark moved so the looks start reading targets. Main thread; no engine call.</summary>
        private void NotePunishWindow(TrackedAgent victim, double now)
        {
            if (victim.Removed) return;
            var pr = PunishRules.From(TraxSettings.Shared);
            if (!pr.WindowsLive) return;
            double end = PunishMath.WindowEnd(in victim.Impact);
            if (!(end > now)) return;
            double until = end + pr.GraceSeconds;
            if (double.IsNaN(victim.PunishWindowEnd) || now >= victim.PunishWindowEnd)
            {
                bool onPlayer = IsPlayer(victim);
                _punish.Opened(onPlayer);
                victim.PunishWindowSeen = false;
                victim.PunishWindowHit = false;
                victim.PunishWindowEnd = until;
                if (!_firstWindowLogged)
                {
                    _firstWindowLogged = true;
                    TraxLog.Info("impact", "first window this battle: " + Name(victim) + (onPlayer ? " (YOU)" : string.Empty) + " " + (victim.Impact.Down(now) ? "down" : "reeling")
                        + " at " + Sec(now) + " s until " + Sec(end) + " s (+ " + F2(pr.GraceSeconds) + " s grace) - " + (pr.Enabled
                            ? "the AI men fighting him press it (attack decisions x" + F2(pr.Share) + ")"
                            : "measured only (" + pr.OffBecause + ")"));
                }
            }
            else if (until > victim.PunishWindowEnd) victim.PunishWindowEnd = until;
            double any = until + PunishMath.AnyWindowSlackSeconds;
            if (any > _punishAnyUntil) _punishAnyUntil = any;
        }

        // ------------------------------------------------------------------ the look (from the fever's)

        /// <summary>An AI man on foot at his fever look (contact already read): the proof's time since the last look; his target read while a
        /// window is open anywhere and he is in contact; a target in a window = he punishes (the share raised by RetargetDecision right after);
        /// a timid man within range steps in.</summary>
        private void PunishLook(TrackedAgent st, double now, in PunishRules pr, double gap)
        {
            if (st.PunishContact && gap > 0) _punish.AddTime(st.PunishOn, st.PunishInWindow, gap);
            bool live = pr.WindowsLive;
            if (live) _punish.EverLive = true;
            _punish.Looks++;
            TrackedAgent? target = null;
            float distance = float.NaN;
            bool inWindow = false;
            if (live && st.FeverContact && now < _punishAnyUntil)
            {
                long t0 = Stopwatch.GetTimestamp();
                _punish.TargetReads++;
                target = Get(PunishBody.Target(st, out distance));
                if (target != null && (target.Removed || ReferenceEquals(target, st))) target = null;
                inWindow = target != null && PunishMath.InWindow(in target.Impact, now, pr.GraceSeconds);
                _punish.LookCostMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            }
            st.PunishTarget = target;
            st.PunishContact = live && st.FeverContact;
            st.PunishOn = pr.Enabled;
            st.PunishInWindow = inWindow;
            if (inWindow && !target!.PunishWindowSeen)
            {
                target.PunishWindowSeen = true;
                _punish.Seen(IsPlayer(target));
            }
            bool want = inWindow && pr.Enabled;
            if (want != st.Punishing)
            {
                st.Punishing = want;
                if (want)
                {
                    _punish.Raised++;
                    if (!_firstRaiseLogged)
                    {
                        _firstRaiseLogged = true;
                        TraxLog.Info("impact", "first AI man pressing a window this battle: " + Name(st) + " at " + Sec(now) + " s - his target " + Name(target!)
                            + (IsPlayer(target!) ? " (YOU)" : string.Empty) + " " + (target!.Impact.Down(now) ? "down" : "reeling") + ", " + F1(distance) + " m off: attack decisions x"
                            + F2(PunishMath.Raise(FeverMath.DecisionFactor(FeverRules.From(TraxSettings.Shared), st.Fever.Phase, st.Fever.Left, st.Fever.Full, st.Fraction, false), in pr, true, false))
                            + " (his own share x" + F2(st.DecisionFactor) + ") until the window closes");
                    }
                }
                else _punish.Ended++;
            }
            if (want) PunishTimid(st, target!, distance, now, in pr);
        }

        /// <summary>A man punishing within ImpactPunishRangeMetres of the target: brave until the window closes (courage's surge clock); a
        /// timid one's spell ended at once (counted). Never in a panic.</summary>
        private void PunishTimid(TrackedAgent st, TrackedAgent target, float distance, double now, in PunishRules pr)
        {
            var cm = st.Courage;
            if (!PunishMath.TimidPresses(in pr, cm.Panic, distance)) return;
            if (!CourageRules.From(TraxSettings.Shared).Enabled) return;
            double until = PunishMath.WindowEnd(in target.Impact) + pr.GraceSeconds;
            if (until > cm.SurgeUntil) cm.SurgeUntil = until;
            if (!cm.Timid) return;
            _punish.TimidPresses++;
            EndTimid(st, CourageEnd.PunishWindow, now, native: true);
            if (!_firstTimidPressLogged)
            {
                _firstTimidPressLogged = true;
                TraxLog.Info("impact", "first timid man stepping into a window this battle: " + Name(st) + " at " + Sec(now) + " s - " + Name(target) + " "
                    + (target.Impact.Down(now) ? "down" : "reeling") + " " + F1(distance) + " m off (within " + F1(pr.RangeMetres) + " m): brave until " + Sec(until) + " s, like a surge");
            }
        }

        /// <summary>He is no longer looked at as an AI man on foot (the player took him, he mounted): no punish, no proof time.</summary>
        private void PunishClear(TrackedAgent st)
        {
            if (st.Punishing)
            {
                st.Punishing = false;
                _punish.Ended++;
            }
            st.PunishTarget = null;
            st.PunishContact = false;
            st.PunishInWindow = false;
        }

        // ------------------------------------------------------------------ the proof (from the release)

        /// <summary>A melee release (StartRelease): an AI man on foot in contact at his last look while windows are live - counted by the punish
        /// on / off and his target in a window or not (the target of his last look, its state NOW); an attack inside a window marks it punished.</summary>
        internal void NotePunishAttack(TrackedAgent st, bool mounted, double now)
        {
            if (mounted || !st.PunishContact || IsPlayer(st)) return;
            var target = st.PunishTarget;
            bool inWindow = target != null && !target.Removed && PunishMath.InWindow(in target.Impact, now, TraxSettings.Shared.ImpactPunishGraceSeconds);
            _punish.AddAttack(st.PunishOn, inWindow);
            if (!inWindow) return;
            bool onPlayer = IsPlayer(target!);
            if (onPlayer) _punish.PlayerAttacksInWindow++;
            if (target!.PunishWindowHit) return;
            target.PunishWindowHit = true;
            _punish.Punished(onPlayer);
            if (!_firstPunishedLogged)
            {
                _firstPunishedLogged = true;
                TraxLog.Info("impact", "first window punished this battle: " + Name(st) + " started an attack at " + Sec(now) + " s on " + Name(target)
                    + " (" + (target.Impact.Down(now) ? "down" : "reeling") + ") - the punish " + (st.PunishOn ? "ON" : "OFF"));
            }
            if (onPlayer && !_firstPlayerPunishedLogged)
            {
                _firstPlayerPunishedLogged = true;
                TraxLog.Info("impact", "YOU: first window of yours punished this battle - " + Name(st) + " attacked you at " + Sec(now) + " s while you were "
                    + (target.Impact.Down(now) ? "down" : "reeling"));
            }
        }

        internal void WritePunishSummary()
        {
            var lines = new List<string>(4);
            _punish.AddSummaryLines(lines, PunishRules.From(TraxSettings.Shared));
            foreach (var line in lines) TraxLog.Info("summary", line);
        }
    }
}
