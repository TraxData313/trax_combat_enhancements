using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 106f (Toni's first openings playtest, 2026-11-09, build b5ddaaf; AI_NOTES "106f", DESIGN 95 (o)): the live pose read back (shown / not
/// shown), where a follow-up's direction is judged (the swing it commits to - the wind-up's first read differed 14 of 33), why a held follow-up
/// was BLOCKED ANYWAY, and the held guard's 'can defend' setting.
/// </summary>
public class Step106fTests
{
    private const int Up = OpeningMath.AttackUp, Down = OpeningMath.AttackDown, Left = OpeningMath.AttackLeft, Right = OpeningMath.AttackRight;

    [Fact]
    public void The_pose_reads_are_at_point_two_and_point_five_seconds()
    {
        Assert.Equal(0.2, OpeningForceMath.PoseFirstReadSeconds);
        Assert.Equal(0.5, OpeningForceMath.PoseLastReadSeconds);
        Assert.True(OpeningForceMath.PoseFirstReadSeconds < OpeningForceMath.PoseLastReadSeconds);
    }

    [Fact]
    public void The_live_pose_verdict_waits_for_the_last_read_and_names_what_he_did_instead()
    {
        // the wanted guard at the first read = shown; at the last = shown late
        Assert.Equal(OpeningPoseResult.Shown, OpeningForceMath.PoseVerdict(OpeningMath.GuardUp, false, OpeningMath.GuardUp, true, first: true));
        Assert.Equal(OpeningPoseResult.ShownLate, OpeningForceMath.PoseVerdict(OpeningMath.GuardUp, false, OpeningMath.GuardUp, true, first: false));
        // not yet at the first read (his parry's own reaction): wait
        Assert.Null(OpeningForceMath.PoseVerdict(OpeningMath.GuardUp, false, OpeningMath.GuardLeft, true, first: true));
        Assert.Null(OpeningForceMath.PoseVerdict(OpeningMath.GuardUp, false, -1, false, first: true));
        // the last read: defending another way / not defending at all
        Assert.Equal(OpeningPoseResult.OtherGuard, OpeningForceMath.PoseVerdict(OpeningMath.GuardUp, false, OpeningMath.GuardLeft, true, first: false));
        Assert.Equal(OpeningPoseResult.NotDefending, OpeningForceMath.PoseVerdict(OpeningMath.GuardUp, false, Right, false, first: false));
        // a lowered guard: no defend stage, no guard direction = shown; a guard still up at the last read = not lowered
        Assert.Equal(OpeningPoseResult.Shown, OpeningForceMath.PoseVerdict(OpeningMath.Unknown, true, -1, false, first: true));
        Assert.Equal(OpeningPoseResult.StillGuarding, OpeningForceMath.PoseVerdict(OpeningMath.Unknown, true, OpeningMath.GuardDown, true, first: false));
        foreach (OpeningPoseResult r in System.Enum.GetValues(typeof(OpeningPoseResult)))
            if (r != OpeningPoseResult.Count) Assert.NotEqual("?", OpeningForceMath.PoseName(r));
    }

    [Fact]
    public void Blocked_anyway_is_explained_another_direction_first_then_the_shield_then_his_guard()
    {
        // judged into the side (right), but the collision says left: the swing went another way - whatever else
        Assert.Equal(OpeningAnyway.OtherDirection, OpeningForceMath.AnywayCause(Right, Left, true, true, OpeningMath.GuardLeft, OpeningMath.GuardRight));
        // the same direction - a shield: held (not lowered) / did not come down (lowered)
        Assert.Equal(OpeningAnyway.ShieldHeld, OpeningForceMath.AnywayCause(Right, Right, true, false, OpeningMath.GuardRight, OpeningMath.GuardRight));
        Assert.Equal(OpeningAnyway.ShieldNotDown, OpeningForceMath.AnywayCause(Right, Right, true, true, -1, OpeningMath.Unknown));
        // a weapon: his guard at contact the forced one = the engine parried anyway; another = his own guard back; none read
        Assert.Equal(OpeningAnyway.ForcedGuardParried, OpeningForceMath.AnywayCause(Down, Down, false, false, OpeningMath.GuardUp, OpeningMath.GuardUp));
        Assert.Equal(OpeningAnyway.OwnGuard, OpeningForceMath.AnywayCause(Down, Down, false, false, OpeningMath.GuardDown, OpeningMath.GuardUp));
        Assert.Equal(OpeningAnyway.Unread, OpeningForceMath.AnywayCause(Down, Down, false, false, -1, OpeningMath.GuardUp));
        // the contact's direction unknown: not compared
        Assert.Equal(OpeningAnyway.ForcedGuardParried, OpeningForceMath.AnywayCause(Up, OpeningMath.Unknown, false, false, OpeningMath.GuardDown, OpeningMath.GuardDown));
        foreach (OpeningAnyway a in System.Enum.GetValues(typeof(OpeningAnyway)))
            if (a != OpeningAnyway.Count) Assert.NotEqual("?", OpeningForceMath.AnywayName(a));
    }

    [Fact]
    public void The_force_stats_count_poses_and_causes_and_the_summary_says_them()
    {
        var s = new OpeningForceStats();
        s.NoteLivePose(OpeningPoseResult.Shown, shield: false, lowered: false);
        s.NoteLivePose(OpeningPoseResult.ShownLate, shield: true, lowered: false);
        s.NoteLivePose(OpeningPoseResult.StillGuarding, shield: true, lowered: true);
        s.NoteAnyway(OpeningAnyway.OtherDirection);
        s.NoteAnyway(OpeningAnyway.ShieldNotDown);
        s.FlagsOff = 2;
        s.FlagsBack = 2;
        s.HoldFlagsOff = 2;
        Assert.Equal(1, s.PoseWeapon[(int)OpeningPoseResult.Shown]);
        Assert.Equal(1, s.PoseShield[(int)OpeningPoseResult.ShownLate]);
        Assert.Equal(1, s.PoseLowered[(int)OpeningPoseResult.StillGuarding]);
        Assert.Equal(2, s.BlockedAnyway);
        Assert.Equal(1, s.Anyway[(int)OpeningAnyway.OtherDirection]);
        var r = OpeningRules.From(TestSettings.Defaults());
        string line = s.SummaryLine(in r, false);
        Assert.Contains("THE LIVE POSE read back (step 106f, 0.2 s / 0.5 s after forcing): the balance guard with a weapon - shown at 0.2 s 1", line);
        Assert.Contains("; with a shield - shown at 0.2 s 0, shown by 0.5 s 1", line);
        Assert.Contains("lowered for a held follow-up -", line);
        Assert.Contains("NOT lowered - a guard still up 1", line);
        Assert.Contains("BLOCKED ANYWAY 2", line);
        Assert.Contains("BLOCKED ANYWAY by cause your swing went another way at contact 1", line);
        Assert.Contains("his shield did not come down in time 1", line);
        Assert.Contains("CanDefend off / back 2 / 2 (held follow-ups 2)", line);
        Assert.Contains("the test tool's poses read back 0", line);
    }

    [Fact]
    public void Where_the_follow_up_was_judged_and_the_wind_up_read_against_it_are_counted()
    {
        var s = new OpeningStats();
        s.NoteJudged(OpeningJudgedAt.Release, Left, Left);
        s.NoteJudged(OpeningJudgedAt.Release, Left, Right); // the wind-up's first read was not the swing it committed to
        s.NoteJudged(OpeningJudgedAt.WindUp, Up, Up);
        s.NoteJudged(OpeningJudgedAt.Contact, OpeningMath.Unknown, Down);
        Assert.Equal(2, s.JudgedAt[(int)OpeningJudgedAt.Release]);
        Assert.Equal(1, s.JudgedAt[(int)OpeningJudgedAt.WindUp]);
        Assert.Equal(1, s.JudgedAt[(int)OpeningJudgedAt.Contact]);
        Assert.Equal(2, s.WindUpAgree);
        Assert.Equal(1, s.WindUpDiffer);
        Assert.Equal(1, s.WindUpUnread);
        s.NoteDirection(Right, Right);
        s.NoteDirection(Right, Left);
        var r = OpeningRules.From(TestSettings.Defaults());
        string line = s.SummaryLine(in r);
        Assert.Contains("your follow-up's direction judged (step 106f) at the release 2, already at the wind-up 1, only at the contact 1 - the wind-up's first read vs the committed swing: agree 2, differ 1, unread 1", line);
        Assert.Contains("the judged direction vs the contact's: agree 1, differ 1, unread 0", line);
    }

    [Fact]
    public void The_held_guard_takes_his_can_defend_by_default_on_the_advanced_page()
    {
        var p = SettingsSchema.OpeningHoldTakesDefend;
        Assert.Equal(ParamType.Bool, p.Type);
        Assert.Equal(1, p.Default);
        Assert.Same(SettingsSchema.ImpactGroup, p.Group);
        Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.OpeningHoldMaxSeconds) + 1, all.IndexOf(p));
        Assert.True(TestSettings.Defaults().OpeningHoldTakesDefend);
    }

    private static class TestSettings
    {
        public static TraxSettings Defaults()
        {
            var s = new TraxSettings();
            return s;
        }
    }
}
