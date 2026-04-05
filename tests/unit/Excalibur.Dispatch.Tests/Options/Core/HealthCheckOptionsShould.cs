using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class HealthCheckOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new HealthCheckOptions();

        options.Enabled.ShouldBeFalse();
        options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
        options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new HealthCheckOptions
        {
            Enabled = true,
            Timeout = TimeSpan.FromSeconds(5),
            Interval = TimeSpan.FromMinutes(1),
        };

        options.Enabled.ShouldBeTrue();
        options.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.Interval.ShouldBe(TimeSpan.FromMinutes(1));
    }
}
