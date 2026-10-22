using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 76 - THE AI PREFERS THE SPEAR + A THRUST PASSES A FRIEND (Toni 2026-10-22: "my shield wall-ers have spears but they keep
    /// using their swords, even though the spear has a longer reach"; AI_NOTES "Step 76"). The REAL logic's spear looks with a stand-in engine side
    /// (slots, the formation's rank, the enemies around each man, what he holds - a crude stand-in of the native AI's choice: a favoured man holds what
    /// is favoured, an unfavoured one draws his sidearm with an enemy within 5 m), the REAL stat decorator writing the favors, the REAL damage decorator
    /// over the game's CustomAgentApplyDamageModel answering a friend's thrust, the friendly-hit stun through the game's ManagedParameters. What only the
    /// game can tell: that the native AI honours the favors (holds the spear), that the engine's ContinueChecking lets the spear reach the enemy behind,
    /// whether the friend still flinches, whether the AI starts thrusts with a mate in front (PLAYTEST D41).
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeSpearBody : ISpearBody
        {
            public bool IsReady = true;
            public readonly Dictionary<int, SpearSlot[]> Slots = new Dictionary<int, SpearSlot[]>();
            public readonly Dictionary<int, int> Version = new Dictionary<int, int>();
            private readonly Dictionary<int, int> _read = new Dictionary<int, int>();
            public readonly Dictionary<int, SpearContext> Ctx = new Dictionary<int, SpearContext>();
            public readonly Dictionary<int, double[]> EnemyAt = new Dictionary<int, double[]>();   // distances
            public readonly Dictionary<int, bool[]> EnemyPolearm = new Dictionary<int, bool[]>();
            public readonly Dictionary<int, int> Wielded = new Dictionary<int, int>();
            public readonly HashSet<int> NotAi = new HashSet<int>();
            public int Hooks, Releases, Scans, LoadoutReads;

            public bool Ready() => IsReady;

            public bool Gate(TrackedAgent st) => !NotAi.Contains(st.AgentIndex);

            public bool ReadLoadout(TrackedAgent st, SpearSlot[] slots)
            {
                int id = st.AgentIndex;
                Version.TryGetValue(id, out int v);
                if (st.Spear.Read && _read.TryGetValue(id, out int r) && r == v) return false;
                _read[id] = v;
                var src = Slots[id];
                for (int i = 0; i < 4; i++) slots[i] = src[i];
                LoadoutReads++;
                return true;
            }

            public void Context(TrackedAgent st, out SpearContext c)
            {
                if (!Ctx.TryGetValue(st.AgentIndex, out c)) c = new SpearContext { Rank = -1, Arrangement = SpearArrangement.Other, Order = BraceOrderKind.NoFormation };
            }

            public void CloseEnemies(TrackedAgent st, double radius, out int other, out int polearm)
            {
                Scans++;
                other = polearm = 0;
                if (!EnemyAt.TryGetValue(st.AgentIndex, out var d)) return;
                var p = EnemyPolearm[st.AgentIndex];
                for (int i = 0; i < d.Length; i++)
                {
                    if (!(d[i] <= radius)) continue;
                    if (p[i]) polearm++;
                    else other++;
                }
            }

            public int WieldedSlot(TrackedAgent st) => Wielded.TryGetValue(st.AgentIndex, out int w) ? w : -1;

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                Hooks++;
                return AiInputHook.HookResult.AlreadyHooked;
            }

            public void Release(TrackedAgent st) => Releases++;

            public void Enemies(int id, params (double d, bool polearm)[] e)
            {
                EnemyAt[id] = e.Select(x => x.d).ToArray();
                EnemyPolearm[id] = e.Select(x => x.polearm).ToArray();
            }

            public double Nearest(int id) => EnemyAt.TryGetValue(id, out var d) && d.Length > 0 ? d.Min() : double.PositiveInfinity;
        }

        private sealed class FakeThrustReads : IThrustPassReads
        {
            public readonly HashSet<(int, int)> Friendly = new HashSet<(int, int)>();
            public bool PolearmInHand = true;

            public bool Friends(Agent attacker, Agent defender) => Friendly.Contains((attacker.Index, defender.Index)) || Friendly.Contains((defender.Index, attacker.Index));

            public bool Polearm(in MissionWeapon weapon) => PolearmInHand;
        }

        private static SpearSlot SmokeSpear(int cm) => new SpearSlot { Melee = true, Polearm = true, LengthCm = cm };
        private static SpearSlot SmokeSword() => new SpearSlot { Melee = true };
        private static SpearSlot SmokeShield() => new SpearSlot();
        private static SpearSlot SmokeJavelin() => new SpearSlot { Melee = true, Polearm = true, Throwable = true, LengthCm = 110 };
        private static SpearSlot SmokeEmpty() => new SpearSlot { Empty = true };

        private sealed class SpearWorld
        {
            public readonly AthleticsLogic Logic;
            public readonly FakeSpearBody Body;
            public readonly TraxAgentStatModel Top;
            public readonly List<TrackedAgent> Men = new List<TrackedAgent>();
            public double T;

            public SpearWorld(AthleticsLogic logic, FakeSpearBody body, TraxAgentStatModel top, double t)
            {
                Logic = logic;
                Body = body;
                Top = top;
                T = t;
            }

            public TrackedAgent Man(int id, SpearSlot[] slots, SpearContext ctx)
            {
                var st = Logic.Track(FakeAgent(id))!;
                Body.Slots[id] = slots;
                Body.Ctx[id] = ctx;
                Body.Wielded[id] = -1;
                Men.Add(st);
                return st;
            }

            /// <summary>A crude stand-in of the native AI's weapon choice, after each frame: a favoured man holds what is favoured; one without a favor
            /// draws his sidearm with an enemy within 5 m (Toni's complaint), else the polearm.</summary>
            public void Run(double seconds)
            {
                int frames = (int)Math.Round(seconds * 60);
                for (int k = 0; k < frames; k++)
                {
                    T += 1.0 / 60;
                    Logic.TickSpears(T);
                    foreach (var st in Men)
                    {
                        if (st.Spear.Read && st.Spear.Loadout.Both)
                        {
                            int side = Lowest(st.Spear.Loadout.SidearmMask);
                            bool wantSide = st.Spear.Favored ? st.Spear.Close : Body.Nearest(st.AgentIndex) <= 5;
                            Body.Wielded[st.AgentIndex] = wantSide ? side : st.Spear.Loadout.PolearmSlot;
                        }
                        st.SpeedDirty = false; // the tick's recompute (not run here - the decorator is asked directly)
                    }
                }
            }

            private static int Lowest(int mask)
            {
                for (int i = 0; i < 4; i++)
                    if ((mask & (1 << i)) != 0) return i;
                return -1;
            }

            /// <summary>The real decorator over the stand-in base model (which assigns the favors 1 / 1 / 1, like SetAiRelatedProperties).</summary>
            public (float polearm, float melee) Favors(TrackedAgent st)
            {
                var p = new AgentDrivenProperties();
                Top.UpdateAgentStats(st.Agent, p);
                return (p.AiWeaponFavorMultiplierPolearm, p.AiWeaponFavorMultiplierMelee);
            }
        }

        private static SpearContext FrontOfWall(int rank = 0, bool inFront = false, BraceOrderKind order = BraceOrderKind.Stop) =>
            new SpearContext { Arrangement = SpearArrangement.Wall, Order = order, Rank = rank, ManInFront = inFront };

        private static void Step76On()
        {
            foreach (var p in new[]
            {
                SettingsSchema.SpearPreferenceEnabled, SettingsSchema.SpearPolearmFavor, SettingsSchema.SpearSidearmFavor, SettingsSchema.SpearSidearmAfterSeconds,
                SettingsSchema.SpearPolearmBackAfterSeconds, SettingsSchema.SpearPolearmBackMountedSeconds, SettingsSchema.SpearRidingFastMetresPerSecond,
                SettingsSchema.SpearCloseFromLength, SettingsSchema.SpearCloseBaseMetres, SettingsSchema.SpearCloseLengthPercent, SettingsSchema.SpearCloseMinMetres,
                SettingsSchema.SpearCloseMaxMetres, SettingsSchema.SpearCloseFallbackMetres, SettingsSchema.SpearBackRankKeeps, SettingsSchema.SpearBackRankInStandingLine,
                SettingsSchema.SpearKeepVsPolearm, SettingsSchema.SpearLookSeconds, SettingsSchema.SpearWieldOverride, SettingsSchema.ThrustsPassFriends,
                SettingsSchema.ThrustsPassPolearmsOnly, SettingsSchema.FriendlyHitStunSeconds,
            })
                S.Set(p, p.Default, SettingSources.File);
        }

        private static TraxAgentStatModel SpearStatTop()
        {
            var baseModel = new FreshStatsModel();
            var top = new TraxAgentStatModel(new AgentStatCalculateModel[] { baseModel });
            top.Initialize(baseModel);
            return top;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SpearmenKeepTheirSpears()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                Step76On();
                Check(SpearMath.Wield0Bit == AiInputHook.Wield0EventBit, "Core's Wield0 bit " + SpearMath.Wield0Bit + " is not the engine's " + AiInputHook.Wield0EventBit);
                Check(ThrustPassMath.ThrustStrikeType == (int)StrikeType.Thrust, "Core's thrust strike type is not the engine's StrikeType.Thrust");
                var body = new FakeSpearBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.SpearBody = body;
                var ss = logic.SpearStats;
                LogHas("[spears] mission start, the AI prefers the spear (step 76): ON (SpearPreferenceEnabled) - an AI man (both sides, never you) carrying a melee polearm AND a melee sidearm favours the polearm x35");
                LogHas("(a 1.5 m spear 1.90, 2.0 m 2.20, 2.5 m 2.50, 3.0 m 2.80; a length not known 2.5 m)");
                var w = new SpearWorld(logic, body, SpearStatTop(), 18000);
                var front = w.Man(7600, new[] { SmokeSpear(200), SmokeSword(), SmokeShield(), SmokeEmpty() }, FrontOfWall());
                var second = w.Man(7601, new[] { SmokeSpear(200), SmokeSword(), SmokeShield(), SmokeEmpty() }, FrontOfWall(1, true));
                var thinned = w.Man(7602, new[] { SmokeSpear(200), SmokeSword(), SmokeShield(), SmokeEmpty() }, FrontOfWall(1, false));
                var facer = w.Man(7603, new[] { SmokeSpear(200), SmokeSword(), SmokeShield(), SmokeEmpty() }, FrontOfWall());
                var pikeman = w.Man(7604, new[] { SmokeSword(), SmokeSpear(300), SmokeEmpty(), SmokeEmpty() }, FrontOfWall());
                var shortSpear = w.Man(7605, new[] { SmokeSpear(150), SmokeSword(), SmokeEmpty(), SmokeEmpty() }, FrontOfWall());
                var skirmisher = w.Man(7606, new[] { SmokeJavelin(), SmokeSword(), SmokeShield(), SmokeEmpty() }, FrontOfWall());
                var me = w.Man(7607, new[] { SmokeSpear(200), SmokeSword(), SmokeShield(), SmokeEmpty() }, FrontOfWall());
                logic.SmokePlayer = me.Agent;
                body.Enemies(7600, (3.0, false));
                body.Enemies(7601, (1.0, false));
                body.Enemies(7602, (1.0, false));
                body.Enemies(7603, (1.5, true), (2.0, true));
                body.Enemies(7604, (2.6, false));   // a pike: radius 2.8 → close
                body.Enemies(7605, (2.0, false));   // a short spear: radius 1.9 → not close
                body.Enemies(7606, (1.0, false));
                body.Enemies(7607, (1.0, false));

                // ---- the first looks: every man with both weapons favours the polearm, the decorator writes x35
                w.Run(1.0);
                Check(front.Spear.Favored && !front.Spear.Close && w.Favors(front) == (35f, 1f), "the front man (nothing close) does not favour the polearm x35: " + w.Favors(front));
                Check(ss.MenBoth == 6 && ss.MenWithout == 1, "the men with both weapons are not counted 6 + 1 without (you never looked at): " + ss.MenBoth + " / " + ss.MenWithout);
                Check(!skirmisher.Spear.Favored && w.Favors(skirmisher) == (1f, 1f), "javelins + a sword counted as a spearman");
                Check(!me.Spear.Favored && w.Favors(me) == (1f, 1f), "you were given a favor");
                Check(ss.Flips(SpearFlip.Started) == 6, "not six men started on the polearm: " + ss.Flips(SpearFlip.Started));
                LogHas("[spears] first AI man with a polearm AND a sidearm this mission: (agent 7600) - his polearm in slot 0 (2.00 m), close radius 2.20 m");

                // ---- an enemy at 2.0 m in front of the front man: the sidearm after 2 s (not at once), the decorator writes Melee x55
                body.Enemies(7600, (2.0, false));
                w.Run(1.4);
                Check(!front.Spear.Close, "the front man drew his sidearm before 2 s");
                w.Run(2.0);
                Check(front.Spear.Close && w.Favors(front) == (1f, 55f), "the front man did not favour his sidearm x55 after 2 s close: " + w.Favors(front));
                Check(body.Wielded[7600] == 1, "the stand-in did not follow the favor");
                LogHas("[spears] first flip to the sidearm (an enemy close) this mission at ");
                // the enemy steps back: the polearm 4 s after the last close look
                body.Enemies(7600, (3.0, false));
                w.Run(3.0);
                Check(front.Spear.Close, "the front man took the polearm back before 4 s");
                w.Run(2.0);
                Check(!front.Spear.Close && w.Favors(front) == (35f, 1f), "the front man did not take the polearm back 4 s after the last close enemy");
                LogHas("[spears] first flip back to the polearm (nothing close) this mission at ");

                // ---- the back rank keeps the spear (an enemy at 1 m); a thinned file (nobody in front) does not
                Check(!second.Spear.Close && second.Spear.Condition == SpearCondition.BackRank && w.Favors(second) == (35f, 1f), "the second-rank man drew his sidearm");
                Check(thinned.Spear.Close, "a man in rank 1 with NOBODY in front kept the spear (he is the front)");
                LogHas("[spears] first back-rank man this mission: (agent 7601) - rank 1 of a Wall under Stop, a man of his in front: he keeps the polearm whatever is close");
                // the thinned file is filled: he is a back-rank man now - the polearm at once
                body.Ctx[7602] = FrontOfWall(1, true);
                w.Run(0.7);
                Check(!thinned.Spear.Close && ss.Flips(SpearFlip.ToPolearmBackRank) == 1, "a sidearm man stepped into a back rank and did not take the polearm at once");

                // ---- facing only polearms: keeps his; a swordsman joins them: the sidearm after 2 s
                Check(!facer.Spear.Close && facer.Spear.Condition == SpearCondition.FacingPolearm, "a man facing only polearms drew his sidearm");
                LogHas("[spears] first man facing only polearms this mission: (agent 7603) - 2 enemy polearm(s) within 2.20 m: he keeps his");
                body.Enemies(7603, (1.5, true), (1.8, false));
                w.Run(3.4);
                Check(facer.Spear.Close, "a swordsman among the spears did not make him draw the sidearm");

                // ---- the radius from the length: the pike (2.8 m) at 2.6 m is close, the short spear (1.9 m) at 2.0 m is not
                Check(pikeman.Spear.Close && pikeman.Spear.Loadout.PolearmSlot == 1, "the pikeman (radius 2.8 m) did not count an enemy at 2.6 m as close");
                Check(!shortSpear.Spear.Close && shortSpear.Spear.Condition == SpearCondition.NotClose, "the short spear (radius 1.9 m) counted an enemy at 2.0 m as close");
                S.Set(SettingsSchema.SpearCloseFromLength, false, SettingSources.Mcm); // RBM's 2.5 for every spear, live
                w.Run(3.4);
                Check(shortSpear.Spear.Close, "SpearCloseFromLength off: the short spear's enemy at 2.0 m (within RBM's 2.5) not close");
                LogHas("[spears] spear settings changed mid-mission at ");
                S.Set(SettingsSchema.SpearCloseFromLength, true, SettingSources.Mcm);

                // ---- a rider: riding through at 6 m/s nothing is close; bogged down at 2 m/s the sword after 2 s, the lance 2 s after
                var rider = w.Man(7608, new[] { SmokeSpear(250), SmokeSword(), SmokeEmpty(), SmokeEmpty() },
                    new SpearContext { Mounted = true, MountSpeed = 6, Rank = -1, Arrangement = SpearArrangement.Other, Order = BraceOrderKind.Charge });
                body.Enemies(7608, (1.5, false));
                w.Run(3.0);
                Check(!rider.Spear.Close && rider.Spear.Condition == SpearCondition.Riding, "a rider riding through at 6 m/s drew his sword");
                body.Ctx[7608] = new SpearContext { Mounted = true, MountSpeed = 2, Rank = -1, Arrangement = SpearArrangement.Other, Order = BraceOrderKind.Charge };
                w.Run(3.4);
                Check(rider.Spear.Close, "a bogged-down rider did not draw his sword");
                body.Enemies(7608, (9.0, false));
                w.Run(3.0);
                Check(!rider.Spear.Close, "the rider did not take his lance back within ~2 s");

                // ---- a dropped spear: the favor lifted; picked up again: back
                body.Slots[7605] = new[] { SmokeEmpty(), SmokeSword(), SmokeEmpty(), SmokeEmpty() };
                body.Version[7605] = 1;
                w.Run(0.7);
                Check(!shortSpear.Spear.Favored && w.Favors(shortSpear) == (1f, 1f), "a man who lost his spear keeps a favor");

                // ---- the wield fallback (off by default): on = a wish on the polearm men, a sidearm Wield turned into the polearm's
                Check(w.Men.All(m => m.Input == null || !m.Input.SpearWield), "the wield fallback ran while off");
                S.Set(SettingsSchema.SpearWieldOverride, true, SettingSources.Mcm);
                w.Run(0.7);
                Check(second.Input != null && second.Input.SpearWield && body.Hooks > 0, "the wield fallback did not wish the second-rank man's polearm");
                uint ev = 0x20u | 0x8000u; // Wield1 (his sword) + a kick
                Check(AiInputHook.ApplySpearWield(second.Input!, ref ev) && ev == (0x10u | 0x8000u), "the sidearm Wield was not turned into the polearm's: 0x" + ev.ToString("X"));
                Check(thinned.Input != null && thinned.Input.SpearWield, "the wield fallback missed the back-rank man");
                Check(facer.Input == null || !facer.Input.SpearWield, "a man favouring his sidearm was forced to the polearm");
                S.Set(SettingsSchema.SpearWieldOverride, false, SettingSources.Mcm);
                w.Run(0.1);
                Check(w.Men.All(m => m.Input == null || !m.Input.SpearWield) && body.Releases > 0, "the wield fallback's wishes not cleared when switched off");

                // ---- the switch off live: every favor lifted (the game's 1 / 1 / 1), the looks still measure; the master switch too
                S.Set(SettingsSchema.SpearPreferenceEnabled, false, SettingSources.Mcm);
                double offBefore = ss.SecondsAll(false);
                w.Run(2.0);
                Check(w.Men.All(m => !m.Spear.Favored) && w.Favors(front) == (1f, 1f) && w.Favors(second) == (1f, 1f), "SpearPreferenceEnabled off: favors still written");
                Check(ss.SecondsAll(false) > offBefore + 5, "off: the looks stopped measuring");
                LogHas("[spears] the spear preference OFF at ");
                S.Set(SettingsSchema.SpearPreferenceEnabled, true, SettingSources.Mcm);
                w.Run(1.0);
                Check(second.Spear.Favored && w.Favors(second) == (35f, 1f), "back on: the back-rank man not favoured again");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                w.Run(1.0);
                Check(w.Men.All(m => !m.Spear.Favored) && w.Favors(second) == (1f, 1f), "the master switch off: favors still written");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                w.Run(1.0);

                // ---- the summary
                ss.BackRankAttacks = 0;
                var lines = ss.SummaryLines(SpearRules.From(S));
                Console.WriteLine("        step 76 spears: " + lines[0].Substring(0, Math.Min(400, lines[0].Length)) + " ...");
                Check(lines[0].Contains("7 AI men carry a melee polearm AND a sidearm") || lines[0].Contains("8 AI men carry a melee polearm AND a sidearm"),
                    "the summary's men: " + lines[0]);
                Check(lines[0].Contains("a back rank ") && lines[0].Contains("facing only polearms ") && lines[0].Contains("riding through "), "the summary's time split lacks a condition");
                double brPole = ss.Seconds(true, SpearCondition.BackRank, SpearHeld.Polearm), brAll = ss.Seconds(true, SpearCondition.BackRank);
                Check(brPole >= 0.8 * brAll && brAll > 10, "the back-rank time is not spent holding the polearm: " + brPole + " of " + brAll + " s"); // the stand-in's choice lags one look after a switch
                Check(ss.FavorPolearm > 0 && ss.FavorSidearm > 0, "the favors written not counted");
                Check(body.LoadoutReads == 9, "the loadout re-read without a change (RBM's cache): " + body.LoadoutReads);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                AthleticsDefaults();
            }
        }

        private static AttackCollisionData Contact(bool thrust, bool alternative = false, bool shield = false) =>
            AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                shield, false, alternative, true, false, false, false, false, false, thrust, false, false,
                shield ? CombatCollisionResult.Blocked : CombatCollisionResult.StrikeAgent, 0, thrust ? 1 : 0, 2, 0, default(BoneBodyPartType), 0, default(Agent.UsageDirection), -1,
                default(CombatHitResultFlags), 0.5f, 0.5f, 0.4f, 0f, 0f, 0f, 0f, 0f,
                Vec3Zero, Vec3Zero, Vec3Zero, Vec3Zero, Vec3Zero, Vec3Zero, Vec3Zero);

        private static TaleWorlds.Library.Vec3 Vec3Zero => TaleWorlds.Library.Vec3.Zero;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrustsPassAFriend()
        {
            var keep = _logic;
            var keepReads = ThrustPass.Reads;
            try
            {
                AthleticsDefaults();
                Step76On();
                ThrustPass.RestoreStun(); // whatever an older step's logic held
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodAttackerFriendlyFire, 0.4f);
                var reads = new FakeThrustReads();
                ThrustPass.Reads = reads;
                var dm = new TraxDamageModel();
                dm.Initialize(new CustomAgentApplyDamageModel());
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                var body = new FakeSpearBody();
                logic.SpearBody = body;
                var ts = logic.ThrustStats;
                double t = 18500;
                logic.TickSpears(t);
                Check(Math.Abs(ThrustPass.StunNow - 0.1f) < 1e-6, "the friendly-hit stun is not held at 0.1 s: " + ThrustPass.StunNow);
                LogHas("[spears] the friendly-hit stun (StunPeriodAttackerFriendlyFire) 0.40 s → 0.10 s at ");

                var back = logic.Track(FakeAgent(7700))!;   // a second-rank spearman
                var mate = logic.Track(FakeAgent(7701))!;   // his front man
                var foe = logic.Track(FakeAgent(7702))!;    // the enemy behind the front man
                body.Slots[7700] = new[] { SmokeSpear(220), SmokeSword(), SmokeShield(), SmokeEmpty() };
                body.Ctx[7700] = FrontOfWall(1, true);
                body.Enemies(7700, (1.6, false));
                body.Slots[7701] = new[] { SmokeSpear(220), SmokeSword(), SmokeShield(), SmokeEmpty() };
                body.Ctx[7701] = FrontOfWall();
                body.Slots[7702] = new[] { SmokeSword(), SmokeShield(), SmokeEmpty(), SmokeEmpty() };
                reads.Friendly.Add((7700, 7701));
                logic.TickSpears(t += 0.6);
                var blow = default(Blow);

                // ---- his thrust touches his front man: carries on (the base model never asked)
                var thrustCd = Contact(thrust: true);
                dm.DecideWeaponCollisionReaction(in blow, in thrustCd, back.Agent, mate.Agent, in MissionWeapon.Invalid, false, false, 0f, out var reaction);
                Check(reaction == MeleeCollisionReaction.ContinueChecking, "a thrust into a friend did not carry on: " + reaction);
                Check(ts.Count(ThrustVerdict.Passed) == 1 && ts.PassedPolearm == 1 && ts.PassedBackRank == 1, "the pass not counted (polearm, a back rank)");
                LogHas("[spears] first THRUST past a friend this mission at ");
                // ... and then reaches the enemy behind within 0.5 s (the next collision of the same attack)
                logic.TickSpears(t += 0.1);
                Check(!ThrustPass.Passes(in thrustCd, back.Agent, foe.Agent, in MissionWeapon.Invalid, true), "an ENEMY's collision answered ContinueChecking");
                Check(ts.FollowedAny == 1 && ts.FollowedLanded == 1, "the enemy hit after the pass not counted");
                LogHas("[spears] first thrust past a friend that then HIT AN ENEMY this mission at ");
                // the friend passed flinches (a hit reaction on his upper channel) - counted
                mate.PrevAction = 49; // StrikeMedium
                logic.TickSpears(t += 0.05);
                Check(ts.FriendFlinched == 1, "the friend's flinch not seen");
                mate.PrevAction = 35;

                // ---- a swing into a friend still bounces (the base model's answer)
                var swingCd = Contact(thrust: false);
                dm.DecideWeaponCollisionReaction(in blow, in swingCd, back.Agent, mate.Agent, in MissionWeapon.Invalid, false, false, 0f, out reaction);
                Check(reaction == MeleeCollisionReaction.Bounced, "a SWING into a friend did not bounce: " + reaction);
                Check(ts.Count(ThrustVerdict.Swing) == 1, "the swing into a friend not counted");
                LogHas("[spears] first SWING into a friend this mission at ");
                // a kick / bash into a friend: the game's
                var kickCd = Contact(thrust: false, alternative: true);
                dm.DecideWeaponCollisionReaction(in blow, in kickCd, back.Agent, mate.Agent, in MissionWeapon.Invalid, false, false, 0f, out reaction);
                Check(reaction != MeleeCollisionReaction.ContinueChecking && ts.Count(ThrustVerdict.KickOrBash) == 1, "a kick into a friend passed");
                // a pass nobody follows: no reaction within 0.5 s = he did not flinch
                dm.DecideWeaponCollisionReaction(in blow, in thrustCd, back.Agent, mate.Agent, in MissionWeapon.Invalid, false, false, 0f, out reaction);
                logic.TickSpears(t += 0.6);
                Check(ts.FriendFine == 1 && ts.Count(ThrustVerdict.Passed) == 2, "the friend who did not flinch not counted");

                // ---- polearms only: a sword's thrust bounces, a spear's passes
                S.Set(SettingsSchema.ThrustsPassPolearmsOnly, true, SettingSources.Mcm);
                reads.PolearmInHand = false;
                dm.DecideWeaponCollisionReaction(in blow, in thrustCd, back.Agent, mate.Agent, in MissionWeapon.Invalid, false, false, 0f, out reaction);
                Check(reaction == MeleeCollisionReaction.Bounced && ts.Count(ThrustVerdict.NotPolearm) == 1, "polearms only: a sword's thrust passed");
                reads.PolearmInHand = true;
                dm.DecideWeaponCollisionReaction(in blow, in thrustCd, back.Agent, mate.Agent, in MissionWeapon.Invalid, false, false, 0f, out reaction);
                Check(reaction == MeleeCollisionReaction.ContinueChecking, "polearms only: a spear's thrust bounced");
                S.Set(SettingsSchema.ThrustsPassPolearmsOnly, false, SettingSources.Mcm);

                // ---- the switch off: the thrust bounces, the game's stun back; on again: ours; the master switch: off, the stun back
                S.Set(SettingsSchema.ThrustsPassFriends, false, SettingSources.Mcm);
                logic.TickSpears(t += 0.1);
                Check(Math.Abs(ThrustPass.StunNow - 0.4f) < 1e-6, "switched off: the game's 0.4 s stun not given back: " + ThrustPass.StunNow);
                dm.DecideWeaponCollisionReaction(in blow, in thrustCd, back.Agent, mate.Agent, in MissionWeapon.Invalid, false, false, 0f, out reaction);
                Check(reaction == MeleeCollisionReaction.Bounced && ts.Count(ThrustVerdict.SwitchedOff) == 1, "switched off: a thrust into a friend passed");
                LogHas("[spears] the friendly-hit stun given back at ");
                S.Set(SettingsSchema.ThrustsPassFriends, true, SettingSources.Mcm);
                logic.TickSpears(t += 0.1);
                Check(Math.Abs(ThrustPass.StunNow - 0.1f) < 1e-6, "on again: the stun not held");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickSpears(t += 0.1);
                dm.DecideWeaponCollisionReaction(in blow, in thrustCd, back.Agent, mate.Agent, in MissionWeapon.Invalid, false, false, 0f, out reaction);
                Check(reaction == MeleeCollisionReaction.Bounced && ts.Count(ThrustVerdict.ModOff) == 1 && Math.Abs(ThrustPass.StunNow - 0.4f) < 1e-6,
                    "the master switch off: the thrust passed or the stun not given back");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickSpears(t += 0.1);

                // ---- an enemy thrust is never touched by the rule (the base model answers)
                reads.Friendly.Clear();
                dm.DecideWeaponCollisionReaction(in blow, in thrustCd, back.Agent, foe.Agent, in MissionWeapon.Invalid, false, false, 0f, out reaction);
                Check(reaction == MeleeCollisionReaction.Bounced, "an enemy's 0-damage thrust was not the base model's Bounced: " + reaction);

                // ---- the summary gives the game's stun back
                logic.WriteSpearSummary();
                Check(Math.Abs(ThrustPass.StunNow - 0.4f) < 1e-6 && !ThrustPass.StunHeld, "the summary (the battle's end) did not give the game's stun back");
                LogHas("[summary] thrusts past friends (step 76 - ThrustsPassFriends ON now): friendly melee contacts ");
                LogHas(" | THEN AN ENEMY HIT by the same man within 0.5 s: 2 of 3 (67%; landed 1, blocked 0, no damage 1)"); // the last: the enemy's 0-damage thrust 0.1 s after the third pass
                LogHas("[summary] spears (step 76 - SpearPreferenceEnabled ON now");
            }
            finally
            {
                ThrustPass.Reads = keepReads;
                ThrustPass.RestoreStun();
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                AthleticsDefaults();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SpearsCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                Step76On();
                var body = new FakeSpearBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.SpearBody = body;
                const int men = 1000;
                double t0 = 19000;
                for (int i = 0; i < men; i++)
                {
                    int id = 10000 + i;
                    logic.Track(FakeAgent(id));
                    body.Slots[id] = i % 3 == 0
                        ? new[] { SmokeSword(), SmokeShield(), SmokeEmpty(), SmokeEmpty() }
                        : new[] { SmokeSpear(180 + i % 120), SmokeSword(), SmokeShield(), SmokeEmpty() };
                    body.Ctx[id] = FrontOfWall(i % 4, i % 4 > 0);
                    body.Enemies(id, (1.0 + (i % 5) * 0.6, i % 7 == 0));
                    body.Wielded[id] = 0;
                }
                for (int k = 0; k < 60; k++) logic.TickSpears(t0 + k / 60.0); // every loadout read once, every man started
                const int ticks = 600;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = new Stopwatch();
                for (int k = 60; k < 60 + ticks; k++)
                {
                    sw.Start();
                    logic.TickSpears(t0 + k / 60.0);
                    sw.Stop();
                }
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                var ss = logic.SpearStats;
                Console.WriteLine("        step 76 cost: " + men + " men (" + ss.MenBoth + " with a spear and a sidearm) - " + msPerTick.ToString("0.000", CultureInfo.InvariantCulture)
                                  + " ms a tick, " + allocated + " bytes allocated over " + ticks + " ticks, " + ss.Looks + " looks, " + body.Scans + " scans");
                Check(ss.MenBoth == 666, "cost: the spearmen not counted: " + ss.MenBoth);
                Check(ss.Looks > men * 10 * 0.8, "cost: the looks are not ~every 0.5 s: " + ss.Looks);
                Check(msPerTick < 0.5, "cost: the spear tick over " + men + " men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the spear looks allocate - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }
    }
}
