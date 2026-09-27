using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 11: the dev and the release copy enabled together - the first to load runs, the other
/// stands down (SingleCopy). Each test uses its own slot, so the real one is never touched.</summary>
public class SingleCopyTests
{
    private static string FreshSlot()
    {
        string slot = "TraxTest." + Guid.NewGuid().ToString("N");
        SingleCopy.ResetForTests(slot);
        return slot;
    }

    [Fact]
    public void The_first_copy_to_claim_runs_and_a_second_one_stands_down()
    {
        string slot = FreshSlot();
        object release = new object(), dev = new object();

        Assert.True(SingleCopy.TryClaim(release, slot));
        Assert.False(SingleCopy.TryClaim(dev, slot));
        Assert.Equal(1, SingleCopy.Refused(slot));
    }

    [Fact]
    public void The_running_copy_asking_again_still_runs_and_is_not_counted()
    {
        string slot = FreshSlot();
        object running = new object();

        Assert.True(SingleCopy.TryClaim(running, slot));
        Assert.True(SingleCopy.TryClaim(running, slot));
        Assert.Equal(0, SingleCopy.Refused(slot));
    }

    [Fact]
    public void Two_instances_of_the_same_type_are_two_copies()
    {
        // Same version = one assembly, two SubModule instances: the claim is by instance.
        string slot = FreshSlot();
        var first = new List<int>();
        var second = new List<int>();

        Assert.True(SingleCopy.TryClaim(first, slot));
        Assert.False(SingleCopy.TryClaim(second, slot));
        Assert.False(SingleCopy.TryClaim(new List<int>(), slot));
        Assert.Equal(2, SingleCopy.Refused(slot));
    }

    [Fact]
    public void An_empty_or_foreign_slot_value_is_claimed()
    {
        string slot = FreshSlot();
        Assert.Equal(0, SingleCopy.Refused(slot));
        AppDomain.CurrentDomain.SetData(slot, "something else");
        Assert.True(SingleCopy.TryClaim(new object(), slot));
    }

    [Fact]
    public void Copies_are_the_release_id_and_its_dotted_variants_only()
    {
        var copies = SingleCopy.CopiesIn(new[]
        {
            "Native", "SandBoxCore", "TraxCombatEnhancements.Dev", "Bannerlord.MBOptionScreen",
            "TraxCombatEnhancementsExtra", "traxcombatenhancements",
        });
        Assert.Equal(new[] { "TraxCombatEnhancements.Dev", "traxcombatenhancements" }, copies);
        Assert.Empty(SingleCopy.CopiesIn(null));
    }

    [Fact]
    public void The_module_id_is_read_from_the_manifest()
    {
        const string manifest = "<Module>\n  <!-- Id note -->\n  <Id value=\"TraxCombatEnhancements.Dev\" />\n  <Name value=\"Trax Combat Enhancements (dev)\" />\n</Module>";
        Assert.Equal("TraxCombatEnhancements.Dev", SingleCopy.IdFromManifest(manifest));
        Assert.Null(SingleCopy.IdFromManifest("<Module><Name value=\"x\" /></Module>"));
        Assert.Null(SingleCopy.IdFromManifest(null));
    }

    [Fact]
    public void One_copy_says_nothing()
    {
        var one = new[] { "TraxCombatEnhancements" };
        Assert.Null(SingleCopy.LoadLine(one, "TraxCombatEnhancements"));
        Assert.Null(SingleCopy.ReportLine(one, "TraxCombatEnhancements", 0));
        Assert.Null(SingleCopy.Message(one, "TraxCombatEnhancements", 0));
    }

    [Fact]
    public void Two_copies_one_stood_down_name_the_one_that_runs()
    {
        var two = new[] { "TraxCombatEnhancements.Dev", "TraxCombatEnhancements" };

        Assert.Contains("2 copies of this mod are enabled (TraxCombatEnhancements.Dev, TraxCombatEnhancements) - this one (TraxCombatEnhancements.Dev) loaded first and runs",
            SingleCopy.LoadLine(two, "TraxCombatEnhancements.Dev"));
        Assert.StartsWith("1 other copy of this mod found this one (TraxCombatEnhancements.Dev) running and stood down",
            SingleCopy.ReportLine(two, "TraxCombatEnhancements.Dev", 1));
        Assert.Equal("Trax Combat Enhancements: 2 copies are enabled (TraxCombatEnhancements.Dev, TraxCombatEnhancements) - only TraxCombatEnhancements.Dev runs. Disable one in the launcher.",
            SingleCopy.Message(two, "TraxCombatEnhancements.Dev", 1));
    }

    [Fact]
    public void Two_copies_none_stood_down_is_a_warning()
    {
        var two = new[] { "TraxCombatEnhancements", "TraxCombatEnhancements.Dev" };
        Assert.StartsWith("WARNING: 2 copies of this mod are enabled", SingleCopy.ReportLine(two, "TraxCombatEnhancements", 0));
        Assert.Equal("Trax Combat Enhancements: 2 copies are enabled (TraxCombatEnhancements, TraxCombatEnhancements.Dev) - disable one in the launcher.",
            SingleCopy.Message(two, "TraxCombatEnhancements", 0));
    }

    [Fact]
    public void A_refusal_is_reported_even_when_the_module_list_was_not_read()
    {
        var none = Array.Empty<string>();
        Assert.StartsWith("1 other copy of this mod found this one (this copy) running", SingleCopy.ReportLine(none, "this copy", 1));
        Assert.Equal("Trax Combat Enhancements: 2 copies are enabled - only this copy runs. Disable one in the launcher.",
            SingleCopy.Message(none, "this copy", 1));
    }
}
