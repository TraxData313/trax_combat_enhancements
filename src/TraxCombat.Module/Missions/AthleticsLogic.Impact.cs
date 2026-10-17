using System;
using System.Diagnostics;
using System.Threading;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>Step 47: what a spike watch measures (one per watched reaction).</summary>
    internal enum SpikePurpose : byte
    {
        /// <summary>Lever 1 (+ 3): a kick the decorator knocked down - the Fall must show; its fall-and-rise timed (slowed or not).</summary>
        KickFall,

        /// <summary>Lever 3 (+ 1's control): a kick NOT knocked down - the reel timed (slowed or not); a Fall here = vanilla knocked him down.</summary>
        KickReel,

        /// <summary>Lever 2: the attacker's recoil after a block (BlockedMelee / ParriedMelee) - a treated block or a control.</summary>
        Recoil,

        /// <summary>Lever 2: the blocker's guard held after the block.</summary>
        Guard,

        /// <summary>Lever 4: the stagger we started - seen, its length, blocks from inside it.</summary>
        Stagger,

        /// <summary>Lever 6: our own knockdown blow - the Fall must show.</summary>
        OwnFall,
    }

    /// <summary>Step 47: one watched reaction - a struct in the logic's fixed pool (no allocation).</summary>
    internal struct SpikeWatch
    {
        public TrackedAgent St;
        public SpikePurpose Purpose;
        public ReactionWatch W;
        public bool Slow;
        public bool Treated;
    }

    /// <summary>Step 47: an engine call the spike decided in a callback, made from the next tick (never inside the engine's callback).</summary>
    internal struct SpikePending
    {
        public SpikeLever Lever; // ForcedStagger or OwnBlow
        public TrackedAgent Victim;
        public TrackedAgent? Attacker;
        public double At;
    }

    /// <summary>Step 47: a shield whose drop was queued - dropped yet? picked up again after how long?</summary>
    internal struct SpikeShield
    {
        public TrackedAgent St;
        public int Slot;
        public double QueuedAt;
        public double DroppedAt; // NaN = not yet
        public double NextLook;
    }

    /// <summary>
    /// Step 47 - IMPACT I1, THE SPIKE (docs/IMPACT.md §7, AI_NOTES "Step 47"): one Advanced switch <c>ImpactSpike</c> (off - a test tool)
    /// applies fixed loud values to prove each engine lever in a real battle and logs per lever whether it worked. Master switch FIRST, read
    /// live. The levers go through the reusable seams of steps 48-53: the decorators' <see cref="ImpactHooks"/> (this logic is the
    /// <see cref="IKnockDownDecider"/> and the <see cref="IBlockStunDecider"/> while it runs) and <see cref="IImpactBody"/> (the engine calls).
    ///   1 KnockDown - the decorator answers KnockDown for kicks 1, 3 of every 4 (the rotation: <see cref="SpikeMath.Kick"/>); a Fall must show.
    ///   2 Block stun - every block's stun x3 for blockers of an EVEN agent index (odd = the control), the game's cap 0.8 → 2 s; the
    ///     attacker's recoil and the blocker's guard timed, treated vs control. The decorator side runs on WORKER threads - reads only.
    ///   3 Reaction speed - kicks 3, 4 of every 4: the reel / the fall-and-rise at 0.5 the tick it shows (again on a _continue); timed vs 1, 2.
    ///   4 Forced stagger - the first block every 10 s: act_stagger_backward on the blocker from the next tick (never a rider or a horse -
    ///     refused before any engine call); accepted?, seen?, its length, blocks made from inside it.
    ///   5 Shield drop - every kicked man holding a shield: DropItem queued (AddTickAction); dropped?, picked up again after how long?
    ///   6 Own blow - the first block every 20 s (not the one a stagger took): our KnockDown blow (1 damage) on the blocker; a Fall must show.
    /// Each lever behind its own try: an exception disables only that lever for the mission ([error] + "lever N disabled"). Main thread
    /// except <see cref="SetBlockStun"/>. No allocation per tick (fixed pools of structs).
    /// </summary>
    public sealed partial class AthleticsLogic : IKnockDownDecider, IBlockStunDecider
    {
        /// <summary>Lever 2's measurements may take at most this many watch slots (blocks are many, kicks are few and precious).</summary>
        private const int MaxBlockWatches = SpikeMath.MaxWatches / 2;

        private readonly ImpactSpikeStats _spike = new ImpactSpikeStats();
        private readonly SpikeWatch[] _watches = new SpikeWatch[SpikeMath.MaxWatches];
        private readonly SpikePending[] _pending = new SpikePending[SpikeMath.MaxPending];
        private readonly SpikeShield[] _shields = new SpikeShield[SpikeMath.MaxShields];
        private readonly bool[] _spikeFirst = new bool[16];
        private int _watchCount;
        private int _blockWatches;
        private int _pendingCount;
        private int _shieldCount;
        private ActionRead _spikeRead;
        private SpikeRules _seenSpike;
        private bool _spikeOn;
        private bool _spikeStarted;
        private bool _spikeClosed;
        private volatile bool _stunLeverOff;
        private int _mainThreadId = -1;
        private double _spikeLastTick = double.NaN;
        private int _kickSeq;
        private int _staggerAttempts;
        private double _lastStaggerAt = double.NaN;
        private double _lastOwnBlowAt = double.NaN;
        private int _spikeCalls;

        /// <summary>This mission's spike numbers (the offline smoke reads them).</summary>
        internal ImpactSpikeStats SpikeStats => _spike;

        /// <summary>The impact levers' engine side (the smoke swaps in its stand-in).</summary>
        internal IImpactBody ImpactBody { get; set; } = ImpactBodyDefault.Current;

        /// <summary>The spike runs now (as of the last tick).</summary>
        internal bool SpikeOn => _spikeOn;

        /// <summary>Watched reactions / queued engine calls / shields followed now (the smoke).</summary>
        internal int SpikeWatches => _watchCount;
        internal int SpikePendingCount => _pendingCount;
        internal int SpikeShields => _shieldCount;

        // ------------------------------------------------------------------ lifecycle

        private void StartImpact()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _seenSpike = SpikeRules.From(TraxSettings.Shared);
            _spikeStarted = true;
            _spike.Stun.Clear();
            ImpactHooks.KnockDown = this;
            ImpactHooks.BlockStun = this;
            ImpactHooks.PickupLock = this; // step 50: the AI's shield pickup lock
            ImpactHooks.ShrugOff = this; // step 52: tired men always flinch
            if (_seenSpike.Enabled) SpikeSwitchedOn(Mission != null ? Mission.CurrentTime : 0, atStart: true);
            else TraxLog.Info("impact", "mission start: spike " + _seenSpike.Describe() + " - read live");
            StartImpactModel(); // step 48: the model (its own line)
        }

        /// <summary>The logic leaves (OnRemoveBehavior): our deciders out of the decorators, the game's stun cap back - always.</summary>
        private void ReleaseImpactHooks()
        {
            if (ReferenceEquals(ImpactHooks.KnockDown, this)) ImpactHooks.KnockDown = null;
            if (ReferenceEquals(ImpactHooks.BlockStun, this)) ImpactHooks.BlockStun = null;
            if (ReferenceEquals(ImpactHooks.PickupLock, this)) ImpactHooks.PickupLock = null;
            if (ReferenceEquals(ImpactHooks.ShrugOff, this)) ImpactHooks.ShrugOff = null;
            if (_spikeStarted && ImpactHooks.RestoreStunCap()) _spike.CapRestores++;
        }

        private void SpikeSwitchedOn(double now, bool atStart)
        {
            _spikeOn = true;
            _spike.EverOn = true;
            string cap = RaiseCapForSpike();
            TraxLog.Info("impact", (atStart ? "mission start: " : "spike switched ON mid-mission at " + Sec(now) + " s: ") + _seenSpike.Describe() + "; " + cap
                + " - read live (WORKS / FAILS / UNSEEN per lever in the [summary] \"impact spike\" lines; PLAYTEST D16)");
        }

        private string RaiseCapForSpike()
        {
            if (_stunLeverOff || _spike.Disabled(SpikeLever.BlockStun)) return "the game's stun cap untouched (lever 2 disabled)";
            try
            {
                float before = ImpactHooks.RaiseStunCap(SpikeMath.StunCapSeconds);
                float after = ManagedParametersCap();
                if (float.IsNaN(_spike.CapBefore)) _spike.CapBefore = before;
                _spike.CapRaisedTo = after;
                return "the game's stun cap (StunPeriodMax) " + F2(before) + " → " + F2(after) + " s";
            }
            catch (Exception e)
            {
                DisableLever(SpikeLever.BlockStun, "impact.stun-cap", e);
                return "the game's stun cap could not be raised (lever 2 disabled)";
            }
        }

        private static float ManagedParametersCap() => TaleWorlds.Core.ManagedParameters.GetParameter(TaleWorlds.Core.ManagedParametersEnum.StunPeriodMax);

        private void SpikeSwitchedOff(double now, string why)
        {
            _spikeOn = false;
            bool restored = ImpactHooks.RestoreStunCap();
            if (restored) _spike.CapRestores++;
            int pending = _pendingCount, watches = _watchCount, shields = _shieldCount;
            ClearSpike(agentsAlive: true);
            TraxLog.Info("impact", "spike switched OFF (" + why + ") mid-mission at " + Sec(now) + " s: vanilla at once - "
                + (restored ? "the game's stun cap back to " + F2(ManagedParametersCap()) + " s, " : string.Empty)
                + pending + " queued action(s) dropped unplayed, " + watches + " watch(es) and " + shields + " shield(s) no longer followed");
        }

        /// <summary>Every watch, queued call and shield let go - no engine call (a slowed reaction ends by itself).</summary>
        private void ClearSpike(bool agentsAlive)
        {
            for (int i = 0; i < _watchCount; i++) _watches[i] = default;
            _watchCount = 0;
            _blockWatches = 0;
            for (int i = 0; i < _pendingCount; i++) _pending[i] = default;
            _pendingCount = 0;
            for (int i = 0; i < _shieldCount; i++)
            {
                if (!double.IsNaN(_shields[i].DroppedAt)) _spike.StillOnGround++;
                _shields[i] = default;
            }
            _shieldCount = 0;
        }

        /// <summary>The summary's turn (mission end): everything let go, the cap back, the deciders out.</summary>
        private void CloseImpact(bool agentsAlive)
        {
            if (!_spikeStarted || _spikeClosed) return;
            _spikeClosed = true;
            _spikeOn = false;
            ClearSpike(agentsAlive);
            ReleaseImpactHooks();
        }

        internal void WriteImpactSummary()
        {
            if (!_spikeStarted) return;
            TakeStunFailure();
            CloseImpact(agentsAlive: true);
            foreach (var line in _spike.SummaryLines(SpikeRules.From(TraxSettings.Shared))) TraxLog.Info("summary", line);
            WriteImpactModelSummary(); // step 48
            WriteImpactLiveSummary(); // step 50
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick (OnMissionTick, own try): the switch (master switch first) - off = vanilla at once; lever 2's worker-thread
        /// failure picked up; the queued engine calls (≤ <see cref="SpikeMath.MaxActionsPerTick"/>); the watches; the shields.</summary>
        internal void TickImpact(double now)
        {
            if (!_spikeStarted || _spikeClosed) return;
            long start = Stopwatch.GetTimestamp();
            var r = SpikeRules.From(TraxSettings.Shared);
            double dt = double.IsNaN(_spikeLastTick) || now < _spikeLastTick ? 0 : now - _spikeLastTick;
            _spikeLastTick = now;
            if (r.Enabled != _spikeOn)
            {
                _seenSpike = r;
                if (_spike.EverOn) _spike.Switches++;
                if (r.Enabled) SpikeSwitchedOn(now, atStart: false);
                else SpikeSwitchedOff(now, r.OffBecause ?? "off");
            }
            if (_spikeOn) _spike.OnSeconds += dt;
            else if (_spike.EverOn) _spike.OffSeconds += dt;
            TakeStunFailure();
            try
            {
                TickImpactModel(now); // step 48: the model's switch and mode, read live
            }
            catch (Exception e)
            {
                Failed("impact.model-tick", e);
            }
            try
            {
                TickImpactLive(now); // step 50: the live kicks' queued drops, watches, shields
            }
            catch (Exception e)
            {
                Failed("impact.live-tick", e);
            }
            if (!_spikeOn) return;

            _spikeCalls = 0;
            if (_pendingCount > 0) RunPending(now);
            if (_watchCount > 0) TickWatches(now);
            if (_shieldCount > 0) TickShields(now);
            _spike.AddTick((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency, _spikeCalls);
        }

        private void TakeStunFailure()
        {
            var e = ImpactHooks.TakeStunFailure(out _);
            if (e == null) return;
            if (_spikeOn) DisableLever(SpikeLever.BlockStun, "impact.stun", e);
            else DisableImpactLever(ImpactLever.BlockStun, "impact.live-stun", e); // step 51: the live seam's error (the spike off)
        }

        private bool LeverOn(SpikeLever lever) => _spikeOn && !_spike.Disabled(lever);

        /// <summary>The mission's time (the last tick's without a mission - the offline smoke).</summary>
        private double SpikeNow => Mission != null ? Mission.CurrentTime : double.IsNaN(_spikeLastTick) ? 0 : _spikeLastTick;

        /// <summary>A lever's error: logged ONCE with its stack (<see cref="Failed"/>), the lever off for the rest of the mission, the others
        /// run on.</summary>
        private void DisableLever(SpikeLever lever, string site, Exception e)
        {
            Failed(site, e);
            if (!_spike.Disable(lever, site)) return;
            if (lever == SpikeLever.BlockStun)
            {
                _stunLeverOff = true;
                if (ImpactHooks.RestoreStunCap()) _spike.CapRestores++;
            }
            TraxLog.Info("impact", "spike lever " + SpikeMath.Number(lever) + " (" + SpikeMath.Short(lever) + ") disabled for this mission after an error at "
                + site + " - the other levers run on; its verdict will read FAILS (tell Claude: the [error] line above has the stack)");
        }

        // ------------------------------------------------------------------ the model seams (deciders)

        /// <summary>Lever 1 (IKnockDownDecider - the damage decorator, MAIN thread): KnockDown for a landed kick / bash on a man on foot when
        /// the rotation says so (<see cref="SpikeMath.Kick"/> on the NEXT kick's number - OnMeleeHit counts it right after, in the same engine
        /// callback). The game's answer is OR-ed by the hook.</summary>
        bool IKnockDownDecider.KnockDownOnBlow(Agent attacker, Agent victim, in AttackCollisionData collision, in TaleWorlds.MountAndBlade.Blow blow, bool gameSays)
        {
            bool spike = SpikeKnockDown(attacker, victim, in collision);
            _seamShrugOff = (blow.BlowFlag & TaleWorlds.MountAndBlade.BlowFlags.ShrugOff) != 0; // step 52: the game shrugged this blow off (a clean hit's knockdown counts it)
            bool model = ImpactModelOnLandedBlow(attacker, victim, in collision); // step 48: a landed blow is decided here (own try; applies from steps 49 / 51)
            return spike || model;
        }

        /// <summary>Lever 1's answer (step 47).</summary>
        private bool SpikeKnockDown(Agent attacker, Agent victim, in AttackCollisionData collision)
        {
            if (!_spikeStarted || _spikeClosed || _spike.Disabled(SpikeLever.KickKnockDown) || !SpikeRules.From(TraxSettings.Shared).Enabled) return false;
            try
            {
                if (!SpikeMath.KickLanded(collision.IsAlternativeAttack, collision.AttackBlockedWithShield, (int)collision.CollisionResult)) return false;
                return KnockDownForKick(Get(victim), Get(attacker), SpikeNow);
            }
            catch (Exception e)
            {
                DisableLever(SpikeLever.KickKnockDown, "impact.knockdown", e);
                return false;
            }
        }

        /// <summary>Lever 1's decision on tracked men (the smoke drives it too): the next kick's plan; counted, the first in full.</summary>
        internal bool KnockDownForKick(TrackedAgent? victim, TrackedAgent? attacker, double now)
        {
            if (victim == null || ImpactBody.IsMounted(victim)) return false;
            var plan = SpikeMath.Kick(_kickSeq + 1, true, !_spike.Disabled(SpikeLever.SlowReaction));
            if (!plan.KnockDown) return false;
            _spike.KnockDownAsked++;
            if (First(0))
                TraxLog.Info("impact", "spike lever 1: first KnockDown answered at " + Sec(now) + " s - kick #" + plan.Number + " of the battle, by "
                    + (attacker != null ? Name(attacker) : "(someone)") + " on " + Name(victim) + (plan.Slow ? " (its rise to be slowed to 0.5 - lever 3)" : " (its rise untouched - the fall's own length)")
                    + "; a Fall must show within " + F1(SpikeMath.FallAppearSeconds) + " s");
            return true;
        }

        /// <summary>Lever 2 (IBlockStunDecider - the damage decorator, ANY THREAD): x3 both stuns for a blocker of an even agent index (never
        /// above the raised cap), the odd ones counted as the control. Reads only; Interlocked counters; no log line, no engine call.</summary>
        public void SetBlockStun(Agent attacker, Agent defender, CombatCollisionResult result, ref float attackerStun, ref float defenderStun)
        {
            if (!_spikeStarted || _spikeClosed || defender == null) return;
            var s = TraxSettings.Shared;
            if (!s.ModEnabled) return; // the master switch first, read live
            if (!s.ImpactSpike)
            {
                BlockStunLive(defender, s, ref defenderStun); // step 51: the stun by tiredness (blocks live)
                return;
            }
            if (_stunLeverOff) return;
            _spike.Stun.Call(Thread.CurrentThread.ManagedThreadId != _mainThreadId);
            if (!SpikeMath.StunTreated(defender.Index))
            {
                _spike.Stun.AddControl(defenderStun, attackerStun);
                return;
            }
            float cap = ImpactHooks.StunCapRaised ? SpikeMath.StunCapSeconds : ImpactHooks.VanillaStunCap;
            float d0 = defenderStun, a0 = attackerStun;
            defenderStun = SpikeMath.StunAsked(d0, cap);
            attackerStun = SpikeMath.StunAsked(a0, cap);
            _spike.Stun.AddTreated(d0, defenderStun, a0, attackerStun, d0 * SpikeMath.StunFactor > cap || a0 * SpikeMath.StunFactor > cap);
        }

        // ------------------------------------------------------------------ the events (main thread)

        /// <summary>OnMeleeHit's part (own try): the collision → <see cref="ImpactHit"/>. Nothing while the spike is off.</summary>
        private void ImpactHitTaken(Agent? attacker, Agent? victim, in AttackCollisionData cd)
        {
            if (!_spikeOn || victim == null || cd.IsMissile || cd.IsHorseCharge) return;
            var v = Get(victim);
            if (v == null) return;
            var hit = new SpikeHit(
                kick: cd.IsAlternativeAttack,
                bash: cd.IsAlternativeAttack && cd.AffectorWeaponSlotOrMissileIndex >= 0,
                landed: cd.IsAlternativeAttack
                    ? SpikeMath.KickLanded(true, cd.AttackBlockedWithShield, (int)cd.CollisionResult)
                    : !cd.AttackBlockedWithShield && (int)cd.CollisionResult == ImpactActions.ResultStrikeAgent,
                block: BlockMath.KindOf(cd.IsMissile, cd.IsAlternativeAttack, cd.IsHorseCharge, cd.CollidedWithShieldOnBack, cd.AttackBlockedWithShield,
                    cd.CorrectSideShieldBlock, (int)cd.CollisionResult),
                damage: cd.InflictedDamage);
            ImpactHit(Get(attacker), v, in hit, Mission.CurrentTime);
        }

        /// <summary>One melee collision on a tracked man while the spike runs (the smoke drives it with made-up hits).</summary>
        internal void ImpactHit(TrackedAgent? attacker, TrackedAgent victim, in SpikeHit hit, double now)
        {
            if (!_spikeOn) return;
            if (hit.Block != BlockKind.None || hit.Landed) NoteInsideStagger(victim, hit.Block != BlockKind.None);
            if (hit.Kick)
            {
                if (hit.Landed) KickLanded(attacker, victim, in hit, now);
                return;
            }
            if (hit.Block != BlockKind.None) BlockSeen(attacker, victim, now);
        }

        private void KickLanded(TrackedAgent? attacker, TrackedAgent victim, in SpikeHit hit, double now)
        {
            _spike.KicksLanded++;
            if (hit.Bash) _spike.Bashes++;
            int n = ++_kickSeq;
            if (ImpactBody.IsMounted(victim))
            {
                _spike.KicksOnRiders++;
                return;
            }
            var plan = SpikeMath.Kick(n, !_spike.Disabled(SpikeLever.KickKnockDown), !_spike.Disabled(SpikeLever.SlowReaction));
            if (hit.Damage <= 0) _spike.KicksZeroDamage++; // the engine plays no reaction at 0 damage - nothing to watch
            else if (LeverOn(SpikeLever.KickKnockDown) || LeverOn(SpikeLever.SlowReaction))
            {
                if (plan.KnockDown)
                {
                    _spike.KnockDownPlanned++;
                    AddWatch(victim, SpikePurpose.KickFall, WatchKind.Fall, now, SpikeMath.FallAppearSeconds, plan.Slow, false);
                }
                else
                {
                    _spike.ControlKicks++;
                    AddWatch(victim, SpikePurpose.KickReel, WatchKind.Reaction, now, SpikeMath.AppearSeconds, plan.Slow, false);
                }
            }
            if (!LeverOn(SpikeLever.ShieldDrop)) return;
            try
            {
                if (!ImpactBody.TryShieldInHand(victim, out int slot))
                {
                    _spike.KickedWithoutShield++;
                    return;
                }
                _spike.KickedWithShield++;
                if (_shieldCount >= _shields.Length)
                {
                    _spike.WatchesFull++;
                    return;
                }
                if (!ImpactBody.DropShield(victim, slot)) return;
                _spike.DropsQueued++;
                _spike.EngineCalls++;
                _shields[_shieldCount++] = new SpikeShield { St = victim, Slot = slot, QueuedAt = now, DroppedAt = double.NaN, NextLook = now };
                if (First(1))
                    TraxLog.Info("impact", "spike lever 5: first shield drop queued at " + Sec(now) + " s - " + Name(victim) + " kicked by "
                        + (attacker != null ? Name(attacker) : "(someone)") + ", his shield in slot " + slot + " (DropItem through Mission.AddTickAction, vanilla's way)");
            }
            catch (Exception e)
            {
                DisableLever(SpikeLever.ShieldDrop, "impact.shield-drop", e);
            }
        }

        /// <summary>A blocked melee blow: lever 4 (the first block every 10 s → a stagger queued), else lever 6 (every 20 s → our blow
        /// queued), else lever 2's measurement (the attacker's recoil and the blocker's guard, treated or control).</summary>
        private void BlockSeen(TrackedAgent? attacker, TrackedAgent victim, double now)
        {
            bool queued = false;
            try
            {
                if (LeverOn(SpikeLever.ForcedStagger) && SpikeMath.Due(now, _lastStaggerAt, SpikeMath.StaggerEverySeconds))
                {
                    var refusal = ImpactBody.CanForce(victim);
                    if (refusal != ForceRefusal.None) _spike.StaggerRefused[(int)refusal]++;
                    else if (Queue(SpikeLever.ForcedStagger, victim, attacker, now))
                    {
                        _lastStaggerAt = now;
                        queued = true;
                    }
                }
            }
            catch (Exception e)
            {
                DisableLever(SpikeLever.ForcedStagger, "impact.stagger-queue", e);
            }
            if (!queued)
            {
                try
                {
                    if (LeverOn(SpikeLever.OwnBlow) && SpikeMath.Due(now, _lastOwnBlowAt, SpikeMath.OwnBlowEverySeconds))
                    {
                        var refusal = ImpactBody.CanForce(victim);
                        if (refusal != ForceRefusal.None) _spike.OwnBlowRefused[(int)refusal]++;
                        else if (Queue(SpikeLever.OwnBlow, victim, attacker, now))
                        {
                            _lastOwnBlowAt = now;
                            queued = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    DisableLever(SpikeLever.OwnBlow, "impact.own-blow-queue", e);
                }
            }
            if (queued || !LeverOn(SpikeLever.BlockStun)) return; // a man we act on is not a clean stun sample
            _spike.BlocksSeen++;
            bool treated = SpikeMath.StunTreated(victim.AgentIndex);
            if (attacker != null) AddWatch(attacker, SpikePurpose.Recoil, WatchKind.Recoil, now, SpikeMath.AppearSeconds, false, treated);
            AddWatch(victim, SpikePurpose.Guard, WatchKind.Guard, now, 0, false, treated);
        }

        /// <summary>Lever 4's "from inside": a block made (or a hit taken) by a man inside the stagger we started.</summary>
        private void NoteInsideStagger(TrackedAgent man, bool blocked)
        {
            for (int i = 0; i < _watchCount; i++)
            {
                ref var w = ref _watches[i];
                if (w.Purpose != SpikePurpose.Stagger || !ReferenceEquals(w.St, man) || !w.W.Appeared) continue;
                if (blocked) _spike.BlocksFromInside++;
                else _spike.HitsFromInside++;
                return;
            }
        }

        private bool Queue(SpikeLever lever, TrackedAgent victim, TrackedAgent? attacker, double now)
        {
            if (_pendingCount >= _pending.Length)
            {
                _spike.PendingFull++;
                return false;
            }
            _pending[_pendingCount++] = new SpikePending { Lever = lever, Victim = victim, Attacker = attacker, At = now };
            return true;
        }

        private void AddWatch(TrackedAgent st, SpikePurpose purpose, WatchKind kind, double now, double appearWithin, bool slow, bool treated)
        {
            bool block = purpose == SpikePurpose.Recoil || purpose == SpikePurpose.Guard;
            if (_watchCount >= _watches.Length || (block && _blockWatches >= MaxBlockWatches))
            {
                _spike.WatchesFull++;
                return;
            }
            ref var w = ref _watches[_watchCount++];
            w.St = st;
            w.Purpose = purpose;
            w.Slow = slow;
            w.Treated = treated;
            w.W.Begin(kind, now, appearWithin);
            if (block) _blockWatches++;
        }

        private void AddStaggerWatch(TrackedAgent st, double now, int actionIndex)
        {
            if (_watchCount >= _watches.Length)
            {
                _spike.WatchesFull++;
                return;
            }
            ref var w = ref _watches[_watchCount++];
            w.St = st;
            w.Purpose = SpikePurpose.Stagger;
            w.Slow = false;
            w.Treated = true;
            w.W.Begin(WatchKind.Stagger, now, SpikeMath.AppearSeconds, actionIndex);
        }

        private void RemoveWatch(int i)
        {
            var p = _watches[i].Purpose;
            if (p == SpikePurpose.Recoil || p == SpikePurpose.Guard) _blockWatches--;
            _watches[i] = _watches[--_watchCount];
            _watches[_watchCount] = default;
        }

        // ------------------------------------------------------------------ the queued engine calls (levers 4, 6)

        private void RunPending(double now)
        {
            int done = 0;
            for (; done < _pendingCount; done++)
            {
                if (_spikeCalls >= SpikeMath.MaxActionsPerTick)
                {
                    _spike.CallsDeferred += _pendingCount - done;
                    break;
                }
                var p = _pending[done];
                if (p.Victim == null || p.Victim.Removed)
                {
                    _spike.LeftField++;
                    continue;
                }
                if (p.Lever == SpikeLever.ForcedStagger) Stagger(p.Victim, now);
                else OwnBlow(p.Victim, p.Attacker, now);
            }
            // keep what waits, in order
            int left = _pendingCount - done;
            for (int i = 0; i < left; i++) _pending[i] = _pending[done + i];
            for (int i = left; i < _pendingCount; i++) _pending[i] = default;
            _pendingCount = left;
        }

        private void Stagger(TrackedAgent v, double now)
        {
            if (!LeverOn(SpikeLever.ForcedStagger)) return;
            try
            {
                var refusal = ImpactBody.CanForce(v);
                if (refusal != ForceRefusal.None)
                {
                    _spike.StaggerRefused[(int)refusal]++;
                    return;
                }
                int attempt = ++_staggerAttempts;
                bool ignore = SpikeMath.IgnorePriority(attempt);
                int mode = ignore ? 1 : 0;
                _spike.StaggerAsked++;
                _spike.StaggerAskedBy[mode]++;
                _spike.EngineCalls++;
                _spikeCalls++;
                bool ok = ImpactBody.ForceStagger(v, SpikeMath.StaggerFor(), SpikeMath.StaggerSpeed, ignore, out int index);
                if (ok)
                {
                    _spike.StaggerAcceptedBy[mode]++;
                    AddStaggerWatch(v, now, index);
                }
                if (First(2) || (ok && First(3)))
                    TraxLog.Info("impact", "spike lever 4: forced stagger #" + attempt + " at " + Sec(now) + " s on " + Name(v) + " (he just blocked) - act_stagger_backward on channel 0, "
                        + (ignore ? "OVER the engine's priority" : "with the engine's priority") + ": " + (ok ? "ACCEPTED (action " + index + ") - watching it play" : "refused by the engine"));
            }
            catch (Exception e)
            {
                DisableLever(SpikeLever.ForcedStagger, "impact.stagger", e);
            }
        }

        private void OwnBlow(TrackedAgent v, TrackedAgent? attacker, double now)
        {
            if (!LeverOn(SpikeLever.OwnBlow)) return;
            try
            {
                var refusal = ImpactBody.CanForce(v);
                if (refusal != ForceRefusal.None)
                {
                    _spike.OwnBlowRefused[(int)refusal]++;
                    return;
                }
                _spike.EngineCalls++;
                _spikeCalls++;
                bool ok = ImpactBody.OwnKnockdownBlow(v, attacker != null && !attacker.Removed ? attacker : null, SpikeMath.OwnBlowDamage);
                if (!ok)
                {
                    _spike.OwnBlowRefusedCall++;
                    return;
                }
                _spike.OwnBlowAsked++;
                AddWatch(v, SpikePurpose.OwnFall, WatchKind.Fall, now, SpikeMath.FallAppearSeconds, false, true);
                if (First(4))
                    TraxLog.Info("impact", "spike lever 6: first blow of our own at " + Sec(now) + " s on " + Name(v) + " (he just blocked" + (attacker != null ? " " + Name(attacker) : string.Empty)
                        + ") - RegisterBlow, KnockDown | KnockBack, " + SpikeMath.OwnBlowDamage + " damage; a Fall must show within " + F1(SpikeMath.FallAppearSeconds) + " s");
            }
            catch (Exception e)
            {
                DisableLever(SpikeLever.OwnBlow, "impact.own-blow", e);
            }
        }

        /// <summary>OnAgentHit's part: our own blow seen by the behaviours (the static mark) - counted; it is 1 damage, the rest of OnAgentHit
        /// (the health cap) treats it as any hit.</summary>
        private void ImpactAgentHit()
        {
            if (ImpactHooks.InOwnBlow) _spike.OwnBlowHitsSeen++;
        }

        // ------------------------------------------------------------------ the watches

        private void TickWatches(double now)
        {
            for (int i = 0; i < _watchCount; i++)
            {
                ref var w = ref _watches[i];
                var st = w.St;
                if (st == null || st.Removed || !ImpactBody.IsActive(st))
                {
                    _spike.LeftField++;
                    RemoveWatch(i--);
                    continue;
                }
                ImpactBody.Read(st, ref _spikeRead);
                var ev = w.W.Observe(now, in _spikeRead, out double length);
                switch (ev)
                {
                    case WatchEvent.None:
                        break;
                    case WatchEvent.Appeared:
                        Appeared(ref w, now);
                        break;
                    case WatchEvent.NewAction:
                        if (w.Slow && SlowNow(ref w, now)) _spike.SlowReapplied++;
                        break;
                    case WatchEvent.Ended:
                    case WatchEvent.Capped:
                        if (ev == WatchEvent.Capped) _spike.Capped++;
                        Measured(ref w, length, now);
                        RemoveWatch(i--);
                        break;
                    case WatchEvent.NeverAppeared:
                        NeverAppeared(ref w, now);
                        RemoveWatch(i--);
                        break;
                }
            }
        }

        private void Appeared(ref SpikeWatch w, double now)
        {
            switch (w.Purpose)
            {
                case SpikePurpose.KickFall:
                    _spike.FallSeen++;
                    if (First(5))
                        TraxLog.Info("impact", "spike lever 1: first Fall seen after a KnockDown kick at " + Sec(now) + " s - " + Name(w.St) + ", " + F2(now - w.W.Since)
                            + " s after the kick, on channel " + w.W.Channel + " (" + ImpactActions.Name(w.W.FirstType) + ", action " + w.W.Index + ")");
                    break;
                case SpikePurpose.KickReel:
                    _spike.NoteControlType(w.W.FirstType);
                    if (ImpactActions.IsFall(w.W.FirstType)) _spike.ControlFalls++;
                    break;
                case SpikePurpose.Stagger:
                    _spike.StaggerSeen++;
                    break;
                case SpikePurpose.OwnFall:
                    _spike.OwnFallSeen++;
                    if (First(6))
                        TraxLog.Info("impact", "spike lever 6: first Fall seen after our own blow at " + Sec(now) + " s - " + Name(w.St) + ", " + F2(now - w.W.Since) + " s after it");
                    break;
            }
            if (w.Slow) SlowNow(ref w, now);
        }

        /// <summary>Lever 3: the reaction now on the watch's channel at <see cref="SpikeMath.ReactionSpeed"/> (own try - an error disables lever 3
        /// only and lets this reaction play on at its own speed).</summary>
        private bool SlowNow(ref SpikeWatch w, double now)
        {
            if (!LeverOn(SpikeLever.SlowReaction))
            {
                w.Slow = false;
                return false;
            }
            try
            {
                ImpactBody.SlowReaction(w.St, w.W.Channel, SpikeMath.ReactionSpeed);
                _spike.SlowCalls++;
                _spike.EngineCalls++;
                _spikeCalls++;
                if (First(7))
                    TraxLog.Info("impact", "spike lever 3: first reaction slowed at " + Sec(now) + " s - " + Name(w.St) + "'s " + ImpactActions.Name(_spikeRead.Type(w.W.Channel))
                        + " (action " + w.W.Index + ") on channel " + w.W.Channel + " set to speed " + F1(SpikeMath.ReactionSpeed) + " the tick it showed");
                return true;
            }
            catch (Exception e)
            {
                w.Slow = false;
                DisableLever(SpikeLever.SlowReaction, "impact.slow", e);
                return false;
            }
        }

        private void Measured(ref SpikeWatch w, double length, double now)
        {
            switch (w.Purpose)
            {
                case SpikePurpose.KickFall:
                    if (w.Slow) _spike.FallSlowed.Add(length);
                    else _spike.FallControl.Add(length);
                    break;
                case SpikePurpose.KickReel:
                    if (ImpactActions.IsFall(w.W.FirstType)) break; // vanilla knocked him down: not a reel
                    if (w.Slow) _spike.ReelSlowed.Add(length);
                    else _spike.ReelControl.Add(length);
                    if (w.Slow && First(8))
                        TraxLog.Info("impact", "spike lever 3: first slowed reel measured at " + Sec(now) + " s - " + Name(w.St) + "'s " + ImpactActions.Name(w.W.FirstType) + " lasted "
                            + F2(length) + " s at speed " + F1(SpikeMath.ReactionSpeed) + " (untouched reels so far: " + ImpactSpikeStats.Len(in _spike.ReelControl) + ")");
                    break;
                case SpikePurpose.Recoil:
                    if (w.Treated) _spike.RecoilTreated.Add(length);
                    else _spike.RecoilControl.Add(length);
                    break;
                case SpikePurpose.Guard:
                    if (w.Treated) _spike.GuardTreated.Add(length);
                    else _spike.GuardControl.Add(length);
                    break;
                case SpikePurpose.Stagger:
                    _spike.StaggerLength.Add(length);
                    if (First(9))
                        TraxLog.Info("impact", "spike lever 4: first forced stagger played out at " + Sec(now) + " s - " + Name(w.St) + "'s lasted " + F2(length) + " s on channel " + w.W.Channel);
                    break;
                case SpikePurpose.OwnFall:
                    break;
            }
        }

        private void NeverAppeared(ref SpikeWatch w, double now)
        {
            switch (w.Purpose)
            {
                case SpikePurpose.KickFall:
                    _spike.FallNotSeen++;
                    if (First(10))
                        TraxLog.Info("impact", "spike lever 1: a KnockDown kick at " + Sec(w.W.Since) + " s showed NO Fall within " + F1(SpikeMath.FallAppearSeconds) + " s on " + Name(w.St)
                            + " (channel 0 " + ImpactActions.Name(_spikeRead.Type0) + ", channel 1 " + ImpactActions.Name(_spikeRead.Type1) + ")");
                    break;
                case SpikePurpose.KickReel:
                    _spike.ReactionNotSeen++;
                    break;
                case SpikePurpose.Recoil:
                    _spike.RecoilNotSeen++;
                    break;
                case SpikePurpose.Guard:
                    _spike.GuardNotSeen++;
                    break;
                case SpikePurpose.Stagger:
                    _spike.StaggerNotSeen++;
                    break;
                case SpikePurpose.OwnFall:
                    _spike.OwnFallNotSeen++;
                    if (First(11))
                        TraxLog.Info("impact", "spike lever 6: our blow at " + Sec(w.W.Since) + " s showed NO Fall within " + F1(SpikeMath.FallAppearSeconds) + " s on " + Name(w.St)
                            + " (channel 0 " + ImpactActions.Name(_spikeRead.Type0) + ", channel 1 " + ImpactActions.Name(_spikeRead.Type1) + ")");
                    break;
            }
        }

        // ------------------------------------------------------------------ the shields (lever 5)

        private void TickShields(double now)
        {
            if (!LeverOn(SpikeLever.ShieldDrop)) return;
            try
            {
                for (int i = 0; i < _shieldCount; i++)
                {
                    ref var s = ref _shields[i];
                    if (now < s.NextLook) continue;
                    s.NextLook = now + SpikeMath.ShieldLookSeconds;
                    bool gone = s.St == null || s.St.Removed || !ImpactBody.IsActive(s.St);
                    if (double.IsNaN(s.DroppedAt))
                    {
                        if (gone)
                        {
                            _spike.LeftField++;
                            RemoveShield(i--);
                        }
                        else if (!ImpactBody.SlotHoldsShield(s.St!, s.Slot))
                        {
                            s.DroppedAt = now;
                            _spike.Dropped++;
                            if (First(12))
                                TraxLog.Info("impact", "spike lever 5: first shield left the hand at " + Sec(now) + " s - " + Name(s.St!) + ", " + F2(now - s.QueuedAt) + " s after the queued drop");
                        }
                        else if (now - s.QueuedAt >= SpikeMath.DropSeenSeconds)
                        {
                            _spike.DropNotSeen++;
                            RemoveShield(i--);
                        }
                        continue;
                    }
                    if (gone)
                    {
                        _spike.DropperLeftField++;
                        RemoveShield(i--);
                    }
                    else if (ImpactBody.HasAnyShield(s.St!))
                    {
                        _spike.PickedUp++;
                        _spike.PickupSeconds.Add(now - s.DroppedAt);
                        if (First(13))
                            TraxLog.Info("impact", "spike lever 5: first dropped shield picked up again at " + Sec(now) + " s - " + Name(s.St!) + ", " + F1(now - s.DroppedAt) + " s after he dropped it");
                        RemoveShield(i--);
                    }
                    else if (now - s.DroppedAt >= SpikeMath.PickupWindowSeconds)
                    {
                        _spike.StillOnGround++;
                        RemoveShield(i--);
                    }
                }
            }
            catch (Exception e)
            {
                DisableLever(SpikeLever.ShieldDrop, "impact.shield-watch", e);
            }
        }

        private void RemoveShield(int i)
        {
            _shields[i] = _shields[--_shieldCount];
            _shields[_shieldCount] = default;
        }

        /// <summary>He left the field (Forget): out of every watch, queue and shield - no engine call on him.</summary>
        private void ImpactLeftField(TrackedAgent st)
        {
            if (!_spikeStarted) return;
            ImpactLiveLeftField(st); // step 50
            for (int i = 0; i < _watchCount; i++)
            {
                if (!ReferenceEquals(_watches[i].St, st)) continue;
                _spike.LeftField++;
                RemoveWatch(i--);
            }
            for (int i = 0; i < _pendingCount; i++)
                if (ReferenceEquals(_pending[i].Attacker, st)) _pending[i].Attacker = null;
            for (int i = 0; i < _shieldCount; i++)
            {
                if (!ReferenceEquals(_shields[i].St, st)) continue;
                if (double.IsNaN(_shields[i].DroppedAt)) _spike.LeftField++;
                else _spike.DropperLeftField++;
                RemoveShield(i--);
            }
        }

        /// <summary>The first of each of the spike's 16 first-time lines (true once).</summary>
        private bool First(int i)
        {
            if (_spikeFirst[i]) return false;
            _spikeFirst[i] = true;
            return true;
        }
    }
}
