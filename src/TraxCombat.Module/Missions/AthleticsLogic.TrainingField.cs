using System;
using System.Text;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 110 (Toni 2026-11-10, in the training field: "upon resetting my health after a fight with a training master, reset my [Athletics] while I
    /// test more"; AI_NOTES "Step 110", DESIGN decision 99 (b)). StoryMode's <c>TrainingFieldMissionController</c> (found by type NAME - the module does
    /// not reference StoryMode; no Harmony) makes every agent immortal, keeps your health in its own field and writes it onto you every tick of a bout,
    /// restores it after the bout (at once after the rookie's fall, 1.5 s after a loss or the veteran's fall) and moves its two trainers onto the enemy
    /// team at "Defeat the trainer!" and back onto yours when the bout ends. So two public signals, both read every tick in that mission only (a handful
    /// of men, no allocation): YOUR HEALTH jumping up (<see cref="TrainingFieldMath.HealthJumped"/>) and the count of men hostile to you turning 0 → some
    /// (a bout began) or some → 0 (it ended - also a bout won without a scratch, when your health never moved). Each moment = step 19's fresh start for
    /// every tracked fighter in the field (you and the trainers): the bar to his wound cap, the swing stack and the sprint full, any pause / step back /
    /// brace ended, the speeds re-targeted. Gated live: ModEnabled FIRST, AthleticsEnabled, TrainingFieldRefill; the moment is logged either way.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly TrainingFieldRefillStats _trainStats = new TrainingFieldRefillStats();
        private readonly TrainingFieldWatch _trainWatch = new TrainingFieldWatch();
        private bool _trainDead;

        internal TrainingFieldRefillStats TrainingFieldStats => _trainStats;

        /// <summary>The smoke's stand-in for "still on the field" (null in game: <c>Agent.IsActive()</c> - a native read).</summary>
        internal Func<TrackedAgent, bool>? TrainingActiveOf { get; set; }

        /// <summary>First tick: the training field's controller among the mission's behaviours (by name).</summary>
        private void NoteTrainingField()
        {
            try
            {
                foreach (var b in Mission.MissionBehaviors)
                {
                    if (!TrainingFieldMath.IsController(b?.GetType().Name)) continue;
                    _trainStats.IsTrainingField = true;
                    TraxLog.Info("athletics", "training field mission (" + TrainingFieldMath.ControllerName + "): " + TrainingFieldRefillRules.From(TraxSettings.Shared).Describe()
                        + " - read live at each moment");
                    break;
                }
            }
            catch (Exception e)
            {
                _trainDead = true;
                Failed("training.mission", e);
            }
        }

        /// <summary>Every tick at the top of the Athletics tick (after the hideout's watch): your health and the men hostile to you. Training field only.</summary>
        private void TickTrainingField(double now)
        {
            if (!_trainStats.IsTrainingField || _trainDead) return;
            try
            {
                double health = double.NaN;
                Agent? main = _player != null && !_player.Removed ? _player.Agent : null;
                if (main != null && main.IsActive()) health = HealthOf(main);
                int hostile = main != null ? HostileTo(main) : -1;
                var r = TrainingFieldRefillRules.From(TraxSettings.Shared);
                double before = _trainWatch.LastHealth;
                var why = _trainWatch.Observe(health, hostile, in r);
                if (why == TrainingRefillWhy.None) return;
                TrainingMoment(why, now, in r, before, health);
            }
            catch (Exception e)
            {
                _trainStats.Errors++;
                _trainDead = true; // the watch stops for this mission (the summary says how many errors)
                Failed("training.watch", e);
            }
        }

        /// <summary>Active men (humans) hostile to you among the tracked ones.</summary>
        private int HostileTo(Agent main)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st.Removed) continue;
                var a = st.Agent;
                if (ReferenceEquals(a, main) || !a.IsHuman || !a.IsActive()) continue;
                if (a.IsEnemyOf(main)) n++;
            }
            return n;
        }

        /// <summary>A training-field moment: the fresh start for every tracked fighter (all switches on), and the line. Internal: the smoke drives it.</summary>
        internal void TrainingMoment(TrainingRefillWhy why, double now, in TrainingFieldRefillRules rules, double healthBefore, double healthNow)
        {
            _trainStats.AddMoment(why);
            if (!rules.On)
            {
                _trainStats.Off++;
                TraxLog.Limited("athletics", TrainingFieldRefillStats.MomentLine(why, now, in rules, healthBefore, healthNow, 0, double.NaN, double.NaN, double.NaN, string.Empty), "training");
                return;
            }
            var r = AthleticsRules.From(TraxSettings.Shared);
            double youBefore = double.NaN, youAfter = double.NaN, youPool = double.NaN;
            int men = 0, others = 0;
            var sb = new StringBuilder(160); // once a bout - never per tick
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st.Removed) continue;
                if (!(TrainingActiveOf?.Invoke(st) ?? st.Agent.IsActive())) continue;
                double h = HealthOf(st.Agent);
                double was = st.Fraction;
                var o = AthleticsMath.FreshStart(st, in r, h > 0 ? h : st.Health);
                if (!o.Refilled) continue;
                _trainStats.Released += ReleaseForFreshStart(st, now, hideout: false);
                ResetPhases(st);                      // the swing stack full; nothing measured spans the fresh start
                _stackStats.FreshStarts++;
                RetargetSpeed(st, in r, exact: true); // full speed back at once
                men++;
                if (IsPlayer(st))
                {
                    youBefore = was;
                    youAfter = st.Fraction;
                    youPool = AthleticsMath.PoolPoints(in r, st);
                    _trainStats.AddYou(was);
                }
                else if (others < 4)
                {
                    if (others > 0) sb.Append(", ");
                    sb.Append(Name(st)).Append(' ').Append(Pct(was)).Append(" → ").Append(Pct(st.Fraction));
                    others++;
                }
            }
            _trainStats.Refills++;
            _trainStats.MenRefilled += men;
            TraxLog.Limited("athletics", TrainingFieldRefillStats.MomentLine(why, now, in rules, healthBefore, healthNow, men, youBefore, youAfter, youPool, sb.ToString()), "training");
        }

        /// <summary>The [summary] line of the training field (none elsewhere).</summary>
        internal void WriteTrainingFieldSummary()
        {
            if (!_trainStats.IsTrainingField) return;
            TraxLog.Info("summary", _trainStats.SummaryLine(TrainingFieldRefillRules.From(TraxSettings.Shared)));
        }
    }
}
