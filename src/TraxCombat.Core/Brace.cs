using System;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 23 - the movement order a fighter's formation has now, as the brace reads it (AI_NOTES "Step 23"). The game's
    /// <c>MovementOrder.MovementOrderEnum</c> (v1.4.8) plus two AI cases: an AI-commanded formation walks into the enemy on a
    /// plain Move order while its behaviour is an ADVANCE (BehaviorAdvance / CautiousAdvance / Vanguard) or a tactical CHARGE
    /// (BehaviorTacticalCharge's reform and charge-past runs) - read as that, not as "move".
    /// </summary>
    public enum BraceOrderKind
    {
        Charge,
        ChargeToTarget,
        AttackEntity,
        AiCharge,
        Advance,
        AiAdvance,
        Stop,
        Move,
        Retreat,
        FallBack,
        Follow,
        FollowEntity,
        NoOrder,
        NoFormation,
        Count,
    }

    /// <summary>Step 23: the three floors - charge (charge, charge a target, attack a gate, the AI's tactical charge), advance
    /// (advance, the AI's advance) and hold (everything else: stop / halt, move, retreat, fall back, follow, no order, no
    /// formation).</summary>
    public enum BraceOrder
    {
        Charge,
        Advance,
        Hold,
        Count,
    }

    /// <summary>Step 23: why a brace ended.</summary>
    public enum BraceEnd
    {
        /// <summary>He refilled to floor + BraceRecoverPercent.</summary>
        Refilled,

        /// <summary>His formation's order changed to one with a lower floor and he is above its target now.</summary>
        OrderChanged,

        /// <summary>He refilled to the top his wounds let him reach (below floor + recover).</summary>
        WoundCap,

        /// <summary>The master switch, AthleticsEnabled or BraceEnabled off - every brace at once.</summary>
        SwitchedOff,

        /// <summary>He fell, fled or left the field (no engine call).</summary>
        LeftField,

        /// <summary>The player took him (RTS Camera) or he became the player's agent.</summary>
        PlayerControl,

        /// <summary>Step 19: the hideout boss fight's fresh start.</summary>
        FreshStart,

        /// <summary>The battle ended with him still bracing.</summary>
        MissionEnd,

        /// <summary>An exception on his brace (logged) - lifted to stay safe.</summary>
        Error,

        Count,
    }

    /// <summary>Step 23: what taking the shield out needs for one man at one look (<see cref="BraceMath.ShieldStepFor"/>).</summary>
    public enum ShieldStep
    {
        /// <summary>The shield is in his other hand already.</summary>
        InHand,

        /// <summary>He carries no shield.</summary>
        NoShield,

        /// <summary>A bow, crossbow or throwing weapon is in his hand: left alone (ranged attacks go on while bracing).</summary>
        RangedInHand,

        /// <summary>His weapon needs both hands (a two-hander, a two-handed polearm): a one-handed weapon first.</summary>
        WieldOneHanded,

        /// <summary>Take the shield out (his weapon goes with it, or his hand is empty).</summary>
        WieldShield,

        /// <summary>A shield, but his weapon needs both hands and he carries no one-handed weapon: left alone.</summary>
        NoOneHandedWeapon,
    }

    /// <summary>Step 23: one man's hands and weapon slots as the brace reads them (the game side fills it; slots are the
    /// equipment indices 0-3, −1 = none).</summary>
    public struct HandFacts
    {
        public int ShieldSlot;
        public bool ShieldInOffHand;
        public bool MainEmpty;
        public bool MainRanged;
        public bool MainNeedsBothHands;
        public int OneHandedSlot;

        public bool HasShield => ShieldSlot >= 0;

        public static HandFacts None => new HandFacts { ShieldSlot = -1, OneHandedSlot = -1, MainEmpty = true };
    }

    /// <summary>Step 23: one man's bracing band at one look, as fractions of his FULL pool (what the bar shows): he starts
    /// bracing at or below <see cref="Floor"/> and stops at or above <see cref="Target"/>.</summary>
    public readonly struct BraceBand
    {
        public BraceBand(double floor, double target, double setFloor, double setTarget, bool capped)
        {
            Floor = floor;
            Target = target;
            SetFloor = setFloor;
            SetTarget = setTarget;
            Capped = capped;
        }

        /// <summary>At or below this he braces (the order's floor; lower when his wounds cap him - <see cref="BraceMath.Band"/>).</summary>
        public double Floor { get; }

        /// <summary>At or above this he stops (floor + recover, never above the top his wounds let him refill to).</summary>
        public double Target { get; }

        /// <summary>The order's floor and floor + recover as set (no wound cap).</summary>
        public double SetFloor { get; }
        public double SetTarget { get; }

        /// <summary>His wounds moved the band (the target is his cap, below floor + recover).</summary>
        public bool Capped { get; }
    }

    /// <summary>
    /// Step 23's settings, read live from <see cref="TraxSettings"/> (hot swap: every poll reads them anew). The master
    /// switch first, then AthleticsEnabled (the bar it reads), then BraceEnabled.
    /// </summary>
    public readonly struct BraceRules
    {
        public BraceRules(bool modEnabled, bool athleticsEnabled, bool braceEnabled, int floorCharge, int floorAdvance, int floorHold,
            int recover, bool wieldShield, bool raiseShield, int recoverSpread = 0)
        {
            RecoverSpreadPercent = recoverSpread;
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            BraceEnabled = braceEnabled;
            FloorChargePercent = floorCharge;
            FloorAdvancePercent = floorAdvance;
            FloorHoldPercent = floorHold;
            RecoverPercent = recover;
            WieldShield = wieldShield;
            RaiseShield = raiseShield;
        }

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }
        public bool BraceEnabled { get; }
        public int FloorChargePercent { get; }
        public int FloorAdvancePercent { get; }
        public int FloorHoldPercent { get; }
        public int RecoverPercent { get; }

        /// <summary>Each AI man's own margin is RecoverPercent ± up to this many points, rolled once per battle (Anton: "adds
        /// some bravery-like randomness").</summary>
        public int RecoverSpreadPercent { get; }

        public bool WieldShield { get; }
        public bool RaiseShield { get; }

        public bool Enabled => ModEnabled && AthleticsEnabled && BraceEnabled;

        /// <summary>The switch that holds it off (the master switch first), or null when on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !BraceEnabled ? "BraceEnabled" : null;

        public int FloorPercent(BraceOrder order) => order switch
        {
            BraceOrder.Charge => FloorChargePercent,
            BraceOrder.Advance => FloorAdvancePercent,
            _ => FloorHoldPercent,
        };

        public static BraceRules From(TraxSettings s) => new BraceRules(s.ModEnabled, s.AthleticsEnabled, s.BraceEnabled,
            s.BraceFloorChargePercent, s.BraceFloorAdvancePercent, s.BraceFloorHoldPercent, s.BraceRecoverPercent, s.BraceWieldShield, s.BraceRaiseShield, s.BraceRecoverSpreadPercent);

        public bool SameAs(in BraceRules o) => Enabled == o.Enabled && FloorChargePercent == o.FloorChargePercent && FloorAdvancePercent == o.FloorAdvancePercent
                                               && FloorHoldPercent == o.FloorHoldPercent && RecoverPercent == o.RecoverPercent && RecoverSpreadPercent == o.RecoverSpreadPercent
                                               && WieldShield == o.WieldShield
                                               && RaiseShield == o.RaiseShield;

        /// <summary>The settings in one sentence (the mission-start and change lines, the summary).</summary>
        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - AI men swing at any Athletics";
            return "ON - an AI man whose Athletics bar (points ÷ his full pool, what the bar shows) is at or below his formation's order floor "
                   + "stops attacking in melee and only defends (guard up) until he refills to floor + " + RecoverPercent + "% (BraceRecoverPercent)"
                   + (RecoverSpreadPercent > 0 ? " ± his own " + RecoverSpreadPercent + " (BraceRecoverSpreadPercent, rolled once per man and battle)" : " exactly (BraceRecoverSpreadPercent 0)")
                   + ": charge "
                   + FloorChargePercent + "% → " + Math.Min(100, FloorChargePercent + RecoverPercent) + "% (BraceFloorChargePercent), advance "
                   + FloorAdvancePercent + "% → " + Math.Min(100, FloorAdvancePercent + RecoverPercent) + "% (BraceFloorAdvancePercent), hold / halt / retreat / move / follow / anything else "
                   + FloorHoldPercent + "% → " + Math.Min(100, FloorHoldPercent + RecoverPercent) + "% (BraceFloorHoldPercent); a wound caps the target at the top he can refill to "
                   + "(the band slides down under it); bows, crossbows and throws go on; "
                   + (WieldShield ? "the shield taken out if he carries one (BraceWieldShield)" : "the shield left as it is (BraceWieldShield off)") + ", "
                   + (RaiseShield ? "held up while he has no guard of his own (BraceRaiseShield)" : "raised only when he wants to attack (BraceRaiseShield off)")
                   + "; AI heroes and riders too, never you";
        }
    }

    /// <summary>
    /// Step 23 - BRACE BY ORDERS (Anton, 2026-09-29: "make soldiers not swing but only defend when they have reached a
    /// certain floor, that depends on their current orders, until they have replenished the floor +20%"; AI_NOTES "Step 23",
    /// DESIGN §2 "Brace by orders"). Pure: the floor by order, the band (with the wound cap), enter / leave, why it ended, the
    /// shield step. The bar is <see cref="Fighter.Fraction"/> - points ÷ his FULL pool, exactly what his bar's fill shows.
    /// </summary>
    public static class BraceMath
    {
        /// <summary>Every AI man is looked at this often (s), staggered over the ticks. Plumbing.</summary>
        public const double PollSeconds = 0.25;

        /// <summary>While bracing, whether the shield is in his hand is checked this often (s) - a wield takes about a
        /// second of animation. Plumbing.</summary>
        public const double ShieldCheckSeconds = 1.0;

        /// <summary>At most this many wield calls per brace (a one-hander and the shield, plus one re-try when the AI puts
        /// the shield away again). Plumbing.</summary>
        public const int MaxWieldCalls = 3;

        /// <summary>A brace this long counts as "long" in the summary (the "do lines turtle forever" check). Plumbing.</summary>
        public const double LongBraceSeconds = 30;

        /// <summary>The floor group of an order.</summary>
        public static BraceOrder Group(BraceOrderKind kind) => kind switch
        {
            BraceOrderKind.Charge => BraceOrder.Charge,
            BraceOrderKind.ChargeToTarget => BraceOrder.Charge,
            BraceOrderKind.AttackEntity => BraceOrder.Charge,
            BraceOrderKind.AiCharge => BraceOrder.Charge,
            BraceOrderKind.Advance => BraceOrder.Advance,
            BraceOrderKind.AiAdvance => BraceOrder.Advance,
            _ => BraceOrder.Hold,
        };

        /// <summary>The least margin a man's own offset can leave him: his target is never below floor + 1 point.</summary>
        public const double MinMargin = 0.01;

        /// <summary>One man's roll for the spread: a uniform unit in [−1, 1), rolled ONCE per battle and kept; his offset is
        /// this × BraceRecoverSpreadPercent, read live (a slider change rescales everyone at once, each keeping his place).</summary>
        public static double RollUnit(IRandomSource dice) => 2.0 * dice.NextDouble() - 1.0;

        /// <summary>His own offset in points of the bar (fraction) for the spread setting now.</summary>
        public static double Offset(in BraceRules r, double unit) =>
            double.IsNaN(unit) ? 0 : Math.Max(-1, Math.Min(1, unit)) * Math.Max(0, r.RecoverSpreadPercent) / 100.0;

        /// <summary>His own margin: recover + his offset, at least <see cref="MinMargin"/>.</summary>
        public static double Margin(in BraceRules r, double unit) =>
            Math.Max(MinMargin, Clamp01(r.RecoverPercent / 100.0) + Offset(in r, unit));

        /// <summary>
        /// His band now. Floor F = the order's %, his margin M = recover ± his own offset (at least 1 point), the top T his
        /// wounds let him refill to (1 without a wound or with HealthCapsAthletics off): target = min(F + M, T); floor =
        /// min(F, max(0, T − M)) - under a wound cap the band keeps its width M and slides down under the cap (at the
        /// bottom: from empty to the cap), so a wounded man is never stuck bracing (he always reaches his target by
        /// refilling) and never braces at a full-to-his-cap bar. <paramref name="unit"/> = his roll (0 = the plain margin).
        /// </summary>
        public static BraceBand Band(in BraceRules r, BraceOrder order, double top, double unit = 0)
        {
            double f = Clamp01(r.FloorPercent(order) / 100.0);
            double rec = Margin(in r, unit);
            double t = double.IsNaN(top) ? 1.0 : Clamp01(top);
            double setTarget = Math.Min(1.0, f + rec);
            double target = Math.Min(setTarget, t);
            double floor = Math.Min(f, Math.Max(0, t - rec));
            bool capped = target < setTarget - AthleticsMath.Epsilon;
            return new BraceBand(floor, target, f, setTarget, capped);
        }

        /// <summary>He starts bracing: at or below the floor and still below the target (a man already at his target - a
        /// capped man full to his cap, or recover 0 exactly at the floor - does not).</summary>
        public static bool Enter(in BraceBand b, double fraction) =>
            fraction <= b.Floor + AthleticsMath.Epsilon && fraction < b.Target - AthleticsMath.Epsilon;

        /// <summary>He stops bracing: at or above the target.</summary>
        public static bool Leave(in BraceBand b, double fraction) => fraction >= b.Target - AthleticsMath.Epsilon;

        /// <summary>Why a brace that <see cref="Leave"/>s now ended: the order's floor group changed since it began → order
        /// changed; else the wound cap moved the target → wound cap; else refilled.</summary>
        public static BraceEnd LeaveReason(BraceOrder atStart, BraceOrder now, in BraceBand b) =>
            atStart != now ? BraceEnd.OrderChanged : b.Capped ? BraceEnd.WoundCap : BraceEnd.Refilled;

        /// <summary>What taking his shield out needs at this look.</summary>
        public static ShieldStep ShieldStepFor(in HandFacts h)
        {
            if (!h.HasShield) return ShieldStep.NoShield;
            if (h.ShieldInOffHand) return ShieldStep.InHand;
            if (h.MainRanged) return ShieldStep.RangedInHand;
            if (h.MainEmpty || !h.MainNeedsBothHands) return ShieldStep.WieldShield;
            return h.OneHandedSlot >= 0 ? ShieldStep.WieldOneHanded : ShieldStep.NoOneHandedWeapon;
        }

        public static string Name(BraceOrderKind k) => k switch
        {
            BraceOrderKind.Charge => "charge",
            BraceOrderKind.ChargeToTarget => "charge a target",
            BraceOrderKind.AttackEntity => "attack a gate or engine",
            BraceOrderKind.AiCharge => "the AI's tactical charge",
            BraceOrderKind.Advance => "advance",
            BraceOrderKind.AiAdvance => "the AI's advance",
            BraceOrderKind.Stop => "stop / hold",
            BraceOrderKind.Move => "move to a position",
            BraceOrderKind.Retreat => "retreat",
            BraceOrderKind.FallBack => "fall back",
            BraceOrderKind.Follow => "follow",
            BraceOrderKind.FollowEntity => "follow an object",
            BraceOrderKind.NoOrder => "no order",
            BraceOrderKind.NoFormation => "no formation",
            _ => k.ToString(),
        };

        public static string Name(BraceOrder g) => g switch
        {
            BraceOrder.Charge => "charge",
            BraceOrder.Advance => "advance",
            _ => "hold and the rest",
        };

        public static string Name(BraceEnd e) => e switch
        {
            BraceEnd.Refilled => "refilled to floor + recover",
            BraceEnd.OrderChanged => "the order changed",
            BraceEnd.WoundCap => "refilled to his wound cap",
            BraceEnd.SwitchedOff => "switched off",
            BraceEnd.LeftField => "fell or left the field",
            BraceEnd.PlayerControl => "the player took him",
            BraceEnd.FreshStart => "the hideout boss fight's fresh start",
            BraceEnd.MissionEnd => "the battle ended",
            BraceEnd.Error => "an error (lifted to stay safe)",
            _ => e.ToString(),
        };

        public static string Name(ShieldStep s) => s switch
        {
            ShieldStep.InHand => "already in his hand",
            ShieldStep.NoShield => "no shield",
            ShieldStep.RangedInHand => "a ranged weapon in hand (left alone)",
            ShieldStep.WieldOneHanded => "a one-handed weapon first (his weapon needs both hands)",
            ShieldStep.WieldShield => "the shield taken out",
            ShieldStep.NoOneHandedWeapon => "his weapon needs both hands and he has no one-handed weapon (left alone)",
            _ => s.ToString(),
        };

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
