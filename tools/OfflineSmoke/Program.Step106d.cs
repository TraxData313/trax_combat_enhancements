using System;
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
    /// Offline check for step 106d - OPENINGS, THE PAYOFF LIVE (AI_NOTES "106d - built", DESIGN decision 95 (l)): through the REAL logic, the damage
    /// decorator's perfect-strike hook and the real queues, with stand-ins of the engine (his guard at contact, your swing, your weapon, the forced
    /// man's body): the whole chain with each weapon class - block → off balance → your follow-up on the flash (the live roll through, his guard
    /// held) → it lands → HE REELS (ReelCause.Opening, our forced stagger from the tick, the forced guard let go) → "OPENING!" (gold feed + centre) →
    /// the shown side = a PERFECT STRIKE x by the weapon (a sword's overhead x4, an axe's left x3 after its overhead stagger, a spear's overhead stab
    /// x2 after its thrust); the SHIELDED 4-move path (the first block holds, the second LOWERS the shield - "GUARD BROKEN" -, the hit, the strike);
    /// a hit from BEHIND (a stagger + the window at timing 0, x2); another side on the reeling man (normal damage, step 105 asked on an opening's
    /// reel); the defensive game with an AXE (his blocked overhead finished by your left swing, x3 - the arrow's side); the live switch and the
    /// master switch close the window; AI vs AI changes nothing; no errors; the [summary] lines.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OpeningPayoffThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("Opening", StringComparison.Ordinal) || p.Key.StartsWith("TimedBlock", StringComparison.Ordinal)
                                                             || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal) || p.Key.StartsWith("ReelingHit", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, false, SettingSources.File);
                Check(SettingsSchema.OpeningsEnabled.Default == 1 && OpeningRules.From(S).Enabled && PerfectStrikeRules.From(S).OpeningOn,
                    "the openings are not live by default (step 106d ships them ON)");
                var inputs = new FakeImpactInputs { Damage = 20 };
                var anim = new FakeAnimBody();
                var body = new FakeOpeningBody();
                var guard = new FakeGuardBody();
                var sink = new FakeMessageSink();
                var impactBody = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = impactBody;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.MessageSink = sink;
                logic.AnimBody = anim;
                logic.OpeningBody = body;
                logic.ImpactDice = new ScriptDice { Rest = 0.999 }; // no impact outcome rolls up (step 105's 80 % neither)
                logic.OpeningDice = new ScriptDice { Rest = 0.0 }; // the first candidate guard; every live roll through unless his chance is 100 %
                var guards = new System.Collections.Generic.Dictionary<int, int>();
                int swingDir = OpeningMath.Unknown, weapon = OpeningMath.AllAttacks;
                logic.OpeningGuardRead = a => guards.TryGetValue(a.Index, out int g) ? g : -1;
                logic.OpeningSwingRead = a => swingDir;
                logic.OpeningWeaponRead = a => weapon;
                double t = 106400;
                logic.TickImpact(t);
                Check(ReferenceEquals(ImpactHooks.Perfect, logic), "the logic is not the decorator's perfect-strike hook");
                int next = 10640;
                var st = logic.OpeningStats;
                var ps = logic.PerfectStrikeStats;
                var fs = logic.OpeningForceStats;
                var ar = Rules;
                int axe = OpeningMath.WeaponMask(true, false), spear = OpeningMath.WeaponMask(false, true);

                TrackedAgent Man(bool shield)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, 1.0);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (shield ? 4.0 : 1.2, 100);
                    if (shield)
                    {
                        inputs.Shields.Add(i);
                        anim.Shields.Add(i);
                    }
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                double Now() => t - 1.0 / 60;
                void Parried(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, false, false, CombatCollisionResult.Parried));
                void ShieldRight(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, true, true, CombatCollisionResult.Blocked));
                void Lands(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d)
                {
                    swingDir = (int)d;
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, false, false, CombatCollisionResult.StrikeAgent));
                }
                void WindUp(TrackedAgent you2, int dir)
                {
                    swingDir = dir;
                    logic.ObserveAction(you2, ActReady, Now(), in ar);
                }
                // in game the damage is decided first (the decorator's last question), then the impact decision - the same order here
                float Strike(TrackedAgent you2, TrackedAgent on, Agent.UsageDirection d, float damage = 20f)
                {
                    float got = ImpactHooks.PerfectStrike(you2.Agent, on.Agent, false, Swing(d, false, false, CombatCollisionResult.StrikeAgent), damage);
                    Lands(you2, on, d);
                    return got;
                }

                var you = Man(true);
                body.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);

                // one whole UNSHIELDED chain with a weapon: his parry of your `first` (his guard `held`) → off balance; your follow-up on the flash;
                // it lands → he reels; the shown side → PERFECT STRIKE x`k`
                TrackedAgent Chain(int w, Agent.UsageDirection first, int held, int follow, int finish, float k, string what)
                {
                    weapon = w;
                    var ai = Man(false);
                    guards[ai.Agent.Index] = held;
                    int asked = st.LiveAsked, staggers = impactBody.Staggers.Count, shown = sink.Lines.Count(l => l.Text == AthleticsLogic.OpeningText);
                    Parried(you, ai, first);
                    Check(logic.OpeningForceKind == OpeningForceKind.Balance && logic.LastOpening.Opening == follow,
                        what + ": his parry did not throw him off balance to your " + OpeningMath.AttackName(follow) + ": " + OpeningMath.AttackName(logic.LastOpening.Opening));
                    Tick(0.3);
                    WindUp(you, follow);
                    Check(logic.OpeningForceKind == OpeningForceKind.Held && Math.Abs(logic.LastOpeningScore - 1) < 1e-6, what + ": your follow-up on the flash was not held through");
                    Tick(0.3);
                    Lands(you, ai, (Agent.UsageDirection)follow);
                    double reel = logic.LastOpeningReelAsked;
                    Check(logic.LastOpeningHit == OpeningHit.Staggers && st.LiveAsked == asked + 1 && ai.Impact.Reeling(Now()) && ai.Impact.ReelCause == ReelCause.Opening
                          && Math.Abs(reel - 0.8) < 1e-6 && logic.OpeningForced == null,
                        what + ": your landed follow-up did not make him REEL 0.8 s (an opening) / let the forced guard go: " + logic.LastOpeningHit + " " + ai.Impact.ReelCause + " " + reel);
                    Check(logic.PerfectStrikeWindowsOpen >= 1, what + ": no perfect-strike window on the reeling man");
                    Tick();
                    Check(impactBody.Staggers.Count == staggers + 1 && impactBody.Staggers.Last().Man == ai.AgentIndex, what + ": our forced stagger was not played from the tick");
                    Check(sink.Lines.Count(l => l.Text == AthleticsLogic.OpeningText && l.Hex == PerfectStrikeMath.GoldHex) == shown + 1 && sink.Centred.Contains(AthleticsLogic.OpeningText),
                        what + ": no gold OPENING! in the feed and the centre");
                    Tick(0.2);
                    float got = Strike(you, ai, (Agent.UsageDirection)finish);
                    Check(Math.Abs(got - 20f * k) < 1e-3, what + ": your " + OpeningMath.AttackName(finish) + " on the reeling man did not land x" + k + ": " + got);
                    Check(logic.LastOpeningHit == OpeningHit.FinishPerfect && !logic.OpeningChain.Active, what + ": the chain did not end in its finish: " + logic.LastOpeningHit);
                    Tick();
                    Check(sink.Lines.Any(l => l.Text == PerfectStrikeMath.Text(k)), what + ": no " + PerfectStrikeMath.Text(k) + " line");
                    Tick(3);
                    return ai;
                }

                // ---- 1. a SWORD: your right swing parried (his left guard) → the high guard (your thrust); it lands → the overhead finishes, x4
                Chain(OpeningMath.AllAttacks, Agent.UsageDirection.AttackRight, OpeningMath.GuardLeft, OpeningMath.AttackDown, OpeningMath.AttackUp, 4f, "a sword");
                LogHas("[impact] openings: first LIVE stagger at ");
                LogHas("[impact] openings: first opening stagger PLAYED at ");
                LogHas("after your OPENING (the shown side: your overhead)");
                // ---- 2. an AXE (no thrust): the low guard (your overhead); its stagger is finished by your LEFT (or right) swing, x3
                Chain(axe, Agent.UsageDirection.AttackRight, OpeningMath.GuardLeft, OpeningMath.AttackUp, OpeningMath.AttackLeft, 3f, "an axe");
                LogHas("the weapon's relaxed side");
                // ---- 3. a SPEAR: your thrust parried (his low guard) → the high guard (your thrust again); its stagger finished by the overhead stab, x2
                Chain(spear, Agent.UsageDirection.AttackDown, OpeningMath.GuardDown, OpeningMath.AttackDown, OpeningMath.AttackUp, 2f, "a spear");
                Check(ps.ByDirections[0] == 1 && ps.ByDirections[1] == 1 && ps.ByDirections[2] == 1 && ps.OpeningLanded == 3, "the strikes by the weapon's directions are not 1 / 1 / 1");
                weapon = OpeningMath.AllAttacks;

                // ---- 4. THE SHIELD, 4 MOVES: the 1st block holds the side (he wins), the 2nd LOWERS the shield - GUARD BROKEN; the hit; the strike
                var sh = Man(true);
                guards[sh.Agent.Index] = OpeningMath.GuardUp;
                ShieldRight(you, sh, Agent.UsageDirection.AttackUp);
                Tick(0.3);
                WindUp(you, logic.LastOpening.Opening);
                Check(logic.OpeningForced == null && fs.Ends[(int)OpeningForceEnd.HeBlocks] >= 1, "the shield's first block did not hold the side");
                Tick(0.3);
                ShieldRight(you, sh, Agent.UsageDirection.AttackUp);
                Tick(0.3);
                int side = logic.LastOpening.Opening;
                WindUp(you, side);
                Check(logic.OpeningForceLowered && sh.Input!.OpeningLower, "the shield's second block did not LOWER his shield");
                Tick();
                Check(sink.Lines.Any(l => l.Text == AthleticsLogic.GuardBrokenText && l.Hex == AthleticsLogic.GuardBrokenHex) && st.GuardBrokenShown == 1, "no GUARD BROKEN in the feed");
                Tick(0.2);
                Lands(you, sh, (Agent.UsageDirection)side);
                Check(sh.Impact.Reeling(Now()) && sh.Impact.ReelCause == ReelCause.Opening && Math.Abs(logic.LastOpeningReelAsked - 1.1) < 1e-6 && logic.OpeningForced == null,
                    "your hit through the lowered shield did not make him reel 1.1 s (link 2): " + logic.LastOpeningReelAsked);
                int shFinish = OpeningMath.Opposite(side);
                Tick(0.2);
                Check(Math.Abs(Strike(you, sh, (Agent.UsageDirection)shFinish) - 80f) < 1e-3, "the shield's 4th move was not a PERFECT STRIKE x4");
                Tick(3);

                // ---- 5. a hit from BEHIND: a stagger and the window at timing 0 (x2 with a sword); another side first = normal damage, step 105 asked
                var back = Man(false);
                inputs.Angles[back.Agent.Index] = 170;
                Lands(you, back, Agent.UsageDirection.AttackRight);
                Check(logic.LastOpeningHit == OpeningHit.Staggers && back.Impact.ReelCause == ReelCause.Opening && st.Staggers[(int)OpeningEntry.Back] == 1,
                    "your hit from behind did not stagger him (an opening)");
                inputs.Angles.Remove(back.Agent.Index);
                Tick(0.2);
                float wrong = ImpactHooks.PerfectStrike(you.Agent, back.Agent, false, Swing(Agent.UsageDirection.AttackRight, false, false, CombatCollisionResult.StrikeAgent), 20f);
                Check(Math.Abs(wrong - 20f) < 1e-3 && ps.OpeningWrongSide == 1 && logic.PerfectStrikeWindowsOpen == 1, "another side on the reeling man was not normal damage with the window kept");
                Lands(you, back, Agent.UsageDirection.AttackRight);
                Check(logic.LastImpact.ReelingHit.Asked && logic.LastImpact.ReelingHit.Cause == ReelCause.Opening, "step 105's knockdown was not asked on an opening's reel");
                Tick(3);
                var back2 = Man(false);
                inputs.Angles[back2.Agent.Index] = 170;
                Lands(you, back2, Agent.UsageDirection.AttackRight);
                inputs.Angles.Remove(back2.Agent.Index);
                Tick(0.2);
                Check(Math.Abs(Strike(you, back2, Agent.UsageDirection.AttackLeft) - 40f) < 1e-3, "the strike after a back hit was not x2 (timing 0, a sword)");
                Tick(3);

                // ---- 6. the DEFENSIVE game with an AXE: his blocked overhead finished by your LEFT swing (the arrow's side), x3
                weapon = axe;
                guard.Keys[you.Agent.Index] = 0;
                Tick(0.1);
                guard.Keys[you.Agent.Index] = 0x800;
                Tick(0.05);
                var att = Man(false);
                logic.ImpactModelHitTaken(att.Agent, you.Agent, Swing(Agent.UsageDirection.AttackUp, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(logic.PerfectStrikeWindowsOpen == 1 && Math.Abs(logic.LastTimedBlock.Score - 1) < 1e-9, "your perfect block opened no window");
                Tick(0.2);
                float dLeft = ImpactHooks.PerfectStrike(you.Agent, att.Agent, false, Swing(Agent.UsageDirection.AttackLeft, false, false, CombatCollisionResult.StrikeAgent), 20f);
                Check(Math.Abs(dLeft - 60f) < 1e-3 && ps.RelaxedLanded >= 2, "an axe's left swing after his blocked overhead was not a PERFECT STRIKE x3: " + dLeft);
                weapon = OpeningMath.AllAttacks;
                Tick(3);

                // ---- 7. the live switch / the master switch mid-reel: the window closes, normal damage; AI vs AI changes nothing
                foreach (var sw in new[] { SettingsSchema.OpeningsEnabled, SettingsSchema.ModEnabled })
                {
                    var m = Man(false);
                    inputs.Angles[m.Agent.Index] = 170;
                    Lands(you, m, Agent.UsageDirection.AttackRight);
                    inputs.Angles.Remove(m.Agent.Index);
                    Check(logic.PerfectStrikeWindowsOpen == 1, "no window before the switch test (" + sw.Key + ")");
                    S.Set(sw, false, SettingSources.File);
                    Tick();
                    Check(logic.PerfectStrikeWindowsOpen == 0, sw.Key + " off did not close the opening's window");
                    float n = ImpactHooks.PerfectStrike(you.Agent, m.Agent, false, Swing(Agent.UsageDirection.AttackLeft, false, false, CombatCollisionResult.StrikeAgent), 20f);
                    Check(Math.Abs(n - 20f) < 1e-3, sw.Key + " off still multiplied");
                    S.Set(sw, true, SettingSources.File);
                    Tick(3);
                }
                int askedBefore = st.LiveAsked;
                var a1 = Man(false);
                var a2 = Man(false);
                inputs.Angles[a2.Agent.Index] = 170;
                logic.ImpactModelHitTaken(a1.Agent, a2.Agent, Swing(Agent.UsageDirection.AttackRight, false, false, CombatCollisionResult.StrikeAgent));
                Check(st.LiveAsked == askedBefore && a2.Impact.ReelCause != ReelCause.Opening, "an AI man's hit from behind on another made an opening");

                // ---- 8. no errors; the [summary] lines
                Check(st.Errors == 0 && fs.Errors == 0 && ps.Errors == 0, "errors: openings " + st.Errors + ", force " + fs.Errors + ", strike " + ps.Errors);
                string line = st.SummaryLine(OpeningRules.From(S));
                Check(line.StartsWith("openings (step 106d; live ON", StringComparison.Ordinal) && line.Contains("LIVE STAGGERS asked " + st.LiveAsked.ToString())
                      && line.Contains("GUARD BROKEN shown 1"), "the [summary] openings line: " + line);
                string pline = ps.SummaryLine(PerfectStrikeRules.From(S));
                Check(pline.Contains("(by an opening's stagger ") && pline.Contains("after an opening 5"), "the [summary] perfect strike line: " + pline);
                Console.WriteLine("        step 106d: " + st.LiveAsked + " opening staggers, " + st.LiveWindows + " windows, " + ps.OpeningLanded + " perfect strikes after an opening");
                Console.WriteLine("        " + line);
                Console.WriteLine("        " + pline);
            }
            finally
            {
                if (_logic != null) _logic.SmokePlayer = null;
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactHooks.PushOut = null;
                ImpactHooks.Perfect = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("Opening", StringComparison.Ordinal) || p.Key.StartsWith("TimedBlock", StringComparison.Ordinal)
                                                             || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal) || p.Key.StartsWith("ReelingHit", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, SettingsSchema.ImpactMeetRulesEnabled.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}
