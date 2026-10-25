using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 72 - the game side of <see cref="OrderClickGuard"/> (AI_NOTES "Step 72"). A plain MissionView with no layer, joined to the mission
    /// screen on the logic's first tick like the HUD views - so it sits AFTER the game's MissionFormationTargetSelectionHandler in the behaviour
    /// list, and behaviours pre-display-tick from the END: its OnPreDisplayMissionTick runs before the handler's, which then recomputes its
    /// focus (or, held off, hands the orders UI an empty one) before the views' screen tick reads the release. Reads only public game state:
    /// the left button, the orders menu, the camera, each enemy formation's median point (vanilla's own focus rule, replicated by
    /// <see cref="OrderClickMath.VanillaPick"/>), the order flag at the press; writes only the handler's public targeting switch. The player's
    /// OrderController.OnOrderIssued tells it what really went out. No Harmony, nothing patched.
    /// </summary>
    internal sealed class OrderClickView : MissionView, IOrderClickWorld
    {
        private const int MaxFormations = 64;

        private readonly OrderClickGuard _guard;
        private readonly double[] _camMetres = new double[MaxFormations];
        private readonly bool[] _behind = new bool[MaxFormations];
        private readonly double[] _sx = new double[MaxFormations];
        private readonly double[] _sy = new double[MaxFormations];
        private readonly double[] _sw = new double[MaxFormations];
        private MissionFormationTargetSelectionHandler? _handler;
        private OrderController? _controller;
        private Vec3 _pressPoint;
        private double _pressOffsetX, _pressOffsetY;
        private bool _released;

        public OrderClickView()
        {
            _guard = new OrderClickGuard(this);
        }

        internal OrderClickGuard Guard => _guard;

        /// <summary>After the screen added the view: find the game's targeting handler, say what this battle gets. Returns the log text.</summary>
        internal string Prepare()
        {
            var m = Mission;
            if (m == null)
            {
                _guard.StandDown("no mission");
                return "no mission";
            }
            if (m.IsNavalBattle || m.IsNavalRaidBattle)
            {
                // War Sails switches the same handler itself (a captain's single ship) - its rule, not ours
                _guard.StandDown("a naval battle (War Sails drives the game's targeting itself)");
                return "naval - not guarding";
            }
            _handler = m.GetMissionBehavior<MissionFormationTargetSelectionHandler>();
            if (_handler == null)
            {
                _guard.StandDown("this mission has no unit targeting (MissionFormationTargetSelectionHandler) - nothing to guard");
                return "no targeting handler - not guarding";
            }
            var list = m.MissionBehaviors;
            int ours = list.IndexOf(this), theirs = list.IndexOf(_handler);
            _guard.Start();
            Subscribe();
            return "guarding: the game's targeting handler at " + theirs + ", this view at " + ours + " of " + list.Count
                   + (ours > theirs ? " (pre-display-ticks BEFORE it - same-frame)" : " (pre-display-ticks after it - one frame late, still safe)");
        }

        public override void OnPreDisplayMissionTick(float dt)
        {
            base.OnPreDisplayMissionTick(dt);
            if (_released || _handler == null) return;
            try
            {
                Subscribe();
            }
            catch (Exception e)
            {
                AthleticsLogic.Failed("orders.subscribe", e);
            }
            _guard.Tick(SafeNow());
        }

        public override void OnMissionScreenFinalize()
        {
            Teardown();
            base.OnMissionScreenFinalize();
        }

        public override void OnRemoveBehavior()
        {
            Teardown();
            base.OnRemoveBehavior();
        }

        private void Teardown()
        {
            if (_released) return;
            _released = true;
            _guard.Release();
            try
            {
                if (_controller != null) _controller.OnOrderIssued -= OnOrderIssued;
            }
            catch (Exception e)
            {
                AthleticsLogic.Failed("orders.unsubscribe", e);
            }
            _controller = null;
        }

        /// <summary>The player's order controller (it can change with his team): OnOrderIssued subscribed once per controller.</summary>
        private void Subscribe()
        {
            var c = Mission?.PlayerTeam?.PlayerOrderController;
            if (ReferenceEquals(c, _controller)) return;
            if (_controller != null) _controller.OnOrderIssued -= OnOrderIssued;
            _controller = c;
            if (c != null) c.OnOrderIssued += OnOrderIssued;
        }

        private void OnOrderIssued(OrderType orderType, MBReadOnlyList<Formation> appliedFormations, OrderController orderController, params object[] delegateParams)
        {
            bool targeted = orderType == OrderType.Charge && delegateParams != null && delegateParams.Length > 0 && delegateParams[0] is Formation;
            bool move = orderType == OrderType.Move || orderType == OrderType.MoveToLineSegment || orderType == OrderType.MoveToLineSegmentWithHorizontalLayout;
            if (targeted || move) _guard.OnOrder(targeted, move, SafeNow());
        }

        // ------------------------------------------------------------------ IOrderClickWorld

        public bool OrderModeActive
        {
            get
            {
                var m = Mission;
                if (m == null || MissionScreen == null || TaleWorlds.InputSystem.Input.IsGamepadActive) return false;
                return m.IsOrderMenuOpen || m.Mode == MissionMode.Deployment;
            }
        }

        public bool ButtonDown => Input != null && Input.IsKeyDown(InputKey.LeftMouseButton);

        public double ScreenHeight => Screen.RealScreenResolutionHeight;

        public void Look(bool press, out OrderClickLook look)
        {
            look = new OrderClickLook { IconPixels = double.NaN };
            var screen = MissionScreen;
            var m = Mission;
            var camera = screen?.CustomCamera ?? screen?.CombatCamera;
            if (screen == null || m == null || camera == null) return;
            double w = Screen.RealScreenResolutionWidth, h = Screen.RealScreenResolutionHeight;
            // step 98: the usable area's pixels - WorldToScreenInsideUsableArea's coordinates are measured from its corner
            double uw = w * ScreenManager.UsableArea.X, uh = h * ScreenManager.UsableArea.Y;
            if (!(uw > 0) || !(uh > 0))
            {
                uw = w;
                uh = h;
            }
            double cx = w / 2, cy = h / 2;
            double mx = cx, my = cy;
            if (screen.MouseVisible)
            {
                Vec2 r = Input.GetMousePositionRanged();
                mx = r.x * w;
                my = r.y * h;
            }

            // vanilla's focus rule (MissionFormationTargetSelectionHandler.OnPreDisplayMissionTick), replicated
            Vec3 camPos = camera.Position;
            int n = 0;
            var teams = m.Teams;
            for (int t = 0; t < teams.Count && n < MaxFormations; t++)
            {
                var team = teams[t];
                if (team == null || team.IsPlayerAlly) continue;
                var formations = team.FormationsIncludingEmpty;
                for (int i = 0; i < formations.Count && n < MaxFormations; i++)
                {
                    var f = formations[i];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    WorldPosition median = f.CachedMedianPosition;
                    double d = median.AsVec2.Distance(camPos.AsVec2);
                    _camMetres[n] = d;
                    _behind[n] = true;
                    _sx[n] = _sy[n] = _sw[n] = double.NaN;
                    if (d < OrderClickMath.FarMetres)
                    {
                        float x = 0, y = 0, ww = 0;
                        MBWindowManager.WorldToScreenInsideUsableArea(camera, median.GetGroundVec3() + new Vec3(0f, 0f, (float)OrderClickMath.MarkerHeightMetres), ref x, ref y, ref ww);
                        _behind[n] = ww <= 0f;
                        _sx[n] = x;
                        _sy[n] = y;
                        _sw[n] = ww;
                    }
                    n++;
                }
            }
            int pick = OrderClickMath.VanillaPick(n, _camMetres, _behind, _sx, _sy, cx, cy, h);
            look.CursorX = mx;
            look.CursorY = my;
            look.ScreenWidth = uw;
            look.ScreenHeight = uh;
            if (pick >= 0)
            {
                look.HasTarget = true;
                look.IconPixels = OrderClickMath.Distance(_sx[pick], _sy[pick], mx, my);
                look.CameraMetres = _camMetres[pick];
                look.NearCamera = _camMetres[pick] <= OrderClickMath.NearCameraMetres;
                look.ScreenX = _sx[pick];
                look.ScreenY = _sy[pick];
                look.W = _sw[pick];
                // step 98: vanilla's 10 m rule focuses a unit wherever its banner projects - is it really on the screen?
                look.OnScreen = OrderClickMath.OnScreen(_sx[pick], _sy[pick], _behind[pick], uw, uh);
            }

            // the press point: the order flag's ground point at the press, followed on the screen as the camera turns / the mouse moves
            if (press)
            {
                _pressPoint = screen.GetOrderFlagPosition();
                if (Project(camera, _pressPoint, out double px, out double py))
                {
                    _pressOffsetX = px - mx;
                    _pressOffsetY = py - my;
                }
                else
                {
                    _pressOffsetX = _pressOffsetY = 0;
                }
                look.DragPixels = 0;
                return;
            }
            look.DragPixels = Project(camera, _pressPoint, out double qx, out double qy)
                ? OrderClickMath.Distance(qx - mx, qy - my, _pressOffsetX, _pressOffsetY)
                : double.PositiveInfinity; // behind the camera: certainly a drag
        }

        public void SetTargetingDisabled(bool disabled) => _handler?.SetIsFormationTargetingDisabled(disabled);

        private static bool Project(Camera camera, Vec3 point, out double x, out double y)
        {
            float sx = 0, sy = 0, w = 0;
            MBWindowManager.WorldToScreenInsideUsableArea(camera, point, ref sx, ref sy, ref w);
            x = sx;
            y = sy;
            return w > 0f;
        }

        private double SafeNow()
        {
            try
            {
                return Mission?.CurrentTime ?? double.NaN;
            }
            catch
            {
                return double.NaN;
            }
        }
    }
}
