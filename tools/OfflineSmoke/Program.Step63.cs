using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 63 - FALLS ON THE HIT, A STRONG REAR BLOW, THE WALL PULLS MEN BACK (AI_NOTES "Step 63"). Anton's battle of
    /// 2026-10-19 11:02: 198 falls decided, 71 played by the game, 126 by our backup 0.53 s later - every one of the 126 on a 0-DAMAGE blow:
    /// a FRIEND's (the engine cancels a friend's damage and a 0-damage blow plays no reaction). Through the real shrug-off hook, the real
    /// decorator's KnockDown seam and the real logic with the stand-in body.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FallsOnTheHit()
        {
            var keep = _logic;
            float thresholdKeep = ManagedParameters.GetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut);
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step63On();
                Check(Math.Abs(TraxSettings.Shared.ImpactFallBackupSeconds - 0.2) < 1e-6, "ImpactFallBackupSeconds is not 0.2 by default: " + TraxSettings.Shared.ImpactFallBackupSeconds);
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, 5f);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var game = new CustomAgentApplyDamageModel();
                var falls = logic.ImpactFallStats;
                double t = 63000;
                logic.TickImpact(t);

                TrackedAgent Man(int index, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, bar);
                    return m;
                }
                (bool Kept, bool Down) Blow(TrackedAgent a, TrackedAgent v, in AttackCollisionData cd)
                {
                    var blow = new Blow(a.AgentIndex);
                    bool kept = ImpactHooks.ShrugOffKept(v.Agent, in cd, in blow, game);
                    if (kept) blow.BlowFlag |= BlowFlags.ShrugOff;
                    bool down = model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                    return (kept, down);
                }
                void Tick(double seconds) => TickFor(logic, ref t, seconds);
                var sword = Man(6300, 1.0);
                inputs.Weapons[6300] = (1.2, 92, ImpactDamageType.Cut, 125);

                // ---- 1. a FRIEND's blow (the rear rank's swing into his own front man): the engine cancels its damage (0) - never decided, no
                // fall, no stagger, no backup; the game's own shrug-off answer stands
                var front = Man(6301, 0.0);
                inputs.NotEnemies = true;
                int decisions = logic.ImpactModelStats.Decisions;
                dice.Next(0.0, 0.0, 0.5);
                var friendly = CleanHitDealing(0);
                var r1 = Blow(sword, front, in friendly);
                Check(!r1.Down && logic.ImpactModelStats.Decisions == decisions, "a friend's blow was decided: down " + r1.Down + ", decisions +" + (logic.ImpactModelStats.Decisions - decisions));
                Check(falls.Friendly == 1 && logic.ImpactModelStats.Skipped[(int)ImpactSkip.NotEnemies] >= 1 && falls.Total(falls.Decided) == 0,
                    "a friend's blow not counted: friendly " + falls.Friendly + ", skipped " + logic.ImpactModelStats.Skipped[(int)ImpactSkip.NotEnemies]);
                Tick(1.0);
                Check(body.OwnBlows.Count == 0, "a backup blow on a man hit by a friend: " + string.Join(", ", body.OwnBlows));
                inputs.NotEnemies = false;

                // ---- 2. an ENEMY's blow the engine plays at once: played by the game, no backup
                var hit = Man(6302, 0.0);
                dice.Next(0.0, 0.5);
                var light = CleanHitDealing(3);
                Check(Blow(sword, hit, in light).Down, "an enemy's decided fall not answered");
                t = PlayReaction(logic, body, hit, ImpactActions.Fall, t + 1.0 / 60, 1.5, index: 6302);
                Check(falls.PlayedByGame[1] == 1 && body.OwnBlows.Count == 0, "the game's own Fall not counted (or a backup sent): game " + falls.PlayedByGame[1]);

                // ---- 3. a fall the engine does NOT play: our blow after 0.2 s (not 0.5), once
                var lost = Man(6303, 0.0);
                dice.Next(0.0, 0.5);
                Check(Blow(sword, lost, in light).Down, "the third fall not answered");
                Tick(0.15);
                Check(body.OwnBlows.Count == 0, "the backup came before its 0.2 s");
                Tick(0.1);
                Check(body.OwnBlows.Count == 1 && falls.BackupsSent == 1, "no backup blow 0.25 s after the blow: " + string.Join(", ", body.OwnBlows));
                t = PlayReaction(logic, body, lost, ImpactActions.Fall, t, 1.5, index: 6303);
                Check(falls.PlayedByBackup[1] == 1 && falls.BackupDelay.Mean < 0.3, "the backup's Fall: " + falls.PlayedByBackup[1] + " after " + falls.BackupDelay.Mean + " s");

                logic.WriteImpactSummary();
                LogHas("a FRIEND's blow (step 63 - the game cancels its damage, nothing plays) 1");
                LogHas("because a FRIEND struck them (step 63 - the game cancels a friend's damage: nothing to play) 1");
                LogHas("backups (ImpactFallBackupSeconds 0.2 s) sent 1");
                Console.WriteLine("        step 63 falls: a friend's blow decided " + (falls.Friendly == 1 ? "never" : "?") + "; enemy falls decided " + falls.Total(falls.Decided)
                                  + " - played by the game " + falls.Total(falls.PlayedByGame) + " (" + falls.GameDelay.Mean.ToString("0.00") + " s), by the backup "
                                  + falls.Total(falls.PlayedByBackup) + " (" + falls.BackupDelay.Mean.ToString("0.00") + " s after the blow)");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactHooks.ShrugOff = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, thresholdKeep);
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>
        /// THE WALL PULLS MEN BACK (step 63) through the REAL logic with step 41's stand-in body: men thrown forward out of their slots (a
        /// fall from an impact, a shove), the game's own flag modelled as a slow walk back (<paramref name="gameSpeed"/>), our walk back
        /// through the real <see cref="AiInputHook.ApplyWallPull"/> at 1.2 m/s.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallPullsMenBack()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                S.Set(SettingsSchema.ShieldWallStepOutMetres, 1.0, SettingSources.File);
                S.Set(SettingsSchema.ShieldWallPullBackSeconds, 0.5, SettingSources.File);
                Check(Math.Abs(SettingsSchema.ShieldWallPullBackSeconds.Default - 0.5) < 1e-6, "ShieldWallPullBackSeconds is not 0.5 by default: " + SettingsSchema.ShieldWallPullBackSeconds.Default);
                var body = new FakeWallBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                var pull = logic.WallStats.Pull;
                LogHas("step 63: a held man more than 1.25 m ahead of his slot (beyond the leash - thrown forward by a blow, pushed by the press) is looked at every 0.1 s and held again at each look, and walked back to within 0.50 m");
                double t = 16300;
                const double gameSpeed = 0.0; // the worst case: the game's flag does not move him (in contact, fighting where he stands)
                const double ourSpeed = 1.2;
                var men = new Dictionary<int, TrackedAgent>();
                var outs = new Dictionary<int, double>();
                void Man(int i, double outM)
                {
                    men[i] = logic.Track(FakeAgent(i))!;
                    outs[i] = outM;
                    body.Facts[i] = WallMan(ahead: outM);
                    body.Contact.Add(i);
                }
                void Frame()
                {
                    t += 1.0 / 60;
                    logic.TickWall(t);
                    foreach (var kv in men)
                    {
                        var st = kv.Value;
                        int i = kv.Key;
                        double o = outs[i];
                        if (!st.Impact.Down(t) && !st.Impact.Reeling(t))
                        {
                            uint flags = 0;
                            float vx = 0, vy = 0;
                            if (st.Input != null && AiInputHook.ApplyWallPull(st.Input, ref flags, ref vx, ref vy)) o += vy * ourSpeed / 60; // vy < 0: back toward his slot
                            else if (o > 0) o = Math.Max(0, o - gameSpeed / 60);
                        }
                        outs[i] = o;
                        body.Facts[i] = WallMan(ahead: o);
                    }
                }
                void Run(double seconds)
                {
                    double end = t + seconds;
                    while (t < end) Frame();
                }
                Man(6600, 0.1);
                Man(6601, 0.1);
                Man(6602, 0.1);
                Run(0.6); // everyone held

                // ---- 1. a man thrown 2 m forward by a FALL (an impact outcome on his record): out of place at once, looked at every 0.1 s, never
                // walked while down, walked back 0.5 s after he is up, the wish off and his callback released once he is back
                var thrown = men[6600];
                thrown.Impact.KnockedDown(t, t + 2.5, 5);
                thrown.Impact.NoteOutcome(t);
                outs[6600] = 2.0;
                int outLooks = logic.WallStats.Writes(WallWriteKind.OutOfPlace);
                double thrownAt = t;
                Run(1.0);
                Check(pull.Spells == 1 && pull.SpellsAfterImpact == 1 && thrown.Wall!.Out.Out, "the thrown man not out of place after an impact: spells " + pull.Spells + " / " + pull.SpellsAfterImpact);
                int looks1s = logic.WallStats.Writes(WallWriteKind.OutOfPlace) - outLooks;
                Check(looks1s >= 5 && looks1s <= 11, "the out-of-place look is not every 0.1 s: " + looks1s + " in ~1 s (the spell begins at the next 0.25 s look)");
                Check(thrown.Input == null || !thrown.Input.WallPull, "walked while on the ground");
                Run(1.9); // up at 2.5 s, the 0.5 s wait - not yet
                Check(thrown.Input == null || !thrown.Input.WallPull, "walked back before 0.5 s of being able to move? (" + F2s(t - thrownAt) + " s)");
                Run(0.4);
                Check(thrown.Input != null && thrown.Input.WallPull && thrown.Input.WallPullY < 0 && body.Hooks >= 1, "not walked back 0.5 s after he got up");
                LogHas("[wall] first man WALKED BACK to his slot (step 63): ");
                Run(2.0);
                Check(!thrown.Wall!.Out.Out && !thrown.Input!.WallPull && body.InputReleases >= 1 && outs[6600] <= 0.5,
                    "the thrown man not back / the wish still on: out " + F2s(outs[6600]) + ", wish " + thrown.Input!.WallPull + ", releases " + body.InputReleases);
                Check(pull.Ends(WallOutEnd.Back) == 1 && pull.BackAfterImpact.Count == 1 && pull.BackPulled.Count == 1,
                    "the time back not counted: " + pull.SummaryLine(0.5, 1.0));
                double backAfterFall = pull.BackAfterImpact.Mean;
                LogHas("[wall] first man back in place (step 63): ");

                // ---- 2. ShieldWallPullBackSeconds 0: the game's flag alone - no extra look, no walk; set back live: walked at once
                S.Set(SettingsSchema.ShieldWallPullBackSeconds, 0.0, SettingSources.Mcm);
                outs[6601] = 1.6; // a shove: no impact on him
                outLooks = logic.WallStats.Writes(WallWriteKind.OutOfPlace);
                int hooks = body.Hooks;
                Run(2.0);
                Check(men[6601].Wall!.Out.Out && !men[6601].Wall!.Out.AfterImpact && logic.WallStats.Writes(WallWriteKind.OutOfPlace) == outLooks && body.Hooks == hooks,
                    "pull 0: extra looks " + (logic.WallStats.Writes(WallWriteKind.OutOfPlace) - outLooks) + ", hooks " + (body.Hooks - hooks) + " (both 0)");
                S.Set(SettingsSchema.ShieldWallPullBackSeconds, 0.5, SettingSources.Mcm);
                Run(0.4);
                Check(men[6601].Input != null && men[6601].Input!.WallPull, "the pull set back to 0.5 live: not walked back");

                // ---- 3. a man reined by the leash just past it (1.1 m) is not out of place
                outs[6602] = 1.1;
                Run(0.5);
                Check(!men[6602].Wall!.Out.Out, "a man just past the leash counted out of place");
                outs[6602] = 0.1;

                // ---- 4. the master switch mid-walk: the wish off at once, his callback released, the spell let go
                int releases = body.InputReleases;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Run(0.1);
                Check(!men[6601].Input!.WallPull && body.InputReleases > releases && pull.Ends(WallOutEnd.LetGo) >= 1, "the master switch left the walk back on");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Run(0.3);
                logic.WriteWallSummary();
                LogHas("[summary] wall pulled back (step 63 - a held man more than 1.25 m ahead of his slot; ShieldWallPullBackSeconds 0.5 s): ");
                LogHas("THE TIME BACK - after an impact ");
                LogHas(", by the out-of-place look (step 63) ");

                // ---- 5. A / B in the stand-in world: 20 men in contact, each thrown 2 m forward by a 2.5 s fall every 10 s (staggered), the game's
                // flag a slow 0.5 m/s walk back - the in-contact share more than 1 m ahead with the walk back off vs on
                const double gameFlag = 0.5;
                double Share(double pullSeconds, out double meanBack)
                {
                    S.Set(SettingsSchema.ShieldWallPullBackSeconds, pullSeconds, SettingSources.File);
                    var b = new FakeWallBody();
                    var lg = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                    lg.WallBody = b;
                    var ms = new List<TrackedAgent>();
                    var o = new double[20];
                    for (int k = 0; k < 20; k++)
                    {
                        var st = lg.Track(FakeAgent(6700 + k))!;
                        ms.Add(st);
                        b.Facts[6700 + k] = WallMan(ahead: 0.1);
                        b.Contact.Add(6700 + k);
                        o[k] = 0.1;
                    }
                    double tt = 17000;
                    for (int frame = 0; frame < 90 * 60; frame++)
                    {
                        tt += 1.0 / 60;
                        lg.TickWall(tt);
                        for (int k = 0; k < 20; k++)
                        {
                            var st = ms[k];
                            if (frame % 600 == k * 30) // each man thrown every 10 s
                            {
                                st.Impact.KnockedDown(tt, tt + 2.5, 0);
                                st.Impact.NoteOutcome(tt);
                                o[k] = 2.0;
                            }
                            if (!st.Impact.Down(tt))
                            {
                                uint fl = 0;
                                float vx = 0, vy = 0;
                                if (st.Input != null && AiInputHook.ApplyWallPull(st.Input, ref fl, ref vx, ref vy)) o[k] = Math.Max(0, o[k] + vy * ourSpeed / 60);
                                else if (o[k] > 0.1) o[k] = Math.Max(0.1, o[k] - gameFlag / 60);
                            }
                            b.Facts[6700 + k] = WallMan(ahead: o[k]);
                        }
                    }
                    meanBack = lg.WallStats.Pull.BackAfterImpact.Mean;
                    return lg.WallStats.On.ContactShareNear;
                }
                double off = Share(0, out double backOff);
                double on = Share(0.5, out double backOn);
                Console.WriteLine("        step 63 the wall pulls men back: a man thrown 2 m by a 2.5 s fall back in " + F2s(backAfterFall) + " s (the game's flag modelled stuck); A/B, 20 men thrown every 10 s, the game's flag 0.5 m/s - in contact more than 1 m ahead "
                                  + (100 * off).ToString("0.0", CultureInfo.InvariantCulture) + "% with the walk back off (back after an impact in " + F2s(backOff) + " s) vs "
                                  + (100 * on).ToString("0.0", CultureInfo.InvariantCulture) + "% on (" + F2s(backOn) + " s)");
                Check(on < off && backOn < backOff, "the walk back did not shorten the time out: " + off + " → " + on + ", " + backOff + " → " + backOn + " s");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                WallDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>
        /// A STRONG REAR BLOW (step 63) on a line, 200 men each, seeded, through the real decorator's KnockDown seam and the real logic: even
        /// FRESH men struck from the front / the side / behind (the front 0 - two fresh even men -, behind a real share down); a legionary
        /// standing behind fresh peasant archers (bow in hand) vs in front of them; the decision line's "+0.50 pressure"; the direction line.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RearBlowOnALine()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step56BlockModel();
                Step63On();
                Check(Math.Abs(TraxSettings.Shared.ImpactRearPressure - 0.5) < 1e-6 && Math.Abs(TraxSettings.Shared.ImpactFlankPressure - 0.2) < 1e-6
                      && TraxSettings.Shared.ImpactRearPercent == 100 && TraxSettings.Shared.ImpactFlankPercent == 100,
                    "the embedded step-63 direction defaults: +" + TraxSettings.Shared.ImpactRearPressure + " / +" + TraxSettings.Shared.ImpactFlankPressure + ", x" + TraxSettings.Shared.ImpactRearPercent + " / x" + TraxSettings.Shared.ImpactFlankPercent);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(63);
                var model = SpikeDamageModel();
                double t = 63500;
                logic.TickImpact(t);
                int next = 63500;
                TrackedAgent Man(int skill, double bar, double armour, int level)
                {
                    var m = logic.Track(FakeAgent(next++))!;
                    m.AthleticsSkill = skill;
                    SetBar(m, bar);
                    if (armour >= 0) inputs.Kits[m.AgentIndex] = (armour, level);
                    return m;
                }
                var swordsman = Man(125, 1.0, -1, 0);
                inputs.Weapons[swordsman.AgentIndex] = (1.2, 92, ImpactDamageType.Cut, 125);
                var legionary = Man(130, 1.0, 110, 26);
                inputs.Weapons[legionary.AgentIndex] = (1.3, 92, ImpactDamageType.Cut, 130);
                inputs.Kits[swordsman.AgentIndex] = (40, 15);

                double chanceSum = 0;
                (double Down, double Staggered) Line(TrackedAgent a, double angle, bool archers)
                {
                    int down = 0, staggered = 0;
                    chanceSum = 0;
                    var cd = CleanHitDealing(20);
                    for (int i = 0; i < 200; i++)
                    {
                        var v = archers ? Man(40, 1.0, 15, 11) : Man(125, 1.0, 40, 15);
                        if (archers) inputs.RangedInHand.Add(v.AgentIndex);
                        inputs.Angles[v.AgentIndex] = angle;
                        var blow = new Blow(a.AgentIndex);
                        if (model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow)) down++;
                        logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                        if (logic.LastImpact.Stagger.Happens) staggered++;
                        chanceSum += logic.LastImpact.Knockdown.Chance;
                        if ((i & 7) == 7) logic.TickImpact(t += 1.0 / 60);
                    }
                    return (down / 200.0, staggered / 200.0);
                }
                var evenFront = Line(swordsman, 0, false);
                var evenSide = Line(swordsman, 90, false);
                var evenRear = Line(swordsman, 160, false);
                double evenRearChance = chanceSum / 200;
                var lastRear = logic.LastImpact;
                var archFront = Line(legionary, 0, true);
                var archRear = Line(legionary, 160, true);
                double archRearChance = chanceSum / 200;
                string P(double v) => (100 * v).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                Console.WriteLine("        step 63 A STRONG REAR BLOW (200 men each, seeded): even FRESH men - front down " + P(evenFront.Down) + " / staggered " + P(evenFront.Staggered)
                                  + ", side " + P(evenSide.Down) + " / " + P(evenSide.Staggered) + ", BEHIND " + P(evenRear.Down) + " / " + P(evenRear.Staggered) + " (the model's down "
                                  + P(evenRearChance) + ") | a legionary into fresh peasant archers - front down " + P(archFront.Down) + ", standing BEHIND " + P(archRear.Down)
                                  + " (the model's " + P(archRearChance) + ")");
                Check(evenFront.Down == 0 && evenFront.Staggered == 0, "two fresh even men from the front were not 0: " + P(evenFront.Down) + " / " + P(evenFront.Staggered));
                Check(evenSide.Down == 0 && evenSide.Staggered > 0 && evenSide.Staggered < 0.2, "the side on a fresh even man: " + P(evenSide.Down) + " / " + P(evenSide.Staggered));
                Check(evenRear.Down > 0.02 && evenRear.Down < 0.15 && evenRear.Staggered > 0.25, "behind a fresh even man: " + P(evenRear.Down) + " / " + P(evenRear.Staggered));
                Check(Math.Abs(lastRear.DirectionPart - 0.5) < 1e-6 && lastRear.Direction == 2 && Math.Abs(lastRear.DirectionFactor - 1) < 1e-9,
                    "the rear decision: pressure " + lastRear.DirectionPart + ", band " + lastRear.Direction + ", x" + lastRear.DirectionFactor);
                Check(archFront.Down < 0.05 && archRear.Down > 0.35, "a legionary behind fresh archers: front " + P(archFront.Down) + ", behind " + P(archRear.Down));
                logic.WriteImpactSummary();
                LogHas("from behind (> 120°) +0.50 pressure, from the side (60°+) +0.20");
                LogHas("| side 200 (x1.00 +0.20 pressure: down ");
                LogHas("| rear 400 (x1.00 +0.50 pressure: down ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactHooks.ShrugOff = null;
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }
    }
}
