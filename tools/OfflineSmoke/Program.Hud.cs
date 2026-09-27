using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for the HUD (step 6) - what can be proven without drawing a frame:
    ///   * the PREFAB (module\GUI\Prefabs\TraxPlayerAthleticsBar.xml) against the game's own
    ///     assemblies and files: well-formed, every element a widget type the game knows, every
    ///     attribute a real (settable) property with a value of its type, every brush and sprite a
    ///     vanilla one, every @binding a ViewModel property of EXACTLY the widget property's type
    ///     (Gauntlet converts only strings - a float bound to a Color throws inside the game);
    ///   * the REAL <see cref="PlayerAthleticsView"/> and <see cref="PlayerAthleticsVM"/> driven by
    ///     made-up frames with a stand-in layer (<see cref="FakeHudLayer"/>): when the layer is built
    ///     and removed and why, what the bar shows as a fighter swings, gets wounded and empties,
    ///     the refresh rate, suspension, pause, mission end, the log and the summary;
    ///   * the fail safe: an exception or a movie that does not load disables the view, removes
    ///     its layer, logs [error] - and never throws.
    /// What it cannot check: that the layer draws where the prefab says at every resolution and UI
    /// scale - PLAYTEST "Your Athletics bar" and the [hud] lines prove that in game.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The engine side of a view's layer, played by the smoke.</summary>
        private sealed class FakeHudLayer : IHudLayer
        {
            public int Created;
            public int Destroyed;
            public bool Up;
            public bool Suspended;
            public ViewModel? Vm;
            public string? Movie;
            public bool FailLoad;
            public bool ThrowOnCreate;
            public bool ThrowOnSuspend;

            public bool Create(string layerName, string movieName, ViewModel dataSource, out string detail)
            {
                if (ThrowOnCreate) throw new InvalidOperationException("smoke: layer creation failed");
                if (FailLoad)
                {
                    detail = "stand-in: no such prefab";
                    return false;
                }
                Created++;
                Up = true;
                Vm = dataSource;
                Movie = movieName;
                detail = "stand-in, 11 widgets";
                return true;
            }

            public void Destroy()
            {
                if (!Up) return;
                Destroyed++;
                Up = false;
                Vm = null;
            }

            public void SetSuspended(bool suspended)
            {
                if (ThrowOnSuspend) throw new InvalidOperationException("smoke: suspend failed");
                Suspended = suspended;
            }
        }

        private static HudFrame Frame(double now, Agent? player, MissionMode mode = MissionMode.Battle, bool hideUi = false, bool photo = false,
            bool paused = false, float dt = 0.05f, bool orderMenu = false) => new HudFrame
            {
                Dt = dt,
                Now = now,
                Paused = paused,
                HideBattleUI = hideUi,
                PhotoMode = photo,
                Mode = (int)mode,
                Player = player,
                PlayerActive = player != null,
                OrderMenuOpen = orderMenu,
            };

        /// <summary>A nested class: Program's own static fields initialise before Main registers the
        /// assembly resolver, so nothing typed from a game DLL may live there.</summary>
        private static class HudColors
        {
            public static readonly Color Green = Color.ConvertStringToColor(BarMath.GreenHex);
            public static readonly Color Alarm = Color.ConvertStringToColor(BarMath.ExhaustedHex);
        }

        private static void HudDefaults()
        {
            S.Set(SettingsSchema.ShowPlayerBar, true, SettingSources.File);
            S.Set(SettingsSchema.HudRefreshSeconds, 0.1, SettingSources.File);
            S.Set(SettingsSchema.BarYellowBelowPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.BarOrangeBelowPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.BarRedBelowPercent, 25, SettingSources.File);
            S.Set(SettingsSchema.PlayerBarWidth, 205, SettingSources.File);
            S.Set(SettingsSchema.PlayerBarHeight, 12, SettingSources.File);
            S.Set(SettingsSchema.PlayerBarOffsetRight, 62, SettingSources.File);
            S.Set(SettingsSchema.PlayerBarOffsetBottom, 54, SettingSources.File);
        }

        /// <summary>A fresh Athletics logic for the HUD checks (the Athletics steps' one has finished).</summary>
        private static AthleticsLogic HudLogic()
        {
            AthleticsDefaults();
            HudDefaults();
            var logic = new AthleticsLogic();
            SetStatic(typeof(AthleticsLogic), "_current", logic);
            _logic = logic; // Swing() drives the running logic
            typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
            return logic;
        }

        // ------------------------------------------------------------------ the prefab

        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TraxCombatEnhancements.sln"))) dir = dir.Parent;
            if (dir == null) throw new DirectoryNotFoundException("repo root (TraxCombatEnhancements.sln) not found above " + AppContext.BaseDirectory);
            return Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
        }

        private static IEnumerable<Type> LoadableTypes(string assemblyName)
        {
            Assembly asm;
            try
            {
                asm = Assembly.Load(assemblyName);
            }
            catch (Exception e)
            {
                Failures.Add("could not load " + assemblyName + ": " + e.Message);
                return Array.Empty<Type>();
            }
            try
            {
                return asm.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null)!;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HudPrefabIsValid() => PrefabIsValid(PlayerAthleticsView.Movie, typeof(PlayerAthleticsVM), 10, 60);

        /// <summary>
        /// One of our prefabs against the game: well-formed, every element a widget type, every
        /// attribute a real settable property with a valid value, only vanilla brushes and sprites,
        /// every @binding typed exactly like its ViewModel property. A <c>DataSource="{X}"</c> switches
        /// the ViewModel for the children: X an MBBindingList&lt;T&gt; → its &lt;ItemTemplate&gt; binds
        /// against T; X a ViewModel → the children bind against it. At the end every public property of
        /// every ViewModel met is drawn by something.
        /// </summary>
        private static void PrefabIsValid(string movie, Type rootVm, int minWidgets, int minAttributes)
        {
            string path = RepoFile("module", "GUI", "Prefabs", movie + ".xml");
            if (!File.Exists(path))
            {
                Failures.Add("prefab missing: " + path);
                return;
            }
            var doc = new XmlDocument();
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreComments = true }))
                doc.Load(reader); // throws on malformed XML - the Step reports it
            var root = doc.SelectSingleNode("Prefab")?.SelectSingleNode("Window")?.FirstChild as XmlElement;
            if (root == null)
            {
                Failures.Add("the prefab has no Prefab/Window/<widget> root (the game reads Window.FirstChild)");
                return;
            }

            // What the game knows: widget types (by class name, as WidgetFactory keys them), brushes, sprites.
            var widgetBase = LoadableTypes("TaleWorlds.GauntletUI").First(t => t.FullName == "TaleWorlds.GauntletUI.BaseTypes.Widget");
            var widgetTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var asm in new[] { "TaleWorlds.GauntletUI", "TaleWorlds.GauntletUI.ExtraWidgets", "TaleWorlds.MountAndBlade.GauntletUI.Widgets" })
                foreach (var t in LoadableTypes(asm))
                    if (!t.IsAbstract && widgetBase.IsAssignableFrom(t) && !widgetTypes.ContainsKey(t.Name))
                        widgetTypes[t.Name] = t;
            Check(widgetTypes.Count > 100, "suspiciously few widget types found: " + widgetTypes.Count);

            var brushes = new HashSet<string>(StringComparer.Ordinal);
            var brushName = new Regex("<Brush\\s+Name=\"([^\"]+)\"");
            foreach (var file in Directory.GetFiles(Path.Combine(_gameFolder, "Modules", "Native", "GUI", "Brushes"), "*.xml", SearchOption.AllDirectories))
                foreach (Match m in brushName.Matches(File.ReadAllText(file)))
                    brushes.Add(m.Groups[1].Value);
            var sprites = new HashSet<string>(StringComparer.Ordinal);
            var spriteDoc = new XmlDocument();
            spriteDoc.Load(Path.Combine(_gameFolder, "Modules", "Native", "GUI", "NativeSpriteData.xml"));
            foreach (XmlNode n in spriteDoc.SelectNodes("//SpritePart/Name | //NineRegionSprite/Name")!)
                sprites.Add(n.InnerText.Trim());
            Check(brushes.Count > 100 && sprites.Count > 1000, "vanilla brushes / sprites not read: " + brushes.Count + " / " + sprites.Count);

            var boundBy = new Dictionary<Type, HashSet<string>> { [rootVm] = new HashSet<string>(StringComparer.Ordinal) };
            int widgets = 0, attributes = 0, templates = 0;
            void Walk(XmlElement e, Type vm)
            {
                widgets++;
                if (!widgetTypes.TryGetValue(e.Name, out var type))
                {
                    Failures.Add("<" + e.Name + "> is not a widget type of the game");
                    return;
                }
                // DataSource first: a widget's OWN @bindings and its children resolve against it
                // (GauntletView.ViewModelPath appends it), and a LIST DataSource binds no property of
                // its widget at all (GauntletView.RefreshBinding) - only its <ItemTemplate> items.
                Type? ownVm = vm;
                Type? itemVm = null;
                var dataSource = e.GetAttributeNode("DataSource");
                if (dataSource != null)
                {
                    var m = Regex.Match(dataSource.Value, "^\\{([A-Za-z0-9_]+)\\}$");
                    var source = m.Success ? vm.GetProperty(m.Groups[1].Value, BindingFlags.Instance | BindingFlags.Public) : null;
                    if (source == null)
                    {
                        Failures.Add("<" + e.Name + "> DataSource=\"" + dataSource.Value + "\": " + vm.Name + " has no public property of that name");
                        return;
                    }
                    boundBy[vm].Add(source.Name);
                    var t = source.PropertyType;
                    if (t.IsGenericType && t.GetGenericTypeDefinition().FullName == "TaleWorlds.Library.MBBindingList`1")
                    {
                        itemVm = t.GetGenericArguments()[0];
                        ownVm = null;
                    }
                    else if (IsViewModel(t))
                    {
                        ownVm = t;
                    }
                    else
                    {
                        Failures.Add("<" + e.Name + "> DataSource {" + source.Name + "} is " + t.Name + " - neither an MBBindingList nor a ViewModel");
                        return;
                    }
                }
                if (ownVm != null && !boundBy.ContainsKey(ownVm)) boundBy[ownVm] = new HashSet<string>(StringComparer.Ordinal);
                if (itemVm != null && !boundBy.ContainsKey(itemVm)) boundBy[itemVm] = new HashSet<string>(StringComparer.Ordinal);
                foreach (XmlAttribute a in e.Attributes)
                {
                    attributes++;
                    if (a.Name == "DataSource") continue; // not a widget property: GauntletView reads it
                    if (ownVm == null && a.Value.StartsWith("@", StringComparison.Ordinal))
                    {
                        Failures.Add("<" + e.Name + (e.HasAttribute("Id") ? " Id=" + e.GetAttribute("Id") : string.Empty) + "> " + a.Name + "=\"" + a.Value
                                     + "\" sits on a widget whose DataSource is a list - the game ignores it (move it to a widget around the list)");
                        continue;
                    }
                    CheckAttribute(e, type, a.Name, a.Value, ownVm ?? vm, brushes, sprites, boundBy[ownVm ?? vm]);
                }
                foreach (XmlNode child in e.ChildNodes)
                {
                    if (!(child is XmlElement ce)) continue;
                    if (ce.Name == "Children")
                    {
                        if (ownVm == null)
                        {
                            Failures.Add("<" + e.Name + "> has fixed <Children> under a list DataSource - they would bind against the list");
                            continue;
                        }
                        foreach (XmlNode w in ce.ChildNodes)
                            if (w is XmlElement we) Walk(we, ownVm);
                    }
                    else if (ce.Name == "ItemTemplate")
                    {
                        templates++;
                        var items = ce.ChildNodes.OfType<XmlElement>().ToList();
                        if (itemVm == null) Failures.Add("<" + e.Name + "> has an <ItemTemplate> but no DataSource list");
                        else if (items.Count != 1) Failures.Add("<" + e.Name + ">'s <ItemTemplate> holds " + items.Count + " widgets - one is expected");
                        else Walk(items[0], itemVm);
                    }
                    else
                    {
                        Failures.Add("<" + e.Name + "> holds <" + ce.Name + "> - only <Children> or <ItemTemplate> is expected");
                    }
                }
            }
            Walk(root, rootVm);
            Check(widgets >= minWidgets && attributes >= minAttributes, movie + ": the walk saw too little: " + widgets + " widgets, " + attributes + " attributes");

            // Every public property of every ViewModel met is drawn by something (no dead data).
            int bindings = 0;
            foreach (var pair in boundBy)
            {
                bindings += pair.Value.Count;
                foreach (var p in pair.Key.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    Check(pair.Value.Contains(p.Name), pair.Key.Name + "." + p.Name + " is bound by nothing in " + movie);
            }
            Console.WriteLine("        prefab " + movie + ": " + widgets + " widgets, " + attributes + " attributes, " + bindings + " bindings over "
                              + boundBy.Count + " ViewModel(s)" + (templates > 0 ? ", " + templates + " item templates" : string.Empty) + " - all known to the game");
        }

        private static bool IsViewModel(Type t)
        {
            for (var b = t; b != null; b = b.BaseType)
                if (b.FullName == "TaleWorlds.Library.ViewModel") return true;
            return false;
        }

        private static void CheckAttribute(XmlElement e, Type widgetType, string name, string value, Type vm, HashSet<string> brushes,
            HashSet<string> sprites, HashSet<string> bound)
        {
            string where = "<" + e.Name + (e.HasAttribute("Id") ? " Id=" + e.GetAttribute("Id") : string.Empty) + "> " + name;
            // Resolve "Brush.FontColor" the way WidgetExtensions.GetObjectAndProperty does.
            Type owner = widgetType;
            PropertyInfo? prop = null;
            var parts = name.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                prop = PublicProperty(owner, parts[i]);
                if (prop == null)
                {
                    Failures.Add(where + ": " + owner.Name + " has no public property " + parts[i] + " (the game would silently ignore it)");
                    return;
                }
                owner = prop.PropertyType;
            }
            if (prop!.GetSetMethod() == null)
            {
                Failures.Add(where + ": " + prop.Name + " has no public setter");
                return;
            }
            Type target = prop.PropertyType;

            if (value.StartsWith("@", StringComparison.Ordinal))
            {
                string p = value.Substring(1);
                bound.Add(p);
                var source = vm.GetProperty(p, BindingFlags.Instance | BindingFlags.Public);
                if (source == null || source.GetGetMethod() == null)
                {
                    Failures.Add(where + ": binds @" + p + " but " + vm.Name + " has no readable property " + p);
                    return;
                }
                // Gauntlet (WidgetExtensions.ConvertObject) converts only strings; anything else must match exactly.
                bool stringConverted = source.PropertyType == typeof(string)
                                       && (target == typeof(string) || target == typeof(int) || target.FullName == "TaleWorlds.Library.Color"
                                           || target.FullName == "TaleWorlds.GauntletUI.Brush" || target.FullName == "TaleWorlds.TwoDimension.Sprite");
                Check(source.PropertyType == target || stringConverted,
                    where + ": @" + p + " is " + source.PropertyType.Name + " but the widget property is " + target.Name + " - the game's binding would throw");
                return;
            }

            bool ok;
            if (target == typeof(int)) ok = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            else if (target == typeof(float)) ok = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            else if (target == typeof(bool)) ok = value == "true" || value == "false";
            else if (target == typeof(string)) ok = true;
            else if (target.IsEnum) ok = Enum.GetNames(target).Contains(value);
            else if (target.FullName == "TaleWorlds.Library.Color") ok = Regex.IsMatch(value, "^#[0-9A-Fa-f]{8}$");
            else if (target.FullName == "TaleWorlds.GauntletUI.Brush") ok = brushes.Contains(value);
            else if (target.FullName == "TaleWorlds.TwoDimension.Sprite") ok = sprites.Contains(value);
            else if (IsWidgetType(target))
                ok = FindById(e, value) != null; // a path of child Ids, as Widget.FindChild(BindingPath) reads it
            else
            {
                Failures.Add(where + ": value type " + target.FullName + " is not checked by the smoke - teach it");
                return;
            }
            Check(ok, where + "=\"" + value + "\" is not a valid " + target.Name
                      + (target.FullName == "TaleWorlds.GauntletUI.Brush" ? " (no such vanilla brush)" : target.FullName == "TaleWorlds.TwoDimension.Sprite" ? " (no such vanilla sprite)" : string.Empty));
        }

        private static bool IsWidgetType(Type t)
        {
            for (var b = t; b != null; b = b.BaseType)
                if (b.FullName == "TaleWorlds.GauntletUI.BaseTypes.Widget") return true;
            return false;
        }

        /// <summary>The public instance property by name - the most derived one when a subclass hides a base's.</summary>
        private static PropertyInfo? PublicProperty(Type owner, string name)
        {
            try
            {
                return owner.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                return owner.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.Name == name)
                    .OrderByDescending(p => Depth(p.DeclaringType!)).First();
            }
        }

        private static int Depth(Type t)
        {
            int d = 0;
            for (var b = t; b != null; b = b.BaseType) d++;
            return d;
        }

        /// <summary>The element a "A\B" Id path names below <paramref name="e"/> (children by Id, one level per segment).</summary>
        private static XmlElement? FindById(XmlElement e, string path)
        {
            var current = e;
            foreach (var id in path.Split('\\'))
            {
                var children = current.SelectSingleNode("Children");
                current = children?.ChildNodes.OfType<XmlElement>().FirstOrDefault(c => c.GetAttribute("Id") == id)!;
                if (current == null) return null;
            }
            return current;
        }

        // ------------------------------------------------------------------ the view, driven

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HudPlayerBarThroughTheView()
        {
            var logic = HudLogic();
            var player = FakeAgent(20);
            var me = logic.Track(player)!;
            me.AthleticsSkill = 120; // pool 120, the peak line at 90; 10 a blow (not a hero here)
            var view = new PlayerAthleticsView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            double t = 0;

            // deployment: not a fight - nothing on screen, and the log says why
            view.Tick(Frame(t, player, MissionMode.Deployment));
            Check(layer.Created == 0 && !view.IsLayerUp && view.LastDecision == HudHide.NotFightMode, "a layer was built during deployment");
            LogHas("[hud] player bar: not shown at 0.0 s - not a fight (mission mode) - mode Deployment");

            // the battle begins, the player on the field: built, sized, first values pushed
            t = 1;
            view.Tick(Frame(t, player));
            Check(layer.Created == 1 && layer.Up && layer.Movie == PlayerAthleticsView.Movie && view.IsLayerUp, "no layer in battle with the player on the field");
            LogHas("[hud] player bar: layer created at 1.0 s (mode Battle, was hidden: not a fight (mission mode)) - movie TraxPlayerAthleticsBar loaded OK (stand-in, 11 widgets)");
            var vm = view.CurrentViewModel!;
            Check(ReferenceEquals(vm, layer.Vm), "the layer got another ViewModel than the view keeps");
            Check(vm.IsShown && vm.NumberText == "120 / 120" && vm.Fill == 1f && vm.Usable == 1f && Near(vm.PeakLine, 0.75f) && vm.FillColor == HudColors.Green
                  && vm.LabelText == "Athletics", "a fresh bar: shown " + vm.IsShown + ", " + vm.NumberText + ", fill " + vm.Fill + ", usable " + vm.Usable + ", peak " + vm.PeakLine);
            Check(vm.BarWidth == 205f && vm.BarHeight == 12f && vm.OffsetRight == 62f && vm.OffsetBottom == 54f, "the layout settings did not reach the ViewModel");
            LogHas("[hud] player bar: first values pushed at 1.0 s - 120 / 120, fill 1.00, usable 1.00, colour green (peak zone - full strength), f 1.00, peak marker at 75% of the bar; "
                   + "bar 205 x 12 px, 62 px from the right edge and 54 px from the bottom (UI pixels - the game's UI scale applies); colours: yellow at or below 75%, orange 50%, red 25% of the peak line");
            LogHas("[hud] player bar: GREEN for the first time this battle at 1.0 s - f 1.00, 120 / 120 (fill 1.00)");

            // swing until empty: every colour in order, the number counting down, empty = red + "Exhausted"
            var firstSeen = new List<Color> { vm.FillColor };
            int refreshes = view.Stats.Refreshes;
            for (int i = 0; i < 12; i++)
            {
                Swing(me, ref t);
                view.Tick(Frame(t, player));
                if (firstSeen.Last() != vm.FillColor) firstSeen.Add(vm.FillColor);
            }
            var expected = new[] { BarMath.GreenHex, BarMath.BlueHex, BarMath.YellowHex, BarMath.OrangeHex, BarMath.RedHex }.Select(Color.ConvertStringToColor).ToList();
            Check(firstSeen.SequenceEqual(expected), "the colours did not run green → blue → yellow → orange → red: " + string.Join(", ", firstSeen.Select(c => c.ToString())));
            Check(view.Stats.Refreshes == refreshes + 12, "not one refresh per swing: " + (view.Stats.Refreshes - refreshes));
            Check(vm.NumberText == "0 / 120" && vm.Fill == 0f && vm.LabelText == "Exhausted" && vm.NumberColor == HudColors.Alarm && vm.LabelColor == HudColors.Alarm && vm.FrameColor == HudColors.Alarm,
                "the empty bar: " + vm.NumberText + ", label " + vm.LabelText + ", number colour " + vm.NumberColor);
            foreach (var band in new[] { "BLUE", "YELLOW", "ORANGE", "RED" }) LogHas("[hud] player bar: " + band + " for the first time this battle at ");
            LogHas(" s - f 0.89, 80 / 120 (fill 0.67)");       // blow 4: 80 of 120, f 80/90
            LogHas("[hud] player bar: EXHAUSTED shown at ");
            LogHas(" s - 0 / 120: the label reads \"Exhausted\", label, number and frame red");

            // wounded at 62%: the last 38% dark, f can never pass 0.83 again
            SetHealth(player, 62f, 100f);
            logic.CheckHealth(me, Rules, t);
            t += 0.2;
            view.Tick(Frame(t, player));
            Check(Near(vm.Usable, 0.62f), "the wounded part is not pushed: usable " + vm.Usable);
            LogHas(" s - the last 38% of the bar shown dark: usable 74 of 120 (health caps the bar); f can reach at most 0.83 now");

            // the refresh rate (live): nothing new within 0.1 s; 0.5 s once HudRefreshSeconds says so
            refreshes = view.Stats.Refreshes;
            view.Tick(Frame(t + 0.05, player));
            Check(view.Stats.Refreshes == refreshes, "refreshed before HudRefreshSeconds was up");
            S.Set(SettingsSchema.HudRefreshSeconds, 0.5, SettingSources.Mcm);
            view.Tick(Frame(t + 0.11, player));      // the 0.1 s slot: refresh, next one 0.5 s later
            view.Tick(Frame(t + 0.4, player));
            Check(view.Stats.Refreshes == refreshes + 1, "HudRefreshSeconds 0.5 did not slow the refresh: " + (view.Stats.Refreshes - refreshes));
            view.Tick(Frame(t + 0.62, player));
            Check(view.Stats.Refreshes == refreshes + 2, "no refresh after 0.5 s");
            S.Set(SettingsSchema.HudRefreshSeconds, 0.1, SettingSources.Mcm);
            t += 2; // past the 0.5 s slot booked at the last refresh

            // a live layout change reaches the next refresh
            S.Set(SettingsSchema.PlayerBarWidth, 300, SettingSources.Mcm);
            view.Tick(Frame(t, player));
            Check(vm.BarWidth == 300f, "PlayerBarWidth 300 did not reach the bar: " + vm.BarWidth);
            S.Set(SettingsSchema.PlayerBarWidth, 205, SettingSources.Mcm);

            // every reason removes the layer (logged), and the bar comes back when it passes
            void GoneAndBack(string reason, Func<double, HudFrame> hidden, Action? hide = null, Action? show = null)
            {
                int created = layer.Created;
                t += 1;
                hide?.Invoke();
                view.Tick(hidden(t));
                Check(!layer.Up && !view.IsLayerUp && view.CurrentViewModel == null, reason + ": the layer stayed");
                LogHas("[hud] player bar: layer removed at " + TraxHudView.S1(t) + " s - " + reason);
                view.Tick(hidden(t + 0.5));
                Check(layer.Created == created, reason + ": rebuilt while still hidden");
                t += 1;
                show?.Invoke();
                view.Tick(Frame(t, player));
                Check(layer.Up && layer.Created == created + 1, reason + ": the bar did not come back");
                vm = view.CurrentViewModel!;
                Check(vm.IsShown && vm.NumberText != string.Empty, reason + ": the rebuilt bar shows nothing");
            }
            GoneAndBack("the game's Hide battle UI is on", now => Frame(now, player, hideUi: true));
            GoneAndBack("ShowPlayerBar off", now => Frame(now, player),
                () => S.Set(SettingsSchema.ShowPlayerBar, false, SettingSources.Mcm), () => S.Set(SettingsSchema.ShowPlayerBar, true, SettingSources.Mcm));
            GoneAndBack("AthleticsEnabled off", now => Frame(now, player),
                () => S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm), () => S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm));
            GoneAndBack("photo mode", now => Frame(now, player, photo: true));
            GoneAndBack("no player agent on the field", now => Frame(now, null));
            GoneAndBack("not a fight (mission mode) - mode Conversation", now => Frame(now, player, MissionMode.Conversation));
            GoneAndBack("ModEnabled off (the master switch)", now => Frame(now, player),
                () => S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm), () => S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm));
            Check(view.Stats.LayersRemoved == 7 && view.Stats.LayersCreated == 8, "layers built / removed: " + view.Stats.LayersCreated + " / " + view.Stats.LayersRemoved);

            // stealth and tournament and duel are fights too
            foreach (var mode in new[] { MissionMode.Stealth, MissionMode.Tournament, MissionMode.Duel })
            {
                t += 0.2;
                view.Tick(Frame(t, player, mode));
                Check(view.IsLayerUp, mode + " is not treated as a fight");
            }

            // suspended by another view: the layer stays, hidden, and on-screen time stops
            view.SuspendView();
            Check(layer.Suspended && view.IsLayerUp, "SuspendView did not suspend the layer");
            double visible = view.Stats.VisibleSeconds;
            t += 0.5;
            view.Tick(Frame(t, player, dt: 0.5f));
            Check(Near(view.Stats.VisibleSeconds, visible), "on-screen time grew while suspended");
            view.ResumeView();
            Check(!layer.Suspended, "ResumeView did not resume the layer");

            // paused: nothing changes, even with Hide battle UI on (vanilla views skip paused frames too)
            view.Tick(Frame(t + 0.1, player, hideUi: true, paused: true));
            Check(view.IsLayerUp, "a paused frame removed the layer");

            // the mission ends: removed, and the view stays quiet afterwards
            t += 1;
            view.Finish(t);
            Check(!layer.Up && layer.Destroyed == 8 && view.Stats.RemovedBy(HudHide.MissionEnd) == 1, "mission end did not remove the layer");
            LogHas("[hud] player bar: layer removed at " + TraxHudView.S1(t) + " s - mission end");
            view.Tick(Frame(t + 1, player));
            Check(layer.Created == 8, "a finished view built a layer");

            // the summary, through the logic as at a real mission end
            ((List<TraxHudView>)typeof(AthleticsLogic).GetField("_hudViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(logic)!).Add(view);
            typeof(AthleticsLogic).GetMethod("WriteHudSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
            var lines = view.Stats.SummaryLines();
            Check(lines.Count == 2, "expected two summary lines, got " + lines.Count);
            LogHas("[summary] hud: player bar (movie TraxPlayerAthleticsBar) - on screen ");
            LogHas("; layer built 8x, removed 8x (ModEnabled off 1, AthleticsEnabled off 1, ShowPlayerBar off 1, Hide battle UI 1, photo mode 1, not a fight 1, no player agent 1, mission end 1); hidden: ");
            LogHas("; errors 0");
            LogHas("[summary] hud: player bar colours on screen - green ");
            LogHas(" colour changes; exhausted shown 1x (");
            LogHas("; wounded part shown ");
            LogHas("(lowest usable 62% - the last 38% of the bar dark)");
            Check(view.Stats.BandSeen(BarBand.Red) && view.Stats.BandChanges >= 4 && view.Stats.ExhaustedEntries == 1, "the stats missed colours or the empty bar");

            // no screen at all: the summary says so
            var bare = new AthleticsLogic();
            typeof(AthleticsLogic).GetMethod("WriteHudSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(bare, null);
            LogHas("[summary] hud: no views attached (the first tick never came)");
        }

        /// <summary>Called from the master-switch step: the bar goes the moment ModEnabled goes off,
        /// comes back when it is on again (the master switch first - CLAUDE.md).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HudFollowsTheMasterSwitch(Agent player)
        {
            HudDefaults();
            var view = new PlayerAthleticsView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.Tick(Frame(900, player));
            Check(layer.Up, "master switch: precondition - the bar is not up");
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(901, player));
            Check(!layer.Up && view.LastDecision == HudHide.ModOff, "mod off: the Athletics bar stayed on screen");
            LogHas("[hud] player bar: layer removed at 901.0 s - ModEnabled off (the master switch)");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(902, player));
            Check(layer.Up && layer.Created == 2, "mod back on: the Athletics bar did not come back");
            view.Finish(903);
            StripFollowsTheMasterSwitch(player);
        }

        // ------------------------------------------------------------------ the fail safe

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HudFailSafe()
        {
            var logic = HudLogic();
            var player = FakeAgent(21);
            logic.Track(player);

            // 1. the layer cannot be built (an exception): [error], disabled, never again, no throw
            var broken = new PlayerAthleticsView();
            var throwing = new FakeHudLayer { ThrowOnCreate = true };
            broken.UseLayer(throwing);
            int errors = TraxLog.ErrorCount;
            broken.Tick(Frame(10, player));
            broken.Tick(Frame(11, player));
            Check(broken.IsDisabled && !broken.IsLayerUp && TraxLog.ErrorCount == errors + 1, "a throwing layer did not disable the view (or logged twice)");
            LogHas("[error] hud.tick: System.InvalidOperationException: smoke: layer creation failed");
            LogHas("[hud] player bar: an error in tick at 10.0 s (InvalidOperationException: smoke: layer creation failed - stack in the [error] line) - DISABLED for the rest of this battle; the battle goes on without it");
            Check(broken.Stats.Errors == 1 && broken.Stats.FailedSite == "tick", "the error is not in the view's stats");
            Check(broken.Stats.SummaryLines()[0].EndsWith("errors 1 - DISABLED at 10.0 s after an error in tick (the battle went on without it)", StringComparison.Ordinal),
                "the summary does not report the disabled view: " + broken.Stats.SummaryLines()[0]);

            // 2. the movie does not load: disabled, reported, nothing left on screen
            var noMovie = new PlayerAthleticsView();
            var missing = new FakeHudLayer { FailLoad = true };
            noMovie.UseLayer(missing);
            noMovie.Tick(Frame(12, player));
            Check(noMovie.IsDisabled && !missing.Up && noMovie.CurrentViewModel == null, "a movie that did not load left the view running");
            LogHas("[hud] player bar: movie TraxPlayerAthleticsBar FAILED to load at 12.0 s - stand-in: no such prefab - DISABLED for the rest of this battle");
            Check(noMovie.Stats.SummaryLines()[0].Contains("movie FAILED to load at 12.0 s: stand-in: no such prefab - never shown"), "the summary does not report the movie failure");

            // 3. a failure while the bar is up (here: the suspend call): the layer is removed at once
            var upView = new PlayerAthleticsView();
            var fragile = new FakeHudLayer();
            upView.UseLayer(fragile);
            upView.Tick(Frame(13, player));
            Check(fragile.Up, "precondition: the bar is not up");
            fragile.ThrowOnSuspend = true;
            upView.SuspendView(); // must not throw into the game
            Check(upView.IsDisabled && !fragile.Up && upView.Stats.RemovedBy(HudHide.Failed) == 1, "a failure with the bar up did not remove it");
            LogHas("[error] hud.suspend: System.InvalidOperationException: smoke: suspend failed");

            // 4. a view never attached through the logic (no layer host) fails safe too
            var orphan = new PlayerAthleticsView();
            orphan.Tick(Frame(14, player));
            Check(orphan.IsDisabled, "a view without a layer host did not disable itself");

            // Leave things as a player would find them: no mission running, the file's values back.
            SetStatic(typeof(AthleticsLogic), "_current", null);
            ConfigStore.Reload("after the HUD smoke");
        }
    }
}
