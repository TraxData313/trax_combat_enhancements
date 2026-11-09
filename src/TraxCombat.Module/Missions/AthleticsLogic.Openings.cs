using System;
using System.Globalization;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 106a / 106a2 - OPENINGS, THE SHADOW of Toni's REVISED design (2026-11-09; AI_NOTES "Step 106", DESIGN decision 95). NOTHING is
    /// applied. Core <see cref="OpeningChain"/> runs on YOUR fights with AI men on foot:
    ///   - a correct block (right-side shield / parry) of your melee attack - the impact decision's block seam, beside
    ///     <see cref="TimedBlockDecided"/> - is the chain's next link: OFF BALANCE, the balance guard planned by <see cref="OpeningMath.Plan"/>
    ///     (a shield below <c>OpeningShieldMinChain</c> still holds the side);
    ///   - your next melee WIND-UP (step 13's phases - <see cref="OpeningWindUp"/>) is judged: its direction (into the balance side or not) and its
    ///     TIMING SCORE around the flash (<see cref="OpeningMath.TimingScore"/>);
    ///   - a follow-up into the side that he blocks correctly anyway gets the SHADOW BLOCK ROLL (<see cref="OpeningMath.BlockChance"/> by its score,
    ///     its link, the shield) - "would it have got through" - and the chain grows;
    ///   - your LANDED hit (the impact decision's clean hit - <see cref="OpeningShadowHit"/>): off balance or from behind = the would-be STAGGER (the
    ///     reel by link, the PERFECT STRIKE side = the opposite of that swing, by the weapon), a front hit on a man not guarding = off balance (the
    ///     longer window), a hit on the reeling man = the would-be PERFECT STRIKE (x by the weapon's directions and the follow-up's score) or step
    ///     105's other side; the mod's own stagger on the same hit counted (it would just show the arrow);
    ///   - the chain's resets (the reel over, the gap, his hit on you, another target, a miss by the setting).
    /// Also counted: your attack's direction × his guard read at contact - the engine's SIDE PAIRING (<c>OpeningSidesCrossed</c>), and AI vs AI
    /// correct blocks on foot. Master switch first (through <see cref="OpeningRules.ShadowOn"/>): off = the chain dropped at the next tick. Main
    /// thread; one record, no allocation per tick (lines only for your fights, the first <see cref="OpeningFullLines"/> at Info).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>Your openings logged at Info per mission; the rest verbose.</summary>
        internal const int OpeningFullLines = 40;

        private readonly OpeningStats _open = new OpeningStats();
        private OpeningChain _openChain = OpeningChain.None;
        private OpeningPlan _openPlan;
        private bool _openPending, _openWound, _openJudged;
        private int _openTarget = -1, _openWindUpDir = OpeningMath.Unknown, _openDir = OpeningMath.Unknown, _openLines, _openStaggerMask = OpeningMath.AllAttacks;
        private double _openBlockAt, _openWindUpAt, _openScore;

        /// <summary>Step 106e: the landed hit being decided - its collision's direction (ImpactDecideEvent), the attacker, the time.</summary>
        private int _hitSwing = OpeningMath.Unknown;
        private TrackedAgent? _hitSwingBy;
        private double _hitSwingAt = double.NaN;
        private OpeningVerdict _openVerdict;

        internal OpeningStats OpeningStats => _open;

        /// <summary>The chain as it stands (the smoke reads it).</summary>
        internal OpeningChain OpeningChain => _openChain;

        /// <summary>The last off-balance state planned (the smoke reads it).</summary>
        internal OpeningPlan LastOpening { get; private set; }

        /// <summary>The last direction verdict on a follow-up (the smoke reads it).</summary>
        internal OpeningVerdict LastOpeningVerdict { get; private set; }

        /// <summary>The last follow-up's timing score (the smoke reads it).</summary>
        internal double LastOpeningScore { get; private set; }

        /// <summary>The last landed hit's verdict (the smoke reads it).</summary>
        internal OpeningHit LastOpeningHit { get; private set; }

        /// <summary>The last would-be perfect strike's multiplier (the smoke reads it; NaN = none yet).</summary>
        internal double LastOpeningMultiplier { get; private set; } = double.NaN;

        /// <summary>A follow-up is awaited for the off-balance state (the smoke reads it).</summary>
        internal bool OpeningPending => _openPending;

        /// <summary>The openings' dice (the balance guard, the shadow block roll); the smoke seeds its own.</summary>
        internal IRandomSource OpeningDice { get; set; } = ThreadSafeRandom.Shared;

        /// <summary>His guard at contact - channel 1's action direction (DefendUp 4 .. DefendRight 7; anything else = unread / not guarding). The smoke's stand-in.</summary>
        internal Func<Agent, int> OpeningGuardRead { get; set; } = GameActionDirection;

        /// <summary>Your swing's direction at its wind-up - channel 1's action direction (AttackUp 0 .. AttackRight 3), the engine's
        /// <c>AttackDirection</c> when that is not an attack. The smoke's stand-in.</summary>
        internal Func<Agent, int> OpeningSwingRead { get; set; } = GameSwingDirection;

        /// <summary>The attacks your wielded weapon can make (<see cref="OpeningMath.WeaponMask"/>). The smoke's stand-in.</summary>
        internal Func<Agent, int> OpeningWeaponRead { get; set; } = GameWeaponMask;

        /// <summary>
        /// Step 106f: the direction your swing is COMMITTED to - channel 1's direction while it is a melee RELEASE (the engine's ready can still
        /// change: Toni's first playtest read the wind-up's direction wrong 14 times in 33), else <see cref="OpeningMath.Unknown"/> (not released
        /// yet). The smoke's stand-in.
        /// </summary>
        internal Func<Agent, int> OpeningCommitRead { get; set; } = GameCommittedDirection;

        private static int GameActionDirection(Agent a) => (int)a.GetCurrentActionDirection(1);

        private static int GameCommittedDirection(Agent a)
        {
            if (a.GetCurrentActionType(1) != Agent.ActionCodeType.ReleaseMelee) return OpeningMath.Unknown;
            int d = (int)a.GetCurrentActionDirection(1);
            return OpeningMath.IsAttack(d) ? d : (int)a.AttackDirection;
        }

        private int ReadCommitted(Agent a)
        {
            try
            {
                int d = OpeningCommitRead(a);
                return OpeningMath.IsAttack(d) ? d : OpeningMath.Unknown;
            }
            catch (Exception)
            {
                return OpeningMath.Unknown;
            }
        }

        /// <summary>Step 106f: this blow's collision direction when the impact decision just noted it for <paramref name="a"/>, else unknown.</summary>
        private int ContactSwing(TrackedAgent a, double now) =>
            ReferenceEquals(_hitSwingBy, a) && Math.Abs(_hitSwingAt - now) < 1e-6 && OpeningMath.IsAttack(_hitSwing) ? _hitSwing : OpeningMath.Unknown;

        private static int GameSwingDirection(Agent a)
        {
            int d = (int)a.GetCurrentActionDirection(1);
            return OpeningMath.IsAttack(d) ? d : (int)a.AttackDirection;
        }

        private static int GameWeaponMask(Agent a)
        {
            var w = a.WieldedWeapon;
            if (w.IsEmpty) return OpeningMath.AllAttacks;
            var u = w.CurrentUsageItem;
            if (u == null) return OpeningMath.AllAttacks;
            return OpeningMath.WeaponMask(u.SwingDamage > 0, u.ThrustDamage > 0);
        }

        /// <summary>From StartImpactModel: the mission-start line.</summary>
        private void StartOpenings()
        {
            _openChain = OpeningChain.None;
            _openPending = false;
            var or = OpeningRules.From(TraxSettings.Shared);
            if (or.Enabled && !OpeningsLive(in or)) // step 106e
                TraxLog.Info("impact", "openings: SHADOW this battle although OpeningsEnabled is on - the impact system cannot carry the stagger ("
                                       + ImpactRules.From(TraxSettings.Shared).ModeText() + "; it needs the riposte live and the hits decided): no forced guard, no flash, counted only");
            TraxLog.Info("impact", "mission start: " + OpeningRules.From(TraxSettings.Shared).Describe()
                                   + " - step 106d: with the live switch the off-balance man's guard is FORCED to the balance side and rolled at your follow-up's wind-up (a shield LOWERED from its 2nd block), your landed hit STAGGERS him (ReelCause.Opening) and opens the PERFECT STRIKE side for the reel; the [summary] \"openings\" lines (PLAYTEST D77 - D80)");
        }

        private int ReadWeaponMask(Agent a)
        {
            try
            {
                return OpeningWeaponRead(a);
            }
            catch (Exception)
            {
                return OpeningMath.AllAttacks;
            }
        }

        private int ReadGuard(Agent v)
        {
            int g = OpeningMath.Unknown;
            try
            {
                g = OpeningGuardRead(v);
            }
            catch (Exception)
            {
                _open.GuardReadFailed++;
            }
            return OpeningMath.IsGuard(g) ? g : OpeningMath.Unknown;
        }

        // ------------------------------------------------------------------ the block (the impact decision's seam)

        /// <summary>
        /// From <see cref="ImpactDecide"/> right after <see cref="TimedBlockDecided"/>: a decided block. A correct block of YOUR attack by an AI man =
        /// the shadow block roll for a follow-up into the side, then the chain's next link (off balance); AI vs AI on foot counted. Its own try.
        /// </summary>
        private void OpeningShadowBlock(TrackedAgent a, TrackedAgent v, in ImpactFacts f, in ImpactDecision d, double now)
        {
            if (!ImpactMath.CanRiposte(d.Event))
            {
                // step 106f: a held follow-up stopped by his shield on the WRONG side is blocked anyway too (the shield did not come down)
                if (d.Event == ImpactEvent.WrongBlock && _openLiveRolled && f.AttackerIsPlayer && !f.DefenderIsPlayer && v.AgentIndex == _openTarget && YouDrive(a))
                {
                    try
                    {
                        OpeningLine("openings: your follow-up hit his shield on the wrong side" + ForceBlockedText(v, ContactSwing(a, now), true, ReadGuard(v.Agent), now));
                    }
                    catch (Exception e)
                    {
                        OpeningFailed(e);
                    }
                }
                return;
            }
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.Runs) return;
                bool you = f.AttackerIsPlayer && YouDrive(a);
                if (!you)
                {
                    if (!f.AttackerIsPlayer && !f.DefenderIsPlayer && !f.AttackerMounted && !f.DefenderMounted) _open.AiVsAi++;
                    return;
                }
                if (f.DefenderIsPlayer) return;
                string rolled = string.Empty;
                bool shield = d.Event == ImpactEvent.RightBlock;
                int guardAtContact = ReadGuard(v.Agent);
                int yourAttack = !double.IsNaN(a.RecoilDirectionAt) && Math.Abs(a.RecoilDirectionAt - now) < 1e-6 ? a.RecoilDirection : OpeningMath.Unknown;
                if (_openLiveRolled) rolled = ForceBlockedText(v, yourAttack, shield, guardAtContact, now); // step 106b: the live roll at your follow-up decided it
                else if (_openPending && _openJudged && _openTarget == v.AgentIndex && _openVerdict == OpeningVerdict.Opened)
                {
                    // THE SHADOW BLOCK ROLL: he blocked your follow-up into the side - would the revised design have let it through?
                    double chance = OpeningMath.BlockChance(in r, _openScore, _openPlan.K, _openPlan.ShieldHolds);
                    double die = OpeningDice.NextDouble();
                    bool through = die >= chance;
                    _open.NoteBlockRoll(chance, through, _openPlan.K);
                    rolled = " - your follow-up (timing " + F2(_openScore) + ") vs his block chance " + Pct0(chance) + (_openPlan.ShieldHolds ? " (his shield still holds)" : string.Empty)
                             + ": " + (through ? "WOULD have got through (his guard forced to the " + OpeningMath.GuardName(_openPlan.Guard) + ")" : "he would still block it");
                }
                CloseOpening(now);
                int k = _openChain.NoteBlock(v.AgentIndex, now, shield, in r, out var reset);
                _open.NoteReset(reset);
                _open.NotePair(yourAttack, guardAtContact);
                int mask = ReadWeaponMask(a.Agent);
                var plan = OpeningMath.Plan(in r, k, shield ? OpeningEntry.ShieldBlocked : OpeningEntry.Blocked, f.AttackerSkill, f.AttackerMounted || f.DefenderMounted,
                    guardAtContact, yourAttack, mask, OpeningDice);
                LastOpening = plan;
                _open.NoteBlock(in plan);
                if (plan.Opens)
                {
                    BeginFollowUp(v.AgentIndex, in plan, now);
                    ForceBalance(v, in plan, now); // step 106b: live - his guard to the balance side (its own try)
                }
                OpeningLine("openings (shadow): YOUR " + OpeningMath.AttackName(yourAttack) + " blocked by " + Name(v) + " (" + (shield ? "shield" : "weapon") + ", his "
                            + OpeningMath.GuardName(guardAtContact) + " at contact) at " + Sec(now) + " s - link " + plan.K.ToString(CultureInfo.InvariantCulture)
                            + (reset != OpeningReset.None ? " (the chain restarted: " + OpeningMath.ResetName(reset) + ")" : string.Empty) + rolled + ": "
                            + (plan.Opens
                                ? "OFF BALANCE to the " + OpeningMath.GuardName(plan.Guard) + " (of " + plan.Candidates.ToString(CultureInfo.InvariantCulture) + ") -> the arrow would flash "
                                  + OpeningMath.ArrowName(plan.Arrow) + " (your " + OpeningMath.AttackName(plan.Opening) + ") at " + F2(plan.Flash) + " s, perfect +/-" + F2(plan.Perfect)
                                  + " s (skill " + SkillText(f.AttackerSkill) + "), the window to " + F2(plan.Flash + plan.Window) + " s"
                                  + (plan.ShieldHolds ? "; his shield still holds that side at link " + plan.K.ToString(CultureInfo.InvariantCulture) : string.Empty)
                                  + "; a landed hit would reel him " + F2(plan.Reel) + " s"
                                : "no balance side (" + SkipWords(plan.Skip) + ")"));
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        private void BeginFollowUp(int target, in OpeningPlan plan, double now)
        {
            _openLiveRolled = false; // step 106b: a live roll belongs to one follow-up
            _openPending = true;
            _openWound = false;
            _openJudged = false; // step 106f
            _openDir = OpeningMath.Unknown;
            _openTarget = target;
            _openPlan = plan;
            _openBlockAt = now;
            _openWindUpAt = double.NaN;
            _openWindUpDir = OpeningMath.Unknown;
            _openScore = 0;
            NoteArrowFlash(plan.Opening, now + plan.Flash, false); // step 106c: the red arrow flashes the side (shown with OpeningsEnabled)
        }

        private static string SkillText(double skill) => double.IsNaN(skill) ? "?" : skill.ToString("0", CultureInfo.InvariantCulture);

        private static string SkipWords(OpeningSkip s) => s switch
        {
            OpeningSkip.Mounted => "a rider - on foot only",
            OpeningSkip.NoOpeningForWeapon => "no other guard opens an attack your weapon can make",
            _ => "-",
        };

        // ------------------------------------------------------------------ your next swing

        /// <summary>
        /// Your melee WIND-UP began (step 13's phases): the chain's gap checked, and the awaited follow-up judged - its direction and its timing
        /// score around the flash (one read). Its own try.
        /// </summary>
        internal void OpeningWindUp(TrackedAgent st, double now)
        {
            if (!_openPending && !_openChain.Active) return;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.Runs) return;
                if (_openChain.Expired(now, in r))
                {
                    CloseOpening(now);
                    EndChain(_openChain.Phase == OpeningPhase.Reeling ? OpeningReset.ReelOver : OpeningReset.Gap);
                    return;
                }
                // step 106f: a NEW wind-up before the last one was released (a feint, a re-ready) re-times the follow-up
                if (!_openPending || _openJudged || !YouDrive(st)) return;
                int dir = ReadSwing(st.Agent);
                _openWound = true;
                _openWindUpAt = now;
                _openWindUpDir = dir;
                double since = now - _openBlockAt;
                _openScore = OpeningMath.TimingScore(in r, in _openPlan, since);
                LastOpeningScore = _openScore;
                // step 106f: the direction is judged once the swing COMMITS (its release - TickOpenings), not from the ready's first read
                int committed = ReadCommitted(st.Agent);
                if (OpeningMath.IsAttack(committed)) JudgeFollowUp(st, committed, OpeningJudgedAt.WindUp, now);
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        /// <summary>
        /// Step 106f: the awaited follow-up's DIRECTION judged - <paramref name="dir"/> the swing it committed to (its release; the wind-up when it was
        /// already released there; the contact's collision when no release was seen first). The verdict, the follow-up's counts, then the LIVE
        /// block roll (not at the contact - the blow has met him already). Main thread.
        /// </summary>
        private void JudgeFollowUp(TrackedAgent you, int dir, OpeningJudgedAt at, double now)
        {
            if (!_openPending || !_openWound || _openJudged) return;
            var r = OpeningRules.From(TraxSettings.Shared);
            _openJudged = true;
            _openDir = dir;
            double since = _openWindUpAt - _openBlockAt;
            _openVerdict = OpeningMath.Verdict(in _openPlan, since, dir);
            LastOpeningVerdict = _openVerdict;
            _open.NoteFollowUp(_openVerdict, _openPlan.K, since, _openScore);
            _open.NoteJudged(at, _openWindUpDir, dir);
            string live = at == OpeningJudgedAt.Contact ? " - judged only at the contact (no release seen first): no live roll" : ForceAtCommit(you, now); // step 106b: the LIVE block roll (its own try)
            OpeningLine("openings (shadow): your follow-up - " + OpeningMath.AttackName(dir) + " (judged at the " + JudgedWords(at) + (_openWindUpDir != dir ? "; the wind-up first read "
                        + OpeningMath.AttackName(_openWindUpDir) : string.Empty) + ") started " + F2(since) + " s after the link (flash " + F2(_openPlan.Flash)
                        + " s, perfect +/-" + F2(_openPlan.Perfect) + ", window to " + F2(_openPlan.Flash + _openPlan.Window) + " s; the side: "
                        + OpeningMath.AttackName(_openPlan.Opening) + ", link " + _openPlan.K.ToString(CultureInfo.InvariantCulture) + "): "
                        + OpeningMath.VerdictName(_openVerdict) + ", timing " + F2(_openScore)
                        + (_openVerdict == OpeningVerdict.Opened ? " -> his block chance " + Pct0(OpeningMath.BlockChance(in r, _openScore, _openPlan.K, _openPlan.ShieldHolds)) : string.Empty)
                        + live);
        }

        private static string JudgedWords(OpeningJudgedAt at) => at switch
        {
            OpeningJudgedAt.WindUp => "wind-up, already released",
            OpeningJudgedAt.Release => "release",
            _ => "contact",
        };

        /// <summary>Step 106f: from the tick - a wound-up follow-up not judged yet is judged the moment your swing commits (its release).</summary>
        private void TickFollowUpCommit(double now)
        {
            if (!_openPending || !_openWound || _openJudged) return;
            var you = PlayerRecord();
            if (you == null || you.Removed || !YouDrive(you)) return;
            int dir = ReadCommitted(you.Agent);
            if (OpeningMath.IsAttack(dir)) JudgeFollowUp(you, dir, OpeningJudgedAt.Release, now);
        }

        /// <summary>Your melee release ended without a hit (step 13's phases): the awaited follow-up met nothing; a miss in a chain counted
        /// (and with <c>OpeningMissKeepsChain</c> off the chain starts over).</summary>
        internal void OpeningMiss(double now)
        {
            if (!_openPending && !_openChain.Active) return;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.Runs) return;
                if (_openPending && _openWound) CloseOpening(now);
                ForceBlowResolved(-1, false, now); // step 106b: the held guard let go - the blow resolved (a miss)
                _openLiveRolled = false;
                if (!_openChain.Active) return;
                _open.Misses++;
                if (!r.MissKeepsChain) EndChain(OpeningReset.Miss);
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        /// <summary>
        /// OnMeleeHit: your blow's contact - on ANOTHER man it closes the awaited follow-up and resets the chain (a target switch); a block of him
        /// leaves the follow-up for the block seam's shadow roll (the wind-up's direction against the contact's counted); a LANDED hit on him is
        /// <see cref="OpeningShadowHit"/>'s (the impact decision runs in the damage decorator's seam BEFORE OnMeleeHit for a landed blow, after it
        /// otherwise - so nothing here depends on the order); his hit on you resets. Kicks / bashes, missiles and horse charges are not attacks
        /// here. Its own try.
        /// </summary>
        internal void OpeningContactSeen(Agent attacker, Agent victim, in AttackCollisionData cd)
        {
            if (!_openPending && !_openChain.Active) return;
            if (cd.IsMissile || cd.IsHorseCharge || cd.IsAlternativeAttack || attacker == null || victim == null) return;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.Runs) return;
                var a = Get(attacker);
                var v = Get(victim);
                if (a == null || v == null) return;
                double now = SpikeNow;
                bool landed = !cd.AttackBlockedWithShield && cd.CollisionResult == CombatCollisionResult.StrikeAgent;
                if (IsPlayer(v) && YouDrive(v))
                {
                    if (landed && _openChain.Active && a.AgentIndex == _openChain.Target)
                    {
                        CloseOpening(now);
                        EndChain(OpeningReset.HeHitYou);
                    }
                    return;
                }
                if (!IsPlayer(a) || !YouDrive(a)) return;
                ForceBlowResolved(v.AgentIndex, landed, now); // step 106b: your blow met the held man - the blow resolved
                bool onTarget = v.AgentIndex == (_openPending ? _openTarget : _openChain.Target);
                if (!onTarget)
                {
                    CloseOpening(now);
                    if (_openChain.Active && v.AgentIndex != _openChain.Target) EndChain(OpeningReset.TargetSwitch);
                    return;
                }
                if (landed) return; // OpeningShadowHit's
                if (_openPending && _openWound)
                {
                    int contact = TimedRecoilMath.DirectionOf((int)cd.AttackDirection);
                    if (!_openJudged && OpeningMath.IsAttack(contact)) JudgeFollowUp(a, contact, OpeningJudgedAt.Contact, now); // step 106f: no release seen first
                    _open.NoteDirection(_openDir, contact);
                }
                else if (_openPending) // its wind-up unseen (Athletics' phases off): the direction unread
                {
                    _openPending = false;
                    LastOpeningVerdict = OpeningVerdict.UnknownDirection;
                    _open.NoteFollowUp(OpeningVerdict.UnknownDirection, _openPlan.K, double.NaN, 0);
                }
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        private int ReadSwing(Agent a)
        {
            try
            {
                int d = OpeningSwingRead(a);
                return OpeningMath.IsAttack(d) ? d : OpeningMath.Unknown;
            }
            catch (Exception)
            {
                return OpeningMath.Unknown;
            }
        }

        /// <summary>
        /// From <see cref="ImpactDecide"/>: a decided CLEAN HIT. Yours on an AI man on foot drives the chain (<see cref="OpeningChain.NoteLanded"/>):
        /// the would-be stagger, off balance after an unguarded front hit, the would-be perfect strike on the reeling man. Your swing's direction is
        /// read from you at the hit (channel 1 - the release's direction); a follow-up landing on him brings its timing score. Its own try.
        /// </summary>
        private void OpeningShadowHit(TrackedAgent a, TrackedAgent v, in ImpactFacts f, in ImpactDecision d, double now)
        {
            if (d.Event != ImpactEvent.CleanHit) return;
            try
            {
                var r = OpeningRules.From(TraxSettings.Shared);
                if (!r.Runs) return;
                if (!f.AttackerIsPlayer || f.DefenderIsPlayer || !YouDrive(a) || f.AttackerMounted || f.DefenderMounted) return;
                // step 106e: the COLLISION's direction when this hit came with one (in game always - the decorator's perfect strike reads the same,
                // so the chain's verdict and the damage agree); your channel 1 only without it (the smoke's direct decisions)
                int swing = ReferenceEquals(_hitSwingBy, a) && Math.Abs(_hitSwingAt - now) < 1e-6 && OpeningMath.IsAttack(_hitSwing) ? _hitSwing : ReadSwing(a.Agent);
                _hitSwingBy = null;
                double score = 0;
                _openLiveRolled = false; // step 106b: the follow-up landed - its live roll is spent
                if (_openPending && _openTarget == v.AgentIndex)
                {
                    if (_openWound)
                    {
                        if (!_openJudged && OpeningMath.IsAttack(swing)) JudgeFollowUp(a, swing, OpeningJudgedAt.Contact, now); // step 106f: no release seen first
                        score = _openScore;
                        _open.NoteDirection(_openDir, swing);
                    }
                    else
                    {
                        LastOpeningVerdict = OpeningVerdict.UnknownDirection;
                        _open.NoteFollowUp(OpeningVerdict.UnknownDirection, _openPlan.K, double.NaN, 0);
                    }
                    _openPending = false;
                }
                int guard = ReadGuard(v.Agent);
                bool behind = d.Direction == 2;
                int mask = ReadWeaponMask(a.Agent);
                bool wasReeling = _openChain.Reeling(now) && _openChain.Target == v.AgentIndex;
                double chainScore = _openChain.Score;
                int staggerSwing = _openChain.StaggerSwing, finishMask = _openChain.FinishMask;
                // step 106e: reeling / stagger-immune BEFORE this hit (the decision already wrote his record - a stagger it rolled means he was not:
                // the impact model refuses a stagger inside the immunity)
                bool immune = OpeningMath.ImmuneBeforeHit(v.Impact.StaggerImmune(now), d.Stagger.Happens);
                var hit = _openChain.NoteLanded(v.AgentIndex, now, in r, behind, OpeningMath.IsGuard(guard), swing, mask, score, out var reset, immune);
                LastOpeningHit = hit;
                _open.NoteReset(reset);
                bool modStagger = d.Stagger.Happens || d.Knockdown.Happens;
                switch (hit)
                {
                    case OpeningHit.Immune:
                        _open.ImmuneRefused++;
                        OpeningLine("openings (shadow): your " + OpeningMath.AttackName(swing) + " LANDED on " + Name(v) + (behind ? " from behind" : " in front, not guarding")
                                    + " - he is still reeling or inside the stagger immunity (" + F2(Math.Max(0, v.Impact.StaggerImmuneUntil - now)) + " s left): no new opening");
                        break;
                    case OpeningHit.Staggers:
                        if (ReferenceEquals(_forceSt, v) && _forceKind != OpeningForceKind.Test) // step 106b: he reels - the force let go
                        {
                            if (_forceKind == OpeningForceKind.Held) _force.HeldLanded++;
                            EndForce(OpeningForceEnd.Reels, now, true);
                        }
                        _openStaggerMask = mask;
                        _open.NoteStagger(_openChain.Entry, modStagger);
                        OpeningMath.MultiplierRange(in r, OpeningMath.DirectionCount(mask), out double lo, out double hi);
                        string head = "openings (shadow): your " + OpeningMath.AttackName(swing) + " LANDED on " + Name(v) + (behind ? " from behind" : string.Empty)
                                      + " (" + OpeningMath.EntryName(_openChain.Entry) + ", link " + _openChain.K.ToString(CultureInfo.InvariantCulture) + ", timing " + F2(score)
                                      + ") -> he " + (r.Enabled ? "staggers " : "WOULD stagger ") + F2(Math.Max(0, _openChain.ReelUntil - now)) + " s" + (modStagger ? " (the mod's own stagger too - the arrow just shows on it)" : string.Empty)
                                      + "; PERFECT STRIKE side: " + OpeningMath.MaskName(_openChain.FinishMask) + " (arrow " + OpeningMath.ArrowName(OpeningMath.FinishArrow(_openChain.FinishMask))
                                      + ") for x" + F2(OpeningMath.StrikeMultiplier(in r, mask, _openChain.Score)) + " (range x" + F2(lo) + "-" + F2(hi) + ", "
                                      + OpeningMath.DirectionCount(mask).ToString(CultureInfo.InvariantCulture) + " directions)";
                        string live = OpeningPayoff(a, v, d.Stagger.Happens, d.Knockdown.Happens, now); // step 106d: the reel, the window, the notice (may end the chain)
                        OpeningLine(head + live);
                        break;
                    case OpeningHit.OffBalance:
                    {
                        _open.UnguardedOffBalance++;
                        var plan = OpeningMath.Plan(in r, 1, OpeningEntry.FrontUnguarded, f.AttackerSkill, false, OpeningMath.Unknown, swing, mask, OpeningDice);
                        LastOpening = plan;
                        _open.NoteState(in plan);
                        if (plan.Opens)
                        {
                            BeginFollowUp(v.AgentIndex, in plan, now);
                            ForceBalance(v, in plan, now); // step 106b: live - his guard to the balance side (its own try)
                        }
                        OpeningLine("openings (shadow): your " + OpeningMath.AttackName(swing) + " LANDED on " + Name(v) + " in front, NOT guarding -> OFF BALANCE"
                                    + (plan.Opens ? " to the " + OpeningMath.GuardName(plan.Guard) + " (the arrow " + OpeningMath.ArrowName(plan.Arrow) + "), the window to "
                                                    + F2(plan.Flash + plan.Window) + " s" : " - no side (" + SkipWords(plan.Skip) + ")"));
                        break;
                    }
                    case OpeningHit.FinishPerfect:
                    {
                        double k = OpeningMath.StrikeMultiplier(in r, _openStaggerMask, chainScore);
                        bool relaxed = swing != OpeningMath.Opposite(staggerSwing);
                        LastOpeningMultiplier = k;
                        _open.NoteFinish(true, k, _openStaggerMask, relaxed);
                        OpeningLine("openings (shadow): your " + OpeningMath.AttackName(swing) + " on the reeling " + Name(v) + " from the shown side ("
                                    + OpeningMath.MaskName(finishMask) + (relaxed ? " - the weapon's relaxed side" : string.Empty) + ") -> " + (r.Enabled ? "a PERFECT STRIKE x" : "WOULD be a PERFECT STRIKE x") + F2(k)
                                    + (r.Enabled ? " (applied by the damage's last question - the [impact] PERFECT STRIKE line)" : string.Empty));
                        break;
                    }
                    case OpeningHit.FinishOther:
                        _open.NoteFinish(false, 0, _openStaggerMask, false);
                        OpeningLine("openings (shadow): your " + OpeningMath.AttackName(swing) + " on the reeling " + Name(v) + " from another side (the shown: "
                                    + OpeningMath.MaskName(finishMask) + ") -> no bonus, step 105's knockdown roll" + (wasReeling ? string.Empty : " (?)"));
                        break;
                }
            }
            catch (Exception e)
            {
                OpeningFailed(e);
            }
        }

        /// <summary>The awaited follow-up ends: no swing seen = <see cref="OpeningVerdict.NoSwing"/>.</summary>
        private void CloseOpening(double now)
        {
            if (!_openPending) return;
            _openPending = false;
            if (_openWound && _openJudged) return;
            if (_openWound) // step 106f: wound up but never committed (cancelled, or the release unseen) - the direction unread
            {
                LastOpeningVerdict = OpeningVerdict.UnknownDirection;
                _open.NoteFollowUp(OpeningVerdict.UnknownDirection, _openPlan.K, _openWindUpAt - _openBlockAt, _openScore);
                return;
            }
            LastOpeningVerdict = OpeningVerdict.NoSwing;
            _open.NoteFollowUp(OpeningVerdict.NoSwing, _openPlan.K, double.NaN, 0);
        }

        private void EndChain(OpeningReset why)
        {
            if (!_openChain.Active) return;
            if (why == OpeningReset.ReelOver) _open.ReelsUnused++;
            _open.NoteReset(why);
            _openChain.Reset();
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>From <see cref="TickImpact"/> (own try there): the switches (the master switch first) - off = everything dropped; a follow-up
        /// that never came (the off-balance time), a reel that ran out unused, the chain's man gone.</summary>
        private void TickOpenings(double now)
        {
            TickOpeningPayoff(now); // step 106d: the notices; the chain's reel follows his record's
            if (_openPending && _openWound && !_openJudged && OpeningRules.From(TraxSettings.Shared).Runs) TickFollowUpCommit(now); // step 106f: judged at the release
            TickOpeningForce(now); // step 106b: the forced guard and the test tool (whatever the chain)
            if (!_openPending && !_openChain.Active) return;
            var r = OpeningRules.From(TraxSettings.Shared);
            if (!r.Runs)
            {
                CloseOpening(now);
                EndChain(OpeningReset.Off);
                return;
            }
            if (_openPending && !_openWound && now - _openBlockAt > r.ChainReset + 1e-9) CloseOpening(now);
            else if (_openPending && _openWound && now > OpeningMath.HoldEnds(in r, _openWindUpAt) + 0.5) CloseOpening(now); // step 106f: an uncommitted one counted
            if (_openChain.Active)
            {
                var t = GetByIndex(_openChain.Target);
                if (t == null || t.Removed)
                {
                    CloseOpening(now);
                    EndChain(OpeningReset.Off);
                }
                else if (_openChain.Expired(now, in r)) EndChain(_openChain.Phase == OpeningPhase.Reeling ? OpeningReset.ReelOver : OpeningReset.Gap);
            }
        }

        /// <summary>The mission's end (before the summary): an awaited follow-up closed.</summary>
        private void CloseOpenings(double now)
        {
            CloseOpening(now);
            _openChain.Reset();
            CloseOpeningForce(now); // step 106b: every force let go, the CanDefend flag given back
        }

        /// <summary>The summary's turn (after step 104a's line).</summary>
        private void WriteOpeningSummary()
        {
            CloseOpenings(SpikeNow);
            TraxLog.Info("summary", _open.SummaryLine(OpeningRules.From(TraxSettings.Shared)));
            WriteOpeningForceSummary(); // step 106b
        }

        private void OpeningLine(string text)
        {
            if (_openLines < OpeningFullLines)
            {
                _openLines++;
                TraxLog.Info("impact", "YOU: " + text + (_openLines == OpeningFullLines ? " (the last at Info this battle - the rest verbose)" : string.Empty));
            }
            else if (TraxLog.VerboseWants("openings")) TraxLog.Verbose("impact", "YOU: " + text, "openings");
        }

        private void OpeningFailed(Exception e)
        {
            _open.Errors++;
            if (_open.Errors == 1) Failed("impact.openings", e);
            _openPending = false;
            _openChain.Reset();
        }
    }
}
