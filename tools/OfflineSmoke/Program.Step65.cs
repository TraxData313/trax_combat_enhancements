using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 65 - THE AI KICKS AND BASHES, THE KICKED MAN BRACES (AI_NOTES "Step 65"). The AI part through the REAL logic's
    /// look and attempt (the stand-in body answers the engine's reads) and the REAL input hook's managed filter (the bits the component writes);
    /// the engine "playing" the kick = step 18's action poll fed the engine's Kick / WeaponBash codes. The brace through the REAL damage
    /// decorator's knock-back and KnockDown questions (the game's own CustomAgentApplyDamageModel underneath) and the REAL logic, the reaction
    /// played by the stand-in impact body. What only the game can tell (does the engine take the injected input; what a refused knock-back
    /// plays) is PLAYTEST D31's.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for the AI kick's engine reads.</summary>
        private sealed class FakeAiKickBody : IAiKickBody
        {
            public readonly HashSet<int> Gone = new HashSet<int>();
            public readonly HashSet<int> NotAi = new HashSet<int>();
            public readonly HashSet<int> Mounted = new HashSet<int>();
            public readonly HashSet<int> Routing = new HashSet<int>();
            public readonly HashSet<int> Ranged = new HashSet<int>();
            public readonly HashSet<int> Shield = new HashSet<int>();
            public readonly Dictionary<int, (TrackedAgent Target, float Distance, float Off)> Targets = new Dictionary<int, (TrackedAgent, float, float)>();
            public readonly Dictionary<int, float> FacingToward = new Dictionary<int, float>();
            public int Hooks, Releases, Reads;

            public bool IsActive(TrackedAgent st) => !Gone.Contains(st.AgentIndex);

            public bool IsAiControlled(TrackedAgent st) => !NotAi.Contains(st.AgentIndex);

            public void Hands(TrackedAgent st, out bool mounted, out bool routing, out bool ranged, out bool shield)
            {
                Reads++;
                int i = st.AgentIndex;
                mounted = Mounted.Contains(i);
                routing = Routing.Contains(i);
                ranged = Ranged.Contains(i);
                shield = Shield.Contains(i);
            }

            public Agent? Target(TrackedAgent st, out float distance, out float offFacingDegrees)
            {
                if (Targets.TryGetValue(st.AgentIndex, out var t))
                {
                    distance = t.Distance;
                    offFacingDegrees = t.Off;
                    return t.Target.Agent;
                }
                distance = offFacingDegrees = float.NaN;
                return null;
            }

            public float FacingTo(TrackedAgent st, TrackedAgent other) => FacingToward.TryGetValue(st.AgentIndex, out var a) ? a : 180f;

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                Hooks++;
                return AiInputHook.HookResult.FirstHookTurnedOn;
            }

            public void Release(TrackedAgent st) => Releases++;

            /// <summary>Step 68: the men with a friend on the line to their target; the men who are FRIENDS of each other (by index pairs).</summary>
            public readonly HashSet<int> FriendBlocks = new HashSet<int>();
            public readonly HashSet<(int, int)> Friends = new HashSet<(int, int)>();
            public int FriendReads;

            /// <summary>Step 80: a world on a plane for the strike path - each man's place, facing and side (none = the old stand-in answers).</summary>
            public readonly Dictionary<int, (double X, double Y, double FX, double FY, int Side)> Bodies = new Dictionary<int, (double, double, double, double, int)>();

            /// <summary>Step 68's stand-in (FriendBlocks = a friend first), step 80's on <see cref="Bodies"/>: the real Core probe over every man of
            /// his side placed there.</summary>
            public AiKickPath StrikePath(TrackedAgent st, Agent target, double reach, double halfWidth, double rearRank, double blockAhead, double blockSide,
                out int looked, out int letThrough, out AiKickBlocker blocker)
            {
                FriendReads++;
                looked = 0;
                letThrough = 0;
                blocker = AiKickBlocker.None;
                if (FriendBlocks.Contains(st.AgentIndex)) return AiKickPath.FriendFirst;
                if (!Bodies.TryGetValue(st.AgentIndex, out var me) || !Bodies.TryGetValue(target.Index, out var t)) return AiKickPath.Clear;
                var p = AiKickPathProbe.Begin(me.X, me.Y, me.FX, me.FY, t.X, t.Y, reach, halfWidth, rearRank, blockAhead, blockSide);
                foreach (var kv in Bodies)
                    if (kv.Key != st.AgentIndex && kv.Value.Side == me.Side && Math.Abs(kv.Value.X - me.X) < reach + halfWidth + 1 && Math.Abs(kv.Value.Y - me.Y) < reach + halfWidth + 1)
                        p.Add(kv.Value.X, kv.Value.Y, kv.Key); // step 87: the agent index as the probe's own index
                looked = p.Looked;
                letThrough = p.LetThrough;
                blocker.LeftNear = p.LeftNear; // step 90
                blocker.RightNear = p.RightNear;
                if (p.NearestFriendIndex >= 0)
                {
                    blocker.Valid = true;
                    blocker.Along = p.NearestFriend;
                    blocker.Side = p.NearestFriendSide;
                    blocker.Lateral = p.NearestFriendLateral;
                    blocker.FriendIndex = p.NearestFriendIndex;
                    Relate(st.AgentIndex, p.NearestFriendIndex, ref blocker);
                }
                return p.Verdict;
            }

            /// <summary>Step 87: each placed man's formation, file and rank (none = another formation / none).</summary>
            public readonly Dictionary<int, (int Formation, int File, int Rank)> Ranks = new Dictionary<int, (int, int, int)>();

            public AiKickBlocker Locate(TrackedAgent st, Agent friend)
            {
                var b = AiKickBlocker.None;
                if (!Bodies.TryGetValue(st.AgentIndex, out var me) || !Bodies.TryGetValue(friend.Index, out var f)) return b;
                AiKickBlockMath.Locate(me.X, me.Y, me.FX, me.FY, f.X, f.Y, out b.Along, out b.Side, out b.Lateral);
                b.Valid = true;
                b.FriendIndex = friend.Index;
                Relate(st.AgentIndex, friend.Index, ref b);
                return b;
            }

            private void Relate(int me, int friend, ref AiKickBlocker b)
            {
                bool same = Ranks.TryGetValue(me, out var a) && Ranks.TryGetValue(friend, out var f) && a.Formation == f.Formation;
                if (same)
                {
                    var fr = Ranks[friend];
                    b.File = a.File;
                    b.Rank = a.Rank;
                    b.FriendFile = fr.File;
                    b.FriendRank = fr.Rank;
                }
                b.Relation = AiKickBlockMath.RelationOf(same, true, b.Rank, b.FriendRank);
            }

            /// <summary>Step 95: the step-in's reads on <see cref="Bodies"/> (his frame: forward = his facing, right = (FY, -FX)); without bodies the
            /// target's stand-in distance and a straight forward walk.</summary>
            public int StepInReads, StepInClearReads;

            public bool StepIn(TrackedAgent st, TrackedAgent target, out float distance, out float localX, out float localY)
            {
                StepInReads++;
                distance = float.NaN;
                localX = localY = 0f;
                if (Gone.Contains(target.AgentIndex)) return false;
                if (!Bodies.TryGetValue(st.AgentIndex, out var me) || !Bodies.TryGetValue(target.AgentIndex, out var t))
                {
                    if (!Targets.TryGetValue(st.AgentIndex, out var tg) || !ReferenceEquals(tg.Target, target)) return false;
                    distance = tg.Distance;
                    localY = 1f;
                    return true;
                }
                double dx = t.X - me.X, dy = t.Y - me.Y;
                distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (!AiBashStepInMath.Vector(dx, dy, me.FY, -me.FX, me.FX, me.FY, out double x, out double y)) return false;
                localX = (float)x;
                localY = (float)y;
                return true;
            }

            /// <summary>Step 95: the walk's corridor against every man of his side on <see cref="Bodies"/> (none placed: <see cref="FriendBlocks"/>).</summary>
            public bool StepInClear(TrackedAgent st, Agent target, double step)
            {
                StepInClearReads++;
                if (!Bodies.TryGetValue(st.AgentIndex, out var me) || !Bodies.TryGetValue(target.Index, out var t)) return !FriendBlocks.Contains(st.AgentIndex);
                foreach (var kv in Bodies)
                    if (kv.Key != st.AgentIndex && kv.Value.Side == me.Side && AiBashStepInMath.InCorridor(me.X, me.Y, t.X, t.Y, kv.Value.X, kv.Value.Y, step)) return false;
                return true;
            }

            public bool IsEnemy(TrackedAgent st, TrackedAgent other)
            {
                if (Bodies.TryGetValue(st.AgentIndex, out var a) && Bodies.TryGetValue(other.AgentIndex, out var b)) return a.Side != b.Side;
                return !Friends.Contains((st.AgentIndex, other.AgentIndex)) && !Friends.Contains((other.AgentIndex, st.AgentIndex));
            }
        }

        /// <summary>Step 65: every logic built from here on reads the AI kick's engine side through the stand-in (the game's reads native agents).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeAiKickBody()
        {
            Check(AiKickBodyDefault.Current is GameAiKickBody, "the default AI kick body is not the game's: " + AiKickBodyDefault.Current.GetType().Name);
            AiKickBodyDefault.Current = new FakeAiKickBody();
            Check(new AthleticsLogic().AiKickBody == AiKickBodyDefault.Current, "a new logic does not start with the default AI kick body");
            // the engine's own values behind the Core's bits / codes
            Check(AiKickMath.KickEventBit == (uint)Agent.EventControlFlag.Kick, "EventControlFlag.Kick moved: " + (uint)Agent.EventControlFlag.Kick);
            Check(AiKickMath.DefendUp == (uint)Agent.MovementControlFlag.DefendUp, "MovementControlFlag.DefendUp moved");
            Check(ImpactActions.DefendShield == (int)Agent.ActionCodeType.DefendShield, "ActionCodeType.DefendShield moved");
            foreach (var busy in new[] { Agent.ActionCodeType.ReadyRanged, Agent.ActionCodeType.ReleaseRanged, Agent.ActionCodeType.ReleaseThrowing, Agent.ActionCodeType.Reload,
                         Agent.ActionCodeType.ReadyMelee, Agent.ActionCodeType.ReleaseMelee, Agent.ActionCodeType.Kick, Agent.ActionCodeType.KickContinue,
                         Agent.ActionCodeType.KickHit, Agent.ActionCodeType.WeaponBash, Agent.ActionCodeType.EquipUnequip, Agent.ActionCodeType.SwitchAlternative })
                Check(AiKickMath.IsBusyAction((int)busy), "the busy action " + busy + " is not busy");
            foreach (var free in new[] { Agent.ActionCodeType.DefendShield, Agent.ActionCodeType.Idle, Agent.ActionCodeType.Guard, Agent.ActionCodeType.BlockedMelee })
                Check(!AiKickMath.IsBusyAction((int)free), "the action " + free + " counts as busy");
        }

        private static void AiKickDefaults()
        {
            foreach (var p in new[]
                     {
                         SettingsSchema.AiKicksEnabled, SettingsSchema.AiShieldmanKickPercent, SettingsSchema.AiShieldmanBashPercent, SettingsSchema.AiNoShieldKickPercent,
                         SettingsSchema.AiKickReachMetres, SettingsSchema.AiKickFacingDegrees, SettingsSchema.AiKickCooldownSeconds, SettingsSchema.AiKickKicksARaisedShield,
                         SettingsSchema.KickBraceEnabled, SettingsSchema.KickBraceFromPressure, SettingsSchema.KickBraceToPressure, SettingsSchema.KickBraceMinBarPercent,
                         SettingsSchema.KickBraceReelPercent, SettingsSchema.KickShortReelPercent, SettingsSchema.KickBraceRefusesKnockBack,
                     })
                S.Set(p, p.Default, SettingSources.File);
            // step 88 moved the shieldman's shipped shares to 25 / 40 and added the no-shield PUSH (20): the older steps keep step 81's shares
            // (20 / 35, no push) - step 88's own checks set the shipped ones (Step88Shares)
            S.Set(SettingsSchema.AiShieldmanKickPercent, 20, SettingSources.File);
            S.Set(SettingsSchema.AiShieldmanBashPercent, 35, SettingSources.File);
            S.Set(SettingsSchema.AiNoShieldPushPercent, 0, SettingSources.File);
            S.Set(SettingsSchema.AiNoShieldKickPercent, 35, SettingSources.File); // step 95 shipped 45 - the older steps keep step 81's 35
            // step 91: the older steps assert numbers with the bash rolled anywhere in the kick reach - the bash reach off (Step91Reach sets the shipped one)
            S.Set(SettingsSchema.AiBashReachMetres, 0.0, SettingSources.File);
            // step 95: the older steps bash from where the man stands - the step-in off (Step95StepIn sets the shipped one)
            S.Set(SettingsSchema.AiBashStepIn, false, SettingSources.File);
        }

        /// <summary>Step 88: the shipped shares (the embedded defaults.json - shieldman 25 / 40 / 35, no shield 35 / 20 / 45).</summary>
        private static void Step88Shares()
        {
            // step 95 moved the shipped shares on (27 / 43, 45 / 25): steps 88-91's checks assert step 88's - 25 / 40, 35 / 20 (Step95Shares the shipped ones)
            S.Set(SettingsSchema.AiShieldmanKickPercent, 25, SettingSources.File);
            S.Set(SettingsSchema.AiShieldmanBashPercent, 40, SettingSources.File);
            S.Set(SettingsSchema.AiNoShieldKickPercent, 35, SettingSources.File);
            S.Set(SettingsSchema.AiNoShieldPushPercent, 20, SettingSources.File);
        }

        /// <summary>Step 95: the shipped shares (defaults.json - 27 / 43 / 30, 45 / 25 / 30).</summary>
        private static void Step95Shares()
        {
            foreach (var p in new[] { SettingsSchema.AiShieldmanKickPercent, SettingsSchema.AiShieldmanBashPercent, SettingsSchema.AiNoShieldKickPercent, SettingsSchema.AiNoShieldPushPercent })
                S.Set(p, p.Default, SettingSources.File);
        }

        /// <summary>Step 81: the engine's melee wind-up code (step 13's phases see it) - a man about to swing, the decision point.</summary>
        private const int ReadyMeleeAction = (int)Agent.ActionCodeType.ReadyMelee;

        /// <summary>Step 81: his wind-up began and the tick rolls it - through the real decision (the dice the logic holds).</summary>
        private static void DecideAt(AthleticsLogic logic, TrackedAgent m, double t)
        {
            m.PrevAction = ReadyMeleeAction;
            logic.DecideAiMove(m, t, AiKickRules.From(S), Rules);
        }

        private static void Step65CleanUp(AthleticsLogic? keep)
        {
            _logic = keep;
            SetStatic(typeof(AthleticsLogic), "_current", keep);
            ImpactHooks.RestoreStunCap();
            ImpactHooks.KnockDown = null;
            ImpactHooks.BlockStun = null;
            ImpactHooks.PickupLock = null;
            ImpactHooks.ShrugOff = null;
            ImpactHooks.Dismount = null;
            ImpactHooks.KnockBack = null;
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            ImpactModelDefaults();
            AthleticsDefaults();
        }

        /// <summary>
        /// THE AI KICKS AND BASHES through the real logic (step 81's decision point - a man's melee WIND-UP rolled by his shares): a kicker (no shield)
        /// rolled a kick, the input frames phase by phase through the real hook filter (the guard first - his wind-up cancelled -, the kick key with the
        /// guard down; his own attack / events out; still in the commit), the engine "playing" it (step 18's poll sees a Kick on channel 0) = PLAYED,
        /// its cost charged by step 18 and counted, its hit counted, the attempt ended when the action is over, the cooldown; a shieldman's rolled
        /// BASH (the guard up 0.4 s first, then the key + the guard; a WeaponBash seen); a rolled bash into a RAISED shield kicked; an attempt the
        /// engine never plays (NOT played); not a move in reach (mounted, routing, a ranged weapon, no target, beyond kick reach, not in front, the
        /// wind-up already over) and the refusals (no Athletics, staggered, no stored swing, the cooldown); the die's swing; the switch / the master
        /// switch mid-attempt; the summary.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiKicksThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File); // the brace has its own step
                Check(S.AiKicksEnabled && S.AiShieldmanKickPercent == 20 && S.AiShieldmanBashPercent == 35 && S.AiNoShieldKickPercent == 35,
                    "the AI kicks are not on at 20 / 35 / 35 by default (step 81)");
                var body = new FakeAiKickBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.AiKickBody = body;
                logic.AiKickDice = new FixedDice(0.0); // a kick for anyone
                var st = logic.AiKickStats;
                LogHas("[impact] mission start, the AI kicks and bashes (steps 65 / 81): ON - each melee move an AI man on foot is about to make (his wind-up)");
                double t = 65000;
                TrackedAgent Man(int index, int skill = 100, double bar = 1.0)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = skill;
                    SetBar(m, bar);
                    m.PrevAction = 35; // idle (the poll's last read)
                    m.PrevLowerAction = 35;
                    return m;
                }
                void Decide(TrackedAgent m) => DecideAt(logic, m, t);
                void Tick(double dt)
                {
                    t += dt;
                    logic.TickAiKicks(t);
                }
                uint Frame(TrackedAgent m, uint flags, ref uint ev, out float vx, out float vy)
                {
                    vx = 0.3f;
                    vy = 1f;
                    AiInputHook.ApplyKick(m.Input!, ref ev, ref flags, ref vx, ref vy);
                    return flags;
                }

                // ---- 1. a KICK in place of his swing: asked, the guard first (the wind-up cancelled), the key, played (the poll sees a Kick), its cost, its hit, over → the cooldown
                var kicker = Man(6500, 130);
                var target = Man(6501, 100);
                body.Targets[6500] = (target, 1.2f, 10f);
                double before = kicker.Fraction;
                Decide(kicker);
                Check(kicker.AiKickPhase == AiKickPhase.Guard && kicker.AiKickKind == AiKickKind.Kick && kicker.Input != null && kicker.Input.KickPhase == AiKickPhase.Guard && body.Hooks == 1,
                    "a kicker's rolled kick was not asked with the guard first (phase " + kicker.AiKickPhase + ", kind " + kicker.AiKickKind + ", hooks " + body.Hooks + ")");
                LogHas("[impact] first AI kick asked at 65000.0 s - (agent 6500) (bar 100%, no shield) on (agent 6501) in place of the swing he was winding up (step 81)");
                uint attackDown = (uint)Agent.MovementControlFlag.AttackDown, defendDown = (uint)Agent.MovementControlFlag.DefendDown, jump = (uint)Agent.EventControlFlag.Jump;
                uint ev = jump;
                uint f = Frame(kicker, attackDown | (uint)Agent.MovementControlFlag.Forward, ref ev, out float vx, out float vy);
                Check(ev == 0 && f == ((uint)Agent.MovementControlFlag.DefendUp | (uint)Agent.MovementControlFlag.Forward),
                    "the kick's guard frame: events " + ev + ", flags " + AiInputHook.FlagNames(f) + " (his attack out WITH the guard up - the wind-up cancelled, never released)");
                Tick(0.16); // the kick's guard (0.15 s) is over: the press
                Check(kicker.AiKickPhase == AiKickPhase.Press, "the kick's guard did not turn into the press after 0.15 s: " + kicker.AiKickPhase);
                ev = jump;
                f = Frame(kicker, attackDown | defendDown | (uint)Agent.MovementControlFlag.Forward, ref ev, out vx, out vy);
                Check(ev == (uint)Agent.EventControlFlag.Kick && f == (uint)Agent.MovementControlFlag.Forward && Math.Abs(vy - 1f) < 1e-6,
                    "the press frame: events " + ev + ", flags " + AiInputHook.FlagNames(f) + " (the kick key, his attack / guard out, still moving)");
                Tick(0.16); // the press is over: the commit
                Check(kicker.AiKickPhase == AiKickPhase.Commit, "the press did not turn into the commit: " + kicker.AiKickPhase);
                ev = jump;
                f = Frame(kicker, attackDown | (uint)Agent.MovementControlFlag.Forward, ref ev, out vx, out vy);
                Check(ev == 0 && f == 0 && vx == 0 && vy == 0, "the commit frame: events " + ev + ", flags " + AiInputHook.FlagNames(f) + ", vector " + vx + " / " + vy + " (nothing of his own, standing still)");
                // the engine plays the kick: step 18's poll sees it on channel 0 - charged there (a kick's cost), seen by the attempt
                logic.ObserveLowerAction(kicker, KickBashTracker.ActionKick, t, Rules);
                Tick(1.0 / 30);
                Check(kicker.AiKickSeen && st.PlayedCount(AiKickKind.Kick) == 1 && kicker.Fraction < before && st.PointsSpent > 0,
                    "the kick played was not seen / charged: seen " + kicker.AiKickSeen + ", played " + st.PlayedCount(AiKickKind.Kick) + ", bar " + kicker.Fraction + ", points " + st.PointsSpent);
                LogHas("[impact] first AI kick PLAYED at ");
                logic.AiKickHit(kicker, false);
                logic.AiKickHit(kicker, false); // once an attempt
                Check(st.LandedCount(AiKickKind.Kick) == 1, "the kick's hit not counted once: " + st.LandedCount(AiKickKind.Kick));
                LogHas("[impact] first AI kick LANDED at ");
                logic.ObserveLowerAction(kicker, 35, t, Rules); // the action is over
                Tick(1.0 / 30);
                Check(kicker.AiKickPhase == AiKickPhase.None && kicker.Input.KickPhase == AiKickPhase.None && st.Ends[(int)AiKickEnd.Played] == 1 && body.Releases == 1,
                    "the played kick did not end: phase " + kicker.AiKickPhase + ", ends " + st.Ends[(int)AiKickEnd.Played] + ", releases " + body.Releases);
                Decide(kicker);
                Check(st.Skips[(int)AiKickSkip.Cooldown] == 1 && kicker.AiKickPhase == AiKickPhase.None, "the cooldown did not refuse the next kick (he swings)");

                // ---- 2. a shieldman's rolled BASH: the guard up 0.4 s first, then the key + the guard; a WeaponBash seen
                logic.AiKickDice = new FixedDice(0.3); // past the kick share (20), under kick + bash (55): a bash
                var basher = Man(6502, 100);
                body.Shield.Add(6502);
                body.Targets[6502] = (Man(6503, 100), 1.0f, 0f);
                Decide(basher);
                Check(basher.AiKickKind == AiKickKind.Bash && basher.AiKickPhase == AiKickPhase.Guard, "a shieldman's rolled bash was not asked with the guard first: " + basher.AiKickKind + " / " + basher.AiKickPhase);
                ev = jump;
                f = Frame(basher, attackDown, ref ev, out vx, out vy);
                Check(ev == 0 && f == (uint)Agent.MovementControlFlag.DefendUp, "the bash's lead frame: events " + ev + ", flags " + AiInputHook.FlagNames(f));
                Tick(0.41);
                Check(basher.AiKickPhase == AiKickPhase.Press, "the bash's lead did not turn into the press after 0.4 s: " + basher.AiKickPhase);
                ev = 0;
                f = Frame(basher, 0, ref ev, out vx, out vy);
                Check(ev == (uint)Agent.EventControlFlag.Kick && f == (uint)Agent.MovementControlFlag.DefendUp, "the bash's press: the kick key with the guard up - events " + ev + ", flags " + AiInputHook.FlagNames(f));
                logic.ObserveAction(basher, KickBashTracker.ActionWeaponBash, t, Rules);
                Tick(1.0 / 30);
                Check(st.PlayedCount(AiKickKind.Bash) == 1 && st.PlayedOtherKind == 0, "the bash played not counted as a bash");
                logic.ObserveAction(basher, 35, t, Rules);
                Tick(0.2);
                Tick(0.2);
                Check(basher.AiKickPhase == AiKickPhase.None, "the bash did not end once over: " + basher.AiKickPhase);

                // ---- 3. a shieldman's rolled bash into a RAISED shield turned toward him becomes a kick (a bash would be stopped)
                var wallman = Man(6504, 100);
                body.Shield.Add(6504);
                var turtle = Man(6505, 100);
                turtle.PrevAction = ImpactActions.DefendShield;
                body.FacingToward[6505] = 20f;
                body.Targets[6504] = (turtle, 1.0f, 0f);
                Decide(wallman);
                Check(wallman.AiKickKind == AiKickKind.Kick && wallman.AiKickPhase == AiKickPhase.Guard && st.RaisedShieldKicks == 1, "a shieldman's rolled bash into a raised shield was not kicked");

                // ---- 4. NOT PLAYED: nothing shows within the guard + the press + 1.5 s
                logic.AiKickDice = new FixedDice(0.0);
                var ignored = Man(6506, 100);
                body.Targets[6506] = (Man(6507, 100), 1.0f, 0f);
                Decide(ignored);
                for (int i = 0; i < 70; i++) Tick(1.0 / 30);
                Check(ignored.AiKickPhase == AiKickPhase.None && st.Ends[(int)AiKickEnd.NotPlayed] >= 1, "an attempt the engine never played did not end NOT played");
                LogHas("[impact] first AI kick NOT played at ");

                // ---- 5. not a move in reach (he swings or thrusts as the game wants) and the refusals (he swings instead), each counted
                int Skips(AiKickSkip s) => st.Skips[(int)s];
                var m6 = Man(6510, 100, 0.3);
                body.Targets[6510] = (Man(6511, 100), 1.0f, 0f);
                Decide(m6);
                Check(m6.AiKickPhase != AiKickPhase.None, "a man at 30% of his bar was not asked his rolled kick - the bar gate is gone (step 81)");
                var m7 = Man(6512, 100);
                body.Targets[6512] = (Man(6513, 100), 1.0f, 0f);
                SetBar(m7, 0.01); // one point: less than a kick's 8
                Decide(m7);
                Check(Skips(AiKickSkip.NoAthletics) == 1 && m7.AiKickPhase == AiKickPhase.None, "a man without the Athletics for it was asked a kick");
                SetBar(m7, 1.0);
                m7.Impact.Reel(t + 1, 2);
                Decide(m7);
                Check(Skips(AiKickSkip.Staggered) == 1, "a reeling man was asked a kick");
                m7.Impact.Clear();
                m7.PrevAction = 35;
                logic.DecideAiMove(m7, t, AiKickRules.From(S), Rules); // his wind-up already over when the tick looked
                Check(Skips(AiKickSkip.ReadyOver) == 1 && m7.AiKickPhase == AiKickPhase.None, "a man whose wind-up was over was asked a kick");
                body.Mounted.Add(6512);
                Decide(m7);
                Check(Skips(AiKickSkip.Mounted) == 1, "a rider was asked a kick");
                body.Mounted.Remove(6512);
                body.Routing.Add(6512);
                Decide(m7);
                Check(Skips(AiKickSkip.Routing) == 1, "a routing man was asked a kick");
                body.Routing.Remove(6512);
                body.Ranged.Add(6512);
                Decide(m7);
                Check(Skips(AiKickSkip.Ranged) == 1, "a man with a bow in hand was asked a kick");
                body.Ranged.Remove(6512);
                body.Targets[6512] = (body.Targets[6512].Target, 2.5f, 0f);
                Decide(m7);
                Check(Skips(AiKickSkip.OutOfReach) == 1 && m7.AiKickPhase == AiKickPhase.None, "an enemy 2.5 m off was kicked (beyond kick reach - a spearman keeps his ground)");
                body.Targets[6512] = (body.Targets[6512].Target, 1.0f, 70f);
                Decide(m7);
                Check(Skips(AiKickSkip.NotFacing) == 1, "an enemy 70° off his facing was kicked");
                body.Targets.Remove(6512);
                Decide(m7);
                Check(Skips(AiKickSkip.NoTarget) == 1 && m7.AiKickPhase == AiKickPhase.None, "a man with no target was asked a kick");
                var noSwing = Man(6514, 100);
                body.Targets[6514] = (Man(6516, 100), 1.0f, 0f);
                noSwing.Input = new AiInputState { HoldAttacks = true, HoldKicks = true };
                Decide(noSwing);
                Check(Skips(AiKickSkip.NoSwing) == 1, "a man held for his stored swings was asked a kick");
                body.NotAi.Add(6515);
                var you = Man(6515, 100);
                long looks = st.Looks;
                Decide(you);
                Check(st.Looks == looks, "a man the AI does not drive was rolled");

                // ---- 6. the die's SWING: nothing asked, his wind-up untouched, the move counted
                logic.AiKickDice = new FixedDice(0.9);
                var swinger = Man(6520, 100);
                body.Targets[6520] = (Man(6521, 100), 1.0f, 0f);
                int moves = st.Moves(1), asked = st.AskedCount(AiKickKind.Kick);
                Decide(swinger);
                Check(swinger.AiKickPhase == AiKickPhase.None && swinger.Input == null && st.Moves(1) == moves + 1 && st.AskedCount(AiKickKind.Kick) == asked && st.Rolled(1, AiKickKind.None) >= 1,
                    "the die's swing touched him or was not counted");

                // ---- 7. the switch / the master switch mid-attempt: ended at once, the input given back; the queue dropped
                logic.AiKickDice = new FixedDice(0.0);
                var tk = Man(6522, 100);
                body.Targets[6522] = (Man(6523, 100), 1.0f, 0f);
                Decide(tk);
                Check(tk.AiKickPhase != AiKickPhase.None, "precondition: no attempt");
                tk.PrevAction = ReadyMeleeAction;
                logic.NoteAiMeleeReady(swinger);
                Check(logic.AiMovesPending == 1, "a wind-up was not queued for the tick");
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.Mcm);
                Tick(1.0 / 30);
                Check(logic.AiKicksRunning == 0 && tk.Input!.KickPhase == AiKickPhase.None && st.Ends[(int)AiKickEnd.SwitchedOff] >= 1 && logic.AiMovesPending == 0 && !swinger.AiMoveQueued,
                    "AiKicksEnabled off did not end the running attempt / drop the queue");
                LogHas("[impact] the AI kicks switched off at ");
                logic.NoteAiMeleeReady(swinger);
                Check(logic.AiMovesPending == 0, "AiKicksEnabled off: a wind-up was queued");
                S.Set(SettingsSchema.AiKicksEnabled, true, SettingSources.Mcm);
                tk.AiKickNext = double.NegativeInfinity;
                Decide(tk);
                Check(tk.AiKickPhase != AiKickPhase.None, "back on: no attempt");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Tick(1.0 / 30);
                Check(logic.AiKicksRunning == 0 && tk.Input!.KickPhase == AiKickPhase.None, "the master switch off did not end the running attempt");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);

                // ---- 8. the summary
                logic.WriteAiKickSummary();
                LogHas("[summary] ai kicks (step 65 / 81): ON");
                LogHas("[summary] ai kicks: asked ");
                LogHas("[summary] ai kicks by man type (asked → played → landed / into a shield): shieldmen ");
                LogHas("not a move in reach - the enemy beyond kick reach 1, the enemy not in front 1, no enemy target 1, mounted 1, routing 1, a ranged weapon in hand 1");
                LogHas("[summary] ai kicks - shares of the melee moves in kick reach (step 81): shieldmen ");
                LogHas("refused by reason: staggered / down 1, no Athletics for it 1, no stored swing 1, cooldown 1");
                Console.WriteLine("        step 65 AI kicks: " + string.Join(" | ", st.SummaryLines(AiKickRules.From(S)).Skip(1).Take(4)));
            }
            finally
            {
                Step65CleanUp(keep);
            }
        }

        /// <summary>
        /// Step 81: THE COST of the decision point - 1000 AI men, every one winding up a swing about every 2 s with an enemy in reach (a third with a
        /// shield), seeded dice through the real tick: the queue, the rolls, the attempts and the contact samples, the time a tick and 0 bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiKicksInAClash()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                Step88Shares(); // step 88: the shipped shares - the men without a shield push too
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                const int many = 1000;
                var logic2 = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                var body2 = new FakeAiKickBody();
                logic2.AiKickBody = body2;
                logic2.AiKickDice = new SeededRandom(7);
                var all = new List<TrackedAgent>(many);
                for (int i = 0; i < many; i++)
                {
                    var m = logic2.Track(FakeAgent(20000 + i))!;
                    m.AthleticsSkill = 100;
                    m.PrevAction = m.PrevLowerAction = 35;
                    m.Input = new AiInputState(); // (his input state is made once, at his first hook - not the tick's cost)
                    if (i % 3 == 0) body2.Shield.Add(20000 + i);
                    all.Add(m);
                }
                for (int i = 0; i < many; i++) body2.Targets[20000 + i] = (all[(i + 1) % many], 1.2f, 10f);
                double t2 = 67000;
                int cursor = 0;
                void Tick()
                {
                    t2 += 1.0 / 60;
                    // ~1000 men / 2 s / 60 ticks = ~8 wind-ups a tick; a man in an attempt is skipped by the queue
                    for (int k = 0; k < 8; k++)
                    {
                        var m = all[cursor++ % many];
                        if (m.AiKickPhase != AiKickPhase.None) continue;
                        m.PrevAction = ReadyMeleeAction;
                        logic2.NoteAiMeleeReady(m);
                    }
                    logic2.TickAiKicks(t2);
                }
                for (int k = 0; k < 300; k++) Tick(); // 5 s: the first-time lines (asked, NOT played, refusals) written before the count
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 0; k < ticks; k++) Tick();
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double ms = sw.Elapsed.TotalMilliseconds / ticks;
                var st = logic2.AiKickStats;
                Console.WriteLine("        step 81 cost: " + many + " men winding up ~8 swings a tick, the AI kicks' tick " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms, " + allocated
                                  + " bytes over " + ticks + " ticks; " + logic2.AiKicksRunning + " attempts running, moves " + (st.Moves(0) + st.Moves(1)) + ", asked "
                                  + (st.AskedCount(AiKickKind.Kick) + st.AskedCount(AiKickKind.Bash) + st.AskedCount(AiKickKind.Push)) + " (pushes " + st.AskedCount(AiKickKind.Push) + "), " + body2.Reads + " engine reads");
                Check(st.Moves(0) + st.Moves(1) > 1000 && st.AskedCount(AiKickKind.Kick) > 0 && st.AskedCount(AiKickKind.Bash) > 0 && st.AskedCount(AiKickKind.Push) > 0,
                    "cost: the wind-ups were not rolled into kicks, bashes and pushes");
                Check(ms < 0.5, "cost: the AI kicks' tick took " + ms + " ms (budget 0.5)");
                Check(allocated < 16 * 1024, "cost: the AI kicks' tick allocates - " + allocated + " bytes");
            }
            finally
            {
                Step65CleanUp(keep);
            }
        }

        /// <summary>
        /// THE KICKED MAN BRACES through the real damage decorator (its knock-back and KnockDown questions over the game's
        /// CustomAgentApplyDamageModel) and the real logic with the stand-in body: a looter's kick on a FRESH legionary shieldman - braced: the
        /// game's knock-back REFUSED, no knockdown, his leftover reaction played at 5 x; a lucky kick: the knock-back kept, a short reel at 2.5 x,
        /// no fall, no shield; 200 seeded kicks (short ~1 in 8, 0 falls, 0 drops); a SPENT man: no brace, the knock-back kept; you braced (the
        /// red line) and your kick braced against (the purple line); KickBraceRefusesKnockBack off; the switch / the master switch; the summary.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void KickBraceThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step63On();
                AiKickDefaults();
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Check(S.KickBraceEnabled && S.KickBraceRefusesKnockBack, "the brace is not on by default");
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var sink = new FakeMessageSink();
                logic.MessageSink = sink;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var bs = logic.KickBraceStats;
                double t = 65500;
                logic.TickImpact(t);
                LogHas("[impact] mission start, the kicked man braces (step 65): ON - a kicked / bashed man on foot braces unless the kick's pressure x fails it");
                TrackedAgent Man(int index, int skill, double bar, double armour, int level, bool shield)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = skill;
                    SetBar(m, bar);
                    inputs.Kits[index] = (armour, level);
                    if (shield) inputs.Shields.Add(index);
                    return m;
                }
                bool KickBack(TrackedAgent a, TrackedAgent v, out bool down)
                {
                    t += 0.05;
                    logic.TickImpact(t); // a new blow (the pending slot of the last one let go)
                    var cd = KickLands();
                    var blow = new Blow(a.AgentIndex);
                    bool back = model.DecideAgentKnockedBackByBlow(a.Agent, v.Agent, in cd, null!, in blow); // the engine asks the knock-back first,
                    down = model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);       // then the knockdown,
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);                                        // then OnMeleeHit
                    return back;
                }
                var legionary = Man(6550, 130, 1.0, 110, 26, true);
                var looter = Man(6551, 30, 1.0, 8, 5, false);

                // ---- 1. BRACED: the die above the 13% failure - the knock-back refused, no fall, the reaction at 5 x
                dice.Next(0.9);
                int decidedBefore = logic.ImpactModelStats.FromDecorator;
                bool back = KickBack(looter, legionary, out bool down);
                var d = logic.LastImpact;
                Check(!back && !down && d.Braced && Math.Abs(d.BraceFail - 0.129) < 0.01 && Math.Abs(d.ReelFactor - 0.2) < 1e-6,
                    "a looter's kick on a fresh legionary not braced: knock-back " + back + ", down " + down + ", brace " + d.Brace + " (failure " + d.BraceFail + "), reel x" + d.ReelFactor);
                Check(logic.ImpactModelStats.FromDecorator == decidedBefore + 1, "the blow was decided more than once across the knock-back / knockdown / OnMeleeHit");
                Check(bs.KnockBackAsked == 1 && bs.KnockBackRefused == 1 && bs.Outcome(KickBraceOutcome.Braced) == 1, "the brace's counts: asked " + bs.KnockBackAsked + ", refused " + bs.KnockBackRefused);
                LogHas("[impact] live: first knock-back REFUSED for a braced man at ");
                LogHas("[impact] live: first kick / bash BRACED at ");
                int slows = body.Slows.Count;
                double after = PlayReaction(logic, body, legionary, ImpactActions.StrikeLight, t + 0.02, 0.2);
                var sped = body.Slows.Skip(slows).Where(x => x.Man == 6550).ToList();
                Check(sped.Count == 1 && Math.Abs(sped[0].Speed - 5f) < 1e-4 && bs.BracedSeen == 1 && bs.BracedLength.Count == 1,
                    "the braced man's leftover reaction was not played at 5 x and measured: " + string.Join(", ", sped) + "; seen " + bs.BracedSeen);
                LogHas("[impact] live: first cut reaction seen at ");
                LogHas("BRACED, played at speed 5.00)");
                Check(!legionary.Impact.Reeling(t + 0.1), "a braced man got an impact stagger on his record");

                // ---- 2. A LUCKY KICK: the die under the failure - the knock-back kept, a short reel at 2.5 x, no fall, no shield
                t = after;
                dice.Next(0.05);
                back = KickBack(looter, legionary, out down);
                d = logic.LastImpact;
                Check(back && !down && !d.Braced && d.Brace == KickBraceOutcome.ShortReel && Math.Abs(d.ReelFactor - 0.4) < 1e-6 && !d.ShieldDrop.Happens && logic.ImpactLiveDrops == 0,
                    "the lucky kick: knock-back " + back + ", down " + down + ", brace " + d.Brace + ", reel x" + d.ReelFactor + ", shield " + d.ShieldDrop.Happens);
                slows = body.Slows.Count;
                after = PlayReaction(logic, body, legionary, ImpactActions.StrikeKnockBack, t + 0.02, 0.45);
                sped = body.Slows.Skip(slows).Where(x => x.Man == 6550).ToList();
                Check(sped.Count == 1 && Math.Abs(sped[0].Speed - 2.5f) < 1e-4 && bs.ShortSeen == 1, "the short reel was not played at 2.5 x: " + string.Join(", ", sped));
                LogHas("[impact] live: first kick / bash SHORT REEL at ");

                // ---- 3. 200 kicks, seeded: about one in eight a short reel, never a fall or a dropped shield
                logic.ImpactDice = new SeededRandom(65);
                int shorts = 0, falls = 0, refused = bs.KnockBackRefused;
                for (int i = 0; i < 200; i++)
                {
                    back = KickBack(looter, legionary, out down);
                    if (logic.LastImpact.Brace == KickBraceOutcome.ShortReel) shorts++;
                    if (down) falls++;
                    t += 1.0; // (the watches run out between kicks)
                    logic.TickImpact(t);
                }
                Console.WriteLine("        step 65 the brace: 200 looter kicks on a fresh legionary - short reels " + shorts + " (" + (shorts / 2.0).ToString("0.0", CultureInfo.InvariantCulture)
                                  + "%), falls " + falls + ", shields queued " + logic.ImpactLiveDrops + ", knock-backs refused " + (bs.KnockBackRefused - refused));
                Check(shorts >= 10 && shorts <= 45 && falls == 0 && logic.ImpactLiveDrops == 0, "200 looter kicks on a fresh legionary: " + shorts + " short reels, " + falls + " falls");

                // ---- 4. a SPENT man (10% of his bar): no brace, the knock-back kept - step 50 in full
                var spent = Man(6552, 130, 0.1, 110, 26, true);
                logic.ImpactDice = dice;
                dice.Next(0.99, 0.99, 0.5); // no fall, no drop, the reel's roll at its centre
                back = KickBack(looter, spent, out down);
                d = logic.LastImpact;
                Check(back && d.BraceSpent && !d.Braced && d.ReelFactor > 1, "a spent man braced, or his knock-back went: " + back + ", spent " + d.BraceSpent + ", reel x" + d.ReelFactor);

                // ---- 5. YOU: braced against an AI's kick (red), and your kick braced against (purple)
                var playerMan = Man(6553, 130, 1.0, 110, 26, true);
                logic.SmokePlayer = playerMan.Agent;
                dice.Next(0.9);
                KickBack(looter, playerMan, out _);
                LogHas("[impact] YOU: (agent 6551)'s kick on you (your bar 100%");
                LogHas("BRACED (failure 13%)");
                logic.SmokePlayer = looter.Agent;
                dice.Next(0.9);
                KickBack(looter, legionary, out _);
                t += 2;
                logic.TickImpact(t);
                Check(sink.Lines.Any(l => l.Text == "You brace against (agent 6551)'s kick" && l.Hex == ImpactMessageMath.ReceivedHex)
                      && sink.Lines.Any(l => l.Text.Contains("braces against your kick") && l.Hex == ImpactMessageMath.DeliveredHex),
                    "the on-screen brace lines: " + string.Join(" | ", sink.Lines.Select(l => l.Text)));
                logic.SmokePlayer = null;

                // ---- 6. KickBraceRefusesKnockBack off: braced, the knock-back kept (counted)
                S.Set(SettingsSchema.KickBraceRefusesKnockBack, false, SettingSources.Mcm);
                dice.Next(0.9);
                back = KickBack(looter, legionary, out _);
                Check(back && logic.LastImpact.Braced && bs.KnockBackKeptBraceOff == 1, "KickBraceRefusesKnockBack off: knock-back " + back + ", kept " + bs.KnockBackKeptBraceOff);
                S.Set(SettingsSchema.KickBraceRefusesKnockBack, true, SettingSources.Mcm);

                // ---- 7. the switch off / the master switch off: the game's knock-back, nothing asked
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.Mcm);
                int asked = bs.KnockBackAsked;
                dice.Next(0.9, 0.99, 0.99, 0.5);
                back = KickBack(looter, legionary, out _);
                Check(back && bs.KnockBackAsked == asked && logic.LastImpact.Brace == KickBraceOutcome.NotAsked, "KickBraceEnabled off: the brace still asked");
                S.Set(SettingsSchema.KickBraceEnabled, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                back = KickBack(looter, legionary, out _);
                Check(back && bs.KnockBackAsked == asked, "the master switch off: the knock-back seam still asked the brace");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);

                // ---- 8. the summary
                logic.WriteKickBraceSummary();
                LogHas("[summary] kick brace (step 65): ");
                LogHas("[summary] kick brace by the kicked man's tier (toughness): ");
                LogHas("heavy (4-7) ");
                LogHas("[summary] kick brace in the game: the knock-back seam asked ");
                LogHas("[summary] kick brace - YOU: kicked 1 (braced 1");
                Console.WriteLine("        step 65 the brace: " + string.Join(" | ", bs.SummaryLines(KickBraceRules.From(S)).Skip(1).Take(1)));
            }
            finally
            {
                Step65CleanUp(keep);
            }
        }

        /// <summary>The master-switch step's part: an AI kick running and the brace on - off = the attempt ended and its input given back, the
        /// game's knock-back; on = asked again.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiKicksFollowTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                AiKickDefaults();
                var body = new FakeAiKickBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.AiKickBody = body;
                logic.AiKickDice = new FixedDice(0.0);
                var a = logic.Track(FakeAgent(6590))!;
                var v = logic.Track(FakeAgent(6591))!;
                a.AthleticsSkill = v.AthleticsSkill = 100;
                a.PrevAction = a.PrevLowerAction = v.PrevAction = v.PrevLowerAction = 35;
                body.Targets[6590] = (v, 1.0f, 0f);
                DecideAt(logic, a, 65900);
                Check(a.AiKickPhase != AiKickPhase.None, "precondition: no AI kick asked (step 65)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickAiKicks(65900.1);
                Check(logic.AiKicksRunning == 0 && a.Input!.KickPhase == AiKickPhase.None, "mod off: the AI kick still running (step 65)");
                Check(logic.KnockBackKept(a.Agent, v.Agent, KickLands()), "mod off: the brace refused a knock-back (step 65)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                a.AiKickNext = double.NegativeInfinity;
                DecideAt(logic, a, 65901);
                Check(a.AiKickPhase != AiKickPhase.None, "mod back on: no AI kick asked (step 65)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                ImpactHooks.KnockBack = null;
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }
    }
}
