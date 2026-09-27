using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 12: when the MCM bridge tries the settings page and when it stops (Anton's playtest,
/// log 21:08 - another mod carried an MCMv5 DLL while MCM's module was off, and the bridge retried
/// "not ready" every second all session).</summary>
public class McmPlanTests
{
    [Fact]
    public void Mcms_module_is_found_in_the_enabled_list_by_its_id()
    {
        Assert.True(McmPlan.ModuleEnabled(new[] { "Bannerlord.Harmony", "Bannerlord.MBOptionScreen", "Native" }));
        Assert.True(McmPlan.ModuleEnabled(new[] { "bannerlord.mboptionscreen " }));   // case and stray spaces
        // Anton's 21:08 session: ButterLib and UIExtenderEx on, MCM's module off
        Assert.False(McmPlan.ModuleEnabled(new[] { "Bannerlord.Harmony", "Bannerlord.ButterLib", "Bannerlord.UIExtenderEx", "Native", "ImmersiveAI.Dev" }));
        // not known (the list could not be read - the offline smoke): the bridge just tries
        Assert.Null(McmPlan.ModuleEnabled(null));
        Assert.Null(McmPlan.ModuleEnabled(Array.Empty<string>()));
    }

    [Fact]
    public void The_assembly_first_then_the_module()
    {
        Assert.Equal(McmVerdict.NotLoaded, McmPlan.Decide(false, true));
        Assert.Equal(McmVerdict.NotLoaded, McmPlan.Decide(false, false));   // no DLL at all: the old "not loaded" line
        Assert.Equal(McmVerdict.ModuleNotEnabled, McmPlan.Decide(true, false));
        Assert.Equal(McmVerdict.Try, McmPlan.Decide(true, true));
        Assert.Equal(McmVerdict.Try, McmPlan.Decide(true, null));
    }

    [Fact]
    public void Retries_are_capped()
    {
        Assert.True(McmPlan.MaxRetries > 0);
        for (int notReady = 1; notReady <= McmPlan.MaxRetries; notReady++) Assert.False(McmPlan.GiveUp(notReady));
        Assert.True(McmPlan.GiveUp(McmPlan.MaxRetries + 1));   // the first attempt + MaxRetries retries, then stop
    }

    [Fact]
    public void The_lines_say_what_happened_in_plain_words()
    {
        Assert.StartsWith("MCM's module is not enabled - no settings page; config.json only.", McmPlan.ModuleNotEnabledLine("5.12.2.0"));
        Assert.Contains("MCM 5.12.2.0 DLL is loaded", McmPlan.ModuleNotEnabledLine("5.12.2.0"));
        Assert.Contains(McmPlan.ModuleId, McmPlan.ModuleNotEnabledLine("5.12.2.0"));
        Assert.Equal("MCM 5.12.3.0 is loaded but not ready yet at main menu - retrying every 1 s, at most " + McmPlan.MaxRetries + " times.",
            McmPlan.NotReadyLine("5.12.3.0", "main menu"));
        Assert.Equal("MCM 5.12.3.0 never became ready - gave up after 31 attempts (the first + " + McmPlan.MaxRetries
                     + " retries, one a second) - no settings page; config.json only.", McmPlan.GiveUpLine("5.12.3.0", 31));
    }
}
