using System;
using System.Collections.Generic;
using System.Threading;

namespace TraxCombat.Core
{
    /// <summary>Who changed a setting - the "(source: …)" of every <c>[config]</c> log line.</summary>
    public static class SettingSources
    {
        /// <summary>The in-game Mod Configuration Menu (a slider, a checkbox, Cancel, a preset).</summary>
        public const string Mcm = "MCM";

        /// <summary>config.json, read at game start, mission start or game load.</summary>
        public const string File = "file";

        /// <summary>Code resetting to the schema's defaults.</summary>
        public const string Defaults = "defaults";
    }

    /// <summary>One changed value, as raised by <see cref="TraxSettings.Changed"/>.</summary>
    public sealed class SettingChange
    {
        public SettingChange(ParamDef param, double oldValue, double newValue, string source, int version, bool clamped)
        {
            Param = param;
            OldValue = oldValue;
            NewValue = newValue;
            Source = source;
            Version = version;
            Clamped = clamped;
        }

        public ParamDef Param { get; }

        public double OldValue { get; }

        public double NewValue { get; }

        public string Source { get; }

        /// <summary><see cref="TraxSettings.Version"/> right after this change.</summary>
        public int Version { get; }

        /// <summary>The requested value was outside the range and was clamped to NewValue.</summary>
        public bool Clamped { get; }

        /// <summary><c>DamageRandomPercent: 50 → 40 (source: MCM)</c> - the exact shape CLAUDE.md asks for.</summary>
        public string ToLogText() =>
            Param.Key + ": " + Param.Format(OldValue) + " → " + Param.Format(NewValue)
            + " (source: " + Source + ")" + (Clamped ? " [clamped into " + Param.Format(Param.Min) + ".." + Param.Format(Param.Max) + "]" : string.Empty);

        public override string ToString() => ToLogText();
    }

    /// <summary>What <see cref="TraxSettings.Set(string,double,string)"/> did.</summary>
    public readonly struct SetResult
    {
        public SetResult(bool known, bool changed, bool clamped, double oldValue, double newValue)
        {
            Known = known;
            Changed = changed;
            Clamped = clamped;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>False: no such key - nothing happened.</summary>
        public bool Known { get; }

        public bool Changed { get; }

        public bool Clamped { get; }

        public double OldValue { get; }

        public double NewValue { get; }
    }

    /// <summary>
    /// THE live settings - one shared object (<see cref="Shared"/>) that every piece of the mod
    /// reads AT USE TIME (the next hit, blow, HUD refresh), never copying a value into its own
    /// field at mission start. That is what makes every setting hot-swappable: MCM's sliders
    /// write here immediately, config.json is re-read here at every mission start.
    ///
    /// Reads are a field load and an array index - cheap enough for the per-hit path - and safe
    /// from any thread (aligned doubles). Writes happen on the main thread (MCM UI, file load).
    /// Anything that must REBUILD on a change (a HUD layer, an agent's cached speed) listens to
    /// <see cref="Changed"/> or compares <see cref="Version"/>.
    /// </summary>
    public sealed class TraxSettings
    {
        /// <summary>The one instance the game uses. Tests build their own.</summary>
        public static TraxSettings Shared { get; } = new TraxSettings();

        private readonly double[] _values;
        private int _version;

        public TraxSettings()
        {
            _values = new double[SettingsSchema.All.Count];
            for (int i = 0; i < _values.Length; i++)
                _values[i] = SettingsSchema.All[i].Default;
        }

        /// <summary>Bumped on every real change (never on a no-op set). Poll it to notice
        /// "something changed since I last looked" without subscribing.</summary>
        public int Version => Volatile.Read(ref _version);

        /// <summary>Raised after every real change, synchronously, on the setter's thread.
        /// A throwing handler is isolated: the value is already set and the other handlers
        /// still run.</summary>
        public event Action<SettingChange>? Changed;

        /// <summary>Raised when a Changed handler throws (the mod logs it as [error]).</summary>
        public event Action<Exception>? HandlerFailed;

        // ------------------------------------------------------------------ generic access

        public double Get(ParamDef p) => _values[p.Index];

        public bool GetBool(ParamDef p) => _values[p.Index] != 0;

        public int GetInt(ParamDef p) => (int)_values[p.Index];

        public float GetFloat(ParamDef p) => (float)_values[p.Index];

        /// <summary>Sets a value by key, clamped and rounded to the setting's type, raising
        /// <see cref="Changed"/> when it really changed. Unknown key → nothing happens,
        /// <see cref="SetResult.Known"/> is false.</summary>
        public SetResult Set(string key, double value, string source)
        {
            if (!SettingsSchema.TryGet(key, out var p))
                return new SetResult(false, false, false, double.NaN, double.NaN);
            return Set(p, value, source);
        }

        public SetResult Set(ParamDef p, bool value, string source) => Set(p, value ? 1.0 : 0.0, source);

        public SetResult Set(ParamDef p, double value, string source)
        {
            double normalized = p.Normalize(value, out bool clamped, out _);
            double old = _values[p.Index];
            if (Math.Abs(old - normalized) < 1e-9)
                return new SetResult(true, false, clamped, old, old);

            _values[p.Index] = normalized;
            int version = Interlocked.Increment(ref _version);
            Raise(new SettingChange(p, old, normalized, source, version, clamped));
            return new SetResult(true, true, clamped, old, normalized);
        }

        /// <summary>Every setting back to its default (each change raised and logged).</summary>
        public void ResetToDefaults(string source)
        {
            foreach (var p in SettingsSchema.All)
                Set(p, p.Default, source);
        }

        /// <summary>A copy of every value by key - for the file writer and the tests.</summary>
        public Dictionary<string, double> Snapshot()
        {
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in SettingsSchema.All)
                map[p.Key] = _values[p.Index];
            return map;
        }

        /// <summary><c>DamageRandomPercent = 30 (default 50)</c> or <c>AthleticsPoolFloor = 50</c> -
        /// the per-setting line of the load dump.</summary>
        public string Describe(ParamDef p)
        {
            double v = _values[p.Index];
            return p.Key + " = " + p.Format(v)
                + (Math.Abs(v - p.Default) < 1e-9 ? string.Empty : " (default " + p.Format(p.Default) + ")");
        }

        private void Raise(SettingChange change)
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (Action<SettingChange> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(change);
                }
                catch (Exception e)
                {
                    try { HandlerFailed?.Invoke(e); } catch { /* the reporter must not break the setter */ }
                }
            }
        }

        // ------------------------------------------------------------------ typed properties
        // One per setting, same name as its key (a test checks the pairing). Read these AT USE
        // TIME - e.g. TraxSettings.Shared.DamageRandomPercent inside the damage hook.

        /// <summary>THE master switch - every feature checks it first (off = vanilla, live).</summary>
        public bool ModEnabled => GetBool(SettingsSchema.ModEnabled);

        public bool DamageRandomEnabled => GetBool(SettingsSchema.DamageRandomEnabled);
        public int DamageRandomPercent => GetInt(SettingsSchema.DamageRandomPercent);
        public bool DamageRandomMelee => GetBool(SettingsSchema.DamageRandomMelee);
        public bool DamageRandomRanged => GetBool(SettingsSchema.DamageRandomRanged);
        public bool DamageRandomOnMounts => GetBool(SettingsSchema.DamageRandomOnMounts);
        public bool DamageRandomOnShields => GetBool(SettingsSchema.DamageRandomOnShields);
        public bool SprintFullCritical => GetBool(SettingsSchema.SprintFullCritical);
        public int DamageReductionPercent => GetInt(SettingsSchema.DamageReductionPercent);

        public bool AthleticsEnabled => GetBool(SettingsSchema.AthleticsEnabled);
        public int AthleticsPoolFloor => GetInt(SettingsSchema.AthleticsPoolFloor);
        public float AthleticsPoolPerSkill => GetFloat(SettingsSchema.AthleticsPoolPerSkill);
        public int AthleticsPeakPercent => GetInt(SettingsSchema.AthleticsPeakPercent);
        public bool HealthCapsAthletics => GetBool(SettingsSchema.HealthCapsAthletics);
        public float CostPerBlow => GetFloat(SettingsSchema.CostPerBlow);
        public float CostPerKickOrBash => GetFloat(SettingsSchema.CostPerKickOrBash);

        /// <summary>Step 22: what the DEFENDER pays for a melee blow he blocks - shield on the right side, on the wrong
        /// side, a weapon parry (points before the multipliers; 0 = free).</summary>
        public float CostPerShieldBlock => GetFloat(SettingsSchema.CostPerShieldBlock);

        public float CostPerWrongSideShieldBlock => GetFloat(SettingsSchema.CostPerWrongSideShieldBlock);

        public float CostPerWeaponParry => GetFloat(SettingsSchema.CostPerWeaponParry);

        /// <summary>Step 29: on = only a wrong-side shield block restarts the refill delay (a clean block / parry still costs).</summary>
        public bool OnlyWrongSideBlocksStopRefill => GetBool(SettingsSchema.OnlyWrongSideBlocksStopRefill);

        /// <summary>Step 64: a shield block from the FRONT arc counts as a right-side block (Core <see cref="ShieldArcMath"/>).</summary>
        public bool ShieldFrontArcIsCorrect => GetBool(SettingsSchema.ShieldFrontArcIsCorrect);

        public int ShieldFrontArcDegrees => GetInt(SettingsSchema.ShieldFrontArcDegrees);
        public bool CostOnMiss => GetBool(SettingsSchema.CostOnMiss);
        public float HeroCostMultiplier => GetFloat(SettingsSchema.HeroCostMultiplier);
        public float PartyLeaderCostMultiplier => GetFloat(SettingsSchema.PartyLeaderCostMultiplier);

        public int ExhaustedAttackSpeedPercent => GetInt(SettingsSchema.ExhaustedAttackSpeedPercent);
        public bool AttackRatePlayerTimer => GetBool(SettingsSchema.AttackRatePlayerTimer);
        public bool AttackRatePaceHold => GetBool(SettingsSchema.AttackRatePaceHold);
        public bool AttackRatePaceByInput => GetBool(SettingsSchema.AttackRatePaceByInput);
        public bool AiHoldRaiseGuard => GetBool(SettingsSchema.AiHoldRaiseGuard);
        public bool AttackRateAiDecisions => GetBool(SettingsSchema.AttackRateAiDecisions);
        public int AttackAnimationMinPercent => GetInt(SettingsSchema.AttackAnimationMinPercent);
        public bool SwingStackEnabled => GetBool(SettingsSchema.SwingStackEnabled);
        public int SwingStackPointsPerSwing => GetInt(SettingsSchema.SwingStackPointsPerSwing);
        public int SwingStackMinimum => GetInt(SettingsSchema.SwingStackMinimum);
        public bool KicksUseSwings => GetBool(SettingsSchema.KicksUseSwings);
        public float MinMoveSpeedMultiplier => GetFloat(SettingsSchema.MinMoveSpeedMultiplier);
        public float MountMinSpeedMultiplier => GetFloat(SettingsSchema.MountMinSpeedMultiplier);
        public bool DamageBonusFollowsAthletics => GetBool(SettingsSchema.DamageBonusFollowsAthletics);

        /// <summary>Step 30: 0-100, the share of a tired AI man's block directions left to chance at empty (x (1 - f)); 0 = off.</summary>
        public int TiredBlockRandomPercent => GetInt(SettingsSchema.TiredBlockRandomPercent);

        public bool StepBackEnabled => GetBool(SettingsSchema.StepBackEnabled);
        public bool StepBackBackpedal => GetBool(SettingsSchema.StepBackBackpedal);
        public int StepBackMaxChancePercent => GetInt(SettingsSchema.StepBackMaxChancePercent);
        public float StepBackDistance => GetFloat(SettingsSchema.StepBackDistance);
        public float StepBackSeconds => GetFloat(SettingsSchema.StepBackSeconds);
        public float StepBackEnemyRange => GetFloat(SettingsSchema.StepBackEnemyRange);
        public bool StepBackHoldAttacks => GetBool(SettingsSchema.StepBackHoldAttacks);
        public int StepBackMaxAtOnce => GetInt(SettingsSchema.StepBackMaxAtOnce);

        // step 21 - battle pace
        public int ShieldInfantrySwingsLessPercent => GetInt(SettingsSchema.ShieldInfantrySwingsLessPercent);
        public int FootMeleeSwingsLessPercent => GetInt(SettingsSchema.FootMeleeSwingsLessPercent);
        public float AiMeleeGapSeconds => GetFloat(SettingsSchema.AiMeleeGapSeconds);
        public float ExtraPauseAfterBowShotSeconds => GetFloat(SettingsSchema.ExtraPauseAfterBowShotSeconds);
        public float ExtraPauseAfterCrossbowShotSeconds => GetFloat(SettingsSchema.ExtraPauseAfterCrossbowShotSeconds);

        // step 41: the shield wall holds its slots
        public bool ShieldWallHoldsSlots => GetBool(SettingsSchema.ShieldWallHoldsSlots);
        public float ShieldWallStepOutMetres => GetFloat(SettingsSchema.ShieldWallStepOutMetres);   // step 42: the leash
        public float ShieldWallPullBackSeconds => GetFloat(SettingsSchema.ShieldWallPullBackSeconds); // step 63: a man out of place walked back
        public bool ShieldWallAdvanceAsOne => GetBool(SettingsSchema.ShieldWallAdvanceAsOne);                 // step 68: the wall pushes as one
        public float ShieldWallAdvanceStepMetres => GetFloat(SettingsSchema.ShieldWallAdvanceStepMetres);
        public float ShieldWallAdvanceNearMetres => GetFloat(SettingsSchema.ShieldWallAdvanceNearMetres);
        public float ShieldWallKickReturnMetres => GetFloat(SettingsSchema.ShieldWallKickReturnMetres);       // step 68: back to the line after a kick
        public bool ShieldWallStandFirm => GetBool(SettingsSchema.ShieldWallStandFirm);                       // step 75: a standing wall never leaves its slot
        public float ShieldWallStandFirmSlackMetres => GetFloat(SettingsSchema.ShieldWallStandFirmSlackMetres);
        public float ShieldWallStandFirmArrivedMetres => GetFloat(SettingsSchema.ShieldWallStandFirmArrivedMetres);
        public float ShieldWallStandFirmStillMetresPerSecond => GetFloat(SettingsSchema.ShieldWallStandFirmStillMetresPerSecond);
        public bool ShieldWallStandFirmPinFrame => GetBool(SettingsSchema.ShieldWallStandFirmPinFrame);
        public bool ShieldWallStandFirmTightTolerance => GetBool(SettingsSchema.ShieldWallStandFirmTightTolerance);

        // step 76: the AI prefers the spear; thrusts pass a friend
        public bool SpearPreferenceEnabled => GetBool(SettingsSchema.SpearPreferenceEnabled);
        public float SpearPolearmFavor => GetFloat(SettingsSchema.SpearPolearmFavor);
        public float SpearSidearmFavor => GetFloat(SettingsSchema.SpearSidearmFavor);
        public float SpearSidearmAfterSeconds => GetFloat(SettingsSchema.SpearSidearmAfterSeconds);
        public float SpearPolearmBackAfterSeconds => GetFloat(SettingsSchema.SpearPolearmBackAfterSeconds);
        public float SpearPolearmBackMountedSeconds => GetFloat(SettingsSchema.SpearPolearmBackMountedSeconds);
        public float SpearRidingFastMetresPerSecond => GetFloat(SettingsSchema.SpearRidingFastMetresPerSecond);
        public bool SpearCloseFromLength => GetBool(SettingsSchema.SpearCloseFromLength);
        public float SpearCloseBaseMetres => GetFloat(SettingsSchema.SpearCloseBaseMetres);
        public int SpearCloseLengthPercent => GetInt(SettingsSchema.SpearCloseLengthPercent);
        public float SpearCloseMinMetres => GetFloat(SettingsSchema.SpearCloseMinMetres);
        public float SpearCloseMaxMetres => GetFloat(SettingsSchema.SpearCloseMaxMetres);
        public float SpearCloseFallbackMetres => GetFloat(SettingsSchema.SpearCloseFallbackMetres);
        public bool SpearBackRankKeeps => GetBool(SettingsSchema.SpearBackRankKeeps);
        public bool SpearBackRankInStandingLine => GetBool(SettingsSchema.SpearBackRankInStandingLine);
        public bool SpearKeepVsPolearm => GetBool(SettingsSchema.SpearKeepVsPolearm);
        public float SpearLookSeconds => GetFloat(SettingsSchema.SpearLookSeconds);
        public bool SpearWieldOverride => GetBool(SettingsSchema.SpearWieldOverride);
        public bool ThrustsPassFriends => GetBool(SettingsSchema.ThrustsPassFriends);
        public bool ThrustsPassPolearmsOnly => GetBool(SettingsSchema.ThrustsPassPolearmsOnly);
        public float FriendlyHitStunSeconds => GetFloat(SettingsSchema.FriendlyHitStunSeconds);

        // step 23: brace by orders
        public bool BraceEnabled => GetBool(SettingsSchema.BraceEnabled);
        public int BraceFloorChargePercent => GetInt(SettingsSchema.BraceFloorChargePercent);
        public int BraceFloorAdvancePercent => GetInt(SettingsSchema.BraceFloorAdvancePercent);
        public int BraceFloorHoldPercent => GetInt(SettingsSchema.BraceFloorHoldPercent);
        public int BraceFloorNoFormationPercent => GetInt(SettingsSchema.BraceFloorNoFormationPercent);   // step 28
        public int BraceFloorSpreadPercent => GetInt(SettingsSchema.BraceFloorSpreadPercent);             // step 28
        public int BraceRecoverPercent => GetInt(SettingsSchema.BraceRecoverPercent);
        public int BraceRecoverSpreadPercent => GetInt(SettingsSchema.BraceRecoverSpreadPercent);
        public bool BraceWieldShield => GetBool(SettingsSchema.BraceWieldShield);
        public bool BraceRaiseShield => GetBool(SettingsSchema.BraceRaiseShield);

        // step 33: the sprint
        public bool SprintEnabled => GetBool(SettingsSchema.SprintEnabled);
        /// <summary>The key's NAME ("LeftControl" - KeyNames); <see cref="SprintKeyIndex"/> is its index.</summary>
        public string SprintKey => KeyNames.Name(Get(SettingsSchema.SprintKey));
        public int SprintKeyIndex => GetInt(SettingsSchema.SprintKey);
        public float SprintPointsPerSecond => GetFloat(SettingsSchema.SprintPointsPerSecond);
        public float SprintMinSeconds => GetFloat(SettingsSchema.SprintMinSeconds);
        public int SprintMinStartPercent => GetInt(SettingsSchema.SprintMinStartPercent);
        public float SprintMaxMultiplier => GetFloat(SettingsSchema.SprintMaxMultiplier);
        public float SprintMinMultiplier => GetFloat(SettingsSchema.SprintMinMultiplier);
        public int SprintFullSpeedSharePercent => GetInt(SettingsSchema.SprintFullSpeedSharePercent);
        public float SprintTopSpeedReachSeconds => GetFloat(SettingsSchema.SprintTopSpeedReachSeconds);
        public float SprintRefillSeconds => GetFloat(SettingsSchema.SprintRefillSeconds);
        public float SprintRefillMovingFactor => GetFloat(SettingsSchema.SprintRefillMovingFactor);
        public float SprintRefillExhaustedFactor => GetFloat(SettingsSchema.SprintRefillExhaustedFactor);
        public bool SprintAiEnabled => GetBool(SettingsSchema.SprintAiEnabled);
        public int SprintAiChargeKeepPercent => GetInt(SettingsSchema.SprintAiChargeKeepPercent);
        public int SprintAiAdvanceKeepPercent => GetInt(SettingsSchema.SprintAiAdvanceKeepPercent);
        public int SprintAiMoveKeepPercent => GetInt(SettingsSchema.SprintAiMoveKeepPercent);
        public bool SprintOnRout => GetBool(SettingsSchema.SprintOnRout);
        // step 64: the charge sprint runs into contact; a small store waits for the enemy
        public bool SprintChargeIntoContact => GetBool(SettingsSchema.SprintChargeIntoContact);
        public float SprintChargeWaitBelowSeconds => GetFloat(SettingsSchema.SprintChargeWaitBelowSeconds);
        public float SprintChargeWaitMetres => GetFloat(SettingsSchema.SprintChargeWaitMetres);
        // step 43: aggression and fever
        public int AiAggressionFreshPercent => GetInt(SettingsSchema.AiAggressionFreshPercent);
        public int AiAggressionExhaustedPercent => GetInt(SettingsSchema.AiAggressionExhaustedPercent);
        public bool FeverEnabled => GetBool(SettingsSchema.FeverEnabled);
        public float FeverPointsPerSecond => GetFloat(SettingsSchema.FeverPointsPerSecond);
        public int FeverAggressionPercent => GetInt(SettingsSchema.FeverAggressionPercent);
        public float FeverContactMetres => GetFloat(SettingsSchema.FeverContactMetres);
        public float FeverShieldDurationMultiplier => GetFloat(SettingsSchema.FeverShieldDurationMultiplier);
        public bool FeverSprintToClose => GetBool(SettingsSchema.FeverSprintToClose);
        public float FeverSprintMinMetres => GetFloat(SettingsSchema.FeverSprintMinMetres);
        public float FeverSprintMaxMetres => GetFloat(SettingsSchema.FeverSprintMaxMetres);
        public float FeverSprintCheckSeconds => GetFloat(SettingsSchema.FeverSprintCheckSeconds);
        public int FeverSprintChancePercent => GetInt(SettingsSchema.FeverSprintChancePercent);
        // step 49: courage and cohesion
        public bool CourageEnabled => GetBool(SettingsSchema.CourageEnabled);
        public float CourageRadiusMetres => GetFloat(SettingsSchema.CourageRadiusMetres);
        public int CourageBaseOddsPercent => GetInt(SettingsSchema.CourageBaseOddsPercent);
        public int CourageArmourWeightPercent => GetInt(SettingsSchema.CourageArmourWeightPercent);
        public int CourageLevelWeightPercent => GetInt(SettingsSchema.CourageLevelWeightPercent);
        public int CourageHealthWeightPercent => GetInt(SettingsSchema.CourageHealthWeightPercent);
        public int CourageHeavyWeaponPercent => GetInt(SettingsSchema.CourageHeavyWeaponPercent);
        public int CourageTiredWeightPercent => GetInt(SettingsSchema.CourageTiredWeightPercent);
        public int CourageMoraleWeightPercent => GetInt(SettingsSchema.CourageMoraleWeightPercent);
        public float CourageShockSeconds => GetFloat(SettingsSchema.CourageShockSeconds);
        public int CourageShockWeightPercent => GetInt(SettingsSchema.CourageShockWeightPercent);
        public int CourageFeverPercent => GetInt(SettingsSchema.CourageFeverPercent);
        public int CourageHeroPercent => GetInt(SettingsSchema.CourageHeroPercent);
        public int CourageHysteresisPercent => GetInt(SettingsSchema.CourageHysteresisPercent);
        public float CourageMinTimidSeconds => GetFloat(SettingsSchema.CourageMinTimidSeconds);
        public int CourageSurgeChancePercent => GetInt(SettingsSchema.CourageSurgeChancePercent); // step 49b
        public float CourageSurgeSeconds => GetFloat(SettingsSchema.CourageSurgeSeconds);
        public float CourageRegroupRadiusMetres => GetFloat(SettingsSchema.CourageRegroupRadiusMetres);
        public int CourageRegroupMates => GetInt(SettingsSchema.CourageRegroupMates);
        public float CourageFlockRadiusMetres => GetFloat(SettingsSchema.CourageFlockRadiusMetres);
        public int CourageFlockMinMen => GetInt(SettingsSchema.CourageFlockMinMen);
        public bool CourageSprintBack => GetBool(SettingsSchema.CourageSprintBack);
        public float CourageSprintMetres => GetFloat(SettingsSchema.CourageSprintMetres);
        public int CourageSprintEnemies => GetInt(SettingsSchema.CourageSprintEnemies);
        public int CourageSprintKeepPercent => GetInt(SettingsSchema.CourageSprintKeepPercent);
        public int CouragePanicOddsPercent => GetInt(SettingsSchema.CouragePanicOddsPercent);
        public int CouragePanicSprintKeepPercent => GetInt(SettingsSchema.CouragePanicSprintKeepPercent);
        public bool CourageTightenWeights => GetBool(SettingsSchema.CourageTightenWeights);
        public float CourageTimidMeleeMetres => GetFloat(SettingsSchema.CourageTimidMeleeMetres);
        public bool FearFlockEnabled => GetBool(SettingsSchema.FearFlockEnabled); // step 67: the fear flock
        public int FearMoraleCalm => GetInt(SettingsSchema.FearMoraleCalm);
        public int FearMoraleBroken => GetInt(SettingsSchema.FearMoraleBroken);
        public int FearFreshPercent => GetInt(SettingsSchema.FearFreshPercent);
        public int FearMoraleWeightPercent => GetInt(SettingsSchema.FearMoraleWeightPercent);
        public int FearTiredWeightPercent => GetInt(SettingsSchema.FearTiredWeightPercent);
        public int DisciplineLevelLow => GetInt(SettingsSchema.DisciplineLevelLow);
        public int DisciplineLevelHigh => GetInt(SettingsSchema.DisciplineLevelHigh);
        public int DisciplineMaxPercent => GetInt(SettingsSchema.DisciplineMaxPercent);
        public int FearUnitSharePercent => GetInt(SettingsSchema.FearUnitSharePercent);
        public int FearThinLinePercent => GetInt(SettingsSchema.FearThinLinePercent);
        public int FearStartPercent => GetInt(SettingsSchema.FearStartPercent);
        public int FearFullPercent => GetInt(SettingsSchema.FearFullPercent);
        public float FearShelterBackMetres => GetFloat(SettingsSchema.FearShelterBackMetres);
        public float FearShelterMaxMetres => GetFloat(SettingsSchema.FearShelterMaxMetres);
        public float FearDriftMetresPerMinute => GetFloat(SettingsSchema.FearDriftMetresPerMinute);
        public int FearDriftHoldPercent => GetInt(SettingsSchema.FearDriftHoldPercent);
        public float FearCoverWidthMetres => GetFloat(SettingsSchema.FearCoverWidthMetres);
        public int FearAnchorFreshPercent => GetInt(SettingsSchema.FearAnchorFreshPercent);
        public float FearAnchorSeconds => GetFloat(SettingsSchema.FearAnchorSeconds);
        public float FearSprintMetres => GetFloat(SettingsSchema.FearSprintMetres);
        public int FearSprintChancePercent => GetInt(SettingsSchema.FearSprintChancePercent);
        public int FearSprintKeepPercent => GetInt(SettingsSchema.FearSprintKeepPercent);
        public int FearWallBreakMorale => GetInt(SettingsSchema.FearWallBreakMorale);
        public float FearChargeLooseMetres => GetFloat(SettingsSchema.FearChargeLooseMetres);
        public bool FearWinCalms => GetBool(SettingsSchema.FearWinCalms); // step 69: winners don't hang back
        public int FearWinEvenOddsPercent => GetInt(SettingsSchema.FearWinEvenOddsPercent);
        public int FearWinClearOddsPercent => GetInt(SettingsSchema.FearWinClearOddsPercent);
        public int FearWinShakenPercent => GetInt(SettingsSchema.FearWinShakenPercent);
        public float FearThreatMetres => GetFloat(SettingsSchema.FearThreatMetres);
        public int FearMopUpWinPercent => GetInt(SettingsSchema.FearMopUpWinPercent);
        public bool FearBandEnabled => GetBool(SettingsSchema.FearBandEnabled); // step 70: the band, the strike, the lone runner
        public int FearBandRanksMin => GetInt(SettingsSchema.FearBandRanksMin);
        public int FearBandRanksMax => GetInt(SettingsSchema.FearBandRanksMax);
        public float FearBandFileMetres => GetFloat(SettingsSchema.FearBandFileMetres);
        public float FearBandRankMetres => GetFloat(SettingsSchema.FearBandRankMetres);
        public float FearBandMaxWidthMetres => GetFloat(SettingsSchema.FearBandMaxWidthMetres);
        public float FearBandGapMetres => GetFloat(SettingsSchema.FearBandGapMetres);
        public float FearStrikeReachMetres => GetFloat(SettingsSchema.FearStrikeReachMetres);
        public float FearStrikeLeashMetres => GetFloat(SettingsSchema.FearStrikeLeashMetres);
        public bool LoneRunnerEnabled => GetBool(SettingsSchema.LoneRunnerEnabled);
        public float LoneRunnerAheadMetres => GetFloat(SettingsSchema.LoneRunnerAheadMetres);

        // step 77: press, don't pass
        public bool PressDontPass => GetBool(SettingsSchema.PressDontPass);
        public float PressToleranceMetres => GetFloat(SettingsSchema.PressToleranceMetres);
        public float PressWatchMetres => GetFloat(SettingsSchema.PressWatchMetres);
        public float PressGapMetres => GetFloat(SettingsSchema.PressGapMetres);
        public float PressManWidthMetres => GetFloat(SettingsSchema.PressManWidthMetres);
        public float PressFrontDepthMetres => GetFloat(SettingsSchema.PressFrontDepthMetres);
        public float PressWrapMarginMetres => GetFloat(SettingsSchema.PressWrapMarginMetres);
        public int PressWrapMates => GetInt(SettingsSchema.PressWrapMates);
        public float PressWrapMatesMetres => GetFloat(SettingsSchema.PressWrapMatesMetres);
        public float PressRoundEndMetres => GetFloat(SettingsSchema.PressRoundEndMetres);
        public float PressWinPercent => GetFloat(SettingsSchema.PressWinPercent);
        public int PressLineMinMen => GetInt(SettingsSchema.PressLineMinMen);
        public float PressLineInSlotPercent => GetFloat(SettingsSchema.PressLineInSlotPercent);
        public float PressBandMarginMetres => GetFloat(SettingsSchema.PressBandMarginMetres);
        public float PressBlockEndMetres => GetFloat(SettingsSchema.PressBlockEndMetres);
        public float PressEndSlackMetres => GetFloat(SettingsSchema.PressEndSlackMetres);
        public bool PressWrapAllowed => GetBool(SettingsSchema.PressWrapAllowed);
        public float PressWingBendDegrees => GetFloat(SettingsSchema.PressWingBendDegrees);
        // step 78: the battle's rhythm - the rotation; step 78c: each man's rest
        public bool RotationEnabled => GetBool(SettingsSchema.RotationEnabled);
        public int RotationTiredPercent => GetInt(SettingsSchema.RotationTiredPercent);
        public bool RotationUseBraceFloor => GetBool(SettingsSchema.RotationUseBraceFloor);
        public int RotationFresherPercent => GetInt(SettingsSchema.RotationFresherPercent);
        public float RotationBehindMetres => GetFloat(SettingsSchema.RotationBehindMetres);
        public float RotationFileMetres => GetFloat(SettingsSchema.RotationFileMetres);
        public float RotationContactMetres => GetFloat(SettingsSchema.RotationContactMetres);
        public float RotationBesideMetres => GetFloat(SettingsSchema.RotationBesideMetres);
        public float RotationApproachSeconds => GetFloat(SettingsSchema.RotationApproachSeconds);
        public float RotationMaxSeconds => GetFloat(SettingsSchema.RotationMaxSeconds);
        public float RotationCooldownSeconds => GetFloat(SettingsSchema.RotationCooldownSeconds);
        public float RotationRestSeconds => GetFloat(SettingsSchema.RotationRestSeconds);
        public bool RestEnabled => GetBool(SettingsSchema.RestEnabled);
        public int RestTiredMinPercent => GetInt(SettingsSchema.RestTiredMinPercent);
        public int RestTiredMaxPercent => GetInt(SettingsSchema.RestTiredMaxPercent);
        public int RestReadyChargeMinPercent => GetInt(SettingsSchema.RestReadyChargeMinPercent);
        public int RestReadyChargeMaxPercent => GetInt(SettingsSchema.RestReadyChargeMaxPercent);
        public int RestReadyAdvanceMinPercent => GetInt(SettingsSchema.RestReadyAdvanceMinPercent);
        public int RestReadyAdvanceMaxPercent => GetInt(SettingsSchema.RestReadyAdvanceMaxPercent);
        public int RestReadyHoldMinPercent => GetInt(SettingsSchema.RestReadyHoldMinPercent);
        public int RestReadyHoldMaxPercent => GetInt(SettingsSchema.RestReadyHoldMaxPercent);
        public float RestReachMetres => GetFloat(SettingsSchema.RestReachMetres);
        public float RestMaxBackMetres => GetFloat(SettingsSchema.RestMaxBackMetres);
        public float RestDefendMetres => GetFloat(SettingsSchema.RestDefendMetres);
        public float RestContagionMetres => GetFloat(SettingsSchema.RestContagionMetres);
        public int RestContagionMaxPercent => GetInt(SettingsSchema.RestContagionMaxPercent);
        public int RestContagionFloorPoints => GetInt(SettingsSchema.RestContagionFloorPoints);
        public float RestGapDiagnosticMetres => GetFloat(SettingsSchema.RestGapDiagnosticMetres);
        // step 79: war cries
        public bool WarCriesEnabled => GetBool(SettingsSchema.WarCriesEnabled);
        public int WarCryBackInPercent => GetInt(SettingsSchema.WarCryBackInPercent);
        public int WarCryAnswerPercent => GetInt(SettingsSchema.WarCryAnswerPercent);
        public float WarCryAnswerDelaySeconds => GetFloat(SettingsSchema.WarCryAnswerDelaySeconds);
        public int WarCryChargeSharePercent => GetInt(SettingsSchema.WarCryChargeSharePercent);
        public int WarCryChargeMaxMen => GetInt(SettingsSchema.WarCryChargeMaxMen);
        public float WarCryChargeStaggerSeconds => GetFloat(SettingsSchema.WarCryChargeStaggerSeconds);
        public float WarCryChargeUnitCooldownSeconds => GetFloat(SettingsSchema.WarCryChargeUnitCooldownSeconds);
        public float WarCryManCooldownSeconds => GetFloat(SettingsSchema.WarCryManCooldownSeconds);
        public int WarCryUnitPerSecond => GetInt(SettingsSchema.WarCryUnitPerSecond);
        public int WarCryBattlePerSecond => GetInt(SettingsSchema.WarCryBattlePerSecond);
        // step 55: cavalry flocking
        public bool CavalryFlockEnabled => GetBool(SettingsSchema.CavalryFlockEnabled);
        public int CavalryFlockFreeEagerPercent => GetInt(SettingsSchema.CavalryFlockFreeEagerPercent);
        public int CavalryFlockHysteresisPercent => GetInt(SettingsSchema.CavalryFlockHysteresisPercent);
        public int CavalryFlockMidEagerPercent => GetInt(SettingsSchema.CavalryFlockMidEagerPercent);
        public int CavalryFlockMidGroupSize => GetInt(SettingsSchema.CavalryFlockMidGroupSize);
        public int CavalryFlockExhaustedSharePercent => GetInt(SettingsSchema.CavalryFlockExhaustedSharePercent);
        public bool CavalryFlockTimid => GetBool(SettingsSchema.CavalryFlockTimid);
        public float CavalryFlockTimidRadiusMetres => GetFloat(SettingsSchema.CavalryFlockTimidRadiusMetres);
        public float CavalryFlockGatherRadiusMetres => GetFloat(SettingsSchema.CavalryFlockGatherRadiusMetres);
        public float CavalryFlockSeparationMetres => GetFloat(SettingsSchema.CavalryFlockSeparationMetres);
        public float CavalryFlockLeadSeconds => GetFloat(SettingsSchema.CavalryFlockLeadSeconds);
        public bool CavalryFlockTightenWeights => GetBool(SettingsSchema.CavalryFlockTightenWeights);
        public float CavalryFlockWaveSeconds => GetFloat(SettingsSchema.CavalryFlockWaveSeconds);
        public float CavalryFlockMaxSeconds => GetFloat(SettingsSchema.CavalryFlockMaxSeconds);
        // step 48: the impact model (docs/IMPACT.md §5; Core ImpactRules reads them all at once)
        public bool ImpactEnabled => GetBool(SettingsSchema.ImpactEnabled);
        public bool ImpactKicksEnabled => GetBool(SettingsSchema.ImpactKicksEnabled);
        public bool ImpactBlocksEnabled => GetBool(SettingsSchema.ImpactBlocksEnabled);
        public bool ImpactHitsEnabled => GetBool(SettingsSchema.ImpactHitsEnabled);
        public bool ImpactRiposteEnabled => GetBool(SettingsSchema.ImpactRiposteEnabled);
        public bool ImpactShadowMode => GetBool(SettingsSchema.ImpactShadowMode);
        public int KickShieldDropMaxPercent => GetInt(SettingsSchema.KickShieldDropMaxPercent);
        public int KickKnockdownMaxPercent => GetInt(SettingsSchema.KickKnockdownMaxPercent);
        public int RightBlockStaggerMaxPercent => GetInt(SettingsSchema.RightBlockStaggerMaxPercent);
        public int RightBlockShieldDropMaxPercent => GetInt(SettingsSchema.RightBlockShieldDropMaxPercent);
        public int RightBlockKnockdownMaxPercent => GetInt(SettingsSchema.RightBlockKnockdownMaxPercent);
        public int ParryStaggerMaxPercent => GetInt(SettingsSchema.ParryStaggerMaxPercent);
        public int ParryKnockdownMaxPercent => GetInt(SettingsSchema.ParryKnockdownMaxPercent);
        public int WrongBlockStaggerMaxPercent => GetInt(SettingsSchema.WrongBlockStaggerMaxPercent);
        public int WrongBlockShieldDropMaxPercent => GetInt(SettingsSchema.WrongBlockShieldDropMaxPercent);
        public int WrongBlockKnockdownMaxPercent => GetInt(SettingsSchema.WrongBlockKnockdownMaxPercent);
        public int HitStaggerMaxPercent => GetInt(SettingsSchema.HitStaggerMaxPercent);
        public int HitKnockdownMaxPercent => GetInt(SettingsSchema.HitKnockdownMaxPercent);
        public int RiposteStaggerMaxPercent => GetInt(SettingsSchema.RiposteStaggerMaxPercent);
        public int CorrectBlockTiredWeightPercent => GetInt(SettingsSchema.CorrectBlockTiredWeightPercent);
        public int RiposteBlockerTiredPercent => GetInt(SettingsSchema.RiposteBlockerTiredPercent);
        public float StaggerBaseSeconds => GetFloat(SettingsSchema.StaggerBaseSeconds);
        public int StaggerTiredExtraPercent => GetInt(SettingsSchema.StaggerTiredExtraPercent);
        public float StaggerMaxSeconds => GetFloat(SettingsSchema.StaggerMaxSeconds);
        public int KickReelTiredExtraPercent => GetInt(SettingsSchema.KickReelTiredExtraPercent);
        public float GetUpMaxSlowdown => GetFloat(SettingsSchema.GetUpMaxSlowdown);
        public int BlockStunTiredExtraPercent => GetInt(SettingsSchema.BlockStunTiredExtraPercent);
        public float RiposteStunMaxSeconds => GetFloat(SettingsSchema.RiposteStunMaxSeconds);
        public int ImpactRandomPercent => GetInt(SettingsSchema.ImpactRandomPercent);
        public float ImpactStunCapSeconds => GetFloat(SettingsSchema.ImpactStunCapSeconds);
        public float ImpactStaggerImmunitySeconds => GetFloat(SettingsSchema.ImpactStaggerImmunitySeconds);
        public float ImpactKnockdownImmunitySeconds => GetFloat(SettingsSchema.ImpactKnockdownImmunitySeconds);
        public float ImpactDiminishWindowSeconds => GetFloat(SettingsSchema.ImpactDiminishWindowSeconds);
        public float ImpactDiminishFactor => GetFloat(SettingsSchema.ImpactDiminishFactor);
        public float ShieldPickupLockSeconds => GetFloat(SettingsSchema.ShieldPickupLockSeconds);
        public int ImpactOnPlayerPercent => GetInt(SettingsSchema.ImpactOnPlayerPercent);
        public int ImpactRiderDismountPercent => GetInt(SettingsSchema.ImpactRiderDismountPercent);
        public bool ImpactTiredNeverShrugsOff => GetBool(SettingsSchema.ImpactTiredNeverShrugsOff);
        public bool ImpactMomentumEnabled => GetBool(SettingsSchema.ImpactMomentumEnabled);
        public int ImpactMomentumPercent => GetInt(SettingsSchema.ImpactMomentumPercent);
        public float ImpactMomentumWalkMetresPerSecond => GetFloat(SettingsSchema.ImpactMomentumWalkMetresPerSecond);
        public float ImpactMomentumSprintMetresPerSecond => GetFloat(SettingsSchema.ImpactMomentumSprintMetresPerSecond);
        public int ImpactMomentumRiderPercent => GetInt(SettingsSchema.ImpactMomentumRiderPercent);
        public float ImpactMomentumRiderWalkMetresPerSecond => GetFloat(SettingsSchema.ImpactMomentumRiderWalkMetresPerSecond);
        public float ImpactMomentumRiderGallopMetresPerSecond => GetFloat(SettingsSchema.ImpactMomentumRiderGallopMetresPerSecond);
        public bool ImpactShowMessages => GetBool(SettingsSchema.ImpactShowMessages);
        public bool ImpactMessagesIncludeYourMen => GetBool(SettingsSchema.ImpactMessagesIncludeYourMen);
        public float ImpactMessageMinSeconds => GetFloat(SettingsSchema.ImpactMessageMinSeconds);
        public bool ImpactPunishEnabled => GetBool(SettingsSchema.ImpactPunishEnabled);
        public int ImpactPunishAggressionPercent => GetInt(SettingsSchema.ImpactPunishAggressionPercent);
        public float ImpactPunishGraceSeconds => GetFloat(SettingsSchema.ImpactPunishGraceSeconds);
        public float ImpactPunishRangeMetres => GetFloat(SettingsSchema.ImpactPunishRangeMetres);
        public bool ImpactShrugOffYieldsToImpact => GetBool(SettingsSchema.ImpactShrugOffYieldsToImpact);
        public float ImpactFallBackupSeconds => GetFloat(SettingsSchema.ImpactFallBackupSeconds);
        public float ImpactToughnessWeight => GetFloat(SettingsSchema.ImpactToughnessWeight);
        public int ImpactRangedInHandPercent => GetInt(SettingsSchema.ImpactRangedInHandPercent);
        public int ImpactRearPercent => GetInt(SettingsSchema.ImpactRearPercent);
        public int ImpactFlankPercent => GetInt(SettingsSchema.ImpactFlankPercent);
        public int ImpactRearDegrees => GetInt(SettingsSchema.ImpactRearDegrees);
        public int ImpactFlankDegrees => GetInt(SettingsSchema.ImpactFlankDegrees);
        public float ImpactRearPressure => GetFloat(SettingsSchema.ImpactRearPressure);
        public float ImpactFlankPressure => GetFloat(SettingsSchema.ImpactFlankPressure);
        public float ImpactChargeCarrySeconds => GetFloat(SettingsSchema.ImpactChargeCarrySeconds);
        public int RiposteKnockdownMaxPercent => GetInt(SettingsSchema.RiposteKnockdownMaxPercent);
        public float RiposteKnockdownFrom => GetFloat(SettingsSchema.RiposteKnockdownFrom);
        public int RiposteMomentumPercent => GetInt(SettingsSchema.RiposteMomentumPercent);
        public int ImpactMultiplierCapPercent => GetInt(SettingsSchema.ImpactMultiplierCapPercent);

        // step 65: the AI kicks and bashes, the kicked man braces
        public bool AiKicksEnabled => GetBool(SettingsSchema.AiKicksEnabled);
        public int AiShieldmanKickPercent => GetInt(SettingsSchema.AiShieldmanKickPercent); // step 81
        public int AiShieldmanBashPercent => GetInt(SettingsSchema.AiShieldmanBashPercent); // step 81
        public bool ImpactMeetRulesEnabled => GetBool(SettingsSchema.ImpactMeetRulesEnabled); // step 82
        public int MeetBashShieldRightStaggerPercent => GetInt(SettingsSchema.MeetBashShieldRightStaggerPercent); // step 82
        public int MeetBashShieldRightFallPercent => GetInt(SettingsSchema.MeetBashShieldRightFallPercent); // step 82
        public int MeetBashWeaponRightStaggerPercent => GetInt(SettingsSchema.MeetBashWeaponRightStaggerPercent); // step 82
        public int MeetBashWeaponRightFallPercent => GetInt(SettingsSchema.MeetBashWeaponRightFallPercent); // step 82
        public int MeetBashShieldWrongStaggerPercent => GetInt(SettingsSchema.MeetBashShieldWrongStaggerPercent); // step 82
        public int MeetBashShieldWrongFallPercent => GetInt(SettingsSchema.MeetBashShieldWrongFallPercent); // step 82
        public int MeetBashShieldWrongShieldDropPercent => GetInt(SettingsSchema.MeetBashShieldWrongShieldDropPercent); // step 82
        public int MeetBashWeaponWrongStaggerPercent => GetInt(SettingsSchema.MeetBashWeaponWrongStaggerPercent); // step 82
        public int MeetBashWeaponWrongFallPercent => GetInt(SettingsSchema.MeetBashWeaponWrongFallPercent); // step 82
        public int MeetBashOpenStaggerPercent => GetInt(SettingsSchema.MeetBashOpenStaggerPercent); // step 82
        public int MeetBashOpenFallPercent => GetInt(SettingsSchema.MeetBashOpenFallPercent); // step 82
        public int MeetBashOpenShieldDropPercent => GetInt(SettingsSchema.MeetBashOpenShieldDropPercent); // step 82
        public int MeetKickShieldDownStaggerPercent => GetInt(SettingsSchema.MeetKickShieldDownStaggerPercent); // step 82
        public int MeetKickShieldDownFallPercent => GetInt(SettingsSchema.MeetKickShieldDownFallPercent); // step 82
        public int MeetKickWeaponDownStaggerPercent => GetInt(SettingsSchema.MeetKickWeaponDownStaggerPercent); // step 82
        public int MeetKickWeaponDownFallPercent => GetInt(SettingsSchema.MeetKickWeaponDownFallPercent); // step 82
        public int MeetAiReadScalePercent => GetInt(SettingsSchema.MeetAiReadScalePercent); // step 82
        public float MeetAiReadReactionSeconds => GetFloat(SettingsSchema.MeetAiReadReactionSeconds); // step 82
        public float MeetAiReadMaxHoldSeconds => GetFloat(SettingsSchema.MeetAiReadMaxHoldSeconds); // step 82
        public int AiNoShieldKickPercent => GetInt(SettingsSchema.AiNoShieldKickPercent); // step 81
        public float AiKickReachMetres => GetFloat(SettingsSchema.AiKickReachMetres);
        public int AiKickFacingDegrees => GetInt(SettingsSchema.AiKickFacingDegrees);
        public float AiKickCooldownSeconds => GetFloat(SettingsSchema.AiKickCooldownSeconds);
        public bool AiKickKicksARaisedShield => GetBool(SettingsSchema.AiKickKicksARaisedShield);
        public bool KickBraceEnabled => GetBool(SettingsSchema.KickBraceEnabled);
        public float KickBraceFromPressure => GetFloat(SettingsSchema.KickBraceFromPressure);
        public float KickBraceToPressure => GetFloat(SettingsSchema.KickBraceToPressure);
        public int KickBraceMinBarPercent => GetInt(SettingsSchema.KickBraceMinBarPercent);
        public int KickBraceReelPercent => GetInt(SettingsSchema.KickBraceReelPercent);
        public int KickShortReelPercent => GetInt(SettingsSchema.KickShortReelPercent);
        public bool KickBraceRefusesKnockBack => GetBool(SettingsSchema.KickBraceRefusesKnockBack);

        // steps 68 / 80: the strike path (step 66's wait for reach and bash credit removed by step 81)
        public float AiKickFriendClearMetres => GetFloat(SettingsSchema.AiKickFriendClearMetres); // step 68: no kick through a friend; step 80: the kick's strike path half-width
        public float AiBashPathHalfWidthMetres => GetFloat(SettingsSchema.AiBashPathHalfWidthMetres); // step 80
        public float AiKickRearRankMetres => GetFloat(SettingsSchema.AiKickRearRankMetres); // step 80
        public float ImpactMismatchWeight => GetFloat(SettingsSchema.ImpactMismatchWeight);
        public float ImpactEvenBandDoublings => GetFloat(SettingsSchema.ImpactEvenBandDoublings);
        public float ImpactBodyExponent => GetFloat(SettingsSchema.ImpactBodyExponent);
        public int ImpactBodyFloorPoints => GetInt(SettingsSchema.ImpactBodyFloorPoints);
        public float ImpactStaggeredBonus => GetFloat(SettingsSchema.ImpactStaggeredBonus);
        public float ImpactDamageWeight => GetFloat(SettingsSchema.ImpactDamageWeight);
        public float ImpactRefWeightKg => GetFloat(SettingsSchema.ImpactRefWeightKg);
        public float ImpactWeightExponent => GetFloat(SettingsSchema.ImpactWeightExponent);
        public float ImpactRefSwingSpeed => GetFloat(SettingsSchema.ImpactRefSwingSpeed);
        public float ImpactSpeedExponent => GetFloat(SettingsSchema.ImpactSpeedExponent);
        public float ImpactBluntFactor => GetFloat(SettingsSchema.ImpactBluntFactor);
        public float ImpactCutFactor => GetFloat(SettingsSchema.ImpactCutFactor);
        public float ImpactPierceFactor => GetFloat(SettingsSchema.ImpactPierceFactor);
        public float ImpactKickFactor => GetFloat(SettingsSchema.ImpactKickFactor);
        public float ImpactBashFactor => GetFloat(SettingsSchema.ImpactBashFactor);
        public float ImpactSkillMinFactor => GetFloat(SettingsSchema.ImpactSkillMinFactor);
        public float ImpactSkillMaxFactor => GetFloat(SettingsSchema.ImpactSkillMaxFactor);
        public float ImpactGuardShieldRight => GetFloat(SettingsSchema.ImpactGuardShieldRight);
        public float ImpactGuardParry => GetFloat(SettingsSchema.ImpactGuardParry);
        public float ImpactGuardShieldWrong => GetFloat(SettingsSchema.ImpactGuardShieldWrong);
        public float ImpactGuardNone => GetFloat(SettingsSchema.ImpactGuardNone);
        public float ImpactStaggerFrom => GetFloat(SettingsSchema.ImpactStaggerFrom);
        public float ImpactShieldDropFrom => GetFloat(SettingsSchema.ImpactShieldDropFrom);
        public float ImpactKnockdownFrom => GetFloat(SettingsSchema.ImpactKnockdownFrom);

        public float RegenDelayBlowTimes => GetFloat(SettingsSchema.RegenDelayBlowTimes);
        public float BlowTimeSeconds => GetFloat(SettingsSchema.BlowTimeSeconds);
        public float FullRegenSecondsStanding => GetFloat(SettingsSchema.FullRegenSecondsStanding);
        public int RegenRateNearFullPercent => GetInt(SettingsSchema.RegenRateNearFullPercent);
        public float RegenMultiplierAtFullRun => GetFloat(SettingsSchema.RegenMultiplierAtFullRun);
        public float WalkEffortFraction => GetFloat(SettingsSchema.WalkEffortFraction);
        public bool HideoutBossFightRefill => GetBool(SettingsSchema.HideoutBossFightRefill);

        public bool ShowPlayerBar => GetBool(SettingsSchema.ShowPlayerBar);
        public bool ShowPlayerBarOutsideBattles => GetBool(SettingsSchema.ShowPlayerBarOutsideBattles);
        public bool ShowAttackRecoveryBar => GetBool(SettingsSchema.ShowAttackRecoveryBar);
        public bool ShowSprintBar => GetBool(SettingsSchema.ShowSprintBar); // step 33
        public bool FlashBarOnEarlyAttack => GetBool(SettingsSchema.FlashBarOnEarlyAttack);
        public int BarYellowBelowPercent => GetInt(SettingsSchema.BarYellowBelowPercent);
        public int BarOrangeBelowPercent => GetInt(SettingsSchema.BarOrangeBelowPercent);
        public int BarRedBelowPercent => GetInt(SettingsSchema.BarRedBelowPercent);

        public bool ShowFormationSpread => GetBool(SettingsSchema.ShowFormationSpread);
        public float FormationSpreadStdDevs => GetFloat(SettingsSchema.FormationSpreadStdDevs);
        public bool ShowInOrderMenu => GetBool(SettingsSchema.ShowInOrderMenu);
        public bool ShowFormationHealth => GetBool(SettingsSchema.ShowFormationHealth);
        public bool ShowReadyCount => GetBool(SettingsSchema.ShowReadyCount);
        public int ReadyYellowBelowPercent => GetInt(SettingsSchema.ReadyYellowBelowPercent);
        public int ReadyRedBelowPercent => GetInt(SettingsSchema.ReadyRedBelowPercent);
        public bool OrderStripUnderCards => GetBool(SettingsSchema.OrderStripUnderCards);
        public bool ShowAltMarkerStats => GetBool(SettingsSchema.ShowAltMarkerStats);
        public bool AltMarkersShowEnemy => GetBool(SettingsSchema.AltMarkersShowEnemy);
        public bool ShowFeverMarks => GetBool(SettingsSchema.ShowFeverMarks); // step 44
        public bool FeverMarksShowEnemy => GetBool(SettingsSchema.FeverMarksShowEnemy);
        public bool ShowAthleticsMarks => GetBool(SettingsSchema.ShowAthleticsMarks); // step 46
        public bool AthleticsMarksShowEnemy => GetBool(SettingsSchema.AthleticsMarksShowEnemy);
        public bool ShowFeverOnLabels => GetBool(SettingsSchema.ShowFeverOnLabels); // step 58
        public bool AltMarksEveryone => GetBool(SettingsSchema.AltMarksEveryone);
        public int AltNearMarksMax => GetInt(SettingsSchema.AltNearMarksMax);
        public float AltNearMarksMetres => GetFloat(SettingsSchema.AltNearMarksMetres);
        public int FeverMarkMax => GetInt(SettingsSchema.FeverMarkMax);
        public bool ShowMoraleMarks => GetBool(SettingsSchema.ShowMoraleMarks); // step 67: morale eyes
        public bool ShowMoraleOnLabels => GetBool(SettingsSchema.ShowMoraleOnLabels);
        public float MoraleLabelMetres => GetFloat(SettingsSchema.MoraleLabelMetres);
        public int MoraleLabelMax => GetInt(SettingsSchema.MoraleLabelMax);

        // step 72: no accidental charge orders
        public bool OrderNoAccidentalCharge => GetBool(SettingsSchema.OrderNoAccidentalCharge);
        public int OrderChargeDragPixels => GetInt(SettingsSchema.OrderChargeDragPixels);
        public int OrderChargeIconPixels => GetInt(SettingsSchema.OrderChargeIconPixels);

        // step 73: line discipline by tier
        public bool LineDisciplineForPlayer => GetBool(SettingsSchema.LineDisciplineForPlayer);
        public bool LineDisciplineEnabled => GetBool(SettingsSchema.LineDisciplineEnabled);
        public float LineDisciplineRanksT0 => GetFloat(SettingsSchema.LineDisciplineRanksT0);
        public float LineDisciplineRanksT1 => GetFloat(SettingsSchema.LineDisciplineRanksT1);
        public float LineDisciplineRanksT2 => GetFloat(SettingsSchema.LineDisciplineRanksT2);
        public float LineDisciplineRanksT3 => GetFloat(SettingsSchema.LineDisciplineRanksT3);
        public float LineDisciplineRanksT4 => GetFloat(SettingsSchema.LineDisciplineRanksT4);
        public float LineDisciplineRanksT5 => GetFloat(SettingsSchema.LineDisciplineRanksT5);
        public float LineDisciplineRanksT6 => GetFloat(SettingsSchema.LineDisciplineRanksT6);
        public int LineDisciplineHeroTier => GetInt(SettingsSchema.LineDisciplineHeroTier);
        public bool LineDisciplineRangedToo => GetBool(SettingsSchema.LineDisciplineRangedToo);
        public bool LineDisciplineInSieges => GetBool(SettingsSchema.LineDisciplineInSieges);
        public bool LineDisciplinePreviewCap => GetBool(SettingsSchema.LineDisciplinePreviewCap);
        public bool LineDisciplineShowMessage => GetBool(SettingsSchema.LineDisciplineShowMessage);
        public float LineDisciplinePollSeconds => GetFloat(SettingsSchema.LineDisciplinePollSeconds);
        public bool LineBlockHoldEnabled => GetBool(SettingsSchema.LineBlockHoldEnabled);
        public float LineBlockSlackMetres => GetFloat(SettingsSchema.LineBlockSlackMetres);
        public float LineBlockFullAtDepth => GetFloat(SettingsSchema.LineBlockFullAtDepth);
        public float LineBlockLooseMetres => GetFloat(SettingsSchema.LineBlockLooseMetres);
        public float LineBlockRankMetres => GetFloat(SettingsSchema.LineBlockRankMetres);
        public float LineBlockAheadMetres => GetFloat(SettingsSchema.LineBlockAheadMetres);
        public float LineBlockStrikeReachMetres => GetFloat(SettingsSchema.LineBlockStrikeReachMetres);
        public float LineBlockStrikeLeashMetres => GetFloat(SettingsSchema.LineBlockStrikeLeashMetres);
        public float LineBlockNearMetres => GetFloat(SettingsSchema.LineBlockNearMetres);
        public float LineBlockLeadMetres => GetFloat(SettingsSchema.LineBlockLeadMetres);
        public float LineBlockMopUpWinPercent => GetFloat(SettingsSchema.LineBlockMopUpWinPercent);

        public float HudRefreshSeconds => GetFloat(SettingsSchema.HudRefreshSeconds);
        public int PlayerBarWidth => GetInt(SettingsSchema.PlayerBarWidth);
        public int PlayerBarHeight => GetInt(SettingsSchema.PlayerBarHeight);
        public int PlayerBarOffsetRight => GetInt(SettingsSchema.PlayerBarOffsetRight);
        public int PlayerBarOffsetBottom => GetInt(SettingsSchema.PlayerBarOffsetBottom);
        public int RecoveryBarWidth => GetInt(SettingsSchema.RecoveryBarWidth);
        public int RecoveryBarHeight => GetInt(SettingsSchema.RecoveryBarHeight);
        public int RecoveryBarOffsetAbove => GetInt(SettingsSchema.RecoveryBarOffsetAbove);
        public int SprintBarOffsetAbove => GetInt(SettingsSchema.SprintBarOffsetAbove); // step 33
        public int OrderStripTextSize => GetInt(SettingsSchema.OrderStripTextSize);
        public int OrderStripTextOffset => GetInt(SettingsSchema.OrderStripTextOffset);
        public int OrderStripBarOffset => GetInt(SettingsSchema.OrderStripBarOffset);
        public int OrderStripBarHeight => GetInt(SettingsSchema.OrderStripBarHeight);
        public int OrderStripReadyOffset => GetInt(SettingsSchema.OrderStripReadyOffset);
        public int OrderStripSideMargin => GetInt(SettingsSchema.OrderStripSideMargin);
        public int OrderPanelOffsetTop => GetInt(SettingsSchema.OrderPanelOffsetTop);
        public int OrderPanelWidth => GetInt(SettingsSchema.OrderPanelWidth);
        public int AltMarkerTextSize => GetInt(SettingsSchema.AltMarkerTextSize);
        public int AltMarkerOffset => GetInt(SettingsSchema.AltMarkerOffset);
        public int AltMarkerBarWidth => GetInt(SettingsSchema.AltMarkerBarWidth);
        public int AltMarkerBarHeight => GetInt(SettingsSchema.AltMarkerBarHeight);
        public int FeverMarkWidth => GetInt(SettingsSchema.FeverMarkWidth); // step 44
        public int FeverMarkHeight => GetInt(SettingsSchema.FeverMarkHeight);
        public float FeverMarkHeightMetres => GetFloat(SettingsSchema.FeverMarkHeightMetres);
        public int AthleticsMarkWidth => GetInt(SettingsSchema.AthleticsMarkWidth); // step 46
        public int AthleticsMarkHeight => GetInt(SettingsSchema.AthleticsMarkHeight);
        public int AthleticsMarkGap => GetInt(SettingsSchema.AthleticsMarkGap);
        public float FormationStatsRefreshSeconds => GetFloat(SettingsSchema.FormationStatsRefreshSeconds);
        public bool ImpactSpike => GetBool(SettingsSchema.ImpactSpike); // step 47
        public bool VerboseLogging => GetBool(SettingsSchema.VerboseLogging);
        public int LogMaxMegabytes => GetInt(SettingsSchema.LogMaxMegabytes);
    }
}
