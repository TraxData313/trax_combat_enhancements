using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Which boss fight the player chose in a hideout (step 19).</summary>
    public enum BossFightKind
    {
        /// <summary>The objective's name was neither known text (a mod, a new game version) - the refill does
        /// not need it: who refills comes from the teams.</summary>
        Unknown = 0,

        /// <summary>"Very well." - you against the boss alone; both sides' men stand aside.</summary>
        Duel = 1,

        /// <summary>"I don't fight duels with brigands." - everyone fights.</summary>
        Battle = 2,
    }

    /// <summary>Where a tracked fighter stands when the boss fight begins (step 19).</summary>
    public enum HideoutSide
    {
        /// <summary>Not on the field any more (no engine call on him).</summary>
        Gone = 0,

        /// <summary>The player's side (the player's team or an allied one) - refilled.</summary>
        Player = 1,

        /// <summary>The boss's side (a real team that is not the player's) - spawned fresh in the intro.</summary>
        Boss = 2,

        /// <summary>No side (the duel's onlookers, both sides' men, are moved to Team.Invalid) - never refilled.</summary>
        Aside = 3,
    }

    /// <summary>
    /// Step 19 (Anton, 2026-09-28): the hideout boss fight is a fresh start for the player's side. The game's
    /// facts the module watches (verified in the v1.4.8 decompile - AI_NOTES "Step 19") and the pure rules.
    /// </summary>
    public static class HideoutBossFightMath
    {
        /// <summary>The <c>UniqueId</c> of SandBox's DefeatHideoutBossObjective - started on the mission's
        /// MissionObjectiveLogic at the boss fight's first moment, for the duel and the battle alike, in both
        /// hideout missions (the classic and the stealth "ambush" one).</summary>
        public const string BossObjectiveId = "hideout_mission_defeat_hideout_boss_objective";

        /// <summary>The text id in the duel objective's name, "{=QEynMlwL}Win the Duel" (TextObject.Value - the
        /// raw string, the same in every language).</summary>
        public const string DuelTextId = "QEynMlwL";

        /// <summary>The text id in the battle objective's name, "{=0sPTRh6L}Win the Fight".</summary>
        public const string BattleTextId = "0sPTRh6L";

        /// <summary>The hideout missions' controllers, by type NAME (the module does not reference SandBox).</summary>
        public static readonly IReadOnlyList<string> ControllerNames = new[] { "HideoutMissionController", "HideoutAmbushMissionController" };

        /// <summary>This objective is the boss fight's.</summary>
        public static bool IsBossObjective(string? uniqueId) => string.Equals(uniqueId, BossObjectiveId, StringComparison.Ordinal);

        /// <summary>Duel or battle from the objective's raw name (<c>TextObject.Value</c>): the text ids decide;
        /// the English words are the fallback; neither → <see cref="BossFightKind.Unknown"/>.</summary>
        public static BossFightKind KindOf(string? nameValue)
        {
            if (string.IsNullOrEmpty(nameValue)) return BossFightKind.Unknown;
            string v = nameValue!;
            if (v.IndexOf("{=" + DuelTextId + "}", StringComparison.Ordinal) >= 0) return BossFightKind.Duel;
            if (v.IndexOf("{=" + BattleTextId + "}", StringComparison.Ordinal) >= 0) return BossFightKind.Battle;
            if (v.IndexOf("Duel", StringComparison.OrdinalIgnoreCase) >= 0) return BossFightKind.Duel;
            if (v.IndexOf("Fight", StringComparison.OrdinalIgnoreCase) >= 0) return BossFightKind.Battle;
            return BossFightKind.Unknown;
        }

        /// <summary>A behaviour of this type name runs a hideout mission.</summary>
        public static bool IsHideoutController(string? typeName)
        {
            if (typeName == null) return false;
            foreach (var n in ControllerNames)
                if (string.Equals(n, typeName, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The word in the log: duel, battle, or "duel or battle" when the name did not say.</summary>
        public static string KindWord(BossFightKind kind) => kind switch
        {
            BossFightKind.Duel => "duel",
            BossFightKind.Battle => "battle",
            _ => "duel or battle",
        };

        /// <summary>The gate, master switch FIRST: which switch keeps the refill off (null = it runs). ModEnabled,
        /// then AthleticsEnabled (everyone reads full anyway), then HideoutBossFightRefill.</summary>
        public static string? RefillOffBecause(bool modEnabled, bool athleticsEnabled, bool hideoutBossFightRefill) =>
            !modEnabled ? "ModEnabled" : !athleticsEnabled ? "AthleticsEnabled" : !hideoutBossFightRefill ? "HideoutBossFightRefill" : null;

        /// <summary>The gate from the live settings.</summary>
        public static string? RefillOffBecause(TraxSettings s) => RefillOffBecause(s.ModEnabled, s.AthleticsEnabled, s.HideoutBossFightRefill);
    }

    /// <summary>
    /// One mission's hideout boss phase (step 19): the intro, the fight's start, the refill of the player's
    /// side, what it released and how fresh the boss's side was - and the <c>[athletics]</c> line and the
    /// <c>[summary]</c> lines built from it. Filled by the module on the main thread; pure.
    /// </summary>
    public sealed class HideoutBossFightStats
    {
        /// <summary>The hideout controller seen at the first tick (null: not a hideout mission).</summary>
        public string? Controller { get; set; }

        /// <summary>Mission time the boss intro began (the mode turned CutScene) - NaN = not seen.</summary>
        public double IntroAt { get; private set; } = double.NaN;

        public bool IntroSeen => !double.IsNaN(IntroAt);

        /// <summary>Mission time the boss fight began (the objective appeared) - NaN = not seen.</summary>
        public double FightAt { get; private set; } = double.NaN;

        public bool FightSeen => !double.IsNaN(FightAt);

        public BossFightKind Kind { get; private set; }

        /// <summary>The switch that kept the refill off (ModEnabled, AthleticsEnabled, HideoutBossFightRefill); null = it ran.</summary>
        public string? OffBecause { get; private set; }

        /// <summary>The refill threw here - nothing was refilled (null = no error).</summary>
        public string? FailedAt { get; private set; }

        /// <summary>The boss objective came back after the fight began (vanilla never does): ignored, counted.</summary>
        public int SeenAgain { get; set; }

        // ---- the player's side
        public int PlayerSide { get; private set; }
        public int Refilled { get; private set; }
        public int AlreadyFull { get; private set; }
        public int Capped { get; private set; }
        public int WereEmpty { get; private set; }
        public double PointsGiven { get; private set; }
        public bool YouOnField { get; private set; }
        public double YouBefore { get; private set; }
        public double YouAfter { get; private set; }
        public double YouPool { get; private set; }
        public bool YouCapped { get; private set; }

        // ---- what the fresh start released on the player's side
        public int StepBacksReleased { get; set; }
        public int StepBacksDropped { get; set; }
        public int PausesReleased { get; set; }
        public int PausesDropped { get; set; }
        public bool YourPauseReleased { get; set; }

        // ---- the others on the field
        public int BossSide { get; private set; }
        public int BossSideFull { get; private set; }
        public double BossSideLowest { get; private set; } = 1.0;
        public int Aside { get; private set; }
        public int Gone { get; private set; }

        /// <summary>The intro began (the first time counts).</summary>
        public void NoteIntro(double now)
        {
            if (!IntroSeen) IntroAt = now;
        }

        /// <summary>The fight began: once per mission. False = it had begun already (counted in <see cref="SeenAgain"/>).</summary>
        public bool BeginFight(double now, BossFightKind kind)
        {
            if (FightSeen)
            {
                SeenAgain++;
                return false;
            }
            FightAt = now;
            Kind = kind;
            return true;
        }

        /// <summary>The refill did not run: this switch was off.</summary>
        public void NotRefilled(string offBecause) => OffBecause = offBecause;

        /// <summary>The refill threw before anything was refilled.</summary>
        public void Failed(string site) => FailedAt = site;

        /// <summary>A fighter of the player's side before the refill decision (counted whether or not it runs).</summary>
        public void AddPlayerSide(bool isYou, double fraction, double pool)
        {
            PlayerSide++;
            if (!isYou) return;
            YouOnField = true;
            YouBefore = fraction * pool;
            YouAfter = YouBefore;
            YouPool = pool;
        }

        /// <summary>One refill done (<see cref="AthleticsMath.FreshStart"/>).</summary>
        public void AddRefill(in FreshStartOutcome o, bool isYou)
        {
            if (!o.Refilled) return;
            if (o.AlreadyAtTop) AlreadyFull++;
            else Refilled++;
            if (o.Capped) Capped++;
            if (o.WasExhausted) WereEmpty++;
            if (o.GainedPoints > 0) PointsGiven += o.GainedPoints;
            if (!isYou) return;
            YouBefore = o.BeforePoints;
            YouAfter = o.AfterPoints;
            YouPool = o.Pool;
            YouCapped = o.Capped;
        }

        /// <summary>A fighter of the boss's side: his fill against the top he can refill to (fresh = at it).</summary>
        public void AddBossSide(double fraction, double top)
        {
            BossSide++;
            if (fraction >= top - AthleticsMath.Epsilon) BossSideFull++;
            if (fraction < BossSideLowest) BossSideLowest = fraction;
        }

        public void AddAside() => Aside++;

        public void AddGone() => Gone++;

        /// <summary>The boss's side was all at full when the fight began (none = vacuously).</summary>
        public bool BossSideFresh => BossSideFull == BossSide;

        /// <summary>
        /// The always-on <c>[athletics]</c> line at the fight's start:
        /// "hideout boss fight (duel): refilled N of the player's side (you X → Y of P) at T s - …".
        /// </summary>
        public string FightLine()
        {
            string kind = HideoutBossFightMath.KindWord(Kind);
            var sb = new StringBuilder("hideout boss fight (").Append(kind).Append(')');
            if (FailedAt != null)
            {
                sb.Append(" at ").Append(S1(FightAt)).Append(" s: the refill FAILED (").Append(FailedAt)
                  .Append(") - nothing refilled, the fight goes on as it was (tell Claude)");
                return sb.ToString();
            }
            if (OffBecause != null)
            {
                sb.Append(" at ").Append(S1(FightAt)).Append(" s: nobody refilled - ").Append(OffWhy(OffBecause))
                  .Append(" (the player's side: ").Append(PlayerSide).Append(", ").Append(YouText(refilled: false)).Append(')');
                AppendOthers(sb);
                return sb.ToString();
            }
            sb.Append(": refilled ").Append(Refilled).Append(" of the player's side (").Append(YouText()).Append(") at ").Append(S1(FightAt)).Append(" s - ")
              .Append(Kind == BossFightKind.Duel ? "in a duel only you: your men stand aside" : "you and your men still standing")
              .Append("; ").Append(PlayerSide).Append(" on the player's side, ").Append(AlreadyFull).Append(" already full, ")
              .Append(Capped).Append(" held below full by their wounds (to the health they have left), ").Append(WereEmpty).Append(" were empty; +")
              .Append(F1(PointsGiven)).Append(" Athletics in all; released: your attack pause ").Append(YourPauseReleased ? "yes" : "no")
              .Append(", AI pauses ").Append(PausesReleased).Append(" (+").Append(PausesDropped).Append(" queued), step backs ")
              .Append(StepBacksReleased).Append(" (+").Append(StepBacksDropped).Append(" queued)");
            AppendOthers(sb);
            return sb.ToString();
        }

        /// <summary>
        /// The <c>[summary]</c> lines: none outside a hideout (unless a boss fight was seen anyway); else the
        /// boss phase in one line - including, plainly, a boss intro whose fight's start was never seen.
        /// </summary>
        public List<string> SummaryLines()
        {
            var lines = new List<string>();
            if (Controller == null && !FightSeen) return lines;
            var sb = new StringBuilder("hideout boss phase (").Append(Controller ?? "no hideout controller seen").Append("): ");
            if (!FightSeen)
            {
                if (!IntroSeen)
                    sb.Append("none this mission - the boss intro never played (the camp was not cleared: left, lost, or no boss phase)");
                else
                    sb.Append("the boss intro played at ").Append(S1(IntroAt)).Append(" s, but the start of the boss fight was NEVER SEEN - ")
                      .Append("if you fought the boss (duel or battle), the refill hook never fired: tell Claude (if you left during the talk with the boss, nothing is wrong)");
                lines.Add(sb.ToString());
                return lines;
            }
            sb.Append(IntroSeen ? "the boss intro at " + S1(IntroAt) + " s, " : "(the intro was not seen - the refill does not need it) ")
              .Append("the fight began at ").Append(S1(FightAt)).Append(" s as ")
              .Append(Kind == BossFightKind.Duel ? "a DUEL" : Kind == BossFightKind.Battle ? "a BATTLE (men to men)" : "a duel or battle (the objective's name did not say)")
              .Append(" - ");
            if (FailedAt != null)
                sb.Append("the refill FAILED (").Append(FailedAt).Append("): nothing refilled - tell Claude");
            else if (OffBecause != null)
                sb.Append("nobody refilled: ").Append(OffWhy(OffBecause)).Append(" (").Append(YouText(refilled: false)).Append(')');
            else
                sb.Append("a fresh start: ").Append(Refilled).Append(" of the player's side refilled (").Append(PlayerSide).Append(" on it, ")
                  .Append(AlreadyFull).Append(" already full, ").Append(Capped).Append(" held by wounds; ").Append(YouText()).Append(')');
            if (FailedAt == null) sb.Append("; the boss's side: ").Append(BossSideText()); // a failed read measured nobody
            if (SeenAgain > 0) sb.Append("; the boss objective came back ").Append(SeenAgain).Append("x (ignored - one fresh start per mission)");
            lines.Add(sb.ToString());
            return lines;
        }

        private void AppendOthers(StringBuilder sb)
        {
            sb.Append("; the boss's side: ").Append(BossSideText());
            if (Aside > 0) sb.Append("; standing aside: ").Append(Aside).Append(" (not refilled)");
        }

        private string BossSideText()
        {
            if (BossSide == 0) return "nobody tracked";
            string n = BossSide + (BossSide == 1 ? " fighter" : " fighters");
            return BossSideFresh
                ? n + ", all fresh (at full - spawned for this fight)"
                : n + ", " + BossSideFull + " at full, lowest " + P0(BossSideLowest) + " - NOT all fresh (tell Claude)";
        }

        private string YouText(bool refilled = true)
        {
            if (!YouOnField) return "you: not on the field";
            if (!refilled) return "you " + F1(YouBefore) + " of " + F0(YouPool);
            return "you " + F1(YouBefore) + " → " + F1(YouAfter) + " of " + F0(YouPool) + (YouCapped ? ", your wounds cap it" : string.Empty);
        }

        private static string OffWhy(string offBecause) => offBecause switch
        {
            "ModEnabled" => "the whole mod is off (ModEnabled)",
            "AthleticsEnabled" => "Athletics is off (AthleticsEnabled) - everyone reads full anyway",
            _ => "HideoutBossFightRefill is off",
        };

        private static string S1(double v) => double.IsNaN(v) ? "?" : v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string P0(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
