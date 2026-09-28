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

        public bool AthleticsEnabled => GetBool(SettingsSchema.AthleticsEnabled);
        public int AthleticsPoolFloor => GetInt(SettingsSchema.AthleticsPoolFloor);
        public float AthleticsPoolPerSkill => GetFloat(SettingsSchema.AthleticsPoolPerSkill);
        public int AthleticsPeakPercent => GetInt(SettingsSchema.AthleticsPeakPercent);
        public bool HealthCapsAthletics => GetBool(SettingsSchema.HealthCapsAthletics);
        public float CostPerBlow => GetFloat(SettingsSchema.CostPerBlow);
        public float CostPerKickOrBash => GetFloat(SettingsSchema.CostPerKickOrBash);
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
        public float MinMoveSpeedMultiplier => GetFloat(SettingsSchema.MinMoveSpeedMultiplier);
        public float MountMinSpeedMultiplier => GetFloat(SettingsSchema.MountMinSpeedMultiplier);
        public bool DamageBonusFollowsAthletics => GetBool(SettingsSchema.DamageBonusFollowsAthletics);

        public bool StepBackEnabled => GetBool(SettingsSchema.StepBackEnabled);
        public bool StepBackBackpedal => GetBool(SettingsSchema.StepBackBackpedal);
        public int StepBackMaxChancePercent => GetInt(SettingsSchema.StepBackMaxChancePercent);
        public float StepBackDistance => GetFloat(SettingsSchema.StepBackDistance);
        public float StepBackSeconds => GetFloat(SettingsSchema.StepBackSeconds);
        public float StepBackEnemyRange => GetFloat(SettingsSchema.StepBackEnemyRange);
        public bool StepBackHoldAttacks => GetBool(SettingsSchema.StepBackHoldAttacks);
        public int StepBackMaxAtOnce => GetInt(SettingsSchema.StepBackMaxAtOnce);

        public float RegenDelayBlowTimes => GetFloat(SettingsSchema.RegenDelayBlowTimes);
        public float BlowTimeSeconds => GetFloat(SettingsSchema.BlowTimeSeconds);
        public float FullRegenSecondsStanding => GetFloat(SettingsSchema.FullRegenSecondsStanding);
        public int RegenRateNearFullPercent => GetInt(SettingsSchema.RegenRateNearFullPercent);
        public float RegenMultiplierAtFullRun => GetFloat(SettingsSchema.RegenMultiplierAtFullRun);
        public float WalkEffortFraction => GetFloat(SettingsSchema.WalkEffortFraction);

        public bool ShowPlayerBar => GetBool(SettingsSchema.ShowPlayerBar);
        public bool ShowPlayerBarOutsideBattles => GetBool(SettingsSchema.ShowPlayerBarOutsideBattles);
        public bool ShowAttackRecoveryBar => GetBool(SettingsSchema.ShowAttackRecoveryBar);
        public bool FlashBarOnEarlyAttack => GetBool(SettingsSchema.FlashBarOnEarlyAttack);
        public int BarYellowBelowPercent => GetInt(SettingsSchema.BarYellowBelowPercent);
        public int BarOrangeBelowPercent => GetInt(SettingsSchema.BarOrangeBelowPercent);
        public int BarRedBelowPercent => GetInt(SettingsSchema.BarRedBelowPercent);

        public bool ShowFormationSpread => GetBool(SettingsSchema.ShowFormationSpread);
        public float FormationSpreadStdDevs => GetFloat(SettingsSchema.FormationSpreadStdDevs);
        public bool ShowInOrderMenu => GetBool(SettingsSchema.ShowInOrderMenu);
        public bool ShowFormationHealth => GetBool(SettingsSchema.ShowFormationHealth);
        public bool OrderStripUnderCards => GetBool(SettingsSchema.OrderStripUnderCards);

        public float HudRefreshSeconds => GetFloat(SettingsSchema.HudRefreshSeconds);
        public int PlayerBarWidth => GetInt(SettingsSchema.PlayerBarWidth);
        public int PlayerBarHeight => GetInt(SettingsSchema.PlayerBarHeight);
        public int PlayerBarOffsetRight => GetInt(SettingsSchema.PlayerBarOffsetRight);
        public int PlayerBarOffsetBottom => GetInt(SettingsSchema.PlayerBarOffsetBottom);
        public int RecoveryBarWidth => GetInt(SettingsSchema.RecoveryBarWidth);
        public int RecoveryBarHeight => GetInt(SettingsSchema.RecoveryBarHeight);
        public int RecoveryBarOffsetAbove => GetInt(SettingsSchema.RecoveryBarOffsetAbove);
        public int OrderStripTextSize => GetInt(SettingsSchema.OrderStripTextSize);
        public int OrderStripTextOffset => GetInt(SettingsSchema.OrderStripTextOffset);
        public int OrderStripBarOffset => GetInt(SettingsSchema.OrderStripBarOffset);
        public int OrderStripBarHeight => GetInt(SettingsSchema.OrderStripBarHeight);
        public int OrderStripSideMargin => GetInt(SettingsSchema.OrderStripSideMargin);
        public int OrderPanelOffsetTop => GetInt(SettingsSchema.OrderPanelOffsetTop);
        public int OrderPanelWidth => GetInt(SettingsSchema.OrderPanelWidth);
        public float FormationStatsRefreshSeconds => GetFloat(SettingsSchema.FormationStatsRefreshSeconds);
        public bool VerboseLogging => GetBool(SettingsSchema.VerboseLogging);
        public int LogMaxMegabytes => GetInt(SettingsSchema.LogMaxMegabytes);
    }
}
