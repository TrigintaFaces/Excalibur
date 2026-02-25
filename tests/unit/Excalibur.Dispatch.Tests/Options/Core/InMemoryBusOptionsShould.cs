using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryBusOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new InMemoryBusOptions();

        options.MaxQueueLength.ShouldBe(1000);
        options.PreserveOrder.ShouldBeTrue();
        options.ProcessingDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new InMemoryBusOptions
        {
            MaxQueueLength = 5000,
            PreserveOrder = false,
            ProcessingDelay = TimeSpan.FromMilliseconds(100),
        };

        options.MaxQueueLength.ShouldBe(5000);
        options.PreserveOrder.ShouldBeFalse();
        options.ProcessingDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
    }
}
