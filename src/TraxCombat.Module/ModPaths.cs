using System;
using System.IO;
using TaleWorlds.Engine;
using Path = System.IO.Path;
using TaleWorlds.Library;
using TraxCombat.Core;

namespace TraxCombat
{
    /// <summary>
    /// Where the mod keeps its two files: <c>Documents\Mount and Blade II Bannerlord\Configs\
    /// TraxCombatEnhancements\</c> holding config.json and trax_combat.log. Resolved through the
    /// game's own path API (<see cref="EngineFilePaths.ConfigsPath"/>, RESEARCH §H), which
    /// honours the game's application name; the hard-coded Documents path is only a fallback.
    /// </summary>
    internal static class ModPaths
    {
        public const string FolderName = "TraxCombatEnhancements";

        private static string? _configDir;

        /// <summary>True when the game's path API failed and the fallback was used (logged at load).</summary>
        public static bool UsedFallback { get; private set; }

        public static string ConfigDir => _configDir ??= Resolve();

        public static string ConfigFilePath => Path.Combine(ConfigDir, ConfigFile.FileName);

        public static string LogFilePath => Path.Combine(ConfigDir, ConfigFile.LogFileName);

        private static string Resolve()
        {
            try
            {
                // The "file" here is our folder: GetFileFullPath = <Documents>\<app name>\Configs\<name>.
                var full = FileHelper.GetFileFullPath(new PlatformFilePath(EngineFilePaths.ConfigsPath, FolderName));
                if (!string.IsNullOrEmpty(full)) return full;
            }
            catch
            {
                // fall through - the engine's file helper is not up (never expected in game)
            }
            UsedFallback = true;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "Configs", FolderName);
        }
    }
}
