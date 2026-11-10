using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 109 - KEEP YOUR DISTANCE (Toni 2026-11-10, DESIGN decision 98), through the REAL logic (its look, its steer, its ends) and the
    /// REAL input frames (AiInputHook.Apply) in a small stand-in world: every man's own "AI" walks him at his target until 1 m (the game's tired man "comes
    /// as close as he can"), the frames move him (a full stick = 2 m/s). (1) a SWORDSMAN in his pause backs off after his quiet 2.5 s - 1.9 m (2 x 0.95),
    /// walking, facing - and HOLDS that gap against his own AI's walk in, then comes in when his pause ends; (2) a SPEARMAN backs off farther, capped at
    /// 4 m; (3) YOU step in on him: he stands (no back-off at once), backs off again only after another quiet wait; you swing: he is released and
    /// defends; (4) swings every 1.5 s keep a waiting man in (the quiet spells broken); (5) never a wall man, a rider, a routing man, a bowman, a resting
    /// man, you, a man who can attack, an enemy beyond reach; (6) the rest takes over; (7) a man in a line stops at the rest's limit behind his front;
    /// (8) the time limit; a new target; (9) the switches end every keep at once and give his input back; the OFF looks measure (the waiting distance ON
    /// vs OFF); (10) the input frame's order; (11) the [summary] line; (12) the cost with 1000 men.
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeKeepMan
        {
            public TrackedAgent St = null!;
            public double X, Y;
            public TrackedAgent? Target;
            public double Length = 0.95;
            public bool Spear, Mounted, Routing, Ranged, Active = true;

            /// <summary>The stand-in AI never moves him (an enemy who just stands there).</summary>
            public bool Still;
        }

        /// <summary>Step 109's stand-in: a flat field of men (index → place, kit, target); the AI's own target is <see cref="FakeKeepMan.Target"/>.</summary>
        private sealed class FakeKeepBody : IKeepBody
        {
            public readonly Dictionary<int, FakeKeepMan> Men = new Dictionary<int, FakeKeepMan>();
            public Agent? You;

            public bool IsActive(TrackedAgent st) => Men.TryGetValue(st.AgentIndex, out var m) && m.Active;

            public bool IsAiControlled(TrackedAgent st) => !ReferenceEquals(st.Agent, You);

            public void Kit(TrackedAgent st, out bool mounted, out bool routing, out bool ranged, out double lengthMetres, out bool spear)
            {
                var m = Men[st.AgentIndex];
                mounted = m.Mounted;
                routing = m.Routing;
                ranged = m.Ranged;
                lengthMetres = m.Length;
                spear = m.Spear;
            }

            public Agent? Target(TrackedAgent st, out double px, out double py, out double ex, out double ey)
            {
                px = py = ex = ey = double.NaN;
                if (!Men.TryGetValue(st.AgentIndex, out var m) || m.Target == null || !Men.TryGetValue(m.Target.AgentIndex, out var e) || !e.Active) return null;
                px = m.X;
                py = m.Y;
                ex = e.X;
                ey = e.Y;
                return e.St.Agent;
            }
        }

        private static void UseFakeKeepBody()
        {
            Check(KeepBodyDefault.Current is GameKeepBody, "the default keep body is not the game's: " + KeepBodyDefault.Current.GetType().Name);
            KeepBodyDefault.Current = new FakeKeepBody(); // knows nobody: an older check's man is never looked at past the cheap gates
            Check(new AthleticsLogic().KeepBody == KeepBodyDefault.Current, "a new logic does not start with the default keep body");
        }

        private static void KeepDistanceDefaults()
        {
            foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("KeepDistance", StringComparison.Ordinal))) S.Set(p, p.Default, SettingSources.File);
            S.Set(SettingsSchema.KeepDistanceEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.KeepDistanceTiredBelowPercent, 0, SettingSources.File); // step 109 exactly: its men stand at 40% (step 110's tired rule has Program.Step110.cs)
            S.Set(SettingsSchema.RestEnabled, false, SettingSources.File); // the rest is driven by courage's looks - here it is set by hand where it matters
            S.Set(SettingsSchema.RestMaxBackMetres, 4.0, SettingSources.File);
        }

        /// <summary>A small world: the logic, the stand-ins, the men and one frame of everybody (the stand-in AI, the real input filter, the move).</summary>
        private sealed class KeepWorld
        {
            public readonly AthleticsLogic Logic;
            public readonly FakeKeepBody Body = new FakeKeepBody();
            public double T = 109000;
            private int _next;
            public double MaxStick;

            /// <summary>The stand-in AI walks at its target until this close (the tired man who comes as close as he can).</summary>
            public double AiCloseMetres = 1.0;

            public KeepWorld(int first)
            {
                _next = first;
                Logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                Logic.CourageBody = new FakeCourageBody();
                Logic.KeepBody = Body;
                Logic.KeepDice = new FixedDice(0.5); // every quiet wait 2.5 s
            }

            public FakeKeepMan Man(double x, double y, double length = 0.95, bool spear = false)
            {
                int i = _next++;
                var st = Logic.Track(FakeAgent(i))!;
                SetBar(st, 0.4);
                var m = new FakeKeepMan { St = st, X = x, Y = y, Length = length, Spear = spear };
                Body.Men[i] = m;
                return m;
            }

            public static void Pause(FakeKeepMan m, bool on)
            {
                m.St.Pace ??= new PaceState();
                m.St.Pace.Active = on;
            }

            public double Gap(FakeKeepMan a, FakeKeepMan b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

            public void Frame()
            {
                const double dt = 1.0 / 60;
                Logic.TickKeep(T);
                foreach (var m in Body.Men.Values)
                {
                    if (!m.Active || m.Still || ReferenceEquals(m.St.Agent, Body.You)) continue;
                    // the stand-in AI: at its target until AiCloseMetres (the game's walk in)
                    float vx = 0f, vy = 0f;
                    if (m.Target != null && Body.Men.TryGetValue(m.Target.AgentIndex, out var e))
                    {
                        double dx = e.X - m.X, dy = e.Y - m.Y, d = Math.Sqrt(dx * dx + dy * dy);
                        if (d > AiCloseMetres + 0.05) { vx = (float)(dx / d); vy = (float)(dy / d); }
                    }
                    uint flags = 0u;
                    var s = m.St.Input;
                    if (s != null && s.Active) AiInputHook.Apply(m.St, s, true, ref flags, ref vx, ref vy);
                    if (s != null && s.Keep && s.KeepMove)
                    {
                        double k = Math.Sqrt(s.KeepX * s.KeepX + s.KeepY * s.KeepY);
                        if (k > MaxStick) MaxStick = k;
                    }
                    m.X += vx * 2.0 * dt;
                    m.Y += vy * 2.0 * dt;
                }
                T += dt;
            }

            public void Run(double seconds)
            {
                double end = T + seconds;
                while (T < end) Frame();
            }

            /// <summary>Runs until <paramref name="until"/> holds (at most <paramref name="max"/> s); the seconds it took (NaN = never).</summary>
            public double RunUntil(Func<bool> until, double max)
            {
                double t0 = T;
                while (T - t0 < max)
                {
                    Frame();
                    if (until()) return T - t0;
                }
                return double.NaN;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void KeepDistanceThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                KeepDistanceDefaults();
                var r0 = KeepDistanceRules.From(S);
                Check(r0.On && Math.Abs(r0.QuietMinSeconds - 2) < 1e-6 && Math.Abs(r0.QuietMaxSeconds - 3) < 1e-6 && Math.Abs(r0.WeaponLengths - 2) < 1e-6
                      && Math.Abs(r0.MaxBackMetres - 4) < 1e-6 && Math.Abs(r0.ReachMetres - 3) < 1e-6, "step 109's shipped numbers: " + r0.Describe());

                // ---- 1. A SWORDSMAN in his pause, in a duel with you (no formation)
                var w = new KeepWorld(10900);
                var you = w.Man(0, 0);
                w.Body.You = you.St.Agent;
                w.Logic.SmokePlayer = you.St.Agent;
                w.Logic.SmokePlayerAiControlled = false;
                var a = w.Man(0, 1.0);
                a.Target = you.St;
                you.Target = a.St;
                var ks = w.Logic.KeepStats;
                KeepWorld.Pause(a, true);
                a.St.LastReleaseTime = w.T; // he just swung: the quiet clock starts now
                double t0 = w.T;
                double began = w.RunUntil(() => a.St.Keep.Keeping, 5);
                Check(!double.IsNaN(began) && began >= 2.45 && began <= 2.8 && ks.Keeps == 1,
                    "the swordsman did not back off after his quiet 2.5 s: " + began + " s, keeps " + ks.Keeps);
                LogHas("first man KEEPS HIS DISTANCE");
                double held = w.RunUntil(() => a.St.Keep.Phase == KeepPhase.Holding, 4);
                double gapHeld = w.Gap(a, you);
                Check(!double.IsNaN(held) && gapHeld >= 2.7 && gapHeld <= 3.05, "the swordsman did not hold at ~2.9 m (1.0 + 2 x 0.95): " + gapHeld + " m after " + held + " s");
                Check(w.MaxStick <= KeepDistanceMath.WalkStick + 1e-6 && w.MaxStick > 0.3, "the back-off was not a walk: stick " + w.MaxStick);
                Check(a.Y > 1.0 + 1.7, "he did not walk straight back from you (away along y): y " + a.Y);
                LogHas("first keeping man HOLDS his gap");
                //      he holds the gap 3 s against his own AI's walk in
                double minGap = 99;
                for (int i = 0; i < 180; i++)
                {
                    w.Frame();
                    minGap = Math.Min(minGap, w.Gap(a, you));
                }
                Check(minGap >= gapHeld - 0.05 && a.St.Keep.Keeping, "his own AI walked him back in while he held: min " + minGap + " m (held " + gapHeld + ")");
                double waitOn = ks.WaitMean(true, false);
                //      his pause ends - READY: released, his AI closes in
                KeepWorld.Pause(a, false);
                double readyIn = w.RunUntil(() => !a.St.Keep.Keeping, 1);
                Check(!double.IsNaN(readyIn) && readyIn <= 0.35 && ks.Ends[(int)KeepEnd.Ready] == 1 && a.St.Input != null && !a.St.Input.Keep,
                    "his pause ended but he was not released (ready): " + readyIn);
                w.Run(1.5);
                Check(w.Gap(a, you) < 1.2, "released, his AI did not close in: " + w.Gap(a, you));
                LogHas("first keeping man READY again");
                double walkedSword = ks.WalkSum / Math.Max(1, ks.WalkN);
                Check(walkedSword >= 1.75 && walkedSword <= 2.05, "the sword's walk was not ~1.9 m: " + walkedSword);

                // ---- 2. A SPEARMAN (2.5 m): farther - the cap 4 m
                var sp = w.Man(10, 1.0, length: 2.5, spear: true);
                var e2 = w.Man(10, 0);
                e2.Still = true;
                sp.Target = e2.St;
                KeepWorld.Pause(sp, true);
                sp.St.LastReleaseTime = w.T;
                w.RunUntil(() => sp.St.Keep.Phase == KeepPhase.Holding, 8);
                double spearGap = w.Gap(sp, e2);
                Check(spearGap >= 4.75 && spearGap <= 5.1 && ks.BacksOffSpear == 1, "the spearman did not back off the cap (1.0 + 4.0): " + spearGap + " m");
                KeepWorld.Pause(sp, false);
                w.Run(0.5);
                sp.Active = e2.Active = false;

                // ---- 3. YOU STEP IN, then SWING
                KeepWorld.Pause(a, true);
                a.St.LastReleaseTime = w.T;
                w.RunUntil(() => a.St.Keep.Phase == KeepPhase.Holding, 8);
                Check(a.St.Keep.Keeping, "no second keep for the step-in check");
                //      you walk 1.5 m in (no swing)
                for (int i = 0; i < 45; i++)
                {
                    you.Y += 1.5 / 45;
                    w.Frame();
                }
                double ya = a.Y;
                w.Run(1.5);
                Check(ks.SteppedIn >= 1 && Math.Abs(a.Y - ya) < 0.1 && a.St.Keep.Keeping, "you stepped in and he backed straight off (or was released): moved " + (a.Y - ya)
                    + " m, stepped in " + ks.SteppedIn);
                LogHas("first enemy STEPPED IN");
                double again = w.RunUntil(() => ks.ReBacks >= 1, 2.5);
                Check(!double.IsNaN(again), "after the step-in and another quiet wait he did not back off again");
                w.RunUntil(() => a.St.Keep.Phase == KeepPhase.Holding, 4);
                //      you swing: released at once (action), he stands and defends; no back-off for his next quiet wait
                you.St.LastReleaseTime = w.T;
                double rel = w.RunUntil(() => !a.St.Keep.Keeping, 1);
                Check(!double.IsNaN(rel) && rel <= 0.35 && ks.Ends[(int)KeepEnd.Action] == 1, "your swing did not release him (action): " + rel);
                double back2 = w.RunUntil(() => a.St.Keep.Keeping, 5);
                Check(!double.IsNaN(back2) && back2 >= 2.2, "after your swing he backed off again before a fresh quiet wait: " + back2);
                KeepWorld.Pause(a, false);
                w.Run(0.5);

                // ---- 4. SWINGS EVERY 1.5 s keep him in (each restarts his wait)
                KeepWorld.Pause(a, true);
                int keeps = (int)ks.Keeps;
                long broken = ks.QuietBroken;
                for (int i = 0; i < 4; i++)
                {
                    you.St.LastReleaseTime = w.T;
                    w.Run(1.5);
                }
                Check(ks.Keeps == keeps && ks.QuietBroken >= broken + 3, "swings every 1.5 s did not keep him in: keeps " + ks.Keeps + " (was " + keeps + "), broken "
                    + (ks.QuietBroken - broken));
                //      a melee collision restarts it too (OnMeleeHit's stamp)
                a.St.KeepActionAt = w.T;
                w.Run(2.0);
                Check(!a.St.Keep.Keeping, "a block / hit 2 s ago did not hold his wait");
                w.RunUntil(() => a.St.Keep.Keeping, 2);
                Check(a.St.Keep.Keeping, "no back-off once it went quiet after the collision");

                // ---- 6. THE REST TAKES OVER (it wins - never a double walk)
                a.St.Rhythm.Resting = true;
                w.Run(0.4);
                Check(!a.St.Keep.Keeping && ks.Ends[(int)KeepEnd.Rest] == 1, "the rest did not take him over: " + ks.Ends[(int)KeepEnd.Rest]);
                w.Run(4);
                Check(!a.St.Keep.Keeping, "a resting man kept his distance on top of the rest");
                a.St.Rhythm.Resting = false;
                KeepWorld.Pause(a, false);
                w.Run(1);

                // ---- 5. NEVER: a wall man, a rider, a routing man, a bowman, you, a man who can attack, an enemy beyond reach
                var never = new List<FakeKeepMan>();
                FakeKeepMan Pair(double x, Action<FakeKeepMan> how, double gap = 1.0)
                {
                    var m = w.Man(x, gap);
                    var f = w.Man(x, 0);
                    f.Still = m.Still = true; // both just stand there
                    m.Target = f.St;
                    how(m);
                    KeepWorld.Pause(m, true);
                    m.St.LastReleaseTime = w.T;
                    never.Add(m);
                    return m;
                }
                Pair(20, m => m.St.Wall = new WallState { Held = true });
                Pair(23, m => m.Mounted = true);
                Pair(26, m => m.Routing = true);
                Pair(29, m => m.Ranged = true);
                Pair(32, m => KeepWorld.Pause(m, false)).St.Pace!.Active = false;
                Pair(35, m => { }, gap: 3.5);
                var youWait = Pair(38, m => { });
                w.Body.You = youWait.St.Agent; // the player himself (his own pause): never
                w.Logic.SmokePlayer = youWait.St.Agent;
                never.Last().St.Pace!.Active = true;
                long keepsBefore = ks.Keeps;
                w.Run(5);
                Check(ks.Keeps == keepsBefore && never.All(m => !m.St.Keep.Keeping), "a man who must never keep his distance did: "
                    + string.Join(", ", never.Where(m => m.St.Keep.Keeping).Select(m => m.X)));
                foreach (var m in never) m.Active = false;
                w.Body.You = you.St.Agent;
                w.Logic.SmokePlayer = you.St.Agent;

                // ---- 7. A MAN IN A LINE: never past the rest's limit (4 m) behind his front
                var l = w.Man(50, 1.0, length: 2.5, spear: true); // he would walk 4 m
                var lf = w.Man(50, 0);
                lf.Still = true;
                l.Target = lf.St;
                var u = w.Logic.RhythmUnits;
                const int key = 3;
                l.St.Rhythm.Key = key;
                u.Valid[key] = true;
                u.UX[key] = 0;
                u.UY[key] = -1; // his enemy is south
                u.Front[key] = (l.Y * -1) + 3.0; // the front scalar = (a front point)·u; he stands 3 m behind it - 1 m left of the rest's 4
                KeepWorld.Pause(l, true);
                l.St.LastReleaseTime = w.T;
                double ly0 = l.Y;
                for (int i = 0; i < 60 * 8 && !(l.St.Keep.Keeping && l.St.Keep.Phase == KeepPhase.Holding); i++)
                {
                    l.St.Rhythm.LookedAt = w.T;
                    u.PublishedAt[key] = w.T;
                    w.Frame();
                }
                double lineWalk = l.Y - ly0;
                Check(l.St.Keep.Keeping && lineWalk >= 0.8 && lineWalk <= 1.15, "a man 3 m behind his front walked more than the rest's limit allows (1 m left): " + lineWalk);
                KeepWorld.Pause(l, false);
                l.St.Rhythm.LookedAt = double.NaN;
                w.Run(0.5);
                l.Active = lf.Active = false;

                // ---- 8. THE TIME LIMIT and A NEW TARGET
                KeepWorld.Pause(a, true);
                a.St.LastReleaseTime = w.T;
                w.RunUntil(() => a.St.Keep.Keeping, 4);
                double timeout = w.RunUntil(() => !a.St.Keep.Keeping, 12);
                Check(!double.IsNaN(timeout) && timeout >= 9.8 && timeout <= 10.4 && ks.Ends[(int)KeepEnd.Timeout] == 1, "the time limit did not end the keep at 10 s: " + timeout);
                var other = w.Man(0, -1.0);
                other.Still = true;
                w.RunUntil(() => a.St.Keep.Keeping, 5);
                Check(a.St.Keep.Keeping, "no keep for the target check");
                a.Target = other.St;
                double tgt = w.RunUntil(() => !a.St.Keep.Keeping, 0.5);
                Check(!double.IsNaN(tgt) && ks.Ends[(int)KeepEnd.Target] == 1, "a new target did not end the keep");
                a.Target = you.St;
                other.Active = false;

                // ---- 9. THE SWITCHES end every keep at once and give his input back; OFF = measured only
                w.RunUntil(() => a.St.Keep.Keeping, 5);
                Check(a.St.Keep.Keeping, "no keep for the switch check");
                S.Set(SettingsSchema.KeepDistanceEnabled, false, SettingSources.File);
                w.Frame();
                Check(!a.St.Keep.Keeping && w.Logic.KeepingNow == 0 && ks.Ends[(int)KeepEnd.SwitchedOff] == 1 && !a.St.Input!.Keep && !a.St.Input.KeepMove,
                    "KeepDistanceEnabled off did not end the keep at once");
                LogHas("keep your distance switched OFF");
                double yOff = a.Y;
                long would = ks.WouldOff;
                w.Run(6);
                Check(ks.WouldOff > would && !a.St.Keep.Keeping && w.Gap(a, you) < 1.1, "OFF: he did not stay close (the game's own) or the would-back-off was not counted: "
                    + w.Gap(a, you) + ", " + ks.WouldOff);
                double waitOff = ks.WaitMean(false, false);
                Check(!double.IsNaN(waitOn) && !double.IsNaN(waitOff) && waitOn > waitOff + 0.5, "the waiting distance ON vs OFF: " + waitOn + " vs " + waitOff);
                S.Set(SettingsSchema.KeepDistanceEnabled, true, SettingSources.File);
                a.St.LastReleaseTime = w.T;
                w.RunUntil(() => a.St.Keep.Keeping, 5);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                w.Frame();
                Check(!a.St.Keep.Keeping && w.Logic.KeepingNow == 0 && !a.St.Input!.Keep, "the master switch did not end the keep at once");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                KeepWorld.Pause(a, false);
                w.Run(0.5);

                // ---- 10. THE INPUT FRAME: the softest wish - the rest's walk, the backpedal beat it; its clamp takes out a walk in
                var s = a.St.Input!;
                AiInputHook.SetKeep(a.St, true, w.T);
                s.KeepMove = true;
                s.KeepX = 0f;
                s.KeepY = -0.6f;
                uint fl = 0u;
                float vx = 0f, vy = 1f;
                AiInputHook.Apply(a.St, s, true, ref fl, ref vx, ref vy);
                Check(Math.Abs(vx) < 1e-6 && Math.Abs(vy + 0.6f) < 1e-6, "the keep's walk was not written: " + vx + ", " + vy);
                S.Set(SettingsSchema.RestEnabled, true, SettingSources.File);
                AiInputHook.SetRhythm(a.St, true, w.T);
                s.RestMove = true;
                s.RestX = 0.5f;
                s.RestY = 0f;
                vx = 0f;
                vy = 1f;
                AiInputHook.Apply(a.St, s, true, ref fl, ref vx, ref vy);
                Check(Math.Abs(vx - 0.5f) < 1e-6 && Math.Abs(vy) < 1e-6, "the rest's walk did not beat the keep's: " + vx + ", " + vy);
                AiInputHook.SetRhythm(a.St, false, w.T);
                S.Set(SettingsSchema.RestEnabled, false, SettingSources.File);
                AiInputHook.SetBackpedal(a.St, true, false, w.T);
                vx = 0f;
                vy = 1f;
                AiInputHook.Apply(a.St, s, true, ref fl, ref vx, ref vy);
                Check(Math.Abs(vy - s.BackY) < 1e-6, "the backpedal did not beat the keep's walk");
                AiInputHook.SetBackpedal(a.St, false, false, w.T);
                s.KeepMove = false;
                s.KeepClamp = true;
                s.KeepNX = 0f;
                s.KeepNY = 1f;
                vx = 0.3f;
                vy = 0.8f;
                AiInputHook.Apply(a.St, s, true, ref fl, ref vx, ref vy);
                Check(Math.Abs(vx - 0.3f) < 1e-6 && Math.Abs(vy) < 1e-6, "the keep's clamp did not take out the walk toward his enemy: " + vx + ", " + vy);
                S.Set(SettingsSchema.KeepDistanceEnabled, false, SettingSources.File);
                vx = 0.3f;
                vy = 0.8f;
                AiInputHook.Apply(a.St, s, true, ref fl, ref vx, ref vy);
                Check(Math.Abs(vy - 0.8f) < 1e-6, "the keep's clamp bit with its switch off");
                S.Set(SettingsSchema.KeepDistanceEnabled, true, SettingSources.File);
                AiInputHook.SetKeep(a.St, false, w.T);

                // ---- 11. no errors; the [summary] line
                Check(ks.Errors == 0, "keep errors: " + ks.Errors);
                string line = ks.SummaryLine(KeepDistanceRules.From(S));
                Check(line.StartsWith("keep distance (step 109) ON: backed off", StringComparison.Ordinal) && line.Contains("ready " + ks.Ends[(int)KeepEnd.Ready] + ",") && ks.Ends[(int)KeepEnd.Ready] >= 3 && line.Contains("action 1,")
                      && line.Contains("timeout 1,") && line.Contains("target 1,") && line.Contains("rest took over 1,") && line.Contains("switch 2,"),
                    "the [summary] line: " + line);
                Console.WriteLine("        step 109: keeps " + ks.Keeps + ", backed off " + ks.BacksOff + " (spear " + ks.BacksOffSpear + ", again " + ks.ReBacks + "), walked mean "
                                  + (ks.WalkSum / Math.Max(1, ks.WalkN)).ToString("0.00") + " m p90 " + ks.WalkQuantile(0.9).ToString("0.00") + " m; quiet spells broken "
                                  + ks.QuietBroken + ", stepped in " + ks.SteppedIn + "; waiting distance ON " + waitOn.ToString("0.00") + " m vs OFF " + waitOff.ToString("0.00")
                                  + " m; the sword held " + gapHeld.ToString("0.00") + " m, the spear " + spearGap.ToString("0.00") + " m, the line man walked " + lineWalk.ToString("0.00") + " m");
                Console.WriteLine("        " + line);
            }
            finally
            {
                if (_logic != null) _logic.SmokePlayer = null;
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.KeepDistanceEnabled, false, SettingSources.File);
            }
        }

        /// <summary>Step 109's cost: 1000 men, half of them in their pause with an enemy at 1 m, about 400 keeping their distance at once - 300 ticks.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step109CostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                KeepDistanceDefaults();
                var w = new KeepWorld(30000);
                var men = new List<FakeKeepMan>();
                for (int i = 0; i < 500; i++)
                {
                    var a = w.Man(i * 3.0, 1.0);
                    var b = w.Man(i * 3.0, 0);
                    a.Target = b.St;
                    b.Target = a.St;
                    KeepWorld.Pause(a, true);
                    KeepWorld.Pause(b, i % 5 == 0);
                    a.St.LastReleaseTime = b.St.LastReleaseTime = w.T;
                    men.Add(a);
                    men.Add(b);
                }
                w.Run(3.0); // the first keeps begin (each man's input state made at his first hook - not the tick's cost)
                w.Run(1.0);
                TraxSettings.Shared.Set(SettingsSchema.LogMaxMegabytes, 100, SettingSources.File); // the log's own trim is not the keep's cost (step 76's note)
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                const int ticks = 300;
                for (int i = 0; i < ticks; i++) w.Logic.TickKeep(w.T + i / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                TraxSettings.Shared.Set(SettingsSchema.LogMaxMegabytes, SettingsSchema.LogMaxMegabytes.Default, SettingSources.File);
                int keeping = w.Logic.KeepingNow;
                Console.WriteLine("        step 109 cost: 1000 men (600 in their pause), " + keeping + " keeping their distance - " + (sw.Elapsed.TotalMilliseconds / ticks).ToString("0.000")
                                  + " ms a tick, " + allocated + " bytes over " + ticks + " ticks");
                Check(keeping >= 300, "the cost check's men did not keep their distance: " + keeping);
                Check(allocated < 64 * 1024, "cost: keep your distance allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(w.Logic.KeepStats.Errors == 0, "keep errors in the cost run: " + w.Logic.KeepStats.Errors);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.KeepDistanceEnabled, false, SettingSources.File);
            }
        }
    }
}
