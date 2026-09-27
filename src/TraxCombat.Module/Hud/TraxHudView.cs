using System;
using System.Globalization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// THE BASE OF EVERY HUD VIEW OF THE MOD (step 6; steps 7-9 add theirs on top). A view is a
    /// <see cref="MissionView"/> that owns ONE Gauntlet layer with ONE movie (a prefab in
    /// module\GUI\Prefabs) over ONE ViewModel, and the layer exists exactly while
    /// <see cref="HudGate.Decide"/> says so - read every frame, live:
    ///   ModEnabled (the master switch, FIRST) → AthleticsEnabled → the view's own Show… switch →
    ///   the game's Hide battle UI → photo mode → a fight mode → the player on the field → the
    ///   view's own extra condition (<see cref="ViewConditionMet"/>).
    /// So an MCM toggle mid-battle, the master switch, the game's own UI toggle or the player's
    /// death builds or removes the layer on the next frame, and every build / removal is logged
    /// with its reason ([hud] lines) and counted for the [summary] (<see cref="HudStats"/>).
    ///
    /// While the layer is up the view refreshes its ViewModel every HudRefreshSeconds (live).
    ///
    /// FAIL SAFE: every entry point is wrapped. An exception (or a movie that does not load)
    /// logs [error] with its stack, removes the layer, and DISABLES this view for the rest of the
    /// mission - the battle and the other views go on. Nothing here ever throws into the game.
    ///
    /// ADDED BY <c>AthleticsLogic</c> on its first tick through <c>MissionScreen.AddMissionView</c>
    /// (RESEARCH §G - not in OnMissionBehaviorInitialize, the screen is not running then), with a
    /// <see cref="GauntletHudLayer"/>; the offline smoke drives <see cref="Tick"/> with made-up
    /// frames and a stand-in <see cref="IHudLayer"/>.
    ///
    /// A NEW VIEW (steps 7-9): derive, pass (name for the log, movie = prefab file name, its Show…
    /// setting) to the base, implement <see cref="CreateDataSource"/> and <see cref="Refresh"/>;
    /// override <see cref="NeedsPlayer"/> / <see cref="ViewConditionMet"/> if it has other
    /// conditions; add one line to <c>AthleticsLogic.AttachHud</c>. AI_NOTES "Step 6" has the recipe.
    /// </summary>
    public abstract class TraxHudView : MissionView
    {
        private IHudLayer? _host;
        private ViewModel? _dataSource;
        private bool _layerUp;
        private bool _suspended;
        private bool _disabled;
        private bool _finished;
        private bool _decided;
        private HudHide _hide;
        private double _nextRefresh;
        private bool _firstPush;
        private int _mode;

        protected TraxHudView(string viewName, string movieName, ParamDef toggle)
        {
            ViewName = viewName;
            MovieName = movieName;
            Toggle = toggle;
            Stats = new HudStats(viewName, movieName, toggle.Key);
        }

        /// <summary>"player bar" - how the log names this view.</summary>
        public string ViewName { get; }

        /// <summary>The prefab (module\GUI\Prefabs\&lt;MovieName&gt;.xml) - also the layer's name.</summary>
        public string MovieName { get; }

        /// <summary>The view's own Show… setting.</summary>
        internal ParamDef Toggle { get; }

        /// <summary>This mission's numbers ([summary] hud: lines).</summary>
        internal HudStats Stats { get; }

        internal bool IsLayerUp => _layerUp;

        internal bool IsDisabled => _disabled;

        /// <summary>The gate's last answer (None = shown).</summary>
        internal HudHide LastDecision => _hide;

        /// <summary>The layer's engine side - set by the attach (real) or the smoke (stand-in).</summary>
        internal void UseLayer(IHudLayer host) => _host = host;

        // ------------------------------------------------------------------ what a view adds

        /// <summary>A fresh ViewModel for a new layer (sized from the live settings, so the first
        /// frame is right). Called each time the layer is built.</summary>
        protected abstract ViewModel CreateDataSource(in HudFrame f);

        /// <summary>Push the current values into the ViewModel (every HudRefreshSeconds while the
        /// layer is up). <paramref name="first"/>: the first push on this layer (log it). Return
        /// false when there was nothing to show yet (then the next refresh is still "first").</summary>
        protected abstract bool Refresh(in HudFrame f, bool first);

        /// <summary>Hidden while the player has no agent on the field (default).</summary>
        protected virtual bool NeedsPlayer => true;

        /// <summary>The view's own extra condition (steps 7-9: someone targeted, markers shown, the
        /// orders menu open…). Checked every frame after the common ones.</summary>
        protected virtual bool ViewConditionMet(in HudFrame f) => true;

        /// <summary>That condition's "not met" in plain words, for the log.</summary>
        protected virtual string ViewConditionText => "its own condition not met";

        /// <summary>That condition met, for the attach line ("while the orders menu is open"); null = none.</summary>
        protected virtual string? ViewConditionWhen => null;

        /// <summary>True for a view whose own condition comes and goes all the time (the orders menu
        /// opening and closing): only the FIRST build per mission is logged in full, later builds and
        /// removals caused by that condition go to the verbose log (the stats still count them all).</summary>
        protected virtual bool QuietConditionToggles => false;

        /// <summary>One frame with the layer on screen (per-frame stats; no allocation please).</summary>
        protected virtual void OnVisibleFrame(float dt)
        {
        }

        /// <summary>One frame with the layer on screen, before the refresh check - for views that
        /// follow something on screen every frame (step 9 places its cells under the vanilla cards).
        /// No allocation please.</summary>
        protected virtual void OnLayerFrame(in HudFrame f)
        {
        }

        /// <summary>The view's own [summary] lines after the common ones (tag-less).</summary>
        internal virtual void AddSummaryLines(System.Collections.Generic.List<string> lines)
        {
        }

        /// <summary>The layer is going: drop references to the ViewModel (it is finalized next).</summary>
        protected virtual void OnLayerGone()
        {
        }

        /// <summary>When the view shows, in words - the attach line of the log.</summary>
        internal string ShowsWhen =>
            "shown while ModEnabled, AthleticsEnabled and " + Toggle.Key + " are on, the game's Hide battle UI and photo mode are off, "
            + "in a fight (battle, duel, tournament or stealth mode)"
            + (NeedsPlayer ? (ViewConditionWhen != null ? ", you are on the field" : " and you are on the field") : string.Empty)
            + (ViewConditionWhen != null ? " and " + ViewConditionWhen : string.Empty);

        // ------------------------------------------------------------------ the game's hooks

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (_disabled || _finished) return;
            HudFrame frame;
            try
            {
                frame = ReadFrame(dt);
            }
            catch (Exception e)
            {
                Fail("read", e, SafeNow());
                return;
            }
            Tick(in frame);
        }

        public override void OnMissionScreenFinalize()
        {
            try
            {
                Finish(SafeNow());
            }
            catch (Exception e)
            {
                AthleticsLogic.Failed("hud.finalize", e);
            }
            base.OnMissionScreenFinalize();
        }

        public override void OnRemoveBehavior()
        {
            try
            {
                Finish(SafeNow());
            }
            catch (Exception e)
            {
                AthleticsLogic.Failed("hud.remove", e);
            }
            base.OnRemoveBehavior();
        }

        protected override void OnSuspendView()
        {
            try
            {
                _suspended = true;
                if (_layerUp) _host?.SetSuspended(true);
            }
            catch (Exception e)
            {
                Fail("suspend", e, SafeNow());
            }
        }

        protected override void OnResumeView()
        {
            try
            {
                _suspended = false;
                if (_layerUp) _host?.SetSuspended(false);
            }
            catch (Exception e)
            {
                Fail("resume", e, SafeNow());
            }
        }

        private HudFrame ReadFrame(float dt)
        {
            var m = Mission;
            var main = m.MainAgent;
            var screen = MissionScreen;
            return new HudFrame
            {
                Dt = dt,
                Now = m.CurrentTime,
                Paused = MBCommon.IsPaused,
                HideBattleUI = BannerlordConfig.HideBattleUI,
                PhotoMode = screen != null && screen.IsPhotoModeEnabled,
                Mode = (int)m.Mode,
                Player = main,
                PlayerActive = main != null && main.IsActive(),
                OrderMenuOpen = m.IsOrderMenuOpen,
            };
        }

        // ------------------------------------------------------------------ the logic (smoke-driven too)

        /// <summary>One frame: decide, build / remove the layer, refresh. Never throws.</summary>
        internal void Tick(in HudFrame f)
        {
            if (_disabled || _finished) return;
            try
            {
                TickCore(in f);
            }
            catch (Exception e)
            {
                Fail("tick", e, f.Now);
            }
        }

        private void TickCore(in HudFrame f)
        {
            if (f.Paused) return;
            _mode = f.Mode;
            var s = TraxSettings.Shared;
            var input = new HudGateInput(s.ModEnabled, s.AthleticsEnabled, s.GetBool(Toggle), f.HideBattleUI, f.PhotoMode,
                HudFrame.IsFightMode(f.Mode), NeedsPlayer, f.Player != null && f.PlayerActive, ViewConditionMet(in f));
            var hide = HudGate.Decide(in input);
            if (!_decided || hide != _hide) Apply(hide, in f);
            if (_disabled) return;

            bool onScreen = _layerUp && !_suspended;
            Stats.AddTick(f.Dt, hide, onScreen);
            if (!_layerUp) return;
            if (onScreen)
            {
                OnVisibleFrame(f.Dt);
                OnLayerFrame(in f);
                if (_disabled || !_layerUp) return;
            }
            if (_firstPush || f.Now >= _nextRefresh || _nextRefresh - f.Now > 2.0)
            {
                _nextRefresh = f.Now + Math.Max(0.02, s.HudRefreshSeconds);
                Stats.AddRefresh();
                if (Refresh(in f, _firstPush)) _firstPush = false;
            }
        }

        private void Apply(HudHide hide, in HudFrame f)
        {
            var before = _hide;
            bool wasDecided = _decided;
            _hide = hide;
            _decided = true;
            if (hide == HudHide.None)
            {
                if (!_layerUp) CreateLayer(in f, wasDecided ? before : HudHide.None);
                return;
            }
            if (_layerUp)
            {
                DestroyLayer(hide, f.Now);
                return;
            }
            if (!wasDecided)
                TraxLog.Limited("hud", ViewName + ": not shown at " + S1(f.Now) + " s - " + Why(hide), "hud-layer");
            else if (TraxLog.VerboseWants("hud-hidden"))
                TraxLog.Verbose("hud", ViewName + ": still hidden at " + S1(f.Now) + " s, now because " + Why(hide), "hud-hidden");
        }

        private void CreateLayer(in HudFrame f, HudHide wasHiddenBy)
        {
            if (_host == null) throw new InvalidOperationException("no layer host - the view was not attached through AthleticsLogic");
            _dataSource = CreateDataSource(in f);
            if (!_host.Create(MovieName, MovieName, _dataSource, out string detail))
            {
                Stats.MovieFailed(detail, f.Now);
                Disable("movie", f.Now, "movie " + MovieName + " FAILED to load at " + S1(f.Now) + " s - " + detail);
                return;
            }
            _layerUp = true;
            Stats.LayerCreated();
            if (_suspended) _host.SetSuspended(true);
            _firstPush = true;
            _nextRefresh = f.Now;
            if (QuietConditionToggles && Stats.LayersCreated > 1 && wasHiddenBy == HudHide.ViewCondition)
            {
                if (TraxLog.VerboseWants("hud-layer-quiet"))
                    TraxLog.Verbose("hud", ViewName + ": layer created at " + S1(f.Now) + " s (" + ViewConditionWhen + ", build #" + Stats.LayersCreated + ")", "hud-layer-quiet");
                return;
            }
            TraxLog.Limited("hud", ViewName + ": layer created at " + S1(f.Now) + " s (mode " + HudFrame.ModeName(f.Mode)
                + (wasHiddenBy != HudHide.None ? ", was hidden: " + HudGate.Describe(wasHiddenBy, Toggle.Key, ViewConditionText) : string.Empty)
                + ") - movie " + MovieName + " loaded OK (" + detail + ")"
                + (QuietConditionToggles ? " - later builds and removals by \"" + ViewConditionText + "\" go to the verbose log only" : string.Empty), "hud-layer");
        }

        private void DestroyLayer(HudHide why, double now)
        {
            _layerUp = false;
            try
            {
                _host?.Destroy();
            }
            finally
            {
                FinalizeDataSource();
                Stats.LayerRemoved(why);
            }
            if (QuietConditionToggles && why == HudHide.ViewCondition)
            {
                if (TraxLog.VerboseWants("hud-layer-quiet")) TraxLog.Verbose("hud", ViewName + ": layer removed at " + S1(now) + " s - " + Why(why), "hud-layer-quiet");
                return;
            }
            TraxLog.Limited("hud", ViewName + ": layer removed at " + S1(now) + " s - " + Why(why), "hud-layer");
        }

        /// <summary>Mission over (the screen finalizes its views before the logic's summary runs).</summary>
        internal void Finish(double now)
        {
            if (_finished) return;
            _finished = true;
            if (_layerUp) DestroyLayer(HudHide.MissionEnd, now);
        }

        private void FinalizeDataSource()
        {
            var vm = _dataSource;
            _dataSource = null;
            try
            {
                OnLayerGone();
            }
            finally
            {
                vm?.OnFinalize();
            }
        }

        /// <summary>An exception: [error] with its stack (first per site per mission), then the view
        /// is disabled for the rest of the mission. Never throws.</summary>
        internal void Fail(string site, Exception e, double now)
        {
            try
            {
                AthleticsLogic.Failed("hud." + site, e);
                Disable(site, now, "an error in " + site + " at " + S1(now) + " s (" + e.GetType().Name + ": " + e.Message + " - stack in the [error] line)");
            }
            catch
            {
                _disabled = true; // the fallback must not fail
            }
        }

        private void Disable(string site, double now, string what)
        {
            if (_disabled) return;
            _disabled = true;
            Stats.Failed(site, now);
            if (_layerUp)
            {
                _layerUp = false;
                try
                {
                    _host?.Destroy();
                }
                catch (Exception e2)
                {
                    AthleticsLogic.Failed("hud.teardown", e2);
                }
                Stats.LayerRemoved(HudHide.Failed);
            }
            try
            {
                FinalizeDataSource();
            }
            catch (Exception e3)
            {
                AthleticsLogic.Failed("hud.teardown", e3);
            }
            TraxLog.Info("hud", ViewName + ": " + what + " - DISABLED for the rest of this battle; the battle goes on without it");
        }

        private string Why(HudHide hide) =>
            HudGate.Describe(hide, Toggle.Key, ViewConditionText) + (hide == HudHide.NotFightMode ? " - mode " + HudFrame.ModeName(_mode) : string.Empty);

        private double SafeNow()
        {
            try
            {
                return Mission?.CurrentTime ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        internal static string S1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
