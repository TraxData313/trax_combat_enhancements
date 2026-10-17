using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TraxCombat.Core;

namespace TraxCombat.Hud
{
    /// <summary>
    /// Step 44's engine side behind one seam (the offline smoke plays it, like step 20's <see cref="IFormationMarkerSource"/>): WHEN the marks
    /// show (vanilla's own "markers shown" flag), the camera and the screen for one frame, where a man in a fever stands for the camera, and
    /// the projection of his mark's point. <see cref="GameFeverMarks"/> is the real one. Every call allocation-free.
    /// </summary>
    internal interface IFeverMarkSource
    {
        /// <summary>Vanilla's own "markers shown" (step 20's flag: <c>MissionFormationMarkerVM.IsEnabled</c> - ALT held or the orders menu
        /// open). False when it cannot be read (the view then copies the rule).</summary>
        bool TryReadShown(out bool shown);

        /// <summary>A show begins (our layer is about to be rebuilt): forget anything cached from the last one.</summary>
        void NewShow();

        /// <summary>This frame's camera and screen into <paramref name="frame"/> (ScreenWidth / Height in pixels, the UI scale). False = no
        /// camera to project from - no marks this frame.</summary>
        bool BeginFrame(FeverMarkFrame frame);

        /// <summary>Where <paramref name="agent"/> stands for the camera: <see cref="FeverMark.Key"/>, Side, the mark's world point
        /// (<paramref name="heightMetres"/> above his feet), the camera distance, in front or not. False = not on the field (no team, not
        /// active).</summary>
        bool Locate(Agent agent, float heightMetres, ref FeverMark m);

        /// <summary>The mark's world point → screen pixels (ScreenX / ScreenY). False = no valid point (behind the camera after all).</summary>
        bool Project(ref FeverMark m);

        /// <summary>What it reads (log lines only - allocates).</summary>
        string Describe();
    }

    /// <summary>
    /// The real source (AI_NOTES "Step 44"): vanilla's flag through step 20's <see cref="GauntletFormationMarkers"/> (the same live read the
    /// ALT labels use, so both come and go together); the combat camera (<c>MissionScreen.CombatCamera</c> - RTS Camera's free camera moves
    /// this one too); each man's feet (<c>Agent.Position</c>) + FeverMarkHeightMetres; his side from his team against the player's
    /// (<c>Team.IsPlayerTeam</c> / <c>IsPlayerAlly</c>); the projection = vanilla's own (<c>MBWindowManager.WorldToScreen</c>, as the formation
    /// markers and <see cref="ProjectedFormationMarkers"/> project); the screen in pixels and the UI scale from our own layer (or vanilla's
    /// marker layer). Nothing patched, nothing of vanilla's ALT icon touched.
    /// </summary>
    internal sealed class GameFeverMarks : IFeverMarkSource
    {
        private readonly MissionScreen _screen;
        private readonly IFormationMarkerSource _markers;
        private readonly string _ownLayer;
        private TaleWorlds.Engine.Camera? _camera;
        private Vec3 _cameraPosition;
        private Vec3 _cameraDirection;
        private GauntletLayer? _layer;
        private bool _ownScale;

        public GameFeverMarks(MissionScreen screen, IFormationMarkerSource markers, string ownLayer)
        {
            _screen = screen;
            _markers = markers;
            _ownLayer = ownLayer;
        }

        public bool TryReadShown(out bool shown) => _markers.TryReadShown(out shown);

        public bool BeginFrame(FeverMarkFrame frame)
        {
            var camera = _screen.CombatCamera;
            _camera = camera;
            if (camera == null) return false;
            _cameraPosition = camera.Position;
            _cameraDirection = camera.Direction;
            ReadScreen(frame);
            return true;
        }

        /// <summary>The screen in pixels and the UI scale: our own layer's context (it is up while marks show), else vanilla's marker layer's.</summary>
        private void ReadScreen(FeverMarkFrame frame)
        {
            if (_layer == null || _layer.UIContext == null)
            {
                _layer = _screen.FindLayer<GauntletLayer>(_ownLayer);
                _ownScale = _layer != null;
                if (_layer == null) _layer = _screen.FindLayer<GauntletLayer>(GauntletFormationMarkers.LayerName);
            }
            var context = _layer?.UIContext;
            if (context == null)
            {
                _layer = null;
                return; // the frame keeps its last screen and scale
            }
            var page = context.EventManager.PageSize;
            frame.ScreenWidth = page.X;
            frame.ScreenHeight = page.Y;
            frame.Scale = context.CustomScale > 0f ? context.CustomScale : 1f;
        }

        /// <summary>Our layer is rebuilt at each show: forget the cached one (the view calls it when a show starts).</summary>
        public void NewShow() => _layer = null;

        public bool Locate(Agent agent, float heightMetres, ref FeverMark m)
        {
            var team = agent.Team; // managed first; IsActive reads the engine's state
            if (team == null || !agent.IsActive()) return false;
            m.Key = agent.Index;
            m.Side = team.IsPlayerTeam ? (int)MarkerTeam.Yours : team.IsPlayerAlly ? (int)MarkerTeam.Ally : (int)MarkerTeam.Enemy;
            var p = agent.Position;
            p.z += heightMetres;
            m.WorldX = p.x;
            m.WorldY = p.y;
            m.WorldZ = p.z;
            var d = p - _cameraPosition;
            m.Distance = d.Length;
            m.InFront = Vec3.DotProduct(d, _cameraDirection) > 0f;
            return true;
        }

        public bool Project(ref FeverMark m)
        {
            var camera = _camera;
            if (camera == null) return false;
            float x = 0f, y = 0f, w = 0f;
            MBWindowManager.WorldToScreen(camera, new Vec3(m.WorldX, m.WorldY, m.WorldZ), ref x, ref y, ref w);
            if (!(w > 0f) || !MathF.IsValidValue(x) || !MathF.IsValidValue(y)) return false;
            m.ScreenX = x;
            m.ScreenY = y;
            return true;
        }

        public string Describe() =>
            "the combat camera's projection (MBWindowManager.WorldToScreen) of each man's feet + the height, the screen and UI scale from "
            + (_layer == null ? "no layer yet" : _ownScale ? "our own layer" : "vanilla's marker layer")
            + "; shown with vanilla's flag (" + _markers.Describe() + ")";
    }
}
