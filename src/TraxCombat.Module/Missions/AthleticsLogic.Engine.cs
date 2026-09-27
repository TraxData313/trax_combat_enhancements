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
    /// The Athletics engine (DESIGN §2, Athletics v2 since step 5c) - per-fighter state, blow
    /// detection, the health cap, regen by effort, the speed curves and hot swap. The rules
    /// themselves are Core's pure <see cref="AthleticsMath"/>; this file feeds them from the game
    /// (RESEARCH §B-§F):
    ///
    /// STATE: one <see cref="TrackedAgent"/> per human agent, built at <c>OnAgentBuild</c> (+ a sweep
    /// on the first tick for anyone spawned before us), in an array by <c>Agent.Index</c>
    /// (reference-checked: indices are reused) and a dense array for the loops (swap-removal at
    /// <c>OnAgentRemoved</c>). The Athletics SKILL (heroes' real one - <c>Character.GetSkillValue</c>),
    /// hero / party-leader flags cached at spawn; the pool, every multiplier and cost are computed
    /// live from them. Athletics is a FRACTION of the full pool, so a pool-setting change keeps
    /// everyone's share and a blow's point cost lands as cost ÷ pool.
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
    /// HEALTH CAP: the health left (managed <c>Health ÷ HealthLimit</c>) is read at every hit on a
    /// fighter (<c>OnAgentHit</c>) and every regen step; while HealthCapsAthletics is on it pulls
    /// the fraction down at once and regen stops at it.
    ///
    /// REGEN: every 0.1 s (engine plumbing - the integration is exact, the refill curve of step 14
    /// included; only the effort sample and "left 0" are that coarse), only for fighters below their
    /// top (full, or the health cap): effort = speed ÷ CURRENT top speed (the horse's for a rider).
    ///
    /// SPEED: three multipliers per fighter from f (attack, run on foot, his horse), re-targeted on
    /// a charge, a refill step, a wound and a settings change, but applied only when one moves by
    /// <see cref="AthleticsMath.SpeedUpdateStep"/> (or onto an end point) - then the fighter (and/or
    /// his horse) is marked and the tick loop calls <c>UpdateAgentProperties()</c>, at most
    /// <see cref="MaxRecomputesPerTick"/> a tick, never from inside an engine hit callback;
    /// <see cref="TraxAgentStatModel"/> applies the multipliers on that and every later recompute.
    ///
    /// HOT SWAP: every rule reads <see cref="TraxSettings.Shared"/> at use; the tick compares the
    /// settings version and, on a change, re-targets everyone's speeds, logs pool / speed changes,
    /// or puts everyone back to full and lifts every penalty (Athletics or the whole mod off).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>Regen step (engine plumbing, see the class doc).</summary>
        private const float RegenStepSeconds = 0.1f;

        /// <summary>Two shot events closer than this are one release of a multi-projectile weapon
        /// (RESEARCH §B; engine plumbing).</summary>
        private const double ShotDedupeSeconds = 0.1;

        /// <summary>
        /// At most this many <c>UpdateAgentProperties()</c> per tick (fighters and horses together);
        /// the rest wait for the next tick (a fighter stays marked). Normal play never reaches it
        /// (a few per tick); it spreads a burst - a settings change re-targeting 1000 fighters at once -
        /// over ~20 frames instead of one long frame. Engine plumbing (step 5c).
        /// </summary>
        internal const int MaxRecomputesPerTick = 50;

        private const int ActionReleaseMelee = (int)Agent.ActionCodeType.ReleaseMelee;
        private const int ActionReleaseRanged = (int)Agent.ActionCodeType.ReleaseRanged;
        private const int ActionReleaseThrowing = (int)Agent.ActionCodeType.ReleaseThrowing;
        private const int ActionKick = (int)Agent.ActionCodeType.Kick;
        private const int ActionWeaponBash = (int)Agent.ActionCodeType.WeaponBash;

        private static AthleticsLogic? _current;

        private TrackedAgent?[] _byIndex = new TrackedAgent?[512];
        private TrackedAgent[] _dense = new TrackedAgent[512];
        private TrackedAgent?[] _mountOwner = new TrackedAgent?[256];
        private int _count;
        private int _swept;
        private readonly System.Collections.Generic.List<TrackedAgent> _heroes = new System.Collections.Generic.List<TrackedAgent>();
        private readonly System.Collections.Generic.List<Agent> _horsesToRelease = new System.Collections.Generic.List<Agent>();
        private readonly AthleticsStats _stats = new AthleticsStats();

        /// <summary>This mission's numbers (the offline smoke reads them).</summary>
        internal AthleticsStats Stats => _stats;

        private int _seenVersion = -1;
        private bool _seenEnabled;
        private string? _lastOffBecause;
        private int _seenSpeedPercent;
        private float _seenRunFloor;
        private float _seenMountFloor;
        private int _seenPeakPercent;
        private int _seenPoolFloor;
        private float _seenPoolPerSkill;
        private double _regenAccum;
        private bool _speedsSampled;

        private Agent? _playerAgent;
        private TrackedAgent? _player;
        private bool _playerPoolLogged;

        // The once-per-mission proof that the penalty reaches the agent's properties.
        private TrackedAgent? _firstExhausted;
        private SpeedPenalty.Snapshot _firstBefore;
        private SpeedPenalty.Snapshot _firstAfter;
        private double _firstAt;
        private float _firstAsked;
        private float _firstAskedRun;
        private bool _firstAfterLogged;
        private bool _firstRecovering;
        private bool _firstDone;
        private bool _firstHorseLogged;

        /// <summary>The Athletics logic of the mission running now (null between missions) - the stat
        /// decorator, the damage decorator and the HUD read through it.</summary>
        internal static AthleticsLogic? Current => _current;

        private static AthleticsRules Rules => AthleticsRules.From(TraxSettings.Shared);

        // ------------------------------------------------------------------ lifecycle

        private void StartAthletics()
        {
            _current = this;
            var r = Rules;
            _seenVersion = TraxSettings.Shared.Version;
            _seenEnabled = r.Enabled;
            _lastOffBecause = r.OffBecause;
            RememberSpeedSettings(in r);
            _seenPoolFloor = r.PoolFloor;
            _seenPoolPerSkill = r.PoolPerSkill;
            TraxLog.Info("athletics", "mission start: " + AthleticsStats.DescribeRules(in r) + " - read live");
            TraxLog.Info("athletics", Campaign.Current != null
                ? "party-leader rule: campaign - the hero who leads the fighter's own party (you for yours)"
                : "party-leader rule: no campaign (custom battle) - the side's general, or every hero of a side without one");
            var top = MissionGameModels.Current?.AgentStatCalculateModel;
            TraxLog.Info("speed", top is TraxAgentStatModel ours
                ? "stat model on top in this mission: ours, over " + ours.BaseModelName + " - the attack-speed, run-speed and horse-speed penalties are applied on every recompute"
                : "WARNING: the stat model on top in this mission is " + (top?.GetType().FullName ?? "(none)")
                  + ", not ours - the speed penalties will NOT apply (another mod registered after us?)");
            StartStepBacks();
            StartAttackRate();
        }

        private void StopAthletics()
        {
            if (ReferenceEquals(_current, this)) _current = null;
        }

        /// <summary>Every tick: this mission is the one running (Mission.Current) but not the running
        /// logic - another mission started on top of it and has ended since (its teardown cleared the
        /// slot). Take it back, or the decorators and the HUD would see no Athletics here any more
        /// (review 10a R19; managed reads only).</summary>
        private void ReclaimCurrent()
        {
            if (!ReferenceEquals(_current, this) && Mission != null && ReferenceEquals(Mission.Current, Mission)) _current = this;
        }

        /// <summary>First tick: pick up any human agent spawned before we were attached.</summary>
        private void SweepAgents()
        {
            int before = _count;
            foreach (var a in Mission.Agents)
            {
                try
                {
                    if (a == null || !a.IsHuman || !a.IsActive()) continue;
                    if (Get(a) == null) Track(a);
                }
                catch (Exception e)
                {
                    Failed("athletics.sweep", e); // one bad agent must not cost everyone after him (review 10a R18)
                }
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

        /// <summary>The rider whose Athletics slows this horse now (the mount table, managed only), or null.</summary>
        private TrackedAgent? MountOwner(Agent? horse)
        {
            if (horse == null) return null;
            int i = horse.Index;
            if (i < 0 || i >= _mountOwner.Length) return null;
            var owner = _mountOwner[i];
            return owner != null && ReferenceEquals(owner.SlowedMount, horse) ? owner : null;
        }

        private void RegisterMount(Agent horse, TrackedAgent owner)
        {
            int i = horse.Index;
            if (i < 0) return;
            if (i >= _mountOwner.Length)
            {
                int size = _mountOwner.Length;
                while (size <= i) size *= 2;
                Array.Resize(ref _mountOwner, size);
            }
            _mountOwner[i] = owner;
        }

        private void UnregisterMount(Agent horse, TrackedAgent owner)
        {
            int i = horse.Index;
            if (i >= 0 && i < _mountOwner.Length && ReferenceEquals(_mountOwner[i], owner)) _mountOwner[i] = null;
        }

        /// <summary>Starts tracking a human agent (idempotent): reads his Athletics skill (the pool),
        /// hero / leader flags. Internal: the offline smoke drives it.</summary>
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
                // a stale record: the index was reused without us seeing the old agent leave. Forget it
                // the way a leaving man is forgotten - his step back, pace hold and slowed horse too,
                // without an engine call on him (review 10a R5: only the loop entry went before, so a
                // running step back or hold stayed listed and the tick kept calling the engine on the
                // old agent)
                Forget(old);
            }

            var st = new TrackedAgent(agent);
            ReadSkill(st);
            FlagHeroAndLeader(st);
            _byIndex[i] = st;
            if (_count == _dense.Length) Array.Resize(ref _dense, _dense.Length * 2);
            st.DenseSlot = _count;
            _dense[_count++] = st;
            if (!st.IsHero && !st.IsLeader && TraxLog.VerboseWants("athletics-pool")) LogPoolAtSpawn(st);
            return st;
        }

        /// <summary>The Athletics SKILL from the agent's character - a hero's real skill (CharacterObject
        /// asks his Hero), a troop's from its XML. No character (or no game) → 0, the floor.</summary>
        private void ReadSkill(TrackedAgent st)
        {
            int skill = 0;
            bool known = false;
            try
            {
                var c = st.Agent.Character;
                if (c != null)
                {
                    skill = c.GetSkillValue(DefaultSkills.Athletics);
                    known = true;
                }
            }
            catch (Exception e)
            {
                Failed("athletics.skill", e);
            }
            st.AthleticsSkill = Math.Max(0, skill);
            st.SkillKnown = known;
            _stats.AddFighter(st.AthleticsSkill, known);
        }

        private void Untrack(Agent agent)
        {
            var st = Get(agent);
            if (st == null) return;
            Forget(st);
        }

        /// <summary>He left the field (or his record went stale): out of the loop, his horse released
        /// from the tick, his step back and pace hold ended - no engine call on him. Idempotent.</summary>
        private void Forget(TrackedAgent st)
        {
            if (st.Removed) return;
            var agent = st.Agent;
            if (ReferenceEquals(st, _firstExhausted) && !_firstDone && st.Exhausted)
            {
                _firstDone = true;
                TraxLog.Info("speed", "first exhausted fighter (" + Name(st) + ") left the field still exhausted after "
                    + Sec(SafeNow() - _firstAt) + " s - properties then: " + SpeedPenalty.Snapshot.Take(agent)
                    + " (" + SpeedPenalty.Snapshot.Take(agent).RatioTo(FirstFresh) + " of his fresh values)");
            }
            if (st.SlowedMount != null)
            {
                // his horse runs on at its own speed: out of the table now (the decorator reads 1 for
                // it from here on), recomputed by the next tick - never inside this engine callback
                var horse = st.SlowedMount;
                st.SlowedMount = null;
                UnregisterMount(horse, st);
                _horsesToRelease.Add(horse);
            }
            StepBackLeftField(st);
            PaceLeftField(st);
            PlayerTimerLeftField(st);
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
            string pool = " - Athletics skill " + st.AthleticsSkill + (st.SkillKnown ? "" : " (not readable)") + " → pool " + F0(AthleticsMath.PoolPoints(in r, st))
                          + " (full strength down to " + F0(AthleticsMath.PoolPoints(in r, st) * r.PeakFraction) + ")";
            if (st.IsLeader)
            {
                string name = st.HeroName + (a.IsMainAgent ? " (you)" : string.Empty);
                _stats.LeaderNames.Add(name);
                _stats.LeaderSkills.Add(new System.Collections.Generic.KeyValuePair<string, int>(name, st.AthleticsSkill));
                TraxLog.Limited("athletics", "party leader: " + st.HeroName + (st.IsHero ? "" : " (not a hero)") + pool
                    + ", pays x" + F2(AthleticsMath.CostMultiplier(in r, st)) + " per blow (" + F1(AthleticsMath.BlowCostPoints(in r, st)) + " now)",
                    "athletics-leader");
            }
            else if (TraxLog.VerboseWants("athletics-hero"))
            {
                TraxLog.Verbose("athletics", "hero: " + st.HeroName + pool + ", pays x" + F2(AthleticsMath.CostMultiplier(in r, st))
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
            TrackPlayer(in r);

            int polled = 0;
            int budget = MaxRecomputesPerTick;
            if (_horsesToRelease.Count > 0) budget = ReleaseHorses(budget);
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
                        if (st.ReadyPolling) PollReady(st, a, now); // step 5e: wind-up vs hold (only in an unfinished ready)
                        _stats.AddPeakTime(AthleticsMath.PeakShare(in r, st), dt);
                    }
                    if (st.SpeedDirty || st.MountDirty)
                    {
                        if (budget > 0)
                        {
                            budget--;
                            ApplySpeed(st);
                        }
                        else
                        {
                            _stats.RecomputesDeferred++;
                        }
                    }
                }
                catch (Exception e)
                {
                    st.SpeedDirty = false;
                    st.MountDirty = false;
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

            // Step 5d: the running step backs and the ones the swings above asked for - every tick,
            // whatever the switches (switching off releases everyone at once).
            try
            {
                TickStepBacks(now);
            }
            catch (Exception e)
            {
                Failed("stepback.tick", e);
            }

            // Step 5e / 13: the AI timers - every tick, whatever the switches (off lifts every hold at once).
            try
            {
                TickPace(now);
            }
            catch (Exception e)
            {
                Failed("rate.pace-tick", e);
            }

            // Step 13: your timer - switched off or not you any more = released at once (the input gate
            // does the frame work; this is the tick's safety net).
            try
            {
                TickPlayerTimer(now);
            }
            catch (Exception e)
            {
                Failed("rate.player-tick", e);
            }

            _stats.AddTick(polled, (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }

        private void TrackPlayer(in AthleticsRules r)
        {
            var main = Mission?.MainAgent;
            if (!ReferenceEquals(main, _playerAgent))
            {
                _playerAgent = main;
                var st = Get(main);
                if (st != null) _player = st;
            }
            if (_player != null && !_playerPoolLogged)
            {
                _playerPoolLogged = true;
                double pool = AthleticsMath.PoolPoints(in r, _player);
                TraxLog.Limited("athletics", "YOU: Athletics skill " + _player.AthleticsSkill + " → pool " + F0(pool) + " (the skill x" + F2(r.PoolPerSkill)
                    + ", at least " + r.PoolFloor + "); full strength down to " + F0(pool * r.PeakFraction) + " (" + r.PeakPercent + "%); a blow costs you "
                    + F1(AthleticsMath.BlowCostPoints(in r, _player)) + " - about " + BlowsAtFullStrength(in r, _player) + " blows at full strength, "
                    + BlowsToEmpty(in r, _player) + " to empty", "athletics-player");
            }
        }

        /// <summary>Blows a fresh fighter strikes at full strength (f 1 before the blow).</summary>
        internal static int BlowsAtFullStrength(in AthleticsRules r, Fighter f)
        {
            double pool = AthleticsMath.PoolPoints(in r, f), cost = AthleticsMath.BlowCostPoints(in r, f);
            if (cost <= 0) return int.MaxValue;
            double line = pool * r.PeakFraction;
            return (int)Math.Floor((pool - line) / cost + AthleticsMath.Epsilon) + 1;
        }

        /// <summary>Blows that empty a fresh fighter.</summary>
        internal static int BlowsToEmpty(in AthleticsRules r, Fighter f)
        {
            double pool = AthleticsMath.PoolPoints(in r, f), cost = AthleticsMath.BlowCostPoints(in r, f);
            return cost <= 0 ? int.MaxValue : (int)Math.Ceiling(pool / cost - AthleticsMath.Epsilon);
        }

        /// <summary>The channel-1 action changed (seen by the poll or inside a hit): the attack-rate
        /// phases first (step 5e - filed at the band BEFORE this action's charge; step 13: they build the
        /// attack's D and flag its end), then the falling edge of a swing ends it (the step-back roll),
        /// then an attack that ended here gets its no-attack timer (step 13 - yours or the AI's), the
        /// rising edge into ReleaseMelee is a swing (and your hold may begin at its start); ranged
        /// releases, kicks and bashes are counted for the cross-checks (never charged here).</summary>
        internal void ObserveAction(TrackedAgent st, int action, double now, in AthleticsRules r)
        {
            int prev = st.PrevAction;
            st.PrevAction = action;
            st.AttackEndedNow = false;
            PhasesOnAction(st, action, now, in r);
            if (prev == ActionReleaseMelee) EndRelease(st, now, in r, action);
            if (st.AttackEndedNow)
            {
                st.AttackEndedNow = false;
                AttackEnded(st, now, action, in r);
            }
            switch (action)
            {
                case ActionReleaseMelee:
                    StartRelease(st, now, in r);
                    PlayerAttackStarting(st, now, AttackKind.Melee, in r);
                    break;
                case ActionReleaseRanged:
                case ActionReleaseThrowing:
                    _stats.RangedReleasesPolled++;
                    PlayerAttackStarting(st, now, AttackKind.Ranged, in r);
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
            StepBackSwingStarted(st);

            // the cycle since the last release ran at the multiplier set after that release's charge
            int binNow = AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st));
            if (st.LastReleaseTime >= 0) NoteCycle(st, AttackKind.Melee, binNow, st.BinAfterLastRelease, now - st.LastReleaseTime, st.AskedAfterLastRelease);
            st.SteppedBackThisCycle = false;
            st.ReleaseStart = now;
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
            int binAfter = AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st));
            st.LastReleaseTime = now;
            st.BinAfterLastRelease = binAfter;
            st.AskedAfterLastRelease = st.SpeedMultiplier;
        }

        /// <summary>Every counted swing's end is the step-back roll (step 5d: "after each melee swing");
        /// the no-attack timer's decision follows in ObserveAction (step 13; a step back asked for just now
        /// takes precedence if the tick starts it - a refused one leaves the timer to run).
        /// <paramref name="next"/> = the action he went into (a ready = a chained blow).</summary>
        private void EndRelease(TrackedAgent st, double now, in AthleticsRules r, int next)
        {
            if (st.ReleaseStart < 0) return;
            st.ReleaseStart = -1;
            StepBackSwingEnded(st, now, in r);
        }

        // ------------------------------------------------------------------ charging

        private void Charge(TrackedAgent st, BlowKind kind, double now, in AthleticsRules r, bool mounted)
        {
            var o = AthleticsMath.Charge(st, in r, now);
            if (!o.Charged) return;
            _stats.AddCharge(kind, mounted, o.Before - o.After); // what was really drained (a swing at 0 drains nothing)
            if (TraxLog.VerboseWants("athletics-blow")) LogBlow(st, kind, in o, mounted);
            float prevAttack = st.SpeedMultiplier, prevRun = st.RunSpeedMultiplier;
            RetargetSpeed(st, in r);
            if (o.LeftPeak)
            {
                _stats.PeakLeft++;
                if (st.Agent.IsMainAgent) LogPlayerLeftPeak(st, in r, now);
            }
            if (o.EnteredExhaustion) OnExhausted(st, now, in r, prevAttack, prevRun);
        }

        /// <summary>Recompute the fighter's three multipliers from the live rules; each that moved enough
        /// (<see cref="AthleticsMath.SpeedUpdateNeeded"/>) - or at all, when <paramref name="exact"/>
        /// (a refill that reached its top: the resting speed is set exactly, not left up to one step
        /// behind) - is taken and the fighter (attack / run) or his horse is marked for a properties
        /// recompute. True when anything was marked.</summary>
        private static bool RetargetSpeed(TrackedAgent st, in AthleticsRules r, bool exact = false)
        {
            bool human = false, horse = false;
            float attack = AthleticsMath.AttackSpeedMultiplier(in r, st);
            if (exact ? attack != st.SpeedMultiplier : AthleticsMath.SpeedUpdateNeeded(st.SpeedMultiplier, attack, r.AttackSpeedFloor))
            {
                st.SpeedMultiplier = attack;
                human = true;
            }
            float run = AthleticsMath.RunSpeedMultiplier(in r, st);
            if (exact ? run != st.RunSpeedMultiplier : AthleticsMath.SpeedUpdateNeeded(st.RunSpeedMultiplier, run, r.RunSpeedFloor))
            {
                st.RunSpeedMultiplier = run;
                human = true;
            }
            float mount = AthleticsMath.MountSpeedMultiplier(in r, st);
            if (exact ? mount != st.MountSpeedMultiplier : AthleticsMath.SpeedUpdateNeeded(st.MountSpeedMultiplier, mount, r.MountSpeedFloor))
            {
                st.MountSpeedMultiplier = mount;
                horse = true;
            }
            if (human) st.SpeedDirty = true;
            if (horse) st.MountDirty = true;
            return human || horse;
        }

        /// <param name="prevAttack">The attack multiplier applied until this blow (the properties still
        /// hold it - the recompute comes on the next tick).</param>
        /// <param name="prevRun">Likewise for the run multiplier.</param>
        private void OnExhausted(TrackedAgent st, double now, in AthleticsRules r, float prevAttack, float prevRun)
        {
            _stats.ExhaustionsEntered++;
            if (_firstExhausted == null)
            {
                _firstExhausted = st;
                _firstAt = now;
                _firstAsked = st.SpeedMultiplier;
                _firstAskedRun = st.RunSpeedMultiplier;
                _firstPrevAttack = prevAttack;
                _firstPrevRun = prevRun;
                _firstBefore = SpeedPenalty.Snapshot.Take(st.Agent); // not yet recomputed: the values at prevAttack / prevRun
                if (!st.SpeedDirty)
                {
                    _firstAfterLogged = true;
                    _firstDone = true; // nothing to follow: no new speed was asked
                    TraxLog.Info("speed", "first exhaustion this mission: " + Name(st) + " at " + Sec(now) + " s - no speed change asked (attacks x"
                        + F2(st.SpeedMultiplier) + ", run x" + F2(st.RunSpeedMultiplier) + "); properties " + _firstBefore);
                }
            }
            if (st.Agent.IsMainAgent)
            {
                TraxLog.Limited("athletics", "YOU are exhausted at " + Sec(now) + " s: 0 of " + F0(AthleticsMath.PoolPoints(in r, st))
                    + " after " + st.Blows + " blows this mission - attacks at " + r.ExhaustedAttackSpeedPercent + "% speed, run x" + F2(r.RunSpeedFloor)
                    + ", no damage upside" + (r.DamageBonusFollows ? string.Empty : " (off: full upside)") + " until you rest (refill starts "
                    + F1(r.RegenDelaySeconds) + " s after your last blow)", "athletics-player");
            }
            else if (TraxLog.VerboseWants("athletics-exhaust"))
            {
                TraxLog.Verbose("athletics", "exhausted: " + Name(st) + " at " + Sec(now) + " s after " + st.Blows + " blows - attacks x"
                    + F2(st.SpeedMultiplier) + ", run x" + F2(st.RunSpeedMultiplier), "athletics-exhaust");
            }
        }

        // ------------------------------------------------------------------ speed

        private void ApplySpeed(TrackedAgent st)
        {
            if (st.SpeedDirty) ApplyFighterSpeed(st);
            if (st.MountDirty) ApplyMountSpeed(st);
        }

        private void ApplyFighterSpeed(TrackedAgent st)
        {
            st.SpeedDirty = false;
            bool first = ReferenceEquals(st, _firstExhausted);
            // step 5e: the mission's first slowing - every value a technique touches, before → after
            bool firstSlowed = !_firstSlowedLogged && st.SpeedMultiplier < 1f;
            var slowBefore = firstSlowed ? SpeedPenalty.Snapshot.Take(st.Agent) : default;
            var aiBefore = firstSlowed ? SpeedPenalty.AiSnapshot.Take(st.Agent) : default;
            try
            {
                st.Agent.UpdateAgentProperties();
                _stats.FighterRecomputes++;
            }
            catch (Exception e)
            {
                Failed("speed.update", e);
                return;
            }
            if (firstSlowed)
            {
                _firstSlowedLogged = true;
                LogFirstSlowed(st, in slowBefore, in aiBefore);
            }
            if (first && !_firstAfterLogged)
            {
                _firstAfterLogged = true;
                _firstAfter = SpeedPenalty.Snapshot.Take(st.Agent);
                // The "before" values were taken right after the emptying blow, while the previous
                // multipliers still applied (the curve had already slowed him) - so each value should
                // move by new ÷ old.
                float stepAttack = _firstPrevAttack > 0f ? _firstAsked / _firstPrevAttack : 1f;
                float stepRun = _firstPrevRun > 0f ? _firstAskedRun / _firstPrevRun : 1f;
                bool stuck = Close(_firstAfter.Swing, _firstBefore.Swing * stepAttack) && Close(_firstAfter.Thrust, _firstBefore.Thrust * stepAttack)
                             && Close(_firstAfter.Reload, _firstBefore.Reload * stepAttack) && Close(_firstAfter.Run, _firstBefore.Run * stepRun);
                TraxLog.Info("speed", "first exhaustion this mission: " + Name(st) + " at " + Sec(_firstAt) + " s - properties before: "
                    + _firstBefore + " (while attacks x" + F2(_firstPrevAttack) + ", run x" + F2(_firstPrevRun) + " applied) → after UpdateAgentProperties: "
                    + _firstAfter + " (" + _firstAfter.RatioTo(_firstBefore) + "; asked attacks x" + F2(_firstAsked) + " / x" + F2(_firstPrevAttack) + " = x"
                    + F2(stepAttack) + ", run x" + F2(_firstAskedRun) + " / x" + F2(_firstPrevRun) + " = x" + F2(stepRun) + ") - "
                    + (stuck ? "the penalties are in the agent's properties" : "the values did NOT take the asked factors - tell Claude"));
            }
            else if (first && _firstRecovering && st.SpeedMultiplier == 1f && st.RunSpeedMultiplier == 1f)
            {
                _firstRecovering = false;
                _firstDone = true;
                var restored = SpeedPenalty.Snapshot.Take(st.Agent);
                TraxLog.Info("speed", "first exhausted fighter back at full strength: properties now " + restored + " (" + restored.RatioTo(FirstFresh)
                    + " of his fresh values - x1.00 expected unless his weapon or armour changed)");
            }
            if (TraxLog.VerboseWants("speed-update"))
            {
                TraxLog.Verbose("speed", Name(st) + ": attacks x" + F2(st.SpeedMultiplier) + ", run x" + F2(st.RunSpeedMultiplier)
                    + " (f " + F2(AthleticsMath.PeakShare(Rules, st)) + (st.Exhausted ? ", empty" : st.SpeedMultiplier == 1f ? ", full strength" : string.Empty) + ")",
                    "speed-update");
            }
        }

        // The multipliers in effect when the first exhaustion's "before" snapshot was taken (the
        // blow that emptied him had not been recomputed yet).
        private float _firstPrevAttack = 1f;
        private float _firstPrevRun = 1f;

        /// <summary>The first exhausted fighter's properties as they were at full strength: the
        /// "before" snapshot divided by the multipliers that applied to it.</summary>
        private SpeedPenalty.Snapshot FirstFresh => new SpeedPenalty.Snapshot(
            _firstBefore.Swing / Math.Max(0.01f, _firstPrevAttack), _firstBefore.Thrust / Math.Max(0.01f, _firstPrevAttack),
            _firstBefore.Reload / Math.Max(0.01f, _firstPrevAttack), _firstBefore.Run / Math.Max(0.01f, _firstPrevRun));

        /// <summary>Horses whose slowed rider left the field: recompute them (back to their own speed)
        /// from the tick, within the per-tick budget. Returns the budget left.</summary>
        private int ReleaseHorses(int budget)
        {
            while (_horsesToRelease.Count > 0 && budget > 0)
            {
                var horse = _horsesToRelease[_horsesToRelease.Count - 1];
                _horsesToRelease.RemoveAt(_horsesToRelease.Count - 1);
                try
                {
                    if (horse.IsActive() && MountOwner(horse) == null)
                    {
                        horse.UpdateAgentProperties();
                        _stats.HorseRecomputes++;
                        budget--;
                    }
                }
                catch (Exception e)
                {
                    Failed("speed.horse-release", e);
                }
            }
            return budget;
        }

        /// <summary>His horse(s): release the one we slowed if he left it (or its multiplier went back to
        /// 1), register and recompute the one he rides while its multiplier is below 1.</summary>
        private void ApplyMountSpeed(TrackedAgent st)
        {
            st.MountDirty = false;
            try
            {
                var old = st.SlowedMount;
                var current = st.Agent.IsActive() ? st.Agent.MountAgent : null;
                float m = st.MountSpeedMultiplier;
                Agent? next = current != null && m < 1f ? current : null;
                st.SlowedMount = next;
                if (next != null) RegisterMount(next, st);
                if (old != null && !ReferenceEquals(old, next))
                {
                    UnregisterMount(old, st);
                    if (old.IsActive())
                    {
                        old.UpdateAgentProperties();
                        _stats.HorseRecomputes++;
                    }
                }
                if (next != null)
                {
                    float before = next.AgentDrivenProperties?.MountSpeed ?? 0f;
                    next.UpdateAgentProperties();
                    _stats.HorseRecomputes++;
                    if (!_firstHorseLogged)
                    {
                        _firstHorseLogged = true;
                        float after = next.AgentDrivenProperties?.MountSpeed ?? 0f;
                        TraxLog.Info("speed", "first horse slowed this mission: the horse of " + Name(st) + " - MountSpeed " + F3(before) + " → " + F3(after)
                            + " after UpdateAgentProperties (asked x" + F2(m) + " of its fresh speed; MountMinSpeedMultiplier " + F2(TraxSettings.Shared.MountMinSpeedMultiplier) + ")");
                    }
                    if (TraxLog.VerboseWants("speed-update"))
                        TraxLog.Verbose("speed", "horse of " + Name(st) + ": speed x" + F2(m), "speed-update");
                }
            }
            catch (Exception e)
            {
                Failed("speed.horse", e);
            }
        }

        /// <summary>A settings change (MCM mid-battle, or the file at mission start): switching off /
        /// on, the pool settings (logged - the fractions keep everyone's share), and every speed
        /// re-targeted (the budget spreads the recomputes). Everything else is read live.</summary>
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
                            ResetPhases(st); // step 5e: no measured phase or cycle spans the time it is off
                            if (RetargetSpeed(st, in r)) lifted++;
                        }
                        TraxLog.Info("athletics", (r.ModEnabled ? "AthleticsEnabled" : "the whole mod (ModEnabled)")
                            + " switched OFF mid-mission: " + refilled + " fighters back to full, "
                            + lifted + " speed penalties lifted (applied over the next ticks)");
                    }
                    else
                    {
                        for (int i = 0; i < _count; i++) ResetPhases(_dense[i]);
                        // Back on - by either switch - means everyone starts from a full bar (Anton's
                        // master-switch rule: "on" is a fresh start, not a resume of the old state).
                        TraxLog.Info("athletics", (_lastOffBecause == "ModEnabled" ? "the whole mod (ModEnabled)" : "AthleticsEnabled")
                            + " switched ON mid-mission: everyone starts full (a wound's cap applies again at the next refill step)");
                    }
                }
                _lastOffBecause = r.OffBecause; // which switch holds it off now (both may be off)
                NoteRateSettings(settings);     // step 5e: the A/B switches (AI decisions re-applied)

                if (r.PoolFloor != _seenPoolFloor || r.PoolPerSkill != _seenPoolPerSkill)
                {
                    _seenPoolFloor = r.PoolFloor;
                    _seenPoolPerSkill = r.PoolPerSkill;
                    string dist = _stats.Pools(in r, out double min, out double mean, out double max, out int atFloor)
                        ? "pools now min " + F0(min) + " / avg " + F1(mean) + " / max " + F0(max) + ", " + atFloor + " at the floor"
                        : "nobody tracked yet";
                    TraxLog.Info("athletics", "pool settings now: the Athletics skill x" + F2(r.PoolPerSkill) + ", at least " + r.PoolFloor + " - "
                        + dist + "; everyone keeps his share (a fighter at 60% stays at 60%)");
                }

                bool speedSettings = r.ExhaustedAttackSpeedPercent != _seenSpeedPercent || r.MinMoveSpeedMultiplier != _seenRunFloor
                                     || r.MountMinSpeedMultiplier != _seenMountFloor || r.PeakPercent != _seenPeakPercent;
                int changed = 0;
                for (int i = 0; i < _count; i++)
                    if (RetargetSpeed(_dense[i], in r)) changed++;
                if (speedSettings)
                {
                    RememberSpeedSettings(in r);
                    TraxLog.Info("speed", "speed settings now: when empty attacks at " + r.ExhaustedAttackSpeedPercent + "%, run x" + F2(r.RunSpeedFloor)
                        + ", horses x" + F2(r.MountsSlow ? r.MountSpeedFloor : 1f) + "; full strength at " + r.PeakPercent + "% of the pool - "
                        + changed + " fighters get new speeds over the next ticks (at most " + MaxRecomputesPerTick + " recomputes a tick)");
                }
            }
            catch (Exception e)
            {
                Failed("athletics.settings", e);
            }
        }

        private void RememberSpeedSettings(in AthleticsRules r)
        {
            _seenSpeedPercent = r.ExhaustedAttackSpeedPercent;
            _seenRunFloor = r.MinMoveSpeedMultiplier;
            _seenMountFloor = r.MountMinSpeedMultiplier;
            _seenPeakPercent = r.PeakPercent;
        }

        // ------------------------------------------------------------------ health cap

        /// <summary>Health left, 0..1 (managed fields - cheap, and safe on an agent without a native
        /// side). An unknown maximum reads as full health.</summary>
        private static double HealthOf(Agent a)
        {
            float limit = a.HealthLimit;
            if (!(limit > 0f)) return 1.0;
            double h = a.Health / limit;
            return h < 0 ? 0 : h > 1 ? 1 : h;
        }

        /// <summary>Reads the fighter's health and applies the cap (DESIGN §2): a cut is counted,
        /// logged, and re-targets his speeds. Internal: the offline smoke drives it.</summary>
        internal void CheckHealth(TrackedAgent st, in AthleticsRules r, double now)
        {
            double health = HealthOf(st.Agent);
            if (health <= 0) return; // the killing blow: he leaves the field, it is no "cut"
            double cut = AthleticsMath.ApplyHealth(st, in r, health);
            if (cut <= 0) return;
            double pool = AthleticsMath.PoolPoints(in r, st);
            _stats.AddHealthCut(cut * pool);
            RetargetSpeed(st, in r);
            double f = AthleticsMath.PeakShare(in r, st);
            if (st.Agent.IsMainAgent)
            {
                TraxLog.Limited("athletics", "YOU are wounded at " + Sec(now) + " s (" + P0(st.Health) + " health): Athletics capped at "
                    + F1(st.Fraction * pool) + " of " + F0(pool) + " (cut " + F1(cut * pool) + ") - f now " + F2(f)
                    + (st.Health < r.PeakFraction ? "; full strength needs " + F0(pool * r.PeakFraction) + ", out of reach until healed" : string.Empty),
                    "athletics-player");
            }
            else if (TraxLog.VerboseWants("athletics-health"))
            {
                TraxLog.Verbose("athletics", "health cap: " + Name(st) + " at " + P0(st.Health) + " health - Athletics " + F1((st.Fraction + cut) * pool)
                    + " → " + F1(st.Fraction * pool) + " of " + F0(pool) + " (f " + F2(f) + ")", "athletics-health");
            }
        }

        // ------------------------------------------------------------------ regen

        private void RegenPass(double step, double now, in AthleticsRules r)
        {
            if (!r.Enabled) return;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                try
                {
                    CheckHealth(st, in r, now);
                    double top = AthleticsMath.UsableFraction(in r, st);
                    if (st.Fraction >= top - AthleticsMath.Epsilon && !st.Exhausted) continue; // at his top: nothing to do, no native call
                    var a = st.Agent;
                    if (!a.IsActive()) continue;
                    var mount = a.MountAgent;
                    var body = mount ?? a; // a rider's effort is his horse's pace (RESEARCH §D)
                    float speed = body.MovementVelocity.Length;
                    float topSpeed = body.GetMaximumForwardUnlimitedSpeed();
                    SampleRunSpeed(st, mount, speed, topSpeed, in r);

                    var o = AthleticsMath.Regen(st, in r, now, step, speed, topSpeed);
                    if (o.Seconds > 0)
                    {
                        _stats.AddEffort(o.Effort, o.Seconds);
                        if (o.Walking) _stats.RegenWalkSeconds += o.Seconds;
                        else
                        {
                            _stats.RegenFasterSeconds += o.Seconds;
                            _stats.RegenFasterRateSeconds += o.Seconds * o.RateMultiplier;
                        }
                    }
                    if (o.Gained > 0 || o.Recovered) RetargetSpeed(st, in r, exact: o.ReachedTop);
                    if (o.EmptyToPeakSeconds > 0) _stats.AddEmptyToPeak(o.EmptyToPeakSeconds);
                    if (o.EnteredPeak) OnEnteredPeak(st, now, in o, in r);
                    if (o.Recovered) OnRecovered(st, now, in o, in r);
                    if (o.ReachedTop) OnRefilled(st, now, in o, in r);
                }
                catch (Exception e)
                {
                    Failed("athletics.regen", e);
                }
            }
        }

        /// <summary>The run-speed check (step 5c): every regen sample, relative to the fighter's (or his
        /// horse's) own top speed while no penalty applied - refreshed whenever the multiplier is 1.</summary>
        private void SampleRunSpeed(TrackedAgent st, Agent? mount, float speed, float topSpeed, in AthleticsRules r)
        {
            if (!(topSpeed > 0f)) return;
            int bin = AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st));
            if (mount == null)
            {
                if (st.RunSpeedMultiplier == 1f) st.FreshTop = topSpeed;
                if (st.FreshTop > 0f) _stats.FootRun.Add(bin, topSpeed / st.FreshTop, st.RunSpeedMultiplier, speed / st.FreshTop);
            }
            else
            {
                if (st.MountSpeedMultiplier == 1f || !ReferenceEquals(st.FreshMountOf, mount))
                {
                    if (st.MountSpeedMultiplier != 1f) return; // a new horse while slowed: no fresh top to compare with
                    st.FreshMountTop = topSpeed;
                    st.FreshMountOf = mount;
                }
                if (st.FreshMountTop > 0f) _stats.HorseRun.Add(bin, topSpeed / st.FreshMountTop, st.MountSpeedMultiplier, speed / st.FreshMountTop);
            }
        }

        private void OnEnteredPeak(TrackedAgent st, double now, in RegenOutcome o, in AthleticsRules r)
        {
            _stats.PeakEntered++;
            if (st.Agent.IsMainAgent && st.PlayerBelowPeakLogged)
            {
                st.PlayerBelowPeakLogged = false;
                double pool = AthleticsMath.PoolPoints(in r, st);
                string fromEmpty = o.EmptyToPeakSeconds > 0
                    ? " - up from empty in " + Sec(o.EmptyToPeakSeconds) + " s of refill (" + Sec(AthleticsMath.RefillSeconds(in r, 0, r.PeakFraction, 1.0))
                      + " s at a walk or slower; " + AthleticsStats.RefillCurveText(in r) + ")"
                    : string.Empty;
                TraxLog.Limited("athletics", "YOU are back at full strength at " + Sec(now) + " s: " + F1(st.Fraction * pool) + " of " + F0(pool)
                    + " (the line is " + F0(pool * r.PeakFraction) + ")" + fromEmpty, "athletics-player");
            }
        }

        private void LogPlayerLeftPeak(TrackedAgent st, in AthleticsRules r, double now)
        {
            st.PlayerBelowPeakLogged = true;
            double pool = AthleticsMath.PoolPoints(in r, st);
            TraxLog.Limited("athletics", "YOU dropped below full strength at " + Sec(now) + " s: " + F1(st.Fraction * pool) + " of " + F0(pool)
                + " (the line is " + F0(pool * r.PeakFraction) + ") after " + st.Blows + " blows this mission - f " + F2(AthleticsMath.PeakShare(in r, st))
                + ": attacks x" + F2(AthleticsMath.AttackSpeedMultiplier(in r, st)) + ", run x" + F2(AthleticsMath.RunSpeedMultiplier(in r, st))
                + ", damage upside " + P0(AthleticsMath.DamageUpside(in r, st)) + " of the full", "athletics-player");
        }

        private void OnRecovered(TrackedAgent st, double now, in RegenOutcome o, in AthleticsRules r)
        {
            _stats.ExhaustionsLeft++;
            if (ReferenceEquals(st, _firstExhausted) && _firstAfterLogged && !_firstDone && !_firstRecovering)
            {
                var still = SpeedPenalty.Snapshot.Take(st.Agent);
                TraxLog.Info("speed", "first exhausted fighter leaves 0 after " + Sec(o.ExhaustedSeconds) + " s: properties just before - "
                    + still + " (" + still.RatioTo(FirstFresh) + " of his fresh values; they stayed penalized: "
                    + (Close(still.Swing, _firstAfter.Swing) ? "yes" : "NO - something recomputed them without us") + ") - they now climb with his bar");
                _firstRecovering = true;
            }
            if (st.Agent.IsMainAgent)
            {
                TraxLog.Limited("athletics", "YOU are off empty at " + Sec(now) + " s: " + F1(AthleticsMath.Points(in r, st)) + " of "
                    + F0(AthleticsMath.PoolPoints(in r, st)) + " after " + Sec(o.ExhaustedSeconds) + " s at 0 - attacks and run speed now climb with your bar (full at "
                    + F0(AthleticsMath.PoolPoints(in r, st) * r.PeakFraction) + ")", "athletics-player");
            }
            else if (TraxLog.VerboseWants("athletics-exhaust"))
            {
                TraxLog.Verbose("athletics", "off empty: " + Name(st) + " at " + Sec(now) + " s after " + Sec(o.ExhaustedSeconds) + " s at 0",
                    "athletics-exhaust");
            }
        }

        private void OnRefilled(TrackedAgent st, double now, in RegenOutcome o, in AthleticsRules r)
        {
            bool full = o.Top >= 1.0 - AthleticsMath.Epsilon;
            if (full) _stats.RefillsToFull++;
            else _stats.RefillsToHealthCap++;
            bool you = st.Agent.IsMainAgent;
            if (!you && !TraxLog.VerboseWants("athletics-regen")) return;
            double pool = AthleticsMath.PoolPoints(in r, st);
            string text = (full ? " back to full at " : " refilled to the wound's cap (" + P0(o.Top) + ") at ") + Sec(now) + " s: "
                + F0(o.EpisodeStartFraction * pool) + " → " + F0(o.Top * pool) + " of " + F0(pool) + " in " + Sec(o.EpisodeSeconds)
                + " s of refill (at a walk or slower " + Sec(o.EpisodeWalkSeconds) + " s, faster " + Sec(o.EpisodeSeconds - o.EpisodeWalkSeconds)
                + " s; avg rate x" + F2(o.EpisodeSeconds > 0 ? o.EpisodeRateSeconds / o.EpisodeSeconds : 1) + "; empty to full takes "
                + F0(r.FullRegenSecondsStanding) + " s at rest, " + F0(r.FullRegenSecondsStanding / Math.Max(0.01, r.RegenMultiplierAtFullRun)) + " s at a full run; "
                + AthleticsStats.RefillCurveText(in r) + ")";
            if (you) TraxLog.Limited("athletics", "YOU are" + text, "athletics-player");
            else TraxLog.Verbose("athletics", Name(st) + " is" + text, "athletics-regen");
        }

        // ------------------------------------------------------------------ engine events

        /// <summary>A fighter was hit: his health cap applies at once (DESIGN §2).</summary>
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try
            {
                var st = Get(affectedAgent);
                if (st == null) return;
                var r = Rules;
                if (!r.Enabled) return;
                CheckHealth(st, in r, Mission.CurrentTime);
            }
            catch (Exception e)
            {
                Failed("athletics.agent-hit", e);
            }
        }

        /// <summary>Mounting / dismounting: the horse multiplier moves with the rider (recomputed on the next tick).</summary>
        public override void OnAgentMount(Agent agent)
        {
            try
            {
                var st = Get(agent);
                if (st != null && (st.MountSpeedMultiplier < 1f || st.SlowedMount != null)) st.MountDirty = true;
            }
            catch (Exception e)
            {
                Failed("athletics.mount", e);
            }
        }

        public override void OnAgentDismount(Agent agent)
        {
            try
            {
                var st = Get(agent);
                if (st != null && st.SlowedMount != null) st.MountDirty = true;
            }
            catch (Exception e)
            {
                Failed("athletics.mount", e);
            }
        }

        /// <summary>Melee collisions (flesh, shield, parry, objects - RESEARCH §B). Couched/braced
        /// hits are charged here; a swing's release is checked here too (the hit can come before the
        /// poll); landed-only mode charges the first hit of each swing on an agent.</summary>
        public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            try
            {
                StepBackHitTaken(victim, isCanceled, in collisionData); // step 5d: blocked or landed, stepping back or not
            }
            catch (Exception e)
            {
                Failed("stepback.hit", e);
            }
            try
            {
                RateHitTaken(victim, isCanceled, in collisionData); // step 5e: the guard by f (tired men must not block less)
            }
            catch (Exception e)
            {
                Failed("rate.hit", e);
            }
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

                int binNow = AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st));
                if (st.LastShotForInterval >= 0) NoteCycle(st, AttackKind.Ranged, binNow, st.BinAfterLastShot, now - st.LastShotForInterval, st.AskedAfterLastShot);

                // Mission.OnAgentShootMissile adds the new missile to MissilesList right before it calls
                // the behaviours (Mission.cs ~4992) - so the last one is this shot's.
                var missiles = Mission.MissilesList;
                if (missiles != null && missiles.Count > 0) st.RememberMissile(missiles[missiles.Count - 1].Index);

                bool mounted = shooterAgent.MountAgent != null;
                if (r.CostOnMiss) Charge(st, BlowKind.Ranged, now, in r, mounted);
                else _stats.ShotsAwaitingHit++;
                st.LastShotForInterval = now;
                st.BinAfterLastShot = AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st));
                st.AskedAfterLastShot = st.SpeedMultiplier;
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

        /// <summary>
        /// The speed multipliers to apply to <paramref name="agent"/> now (1 = none): a tracked
        /// fighter's attack and run multipliers, or - for a horse a slowed rider rides - that
        /// rider's horse multiplier. False (all 1) for anyone else, with no mission running, and for
        /// everyone while the mod (ModEnabled) or Athletics (AthleticsEnabled) is off (read live -
        /// fail safe). Any thread; reads only, managed only (no native call).
        /// </summary>
        internal static bool SpeedFactorsFor(Agent agent, out float attack, out float run, out float mount)
        {
            attack = run = mount = 1f;
            var logic = _current;
            if (logic == null || agent == null) return false;
            var s = TraxSettings.Shared;
            if (!s.ModEnabled || !s.AthleticsEnabled) return false; // the master switch first (vanilla)
            var st = logic.Get(agent);
            if (st != null)
            {
                attack = st.SpeedMultiplier;
                run = st.RunSpeedMultiplier;
                return attack != 1f || run != 1f;
            }
            var owner = logic.MountOwner(agent);
            if (owner == null) return false;
            mount = owner.MountSpeedMultiplier;
            return mount != 1f;
        }

        internal static void NoteDecoratorScaled(bool attack, bool run, bool mount, bool aiDecisions = false)
        {
            var logic = _current;
            if (logic == null) return;
            logic._stats.AddDecoratorScaled(attack, run, mount);
            if (aiDecisions) logic._rateStats.AddAiScaled();
        }

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

        // ------------------------------------------------------------------ walk vs run speeds

        /// <summary>Once per mission (deployment finished, else at the summary): every tracked
        /// fighter's walk-speed limit and top speed - on foot the agent's own, riders their
        /// horse's - so the real walk/run ratio behind WalkEffortFraction is measured. Cheap: one
        /// pass, pointer reads.</summary>
        private void SampleSpeeds(string when)
        {
            if (_speedsSampled) return;
            _speedsSampled = true;
            try
            {
                for (int i = 0; i < _count; i++)
                {
                    var st = _dense[i];
                    var a = st.Agent;
                    if (!a.IsActive()) continue;
                    var mount = a.MountAgent;
                    if (mount == null)
                    {
                        if (st.RunSpeedMultiplier != 1f) continue; // a tired man's top is not his top
                        float top = a.GetMaximumForwardUnlimitedSpeed();
                        float walk = a.WalkSpeedCached > 0f ? a.WalkSpeedCached : a.Monster?.WalkingSpeedLimit ?? 0f;
                        if (top > 0f) _stats.FootTop.Add(top);
                        if (walk > 0f) _stats.FootWalk.Add(walk);
                    }
                    else
                    {
                        if (st.MountSpeedMultiplier != 1f) continue;
                        float top = mount.GetMaximumForwardUnlimitedSpeed();
                        float walk = mount.WalkingSpeedLimitOfMountable;
                        if (top > 0f) _stats.HorseTop.Add(top);
                        if (walk > 0f) _stats.HorseWalk.Add(walk);
                    }
                }
                if (TraxLog.VerboseWants("athletics-speeds"))
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
