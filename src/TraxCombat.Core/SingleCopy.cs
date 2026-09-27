using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 11: ONE copy of the mod runs per game. The dev install (<c>Modules\TraxCombatEnhancements.Dev</c>,
    /// tools\deploy.ps1) and the release (the Steam Workshop copy, Id <c>TraxCombatEnhancements</c>) can
    /// both be enabled in the launcher. Both would register their damage and stat decorators and attach
    /// their mission logic - every hit would roll twice, every blow would cost Athletics twice.
    ///
    /// How: each copy's SubModule claims one process-wide slot (an AppDomain data slot, so it is shared
    /// even by two different assemblies - dev 0.1.1 beside release 0.1.0; with the SAME version the game's
    /// <c>Assembly.LoadFrom</c> hands both modules ONE assembly and the loader makes two SubModule
    /// instances of it, so the claim is by instance, never by type or file). The game calls
    /// OnSubModuleLoad in module order on its main thread, so the copy that loads first runs; a later one
    /// is refused and stays inert: no log line (the running copy holds the log open), no config, no MCM
    /// page, no models, no mission logic. The running copy counts the refusals and reports them - one
    /// [compat] line and one on-screen message (SubModule). No lock: the loader is single-threaded, and a
    /// lock object would not be shared between two assemblies anyway.
    /// </summary>
    public static class SingleCopy
    {
        /// <summary>The AppDomain data slot: <c>object[] { the running copy's token, refusals (int) }</c>.</summary>
        public const string SlotName = "TraxCombatEnhancements.RunningCopy";

        /// <summary>The release module id; a copy is this id or <c>TraxCombatEnhancements.&lt;anything&gt;</c>.</summary>
        public const string ReleaseId = "TraxCombatEnhancements";

        private static readonly Regex ManifestIdPattern = new Regex("<Id\\s+value\\s*=\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant);

        /// <summary>
        /// True = this copy runs (the first to ask, or the one already holding the slot asking again);
        /// false = another copy runs and the caller must stay inert (the refusal is counted).
        /// </summary>
        public static bool TryClaim(object token, string slotName = SlotName)
        {
            if (token == null) throw new ArgumentNullException(nameof(token));
            var domain = AppDomain.CurrentDomain;
            if (!(domain.GetData(slotName) is object[] held) || held.Length != 2 || held[0] == null)
            {
                domain.SetData(slotName, new object[] { token, 0 });
                return true;
            }
            if (ReferenceEquals(held[0], token)) return true;
            int refused = held[1] is int n ? n : 0;
            domain.SetData(slotName, new object[] { held[0], refused + 1 });
            return false;
        }

        /// <summary>How many copies found the slot taken and stood down.</summary>
        public static int Refused(string slotName = SlotName) =>
            AppDomain.CurrentDomain.GetData(slotName) is object[] held && held.Length == 2 && held[1] is int n ? n : 0;

        /// <summary>Tests only: an empty slot.</summary>
        internal static void ResetForTests(string slotName) => AppDomain.CurrentDomain.SetData(slotName, null);

        /// <summary>The enabled module ids that are copies of this mod, in load order.</summary>
        public static IReadOnlyList<string> CopiesIn(IEnumerable<string>? moduleIds) =>
            (moduleIds ?? Enumerable.Empty<string>())
            .Where(IsCopy)
            .ToList();

        public static bool IsCopy(string? moduleId) =>
            moduleId != null
            && (string.Equals(moduleId, ReleaseId, StringComparison.OrdinalIgnoreCase)
                || moduleId.StartsWith(ReleaseId + ".", StringComparison.OrdinalIgnoreCase));

        /// <summary>The <c>&lt;Id value="…"/&gt;</c> of a SubModule.xml text, or null.</summary>
        public static string? IdFromManifest(string? manifestText)
        {
            if (string.IsNullOrEmpty(manifestText)) return null;
            var m = ManifestIdPattern.Match(manifestText);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>The running copy's [compat] line at load, or null with one copy enabled.</summary>
        public static string? LoadLine(IReadOnlyList<string> copies, string self)
        {
            if (copies.Count < 2) return null;
            return copies.Count + " copies of this mod are enabled (" + string.Join(", ", copies) + ") - this one ("
                + self + ") loaded first and runs; any other must stand down (its damage and stat decorators and "
                + "mission logic would stack on ours: two damage rolls per hit, Athletics charged twice) - "
                + "confirmed at the main menu. Disable one in the launcher.";
        }

        /// <summary>The running copy's [compat] report at the main menu (or game start), or null when
        /// there is nothing to report.</summary>
        public static string? ReportLine(IReadOnlyList<string> copies, string self, int refused)
        {
            if (refused > 0)
                return refused + " other " + (refused == 1 ? "copy" : "copies") + " of this mod found this one ("
                    + self + ") running and stood down: no models, no mission logic, no MCM page, no log lines of "
                    + "its own" + (copies.Count > 0 ? ". Enabled: " + string.Join(", ", copies) : "")
                    + ". Disable one in the launcher.";
            if (copies.Count > 1)
                return "WARNING: " + copies.Count + " copies of this mod are enabled (" + string.Join(", ", copies)
                    + ") but none stood down - an older build without this guard, or one that failed to load? If "
                    + "both run, every hit rolls twice and Athletics is charged twice. Disable one in the launcher.";
            return null;
        }

        /// <summary>The ONE on-screen message, or null when there is nothing to say.</summary>
        public static string? Message(IReadOnlyList<string> copies, string self, int refused)
        {
            if (refused > 0)
                return "Trax Combat Enhancements: " + Math.Max(refused + 1, copies.Count) + " copies are enabled"
                    + (copies.Count > 1 ? " (" + string.Join(", ", copies) + ")" : "")
                    + " - only " + self + " runs. Disable one in the launcher.";
            if (copies.Count > 1)
                return "Trax Combat Enhancements: " + copies.Count + " copies are enabled (" + string.Join(", ", copies)
                    + ") - disable one in the launcher.";
            return null;
        }
    }
}
