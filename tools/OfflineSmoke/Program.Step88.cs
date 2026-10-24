using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 88 - A WEAPON PUSH FOR MEN WITHOUT A SHIELD + NEW SHARES (Toni 2026-10-24; AI_NOTES "Step 88"): the engine's codes
    /// (the push = the game's WeaponBash action, the same as a shield bash - the item usage sets' kick usages in the game's own ModuleData), step 80's
    /// two grinding lines with the shipped shares (shieldman 25 / 40 / 35, no shield 35 / 20 / 45) - the men without a shield PUSHING, played as
    /// WeaponBash, charged from the stack, landing on enemies; the push's meet through the real seams (a guard UP the correct meet, the bash's rows
    /// otherwise) and the AI's read raising the guard UP.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>
        /// THE CODES: the push's action is the engine's WeaponBash (31) like a shield bash - step 18's tracker charges and counts it; in the game's
        /// own ModuleData every weapon bash (act_1h_bash, act_2h_bash, act_staff_bash) and the shield bash are actt_weapon_bash, and the weapon usage
        /// sets carry them as their kick usages (the one-hander's only with the left hand empty - no shield); the input bits the push writes.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushCodesMatchTheGame()
        {
            Check(KickBashTracker.ActionWeaponBash == (int)Agent.ActionCodeType.WeaponBash && KickBashTracker.ActionKick == (int)Agent.ActionCodeType.Kick,
                "the engine's WeaponBash / Kick codes moved");
            Check(KickBashTracker.KindOf((int)Agent.ActionCodeType.WeaponBash) == KickBashKind.Bash && AiKickMath.Shown(AiKickKind.Push) == KickBashKind.Bash,
                "a push is not seen as the engine's WeaponBash by step 18's tracker");
            Check(AiKickMath.DefendUp == (uint)Agent.MovementControlFlag.DefendUp && AiKickMath.KickEventBit == (uint)Agent.EventControlFlag.Kick
                  && ImpactMeetMath.GuardFlag(true, true) == (uint)Agent.MovementControlFlag.DefendUp,
                "the push's input bits (DefendUp, the Kick event) or the read's UP guard moved");
            string data = Path.Combine(_gameFolder, "Modules", "Native", "ModuleData");
            string actions = File.ReadAllText(Path.Combine(data, "action_types.xml"));
            foreach (var act in new[] { "act_1h_bash", "act_2h_bash", "act_staff_bash", "act_shield_bash" })
                Check(Regex.IsMatch(actions, "name=\"" + act + "\"\\s+type=\"actt_weapon_bash\""), "the game's " + act + " is not an actt_weapon_bash action");
            string usages = File.ReadAllText(Path.Combine(data, "item_usage_sets.xml"));
            string KickUsages(string set)
            {
                var m = Regex.Match(usages, "<item_usage_set\\s+id=\"" + set + "\"(.*?)</item_usage_set>", RegexOptions.Singleline);
                if (!m.Success) return string.Empty;
                var k = Regex.Match(m.Groups[1].Value, "<kick_usages>(.*?)</kick_usages>", RegexOptions.Singleline);
                return k.Success ? k.Groups[1].Value : string.Empty;
            }
            string oneH = KickUsages("onehanded_shield_swing"), twoH = KickUsages("twohanded_block_swing"), staff = KickUsages("polearm_block"), shield = KickUsages("shield"),
                none = KickUsages("no_weapon");
            Check(oneH.Contains("release_action=\"act_1h_bash\"") && oneH.Contains("left_hand_empty=\"True\""), "the one-hander's weapon bash (left hand empty) is not in the game's usage set");
            Check(twoH.Contains("release_action=\"act_2h_bash\""), "the two-hander's weapon bash is not in the game's usage set");
            Check(staff.Contains("release_action=\"act_staff_bash\""), "the polearm's weapon bash is not in the game's usage set");
            Check(shield.Contains("release_action=\"act_shield_bash\""), "the shield's bash is not in the game's usage set");
            Check(none.Contains("release_action=\"act_kick_right_leg\""), "an empty hand's kick usage is not the leg kick");
            Console.WriteLine("        step 88 codes: WeaponBash " + (int)Agent.ActionCodeType.WeaponBash + " (a shield bash and a weapon push alike), DefendUp 0x"
                              + AiKickMath.DefendUp.ToString("X") + "; the game's act_1h_bash / act_2h_bash / act_staff_bash / act_shield_bash all actt_weapon_bash; the kick usages of"
                              + " onehanded_shield_swing (left hand empty), twohanded_block_swing, polearm_block and shield are their bashes");
        }

        /// <summary>
        /// THE PUSHES IN A GRIND: step 80's two two-deep lines (half shieldmen) for 3 minutes with the SHIPPED shares and the swing stack on - the
        /// rolls near 25 / 40 / 35 and 35 / 20 / 45; the men without a shield PUSH: the guard first, the key with it held, the engine's WeaponBash seen
        /// (played as asked), charged from the stack (never at 0), on a bash's strike path, landing on enemies.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushesInAGrind()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                AiKickDefaults();
                Step88Shares();
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.File);
                foreach (var p in new[]
                         {
                             SettingsSchema.AiKickFriendClearMetres, SettingsSchema.AiBashPathHalfWidthMetres, SettingsSchema.AiKickRearRankMetres, SettingsSchema.SwingStackEnabled,
                             SettingsSchema.SwingStackMinimum, SettingsSchema.SwingStackPointsPerSwing, SettingsSchema.KicksUseSwings, SettingsSchema.CostPerKickOrBash, SettingsSchema.CostPerBlow,
                         })
                    S.Set(p, p.Default, SettingSources.File);
                Step80PathDefaults(); // step 90 narrowed the shipped path - these numbers are step 80's path
                Check(S.AiShieldmanKickPercent == 25 && S.AiShieldmanBashPercent == 40 && S.AiNoShieldKickPercent == 35 && S.AiNoShieldPushPercent == 20,
                    "the shipped step-88 shares are not 25 / 40 and 35 / 20: " + S.AiShieldmanKickPercent + " / " + S.AiShieldmanBashPercent + ", " + S.AiNoShieldKickPercent + " / " + S.AiNoShieldPushPercent);
                var g = FightTwoLines(88, 8800, stack: true);
                var st = g.Stats;
                double Share(int type, AiKickKind k, bool done)
                {
                    int n = st.Moves(type);
                    return n > 0 ? (double)(done ? st.Done(type, k) : st.Rolled(type, k)) / n : 0;
                }
                string Pct(double v) => (100 * v).ToString("0.0", CultureInfo.InvariantCulture) + "%";
                Console.WriteLine("        step 88 two grinding lines, 3 minutes, the shipped shares - " + g.SharesLine);
                Console.WriteLine("        step 88 pushes: asked " + st.AskedCount(AiKickKind.Push) + " → played " + st.PlayedCount(AiKickKind.Push) + " → on an ENEMY " + st.EnemyHits(AiKickKind.Push)
                                  + ", on a FRIEND " + st.FriendHits(AiKickKind.Push) + ", missed " + st.OutcomeCount(AiKickKind.Push, AiKickOutcome.Miss) + "; played as the other kind "
                                  + st.PlayedOtherKind + "; spent from the stack " + g.KicksSpent + ", at an empty stack " + g.KicksAtEmpty + "; " + g);
                Check(st.Moves(0) >= 300 && st.Moves(1) >= 300, "too few moves in kick reach: shieldmen " + st.Moves(0) + ", no shield " + st.Moves(1));
                Check(Math.Abs(Share(0, AiKickKind.Kick, false) - 0.25) < 0.05 && Math.Abs(Share(0, AiKickKind.Bash, false) - 0.40) < 0.05 && Math.Abs(Share(0, AiKickKind.None, false) - 0.35) < 0.05
                      && st.Rolled(0, AiKickKind.Push) == 0,
                    "a shieldman's rolled shares are not near 25 / 40 / 35: " + Pct(Share(0, AiKickKind.Kick, false)) + " / " + Pct(Share(0, AiKickKind.Bash, false)) + " / " + Pct(Share(0, AiKickKind.None, false)));
                Check(Math.Abs(Share(1, AiKickKind.Kick, false) - 0.35) < 0.05 && Math.Abs(Share(1, AiKickKind.Push, false) - 0.20) < 0.05 && Math.Abs(Share(1, AiKickKind.None, false) - 0.45) < 0.05
                      && st.Rolled(1, AiKickKind.Bash) == 0,
                    "a man without a shield's rolled shares are not near 35 / 20 / 45: " + Pct(Share(1, AiKickKind.Kick, false)) + " / " + Pct(Share(1, AiKickKind.Push, false)) + " / " + Pct(Share(1, AiKickKind.None, false)));
                // the pushes are DONE, PLAYED as the engine's WeaponBash, land on enemies
                Check(st.Done(1, AiKickKind.Push) > 0.08 * st.Moves(1), "the men without a shield hardly pushed: " + g.SharesLine);
                Check(st.PlayedCount(AiKickKind.Push) > 0 && st.PlayedCount(AiKickKind.Push) >= 0.8 * st.AskedCount(AiKickKind.Push) && st.PlayedOtherKind == 0,
                    "the pushes were not played as asked: asked " + st.AskedCount(AiKickKind.Push) + ", played " + st.PlayedCount(AiKickKind.Push) + ", as the other kind " + st.PlayedOtherKind);
                Check(st.EnemyHits(AiKickKind.Push) > 0, "no push landed on an enemy");
                Check(st.PathCount(AiKickKind.Push, AiKickPath.Clear) + st.PathCount(AiKickKind.Push, AiKickPath.FriendFirst) + st.PathCount(AiKickKind.Push, AiKickPath.BehindFriend) > 0,
                    "the pushes never went through the strike path");
                // the stack and the cost: a push spends a stored swing like a bash, never at 0
                Check(g.KicksAtEmpty == 0 && g.KicksSpent >= st.PlayedCount(AiKickKind.Push), "a push not charged from the stack, or started at an empty one (" + g.KicksAtEmpty + ")");
                Check(st.PointsSpent > 0, "no Athletics spent on the kicks / bashes / pushes");
                Check(g.FriendShare < 0.05, "the strike path: friends " + Pct(g.FriendShare) + " of the hits");
                LogHas("[impact] first AI weapon push asked at ");
                LogHas("(no shield: the engine plays his weapon's bash - step 88)");
                LogHas("/ push ");
                LogHas("| pushes: on an ENEMY ");
            }
            finally
            {
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                AiKickDefaults();
                S.Set(SettingsSchema.AiKicksEnabled, false, SettingSources.File);
                Step90PathDefaults();
                Step65CleanUp(keep);
            }
        }

        /// <summary>
        /// THE PUSH'S MEET through the real decorator's seams and OnMeleeHit (a stand-in engine): a spent pusher's push the shield stopped guarding UP
        /// - the PUSHER staggered (the bash's 70%) from the tick; the same push into a shield guarding RIGHT on an exhausted blocker - the blocker's
        /// row (85%); a push on a weapon guard UP with the brace off - the knock-back refused, held; a shieldman's BASH into that same guard UP - the
        /// blocker's row (up is a push's meet, not a bash's); the AI's read of a push raises the guard UP and meets it correctly; the summary's push rows.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushMeetsThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                var w = NewMeetWorld(88500);
                var logic = w.Logic;
                var pusher = MeetMan(w, 8850, 60, 0.1, false);       // a spent man with a two-handed axe, no shield
                w.Inputs.Weapons[8850] = (8.0, 0, ImpactDamageType.Blunt, 60);
                w.Inputs.Pushers.Add(8850);
                var legionary = MeetMan(w, 8851, 130, 1.0, true);
                var spentWall = MeetMan(w, 8852, 130, 0.05, true);
                var sworder = MeetMan(w, 8853, 130, 1.0, false);
                var fresh = MeetMan(w, 8854, 100, 1.0, false);
                w.Inputs.Weapons[8854] = (8.0, 0, ImpactDamageType.Blunt, 100);
                w.Inputs.Pushers.Add(8854);

                // ---- 1. a push the shield stopped, guarding UP: the PUSHER at risk - fall no, stagger yes (the bash-right ceiling 70%)
                w.Meet.Guards[legionary.AgentIndex] = ShieldGuard(MeetDir.Up);
                w.Dice.Next(0.99, 0.01, 0.5);
                int staggers = w.Body.Staggers.Count;
                MeetHit(w, pusher, legionary, BashIntoShield(), out _);
                var d = logic.LastImpact;
                Check(d.Meet == ImpactMeet.PushShieldUp && d.MeetAttacker && d.MeetBlocked && d.Riposte.Happens && !d.Knockdown.InRow && !d.Stagger.InRow,
                    "a push on a shield guarding up: " + d.Meet + ", attacker " + d.MeetAttacker + ", riposte " + d.Riposte.Happens + " (" + d.Riposte.Chance + ")");
                Check(Math.Abs(d.Riposte.Chance - 0.70) < 0.02, "the spent pusher's stagger is not the bash's ceiling 70%: " + d.Riposte.Chance);
                w.Logic.TickImpact(w.T + 0.02);
                Check(w.Body.Staggers.Skip(staggers).Any(s => s.Man == pusher.AgentIndex) && !w.Body.Staggers.Skip(staggers).Any(s => s.Man == legionary.AgentIndex),
                    "the pusher was not staggered from the tick (or the blocker was)");
                LogHas("[impact] first meet push - shield, guard up at ");
                LogHas("the shield STOPPED the push");

                // ---- 2. a fresh man's push into a shield guarding RIGHT on an exhausted blocker: the blocker's row (right is a bash's meet, not a push's)
                w.Meet.Guards[spentWall.AgentIndex] = ShieldGuard(MeetDir.Right);
                staggers = w.Body.Staggers.Count;
                w.T += 3;
                w.Dice.Next(0.99, 0.99, 0.01, 0.5);
                MeetHit(w, fresh, spentWall, BashIntoShield(), out _);
                d = logic.LastImpact;
                Check(d.Meet == ImpactMeet.PushShieldWrong && !d.MeetAttacker && d.Stagger.Happens && Math.Abs(d.Stagger.Chance - 0.85) < 0.02,
                    "a push on a shield guarding right: " + d.Meet + ", the blocker staggered " + d.Stagger.Happens + " (" + d.Stagger.Chance + ")");
                w.Logic.TickImpact(w.T + 0.02);
                Check(w.Body.Staggers.Skip(staggers).Any(s => s.Man == spentWall.AgentIndex), "the blocker was not staggered from the tick");

                // ---- 3. a push on a weapon guard UP, the brace OFF: the engine lands it, the knock-back refused all the same (the pusher at risk)
                S.Set(SettingsSchema.KickBraceEnabled, false, SettingSources.Mcm);
                w.Meet.Guards[sworder.AgentIndex] = WeaponGuard(MeetDir.Up);
                w.T += 3;
                w.Dice.Next(0.99, 0.99);
                bool back = MeetHit(w, pusher, sworder, BashLands(), out bool down);
                d = logic.LastImpact;
                Check(!back && !down && d.Meet == ImpactMeet.PushWeaponUp && d.Braced, "a push on a weapon guard up: knock-back " + back + ", " + d.Meet + ", braced " + d.Braced);
                S.Set(SettingsSchema.KickBraceEnabled, true, SettingSources.Mcm);

                // ---- 4. a shieldman's BASH into a shield guarding UP: the blocker's row - up meets a push, not a bash
                var basher = MeetMan(w, 8855, 100, 1.0, true);
                w.T += 3;
                w.Dice.Next(0.99, 0.99, 0.99);
                MeetHit(w, basher, legionary, BashIntoShield(), out _);
                Check(logic.LastImpact.Meet == ImpactMeet.BashShieldWrong, "a shield bash into a guard up is not the bash's wrong row: " + logic.LastImpact.Meet);

                // ---- 5. the AI READS a push: the guard UP after the reaction, the stopped push met correctly
                var reader = MeetMan(w, 8856, 250, 1.0, true);
                w.Meet.Guards[reader.AgentIndex] = ShieldGuard(MeetDir.Down); // his own raised guard when he does not read
                w.Meet.Abilities[reader.AgentIndex] = (1.0f, 0.0f);
                var rd = new ScriptDice();
                logic.MeetDice = rd;
                rd.Next(0.5);
                double t = w.T + 3;
                logic.MeetReadStart(pusher, reader, true, t, push: true);
                Check(reader.MeetReading && reader.MeetReadPush, "a sure read of a push was not rolled");
                logic.TickMeetReads(t + 0.16);
                Check(reader.MeetGuardUp && reader.Input!.MeetGuard && reader.Input.MeetGuardFlag == ImpactMeetMath.FlagDefendUp, "the read guard against a push is not UP");
                w.T = t + 0.45;
                w.Dice.Next(0.99, 0.01, 0.5);
                MeetHit(w, pusher, reader, BashIntoShield(), out _);
                Check(logic.LastImpact.Meet == ImpactMeet.PushShieldUp && logic.LastImpact.MeetAttacker, "the read UP guard did not meet the push: " + logic.LastImpact.Meet);
                Check(!reader.MeetReading && logic.ImpactMeetStats.ReadMet[0] >= 1, "the read did not end met at the hit");
                LogHas("[impact] first AI READ of a weapon push at ");
                LogHas("the guard UP from ");

                // ---- 6. the summary: the push rows as a group
                var lines = logic.ImpactMeetStats.SummaryLines(ImpactMeetRules.From(S));
                Check(lines[0].Contains("PUSHES (step 88 - the bash's ceilings, a guard UP the correct meet): push - shield, guard up 2 → the ATTACKER staggered 2")
                      && lines[0].Contains("push - weapon, guard up 1") && lines[0].Contains("push - shield, wrong guard 1"),
                    "the meets line's push rows: " + lines[0]);
                Console.WriteLine("        step 88 the push's meet: shield up → the pusher staggered (70%), shield right → the exhausted blocker staggered (85%), weapon up → held with the brace off,"
                                  + " a shield bash into a guard up = the blocker's row, the AI's read raises the guard UP and meets it");
            }
            finally
            {
                MeetCleanUp(keep);
            }
        }
    }
}
