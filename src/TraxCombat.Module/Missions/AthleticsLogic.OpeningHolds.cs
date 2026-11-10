using System;
using System.Globalization;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 107 - HOLD-BLOCK OPENINGS (Toni 2026-11-10; AI_NOTES "Step 107", DESIGN decision 96). Only YOUR actions on an AI man on foot:
    ///   - ENTRY: your landed FRONT hit (the impact decision's clean hit - <see cref="HoldOnHit"/>), or a shieldman's correct SHIELD block of your attack
    ///     (<see cref="HoldOnBlock"/>) - he HOLDS a block in a random guard G (106b's forced guard, <see cref="OpeningForceKind.Hold"/>; not under the
    ///     1 s attack lock), the red arrow steady on the opening O; the first hold by your weapon's speed, a shieldman's short by his shield's size.
    ///   - DURING IT: your swing is judged when it COMMITS (106f's release read): a WINNING one (a sword exactly O, any other weapon any side G does not
    ///     block) takes his 'can defend' (and LOWERS a shield - GUARD BROKEN) until the blow resolves; landing = he staggers (106d's payoff through
    ///     <see cref="OpeningChain.HoldStagger"/>); into G (or into a raised shield) he blocks = a LINK (re-held longer; your next quick wind-up adds the
    ///     chain-speed bonus); a sword's third side lands as a normal hit = a fresh hold.
    ///   - THE HOLD RUNS OUT without a stagger: the counter-swing roll (30 % / 100 %) - forced through step 16's input hook (ready, then release).
    /// Every forced guard / flag / counter-swing is released on every exit (the hold's end, a stagger, his death / removal, mounted, the switches, a target
    /// switch, the mission end, an error). Main thread; one record, no allocation per tick (lines only for your fights).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly OpeningHoldStats _openHoldStats = new OpeningHoldStats();
        private OpeningHold _hold = OpeningHold.None;
        private bool _holdShield, _holdWound, _holdJudged, _holdFlight, _holdFirstLine;
        private HoldSwing _holdCls;
        private double _holdFlightUntil;
        private int _holdDir = OpeningMath.Unknown;

        // step 108: your weapon's mask at the hold's start (the "Unbalanced" words), the line queued for the tick, a counter-swing thrown after the hold
        private int _holdMask = OpeningMath.AllAttacks;
        private string? _holdText;
        private bool _holdCounterThrown;
        private double _holdEndedAt;

        /// <summary>The feed colour of the "Unbalanced ..." line (#RRGGBBAA - the red arrow's red).</summary>
        internal const string UnbalancedHex = "#F01E1EFF";

        // the forced counter-swing (one man at a time)
        private TrackedAgent? _counterSt;
        private bool _counterReady;
        private uint _counterFlag;
        private double _counterReleaseAt, _counterUntil;

        internal OpeningHoldStats OpeningHoldStats => _openHoldStats;

        /// <summary>The hold chain as it stands (the smoke reads it).</summary>
        internal OpeningHold OpeningHold => _hold;

        /// <summary>The man counter-swinging now (null none; the smoke reads it).</summary>
        internal TrackedAgent? OpeningCounterSwinger => _counterSt;

        internal bool OpeningCounterReady => _counterSt != null && _counterReady;

        /// <summary>The last swing judged in a hold (the smoke reads it).</summary>
        internal HoldSwing LastHoldSwing => _holdCls;

        /// <summary>Your weapon's speed (its swing speed; the thrust speed for a thrust-only weapon). The smoke's stand-in.</summary>
        internal Func<Agent, double> OpeningWeaponSpeedRead { get; set; } = GameWeaponSpeed;

        /// <summary>His shield's length (the engine's <c>WeaponLength</c>, cm; NaN = no shield in hand). The smoke's stand-in.</summary>
        internal Func<Agent, double> OpeningShieldLengthRead { get; set; } = GameShieldLength;

        /// <summary>Step 108: YOUR skill in the weapon you hold (One / Two Handed / Polearm, One Handed for a shield alone - the timed block's lookup,
        /// <see cref="GameImpactInputs.GuardSkillOf"/>); NaN = unread. The smoke's stand-in.</summary>
        internal Func<Agent, double> OpeningSkillRead { get; set; } = GameWeaponSkill;

        private static double GameWeaponSkill(Agent a) => GameImpactInputs.Skill(a, GameImpactInputs.GuardSkillOf(a.WieldedWeapon.CurrentUsageItem?.RelevantSkill));

        private double ReadSkill(Agent a)
        {
            try
            {
                return OpeningSkillRead(a);
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        private bool SafeGuardShown(TrackedAgent v, int guard)
        {
            try
            {
                return OpeningBody.GuardShown(v, guard);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static double GameWeaponSpeed(Agent a)
        {
            var w = a.WieldedWeapon;
            if (w.IsEmpty) return double.NaN;
            var u = w.CurrentUsageItem;
            if (u == null) return double.NaN;
            return u.SwingDamage > 0 ? u.SwingSpeed : u.ThrustSpeed;
        }

        private static double GameShieldLength(Agent a)
        {
            var i = a.GetOffhandWieldedItemIndex();
            if (i < TaleWorlds.Core.EquipmentIndex.WeaponItemBeginSlot || i >= TaleWorlds.Core.EquipmentIndex.NumAllWeaponSlots) return double.NaN;
            var w = a.Equipment[i];
            if (w.IsEmpty || w.CurrentUsageItem == null || !w.CurrentUsageItem.IsShield) return double.NaN;
            return w.CurrentUsageItem.WeaponLength;
        }

        private double ReadSpeed(Agent a)
        {
            try
            {
                return OpeningWeaponSpeedRead(a);
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        private double ReadShieldLength(Agent a)
        {
            try
            {
                return OpeningShieldLengthRead(a);
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>The holds are LIVE: their rules (the master switch first, <c>OpeningsEnabled</c>, <c>OpeningHoldsEnabled</c>) AND the openings live
        /// (decision 95 (m) - the impact system can carry the stagger).</summary>
        private bool HoldsLive(out OpeningHoldRules hr)
        {
            hr = OpeningHoldRules.From(TraxSettings.Shared);
            return hr.On && OpeningsLive(OpeningRules.From(TraxSettings.Shared));
        }

        // ------------------------------------------------------------------ the entry and the links

        /// <summary>
        /// From <see cref="OpeningShadowBlock"/>: a block of YOUR attack by an AI man on foot (a parry, a right- or wrong-side shield block). True = the
        /// holds took it (step 106's off balance skipped): a LINK when a hold is on him (or ran out within the link window); a shieldman's correct shield
        /// block = a fresh (short) hold; an unshielded man's parry with no hold = nothing (106's entry replaced).
        /// </summary>
        private bool HoldOnBlock(TrackedAgent a, TrackedAgent v, in ImpactFacts f, in ImpactDecision d, double now)
        {
            if (!HoldsLive(out var hr) || f.AttackerMounted || f.DefenderMounted) return false;
            try
            {
                bool shieldBlock = d.Event == ImpactEvent.RightBlock || d.Event == ImpactEvent.WrongBlock;
                CloseOpening(now);
                if (_openChain.Active && _openChain.Target != v.AgentIndex) EndChain(OpeningReset.TargetSwitch);
                if (_hold.LinkOpen(v.AgentIndex, now))
                {
                    bool after = _hold.Phase == HoldPhase.After;
                    NoteWinBlocked(v); // the smoke's direct block (in game OnMeleeHit's contact counted it first and closed the flight)
                    ClearFlight();
                    EndCounter(v, now, true, false);
                    bool wasRecovering = _hold.Phase == HoldPhase.Recovering;
                    double len = _hold.Link(now, in hr);
                    _holdCounterThrown = false;
                    if (wasRecovering) // step 108: his block shows his guard - the recovery is over, the link's clock from his block
                    {
                        _openHoldStats.NoteRecovery(now - _hold.HitAt, false);
                        QueueHoldShown(in hr);
                    }
                    _openHoldStats.Links++;
                    if (after) _openHoldStats.LinksAfter++;
                    _openHoldStats.NoteLength(len);
                    StartHoldForce(v, now, false);
                    OpeningLine("openings (hold-block): " + Name(v) + " BLOCKED your " + OpeningMath.AttackName(ContactSwing(a, now)) + (after ? " right after his hold ran out" : " into his held guard")
                                + " - LINK " + _hold.Links.ToString(CultureInfo.InvariantCulture) + ": held again " + F2(len) + " s from his block (the "
                                + OpeningMath.GuardName(_hold.Guard) + ", the arrow on your " + OpeningMath.AttackName(_hold.Opening) + "; your next wind-up within "
                                + F2(hr.BonusFull) + "-" + F2(hr.BonusZero) + " s adds up to " + F2(hr.BonusMax) + " s, at most " + F2(hr.Cap) + " s)");
                    return true;
                }
                if (shieldBlock || SafeShield(v))
                {
                    if (d.Event == ImpactEvent.RightBlock) BeginHold(a, v, now, true, true, in hr, "his shield blocked your attack");
                    return true; // a wrong-side shield block with no hold: nothing (the shield did its job)
                }
                _openHoldStats.BlocksNoHold++;
                return true;
            }
            catch (Exception e)
            {
                HoldFailed(e, now);
                return true;
            }
        }

        /// <summary>
        /// From <see cref="OpeningShadowHit"/> before step 106's chain: YOUR landed hit on an AI man on foot. True = the holds took it (<paramref name="staggers"/>:
        /// your winning strike - the caller runs 106d's stagger case on <see cref="_openChain"/>, set reeling here). False = step 106's chain decides (a hit
        /// from behind, the finish on a reeling man, the holds off).
        /// </summary>
        private bool HoldOnHit(TrackedAgent a, TrackedAgent v, in ImpactDecision d, double now, int swing, bool behind, bool immune, int mask, out bool staggers)
        {
            staggers = false;
            if (!HoldsLive(out var hr)) return false;
            try
            {
                bool onHim = _hold.Active && _hold.Target == v.AgentIndex;
                if (behind)
                {
                    if (onHim) EndHoldChain(HoldEnd.BackHit, now, true); // 106's back hit reels him (kept - Toni)
                    return false;
                }
                if (_openChain.Phase == OpeningPhase.Reeling && _openChain.Target == v.AgentIndex) return false; // the finish (106)
                if (_hold.Holding(v.AgentIndex))
                {
                    var cls = OpeningHoldMath.Classify(_hold.Guard, swing, mask, hr.SidesCrossed);
                    _holdCls = cls;
                    _openHoldStats.NoteSwing(cls, mask);
                    if (cls == HoldSwing.Wins)
                    {
                        var r = OpeningRules.From(TraxSettings.Shared);
                        int k = _hold.Links + 1;
                        double score = _hold.Score;
                        _openChain.HoldStagger(v.AgentIndex, now, in r, k, swing, mask, score);
                        _openHoldStats.Staggers[OpeningHoldMath.ClassIndex(mask)]++;
                        OpeningLine("openings (hold-block): your " + OpeningMath.AttackName(swing) + " WINS the hold on " + Name(v) + " (" + (_holdShield ? "shield" : "weapon") + " in the "
                                    + OpeningMath.GuardName(_hold.Guard) + ", link " + _hold.Links.ToString(CultureInfo.InvariantCulture) + ", your wind-up's speed " + F2(score) + ")");
                        EndHoldChain(HoldEnd.Staggered, now, true);
                        staggers = true;
                        return true;
                    }
                    if (cls == HoldSwing.IntoGuard) _openHoldStats.FreshAfterLeak++;
                    else if (cls == HoldSwing.ThirdSide) _openHoldStats.FreshAfterThirdSide++;
                    EndHoldChain(HoldEnd.FreshHold, now, false);
                    if (immune || d.Stagger.Happens || d.Knockdown.Happens)
                    {
                        _openHoldStats.Immune++;
                        EndHoldForce(v, now);
                        return true;
                    }
                    BeginHold(a, v, now, SafeShield(v), false, in hr, "your " + OpeningMath.AttackName(swing) + " landed - " + OpeningHoldMath.SwingName(cls) + ", a normal hit");
                    if (!_hold.Holding(v.AgentIndex)) EndHoldForce(v, now); // no fresh hold after all: the old force let go
                    return true;
                }
                if (immune || d.Stagger.Happens || d.Knockdown.Happens)
                {
                    _openHoldStats.Immune++;
                    return true; // he reels / falls from this hit or is still immune: no hold (the 106e rule)
                }
                if (onHim) EndHoldChain(HoldEnd.FreshHold, now, false);
                BeginHold(a, v, now, SafeShield(v), false, in hr, "your " + OpeningMath.AttackName(swing) + " landed");
                return true;
            }
            catch (Exception e)
            {
                HoldFailed(e, now);
                return true;
            }
        }

        /// <summary>A FRESH hold on <paramref name="v"/>: a random guard your weapon can open, the first hold (a shieldman's short by his shield), forced.</summary>
        private void BeginHold(TrackedAgent you, TrackedAgent v, double now, bool shield, bool byBlock, in OpeningHoldRules hr, string why)
        {
            if (!OpeningBody.OnFoot(v) || !OpeningBody.AiDriven(v)) return;
            int mask = ReadWeaponMask(you.Agent);
            int guard = OpeningMath.ChooseGuard(OpeningMath.Unknown, mask, hr.SidesCrossed, OpeningDice.NextDouble());
            if (!OpeningMath.IsGuard(guard)) return;
            double speed = ReadSpeed(you.Agent);
            double shieldLength = shield ? ReadShieldLength(v.Agent) : double.NaN;
            double skill = shield ? double.NaN : ReadSkill(you.Agent);
            double len = shield ? OpeningHoldMath.ShieldFirstHold(in hr, shieldLength, speed) : OpeningHoldMath.FirstHold(in hr, speed, skill);
            if (_hold.Active && _hold.Target != v.AgentIndex) EndHoldChain(HoldEnd.TargetSwitch, now, true);
            if (_openChain.Active && !(_openChain.Phase == OpeningPhase.Reeling && _openChain.Target == v.AgentIndex)) EndChain(OpeningReset.TargetSwitch);
            EndCounter(v, now, true, false);
            ClearFlight();
            _hold.Begin(v.AgentIndex, guard, hr.SidesCrossed, now, len, hr.RecoveryCap); // step 108: RECOVERING until his guard shows
            _holdMask = mask;
            _holdCounterThrown = false;
            _holdShield = shield;
            _holdWound = _holdJudged = false;
            _holdCls = HoldSwing.Unknown;
            _openHoldStats.Holds++;
            _openHoldStats.NoteLength(len);
            _openHoldStats.FactorSum += OpeningHoldMath.SpeedFactor(in hr, speed);
            _openHoldStats.FactorCount++;
            if (shield)
            {
                _openHoldStats.ShieldHolds++;
                _openHoldStats.ShieldFirstSum += len;
                if (byBlock) _openHoldStats.ShieldHoldsByBlock++;
            }
            StartHoldForce(v, now, false);
            bool recovering = _hold.Phase == HoldPhase.Recovering;
            if (recovering) _openHoldStats.Recoveries++;
            else QueueHoldShown(in hr); // the recovery cap 0: step 107 - the clock, the arrow and the words from the hit
            if (!_holdFirstLine)
            {
                _holdFirstLine = true;
                TraxLog.Info("impact", "openings: first HOLD-BLOCK at " + Sec(now) + " s - " + Name(v) + " - " + hr.Describe());
            }
            OpeningLine("openings (hold-block): " + why + " -> " + Name(v) + " HOLDS his " + (shield ? "shield" : "guard") + " in the " + OpeningMath.GuardName(guard) + " for "
                        + F2(len) + " s" + (recovering ? " FROM THE MOMENT IT SHOWS (his attacks held off and the guard pushed until then, at most " + F2(hr.RecoveryCap) + " s)" : " from now")
                        + " (" + (shield ? string.Empty : "your swing " + F2(hr.SwingSeconds) + " + reading margin " + F2(OpeningHoldMath.ReadMargin(in hr, skill)) + " at your skill "
                        + (double.IsNaN(skill) ? "?" : skill.ToString("0", CultureInfo.InvariantCulture)) + "; ")
                        + "your weapon's speed " + (double.IsNaN(speed) ? "?" : speed.ToString("0", CultureInfo.InvariantCulture)) + " -> x"
                        + F2(OpeningHoldMath.SpeedFactor(in hr, speed)) + (shield ? ", his shield " + (double.IsNaN(shieldLength) ? "?" : shieldLength.ToString("0", CultureInfo.InvariantCulture)) + " cm" : string.Empty)
                        + "); the red arrow on your " + OpeningMath.AttackName(_hold.Opening) + (recovering ? " once his guard shows" : string.Empty) + " - "
                        + (OpeningHoldMath.ExactSide(mask) ? "your sword must strike exactly there" : "any side but his guard wins (" + OpeningMath.MaskName(OpeningHoldMath.WinMask(guard, mask, hr.SidesCrossed)) + ")"));
        }

        /// <summary>His guard forced to the hold's G (<paramref name="lower"/>: every guard bit out - a shield lowered for your winning strike) until the
        /// hold ends (or your winning swing resolves). 106b's force record, kind <see cref="OpeningForceKind.Hold"/>.</summary>
        private void StartHoldForce(TrackedAgent v, double now, bool lower)
        {
            double until = HoldForceUntil();
            if (_forceKind == OpeningForceKind.Test && ReferenceEquals(_forceSt, v)) return; // the test tool has him
            StartForce(v, OpeningForceKind.Hold, _hold.Guard, lower, until, now);
        }

        private double HoldForceUntil() => Math.Max(_hold.Until, _holdFlight ? _holdFlightUntil : 0) + 0.05;

        private void EndHoldForce(TrackedAgent v, double now)
        {
            if (_forceKind == OpeningForceKind.Hold && ReferenceEquals(_forceSt, v)) EndForce(OpeningForceEnd.ChainEnded, now, !v.Removed);
        }

        /// <summary>The hold chain ends: counted, its force let go (<paramref name="releaseForce"/> false = the caller forces him again at once).</summary>
        private void EndHoldChain(HoldEnd why, double now, bool releaseForce)
        {
            if (!_hold.Active) return;
            if (_hold.Phase == HoldPhase.Recovering) _openHoldStats.RecoveryCut++; // step 108: ended before his guard showed
            _openHoldStats.NoteEnd(why);
            var t = GetByIndex(_hold.Target);
            _hold.Reset();
            ClearFlight();
            _holdWound = _holdJudged = false;
            if (!releaseForce || t == null || _forceKind != OpeningForceKind.Hold || !ReferenceEquals(_forceSt, t)) return;
            EndForce(why == HoldEnd.Staggered || why == HoldEnd.BackHit ? OpeningForceEnd.Reels : why == HoldEnd.Off ? OpeningForceEnd.Off : OpeningForceEnd.ChainEnded, now, !t.Removed);
        }

        private void ClearFlight()
        {
            _holdFlight = false;
            _holdFlightUntil = 0;
        }

        // ------------------------------------------------------------------ your swing during a hold

        /// <summary>From <see cref="OpeningWindUp"/>: your melee wind-up while he holds - the timing share, the chain-speed bonus (the first wind-up after a link).</summary>
        private void HoldWindUp(TrackedAgent you, double now)
        {
            if (!_hold.Active || !YouDrive(you)) return;
            if (_hold.Phase == HoldPhase.Recovering)
            {
                // step 108: a swing started before his guard shows is judged at its commit once the hold runs (no timing share - it was early)
                _holdWound = true;
                _holdJudged = false;
                return;
            }
            if (_hold.Phase != HoldPhase.Holding) return;
            try
            {
                if (!HoldsLive(out var hr)) return;
                bool firstAfterLink = !_hold.WindUpSeen && _hold.Links > 0;
                double before = _hold.Length;
                double bonus = _hold.NoteWindUp(now, in hr);
                if (firstAfterLink)
                {
                    _openHoldStats.NoteBonus(bonus, hr.BonusMax);
                    if (bonus > 0)
                    {
                        _openHoldStats.NoteLength(_hold.Length);
                        if (_forceKind == OpeningForceKind.Hold && _forceSt != null && _forceSt.AgentIndex == _hold.Target) _forceUntil = HoldForceUntil();
                    }
                    OpeningLine("openings (hold-block): your wind-up " + F2(now - _hold.Start) + " s after his block - chain-speed bonus +" + F2(bonus) + " s (hold "
                                + F2(before) + " -> " + F2(_hold.Length) + " s)");
                }
                _holdWound = true;
                _holdJudged = false;
                int committed = ReadCommitted(you.Agent);
                if (OpeningMath.IsAttack(committed)) JudgeHoldSwing(you, committed, now, in hr);
            }
            catch (Exception e)
            {
                HoldFailed(e, now);
            }
        }

        /// <summary>Your swing COMMITTED (its release) while he holds: a WINNING one takes his 'can defend' (and lowers his shield) until the blow resolves,
        /// at most <c>OpeningHoldMaxSeconds</c>; his guard held through it.</summary>
        private void JudgeHoldSwing(TrackedAgent you, int dir, double now, in OpeningHoldRules hr)
        {
            _holdJudged = true;
            _holdDir = dir;
            var v = GetByIndex(_hold.Target);
            if (v == null || v.Removed) return;
            int mask = ReadWeaponMask(you.Agent);
            _holdCls = OpeningHoldMath.Classify(_hold.Guard, dir, mask, hr.SidesCrossed);
            _holdFlight = true;
            _holdFlightUntil = now + hr.FlightMax;
            string extra = string.Empty;
            if (_holdCls == HoldSwing.Wins)
            {
                if (_holdShield)
                {
                    StartHoldForce(v, now, true);
                    _openHoldStats.ShieldLowered++;
                    NoteGuardBroken(); // step 106d's notice - his shield comes down for your winning strike
                    extra += ", his shield LOWERED";
                }
                if (TraxSettings.Shared.OpeningHoldTakesDefend && !_forceFlagOff && ReferenceEquals(_forceSt, v) && OpeningBody.CanDefend(v))
                {
                    OpeningBody.SetCanDefend(v, false);
                    _forceFlagOff = true;
                    _force.FlagsOff++;
                    _openHoldStats.FlagsOff++;
                    extra += ", his 'can defend' off until the blow";
                }
            }
            if (_forceKind == OpeningForceKind.Hold && ReferenceEquals(_forceSt, v)) _forceUntil = HoldForceUntil();
            OpeningLine("openings (hold-block): your " + OpeningMath.AttackName(dir) + " commits against " + Name(v) + "'s " + OpeningMath.GuardName(_hold.Guard) + " - "
                        + OpeningHoldMath.SwingName(_holdCls) + extra + "; his guard held through it");
        }

        /// <summary>Your blow resolved (its contact or a miss): the flight over, the flag given back, a lowered shield up again while the hold lasts.</summary>
        private void HoldBlowResolved(double now)
        {
            _holdWound = false;
            _holdJudged = false;
            if (!_holdFlight) return;
            ClearFlight();
            var v = _forceSt;
            if (v == null || _forceKind != OpeningForceKind.Hold || v.AgentIndex != _hold.Target) return;
            if (_forceFlagOff && !v.Removed) GiveFlagBack(v);
            if (_forceLower && _hold.Holding(v.AgentIndex)) StartHoldForce(v, now, false);
            else _forceUntil = HoldForceUntil();
        }

        /// <summary>From <see cref="OpeningContactSeen"/> (before step 106's part): your blow on ANOTHER man ends the hold chain; his blow landing on you too.</summary>
        private void HoldContact(TrackedAgent a, TrackedAgent v, bool landed, double now)
        {
            if (!_hold.Active && !_holdFlight) return;
            try
            {
                if (IsPlayer(v) && YouDrive(v))
                {
                    if (landed && _hold.Active && a.AgentIndex == _hold.Target)
                    {
                        NoteHitYou(a, now); // step 108: in which phase, by what (the next playtest's proof of pic 2)
                        EndHoldChain(HoldEnd.HeHitYou, now, true);
                    }
                    return;
                }
                if (!IsPlayer(a) || !YouDrive(a)) return;
                if (!landed) NoteWinBlocked(v); // step 107d: OnMeleeHit's contact comes BEFORE the block's impact decision - count it before the flight closes
                HoldBlowResolved(now);
                if (_hold.Active && v.AgentIndex != _hold.Target) EndHoldChain(HoldEnd.TargetSwitch, now, true);
            }
            catch (Exception e)
            {
                HoldFailed(e, now);
            }
        }

        /// <summary>Step 108: his blow landed on you during the chain - counted by phase (recovering / holding / after his forced counter-swing / after
        /// the hold on his own) with a line: what was forcing him, so the next playtest names what released his attack.</summary>
        private void NoteHitYou(TrackedAgent a, double now)
        {
            string phase;
            switch (_hold.Phase)
            {
                case HoldPhase.Recovering:
                    _openHoldStats.HitYouRecovering++;
                    phase = "while he RECOVERED from your hit (" + F2(now - _hold.HitAt) + " s after it, his guard not shown yet)";
                    break;
                case HoldPhase.Holding:
                    _openHoldStats.HitYouHolding++;
                    phase = "while he HELD his forced guard (" + F2(now - _hold.Start) + " s into a " + F2(_hold.Length) + " s hold)";
                    break;
                default:
                    if (_holdCounterThrown)
                    {
                        _openHoldStats.HitYouAfterCounter++;
                        phase = "after his hold ran out - his FORCED counter-swing";
                    }
                    else
                    {
                        _openHoldStats.HitYouAfterOwn++;
                        phase = "after his hold ran out - his OWN attack (no counter-swing thrown; " + F2(now - _holdEndedAt) + " s after the hold)";
                    }
                    break;
            }
            bool forced = _forceKind == OpeningForceKind.Hold && ReferenceEquals(_forceSt, a);
            OpeningLine("openings (hold-block): " + Name(a) + "'s blow LANDED on you " + phase + "; the forced guard " + (forced ? "ON" : "off")
                        + (ReferenceEquals(_counterSt, a) ? ", his counter-swing " + (_counterReady ? "READY" : "RELEASED") : string.Empty) + " - the chain ends");
        }

        /// <summary>Step 107d: a swing judged WINNING at its commit was stopped by him anyway (his parry / his shield - lowered too late?): counted once, a line.</summary>
        private void NoteWinBlocked(TrackedAgent v)
        {
            if (!_holdFlight || _holdCls != HoldSwing.Wins || !_hold.Active || v.AgentIndex != _hold.Target) return;
            _openHoldStats.WinBlocked++;
            OpeningLine("openings (hold-block): your WINNING swing was stopped by " + Name(v) + " anyway (" + (_holdShield ? (_forceLower ? "his shield was lowered" : "his shield") : "his weapon")
                        + ", 'can defend' " + (_forceFlagOff ? "off" : "on") + ") - a link");
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>From <see cref="TickOpenings"/> before the forced guard's tick: the switches, the man gone, your swing's commit, the hold's end (the
        /// counter-swing roll), the link window's end, the counter-swing's phases.</summary>
        private void TickHolds(double now)
        {
            TickCounter(now);
            ShowHoldText(now);
            if (!_hold.Active) return;
            try
            {
                bool live = HoldsLive(out var hr);
                var v = GetByIndex(_hold.Target);
                if (v == null || v.Removed)
                {
                    EndHoldChain(HoldEnd.Off, now, false);
                    return;
                }
                if (!live || !OpeningBody.OnFoot(v) || !OpeningBody.AiDriven(v))
                {
                    EndHoldChain(HoldEnd.Off, now, true);
                    return;
                }
                if (_hold.Phase == HoldPhase.Recovering) // step 108: his guard pushed until it SHOWS - then the hold's clock, the arrow, the words
                {
                    bool visible = SafeGuardShown(v, _hold.Guard);
                    if (OpeningHoldMath.RecoveryOver(in hr, _hold.HitAt, now, visible)) HoldGuardShown(v, now, !visible, in hr);
                    return;
                }
                if (_hold.Phase == HoldPhase.After)
                {
                    if (_hold.Lapsed(now))
                    {
                        _openHoldStats.NoteEnd(HoldEnd.Gap);
                        _hold.Reset();
                    }
                    return;
                }
                if (_holdWound && !_holdJudged)
                {
                    var you = PlayerRecord();
                    if (you != null && !you.Removed && YouDrive(you))
                    {
                        int dir = ReadCommitted(you.Agent);
                        if (OpeningMath.IsAttack(dir)) JudgeHoldSwing(you, dir, now, in hr);
                    }
                }
                if (_holdFlight && now > _holdFlightUntil) HoldBlowResolved(now);
                if (_hold.HoldOver(now) && !_holdFlight) HoldRanOut(v, now, in hr);
            }
            catch (Exception e)
            {
                HoldFailed(e, now);
            }
        }

        /// <summary>Step 108: his guard SHOWS (or the recovery cap ran out - <paramref name="capped"/>): the hold's clock starts now; the arrow and the
        /// "Unbalanced" line from here.</summary>
        private void HoldGuardShown(TrackedAgent v, double now, bool capped, in OpeningHoldRules hr)
        {
            _hold.GuardShown(now, capped);
            _openHoldStats.NoteRecovery(_hold.RecoverySeconds, capped);
            if (_forceKind == OpeningForceKind.Hold && ReferenceEquals(_forceSt, v)) _forceUntil = HoldForceUntil();
            QueueHoldShown(in hr);
            OpeningLine("openings (hold-block): " + Name(v) + "'s " + (_holdShield ? "shield" : "guard") + " in the " + OpeningMath.GuardName(_hold.Guard)
                        + (capped ? " NOT seen within " + F2(hr.RecoveryCap) + " s of your hit (the recovery cap) - the hold's clock starts anyway: "
                                  : " is VISIBLE " + F2(_hold.RecoverySeconds) + " s after your hit - the hold's clock starts: ")
                        + F2(_hold.Length) + " s; " + (_holdText != null ? "'" + _holdText + "'" : "no words (OpeningHoldMessage off)") + ", the red arrow on your "
                        + OpeningMath.AttackName(_hold.Opening) + (HoldArrowOn() ? string.Empty : " OFF (OpeningHoldArrowShow)"));
        }

        /// <summary>Step 108: the hold has started (his guard visible): the arrow counted, the "Unbalanced" line queued for the tick.</summary>
        private void QueueHoldShown(in OpeningHoldRules hr)
        {
            if (HoldArrowOn()) _openHoldStats.ArrowShown++;
            else _openHoldStats.ArrowOff++;
            _holdText = hr.Message ? OpeningHoldMath.UnbalancedText(_hold.Guard, _holdMask, hr.SidesCrossed) : null;
        }

        private static bool HoldArrowOn() => TraxSettings.Shared.OpeningHoldArrowShow && OpeningRules.From(TraxSettings.Shared).ArrowShow;

        /// <summary>Step 108: the queued "Unbalanced ..." line in the feed (from the tick, never a callback), while the hold lasts.</summary>
        private void ShowHoldText(double now)
        {
            var text = _holdText;
            if (text == null) return;
            _holdText = null;
            if (!_hold.Active || !TraxSettings.Shared.OpeningHoldMessage) return;
            try
            {
                MessageSink.Show(text, UnbalancedHex);
                _openHoldStats.MessagesShown++;
            }
            catch (Exception e)
            {
                HoldFailed(e, now);
            }
        }

        /// <summary>The hold ran out without a stagger: the AFTER phase (a block of yours is still a link), the force let go, the counter-swing rolled.</summary>
        private void HoldRanOut(TrackedAgent v, double now, in OpeningHoldRules hr)
        {
            int links = _hold.Links;
            _openHoldStats.NoteEnd(HoldEnd.Timeout);
            _hold.EndHold(now, in hr);
            _holdWound = _holdJudged = false;
            if (_forceKind == OpeningForceKind.Hold && ReferenceEquals(_forceSt, v)) EndForce(OpeningForceEnd.Timeout, now, true);
            double chance = OpeningHoldMath.CounterChance(in hr, links);
            double die = OpeningDice.NextDouble();
            bool thrown = OpeningHoldMath.CounterThrown(die, chance);
            _holdCounterThrown = thrown;
            _holdEndedAt = now;
            if (links > 0)
            {
                _openHoldStats.CounterRolledLater++;
                if (thrown) _openHoldStats.CounterThrownLater++;
            }
            else
            {
                _openHoldStats.CounterRolledFirst++;
                if (thrown) _openHoldStats.CounterThrownFirst++;
            }
            string what = thrown ? StartCounter(v, now) : "no counter-swing";
            OpeningLine("openings (hold-block): " + Name(v) + "'s hold ran out (" + (links > 0 ? "lengthened by " + links.ToString(CultureInfo.InvariantCulture) + " link(s)" : "the first hold")
                        + ") - counter-swing chance " + Pct0(chance) + ": " + what + "; your block within " + F2(hr.LinkWindow) + " s is still a link");
        }

        // ------------------------------------------------------------------ the forced counter-swing

        private string StartCounter(TrackedAgent v, double now)
        {
            if (v.Removed || !OpeningBody.OnFoot(v) || !OpeningBody.AiDriven(v))
            {
                _openHoldStats.CounterRefused++;
                return "refused (not his to drive)";
            }
            if (_counterSt != null) EndCounter(_counterSt, now, true, true);
            int dir = OpeningHoldMath.CounterDirection(ReadWeaponMask(v.Agent), OpeningDice.NextDouble());
            _counterSt = v;
            _counterFlag = OpeningHoldMath.AttackFlag(dir);
            _counterReady = true;
            _counterReleaseAt = now + OpeningHoldMath.CounterReadySeconds;
            _counterUntil = _counterReleaseAt + OpeningHoldMath.CounterReleaseSeconds;
            EnsureInput(v);
            AiInputHook.SetCounter(v, true, _counterFlag, true, now);
            NoteHooked(v, OpeningBody.Hook(v));
            return "he COUNTER-SWINGS - his " + OpeningMath.AttackName(dir) + " (ready " + F2(OpeningHoldMath.CounterReadySeconds) + " s, then released) - time your block";
        }

        private void TickCounter(double now)
        {
            var st = _counterSt;
            if (st == null) return;
            try
            {
                if (st.Removed)
                {
                    EndCounter(st, now, false, true);
                    return;
                }
                if (!HoldsLive(out _) || !OpeningBody.OnFoot(st) || !OpeningBody.AiDriven(st) || st.Impact.Reeling(now) || st.Impact.Down(now))
                {
                    EndCounter(st, now, true, true);
                    return;
                }
                if (_counterReady && now >= _counterReleaseAt)
                {
                    _counterReady = false;
                    AiInputHook.SetCounter(st, true, _counterFlag, false, now);
                    _openHoldStats.CounterReleased++;
                }
                if (now >= _counterUntil) EndCounter(st, now, true, false);
            }
            catch (Exception e)
            {
                HoldFailed(e, now);
            }
        }

        /// <summary>The counter-swing let go (<paramref name="engine"/> false = he left the field; <paramref name="cut"/> = before its end).</summary>
        private void EndCounter(TrackedAgent? only, double now, bool engine, bool cut)
        {
            var st = _counterSt;
            if (st == null || (only != null && !ReferenceEquals(only, st))) return;
            _counterSt = null;
            _counterReady = false;
            if (cut) _openHoldStats.CounterCut++;
            try
            {
                AiInputHook.SetCounter(st, false, 0u, false, now);
                if (engine && !st.Removed) OpeningBody.Release(st);
            }
            catch (Exception e)
            {
                _openHoldStats.Errors++;
                if (_openHoldStats.Errors == 1) Failed("impact.opening-counter", e);
            }
        }

        // ------------------------------------------------------------------ the arrow, the end, the summary

        /// <summary>The hold's opening for the red arrow (attack bits), 0 = none.</summary>
        private int HoldArrowMask() =>
            _hold.Phase == HoldPhase.Holding && OpeningMath.IsAttack(_hold.Opening) && TraxSettings.Shared.OpeningHoldArrowShow ? OpeningMath.Bit(_hold.Opening) : 0; // step 108: never while recovering; its switch

        /// <summary>The mission's end (before the summary) / an error: every hold, force, flag and counter-swing let go.</summary>
        private void CloseHolds(double now)
        {
            EndHoldChain(HoldEnd.Off, now, true);
            if (_counterSt != null) EndCounter(_counterSt, now, true, true);
        }

        private void HoldFailed(Exception e, double now)
        {
            _openHoldStats.Errors++;
            if (_openHoldStats.Errors == 1) Failed("impact.opening-holds", e);
            try
            {
                CloseHolds(now);
            }
            catch (Exception)
            {
                _hold.Reset();
            }
        }

        private void WriteOpeningHoldSummary() => TraxLog.Info("summary", _openHoldStats.SummaryLine(OpeningHoldRules.From(TraxSettings.Shared)));
    }
}
