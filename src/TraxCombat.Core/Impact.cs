using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 48 (IMPACT I2): the six events the impact model decides (docs/IMPACT.md §3.5's rows; the riposte is an outcome of a
    /// right-side block or a parry, against the ATTACKER).</summary>
    public enum ImpactEvent : byte
    {
        /// <summary>A kick that lands (the engine's alternative attack without a weapon).</summary>
        Kick,

        /// <summary>A shield bash that lands (an alternative attack with the shield).</summary>
        Bash,

        /// <summary>A melee blow blocked with the shield on the RIGHT side.</summary>
        RightBlock,

        /// <summary>A melee blow parried with a weapon.</summary>
        Parry,

        /// <summary>A melee blow blocked with the shield on the WRONG side.</summary>
        WrongBlock,

        /// <summary>A melee weapon blow that lands unblocked.</summary>
        CleanHit,
    }

    /// <summary>Step 48: the event families - one switch each (<c>ImpactKicksEnabled</c>, <c>ImpactBlocksEnabled</c>, <c>ImpactHitsEnabled</c>,
    /// <c>ImpactRiposteEnabled</c>) and one effect step each (50 kicks - built, 51 blocks + riposte - built, 52 hits; the board moved by one when step 49, courage, was inserted).</summary>
    [Flags]
    public enum ImpactFamily : byte
    {
        None = 0,
        Kicks = 1,
        Blocks = 2,
        Hits = 4,
        Riposte = 8,
    }

    /// <summary>Step 48: the blow's damage type as the impact counts it (the engine's DamageTypes Cut 0 / Pierce 1 / Blunt 2).</summary>
    public enum ImpactDamageType : byte
    {
        Cut,
        Pierce,
        Blunt,
    }

    /// <summary>Step 48: the rolled outcomes, in the order they are rolled (IMPACT §3.4) - the riposte last, on the attacker.</summary>
    public enum ImpactOutcome : byte
    {
        Knockdown,
        ShieldDrop,
        Stagger,
        Riposte,
    }

    /// <summary>Step 48: why an outcome was not rolled although its row has a ceiling (the summary's "refused" - IMPACT §3.9, §3.8).</summary>
    public enum ImpactRefusal : byte
    {
        None,

        /// <summary>He is on the ground (a knockdown of this system not over yet) - nothing more can happen to him.</summary>
        Down,

        /// <summary>He got up less than <c>ImpactKnockdownImmunitySeconds</c> ago.</summary>
        KnockdownImmune,

        /// <summary>He is reeling from an impact stagger, or it ended less than <c>ImpactStaggerImmunitySeconds</c> ago.</summary>
        StaggerImmune,

        /// <summary>A shield drop on a man with no shield in his hand.</summary>
        NoShield,

        /// <summary>A stagger on a rider (never - the horse shares the channel; IMPACT §3.8).</summary>
        Rider,

        /// <summary>This blow already knocked him down (the knockdown replaces the stagger), or the defender was staggered / knocked down (no
        /// riposte from him).</summary>
        Replaced,
    }

    /// <summary>
    /// Step 48: everything the model needs to know about one event (IMPACT §3.2-3.3) - filled by the module from the collision, both men's
    /// Athletics (our <see cref="Fighter"/>s) and the engine's managed reads (weapon, guard, skills, mounts, health); built field by field (a
    /// struct, no allocation). Units: points = CURRENT Athletics points (pool × bar); f = the peak share (1 at or above the peak line);
    /// weights in kg; speeds the weapon's own numbers; skills the character's skill values.
    /// </summary>
    public struct ImpactFacts
    {
        public ImpactEvent Event;

        /// <summary>The attacker's CURRENT Athletics points, his peak share f, the animation share of his swing (step 20b's tired swing:
        /// 1 fresh, 0.85 at empty with the defaults; 0 or less = 1), his skill with what he struck with (a kick: Athletics, a bash:
        /// one-handed, a weapon: its relevant skill).</summary>
        public double AttackerPoints, AttackerF, AttackerAnimation, AttackerSkill;

        /// <summary>The defender's CURRENT points, his BAR (points ÷ his FULL pool - the summary's band), his f, his skill with what he guards
        /// with (a shield: one-handed, a parry: his weapon's skill, no guard: Athletics).</summary>
        public double DefenderPoints, DefenderBar, DefenderF, DefenderSkill;

        /// <summary>What the attacker struck with: its weight (kg - for a bash the shield's), its swing / thrust speed (the weapon's modified
        /// speed for the strike type; not used for a kick or a bash), its damage type.</summary>
        public double WeaponWeight, SwingSpeed;

        public ImpactDamageType DamageType;

        /// <summary>The defender's guarding item's weight (kg): the shield on a block, the parrying weapon on a parry (0 = unknown = the
        /// reference weight).</summary>
        public double GuardWeight;

        /// <summary>The defender holds a shield in his hand now (a shield drop needs one).</summary>
        public bool DefenderHasShield;

        public bool DefenderMounted, DefenderIsPlayer, AttackerMounted, AttackerIsPlayer;

        /// <summary>The landed blow's damage and the defender's full health (the damage term - <c>ImpactDamageWeight</c>).</summary>
        public double Damage, DefenderMaxHealth;

        /// <summary>Step 52, MOMENTUM: the attacker's own speed INTO the blow (m/s) - his velocity along the line to the defender at the
        /// moment of impact (0 or less = standing, or moving away). Step 53: a rider's = his HORSE's speed along that line (vanilla's own rider
        /// velocity - the rider's forward speed along the mount's movement direction), weighed by the rider settings.</summary>
        public double AttackerSpeed;

        /// <summary>Step 53: the attacker's blow is a PASSIVE attack - a couched lance (or a braced polearm): the game's own big hit, so no
        /// momentum is added to it.</summary>
        public bool AttackerPassive;

        /// <summary>Step 52: the attacker's weapon is two-handed (a great axe, a two-handed sword or a polearm held in both hands) - the clean
        /// hits' summary class.</summary>
        public bool TwoHanded;
    }

    /// <summary>
    /// Step 48: one man's impact state (a struct on his record - IMPACT §3.2's reeling and §3.9's anti-abuse): reeling until (an impact
    /// stagger or a lengthened reel - the bonus, and no new stagger meanwhile), stagger-immune until (its end + the immunity), on the ground
    /// until, knockdown-immune until (up + the immunity), and his last <see cref="ImpactMath.MaxRemembered"/> outcomes' times (the halving).
    /// All times are the mission's seconds; the default (all 0) = nothing happened. In shadow mode the same state is kept for what WOULD
    /// have happened, so the summary's limits read as they would live. Steps 50-52 set the true ends from the measured reaction
    /// (<see cref="EndReel"/>, <see cref="GotUp"/>).
    /// </summary>
    public struct ImpactState
    {
        public double ReelingUntil;
        public double StaggerImmuneUntil;
        public double DownUntil;
        public double KnockdownImmuneUntil;

        private double _o0, _o1, _o2, _o3, _o4, _o5;
        private int _next;
        private int _count;

        public bool Reeling(double now) => now < ReelingUntil;

        public bool Down(double now) => now < DownUntil;

        public bool StaggerImmune(double now) => now < StaggerImmuneUntil;

        public bool KnockdownImmune(double now) => !Down(now) && now < KnockdownImmuneUntil;

        /// <summary>His outcomes within the last <paramref name="window"/> seconds (0 = none counted).</summary>
        public int Recent(double now, double window)
        {
            if (!(window > 0) || _count == 0) return 0;
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                double t = At(i);
                if (t <= now && now - t < window) n++;
            }
            return n;
        }

        /// <summary>An outcome against him at <paramref name="now"/> (the oldest of the remembered ones forgotten).</summary>
        public void NoteOutcome(double now)
        {
            switch (_next)
            {
                case 0: _o0 = now; break;
                case 1: _o1 = now; break;
                case 2: _o2 = now; break;
                case 3: _o3 = now; break;
                case 4: _o4 = now; break;
                default: _o5 = now; break;
            }
            _next = (_next + 1) % ImpactMath.MaxRemembered;
            if (_count < ImpactMath.MaxRemembered) _count++;
        }

        /// <summary>He reels (an impact stagger, a lengthened reel or a riposte's recoil) until <paramref name="until"/>; no new stagger until
        /// <paramref name="immunity"/> seconds after it.</summary>
        public void Reel(double until, double immunity)
        {
            if (until > ReelingUntil) ReelingUntil = until;
            double immune = ReelingUntil + Math.Max(0, immunity);
            if (immune > StaggerImmuneUntil) StaggerImmuneUntil = immune;
        }

        /// <summary>He is on the ground until <paramref name="upAt"/>; no new knockdown until <paramref name="immunity"/> seconds after.</summary>
        public void KnockedDown(double now, double upAt, double immunity)
        {
            DownUntil = upAt;
            KnockdownImmuneUntil = upAt + Math.Max(0, immunity);
            if (ReelingUntil > now) ReelingUntil = now; // a man on the ground no longer reels
        }

        /// <summary>The measured end of his reel (steps 50-52's watch): the reeling and its immunity re-timed from it.</summary>
        public void EndReel(double at, double immunity)
        {
            ReelingUntil = at;
            StaggerImmuneUntil = at + Math.Max(0, immunity);
        }

        /// <summary>The measured moment he is up again (steps 50-52's watch).</summary>
        public void GotUp(double at, double immunity) => KnockedDown(at, at, immunity);

        /// <summary>Forget everything (a fresh start).</summary>
        public void Clear() => this = default;

        private double At(int i) => i switch
        {
            0 => _o0,
            1 => _o1,
            2 => _o2,
            3 => _o3,
            4 => _o4,
            _ => _o5,
        };
    }

    /// <summary>Step 48: one rolled outcome of a decision - its ceiling (0-1), the curve's raw chance, the halvings applied, the final chance,
    /// the die (NaN = not rolled), whether it happens, and why it was not rolled.</summary>
    public struct ImpactRoll
    {
        /// <summary>The row has this outcome at all (a ceiling above 0 - or the riposte asked).</summary>
        public bool InRow;
        public double Ceiling;
        public double Raw;
        public int Halvings;
        public double Chance;
        public double Die;
        public bool Happens;
        public ImpactRefusal Refusal;

        /// <summary>Rolled: in the row, not refused, a chance above 0.</summary>
        public bool Rolled => !double.IsNaN(Die);

        public static ImpactRoll None => new ImpactRoll { Die = double.NaN };
    }

    /// <summary>
    /// Step 48: the model's answer for one event (IMPACT §3): the pressure and its parts, each outcome's roll, the lengths (NaN = not
    /// applicable) - ONE decision per event; steps 50-52 apply it through the spike's seams (the decorator's KnockDown, the block stun,
    /// <c>IImpactBody</c>).
    /// </summary>
    public struct ImpactDecision
    {
        public ImpactEvent Event;
        public double Now;

        /// <summary>The defender's tiredness T = 1 − f, his bar, the bodies, the blow's impact I, his guard G, both skill factors K, the
        /// mismatch R and log₂ R, the part of it beyond the even band × the weight, reeling (the bonus given), the damage term, x.</summary>
        public double T, Bar, BodyA, BodyD, Impact, Guard, SkillA, SkillD, R, Log2R, MismatchPart, DamagePart, X;

        /// <summary>Step 56: the share of T that entered x (1 = all of it; a CORRECT block - right-side shield or parry - counts
        /// <c>CorrectBlockTiredWeightPercent</c> of it) and the part it gave (TiredWeight × T).</summary>
        public double TiredWeight, TiredPart;

        /// <summary>Step 52: the attacker's speed into the blow (m/s) and the momentum factor it gave the impact (1 = none; a rider 1).</summary>
        public double AttackerSpeed, Momentum;

        public bool Reeling;

        public ImpactRoll Knockdown, ShieldDrop, Stagger, Riposte;

        /// <summary>The riposte's pressure on the attacker (NaN = not asked), his tiredness, and (step 56) the factor the blocker's own
        /// tiredness left on its chance: 1 − <c>RiposteBlockerTiredPercent</c> × T_D (1 at the default 0; step 48's f_D at 100).</summary>
        public double XRiposte, TAttacker, RiposteBlockerFactor;

        /// <summary>A rider's knockdown = knocked OFF his horse (at <c>ImpactRiderDismountPercent</c> of the chance).</summary>
        public bool Dismount;

        /// <summary>Lengths (NaN = not applicable): the stagger (s), the kicked man's reel (x his vanilla reel), the get-up (x), the blocker's
        /// stun (x), the riposted attacker's recoil (s).</summary>
        public double StaggerSeconds, ReelFactor, GetUpFactor, StunFactor, RiposteStunSeconds;

        public bool DefenderIsPlayer, AttackerIsPlayer, DefenderMounted;

        /// <summary>Anything happens (a rolled outcome came up).</summary>
        public bool AnyOutcome => Knockdown.Happens || ShieldDrop.Happens || Stagger.Happens || Riposte.Happens;

        public ImpactRoll Outcome(ImpactOutcome o) => o switch
        {
            ImpactOutcome.Knockdown => Knockdown,
            ImpactOutcome.ShieldDrop => ShieldDrop,
            ImpactOutcome.Stagger => Stagger,
            _ => Riposte,
        };
    }

    /// <summary>
    /// Step 48: the impact settings, read live (IMPACT §5 - every value a setting; DESIGN's table). <see cref="Enabled"/> = the master
    /// switch FIRST, then <c>AthleticsEnabled</c> (the model's only inputs are Athletics), then <c>ImpactEnabled</c>. <see cref="Applies"/>:
    /// a family's decision changes the battle only while it is on, shadow mode is off, the spike is off (the spike tests the engine with its
    /// own loud values - the model stays shadow meanwhile) AND its effect is built (<see cref="ImpactMath.EffectsBuilt"/>).
    /// </summary>
    public readonly struct ImpactRules
    {
        public ImpactRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            AthleticsOn = s.AthleticsEnabled;
            ImpactOn = s.ImpactEnabled;
            Kicks = s.ImpactKicksEnabled;
            Blocks = s.ImpactBlocksEnabled;
            Hits = s.ImpactHitsEnabled;
            RiposteOn = s.ImpactRiposteEnabled;
            Shadow = s.ImpactShadowMode;
            Spike = s.ImpactSpike;
            KickShieldDrop = s.KickShieldDropMaxPercent / 100.0;
            KickKnockdown = s.KickKnockdownMaxPercent / 100.0;
            RightStagger = s.RightBlockStaggerMaxPercent / 100.0;
            RightShieldDrop = s.RightBlockShieldDropMaxPercent / 100.0;
            RightKnockdown = s.RightBlockKnockdownMaxPercent / 100.0;
            ParryStagger = s.ParryStaggerMaxPercent / 100.0;
            ParryKnockdown = s.ParryKnockdownMaxPercent / 100.0;
            WrongStagger = s.WrongBlockStaggerMaxPercent / 100.0;
            WrongShieldDrop = s.WrongBlockShieldDropMaxPercent / 100.0;
            WrongKnockdown = s.WrongBlockKnockdownMaxPercent / 100.0;
            HitStagger = s.HitStaggerMaxPercent / 100.0;
            HitKnockdown = s.HitKnockdownMaxPercent / 100.0;
            RiposteMax = s.RiposteStaggerMaxPercent / 100.0;
            CorrectBlockTiredWeight = s.CorrectBlockTiredWeightPercent / 100.0;
            RiposteBlockerTired = s.RiposteBlockerTiredPercent / 100.0;
            StaggerBase = s.StaggerBaseSeconds;
            StaggerExtra = s.StaggerTiredExtraPercent / 100.0;
            StaggerMax = s.StaggerMaxSeconds;
            ReelExtra = s.KickReelTiredExtraPercent / 100.0;
            GetUpMax = Math.Max(1, (double)s.GetUpMaxSlowdown);
            StunExtra = s.BlockStunTiredExtraPercent / 100.0;
            RiposteStunMax = s.RiposteStunMaxSeconds;
            Random = s.ImpactRandomPercent / 100.0;
            StunCap = s.ImpactStunCapSeconds;
            StaggerImmunity = s.ImpactStaggerImmunitySeconds;
            KnockdownImmunity = s.ImpactKnockdownImmunitySeconds;
            DiminishWindow = s.ImpactDiminishWindowSeconds;
            DiminishFactor = Math.Max(0, Math.Min(1, (double)s.ImpactDiminishFactor));
            PickupLock = s.ShieldPickupLockSeconds;
            OnPlayer = s.ImpactOnPlayerPercent / 100.0;
            RiderDismount = s.ImpactRiderDismountPercent / 100.0;
            TiredNeverShrugsOff = s.ImpactTiredNeverShrugsOff;
            MismatchWeight = s.ImpactMismatchWeight;
            EvenBand = s.ImpactEvenBandDoublings;
            BodyExponent = s.ImpactBodyExponent;
            BodyFloor = Math.Max(1, s.ImpactBodyFloorPoints);
            StaggeredBonus = s.ImpactStaggeredBonus;
            DamageWeight = s.ImpactDamageWeight;
            RefWeight = Math.Max(0.01, (double)s.ImpactRefWeightKg);
            WeightExponent = s.ImpactWeightExponent;
            RefSpeed = Math.Max(1, (double)s.ImpactRefSwingSpeed);
            SpeedExponent = s.ImpactSpeedExponent;
            Blunt = s.ImpactBluntFactor;
            Cut = s.ImpactCutFactor;
            Pierce = s.ImpactPierceFactor;
            KickFactor = s.ImpactKickFactor;
            BashFactor = s.ImpactBashFactor;
            SkillMin = s.ImpactSkillMinFactor;
            SkillMax = s.ImpactSkillMaxFactor;
            GuardRight = s.ImpactGuardShieldRight;
            GuardParry = s.ImpactGuardParry;
            GuardWrong = s.ImpactGuardShieldWrong;
            GuardNone = s.ImpactGuardNone;
            StaggerFrom = s.ImpactStaggerFrom;
            ShieldDropFrom = s.ImpactShieldDropFrom;
            KnockdownFrom = s.ImpactKnockdownFrom;
            MomentumOn = s.ImpactMomentumEnabled;
            MomentumWeight = s.ImpactMomentumPercent / 100.0;
            MomentumWalk = s.ImpactMomentumWalkMetresPerSecond;
            MomentumSprint = s.ImpactMomentumSprintMetresPerSecond;
            RiderMomentumWeight = s.ImpactMomentumRiderPercent / 100.0;
            RiderMomentumWalk = s.ImpactMomentumRiderWalkMetresPerSecond;
            RiderMomentumGallop = s.ImpactMomentumRiderGallopMetresPerSecond;
        }

        public static ImpactRules From(TraxSettings s) => new ImpactRules(s);

        public bool ModEnabled { get; }
        public bool AthleticsOn { get; }
        public bool ImpactOn { get; }
        public bool Kicks { get; }
        public bool Blocks { get; }
        public bool Hits { get; }
        public bool RiposteOn { get; }
        public bool Shadow { get; }
        public bool Spike { get; }

        public double KickShieldDrop { get; }
        public double KickKnockdown { get; }
        public double RightStagger { get; }
        public double RightShieldDrop { get; }
        public double RightKnockdown { get; }
        public double ParryStagger { get; }
        public double ParryKnockdown { get; }
        public double WrongStagger { get; }
        public double WrongShieldDrop { get; }
        public double WrongKnockdown { get; }
        public double HitStagger { get; }
        public double HitKnockdown { get; }
        public double RiposteMax { get; }
        /// <summary>Step 56: the share of the blocker's tiredness in x on a CORRECT block (right-side shield or parry) - 0.25 by default, 1 =
        /// step 48 (his tiredness alone staggered a correct blocker).</summary>
        public double CorrectBlockTiredWeight { get; }
        /// <summary>Step 56: how much the blocker's own tiredness lowers the riposte (chance × (1 − this × T_D)) - 0 by default, 1 = step 48's
        /// × f_D (only a fresh blocker riposted).</summary>
        public double RiposteBlockerTired { get; }
        public double StaggerBase { get; }
        public double StaggerExtra { get; }
        public double StaggerMax { get; }
        public double ReelExtra { get; }
        public double GetUpMax { get; }
        public double StunExtra { get; }
        public double RiposteStunMax { get; }
        public double Random { get; }
        public double StunCap { get; }
        public double StaggerImmunity { get; }
        public double KnockdownImmunity { get; }
        public double DiminishWindow { get; }
        public double DiminishFactor { get; }
        public double PickupLock { get; }
        public double OnPlayer { get; }
        public double RiderDismount { get; }
        public bool TiredNeverShrugsOff { get; }
        public double MismatchWeight { get; }
        public double EvenBand { get; }
        public double BodyExponent { get; }
        public double BodyFloor { get; }
        public double StaggeredBonus { get; }
        public double DamageWeight { get; }
        public double RefWeight { get; }
        public double WeightExponent { get; }
        public double RefSpeed { get; }
        public double SpeedExponent { get; }
        public double Blunt { get; }
        public double Cut { get; }
        public double Pierce { get; }
        public double KickFactor { get; }
        public double BashFactor { get; }
        public double SkillMin { get; }
        public double SkillMax { get; }
        public double GuardRight { get; }
        public double GuardParry { get; }
        public double GuardWrong { get; }
        public double GuardNone { get; }
        public double StaggerFrom { get; }
        public double ShieldDropFrom { get; }
        public double KnockdownFrom { get; }

        /// <summary>Step 52, MOMENTUM: on / the impact added at a full sprint (0.6 = +60%) / the speed (m/s) below which nothing is added (a
        /// walk) / the speed at which all of it is (a sprint).</summary>
        public bool MomentumOn { get; }
        public double MomentumWeight { get; }
        public double MomentumWalk { get; }
        public double MomentumSprint { get; }

        /// <summary>Step 53, a RIDER's momentum: the impact added at a full gallop (0.3 = +30%) / the horse speed (m/s) below which nothing is
        /// added / the speed at which all of it is. A couched lance never (<see cref="ImpactFacts.AttackerPassive"/>).</summary>
        public double RiderMomentumWeight { get; }
        public double RiderMomentumWalk { get; }
        public double RiderMomentumGallop { get; }

        /// <summary>The model runs (decides and logs): the master switch FIRST, Athletics, the impact switch.</summary>
        public bool Enabled => ModEnabled && AthleticsOn && ImpactOn;

        /// <summary>The switch that holds it off (the master switch first), or null when on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsOn ? "AthleticsEnabled" : !ImpactOn ? "ImpactEnabled" : null;

        /// <summary>The family's own switch (the riposte's for <see cref="ImpactFamily.Riposte"/>).</summary>
        public bool FamilyOn(ImpactFamily f) => f switch
        {
            ImpactFamily.Kicks => Kicks,
            ImpactFamily.Blocks => Blocks,
            ImpactFamily.Hits => Hits,
            ImpactFamily.Riposte => RiposteOn,
            _ => false,
        };

        /// <summary>The model decides this event's family (on, and its switch on).</summary>
        public bool Decides(ImpactFamily f) => Enabled && FamilyOn(f);

        /// <summary>Its decisions change the battle: decided, not shadow, the spike off, and its effect built (step 50: kicks / bashes; step 51: blocks + the riposte; 52 hits).</summary>
        public bool Applies(ImpactFamily f) => Decides(f) && !ShadowNow && (ImpactMath.EffectsBuilt & f) != 0;

        /// <summary>Step 51: is this EVENT decided at all? Its family's switch - or, for a right-side block / a parry, the riposte's own switch
        /// (<c>ImpactBlocksEnabled</c> off and <c>ImpactRiposteEnabled</c> on = only the riposte is rolled on a block; <see cref="ImpactMath.Decide"/>
        /// leaves the block's own outcomes out then).</summary>
        public bool DecidesEvent(ImpactEvent e) => Decides(ImpactMath.Family(e)) || (ImpactMath.CanRiposte(e) && Decides(ImpactFamily.Riposte));

        /// <summary>Shadow for every family now: the switch, or the spike running (it owns the levers while it tests them).</summary>
        public bool ShadowNow => Shadow || Spike;

        /// <summary>
        /// Step 50: does this family's decision WRITE the men's impact states (reeling, immunities, the recent outcomes)? Yes when it applies
        /// (what happened) and in shadow mode (everything is "would" alike - step 48's reading). NO for a family decided LIVE whose effect is
        /// not built yet (blocks and clean hits in step 50): a stagger that never happened must not give the reeling bonus, an immunity or a
        /// halving to a live kick, nor shock to courage - that decision is made on copies of the states.
        /// </summary>
        public bool KeepsState(ImpactFamily f) => Applies(f) || (Decides(f) && ShadowNow);

        /// <summary>The mode in words (the log): "SHADOW …" / "LIVE …".</summary>
        public string ModeText()
        {
            if (!Enabled) return "OFF (" + OffBecause + ")";
            if (Shadow) return "SHADOW (ImpactShadowMode) - every event decided and logged, nothing applied";
            if (Spike) return "SHADOW (the spike is on - ImpactSpike owns the levers) - decided and logged, nothing applied";
            var built = ImpactMath.EffectsBuilt;
            var notBuilt = (ImpactFamily.Kicks | ImpactFamily.Blocks | ImpactFamily.Hits | ImpactFamily.Riposte) & ~built;
            if (built == ImpactFamily.None)
                return "LIVE - but no effect is built yet (steps 50-52 bring them): every event decided and logged only";
            return "LIVE - " + ImpactMath.FamiliesText(built) + " applied"
                   + (notBuilt != ImpactFamily.None ? ", " + ImpactMath.FamiliesText(notBuilt) + " decided and logged only (effect not built yet)" : string.Empty);
        }

        /// <summary>The settings in one sentence (the mission-start line; IMPACT §6: the anchors in words).</summary>
        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - the game's own reactions";
            return ModeText() + "; families: kicks / bashes " + OnOff(Kicks) + ", blocks " + OnOff(Blocks) + ", clean hits " + OnOff(Hits) + ", riposte " + OnOff(RiposteOn)
                   + "; anchors - a kick on an empty even man: shield " + P(KickShieldDrop) + ", down " + P(KickKnockdown)
                   + "; a block on the right side: stagger " + P(RightStagger) + " / down " + P(RightKnockdown) + (RightShieldDrop > 0 ? " / shield " + P(RightShieldDrop) : string.Empty)
                   + "; a parry: stagger " + P(ParryStagger) + " / down " + P(ParryKnockdown)
                   + "; the wrong side: stagger " + P(WrongStagger) + " / shield " + P(WrongShieldDrop) + " / down " + P(WrongKnockdown)
                   + "; a clean hit: stagger " + P(HitStagger) + " / down " + P(HitKnockdown) + "; the riposte " + P(RiposteMax)
                   + " (step 56: a correct block counts " + F0(100 * CorrectBlockTiredWeight) + "% of the blocker's tiredness, the riposte x (1 - " + F0(100 * RiposteBlockerTired) + "% of his T))"
                   + "; two even fresh men: 0% always (the even band ±" + F2(EvenBand) + " doublings, " + F2(MismatchWeight) + " a doubling beyond it, reeling +" + F2(StaggeredBonus)
                   + ", damage x" + F2(DamageWeight) + "); the S-curves from x " + F2(StaggerFrom) + " / " + F2(ShieldDropFrom) + " / " + F2(KnockdownFrom)
                   + "; a stagger " + F2(StaggerBase) + " s fresh → " + F2(StaggerBase * (1 + StaggerExtra)) + " s empty (± " + F0(100 * Random) + "% x T, at most " + F1(StaggerMax) + " s)"
                   + ", a kicked man's reel x" + F2(1 + ReelExtra) + " empty, get-up x" + F2(GetUpMax) + ", the block stun x" + F2(1 + StunExtra) + " (cap " + F1(StunCap) + " s)"
                   + "; limits - no new stagger " + F1(StaggerImmunity) + " s after one, no new knockdown " + F1(KnockdownImmunity) + " s after getting up, each outcome x" + F2(DiminishFactor)
                   + " for " + F1(DiminishWindow) + " s; against you " + F0(100 * OnPlayer) + "%; a rider knocked off at " + F0(100 * RiderDismount) + "% of a knockdown"
                   + "; tired men always flinch " + OnOff(TiredNeverShrugsOff)
                   + "; momentum (the attacker's run into the blow) " + (MomentumOn && MomentumWeight > 0
                       ? "+" + F0(100 * MomentumWeight) + "% impact at " + F1(MomentumSprint) + " m/s and faster, none at " + F1(MomentumWalk) + " m/s or slower; a rider's horse +"
                         + F0(100 * RiderMomentumWeight) + "% at " + F1(RiderMomentumGallop) + " m/s, none at " + F1(RiderMomentumWalk) + " m/s (a couched lance never)"
                       : "OFF");
        }

        private static string OnOff(bool on) => on ? "on" : "OFF";

        private static string P(double share) => F0(100 * share) + "%";

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 48 - IMPACT I2, THE MODEL (docs/IMPACT.md §3; AI_NOTES "Step 48"). Pure: x = T_D + w × band(log₂ R) + (reeling ? bonus : 0) +
    /// d × damage ÷ max health, R = (B_A ÷ B_D)^e × (I_A × K_A) ÷ (G_D × K_D); chance = ceiling × S((x − from) ÷ (1 − from)) × factor^n (×
    /// the player's share; a rider's knockdown × the dismount share), S(t) = 3t² − 2t³; one die per outcome, knockdown → shield → stagger,
    /// the knockdown replacing the stagger; the riposte with the roles turned × (1 − k T_D). Step 56: on a CORRECT block (right-side
    /// shield, parry) T_D enters x × <c>CorrectBlockTiredWeightPercent</c> (0.25) - a correct block is mostly the ATTACKER's risk. Lengths = base × (1 + extra × T) × a roll
    /// in [1 − r T, 1 + r T]. The dice are injected (<see cref="IRandomSource"/>).
    /// </summary>
    public static class ImpactMath
    {
        public const int EventCount = 6;
        public const int OutcomeCount = 4;
        public const int RefusalCount = 7;

        /// <summary>The summary's bar bands: 100-75 / 75-50 / 50-25 / 25-0 % (the defender's bar = points ÷ his FULL pool).</summary>
        public const int BandCount = 4;

        /// <summary>The skill at which the skill factor reaches its maximum (IMPACT §3.3 - plumbing, documented).</summary>
        public const double SkillCap = 300;

        /// <summary>How many recent outcomes a man's state remembers (0.5^6 = 1.6% - more would change nothing; plumbing).</summary>
        public const int MaxRemembered = 6;

        /// <summary>Plumbing until steps 50-52 measure the real ones with the spike's ReactionWatch: a vanilla kick reel ~0.45 s (the spike's
        /// stand-in 0.42 s) and a vanilla fall-and-rise ~1.5 s - the shadow state's reel / ground times are these × the decided factor.</summary>
        public const double VanillaReelSeconds = 0.45;

        public const double VanillaFallRiseSeconds = 1.5;

        /// <summary>The families whose EFFECT is built (step 48: none - decide and log only; step 50 adds Kicks, step 51 Blocks + Riposte, step
        /// 52 Hits - every family is live).</summary>
        public static readonly ImpactFamily EffectsBuilt = ImpactFamily.Kicks | ImpactFamily.Blocks | ImpactFamily.Riposte | ImpactFamily.Hits;

        /// <summary>Step 52: the attacker-speed bands of the momentum summary: standing / walking / running / sprinting.</summary>
        public const int SpeedBandCount = 4;

        /// <summary>Step 52: below this speed into the blow (m/s) a man counts as standing (step 33's "moving" line, 0.5 m/s).</summary>
        public const double StandingMetresPerSecond = 0.5;

        /// <summary>
        /// Step 52, MOMENTUM (Anton 2026-10-17: "a fresh shock unit attacking a tired line must be devastating"): the share of the full
        /// momentum bonus the attacker's speed into the blow earns - clamp((speed − walk) ÷ (sprint − walk), 0, 1): none at a walk or
        /// slower, all of it at a sprint. A sprint reference at or below the walk's = all or nothing at the walk's.
        /// </summary>
        public static double MomentumShare(double speed, double walk, double sprint)
        {
            if (double.IsNaN(speed) || double.IsNaN(walk) || double.IsNaN(sprint)) return 0;
            if (sprint <= walk) return speed >= walk && speed > 0 ? 1 : 0;
            double s = (speed - walk) / (sprint - walk);
            return s <= 0 ? 0 : s >= 1 ? 1 : s;
        }

        /// <summary>Step 52: the impact factor of the attacker's speed into the blow - 1 + weight × <see cref="MomentumShare"/>; 1 while
        /// momentum is off. Kicks, bashes and weapon blows alike, blocked or not. Step 53: a RIDER's = his horse's speed at the rider weight and
        /// references (<c>ImpactMomentumRider*</c>) - and 1 for a couched lance (<paramref name="passive"/>: the game's own big hit already).</summary>
        public static double MomentumFactor(in ImpactRules r, double speed, bool attackerMounted, bool passive = false)
        {
            if (!r.MomentumOn) return 1;
            if (attackerMounted)
            {
                if (passive || !(r.RiderMomentumWeight > 0)) return 1;
                return 1 + r.RiderMomentumWeight * MomentumShare(speed, r.RiderMomentumWalk, r.RiderMomentumGallop);
            }
            if (!(r.MomentumWeight > 0)) return 1;
            return 1 + r.MomentumWeight * MomentumShare(speed, r.MomentumWalk, r.MomentumSprint);
        }

        /// <summary>Step 53: the same bands for a RIDER's horse - 0 standing, 1 walking (below the rider walk reference - nothing added), 2
        /// trotting / cantering (some), 3 galloping (at or above the gallop reference - all of it).</summary>
        public static int RiderSpeedBand(in ImpactRules r, double speed)
        {
            if (double.IsNaN(speed) || speed < StandingMetresPerSecond) return 0;
            if (speed < r.RiderMomentumWalk) return 1;
            if (speed < r.RiderMomentumGallop) return 2;
            return 3;
        }

        public static string RiderSpeedBandName(int band) => band switch
        {
            0 => "standing",
            1 => "walking",
            2 => "trotting / cantering",
            _ => "galloping",
        };

        /// <summary>Step 52: the summary's band of the attacker's speed into the blow - 0 standing (below 0.5 m/s), 1 walking (below the walk
        /// reference - no momentum), 2 running (some), 3 sprinting (at or above the sprint reference - all of it).</summary>
        public static int SpeedBand(in ImpactRules r, double speed)
        {
            if (double.IsNaN(speed) || speed < StandingMetresPerSecond) return 0;
            if (speed < r.MomentumWalk) return 1;
            if (speed < r.MomentumSprint) return 2;
            return 3;
        }

        public static string SpeedBandName(int band) => band switch
        {
            0 => "standing",
            1 => "walking",
            2 => "running",
            _ => "sprinting",
        };

        public static ImpactFamily Family(ImpactEvent e) => e switch
        {
            ImpactEvent.Kick or ImpactEvent.Bash => ImpactFamily.Kicks,
            ImpactEvent.CleanHit => ImpactFamily.Hits,
            _ => ImpactFamily.Blocks,
        };

        public static bool IsBlock(ImpactEvent e) => e == ImpactEvent.RightBlock || e == ImpactEvent.Parry || e == ImpactEvent.WrongBlock;

        public static bool IsLanded(ImpactEvent e) => !IsBlock(e);

        public static bool IsAlternative(ImpactEvent e) => e == ImpactEvent.Kick || e == ImpactEvent.Bash;

        /// <summary>Step 51: a right-side block or a parry - the two blocks a defender can riposte from (IMPACT §3.6).</summary>
        public static bool CanRiposte(ImpactEvent e) => e == ImpactEvent.RightBlock || e == ImpactEvent.Parry;

        /// <summary>
        /// Step 51 (IMPACT §3.5's "always" column for blocks, §3.7): the blocker's stun x (1 + extra × T) - DETERMINISTIC (no die): the
        /// game asks it on WORKER threads before the decision, so the decorator's seam computes it from his tiredness alone and the decision
        /// logs the same number. The randomness of a tired block lives in the rolled stagger. T clamped 0-1; extra below 0 = 0.
        /// </summary>
        public static double BlockStunFactor(double extraShare, double tired)
        {
            double t = double.IsNaN(tired) ? 0 : Math.Max(0, Math.Min(1, tired));
            return 1 + Math.Max(0, double.IsNaN(extraShare) ? 0 : extraShare) * t;
        }

        public static double BlockStunFactor(in ImpactRules r, double tired) => BlockStunFactor(r.StunExtra, tired);

        /// <summary>
        /// Step 51: the stun the engine gets for a block lengthened by <paramref name="factor"/>: the game's own stun held at the game's own
        /// cap first (what vanilla would have played), × the factor, never above <paramref name="cap"/> (<c>ImpactStunCapSeconds</c>). A
        /// factor of 1 (a fresh blocker) = the game's stun untouched (the hook then clamps it to the game's cap as vanilla does).
        /// </summary>
        public static float LengthenedStun(float vanilla, float vanillaCap, double factor, float cap)
        {
            if (float.IsNaN(vanilla) || vanilla <= 0f || double.IsNaN(factor) || factor <= 1.001) return vanilla;
            float v = vanillaCap > 0f && vanilla > vanillaCap ? vanillaCap : vanilla;
            float s = (float)(v * factor);
            if (cap > 0f && s > cap) s = cap;
            return s < v ? v : s;
        }

        /// <summary>The event of one melee collision (the module reads the engine's flags): a landed kick / bash, a block by its kind
        /// (step 22's <see cref="BlockMath.KindOf"/>), a landed weapon blow - or none (a kick into a shield, a blow into nothing).</summary>
        public static bool EventOf(bool alternative, bool withWeapon, bool landed, BlockKind block, out ImpactEvent e)
        {
            e = ImpactEvent.CleanHit;
            if (alternative)
            {
                if (!landed) return false;
                e = withWeapon ? ImpactEvent.Bash : ImpactEvent.Kick;
                return true;
            }
            switch (block)
            {
                case BlockKind.ShieldRightSide:
                    e = ImpactEvent.RightBlock;
                    return true;
                case BlockKind.ShieldWrongSide:
                    e = ImpactEvent.WrongBlock;
                    return true;
                case BlockKind.WeaponParry:
                    e = ImpactEvent.Parry;
                    return true;
            }
            if (!landed) return false;
            e = ImpactEvent.CleanHit;
            return true;
        }

        /// <summary>The summary's band of a bar (points ÷ FULL pool): 0 = 100-75 %, 1 = 75-50, 2 = 50-25, 3 = 25-0.</summary>
        public static int BarBand(double bar)
        {
            if (double.IsNaN(bar) || bar >= 0.75) return 0;
            if (bar >= 0.5) return 1;
            if (bar >= 0.25) return 2;
            return 3;
        }

        public static string BandName(int band) => band switch
        {
            0 => "100-75%",
            1 => "75-50%",
            2 => "50-25%",
            _ => "25-0%",
        };

        /// <summary>The smooth S-curve S(t) = 3t² − 2t³ on 0..1 (0 below, 1 above).</summary>
        public static double S(double t)
        {
            if (double.IsNaN(t) || t <= 0) return 0;
            if (t >= 1) return 1;
            return t * t * (3 - 2 * t);
        }

        /// <summary>The curve of one outcome: S((x − from) ÷ (1 − from)), from clamped to 0..0.95.</summary>
        public static double Curve(double x, double from)
        {
            double f = Math.Max(0, Math.Min(0.95, from));
            return S((x - f) / (1 - f));
        }

        /// <summary>The skill factor K = min + (max − min) × min(1, skill ÷ 300) (skill below 0 = 0).</summary>
        public static double SkillFactor(in ImpactRules r, double skill)
        {
            double s = double.IsNaN(skill) ? 0 : Math.Max(0, Math.Min(1, skill / SkillCap));
            return r.SkillMin + (r.SkillMax - r.SkillMin) * s;
        }

        /// <summary>(weight ÷ the reference)^exponent; an unknown weight (0 or less) counts as the reference (1).</summary>
        public static double WeightFactor(in ImpactRules r, double kg)
        {
            if (!(kg > 0)) return 1;
            return Math.Pow(kg / r.RefWeight, r.WeightExponent);
        }

        public static double DamageFactor(in ImpactRules r, ImpactDamageType t) => t switch
        {
            ImpactDamageType.Blunt => r.Blunt,
            ImpactDamageType.Pierce => r.Pierce,
            _ => r.Cut,
        };

        /// <summary>A swing's / thrust's impact: weight factor × (speed × the animation share ÷ the reference)^exponent × the damage type;
        /// an unknown speed (0 or less) counts as the reference, an animation share of 0 or less as 1.</summary>
        public static double SwingImpact(in ImpactRules r, double kg, double speed, double animation, ImpactDamageType type)
        {
            double anim = animation > 0 ? animation : 1;
            double s = speed > 0 ? speed * anim / r.RefSpeed : anim;
            return WeightFactor(in r, kg) * Math.Pow(s, r.SpeedExponent) * DamageFactor(in r, type);
        }

        /// <summary>The blow's impact I (IMPACT §3.3): a kick's factor, a bash = the shield's weight factor × the bash factor, else the swing -
        /// step 52: × the MOMENTUM of the attacker's speed into the blow (<see cref="MomentumFactor"/>, 1 standing).</summary>
        public static double Impact(in ImpactRules r, in ImpactFacts f) => BlowImpact(in r, in f) * MomentumFactor(in r, f.AttackerSpeed, f.AttackerMounted, f.AttackerPassive);

        /// <summary>The blow's own impact, before the momentum: a kick's factor, a bash = the shield's weight factor × the bash factor, else the swing.</summary>
        public static double BlowImpact(in ImpactRules r, in ImpactFacts f) => f.Event switch
        {
            ImpactEvent.Kick => r.KickFactor,
            ImpactEvent.Bash => WeightFactor(in r, f.WeaponWeight) * r.BashFactor,
            _ => SwingImpact(in r, f.WeaponWeight, f.SwingSpeed, f.AttackerAnimation, f.DamageType),
        };

        /// <summary>The defender's guard G: the right-side shield / the parry / the wrong-side shield × the guarding item's weight factor;
        /// none (a kick, a clean hit) = his body only. Never below 0.01.</summary>
        public static double Guard(in ImpactRules r, ImpactEvent e, double guardKg)
        {
            double g = e switch
            {
                ImpactEvent.RightBlock => r.GuardRight * WeightFactor(in r, guardKg),
                ImpactEvent.Parry => r.GuardParry * WeightFactor(in r, guardKg),
                ImpactEvent.WrongBlock => r.GuardWrong * WeightFactor(in r, guardKg),
                _ => r.GuardNone,
            };
            return Math.Max(0.01, g);
        }

        /// <summary>The body: his current points, never below the floor.</summary>
        public static double Body(in ImpactRules r, double points) => Math.Max(r.BodyFloor, double.IsNaN(points) ? 0 : points);

        /// <summary>The even band: a log₂ mismatch within ± the band counts 0; beyond it only the part beyond (negative for the weaker attacker).</summary>
        public static double Band(in ImpactRules r, double log2)
        {
            double band = Math.Max(0, r.EvenBand);
            double a = Math.Abs(log2) - band;
            if (!(a > 0)) return 0;
            return log2 < 0 ? -a : a;
        }

        /// <summary>The tiredness T = 1 − f (f clamped 0..1).</summary>
        public static double Tired(double f) => double.IsNaN(f) ? 0 : 1 - Math.Max(0, Math.Min(1, f));

        /// <summary>A length's multiplier: (1 + extra × T) × a roll in [1 − r T, 1 + r T] from the die <paramref name="u"/> in [0, 1).</summary>
        public static double Lengthen(double extra, double tired, double random, double u)
            => (1 + Math.Max(0, extra) * tired) * Spread(random, tired, u);

        /// <summary>The roll's factor: 1 + r T (2u − 1).</summary>
        public static double Spread(double random, double tired, double u) => 1 + Math.Max(0, random) * tired * (2 * u - 1);

        /// <summary>The pressure x on the defender and its parts (written into <paramref name="d"/>).</summary>
        public static double Pressure(in ImpactRules r, in ImpactFacts f, bool reeling, ref ImpactDecision d)
        {
            d.T = Tired(f.DefenderF);
            d.Bar = f.DefenderBar;
            d.BodyA = Body(in r, f.AttackerPoints);
            d.BodyD = Body(in r, f.DefenderPoints);
            d.AttackerSpeed = f.AttackerSpeed;
            d.Momentum = MomentumFactor(in r, f.AttackerSpeed, f.AttackerMounted, f.AttackerPassive);
            d.Impact = BlowImpact(in r, in f) * d.Momentum;
            d.Guard = Guard(in r, f.Event, f.GuardWeight);
            d.SkillA = SkillFactor(in r, f.AttackerSkill);
            d.SkillD = SkillFactor(in r, f.DefenderSkill);
            d.R = Math.Pow(d.BodyA / d.BodyD, r.BodyExponent) * (d.Impact * d.SkillA) / (d.Guard * d.SkillD);
            d.Log2R = d.R > 0 ? Math.Log(d.R, 2) : 0;
            d.MismatchPart = r.MismatchWeight * Band(in r, d.Log2R);
            d.Reeling = reeling;
            d.DamagePart = IsLanded(f.Event) && f.DefenderMaxHealth > 0 && f.Damage > 0
                ? r.DamageWeight * Math.Min(1, f.Damage / f.DefenderMaxHealth)
                : 0;
            // step 56 (Anton 2026-10-18: "right blocks a possible penalty for the ATTACKER"): on a correct block the blocker's own tiredness
            // counts only CorrectBlockTiredWeightPercent of itself - he is broken mainly by a big mismatch (the stick against the axe)
            d.TiredWeight = IsCorrectBlock(f.Event) ? Math.Max(0, Math.Min(1, r.CorrectBlockTiredWeight)) : 1;
            d.TiredPart = d.TiredWeight * d.T;
            d.X = d.TiredPart + d.MismatchPart + (reeling ? r.StaggeredBonus : 0) + d.DamagePart;
            return d.X;
        }

        /// <summary>Step 56: a CORRECT block - a right-side shield block or a weapon parry (the events that can riposte).</summary>
        public static bool IsCorrectBlock(ImpactEvent e) => CanRiposte(e);

        /// <summary>The riposte's pressure on the ATTACKER (IMPACT §3.6): x_r = T_A + w × band(log₂ R_r), R_r = (B_D ÷ B_A)^e × (G_D × K_D) ÷
        /// (I_A × K_A) - the defender's guard against the attacker's blow, the roles turned. Call after <see cref="Pressure"/>.</summary>
        public static double RipostePressure(in ImpactRules r, in ImpactFacts f, ref ImpactDecision d)
        {
            d.TAttacker = Tired(f.AttackerF);
            double rr = Math.Pow(d.BodyD / d.BodyA, r.BodyExponent) * (d.Guard * d.SkillD) / (d.Impact * d.SkillA);
            double log2 = rr > 0 ? Math.Log(rr, 2) : 0;
            d.XRiposte = d.TAttacker + r.MismatchWeight * Band(in r, log2);
            return d.XRiposte;
        }

        /// <summary>The ceilings of an event's row (0-1; 0 = the row has no such outcome - a kick's stagger is its reel, never rolled).</summary>
        public static void Ceilings(in ImpactRules r, ImpactEvent e, out double knockdown, out double shieldDrop, out double stagger)
        {
            switch (e)
            {
                case ImpactEvent.Kick:
                case ImpactEvent.Bash:
                    knockdown = r.KickKnockdown;
                    shieldDrop = r.KickShieldDrop;
                    stagger = 0;
                    return;
                case ImpactEvent.RightBlock:
                    knockdown = r.RightKnockdown;
                    shieldDrop = r.RightShieldDrop;
                    stagger = r.RightStagger;
                    return;
                case ImpactEvent.Parry:
                    knockdown = r.ParryKnockdown;
                    shieldDrop = 0;
                    stagger = r.ParryStagger;
                    return;
                case ImpactEvent.WrongBlock:
                    knockdown = r.WrongKnockdown;
                    shieldDrop = r.WrongShieldDrop;
                    stagger = r.WrongStagger;
                    return;
                default:
                    knockdown = r.HitKnockdown;
                    shieldDrop = 0;
                    stagger = r.HitStagger;
                    return;
            }
        }

        /// <summary>The chance of one outcome before the limits: ceiling × the curve from <paramref name="from"/>.</summary>
        public static double RawChance(double ceiling, double x, double from) => Math.Max(0, ceiling) * Curve(x, from);

        /// <summary>
        /// THE decision for one event (one per event - steps 50-52 apply it). Reads and WRITES both men's states (what happens - or in
        /// shadow mode what would - is remembered: reeling, immunities, the outcomes for the halving). The dice: one per rolled outcome,
        /// one per length.
        /// </summary>
        public static ImpactDecision Decide(in ImpactRules r, in ImpactFacts f, ref ImpactState defender, ref ImpactState attacker, double now, IRandomSource dice)
        {
            var d = new ImpactDecision
            {
                Event = f.Event,
                Now = now,
                Knockdown = ImpactRoll.None,
                ShieldDrop = ImpactRoll.None,
                Stagger = ImpactRoll.None,
                Riposte = ImpactRoll.None,
                XRiposte = double.NaN,
                TAttacker = double.NaN,
                RiposteBlockerFactor = double.NaN,
                StaggerSeconds = double.NaN,
                ReelFactor = double.NaN,
                GetUpFactor = double.NaN,
                StunFactor = double.NaN,
                RiposteStunSeconds = double.NaN,
                DefenderIsPlayer = f.DefenderIsPlayer,
                AttackerIsPlayer = f.AttackerIsPlayer,
                DefenderMounted = f.DefenderMounted,
            };
            bool reeling = defender.Reeling(now);
            double x = Pressure(in r, in f, reeling, ref d);
            Ceilings(in r, f.Event, out double kdCeil, out double sdCeil, out double stCeil);
            bool blockOutcomes = !IsBlock(f.Event) || r.Blocks; // step 51: blocks off, the riposte on = only the riposte is rolled
            if (!blockOutcomes) kdCeil = sdCeil = stCeil = 0;
            double scale = f.DefenderIsPlayer ? Math.Max(0, r.OnPlayer) : 1;
            int n = defender.Recent(now, r.DiminishWindow);
            double dim = n > 0 ? Math.Pow(r.DiminishFactor, n) : 1;
            bool down = defender.Down(now);

            // knockdown (a rider: knocked OFF, at the dismount share)
            if (kdCeil > 0)
            {
                double extra = f.DefenderMounted ? Math.Max(0, r.RiderDismount) : 1;
                d.Knockdown = Roll(kdCeil, RawChance(kdCeil, x, r.KnockdownFrom), n, dim * scale * extra,
                    down ? ImpactRefusal.Down : defender.KnockdownImmune(now) ? ImpactRefusal.KnockdownImmune : ImpactRefusal.None, dice);
                d.Dismount = f.DefenderMounted && d.Knockdown.Happens;
            }
            // shield dropped (needs a shield in his hand)
            if (sdCeil > 0)
                d.ShieldDrop = Roll(sdCeil, RawChance(sdCeil, x, r.ShieldDropFrom), n, dim * scale,
                    down ? ImpactRefusal.Down : !f.DefenderHasShield ? ImpactRefusal.NoShield : ImpactRefusal.None, dice);
            // stagger (never a rider; immune while reeling and after; a knockdown replaces it)
            if (stCeil > 0)
                d.Stagger = Roll(stCeil, RawChance(stCeil, x, r.StaggerFrom), n, dim * scale,
                    down ? ImpactRefusal.Down
                    : d.Knockdown.Happens ? ImpactRefusal.Replaced
                    : f.DefenderMounted ? ImpactRefusal.Rider
                    : defender.StaggerImmune(now) ? ImpactRefusal.StaggerImmune
                    : ImpactRefusal.None, dice);

            // the riposte: a right-side block or a parry, the roles turned (x_r = T_A + the mismatch turned), × (1 − k T_D) - step 56: k 0 by
            // default, so a tired man flailing into a good guard reels whoever holds it (step 48 was × f_D, k = 1)
            if (CanRiposte(f.Event) && r.RiposteOn && r.RiposteMax > 0 && !down)
            {
                double xr = RipostePressure(in r, in f, ref d);
                int na = attacker.Recent(now, r.DiminishWindow);
                double dimA = na > 0 ? Math.Pow(r.DiminishFactor, na) : 1;
                double scaleA = f.AttackerIsPlayer ? Math.Max(0, r.OnPlayer) : 1;
                double k = Math.Max(0, Math.Min(1, r.RiposteBlockerTired));
                d.RiposteBlockerFactor = 1 - k * d.T;
                d.Riposte = Roll(r.RiposteMax, RawChance(r.RiposteMax, xr, r.StaggerFrom) * d.RiposteBlockerFactor, na, dimA * scaleA,
                    attacker.Down(now) ? ImpactRefusal.Down
                    : d.Knockdown.Happens || d.Stagger.Happens ? ImpactRefusal.Replaced
                    : f.AttackerMounted ? ImpactRefusal.Rider
                    : attacker.StaggerImmune(now) ? ImpactRefusal.StaggerImmune
                    : ImpactRefusal.None, dice);
            }

            // lengths (the randomness grows with T) and the state
            double T = d.T;
            if (IsAlternative(f.Event) && !d.Knockdown.Happens)
            {
                d.ReelFactor = Math.Max(1, Lengthen(r.ReelExtra, T, r.Random, dice.NextDouble()));
                if (d.ReelFactor > 1 && !f.DefenderMounted) defender.Reel(now + VanillaReelSeconds * d.ReelFactor, r.StaggerImmunity); // a lengthened reel = an impact stagger
            }
            if (IsBlock(f.Event) && blockOutcomes)
                d.StunFactor = BlockStunFactor(in r, T); // step 51: no die - the decorator's worker-thread seam applies this very number
            if (d.Knockdown.Happens)
            {
                d.GetUpFactor = Math.Max(1, Math.Min(r.GetUpMax, Lengthen(r.GetUpMax - 1, T, r.Random, dice.NextDouble())));
                defender.KnockedDown(now, now + VanillaFallRiseSeconds * d.GetUpFactor, r.KnockdownImmunity);
                defender.NoteOutcome(now);
            }
            if (d.ShieldDrop.Happens) defender.NoteOutcome(now);
            if (d.Stagger.Happens)
            {
                d.StaggerSeconds = Math.Max(0, Math.Min(r.StaggerMax, r.StaggerBase * Lengthen(r.StaggerExtra, T, r.Random, dice.NextDouble())));
                defender.Reel(now + d.StaggerSeconds, r.StaggerImmunity);
                defender.NoteOutcome(now);
            }
            if (d.Riposte.Happens)
            {
                double ta = d.TAttacker;
                double s = r.RiposteStunMax * (0.5 + 0.5 * ta) * Spread(r.Random, ta, dice.NextDouble());
                d.RiposteStunSeconds = Math.Max(0, Math.Min(Math.Max(0, r.StunCap), s));
                attacker.Reel(now + d.RiposteStunSeconds, r.StaggerImmunity);
                attacker.NoteOutcome(now);
            }
            return d;
        }

        /// <summary>One outcome: the raw chance × the limits' factor (the halvings, the player's share, a rider's share), rolled unless refused.</summary>
        private static ImpactRoll Roll(double ceiling, double raw, int halvings, double factor, ImpactRefusal refusal, IRandomSource dice)
        {
            var o = new ImpactRoll
            {
                InRow = true,
                Ceiling = ceiling,
                Raw = raw,
                Halvings = halvings,
                Chance = Math.Max(0, Math.Min(1, raw * factor)),
                Die = double.NaN,
                Refusal = refusal,
            };
            if (refusal != ImpactRefusal.None || !(o.Chance > 0)) return o;
            o.Die = dice.NextDouble();
            o.Happens = o.Die < o.Chance;
            return o;
        }

        public static string EventName(ImpactEvent e) => e switch
        {
            ImpactEvent.Kick => "kick",
            ImpactEvent.Bash => "shield bash",
            ImpactEvent.RightBlock => "right-side block",
            ImpactEvent.Parry => "parry",
            ImpactEvent.WrongBlock => "wrong-side block",
            _ => "clean hit",
        };

        /// <summary>The plural the summary uses ("kicks", "right-side blocks" …).</summary>
        public static string EventPlural(ImpactEvent e) => e switch
        {
            ImpactEvent.Kick => "kicks",
            ImpactEvent.Bash => "shield bashes",
            ImpactEvent.RightBlock => "right-side blocks",
            ImpactEvent.Parry => "parries",
            ImpactEvent.WrongBlock => "wrong-side blocks",
            _ => "clean hits",
        };

        public static string OutcomeName(ImpactOutcome o) => o switch
        {
            ImpactOutcome.Knockdown => "knocked down",
            ImpactOutcome.ShieldDrop => "shield dropped",
            ImpactOutcome.Stagger => "staggered",
            _ => "riposte (the attacker staggered)",
        };

        public static string RefusalName(ImpactRefusal r) => r switch
        {
            ImpactRefusal.Down => "on the ground",
            ImpactRefusal.KnockdownImmune => "knockdown immunity",
            ImpactRefusal.StaggerImmune => "stagger immunity (reeling or just after)",
            ImpactRefusal.NoShield => "no shield in hand",
            ImpactRefusal.Rider => "a rider (never staggered)",
            ImpactRefusal.Replaced => "replaced (knocked down / staggered instead)",
            _ => "none",
        };

        /// <summary>"kicks, blocks" for a set of families.</summary>
        public static string FamiliesText(ImpactFamily f)
        {
            if (f == ImpactFamily.None) return "nothing";
            string s = string.Empty;
            if ((f & ImpactFamily.Kicks) != 0) s += "kicks / bashes";
            if ((f & ImpactFamily.Blocks) != 0) s += (s.Length > 0 ? ", " : string.Empty) + "blocks";
            if ((f & ImpactFamily.Hits) != 0) s += (s.Length > 0 ? ", " : string.Empty) + "clean hits";
            if ((f & ImpactFamily.Riposte) != 0) s += (s.Length > 0 ? ", " : string.Empty) + "the riposte";
            return s;
        }

        /// <summary>One decision in full (the first of each kind, the verbose lines): the inputs → x → each chance → its roll → the outcome.</summary>
        public static string Describe(in ImpactDecision d, in ImpactFacts f, string attacker, string defender, string mode)
        {
            var c = CultureInfo.InvariantCulture;
            string what = IsAlternative(d.Event) ? attacker + "'s " + EventName(d.Event) + " on " + defender
                : IsBlock(d.Event) ? defender + "'s " + EventName(d.Event) + " of " + attacker + "'s blow"
                : attacker + "'s " + EventName(d.Event) + " on " + defender;
            string s = what + " (" + mode + "): " + defender + " bar " + (100 * d.Bar).ToString("0", c) + "% (T " + d.T.ToString("0.00", c)
                       + (d.TiredWeight < 0.9999 ? ", counted x" + d.TiredWeight.ToString("0.00", c) + " on a correct block" : string.Empty) + ")"
                       + "; points " + f.AttackerPoints.ToString("0", c) + " vs " + f.DefenderPoints.ToString("0", c) + " (bodies " + d.BodyA.ToString("0", c) + " / " + d.BodyD.ToString("0", c) + ")"
                       + "; impact " + d.Impact.ToString("0.00", c) + BlowText(in f)
                       + (d.Momentum > 1.0001 ? " incl. momentum x" + d.Momentum.ToString("0.00", c) + " (" + (f.AttackerMounted ? "his horse at " : string.Empty) + d.AttackerSpeed.ToString("0.0", c) + " m/s into the blow)"
                          : f.AttackerMounted && f.AttackerPassive ? " (a couched lance - no momentum)" : string.Empty)
                       + " vs guard " + d.Guard.ToString("0.00", c) + " (" + GuardText(d.Event, f.GuardWeight) + ")"
                       + "; skill " + f.AttackerSkill.ToString("0", c) + " vs " + f.DefenderSkill.ToString("0", c) + " (x" + d.SkillA.ToString("0.00", c) + " / x" + d.SkillD.ToString("0.00", c) + ")"
                       + " → R " + d.R.ToString("0.00", c) + " (log2 " + d.Log2R.ToString("+0.00;-0.00", c) + ", " + d.MismatchPart.ToString("+0.00;-0.00", c) + " beyond the band)"
                       + (d.Reeling ? ", reeling +" : string.Empty) + (d.DamagePart > 0 ? ", damage +" + d.DamagePart.ToString("0.00", c) : string.Empty)
                       + " → x " + d.X.ToString("0.00", c) + ":";
            s += RollText(" knocked down" + (d.DefenderMounted ? " (off his horse)" : string.Empty), in d.Knockdown);
            s += RollText(" shield dropped", in d.ShieldDrop);
            s += RollText(" staggered", in d.Stagger);
            if (!double.IsNaN(d.XRiposte)) s += RollText(" riposte (x_r " + d.XRiposte.ToString("0.00", c)
                + (d.RiposteBlockerFactor < 0.9999 ? ", x" + d.RiposteBlockerFactor.ToString("0.00", c) + " for the blocker's tiredness" : string.Empty) + ")", in d.Riposte);
            if (!double.IsNaN(d.ReelFactor)) s += " reel x" + d.ReelFactor.ToString("0.00", c) + ";";
            if (!double.IsNaN(d.StunFactor)) s += " block stun x" + d.StunFactor.ToString("0.00", c) + ";";
            if (!double.IsNaN(d.GetUpFactor)) s += " gets up x" + d.GetUpFactor.ToString("0.00", c) + " slower;";
            if (!double.IsNaN(d.StaggerSeconds)) s += " stagger " + d.StaggerSeconds.ToString("0.00", c) + " s;";
            if (!double.IsNaN(d.RiposteStunSeconds)) s += " the attacker recoils " + d.RiposteStunSeconds.ToString("0.00", c) + " s;";
            return s.TrimEnd(';');
        }

        private static string BlowText(in ImpactFacts f)
        {
            var c = CultureInfo.InvariantCulture;
            return f.Event switch
            {
                ImpactEvent.Kick => " (a kick)",
                ImpactEvent.Bash => " (a bash, the shield " + f.WeaponWeight.ToString("0.0", c) + " kg)",
                _ => " (" + f.WeaponWeight.ToString("0.0", c) + " kg, speed " + f.SwingSpeed.ToString("0", c) + " x" + (f.AttackerAnimation > 0 ? f.AttackerAnimation : 1).ToString("0.00", c) + ", " + f.DamageType.ToString().ToLowerInvariant() + ")",
            };
        }

        private static string GuardText(ImpactEvent e, double kg)
        {
            string w = kg > 0 ? kg.ToString("0.0", CultureInfo.InvariantCulture) + " kg" : "weight unknown";
            return e switch
            {
                ImpactEvent.RightBlock => "shield, right side, " + w,
                ImpactEvent.WrongBlock => "shield, WRONG side, " + w,
                ImpactEvent.Parry => "parry, " + w,
                _ => "no guard",
            };
        }

        private static string RollText(string name, in ImpactRoll o)
        {
            if (!o.InRow) return string.Empty;
            var c = CultureInfo.InvariantCulture;
            string s = name + " " + (100 * o.Chance).ToString("0.#", c) + "%";
            if (o.Halvings > 0) s += " (cut for " + o.Halvings.ToString(c) + " recent)";
            if (o.Refusal != ImpactRefusal.None) return s + " - refused: " + RefusalName(o.Refusal) + ";";
            if (!o.Rolled) return s + ";";
            return s + " → rolled " + o.Die.ToString("0.00", c) + (o.Happens ? " YES;" : " no;");
        }
    }
}
