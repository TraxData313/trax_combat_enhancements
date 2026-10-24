namespace TraxCombat.Core
{
    /// <summary>Step 86: what the sprint bar shows after its word to name the key that starts a sprint.</summary>
    public enum SprintHintKind
    {
        /// <summary>Nothing: the hint is switched off, the master switch is off, or the device in use has no sprint key (a pad with
        /// SprintControllerKey None).</summary>
        None,

        /// <summary>A short text in brackets - "(Ctrl)" on the keyboard, "(L3)" on a pad when the game's glyph cannot be drawn.</summary>
        Text,

        /// <summary>The game's own controller glyph (the sprite vanilla's key hints use - General\InputKeys\…), drawn by the game's
        /// InputKeyVisualWidget from the key's InputKey name.</summary>
        Glyph,
    }

    /// <summary>Step 86: one hint, decided by <see cref="SprintKeyHints.Decide"/> - rebuilt only when the key, the device or the setting
    /// changes, never per frame.</summary>
    public readonly struct SprintKeyHint
    {
        public SprintKeyHint(SprintHintKind kind, string text, string glyphKey)
        {
            Kind = kind;
            Text = text;
            GlyphKey = glyphKey;
        }

        public SprintHintKind Kind { get; }

        /// <summary>"(Ctrl)" for <see cref="SprintHintKind.Text"/>, else "".</summary>
        public string Text { get; }

        /// <summary>The game's InputKey name ("ControllerLThumb") for <see cref="SprintHintKind.Glyph"/>, else "".</summary>
        public string GlyphKey { get; }

        public static readonly SprintKeyHint Nothing = new SprintKeyHint(SprintHintKind.None, string.Empty, string.Empty);

        /// <summary>For the log: "(Ctrl)", "the game's glyph for ControllerLThumb", "nothing".</summary>
        public string Describe() => Kind switch
        {
            SprintHintKind.Text => Text,
            SprintHintKind.Glyph => "the game's glyph for " + GlyphKey,
            _ => "nothing",
        };
    }

    /// <summary>
    /// Step 86 (Toni 2026-10-24, his HUD screenshot: "(Cntr)" beside "Sprint 2.6 s"): the sprint bar names the key that starts a sprint -
    /// the keyboard key while he plays on mouse and keyboard, the controller button while he plays on a pad (the game's
    /// <c>Input.IsGamepadActive</c> - a pad connected and the mouse not in use, read every frame by the game itself). Pure: the module
    /// hands in the device and whether the glyph's sprite exists.
    /// </summary>
    public static class SprintKeyHints
    {
        /// <summary>The sprite folder vanilla's key glyphs live in (InputKeyVisualWidget's IconsPath, HyperlinkTexts' &lt;img&gt;).</summary>
        public const string GlyphFolder = "General\\InputKeys\\";

        /// <summary>A keyboard / mouse key's short readable name: LeftControl → "Ctrl", LeftShift → "Shift", LeftAlt → "Alt", a letter
        /// itself, the mouse's side buttons "Mouse 4" / "Mouse 5". An unknown name is returned as it is; null / empty = "".</summary>
        public static string KeyboardShort(string? keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return string.Empty;
            switch (keyName)
            {
                case "LeftControl": case "RightControl": return "Ctrl";
                case "LeftAlt": case "RightAlt": return "Alt";
                case "LeftShift": case "RightShift": return "Shift";
                case "CapsLock": return "Caps";
                case "Tab": return "Tab";
                case "MiddleMouseButton": return "Mouse 3";
                case "X1MouseButton": return "Mouse 4";
                case "X2MouseButton": return "Mouse 5";
            }
            return keyName!;
        }

        /// <summary>A controller button's short name - step 85's short forms, the Xbox way the game's own key names read: ControllerLThumb →
        /// "L3", RThumb "R3", LBumper "LB", RBumper "RB", LTrigger "LT", RTrigger "RT", RUp "Y", RDown "A", RLeft "X", RRight "B", the d-pad
        /// "D-pad up" / "down" / "left" / "right". None / empty = ""; an unknown name is returned as it is.</summary>
        public static string ControllerShort(string? name)
        {
            if (string.IsNullOrEmpty(name) || name == "None") return string.Empty;
            switch (name)
            {
                case "ControllerLThumb": return "L3";
                case "ControllerRThumb": return "R3";
                case "ControllerLBumper": return "LB";
                case "ControllerRBumper": return "RB";
                case "ControllerLTrigger": return "LT";
                case "ControllerRTrigger": return "RT";
                case "ControllerRUp": return "Y";
                case "ControllerRDown": return "A";
                case "ControllerRLeft": return "X";
                case "ControllerRRight": return "B";
                case "ControllerLUp": return "D-pad up";
                case "ControllerLDown": return "D-pad down";
                case "ControllerLLeft": return "D-pad left";
                case "ControllerLRight": return "D-pad right";
            }
            return name!;
        }

        /// <summary>The glyph's sprite name for a controller key ("General\InputKeys\controllerlthumb") - what InputKeyVisualWidget asks the
        /// sprite data for (its "_ps" twin on a PlayStation pad is the widget's own business).</summary>
        public static string GlyphSprite(string inputKeyName) => GlyphFolder + (inputKeyName ?? string.Empty).ToLowerInvariant();

        /// <summary>
        /// The hint, in gate order: the master switch FIRST, then SprintShowKeyHint; on a pad (<paramref name="gamepadActive"/>) the
        /// controller button - its glyph when <paramref name="glyphAvailable"/> says the game has its sprite, else "(L3)"; nothing when
        /// SprintControllerKey is None or out of the list; on the keyboard "(Ctrl)" - nothing for an index outside the list.
        /// <paramref name="glyphAvailable"/> is asked only on a pad with a button chosen.
        /// </summary>
        public static SprintKeyHint Decide(bool modEnabled, bool showHint, bool gamepadActive, int keyIndex, int controllerIndex,
            System.Func<string, bool>? glyphAvailable)
        {
            if (!modEnabled || !showHint) return SprintKeyHint.Nothing;
            if (gamepadActive)
            {
                if (!SprintKeys.HasController(controllerIndex)) return SprintKeyHint.Nothing;
                string name = KeyNames.Controller.All[controllerIndex];
                bool glyph = false;
                if (glyphAvailable != null)
                {
                    try
                    {
                        glyph = glyphAvailable(name);
                    }
                    catch
                    {
                        glyph = false; // fail safe: the text
                    }
                }
                return glyph
                    ? new SprintKeyHint(SprintHintKind.Glyph, string.Empty, name)
                    : new SprintKeyHint(SprintHintKind.Text, Bracket(ControllerShort(name)), string.Empty);
            }
            var keys = KeyNames.Keyboard.All;
            if (keyIndex < 0 || keyIndex >= keys.Length) return SprintKeyHint.Nothing;
            string shortName = KeyboardShort(keys[keyIndex]);
            return shortName.Length == 0 ? SprintKeyHint.Nothing : new SprintKeyHint(SprintHintKind.Text, Bracket(shortName), string.Empty);
        }

        /// <summary>"(Ctrl)".</summary>
        public static string Bracket(string shortName) => "(" + shortName + ")";
    }
}
