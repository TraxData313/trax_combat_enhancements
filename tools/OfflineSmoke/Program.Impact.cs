using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 47 - IMPACT I1, THE SPIKE (AI_NOTES "Step 47", docs/IMPACT.md §7): the REAL damage decorator (its
    /// KnockDown decision and its block stun, on worker threads too) and the REAL <see cref="AthleticsLogic"/> with a stand-in for the engine
    /// side (<see cref="IImpactBody"/>: the channel reads, the slowed reaction, the forced stagger, the queued drop, our own blow). What runs
    /// for real: the switch with the master switch first, the kick rotation, the stun A/B split and the cap, each lever asked on the right
    /// events, never a stagger on a rider, the watches and their lengths, the shields, one lever disabled after an error (the others on),
    /// the summary's verdicts - and the cost for a thousand men. What only the game can tell: whether the ENGINE follows each lever
    /// (PLAYTEST D16 - that is what the spike is for).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for the impact levers' engine side: each man's two channels by agent index (idle when unset), the
        /// refusals by sets, every call recorded (unless <see cref="Record"/> is off), a throw on demand per lever.</summary>
        private sealed class FakeImpactBody : IImpactBody
        {
            public readonly Dictionary<int, ActionRead> Actions = new Dictionary<int, ActionRead>();
            public readonly HashSet<int> Mounted = new HashSet<int>();
            public readonly HashSet<int> Gone = new HashSet<int>();
            public readonly HashSet<int> Water = new HashSet<int>();
            public readonly Dictionary<int, int> ShieldInHand = new Dictionary<int, int>();
            public readonly HashSet<int> SlotEmptied = new HashSet<int>();
            public readonly HashSet<int> ShieldAgain = new HashSet<int>();
            public readonly List<(int Man, int Channel, float Speed)> Slows = new List<(int, int, float)>();
            public readonly List<(int Man, bool IgnorePriority)> Staggers = new List<(int, bool)>();
            public readonly List<(int Man, float Speed)> StaggerSpeeds = new List<(int, float)>(); // step 51: the speed asked of each accepted-or-not call
            public readonly List<(int Man, int Slot)> Drops = new List<(int, int)>();
            public readonly List<(int Victim, int Attacker, int Damage)> OwnBlows = new List<(int, int, int)>();
            public bool AcceptStagger = true;
            public int StaggerIndex = 4242;
            public bool Record = true;
            public bool ThrowOnSlow, ThrowOnDrop, ThrowOnStagger, ThrowOnOwnBlow;
            public int StaggerCallsOnRefused;
            public int Reads;

            public bool IsActive(TrackedAgent st) => !Gone.Contains(st.AgentIndex);

            public bool IsMounted(TrackedAgent st) => Mounted.Contains(st.AgentIndex);

            public ForceRefusal CanForce(TrackedAgent st)
            {
                int i = st.AgentIndex;
                if (st.Removed || Gone.Contains(i)) return ForceRefusal.Gone;
                if (Mounted.Contains(i)) return ForceRefusal.Mounted;
                if (Water.Contains(i)) return ForceRefusal.Water;
                if (Actions.TryGetValue(i, out var a) && a.Type0 == ImpactActions.Fall) return ForceRefusal.Down;
                return ForceRefusal.None;
            }

            public void Read(TrackedAgent st, ref ActionRead read)
            {
                Reads++;
                read = Actions.TryGetValue(st.AgentIndex, out var a) ? a : new ActionRead(35, 100, 35, 200);
            }

            public void SlowReaction(TrackedAgent st, int channel, float speed)
            {
                if (ThrowOnSlow) throw new InvalidOperationException("smoke: SetCurrentActionSpeed failed");
                if (Record) Slows.Add((st.AgentIndex, channel, speed));
            }

            public bool ForceStagger(TrackedAgent st, StaggerKind kind, float speed, bool ignorePriority, out int actionIndex)
            {
                actionIndex = -1;
                if (ThrowOnStagger) throw new InvalidOperationException("smoke: SetActionChannel failed");
                if (CanForce(st) != ForceRefusal.None)
                {
                    StaggerCallsOnRefused++; // the logic must never get here for a rider / horse
                    return false;
                }
                if (Record)
                {
                    Staggers.Add((st.AgentIndex, ignorePriority));
                    StaggerSpeeds.Add((st.AgentIndex, speed));
                }
                actionIndex = StaggerIndex;
                return AcceptStagger;
            }

            public bool TryShieldInHand(TrackedAgent st, out int slot)
            {
                if (ShieldInHand.TryGetValue(st.AgentIndex, out slot) && !SlotEmptied.Contains(st.AgentIndex)) return true;
                slot = -1;
                return false;
            }

            public bool SlotHoldsShield(TrackedAgent st, int slot) => ShieldInHand.ContainsKey(st.AgentIndex) && !SlotEmptied.Contains(st.AgentIndex);

            public bool HasAnyShield(TrackedAgent st) => ShieldAgain.Contains(st.AgentIndex) || SlotHoldsShield(st, 0);

            public bool DropShield(TrackedAgent st, int slot)
            {
                if (ThrowOnDrop) throw new InvalidOperationException("smoke: AddTickAction failed");
                if (Record) Drops.Add((st.AgentIndex, slot));
                return true;
            }

            public bool OwnKnockdownBlow(TrackedAgent victim, TrackedAgent? attacker, int damage)
            {
                if (ThrowOnOwnBlow) throw new InvalidOperationException("smoke: RegisterBlow failed");
                if (CanForce(victim) != ForceRefusal.None) return false;
                if (Record) OwnBlows.Add((victim.AgentIndex, attacker?.AgentIndex ?? -1, damage));
                return true;
            }

            // step 53: our dismount blow (only on a man in Mounted; a true one takes him off his horse unless KeepRiders holds him), your side
            public readonly List<(int Victim, int Attacker, int Damage)> OwnDismounts = new List<(int, int, int)>();
            public readonly HashSet<int> Yours = new HashSet<int>();
            public bool ThrowOnDismount, AcceptDismount = true, DismountTakesHimOff = true;

            public bool OwnDismountBlow(TrackedAgent victim, TrackedAgent? attacker, int damage)
            {
                if (ThrowOnDismount) throw new InvalidOperationException("smoke: the dismount blow failed");
                int i = victim.AgentIndex;
                if (victim.Removed || Gone.Contains(i) || !Mounted.Contains(i)) return false;
                if (Record) OwnDismounts.Add((i, attacker?.AgentIndex ?? -1, damage));
                if (AcceptDismount && DismountTakesHimOff) Mounted.Remove(i);
                return AcceptDismount;
            }

            public bool OnPlayerSide(TrackedAgent st) => Yours.Contains(st.AgentIndex);
        }

        /// <summary>Step 53: the smoke's on-screen lines - collected, never the game's message feed.</summary>
        private sealed class FakeMessageSink : IImpactMessageSink
        {
            public readonly List<(string Text, string Hex)> Lines = new List<(string, string)>();

            public void Show(string text, string hex) => Lines.Add((text, hex));
        }

        /// <summary>Step 47: every logic built from here on plays the impact levers' engine side through the stand-in (the game's calls the
        /// engine - a native call on a fake agent would crash the process).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeImpactBody()
        {
            Check(ImpactBodyDefault.Current is GameImpactBody, "the default impact body is not the game's: " + ImpactBodyDefault.Current.GetType().Name);
            ImpactBodyDefault.Current = new FakeImpactBody();
            Check(new AthleticsLogic().ImpactBody == ImpactBodyDefault.Current, "a new logic does not start with the default impact body");
            // step 53: the on-screen lines go to the smoke's sink (never the game's message feed)
            Check(ImpactMessageSinkDefault.Current is GameImpactMessageSink, "the default message sink is not the game's: " + ImpactMessageSinkDefault.Current.GetType().Name);
            ImpactMessageSinkDefault.Current = new FakeMessageSink();
            Check(new AthleticsLogic().MessageSink == ImpactMessageSinkDefault.Current, "a new logic does not start with the default message sink");
        }

        /// <summary>The spike on, the mod on, the game's own stun cap (0.8 s - managed_core_parameters.xml; the smoke has no game to load it).
        /// Step 51: the impact MODEL off while the spike's checks run (its live blocks would own the cap the moment the spike goes off) - back
        /// on with the spike off.</summary>
        private static void SpikeDefaults(bool on = true)
        {
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.ImpactSpike, on, SettingSources.File);
            S.Set(SettingsSchema.ImpactEnabled, !on, SettingSources.File);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, 0.8f);
        }

        private static float StunCap => ManagedParameters.GetParameter(ManagedParametersEnum.StunPeriodMax);

        private static TraxDamageModel SpikeDamageModel()
        {
            var model = new TraxDamageModel();
            model.Initialize(new CustomAgentApplyDamageModel());
            return model;
        }

        /// <summary>A kick's collision as the engine builds it: an alternative attack on a man, landed (StrikeAgent) or into the shield.</summary>
        private static AttackCollisionData KickCollision(bool landed = true) =>
            AttackCollisionData.GetAttackCollisionDataForDebugPurpose(!landed, false, true, true, false, false, false, false, false, false, false, false,
                landed ? CombatCollisionResult.StrikeAgent : CombatCollisionResult.Blocked, -1, 0, 2, 0, default(BoneBodyPartType), 0,
                default(Agent.UsageDirection), 0, default(CombatHitResultFlags), 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f,
                Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero);

        private static void SpikeFrames(AthleticsLogic logic, ref double t, double seconds)
        {
            int frames = Math.Max(1, (int)Math.Round(seconds * 60));
            for (int k = 0; k < frames; k++)
            {
                t += 1.0 / 60;
                logic.TickImpact(t);
            }
        }

        private static ActionRead Ch0(int type, int index) => new ActionRead(type, index, 35, 200);

        private static ActionRead Ch1(int type, int index) => new ActionRead(35, 100, type, index);

        private static bool Within(double v, double target, double tol = 0.05) => Math.Abs(v - target) <= tol;

        /// <summary>
        /// The spike through the real decorator and logic: kicks 1-8 rotate KnockDown / reel x untouched / slowed (the falls seen, the
        /// reels and the fall-and-rise timed - x2 at 0.5), a ninth's missing fall, a rider and a 0-damage kick; the shield dropped and picked
        /// up, another never left the hand; the first block → a stagger (accepted, seen, 0.9 s, a block and a hit from inside it), the next →
        /// our own blow (a Fall), a rider's block refused with no engine call, a stagger the engine refuses; the stun x3 for even blockers
        /// through the real decorator on worker threads (0.21 → 0.63, the cap 2 s, an odd blocker's 0.9 clamped to the game's 0.8), the
        /// recoil and guard timed treated vs control; the switch off / on live; a watched man leaving; the summary's verdicts.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactSpikeThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                SpikeDefaults();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                var st = logic.SpikeStats;
                var model = SpikeDamageModel();
                LogHas("[impact] mission start: SPIKE ON (ImpactSpike - a test tool, not gameplay): fixed loud values prove each engine lever - 1) half the landed kicks / bashes get the KnockDown decision");
                LogHas("; the game's stun cap (StunPeriodMax) 0.80 → 2.00 s - read live (WORKS / FAILS / UNSEEN per lever in the [summary] \"impact spike\" lines; PLAYTEST D16)");
                Check(logic.SpikeOn && StunCap == 2f && ImpactHooks.StunCapRaised, "the spike did not start on, or the cap is not 2 s: " + StunCap);
                double t = 15000;
                logic.TickImpact(t);

                var men = new Dictionary<int, TrackedAgent>();
                TrackedAgent Man(int i)
                {
                    if (!men.TryGetValue(i, out var m)) men[i] = m = logic.Track(FakeAgent(i))!;
                    return m;
                }
                var kicker = Man(900);
                bool Kick(TrackedAgent on, int damage = 3)
                {
                    var cd = KickCollision();
                    var blow = new Blow(kicker.AgentIndex);
                    bool down = model.DecideAgentKnockedDownByBlow(kicker.Agent, on.Agent, in cd, null!, in blow);
                    logic.ImpactHit(kicker, on, SpikeHit.LandedKick(damage), t);
                    return down;
                }
                // one reaction played for `seconds` (an index change at `continueAt` - the fall's _continue), then idle again
                void Play(TrackedAgent on, int type, int index, double seconds, double continueAt = -1)
                {
                    SpikeFrames(logic, ref t, 1.0 / 60); // the reaction shows the tick after the blow
                    body.Actions[on.AgentIndex] = Ch0(type, index);
                    if (continueAt > 0)
                    {
                        SpikeFrames(logic, ref t, continueAt);
                        body.Actions[on.AgentIndex] = Ch0(type, index + 1);
                        SpikeFrames(logic, ref t, seconds - continueAt);
                    }
                    else SpikeFrames(logic, ref t, seconds);
                    body.Actions.Remove(on.AgentIndex);
                    SpikeFrames(logic, ref t, 1.0 / 60);
                }

                // ---- a kick into the shield: never asked, never counted
                var cdBlocked = KickCollision(landed: false);
                var b0 = new Blow(kicker.AgentIndex);
                Check(!model.DecideAgentKnockedDownByBlow(kicker.Agent, Man(901).Agent, in cdBlocked, null!, in b0) && st.KnockDownAsked == 0, "a kick into the shield got KnockDown");

                // ---- kicks 1-8: the rotation (lever 1 + lever 3) and the shields (lever 5)
                body.ShieldInHand[901] = 3;
                body.ShieldInHand[902] = 2;
                for (int round = 0; round < 2; round++)
                {
                    var v1 = Man(901 + 4 * round); var v2 = Man(902 + 4 * round); var v3 = Man(903 + 4 * round); var v4 = Man(904 + 4 * round);
                    Check(Kick(v1), "kick " + (1 + 4 * round) + " (KnockDown, untouched) did not get KnockDown");
                    if (round == 0) body.SlotEmptied.Add(901); // the queued drop happened (the engine's tick)
                    Play(v1, ImpactActions.Fall, 900, 1.5);
                    Check(!Kick(v2), "kick " + (2 + 4 * round) + " (the reel, untouched) got KnockDown");
                    Play(v2, ImpactActions.StrikeKnockBack, 700, 0.42);
                    Check(Kick(v3), "kick " + (3 + 4 * round) + " (KnockDown, slowed) did not get KnockDown");
                    Play(v3, ImpactActions.Fall, 910, 3.0, continueAt: 1.4);
                    Check(!Kick(v4), "kick " + (4 + 4 * round) + " (the reel, slowed) got KnockDown");
                    Play(v4, ImpactActions.StrikeKnockBack, 720, 0.84);
                }
                Check(st.KicksLanded == 8 && st.KnockDownAsked == 4 && st.KnockDownPlanned == 4 && st.FallSeen == 4 && st.FallNotSeen == 0 && st.ControlKicks == 4 && st.ControlFalls == 0,
                    "kicks 1-8: landed " + st.KicksLanded + ", asked " + st.KnockDownAsked + ", planned " + st.KnockDownPlanned + ", falls " + st.FallSeen + " / not " + st.FallNotSeen + ", control " + st.ControlKicks);
                Check(st.ControlFirstTypes[ImpactActions.StrikeKnockBack] == 4, "the control kicks' reaction was not StrikeKnockBack x4");
                Check(st.FallControl.Count == 2 && Within(st.FallControl.Mean, 1.5) && st.FallSlowed.Count == 2 && Within(st.FallSlowed.Mean, 3.0),
                    "the fall-and-rise: control " + ImpactSpikeStats.Len(in st.FallControl) + ", slowed " + ImpactSpikeStats.Len(in st.FallSlowed));
                Check(st.ReelControl.Count == 2 && Within(st.ReelControl.Mean, 0.42) && st.ReelSlowed.Count == 2 && Within(st.ReelSlowed.Mean, 0.84),
                    "the reel: control " + ImpactSpikeStats.Len(in st.ReelControl) + ", slowed " + ImpactSpikeStats.Len(in st.ReelSlowed));
                // slowed: kicks 3 / 7 at the fall and again at its _continue, kicks 4 / 8 once - always 0.5, on the channel it showed on
                Check(body.Slows.Count == 6 && st.SlowCalls == 6 && st.SlowReapplied == 2 && body.Slows.TrueForAll(s => s.Speed == 0.5f && s.Channel == 0)
                      && body.Slows.FindAll(s => s.Man == 903).Count == 2 && body.Slows.FindAll(s => s.Man == 904).Count == 1 && body.Slows.FindAll(s => s.Man == 901 || s.Man == 902).Count == 0,
                    "the slow calls: " + body.Slows.Count + " (" + st.SlowCalls + ", again " + st.SlowReapplied + ")");
                LogHas("[impact] spike lever 1: first KnockDown answered at ");
                LogHas(" - kick #1 of the battle, by (agent 900) on (agent 901) (its rise untouched - the fall's own length); a Fall must show within 1.0 s");
                LogHas("[impact] spike lever 1: first Fall seen after a KnockDown kick at ");
                LogHas("[impact] spike lever 3: first reaction slowed at ");
                LogHas("'s Fall (action 910) on channel 0 set to speed 0.5 the tick it showed");
                LogHas("[impact] spike lever 3: first slowed reel measured at ");

                // ---- kick 9 (KnockDown) shows no fall; kick 10 on a rider; kick 11 with 0 damage
                Check(Kick(Man(909)), "kick 9 did not get KnockDown");
                SpikeFrames(logic, ref t, 1.1);
                Check(st.FallNotSeen == 1, "kick 9's missing Fall was not counted");
                LogHas(" s showed NO Fall within 1.0 s on (agent 909) (channel 0 type 35, channel 1 type 35)");
                var rider = Man(950);
                body.Mounted.Add(950);
                int watches = logic.SpikeWatches;
                Check(!Kick(rider) && st.KicksOnRiders == 1 && logic.SpikeWatches == watches, "a kick on a rider got KnockDown or a watch");
                Check(Kick(Man(910), damage: 0) && st.KicksZeroDamage == 1 && logic.SpikeWatches == watches, "a 0-damage kick: asked by the decorator (the engine asks it), but watched");
                Check(st.KnockDownAsked == 6 && st.KnockDownPlanned == 5 && st.Verdict(SpikeLever.KickKnockDown) == SpikeVerdict.Works && st.Verdict(SpikeLever.SlowReaction) == SpikeVerdict.Works,
                    "levers 1 / 3 after 11 kicks: asked " + st.KnockDownAsked + ", planned " + st.KnockDownPlanned + ", verdicts " + st.Verdict(SpikeLever.KickKnockDown) + " / " + st.Verdict(SpikeLever.SlowReaction));

                // ---- lever 5: 901's shield left his hand, 902's never did; 901 picks one up 20 s later
                Check(body.Drops.Count == 2 && body.Drops[0] == (901, 3) && body.Drops[1] == (902, 2) && st.DropsQueued == 2 && st.KickedWithShield == 2 && st.KickedWithoutShield == 8,
                    "the drops queued: " + body.Drops.Count + ", with a shield " + st.KickedWithShield + ", without " + st.KickedWithoutShield);
                Check(st.Dropped == 1 && st.DropNotSeen == 1 && logic.SpikeShields == 1, "the shields: dropped " + st.Dropped + ", not " + st.DropNotSeen + ", followed " + logic.SpikeShields);
                LogHas("[impact] spike lever 5: first shield drop queued at ");
                LogHas(" - (agent 901) kicked by (agent 900), his shield in slot 3 (DropItem through Mission.AddTickAction, vanilla's way)");
                LogHas("[impact] spike lever 5: first shield left the hand at ");

                // ---- the first block → a stagger; the next → our own blow
                var d962 = Man(962); var d964 = Man(964);
                logic.ImpactHit(Man(970), d962, SpikeHit.Blocked(), t);
                logic.ImpactHit(Man(971), d964, SpikeHit.Blocked(BlockKind.WeaponParry), t);
                Check(logic.SpikePendingCount == 2 && body.Staggers.Count == 0, "the stagger and our blow were not queued for the tick (or called inside the callback)");
                SpikeFrames(logic, ref t, 1.0 / 60);
                Check(body.Staggers.Count == 1 && body.Staggers[0] == (962, false) && st.StaggerAsked == 1 && st.StaggerAcceptedBy[0] == 1,
                    "the first stagger: " + body.Staggers.Count + " calls, asked " + st.StaggerAsked);
                Check(body.OwnBlows.Count == 1 && body.OwnBlows[0] == (964, 971, 1) && st.OwnBlowAsked == 1, "our blow: " + body.OwnBlows.Count + " calls");
                LogHas("[impact] spike lever 4: forced stagger #1 at ");
                LogHas(" on (agent 962) (he just blocked) - act_stagger_backward on channel 0, with the engine's priority: ACCEPTED (action 4242) - watching it play");
                LogHas("[impact] spike lever 6: first blow of our own at ");
                LogHas(" on (agent 964) (he just blocked (agent 971)) - RegisterBlow, KnockDown | KnockBack, 1 damage; a Fall must show within 1.0 s");
                body.Actions[962] = Ch0(ImpactActions.StrikeLight, 4242);
                body.Actions[964] = Ch0(ImpactActions.Fall, 950);
                SpikeFrames(logic, ref t, 0.1);
                Check(st.StaggerSeen == 1 && st.OwnFallSeen == 1, "the stagger / our blow's fall not seen");
                LogHas("[impact] spike lever 6: first Fall seen after our own blow at ");
                // from inside the stagger: a block and a hit
                logic.ImpactHit(Man(972), d962, SpikeHit.Blocked(), t);
                logic.ImpactHit(Man(973), d962, new SpikeHit(false, false, true, BlockKind.None, 10), t);
                Check(st.BlocksFromInside == 1 && st.HitsFromInside == 1, "from inside the stagger: blocks " + st.BlocksFromInside + ", hits " + st.HitsFromInside);
                SpikeFrames(logic, ref t, 0.8);
                body.Actions.Remove(962);
                SpikeFrames(logic, ref t, 1.0 / 60);
                Check(st.StaggerLength.Count == 1 && Within(st.StaggerLength.Mean, 0.9), "the stagger's length: " + ImpactSpikeStats.Len(in st.StaggerLength));
                LogHas("[impact] spike lever 4: first forced stagger played out at ");
                body.Actions.Remove(964);
                SpikeFrames(logic, ref t, 1.0);

                // ---- 10 s on: a RIDER's block - refused before any engine call; then a stagger the engine refuses (over its priority)
                SpikeFrames(logic, ref t, 8.5);
                logic.ImpactHit(Man(974), rider, SpikeHit.Blocked(), t);
                Check(st.StaggerRefused[(int)ForceRefusal.Mounted] == 1 && logic.SpikePendingCount == 0, "a rider's block was queued for a stagger");
                logic.ImpactHit(Man(975), Man(966), SpikeHit.Blocked(), t);
                body.AcceptStagger = false;
                SpikeFrames(logic, ref t, 1.0 / 60);
                body.AcceptStagger = true;
                Check(body.Staggers.Count == 2 && body.Staggers[1] == (966, true) && st.StaggerAskedBy[1] == 1 && st.StaggerAcceptedBy[1] == 0 && body.StaggerCallsOnRefused == 0
                      && !body.Staggers.Exists(s => s.Man == 950),
                    "the second stagger (over the priority, refused by the engine) or a call on the rider: " + body.Staggers.Count);
                Check(st.Verdict(SpikeLever.ForcedStagger) == SpikeVerdict.Works && st.Verdict(SpikeLever.OwnBlow) == SpikeVerdict.Works, "levers 4 / 6 do not read WORKS");

                // ---- lever 2 through the REAL decorator: x3 for an even blocker, the odd untouched, the caps; on worker threads too
                var a980 = Man(980); var d960 = Man(960); var d961 = Man(961);
                float att = 0.1f, def = 0.21f;
                model.CalculateDefendedBlowStunMultipliers(a980.Agent, d960.Agent, CombatCollisionResult.Blocked, null!, null!, ref att, ref def);
                Check(Near(att, 0.3f) && Near(def, 0.63f), "an even blocker's stun was not x3: " + att + " / " + def);
                att = 0.1f; def = 0.21f;
                model.CalculateDefendedBlowStunMultipliers(a980.Agent, d961.Agent, CombatCollisionResult.Blocked, null!, null!, ref att, ref def);
                Check(Near(att, 0.1f) && Near(def, 0.21f), "an odd blocker's stun was changed: " + att + " / " + def);
                att = 0.5f; def = 0.9f;
                model.CalculateDefendedBlowStunMultipliers(a980.Agent, d960.Agent, CombatCollisionResult.Parried, null!, null!, ref att, ref def);
                Check(Near(def, 2f) && Near(att, 1.5f), "x3 above the raised cap was not held at 2 s: " + att + " / " + def);
                att = 0.5f; def = 0.9f;
                model.CalculateDefendedBlowStunMultipliers(a980.Agent, d961.Agent, CombatCollisionResult.Parried, null!, null!, ref att, ref def);
                Check(Near(def, 0.8f) && Near(att, 0.5f), "an untouched blow above the game's 0.8 s was not clamped to it while our cap is raised: " + def);
                long callsBefore = st.Stun.Calls;
                Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                {
                    float x = 0.1f, y = 0.2f;
                    model.CalculateDefendedBlowStunMultipliers(a980.Agent, (i % 2 == 0 ? d960 : d961).Agent, CombatCollisionResult.Blocked, null!, null!, ref x, ref y);
                });
                Task.Run(() =>
                {
                    float x = 0.1f, y = 0.2f;
                    model.CalculateDefendedBlowStunMultipliers(a980.Agent, d960.Agent, CombatCollisionResult.Blocked, null!, null!, ref x, ref y);
                }).Wait();
                Check(st.Stun.Calls == callsBefore + 401 && st.Stun.OffMainThread >= 1 && st.Stun.Treated == 203 && st.Stun.Control == 202,
                    "the stun tally from worker threads: calls " + st.Stun.Calls + ", off the main thread " + st.Stun.OffMainThread + ", treated " + st.Stun.Treated + ", control " + st.Stun.Control);
                Check(Within(st.Stun.DefenderBefore, 0.2, 0.01) && Within(st.Stun.AttackerAfter, 0.3, 0.01), "the stun tally's means: " + st.Stun.DefenderBefore + " → " + st.Stun.DefenderAfter);

                // the recoil and the guard timed: an even blocker's x3, an odd one's untouched (two of each)
                for (int k = 0; k < 2; k++)
                {
                    foreach (var (attacker, blocker, recoil, guard) in new[] { (981 + 2 * k, 960, 0.3, 0.63), (982 + 2 * k, 961, 0.1, 0.21) })
                    {
                        var am = Man(attacker);
                        var dm = Man(blocker);
                        body.Actions[attacker] = Ch1(ImpactActions.BlockedMelee, 40 + k);
                        body.Actions[blocker] = Ch1(2, 60 + k);
                        logic.ImpactHit(am, dm, SpikeHit.Blocked(), t);
                        double start = t;
                        SpikeFrames(logic, ref t, 1.0 / 60);
                        while (t - start < guard + 0.05)
                        {
                            if (t - start >= recoil) body.Actions[attacker] = Ch1(19, 77);
                            if (t - start >= guard) body.Actions[blocker] = Ch1(2, 99);
                            SpikeFrames(logic, ref t, 1.0 / 60);
                        }
                        body.Actions.Remove(attacker);
                        body.Actions.Remove(blocker);
                    }
                }
                Check(st.RecoilTreated.Count == 2 && st.RecoilControl.Count == 2 && st.GuardTreated.Count == 2 && st.GuardControl.Count == 2,
                    "the recoil / guard samples: " + st.RecoilTreated.Count + " / " + st.RecoilControl.Count + " / " + st.GuardTreated.Count + " / " + st.GuardControl.Count);
                Check(Within(st.RecoilTreated.Mean / st.RecoilControl.Mean, 3, 1.0) && Within(st.GuardTreated.Mean / st.GuardControl.Mean, 3, 0.4) && st.Verdict(SpikeLever.BlockStun) == SpikeVerdict.Works,
                    "lever 2 measured: recoil " + ImpactSpikeStats.Pair(in st.RecoilControl, in st.RecoilTreated) + ", guard " + ImpactSpikeStats.Pair(in st.GuardControl, in st.GuardTreated));

                // ---- 901 picks a shield up again
                body.ShieldAgain.Add(901);
                SpikeFrames(logic, ref t, 0.6);
                Check(st.PickedUp == 1 && st.PickupSeconds.Count == 1 && st.PickupSeconds.Mean > 20 && logic.SpikeShields == 0, "the pick-up: " + st.PickedUp + " after " + ImpactSpikeStats.Len(in st.PickupSeconds));
                LogHas("[impact] spike lever 5: first dropped shield picked up again at ");

                // ---- the switch OFF live: vanilla at once - the cap back, no decision, no stun, nothing watched or queued
                logic.ImpactHit(Man(985), Man(986), SpikeHit.LandedKick(), t); // a kick being watched when it goes off
                S.Set(SettingsSchema.ImpactSpike, false, SettingSources.Mcm);
                att = 0.1f; def = 0.21f;
                model.CalculateDefendedBlowStunMultipliers(a980.Agent, d960.Agent, CombatCollisionResult.Blocked, null!, null!, ref att, ref def);
                Check(Near(def, 0.21f), "switched off: the stun was still x3 before the tick");
                SpikeFrames(logic, ref t, 1.0 / 60);
                Check(!logic.SpikeOn && StunCap == 0.8f && !ImpactHooks.StunCapRaised && logic.SpikeWatches == 0 && logic.SpikePendingCount == 0, "switched off: not vanilla at once (cap " + StunCap + ")");
                LogHas("[impact] spike switched OFF (ImpactSpike) mid-mission at ");
                LogHas(" s: vanilla at once - the game's stun cap back to 0.80 s, 0 queued action(s) dropped unplayed, 1 watch(es) and 0 shield(s) no longer followed");
                int kicks = st.KicksLanded;
                var cd = KickCollision();
                var bk = new Blow(kicker.AgentIndex);
                Check(!model.DecideAgentKnockedDownByBlow(kicker.Agent, Man(987).Agent, in cd, null!, in bk), "switched off: a kick got KnockDown");
                logic.ImpactHit(kicker, Man(987), SpikeHit.LandedKick(), t);
                logic.ImpactHit(Man(988), Man(989), SpikeHit.Blocked(), t);
                Check(st.KicksLanded == kicks && logic.SpikeWatches == 0 && logic.SpikePendingCount == 0, "switched off: a hit was still handled");
                S.Set(SettingsSchema.ImpactSpike, true, SettingSources.Mcm);
                SpikeFrames(logic, ref t, 1.0 / 60);
                Check(logic.SpikeOn && StunCap == 2f, "switched on again: not on, or the cap not raised");
                LogHas("[impact] spike switched ON mid-mission at ");

                // ---- a watched man leaves the field: dropped, no engine call
                logic.ImpactHit(kicker, Man(991), SpikeHit.LandedKick(), t);
                Check(logic.SpikeWatches == 1, "the kick on 991 is not watched");
                int reads = body.Reads;
                int left = st.LeftField;
                typeof(AthleticsLogic).GetMethod("Untrack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, new object[] { men[991].Agent });
                SpikeFrames(logic, ref t, 1.0 / 60);
                Check(logic.SpikeWatches == 0 && st.LeftField == left + 1 && body.Reads == reads, "a man who left the field was still watched or read");

                // ---- the summary: a header and a line per lever with its verdict and what it means
                logic.WriteImpactSummary();
                Check(StunCap == 0.8f && !ImpactHooks.StunCapRaised && !ReferenceEquals(ImpactHooks.KnockDown, logic) && !ReferenceEquals(ImpactHooks.BlockStun, logic),
                    "the summary did not give the cap back or take our deciders out");
                LogHas("[summary] impact spike (step 47, ImpactSpike - a test tool): on ");
                LogHas(" (switched 2 time(s)); levers disabled by an error: none; engine calls ");
                LogHas("[summary] impact spike lever 1 - KnockDown on a landed kick / bash (the damage decorator's decision): landed kicks 13 (bashes 0, 0 damage - no reaction at all 1, on riders 1); KnockDown answered by the decorator 6 (the rotation asked 6) → Fall seen within 1.0 s 4, not seen 1");
                LogHas("their reaction StrikeKnockBack 4 → WORKS - I3 / I5 knock men down through the decorator's DecideAgentKnockedDownByBlow");
                LogHas("[summary] impact spike lever 2 - block stun x3, cap 2 s (CalculateDefendedBlowStunMultipliers): the decorator's calls ");
                LogHas("the game's cap 0.80 → 2.00 s (restored ");
                LogHas(" → WORKS - I4 lengthens the blocker's stun and the riposted attacker's recoil through CalculateDefendedBlowStunMultipliers");
                LogHas("[summary] impact spike lever 3 - reaction speed 0.5 (SetCurrentActionSpeed the tick after): SetCurrentActionSpeed 0.5 calls 6 (again on a _continue 2); the reel (no knockdown) control 0.4");
                LogHas(" → WORKS - I3's longer reel and slower get-up = SetCurrentActionSpeed the tick after");
                LogHas("[summary] impact spike lever 4 - forced stagger (SetActionChannel act_stagger_backward): asked 2 (with the engine's priority 1 → accepted 1, over it 1 → accepted 0), accepted 1 → seen on channel 0 1, not seen 0, lasted 0.9");
                LogHas("; blocks made from inside it 1, hits taken inside it 1; never asked (no engine call): a rider 1 → WORKS - I4's rolled stagger = act_stagger_* on channel 0");
                LogHas("[summary] impact spike lever 5 - shield dropped (DropItem queued with AddTickAction): kicked men holding a shield 2 (without 10); drops queued 2 → the shield left his hand within 1.5 s 1, not 1; picked up again 1");
                LogHas(" → WORKS - I3 / I4 drop shields with DropItem queued, vanilla's way");
                LogHas("[summary] impact spike lever 6 - our own knockdown blow (RegisterBlow, KnockDown, 1 damage): our blows registered 1");
                LogHas("→ Fall seen within 1.0 s 1, not seen 0; never asked (no engine call): none → WORKS - I4 knocks a blocker down with a blow of our own");
                logic.TickImpact(t + 1);
                Check(!logic.SpikeOn, "a tick after the summary turned the spike on again");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                SpikeDefaults(on: false);
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                AthleticsDefaults();
            }
        }

        /// <summary>A stun decider that throws (the fail-safe check).</summary>
        private sealed class ThrowingStun : IBlockStunDecider
        {
            public void SetBlockStun(Agent attacker, Agent defender, CombatCollisionResult result, ref float attackerStun, ref float defenderStun)
            {
                defenderStun = 99f; // a half-done write must not survive
                throw new InvalidOperationException("smoke: the stun decider failed");
            }
        }

        /// <summary>
        /// One lever's error disables only that lever for the mission: SetCurrentActionSpeed throwing → lever 3 off (the reaction plays on,
        /// the next kicks are not slowed, lever 1 still asked), the stun decider throwing on a worker thread → the base's stun kept, then
        /// lever 2 off at the next tick and the game's cap back, the queued drop throwing → lever 5 off; each [error] once, each "lever N
        /// disabled" line, the summary names them and reads FAILS for them.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactSpikeFailSafe()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                SpikeDefaults();
                var body = new FakeImpactBody { ThrowOnSlow = true, ThrowOnDrop = true };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                var st = logic.SpikeStats;
                var model = SpikeDamageModel();
                double t = 16000;
                logic.TickImpact(t);
                var kicker = logic.Track(FakeAgent(1900))!;
                var men = new TrackedAgent[8];
                for (int i = 0; i < men.Length; i++)
                {
                    men[i] = logic.Track(FakeAgent(1901 + i))!;
                    body.ShieldInHand[1901 + i] = 3;
                }
                bool Kick(TrackedAgent on)
                {
                    var cd = KickCollision();
                    var blow = new Blow(kicker.AgentIndex);
                    bool down = model.DecideAgentKnockedDownByBlow(kicker.Agent, on.Agent, in cd, null!, in blow);
                    logic.ImpactHit(kicker, on, SpikeHit.LandedKick(), t);
                    return down;
                }
                Check(Kick(men[0]) && st.Disabled(SpikeLever.ShieldDrop) && !st.Disabled(SpikeLever.KickKnockDown), "the drop's error did not disable lever 5 alone");
                LogHas("[error] impact.shield-drop");
                LogHas("[impact] spike lever 5 (shield drop) disabled for this mission after an error at impact.shield-drop - the other levers run on");
                Kick(men[1]);
                Check(Kick(men[2]), "kick 3 did not get KnockDown");
                body.Actions[1903] = Ch0(ImpactActions.Fall, 5);
                SpikeFrames(logic, ref t, 0.1);
                Check(st.Disabled(SpikeLever.SlowReaction) && st.FallSeen == 1, "SetCurrentActionSpeed's error did not disable lever 3 (or the fall was lost)");
                LogHas("[error] impact.slow");
                LogHas("[impact] spike lever 3 (slow reaction) disabled for this mission after an error at impact.slow");
                body.Actions.Remove(1903);
                SpikeFrames(logic, ref t, 0.1);
                Check(st.FallControl.Count == 1 && st.FallSlowed.Count == 0, "the fall whose slow failed (it played at its own speed) was not filed as untouched");
                body.ThrowOnSlow = false;
                Kick(men[3]);
                body.Actions[1904] = Ch0(ImpactActions.StrikeKnockBack, 6);
                SpikeFrames(logic, ref t, 0.1);
                Check(body.Slows.Count == 0, "a reaction was slowed after lever 3 was disabled");
                Check(Kick(men[4]) && st.KnockDownAsked == 3 && st.DropsQueued == 0, "lever 1 stopped with lever 3 / 5, or a drop was queued after lever 5 went");

                // the stun decider throwing on a worker thread: the base's stun kept, lever 2 off at the next tick, the cap back
                ImpactHooks.BlockStun = new ThrowingStun();
                float att = 0.1f, def = 0.2f;
                Task.Run(() => model.CalculateDefendedBlowStunMultipliers(kicker.Agent, men[5].Agent, CombatCollisionResult.Blocked, null!, null!, ref att, ref def)).Wait();
                Check(Near(def, 0.2f) && Near(att, 0.1f), "a throwing stun decider's write survived: " + def);
                ImpactHooks.BlockStun = logic;
                Check(StunCap == 2f, "precondition: the cap is not raised");
                SpikeFrames(logic, ref t, 1.0 / 60);
                Check(st.Disabled(SpikeLever.BlockStun) && StunCap == 0.8f, "the stun decider's error did not disable lever 2 and give the cap back");
                LogHas("[error] impact.stun");
                LogHas("[impact] spike lever 2 (block stun) disabled for this mission after an error at impact.stun");
                att = 0.1f; def = 0.2f;
                model.CalculateDefendedBlowStunMultipliers(kicker.Agent, men[5].Agent, CombatCollisionResult.Blocked, null!, null!, ref att, ref def);
                Check(Near(def, 0.2f), "lever 2 still x3 after it was disabled");
                int errors = Occurrences(LogText, "[error] impact.slow");
                body.ThrowOnSlow = true;
                SpikeFrames(logic, ref t, 1);
                Check(Occurrences(LogText, "[error] impact.slow") == errors, "a disabled lever logged again");

                // the forced stagger and our own blow throwing in the tick: levers 4 and 6 off, lever 1 still on
                body.ThrowOnStagger = true;
                body.ThrowOnOwnBlow = true;
                logic.ImpactHit(kicker, men[6], SpikeHit.Blocked(), t);
                SpikeFrames(logic, ref t, 1.0 / 60);
                Check(st.Disabled(SpikeLever.ForcedStagger) && !st.Disabled(SpikeLever.OwnBlow), "the stagger's error did not disable lever 4 alone");
                logic.ImpactHit(kicker, men[7], SpikeHit.Blocked(), t);
                SpikeFrames(logic, ref t, 1.0 / 60);
                Check(st.Disabled(SpikeLever.OwnBlow) && !st.Disabled(SpikeLever.KickKnockDown), "our blow's error did not disable lever 6 alone");
                LogHas("[impact] spike lever 4 (forced stagger) disabled for this mission after an error at impact.stagger");
                LogHas("[impact] spike lever 6 (own knockdown blow) disabled for this mission after an error at impact.own-blow");
                Check(Kick(men[5]) || st.KnockDownAsked == 3, "lever 1 stopped with the others"); // #6 - not a KnockDown kick, still decided
                Check(Kick(men[6]) && st.KnockDownAsked == 4, "lever 1 did not ask kick #7 after five levers went");

                logic.WriteImpactSummary();
                LogHas("levers disabled by an error: 2 (impact.stun), 3 (impact.slow), 4 (impact.stagger), 5 (impact.shield-drop), 6 (impact.own-blow)");
                LogHas(" → FAILS (disabled by an error at impact.slow) - IMPACT §7 fallback 1: SetOverridenStrikeAndDeathAction");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                SpikeDefaults(on: false);
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                AthleticsDefaults();
            }
        }

        /// <summary>The spike's cost: a thousand men, ten blocks a tick and a kick every few ticks (the watch pool full, the queue busy, a
        /// stagger every 10 s, our blow every 20 s) - the tick and the hits stay small and allocate nothing once the first lines are out.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactSpikeCostStaysFlat()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                SpikeDefaults();
                var body = new FakeImpactBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                const int men = 1000;
                var all = new TrackedAgent[men];
                for (int i = 0; i < men; i++) all[i] = logic.Track(FakeAgent(7000 + i))!;
                double t0 = 17000;
                var hit = SpikeHit.Blocked();
                var kick = SpikeHit.LandedKick();
                int k = 0;
                void Frame(int f)
                {
                    double t = t0 + f / 60.0;
                    for (int b = 0; b < 10; b++, k++) logic.ImpactHit(all[(k * 7) % men], all[(k * 13 + 1) % men], in hit, t);
                    if (f % 6 == 0) logic.ImpactHit(all[(f * 3) % men], all[(f * 11 + 5) % men], in kick, t);
                    logic.TickImpact(t);
                }
                for (int f = 0; f < 1300; f++) Frame(f); // warm-up: every first line written, 20 s of it (a stagger and our blow due)
                const int ticks = 1200;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int f = 1300; f < 1300 + ticks; f++) Frame(f);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double ms = sw.Elapsed.TotalMilliseconds / ticks;
                var st = logic.SpikeStats;
                Console.WriteLine("        step 47 cost: " + men + " men, 10 blocks + a kick every 6 ticks - " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick (hits included), "
                                  + allocated + " bytes over " + ticks + " ticks; watch pool full " + st.WatchesFull + ", staggers " + st.StaggerAsked + ", our blows " + st.OwnBlowAsked);
                Check(ms < 0.5, "cost: the spike's tick + hits over " + men + " men took " + ms + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the spike allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(st.StaggerAsked >= 4 && st.OwnBlowAsked >= 2 && st.WatchesFull > 0, "cost: the levers did not run (staggers " + st.StaggerAsked + ", blows " + st.OwnBlowAsked + ")");
                logic.WriteImpactSummary();
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                SpikeDefaults(on: false);
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                AthleticsDefaults();
            }
        }

        /// <summary>The game's side of the levers (no game launched, no native call): the stagger actions are fields of the game's
        /// ActionIndexCache (read by reflection - its static constructor would ask the engine), Core's action codes are the game's
        /// ActionCodeType / CombatCollisionResult values, the drop's tick action, the blow flags, the stun cap is a managed parameter we can
        /// raise and restore, and every engine call the body makes exists with its v1.4.8 signature.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactGameFacts()
        {
            foreach (var name in new[] { "act_stagger_backward", "act_stagger_forward", "act_stagger_left", "act_stagger_right" })
            {
                var f = typeof(ActionIndexCache).GetField(name, BindingFlags.Static | BindingFlags.Public);
                Check(f != null && f.FieldType == typeof(ActionIndexCache), "ActionIndexCache has no " + name);
            }
            Check(ImpactActions.DefendFirst == (int)Agent.ActionCodeType.DefendFist && ImpactActions.DefendLast == (int)Agent.ActionCodeType.DefendLeftStaff
                  && ImpactActions.ParriedMelee == (int)Agent.ActionCodeType.ParriedMelee && ImpactActions.BlockedMelee == (int)Agent.ActionCodeType.BlockedMelee
                  && ImpactActions.Fall == (int)Agent.ActionCodeType.Fall && ImpactActions.StrikeLight == (int)Agent.ActionCodeType.StrikeLight
                  && ImpactActions.StrikeMedium == (int)Agent.ActionCodeType.StrikeMedium && ImpactActions.StrikeHeavy == (int)Agent.ActionCodeType.StrikeHeavy
                  && ImpactActions.StrikeKnockBack == (int)Agent.ActionCodeType.StrikeKnockBack && ImpactActions.MountStrike == (int)Agent.ActionCodeType.MountStrike,
                "Core's action codes are not the game's ActionCodeType");
            // step 50: vanilla's drop rule (Agent.HandleDropWeapon) - no drop during his own release / bash
            Check(ImpactActions.ReleaseMelee == (int)Agent.ActionCodeType.ReleaseMelee && ImpactActions.ReleaseRanged == (int)Agent.ActionCodeType.ReleaseRanged
                  && ImpactActions.ReleaseThrowing == (int)Agent.ActionCodeType.ReleaseThrowing && ImpactActions.WeaponBash == (int)Agent.ActionCodeType.WeaponBash,
                "Core's release / bash codes (step 50's drop rule) are not the game's ActionCodeType");
            Check(ImpactActions.ResultStrikeAgent == (int)CombatCollisionResult.StrikeAgent, "Core's StrikeAgent is not the game's");
            Check(Enum.IsDefined(typeof(Mission.MissionTickAction), Mission.MissionTickAction.DropItem), "Mission.MissionTickAction has no DropItem");
            Check((int)(BlowFlags.KnockDown | BlowFlags.KnockBack) == 0x30, "the blow flags moved");
            float keep = ManagedParameters.GetParameter(ManagedParametersEnum.StunPeriodMax);
            try
            {
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, 0.8f);
                Check(Near(ImpactHooks.RaiseStunCap(2f), 0.8f) && StunCap == 2f && ImpactHooks.StunCapRaised && Near(ImpactHooks.VanillaStunCap, 0.8f), "the cap was not raised to 2 s");
                Check(Near(ImpactHooks.RaiseStunCap(2f), 0.8f) && StunCap == 2f, "raising twice lost the game's value");
                Check(ImpactHooks.RestoreStunCap() && StunCap == 0.8f && !ImpactHooks.RestoreStunCap(), "the cap was not restored once");
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, 3f);
                ImpactHooks.RaiseStunCap(2f);
                Check(!ImpactHooks.StunCapRaised && StunCap == 3f, "a cap already above 2 s was lowered");
            }
            finally
            {
                ImpactHooks.RestoreStunCap();
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, keep);
            }
            var agent = typeof(Agent);
            Check(agent.GetMethod("SetCurrentActionSpeed", new[] { typeof(int), typeof(float) }) != null, "Agent.SetCurrentActionSpeed(int, float) is gone");
            Check(agent.GetMethod("RegisterBlow", new[] { typeof(Blow), typeof(AttackCollisionData).MakeByRefType() }) != null, "Agent.RegisterBlow(Blow, in AttackCollisionData) is gone");
            Check(agent.GetMethod("DropItem", new[] { typeof(EquipmentIndex), typeof(WeaponClass) }) != null, "Agent.DropItem(EquipmentIndex, WeaponClass) is gone");
            Check(agent.GetMethod("GetCurrentAction", new[] { typeof(int) }) != null && agent.GetMethod("GetCurrentActionType", new[] { typeof(int) }) != null, "Agent's channel reads are gone");
            var set = agent.GetMethod("SetActionChannel");
            Check(set != null && set.ReturnType == typeof(bool) && set.GetParameters()[1].ParameterType == typeof(ActionIndexCache).MakeByRefType(), "Agent.SetActionChannel(int, in ActionIndexCache, …) changed");
        }

        /// <summary>The master-switch step's part: the spike on, then ModEnabled off - vanilla at once (the decorator's KnockDown is the
        /// game's, the stun untouched even before the tick, the cap back at the tick, nothing queued or watched), on again = the spike back.</summary>
        private static void ImpactSpikeFollowsTheMasterSwitch()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                SpikeDefaults();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                var model = SpikeDamageModel();
                double t = 18000;
                logic.TickImpact(t);
                var a = logic.Track(FakeAgent(2900))!;
                var d = logic.Track(FakeAgent(2902))!;
                logic.ImpactHit(a, d, SpikeHit.Blocked(), t); // a stagger queued
                Check(logic.SpikeOn && StunCap == 2f && logic.SpikePendingCount == 1, "precondition: the spike is not on with a stagger queued");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                float att = 0.1f, def = 0.2f;
                model.CalculateDefendedBlowStunMultipliers(a.Agent, d.Agent, CombatCollisionResult.Blocked, null!, null!, ref att, ref def);
                var cd = KickCollision();
                var blow = new Blow(a.AgentIndex);
                Check(Near(def, 0.2f) && !model.DecideAgentKnockedDownByBlow(a.Agent, d.Agent, in cd, null!, in blow), "mod off: the decorator still lengthened a stun or knocked down (step 47)");
                logic.TickImpact(t + 0.02);
                Check(!logic.SpikeOn && StunCap == 0.8f && logic.SpikePendingCount == 0 && body.Staggers.Count == 0, "mod off: the spike was not vanilla at once - the cap, the queued stagger (step 47)");
                LogHas("[impact] spike switched OFF (ModEnabled) mid-mission at ");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t + 0.04);
                Check(logic.SpikeOn && StunCap == 2f, "mod back on: the spike did not come back (step 47)");
                logic.WriteImpactSummary();
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                SpikeDefaults(on: false);
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
            }
        }
    }
}
