using System;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 103 - CROUCH REST (AI_NOTES "Step 103", DESIGN decision 92): through the REAL logic's crouch seam with a stand-in for
    /// the engine's <c>Agent.CrouchMode</c> - you crouched refill x2 (an empty bar: 30 s instead of 60), standing x1, mounted x1; an AI man's crouch
    /// is never even read while CrouchRefillForAi is off, x2 when on; the rest after a blow still holds; your bar reads "Resting" (the API the HUD
    /// reads) only while it refills crouched; the first-time line; the switch and the master switch = x1; the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CrouchRestThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                foreach (var p in new[] { SettingsSchema.CrouchRefillMultiplier, SettingsSchema.CrouchRefillForPlayer, SettingsSchema.CrouchRefillForAi })
                    S.Set(p, p.Default, SettingSources.File);
                Check(Near(SettingsSchema.CrouchRefillMultiplier.Default, 2.0) && SettingsSchema.CrouchRefillForPlayer.Default == 1 && SettingsSchema.CrouchRefillForAi.Default == 0,
                    "the embedded crouch-rest defaults are not the design's (2.0 / true / false)");
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                var you = logic.Track(FakeAgent(7400))!;
                var ai = logic.Track(FakeAgent(7401))!;
                you.AthleticsSkill = 100;
                ai.AthleticsSkill = 100;
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                bool youCrouch = true, aiCrouch = true;
                int aiReads = 0;
                logic.CrouchRead = a =>
                {
                    if (ReferenceEquals(a, you.Agent)) return youCrouch;
                    aiReads++;
                    return aiCrouch;
                };
                var r = AthleticsRules.From(S);

                // one regen step through the logic's own seam, as RegenPass runs it
                RegenOutcome Step(TrackedAgent st, double now, double dt, bool mounted, out double k)
                {
                    var cr = CrouchRestRules.From(S);
                    k = logic.CrouchMultiplierFor(st, mounted, in cr, now);
                    st.CrouchResting = false;
                    var o = AthleticsMath.Regen(st, in r, now, dt, 0f, 5f, k);
                    logic.NoteCrouchRefill(st, k, dt, in o, in r);
                    return o;
                }
                double RefillSeconds(TrackedAgent st, bool crouched)
                {
                    youCrouch = crouched;
                    AthleticsMath.Charge(st, AthleticsRules.From(S), 0);
                    while (st.Fraction > 1e-9) AthleticsMath.Charge(st, r, 0);
                    double t = r.RegenDelaySeconds;
                    for (int n = 0; n < 20000 && st.Fraction < 1 - 1e-9; n++) Step(st, t += 0.05, 0.05, false, out _);
                    return t - r.RegenDelaySeconds;
                }

                // ---- 1. you crouched: x2 - an empty bar refills in half the time; standing: the normal time
                double crouched = RefillSeconds(you, true);
                double standing = RefillSeconds(you, false);
                Check(Math.Abs(standing - 2 * crouched) < 0.2 && standing > 10, "crouched did not refill twice as fast: standing " + standing + " s, crouched " + crouched + " s");
                LogHas("[athletics] YOU: first crouch at ");
                LogHas("crouch rest ON: crouched (Z) the Athletics refill runs x2.0 for you");

                // ---- 2. the rest after a blow holds while crouched; the bar's "Resting" only while it refills crouched
                youCrouch = true;
                AthleticsMath.Charge(you, r, 1000);
                Step(you, 1001, 0.1, false, out double k1);
                Check(Near(k1, 2.0) && !you.CrouchResting && !AthleticsLogic.IsCrouchResting(you.Agent), "crouched inside the rest after a blow: refilled or showed Resting");
                Step(you, 1010, 0.1, false, out _);
                Check(you.CrouchResting && AthleticsLogic.IsCrouchResting(you.Agent), "crouched and refilling, the bar's Resting was not set");
                youCrouch = false;
                Step(you, 1010.1, 0.1, false, out double k0);
                Check(Near(k0, 1.0) && !you.CrouchResting, "standing up did not end the Resting / the x2");
                Check(BarMath.Label(false, true) == "Resting", "the bar's resting label");

                // ---- 3. mounted: x1; the AI: never read while off, x2 when on
                youCrouch = true;
                Step(you, 1011, 0.1, true, out double kMounted);
                Check(Near(kMounted, 1.0), "a mounted crouch counted");
                AthleticsMath.Charge(ai, r, 1000);
                Step(ai, 1010, 0.1, false, out double kAi);
                Check(Near(kAi, 1.0) && aiReads == 0, "the AI's crouch counted / was read while CrouchRefillForAi is off (reads " + aiReads + ")");
                S.Set(SettingsSchema.CrouchRefillForAi, true, SettingSources.File);
                Step(ai, 1010.2, 0.1, false, out kAi);
                Check(Near(kAi, 2.0) && aiReads == 1, "CrouchRefillForAi on: the AI's crouch did not count");
                S.Set(SettingsSchema.CrouchRefillForAi, false, SettingSources.File);

                // ---- 4. the switch and the MASTER SWITCH: x1
                S.Set(SettingsSchema.CrouchRefillForPlayer, false, SettingSources.File);
                Step(you, 1011.5, 0.1, false, out double kOff);
                Check(Near(kOff, 1.0), "CrouchRefillForPlayer off still multiplied");
                S.Set(SettingsSchema.CrouchRefillForPlayer, true, SettingSources.File);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Check(Near(logic.CrouchMultiplierFor(you, false, CrouchRestRules.From(S), 1012), 1.0), "the MASTER SWITCH off still multiplied the refill");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);

                // ---- 5. the [summary] line
                var st = logic.CrouchRestStats;
                string line = st.SummaryLine(CrouchRestRules.From(S));
                Check(line.StartsWith("crouch rest (step 103; ON, x2.0): you crouched ", StringComparison.Ordinal) && st.PlayerSeconds > 10 && st.PlayerPoints > 50
                      && line.Contains("of them the crouch's extra") && line.Contains("the AI off - 0.1 s crouched"),
                    "the [summary] line: " + line);
                Console.WriteLine("        step 103: standing " + standing.ToString("0.0") + " s, crouched " + crouched.ToString("0.0") + " s from empty to full");
                Console.WriteLine("        " + line);
            }
            finally
            {
                if (_logic != null)
                {
                    _logic.SmokePlayer = null;
                    _logic.CrouchRead = a => a.CrouchMode;
                }
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                foreach (var p in new[] { SettingsSchema.CrouchRefillMultiplier, SettingsSchema.CrouchRefillForPlayer, SettingsSchema.CrouchRefillForAi, SettingsSchema.ModEnabled })
                    S.Set(p, p.Default, SettingSources.File);
            }
        }
    }
}
