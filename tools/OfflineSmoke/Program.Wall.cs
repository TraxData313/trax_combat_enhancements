using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 41 - THE SHIELD WALL HOLDS ITS SLOTS (AI_NOTES "Step 41", RESEARCH §O): the game's own update timer
    /// (the private field we read and the real <c>TaleWorlds.Core.Timer</c> moving exactly when vanilla's update runs), then the
    /// REAL <see cref="AthleticsLogic"/> with a stand-in for the engine side (<see cref="IWallBody"/>: the facts, the timer, the
    /// writes). What runs for real: the gates in order, the held list, the rewrite right after vanilla's update, the fallback,
    /// the staggered look, every let-go path with vanilla's value given back, the escape, the switch and the master switch live,
    /// leaving the field, the samples ON vs OFF, the lines and the summary - and the cost for a thousand men. What only the game can
    /// tell: that the engine keeps a held man in his slot (PLAYTEST D11, the [summary] "wall" lines).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for the hold's engine side: facts, a timer stamp and contact per agent index; the writes
        /// counted (and recorded per man unless <see cref="Record"/> is off).</summary>
        private sealed class FakeWallBody : IWallBody
        {
            private static readonly object TimerMarker = new object();
            public readonly Dictionary<int, WallFacts> Facts = new Dictionary<int, WallFacts>();
            public readonly Dictionary<int, float> Stamps = new Dictionary<int, float>();
            public readonly HashSet<int> NoTimer = new HashSet<int>();
            public readonly HashSet<int> Gone = new HashSet<int>();
            public readonly HashSet<int> Contact = new HashSet<int>();
            public readonly Dictionary<int, int> Holds = new Dictionary<int, int>();
            public readonly List<int> Released = new List<int>();
            public int HoldCalls;
            public int ReleaseCalls;
            public bool Record = true;

            public bool IsActive(TrackedAgent st) => !Gone.Contains(st.AgentIndex);

            public WallFacts Read(TrackedAgent st) => Facts.TryGetValue(st.AgentIndex, out var f) ? f : WallMan();

            public bool TryStamp(TrackedAgent st, WallState ws, out float stamp)
            {
                ws.TimerLooked = true;
                if (NoTimer.Contains(st.AgentIndex))
                {
                    ws.Timer = null;
                    stamp = 0f;
                    return false;
                }
                ws.Timer = TimerMarker;
                stamp = Stamps.TryGetValue(st.AgentIndex, out var s) ? s : 0f;
                return true;
            }

            public void Hold(TrackedAgent st, in WallFacts f)
            {
                HoldCalls++;
                if (Record) Holds[st.AgentIndex] = Held(st.AgentIndex) + 1;
            }

            public int Held(int index) => Holds.TryGetValue(index, out var n) ? n : 0;

            public void Release(TrackedAgent st)
            {
                ReleaseCalls++;
                if (Record) Released.Add(st.AgentIndex);
            }

            public bool EnemyWithin(TrackedAgent st, double metres) => Contact.Contains(st.AgentIndex);

            /// <summary>Step 42: the enemy each man fights (his target), by agent index; none = no enemy.</summary>
            public readonly Dictionary<int, (double X, double Y)> Enemies = new Dictionary<int, (double X, double Y)>();
            public int EnemyReads;

            public bool TryEnemy(TrackedAgent st, out double x, out double y)
            {
                EnemyReads++;
                if (Enemies.TryGetValue(st.AgentIndex, out var e))
                {
                    x = e.X;
                    y = e.Y;
                    return true;
                }
                x = y = double.NaN;
                return false;
            }

            /// <summary>Step 63: the walk back's hook and the vector (his frame = the world's here).</summary>
            public int Hooks, InputReleases;
            public bool HookThrows;

            public bool ToLocal(TrackedAgent st, double dx, double dy, out float lx, out float ly)
            {
                double len = Math.Sqrt(dx * dx + dy * dy);
                lx = len > 1e-9 ? (float)(dx / len) : 0f;
                ly = len > 1e-9 ? (float)(dy / len) : -1f;
                return true;
            }

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                if (HookThrows) throw new InvalidOperationException("smoke: the input hook failed");
                Hooks++;
                return AiInputHook.HookResult.FirstHookTurnedOn;
            }

            public void ReleaseInput(TrackedAgent st) => InputReleases++;

            /// <summary>Step 68: the paced frames written (the last point per man) and the game's frames given back.</summary>
            public readonly Dictionary<int, (double X, double Y)> Paced = new Dictionary<int, (double X, double Y)>();
            public int PaceCalls, UnpaceCalls;
            public bool PaceRefused;

            public bool Pace(TrackedAgent st, double x, double y)
            {
                if (PaceRefused) return false;
                PaceCalls++;
                if (Record) Paced[st.AgentIndex] = (x, y);
                return true;
            }

            public void Unpace(TrackedAgent st)
            {
                UnpaceCalls++;
                if (Record) Paced.Remove(st.AgentIndex);
            }
        }

        /// <summary>An AI man on foot in a halted shield wall facing +y, <paramref name="ahead"/> m ahead of his slot (0, 0), the
        /// formation's centre 1 m behind the slot, its speed 3.5 m/s (far = 21 m).</summary>
        private static WallFacts WallMan(double ahead = 0.3, WallArrangement arrangement = WallArrangement.ShieldWall, BraceOrderKind order = BraceOrderKind.Stop) =>
            new WallFacts
            {
                Ai = true,
                HasFormation = true,
                Arrangement = arrangement,
                Order = order,
                HasPositions = true,
                AgentX = 0, AgentY = ahead,
                SlotX = 0, SlotY = 0,
                DirX = 0, DirY = 1,
                CentreX = 0, CentreY = -1,
                AvgSpeed = 3.5,
                Deviation = 1.0,
            };

        /// <summary>Step 41: every logic built from here on plays the hold's engine side through a stand-in (the game's writes the
        /// engine).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeWallBody()
        {
            Check(WallBodyDefault.Current is GameWallBody, "the default wall body is not the game's: " + WallBodyDefault.Current.GetType().Name);
            WallBodyDefault.Current = new FakeWallBody();
            Check(new AthleticsLogic().WallBody == WallBodyDefault.Current, "a new logic does not start with the default wall body");
        }

        /// <summary>Step 41's checks run the HARD hold (step 42's leash pinned at 0 - the proof that 0 is step 41 exactly).</summary>
        private static void WallDefaults()
        {
            S.Set(SettingsSchema.ShieldWallHoldsSlots, true, SettingSources.File);
            S.Set(SettingsSchema.ShieldWallStepOutMetres, 0.0, SettingSources.File);
            S.Set(SettingsSchema.ShieldWallPullBackSeconds, 0.0, SettingSources.File); // step 63 off: steps 41 / 42 exactly (no extra looks, no walk back)
            S.Set(SettingsSchema.ShieldWallKickReturnMetres, 0.0, SettingSources.File); // step 68 off: the walk back after a kick (its 0.1 s looks too)
            S.Set(SettingsSchema.ShieldWallAdvanceAsOne, false, SettingSources.File);   // step 68 off: the game's own frames (Program.Step68.cs runs them)
        }

        /// <summary>
        /// Step 41, the game's side of the timing (RESEARCH §O): the private timer field exists on v1.4.8's Agent with the game's
        /// Timer type, and the real Timer driven exactly as vanilla drives it (Agent.TickParallel: Check, then AdjustStartTime(−5);
        /// Agent.TickAsAI: Check again) moves its StartTime exactly on the frames vanilla's update runs and on no other - the
        /// signal the logic rewrites on. The arrangements map onto ours.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallTimerIsVanillasUpdate()
        {
            var field = GameWallBody.TimerField;
            Check(field != null, "Agent has no private Timer field " + GameWallBody.TimerFieldName + " (renamed? the fallback would run in game)");
            Check(field!.FieldType == typeof(TaleWorlds.Core.Timer) && field.IsInitOnly, "the timer field is not the game's readonly Timer: " + field.FieldType.FullName);
            var start = typeof(TaleWorlds.Core.Timer).GetProperty("StartTime");
            Check(start != null && start.GetMethod != null && start.GetMethod.IsPublic, "Timer.StartTime is not publicly readable");

            // vanilla's frames: an agent created at 100 s with a 0.5 s period (0.45 + 0.1 x rand)
            var timer = new TaleWorlds.Core.Timer(100f, 0.5f);
            int moves = 0, updates = 0;
            float last = timer.StartTime;
            for (int frame = 1; frame <= 180; frame++) // 3 s at 60 fps
            {
                float now = 100f + frame / 60f;
                bool update = false;
                if (timer.Check(now))           // Agent.TickParallel → ParallelUpdateFormationMovement
                {
                    timer.AdjustStartTime(-5f);
                    update = true;
                    updates++;
                }
                timer.Check(now);               // Agent.TickAsAI → ApplyFormationValuesPostUpdate
                bool moved = timer.StartTime != last;
                last = timer.StartTime;
                if (moved) moves++;
                Check(moved == update, "frame " + frame + ": the timer's start " + (moved ? "moved without" : "stood still at") + " vanilla's update");
            }
            Check(updates == 6 && moves == 6, "3 s of vanilla's frames: " + updates + " updates, " + moves + " moves (expected 6 each - one every 0.5 s)");

            Check(GameWallBody.Map(ArrangementOrder.ArrangementOrderEnum.ShieldWall) == WallArrangement.ShieldWall
                  && GameWallBody.Map(ArrangementOrder.ArrangementOrderEnum.Square) == WallArrangement.Square
                  && GameWallBody.Map(ArrangementOrder.ArrangementOrderEnum.Circle) == WallArrangement.Circle
                  && GameWallBody.Map(ArrangementOrder.ArrangementOrderEnum.Column) == WallArrangement.Column
                  && GameWallBody.Map(ArrangementOrder.ArrangementOrderEnum.Line) == WallArrangement.Other
                  && GameWallBody.Map(ArrangementOrder.ArrangementOrderEnum.Loose) == WallArrangement.Other
                  && GameWallBody.Map(ArrangementOrder.ArrangementOrderEnum.Scatter) == WallArrangement.Other
                  && GameWallBody.Map(ArrangementOrder.ArrangementOrderEnum.Skein) == WallArrangement.Other,
                "the game's arrangements do not map onto ours");
            foreach (ArrangementOrder.ArrangementOrderEnum e in Enum.GetValues(typeof(ArrangementOrder.ArrangementOrderEnum)))
                Check(WallMath.IsDefensive(GameWallBody.Map(e)) == (ArrangementOrder.GetArrangementOrderDefensiveness(e) == 1),
                    "our defensive set differs from the game's GetArrangementOrderDefensiveness at " + e);
        }

        /// <summary>One look at everyone (the hold's tick after a gap of a look or more).</summary>
        private static void WallLook(AthleticsLogic logic, ref double t)
        {
            t += 0.3;
            logic.TickWall(t);
        }

        private static void WallFrames(AthleticsLogic logic, ref double t, double seconds)
        {
            int frames = (int)Math.Round(seconds * 60);
            for (int k = 0; k < frames; k++)
            {
                t += 1.0 / 60;
                logic.TickWall(t);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                var body = new FakeWallBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                var ws = logic.WallStats;
                LogHas("[wall] mission start: ON (ShieldWallHoldsSlots) - every AI man on foot in a shield wall, square or circle keeps his slot");
                double t = 13000;

                // ---- the men: 4 held, 10 left to vanilla - one per reason
                var men = new Dictionary<int, TrackedAgent>();
                void Man(int i, WallFacts f)
                {
                    men[i] = logic.Track(FakeAgent(i))!;
                    body.Facts[i] = f;
                }
                Man(800, WallMan());                                                                  // a halted shield wall
                Man(801, WallMan(arrangement: WallArrangement.Square, order: BraceOrderKind.Move));    // a square moved
                Man(802, WallMan(arrangement: WallArrangement.Circle, order: BraceOrderKind.AiAdvance)); // a circle, the AI's advance
                Man(803, WallMan(arrangement: WallArrangement.Other));                                 // a line
                var mounted = WallMan(); mounted.Mounted = true; Man(804, mounted);
                var detached = WallMan(); detached.Detached = true; Man(805, detached);
                Man(806, WallMan(ahead: -30));                                                         // 30 m behind his slot and the line
                Man(807, WallMan());                                                                  // you
                Man(808, WallMan(order: BraceOrderKind.Charge));
                var attacker = WallMan(); attacker.SiegeAttacker = true; Man(809, attacker);
                var naval = WallMan(); naval.Naval = true; Man(810, naval);
                var fleeing = WallMan(); fleeing.Retreating = true; Man(811, fleeing);
                Man(812, WallMan());                                                                  // held, his timer unreadable
                body.NoTimer.Add(812);
                Man(813, WallMan(arrangement: WallArrangement.Column));
                logic.SmokePlayer = men[807].Agent;

                logic.TickWall(t); // the first call looks at everyone
                Check(logic.WalledNow == 4 && body.Held(800) == 1 && body.Held(801) == 1 && body.Held(802) == 1 && body.Held(812) == 1 && body.HoldCalls == 4,
                    "the first look did not hold exactly the shield wall, the square, the circle and the no-timer man: " + logic.WalledNow + " held, " + body.HoldCalls + " writes");
                Check(ws.MenLooked == 14 && ws.MenHeld == 4 && ws.Writes(WallWriteKind.Poll) == 4, "the first look's counts: looked " + ws.MenLooked + ", held " + ws.MenHeld);
                foreach (var v in new[] { WallVerdict.Mounted, WallVerdict.Detached, WallVerdict.FarBehind, WallVerdict.NotAi, WallVerdict.ChargeOrder,
                             WallVerdict.SiegeAttacker, WallVerdict.Naval, WallVerdict.Retreating })
                    Check(ws.Looks(v) == 1, "the first look did not leave one man to vanilla for '" + WallMath.Name(v) + "': " + ws.Looks(v));
                Check(ws.Looks(WallVerdict.Arrangement) == 2, "the line and the column were not left to vanilla");
                Check(men[807].Wall != null && !men[807].Wall!.EverHeld, "YOU were held");
                LogHas("[wall] first man held in his slot this mission: (agent 800) at 13000.0 s - his formation in a shield wall under the order ");
                LogHas("\"keep with the formation instead of moving to the agent\" written TRUE (vanilla's update timer read: he is rewritten right after each of vanilla's own updates)");
                LogHas("[wall] vanilla's update timer (Agent._cachedAndFormationValuesUpdateTimer) cannot be read on (agent 812) - a game update renamed it? Held men are rewritten every 0.10 s instead (tell Claude)");
                Check(ws.MenWithoutTimer == 1, "the no-timer man was not counted");

                // ---- frames with no vanilla update: nothing rewritten after vanilla; the no-timer man every 0.1 s
                int noTimerWrites = body.Held(812);
                WallFrames(logic, ref t, 0.5);
                Check(ws.Writes(WallWriteKind.AfterVanilla) == 0, "a rewrite 'after vanilla' with no vanilla update");
                Check(ws.Writes(WallWriteKind.Fallback) >= 2 && body.Held(812) - noTimerWrites >= 4 && body.Held(812) - noTimerWrites <= 6,
                    "the no-timer man was not rewritten every 0.1 s (by the fallback or the look): " + (body.Held(812) - noTimerWrites) + " in 0.5 s, " + ws.Writes(WallWriteKind.Fallback) + " by the fallback");

                // ---- vanilla's update on 800 (his timer moved): rewritten in the very next pre-tick, once
                int before = body.Held(800);
                body.Stamps[800] = 0.5f;
                WallFrames(logic, ref t, 1.0 / 60);
                Check(body.Held(800) >= before + 1 && ws.Writes(WallWriteKind.AfterVanilla) == 1 && ws.VanillaUpdates == 1, "vanilla's update on 800 was not followed by our write in the next frame");
                WallFrames(logic, ref t, 2.0 / 60);
                Check(ws.Writes(WallWriteKind.AfterVanilla) == 1, "the same timer read twice was rewritten again");
                body.Stamps[800] = 1.0f;
                t += 0.5 - 3.0 / 60;
                WallFrames(logic, ref t, 1.0 / 60);
                Check(ws.Writes(WallWriteKind.AfterVanilla) == 2 && ws.VanillaIntervals == 1 && Math.Abs(ws.VanillaIntervalSum - 0.5) < 0.02,
                    "vanilla's second update: writes " + ws.Writes(WallWriteKind.AfterVanilla) + ", interval " + ws.VanillaIntervalSum);

                // ---- a CHARGE order on the square: let go at his next look, vanilla's own value given back
                body.Facts[801] = WallMan(arrangement: WallArrangement.Square, order: BraceOrderKind.Charge);
                WallLook(logic, ref t);
                Check(!men[801].Wall!.Held && body.Released.Contains(801) && ws.Releases(WallVerdict.ChargeOrder) == 1 && logic.WalledNow == 3,
                    "a charge order did not let the square's man go with vanilla's value");
                // the order changing is seen at vanilla's update too: the circle to a RETREAT, its man's timer moves - let go in the next frame
                body.Facts[802] = WallMan(arrangement: WallArrangement.Circle, order: BraceOrderKind.Retreat);
                body.Stamps[802] = 2f;
                WallFrames(logic, ref t, 1.0 / 60);
                Check(!men[802].Wall!.Held && body.Released.Contains(802) && ws.Releases(WallVerdict.RetreatOrder) == 1, "a retreat order was not let go at vanilla's update");

                // ---- the ESCAPE: 800 pushed 25 m behind his slot (behind the line) runs free to rejoin; far AHEAD he is held (walked back)
                body.Facts[800] = WallMan(ahead: -25);
                WallLook(logic, ref t);
                Check(!men[800].Wall!.Held && ws.Releases(WallVerdict.FarBehind) == 1, "a man far behind his slot and the line was still held");
                body.Facts[800] = WallMan(ahead: 25);
                WallLook(logic, ref t);
                Check(men[800].Wall!.Held, "a man far AHEAD of his slot was not held");
                body.Facts[800] = WallMan();
                body.Facts[806] = WallMan(ahead: -2);   // the far man is back near his slot
                WallLook(logic, ref t);
                Check(men[806].Wall!.Held && ws.MenHeld == 5, "the man who ran back was not held");
                // a man the player takes over (RTS Camera) is let go
                var taken = WallMan(ahead: -2); taken.Ai = false;
                body.Facts[806] = taken;
                WallLook(logic, ref t);
                Check(!men[806].Wall!.Held && ws.Releases(WallVerdict.NotAi) == 1, "a man the player took over stayed held");

                // ---- samples ON: the held men stand 0.3 m ahead, 800 in contact
                body.Contact.Add(800);
                int onBefore = ws.On.Samples, onOverBefore = ws.On.OverNear; // (the 25 m-ahead look above was sampled ON - rightly)
                WallFrames(logic, ref t, 2);
                Check(ws.On.Samples >= onBefore + 6 && ws.On.Contact > 0 && ws.On.OverNear == onOverBefore, "ON: no samples, or a man > 1 m ahead (before: " + onBefore + " samples, " + onOverBefore + " over 1 m): " + ws.On.Text());
                int onSamples = ws.On.Samples;

                // ---- the switch OFF, live: every held man given vanilla's value at once, nothing written after, the samples go OFF
                int heldNow = logic.WalledNow;
                int released = body.Released.Count;
                S.Set(SettingsSchema.ShieldWallHoldsSlots, false, SettingSources.Mcm);
                WallFrames(logic, ref t, 1.0 / 60);
                Check(heldNow == 2 && logic.WalledNow == 0 && body.Released.Count == released + heldNow && ws.Releases(WallVerdict.SwitchedOff) == heldNow,
                    "switched off: the held men (" + heldNow + ") were not given back at once");
                LogHas("[wall] ShieldWallHoldsSlots switched OFF mid-mission at ");
                LogHas(" s: 2 men given vanilla's own value back at once - they may step out at their enemies again");
                int writes = body.HoldCalls;
                body.Facts[800] = WallMan(ahead: 2.5); // vanilla lets him step out
                body.Stamps[800] = 9f;
                WallFrames(logic, ref t, 2);
                Check(body.HoldCalls == writes && logic.WalledNow == 0, "switched off: still written");
                Check(ws.Off.Samples > 0 && ws.Off.OverFar > 0 && ws.On.Samples == onSamples, "OFF: the samples did not go to OFF: " + ws.Off.Text());
                S.Set(SettingsSchema.ShieldWallHoldsSlots, true, SettingSources.Mcm);
                body.Facts[800] = WallMan();
                WallLook(logic, ref t);
                Check(men[800].Wall!.Held && logic.WalledNow == 2, "switched on: not held again at the next look");
                LogHas("[wall] ShieldWallHoldsSlots switched ON mid-mission at ");

                // ---- the MASTER SWITCH: the same, by ModEnabled first
                released = body.Released.Count;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                WallFrames(logic, ref t, 1.0 / 60);
                Check(logic.WalledNow == 0 && body.Released.Count == released + 2 && ws.Releases(WallVerdict.ModOff) == 2, "mod off: the held men were not given back at once");
                LogHas("[wall] the whole mod (ModEnabled) switched OFF mid-mission at ");
                writes = body.HoldCalls;
                WallFrames(logic, ref t, 1);
                Check(body.HoldCalls == writes && ws.Looks(WallVerdict.ModOff) > 0, "mod off: still written, or the looks not filed under the master switch");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                WallLook(logic, ref t);
                Check(logic.WalledNow == 2, "mod on: not held again");
                LogHas("[wall] the whole mod (ModEnabled) switched ON mid-mission at ");

                // ---- leaving the field: out of the list, no engine call
                released = body.Released.Count;
                typeof(AthleticsLogic).GetMethod("Untrack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, new object[] { men[812].Agent });
                Check(!men[812].Wall!.Held && logic.WalledNow == 1 && body.Released.Count == released && ws.LeftFieldHeld == 1, "leaving the field: an engine call, or still in the list");
                body.Gone.Add(812);
                WallFrames(logic, ref t, 0.5);

                // ---- the summary
                logic.WriteWallSummary();
                Check(logic.WalledNow == 0 && ws.HeldAtEnd == 1, "the summary did not close the held list: " + ws.HeldAtEnd);
                LogHas("[summary] shield wall holds its slots (step 41): ON (ShieldWallHoldsSlots) - every AI man on foot");
                LogHas(" - switched 4 time(s) during this battle (the ON / OFF lines below split the samples by it)");
                LogHas("[summary] wall: 14 men looked at, 5 held in their slots at some point; looks - held ");
                LogHas("[summary] wall writes: ");
                LogHas(", men whose update timer could not be read 1 (should be 0 - else the fallback); released to vanilla's own value 8 (the mod off 2, switched off 2, you or a man you drive 1, a charge order 1, a retreat order 1, far from his slot and behind the line 1)");
                LogHas("; held men who fell or left the field 1, still held when the battle ended 1; errors 0; cost avg ");
                LogHas("[summary] wall slots ON: ");
                LogHas("[summary] wall slots OFF (vanilla): ");
                Check(ws.On.ShareNear < ws.Off.ShareNear && ws.Errors == 0, "the summary does not show OFF ahead of ON: ON " + ws.On.ShareNear + ", OFF " + ws.Off.ShareNear);
                logic.TickWall(t + 1); // after the summary: nothing more
                Check(logic.WalledNow == 0, "a look after the summary held somebody");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                AthleticsDefaults();
                WallDefaults();
            }
        }

        /// <summary>Step 41's cost: a thousand men in a halted shield wall, each one's vanilla update every 0.5 s (staggered) followed
        /// by our write in the next frame, the staggered look every 0.25 s - the pre-tick stays small and allocates nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallTickCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                var body = new FakeWallBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                const int men = 1000;
                double t0 = 14000;
                for (int i = 0; i < men; i++)
                {
                    logic.Track(FakeAgent(5000 + i));
                    body.Facts[5000 + i] = WallMan(ahead: (i % 7) * 0.4);
                    body.Stamps[5000 + i] = 0f;
                    if (i % 5 == 0) body.Contact.Add(5000 + i);
                }
                logic.TickWall(t0);
                Check(logic.WalledNow == men, "cost: not every man held: " + logic.WalledNow);
                const int ticks = 600;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = new Stopwatch();
                int calls = body.HoldCalls;
                for (int k = 1; k <= ticks; k++)
                {
                    // vanilla's update: each man every 30 frames (0.5 s), staggered
                    for (int i = k % 30; i < men; i += 30) body.Stamps[5000 + i] = k;
                    sw.Start();
                    logic.TickWall(t0 + k / 60.0);
                    sw.Stop();
                }
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                int written = body.HoldCalls - calls;
                Console.WriteLine("        step 41 cost: " + men + " men held - the pre-tick " + msPerTick.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                  + " ms a tick, " + allocated + " bytes allocated over " + ticks + " ticks, " + written + " writes (" + (written / (double)men / 10).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " a man a second)");
                Check(msPerTick < 0.5, "cost: the hold's pre-tick over " + men + " men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 256 * 1024, "cost: the hold's pre-tick allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.WallStats.Writes(WallWriteKind.AfterVanilla) >= men * 19 && logic.WallStats.Writes(WallWriteKind.AfterVanilla) <= men * 21,
                    "cost: not one write after each vanilla update (~20 a man in 10 s): " + logic.WallStats.Writes(WallWriteKind.AfterVanilla));
                Check(logic.WallStats.On.Samples >= men * 19 && logic.WallStats.On.Samples <= men * 21, "cost: the samples are not ~2 a man a second: " + logic.WallStats.On.Samples);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                WallDefaults();
            }
        }
    }
}
