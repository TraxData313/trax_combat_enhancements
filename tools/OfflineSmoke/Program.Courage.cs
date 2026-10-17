using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 49 (Anton, 2026-10-17 night): COURAGE AND COHESION through the real logic with a stand-in world - men placed on a plane, the
    /// courage circle, formation centres and the flock computed from their positions (AI_NOTES "Step 49").
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeCourageMan
        {
            public TrackedAgent St = null!;
            public double X, Y;
            public int Team;
            public int Formation;
            public double Armour;
            public int Level;
            public bool Heavy;
            public double Health = 1;
            public double Morale = 50;
            public CourageSkip Gate = CourageSkip.Count;
        }

        private sealed class FakeCourageBody : ICourageBody
        {
            public readonly List<FakeCourageMan> Men = new List<FakeCourageMan>();
            public readonly Dictionary<int, FakeCourageMan> ByIndex = new Dictionary<int, FakeCourageMan>();
            public bool Allows = true;
            public bool Edge;
            public int Tightened, Restored, Hooks, Releases, KitReads, Scans;

            public FakeCourageMan Add(TrackedAgent st, double x, double y, int team, int formation, double armour, int level, bool heavy = false)
            {
                var m = new FakeCourageMan { St = st, X = x, Y = y, Team = team, Formation = formation, Armour = armour, Level = level, Heavy = heavy };
                Men.Add(m);
                ByIndex[st.AgentIndex] = m;
                return m;
            }

            public bool MissionAllows() => Allows;

            public bool IsActive(TrackedAgent st) => ByIndex.ContainsKey(st.AgentIndex);

            public CourageSkip Gate(TrackedAgent st) => ByIndex.TryGetValue(st.AgentIndex, out var m) ? m.Gate : CourageSkip.Count;

            public void ReadKit(TrackedAgent st, out double armour, out int level, out bool heavy)
            {
                KitReads++;
                var m = ByIndex[st.AgentIndex];
                armour = m.Armour;
                level = m.Level;
                heavy = m.Heavy;
            }

            public double HealthShare(TrackedAgent st) => ByIndex[st.AgentIndex].Health;

            public double Morale(TrackedAgent st) => ByIndex[st.AgentIndex].Morale;

            public bool Position(TrackedAgent st, out double x, out double y)
            {
                var m = ByIndex[st.AgentIndex];
                x = m.X;
                y = m.Y;
                return true;
            }

            public bool FormationCentre(TrackedAgent st, out double x, out double y)
            {
                int f = ByIndex[st.AgentIndex].Formation;
                double sx = 0, sy = 0;
                int n = 0;
                for (int i = 0; i < Men.Count; i++)
                {
                    var m = Men[i];
                    if (m.Formation != f) continue;
                    sx += m.X;
                    sy += m.Y;
                    n++;
                }
                x = n > 0 ? sx / n : double.NaN;
                y = n > 0 ? sy / n : double.NaN;
                return n > 0;
            }

            public void Scan(TrackedAgent st, float radius, CourageScan into)
            {
                Scans++;
                into.Clear();
                var me = ByIndex[st.AgentIndex];
                for (int i = 0; i < Men.Count; i++)
                {
                    var m = Men[i];
                    if (ReferenceEquals(m, me)) continue;
                    double dx = m.X - me.X, dy = m.Y - me.Y;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > radius) continue;
                    if (!into.Add(m.St, (float)d, (float)m.X, (float)m.Y, m.Team != me.Team)) break;
                }
            }

            public int Flock(TrackedAgent st, float radius, out double x, out double y)
            {
                var me = ByIndex[st.AgentIndex];
                double sx = 0, sy = 0;
                int n = 0;
                for (int i = 0; i < Men.Count; i++)
                {
                    var m = Men[i];
                    if (ReferenceEquals(m, me) || m.Team != me.Team) continue;
                    double dx = m.X - me.X, dy = m.Y - me.Y;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    sx += m.X;
                    sy += m.Y;
                    n++;
                }
                x = n > 0 ? sx / n : double.NaN;
                y = n > 0 ? sy / n : double.NaN;
                return n;
            }

            /// <summary>Every stand-in man faces +y (north): his frame is the world's - the local vector IS the world direction.</summary>
            public bool ToLocal(TrackedAgent st, double dx, double dy, out float lx, out float ly)
            {
                lx = (float)dx;
                ly = (float)dy;
                return true;
            }

            public bool GroundOk(TrackedAgent st, double dx, double dy) => !Edge;

            public void Tighten(TrackedAgent st, double reach) => Tightened++;

            public void Restore(TrackedAgent st) => Restored++;

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                Hooks++;
                return AiInputHook.HookResult.FirstHookTurnedOn;
            }

            public void Release(TrackedAgent st) => Releases++;
        }

        /// <summary>Step 49: every logic built from here on plays courage's engine side through a stand-in (the game's reads native agents).</summary>
        private static void UseFakeCourageBody()
        {
            Check(CourageBodyDefault.Current is GameCourageBody, "the default courage body is not the game's: " + CourageBodyDefault.Current.GetType().Name);
            CourageBodyDefault.Current = new FakeCourageBody { Allows = false };
            Check(new AthleticsLogic().CourageBody == CourageBodyDefault.Current, "a new logic does not start with the default courage body");
        }

        private static void CourageDefaults()
        {
            S.Set(SettingsSchema.CourageEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.CourageRadiusMetres, 6.0, SettingSources.File);
            S.Set(SettingsSchema.CourageBaseOddsPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.CourageArmourWeightPercent, 200, SettingSources.File);
            S.Set(SettingsSchema.CourageLevelWeightPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.CourageHealthWeightPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.CourageHeavyWeaponPercent, 150, SettingSources.File);
            S.Set(SettingsSchema.CourageTiredWeightPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.CourageMoraleWeightPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.CourageShockSeconds, 6.0, SettingSources.File);
            S.Set(SettingsSchema.CourageShockWeightPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.CourageFeverPercent, 40, SettingSources.File);
            S.Set(SettingsSchema.CourageHeroPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.CourageHysteresisPercent, 25, SettingSources.File);
            S.Set(SettingsSchema.CourageMinTimidSeconds, 2.0, SettingSources.File);
            S.Set(SettingsSchema.CourageRegroupRadiusMetres, 5.0, SettingSources.File);
            S.Set(SettingsSchema.CourageRegroupMates, 4, SettingSources.File);
            S.Set(SettingsSchema.CourageFlockRadiusMetres, 20.0, SettingSources.File);
            S.Set(SettingsSchema.CourageFlockMinMen, 6, SettingSources.File);
            S.Set(SettingsSchema.CourageSprintBack, true, SettingSources.File);
            S.Set(SettingsSchema.CourageSprintMetres, 12.0, SettingSources.File);
            S.Set(SettingsSchema.CourageSprintEnemies, 3, SettingSources.File);
            S.Set(SettingsSchema.CourageSprintKeepPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.CouragePanicOddsPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.CouragePanicSprintKeepPercent, 0, SettingSources.File);
            S.Set(SettingsSchema.CourageTightenWeights, true, SettingSources.File);
            S.Set(SettingsSchema.CourageTimidMeleeMetres, 2.0, SettingSources.File);
        }

        private const double LooterArmour = 8, LegionaryArmour = 110;
        private const int LooterLevel = 5, LegionaryLevel = 26;

        private static AthleticsLogic NewCourageLogic(FakeCourageBody body)
        {
            var logic = NewSprintLogic(new FakeSprintBody());
            logic.CourageBody = body;
            return logic;
        }

        /// <summary>One full round of looks (everyone once - the tick caps its catch-up at one poll interval) at <paramref name="t"/>.</summary>
        private static void CourageRound(AthleticsLogic logic, double t) => logic.TickCourage(t);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CourageThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                CourageDefaults();
                var body = new FakeCourageBody();
                var logic = NewCourageLogic(body);
                LogHas("[courage] mission start: courage: each AI man on foot in a formation weighs his side against the enemy within 6.0 m");
                LogHas("a looter (armour 8, level 5) 1.35, an imperial legionary (armour 110, level 26) 5.97 = 4.4 looters");
                double t = 20000;
                int idx = 6000;
                TrackedAgent Man(double x, double y, int team, int formation, bool legionary, bool heavy = false)
                {
                    var st = logic.Track(FakeAgent(idx++))!;
                    st.AthleticsSkill = 100; // sprint store 100 / 20 = 5 s
                    body.Add(st, x, y, team, formation, legionary ? LegionaryArmour : LooterArmour, legionary ? LegionaryLevel : LooterLevel, heavy);
                    return st;
                }

                // ---- A LONE LOOTER near five metal men, his formation 15 m behind him: timid, walks back facing them, sprints (far)
                var lone = Man(0, 0, 1, 1, legionary: false);
                for (int i = 0; i < 6; i++) Man(-2 + i * 0.8, -16 + (i % 2), 1, 1, legionary: false); // his mates around (0, -15.5); the centre (with him) ~(0, -13.3)
                for (int i = 0; i < 5; i++) Man(-2 + i, 2, 2, 2, legionary: true);                    // the metal wall 2 m north of him
                CourageRound(logic, t += 0.5);
                var cm = lone.Courage;
                Check(cm.Phase == CouragePhase.Moving, "the lone looter is not walking back: " + cm.Phase + " odds " + cm.Odds + " required " + cm.Required);
                Check(Math.Abs(cm.Odds - 1.3533 / (5 * 5.9733)) < 0.01, "his local odds are not 1.35 / (5 x 5.97) = 0.045: " + cm.Odds);
                Check(Near(cm.Required, 1.0), "a fresh, steady looter's required odds are not 1.0: " + cm.Required);
                Check(cm.FlockTarget && logic.CourageStats.WalksToGroup == 1, "his 6 mates within 20 m are the nearest bigger group of his side - his walk does not go to them");
                var input = lone.Input!;
                Check(input.Regroup && Math.Abs(input.RegroupX) < 0.05 && input.RegroupY < -0.99, "the walk's vector is not straight back (0, -1) in his frame (facing the wall): "
                    + input.RegroupX + ", " + input.RegroupY);
                uint flags = 0x1u; // Forward - his own AI wanted to walk at them
                float vx = 0f, vy = 1f;
                AiInputHook.Apply(lone, input, true, ref flags, ref vx, ref vy);
                Check((flags & 0xFu) == 0 && Math.Abs(vy + 1f) < 0.01f, "the input frame did not replace his forward walk with the walk back: flags " + flags + " vy " + vy);
                Check(lone.Sprint.Active && lone.Sprint.Regroup && Math.Abs(lone.Sprint.Length - 2.5) < 1e-6, "13+ m from the centre: no sprint back of the 50% above the reserve (2.5 s of 5): "
                    + lone.Sprint.Active + " " + lone.Sprint.Length);
                Check(body.Tightened >= 1 && cm.WeightsSet, "the timid man's behaviour weights were not set");
                LogHas("[courage] first walk back this battle: (agent 6000) at ");
                LogHas(" = odds 0.05 < required 1.00 (base 1.00 x tired 1.00 x morale 1.00 x shock 1.00 (0 hits in 6.0 s) x fever 1.00 x hero 1.00); ");
                // he walks back 3 m/s along the vector; the steering keeps writing it every tick
                var me = body.ByIndex[lone.AgentIndex];
                int tightBefore = body.Tightened;
                for (int k = 0; k < 40 && cm.Phase == CouragePhase.Moving; k++)
                {
                    me.X += input.RegroupX * 3 * 0.1;
                    me.Y += input.RegroupY * 3 * 0.1;
                    logic.TickCourage(t += 0.1);
                }
                Check(cm.Phase == CouragePhase.Holding && !input.Regroup, "the walk did not end near his mates: " + cm.Phase);
                Check(logic.CourageStats.RegroupEnded(RegroupEnd.Arrived) + logic.CourageStats.RegroupEnded(RegroupEnd.AmongMates) == 1, "the walk's end not counted as arrived / among mates");
                Check(!lone.Sprint.Active, "the sprint back did not end with the walk");
                Check(body.Tightened > tightBefore && logic.CourageStats.WeightsReapplied > 0, "the weights were not re-applied at his looks (the game resets them on every order)");
                LogHas("[courage] first walk back ended at ");
                // brave again once the odds are back and 2 s have passed (no enemy within 6 m now)
                for (int k = 0; k < 6; k++) CourageRound(logic, t += 0.5);
                Check(!cm.Timid && !cm.WeightsSet && body.Restored >= 1, "back among his mates with no enemy near: not brave again (or the game's weights not given back)");

                // ---- A BRAVE MASS: a looter among 20 mates vs 2 metal men = 21 x 1.35 / 11.9 = 2.4 - brave, never timid
                double ox = 200;
                var massMan = Man(ox, 0, 3, 3, legionary: false);
                for (int i = 0; i < 20; i++) Man(ox - 2.5 + (i % 5) * 1.2, -2.5 + (i / 5) * 1.2, 3, 3, legionary: false);
                Man(ox, 2.5, 4, 4, legionary: true);
                Man(ox + 1, 2.5, 4, 4, legionary: true);
                CourageRound(logic, t += 0.5);
                Check(!massMan.Courage.Timid && massMan.Courage.Odds > 1.5, "a looter in a mass of 21 vs 2 metal men is timid: odds " + massMan.Courage.Odds);

                // ---- FEVER: a looter + 1 mate vs 1 metal man (odds 0.45): timid calm, brave in a fever (required 0.4)
                ox = 400;
                var hot = Man(ox, 0, 5, 5, legionary: false);
                Man(ox + 0.5, -0.5, 5, 5, legionary: false);
                Man(ox, 2, 6, 6, legionary: true);
                hot.Fever.Arm(FeverRules.From(S), SprintOrder.Charge, 100, t, false);
                CourageRound(logic, t += 0.5);
                Check(!hot.Courage.Timid && Near(hot.Courage.Required, 0.4), "an armed fever: required not 0.4 or the man timid at odds 0.45: " + hot.Courage.Required + " / " + hot.Courage.Odds);
                hot.Fever.Stop();
                CourageRound(logic, t += 0.5);
                Check(hot.Courage.Phase == CouragePhase.Holding, "the fever over: odds 0.45 < 1 not timid (holding - his mate's centre is close): " + hot.Courage.Phase);

                // ---- THE WALL: a man held in a shield wall (step 41) is never looked at; a rider; the player
                ox = 600;
                var waller = Man(ox, 0, 7, 7, legionary: false);
                waller.Wall = new WallState { Held = true };
                var rider = Man(ox + 1, 0, 7, 7, legionary: false);
                body.ByIndex[rider.AgentIndex].Gate = CourageSkip.Mounted;
                for (int i = 0; i < 4; i++) Man(ox - 1 + i, 2, 8, 8, legionary: true);
                CourageRound(logic, t += 0.5);
                Check(!waller.Courage.Timid && logic.CourageStats.Skipped(CourageSkip.WallHeld) >= 1, "a man held in the shield wall was looked at / made timid");
                Check(!rider.Courage.Timid && logic.CourageStats.Skipped(CourageSkip.Mounted) >= 1, "a rider was made timid");

                // ---- SHOCK + HEAVY + PANIC: a legionary alone, 8 m from his formation, two enemy legionaries with great axes (x1.5), one hit
                // on him: odds 5.97 / (2 x 5.97 x 1.5) = 0.33; required 1 x (1 + 1/3) = 1.33; panic below 0.67 → he sprints at once keeping 0%
                ox = 800;
                var shaken = Man(ox, 0, 9, 9, legionary: true);
                for (int i = 0; i < 3; i++) Man(ox - 1 + i, -10, 9, 9, legionary: true); // his formation: centre ~(ox, -7.5)
                Man(ox - 0.7, 2, 10, 10, legionary: true, heavy: true);
                Man(ox + 0.7, 2, 10, 10, legionary: true, heavy: true);
                shaken.Hits.Note(t); // a landed hit (OnAgentHit)
                CourageRound(logic, t += 0.5);
                var sm = shaken.Courage;
                Check(Math.Abs(sm.Odds - 1.0 / 3) < 0.01, "two great-axe legionaries vs one: the odds are not 1 / (2 x 1.5) = 0.33: " + sm.Odds);
                Check(Math.Abs(sm.Required - 4.0 / 3) < 0.01, "one hit in the shock window: required not 1.33: " + sm.Required);
                Check(sm.Phase == CouragePhase.Moving && sm.ShockStart && sm.Panic, "shocked and outmatched: not a PANIC walk back: " + sm.Phase + " panic " + sm.Panic);
                Check(shaken.Sprint.Active && Math.Abs(shaken.Sprint.Length - 5.0) < 1e-6, "panic: the sprint does not spend everything (5 s, keeping 0%): " + shaken.Sprint.Length);
                Check(logic.CourageStats.TimidByShock >= 1 && logic.CourageStats.PanicSprints == 1, "the shock / panic counts are off");
                LogHas("[courage] first PANIC this battle: (agent ");
                LogHas("keeping only 0% of his store");
                // the hit ages out of the window: the shock is gone after 6 s
                Check(shaken.Hits.Recent(t + 6.1, 6) == 0 && shaken.Hits.Recent(t, 6) == 1, "the shock window does not end after 6 s");

                // ---- FLOCKING: a man whose formation is 40 m away, a bigger group of his side (8 men) 12 m off: he walks to the group
                ox = 1000;
                var stray = Man(ox, 0, 11, 11, legionary: false);
                for (int i = 0; i < 4; i++) Man(ox + i, -40, 11, 11, legionary: false);       // his own formation far away
                for (int i = 0; i < 8; i++) Man(ox - 12 + (i % 3), -2 + (i / 3), 11, 12, legionary: false); // another formation of his side, 12 m west
                Man(ox, 2, 13, 13, legionary: true);
                Man(ox + 1, 2, 13, 13, legionary: true);
                int toGroup = logic.CourageStats.WalksToGroup;
                CourageRound(logic, t += 0.5);
                Check(stray.Courage.Phase == CouragePhase.Moving && stray.Courage.FlockTarget && logic.CourageStats.WalksToGroup == toGroup + 1,
                    "the stray does not walk to the nearest bigger group of his side: " + stray.Courage.Phase + " flock " + stray.Courage.FlockTarget);
                Check(stray.Input!.RegroupX < -0.5, "the walk does not head west to the group: " + stray.Input.RegroupX);

                // ---- the switch: CourageEnabled off mid-walk = everyone let go at once, the game's weights back; the looks keep measuring
                int restored = body.Restored;
                S.Set(SettingsSchema.CourageEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[courage] settings changed mid-mission at ");
                logic.TickCourage(t += 0.1);
                Check(logic.TimidNow == 0 && logic.WalkingNow == 0 && !stray.Input.Regroup && body.Restored > restored, "CourageEnabled off: not let go at once");
                LogHas("[courage] CourageEnabled switched OFF mid-mission at ");
                CourageRound(logic, t += 0.5);
                CourageRound(logic, t += 0.5);
                Check(logic.CourageStats.ManSeconds(false) > 0 && logic.CourageStats.DistanceLooks(false) > 0, "the OFF looks did not measure");
                Check(logic.CourageStats.IsolatedSeconds(false) > 0, "OFF: the lone men among enemies were not counted isolated");
                S.Set(SettingsSchema.CourageEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);

                // ---- Athletics off: courage still works (tiredness 0) - the walk's frame through the Athletics-off path
                var s2 = stray.Input;
                s2.Regroup = true;
                s2.RegroupX = 0.6f;
                s2.RegroupY = -0.8f;
                uint f2 = 0x1u;
                float x2 = 0f, y2 = 1f;
                Check(AiInputHook.ApplyRegroup(s2, ref f2, ref x2, ref y2) && (f2 & 0xFu) == 0 && Near(x2, 0.6f), "the regroup frame (Athletics off path) did not write the walk");
                s2.Regroup = false;
                S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
                double req = CourageMath.Required(CourageRules.From(S), 0.0, 50, 0, false, out _);
                Check(Near(req, 1.0), "Athletics off: an empty bar still raised the required odds: " + req);
                S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);

                // ---- the summary
                logic.WriteCourageSummary();
                LogHas("[summary] courage settings: courage: each AI man on foot");
                LogHas("[summary] courage isolated (fewer than 2 mates within 6.0 m while an enemy is within it): ON ");
                LogHas("[summary] courage distance from the formation's centre (its men's centre of mass, every look): ON mean ");
                LogHas("[summary] courage timid: ");
                LogHas("[summary] courage regroup: walks back ended ");
                LogHas("[summary] courage flocking and panic: walks to the nearest bigger group of their side (6+ allies within 20 m) ");
                LogHas("[summary] courage by local odds, in contact (an enemy within 6.0 m, courage on): < 0.25: ");
                LogHas("[summary] courage toughness (per man, read once): min 1.35, mean ");
                LogHas("[summary] courage cost: ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>Step 49: the master switch off = every timid man let go at once (the game's weights back); back on = looked at again.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CourageFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                CourageDefaults();
                var body = new FakeCourageBody();
                var logic = NewCourageLogic(body);
                double t = 21000;
                var lone = logic.Track(FakeAgent(6900))!;
                body.Add(lone, 0, 0, 1, 1, LooterArmour, LooterLevel);
                var mate = logic.Track(FakeAgent(6901))!;
                body.Add(mate, 0, -15, 1, 1, LooterArmour, LooterLevel);
                for (int i = 0; i < 3; i++) body.Add(logic.Track(FakeAgent(6902 + i))!, i, 2, 2, 2, LegionaryArmour, LegionaryLevel);
                logic.TickCourage(t += 0.5);
                Check(lone.Courage.Timid && logic.WalkingNow == 1, "precondition: the lone looter is not walking back");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.02);
                Check(!lone.Courage.Timid && logic.WalkingNow == 0 && !lone.Input!.Regroup && body.Restored == 1, "mod off: the timid man was not let go at once (step 49)");
                LogHas("[courage] the whole mod (ModEnabled) switched OFF mid-mission at ");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                Check(lone.Courage.Timid, "mod back on: the lone looter is not looked at again (step 49)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CourageCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                CourageDefaults();
                var body = new FakeCourageBody();
                var logic = NewCourageLogic(body);
                const int men = 1000;
                double t0 = 22000;
                // two blocks of 500 face to face, 1.2 m apart in each, the lines 1.5 m apart - every man has ~20-30 within 6 m
                for (int i = 0; i < men; i++)
                {
                    var st = logic.Track(FakeAgent(9000 + i))!;
                    st.AthleticsSkill = 100;
                    bool south = i < men / 2;
                    int j = south ? i : i - men / 2;
                    double x = (j % 50) * 1.2, y = (j / 50) * 1.2;
                    body.Add(st, x, south ? -0.75 - y : 0.75 + y, south ? 1 : 2, south ? 1 : 2, south ? LooterArmour : LegionaryArmour, south ? LooterLevel : LegionaryLevel);
                }
                for (int k = 0; k < 4; k++) logic.TickCourage(t0 += 0.5); // everyone looked at (kits read, the first lines written)
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickCourage(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                Console.WriteLine("        step 49 cost: " + men + " men in two blocks (" + logic.TimidNow + " timid, " + logic.WalkingNow + " walking back) - the courage tick "
                                  + msPerTick.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms (the stand-in's scan is a plain loop over all 1000), "
                                  + allocated + " bytes over " + ticks + " ticks");
                Check(msPerTick < 2.0, "cost: the courage tick over " + men + " men took " + msPerTick + " ms (budget 2 ms with the stand-in's O(n) scan)");
                Check(allocated < 256 * 1024, "cost: the courage tick allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.TimidNow > 0, "cost: the looters facing a metal block are not timid");
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
