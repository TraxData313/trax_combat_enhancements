using System;
using System.Globalization;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 100d - THE TIMED RIPOSTE'S TWO ANIMATIONS (Toni 2026-11-08: "add an animation where the blocker shield or weapon goes out as the attack
    /// lands making the attacker stagger cause it looks strange now - the blocker just sits while the attacker gets staggered"; and "Yes" to the
    /// attacker's stagger matching the blocked direction; AI_NOTES "Step 100" §4 / "100d - built", DESIGN decision 91 (d)):
    ///   - THE PUSH-OUT (<see cref="MaybePushOut"/>, from 100b's seam <see cref="TimedRiposteAccepted"/> beside the slow motion, its own try): the
    ///     blocker's channel 1 (the upper body - the legs keep the stance) plays his kind's action from Core <see cref="TimedPushOutMath"/>
    ///     (a shield's / weapon's own bash, its windup skipped, sped up; or his guard's own heavy parry), every blocker whose TIMED riposte the
    ///     engine took - you too behind <c>TimedBlockPushOutForYou</c>; never a rider, never a man no longer guarding. For its window his own
    ///     bash collision is NO BLOW (<see cref="IPushOutGuard"/> in the damage decorator: damage ignored, no knock-back / knockdown / dismount,
    ///     shrugged off) and our own systems skip it (OnMeleeHit, the kick-and-bash start: no Athletics cost, no meet read, no impact decision);
    ///     at the window's end his channel 1 is cleared if our push still plays (no stuck guard - a slow motion or a stall only lengthens it in
    ///     real time, the window is battle time like the animation; the mission's end leaves nothing behind - the pool lives in this logic).
    ///   - THE RECOIL (<see cref="TryStartRecoil"/>, from <see cref="RunStagger"/> for a TIMED riposte only): the game's own "blocked" recoil of
    ///     the attacker's swing (Core <see cref="TimedRecoilMath"/> - the direction read at contact, his weapon's usage set, his stance) on
    ///     channel 1 slowed to its share, then (<see cref="TickTimedAnims"/>) the backward stagger for the rest of the asked length - watched and
    ///     measured like before. No match / too short / refused = the old single stagger. An untimed riposte keeps step 56's exactly.
    ///   - THE TEST TOOL (<c>TimedBlockAnimTest</c>, off): its key plays the next candidate on the nearest man (else you) - the push-outs, every
    ///     recoil by direction and variant, the full timed riposte by direction - through the same calls; Left Shift + the key goes back.
    /// Master switch first (Core <see cref="TimedAnimRules"/>); main thread; fixed pools, nothing allocated per riposte or tick but the tool's lines.
    /// </summary>
    public sealed partial class AthleticsLogic : IPushOutGuard
    {
        private struct PushOutEntry
        {
            public TrackedAgent St;
            public double Until;
            public int Index;
        }

        private struct RecoilChain
        {
            public ImpactBlockPending P;
            public double StaggerAt;
            public double StaggerSeconds;
            public bool Test;
        }

        private const int MaxPushOuts = 32;
        private const int MaxRecoilChains = 32;
        private const float TestNearestMetres = 8f;

        private readonly TimedAnimStats _anim = new TimedAnimStats();
        private readonly PushOutEntry[] _pushOuts = new PushOutEntry[MaxPushOuts];
        private readonly RecoilChain[] _recoilChains = new RecoilChain[MaxRecoilChains];
        private int _pushOutCount, _recoilChainCount;
        private bool _animDisabled, _pushFirstLine, _recoilFirstLine, _animTestFirstLine;
        private int _animTestStep = -1;

        /// <summary>Step 100d: the animation calls (the smoke swaps in its stand-in).</summary>
        internal IAnimBody AnimBody { get; set; } = AnimBodyDefault.Current;

        internal TimedAnimStats TimedAnimStats => _anim;

        internal int PushOutsRunning => _pushOutCount;

        internal int RecoilChainsRunning => _recoilChainCount;

        // ------------------------------------------------------------------ the push-out

        /// <summary>From <see cref="TimedRiposteAccepted"/>: the engine took a timed riposte's stagger - the blocker pushes out. Its own try: an
        /// error disables the animations for the battle, never the riposte.</summary>
        private void MaybePushOut(TrackedAgent? blocker, ImpactEvent ev, double score, double now)
        {
            if (_animDisabled || !(score > 0)) return;
            try
            {
                var r = TimedAnimRules.From(TraxSettings.Shared);
                if (!r.PushEnabled) return; // not a candidate - not counted
                var body = AnimBody;
                bool onField = blocker != null && !blocker.Removed && ImpactBody.IsActive(blocker);
                bool yours = onField && IsPlayer(blocker!);
                bool mounted = onField && body.Mounted(blocker!);
                bool busy = onField && (PushOutSlot(blocker!) >= 0 || _pushOutCount >= MaxPushOuts);
                bool guarding = onField && !mounted && !busy && body.Guarding(blocker!);
                var skip = TimedPushOutMath.Decide(in r, score, onField, yours, mounted, guarding, busy);
                if (skip == PushOutSkip.Busy && onField && PushOutSlot(blocker!) < 0) _anim.PoolFull++;
                if (skip != PushOutSkip.Play)
                {
                    _anim.NoteSkip(skip);
                    return;
                }
                var st = blocker!;
                var kind = TimedPushOutMath.KindOf(ev == ImpactEvent.RightBlock, ev == ImpactEvent.RightBlock ? null : body.UsageOf(st));
                if (!PlayPushOut(st, kind, TimedPushOutMath.ActionFor(kind, in r), in r, now, yours, out string played, out float speed, out double seconds))
                    return;
                if (!_pushFirstLine)
                {
                    _pushFirstLine = true;
                    TraxLog.Info("impact", "live: first PUSH-OUT at " + Sec(now) + " s - " + Name(st) + " (" + TimedPushOutMath.KindName(kind) + ", timing " + F2(score) + ") plays "
                        + played + " on his upper body at x" + F2(speed) + " for " + F2(seconds) + " s; his own bash collisions are no blow for "
                        + F2(TimedPushOutMath.GuardWindow(in r, seconds)) + " s; " + r.Describe());
                }
            }
            catch (Exception e)
            {
                AnimFailed("impact.pushout", e);
            }
        }

        /// <summary>The push itself (live and the test tool): the action resolved in his set (his left stance first; "parry_heavy" = his guard's
        /// own heavy parry; a bash missing = that fallback), played on channel 1, his window armed. False = skipped (counted).</summary>
        private bool PlayPushOut(TrackedAgent st, PushOutKind kind, string action, in TimedAnimRules r, double now, bool yours, out string played, out float speed,
            out double seconds)
        {
            var body = AnimBody;
            played = string.Empty;
            speed = 1;
            seconds = 0;
            bool left = body.LeftStance(st);
            bool parryHeavy = action == TimedPushOutMath.ParryHeavy;
            bool fellBack = false, leftUsed = false;
            int index = -1;
            double len = double.NaN;
            bool found = false;
            if (!parryHeavy && !string.IsNullOrEmpty(action))
            {
                if (left && body.Find(st, action + "_left_stance", out index, out len))
                {
                    found = true;
                    leftUsed = true;
                    played = action + "_left_stance";
                }
                else if (body.Find(st, action, out index, out len))
                {
                    found = true;
                    played = action;
                }
                else fellBack = true;
            }
            if (!found && (parryHeavy || fellBack))
            {
                string? heavy = TimedPushOutMath.ParryHeavyOf(body.CurrentName(st, 1));
                if (heavy != null && body.Find(st, heavy, out index, out len))
                {
                    found = true;
                    parryHeavy = true;
                    played = heavy;
                }
                else if (heavy != null && heavy.EndsWith("_left_stance", StringComparison.Ordinal)
                         && body.Find(st, heavy.Substring(0, heavy.Length - "_left_stance".Length), out index, out len))
                {
                    found = true;
                    parryHeavy = true;
                    played = heavy.Substring(0, heavy.Length - "_left_stance".Length);
                }
            }
            if (!found)
            {
                _anim.NoteSkip(PushOutSkip.NoAction);
                return false;
            }
            float start = parryHeavy ? 0f : (float)r.PushStart; // a heavy parry is a short recoil already - played whole, sped up
            speed = TimedPushOutMath.Speed(len, start, r.PushSeconds);
            if (!body.Play(st, 1, index, speed, start, 0.05f, 0.15f))
            {
                _anim.NoteSkip(PushOutSkip.Refused);
                return false;
            }
            seconds = TimedPushOutMath.PlaySeconds(len, start, speed);
            ArmPushOut(st, now + TimedPushOutMath.GuardWindow(in r, seconds), index);
            _anim.NotePush(kind, yours, parryHeavy, leftUsed, fellBack && parryHeavy);
            return true;
        }

        private void ArmPushOut(TrackedAgent st, double until, int index)
        {
            int i = PushOutSlot(st);
            if (i < 0)
            {
                if (_pushOutCount >= MaxPushOuts)
                {
                    _anim.PoolFull++;
                    return;
                }
                i = _pushOutCount++;
            }
            _pushOuts[i] = new PushOutEntry { St = st, Until = until, Index = index };
        }

        private int PushOutSlot(TrackedAgent st)
        {
            for (int i = 0; i < _pushOutCount; i++)
                if (ReferenceEquals(_pushOuts[i].St, st)) return i;
            return -1;
        }

        /// <summary>He is inside his push-out window now (his own bash collisions are no blow).</summary>
        private bool InPushOut(int agentIndex, double now)
        {
            for (int i = 0; i < _pushOutCount; i++)
            {
                ref var e = ref _pushOuts[i];
                if (e.St.AgentIndex == agentIndex && now < e.Until && !e.St.Removed) return true;
            }
            return false;
        }

        /// <summary>The damage decorator's question (main thread, inside the engine's hit callback): a pushing man's own bash?</summary>
        bool IPushOutGuard.PushOutBash(int attackerIndex, bool count)
        {
            if (_pushOutCount == 0) return false;
            bool hit = InPushOut(attackerIndex, SpikeNow);
            if (hit && count) _anim.BashBlowsIgnored++;
            return hit;
        }

        /// <summary>OnMeleeHit's first question: a pushing man's own bash collision - none of our hit systems sees it.</summary>
        private bool PushOutHitSkipped(Agent? attacker, in AttackCollisionData cd)
        {
            if (_pushOutCount == 0 || attacker == null || !cd.IsAlternativeAttack) return false;
            if (!InPushOut(attacker.Index, SpikeNow)) return false;
            _anim.BashHitsSkipped++;
            return true;
        }

        /// <summary>Step 18's kick-and-bash start: a pushing man's bash is not his kick (no cost, no swing spent, no meet read).</summary>
        private bool PushOutStartSkipped(TrackedAgent st, double now)
        {
            if (_pushOutCount == 0 || !InPushOut(st.AgentIndex, now)) return false;
            _anim.BashStartsSkipped++;
            return true;
        }

        // ------------------------------------------------------------------ the recoil

        /// <summary>
        /// From <see cref="RunStagger"/> for a TIMED riposte (the attacker <c>p.St</c> already passed CanForce): the direction-matched recoil
        /// first and the backward stagger queued for the rest. True = it played (the riposte accepted - the seam called, the line shown); false
        /// = the old single stagger plays (the reason counted).
        /// </summary>
        private bool TryStartRecoil(in ImpactBlockPending p, double now, ref int calls)
        {
            if (_animDisabled) return false;
            try
            {
                var r = TimedAnimRules.From(TraxSettings.Shared);
                if (!r.RecoilEnabled)
                {
                    _anim.NoteFallback(RecoilFallback.Off);
                    return false;
                }
                var st = p.St;
                int dir = !double.IsNaN(st.RecoilDirectionAt) && now - st.RecoilDirectionAt <= TimedRecoilMath.DirectionMaxAge + 1e-9 ? st.RecoilDirection : -1;
                var fallback = PlanRecoil(st, dir, r.RecoilVariant, p.Asked, in r, out int index, out var plan, out string name, out bool inPlace);
                if (fallback != RecoilFallback.None)
                {
                    _anim.NoteFallback(fallback);
                    return false;
                }
                if (_recoilChainCount >= MaxRecoilChains)
                {
                    _anim.RecoilChainFull++;
                    return false;
                }
                calls++;
                if (!StartRecoil(st, index, plan.RecoilSpeed, inPlace))
                {
                    _anim.NoteFallback(RecoilFallback.Refused);
                    return false;
                }
                _anim.NoteRecoil(dir, plan.RecoilSeconds, plan.StaggerSeconds, inPlace);
                _recoilChains[_recoilChainCount++] = new RecoilChain { P = p, StaggerAt = now + plan.RecoilSeconds, StaggerSeconds = plan.StaggerSeconds };
                _blocks.StaggerAcceptedWithPriority++;
                _blocks.NoteRiposteApplied(p.AttackerBand);
                TimedRiposteAccepted(p.St, p.Other, p.Timed, p.Event, now); // step 100b: THE SEAM - the engine took it (the recoil is the riposte's start)
                ImpactMessage(ImpactMessageKind.Riposte, p.Other != null && !p.Other.Removed ? p.Other : null, p.St);
                if (!_recoilFirstLine)
                {
                    _recoilFirstLine = true;
                    TraxLog.Info("impact", "live: first DIRECTION-MATCHED RECOIL at " + Sec(now) + " s - " + Name(st) + "'s " + TimedRecoilMath.DirectionName(dir) + " met by "
                        + (p.Other != null ? Name(p.Other) : "(someone)") + "'s timed " + ImpactMath.EventName(p.Event) + ": " + name + (inPlace ? " (the engine's own, slowed in place)" : string.Empty)
                        + " at x" + F2(plan.RecoilSpeed) + " for " + F2(plan.RecoilSeconds) + " s, then act_stagger_backward for " + F2(plan.StaggerSeconds) + " s (" + F2(p.Asked) + " s asked)");
                }
                return true;
            }
            catch (Exception e)
            {
                AnimFailed("impact.recoil", e);
                return false;
            }
        }

        /// <summary>The recoil's choice (live and the tool): his class and stance, the first name of Core's list his set has, the split.</summary>
        private RecoilFallback PlanRecoil(TrackedAgent st, int dir, int variant, double total, in TimedAnimRules r, out int index, out RecoilPlan plan, out string name,
            out bool inPlace)
        {
            index = -1;
            plan = default;
            name = string.Empty;
            inPlace = false;
            if (dir < 0) return RecoilFallback.NoDirection;
            var body = AnimBody;
            var cls = TimedRecoilMath.ClassOf(body.UsageOf(st), body.ShieldInHand(st));
            var names = TimedRecoilMath.Names(cls, dir, variant, body.LeftStance(st));
            if (names.Length == 0) return RecoilFallback.NoClass;
            double len = double.NaN;
            foreach (var n in names)
            {
                if (!body.Find(st, n, out index, out len)) continue;
                name = n;
                break;
            }
            if (name.Length == 0) return RecoilFallback.NoAction;
            plan = TimedRecoilMath.Plan(total, len, in r);
            if (!plan.Use) return RecoilFallback.TooShort;
            inPlace = body.CurrentIndex(st, 1) == index;
            return RecoilFallback.None;
        }

        /// <summary>The engine already plays this very recoil (its own, from the block) = slowed in place; else played from its start.</summary>
        private bool StartRecoil(TrackedAgent st, int index, float speed, bool inPlace)
        {
            if (inPlace)
            {
                AnimBody.SetSpeed(st, 1, speed);
                return true;
            }
            return AnimBody.Play(st, 1, index, speed, 0f, 0.05f, 0.2f);
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick (from <see cref="TickImpact"/>, its own try): the recoils' backward staggers due, the push-out windows' ends (the
        /// guard checked back), the test tool's key.</summary>
        internal void TickTimedAnims(double now)
        {
            if (_recoilChainCount > 0) TickRecoilChains(now);
            if (_pushOutCount > 0) TickPushOuts(now);
            TickAnimTest(now);
        }

        private void TickRecoilChains(double now)
        {
            bool off = !TraxSettings.Shared.ModEnabled;
            int keep = 0;
            Exception? error = null;
            for (int i = 0; i < _recoilChainCount; i++)
            {
                var c = _recoilChains[i];
                var st = c.P.St;
                if (!off && now < c.StaggerAt - 1e-9)
                {
                    _recoilChains[keep++] = c;
                    continue;
                }
                try
                {
                    if (off || _live.Disabled(ImpactLever.ForcedStagger))
                    {
                        if (!c.Test) FixState(ImpactBlockAction.Riposte, st, now); // the master switch: vanilla at once - no stagger after the recoil
                        continue;
                    }
                    if (st.Removed || !ImpactBody.IsActive(st))
                    {
                        if (!c.Test) _anim.RecoilLeftField++;
                        continue;
                    }
                    if (ImpactBody.CanForce(st) != ForceRefusal.None)
                    {
                        if (!c.Test)
                        {
                            _anim.RecoilStaggerRefused++;
                            FixState(ImpactBlockAction.Riposte, st, now);
                        }
                        continue;
                    }
                    float speed = ImpactBlockMath.StaggerSpeed(c.StaggerSeconds, _staggerNatural.Seconds);
                    bool ok = ImpactBody.ForceStagger(st, StaggerKind.Backward, speed, false, out int index)
                              || ImpactBody.ForceStagger(st, StaggerKind.Backward, speed, true, out index);
                    if (c.Test) continue;
                    if (!ok)
                    {
                        _anim.RecoilStaggerRefused++;
                        FixState(ImpactBlockAction.Riposte, st, now);
                        continue;
                    }
                    _anim.RecoilStaggerPlayed++;
                    AddLiveWatch(st, ImpactWatchPurpose.Riposte, WatchKind.Stagger, now, ImpactBlockMath.StaggerAppearSeconds, 1,
                        expectIndex: index, ev: c.P.Event, asked: c.StaggerSeconds, speed: speed, timed: c.P.Timed);
                }
                catch (Exception e)
                {
                    error ??= e;
                    if (!c.Test) FixState(ImpactBlockAction.Riposte, st, now);
                }
            }
            for (int i = keep; i < _recoilChainCount; i++) _recoilChains[i] = default;
            _recoilChainCount = keep;
            if (error != null) DisableImpactLever(ImpactLever.ForcedStagger, "impact.recoil-stagger", error);
        }

        private void TickPushOuts(double now)
        {
            int keep = 0;
            for (int i = 0; i < _pushOutCount; i++)
            {
                var e = _pushOuts[i];
                if (now < e.Until && !e.St.Removed)
                {
                    _pushOuts[keep++] = e;
                    continue;
                }
                try
                {
                    if (!e.St.Removed && ImpactBody.IsActive(e.St) && AnimBody.CurrentIndex(e.St, 1) == e.Index)
                    {
                        AnimBody.ClearUpper(e.St); // our push still on his upper body at the window's end - cleared, his guard comes back
                        _anim.Unstuck++;
                    }
                    else _anim.GuardsBack++;
                }
                catch (Exception ex)
                {
                    AnimFailed("impact.pushout-end", ex);
                }
            }
            for (int i = keep; i < _pushOutCount; i++) _pushOuts[i] = default;
            _pushOutCount = keep;
        }

        // ------------------------------------------------------------------ the test tool

        private const int TestPushCount = 6, TestRecoilCount = 16, TestRiposteCount = 4;

        /// <summary>The test tool: the key plays the next candidate (Left Shift: the one before) on the nearest man within 8 m, else on you.</summary>
        private void TickAnimTest(double now)
        {
            var r = TimedAnimRules.From(TraxSettings.Shared);
            if (!r.TestOn || _animDisabled) return;
            var body = AnimBody;
            if (!body.KeyPressed(r.TestKey, out bool back)) return;
            try
            {
                var you = PlayerRecord();
                if (you == null || you.Removed) return;
                var near = body.Nearest(you.Agent, TestNearestMetres);
                var st = (near != null ? Get(near) : null) ?? you;
                if (st.Removed || !ImpactBody.IsActive(st)) return;
                int count = TestPushCount + TestRecoilCount + TestRiposteCount;
                _animTestStep = back ? (_animTestStep <= 0 ? count - 1 : _animTestStep - 1) : (_animTestStep + 1) % count;
                int k = _animTestStep;
                string who = Name(st) + (ReferenceEquals(st, you) ? " (you)" : string.Empty);
                string text;
                if (k < TestPushCount)
                {
                    string action = TimedPushOutMath.Candidates[k];
                    bool shield = body.ShieldInHand(st);
                    var kind = TimedPushOutMath.KindOf(shield, body.UsageOf(st));
                    int skippedBefore = _anim.PushSkipped[(int)PushOutSkip.NoAction];
                    bool ok = PlayPushOut(st, kind, action, in r, now, IsPlayer(st), out string played, out float speed, out double seconds);
                    if (!ok && _anim.PushSkipped[(int)PushOutSkip.NoAction] > skippedBefore) _anim.TestMissing++;
                    text = "push-out " + action + " (" + TimedPushOutMath.KindName(kind) + "): " + (ok ? played + " x" + F2(speed) + " for " + F2(seconds) + " s"
                        : "not played (not in his action set / refused)");
                }
                else if (k < TestPushCount + TestRecoilCount)
                {
                    int dir = (k - TestPushCount) / 4, variant = (k - TestPushCount) % 4;
                    var cls = TimedRecoilMath.ClassOf(body.UsageOf(st), body.ShieldInHand(st));
                    var names = TimedRecoilMath.Names(cls, dir, variant, body.LeftStance(st));
                    string name = names.Length > 0 ? names[0] : "(none for " + TimedRecoilMath.ClassName(cls) + ")";
                    if (names.Length > 0 && body.Find(st, name, out int index, out double len))
                    {
                        double want = Math.Max(TimedRecoilMath.MinRecoilSeconds, r.RecoilSeconds);
                        float speed = (float)Math.Max(TimedRecoilMath.MinSpeed, Math.Min(TimedRecoilMath.MaxSpeed, (double.IsNaN(len) || len <= 0 ? 1 : len) / want));
                        bool ok = body.Play(st, 1, index, speed, 0f, 0.05f, 0.2f);
                        text = "recoil " + TimedRecoilMath.DirectionName(dir) + ", " + TimedRecoilMath.VariantName(variant) + ": " + name + (ok ? " x" + F2(speed) + " (" + F2(len) + " s own length)" : " - refused");
                    }
                    else
                    {
                        _anim.TestMissing++;
                        text = "recoil " + TimedRecoilMath.DirectionName(dir) + ", " + TimedRecoilMath.VariantName(variant) + ": " + name + " - not in his action set (the riposte would fall back)";
                    }
                }
                else
                {
                    int dir = k - TestPushCount - TestRecoilCount;
                    double total = TimedBlockRules.From(TraxSettings.Shared).StaggerMax;
                    var fb = PlanRecoil(st, dir, r.RecoilVariant, total, in r, out int index, out var plan, out string name, out bool inPlace);
                    if (fb == RecoilFallback.None && _recoilChainCount < MaxRecoilChains && ImpactBody.CanForce(st) == ForceRefusal.None && StartRecoil(st, index, plan.RecoilSpeed, inPlace))
                    {
                        _recoilChains[_recoilChainCount++] = new RecoilChain
                        {
                            P = new ImpactBlockPending { Action = ImpactBlockAction.Riposte, St = st, Event = ImpactEvent.RightBlock, At = now, Asked = total, Timed = 1 },
                            StaggerAt = now + plan.RecoilSeconds, StaggerSeconds = plan.StaggerSeconds, Test = true,
                        };
                        text = "timed riposte, " + TimedRecoilMath.DirectionName(dir) + ": " + name + " x" + F2(plan.RecoilSpeed) + " for " + F2(plan.RecoilSeconds)
                               + " s, then the backward stagger " + F2(plan.StaggerSeconds) + " s (" + F2(total) + " s in all)";
                    }
                    else text = "timed riposte, " + TimedRecoilMath.DirectionName(dir) + ": the single backward stagger (" + (fb == RecoilFallback.None ? "he cannot be staggered now" : TimedRecoilMath.FallbackName(fb)) + ")";
                }
                _anim.TestPlays++;
                string line = "[anim test " + (k + 1).ToString(CultureInfo.InvariantCulture) + "/" + count.ToString(CultureInfo.InvariantCulture) + "] " + who + " - " + text;
                body.Say(line);
                if (!_animTestFirstLine)
                {
                    _animTestFirstLine = true;
                    TraxLog.Info("impact", "anim test tool ON (TimedBlockAnimTest): " + r.TestKey + " = the next candidate, Left Shift + " + r.TestKey + " = the one before; "
                        + count.ToString(CultureInfo.InvariantCulture) + " candidates (6 push-outs, 16 recoils = 4 directions x 4 variants, 4 full timed ripostes)");
                }
                TraxLog.Info("impact", "anim test: " + line);
            }
            catch (Exception e)
            {
                AnimFailed("impact.anim-test", e);
            }
        }

        // ------------------------------------------------------------------ the rest

        private void AnimFailed(string site, Exception e)
        {
            _animDisabled = true;
            Failed(site, e);
            TraxLog.Info("impact", "the timed riposte's animations are OFF for this battle after an error (" + site + ") - the ripostes play the single stagger");
        }

        /// <summary>The summary's turn (after the slow motion's line).</summary>
        private void WriteTimedAnimSummary()
        {
            foreach (var line in _anim.SummaryLines(TimedAnimRules.From(TraxSettings.Shared))) TraxLog.Info("summary", line);
        }
    }
}
