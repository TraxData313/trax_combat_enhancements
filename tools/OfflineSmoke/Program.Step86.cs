using System;
using System.Globalization;
using TaleWorlds.Core;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 86 (Toni 2026-10-24, his HUD screenshot: "(Cntr)" beside "Sprint 2.6 s"): THE SPRINT BAR NAMES ITS KEY through the real
    /// SprintBarView over the real logic (step 33's stand-in sprint body, a stand-in layer) - "Sprint (Ctrl) 7.5 s" on mouse and keyboard, the
    /// game's controller glyph on a pad (the KeyID pushed empty, then set the next frame), "(L3)" when the glyph's sprite is missing, the
    /// switch back, a new pad type re-asked, SprintShowKeyHint / SprintControllerKey None / SprintKey live, the master switch first; the hint
    /// rebuilt only on a change and nothing allocated frame to frame; the first-time lines. What it cannot check: the game's widget drawing the
    /// glyph on screen and Input.IsGamepadActive flipping with a real pad (PLAYTEST D51).
    /// </summary>
    internal static partial class Program
    {
        private static void SprintBarNamesItsKey()
        {
            var keep = _logic;
            var keepProbe = SprintBarView.GlyphProbe;
            try
            {
                var logic = HudLogic();
                SprintDefaults();
                S.Set(SettingsSchema.SprintKey, KeyIndex("LeftControl"), SettingSources.File);
                S.Set(SettingsSchema.SprintControllerKey, PadIndex("ControllerLThumb"), SettingSources.File);
                S.Set(SettingsSchema.SprintShowKeyHint, true, SettingSources.File);
                var body = new FakeSprintBody();
                logic.SprintBody = body;
                var me = logic.Track(FakeAgent(860))!;
                me.AthleticsSkill = 150;
                logic.SmokePlayer = me.Agent;
                string? asked = null;
                bool glyphThere = true;
                SprintBarView.GlyphProbe = k => { asked = k; return glyphThere; };
                var view = new SprintBarView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                double t = 18600;
                logic.TickSprint(0.1f, t, Rules);

                HudFrame F(bool pad, int padType = 1)
                {
                    var fr = Frame(t, me.Agent);
                    fr.GamepadActive = pad;
                    fr.ControllerType = padType;
                    return fr;
                }

                // mouse and keyboard: "Sprint (Ctrl) 7.5 s"
                view.Tick(F(false));
                var vm = view.CurrentViewModel;
                Check(layer.Up && vm != null && vm.IsShown, "the sprint bar is not up");
                Check(vm!.WordText == "Sprint" && vm.KeyText == "(Ctrl)" && vm.KeyTextShown && !vm.KeyGlyphShown && vm.KeyGlyph == string.Empty && vm.SecondsText == "7.5 s",
                    "keyboard: \"" + vm.WordText + "\" \"" + vm.KeyText + "\" (" + vm.KeyTextShown + ") glyph \"" + vm.KeyGlyph + "\" \"" + vm.SecondsText + "\"");
                Check(vm.LabelText == "Sprint 7.5 s", "the label's words for the log: " + vm.LabelText);
                Check(asked == null, "the keyboard asked for a glyph");
                LogHas("[hud] sprint bar: key hint (Ctrl) at ");
                LogHas(" s - on mouse and keyboard, LeftControl or ControllerLThumb");

                // nothing changes: no rebuild, nothing allocated
                int builds = view.HintBuilds;
                for (int k = 0; k < 20; k++)
                {
                    t += 1 / 60.0;
                    view.Tick(F(false));
                }
                Check(view.HintBuilds == builds, "the hint was rebuilt with nothing changed: " + builds + " -> " + view.HintBuilds);
                const int frames = 600;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                for (int k = 0; k < frames; k++)
                {
                    t += 1 / 60.0;
                    view.Tick(F(false));
                }
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                Console.WriteLine("        step 86: the sprint bar with its key hint - " + allocated + " bytes allocated over " + frames + " unchanged frames, "
                                  + (view.HintBuilds - builds) + " rebuilds");
                Check(view.HintBuilds == builds, "the hint was rebuilt over unchanged frames");
                Check(allocated < 64 * 1024, "the sprint bar allocates frame to frame: " + allocated + " bytes over " + frames + " frames");

                // a pad: the glyph - the KeyID empty on the first frame (the game's widget picks its sprite when the id changes), set on the next
                t += 1 / 60.0;
                view.Tick(F(true));
                Check(view.CurrentHint.Kind == SprintHintKind.Glyph && !vm.KeyTextShown && vm.KeyGlyph == string.Empty && !vm.KeyGlyphShown,
                    "pad, first frame: the glyph not pushed empty first - \"" + vm.KeyGlyph + "\" " + vm.KeyGlyphShown + " text " + vm.KeyTextShown);
                Check(asked == "ControllerLThumb", "the glyph asked for: " + asked);
                t += 1 / 60.0;
                view.Tick(F(true));
                Check(vm.KeyGlyph == "ControllerLThumb" && vm.KeyGlyphShown && !vm.KeyTextShown && vm.KeyText == string.Empty,
                    "pad: not the game's glyph for ControllerLThumb - \"" + vm.KeyGlyph + "\" " + vm.KeyGlyphShown + " text \"" + vm.KeyText + "\"");
                LogHas("[hud] sprint bar: key hint the game's glyph for ControllerLThumb at ");
                LogHas(" s - on a pad (the game's Input.IsGamepadActive)");
                builds = view.HintBuilds;
                for (int k = 0; k < 10; k++)
                {
                    t += 1 / 60.0;
                    view.Tick(F(true));
                }
                Check(view.HintBuilds == builds && vm.KeyGlyph == "ControllerLThumb", "the pad's glyph rebuilt or lost with nothing changed");

                // a PlayStation pad now: the id pushed empty and set again (the widget's own _ps picture)
                t += 1 / 60.0;
                view.Tick(F(true, 4));
                Check(vm.KeyGlyph == string.Empty && view.HintBuilds == builds + 1, "a new pad type did not re-push the glyph: \"" + vm.KeyGlyph + "\"");
                t += 1 / 60.0;
                view.Tick(F(true, 4));
                Check(vm.KeyGlyph == "ControllerLThumb" && vm.KeyGlyphShown, "the PlayStation pad's glyph not set again");

                // back on the keyboard: "(Ctrl)" at once
                t += 1 / 60.0;
                view.Tick(F(false));
                Check(vm.KeyText == "(Ctrl)" && vm.KeyTextShown && !vm.KeyGlyphShown && vm.KeyGlyph == string.Empty, "back on the keyboard: not (Ctrl)");

                // the glyph's sprite missing: "(L3)" on the pad
                glyphThere = false;
                t += 1 / 60.0;
                view.Tick(F(true));
                Check(view.CurrentHint.Kind == SprintHintKind.Text && vm.KeyText == "(L3)" && vm.KeyTextShown && !vm.KeyGlyphShown, "no glyph sprite: not (L3) - \"" + vm.KeyText + "\"");
                t += 1 / 60.0;
                view.Tick(F(true));
                Check(vm.KeyText == "(L3)" && !vm.KeyGlyphShown, "the text fallback changed on the next frame");
                glyphThere = true;

                // the controller button live: RB (the glyph), None (nothing on the pad, the keyboard still names its key)
                S.Set(SettingsSchema.SprintControllerKey, PadIndex("ControllerRBumper"), SettingSources.Mcm);
                t += 1 / 60.0;
                view.Tick(F(true));
                t += 1 / 60.0;
                view.Tick(F(true));
                Check(vm.KeyGlyph == "ControllerRBumper" && vm.KeyGlyphShown, "SprintControllerKey RB live: \"" + vm.KeyGlyph + "\"");
                S.Set(SettingsSchema.SprintControllerKey, SprintKeys.NoController, SettingSources.Mcm);
                t += 1 / 60.0;
                view.Tick(F(true));
                t += 1 / 60.0;
                view.Tick(F(true));
                Check(view.CurrentHint.Kind == SprintHintKind.None && !vm.KeyTextShown && !vm.KeyGlyphShown && vm.KeyGlyph == string.Empty, "a pad with the controller button None shows a hint");
                Check(layer.Up && vm.IsShown && vm.SecondsText == "7.5 s", "the bar itself went with the hint");
                t += 1 / 60.0;
                view.Tick(F(false));
                Check(vm.KeyText == "(Ctrl)" && vm.KeyTextShown, "the keyboard lost its key with the controller button None");
                S.Set(SettingsSchema.SprintControllerKey, PadIndex("ControllerLThumb"), SettingSources.Mcm);

                // the keyboard key live: Shift
                S.Set(SettingsSchema.SprintKey, KeyIndex("LeftShift"), SettingSources.Mcm);
                t += 1 / 60.0;
                view.Tick(F(false));
                Check(vm.KeyText == "(Shift)", "SprintKey LeftShift live: " + vm.KeyText);
                S.Set(SettingsSchema.SprintKey, KeyIndex("LeftControl"), SettingSources.Mcm);

                // the switch off live: nothing on either device, the bar stays; on again: back
                S.Set(SettingsSchema.SprintShowKeyHint, false, SettingSources.Mcm);
                t += 1 / 60.0;
                view.Tick(F(false));
                Check(!vm.KeyTextShown && !vm.KeyGlyphShown && layer.Up && vm.IsShown, "SprintShowKeyHint off: a hint still shows (or the bar went)");
                t += 1 / 60.0;
                view.Tick(F(true));
                t += 1 / 60.0;
                view.Tick(F(true));
                Check(!vm.KeyTextShown && !vm.KeyGlyphShown && vm.KeyGlyph == string.Empty, "SprintShowKeyHint off: the pad shows a hint");
                S.Set(SettingsSchema.SprintShowKeyHint, true, SettingSources.Mcm);
                t += 1 / 60.0;
                view.Tick(F(false));
                Check(vm.KeyText == "(Ctrl)" && vm.KeyTextShown, "SprintShowKeyHint on again: no (Ctrl)");

                // the master switch first: the bar (and its hint) gone; back on = a new layer, the hint pushed again
                int created = layer.Created;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                t += 1 / 60.0;
                view.Tick(F(false));
                Check(!layer.Up, "ModEnabled off: the sprint bar stayed");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                t += 1 / 60.0;
                view.Tick(F(false));
                vm = view.CurrentViewModel;
                Check(layer.Up && layer.Created == created + 1 && vm != null && vm.KeyText == "(Ctrl)" && vm.KeyTextShown,
                    "ModEnabled on again: no new layer with (Ctrl) - " + (vm == null ? "no VM" : "\"" + vm.KeyText + "\""));
                Check(SprintKeyHints.Decide(false, true, true, 0, 1, _ => true).Kind == SprintHintKind.None, "the hint shows with the mod off");

                view.Finish(t);
                logic.SmokePlayer = null;
            }
            finally
            {
                SprintBarView.GlyphProbe = keepProbe;
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                HudDefaults();
                ConfigStore.Reload("after the sprint bar's key hint smoke");
            }
        }

        private static int KeyIndex(string name)
        {
            KeyNames.Keyboard.TryIndex(name, out int i);
            return i;
        }

        private static int PadIndex(string name)
        {
            KeyNames.Controller.TryIndex(name, out int i);
            return i;
        }
    }
}
