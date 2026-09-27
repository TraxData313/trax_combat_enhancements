using System;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// The Athletics engine (DESIGN §2) - per-fighter state, blow detection, regen, the attack-speed
    /// penalty and hot swap. The rules themselves are Core's pure <see cref="AthleticsMath"/>; this
    /// file feeds them from the game (RESEARCH §B-§F):
    ///
    /// STATE: one <see cref="TrackedAgent"/> per human agent, built at <c>OnAgentBuild</c> (+ a sweep
    /// on the first tick for anyone spawned before us), in an array by <c>Agent.Index</c>
    /// (reference-checked: indices are reused) and a dense array for the loops (swap-removal at
    /// <c>OnAgentRemoved</c>). Hero / party-leader flags cached at spawn; every multiplier and cost
    /// is read live per blow.
    ///
    /// BLOWS: melee = the rising edge into <c>ReleaseMelee</c> on action channel 1, polled for every
    /// fighter every tick (one native call each - no engine event exists for a swing), and also
    /// checked inside <c>OnMeleeHit</c> in case the hit comes before the poll saw the release.
    /// Ranged = <c>OnAgentShootMissile</c> (a second projectile within 0.1 s is the same shot).
    /// CostOnMiss off = charge at the first landed hit of a swing (keyed on the swing counter) and
    /// when one of the fighter's own missiles hits an agent. Couched lance / braced spear
    /// (<c>IsDoingPassiveAttack</c>) = one blow when it lands, at most one per BlowTimeSeconds.
    /// Kicks, bashes (<c>IsAlternativeAttack</c>), horse charges and siege engines are free.
    ///
    /// REGEN: every 0.1 s (engine plumbing - the integration is exact, only the moving sample and
    /// the recovery are that coarse), only for fighters below full or exhausted: the horse's speed
    /// for a rider, its top speed passed along for step 5c.
    ///
    /// SPEED: the multiplier (<see cref="AthleticsMath.AttackSpeedMultiplier"/>, a float per fighter)
    /// changes only on a transition or a settings change; the fighter is marked and the tick loop
    /// calls <c>Agent.UpdateAgentProperties()</c> once - never from inside an engine hit callback -
    /// and <see cref="TraxAgentStatModel"/> applies the multiplier on that and every later recompute.
    ///
    /// HOT SWAP: every rule reads <see cref="TraxSettings.Shared"/> at use; the tick compares the
    /// settings version and, on a change, re-targets speeds (ExhaustedAttackSpeedPercent) or puts
    /// everyone back to full and lifts every penalty (AthleticsEnabled off).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>Regen step (engine plumbing, see the class doc).</summary>
        private const float RegenStepSeconds = 0.1f;

        /// <summary>Two shot events closer than this are one release of a multi-projectile weapon
        /// (RESEARCH §B; engine plumbing).</summary>
        private const double ShotDedupeSeconds = 0.1;

        private const int ActionReleaseMelee = (int)Agent.ActionCodeType.ReleaseMelee;
        private const int ActionReleaseRanged = (int)Agent.ActionCodeType.ReleaseRanged;
        private const int ActionReleaseThrowing = (int)Agent.ActionCodeType.ReleaseThrowing;
        private const int ActionKick = (int)Agent.ActionCodeType.Kick;
        private const int ActionWeaponBash = (int)Agent.ActionCodeType.WeaponBash;

        private static AthleticsLogic? _current;

        private TrackedAgent?[] _byIndex = new TrackedAgent?[512];
        private TrackedAgent[] _dense = new TrackedAgent[512];
        private int _count;
        private int _swept;
        private readonly System.Collections.Generic.List<TrackedAgent> _heroes = new System.Collections.Generic.List<TrackedAgent>();
        private readonly AthleticsStats _stats = new AthleticsStats();

        /// <summary>This mission's numbers (the offline smoke reads them).</summary>
        internal AthleticsStats Stats => _stats;

        private int _seenVersion = -1;
        private bool _seenEnabled;
        private int _seenSpeedPercent;
        private double _regenAccum;
        private bool _speedsSampled;

        private Agent? _playerAgent;
        private TrackedAgent? _player;

        // The once-per-mission proof that the penalty reaches the agent's properties.
        private TrackedAgent? _firstExhausted;
        private SpeedPenalty.Snapshot _firstBefore;
        private SpeedPenalty.Snapshot _firstAfter;
        private double _firstAt;
        private float _firstAsked;
        private bool _firstAfterLogged;
        private bool _firstRecovering;
        private bool _firstDone;

        /// <summary>The Athletics logic of the mission running now (null between missions) - the stat
        /// decorator and the HUD read through it.</summary>
        internal static AthleticsLogic? Current => _current;

        private static AthleticsRules Rules => AthleticsRules.From(TraxSettings.Shared);

        // ------------------------------------------------------------------ lifecycle

        private void StartAthletics()
        {
            _current = this;
            var r = Rules;
            _seenVersion = TraxSettings.Shared.Version;
            _seenEnabled = r.Enabled;
            _seenSpeedPercent = r.ExhaustedAttackSpeedPercent;
            TraxLog.Info("athletics", "mission start: " + AthleticsStats.DescribeRules(in r) + " - read live");
            TraxLog.Info("athletics", Campaign.Current != null
                ? "party-leader rule: campaign - the hero who leads the fighter's own party (you for yours)"
                : "party-leader rule: no campaign (custom battle) - the side's general, or every hero of a side without one");
            var top = MissionGameModels.Current?.AgentStatCalculateModel;
            TraxLog.Info("speed", top is TraxAgentStatModel ours
                ? "stat model on top in this mission: ours, over " + ours.BaseModelName + " - the attack-speed penalty is applied on every recompute"
                : "WARNING: the stat model on top in this mission is " + (top?.GetType().FullName ?? "(none)")
                  + ", not ours - the attack-speed penalty will NOT apply (another mod registered after us?)");
        }

        private void StopAthletics()
        {
            if (ReferenceEquals(_current, this)) _current = null;
        }

        /// <summary>First tick: pick up any human agent spawned before we were attached.</summary>
        private void SweepAgents()
        {
            int before = _count;
            foreach (var a in Mission.Agents)
            {
                if (a == null || !a.IsHuman || !a.IsActive()) continue;
                if (Get(a) == null) Track(a);
            }
            _swept = _count - before;
            TraxLog.Info("athletics", "first tick: tracking " + _count + " fighters" + (_swept > 0 ? " (" + _swept + " picked up by the first-tick sweep)" : string.Empty));
        }

        // ------------------------------------------------------------------ per-agent state

        private TrackedAgent? Get(Agent? agent)
        {
            if (agent == null) return null;
            int i = agent.Index;
            if (i < 0 || i >= _byIndex.Length) return null;
            var st = _byIndex[i];
            return st != null && ReferenceEquals(st.Agent, agent) ? st : null;
        }

        /// <summary>Starts tracking a human agent (idempotent). Internal: the offline smoke drives it.</summary>
        internal TrackedAgent? Track(Agent agent)
        {
            int i = agent.Index;
            if (i < 0) return null;
            if (i >= _byIndex.Length)
            {
                int size = _byIndex.Length;
                while (size <= i) size *= 2;
                Array.Resize(ref _byIndex, size);
            }
            var old = _byIndex[i];
            if (old != null)
            {
                if (ReferenceEquals(old.Agent, agent)) return old;
                RemoveFromLoop(old); // a stale record: the index was reused
            }

            var st = new TrackedAgent(agent);
            FlagHeroAndLeader(st);
            _byIndex[i] = st;
            if (_count == _dense.Length) Array.Resize(ref _dense, _dense.Length * 2);
            st.DenseSlot = _count;
            _dense[_count++] = st;
            return st;
        }

        private void Untrack(Agent agent)
        {
            var st = Get(agent);
            if (st == null) return;
            if (ReferenceEquals(st, _firstExhausted) && !_firstDone && st.Exhausted)
            {
                _firstDone = true;
                TraxLog.Info("speed", "first exhausted fighter (" + Name(st) + ") left the field still exhausted after "
                    + Sec(Mission.CurrentTime - _firstAt) + " s - attack properties then: " + SpeedPenalty.Snapshot.Take(agent)
                    + " (" + SpeedPenalty.Snapshot.Take(agent).RatioTo(_firstBefore) + " of the fresh values)");
            }
            RemoveFromLoop(st);
        }

        private void RemoveFromLoop(TrackedAgent st)
        {
            if (st.Removed) return;
            st.Removed = true;
            if (st.AgentIndex >= 0 && st.AgentIndex < _byIndex.Length && ReferenceEquals(_byIndex[st.AgentIndex], st))
                _byIndex[st.AgentIndex] = null;
            int slot = st.DenseSlot;
            if (slot >= 0 && slot < _count && ReferenceEquals(_dense[slot], st))
            {
                var last = _dense[--_count];
                _dense[slot] = last;
                last.DenseSlot = slot;
                _dense[_count] = null!;
            }
            st.DenseSlot = -1;
        }

        /// <summary>
        /// RESEARCH §E. Campaign: the hero whose party (the agent's origin → PartyBase) he leads -
        /// the player for his own party, a lord for his (in a tournament too: the origin's party is
        /// the hero's own). No campaign (custom battle): the side's general; a side without one
        /// (the enemy side in vanilla custom battle) → every hero on it (DESIGN interpretation 7).
        /// Cached here, never the product: the multipliers are read live per blow.
        /// </summary>
        private void FlagHeroAndLeader(TrackedAgent st)
        {
            var a = st.Agent;
            st.IsHero = a.IsHero;
            bool leader;
            if (Campaign.Current != null)
            {
                var leaderHero = (a.Origin?.BattleCombatant as PartyBase)?.LeaderHero;
                leader = leaderHero != null && ReferenceEquals(leaderHero.CharacterObject, a.Character);
            }
            else
            {
                var general = a.Origin?.BattleCombatant?.General;
                leader = general != null ? ReferenceEquals(general, a.Character) : st.IsHero;
            }
            st.IsLeader = leader;
            if (!st.IsHero && !st.IsLeader) return;

            st.HeroName = SafeName(a);
            _heroes.Add(st);
            _stats.HeroesFlagged++;
            var r = Rules;
            if (st.IsLeader)
            {
                _stats.LeaderNames.Add(st.HeroName + (a.IsMainAgent ? " (you)" : string.Empty));
                TraxLog.Limited("athletics", "party leader: " + st.HeroName + (st.IsHero ? "" : " (not a hero)")
                    + " - pays x" + F2(AthleticsMath.CostMultiplier(in r, st)) + " per blow (" + F1(AthleticsMath.BlowCostPoints(in r, st)) + " now)",
                    "athletics-leader");
            }
            else if (TraxLog.VerboseOn)
            {
                TraxLog.Verbose("athletics", "hero: " + st.HeroName + " - pays x" + F2(AthleticsMath.CostMultiplier(in r, st))
                    + " per blow (" + F1(AthleticsMath.BlowCostPoints(in r, st)) + " now)", "athletics-hero");
            }
        }

        // ------------------------------------------------------------------ the tick

        private void TickAthletics(float dt)
        {
            var settings = TraxSettings.Shared;
            if (settings.Version != _seenVersion) ApplySettingsChange(settings);
            var r = AthleticsRules.From(settings);
            double now = Mission.CurrentTime;
            long start = Stopwatch.GetTimestamp();
            TrackPlayer();

            int polled = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                try
                {
                    var a = st.Agent;
                    if (!a.IsActive()) continue;
                    if (r.Enabled)
                    {
                        polled++;
                        int action = (int)a.GetCurrentActionType(1);
                        if (action != st.PrevAction) ObserveAction(st, action, now, in r);
                    }
                    if (st.SpeedDirty) ApplySpeed(st);
                }
                catch (Exception e)
                {
                    st.SpeedDirty = false;
                    Failed("athletics.poll", e);
                }
            }

            _regenAccum += dt;
            if (_regenAccum >= RegenStepSeconds)
            {
                double step = _regenAccum;
                _regenAccum = 0;
                RegenPass(step, now, in r);
            }

            if (now - _formationRefreshedAt >= settings.FormationStatsRefreshSeconds || now < _formationRefreshedAt)
            {
                try
                {
                    RefreshFormationStats(now, in r);
                }
                catch (Exception e)
                {
                    Failed("athletics.formation-stats", e);
                }
            }

            _stats.AddTick(polled, (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }

        private void TrackPlayer()
        {
            var main = Mission?.MainAgent;
            if (ReferenceEquals(main, _playerAgent)) return;
            _playerAgent = main;
            var st = Get(main);
            if (st != null) _player = st;
        }

        /// <summary>The channel-1 action changed (seen by the poll or inside a hit): the falling edge
        /// of a swing ends its measured length, the rising edge into ReleaseMelee is a swing; ranged
        /// releases, kicks and bashes are counted for the cross-checks (never charged here).</summary>
        internal void ObserveAction(TrackedAgent st, int action, double now, in AthleticsRules r)
        {
            int prev = st.PrevAction;
            st.PrevAction = action;
            if (prev == ActionReleaseMelee) EndRelease(st, now);
            switch (action)
            {
                case ActionReleaseMelee:
                    StartRelease(st, now, in r);
                    break;
                case ActionReleaseRanged:
                case ActionReleaseThrowing:
                    _stats.RangedReleasesPolled++;
                    break;
                case ActionKick:
                    _stats.KicksSeen++;
                    break;
                case ActionWeaponBash:
                    _stats.BashesSeen++;
                    break;
            }
        }

        private void StartRelease(TrackedAgent st, double now, in AthleticsRules r)
        {
            bool mounted = st.Agent.MountAgent != null;
            _stats.MeleeReleasesSeen++;
            if (mounted) _stats.MeleeReleasesMounted++;
            st.ReleaseSerial++;

            bool penalizedNow = st.SpeedMultiplier < 1f;
            if (st.LastReleaseTime >= 0)
                AddInterval(_stats.MeleeFresh, _stats.MeleeExhausted, now - st.LastReleaseTime, st.PenalizedAfterLastRelease, penalizedNow);
            st.ReleaseStart = now;
            st.ReleaseStartPenalized = penalizedNow;
            st.HitThisRelease = false;

            if (r.CostOnMiss)
            {
                st.LandedSerial = st.ReleaseSerial; // a mid-swing switch to landed-only must not charge this swing twice
                Charge(st, BlowKind.Melee, now, in r, mounted);
            }
            else
            {
                _stats.ReleasesAwaitingHit++;
            }
            st.ReleaseMixed = (st.SpeedMultiplier < 1f) != penalizedNow;
            st.LastReleaseTime = now;
            st.PenalizedAfterLastRelease = st.SpeedMultiplier < 1f;
        }

        /// <summary>A swing that hit nothing ran its whole animation: its length measures the speed.</summary>
        private void EndRelease(TrackedAgent st, double now)
        {
            if (st.ReleaseStart < 0) return;
            if (!st.HitThisRelease && !st.ReleaseMixed)
                (st.ReleaseStartPenalized ? _stats.SwingExhausted : _stats.SwingFresh).Add(now - st.ReleaseStart);
            st.ReleaseStart = -1;
        }

        private void AddInterval(IntervalStats fresh, IntervalStats exhausted, double seconds, bool penalizedBefore, bool penalizedNow)
        {
            if (penalizedBefore && penalizedNow) exhausted.Add(seconds);
            else if (!penalizedBefore && !penalizedNow) fresh.Add(seconds);
            else _stats.IntervalsMixed++;
        }

        // ------------------------------------------------------------------ charging

        private void Charge(TrackedAgent st, BlowKind kind, double now, in AthleticsRules r, bool mounted)
        {
            var o = AthleticsMath.Charge(st, in r, now);
            if (!o.Charged) return;
            _stats.AddCharge(kind, mounted, o.Before - o.After); // what was really drained (a swing at 0 drains nothing)
            if (TraxLog.VerboseOn) LogBlow(st, kind, in o, mounted);
            RetargetSpeed(st, in r);
            if (o.EnteredExhaustion) OnExhausted(st, now, in r);
        }

        /// <summary>Recompute the fighter's speed multiplier from the live rules; if it moved enough,
        /// take it and mark the fighter for a properties recompute. True when marked.</summary>
        private static bool RetargetSpeed(TrackedAgent st, in AthleticsRules r)
        {
            float desired = AthleticsMath.AttackSpeedMultiplier(in r, st);
            if (!AthleticsMath.SpeedUpdateNeeded(st.SpeedMultiplier, desired)) return false;
            st.SpeedMultiplier = desired;
            st.SpeedDirty = true;
            return true;
        }

        private void OnExhausted(TrackedAgent st, double now, in AthleticsRules r)
        {
            _stats.ExhaustionsEntered++;
            if (_firstExhausted == null)
            {
                _firstExhausted = st;
                _firstAt = now;
                _firstAsked = st.SpeedMultiplier;
                _firstBefore = SpeedPenalty.Snapshot.Take(st.Agent); // not yet recomputed: the fresh values
                if (!st.SpeedDirty)
                {
                    _firstAfterLogged = true;
                    _firstDone = true; // nothing to follow: no penalty was asked
                    TraxLog.Info("speed", "first exhaustion this mission: " + Name(st) + " at " + Sec(now) + " s - no speed change asked (x"
                        + F2(st.SpeedMultiplier) + ", ExhaustedAttackSpeedPercent " + r.ExhaustedAttackSpeedPercent + "); properties " + _firstBefore);
                }
            }
            if (st.Agent.IsMainAgent)
            {
                TraxLog.Limited("athletics", "YOU are exhausted at " + Sec(now) + " s: 0 of " + F0(AthleticsMath.PoolPoints(in r, st))
                    + " after " + st.Blows + " blows this mission - attacks at " + r.ExhaustedAttackSpeedPercent
                    + "% speed until you rest (refill starts " + F1(r.RegenDelaySeconds) + " s after your last blow)", "athletics-player");
            }
            else if (TraxLog.VerboseOn)
            {
                TraxLog.Verbose("athletics", "exhausted: " + Name(st) + " at " + Sec(now) + " s after " + st.Blows + " blows - attacks x"
                    + F2(st.SpeedMultiplier), "athletics-exhaust");
            }
        }

        // ------------------------------------------------------------------ speed

        private void ApplySpeed(TrackedAgent st)
        {
            st.SpeedDirty = false;
            bool first = ReferenceEquals(st, _firstExhausted);
            try
            {
                st.Agent.UpdateAgentProperties();
                _stats.SpeedUpdates++;
            }
            catch (Exception e)
            {
                Failed("speed.update", e);
                return;
            }
            if (first && !_firstAfterLogged)
            {
                _firstAfterLogged = true;
                _firstAfter = SpeedPenalty.Snapshot.Take(st.Agent);
                bool stuck = Close(_firstAfter.Swing, _firstBefore.Swing * _firstAsked) && Close(_firstAfter.Thrust, _firstBefore.Thrust * _firstAsked)
                             && Close(_firstAfter.Reload, _firstBefore.Reload * _firstAsked);
                TraxLog.Info("speed", "first exhaustion this mission: " + Name(st) + " at " + Sec(_firstAt) + " s - attack properties before: "
                    + _firstBefore + " → after UpdateAgentProperties: " + _firstAfter + " (" + _firstAfter.RatioTo(_firstBefore) + ", asked x" + F2(_firstAsked) + ") - "
                    + (stuck ? "the penalty is in the agent's properties" : "the values did NOT take the asked factor - tell Claude"));
            }
            else if (first && _firstRecovering)
            {
                _firstRecovering = false;
                _firstDone = true;
                var restored = SpeedPenalty.Snapshot.Take(st.Agent);
                TraxLog.Info("speed", "first exhausted fighter back to full speed: properties now " + restored + " (" + restored.RatioTo(_firstBefore)
                    + " of the fresh values - x1.00 expected unless his weapon changed)");
            }
            if (TraxLog.VerboseOn)
            {
                TraxLog.Verbose("speed", Name(st) + ": attack speed x" + F2(st.SpeedMultiplier)
                    + (st.SpeedMultiplier < 1f ? " (exhausted)" : " (full)"), "speed-update");
            }
        }

        /// <summary>A settings change (MCM mid-battle, or the file at mission start): the two that
        /// need a rebuild. Everything else is read live where it is used.</summary>
        internal void ApplySettingsChange(TraxSettings settings)
        {
            _seenVersion = settings.Version;
            try
            {
                var r = AthleticsRules.From(settings);
                if (r.Enabled != _seenEnabled)
                {
                    _seenEnabled = r.Enabled;
                    if (!r.Enabled)
                    {
                        int refilled = 0, lifted = 0;
                        for (int i = 0; i < _count; i++)
                        {
                            var st = _dense[i];
                            if (st.Fraction < 1.0 || st.Exhausted) refilled++;
                            st.ResetFull();
                            if (RetargetSpeed(st, in r)) lifted++;
                        }
                        TraxLog.Info("athletics", "AthleticsEnabled switched OFF mid-mission: " + refilled + " fighters back to full, "
                            + lifted + " attack-speed penalties lifted (applied on the next tick)");
                    }
                    else
                    {
                        TraxLog.Info("athletics", "AthleticsEnabled switched ON mid-mission: everyone starts full");
                    }
                }
                if (r.ExhaustedAttackSpeedPercent != _seenSpeedPercent)
                {
                    _seenSpeedPercent = r.ExhaustedAttackSpeedPercent;
                    int changed = 0;
                    for (int i = 0; i < _count; i++)
                        if (RetargetSpeed(_dense[i], in r)) changed++;
                    TraxLog.Info("speed", "ExhaustedAttackSpeedPercent now " + r.ExhaustedAttackSpeedPercent + "%: " + changed
                        + " exhausted fighters get the new speed on the next tick");
                }
            }
            catch (Exception e)
            {
                Failed("athletics.settings", e);
            }
        }

        // ------------------------------------------------------------------ regen

        private void RegenPass(double step, double now, in AthleticsRules r)
        {
            if (!r.Enabled) return;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st.Fraction >= 1.0 && !st.Exhausted) continue; // full: nothing to do, no native call
                try
                {
                    var a = st.Agent;
                    if (!a.IsActive()) continue;
                    float speed = 0f, top = 0f;
                    if (now - st.LastBlowTime > r.RegenDelaySeconds)
                    {
                        var body = a.MountAgent ?? a; // a rider's rest is his horse's pace (RESEARCH §D)
                        speed = body.MovementVelocity.Length;
                        top = body.GetMaximumForwardUnlimitedSpeed();
                        if (top > 0f) _stats.AddEffort(speed / top);
                    }
                    var o = AthleticsMath.Regen(st, in r, now, step, speed, top);
                    if (o.Seconds > 0)
                    {
                        if (o.Moving) _stats.RegenMovingSeconds += o.Seconds;
                        else _stats.RegenStandingSeconds += o.Seconds;
                    }
                    if (o.Recovered) OnRecovered(st, now, in o, in r);
                    if (o.ReachedFull) OnRefilled(st, now, in o, in r);
                }
                catch (Exception e)
                {
                    Failed("athletics.regen", e);
                }
            }
        }

        private void OnRecovered(TrackedAgent st, double now, in RegenOutcome o, in AthleticsRules r)
        {
            _stats.ExhaustionsLeft++;
            if (ReferenceEquals(st, _firstExhausted) && _firstAfterLogged && !_firstDone && !_firstRecovering)
            {
                var still = SpeedPenalty.Snapshot.Take(st.Agent);
                TraxLog.Info("speed", "first exhausted fighter recovers after " + Sec(o.ExhaustedSeconds) + " s: properties just before - "
                    + still + " (" + still.RatioTo(_firstBefore) + " of the fresh values; they stayed penalized: "
                    + (Close(still.Swing, _firstAfter.Swing) ? "yes" : "NO - something recomputed them without us") + ")");
                _firstRecovering = true;
            }
            RetargetSpeed(st, in r);
            if (st.Agent.IsMainAgent)
            {
                TraxLog.Limited("athletics", "YOU recovered at " + Sec(now) + " s: " + F1(AthleticsMath.Points(in r, st)) + " of "
                    + F0(AthleticsMath.PoolPoints(in r, st)) + " after " + Sec(o.ExhaustedSeconds) + " s exhausted - full attack speed again",
                    "athletics-player");
            }
            else if (TraxLog.VerboseOn)
            {
                TraxLog.Verbose("athletics", "recovered: " + Name(st) + " at " + Sec(now) + " s after " + Sec(o.ExhaustedSeconds) + " s exhausted",
                    "athletics-exhaust");
            }
        }

        private void OnRefilled(TrackedAgent st, double now, in RegenOutcome o, in AthleticsRules r)
        {
            _stats.RefillsToFull++;
            bool you = st.Agent.IsMainAgent;
            if (!you && !TraxLog.VerboseOn) return;
            double pool = AthleticsMath.PoolPoints(in r, st);
            string text = " back to full at " + Sec(now) + " s: " + F0(o.EpisodeStartFraction * pool) + " → " + F0(pool) + " in "
                + Sec(o.EpisodeStandingSeconds + o.EpisodeMovingSeconds) + " s of refill (standing " + Sec(o.EpisodeStandingSeconds)
                + " s, moving " + Sec(o.EpisodeMovingSeconds) + " s; empty to full takes " + F0(r.FullRegenSecondsStanding) + " s standing, "
                + F0(r.FullRegenSecondsMoving) + " s moving)";
            if (you) TraxLog.Limited("athletics", "YOU are" + text, "athletics-player");
            else TraxLog.Verbose("athletics", Name(st) + " is" + text, "athletics-regen");
        }

        // ------------------------------------------------------------------ engine events

        /// <summary>Melee collisions (flesh, shield, parry, objects - RESEARCH §B). Couched/braced
        /// hits are charged here; a swing's release is checked here too (the hit can come before the
        /// poll); landed-only mode charges the first hit of each swing on an agent.</summary>
        public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            try
            {
                var st = Get(attacker);
                if (st == null || collisionData.IsHorseCharge) return; // horses (charges) are not tracked; bumps are free
                if (collisionData.IsAlternativeAttack)
                {
                    _stats.KickOrBashHits++; // kicks and shield bashes are free (DESIGN interpretation 8)
                    return;
                }
                var r = Rules;
                double now = Mission.CurrentTime;
                if (!r.Enabled)
                {
                    _stats.AttacksWhileOff++;
                    return;
                }
                bool mounted = attacker.MountAgent != null;
                if (attacker.IsDoingPassiveAttack)
                {
                    if (victim == null) return; // a lance into a wall is not a landed blow
                    if (now - st.LastPassiveCharge < r.BlowTimeSeconds)
                    {
                        _stats.CouchedWithinBlowTime++;
                        return;
                    }
                    st.LastPassiveCharge = now;
                    Charge(st, BlowKind.Couched, now, in r, mounted);
                    return;
                }

                // The hit belongs to a release we counted if we were in one until now (the engine may
                // already have moved the action on to a blocked/parried reaction) or if the action is a
                // release right now (then ObserveAction counts it, if the poll has not yet).
                int action = (int)attacker.GetCurrentActionType(1);
                bool wasInRelease = st.PrevAction == ActionReleaseMelee;
                st.HitThisRelease = true; // before a falling edge ends the swing: a hit swing is no clean miss
                if (action != st.PrevAction) ObserveAction(st, action, now, in r);
                if (action == ActionReleaseMelee) st.HitThisRelease = true; // a release that just started
                bool inRelease = wasInRelease || action == ActionReleaseMelee;
                if (mounted) _stats.MeleeHitsMounted++;
                else _stats.MeleeHitsOnFoot++;
                if (inRelease) _stats.MeleeHitsInRelease++;
                else _stats.AddHitOutsideRelease(action);

                if (!r.CostOnMiss && victim != null)
                {
                    if (inRelease)
                    {
                        if (st.LandedSerial != st.ReleaseSerial)
                        {
                            st.LandedSerial = st.ReleaseSerial;
                            st.LastLandedCharge = now;
                            Charge(st, BlowKind.LandedMelee, now, in r, mounted);
                        }
                    }
                    else if (now - st.LastLandedCharge >= r.BlowTimeSeconds)
                    {
                        st.LastLandedCharge = now;
                        _stats.LandedMeleeByTimeFallback++;
                        Charge(st, BlowKind.LandedMelee, now, in r, mounted);
                    }
                }
            }
            catch (Exception e)
            {
                Failed("athletics.melee-hit", e);
            }
        }

        /// <summary>Every shot and throw, AI and player (RESEARCH §B). Siege engines never come here.</summary>
        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            try
            {
                var st = Get(shooterAgent);
                if (st == null) return;
                var r = Rules;
                double now = Mission.CurrentTime;
                if (!r.Enabled)
                {
                    _stats.AttacksWhileOff++;
                    return;
                }
                if (now - st.LastShotTime < ShotDedupeSeconds)
                {
                    _stats.ExtraProjectiles++;
                    return;
                }
                st.LastShotTime = now;
                _stats.ShotsSeen++;

                bool penalizedNow = st.SpeedMultiplier < 1f;
                if (st.LastShotForInterval >= 0)
                    AddInterval(_stats.RangedFresh, _stats.RangedExhausted, now - st.LastShotForInterval, st.PenalizedAfterLastShot, penalizedNow);

                // Mission.OnAgentShootMissile adds the new missile to MissilesList right before it calls
                // the behaviours (Mission.cs ~4992) - so the last one is this shot's.
                var missiles = Mission.MissilesList;
                if (missiles != null && missiles.Count > 0) st.RememberMissile(missiles[missiles.Count - 1].Index);

                bool mounted = shooterAgent.MountAgent != null;
                if (r.CostOnMiss) Charge(st, BlowKind.Ranged, now, in r, mounted);
                else _stats.ShotsAwaitingHit++;
                st.LastShotForInterval = now;
                st.PenalizedAfterLastShot = st.SpeedMultiplier < 1f;
            }
            catch (Exception e)
            {
                Failed("athletics.shoot", e);
            }
        }

        /// <summary>Landed-only mode: one of the shooter's own recent missiles hit a person, a horse or
        /// a shield.</summary>
        public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            try
            {
                if (victim == null) return;
                var st = Get(attacker);
                if (st == null) return;
                var r = Rules;
                if (!r.Enabled || r.CostOnMiss) return;
                if (!st.TakeMissile(collisionData.AffectorWeaponSlotOrMissileIndex)) return;
                Charge(st, BlowKind.LandedRanged, Mission.CurrentTime, in r, attacker.MountAgent != null);
            }
            catch (Exception e)
            {
                Failed("athletics.missile-hit", e);
            }
        }

        // ------------------------------------------------------------------ for the stat decorator

        /// <summary>The attack-speed multiplier to apply to <paramref name="agent"/> now (1 = none):
        /// the running mission's value for a tracked human, 1 for anyone else, and 1 for everyone
        /// while AthleticsEnabled is off (read live - fail safe). Any thread; reads only.</summary>
        internal static float SpeedMultiplierFor(Agent agent)
        {
            var logic = _current;
            if (logic == null || agent == null) return 1f;
            if (!TraxSettings.Shared.AthleticsEnabled) return 1f;
            var st = logic.Get(agent);
            return st?.SpeedMultiplier ?? 1f;
        }

        internal static void NoteDecoratorScaled() => _current?._stats.AddDecoratorScaled();

        /// <summary>A caught exception: the FIRST per site per mission goes to the log with its stack
        /// (TraxLog.Error, itself rate-limited), the rest are counted for the summary. Never throws.</summary>
        internal static void Failed(string site, Exception e)
        {
            try
            {
                var logic = _current;
                if (logic == null || logic._stats.AddError(site)) TraxLog.Error(site, e);
            }
            catch
            {
                // the fallback must not fail
            }
        }

        // ------------------------------------------------------------------ speeds for step 5c

        /// <summary>Once per mission (deployment finished, else at the summary): every tracked
        /// fighter's walk-speed limit and top speed - on foot the agent's own, riders their
        /// horse's - so step 5c learns the real walk/run ratio. Cheap: one pass, pointer reads.</summary>
        private void SampleSpeeds(string when)
        {
            if (_speedsSampled) return;
            _speedsSampled = true;
            try
            {
                for (int i = 0; i < _count; i++)
                {
                    var a = _dense[i].Agent;
                    if (!a.IsActive()) continue;
                    var mount = a.MountAgent;
                    if (mount == null)
                    {
                        float top = a.GetMaximumForwardUnlimitedSpeed();
                        float walk = a.WalkSpeedCached > 0f ? a.WalkSpeedCached : a.Monster?.WalkingSpeedLimit ?? 0f;
                        if (top > 0f) _stats.FootTop.Add(top);
                        if (walk > 0f) _stats.FootWalk.Add(walk);
                    }
                    else
                    {
                        float top = mount.GetMaximumForwardUnlimitedSpeed();
                        float walk = mount.WalkingSpeedLimitOfMountable;
                        if (top > 0f) _stats.HorseTop.Add(top);
                        if (walk > 0f) _stats.HorseWalk.Add(walk);
                    }
                }
                if (TraxLog.VerboseOn)
                    TraxLog.Verbose("athletics", "speeds sampled at " + when + ": on foot " + _stats.FootTop.Count + ", riders " + _stats.HorseTop.Count, "athletics-speeds");
            }
            catch (Exception e)
            {
                Failed("athletics.speeds", e);
            }
        }

        private static bool Close(float a, float b) => Math.Abs(a - b) <= 0.01f * Math.Max(0.05f, Math.Abs(b));
    }
}
