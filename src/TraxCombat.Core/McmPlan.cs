using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>What the MCM bridge does before an attempt to register the settings page.</summary>
    public enum McmVerdict
    {
        /// <summary>Try to build the page now.</summary>
        Try = 0,

        /// <summary>No MCMv5 assembly in the game - stand down (one line).</summary>
        NotLoaded = 1,

        /// <summary>An MCMv5 assembly is loaded, but MCM's own module is not enabled - another mod
        /// carries MCM's DLL, MCM itself never runs, so its page could never be ready. Stand down.</summary>
        ModuleNotEnabled = 2,
    }

    /// <summary>
    /// When the MCM bridge tries MCM's settings page, and when it stops (step 12 - Anton's playtest,
    /// log 21:08: MCM's module was NOT enabled, another mod carried an MCMv5 DLL, and the bridge
    /// retried "not ready" every second all session).
    ///
    /// MCM builds its services in its OWN main-menu hook (MCMv5 5.12.3,
    /// MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot) - the same frame as ours. So the page is
    /// ready at the main menu, or one retry later (our module loaded before MCM's), or never (MCM not
    /// running). Hence: no attempt before the main menu (the bridge's first attempt is the main-menu
    /// hook, or the first game start), none at all when the module list says MCM's module is off, and
    /// at most <see cref="MaxRetries"/> retries after a "not ready".
    /// </summary>
    public static class McmPlan
    {
        /// <summary>MCM's module id (its SubModule.xml, Workshop 2859238197).</summary>
        public const string ModuleId = "Bannerlord.MBOptionScreen";

        /// <summary>Seconds between retries.</summary>
        public const double RetrySeconds = 1.0;

        /// <summary>
        /// Retries after the first "not ready" before the bridge gives up - a documented CONSTANT, not
        /// a parameter: it is not a tuning knob (a player gains nothing by changing it). MCM is ready
        /// one retry after the main menu at worst (see the class doc); 30 retries = half a minute of
        /// margin for a slow machine, then one clear line and silence.
        /// </summary>
        public const int MaxRetries = 30;

        /// <summary>MCM's module in the enabled module list: true / false; null when the list is
        /// unknown (not read, or empty - the offline smoke) - then the bridge simply tries.</summary>
        public static bool? ModuleEnabled(IReadOnlyCollection<string>? modules)
        {
            if (modules == null || modules.Count == 0) return null;
            foreach (var m in modules)
                if (m != null && string.Equals(m.Trim(), ModuleId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Before an attempt: the assembly first (as before step 12), then the module.</summary>
        public static McmVerdict Decide(bool assemblyLoaded, bool? moduleEnabled)
        {
            if (!assemblyLoaded) return McmVerdict.NotLoaded;
            if (moduleEnabled == false) return McmVerdict.ModuleNotEnabled;
            return McmVerdict.Try;
        }

        /// <summary>After an attempt that found MCM not ready (<paramref name="notReady"/> of them so
        /// far, this one included): true = give up now. The first attempt + <see cref="MaxRetries"/>
        /// retries, then stop.</summary>
        public static bool GiveUp(int notReady) => notReady > MaxRetries;

        /// <summary>The one line when MCM's module is off but its DLL is loaded.</summary>
        public static string ModuleNotEnabledLine(string mcmVersion) =>
            "MCM's module is not enabled - no settings page; config.json only. (An MCM " + mcmVersion + " DLL is loaded - another mod "
            + "carries it - but MCM itself, module " + ModuleId + ", is not in the enabled module list, so its page could never be ready. "
            + "Enable Mod Configuration Menu in the launcher for the in-game page.)";

        /// <summary>The first "not ready" line.</summary>
        public static string NotReadyLine(string mcmVersion, string when) =>
            "MCM " + mcmVersion + " is loaded but not ready yet at " + when + " - retrying every "
            + RetrySeconds.ToString("0", CultureInfo.InvariantCulture) + " s, at most " + MaxRetries + " times.";

        /// <summary>The one line when the retries run out.</summary>
        public static string GiveUpLine(string mcmVersion, int attempts) =>
            "MCM " + mcmVersion + " never became ready - gave up after " + attempts + " attempts (the first + " + MaxRetries
            + " retries, one a second) - no settings page; config.json only.";
    }
}
