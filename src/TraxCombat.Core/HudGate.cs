namespace TraxCombat.Core
{
    /// <summary>
    /// Why a HUD view is not on screen - or <see cref="None"/> when it is. The first eight come
    /// from <see cref="HudGate.Decide"/>; the last two only ever name why a layer was removed.
    /// The order is the order the gate checks in (the master switch FIRST - CLAUDE.md).
    /// </summary>
    public enum HudHide
    {
        /// <summary>Shown.</summary>
        None = 0,

        /// <summary>ModEnabled off - the master switch.</summary>
        ModOff = 1,

        /// <summary>AthleticsEnabled off - nothing to show.</summary>
        AthleticsOff = 2,

        /// <summary>The view's own switch (ShowPlayerBar, ShowTargetBar, …) off.</summary>
        ToggleOff = 3,

        /// <summary>The game's "hide battle UI" is on.</summary>
        HideBattleUI = 4,

        /// <summary>Photo mode.</summary>
        PhotoMode = 5,

        /// <summary>The mission is not in a fight mode (a conversation, deployment, a cutscene, a
        /// walk through town…).</summary>
        NotFightMode = 6,

        /// <summary>No player agent on the field (not spawned yet, knocked out, dead).</summary>
        NoPlayer = 7,

        /// <summary>The view's own extra condition is not met (steps 7-9: nobody targeted, the
        /// formation markers hidden, the orders menu closed…).</summary>
        ViewCondition = 8,

        /// <summary>(removal only) The mission ended.</summary>
        MissionEnd = 9,

        /// <summary>(removal only) An error disabled the view for the rest of the mission.</summary>
        Failed = 10,
    }

    /// <summary>What the gate looks at, gathered once per frame (a struct: no allocation).</summary>
    public readonly struct HudGateInput
    {
        public HudGateInput(bool modEnabled, bool athleticsEnabled, bool toggle, bool hideBattleUI, bool photoMode, bool fightMode,
            bool needsPlayer, bool hasPlayer, bool viewConditionMet)
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
    }

    /// <summary>
    /// THE show/hide rule for every HUD view of the mod (steps 6-9, DESIGN §3): a view's layer
    /// exists exactly while <see cref="Decide"/> says <see cref="HudHide.None"/>. Read every frame,
    /// live - so an MCM toggle, the master switch, the game's "hide battle UI" or the player's death
    /// takes effect on the next frame, and the log can say WHY a bar came or went.
    /// </summary>
    public static class HudGate
    {
        /// <summary>How many <see cref="HudHide"/> values there are (per-reason counters).</summary>
        public const int ReasonCount = 11;

        public static HudHide Decide(in HudGateInput i)
        {
            if (!i.ModEnabled) return HudHide.ModOff;
            if (!i.AthleticsEnabled) return HudHide.AthleticsOff;
            if (!i.Toggle) return HudHide.ToggleOff;
            if (i.HideBattleUI) return HudHide.HideBattleUI;
            if (i.PhotoMode) return HudHide.PhotoMode;
            if (!i.FightMode) return HudHide.NotFightMode;
            if (i.NeedsPlayer && !i.HasPlayer) return HudHide.NoPlayer;
            if (!i.ViewConditionMet) return HudHide.ViewCondition;
            return HudHide.None;
        }

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
            HudHide.NoPlayer => "no player agent on the field",
            HudHide.ViewCondition => viewCondition,
            HudHide.MissionEnd => "mission end",
            _ => "an error (view disabled for this mission)",
        };

        /// <summary>A short key for the summary's per-reason counts: "ShowPlayerBar off" → as is,
        /// others one or two words.</summary>
        public static string ShortName(HudHide h, string toggleKey) => h switch
        {
            HudHide.None => "shown",
            HudHide.ModOff => "ModEnabled off",
            HudHide.AthleticsOff => "AthleticsEnabled off",
            HudHide.ToggleOff => toggleKey + " off",
            HudHide.HideBattleUI => "Hide battle UI",
            HudHide.PhotoMode => "photo mode",
            HudHide.NotFightMode => "not a fight",
            HudHide.NoPlayer => "no player agent",
            HudHide.ViewCondition => "view condition",
            HudHide.MissionEnd => "mission end",
            _ => "error",
        };
    }
}
