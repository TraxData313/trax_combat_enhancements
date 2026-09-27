namespace TraxCombat.Core
{
    /// <summary>
    /// Why a HUD view is not on screen - or <see cref="None"/> when it is. Those up to
    /// <see cref="ViewCondition"/> come from <see cref="HudGate.Decide"/>; the last two only ever name
    /// why a layer was removed. The order is the order the gate checks in (the master switch FIRST -
    /// CLAUDE.md); the three step-12 reasons are checked only outside a fight, by a view with the
    /// outside-a-battle rule (the player bar).
    /// </summary>
    public enum HudHide
    {
        /// <summary>Shown.</summary>
        None = 0,

        /// <summary>ModEnabled off - the master switch.</summary>
        ModOff = 1,

        /// <summary>AthleticsEnabled off - nothing to show.</summary>
        AthleticsOff = 2,

        /// <summary>The view's own switch (ShowPlayerBar, ShowInOrderMenu, …) off.</summary>
        ToggleOff = 3,

        /// <summary>The game's "hide battle UI" is on.</summary>
        HideBattleUI = 4,

        /// <summary>Photo mode.</summary>
        PhotoMode = 5,

        /// <summary>The mission is not in a fight mode, and either the view shows only in fights (the
        /// orders strip) or the mode is one where no bar belongs (a conversation, barter, deployment,
        /// a cutscene, a replay).</summary>
        NotFightMode = 6,

        /// <summary>(step 12, the player bar) Outside a battle while ShowPlayerBarOutsideBattles is off.</summary>
        OutsideBattlesOff = 7,

        /// <summary>No player agent on the field (not spawned yet, knocked out, dead).</summary>
        NoPlayer = 8,

        /// <summary>(step 12) Outside a battle, and this mission does not track the player's Athletics.</summary>
        NotTracked = 9,

        /// <summary>(step 12) Outside a battle, no weapon drawn and the player's Athletics full.</summary>
        OutsideIdle = 10,

        /// <summary>The view's own extra condition is not met (steps 7-9: nobody targeted, the
        /// formation markers hidden, the orders menu closed…).</summary>
        ViewCondition = 11,

        /// <summary>(removal only) The mission ended.</summary>
        MissionEnd = 12,

        /// <summary>(removal only) An error disabled the view for the rest of the mission.</summary>
        Failed = 13,
    }

    /// <summary>Why a view IS on screen (step 12) - what the log says when it appears.</summary>
    public enum HudShow
    {
        /// <summary>Not on screen.</summary>
        Hidden = 0,

        /// <summary>A fight mode (battle, duel, tournament, stealth) - the only case before step 12.</summary>
        Fight = 1,

        /// <summary>Outside a battle: a weapon or a shield in hand.</summary>
        WeaponDrawn = 2,

        /// <summary>Outside a battle: Athletics below full - it stays up while it refills.</summary>
        Refilling = 3,

        /// <summary>Outside a battle: the short grace after the last of the two
        /// (<see cref="HudGate.OutsideLingerSeconds"/> - a weapon switch empties both hands for a moment).</summary>
        Lingering = 4,
    }

    /// <summary>
    /// The outside-a-battle facts of one frame (step 12) - only a view with the rule (the player bar)
    /// fills them. <c>default</c> = no rule: outside a fight the view hides, as every view did before.
    /// </summary>
    public readonly struct HudOutside
    {
        public HudOutside(bool toggle, bool walkMode, bool tracked, bool weaponDrawn, bool belowFull, bool lingering)
        {
            Rule = true;
            Toggle = toggle;
            WalkMode = walkMode;
            Tracked = tracked;
            WeaponDrawn = weaponDrawn;
            BelowFull = belowFull;
            Lingering = lingering;
        }

        /// <summary>The view has the outside-a-battle rule.</summary>
        public bool Rule { get; }

        /// <summary>ShowPlayerBarOutsideBattles.</summary>
        public bool Toggle { get; }

        /// <summary>The mission mode lets the player walk about (the game's StartUp mode: towns,
        /// villages, the training field, a lord's hall) - not a conversation, barter, deployment,
        /// cutscene or replay.</summary>
        public bool WalkMode { get; }

        /// <summary>This mission tracks the player's Athletics (he has a reading).</summary>
        public bool Tracked { get; }

        /// <summary>A weapon or a shield in either hand (fists only = false).</summary>
        public bool WeaponDrawn { get; }

        /// <summary>His Athletics is below the top it can refill to (<see cref="AthleticsReading.BelowFull"/>).</summary>
        public bool BelowFull { get; }

        /// <summary>The bar is up and one of the two was true less than <see cref="HudGate.OutsideLingerSeconds"/> ago.</summary>
        public bool Lingering { get; }

        /// <summary>The bar is wanted outside a battle.</summary>
        public bool Wanted => WeaponDrawn || BelowFull || Lingering;
    }

    /// <summary>What the gate looks at, gathered once per frame (a struct: no allocation).</summary>
    public readonly struct HudGateInput
    {
        public HudGateInput(bool modEnabled, bool athleticsEnabled, bool toggle, bool hideBattleUI, bool photoMode, bool fightMode,
            bool needsPlayer, bool hasPlayer, bool viewConditionMet)
            : this(modEnabled, athleticsEnabled, toggle, hideBattleUI, photoMode, fightMode, needsPlayer, hasPlayer, viewConditionMet, default)
        {
        }

        public HudGateInput(bool modEnabled, bool athleticsEnabled, bool toggle, bool hideBattleUI, bool photoMode, bool fightMode,
            bool needsPlayer, bool hasPlayer, bool viewConditionMet, in HudOutside outside)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            Toggle = toggle;
            HideBattleUI = hideBattleUI;
            PhotoMode = photoMode;
            FightMode = fightMode;
            NeedsPlayer = needsPlayer;
            HasPlayer = hasPlayer;
            ViewConditionMet = viewConditionMet;
            Outside = outside;
        }

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }
        public bool Toggle { get; }
        public bool HideBattleUI { get; }
        public bool PhotoMode { get; }
        public bool FightMode { get; }
        public bool NeedsPlayer { get; }
        public bool HasPlayer { get; }
        public bool ViewConditionMet { get; }

        /// <summary>Step 12's outside-a-battle facts (default = the view has no such rule).</summary>
        public HudOutside Outside { get; }
    }

    /// <summary>
    /// THE show/hide rule for every HUD view of the mod (steps 6-9, DESIGN §3): a view's layer
    /// exists exactly while <see cref="Decide"/> says <see cref="HudHide.None"/>. Read every frame,
    /// live - so an MCM toggle, the master switch, the game's "hide battle UI" or the player's death
    /// takes effect on the next frame, and the log can say WHY a bar came or went.
    ///
    /// Step 12 (Anton's playtest: no bar in the training field, which runs in the game's walk-about
    /// mode): a view with the OUTSIDE-A-BATTLE rule (the player bar; the orders strip keeps its
    /// fights-only gate) also shows outside the fight modes - in the walk-about mode only, with the
    /// player on the field and his Athletics tracked, while he holds a weapon or a shield OR his
    /// Athletics is below full (so it stays up while it refills), plus a short grace after both end.
    /// </summary>
    public static class HudGate
    {
        /// <summary>How many <see cref="HudHide"/> values there are (per-reason counters).</summary>
        public const int ReasonCount = 14;

        /// <summary>How many <see cref="HudShow"/> values there are.</summary>
        public const int ShowCount = 5;

        /// <summary>
        /// Outside a battle, the bar stays this long after the weapon went away and the bar was full
        /// (step 12). Plumbing, not a tuning knob: switching weapons sheathes one and draws the other,
        /// both hands empty for a moment - without the grace the bar (a layer and a movie) would be
        /// torn down and rebuilt at every switch. One second covers the game's sheathe-and-draw.
        /// </summary>
        public const double OutsideLingerSeconds = 1.0;

        public static HudHide Decide(in HudGateInput i)
        {
            if (!i.ModEnabled) return HudHide.ModOff;
            if (!i.AthleticsEnabled) return HudHide.AthleticsOff;
            if (!i.Toggle) return HudHide.ToggleOff;
            if (i.HideBattleUI) return HudHide.HideBattleUI;
            if (i.PhotoMode) return HudHide.PhotoMode;
            if (!i.FightMode)
            {
                var o = i.Outside;
                if (!o.Rule || !o.WalkMode) return HudHide.NotFightMode;
                if (!o.Toggle) return HudHide.OutsideBattlesOff;
                if (i.NeedsPlayer && !i.HasPlayer) return HudHide.NoPlayer;
                if (!o.Tracked) return HudHide.NotTracked;
                if (!o.Wanted) return HudHide.OutsideIdle;
            }
            if (i.NeedsPlayer && !i.HasPlayer) return HudHide.NoPlayer;
            if (!i.ViewConditionMet) return HudHide.ViewCondition;
            return HudHide.None;
        }

        /// <summary>Why the view is on screen (<see cref="HudShow.Hidden"/> when <see cref="Decide"/>
        /// hides it) - the weapon first, then the refill, then the grace.</summary>
        public static HudShow ShowReason(in HudGateInput i)
        {
            if (Decide(in i) != HudHide.None) return HudShow.Hidden;
            if (i.FightMode) return HudShow.Fight;
            var o = i.Outside;
            if (o.WeaponDrawn) return HudShow.WeaponDrawn;
            if (o.BelowFull) return HudShow.Refilling;
            return HudShow.Lingering;
        }

        /// <summary>The grace (step 12): the bar is up outside a battle and it was last wanted (a weapon
        /// drawn or Athletics below full) less than <see cref="OutsideLingerSeconds"/> ago. A bar that is
        /// not up never lingers - the grace only keeps a shown bar, it never brings one back.</summary>
        public static bool Lingers(bool shownNow, double now, double lastWantedAt) =>
            shownNow && now >= lastWantedAt && now - lastWantedAt < OutsideLingerSeconds;

        /// <summary>A reason that belongs to the outside-a-battle rule (the log's own rate bucket).</summary>
        public static bool IsOutsideReason(HudHide h) =>
            h == HudHide.OutsideBattlesOff || h == HudHide.NotTracked || h == HudHide.OutsideIdle;

        /// <summary>
        /// The reason in plain words: "ModEnabled off (the master switch)", "ShowPlayerBar off" …
        /// <paramref name="toggleKey"/> names the view's switch, <paramref name="viewCondition"/> its
        /// extra condition ("nobody targeted").
        /// </summary>
        public static string Describe(HudHide h, string toggleKey, string viewCondition = "its own condition not met") => h switch
        {
            HudHide.None => "shown",
            HudHide.ModOff => "ModEnabled off (the master switch)",
            HudHide.AthleticsOff => "AthleticsEnabled off",
            HudHide.ToggleOff => toggleKey + " off",
            HudHide.HideBattleUI => "the game's Hide battle UI is on",
            HudHide.PhotoMode => "photo mode",
            HudHide.NotFightMode => "not a fight (mission mode)",
            HudHide.OutsideBattlesOff => "outside a battle, and " + SettingsSchema.ShowPlayerBarOutsideBattles.Key + " is off",
            HudHide.NoPlayer => "no player agent on the field",
            HudHide.NotTracked => "outside a battle, and this mission does not track your Athletics",
            HudHide.OutsideIdle => "outside a battle: no weapon drawn and your Athletics full",
            HudHide.ViewCondition => viewCondition,
            HudHide.MissionEnd => "mission end",
            _ => "an error (view disabled for this mission)",
        };

        /// <summary>A short key for the summary's per-reason counts: "ShowPlayerBar off" → as is,
        /// others one or two words.</summary>
        public static string ShortName(HudHide h, string toggleKey, string conditionName = "view condition") => h switch
        {
            HudHide.None => "shown",
            HudHide.ModOff => "ModEnabled off",
            HudHide.AthleticsOff => "AthleticsEnabled off",
            HudHide.ToggleOff => toggleKey + " off",
            HudHide.HideBattleUI => "Hide battle UI",
            HudHide.PhotoMode => "photo mode",
            HudHide.NotFightMode => "not a fight",
            HudHide.OutsideBattlesOff => SettingsSchema.ShowPlayerBarOutsideBattles.Key + " off",
            HudHide.NoPlayer => "no player agent",
            HudHide.NotTracked => "not tracked",
            HudHide.OutsideIdle => "outside a battle, no weapon, full",
            HudHide.ViewCondition => conditionName,
            HudHide.MissionEnd => "mission end",
            _ => "error",
        };

        /// <summary>Why it is on screen, in plain words (the log's "layer created" line).</summary>
        public static string Describe(HudShow s) => s switch
        {
            HudShow.Fight => "in a fight",
            HudShow.WeaponDrawn => "outside a battle: a weapon drawn",
            HudShow.Refilling => "outside a battle: your Athletics refilling",
            HudShow.Lingering => "outside a battle: the " + OutsideLingerSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                 + " s grace after the weapon went away or the bar filled",
            _ => "hidden",
        };

        /// <summary>A short key for the summary ("weapon drawn 2, refilling 1").</summary>
        public static string ShortName(HudShow s) => s switch
        {
            HudShow.Fight => "in a fight",
            HudShow.WeaponDrawn => "weapon drawn",
            HudShow.Refilling => "refilling",
            HudShow.Lingering => "grace",
            _ => "hidden",
        };
    }
}
