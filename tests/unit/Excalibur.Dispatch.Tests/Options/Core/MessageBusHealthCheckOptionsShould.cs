using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageBusHealthCheckOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new MessageBusHealthCheckOptions();

        options.Enabled.ShouldBeFalse();
        options.Timeout.ShouldBe(TimeSpan.FromSeconds(15));
        options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
        options.FailureThreshold.ShouldBe(3);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new MessageBusHealthCheckOptions
        {
            Enabled = true,
            Timeout = TimeSpan.FromSeconds(5),
            Interval = TimeSpan.FromMinutes(1),
            FailureThreshold = 5,
        };

        options.Enabled.ShouldBeTrue();
        options.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.Interval.ShouldBe(TimeSpan.FromMinutes(1));
        options.FailureThreshold.ShouldBe(5);
    }
}
