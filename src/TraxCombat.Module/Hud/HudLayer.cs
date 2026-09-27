using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace TraxCombat.Hud
{
    /// <summary>
    /// One frame of the world as a HUD view sees it - read from the game by
    /// <see cref="TraxHudView"/> (ReadFrame) or made up by the offline smoke, so the whole
    /// show/hide/refresh logic runs without a game. A struct: nothing allocated per frame.
    /// </summary>
    public struct HudFrame
    {
        /// <summary>Seconds since the last frame (real time).</summary>
        public float Dt;

        /// <summary>Mission time (Mission.CurrentTime).</summary>
        public double Now;

        /// <summary>MBCommon.IsPaused - nothing changes while paused (vanilla's HUD views skip it too).</summary>
        public bool Paused;

        /// <summary>BannerlordConfig.HideBattleUI - the game's "hide battle UI".</summary>
        public bool HideBattleUI;

        /// <summary>MissionScreen.IsPhotoModeEnabled.</summary>
        public bool PhotoMode;

        /// <summary>(int)Mission.Mode.</summary>
        public int Mode;

        /// <summary>Mission.MainAgent (may be null).</summary>
        public Agent? Player;

        /// <summary>The player agent exists and is active (read by the game side: IsActive is not
        /// for the smoke's native-less agents).</summary>
        public bool PlayerActive;

        /// <summary>The fight modes DESIGN §3 shows bars in: battle, duel, tournament - and stealth
        /// (a stealth mission's fights cost Athletics too; step 6's call).</summary>
        public static bool IsFightMode(int mode) =>
            mode == (int)MissionMode.Battle || mode == (int)MissionMode.Duel || mode == (int)MissionMode.Tournament || mode == (int)MissionMode.Stealth;

        /// <summary>"Battle", "Deployment", … for the log (allocates - log lines only).</summary>
        public static string ModeName(int mode) => ((MissionMode)mode).ToString();
    }

    /// <summary>
    /// The engine side of a HUD view's layer behind one seam (the offline smoke plays it, like
    /// step 5d's IStepBackBody): create the Gauntlet layer with its movie over a ViewModel, remove
    /// it, suspend it. <see cref="GauntletHudLayer"/> is the real one.
    /// </summary>
    internal interface IHudLayer
    {
        /// <summary>Builds the layer, loads <paramref name="movieName"/> over <paramref name="dataSource"/>
        /// and puts it on screen. False (nothing left on screen) when the movie did not load;
        /// <paramref name="detail"/> says why - or, on success, what loaded.</summary>
        bool Create(string layerName, string movieName, ViewModel dataSource, out string detail);

        /// <summary>Takes the layer off screen and releases its movie (no-op when none).</summary>
        void Destroy();

        /// <summary>Vanilla's view suspension (another view asks for the screen) - the layer stays, hidden.</summary>
        void SetSuspended(bool suspended);
    }

    /// <summary>
    /// The real layer: the vanilla recipe of <c>MissionGauntletCrosshair</c> /
    /// <c>MissionGauntletAgentStatus</c> (RESEARCH §G) - <c>new GauntletLayer</c>, <c>LoadMovie</c>,
    /// <c>MissionScreen.AddLayer</c>; a passive overlay (no input restrictions, never a focus layer,
    /// the prefab's widgets take no events).
    /// </summary>
    internal sealed class GauntletHudLayer : IHudLayer
    {
        /// <summary>Draw order among the mission screen's layers - vanilla's crosshair uses 1.
        /// Engine plumbing, not a tunable.</summary>
        private const int LocalOrder = 1;

        private readonly MissionScreen _screen;
        private GauntletLayer? _layer;
        private GauntletMovieIdentifier? _movie;

        public GauntletHudLayer(MissionScreen screen)
        {
            _screen = screen;
        }

        /// <summary>The module's prefab is known to the game (every enabled module's GUI\Prefabs
        /// is scanned at startup) - false means the GUI folder did not ship.</summary>
        public static bool PrefabInstalled(string movieName)
        {
            var factory = UIResourceManager.WidgetFactory;
            return factory == null || factory.IsCustomType(movieName);
        }

        public bool Create(string layerName, string movieName, ViewModel dataSource, out string detail)
        {
            if (!PrefabInstalled(movieName))
            {
                detail = "the prefab " + movieName + ".xml is not installed (GUI\\Prefabs missing from the module folder?)";
                return false;
            }
            var layer = new GauntletLayer(layerName, LocalOrder, false);
            var movie = layer.LoadMovie(movieName, dataSource);
            var root = movie?.Movie?.RootWidget;
            if (root == null)
            {
                // Never put on screen: removing a layer releases its movies, and releasing one that
                // did not load throws inside the game (GauntletMovie.Release with no prefab). The
                // layer object is simply dropped - once per mission at most (the view disables itself).
                detail = "LoadMovie(" + movieName + ") gave no root widget";
                return false;
            }
            _screen.AddLayer(layer);
            _layer = layer;
            _movie = movie;
            detail = root.GetAllChildrenAndThisRecursive().Count + " widgets";
            return true;
        }

        public void Destroy()
        {
            var layer = _layer;
            var movie = _movie;
            _layer = null;
            _movie = null;
            if (layer == null) return;
            try
            {
                // Release first: a layer finalized with a loaded movie asserts (GauntletLayer.OnFinalize).
                if (movie != null) layer.ReleaseMovie(movie);
            }
            finally
            {
                _screen.RemoveLayer(layer);
            }
        }

        public void SetSuspended(bool suspended)
        {
            if (_layer != null) ScreenManager.SetSuspendLayer(_layer, suspended);
        }
    }
}
