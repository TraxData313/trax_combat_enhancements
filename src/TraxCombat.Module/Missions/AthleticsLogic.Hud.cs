using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TraxCombat.Hud;

namespace TraxCombat.Missions
{
    /// <summary>
    /// The HUD views' attach point (step 6; steps 7-9 add one line each to <see cref="AttachHud"/>).
    /// On the logic's FIRST TICK - the mission screen is running by then; in
    /// OnMissionBehaviorInitialize it is not (RESEARCH §G) - each view joins the screen through
    /// <c>MissionScreen.AddMissionView</c> (= AddMissionBehavior + RegisterView +
    /// OnMissionScreenInitialize; Mission ticks its behaviours by index from the end, so adding one
    /// inside this tick is safe - it ticks from the next frame). Every view is attached in every
    /// mission that has a screen; each decides every frame whether its layer is up
    /// (<see cref="TraxHudView"/>), so a Show… switch flipped mid-battle works both ways. The
    /// screen finalizes its views at mission end BEFORE this logic writes the summary, so the
    /// "hud:" lines below see the whole mission.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly List<TraxHudView> _hudViews = new List<TraxHudView>();
        private string? _hudNote;

        /// <summary>The views attached in this mission (the smoke and the summary read them).</summary>
        internal IReadOnlyList<TraxHudView> HudViews => _hudViews;

        private void AttachHud()
        {
            var screen = FindMissionScreen();
            if (screen == null)
            {
                _hudNote = "no mission screen in this mission - nothing to draw on";
                TraxLog.Info("hud", "no mission screen in this mission - no HUD views attached");
                return;
            }
            AttachHudView(screen, new PlayerAthleticsView());
            var strip = new OrderStripView();
            strip.UseSources(new GauntletOrderCards(screen), new MissionStripFormations(Mission));
            AttachHudView(screen, strip);
            // step 7 (LATER): AttachHudView(screen, new TargetAthleticsView());
            // step 8 (LATER): AttachHudView(screen, new FormationAthleticsView());
        }

        private MissionScreen? FindMissionScreen()
        {
            var screen = MissionState.Current?.Handler as MissionScreen ?? ScreenManager.TopScreen as MissionScreen;
            return screen != null && ReferenceEquals(screen.Mission, Mission) ? screen : null;
        }

        private void AttachHudView(MissionScreen screen, TraxHudView view)
        {
            try
            {
                view.UseLayer(new GauntletHudLayer(screen));
                screen.AddMissionView(view);
                _hudViews.Add(view);
                bool installed = GauntletHudLayer.PrefabInstalled(view.MovieName);
                TraxLog.Info("hud", "attached: " + view.ViewName + " (" + view.GetType().Name + ", movie " + view.MovieName
                    + (installed ? ", prefab installed" : ", prefab NOT FOUND - GUI\\Prefabs\\" + view.MovieName + ".xml missing from the module; it will not show")
                    + ") - " + view.ShowsWhen);
            }
            catch (Exception e)
            {
                Failed("hud.attach", e);
                TraxLog.Info("hud", "could not attach the " + view.ViewName + " - the battle goes on without it (see the [error] above)");
            }
        }

        /// <summary>The [summary] "hud:" lines - one or two per view.</summary>
        private void WriteHudSummary()
        {
            if (_hudViews.Count == 0)
            {
                TraxLog.Info("summary", "hud: no views attached (" + (_hudNote ?? "the first tick never came") + ")");
                return;
            }
            foreach (var view in _hudViews)
            {
                try
                {
                    var lines = view.Stats.SummaryLines();
                    view.AddSummaryLines(lines);
                    foreach (var line in lines) TraxLog.Info("summary", line);
                }
                catch (Exception e)
                {
                    Failed("hud.summary", e);
                }
            }
        }
    }
}
