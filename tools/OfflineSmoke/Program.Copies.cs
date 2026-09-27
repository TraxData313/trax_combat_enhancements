using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 11: the dev copy and the release copy enabled together. The game's loader makes one
    /// SubModule instance per module (the same assembly when the versions match) and calls their hooks
    /// in module order. Here: two REAL SubModule instances; the first holds the claim (as its
    /// OnSubModuleLoad would make it - its LogLoad is skipped, the module list is a native call), the
    /// second goes through the real OnSubModuleLoad and must stand down: not one log line, no model,
    /// no mission logic, no message - while the running one registers ONE decorator of each kind and
    /// reports the stood-down copy once ([compat] line + one message). What it cannot check: the real
    /// launcher and module loader (PLAYTEST: enable both copies once).
    /// </summary>
    internal static partial class Program
    {
        private static void TwoCopiesOneRuns()
        {
            // The manifest -> module id lookup, on a made-up module folder.
            string moduleDir = Path.Combine(_dir, "Modules", "TraxCombatEnhancements.Dev");
            Directory.CreateDirectory(Path.Combine(moduleDir, "bin", "Win64_Shipping_Client"));
            File.WriteAllText(Path.Combine(moduleDir, "SubModule.xml"),
                "<Module>\n  <Id value=\"TraxCombatEnhancements.Dev\" />\n  <Name value=\"Trax Combat Enhancements (dev)\" />\n</Module>");
            string dllPath = Path.Combine(moduleDir, "bin", "Win64_Shipping_Client", "TraxCombatEnhancements.dll");
            Check(SubModule.SelfModuleId(dllPath) == "TraxCombatEnhancements.Dev", "SelfModuleId read " + (SubModule.SelfModuleId(dllPath) ?? "null"));
            Check(SubModule.SelfModuleId(typeof(SubModule).Assembly.Location) == null, "the build folder has no manifest - expected null");

            var running = new SubModule();
            var second = new SubModule();
            Check(SingleCopy.TryClaim(running), "the first copy's claim was refused");
            SetStatic(typeof(SubModule), "_ourCopies", (IReadOnlyList<string>)new[] { "TraxCombatEnhancements.Dev", "TraxCombatEnhancements" });
            SetStatic(typeof(SubModule), "_selfId", "TraxCombatEnhancements.Dev");

            var messages = new List<string>();
            Action<InformationMessage> listen = m => messages.Add(m.Information);
            InformationManager.DisplayMessageInternal += listen;
            try
            {
                var starter = new BasicGameStarter();
                starter.AddModel(new CustomAgentApplyDamageModel());
                starter.AddModel(new FreshStatsModel());
                IGameStarter asStarter = starter;
                int modelsBefore = asStarter.Models.Count();
                string logBefore = LogText;

                // The second copy: every hook the game calls, in the game's order.
                Hook(second, "OnSubModuleLoad");
                Check(GetInstance<bool>(second, "_inert"), "the second copy did not stand down");
                Check(SingleCopy.Refused() == 1, "refusals counted: " + SingleCopy.Refused());
                Hook(second, "OnBeforeInitialModuleScreenSetAsRoot");
                Hook(second, "OnApplicationTick", 0.1f);
                Hook(second, "OnGameStart", null, starter);
                var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
                Hook(second, "OnMissionBehaviorInitialize", mission); // would attach (and fail on the empty mission, logged) if it ran
                Hook(second, "OnSubModuleUnloaded");
                string logAfter = LogText;
                Check(logAfter == logBefore, "the stood-down copy wrote to the log: " + (logAfter.Length > logBefore.Length ? logAfter.Substring(logBefore.Length).Trim() : "(changed)"));
                Check(asStarter.Models.Count() == modelsBefore, "the stood-down copy registered models: " + (asStarter.Models.Count() - modelsBefore));
                Check(messages.Count == 0, "the stood-down copy showed a message: " + string.Join(" | ", messages));

                // The running copy: main menu, then a game start - it registers once and reports once.
                Hook(running, "OnBeforeInitialModuleScreenSetAsRoot");
                Hook(running, "OnGameStart", null, starter);
                Check(asStarter.Models.OfType<TraxDamageModel>().Count() == 1, "damage decorators: " + asStarter.Models.OfType<TraxDamageModel>().Count() + " (expected 1)");
                Check(asStarter.Models.OfType<TraxAgentStatModel>().Count() == 1, "stat decorators: " + asStarter.Models.OfType<TraxAgentStatModel>().Count() + " (expected 1)");
                var copyMessages = messages.Where(m => m.Contains("copies are enabled")).ToList();
                Check(copyMessages.Count == 1, "copies messages: " + copyMessages.Count + " (expected 1)");
                Check(copyMessages.FirstOrDefault() == "Trax Combat Enhancements: 2 copies are enabled (TraxCombatEnhancements.Dev, TraxCombatEnhancements) - only TraxCombatEnhancements.Dev runs. Disable one in the launcher.",
                    "copies message: " + copyMessages.FirstOrDefault());
                LogHas("[compat] 1 other copy of this mod found this one (TraxCombatEnhancements.Dev) running and stood down");
                LogHas("(reported at main menu)");
                Check(Occurrences(LogText, "running and stood down") == 1, "the [compat] report should be logged once");
            }
            finally
            {
                InformationManager.DisplayMessageInternal -= listen;
            }
        }

        private static void Hook(SubModule sub, string name, params object?[] args)
        {
            var method = typeof(SubModule).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                Failures.Add("SubModule." + name + " not found");
                return;
            }
            try
            {
                method.Invoke(sub, args.Length == 0 ? null : args);
            }
            catch (TargetInvocationException e)
            {
                Failures.Add("SubModule." + name + " threw " + e.InnerException);
            }
        }

        private static T GetInstance<T>(object target, string field) =>
            (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(target)!;
    }
}
