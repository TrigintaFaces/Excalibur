using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MetricsOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new MetricsOptions();

        options.Enabled.ShouldBeFalse();
        options.ExportInterval.ShouldBe(TimeSpan.FromSeconds(30));
        options.CustomTags.ShouldNotBeNull();
        options.CustomTags.Count.ShouldBe(0);
    }

    [Fact]
    public void AllowSettingProperties()
    {
        var options = new MetricsOptions
        {
            Enabled = true,
            ExportInterval = TimeSpan.FromMinutes(1),
        };

        options.Enabled.ShouldBeTrue();
        options.ExportInterval.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void AllowAddingCustomTags()
    {
        var options = new MetricsOptions();
        options.CustomTags["environment"] = "production";
        options.CustomTags["service"] = "orders";

        options.CustomTags.Count.ShouldBe(2);
        options.CustomTags["environment"].ShouldBe("production");
        options.CustomTags["service"].ShouldBe("orders");
    }
}
