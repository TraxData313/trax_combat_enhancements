using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>The HUD plumbing's pure half (step 6, reused by 7-9): the show/hide gate - the master
/// switch first, then Athletics, the view's switch, the game's Hide battle UI, photo mode, fight
/// modes, the player, the view's own condition - and the per-view stats behind the [summary]
/// "hud:" lines.</summary>
public class HudTests
{
    private static HudGateInput Input(bool mod = true, bool athletics = true, bool toggle = true, bool hideUi = false, bool photo = false,
        bool fight = true, bool needsPlayer = true, bool player = true, bool condition = true)
        => new(mod, athletics, toggle, hideUi, photo, fight, needsPlayer, player, condition);

    // ------------------------------------------------------------------ the gate

    [Fact]
    public void Everything_on_shows_the_view()
    {
        Assert.Equal(HudHide.None, HudGate.Decide(Input()));
    }

    [Fact]
    public void The_master_switch_is_checked_first()
    {
        // Everything else also off - the reason is still the master switch (CLAUDE.md: ModEnabled FIRST).
        var all = Input(mod: false, athletics: false, toggle: false, hideUi: true, photo: true, fight: false, player: false, condition: false);
        Assert.Equal(HudHide.ModOff, HudGate.Decide(all));
    }

    [Fact]
    public void Each_condition_hides_it_in_order()
    {
        Assert.Equal(HudHide.AthleticsOff, HudGate.Decide(Input(athletics: false, toggle: false, hideUi: true)));
        Assert.Equal(HudHide.ToggleOff, HudGate.Decide(Input(toggle: false, hideUi: true, player: false)));
        Assert.Equal(HudHide.HideBattleUI, HudGate.Decide(Input(hideUi: true, photo: true, fight: false)));
        Assert.Equal(HudHide.PhotoMode, HudGate.Decide(Input(photo: true, fight: false)));
        Assert.Equal(HudHide.NotFightMode, HudGate.Decide(Input(fight: false, player: false)));
        Assert.Equal(HudHide.NoPlayer, HudGate.Decide(Input(player: false, condition: false)));
        Assert.Equal(HudHide.ViewCondition, HudGate.Decide(Input(condition: false)));
    }

    [Fact]
    public void A_view_that_does_not_need_the_player_ignores_his_absence()
    {
        Assert.Equal(HudHide.None, HudGate.Decide(Input(needsPlayer: false, player: false)));
    }

    [Fact]
    public void Reasons_read_in_plain_words_with_the_views_own_switch()
    {
        Assert.Equal("ModEnabled off (the master switch)", HudGate.Describe(HudHide.ModOff, "ShowPlayerBar"));
        Assert.Equal("ShowPlayerBar off", HudGate.Describe(HudHide.ToggleOff, "ShowPlayerBar"));
        Assert.Equal("the game's Hide battle UI is on", HudGate.Describe(HudHide.HideBattleUI, "ShowPlayerBar"));
        Assert.Equal("the orders menu is closed", HudGate.Describe(HudHide.ViewCondition, "ShowInOrderMenu", "the orders menu is closed"));
        Assert.Equal("ShowInOrderMenu off", HudGate.ShortName(HudHide.ToggleOff, "ShowInOrderMenu"));
        Assert.Equal("Hide battle UI", HudGate.ShortName(HudHide.HideBattleUI, "x"));
        Assert.Equal(HudGate.ReasonCount, Enum.GetValues<HudHide>().Length);
        foreach (var h in Enum.GetValues<HudHide>())
        {
            Assert.False(string.IsNullOrWhiteSpace(HudGate.Describe(h, "T")));
            Assert.False(string.IsNullOrWhiteSpace(HudGate.ShortName(h, "T")));
        }
    }

    // ------------------------------------------------------------------ outside a battle (step 12)

    /// <summary>The player bar outside a fight: walk-about mode, the player on the field and tracked,
    /// ShowPlayerBarOutsideBattles on; the three wants off unless asked.</summary>
    private static HudGateInput Outside(bool toggle = true, bool walk = true, bool tracked = true, bool weapon = false, bool belowFull = false,
        bool lingering = false, bool player = true, bool mod = true, bool hideUi = false, bool viewToggle = true)
        => new(mod, true, viewToggle, hideUi, false, false, true, player, true, new HudOutside(toggle, walk, tracked, weapon, belowFull, lingering));

    [Fact]
    public void Outside_a_battle_the_bar_shows_with_a_weapon_drawn_or_while_it_refills()
    {
        Assert.Equal(HudHide.None, HudGate.Decide(Outside(weapon: true)));
        Assert.Equal(HudShow.WeaponDrawn, HudGate.ShowReason(Outside(weapon: true)));
        Assert.Equal(HudHide.None, HudGate.Decide(Outside(belowFull: true)));
        Assert.Equal(HudShow.Refilling, HudGate.ShowReason(Outside(belowFull: true)));
        Assert.Equal(HudShow.WeaponDrawn, HudGate.ShowReason(Outside(weapon: true, belowFull: true)));   // the weapon names it first
        Assert.Equal(HudShow.Lingering, HudGate.ShowReason(Outside(lingering: true)));
        // empty hands, full bar, no grace: gone - and the log says why
        Assert.Equal(HudHide.OutsideIdle, HudGate.Decide(Outside()));
        Assert.Equal(HudShow.Hidden, HudGate.ShowReason(Outside()));
        Assert.Equal("outside a battle: no weapon drawn and your Athletics full", HudGate.Describe(HudHide.OutsideIdle, "ShowPlayerBar"));
    }

    [Fact]
    public void Outside_a_battle_it_hides_in_menus_without_the_player_untracked_or_switched_off()
    {
        // a conversation / barter / deployment / cutscene: not the walk-about mode - even with a weapon
        Assert.Equal(HudHide.NotFightMode, HudGate.Decide(Outside(walk: false, weapon: true, belowFull: true)));
        Assert.Equal(HudHide.OutsideBattlesOff, HudGate.Decide(Outside(toggle: false, weapon: true)));
        Assert.Equal(HudHide.NoPlayer, HudGate.Decide(Outside(player: false, tracked: false, weapon: true)));
        Assert.Equal(HudHide.NotTracked, HudGate.Decide(Outside(tracked: false, weapon: true)));
        // the common gates still come first - the master switch before everything
        Assert.Equal(HudHide.ModOff, HudGate.Decide(Outside(mod: false, weapon: true)));
        Assert.Equal(HudHide.ToggleOff, HudGate.Decide(Outside(viewToggle: false, weapon: true)));
        Assert.Equal(HudHide.HideBattleUI, HudGate.Decide(Outside(hideUi: true, weapon: true)));
        Assert.Equal("outside a battle, and ShowPlayerBarOutsideBattles is off", HudGate.Describe(HudHide.OutsideBattlesOff, "ShowPlayerBar"));
        Assert.True(HudGate.IsOutsideReason(HudHide.OutsideIdle) && HudGate.IsOutsideReason(HudHide.NotTracked)
                    && HudGate.IsOutsideReason(HudHide.OutsideBattlesOff) && !HudGate.IsOutsideReason(HudHide.NotFightMode));
    }

    [Fact]
    public void A_view_without_the_outside_rule_keeps_the_fights_only_gate()
    {
        // the orders strip (no HudOutside): outside a fight it is NotFightMode, whatever the player holds
        Assert.Equal(HudHide.NotFightMode, HudGate.Decide(Input(fight: false)));
        Assert.Equal(HudShow.Hidden, HudGate.ShowReason(Input(fight: false)));
        // and in a fight nothing changed: shown, because it is a fight
        Assert.Equal(HudShow.Fight, HudGate.ShowReason(Input()));
    }

    [Fact]
    public void The_grace_keeps_a_shown_bar_for_a_moment_and_never_brings_one_back()
    {
        double g = HudGate.OutsideLingerSeconds;
        Assert.True(HudGate.Lingers(true, 10.0, 10.0));
        Assert.True(HudGate.Lingers(true, 10.0 + g * 0.9, 10.0));
        Assert.False(HudGate.Lingers(true, 10.0 + g, 10.0));                  // over
        Assert.False(HudGate.Lingers(false, 10.2, 10.0));                     // not up: no grace
        Assert.False(HudGate.Lingers(true, 5.0, double.NegativeInfinity));    // never wanted
        Assert.False(HudGate.Lingers(true, 9.0, 10.0));                       // time went back (a new mission): no grace
    }

    [Fact]
    public void Every_show_reason_reads_in_plain_words()
    {
        Assert.Equal(HudGate.ShowCount, Enum.GetValues<HudShow>().Length);
        foreach (var s in Enum.GetValues<HudShow>())
        {
            Assert.False(string.IsNullOrWhiteSpace(HudGate.Describe(s)));
            Assert.False(string.IsNullOrWhiteSpace(HudGate.ShortName(s)));
        }
        Assert.Equal("outside a battle: a weapon drawn", HudGate.Describe(HudShow.WeaponDrawn));
        Assert.Equal("outside a battle: your Athletics refilling", HudGate.Describe(HudShow.Refilling));
    }

    // ------------------------------------------------------------------ the stats

    private static HudStats Stats() => new("player bar", "TraxPlayerAthleticsBar", "ShowPlayerBar");

    [Fact]
    public void Summary_says_how_the_bar_did_outside_a_battle()
    {
        var s = Stats();
        s.HasOutsideRule = true;
        s.AddTick(4, HudHide.OutsideIdle, false);
        s.LayerCreated();
        s.NoteOutsideShown(HudShow.WeaponDrawn);
        s.AddTick(10, HudHide.None, true);
        s.AddOutsideVisible(10);
        s.LayerRemoved(HudHide.OutsideIdle);
        s.LayerCreated();
        s.NoteOutsideShown(HudShow.Refilling);
        s.AddTick(2.5, HudHide.None, true);
        s.AddOutsideVisible(2.5);
        s.LayerRemoved(HudHide.NotFightMode);
        s.AddTick(3, HudHide.NotFightMode, false);   // a conversation
        Assert.Equal("hud: player bar (movie TraxPlayerAthleticsBar) - on screen 12.5 s of 19.5 s (64%); layer built 2x, removed 2x (not a fight 1, "
                     + "outside a battle, no weapon, full 1); hidden: not a fight 3.0 s, outside a battle, no weapon, full 4.0 s; 0 refreshes; "
                     + "outside a battle: shown 2x (weapon drawn 1, refilling 1), on screen 12.5 s; errors 0", s.SummaryLines()[0]);

        var quiet = Stats();
        quiet.HasOutsideRule = true;
        quiet.AddTick(8, HudHide.OutsideIdle, false);
        Assert.Equal("hud: player bar (movie TraxPlayerAthleticsBar) - never on screen (hidden: outside a battle, no weapon, full 8.0 s); "
                     + "outside a battle: never shown; errors 0", Assert.Single(quiet.SummaryLines()));
    }

    [Fact]
    public void Time_on_screen_and_hidden_by_reason()
    {
        var s = Stats();
        s.AddTick(2.0, HudHide.NotFightMode, false);   // deployment
        s.AddTick(10.0, HudHide.None, true);
        s.AddTick(1.5, HudHide.HideBattleUI, false);
        s.AddTick(-1, HudHide.None, true);              // nonsense dt ignored
        Assert.Equal(13.5, s.TickedSeconds, 9);
        Assert.Equal(10.0, s.VisibleSeconds, 9);
        Assert.Equal(2.0, s.HiddenSeconds(HudHide.NotFightMode), 9);
        Assert.Equal(1.5, s.HiddenSeconds(HudHide.HideBattleUI), 9);
    }

    [Fact]
    public void Layers_are_counted_with_why_they_went()
    {
        var s = Stats();
        s.LayerCreated();
        s.LayerRemoved(HudHide.ToggleOff);
        s.LayerCreated();
        s.LayerRemoved(HudHide.MissionEnd);
        Assert.Equal(2, s.LayersCreated);
        Assert.Equal(2, s.LayersRemoved);
        Assert.Equal(1, s.RemovedBy(HudHide.ToggleOff));
        Assert.Equal(1, s.RemovedBy(HudHide.MissionEnd));
    }

    [Fact]
    public void Each_colour_is_news_once_and_changes_are_counted()
    {
        var s = Stats();
        Assert.True(s.NoteBand(BarBand.Green));
        Assert.False(s.NoteBand(BarBand.Green));   // same colour again: no change
        Assert.True(s.NoteBand(BarBand.Blue));
        Assert.True(s.NoteBand(BarBand.Yellow));
        Assert.False(s.NoteBand(BarBand.Blue));    // seen before
        Assert.Equal(3, s.BandChanges);
        Assert.True(s.BandSeen(BarBand.Yellow));
        Assert.False(s.BandSeen(BarBand.Red));
    }

    [Fact]
    public void Exhaustion_is_news_once_but_every_entry_counts()
    {
        var s = Stats();
        Assert.False(s.NoteExhausted(false));
        Assert.True(s.NoteExhausted(true));
        Assert.False(s.NoteExhausted(true));       // still empty: not a new entry
        Assert.False(s.NoteExhausted(false));
        Assert.False(s.NoteExhausted(true));       // second entry: counted, not news
        Assert.Equal(2, s.ExhaustedEntries);
    }

    [Fact]
    public void The_first_wound_is_news_and_the_lowest_is_kept()
    {
        var s = Stats();
        Assert.False(s.NoteUsable(1.0));
        Assert.True(s.NoteUsable(0.8));
        Assert.False(s.NoteUsable(0.62));
        Assert.False(s.NoteUsable(0.9));
        Assert.Equal(0.62, s.LowestUsable, 9);
    }

    [Fact]
    public void Summary_tells_the_whole_story()
    {
        var s = Stats();
        s.AddTick(5, HudHide.NoPlayer, false);
        s.LayerCreated();
        for (int i = 0; i < 3; i++) s.AddRefresh();
        s.AddTick(30, HudHide.None, true);
        s.AddBarTime(20, BarBand.Green, false, 1.0);
        s.AddBarTime(6, BarBand.Blue, false, 0.8);
        s.AddBarTime(4, BarBand.Red, true, 0.62);
        s.NoteBand(BarBand.Green);
        s.NoteBand(BarBand.Blue);
        s.NoteBand(BarBand.Red);
        s.NoteExhausted(true);
        s.NoteUsable(0.62);
        s.LayerRemoved(HudHide.ToggleOff);
        s.AddTick(5, HudHide.ToggleOff, false);

        var lines = s.SummaryLines();
        Assert.Equal(2, lines.Count);
        Assert.Equal("hud: player bar (movie TraxPlayerAthleticsBar) - on screen 30.0 s of 40.0 s (75%); layer built 1x, removed 1x (ShowPlayerBar off 1); "
                     + "hidden: ShowPlayerBar off 5.0 s, no player agent 5.0 s; 3 refreshes; errors 0", lines[0]);
        Assert.Equal("hud: player bar colours on screen - green 20.0 s (67%), blue 6.0 s (20%), yellow 0.0 s, orange 0.0 s, red 4.0 s (13%); "
                     + "2 colour changes; exhausted shown 1x (4.0 s); wounded part shown 10.0 s (lowest usable 62% - the last 38% of the bar dark)", lines[1]);
    }

    [Fact]
    public void Summary_of_a_bar_that_never_showed_or_failed()
    {
        var never = Stats();
        never.AddTick(12, HudHide.ToggleOff, false);
        Assert.Equal(new[] { "hud: player bar (movie TraxPlayerAthleticsBar) - never on screen (hidden: ShowPlayerBar off 12.0 s); errors 0" }, never.SummaryLines());

        var failed = Stats();
        failed.MovieFailed("prefab not installed", 3.2);
        failed.Failed("create", 3.2);
        var line = Assert.Single(failed.SummaryLines());
        Assert.Contains("movie FAILED to load at 3.2 s: prefab not installed - never shown", line);
        Assert.EndsWith("errors 1 - DISABLED at 3.2 s after an error in create (the battle went on without it)", line);
    }
}
