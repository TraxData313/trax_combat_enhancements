using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using TraxCombat.Core;
using TraxCombat.Mcm;

namespace TraxCombat.Tools
{
    /// <summary>
    /// OFFLINE SMOKE TEST - see OfflineSmoke.csproj. Usage:
    ///   dotnet run --project tools\OfflineSmoke -c Release -- [gameFolder] [mcmBinFolder]
    /// Exit 0 = all checks passed, 1 = a check failed (the temp folder with config.json and the
    /// log is kept and printed for a look), 2 = could not start.
    ///
    /// Order matters: MCM is refused by the assembly resolver until phase 2, exactly like a
    /// player's game without MCM; once loaded it cannot be unloaded.
    /// </summary>
    internal static partial class Program
    {
        private static readonly List<string> Failures = new List<string>();
        private static string _gameFolder = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord";
        private static string _mcmBin = @"D:\SteamLibrary\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client";
        private static bool _allowMcm;
        private static string _dir = string.Empty;

        private static string ConfigPath => Path.Combine(_dir, ConfigFile.FileName);

        private static string LogText => File.Exists(Path.Combine(_dir, ConfigFile.LogFileName))
            ? ReadShared(Path.Combine(_dir, ConfigFile.LogFileName))
            : string.Empty;

        /// <summary>A file read the way an editor reads trax_combat.log while the game holds it open
        /// for writing (review 10a R6: TraxLog keeps one handle, shared for reading and writing).</summary>
        private static string ReadShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static int Main(string[] args)
        {
            if (args.Length > 0) _gameFolder = args[0];
            if (args.Length > 1) _mcmBin = args[1];
            if (!Directory.Exists(_gameFolder))
            {
                Console.Error.WriteLine("game folder not found: " + _gameFolder);
                return 2;
            }
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            _dir = Path.Combine(Path.GetTempPath(), "trax_offline_smoke_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(_dir);
            Console.WriteLine("offline smoke test - temp folder " + _dir);

            // Phase 1 - a player WITHOUT MCM.
            Step("types load without MCM (the game's GetTypes() scan)", TypesLoadWithoutMcm);
            Step("paths point at the temp folder", RedirectPaths);
            Step("step 21: every logic built from here on reads what a fighter holds through the smoke's stand-in (the game's read needs a native agent)", UseFakeWeaponFacts);
            Step("step 23: every logic built from here on plays the brace's engine side through the smoke's stand-in (the game's calls the engine)", UseFakeBraceBody);
            Step("step 33: every logic built from here on plays the sprint's engine side through the smoke's stand-in; every SprintKey name is one of the game's InputKey names (LeftControl = 29)", UseFakeSprintBody);
            Step("step 41: every logic built from here on plays the shield wall hold's engine side through the smoke's stand-in (the game's writes the engine)", UseFakeWallBody);
            Step("step 47: every logic built from here on plays the impact levers' engine side through the smoke's stand-in (the game's calls the engine)", UseFakeImpactBody);
            Step("step 48: every logic built from here on reads the impact model's engine inputs through the smoke's stand-in (the game's reads native pointers); the game's DamageTypes and StrikeType map onto the model's", UseFakeImpactInputs);
            Step("step 49: every logic built from here on plays courage's engine side through the smoke's stand-in (the game's reads native agents)", UseFakeCourageBody);
            Step("step 74: every logic built from here on plays the block hold's engine side through a stand-in (no formation counted unless a step 74 check sets its cap)", UseFakeBlockBody);
            Step("step 54: every logic built from here on reads the punish's target through the smoke's stand-in (the game's reads native agents)", UseFakePunishBody);
            Step("step 55: the game's side of the flocking lever (Agent.SetScriptedPosition / DisableScriptedMovement / CanBeAssignedForScriptedMovement, GoToPosition = 1, the formation's goal reads); every logic built from here on plays flocking's engine side through the smoke's stand-in", UseFakeFlockBody);
            Step("step 79: every logic built from here on plays the war cries' voice through a counting stand-in (the smoke cannot play sound); the game's Agent.MakeVoice(SkinVoiceType, CombatVoiceNetworkPredictionType) and VoiceType.Yell / HorseRally exist", UseFakeWarCryBody);
            Step("step 65: every logic built from here on reads the AI kick's engine side through the smoke's stand-in (the game's reads native agents); the engine's Kick event bit, DefendUp, DefendShield and the busy actions = the Core's", UseFakeAiKickBody);
            Step("step 82: every logic built from here on reads the meet's engine side (a man's guard at contact, his block ability, a player's aim) through the smoke's stand-in; the engine's ActionCodeType defend / attack codes, UsageDirection Defend* and MovementControlFlag defend bits = the Core's guard read", UseFakeMeetBody);
            Step("step 78: every logic built from here on plays the rhythm's own engine calls (the player's unit, the slot swap) through the smoke's stand-in; the engine's action codes behind the busy rule; Formation.SwitchUnitLocations(Agent, Agent) is public (the swap lever)", UseFakeRhythmBody);
            Step("step 76: every logic built from here on plays the spear looks' engine side through the smoke's stand-in, NOT ready (no look until a step 76 check builds its own world - the game's reads native agents)", () => TraxCombat.Missions.SpearBodyDefault.Current = new FakeSpearBody { IsReady = false });
            Step("MCM absent: the bridge logs it once and stands down", McmAbsent);
            Step("first run: config.json created, every key explained", FirstRun);
            Step("defaults: every default from the defaults.json embedded in the DLL, logged, in the first-run file", DefaultsFromTheEmbeddedFile);
            Step("MCM save keeps a hand edit made meanwhile (the rewrite rule)", HandEditSurvivesMcmSave);
            Step("mission-start re-read applies the hand edit", ReloadAppliesHandEdit);
            Step("broken file: kept values, backed up before the next write", BrokenFile);
            Step("missing keys defaulted and added, unknown key kept and reported", MissingAndUnknownKeys);
            Step("log rate limit: a verbose flood is capped", VerboseFloodCapped);
            Step("log file (review 10a R6): one handle kept open - readable meanwhile, follows a new path, released (the file can go, the next line makes a new one), trimmed to the newest half past 2 MB", LogWriterKeepsTheFileUsable);
            Step("tournament AI-level fix reads the private field and pushes it down the chain", TournamentAiLevelFix);
            // Damage randomness (step 4) - the real decorator over the game's own custom-battle
            // model, fed the game's own AttackCollisionData / AttackInformation structs.
            Step("damage: every roll lands in [1-p, 1+p] of the game's value, mean ~1, all four kinds roll", DamageRollsThroughDecorator);
            Step("damage: never rolled - shield (toggle off), fall, objects, a 0 hit; a positive hit never below 1", DamageSkipRules);
            Step("damage: settings changed mid-battle apply to the very next hit (hot swap)", DamageHotSwap);
            Step("damage: a bug in the roll keeps the game's value and logs ONE [error] per mission", DamageFailSafe);
            Step("damage: verbose roll/skip lines and the [summary] damage block", DamageLogAndSummary);
            Step("damage: a roll off the main thread is detected and reported", DamageOffMainThread);
            Step("damage reduction (step 25): through the real decorator - AFTER the roll (100 → 37.5..112.5, mean 75), the roll off still reduced (melee / ranged / mount / shield / charge 100 → 75), never a fall, an object or a 0 hit, a positive hit never below 1 (90%: 5 → 1), live (50, 0), the master switch (the game's value, counted), the first-reduced-hit line, the verbose lines, the [summary] line, the mission-start line at 25 and at 0", DamageReductionThroughDecorator);
            // Athletics (steps 5, 5c) - the real speed penalties, stat decorator, Athletics logic and
            // damage decorator, fed uninitialized Agent objects (no native side).
            Step("Athletics: the attack, run and horse levers each scale only their own AgentDrivenProperties", SpeedPenaltyScalesOnlyItsOwnValues);
            Step("Athletics: the stat decorator applies each fighter's run multiplier and a slowed rider's horse's, the attack ANIMATIONS on the straight line A + (1 - A) f (AttackAnimationMinPercent 100 = full speed - step 13; step 20b: 85 at empty and halfway, 60 halfway, 0 held at 5), the AI decisions only while their A/B switch is on, on every recompute, never compounds, lifts them when off", DecoratorAppliesEachFightersMultipliers);
            Step("Athletics: swings through the real logic - a recruit (floor 50) 2 blows at full strength, slower every blow, empty on the 5th; a 300-skill party leader 14 and 54; a kick and a bash charged apart (step 18 - never a blow, a timer or a step-back roll), ranged releases left to the shot event; the attack-speed check by f", BlowsThroughTheLogic);
            Step("Athletics (step 18): kicks and shield bashes cost CostPerKickOrBash x the blow's multipliers - 3 of 100, you as a hero party leader 1.69 (the YOU line), the refill delay restarted; once each: a kick that lands, a kick on both channels, a bash that lands, a hit no channel showed (then seen late); never a blow, an attack timer (yours or the AI's) or a step-back roll; 0 = free, Athletics / the mod off = nothing, live; the game's action codes", KicksAndBashesCost);
            Step("Athletics (step 22): a blocked melee blow costs the DEFENDER - Core's classifier on the game's own AttackCollisionData (shield right / wrong side, parry, chamber, landed, missile, bash) and result codes; right side 1, wrong side 5, a parry 2 of 100, once per blocked blow (the same swing again not charged), the refill delay restarted, never a blow, a pause or a step-back roll; an AI hero 3.75, a party leader's parry 1.13, you 0.56 (the YOU line once in full); ten wrong-side blocks empty a recruit and his speeds follow; live prices, 0 = free, Athletics / the mod off = nothing", BlocksCostTheDefender);
            Step("Athletics (step 29): only a WRONG-side shield block restarts the refill delay (OnlyWrongSideBlocksStopRefill) - you, a 100-pool hero emptied at 0, parry every 1.5 s through the real logic: every parry paid (1.5), the delay clock untouched, ~38 of 100 after a minute (2.5 in, 1.5 out per parry); right-side blocks let it run too; wrong-side blocks every 1.5 s hold a man at 0; the YOU line and the verbose line say so; the switch live (off = step 22: the parry restarts the delay, the bar sinks; on again: it climbs); the master switch: nothing charged, no clock touched; the summary splits the restarts by kind", BlocksLetTheRefillRun);
            Step("tired block direction (step 30): through the real stat decorator and logic - the game's AiRandomizedDefendDirectionChance (DrivenProperty 40) untouched at full strength, raised to own + (1 - own) x w below the peak line (a tournament's -1.10 counted as 0: f 0.52 → 0.48; own 0.2 → 0.58; empty → 1.00), never compounding over recomputes; never you; the slider live (50: 0.60, 0: the game's value), AthleticsEnabled and the master switch = the game's value; the input frame's guard direction never touched (a braced man's own DefendLeft kept); the guard by f band through GuardTaken (you never), step 34: split by a shield in hand at the blow (the stand-in weapon read; the recomputes too); the [guard] lines and the 4 [summary] lines", TiredBlockDirection);
            Step("tired block direction (step 30) cost: a thousand tired men each blocking and recomputed - small, nothing allocated per blow", TiredBlockCostStaysFlat);
            Step("Athletics: health caps the bar at once, the peak line stays on the full pool, the cap switch works live", HealthCapsTheBar);
            Step("Athletics: the damage upside follows the attacker's f through the real damage decorator (empty: never above x1.00; untracked: full)", DamageUpsideFollowsTheAttacker);
            Step("Athletics: mid-battle settings - speeds, peak line, horses, pool size (shares kept) re-target live; switching off refills and lifts", AthleticsHotSwap);
            Step("Athletics: the [summary] Athletics block", AthleticsSummary);
            // The step back (step 5d) - the real logic's bookkeeping with a stand-in for the engine side.
            Step("step back: rolled at every AI swing's end by f (0% at full strength, 100% empty), started from the tick, timed live, capped, refused by the safety checks, released on every path (time, order, hand-over untouched, left the field, switched off, mission end), logged and summarised", StepBackThroughTheLogic);
            Step("stale records (review 10a R5): an index reused, or an agent deleted unseen - his step back, pace hold and slowed horse end like a man leaving the field, no engine call on him", StaleRecordsAreForgotten);
            // The attack rate (step 5e; step 13 PAUSE ONLY) - phases, D, the no-attack timer (AI with a stand-in
            // engine side; yours through the input gate's managed decision), cycles and verdicts.
            Step("attack rate (step 13): full-speed animations, the AI timer D x (1/m - 1) after every attack - none at full strength or below 0.1 s, melee (D = wind-up + swing), ranged (from the reload's end, + the reload; a throw without), riders held; the gap it leaves = the timer, none early; lifted on every path (time, an attack slipping through, a game job waited out, a long frame, the player took him, left the field, switched off, mission end), refusals, chained blows, R1 / R2, the AI-decision and animation-floor switches re-applied; YOUR timer - the hold from the release, presses swallowed (during the swing and the countdown), the flash, hold-to-attack, the recovery read, an attack the gate missed, AttackRatePlayerTimer and ModEnabled release it, ranged from the reload; the summary", AttackRateThroughTheLogic);
            Step("AI holds by input (step 16): the Core bits are the game's; the timer by input - hooked, only the attack bits out, the guard raised (or only dropped with AiHoldRaiseGuard off), his own guard kept, a ready cancelled not released, the hits on a held man filed by state; the timer SURVIVES a step back both ways round (the later end: backpedal arrived with the timer still on; timer over with the backpedal still on, then the edge ahead); the backwards vector written; ModEnabled off releases both at once; leaving the field clears every wish without an engine call; a hold the engine never called us during is counted and warned; the switch back to the old ways mid-battle (the scripted walk, NoAttack deferred behind it and set at its end); the summary lines and the header; the component leaves an idle man's input alone", AiHoldsByInput);
            Step("battle pace (step 21): the class read at each release from what he holds; a FRESH shield man waits 0.3 x (D + his own gap) / 0.7 = 0.78 s, guard up (by input), a tired one (tired + 0.3 x 1.82) / 0.7; a two-hander 15%; riders none fresh, the tired pause alone tired (a shield on horseback too); bowmen 2.0 s (horse archers too) on top of the tired pause, crossbows 2.5 s, a javelin none; YOU never (no read, no count); the model's cycle and the class cycle; a slider change applies at the next swing (0 = off, 50), logged; the master switch lifts a running one; the first line of each class, the first timer's parts; the [summary] battle pace lines, the timer rows' share, the peak band in the holds", BattlePaceThroughTheLogic);
            Step("battle pace (step 21) cost: a thousand men held at once - the AI timer's tick stays small and allocates nothing", BattlePaceTickCostStaysFlat);
            Step("brace by orders (step 23): a CHARGING man braces at 20% of his bar and attacks again at 40, a HOLDING man 60 → 80 (not a point before), the AI's own advance at 40; the order changing mid-brace (hold → charge: out at once) and before one (charge → hold: in at once); a WOUNDED man (cap 50) never braces full to his cap and stops at it (30 → 50); never the player, a man the player takes over stops; the input frame - a melee attack wish becomes a guard, a ready cancelled, his own guard kept, a bow in hand and a ranged action let through (the release counted), the shield held up (live switch); the shield taken out - a one-hander first, then the shield, checked every 1 s, seen in hand, put away by the AI and asked again, at most 3 calls, none with BraceWieldShield off or a shield in hand; one hold's end never lifts another (the AI timer and the brace both ways round, the callback kept on); the guard while bracing through OnMeleeHit; a floor changed live; each man's margin rolled once and rescaled live; leaving the field (no engine call); BraceEnabled and the master switch lift every brace at once; the formation read API's Ready / Bracing; the [summary] brace lines", BraceThroughTheLogic);
            Step("brace floors (step 28): a man in NO formation (tournaments, arenas, duels) braces at 20% ± his own floor roll, not at the hold floor 60; the floor roll once per man, its own dice, rescaled live with the sliders; his target = his floor + his margin; a holding man on 60 ± his own roll; the [summary] floor lines", BraceNoFormationAndFloorRoll);
            Step("brace by orders (step 23) cost: a thousand men looked at about four times a second - the tick stays small and allocates nothing", BraceTickCostStaysFlat);
            Step("shield wall holds its slots (step 41), the game's side: v1.4.8's Agent has the private readonly Timer _cachedAndFormationValuesUpdateTimer, Timer.StartTime is public; the real Timer driven as vanilla drives it (TickParallel: Check + AdjustStartTime(-5), TickAsAI: Check) moves its start exactly on the frames vanilla's formation update runs - 6 in 3 s - and on no other; the game's arrangements map onto ours and our defensive set is GetArrangementOrderDefensiveness's", WallTimerIsVanillasUpdate);
            Step("shield wall holds its slots (step 41): through the real logic - the first look holds the halted shield wall, a moved square, a circle on the AI's advance and a man whose timer is unreadable, and leaves to vanilla one man per reason (a line, a column, mounted, detached, far behind his slot and the line, YOU, a charge, a siege attacker, naval, fleeing); the first-held line in full and the no-timer line; no rewrite without vanilla's update, the no-timer man every 0.1 s; vanilla's update (his timer moved) followed by our write in the very next pre-tick, once, its interval 0.5 s; a charge order let go at the next look and a retreat at vanilla's update, each with vanilla's own value given back; the escape (25 m behind = free, 25 m ahead = held, back near = held again); a man the player takes over let go; the samples ON (0.3 m ahead, in contact) vs OFF (2.5 m); ShieldWallHoldsSlots off live = every held man given back at once and nothing written, on again = held at the next look; the master switch the same; leaving the field = no engine call; the summary's five lines; nothing after it", WallThroughTheLogic);
            Step("shield wall holds its slots (step 41) cost: a thousand men held, each one's vanilla update every 0.5 s followed by our write - the pre-tick stays small and allocates nothing", WallTickCostStaysFlat);
            Step("shield wall step out (step 42): through the real logic with the leash at 1 m - the first look frees the shield wall's man and the square's side man whose enemies stand close in front of their slots (vanilla's value, nothing written), not a man without an enemy, with one behind his slot or one 5 m off; walking out at 2 m/s he is not rewritten at vanilla's update and is reined within one 0.1 s look of 1 m (one leash write, the first step and its end in full); walking back no new step before he is back in his slot, then the next; the enemy backing off far = held within 0.1 s (enemy gone); the leash to 0 live = held at once, then not one enemy read, step or release; 2 m live = reined at 2 m; the master switch lets a man out go; the summary's step-out line", WallStepOutThroughTheLogic);
            Step("shield wall step out (step 42): the leash at 0 is step 41 exactly - the same 3 s with and without an enemy close in front give the same writes and releases, and not one enemy read", WallLeashZeroIsStep41);
            Step("shield wall step out (step 42) cost: a thousand men held, 200 out on the leash and looked at every 0.1 s, vanilla's update every 0.5 s - the pre-tick stays small and allocates nothing", WallLeashCostStaysFlat);
            Step("swing stack (step 32): through the real logic - YOU: 120 points hold 6, a burst of five with no hold and the button never cleared, the count following the points (1/4), the sixth spends the last - the hold from its release, the countdown to the next stored swing (1.16 s at m 0.41), the kick key refused at 0 (never on a horse), half full halfway, the held button fires the moment the swing is back; saved up (3) and burst again; the master switch (no hold, no gate, full again) and SwingStackEnabled off (step 13's pause after the attack) live; THE AI by input: a swing from the stack never held and his own gap (1.0 s) not refilling, at 0 held exactly for the rest of the refill (0.5 x 3.28 s) with his kick bit (the game's EventControlFlag.Kick) and attack taken out, released when the swing is back, the swing after it 'after waiting'; a kick spends one, a kick that spends the last starts his wait, KicksUseSwings off = free; the master switch lifts the waits and fills the stacks; the [summary] swing stack lines", SwingStackThroughTheLogic);
            Step("swing stack (step 32) cost: a thousand AI men waiting at 0 - the AI timer's tick and the stack pass stay small and allocate nothing", SwingStackCostStaysFlat);
            Step("sprint (step 33): through the real logic - YOU: 150 points store 7.5 s; the key pressed (SprintKey's index) starts it at x1.6 and the real stat decorator puts MaxSpeedMultiplier ABOVE 1 (0.80 x 1.6 = 1.28; step 35: x the FRESH run speed, a tired man too); no Athletics refill while sprinting; x1.6 to 2.25 s then x1.35 at 4.875 s (0.05 steps); it drains 1 s a second only while moving; letting go ends it, the rest stored; refill cap / 7 standing, half moving; below 20% refused (logged in full), a held key never restarts it, a new press does; a menu ends it, a horse refuses it, never while the AI drives your hero; THE AI: the first look records, a new CHARGE starts every man on foot in that formation at x1.6 for his whole store (a rider, a man you drive and another formation not), an enemy within 3 m stops him, a new ADVANCE ends the running ones and starts what he holds above half his FULL store (step 37) (ended at its share, the rest stored), a HOLD none; SprintAiEnabled off ends only the AI's and back on fires no stale order; the MASTER SWITCH ends every sprint at once (the decorator vanilla) and nothing restarts; leaving the field, the hideout's fresh start (full again), a points drop clamps the store, an empty bar refused; the speed check sees the engine follow; the [sprint] lines and the 4 [summary] lines", SprintThroughTheLogic);
            Step("sprint (step 33) cost: a thousand men on a charge - every one marked for a recompute (the 50-a-tick budget queues them), ~12 recomputes a man over the whole sprint, the sprint's tick small and allocating nothing", SprintCostStaysFlat);
            Step("sprint tuning (step 35): through the real logic and the real stat decorator - an EXHAUSTED you (points 0, run x0.3) still holds the 2 s floor and sprints it: the run property 0.8 x 2.0 at the start (the FRESH run, not 0.8 x 0.3 x 2.0), x2.0 held to 0.6 s, then fading towards 1.1 x 0.3; TopSpeedReachDuration 2.6 → 1.0 while sprinting and the game's 2.6 back at its end; the floor refills 2 / 7 x 0.5 a second standing exhausted; SprintTopSpeedReachSeconds 0 leaves the game's value, a change mid-sprint recomputes the sprinter; the master switch = the base model's values; a tired AI man sprints a charge off the floor; SprintMinSeconds 0 = an empty bar refused (live); the first-sprint lines (floor, fresh-run peak, fade target, game → our acceleration) and the summary's floor and acceleration clauses", SprintTuningThroughTheLogic);
            Step("sprint by order (steps 36 / 37, one reserve per order): through the real logic - shock troops on a MOVE from a full 5 s store sprint 1 s down to the 80% reserve, a second move right after nothing (reserve kept, one line); the real CHARGE then sprints the 4 s (80%) left; an ADVANCE from full 2.5 s (down to 50%); the move's reserve read live; the master switch; the summary by order kind", SprintByOrderThroughTheLogic);
            Step("routing men sprint away (step 57): through the real logic's MissionBehavior.OnAgentFleeing (the game's rout) with the stand-in sprint body - a routing AI man sprints his WHOLE 5 s store (no reserve), never ended near an enemy, run dry; a second call finds it empty; a running MOVE sprint handed over (all that is left); you and a rider never; SprintOnRout / SprintAiEnabled / the master switch off = none, back on = it starts; the first-rout line and the [summary] \"sprint on rout\" line", RoutSprintThroughTheLogic);
            Step("aggression and fever (step 43): through the real stat decorator and logic - AGGRESSION BY THE BAR: a full AI man's attack chance 0.144 → 0.086 and riposte 0.080 → 0.048 (nothing else moved, no compounding over recomputes), a man in no formation the same, never you, never a rider; a CHARGE (the AI sprint off - the poll serves the fever alone) arms 100 / 10 = 10 s at the absolute 80% peak (0.115), you and the rider not armed, nothing burns over 5 s of walking; CONTACT (an enemy at 2 m) starts the burn, 3 s without a shield = 7 s left and 0.75 (0.108), the shield taken out burns the 7 s in 4.9 s, faded back to his tired 0.6 after 5 recomputes; the proof's groups (no fever, in a fever, no formation) by bar band; the FEVER SPRINT on an advance with the enemy at 8 m - refused while SprintAiEnabled is off, then 2.5 s above the 50% reserve (armed = full chance, the 100-67% band), refused while sprinting, one roll a second, the reserve caps a second, in melee / out of range / a missed roll; re-arming (charge after advance re-armed, a repeat ignored, a hold ends a burning one); 100 / 100 and FeverEnabled off live; a half bar after ten wrong-side blocks = 0.4 (0.058); the [fever] lines and the [summary] fever lines", FeverThroughTheLogic);
            Step("aggression and fever (step 43) cost: a thousand men in a fever, half burning, half rolling for a sprint - the fever's tick stays small and allocates nothing", FeverCostStaysFlat);
            Step("courage and cohesion (step 49): through the real logic with a stand-in world - a LONE looter 2 m from five metal men (odds 1.35 / 29.9 = 0.05 < 1.00) walks back to his mates (the nearest bigger group) straight backwards facing them, his forward walk replaced by the input frame, sprinting the 2.5 s above the 50% reserve, his weights tightened and re-applied every look, holding among his mates, brave again with the game's weights back; a MASS of 21 looters vs 2 metal men stays brave; an armed FEVER makes odds 0.45 enough (required 0.4), timid when it ends; a man held in the SHIELD WALL and a RIDER never looked at; SHOCK + two great-axe legionaries: odds 0.33 vs required 1.33 = a PANIC walk sprinting all 5 s (keeping 0%); FLOCKING to an 8-man group 12 m away instead of his formation 32 m off; CourageEnabled off mid-walk lets everyone go at once while the looks keep measuring; Athletics off: the walk's frame still written, no tiredness; step 49b: a feverish, unshocked looter presses at odds 0.23, a hit mid-fever or the fever's end sends him back to the mass, a surge (brave 4 s, then timid again); the [courage] and [summary] lines", CourageThroughTheLogic);
            Step("courage and cohesion (step 49) cost: a thousand men in two blocks face to face, every man looked at every 0.5 s - the courage tick stays small and allocates nothing", CourageCostStaysFlat);
            Step("cavalry flocking (step 55): through the real logic with a stand-in world - a TIRING UNIT of 20 lancers: all eager (90%) = the dance, nothing touched, kinds read once; five at 55-60% flock (steered, weights lowered), no flip before 3 s, then FLIP as one wave of 5 (5 >= 3 wanted at 56%, the leader ready 80%), released (scripted walk off, weights back), 10 s in the wave, then flock again; the whole unit exhausted (15%) in two clusters: no flip without a rested leader, the heading leans to the unit's centre with a little of the goal, one rider rests to 45% = a BIG wave of 10 (9 wanted at 18%), the other cluster keeps riding; a MIXED formation splits by kind - two waves of 4, never one of 8; a lone fresh rider among 4 legionaries flocks (timid, eagerness 50%), free again with courage off; the player's HELD cavalry and YOU never touched, a charge flocks him, a hold lets him go at once; an exhausted lone rider gives up after 45 s; no ground = nothing sent; lone passes and the time between passes ON and OFF; CavalryFlockEnabled off lets everyone go while the census measures; Athletics off = everyone eager; the [cavalry] and [summary] lines", FlockThroughTheLogic);
            Step("cavalry flocking (step 55) cost: a thousand men, 500 riders of three kinds on two sides, half of them tired - the census every 0.5 s with the groups, the flips and the timid looks stays small and allocates nothing", FlockCostStaysFlat);
            // Step 47 - IMPACT I1, the spike: the six engine levers through the real decorator and logic with a stand-in engine side.
            Step("impact spike (step 47), the game's side: act_stagger_backward / forward / left / right are fields of the game's ActionIndexCache; Core's action codes are the game's ActionCodeType and CombatCollisionResult values; MissionTickAction.DropItem; KnockDown | KnockBack = 0x30; StunPeriodMax raised to 2 s and restored once (a cap already above it left alone); SetCurrentActionSpeed, SetActionChannel(int, in ActionIndexCache, …) → bool, RegisterBlow(Blow, in AttackCollisionData), DropItem, the channel reads exist", ImpactGameFacts);
            Step("impact spike (step 47): through the real damage decorator and logic - the start line and the cap 0.8 → 2 s; a kick into the shield never asked; kicks 1-8 rotate KnockDown / reel x untouched / slowed: 4 falls seen, the reel 0.42 → 0.83 s and the fall-and-rise 1.5 → 3.0 s at speed 0.5 (slowed the tick it showed and again on its _continue, channel 0), the control kicks StrikeKnockBack; a ninth's missing fall, a rider (no decision, no watch), a 0-damage kick; the shield dropped (queued, never inside the callback's engine) and picked up 20 s later, another never left the hand; the first block → a stagger from the next tick (with the engine's priority, accepted, seen, 0.9 s, a block and a hit from inside it), the next → our own blow (KnockDown, 1 damage, a Fall), a RIDER's block refused with no engine call, the next stagger over the priority refused by the engine; the block stun x3 for an even blocker through the real decorator (0.21 → 0.63, above the cap held at 2 s, an odd blocker's 0.9 clamped to the game's 0.8), 401 calls from worker threads tallied, the recoil and the guard timed x3 vs the control; ImpactSpike off live = vanilla at once (the cap, no decision, no stun, nothing watched or queued), on again; a watched man leaving (no engine call); the [impact] lines and the [summary] verdicts (all six WORKS)", ImpactSpikeThroughTheLogic);
            Step("impact spike (step 47) fail safe: one lever's error disables that lever alone for the mission - SetCurrentActionSpeed throwing (lever 3: the fall plays on untouched, no later slow, lever 1 still asked), the queued drop throwing (lever 5), the stun decider throwing on a worker thread (the base's stun kept, lever 2 off at the next tick, the game's cap back); each [error] once, each \"lever N disabled\" line, the summary names them and reads FAILS", ImpactSpikeFailSafe);
            Step("impact spike (step 47) cost: a thousand men, ten blocks a tick and a kick every 6 ticks (the watch pool full, a stagger every 10 s, our blow every 20 s) - the tick and the hits stay small and allocate nothing", ImpactSpikeCostStaysFlat);
            Step("impact model (step 48): through the real damage decorator's KnockDown seam and the real logic - the start line with the anchors; the king's landed kick on the 50-at-20% man decided ONCE in the decorator (x 1.33, shield 90%, down 80% - the dice knock him down: since step 50 the KnockDown answer) and taken by OnMeleeHit's part, logged in full; on the ground he is refused; his right-side block x 1.09 (stagger 80%, down 40%, the stagger's length and the stun x(1 + T)), since step 51 a live block writes his record (the stagger his, queued for the tick), in shadow a wrong-side block while reeling (immune, +0.75, halved), x 1.42 (90 / 50 / 40), the man's tired swing on the fresh king x -0.76; even men at 25%: a parry 77 / 36, a clean hit 68 / 35 in the decorator; two even fresh men 0; a bash by the shield's weight; a rider knocked off at 50% and never staggered; you at ImpactOnPlayerPercent 0 never, your own kick unprotected; shadow mode and the spike force shadow (the mode lines); ImpactKicksEnabled off; ImpactEnabled off / on live; a failing read (one [error], counted, the next fine); an untracked attacker never read; a missile no event; the [summary] impact lines", ImpactModelThroughTheLogic);
            Step("impact model (step 48) cost: a thousand men at every bar, twenty events a tick (kicks, bashes and clean hits through the decorator's seam, the three blocks) - the decisions stay small and allocate nothing", ImpactModelCostStaysFlat);
            Step("impact kicks LIVE (step 50): through the real damage decorator's KnockDown seam and the real logic with the stand-in body - a kick on an EXHAUSTED man with a shield: KnockDown answered, the shield drop queued and made FROM THE TICK (slot 3; never inside the callback), his Fall slowed to 0.5 the tick it showed and again on its _continue, the fall-and-rise measured 3.0 s (asked x2) and written as his get-up (5 s immunity from it), the shield left his hand, the AI pickup lock (15 s, 0 = never), picked up again; a second kick within the immunity refused, its reel slowed to 1 / x2.5 and measured, the reel's end in his record; a BASH on a tired turtle: the drop waits out his own bash, then goes; the reel slowed and measured; YOU vs a fresh king: nothing (x < 0, the YOU line), vs the king exhausted: down; a rider: nothing of the kicks' own path, the KnockDown seam never answers him (step 53: his is the dismount seam); courage's shock counts a live outcome, since step 51 a live wrong-side block's two outcomes once each; SHADOW = nothing applied; SetCurrentActionSpeed throwing disables that lever alone (the fall measured untouched, the knockdown still answered), the drop throwing likewise; the master switch lets every watch / drop go; the [summary] impact live lines", ImpactKicksLiveThroughTheLogic);
            Step("impact kicks LIVE (step 50) cost: a thousand men at every bar, twenty live kicks a tick through the decorator's seam (the watch pool and the drop queue busy) - small and allocating nothing", ImpactKicksLiveCostStaysFlat);
            Step("impact blocks LIVE (step 51): the real damage decorator's stun seam from WORKER threads and the real logic with the stand-in body - the game's cap 0.8 → 2 s while blocks are live; the stun by tiredness from the tick's T snapshot (an exhausted blocker x2, a fresh one untouched, the game's 1.2 held at its 0.8 first, an untouched stun clamped, ImpactStunCapSeconds / BlockStunTiredExtraPercent live, you x ImpactOnPlayerPercent, a rider's like anyone's since step 53, 200 calls tallied off the main thread); a RIGHT-side block by an exhausted man: the stagger queued and made FROM THE TICK (with the engine's priority, speed 0.6 for 1.5 s), seen, a block and a hit from inside it counted (the block refused - immune), measured 1.5 s (the animation's 0.9 s learned), the end + 2 s immunity in his record; a WRONG-side block: the shield dropped from the tick + the stagger; a blocker KNOCKED DOWN by our own blow from the tick (the Fall slowed x2 and measured, the 5 s immunity, ONE courage shock - our blow's hit is not a second); the RIPOSTE on you (the YOU line, your forced stagger from the tick, measured); YOU staggered on your block (the weapon's name); ImpactOnPlayerPercent 0; a rider (no stagger, no blocks' action - step 53 queues his dismount blow); a man down at the tick (no call); the engine refusing both ways (his record fixed); the guard timed x1.2 vs fresh; shadow (the cap back, nothing queued, no stun); the riposte off; blocks off = the riposte alone; the forced stagger / our blow / the stun seam (a worker thread's error) each disabled alone; the [summary] impact live block lines", ImpactBlocksLiveThroughTheLogic);
            Step("right blocks favour the defender (step 56): the defaults embedded in the real DLL (right block / parry 30 / 10, the riposte 70, the block stun 0, a correct block counting 25% of the blocker's tiredness, format 10) through the real logic and decorator - two EMPTY even men: a right-side block or a parry leaves the blocker standing (x 0.25, at most 5% staggered, never down, the game's stun) and the ATTACKER reels (the riposte at 70%, his forced stagger from the tick); the WRONG side 90 / 50 / 40 and no riposte, drop and stagger from the tick; two fresh men nothing either way (the steps 48-55 checks pin the step-48 block model)", RightBlocksFavourTheDefender);
            Step("impact blocks LIVE (step 51) cost: a thousand men at every bar, twenty live blocks a tick of the three kinds + twenty stun-seam calls and the T snapshot of every man - small and allocating nothing", ImpactBlocksLiveCostStaysFlat);
            Step("impact clean hits LIVE (step 52): through the real damage decorator's KnockDown seam, the real shrug-off hook and the real logic with the stand-in body - a clean hit on a fresh even man: no outcome, his untouched flinch timed 0.45 s (the control, its length learned); an EXHAUSTED man's stagger = his own flinch slowed to 1.5 s (the tick it showed, speed 0.30), measured, the end + 2 s immunity in his record; THE REELING BONUS live - a man at 70% staggered by a clean hit, then KICKED inside it: x + 0.75 → knocked down (the same kick on a calm man: 0%), a two-handed axe blow on a staggered man knocks him down (a reeling knockdown counted); TIRED MEN NEVER SHRUG OFF through the real hook and the base model's threshold 5 - an exhausted / half-tired man flinches at 3 damage, 2 is still shrugged off at a half bar, a fresh man / a kick / a rider / the switch off / clean hits off keep the game's; a knockdown on a shrugged-off blow counted; YOU knocked down / you stagger a man (the YOU lines); a rider (not through the KnockDown seam - step 53's dismount seam); SHADOW (nothing answered, the 'would' record kept, no shrug-off refused); SetCurrentActionSpeed throwing disables that lever alone (the flinch measured untouched, the next stagger not applied, the knockdown still answered) and a throwing shrug-off decider = the game's answer; the master switch lets the hits' watches go and answers nothing; the [summary] momentum and clean-hit lines", ImpactHitsLiveThroughTheLogic);
            Step("impact MOMENTUM (step 52) - Anton's shock charge: 200-man lines hit once each with seeded dice through the real decorator and logic - a standing fresh swordsman into a 40% line, a standing two-handed axe, the same axe SPRINTING at 8 m/s (x1.6 impact), the sprinting axe into an exhausted line, a standing vs a sprinting kick; the charge throws the tired line around (knockdowns several times the standing swordsman's); the [summary] momentum line by the attacker's speed band", ImpactShockChargeThrowsTiredMenAround);
            Step("impact clean hits LIVE (step 52) cost: a thousand men at every bar, twenty live clean hits a tick through the decorator's seam (half the attackers running or sprinting) and twenty shrug-off questions through the real hook - small and allocating nothing", ImpactHitsLiveCostStaysFlat);
            // Step 53 - IMPACT I6: riders knocked off their horses, the riders' momentum, the on-screen lines.
            Step("impact RIDERS LIVE (step 53): through the real damage decorator's DISMOUNT seam and the real logic with the stand-in body - an exhausted rider's clean hit: knocked off (50% x 50% = 25%, the dice say yes) answered in the seam, followed, seen off his horse; never a stagger on a rider (a right-side block: refused by the model, no engine call); a kick knocks a rider's shield from his hand from the tick, no reel slowed; a BLOCKING rider knocked off by our own CanDismount blow from the tick (owned by the attacker), seen; one with no horse by the tick refused (no call, record fixed); a dismount the engine never plays: not seen after 2 s, his record's ground time taken back; a fresh rider untouched, the KnockDown seam never answers a rider; a galloping rider's momentum x1.30, a couched lance x1.00, the riders' columns; THE ON-SCREEN LINES - you knocked off (red, at once), you knock a rider off and a man down within 1.5 s = ONE merged purple line, your shield knocked away (red), your man unhorsing an enemy hero (white, behind ImpactMessagesIncludeYourMen; a common man none), none in shadow, ImpactShowMessages off throws what waits away; a throwing dismount blow disables its lever alone, a throwing decider = the game's answer; the [summary] riders, momentum and messages lines", ImpactRidersLiveThroughTheLogic);
            Step("impact CAVALRY CHARGE (step 53): 200-man lines hit once by a fresh rider's sword with seeded dice - standing, at a canter (x1.15), at a full gallop (x1.30), the gallop into an exhausted and into a FRESH line, beside a man sprinting on foot (x1.60): the gallop throws a tired line harder, never fells everyone, leaves a fresh line standing, stays below the sprinting man's; the riders' momentum columns", ImpactCavalryChargeThrowsTiredMenDown);
            Step("impact riders (step 53) cost: a thousand men (half riders) at every bar, twenty landed blows on riders through the dismount seam + twenty blocks by riders (our dismount blows from the tick, the checks) a tick - small and allocating nothing", ImpactRidersCostStaysFlat);
            Step("impact PUNISH (step 54): through the real logic (the fever's look, the decision through OnMeleeHit's part) and the real stat decorator - no target read while no window is open anywhere; an exhausted blocker's broken block opens a window (counted, the first line); the AI man fighting him presses it - his share 0.6 → 1.0, the game's own 0.144 / 0.080 written - and his attack inside punishes it (once); you never raised, a rider never; a timid man 4 m off keeps away, at 2.5 m he steps in (his spell ended, brave until the window's end + 0.5 s); ImpactPunishEnabled off / on live (the OFF row counted); the window closes + 0.5 s: back to 0.6 / 0.086; YOUR broken block: the AI man fighting you presses and punishes it (the YOU line); shadow opens none; the [summary] punish lines", ImpactPunishThroughTheLogic);
            Step("impact punish (step 54) cost: a thousand AI men in contact, half facing a reeling target, through the fever's look with the punish's target read - small and allocating nothing", ImpactPunishCostStaysFlat);
            // Step 62 - more men fall, and the charge matters.
            Step("impact LOST FALLS (step 62): through the real shrug-off hook, the real decorator's KnockDown seam and the real logic - a decided fall on a blow the game would shrug off decided AT the shrug-off, the shrug-off REFUSED, the seam answering the same decision (one per blow), the Fall played; a fall the engine never plays backed up by our own blow after 0.5 s (once, owned by the attacker) and its Fall counted; refused in the water, then not seen; a decided stagger refuses the shrug-off too, nothing decided keeps it (a fresh man); a killing blow never decided; the switch off = the game's shrug-off; no backup at 0; a kick's fall backed up; the [summary] falls line", ImpactLostFallsThroughTheLogic);
            Step("impact THE CHARGE (step 62): through the real logic and decorator - toughness from the kit read once a man (a legionary on a looter x2.10, a wounded man softer), a blow from behind x2 / the side, a bow in his hand x1.3; the charge carried 0.5 s after a sprint into the FIRST blow only (x1.6 at a measured 2 m/s), never after 1.5 s, every blow while sprinting, never a rout; YOU charging into a fresh legionary's right-side block THROWN BACK (our blow on you owned by him from the tick, the red line, your Fall seen; walking in never, a parry never); an EMPTY man's charge rolls up to x1.5 through the real damage decorator (his plain blow x1.0), SprintFullCritical off; the [summary] lines", ImpactChargeThroughTheLogic);
            Step("impact THE CHARGE ON A LINE (step 62): 200 men each, seeded - a legionary walking into fresh peasant archers' front vs charging into their backs (fresh / half a bar); looters walking vs charging into a fresh legionary shield wall blocking right (thrown back / reeling); the charges line by kind", ImpactChargeOnALine);
            Step("impact step 62 cost: a thousand men (kits, angles, bows, a third charging), twenty clean hits a tick through the shrug-off hook, the seam and OnMeleeHit + twenty right-side blocks - small and allocating nothing", ImpactChargeCostStaysFlat);
            // Step 63 - falls on the hit, a strong rear blow, the wall pulls men back.
            Step("impact FALLS ON THE HIT (step 63): a FRIEND's blow (the engine cancels its damage - 0, no reaction) is never decided: no fall, no backup, counted in both summary lines; an enemy's decided fall played by the game at once; one the engine does not play backed up by our blow after 0.2 s (ImpactFallBackupSeconds' new default), not 0.5", FallsOnTheHit);
            Step("impact A STRONG REAR BLOW (step 63): the embedded defaults (+0.5 behind, +0.2 the side, the multipliers x1); 200 men each, seeded, through the real seam and logic - even FRESH men from the front 0 / 0, the side never down, BEHIND a real share down and staggered; the rear decision's +0.50 pressure (no multiplier); a legionary standing behind fresh peasant archers vs in front; the mission-start sentence and the direction line", RearBlowOnALine);
            Step("shield wall THE WALL PULLS MEN BACK (step 63): through the real logic with the stand-in body - a man thrown 2 m forward by a fall is out of place AFTER AN IMPACT at once, held again at a look every 0.1 s, never walked while down, walked back (the real input wish, facing the enemy) 0.5 s after he is up, the wish off and his callback released once back; ShieldWallPullBackSeconds 0 = no extra look, no walk (live back to 0.5: walked at once); a man just past the leash not out of place; the master switch mid-walk; the first lines and the summary; A / B - 20 men thrown every 10 s, in contact more than 1 m ahead with the walk back off vs on", WallPullsMenBack);
            Step("shield THE WALL'S BLOCKS COUNT (step 64): through the real logic's OnMeleeHit parts - a block the game calls WRONG side with the attacker 20° off the blocker's facing is a RIGHT-side block for the impact model (RightBlock), the guard lines and the arc's count, the first line in full; a charging looter into it thrown back; from 100° the game's verdict; the game's right side and a parry never looked at; an untracked attacker the game's (no native read); the arc / the switch live; the [summary] \"shield front arc\" line", ShieldFrontArc);
            Step("sprint THE AI CHARGE HITS AT SPEED (step 64): through the real logic with the stand-in body - a legionary's 6.5 s sprints at the charge order INTO contact (no end at 3 m) and his first attack ends it (the carry's clock from then); a looter's 2.5 s WAITS until an enemy is within 10 m, sprints into contact, ends when he stands still 0.5 s; a new order drops a wait; SprintChargeIntoContact off = an end at 3 m again, SprintChargeWaitBelowSeconds 0 = sprinted at the order (live); the first lines and the summary's into-contact line", ChargeSprintIntoContact);
            Step("impact THE CHARGE CARRIES TO THE BLOW (step 64): the carry 3 s by default; an attack begun inside it stays a charge to its blow 4 s after the sprint (full momentum - the impact model and the damage decorator's read agree), spent on that blow; an attack begun after the carry none; the charges line's carried-by-an-attack count", ChargeCarriesToTheBlow);
            // Step 65 - the AI kicks and bashes, the kicked man braces.
            Step("impact THE AI KICKS AND BASHES (steps 65 / 81): through the real decision point (a melee wind-up rolled by the shares) and attempt with the stand-in body and the real hook filter - a rolled kick (the guard first - the wind-up cancelled -, the kick key with the guard down, his own attack / events out, standing still in the commit), PLAYED when step 18's poll sees the Kick (its cost charged and counted), its hit counted once, ended when over, the cooldown refusing the next; a shieldman's rolled BASH (the guard up 0.4 s, then the key + the guard; a WeaponBash seen); a rolled bash into a raised shield kicked; NOT played; not a move in reach (a rider, routing, a ranged weapon, no target, beyond kick reach, not in front, the wind-up over) and the refusals (no Athletics, staggered, no stored swing) counted; the bar gate gone (a man at 30% kicks); the die's swing untouched; the switch / the master switch mid-attempt and the queue dropped; the [summary] lines", AiKicksThroughTheLogic);
            Step("impact THE DECISION POINT'S COST (step 81): 1000 AI men winding up ~8 swings a tick with an enemy in reach (a third with a shield), seeded - the queue, the rolls, the attempts and the contact samples through the real tick: small, 0 bytes", AiKicksInAClash);
            Step("impact THE KICKED MAN BRACES (step 65): through the real damage decorator's knock-back and KnockDown questions and the real logic - a looter's kick on a fresh legionary braced (the knock-back REFUSED, one decision, no fall, his leftover reaction at 5 x, measured), a lucky kick (the knock-back kept, a short reel at 2.5 x, no fall, no shield), 200 seeded kicks (about 1 in 8 short, 0 falls, 0 drops), a spent man step 50 in full, you braced (the red line) and your kick braced against (purple), KickBraceRefusesKnockBack off, the switch and the master switch; the [summary] lines", KickBraceThroughTheLogic);
            Step("THE FEAR FLOCK (step 67): through the real logic with courage's stand-in world - a fresh legionary line obeys exactly (fear 0); a frightened single rank of looters (morale 20, half a bar, nobody behind: fear 1, pull 1) leaves its line for its mass - the end man heads in, 20 s later the spread under 60% - and the shelter edges back at the hold's cap (0.5 m in 20 s); the input frame (the flock's vector in, courage's walk back wins, off with the switch); an armed fever ends a spell; FearFlockEnabled off lets everyone go at once and keeps measuring, on again; a fresh comrade in front ANCHORS a man in his spot, tired (30%) he lets him go; courage's walk back takes a lone frightened looter (the flock yields); a man 14 m out sprints back (half his store above the 50% reserve); a shield wall at morale 40 holds, at 25 breaks into the flock; a charge is a loose mass (men near the shelter left to their AI, a roamer 8 m out pulled in); the [fear] lines and the [summary] fear flock lines", FearFlockThroughTheLogic);
            Step("THE FEAR FLOCK (step 67) A / B: a frightened single rank of 12 looters facing 12 fresh legionaries for 30 s, the flock ON in one logic and OFF in another - the summary's spread ON well below OFF, the looks filed by the switch", FearFlockOnAndOff);
            Step("THE FEAR FLOCK (step 67) cost: a thousand men in two blocks face to face, half of them frightened looters flocking (courage on), every man looked at every 0.5 s, the flockers steered every 0.1 s - small and allocating nothing", FearCostStaysFlat);
            Step("THE WALL PUSHES AS ONE (step 68): through the real logic with the stand-in body - the first look sends the two held men of an ADVANCE (yours, the AI's) near the enemy 1 m toward the game's order slot 6 m ahead (not to it), and leaves a stop, a march 25 m off and a move within the step to the game; rewritten in the next frame after vanilla's update; the slot within the step = the game's frame back, out again = paced; a man stepping out on the leash keeps the paced frame; a charge = let go with the game's frame back; the switch off live = every paced man given back at once and nothing paced, on = paced at the next look; the step 2 m live; the ON samples by order group; the summary's two lines", WallAdvancesAsOne);
            Step("THE WALL PUSHES AS ONE (step 68) cost: a thousand men of an advancing wall, each one's vanilla update every 0.5 s followed by the flag and the paced frame - the pre-tick stays small and allocates nothing", WallPaceCostStaysFlat);
            Step("BACK TO THE LINE AFTER A KICK (step 68): a held man carried 0.8 m out by his own kick (inside step 63's 1.25 m) is out of place after a kick, never walked while the kick plays, walked back at once when it ends (no 0.5 s wait) and back within 0.25 m; a punish press the same; ShieldWallKickReturnMetres 0 = step 63 exactly; the summary's count", WallWalksBackAfterAKick);
            Step("NO KICK THROUGH A FRIEND (steps 68 / 80 / 81): a rear-rank man whose mate stands first in his strike path - his rolled kick refused (he swings), the line clear = his next one kicked; whom the kicks landed on (a FRIEND, his target) in the ai kicks line; AiKickFriendClearMetres 0 live = no check", AiKickNotThroughAFriend);
            Step("KICKS LANDED VS DECIDED (step 68): through the real decorator's seams and OnMeleeHit - a kick on an enemy decided once, a bash on a FRIEND named (not decided), a bash into a raised shield counted apart; the summary line with nothing unaccounted", KicksLandedVsDecided);
            Step("KICKS LAND ON ENEMIES, NOT FRIENDS (step 80): two two-deep lines of 12 files facing each other, shuffling and turning, half shieldmen, 3 minutes through the real looks and attempts with a stand-in engine finding the first body along the facing - the strike path ON: friends under 5% of the hits, enemies still kicked and bashed; OFF: the logs' problem", KicksLandOnEnemiesNotFriends);
            Step("KICKS AND BASHES AS A SHARE OF THE MELEE MOVES (step 81): step 80's two grinding lines, every man swinging on his own rhythm, the swing stack ON (at least 3, kicks use swings), a kick / bash 8, a swing 10, 3 minutes - the shares ROLLED at the wind-up near 20 / 35 / 45 (shieldmen) and 35 / 65 (no shield), DONE after the stack, the cooldown and the strike path; never a kick / bash at an empty stack; friends under 5% of the hits", SharesInAGrind);
            Step("THE STACK GATES THE KICK (step 81): the minimum 3 at 30% of a bar, a played kick spends one stored swing, at 0 the rolled kick is refused (stack empty - he swings), one back = kicked", TheStackGatesTheKick);
            Step("MEETING A BASH OR A KICK (step 82): through the real decorator's seams and OnMeleeHit with a stand-in engine - a bash the shield stopped guarding RIGHT: the spent basher staggered from the tick (70%), the blocker untouched; a bash on a weapon guard RIGHT with the brace OFF: the knock-back refused, held; a bash the shield stopped guarding DOWN: an exhausted blocker staggered from the tick (85%); a kick on a shield guarding DOWN: the spent kicker thrown down by our blow; a kick with no guard: step 65's brace; a kick the shield stopped, no guard down: nothing; YOU met correctly (the YOU line); the rules / the master switch off = step 65; the summary lines", MeetRowsThroughTheLogic);
            Step("THE AI READS IT (step 82): through the real logic - rolled once per start (a sure read; a looter's one in eight), the guard RIGHT only after 0.15 s, the stopped bash then met correctly and the read ended at the hit (in time, met); a hit after 0.1 s = TOO LATE; a kick read = the guard DOWN; no hit = let go after 1.5 s; never you, never a man in his own kick; YOUR bash seen by the poll = the man you aim at reads it; the master switch off lets the guard go at the next tick", AiReadsTheMeetThroughTheLogic);
            Step("SHIELDMEN BASHING FRESH VS TIRED DEFENDERS (step 82): 400 seeded bashes each by an average shieldman at half his bar on a skilled shieldman whose own guard is DOWN unless he reads the bash - FRESH defenders punish at least a fifth of the bashes and are staggered under 5%; TIRED ones (10%) read less and are staggered or felled at least 40%", BashesOnFreshAndTiredDefenders);
            Step("MEETING A BASH OR A KICK (step 82) cost: 1000 men, 8 kicks / bashes starting a tick (each target rolling his read), every reader ticked, 20 stopped bashes decided a tick with the guard read - small and allocating nothing", MeetCostStaysFlat);
            Step("WINNERS DON'T HANG BACK (step 69): through the real logic with courage's stand-in world - 12 tired green looters charging 2 broken looters: their fear (0.69) calmed to 0 by the win (odds 6:1), nobody flocks, the roamer 8 m out left to his charge; the same world with FearWinCalms OFF flocks and pulls the roamer in, ON live ends the spells 'winning'; a partial win (clear 4:1) leaves them afraid but the charge MOPS UP - the roamer 12 m out not leashed, FearMopUpWinPercent 0 live = pulled in; a LOSING rank flocks exactly as step 67 (every position and fear equal with every step-69 rule off); frightened men with the enemy 20 m off stand to their order, at 10 m flock, at 25 m their spells end 'no enemy near', FearThreatMetres 0 live = step 67; the [summary] winning line", WinnersDontHangBack);
            Step("IDLE MEN (step 69): men of a charge standing 10 m from the enemy for 7 s, filed by who holds them - a calm man (the game), a crossbowman (normal), a bracer, a timid man, a frightened looter the fear flock holds 5 m from his mass, a held shield-wall man on ADVANCE; never a walking man, a man in a fight or a man on a hold; the enemy read beyond courage's scan; the first-time lines; measuring with the flock off; the [summary] idle line", IdleMenDiagnostic);
            Step("WINNERS DON'T HANG BACK (step 69) cost: a thousand men charging in two blocks, half winning legionaries, half frightened looters - the win, the threat line and the idle watch on every look - small and allocating nothing", Step69CostStaysFlat);
            Step("THE BAND (step 70): through the real logic with courage's stand-in world - 40 frightened looters in a column 3 wide and 14 deep facing a fresh line 20 m wide: the band faces the line (20 m, 6 ranks), the men keep their places along it, the men behind full files slide out toward its ends - the column becomes a band at most 6 ranks deep; the A / B with FearBandEnabled OFF keeps the deep column (step 67's point), ON live forms the band; a front man with an enemy at 1.5 m is let go to strike, walked back past the leash, FearStrikeReachMetres 0 live = never; the [summary] band line", FearBandAlongTheLine);
            Step("NO LONE RUNNER BEHIND THE LINE (step 70): a feverish looter ALONE 3 m behind a legionary line (his target you) - the fixes OFF: his fever keeps him there, counted by the fever press and his target; a calm one chasing a hero counted too; the fixes ON live: his fever press refused, courage walks him back round the line to his group; his sprint into contact ends alone while a charging MASS keeps its sprints and its fever (the first charge's punch); the first-time lines and the [summary] lone-runner line", NoLoneRunnerBehindTheLine);
            Step("THE BAND + THE LONE RUNNER (step 70) cost: a thousand men - 500 frightened looters in a deep column against 500 legionaries - the band published and ranked every 0.5 s, every place in it, the lone-runner look - small and allocating nothing", Step70CostStaysFlat);
            Step("NO ACCIDENTAL CHARGE ORDERS (step 72): through the real order-click guard with a stand-in world and a stand-in of the game's release (a focused unit = a targeted charge, else the placer's move), in both frame orders - a drag from the ground across an enemy banner and released on it = a MOVE (the targeting held off from the press through the release, given back after); a plain click ON the icon = the game's charge; a click near the banner (vanilla's 70 px, not on the icon) = a move; a click on the ground = a plain move; no button (the F keys) = the targeting never touched; the menu closing mid-hold and OrderNoAccidentalCharge off mid-drag let go at once; ModEnabled off = vanilla (the drag charges); a naval stand-down; a charge slipping through a conversion = the FAILURE line; the [orders] first-time lines and the [summary] order clicks line", NoAccidentalChargeOrders);
            Step("LINE DISCIPLINE BY TIER (step 73): through the real line discipline with a stand-in world of formations - Toni's 120 tier-1 recruits on the AI's FormOrder Wide (48 files, 2.5 deep) narrowed at the first look to 24 files x 5 ranks, his 60 tier-5 men (D 1) never touched, a mixed unit (70 T1 + 30 T3 + 20 T5, D 3.83) to 32 files (3 ranks + 24); cavalry, a square, a column untouched; nothing again while nothing changes; 20 lost = an AUTO-SHRINK to 20 files at once; the AI widening again narrowed at once; the mean depth recounted when the veterans fall (23 files); YOUR drag of 40 m stops at the capped maximum width (12 files) and the order keeps to it - counted at the cap, the on-screen line once; the preview cap off = the drag wide, narrowed at the next look (a clamp of yours), not said twice in 2 s; the game's arrangement order puts its own line back = ours again; your switch off = your width given back, the AI's kept; a clamp the game does not take retried after a second, not every frame; naval never, a siege only with its switch; a non-plain arrangement skipped once; the [lines] first-time lines and the [summary] lines", LineDisciplineThroughTheLogic);
            Step("LINE DISCIPLINE BY TIER (step 73), the game's side: the REAL DisciplinedLineFormation (the game's own LineFormation code) over a stand-in owner - no cap = the game's own maximum width (120 men: one rank at the widest interval), the cap answered and counted, the game's own floor (GetFileCountFromWidth) of the cap width gives exactly 24 files, a 60 m drag clamped by the game's rule = 24 files, never below the game's minimum, a cap above the game's maximum changes nothing, the simulation clone keeps the type with no cap, the staggering copied", DisciplinedLineIsTheGamesLine);
            Step("LINE DISCIPLINE BY TIER (step 73) cost: 20 formations / 1000 men looked at every frame, the depth recounted every second, half of them re-widened by the AI each second - the look stays small and allocates nothing", LineDisciplineCostStaysFlat);
            Step("SMALL UNITS KEEP A WIDE LINE (step 83): through the real line discipline with step 73's stand-in world on the shipped map - Toni's 20 tier-1 recruits (D 6 = 4 files x 5 ranks, a column) held 3 deep: 7 files x 3 ranks, at least twice as wide as deep, yours and the AI's; the drag preview's cap = the same 7 files and a 40 m drag stops there; 8 looters 4 x 2; 100 recruits keep their tier's 6 (17 files) and, cut to 20, shrink to 7 files; LineMinWidthToDepthRatio 0 live = step 73's 4 files, 2 again = 7; the REAL DisciplinedLineFormation over 20 men floors the capped width to 7 files; the [lines] small-unit lines", SmallUnitsKeepAWideLine);
            Step("TIER-1 MEN GET A CHEAP CULTURE SHIELD (step 84): through the real logic's spawn path (StartUnitKit / GiveUnitKit - what OnAgentBuild calls after the game's build) with a stand-in engine side - each main culture's recruit in each of his four vanilla variants gets his culture's shield in slot 1, the Imperial / Khuzait scythe and the Battanian maul swapped for the culture spear, every other weapon (the pitchforks have a one-handed use) kept; a Nord youngling with his own shield, a hero, a rider, a tier-2 man and a looter left alone; a shieldless Nord takes his culture's cheapest shield and spear, a culture with no shield skipped and said once; GiveTier1Shields off = nothing given and no engine read, on again = given; an unknown id mid-battle = that culture skipped and said, the real id again = given; a tournament's fixed gear; the [units] lines and the [summary] line; 988 spawns allocate nothing", TierOneShields);
            Step("THE TIER-1 SHIELDS SHOW IN THE TROOPS' OWN GEAR (step 84b): through the real TroopGear and the module's real write path on the game's own Equipment objects - a stand-in Sturgian Recruit of 4 battle sets and an Imperial Recruit with the scythe: the culture shield in slot 1 of every set, the scythe set's spear, the pitchfork kept; a mission start and an unrelated setting change nothing; step 84's spawn path on a man from an edited set gives nothing (has a shield) - no second shield; an item setting live re-applies (never twice); GiveTier1Shields off live = the exact originals back, the master switch too, on again = edited; the game's end = everything back and a later setting change touches nothing; the [units] troop gear lines", TroopGearInMemory);
            Step("THE BLOCK HOLD (step 74): through the real logic with courage's stand-in world - 180 recruits (D 6 on the shipped map: 30 files, 44 m) CHARGING a 28.5 m line, driven by a stand-in of the game's charge (each man at the nearest enemy, the flank men round the line's ends to its rear, the right-hand files drifting out along a rock): OFF the crowd spreads and trails round the flank; ON it stays within its 44 m (10th-90th) and nobody beyond the half-width + the slack, men walked back round the flank, the front men let go to fight, contact as fast as OFF; the live switch; the [block] lines (first look, FIRST CONTACT, the first man walked back) and the [summary] block hold / block shape lines; a D 1 elite unit never touched; the input frame - the block's vector the LAST wish (the flock and courage's walk back win, the master switch off = none)", BlockHoldThroughTheLogic);
            Step("THE BLOCK HOLD (step 74) cost: a thousand men - 500 recruits charging with 100 trailing far round the flank, 500 legionaries - every block frame, every walked-back man steered - small and allocating nothing", Step74CostStaysFlat);
            Step("A STANDING WALL STANDS FIRM (step 75): through the real logic with a stand-in world - ten men of a HALTED shield wall, an enemy 2.2 m in front of each, shoved 0.5 m every 4 s, swinging every 1.3 s: ON nobody steps out (every step the leash would give refused and counted), the engine's walk target pinned to each slot, the tolerance 0.3 m, the shoves walked straight back, the men within ~0.3 m and swinging from the slot; OFF live = they step out on the leash as today, the game's frame and tolerance given back; an ADVANCE never; a MOVE only at its place and still (latched while it shuffles, let go when pushed far); a running sprint ended; his own kick walked back after it plays; a charge, a man far behind and the master switch let go; the [wall] lines and the [summary] wall stands firm line", StandingWallStandsFirm);
            Step("A STANDING WALL STANDS FIRM (step 75) cost: a thousand men standing firm with an enemy in front - the flag, the pinned frame, the refused steps' enemy reads - small and allocating nothing", StandFirmCostStaysFlat);
            Step("THE AI PREFERS THE SPEAR (step 76): through the real logic's looks with a stand-in engine side (slots, ranks, the enemies around each man, a crude stand-in of the native AI's weapon choice) and the REAL stat decorator - every AI man with a spear AND a sword favours the spear x35 (javelins + a sword never; you never); an enemy at 2.0 m (a 2 m spear: radius 2.2) = the sword x55 after 2 s, not before; it steps back = the spear 4 s after the last close look; a second-rank man of a halted wall with an enemy at 1 m keeps the spear, a man in rank 1 with NOBODY in front draws his sword - and takes the spear at once when his file is filled; facing only spears = keeps his, a swordsman among them = the sword; a pike (radius 2.8) counts an enemy at 2.6 m, a short spear (1.9) not at 2.0 m - SpearCloseFromLength off = RBM's 2.5 live; a rider riding through at 6 m/s never, bogged down at 2 m/s the sword after 2 s and the lance 2 s after; a dropped spear = no favor; the loadout read once (RBM's cache); the wield fallback (off by default) on = a sidearm Wield turned into the spear's, off = cleared; the switch / the master switch off = every favor lifted (1 / 1 / 1) while the looks measure; the [spears] lines and the [summary] spears lines", SpearmenKeepTheirSpears);
            Step("A THRUST PASSES A FRIEND (step 76): the REAL damage decorator over the game's CustomAgentApplyDamageModel - a second-rank spearman's thrust into his front man answers ContinueChecking (counted: a polearm, a back rank), the same man then hitting the enemy behind within 0.5 s counted, the friend's flinch / no reaction counted; a swing into a friend still BOUNCES (the game's answer), a kick the game's; polearms only = a sword's thrust bounces; the friendly-hit stun 0.4 -> 0.1 s through the game's ManagedParameters, given back when switched off / the master switch / the summary; an enemy's thrust never touched; the [spears] lines and the [summary] thrusts past friends line", ThrustsPassAFriend);
            Step("THE AI PREFERS THE SPEAR (step 76) cost: a thousand men (666 with a spear and a sword) looked at every ~0.5 s - small and allocating nothing", SpearsCostStaysFlat);
            Step("PRESS, DON'T PASS (step 77): through the real logic with courage's stand-in world and the REAL input filter (AiInputHook.Apply) - Toni's 108 recruits (D 6, 18 files, 26.6 m) CHARGING his 23 m two-deep shield wall under a stand-in of the game's charge with half their rear ranks dancing round its ends and in behind it: OFF men get behind the wall (crossings counted by where and cause - the dancers' targets); ON every man stays on his side (nobody past the front by more than the tolerance at any step), they press its front, no crossing; the live switch; the [press] lines and the two [summary] lines; a man already behind it walked ROUND its nearest end to his side", PressThroughTheLogic);
            Step("PRESS, DON'T PASS (step 77): a REAL GAP (four files fallen - one ~2.8 m gap in the front) lets the men facing it through (allowed - a gap), nobody else past, a man behind the broken wall left there; a GENUINELY WIDER unit (40 files, 60 m) wraps round the wall's ends as a body while its middle presses and a lone straggler of it does not go round; the input frame - the press's walk after the wall's pull, the clamp LAST and never with the master switch or the switch off", PressGapsAndWraps);
            Step("PRESS, DON'T PASS (step 77) cost: a thousand men - 500 recruits pressing a 500-man wall - every line published, every pressing man clamped and steered - small and allocating nothing", Step77CostStaysFlat);
            Step("THE BATTLE'S RHYTHM - ROTATION (step 78): through the real logic with courage's stand-in world and the REAL input filter - two lines grinding (12 files x 3, 1 m apart), the south front rank at 25%, its second rank at 85%: OFF the chances counted, nobody moves; ON the fresh man walks up BESIDE each tired one while the tired one holds and guards, the slots swap (the game's call), the tired man backs into the second rank and rests there - every file keeps a man at the front at every step (the line never opens), the front fresh; the input frame (the swap's walk before the flock, the attack held, the clamp, the master switch); a held wall man's slot = the swap's place", RotationInTheGrind);
            Step("THE BATTLE'S RHYTHM (step 78) cost: a thousand men - two 500-man blocks in contact, every other rank tired - every swap looked for and steered, the units published - small and allocating nothing", Step78CostStaysFlat);
            Step("NO WRAPPING (step 78b): a 69 m unit (46 files x 5) charging a 31 m held line under a stand-in of the game's charge that sends every man beyond the line's end round it - with step 77's wrap (the A / B) they go round both ends; with the wrap off nobody passes the line's front or goes round, the wings stand off the ends behind the bent front (30 degrees) and a man forward of it is walked back; the same unit ORDERED there (a move) goes round (the order's own place) and never through the middle; the [press] lines and the third [summary] line", WideUnitStandsOffTheEnds);
            Step("EACH MAN RESTS ON HIS OWN (step 78c): a MESHED six-minute grind (14 x 3 a side, a metre into each other) through the real logic with courage's stand-in world and the REAL input filter - the bars drain in contact and refill out of it: many staggered individual rests (each man's own marks, never a unit at once), every resting man backs WALKING and never toward his enemy, facing him, at most 4 m from where his rest began and never more than ~5 m behind his own front (no mass walk-off), held back at the edge of reach; back in by his own mark and by contagion; a new charge order does not drag resting men in; the [rhythm] lines and the two [summary] rest lines", MeshedGrindRestsManByMan);
            Step("EACH MAN RESTS ON HIS OWN (step 78c): BOTH SIDES TIRED - two spent lines in contact step back man by man and a gap of 2.5 m+ appears between them with no unit rule (the diagnostic counts it for both sides); ONE SIDE TIRED - the fresh side follows and presses, the tired men back at most 4 m and then stand and defend; the input frame - the rest's walk beats the press's, the swap's, courage's and the block's, the backpedal, the wall's pull and the fear flock beat it, its guard / attack hold, the master switch; the switch off ends every rest at once", BothSidesTiredAGapAppears);
            Step("EACH MAN RESTS ON HIS OWN (step 78c): A SHIELD WALL never leaves its slots - a 12 x 2 held wall ground down by a fresh enemy until its men are tired: they rest IN their slots (guard only, no walk, the leash 0), 0 men more than 0.5 m out of their slots because of resting (the summary's number), the rotation may still swap them", ShieldWallRestsInItsSlots);
            Step("EACH MAN RESTS ON HIS OWN (step 78c) cost: a thousand men - two 500-man blocks in contact, every man tired, resting, steered, the contagion rolled, the gap measured - small and allocating nothing", Step78cCostStaysFlat);
            Step("WAR CRIES (step 79): a 1000-man charge order (10 formations, both armies at once) through the real order poll and queue with a counting voice - 40% of each unit, at most 12, staggered over 1.5 s, the battle never past 16 cries in any 1 s, a unit never past 8, the routed / the dead / you never, riders yes; the unit's roar cooling 15 s; a charge 5 s later held by each man's own 8 s, nobody twice within 8 s; 0 bytes; WarCriesEnabled and the master switch drop what waits", ThousandManChargeStaysUnderTheCaps);
            Step("WAR CRIES (step 79): the meshed grind of step 78c with the cries ON and OFF - the rests IDENTICAL (sound only); a rested man going back in by his own mark shouts ~35%, a mate going in by contagion answers ~20%, nobody twice within 8 s; the [warcry] first-time lines", RestedMenShoutGoingBackIn);
            Step("attack rate (step 20b): a slower swing (AttackAnimationMinPercent 85, x0.91 at f 0.4) never lengthens the pause - D taken at full animation speed (played x the multiplier), the timer = 0.82 s x (1/m - 1) as with full-speed animations, the gap it leaves the same; the first-timer line, the timer row and the band row name the played attack; back at 100 the tired man is recomputed and D = played", SlowerSwingKeepsThePauseInSeconds);
            Step("attack rate (step 13): your input gate goes FIRST in the behaviour list, so the reverse pre-tick loop runs it right after MissionMainAgentController; with no pause it touches nothing", GateGoesFirstInTheBehaviourList);
            Step("attack rate: the first slowed fighter's every touched value before → after, each checked against its factor (step 13: animations x1.00, AI decisions unchanged)", FirstSlowedIsLogged);
            Step("master switch: ModEnabled off is vanilla at once - damage unrolled but recorded, penalties lifted, no costs (no swing, kick or block charged), every step back released, the Athletics bar gone; on = everyone full, the bar back", MasterSwitchIsVanillaLive);
            Step("Athletics: a failure is logged once per site, counted, and reported in the summary", AthleticsFailSafe);
            // Step 19 - the hideout boss fight is a fresh start for the player's side (the game's objective type, stand-in teams).
            Step("hideout boss fight (step 19): the game's boss objective (and nothing else) starts it once; the BATTLE refills the whole player's side - to a wound's cap, not past it - and ends what ran on them (a step back, an AI pause, a queued step back and pause, a deferred pause, your pause), speeds re-targeted, the man standing aside and the fresh boss's side untouched; the DUEL refills you alone; HideoutBossFightRefill off (live) = nobody, logged; a failing side read = ONE [error], nothing refilled; the [athletics] line, the [summary] line (incl. \"intro played, fight never seen: tell Claude\"), the fresh-start ends named in the step-back and attack-rate summaries", HideoutBossFightIsAFreshStart);
            // The HUD (step 6) - the prefab against the game's own types and files, the real view and
            // ViewModel driven by made-up frames with a stand-in layer.
            Step("HUD prefab: well-formed; every element a widget type of the game, every attribute a real property with a valid value, only vanilla brushes and sprites, every @binding typed exactly like its ViewModel property, every ViewModel property drawn", HudPrefabIsValid);
            Step("HUD player bar: through the real view - hidden outside fights, built in battle with you on the field, number / fill / colours green → blue → yellow → orange → red / peak marker / wounded part / Exhausted pushed and logged, refreshed every HudRefreshSeconds (live), layout live, removed and rebuilt with its reason (Hide battle UI, ShowPlayerBar, AthleticsEnabled, photo mode, no player, not a fight, ModEnabled), stealth/tournament/duel are fights, suspended, paused, mission end, the [summary] hud lines", HudPlayerBarThroughTheView);
            Step("HUD player bar outside a battle (step 12): the walk-about mode - hidden with empty hands and a full bar, built with a weapon drawn, a 1 s grace (a weapon switch does not rebuild it), kept while Athletics refills, gone once full (a wound's cap counts as full); never in a conversation, barter, cutscene or deployment; ShowPlayerBarOutsideBattles, no player, an untracked player, the master switch; fights unchanged; every build / removal logged with its reason; the attach text; the [summary] clause", HudPlayerBarOutsideBattles);
            Step("HUD fail safe: an exception, a movie that does not load, a failure with the bar up or a view never attached - the view is disabled for the mission, its layer removed, [error] logged, nothing thrown", HudFailSafe);
            // The Attack recovery bar (step 13, Anton's "attack recovery" bar above the Athletics bar).
            Step("recovery bar prefab: well-formed; every element a widget type, every attribute a real property, vanilla brushes and sprites only, every @binding typed exactly like AttackRecoveryVM, every ViewModel property drawn", RecoveryPrefabIsValid);
            Step("recovery bar: through the real view over a running player timer - hidden outside a battle at rest, full and quiet in battle, EMPTY at the attack, a press during the swing flashes it (two pulses), filling over the pause with the seconds inside (\"0.6 s\" → \"0.3 s\"), full again after; no flash with FlashBarOnEarlyAttack off; the layout live; removed by its switch, ShowPlayerBar, AttackRatePlayerTimer and ModEnabled and rebuilt; the log lines and the summary line", RecoveryBarThroughTheView);
            Step("recovery bar with the swing stack (step 32): through the real view - \"Swings 6/6\" full at rest, \"Swings 0/3\" amber with the seconds after a burst of six, half full halfway, \"Swings 1/3\" when one is back, \"Attack recovery\" with the stack off; the first-empty line and the summary clause", RecoveryBarShowsTheStack);
            Step("sprint bar prefab (step 33): well-formed; every element a widget type, every attribute a real property, vanilla brushes and sprites only, every @binding typed exactly like SprintBarVM, every ViewModel property drawn", SprintPrefabIsValid);
            Step("sprint bar (step 33): through the real view - never outside a battle; in battle \"Sprint 7.5 s\", full, blue, the start mark at 20% of the bar, 100 px up (54 + 46); yellow and draining while you sprint, grey below the mark; SprintMinStartPercent 0 hides the mark (live); removed by ShowSprintBar, ShowPlayerBar, SprintEnabled and ModEnabled and rebuilt; the log lines and the summary line", SprintBarThroughTheView);
            Step("orders strip prefab: well-formed; every element a widget type, every attribute a real property, vanilla brushes and sprites only, the {Cells} item template bound against OrderStripCellVM, every @binding typed exactly, every ViewModel property drawn", StripPrefabIsValid);
            Step("orders strip: through the real view with stand-in cards - hidden while the menu is closed; open: cells on the cards' bottom edges (keyboard columns, another UI scale, the gamepad row, RTS Camera's one set), lifted at the screen's edge, values / colours by f / ± band / health, live switches, new stats on the version; closing and reopening quiet; a short mismatch waited out, a long one → the panel; no order layer, not whole sets, never drawn, OrderStripUnderCards off (and on again mid-open), a lost card tree re-scanned; the usual gates; the [summary] lines", StripThroughTheView);
            Step("orders strip fail safe: a failing card walk or a view without sources - disabled for the mission, its layer removed, [error] logged, nothing thrown", StripFailSafe);
            // Step 20 - hold ALT: the numbers under vanilla's formation markers.
            Step("ALT markers prefab: well-formed; every element a widget type, every attribute a real property, vanilla brushes (the markers' own NameMarker.Distance.Text) and sprites only, the {Labels} item template bound against AltMarkerLabelVM, every @binding typed exactly, every ViewModel property drawn", AltMarkersPrefabIsValid);
            Step("ALT markers (step 20): through the real view with stand-in marker sources - shown exactly with vanilla's own flag (the player down too), its rule copied when unreadable (ALT or the orders menu); a label under each marker (the marker's bottom centre + the gap, at UI scale 1 and 4/3), keyed by formation through a re-sort, values / colours by f / ± / health / bar; the enemy's (AltMarkersShowEnemy live), none behind the camera, faded or not laid out, a pinned marker's label follows it; live settings, new stats on the version; hiding and showing quiet; a marker without its widget, no widgets, no layer (our own projection, the next show live again); the gates; the [summary] lines", AltMarkersThroughTheView);
            Step("ALT markers fail safe: a failing marker read or a view without sources - disabled for the mission, its layer removed, [error] logged once, nothing thrown", AltMarkersFailSafe);
            // Step 44 - hold ALT: a small mark above each man in a fever.
            Step("fever marks prefab (step 44): well-formed; every element a widget type, every attribute a real property, the native sprite only, the {Marks} item template bound against FeverMarkVM, every @binding typed exactly, every ViewModel property drawn", FeverMarksPrefabIsValid);
            Step("fever marks (step 44): through the real view over the real logic's fevers (TryGetFever / the walk) with a stand-in camera - shown exactly with vanilla's flag (the player down too), its rule copied when unreadable; a mark above each man in a fever: armed bright orange and full, burning 70% → orange-yellow, fill 0.7, fading to a faint yellow sliver, gone when it fades; calm men, you, behind the camera, closer than 1.5 m, off the screen, not placed - none; the enemy's (FeverMarksShowEnemy live); FeverMarkMax picks the nearest on the screen; vanilla's fade at 5 m, 60% size at 60 m, the height / length live, UI scale 4/3; no camera; FeverEnabled off; a hold ends them; the gates; the log and the [summary] line", FeverMarksThroughTheView);
            Step("fever marks fail safe: a failing read or a view without a source - disabled for the mission, its layer removed, [error] logged once, nothing thrown", FeverMarksFailSafe);
            Step("fever marks (step 44) cost: 1000 men, 400 in a fever (half the enemy's, some behind the camera), the cap 200 - a frame stays small and allocates nothing once the pool has grown", FeverMarksCostStaysFlat);
            // Step 46 - hold ALT: every man's Athletics bar above his fever bar (the same view).
            Step("Athletics bars (step 46): through the real view over the real logic's men with the stand-in camera - every man but you a bar (green / blue / yellow / orange / red by f, the wound cap), the two in a fever their fever bar under it, a rider 1 m higher; the fever ends = no jump; the enemy switch; ShowFeverMarks off = the bars drop onto the point; ShowAthleticsMarks off = step 44 exactly; the cap = the nearest on the screen (an off-screen man nearer takes no slot, only 4 projected); gap / length / bands live; vanilla's fade; FeverEnabled off keeps them; nothing to draw / both switches / Athletics off / vanilla's flag; the log and the [summary] line", AthleticsMarksThroughTheView);
            Step("Athletics bars fail safe: a failing placement mid-walk - disabled for the mission, its layer removed, one error, nothing thrown", AthleticsMarksFailSafe);
            Step("Athletics bars (step 46) cost: 1000 men ALL on the screen (every one a candidate), 400 in a fever, the cap 200 - about the cap's worth projected, a frame small, nothing allocated once the pool has grown", AthleticsMarksCostStaysFlat);
            Step("the near view (step 58): through the real view over the real logic's men with the stand-in camera - the five men nearest the screen's CENTRE within 15 m of the camera (the enemy's too; a man nearer the camera at the screen's edge waits, a dead-centre man at 25 m never counts, the rest never projected), no distance fade; the enemy switch, AltNearMarksMax / AltNearMarksMetres live, FeverMarkMax the ceiling; AltMarksEveryone = step 46's view exactly (every man on the screen, vanilla's fade); the master switch; the first-bars line with the mode and each man's angle; the summary's mode clause", NearMarksThroughTheView);
            Step("the near view in a TOURNAMENT (step 58): no formation markers (the flag unreadable) - nothing until the game's ALT key is held, then the near bars with the man you fight first (his tired orange bar, bright), the five nearest the centre within 15 m; gone when ALT is let go", NearMarksInATournament);
            Step("the fever share under the formation markers (step 58): through the real ALT labels - \"fever 12/40\" under \"ready 34/40\" for a charging formation, the enemy's \"fever 60/80\", none for a calm one; ShowFeverOnLabels / FeverEnabled live, the line gone when the surge ends, the master switch; the values line and the [summary] fever-share line", FeverShareUnderTheMarkers);
            Step("the near view (step 58) cost: 1000 men on the screen, hundreds within 15 m - the walk as step 46, about five projections a frame, a frame small, nothing allocated", NearMarksCostStaysFlat);
            // Step 24 - the ready count (men not bracing) on the strip and under the markers.
            Step("THE MORALE EYES (step 67): through the real view over the real logic's men with the stand-in camera - a blue morale bar (morale 64 → 0.64) at the bottom of the near men's stacks, the fever bar lifted above it by its height + the gap; no morale known (a tournament) = its place kept, no bar; the enemy's by AthleticsMarksShowEnemy; ShowMoraleMarks off = the stack without it; only the morale bars on = the men still marked; the master switch; the first-bars line and the summary clause", MoraleBarsThroughTheView);
            Step("THE MORALE EYES (step 67) under the formation markers: through the real ALT labels - \"morale 64, fresh 41%\" on the nearest formation (MoraleLabelMax 1), the enemy's \"morale 22, fresh 10%\" too at 4, the 90 m formation only within 100 m (MoraleLabelMetres), ShowMoraleOnLabels live, the master switch, the values line and the [summary] morale-line line", MoraleLineUnderTheMarkers);
            Step("ready count (step 24) on the orders strip: through the real view - \"ready 36/40\" under each cell's bar, plain / yellow / red by the share ready, the cell one row deeper (the 1080p bottom card lifted only while it is drawn), the fallback panel's rows, new stats on the version, the thresholds / BraceEnabled / ShowReadyCount live, the master switch, the values line, the [summary] line", ReadyCountOnTheStrip);
            Step("ready count (step 24) under the formation markers: through the real view - yours and the enemy's, colours by the share ready, the thresholds / BraceEnabled / ShowReadyCount live at the next frame, new stats on the version, the master switch, the values line, the [summary] line", ReadyCountUnderTheMarkers);
            Step("MCM still not loaded after phase 1", () => Check(!McmLoaded(), "MCMv5 got loaded during phase 1"));

            // Phase 2 - a player WITH MCM.
            _allowMcm = true;
            Step("MCM's DLL loaded by another mod, MCM's module not enabled (step 12): one line, no page, no attempt, no retry", McmModuleNotEnabled);
            Step("MCM's module on but MCM never ready (step 12): no try before the main menu, the first + " + McmPlan.MaxRetries + " retries, then one line and silence", McmNeverReadyIsCapped);
            Step("MCM present: both settings pages (the player page and Advanced - step 59) build through MCM's real fluent builder", McmPageBuilds);
            Step("MCM pages: every setting on exactly one page, right type, range, hint, group; each page's group order and buttons", McmPageMatchesSchema);
            Step("MCM sliders on both pages write the live settings at once (hot swap)", McmSlidersAreLive);
            Step("MCM Reset (each page's 'default' preset) restores the mod's defaults (defaults.json) for that page's settings, not the values at build time", McmPresetRestoresDefaults);
            Step("MCM Done writes config.json", McmDoneWritesFile);
            Step("MCM buttons: \"Save current values as a defaults file\" (Advanced) writes a clean defaults.json beside config.json; \"Revert all to defaults\" (the player page) is live, logged, rewrites config.json, refreshes both pages", McmButtons);

            // Step 11 - LAST (it claims the one-copy slot for good and runs a game start).
            Step("two copies enabled (dev + release): the second SubModule stands down - no log line, no model, no mission logic, no message; the running one registers ONE decorator of each kind and reports the other once ([compat] + one message)", TwoCopiesOneRuns);

            Console.WriteLine();
            TraxLog.Release(); // the log handle (R6) - the temp folder can go
            if (Failures.Count == 0)
            {
                Console.WriteLine("OFFLINE SMOKE: all checks passed.");
                // TRAX_SMOKE_KEEP=1 keeps the folder, to read the log lines the checks produced.
                if (Environment.GetEnvironmentVariable("TRAX_SMOKE_KEEP") == "1") Console.WriteLine("kept: " + _dir);
                else try { Directory.Delete(_dir, true); } catch { /* temp */ }
                return 0;
            }
            Console.WriteLine("OFFLINE SMOKE: " + Failures.Count + " check(s) FAILED - files kept in " + _dir);
            foreach (var f in Failures) Console.WriteLine("  - " + f);
            return 1;
        }

        // ------------------------------------------------------------------ plumbing

        private static void Step(string name, Action check)
        {
            int before = Failures.Count;
            try
            {
                check();
            }
            catch (Exception e)
            {
                Failures.Add(name + ": threw " + e);
            }
            Console.WriteLine((Failures.Count == before ? "  ok    " : "  FAIL  ") + name);
            for (int i = before; i < Failures.Count; i++) Console.WriteLine("        " + Failures[i]);
        }

        private static void Check(bool condition, string failure)
        {
            if (!condition) Failures.Add(failure);
        }

        private static void LogHas(string text) => Check(LogText.Contains(text), "log lacks: " + text);

        private static bool McmLoaded() =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MCMv5");

        /// <summary>Game DLLs from the game's bin and Native's bin; MCM only in phase 2.</summary>
        private static Assembly? Resolve(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            if (name.StartsWith("MCM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Bannerlord.", StringComparison.OrdinalIgnoreCase)
                || name == "0Harmony")
            {
                if (!_allowMcm) return null; // like a player without MCM (and its Harmony dependency module)
                // MCM's own folder, then its dependency Bannerlord.Harmony (Workshop 2859188632),
                // a sibling folder of MCM's in the Workshop tree.
                string harmonyBin = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(_mcmBin)!)!)!,
                    "2859188632", "bin", "Win64_Shipping_Client");
                foreach (var dir in new[] { _mcmBin, harmonyBin })
                {
                    string candidate = Path.Combine(dir, name + ".dll");
                    if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
                }
                return null;
            }
            foreach (var dir in new[]
                     {
                         Path.Combine(_gameFolder, "bin", "Win64_Shipping_Client"),
                         Path.Combine(_gameFolder, "Modules", "Native", "bin", "Win64_Shipping_Client"),
                     })
            {
                string candidate = Path.Combine(dir, name + ".dll");
                if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
            }
            return null;
        }

        private static object? GetStatic(Type t, string field) =>
            t.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null);

        private static void SetStatic(Type t, string field, object? value) =>
            t.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(null, value);

        // ------------------------------------------------------------------ phase 1

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TypesLoadWithoutMcm()
        {
            var module = typeof(SubModule).Assembly;
            Type[] types;
            try
            {
                types = module.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Failures.Add("GetTypes() threw - the mod would not load without MCM: "
                    + string.Join("; ", e.LoaderExceptions.Select(x => x?.Message).Distinct()));
                return;
            }
            Check(types.Length > 5, "suspiciously few types: " + types.Length);
            Check(types.Any(t => t.FullName == "TraxCombat.SubModule"), "SubModule type missing");
            Check(!McmLoaded(), "GetTypes() pulled MCMv5 in");
            Check(typeof(TraxSettings).Assembly.GetTypes().Length > 5, "Core types did not load");
        }

        private static void RedirectPaths()
        {
            SetStatic(typeof(ModPaths), "_configDir", _dir);
            Check(ModPaths.ConfigFilePath == ConfigPath, "ModPaths did not take the temp folder");
        }

        private static void McmAbsent()
        {
            McmBridge.TryRegister("smoke without MCM");
            Check(!McmBridge.IsRegistered, "bridge claims a page without MCM");
            LogHas("[mcm] MCM (Mod Configuration Menu) is not loaded");
            McmBridge.TryRegister("second call");
            Check(Occurrences(LogText, "is not loaded") == 1, "the 'MCM absent' line should be logged once");
        }

        // The config checks below work for ANY default in defaults.json (Anton tunes it): they
        // read the default and pick hand-edit values that differ from it.
        private static readonly ParamDef Pct = SettingsSchema.DamageRandomPercent;
        private static int PctDefault => (int)Pct.Default;
        private static int HandPct => PctDefault == 30 ? 35 : 30;
        private static bool VerboseDefault => SettingsSchema.VerboseLogging.Default != 0;
        private static string OnOff(bool b) => b ? "true" : "false";

        private static void FirstRun()
        {
            ConfigStore.Initialize();
            Check(File.Exists(ConfigPath), "config.json not created");
            string text = File.ReadAllText(ConfigPath, Encoding.UTF8);
            var read = ConfigFile.Read(text);
            Check(read.Ok && read.Values.Count == SettingsSchema.All.Count && read.Issues.Count == 0,
                "created file does not read back cleanly: " + read.Error);
            Check(text.Contains("// Trax Combat Enhancements - settings"), "header missing");
            Check(Occurrences(text, "  // (default ") == SettingsSchema.All.Count, "not every key has its default/range comment");
            LogHas("[config] first run: created config.json");
            LogHas("[config] settings in effect (" + SettingsSchema.All.Count);
            LogHas("[config]   DamageRandomPercent = " + PctDefault);
            LogHas("[config]   VerboseLogging = " + OnOff(VerboseDefault));
        }

        /// <summary>DESIGN 2c: the real DLL takes every default from the defaults.json embedded in
        /// TraxCombat.Core.dll, logs it, and the first-run file carries exactly those values.</summary>
        private static void DefaultsFromTheEmbeddedFile()
        {
            Check(DefaultsFile.Problems.Count == 0, "embedded defaults.json problems: " + string.Join("; ", DefaultsFile.Problems));
            LogHas("[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), " + SettingsSchema.All.Count
                + " keys for " + SettingsSchema.All.Count + " settings - every default read from it");
            var embedded = DefaultsFile.Check(DefaultsFile.ReadEmbeddedText());
            Check(embedded.Ok, "the embedded defaults.json does not check clean: " + embedded.Describe());
            var created = ConfigFile.Read(File.ReadAllText(ConfigPath, Encoding.UTF8));
            foreach (var p in SettingsSchema.All)
            {
                if (!embedded.Values.TryGetValue(p.Key, out double v)) continue;
                Check(Math.Abs(p.Default - v) < 1e-9, p.Key + ": schema default " + p.Format(p.Default) + " vs defaults.json " + p.Format(v));
                Check(created.Values.TryGetValue(p.Key, out double c) && Math.Abs(c - v) < 1e-9, p.Key + ": first-run config.json does not hold the defaults.json value");
            }
        }

        private static void HandEditSurvivesMcmSave()
        {
            // The player edits the file by hand while the game runs...
            File.WriteAllText(ConfigPath, File.ReadAllText(ConfigPath).Replace("\"DamageRandomPercent\": " + PctDefault, "\"DamageRandomPercent\": " + HandPct));
            // ...then flips another setting in MCM and presses Done.
            TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, !VerboseDefault, SettingSources.Mcm);
            LogHas("[config] VerboseLogging: " + OnOff(VerboseDefault) + " → " + OnOff(!VerboseDefault) + " (source: MCM)");
            ConfigStore.SaveAfterMcm();

            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Values["DamageRandomPercent"] == HandPct, "the hand edit was lost by the MCM save");
            Check(disk.Values["VerboseLogging"] == (VerboseDefault ? 0 : 1), "the MCM change was not written");
            Check(TraxSettings.Shared.DamageRandomPercent == PctDefault, "memory took the hand edit before the next battle");
            LogHas("from MCM VerboseLogging; kept hand edit(s) from the file that apply at the next battle start: DamageRandomPercent = " + HandPct + " (now " + PctDefault + ")");
        }

        private static void ReloadAppliesHandEdit()
        {
            ConfigStore.Reload("mission start");
            Check(TraxSettings.Shared.DamageRandomPercent == HandPct, "hand edit not applied at mission start");
            LogHas("[config] DamageRandomPercent: " + PctDefault + " → " + HandPct + " (source: file)");
            LogHas("[config] config.json re-read at mission start: 1 change(s)");
            ConfigStore.Reload("mission start");
            LogHas("[config] config.json re-read at mission start: no changes");
        }

        private static void BrokenFile()
        {
            File.WriteAllText(ConfigPath, "{\r\n  \"HeroCostMultiplier\": 0,75\r\n}\r\n");
            int version = TraxSettings.Shared.Version;
            ConfigStore.Reload("mission start");
            Check(TraxSettings.Shared.Version == version, "a broken file changed values");
            LogHas("[config] could not read config.json at mission start (line");

            TraxSettings.Shared.Set(SettingsSchema.ShowFormationHealth, false, SettingSources.Mcm);
            ConfigStore.SaveAfterMcm();
            Check(Directory.GetFiles(_dir, ConfigFile.FileName + ".broken-*").Length == 1, "broken file not backed up");
            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Ok && disk.Values.Count == SettingsSchema.All.Count, "fresh file after a broken one is not complete");
            Check(disk.Values["ShowFormationHealth"] == 0 && disk.Values["DamageRandomPercent"] == HandPct, "fresh file lost the values in effect");
            LogHas("did not parse");
        }

        private static void MissingAndUnknownKeys()
        {
            int odd = PctDefault == 20 ? 25 : 20; // must differ from the default (McmPageBuilds needs a non-default value)
            File.WriteAllText(ConfigPath, "{ \"damageRandomPercent\": " + odd + ", \"DamageRandomPercnt\": 5, \"ConfigVersion\": 1 }");
            ConfigStore.Reload("game start");
            Check(TraxSettings.Shared.DamageRandomPercent == odd, "value with odd key casing not applied");
            Check(TraxSettings.Shared.ShowFormationHealth == (SettingsSchema.ShowFormationHealth.Default != 0), "missing key did not go back to its default");
            Check(TraxSettings.Shared.VerboseLogging == VerboseDefault, "missing key did not go back to its default (VerboseLogging)");
            LogHas("\"DamageRandomPercnt\" is not a setting of this version");
            LogHas("added " + (SettingsSchema.All.Count - 1) + " missing setting(s)");
            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Values.Count == SettingsSchema.All.Count && disk.Values["DamageRandomPercent"] == odd, "missing keys not written in");
            Check(disk.Unknown.Count == 1 && disk.Unknown[0].Key == "DamageRandomPercnt", "unknown key not carried along");
        }

        private static void VerboseFloodCapped()
        {
            TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
            int before = Occurrences(LogText, "[damage] smoke roll");
            for (int i = 0; i < 5000; i++) TraxLog.Verbose("damage", "smoke roll " + i);
            int written = Occurrences(LogText, "[damage] smoke roll") - before;
            Check(written >= 40 && written < 200, "verbose flood not capped: " + written + " lines");
            LogHas(" ~[damage] smoke roll "); // step 10b: a verbose line carries the mark the trim reads
            TraxLog.FlushSuppressedCounts();
            LogHas("more verbose [damage] lines were suppressed by the rate limit");

            // Review R8 (step 10b): the hot paths ask VerboseWants(bucket) BEFORE building a line. A line
            // never built is counted as suppressed exactly like one built and dropped.
            const int asks = 5000;
            for (int i = 0; i < asks; i++)
                if (TraxLog.VerboseWants("smoke-peek")) TraxLog.Verbose("damage", "smoke peek " + i, "smoke-peek");
            TraxLog.FlushSuppressedCounts();
            string text = LogText;
            var peekLines = text.Split('\n').Where(l => l.Contains("] smoke peek ")).ToList();
            int reported = peekLines.Sum(l => { var m = Regex.Match(l, @"\(\+(\d+) similar lines suppressed\)"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; });
            var drained = Regex.Match(text, @"\[log\] (\d+) more verbose \[smoke-peek\] lines were suppressed");
            int suppressed = reported + (drained.Success ? int.Parse(drained.Groups[1].Value) : 0);
            Check(peekLines.Count >= 40 && peekLines.Count < 200, "VerboseWants did not cap the flood: " + peekLines.Count + " lines");
            Check(peekLines.Count + suppressed == asks, "VerboseWants lost count: " + peekLines.Count + " written + " + suppressed + " suppressed != " + asks);

            TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            TraxLog.Verbose("damage", "must not appear");
            Check(!LogText.Contains("must not appear"), "verbose line written with VerboseLogging off");
            Check(!TraxLog.VerboseWants("smoke-off"), "VerboseWants said yes with VerboseLogging off");
            ConfigStore.Reload("after flood"); // file and memory in step again
        }

        /// <summary>Review 10a R6: TraxLog keeps ONE handle (AutoFlush) instead of an open / append / close
        /// per line. The log must stay usable: readable while held, following a changed path, gone
        /// cleanly after Release (the next line starts a new file), and trimmed past LogMaxMegabytes -
        /// step 10b (R7): only the oldest VERBOSE lines go, every other line stays.</summary>
        private static void LogWriterKeepsTheFileUsable()
        {
            TraxLog.Info("log", "smoke: a line while the handle is held");
            LogHas("smoke: a line while the handle is held"); // read (shared) while TraxLog holds the file

            string sub = Path.Combine(_dir, "log_writer");
            Directory.CreateDirectory(sub);
            string subLog = Path.Combine(sub, ConfigFile.LogFileName);
            SetStatic(typeof(ModPaths), "_configDir", sub);
            try
            {
                TraxLog.Info("log", "smoke: first line in the second folder");
                Check(File.Exists(subLog) && ReadShared(subLog).Contains("smoke: first line in the second folder"), "the log did not follow the new path");
                TraxLog.Release();
                File.Delete(subLog);
                Check(!File.Exists(subLog), "released, the log could not be deleted");
                TraxLog.Info("log", "smoke: after the release");
                string fresh = ReadShared(subLog);
                Check(fresh.Contains("smoke: after the release") && !fresh.Contains("first line in the second folder"), "the next line did not start a new file");

                // Step 10b (R7): at a 1 MB cap, 2.1 MB of verbose lines around the lines the playtest is
                // read from - a summary, a first-time line, an error with its stack.
                TraxSettings.Shared.Set(SettingsSchema.LogMaxMegabytes, 1, SettingSources.File);
                TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
                Check(TraxLog.MaxBytes == 1024 * 1024, "LogMaxMegabytes not read live: " + TraxLog.MaxBytes);
                TraxLog.Info("summary", "smoke: an early summary line");
                TraxLog.Limited("hud", "smoke: an early first-time line", "smoke-first");
                TraxLog.Error("smoke.trim", new InvalidOperationException("smoke: an early error"));
                string filler = new string('x', 1000);
                for (int i = 0; i < 2100; i++) TraxLog.Verbose("damage", "smoke filler " + i + " " + filler, "smoke-filler-" + i); // own buckets: all pass
                long size = new FileInfo(subLog).Length;
                string text = ReadShared(subLog);
                Check(size < TraxLog.MaxBytes && size > TraxLog.MaxBytes / 4, "the log was not trimmed to about half its limit: " + size + " bytes");
                Check(text.Contains("[log] " + LogTrim.NoteStart) && text.Contains("smoke filler 2099 ") && !text.Contains("smoke filler 5 "),
                    "the trim did not keep the newest verbose lines (with its note)");
                Check(text.Contains("[summary] smoke: an early summary line") && text.Contains("[hud] smoke: an early first-time line")
                    && text.Contains("[error] smoke.trim: System.InvalidOperationException: smoke: an early error") && text.Contains("    System.InvalidOperationException: smoke: an early error"),
                    "the trim cut a line that must be kept (a summary, a first-time line, an error and its stack)");
                Check(text.Contains("smoke: after the release"), "the trim cut a non-verbose line");
                Check(text.Split('\n').All(l => l.Length == 0 || l.StartsWith("20", StringComparison.Ordinal) || l.StartsWith("    ", StringComparison.Ordinal)), "the trim left a half line");
            }
            finally
            {
                TraxLog.Release();
                SetStatic(typeof(ModPaths), "_configDir", _dir);
                TraxSettings.Shared.Set(SettingsSchema.LogMaxMegabytes, SettingsSchema.LogMaxMegabytes.Default, SettingSources.File);
                TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, VerboseDefault, SettingSources.File);
                ConfigStore.Reload("after the log trim"); // file and memory in step again
            }
            TraxLog.Info("log", "smoke: back in the main log");
            LogHas("smoke: back in the main log");
        }

        /// <summary>TournamentBehavior calls SetAILevelMultiplier on the TOP stat model (us); the
        /// decorator must read its own private _AILevelMultiplier (compiled accessor against the
        /// real TaleWorlds.MountAndBlade.dll) and push it into every model below it.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TournamentAiLevelFix()
        {
            Check(Models.TraxAgentStatModel.AiLevelFixAvailable, "the _AILevelMultiplier accessor could not be built");
            var below = new Models.TraxAgentStatModel(Array.Empty<TaleWorlds.MountAndBlade.AgentStatCalculateModel>()); // stands in for Sandbox's model
            var top = new Models.TraxAgentStatModel(new TaleWorlds.MountAndBlade.AgentStatCalculateModel[] { below });
            var read = (Func<TaleWorlds.MountAndBlade.AgentStatCalculateModel, float>)GetStatic(typeof(Models.TraxAgentStatModel), "ReadAiLevel")!;
            var sync = typeof(Models.TraxAgentStatModel).GetMethod("SyncAiLevel", BindingFlags.Instance | BindingFlags.NonPublic)!;

            top.SetAILevelMultiplier(1f + 2f / 3f); // what TournamentBehavior does at round 2
            Check(Math.Abs(read(top) - 1.6667f) < 1e-3, "accessor read " + read(top));
            Check(Math.Abs(read(below) - 1f) < 1e-6, "base model changed before the sync");
            sync.Invoke(top, null);
            Check(Math.Abs(read(below) - 1.6667f) < 1e-3, "multiplier not pushed to the base model: " + read(below));
            LogHas("[speed] tournament AI level multiplier 1 → 1.67 passed on to 1 base stat model(s)");

            top.ResetAILevelMultiplier(); // tournament over
            sync.Invoke(top, null);
            Check(Math.Abs(read(below) - 1f) < 1e-6, "reset not pushed to the base model");
        }

        private static int Occurrences(string text, string what)
        {
            int n = 0, at = 0;
            while ((at = text.IndexOf(what, at, StringComparison.Ordinal)) >= 0) { n++; at += what.Length; }
            return n;
        }

        // ------------------------------------------------------------------ phase 2 (MCM present)

        /// <summary>The bridge as a fresh game start finds it, with this enabled-module list (null = unknown).</summary>
        private static void ResetMcmBridge(string[]? modules)
        {
            SetStatic(typeof(McmBridge), "_done", false);
            SetStatic(typeof(McmBridge), "_waiting", false);
            SetStatic(typeof(McmBridge), "_attempts", 0);
            SetStatic(typeof(McmBridge), "_notReady", 0);
            SetStatic(typeof(McmBridge), "_nextTryAt", 0.0);
            McmBridge.UseModuleList(modules);
        }

        /// <summary>Step 12 (Anton's log 21:08): another mod carries an MCMv5 DLL, MCM's own module is
        /// off - one line, no page, no attempt, no retry.</summary>
        private static void McmModuleNotEnabled()
        {
            Assembly.LoadFrom(Path.Combine(_mcmBin, "MCMv5.dll"));
            Check(McmLoaded(), "precondition: MCMv5 not loaded");
            ResetMcmBridge(new[] { "Bannerlord.Harmony", "Bannerlord.ButterLib", "Bannerlord.UIExtenderEx", "Native", "ImmersiveAI.Dev", "TraxCombatEnhancements.Dev" });
            McmBridge.TryRegister("main menu");
            Check(!McmBridge.IsRegistered, "a page with MCM's module off");
            LogHas("[mcm] MCM's module is not enabled - no settings page; config.json only. (An MCM 5.");
            McmBridge.TryRegister("game start");
            for (int i = 0; i < 5; i++)
            {
                SetStatic(typeof(McmBridge), "_nextTryAt", 0.0);
                McmBridge.Tick();
            }
            Check(Occurrences(LogText, "MCM's module is not enabled") == 1, "the 'module not enabled' line should be logged once");
            Check((int)GetStatic(typeof(McmBridge), "_attempts")! == 0, "the bridge tried MCM with its module off");
            Check(Occurrences(LogText, "not ready yet") == 0, "a 'not ready' retry with MCM's module off");
        }

        /// <summary>Step 12: MCM's module on but MCM never ready (its services never built - here: no
        /// main-menu hook of MCM ran) - no attempt before the main menu, the first + MaxRetries retries,
        /// then one line and silence.</summary>
        private static void McmNeverReadyIsCapped()
        {
            ResetMcmBridge(new[] { "Bannerlord.Harmony", McmPlan.ModuleId, "Native", "TraxCombatEnhancements.Dev" });
            McmBridge.Tick();
            Check((int)GetStatic(typeof(McmBridge), "_attempts")! == 0, "the tick tried MCM before the main menu");
            McmBridge.TryRegister("main menu");
            LogHas(" is loaded but not ready yet at main menu - retrying every 1 s, at most " + McmPlan.MaxRetries + " times.");
            for (int i = 0; i < 3 * McmPlan.MaxRetries; i++)
            {
                SetStatic(typeof(McmBridge), "_nextTryAt", 0.0); // a second passed
                McmBridge.Tick();
            }
            int attempts = (int)GetStatic(typeof(McmBridge), "_attempts")!;
            Check(attempts == McmPlan.MaxRetries + 1, "attempts " + attempts + ", expected the first + " + McmPlan.MaxRetries + " retries");
            Check(!McmBridge.IsRegistered, "a page without MCM's services");
            LogHas(" never became ready - gave up after " + (McmPlan.MaxRetries + 1) + " attempts (the first + " + McmPlan.MaxRetries
                   + " retries, one a second) - no settings page; config.json only.");
            McmBridge.TryRegister("game start");
            Check(Occurrences(LogText, "not ready yet") == 1 && Occurrences(LogText, "never became ready") == 1
                  && (int)GetStatic(typeof(McmBridge), "_attempts")! == attempts, "the bridge kept going after it gave up");
        }

        private static void McmPageBuilds()
        {
            // Precondition for the Reset check: a non-default value is live while the page is
            // built (MCM would otherwise bake it into its "default" preset).
            Check(TraxSettings.Shared.DamageRandomPercent != (int)SettingsSchema.DamageRandomPercent.Default, "precondition: expected a non-default value before the build");
            // Step 59: and one on the Advanced page, so its preset is checked the same way.
            var kick = SettingsSchema.CostPerKickOrBash;
            TraxSettings.Shared.Set(kick, kick.Default == 5 ? 6 : 5, "smoke");
            Assembly.LoadFrom(Path.Combine(_mcmBin, "MCMv5.dll"));
            McmHarness.InstallServices();
            ResetMcmBridge(null); // phase 1 and the step-12 checks stood it down; the module list unknown = just try
            McmBridge.TryRegister("smoke with MCM");
            Check(McmBridge.IsRegistered, "page not registered with MCM present");
            Check(GetStatic(typeof(McmBridge), "_advanced") != null, "the Advanced page was not built");
            LogHas("[mcm] settings pages registered at smoke with MCM (attempt 1): MCM 5.");
            LogHas("page \"Trax Combat Enhancements\" (" + SettingsSchema.PlayerPage.Count + " settings in 8 groups; group \"Defaults\": \"Revert all to defaults\") "
                + "and page \"Trax Combat Enhancements - Advanced\" (" + SettingsSchema.OnPage(SettingsPage.Advanced).Count + " settings in 19 groups"); // step 72: + "Orders: no accidental charges"; step 73: + "Line discipline by tier"; step 84: + "Unit changes" on both pages
        }

        private static object PlayerPage => GetStatic(typeof(McmBridge), "_settings")!;

        private static object AdvancedPage => GetStatic(typeof(McmBridge), "_advanced")!;

        private static void McmPageMatchesSchema() => McmHarness.PagesMatchSchema(PlayerPage, AdvancedPage);

        private static void McmSlidersAreLive() => McmHarness.SlidersAreLive(PlayerPage, AdvancedPage, LogHas);

        private static void McmPresetRestoresDefaults() => McmHarness.PresetRestoresDefaults(PlayerPage, AdvancedPage);

        private static void McmButtons() =>
            McmHarness.Buttons(PlayerPage, AdvancedPage, ConfigPath, _dir, LogHas);

        private static void McmDoneWritesFile()
        {
            McmHarness.PressDone(GetStatic(typeof(McmBridge), "_settings")!);
            LogHas("[mcm] Done pressed - writing config.json");
            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            foreach (var p in SettingsSchema.All)
                Check(Math.Abs(disk.Values[p.Key] - TraxSettings.Shared.Get(p)) < 1e-9, "after Done the file differs from memory at " + p.Key);
        }

        /// <summary>Everything that NAMES an MCM type - JIT-compiled only in phase 2.</summary>
        private static class McmHarness
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void InstallServices()
            {
                var gsp = typeof(BUTR.DependencyInjection.GenericServiceProvider);
                gsp.GetField("GlobalServiceProvider", BindingFlags.Static | BindingFlags.NonPublic)!
                    .SetValue(null, new MiniProvider());
            }

            /// <summary>Step 59: both pages - ids and names, every setting on exactly one page (its own), each page's group order and
            /// buttons; then every setting checked on its page.</summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void PagesMatchSchema(object playerObject, object advancedObject)
            {
                var player = (MCM.Abstractions.Base.BaseSettings)playerObject;
                var advanced = (MCM.Abstractions.Base.BaseSettings)advancedObject;
                Check(player.Id == McmBridge.SettingsId && player.DisplayName == "Trax Combat Enhancements", "the player page is " + player.Id + " / " + player.DisplayName);
                Check(advanced.Id == McmBridge.AdvancedSettingsId && advanced.DisplayName == "Trax Combat Enhancements - Advanced",
                    "the Advanced page is " + advanced.Id + " / " + advanced.DisplayName);
                int total = 0;
                foreach (var (settings, page) in new[] { (player, SettingsPage.Player), (advanced, SettingsPage.Advanced) })
                {
                    total += PageMatchesSchema(settings, page);
                    // a setting of the OTHER page never shows here
                    var ids = new HashSet<string>(MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).Select(d => d.Id));
                    foreach (var p in SettingsSchema.All)
                        if (p.Group.Page != page) Check(!ids.Contains(p.Key), p.Key + " is on the " + page + " page too");
                }
                Check(total == SettingsSchema.All.Count, "the two pages hold " + total + " settings, the schema " + SettingsSchema.All.Count);
                // The player page is short (Anton: "a few options for me and the players").
                Check(SettingsSchema.PlayerPage.Count >= 12 && SettingsSchema.PlayerPage.Count <= 29, "the player page has " + SettingsSchema.PlayerPage.Count + " settings");
            }

            /// <summary>One page: returns how many settings it holds.</summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static int PageMatchesSchema(MCM.Abstractions.Base.BaseSettings settings, SettingsPage page)
            {
                Check(settings.FormatType == "none", page + ": format is " + settings.FormatType + ", not none");
                var all = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).ToList();
                var defs = all.Where(d => d.SettingType != MCM.Abstractions.SettingType.Button).ToList();
                var mine = SettingsSchema.OnPage(page);
                Check(defs.Count == mine.Count, page + " page has " + defs.Count + " settings, the schema " + mine.Count);
                // DESIGN 2c: the "Defaults" group - step 59: "Revert all to defaults" on both pages, the export on Advanced only.
                var buttons = all.Where(d => d.SettingType == MCM.Abstractions.SettingType.Button).ToList();
                int expectedButtons = McmBridge.HasExportButton(page) ? 2 : 1;
                Check(buttons.Count == expectedButtons, page + " page has " + buttons.Count + " buttons, expected " + expectedButtons);
                var revert = buttons.FirstOrDefault(b => b.Id == McmBridge.RevertButtonId);
                var export = buttons.FirstOrDefault(b => b.Id == McmBridge.ExportButtonId);
                Check(revert != null && revert.DisplayName == McmBridge.RevertButtonName && revert.Content == "Revert"
                    && revert.GroupName == McmBridge.DefaultsGroupTitle && revert.HintText.Contains("defaults.json")
                    && revert.HintText.Contains(page == SettingsPage.Player ? "Advanced page" : "main page"), page + ": the revert button is missing or wrong");
                if (McmBridge.HasExportButton(page))
                    Check(export != null && export.DisplayName == McmBridge.ExportButtonName && export.Content == "Save"
                        && export.GroupName == McmBridge.DefaultsGroupTitle && export.HintText.Contains("defaults.json"), "the export button is missing or wrong");
                else
                    Check(export == null, "the export button is on the " + page + " page");
                Check(buttons.All(b => b.PropertyReference.Value is Action), page + ": a button does not hand MCM an Action to invoke");
                // Step 10b: the page's group order. MCM's UI (Bannerlord.MBOptionScreen for 1.4.8,
                // UISettingsUtils.SettingsPropertyGroupVMComparer) shows groups by Order ASCENDING. Step 59: the player page -
                // Master switch first, its six groups, Defaults last; the Advanced page - the schema's order, the Defaults buttons,
                // Advanced last.
                var groupOrder = MCM.Abstractions.BaseSettingsExtensions.GetSettingPropertyGroups(settings)
                    .OrderBy(g => g.Order).Select(g => g.GroupName).ToList();
                var expectedGroups = SettingsSchema.GroupsOn(page).Select(g => g.Title).ToList();
                if (page == SettingsPage.Player) expectedGroups.Add(McmBridge.DefaultsGroupTitle);
                else expectedGroups.Insert(expectedGroups.Count - 1, McmBridge.DefaultsGroupTitle);
                Check(groupOrder.SequenceEqual(expectedGroups), page + ": MCM group order is " + string.Join(", ", groupOrder) + " - expected " + string.Join(", ", expectedGroups));
                if (page == SettingsPage.Player)
                    Check(groupOrder[0] == SettingsSchema.MasterGroup.Title, "the player page does not start with the master switch");
                else
                    Check(groupOrder[groupOrder.Count - 1] == SettingsSchema.AdvancedGroup.Title, "Advanced is not last on the Advanced page");
                // the settings inside each group in schema order (MCM sorts a group's properties by their Order)
                foreach (var g in MCM.Abstractions.BaseSettingsExtensions.GetSettingPropertyGroups(settings))
                {
                    var schemaGroup = SettingsSchema.GroupsOn(page).FirstOrDefault(x => x.Title == g.GroupName);
                    if (schemaGroup == null) continue; // the Defaults buttons
                    var shown = g.SettingProperties.OrderBy(sp => sp.Order).Select(sp => sp.Id).ToList();
                    Check(shown.SequenceEqual(SettingsSchema.InGroup(schemaGroup).Select(p => p.Key)), page + ": group " + g.GroupName + " is out of order: " + string.Join(", ", shown));
                }
                foreach (var p in mine)
                {
                    var d = defs.FirstOrDefault(x => x.Id == p.Key);
                    if (d == null) { Failures.Add("MCM " + page + " page lacks " + p.Key); continue; }
                    var expected = p.Type switch
                    {
                        ParamType.Bool => MCM.Abstractions.SettingType.Bool,
                        ParamType.Int => MCM.Abstractions.SettingType.Int,
                        ParamType.Key => MCM.Abstractions.SettingType.String, // step 33: a text box holding the key's name
                        ParamType.Text => MCM.Abstractions.SettingType.String, // step 84: a text box holding the item id
                        _ => MCM.Abstractions.SettingType.Float,
                    };
                    Check(d.SettingType == expected, p.Key + ": MCM type " + d.SettingType);
                    Check(d.DisplayName == p.Label, p.Key + ": label " + d.DisplayName);
                    Check(d.HintText == p.HintText, p.Key + ": hint differs");
                    Check(!d.RequireRestart, p.Key + ": marked require-restart");
                    Check(d.GroupName == p.Group.Title, p.Key + ": group " + d.GroupName);
                    if (p.Type == ParamType.Key || p.Type == ParamType.Text)
                    {
                        Check(d.PropertyReference.Value as string == p.Format(TraxSettings.Shared.Get(p)), p.Key + ": getter does not read the live key name");
                        continue;
                    }
                    Check(Math.Abs(Convert.ToDouble(d.PropertyReference.Value) - TraxSettings.Shared.Get(p)) < 1e-4,
                        p.Key + ": getter does not read the live value");
                    if (p.Type != ParamType.Bool)
                        Check((double)d.MinValue == p.Min && (double)d.MaxValue == p.Max, p.Key + ": range " + d.MinValue + ".." + d.MaxValue);
                }
                return defs.Count;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void SlidersAreLive(object settingsObject, object advancedObject, Action<string> logHas)
            {
                var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                var defs = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).ToDictionary(d => d.Id);

                // Step 59: a slider on the Advanced page reaches the same live settings.
                var advanced = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions((MCM.Abstractions.Base.BaseSettings)advancedObject).ToDictionary(d => d.Id);
                float oldParry = TraxSettings.Shared.CostPerWeaponParry;
                float parry = Math.Abs(oldParry - 4f) < 1e-6 ? 3f : 4f;
                advanced["CostPerWeaponParry"].PropertyReference.Value = parry;
                Check(Math.Abs(TraxSettings.Shared.CostPerWeaponParry - parry) < 1e-6, "an Advanced-page slider did not reach the live settings");
                logHas("[config] CostPerWeaponParry: ");
                advanced["CostPerWeaponParry"].PropertyReference.Value = oldParry;

                int oldPercent = TraxSettings.Shared.DamageRandomPercent;
                int slid = oldPercent == 40 ? 45 : 40;
                defs["DamageRandomPercent"].PropertyReference.Value = slid;        // an int slider
                Check(TraxSettings.Shared.DamageRandomPercent == slid, "int slider did not reach the live settings");
                logHas("[config] DamageRandomPercent: " + oldPercent + " → " + slid + " (source: MCM)");

                defs["HeroCostMultiplier"].PropertyReference.Value = 0.5f;         // a float slider
                Check(Math.Abs(TraxSettings.Shared.HeroCostMultiplier - 0.5f) < 1e-6, "float slider did not reach the live settings");

                defs["ShowPlayerBar"].PropertyReference.Value = false;             // a checkbox
                Check(!TraxSettings.Shared.ShowPlayerBar, "checkbox did not reach the live settings");

                // step 33: the sprint key's text box - a name (loosely typed) sets it at once, an unknown one is refused and logged
                string oldKey = TraxSettings.Shared.SprintKey;
                defs["SprintKey"].PropertyReference.Value = "right alt";
                Check(TraxSettings.Shared.SprintKey == "RightAlt", "the key text box did not reach the live settings (" + TraxSettings.Shared.SprintKey + ")");
                logHas("[config] SprintKey: " + oldKey + " → RightAlt (source: MCM)");
                defs["SprintKey"].PropertyReference.Value = "Banana";
                Check(TraxSettings.Shared.SprintKey == "RightAlt", "an unknown key name changed the key");
                logHas("SprintKey: \"Banana\" is not a key name - kept RightAlt");
                defs["SprintKey"].PropertyReference.Value = oldKey;

                Check(Convert.ToInt32(defs["DamageRandomPercent"].PropertyReference.Value) == slid, "getter does not read back the live value");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void PresetRestoresDefaults(object playerObject, object advancedObject)
            {
                // Step 59: each page's Reset resets that page's settings - the player page first: its own back to the defaults,
                // the Advanced page's non-default value (McmPageBuilds) untouched until the Advanced page's own Reset.
                var kick = SettingsSchema.CostPerKickOrBash;
                Check(Math.Abs(TraxSettings.Shared.Get(kick) - kick.Default) > 1e-6, "precondition: an Advanced setting off its default");
                foreach (var (settingsObject, page) in new[] { (playerObject, SettingsPage.Player), (advancedObject, SettingsPage.Advanced) })
                {
                    var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                    var presets = settings.GetBuiltInPresets().ToList();
                    // MCM.UI's Reset = ChangePreset("default"): the preset with that id, applied onto the page.
                    var reset = presets.SingleOrDefault(p => p.Id == "default");
                    if (reset == null) { Failures.Add(page + ": no \"default\" preset - MCM's Reset buttons would do nothing"); return; }
                    MCM.Abstractions.SettingsUtils.OverrideValues(settings, reset.LoadPreset());
                    foreach (var p in SettingsSchema.OnPage(page))
                        Check(Math.Abs(TraxSettings.Shared.Get(p) - p.Default) < 1e-6, page + " preset left " + p.Key + " at " + p.Format(TraxSettings.Shared.Get(p)));
                    if (page == SettingsPage.Player)
                        Check(Math.Abs(TraxSettings.Shared.Get(kick) - kick.Default) > 1e-6, "the player page's Reset reset an Advanced setting");
                }
                foreach (var p in SettingsSchema.All)
                    Check(Math.Abs(TraxSettings.Shared.Get(p) - p.Default) < 1e-6, "the presets left " + p.Key + " at " + p.Format(TraxSettings.Shared.Get(p)));
            }

            /// <summary>Clicks a button the way MCM.UI does (SettingsPropertyVM.OnValueClick).</summary>
            private static void Click(MCM.Abstractions.Base.BaseSettings settings, string id)
            {
                var def = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).First(d => d.Id == id);
                if (def.PropertyReference.Value is Action action) action();
                else Failures.Add("button " + id + " holds no Action");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Buttons(object settingsObject, object advancedObject, string configPath, string dir, Action<string> logHas)
            {
                var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                var advanced = (MCM.Abstractions.Base.BaseSettings)advancedObject;
                var defs = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).ToDictionary(d => d.Id);
                var advancedDefs = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(advanced).ToDictionary(d => d.Id);
                var s = TraxSettings.Shared;

                // A tuning found in game: two sliders on the player page moved away from the defaults, one on the Advanced page.
                int pct = (int)SettingsSchema.DamageRandomPercent.Default == 35 ? 45 : 35;
                defs["DamageRandomPercent"].PropertyReference.Value = pct;
                defs["ShowPlayerBar"].PropertyReference.Value = SettingsSchema.ShowPlayerBar.Default == 0;
                advancedDefs["DamageRandomOnShields"].PropertyReference.Value = SettingsSchema.DamageRandomOnShields.Default == 0;

                // --- Save current values as a defaults file (the Advanced page's tuning tool - step 59)
                Click(advanced, McmBridge.ExportButtonId);
                string exported = Path.Combine(dir, DefaultsFile.FileName);
                Check(File.Exists(exported), "no defaults.json beside config.json after the export");
                var check = DefaultsFile.Check(File.Exists(exported) ? File.ReadAllText(exported, Encoding.UTF8) : string.Empty);
                Check(check.Ok, "the exported defaults.json does not check clean: " + check.Describe());
                foreach (var p in SettingsSchema.All)
                    Check(check.Values.TryGetValue(p.Key, out double v) && Math.Abs(v - s.Get(p)) < 1e-9, "export: " + p.Key + " is not the value in effect");
                Check(s.DamageRandomPercent == pct, "the export changed a value");
                logHas("[mcm] \"" + McmBridge.ExportButtonName + "\" pressed");
                logHas("[config] saved the current values as a defaults file: " + exported + " - ");
                logHas("DamageRandomPercent " + pct + " (built-in " + SettingsSchema.DamageRandomPercent.Format(SettingsSchema.DamageRandomPercent.Default) + ")");

                // --- Revert all to defaults: beats MCM edits AND a hand edit waiting in config.json
                var disk = ConfigFile.Read(File.ReadAllText(configPath, Encoding.UTF8));
                double hero = SettingsSchema.HeroCostMultiplier.Default == 0.5 ? 0.6 : 0.5;
                File.WriteAllText(configPath, ConfigFile.Write(new Dictionary<string, double>(disk.Values) { ["HeroCostMultiplier"] = hero }), Encoding.UTF8);
                bool refreshed = false, advancedRefreshed = false;
                System.ComponentModel.PropertyChangedEventHandler onChanged = (o, e) => { if (e.PropertyName != "SAVE_TRIGGERED") refreshed = true; };
                System.ComponentModel.PropertyChangedEventHandler onAdvanced = (o, e) => { if (e.PropertyName != "SAVE_TRIGGERED") advancedRefreshed = true; };
                settings.PropertyChanged += onChanged;
                advanced.PropertyChanged += onAdvanced;
                Click(settings, McmBridge.RevertButtonId); // the player page's button reverts BOTH pages
                settings.PropertyChanged -= onChanged;
                advanced.PropertyChanged -= onAdvanced;
                Check(advancedRefreshed, "the Advanced page was not told to re-read its values after the revert");
                Check(Convert.ToBoolean(advancedDefs["DamageRandomOnShields"].PropertyReference.Value) == (SettingsSchema.DamageRandomOnShields.Default != 0),
                    "the Advanced page reads a stale value after the player page's revert");

                foreach (var p in SettingsSchema.All)
                    Check(Math.Abs(s.Get(p) - p.Default) < 1e-9, "revert left " + p.Key + " at " + p.Format(s.Get(p)));
                var after = ConfigFile.Read(File.ReadAllText(configPath, Encoding.UTF8));
                foreach (var p in SettingsSchema.All)
                    Check(after.Values.TryGetValue(p.Key, out double v) && Math.Abs(v - p.Default) < 1e-9, "revert: config.json holds " + p.Key + " = " + p.Format(v));
                Check(refreshed, "the page was not told to re-read its values after the revert");
                Check(Convert.ToInt32(defs["DamageRandomPercent"].PropertyReference.Value) == (int)SettingsSchema.DamageRandomPercent.Default, "the page reads a stale value");
                logHas("[mcm] \"" + McmBridge.RevertButtonName + "\" pressed");
                logHas("[config] DamageRandomPercent: " + pct + " → " + SettingsSchema.DamageRandomPercent.Format(SettingsSchema.DamageRandomPercent.Default) + " (source: defaults)");
                logHas("[config] wrote config.json (reverted to defaults): every value as it is in effect now");
                logHas("[config] reverted all " + SettingsSchema.All.Count + " settings to their defaults (defaults.json (embedded in TraxCombat.Core.dll)): ");

                Click(advanced, McmBridge.RevertButtonId); // the Advanced page's own button: nothing left to change
                logHas("settings to their defaults (defaults.json (embedded in TraxCombat.Core.dll)): 0 changed, applied live");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void PressDone(object settingsObject)
            {
                var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                settings.OnPropertyChanged("SAVE_TRIGGERED"); // what BaseSettingsContainer.SaveSettings raises after Done
            }

            /// <summary>Just enough of MCM's service container: its own (internal) fluent builder
            /// factory, and the fluent property discoverer the menu uses to list a page's settings.
            /// Everything else asked for is "not available" (so Register() is a no-op here).</summary>
            private sealed class MiniProvider : BUTR.DependencyInjection.IGenericServiceProvider
            {
                private static readonly Assembly Mcm = typeof(MCM.Abstractions.FluentBuilder.ISettingsBuilder).Assembly;

                private readonly object _factory = Activator.CreateInstance(
                    Mcm.GetType("MCM.Implementation.FluentBuilder.DefaultSettingsBuilderFactory", true)!, true)!;

                private readonly MCM.Abstractions.Properties.ISettingsPropertyDiscoverer[] _discoverers =
                {
                    (MCM.Abstractions.Properties.ISettingsPropertyDiscoverer)Activator.CreateInstance(
                        Mcm.GetType("MCM.Implementation.FluentSettingsPropertyDiscoverer", true)!, true)!,
                };

                public BUTR.DependencyInjection.IGenericServiceProviderScope CreateScope() => throw new NotSupportedException();

                public TService? GetService<TService>() where TService : class
                {
                    if (typeof(TService) == typeof(MCM.Abstractions.FluentBuilder.ISettingsBuilderFactory)) return (TService)_factory;
                    if (typeof(TService) == typeof(IEnumerable<MCM.Abstractions.Properties.ISettingsPropertyDiscoverer>)) return (TService)(object)_discoverers;
                    return null;
                }

                public void Dispose()
                {
                }
            }
        }
    }
}
