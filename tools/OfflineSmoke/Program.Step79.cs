using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 79 (Toni 2026-10-22: battles that sound alive) - WAR CRIES ONLY through the real logic with a COUNTING stand-in for the voice (the smoke cannot
    /// play sound): a thousand-man charge order stays under every cap, nobody shouts twice within his 8 s, the roar is staggered, the routed / the dead /
    /// you never shout, riders do; a unit's roar cools down; switching off drops what waits; the rested men's cries and their mates' answers in a real
    /// meshed grind - and the grind's rests IDENTICAL with the cries on and off (sound only). What only the game can tell: how it sounds (PLAYTEST D44).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The voice's stand-in: counts every call with its time, man and horse.</summary>
        private sealed class FakeWarCryBody : IWarCryBody
        {
            public readonly HashSet<int> Banned = new HashSet<int>();  // routed / fleeing / dead - MayShout false
            public readonly HashSet<int> Mounted = new HashSet<int>();
            public readonly List<(double T, int Index, bool Mounted)> Cries = new List<(double, int, bool)>(8192);

            public bool MayShout(TrackedAgent st) => !Banned.Contains(st.AgentIndex);

            public bool IsMounted(TrackedAgent st) => Mounted.Contains(st.AgentIndex);

            public void Shout(TrackedAgent st, bool mounted, double now) => Cries.Add((now, st.AgentIndex, mounted));
        }

        /// <summary>Every logic built from here on plays the voice through a stand-in; the game's own call exists as used.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeWarCryBody()
        {
            Check(WarCryBodyDefault.Current is GameWarCryBody, "the default war-cry body is not the game's");
            var voice = typeof(SkinVoiceManager).GetNestedType("VoiceType");
            Check(voice != null && voice.GetField("Yell") != null && voice.GetField("HorseRally") != null, "SkinVoiceManager.VoiceType lacks Yell / HorseRally");
            var make = typeof(Agent).GetMethod("MakeVoice", new[] { typeof(SkinVoiceManager.SkinVoiceType), typeof(SkinVoiceManager.CombatVoiceNetworkPredictionType) });
            Check(make != null, "Agent.MakeVoice(SkinVoiceType, CombatVoiceNetworkPredictionType) not found");
            Check(Enum.IsDefined(typeof(SkinVoiceManager.CombatVoiceNetworkPredictionType), "NoPrediction"), "NoPrediction missing");
            WarCryBodyDefault.Current = new FakeWarCryBody();
            Check(new AthleticsLogic().WarCryBody == WarCryBodyDefault.Current, "a new logic does not start with the default war-cry body");
        }

        /// <summary>DESIGN's initial numbers.</summary>
        private static void WarCryDefaults(bool on = true)
        {
            S.Set(SettingsSchema.WarCriesEnabled, on, SettingSources.File);
            S.Set(SettingsSchema.WarCryBackInPercent, 35, SettingSources.File);
            S.Set(SettingsSchema.WarCryAnswerPercent, 20, SettingSources.File);
            S.Set(SettingsSchema.WarCryAnswerDelaySeconds, 1.0, SettingSources.File);
            S.Set(SettingsSchema.WarCryChargeSharePercent, 40, SettingSources.File);
            S.Set(SettingsSchema.WarCryChargeMaxMen, 12, SettingSources.File);
            S.Set(SettingsSchema.WarCryChargeStaggerSeconds, 1.5, SettingSources.File);
            S.Set(SettingsSchema.WarCryChargeUnitCooldownSeconds, 15.0, SettingSources.File);
            S.Set(SettingsSchema.WarCryManCooldownSeconds, 8.0, SettingSources.File);
            S.Set(SettingsSchema.WarCryUnitPerSecond, 8, SettingSources.File);
            S.Set(SettingsSchema.WarCryBattlePerSecond, 16, SettingSources.File);
        }

        /// <summary>The most cries in any 1 s (a sliding window) among these times.</summary>
        private static int PeakInAnySecond(IEnumerable<double> times)
        {
            var t = times.OrderBy(x => x).ToList();
            int peak = 0;
            for (int i = 0, j = 0; i < t.Count; i++)
            {
                while (t[i] - t[j] >= 1.0 - 1e-9) j++;
                peak = Math.Max(peak, i - j + 1);
            }
            return peak;
        }

        /// <summary>The shortest gap between one man's two cries (NaN = nobody twice).</summary>
        private static double ShortestManGap(FakeWarCryBody voice)
        {
            double best = double.NaN;
            foreach (var g in voice.Cries.GroupBy(c => c.Index))
            {
                var ts = g.Select(c => c.T).OrderBy(x => x).ToList();
                for (int i = 1; i < ts.Count; i++)
                    if (double.IsNaN(best) || ts[i] - ts[i - 1] < best) best = ts[i] - ts[i - 1];
            }
            return best;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThousandManChargeStaysUnderTheCaps()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults(on: false);
                FeverOff();
                WarCryDefaults();
                var orders = new FakeSprintBody();
                var logic = NewSprintLogic(orders);
                var voice = new FakeWarCryBody();
                logic.WarCryBody = voice;
                logic.WarCryDice = new SeededRandom(7901);
                CallPrivate(logic, "StartWarCries");
                LogHas("[warcry] mission start: war cries (step 79, sound only");
                // two armies of 500, five formations of 100 each; 50 riders in each army's 5th formation; 20 routed, 5 dead, you in slot 0
                var slots = new List<int>();
                for (int team = 0; team < 2; team++)
                    for (int f = 0; f < 5; f++)
                        slots.Add(team * GameSprintBody.SlotsPerTeam + f);
                var men = new List<TrackedAgent>();
                for (int i = 0; i < 1000; i++)
                {
                    int idx = 79000 + i;
                    var st = logic.Track(FakeAgent(idx))!;
                    men.Add(st);
                    int slot = slots[i / 100];
                    orders.Slots[idx] = slot;
                    if (slot % GameSprintBody.SlotsPerTeam == 4 && i % 2 == 0) voice.Mounted.Add(idx);
                    if (i % 50 == 7) voice.Banned.Add(idx); // routed / fleeing / dead
                }
                logic.SmokePlayer = men[3].Agent;
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Advance;
                double t = 79000;
                void Tick(double dt = 0.05)
                {
                    t += dt;
                    logic.TickSprint((float)dt, t, Rules);
                    logic.TickWarCries(t);
                }
                for (int k = 0; k < 10; k++) Tick(); // the poll's first look records the advances
                Check(voice.Cries.Count == 0, "cries before any charge order");

                // ---- THE CHARGE: every formation of both armies
                double t0 = t;
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Charge;
                for (int k = 0; k < 60; k++) Tick(); // 3 s
                var ws = logic.WarCryStats;
                var first = voice.Cries.ToList();
                int peak = PeakInAnySecond(first.Select(c => c.T));
                var byUnit = first.GroupBy(c => orders.Slots[c.Index]).ToDictionary(g => g.Key, g => g.ToList());
                int peakUnit = byUnit.Values.Select(l => PeakInAnySecond(l.Select(c => c.T))).DefaultIfEmpty(0).Max();
                int mostInUnit = byUnit.Values.Select(l => l.Count).DefaultIfEmpty(0).Max();
                double firstAt = first.Count > 0 ? first.Min(c => c.T) - t0 : double.NaN, lastAt = first.Count > 0 ? first.Max(c => c.T) - t0 : double.NaN;
                int riders = first.Count(c => c.Mounted);
                Console.WriteLine("        step 79 a 1000-man charge order (10 formations of 100, both armies at once): queued " + ws.Queued[(int)WarCryCause.Charge] + " (rolls won "
                                  + ws.RollsWon[(int)WarCryCause.Charge] + ", past the unit shouter cap " + ws.ChargeOverMax + ", not eligible " + ws.NotEligible[(int)WarCryCause.Charge]
                                  + "), PLAYED " + first.Count + " (riders " + riders + ") between +" + F2s(firstAt) + " and +" + F2s(lastAt) + " s; dropped by the battle's cap "
                                  + ws.DroppedBy(WarCryGate.Battle) + ", the unit's " + ws.DroppedBy(WarCryGate.Unit) + "; the peak in any 1 s: the battle " + peak + " (cap 16), one unit "
                                  + peakUnit + " (cap 8), the most from one unit " + mostInUnit + " (cap 12)");
                Check(ws.ChargeOrders == 10 && ws.ChargeRoars == 10, "10 charge orders did not roar 10 times: " + ws.ChargeOrders + " / " + ws.ChargeRoars);
                Check(ws.Queued[(int)WarCryCause.Charge] == 10 * 3, "the queue is not 10 units x 3 (16 a second x 1.5 s shared by 10 roaring units): " + ws.Queued[(int)WarCryCause.Charge]);
                Check(first.Count >= 16 && peak <= 16, "the battle's cap: " + peak + " in 1 s, " + first.Count + " played");
                Check(peakUnit <= 8 && mostInUnit <= 12, "a unit's caps: " + peakUnit + " in 1 s, " + mostInUnit + " in all");
                Check(firstAt >= 0 && lastAt <= 1.5 + 0.05 + 1e-9, "the roar is not within the 0-1.5 s stagger: +" + firstAt + " .. +" + lastAt);
                Check(first.Select(c => Math.Round(c.T, 3)).Distinct().Count() >= 10, "the roar is in sync, not staggered");
                Check(!first.Any(c => voice.Banned.Contains(c.Index)), "a routed / dead man shouted");
                Check(!first.Any(c => c.Index == men[3].AgentIndex), "YOU shouted");
                Check(riders > 0, "no rider shouted on the charge");
                Check(ws.PeakBattle == peak && ws.PeakUnit == peakUnit, "the logic's peaks " + ws.PeakBattle + " / " + ws.PeakUnit + " differ from the sink's " + peak + " / " + peakUnit);
                LogHas("[warcry] first CHARGE ROAR at ");
                LogHas("[warcry] first WAR CRY (charge order) at ");
                LogHas("[warcry] first cry dropped by the battle's cap at ");

                // ---- 3 s later the same units flip to an advance and back: the unit's roar is cooling (15 s) - nothing
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Advance;
                for (int k = 0; k < 6; k++) Tick(); // past a 0.25 s order poll
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Charge;
                for (int k = 0; k < 40; k++) Tick();
                Check(voice.Cries.Count == first.Count && ws.ChargeCooled == 10, "a cooling unit roared again: " + (voice.Cries.Count - first.Count) + " cries, cooled " + ws.ChargeCooled);

                // ---- the unit cooldown at 0: a new charge 5 s after the first roar - the men who shouted are inside their own 8 s
                S.Set(SettingsSchema.WarCryChargeUnitCooldownSeconds, 0.0, SettingSources.Mcm);
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Advance;
                for (int k = 0; k < 6; k++) Tick(); // past a 0.25 s order poll
                long manDrops = ws.DroppedBy(WarCryGate.Man);
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Charge;
                for (int k = 0; k < 40; k++) Tick();
                long manDropped = ws.DroppedBy(WarCryGate.Man) - manDrops;

                // ---- 300 ticks with roars every 20 s, the allocation measured (warm)
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                double cost0 = ws.CostMs;
                for (int round = 0; round < 3; round++)
                {
                    foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Advance;
                    for (int k = 0; k < 10; k++) Tick();
                    foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Charge;
                    for (int k = 0; k < 90; k++) Tick();
                }
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double gap = ShortestManGap(voice);
                Console.WriteLine("        step 79 then: a flip back to a charge 3 s later - the units' roar cooling, 0 cries; with the unit cooldown at 0 a charge 5 s after the roar: "
                                  + manDropped + " shouters dropped by their own 8 s; 3 more roars 5 s apart: " + voice.Cries.Count + " cries in all, the peak in any 1 s "
                                  + PeakInAnySecond(voice.Cries.Select(c => c.T)) + ", one man's shortest gap " + F2s(gap) + " s; the cries' own cost "
                                  + ((ws.CostMs - cost0) / 300).ToString("0.0000", CultureInfo.InvariantCulture) + " ms a tick, " + allocated + " bytes over 300 ticks");
                Check(manDropped > 0, "nobody was held by his own 8 s on a charge 5 s after the roar");
                Check(double.IsNaN(gap) || gap >= 8.0 - 1e-9, "a man shouted twice within 8 s: " + gap);
                Check(PeakInAnySecond(voice.Cries.Select(c => c.T)) <= 16, "the battle's cap broken over the run");
                Check(allocated < 64 * 1024, "war cries allocate: " + allocated + " bytes over 300 ticks");
                LogHas("[warcry] first cry dropped by the man's own limit at ");

                // ---- switched off mid-stagger: everything waiting is dropped, nothing plays; the master switch the same
                S.Set(SettingsSchema.WarCryChargeUnitCooldownSeconds, 15.0, SettingSources.Mcm);
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Advance;
                for (int k = 0; k < 400; k++) Tick(); // 20 s - every cooldown over
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Charge;
                Tick(0.25);
                Tick(0.01);
                int waiting = logic.WarCriesQueued, before = voice.Cries.Count;
                S.Set(SettingsSchema.WarCriesEnabled, false, SettingSources.Mcm);
                for (int k = 0; k < 40; k++) Tick();
                Check(waiting > 0 && logic.WarCriesQueued == 0 && voice.Cries.Count == before, "WarCriesEnabled off did not drop the " + waiting + " waiting cries");
                LogHas("[warcry] war cries switched OFF at ");
                S.Set(SettingsSchema.WarCriesEnabled, true, SettingSources.Mcm);
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Advance;
                for (int k = 0; k < 400; k++) Tick();
                foreach (var s in slots) orders.Orders[s] = BraceOrderKind.Charge;
                Tick(0.25);
                Tick(0.01);
                int waiting2 = logic.WarCriesQueued;
                before = voice.Cries.Count;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                for (int k = 0; k < 40; k++) Tick();
                Check(waiting2 > 0 && logic.WarCriesQueued == 0 && voice.Cries.Count == before, "ModEnabled off did not drop the " + waiting2 + " waiting cries");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Console.WriteLine("        step 79 the switches: " + waiting + " waiting cries dropped by WarCriesEnabled off, " + waiting2 + " by the master switch - 0 played");
                logic.WriteWarCrySummary();
                LogHas("[summary] war cries (step 79) - ON: cries ");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                WarCryDefaults();
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>One seeded meshed grind (step 78c's) with the cries on or off; the rests' numbers and the voice.</summary>
        private static (RestStats Rests, WarCryStats Cries, FakeWarCryBody Voice) Grind79(bool cries, double seconds)
        {
            var body = new FakeCourageBody();
            var rhythm = new FakeRhythmBody();
            var logic = NewStep78cLogic(body, rhythm, rotation: true, seed: 7903);
            WarCryDefaults(cries);
            var voice = new FakeWarCryBody();
            logic.WarCryBody = voice;
            logic.WarCryDice = new SeededRandom(7904);
            CallPrivate(logic, "StartWarCries");
            body.Kinds[1] = body.Kinds[2] = BraceOrderKind.Advance;
            var sprint = (FakeSprintBody)logic.SprintBody;
            for (int k = 0; k < 3; k++)
                for (int f = 0; f < 14; f++)
                {
                    var a = FearMan(logic, body, 57000 + k * 14 + f, -6.5 + f, -k * 1.0, 0, 1, legionary: true, morale: 70, bar: 1.0);
                    var b = FearMan(logic, body, 57100 + k * 14 + f, -6.0 + f, -1.0 + k * 1.0, 1, 2, legionary: true, morale: 70, bar: 1.0);
                    sprint.Slots[a.AgentIndex] = 1;
                    sprint.Slots[b.AgentIndex] = GameSprintBody.SlotsPerTeam + 2;
                }
            RunGrind(logic, body, 106000, seconds, 0.008, new Random(793), each: now => logic.TickWarCries(now), lines: true);
            return (logic.RestStats, logic.WarCryStats, voice);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RestedMenShoutGoingBackIn()
        {
            var keep = _logic;
            try
            {
                var off = Grind79(cries: false, seconds: 240);
                var on = Grind79(cries: true, seconds: 240);
                var ws = on.Cries;
                int bi = (int)WarCryCause.BackIn, an = (int)WarCryCause.Answer;
                double backShare = ws.Events[bi] - ws.NotEligible[bi] > 0 ? (double)ws.RollsWon[bi] / Math.Max(1, ws.Events[bi] - ws.NotEligible[bi] - ws.Dropped[bi, (int)WarCryGate.Man]) : double.NaN;
                double ansShare = ws.Events[an] > 0 ? (double)ws.RollsWon[an] / Math.Max(1, ws.Events[an] - ws.NotEligible[an] - ws.Dropped[an, (int)WarCryGate.Man]) : double.NaN;
                double gap = ShortestManGap(on.Voice);
                Console.WriteLine("        step 79 the meshed grind (14 x 3 a side, 240 s): rests " + on.Rests.Started + " (cries off: " + off.Rests.Started + "), back in by his own mark "
                                  + on.Rests.Ends[(int)RestIn.OwnMark] + " (off " + off.Rests.Ends[(int)RestIn.OwnMark] + "), by contagion " + on.Rests.Ends[(int)RestIn.Contagion] + " (off "
                                  + off.Rests.Ends[(int)RestIn.Contagion] + "); BACK-IN cries " + ws.Cried[bi] + " of " + ws.Events[bi] + " (rolls won " + F2s(backShare) + " of the free men), "
                                  + "ANSWERS " + ws.Cried[an] + " of " + ws.Events[an] + " (" + F2s(ansShare) + "), held by his own 8 s " + (ws.Dropped[bi, (int)WarCryGate.Man] + ws.Dropped[an, (int)WarCryGate.Man])
                                  + "; one man's shortest gap " + F2s(gap) + " s; the cries-off grind played " + off.Voice.Cries.Count);
                Check(on.Rests.Started == off.Rests.Started && on.Rests.Ends.SequenceEqual(off.Rests.Ends) && Math.Abs(on.Rests.BackSum - off.Rests.BackSum) < 1e-9,
                    "the war cries changed the rests (sound only): " + on.Rests.Started + " vs " + off.Rests.Started);
                Check(off.Voice.Cries.Count == 0, "cries with WarCriesEnabled off");
                Check(ws.Events[bi] == on.Rests.Ends[(int)RestIn.OwnMark], "back-in events " + ws.Events[bi] + " != own-mark ends " + on.Rests.Ends[(int)RestIn.OwnMark]);
                Check(ws.Events[an] == on.Rests.Ends[(int)RestIn.Contagion], "answer events " + ws.Events[an] + " != contagion ends " + on.Rests.Ends[(int)RestIn.Contagion]);
                Check(ws.Events[bi] >= 20 && ws.Cried[bi] > 0 && backShare > 0.15 && backShare < 0.6, "back-in cries off their 35%: " + backShare + " of " + ws.Events[bi]);
                Check(ws.Events[an] >= 20 && ws.Cried[an] > 0 && ansShare > 0.05 && ansShare < 0.4, "answers off their 20%: " + ansShare + " of " + ws.Events[an]);
                Check(double.IsNaN(gap) || gap >= 8.0 - 1e-9, "a man shouted twice within 8 s in the grind: " + gap);
                LogHas("[warcry] first WAR CRY (back in) at ");
                LogHas("[warcry] first WAR CRY (contagion answer) at ");
            }
            finally
            {
                WarCryDefaults();
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }
    }
}
