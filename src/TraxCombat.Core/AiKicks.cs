using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 65: a kick or a shield bash (what the AI is asked to do).</summary>
    public enum AiKickKind : byte
    {
        None,
        Kick,
        Bash,
    }

    /// <summary>Step 65: where one AI man's kick attempt is (the input wish the hook writes).</summary>
    public enum AiKickPhase : byte
    {
        /// <summary>No attempt.</summary>
        None,

        /// <summary>A bash's lead: his guard held up (a direction bit) for <see cref="AiKickMath.BashLeadSeconds"/> - the engine's kick input
        /// while blocking is a bash.</summary>
        Guard,

        /// <summary>The kick input (<c>EventControlFlag.Kick</c>) pressed for <see cref="AiKickMath.PressSeconds"/>.</summary>
        Press,

        /// <summary>The press is over: his own attack / guard / event input held off and he stands still until the action has played out
        /// (or <see cref="AiKickMath.WatchSeconds"/> passed) - the native AI's own input would cancel it before the hit frame.</summary>
        Commit,
    }

    /// <summary>Step 81: what happened at one AI melee wind-up (the decision point) other than the roll's own kick / bash / swing - the first six
    /// make it NOT a move in kick reach (he swings or thrusts as the game wants: a spearman holding his ground with the enemy beyond his kick, a
    /// rider, a routing man); the rest REFUSE a rolled kick / bash (he swings instead) - by group: safety, the swing stack, the cooldown, the strike
    /// path (<see cref="AiKickMath.GroupOf"/>). Step 65's look and its reasons (the dice, the bar gate, the wait for reach) are gone.</summary>
    public enum AiKickSkip : byte
    {
        /// <summary>The enemy he fights beyond <c>AiKickReachMetres</c> - not a move in reach (a spearman keeps thrusting).</summary>
        OutOfReach,

        /// <summary>The enemy he fights outside <c>AiKickFacingDegrees</c> of his facing.</summary>
        NotFacing,

        /// <summary>No enemy he fights (no target, a friend, gone, on the ground).</summary>
        NoTarget,

        /// <summary>On a horse.</summary>
        Mounted,

        /// <summary>Running away (a rout).</summary>
        Routing,

        /// <summary>A bow / crossbow / throwing weapon in his hand.</summary>
        Ranged,

        /// <summary>Refused (safety): reeling, staggered or on the ground.</summary>
        Staggered,

        /// <summary>Refused (safety): a kick / bash of his still playing.</summary>
        Busy,

        /// <summary>Refused (safety): fewer Athletics points than the kick costs.</summary>
        NoAthletics,

        /// <summary>Refused (safety): the attempt pool is full (plumbing - <see cref="AiKickMath.MaxAttempts"/>).</summary>
        PoolFull,

        /// <summary>Refused: no stored swing (step 32's stack empty - KicksUseSwings).</summary>
        NoSwing,

        /// <summary>Refused: within <c>AiKickCooldownSeconds</c> of his last kick or bash.</summary>
        Cooldown,

        /// <summary>Refused (step 80's path): the enemy in reach and in front but not in his strike path.</summary>
        TargetOffPath,

        /// <summary>Refused (step 80's path): a friend first in his strike path.</summary>
        FriendInTheWay,

        /// <summary>Refused (step 80's path): a REAR-RANK man - a friend in his strike path within <c>AiKickRearRankMetres</c>.</summary>
        BehindFriend,

        /// <summary>His wind-up was over (released, cancelled) before the tick looked - nothing to replace (not a move).</summary>
        ReadyOver,
    }

    /// <summary>Step 80: the strike path's verdict (<see cref="AiKickPathProbe"/>).</summary>
    public enum AiKickPath : byte
    {
        /// <summary>The enemy is the first body in his strike path (or the check is off).</summary>
        Clear,

        /// <summary>The enemy is not in his strike path at all (off the line of his facing, behind him, beyond the reach).</summary>
        TargetOffPath,

        /// <summary>A friend is in his strike path closer than the enemy (beyond the rear-rank distance) - the decision waits.</summary>
        FriendFirst,

        /// <summary>A friend is in his strike path closer than the enemy AND within the rear-rank distance - a rear-rank man: no kick.</summary>
        BehindFriend,

        Count,
    }

    /// <summary>Step 80: whom one PLAYED AI kick / bash hit first (the summary's enemy / friend / miss split).</summary>
    public enum AiKickOutcome : byte
    {
        /// <summary>The enemy's body - his target.</summary>
        EnemyTarget,

        /// <summary>The body of another enemy.</summary>
        EnemyOther,

        /// <summary>An enemy's raised shield.</summary>
        EnemyShield,

        /// <summary>A friend's body (the game cancels it).</summary>
        FriendBody,

        /// <summary>A friend's shield.</summary>
        FriendShield,

        /// <summary>A horse or a man the mod does not track (or nobody named).</summary>
        Untracked,

        /// <summary>Played, but it hit nobody (a whiff).</summary>
        Miss,

        Count,
    }

    /// <summary>
    /// Step 80 (Toni's logs since step 68: 314 of 570 landed AI kicks / bashes on a FRIEND in his 17:18 battle, 427 of 668 at 16:17): THE STRIKE
    /// PATH. A kick or bash goes along the man's FACING, not along the line to the enemy he fights, and it hits the first body there. Step 68
    /// looked for a friend within 0.5 m of the line to the enemy - a man 40° off his facing has that line 1 m away from where his foot goes,
    /// and a rear-rank man's front mate stands straight ahead of him while his target stands half a file to the side. The probe works in his
    /// own frame: the enemy must stand IN the path (0 &lt; along ≤ reach, |side| ≤ the half-width - a body centre that close to the line is
    /// touched), and no friend may stand in it closer (along &gt; <see cref="AiKickMath.FriendPathMinAlong"/>, along &lt; the enemy's, |side| ≤ the
    /// half-width); the nearest such friend within the rear-rank distance = a rear-rank man. Allocation-free: a struct fed one friend at a time.
    /// </summary>
    public struct AiKickPathProbe
    {
        private double _ax, _ay, _ux, _uy, _half, _rear;
        private bool _off;

        /// <summary>His facing's unit vector after <see cref="Begin"/> (a zero facing = the line to the target).</summary>
        public double FaceX => _ux;
        public double FaceY => _uy;

        /// <summary>The enemy along his facing and off its line (m).</summary>
        public double TargetAlong { get; private set; }
        public double TargetSide { get; private set; }

        /// <summary>The enemy stands in the strike path.</summary>
        public bool TargetIn { get; private set; }

        /// <summary>The nearest friend in the path closer than the enemy (along, m); NaN = none.</summary>
        public double NearestFriend { get; private set; }

        /// <summary>Friends looked at / found in the path.</summary>
        public int Looked { get; private set; }
        public int InPath { get; private set; }

        /// <summary>Starts a probe for the man at (<paramref name="ax"/>, <paramref name="ay"/>) facing (<paramref name="faceX"/>, <paramref name="faceY"/>)
        /// (any length) at the enemy at (<paramref name="tx"/>, <paramref name="ty"/>). <paramref name="halfWidth"/> 0 = the check off (always Clear).</summary>
        public static AiKickPathProbe Begin(double ax, double ay, double faceX, double faceY, double tx, double ty, double reach, double halfWidth, double rearRank)
        {
            var p = new AiKickPathProbe
            {
                _ax = ax,
                _ay = ay,
                _half = double.IsNaN(halfWidth) || halfWidth < 0 ? 0 : halfWidth,
                _rear = double.IsNaN(rearRank) || rearRank < 0 ? 0 : rearRank,
                NearestFriend = double.NaN,
            };
            p._off = !(p._half > 0);
            double fl = Math.Sqrt(faceX * faceX + faceY * faceY);
            if (!(fl > 1e-6))
            {
                faceX = tx - ax;
                faceY = ty - ay;
                fl = Math.Sqrt(faceX * faceX + faceY * faceY);
            }
            if (!(fl > 1e-6))
            {
                faceX = 0;
                faceY = 1;
                fl = 1;
            }
            p._ux = faceX / fl;
            p._uy = faceY / fl;
            p.TargetAlong = (tx - ax) * p._ux + (ty - ay) * p._uy;
            p.TargetSide = Math.Abs(-(tx - ax) * p._uy + (ty - ay) * p._ux);
            double r = double.IsNaN(reach) || reach < 0 ? 0 : reach;
            p.TargetIn = p._off || (p.TargetAlong > 0 && p.TargetAlong <= r + 1e-9 && p.TargetSide <= p._half + 1e-9);
            return p;
        }

        /// <summary>One friend at (<paramref name="fx"/>, <paramref name="fy"/>): in the path closer than the enemy = noted (the nearest kept).</summary>
        public void Add(double fx, double fy)
        {
            if (_off || !TargetIn) return;
            Looked++;
            double along = (fx - _ax) * _ux + (fy - _ay) * _uy;
            if (!(along > AiKickMath.FriendPathMinAlong) || !(along < TargetAlong)) return;
            double side = Math.Abs(-(fx - _ax) * _uy + (fy - _ay) * _ux);
            if (!(side <= _half + 1e-9)) return;
            InPath++;
            if (double.IsNaN(NearestFriend) || along < NearestFriend) NearestFriend = along;
        }

        /// <summary>The verdict: off = Clear; the enemy not in the path; a friend first (within the rear-rank distance = behind a friend); clear.</summary>
        public AiKickPath Verdict
        {
            get
            {
                if (_off) return AiKickPath.Clear;
                if (!TargetIn) return AiKickPath.TargetOffPath;
                if (double.IsNaN(NearestFriend)) return AiKickPath.Clear;
                return _rear > 0 && NearestFriend <= _rear + 1e-9 ? AiKickPath.BehindFriend : AiKickPath.FriendFirst;
            }
        }
    }

    /// <summary>Step 68: whom an AI kick / bash's first hit LANDED on (the [summary] "ai kicks" line - the step-68 question).</summary>
    public enum AiKickVictim : byte
    {
        /// <summary>The enemy he kicked at.</summary>
        Target,

        /// <summary>Another enemy (a man in the way, a neighbour).</summary>
        OtherEnemy,

        /// <summary>One of his own side - the game cancels it, nothing is decided (step 63's rule).</summary>
        Friend,

        /// <summary>A horse, or a man the mod does not track.</summary>
        Untracked,

        Count,
    }

    /// <summary>Step 65: how an attempt ended.</summary>
    public enum AiKickEnd : byte
    {
        /// <summary>The engine played a kick / bash and it is over.</summary>
        Played,

        /// <summary>Nothing showed within <see cref="AiKickMath.WatchSeconds"/> - the engine ignored the input (or played it too briefly to be seen).</summary>
        NotPlayed,

        /// <summary>He was struck / staggered / knocked down meanwhile.</summary>
        Interrupted,

        /// <summary>He left the field, was taken over by the player, or got on a horse.</summary>
        Gone,

        /// <summary>The switch, Athletics or the master switch went off.</summary>
        SwitchedOff,

        /// <summary>The mission ended.</summary>
        MissionEnd,

        /// <summary>Step 80: a bash's guard over, the strike path read again before the key - no longer clear (a friend stepped in, the enemy
        /// moved off it, gone): ended with no kick key.</summary>
        PathBlocked,
    }

    /// <summary>
    /// Step 65 (Anton 2026-10-19: "make the AI do a few shield bashes and kicks now and then - the game has it but the NPCs never use them"):
    /// THE AI KICKS AND BASHES - the rules, read live. <see cref="Enabled"/> = the master switch FIRST, then AthleticsEnabled (a kick costs
    /// Athletics, and the kick is SEEN through the Athletics action poll), then <c>AiKicksEnabled</c>. Step 81 (Toni 2026-10-22: "they add good
    /// disturbance to the battle and also dont do much damage"): no rate any more - each melee move an AI man on foot is about to make with the
    /// enemy in kick reach is ROLLED by his SHARES: a shieldman kick <see cref="ShieldKick"/> / bash <see cref="ShieldBash"/> / swing the rest, a
    /// man with no shield kick <see cref="NoShieldKick"/> / swing the rest; a rolled kick / bash TAKES THE PLACE of that swing.
    /// </summary>
    public readonly struct AiKickRules
    {
        public AiKickRules(bool modEnabled, bool athleticsEnabled, bool enabled, double reachMetres, double facingDegrees, double cooldownSeconds,
            bool kicksARaisedShield, int shieldKickPercent, int shieldBashPercent, int noShieldKickPercent, double friendClearMetres = 0,
            double bashPathHalfWidthMetres = 0, double rearRankMetres = 0)
        {
            FriendClear = Pos(friendClearMetres);
            BashPathHalfWidth = Pos(bashPathHalfWidthMetres);
            RearRank = Pos(rearRankMetres);
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            KicksEnabled = enabled;
            Reach = Pos(reachMetres);
            Facing = Math.Max(0, Math.Min(180, double.IsNaN(facingDegrees) ? 0 : facingDegrees));
            Cooldown = Pos(cooldownSeconds);
            KicksARaisedShield = kicksARaisedShield;
            // step 81: each share 0-100; a shieldman's kick + bash never over 100 - the bash gives way (the kick as set, the bash = what is left)
            int k = Math.Max(0, Math.Min(100, shieldKickPercent)), b = Math.Max(0, Math.Min(100, shieldBashPercent));
            ShieldSharesCut = k + b > 100;
            if (ShieldSharesCut) b = 100 - k;
            ShieldKick = k / 100.0;
            ShieldBash = b / 100.0;
            NoShieldKick = Math.Max(0, Math.Min(100, noShieldKickPercent)) / 100.0;
        }

        public static AiKickRules From(TraxSettings s) => new AiKickRules(s.ModEnabled, s.AthleticsEnabled, s.AiKicksEnabled, s.AiKickReachMetres, s.AiKickFacingDegrees,
            s.AiKickCooldownSeconds, s.AiKickKicksARaisedShield, s.AiShieldmanKickPercent, s.AiShieldmanBashPercent, s.AiNoShieldKickPercent, s.AiKickFriendClearMetres,
            s.AiBashPathHalfWidthMetres, s.AiKickRearRankMetres);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }
        public bool KicksEnabled { get; }

        /// <summary>The AI kicks: the master switch FIRST, Athletics, AiKicksEnabled, and a share above 0 (step 81).</summary>
        public bool Enabled => ModEnabled && AthleticsEnabled && KicksEnabled && (ShieldKick > 0 || ShieldBash > 0 || NoShieldKick > 0);

        public double Reach { get; }
        public double Facing { get; }

        /// <summary>Step 81: at least this long between two kicks / bashes of one man, from the ask (<c>AiKickCooldownSeconds</c> - both kinds;
        /// step 66's separate bash cooldown is gone).</summary>
        public double Cooldown { get; }

        public bool KicksARaisedShield { get; }

        /// <summary>Step 81: a shieldman's shares of his melee moves in kick reach (0-1): kick, bash; the swing = 1 − both.</summary>
        public double ShieldKick { get; }
        public double ShieldBash { get; }

        /// <summary>Step 81: a man WITHOUT a shield's kick share (0-1); the swing = 1 − it.</summary>
        public double NoShieldKick { get; }

        /// <summary>Step 81: the shieldman's kick + bash were set over 100 - the bash was cut to 100 − the kick (the describe line says it).</summary>
        public bool ShieldSharesCut { get; }

        /// <summary>The swing's share for a man with / without a shield.</summary>
        public double SwingShare(bool shield) => shield ? Math.Max(0, 1 - ShieldKick - ShieldBash) : Math.Max(0, 1 - NoShieldKick);

        /// <summary>Step 68: no kick / bash while a man of his own side stands within this many metres of the line to the enemy, between them
        /// (<c>AiKickFriendClearMetres</c>; 0 = no check).</summary>
        public double FriendClear { get; }

        /// <summary>Step 80: a BASH's strike path half-width (<c>AiBashPathHalfWidthMetres</c>; 0 = no check for bashes). A kick's is
        /// <see cref="FriendClear"/> (<c>AiKickFriendClearMetres</c> - since step 80 measured from the line of his FACING).</summary>
        public double BashPathHalfWidth { get; }

        /// <summary>Step 80: a friend in the strike path within this many metres of him = a rear-rank man - no kick
        /// (<c>AiKickRearRankMetres</c>; 0 = a friend first only refuses).</summary>
        public double RearRank { get; }

        /// <summary>Step 80: the strike path's half-width for this kind (0 = no check).</summary>
        public double PathHalfWidth(AiKickKind kind) => kind == AiKickKind.Bash ? BashPathHalfWidth : FriendClear;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !KicksEnabled ? "AiKicksEnabled"
            : !(ShieldKick > 0) && !(ShieldBash > 0) && !(NoShieldKick > 0) ? "AiShieldmanKickPercent, AiShieldmanBashPercent and AiNoShieldKickPercent all 0" : null;

        private static double Pos(double v) => double.IsNaN(v) || v < 0 ? 0 : v;

        /// <summary>The settings in one sentence (the mission-start line, the summary).</summary>
        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - the game's AI, which never kicks";
            return "ON - each melee move an AI man on foot is about to make (his wind-up) with the enemy he fights within " + AiKickMath.F1(Reach) + " m and "
                   + AiKickMath.F0(Facing) + "° of his facing is ROLLED (step 81): a SHIELDMAN kick " + AiKickMath.P0(ShieldKick) + " / shield bash " + AiKickMath.P0(ShieldBash)
                   + " / swing " + AiKickMath.P0(SwingShare(true)) + (ShieldSharesCut ? " (kick + bash set over 100% - the bash cut to what is left)" : string.Empty)
                   + ", a man with NO shield kick " + AiKickMath.P0(NoShieldKick) + " / swing " + AiKickMath.P0(SwingShare(false))
                   + "; a rolled kick / bash takes the place of that swing (his wind-up cancelled by the guard); farther off he swings or thrusts as the game wants (a spearman keeps his ground)"
                   + "; refused - he swings - when staggered, down, without the Athletics or a stored swing for it, within " + AiKickMath.F1(Cooldown) + " s of his last kick or bash"
                   + (KicksARaisedShield ? "; a shieldman KICKS a raised shield facing him (a rolled bash becomes a kick)" : string.Empty)
                   + (FriendClear > 0 || BashPathHalfWidth > 0
                       ? "; only when the enemy is the FIRST body in his strike path along his facing (step 80: a kick's path " + AiKickMath.F2(FriendClear)
                         + " m, a bash's " + AiKickMath.F2(BashPathHalfWidth) + " m either side, 0 = no check; the enemy in it, no friend in it closer"
                         + (RearRank > 0 ? "; a friend in it within " + AiKickMath.F1(RearRank) + " m = a rear-rank man" : string.Empty)
                         + "; a bash's path read again after its guard)"
                       : string.Empty)
                   + "; never you, a rider, a routing man or one with a ranged weapon - through the AI's own input (the engine's kick key; a bash = the kick key with the guard up)";
        }
    }

    /// <summary>Step 65 pure: the phases' timing and input bits, the kind; step 81: the ROLL of a melee move by the shares, the refusal groups.</summary>
    public static class AiKickMath
    {
        // ---- plumbing (documented, not settings - RBM's AiKickBash proven numbers, its AiModule/Agents/AiKickBash.cs)

        /// <summary>Step 81: the contact-time sample - each AI man's "in contact" read about this often (staggered; the summary's man-minutes).</summary>
        public const double LookSeconds = 0.25;

        /// <summary>A bash's lead: the guard held up this long before the kick key (RBM's BlockSeconds).</summary>
        public const double BashLeadSeconds = 0.4;

        /// <summary>Step 81: a KICK's lead - the guard raised this long first: the kick now takes the place of a swing whose wind-up has begun, and
        /// taking his attack bits out mid-wind-up with no guard would RELEASE the blow (step 16); the guard cancels it (step 16's ready cancel), then
        /// the kick key with the guard down. Plumbing.</summary>
        public const double KickLeadSeconds = 0.15;

        /// <summary>The kick key held this long (RBM's KickSeconds).</summary>
        public const double PressSeconds = 0.15;

        /// <summary>His own input held off at most this long after the press, waiting for the action to play out (RBM's WatchSeconds).</summary>
        public const double WatchSeconds = 1.5;

        /// <summary>Attempts at once (a 1000-man battle has a few dozen at a time; a full pool refuses - counted).</summary>
        public const int MaxAttempts = 256; // step 66: shieldmen bash as often as they swing - a 1000-man battle can run ~100 at once

        /// <summary>Step 81: AI melee wind-ups waiting for the tick's decision at once (one tick's worth; a full queue = counted, he swings).</summary>
        public const int MaxPendingMoves = 256;

        /// <summary>A raised shield facing the kicker within this angle is kicked, not bashed (RBM's ShieldFacingCosine 0.5 = 60°).</summary>
        public const double RaisedShieldDegrees = 60;

        public const int SkipCount = 16;
        public const int EndCount = 7;

        /// <summary>His own action (channel 1) keeps him busy: a ready / release of a melee or ranged attack, a reload, a kick / bash, a weapon
        /// change (the engine's <c>ActionCodeType</c> 15-20, 28-31, 33-34) - taking his attack bits out mid-ready would RELEASE the blow (step 16).</summary>
        public static bool IsBusyAction(int action) => (action >= 15 && action <= 20) || (action >= 28 && action <= 31) || action == 33 || action == 34;

        /// <summary>
        /// Step 80: is the enemy he fights - <paramref name="distance"/> m off, <paramref name="offFacingDegrees"/>° off his facing - IN his strike
        /// path: along his facing at most <paramref name="reach"/> m and within <paramref name="halfWidth"/> m of its line (d × cos, d × sin)? A
        /// half-width of 0 = no check (true). The engine's reads he already has - no positions needed (the cheap gate before the friends).
        /// </summary>
        public static bool TargetInPath(double distance, double offFacingDegrees, double reach, double halfWidth)
        {
            if (double.IsNaN(halfWidth) || !(halfWidth > 0)) return true;
            if (double.IsNaN(distance) || double.IsNaN(offFacingDegrees)) return false;
            double a = offFacingDegrees * Math.PI / 180.0;
            double along = distance * Math.Cos(a), side = Math.Abs(distance * Math.Sin(a));
            return along > 0 && along <= reach + 1e-9 && side <= halfWidth + 1e-9;
        }

        /// <summary>Step 68 / 80: a friend counts as in the strike path only past this along his facing (m). Step 68's 0.2 left out a man half a
        /// step forward beside him - his body (0.4 m round) reaches into a bash's sweep: step 80's smoke saw front-rank shieldmen bash their
        /// neighbours there. 0 = anyone ahead of his shoulders within the half-width. Plumbing.</summary>
        public const double FriendPathMinAlong = 0.0;

        /// <summary>
        /// Step 81 (Toni 2026-10-22): ONE melee move of an AI man on foot with the enemy in kick reach, rolled by his shares on the die
        /// <paramref name="u"/> (0 ≤ u &lt; 1): a shieldman - u below the kick share = a KICK, below kick + bash = a BASH, else a swing
        /// (<see cref="AiKickKind.None"/>); no shield - u below the kick share = a KICK, else a swing.
        /// </summary>
        public static AiKickKind Roll(in AiKickRules r, bool shield, double u)
        {
            if (double.IsNaN(u)) return AiKickKind.None;
            double kick = shield ? r.ShieldKick : r.NoShieldKick;
            if (u < kick) return AiKickKind.Kick;
            if (shield && u < kick + r.ShieldBash) return AiKickKind.Bash;
            return AiKickKind.None;
        }

        /// <summary>Step 81: is his move rolled at all - the enemy he fights within <c>AiKickReachMetres</c> and <c>AiKickFacingDegrees</c> of his
        /// facing? Beyond the reach nothing is rolled: he swings or thrusts as the game wants (a spearman keeps his ground with the spear - step 76's
        /// preference untouched); <paramref name="why"/> = OutOfReach / NotFacing when not.</summary>
        public static bool InReach(in AiKickRules r, double distance, double offFacingDegrees, out AiKickSkip why)
        {
            why = AiKickSkip.OutOfReach;
            if (!(distance <= r.Reach)) return false;
            why = AiKickSkip.NotFacing;
            return offFacingDegrees <= r.Facing;
        }

        /// <summary>What he does: a shield in hand → a bash, unless the target's own shield is raised toward him and the rule says kick it;
        /// no shield → a kick. Step 81: a ROLLED bash goes through it (the rolled kind stands otherwise).</summary>
        public static AiKickKind KindFor(in AiKickRules r, bool shieldInHand, bool targetShieldRaisedFacing) =>
            !shieldInHand ? AiKickKind.Kick : r.KicksARaisedShield && targetShieldRaisedFacing ? AiKickKind.Kick : AiKickKind.Bash;

        /// <summary>The first phase of an attempt: since step 81 both kinds hold the guard first - it cancels the wind-up the move replaces.</summary>
        public static AiKickPhase FirstPhase(AiKickKind kind) => AiKickPhase.Guard;

        /// <summary>Step 81: the refusal groups of the summary (Toni's: stack empty, cooldown, path, safety) - 0 = not a move in reach.</summary>
        public const int GroupCount = 5;

        public static int GroupOf(AiKickSkip s) => s switch
        {
            AiKickSkip.Staggered or AiKickSkip.Busy or AiKickSkip.NoAthletics or AiKickSkip.PoolFull => 1,
            AiKickSkip.NoSwing => 2,
            AiKickSkip.Cooldown => 3,
            AiKickSkip.TargetOffPath or AiKickSkip.FriendInTheWay or AiKickSkip.BehindFriend => 4,
            _ => 0,
        };

        public static string GroupName(int g) => g switch
        {
            1 => "safety",
            2 => "stack empty",
            3 => "cooldown",
            4 => "path",
            _ => "not a move in reach",
        };

        /// <summary>The end of a phase begun at <paramref name="start"/> (a kick's guard is shorter than a bash's).</summary>
        public static double PhaseEnd(AiKickPhase phase, double start, AiKickKind kind = AiKickKind.Bash) => phase switch
        {
            AiKickPhase.Guard => start + (kind == AiKickKind.Bash ? BashLeadSeconds : KickLeadSeconds),
            AiKickPhase.Press => start + PressSeconds,
            AiKickPhase.Commit => start + WatchSeconds,
            _ => start,
        };

        /// <summary>The next phase once one ends (Guard → Press → Commit → None).</summary>
        public static AiKickPhase Next(AiKickPhase phase) => phase switch
        {
            AiKickPhase.Guard => AiKickPhase.Press,
            AiKickPhase.Press => AiKickPhase.Commit,
            _ => AiKickPhase.None,
        };

        /// <summary>
        /// One frame of an attempting man's input (the engine's bit values - <see cref="AiInputMath"/>'s): every phase takes his own attack and
        /// guard bits out and his own event flags (a weapon switch, a jump - anything would cancel the kick); Guard = DefendUp (a direction: the
        /// guard raised - a bash's context, and since step 81 the cancel of the wind-up a kick replaces); Press = the kick event bit (+ DefendUp
        /// for a bash); Commit = nothing more (he stands still - <paramref name="stopMoving"/>). Returns the new movement flags;
        /// <paramref name="eventFlags"/> rewritten.
        /// </summary>
        public static uint Frame(AiKickPhase phase, AiKickKind kind, uint movementFlags, ref uint eventFlags, out bool stopMoving)
        {
            stopMoving = phase == AiKickPhase.Commit;
            if (phase == AiKickPhase.None) return movementFlags;
            uint f = movementFlags & ~(AiInputMath.AttackMask | AiInputMath.DefendMask);
            eventFlags = 0;
            bool bash = kind == AiKickKind.Bash;
            if (phase == AiKickPhase.Guard || (bash && phase == AiKickPhase.Press)) f |= DefendUp;
            if (phase == AiKickPhase.Press) eventFlags = KickEventBit;
            return f;
        }

        /// <summary><c>MovementControlFlag.DefendUp</c> (the smoke checks it).</summary>
        public const uint DefendUp = 0x1000;

        /// <summary><c>EventControlFlag.Kick</c> (the smoke checks it).</summary>
        public const uint KickEventBit = 0x8000;

        public static string SkipName(AiKickSkip s) => s switch
        {
            AiKickSkip.OutOfReach => "the enemy beyond kick reach",
            AiKickSkip.NotFacing => "the enemy not in front",
            AiKickSkip.NoTarget => "no enemy target",
            AiKickSkip.Mounted => "mounted",
            AiKickSkip.Routing => "routing",
            AiKickSkip.Ranged => "a ranged weapon in hand",
            AiKickSkip.Staggered => "staggered / down",
            AiKickSkip.Busy => "his kick still playing",
            AiKickSkip.NoAthletics => "no Athletics for it",
            AiKickSkip.PoolFull => "the attempt pool full",
            AiKickSkip.NoSwing => "no stored swing",
            AiKickSkip.Cooldown => "cooldown",
            AiKickSkip.TargetOffPath => "target off his strike path (step 80)",
            AiKickSkip.FriendInTheWay => "a friend first in his strike path (step 80)",
            AiKickSkip.BehindFriend => "behind a friend - rear rank (step 80)",
            _ => "his wind-up already over",
        };

        public static string EndName(AiKickEnd e) => e switch
        {
            AiKickEnd.Played => "played",
            AiKickEnd.NotPlayed => "NOT played (nothing showed within 1.5 s)",
            AiKickEnd.Interrupted => "interrupted (struck / staggered)",
            AiKickEnd.Gone => "gone (left / taken over / mounted)",
            AiKickEnd.SwitchedOff => "switched off",
            AiKickEnd.PathBlocked => "path no longer clear after a bash's guard (step 80)",
            _ => "mission end",
        };

        public static string PathName(AiKickPath p) => p switch
        {
            AiKickPath.Clear => "clear",
            AiKickPath.TargetOffPath => "the enemy off his strike path",
            AiKickPath.FriendFirst => "a friend first in his strike path",
            _ => "behind a friend (rear rank)",
        };

        public static string OutcomeName(AiKickOutcome o) => o switch
        {
            AiKickOutcome.EnemyTarget => "his target",
            AiKickOutcome.EnemyOther => "another enemy",
            AiKickOutcome.EnemyShield => "an enemy's shield",
            AiKickOutcome.FriendBody => "a FRIEND",
            AiKickOutcome.FriendShield => "a friend's shield",
            AiKickOutcome.Untracked => "a horse / untracked",
            _ => "missed",
        };

        public static string KindName(AiKickKind k) => k == AiKickKind.Bash ? "shield bash" : k == AiKickKind.Kick ? "kick" : "swing";

        public static string VictimName(AiKickVictim v) => v switch
        {
            AiKickVictim.Target => "on his target",
            AiKickVictim.OtherEnemy => "on another enemy",
            AiKickVictim.Friend => "on a FRIEND (the game cancels it - never decided)",
            _ => "on a horse / a man not tracked",
        };

        internal static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        internal static string P0(double share) => (100 * share).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Step 65 per mission: the AI's kicks / bashes - the asks by kind and by man type (step 81: a shieldman / a man with no shield), PLAYED
    /// (the engine showed the action) → LANDED (on the man / into a shield / on nothing), the ends, the Athletics they cost; step 81: THE
    /// DECISION POINT - the AI melee wind-ups seen, the moves in kick reach by man type, what the die rolled and what was done, the refusals
    /// by reason and group + the [summary] "ai kicks" lines. Main thread.
    /// </summary>
    public sealed class AiKickStats
    {
        /// <summary>Step 81: the man types - 0 a shieldman, 1 no shield (step 65's turtle / fever reasons are gone with the rate).</summary>
        public const int ReasonCount = 2;

        private readonly int[,] _asked = new int[3, ReasonCount];
        private readonly int[,] _played = new int[3, ReasonCount];
        private readonly int[,] _landed = new int[3, ReasonCount];
        private readonly int[,] _blocked = new int[3, ReasonCount];
        public readonly int[] Skips = new int[AiKickMath.SkipCount];
        public readonly int[] Ends = new int[AiKickMath.EndCount];

        /// <summary>Step 81: the AI melee wind-ups looked at (the decision point); the man-seconds sampled while IN CONTACT (the fever's contact - an
        /// enemy within <c>FeverContactMetres</c> - when the fever's look runs).</summary>
        public long Looks;
        public double ContactManSeconds;

        /// <summary>Step 81: the moves in kick reach by man type, what the die rolled for them and what was DONE (kind 0 = a swing, 1 kick, 2 bash);
        /// a refused kick / bash is a swing done; the refusals by man type and group (<see cref="AiKickMath.GroupOf"/>).</summary>
        private readonly int[] _moves = new int[ReasonCount];
        private readonly int[,] _rolled = new int[ReasonCount, 3];
        private readonly int[,] _refused = new int[ReasonCount, AiKickMath.GroupCount];

        /// <summary>Step 81: a shieldman's rolled bash turned into a kick by a raised shield facing him (<c>AiKickKicksARaisedShield</c>).</summary>
        public int RaisedShieldKicks;

        /// <summary>Step 81: wind-ups the pending queue had no room for (he swings - plumbing).</summary>
        public int QueueFull;

        public static int TypeOf(bool shield) => shield ? 0 : 1;

        public static string TypeName(int t) => t == 0 ? "shieldmen" : "no shield";

        public void NoteMove(int type, AiKickKind rolled)
        {
            _moves[type]++;
            _rolled[type, (int)rolled]++;
        }

        public void NoteRefused(int type, AiKickSkip why) => _refused[type, AiKickMath.GroupOf(why)]++;

        public int Moves(int type) => _moves[type];

        public int Rolled(int type, AiKickKind k) => _rolled[type, (int)k];

        public int Refused(int type, int group) => _refused[type, group];

        /// <summary>Step 81: what was DONE at the decision point - a kick / bash ASKED (the attempt begun), else a swing.</summary>
        public int Done(int type, AiKickKind k)
        {
            if (k != AiKickKind.None) return _asked[(int)k, type];
            return _moves[type] - _asked[1, type] - _asked[2, type];
        }

        /// <summary>Played as the other kind (asked a bash, the engine kicked - or the other way round).</summary>
        public int PlayedOtherKind;

        /// <summary>Step 68: whom the landed ones landed on (<see cref="AiKickVictim"/>).</summary>
        public readonly int[] Victims = new int[(int)AiKickVictim.Count];

        public void NoteVictim(AiKickVictim v) => Victims[(int)v]++;

        /// <summary>Step 68: the friend-in-the-way reads (in reach and in front): a mate on the line - held; the line clear.</summary>
        public int FriendChecksBlocked;
        public int FriendChecksClear;

        /// <summary>Step 80: whom each PLAYED kick / bash hit first, by kind (<see cref="AiKickOutcome"/>; a miss = played, no collision).</summary>
        private readonly int[,] _outcomes = new int[3, (int)AiKickOutcome.Count];

        /// <summary>Step 80: the strike path's verdicts at the look (in reach and in front) by kind, and at a bash's press (after its guard).</summary>
        private readonly int[,] _paths = new int[3, (int)AiKickPath.Count];
        private readonly int[] _pressPaths = new int[(int)AiKickPath.Count];

        /// <summary>Step 80: friends looked at by the probe, the probes made.</summary>
        public long PathFriendsLooked;
        public int PathProbes;

        public void NoteOutcome(AiKickKind k, AiKickOutcome o) => _outcomes[(int)k, (int)o]++;

        public int OutcomeCount(AiKickKind k, AiKickOutcome o) => _outcomes[(int)k, (int)o];

        public void NotePath(AiKickKind k, AiKickPath p) => _paths[(int)k, (int)p]++;

        public int PathCount(AiKickKind k, AiKickPath p) => _paths[(int)k, (int)p];

        public void NotePressPath(AiKickPath p) => _pressPaths[(int)p]++;

        public int PressPathCount(AiKickPath p) => _pressPaths[(int)p];

        /// <summary>Step 80: hits on an enemy (his body or his shield) / on a friend (body or shield) / misses / untracked, by kind.</summary>
        public int EnemyHits(AiKickKind k) => OutcomeCount(k, AiKickOutcome.EnemyTarget) + OutcomeCount(k, AiKickOutcome.EnemyOther) + OutcomeCount(k, AiKickOutcome.EnemyShield);

        public int FriendHits(AiKickKind k) => OutcomeCount(k, AiKickOutcome.FriendBody) + OutcomeCount(k, AiKickOutcome.FriendShield);

        /// <summary>Step 80: the "whom they hit" part for one kind.</summary>
        public string OutcomeText(AiKickKind k)
        {
            var c = CultureInfo.InvariantCulture;
            int e = EnemyHits(k), f = FriendHits(k), u = OutcomeCount(k, AiKickOutcome.Untracked), m = OutcomeCount(k, AiKickOutcome.Miss);
            int hits = e + f + u;
            return (k == AiKickKind.Kick ? "kicks" : "bashes") + ": on an ENEMY " + e + " (his target " + OutcomeCount(k, AiKickOutcome.EnemyTarget) + ", another "
                   + OutcomeCount(k, AiKickOutcome.EnemyOther) + ", into his shield " + OutcomeCount(k, AiKickOutcome.EnemyShield) + "), on a FRIEND " + f + " (the man "
                   + OutcomeCount(k, AiKickOutcome.FriendBody) + ", his shield " + OutcomeCount(k, AiKickOutcome.FriendShield) + "), a horse / untracked " + u + ", MISSED " + m
                   + (hits > 0 ? " - friends " + (100.0 * f / hits).ToString("0.0", c) + "% of the hits" : string.Empty);
        }

        /// <summary>Step 80: the [summary] "ai kicks - whom they hit and the strike path" line.</summary>
        public string PathLine(in AiKickRules r)
        {
            var c = CultureInfo.InvariantCulture;
            double minutes = ContactManSeconds / 60.0;
            int enemy = EnemyHits(AiKickKind.Kick) + EnemyHits(AiKickKind.Bash);
            var sb = new System.Text.StringBuilder("ai kicks - whom they hit and the strike path (step 80): ");
            sb.Append(OutcomeText(AiKickKind.Kick)).Append(" | ").Append(OutcomeText(AiKickKind.Bash));
            sb.Append("; enemy hits a man-minute in contact ").Append(minutes > 0 ? (enemy / minutes).ToString("0.000", c) : "n/a");
            sb.Append(" | the strike path at the look (in reach and in front; kicks ").Append(AiKickMath.F2(r.FriendClear)).Append(" m, bashes ")
              .Append(AiKickMath.F2(r.BashPathHalfWidth)).Append(" m either side, rear rank ").Append(AiKickMath.F1(r.RearRank)).Append(" m): ");
            for (int k = 1; k <= 2; k++)
            {
                if (k > 1) sb.Append("; ");
                sb.Append(k == 1 ? "kicks" : "bashes");
                for (int p = 0; p < (int)AiKickPath.Count; p++)
                    sb.Append(p == 0 ? " " : ", ").Append(AiKickMath.PathName((AiKickPath)p)).Append(' ').Append(_paths[k, p]);
            }
            int refused = 0;
            for (int k = 1; k <= 2; k++)
                for (int p = 1; p < (int)AiKickPath.Count; p++) refused += _paths[k, p];
            sb.Append(" - REFUSED ").Append(refused).Append("; a bash's path again after its guard: ");
            for (int p = 0; p < (int)AiKickPath.Count; p++)
                sb.Append(p == 0 ? string.Empty : ", ").Append(AiKickMath.PathName((AiKickPath)p)).Append(' ').Append(_pressPaths[p]);
            sb.Append("; probes ").Append(PathProbes).Append(", friends looked at ").Append(PathFriendsLooked);
            return sb.ToString();
        }

        /// <summary>Athletics spent on the AI's kicks (points), the native reads made, the look's cost (ms).</summary>
        public double PointsSpent;
        public int NativeReads;
        public double CostMs;
        public double PlayedDelay;
        public int PlayedDelayCount;

        public static string ReasonName(int r) => TypeName(r);

        public void Asked(AiKickKind k, int reason) => _asked[(int)k, reason]++;

        public void Played(AiKickKind k, int reason, double delay)
        {
            _played[(int)k, reason]++;
            if (delay >= 0)
            {
                PlayedDelay += delay;
                PlayedDelayCount++;
            }
        }

        public void Landed(AiKickKind k, int reason, bool intoShield)
        {
            if (intoShield) _blocked[(int)k, reason]++;
            else _landed[(int)k, reason]++;
        }

        public int AskedCount(AiKickKind k) => Sum(_asked, k);
        public int PlayedCount(AiKickKind k) => Sum(_played, k);
        public int LandedCount(AiKickKind k) => Sum(_landed, k);
        public int BlockedCount(AiKickKind k) => Sum(_blocked, k);
        public int AskedFor(int reason) => _asked[1, reason] + _asked[2, reason];

        private static int Sum(int[,] a, AiKickKind k)
        {
            int n = 0;
            for (int r = 0; r < ReasonCount; r++) n += a[(int)k, r];
            return n;
        }

        /// <summary>Step 68: " (on his target a, on another enemy b, on a FRIEND c, on a horse / untracked d)" - nothing when none was noted.</summary>
        public string VictimsText()
        {
            int n = 0;
            foreach (int v in Victims) n += v;
            if (n == 0) return string.Empty;
            var parts = new List<string>();
            for (int i = 0; i < (int)AiKickVictim.Count; i++) parts.Add(AiKickMath.VictimName((AiKickVictim)i) + " " + Victims[i]);
            return " (step 68: " + string.Join(", ", parts) + ")";
        }

        /// <summary>Step 81: the [summary] "ai kicks - shares of the melee moves in kick reach" line: per man type the moves, the shares ROLLED and
        /// DONE (after the refusals) against the targets, the kicks / bashes a man-minute in contact, the refusals by group, the Athletics spent.</summary>
        public string SharesLine(in AiKickRules r)
        {
            var c = CultureInfo.InvariantCulture;
            string P(int n, int of) => of > 0 ? (100.0 * n / of).ToString("0.0", c) + "%" : "n/a";
            var sb = new System.Text.StringBuilder("ai kicks - shares of the melee moves in kick reach (step 81): ");
            for (int t = 0; t < ReasonCount; t++)
            {
                if (t > 0) sb.Append(" | ");
                int n = _moves[t];
                bool shield = t == 0;
                sb.Append(TypeName(t)).Append(' ').Append(n).Append(" moves - rolled kick ").Append(P(_rolled[t, 1], n));
                if (shield) sb.Append(" / bash ").Append(P(_rolled[t, 2], n));
                sb.Append(" / swing ").Append(P(_rolled[t, 0], n)).Append(" (target ").Append(AiKickMath.P0(shield ? r.ShieldKick : r.NoShieldKick));
                if (shield) sb.Append(" / ").Append(AiKickMath.P0(r.ShieldBash));
                sb.Append(" / ").Append(AiKickMath.P0(r.SwingShare(shield))).Append(") → DONE kick ").Append(P(Done(t, AiKickKind.Kick), n));
                if (shield) sb.Append(" / bash ").Append(P(Done(t, AiKickKind.Bash), n));
                sb.Append(" / swing ").Append(P(Done(t, AiKickKind.None), n)).Append("; refused ");
                for (int g = 1; g < AiKickMath.GroupCount; g++) sb.Append(g > 1 ? ", " : string.Empty).Append(AiKickMath.GroupName(g)).Append(' ').Append(_refused[t, g]);
            }
            double minutes = ContactManSeconds / 60.0;
            int kicks = AskedCount(AiKickKind.Kick), bashes = AskedCount(AiKickKind.Bash);
            sb.Append(" | kicks ").Append(kicks).Append(" and bashes ").Append(bashes).Append(" asked = ")
              .Append(minutes > 0 ? ((kicks + bashes) / minutes).ToString("0.00", c) + " a man-minute in contact (" + minutes.ToString("0.0", c) + " man-minutes)" : "n/a a man-minute (no contact measured)");
            sb.Append("; a raised shield kicked instead of bashed ").Append(RaisedShieldKicks);
            sb.Append("; refused by reason: ");
            bool first = true;
            for (int s = 0; s < AiKickMath.SkipCount; s++)
            {
                if (AiKickMath.GroupOf((AiKickSkip)s) == 0 || Skips[s] == 0) continue;
                sb.Append(first ? string.Empty : ", ").Append(AiKickMath.SkipName((AiKickSkip)s)).Append(' ').Append(Skips[s]);
                first = false;
            }
            if (first) sb.Append("none");
            sb.Append("; Athletics spent on them ").Append(PointsSpent.ToString("0", c));
            return sb.ToString();
        }

        public IEnumerable<string> SummaryLines(in AiKickRules r)
        {
            var lines = new List<string>();
            var c = CultureInfo.InvariantCulture;
            int asked = AskedCount(AiKickKind.Kick) + AskedCount(AiKickKind.Bash);
            int played = PlayedCount(AiKickKind.Kick) + PlayedCount(AiKickKind.Bash);
            int landed = LandedCount(AiKickKind.Kick) + LandedCount(AiKickKind.Bash);
            int blocked = BlockedCount(AiKickKind.Kick) + BlockedCount(AiKickKind.Bash);
            double minutes = ContactManSeconds / 60.0;
            lines.Add("ai kicks (step 65 / 81): " + r.Describe());
            lines.Add("ai kicks: asked " + asked + " (kicks " + AskedCount(AiKickKind.Kick) + ", bashes " + AskedCount(AiKickKind.Bash) + ") → PLAYED " + played
                      + (asked > 0 ? " (" + (100.0 * played / asked).ToString("0", c) + "%)" : string.Empty) + (PlayedDelayCount > 0 ? ", seen " + (PlayedDelay / PlayedDelayCount).ToString("0.00", c) + " s after the ask" : string.Empty)
                      + " → landed on the man " + landed + VictimsText() + ", into a shield " + blocked + (PlayedOtherKind > 0 ? "; played as the other kind " + PlayedOtherKind : string.Empty)
                      + "; " + (minutes > 0 ? (asked / minutes).ToString("0.00", c) + " asked a man-minute in contact (" + minutes.ToString("0.0", c) + " man-minutes)" : "no man-minute in contact measured (the fever's look off)")
                      + "; Athletics spent on them " + PointsSpent.ToString("0", c));
            var sb = new System.Text.StringBuilder("ai kicks by man type (asked → played → landed / into a shield): ");
            for (int k = 0; k < ReasonCount; k++)
            {
                if (k > 0) sb.Append(" | ");
                int a = _asked[1, k] + _asked[2, k], p = _played[1, k] + _played[2, k], l = _landed[1, k] + _landed[2, k], b = _blocked[1, k] + _blocked[2, k];
                sb.Append(ReasonName(k)).Append(' ').Append(a).Append(" → ").Append(p).Append(" → ").Append(l).Append(" / ").Append(b);
            }
            lines.Add(sb.ToString());
            sb.Clear().Append("ai kicks - ends: ");
            for (int e = 0; e < AiKickMath.EndCount; e++)
            {
                if (e > 0) sb.Append(", ");
                sb.Append(AiKickMath.EndName((AiKickEnd)e)).Append(' ').Append(Ends[e]);
            }
            sb.Append("; at the decision point (step 81 - ").Append(Looks).Append(" AI melee wind-ups): not a move in reach - ");
            bool first = true;
            for (int s = 0; s < AiKickMath.SkipCount; s++)
            {
                if (AiKickMath.GroupOf((AiKickSkip)s) != 0) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(AiKickMath.SkipName((AiKickSkip)s)).Append(' ').Append(Skips[s]);
            }
            if (QueueFull > 0) sb.Append(", the queue full ").Append(QueueFull);
            sb.Append("; native reads ").Append(NativeReads).Append(", the decisions' cost ").Append(CostMs.ToString("0.000", c)).Append(" ms");
            if (r.FriendClear > 0 || r.BashPathHalfWidth > 0 || FriendChecksBlocked + FriendChecksClear > 0)
                sb.Append("; step 68 / 80 - a friend first in the strike path (a kick's ").Append(AiKickMath.F1(r.FriendClear)).Append(" m either side): held ").Append(FriendChecksBlocked)
                  .Append(", clear ").Append(FriendChecksClear);
            lines.Add(sb.ToString());
            lines.Add(SharesLine(in r));
            lines.Add(PathLine(in r));
            return lines;
        }
    }
}
