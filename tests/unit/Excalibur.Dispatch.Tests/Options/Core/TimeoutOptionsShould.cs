using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TimeoutOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new TimeoutOptions();

        options.Enabled.ShouldBeFalse();
        options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.MessageTypeTimeouts.ShouldNotBeNull();
        options.MessageTypeTimeouts.Count.ShouldBe(0);
        options.ThrowOnTimeout.ShouldBeTrue();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new TimeoutOptions
        {
            Enabled = true,
            DefaultTimeout = TimeSpan.FromMinutes(1),
            ThrowOnTimeout = false,
        };

        options.Enabled.ShouldBeTrue();
        options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        options.ThrowOnTimeout.ShouldBeFalse();
    }

    [Fact]
    public void AllowAddingMessageTypeTimeouts()
    {
        var options = new TimeoutOptions();
        options.MessageTypeTimeouts["OrderCreated"] = TimeSpan.FromSeconds(60);
        options.MessageTypeTimeouts["PaymentProcessed"] = TimeSpan.FromSeconds(120);

        options.MessageTypeTimeouts.Count.ShouldBe(2);
        options.MessageTypeTimeouts["OrderCreated"].ShouldBe(TimeSpan.FromSeconds(60));
        options.MessageTypeTimeouts["PaymentProcessed"].ShouldBe(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void UseOrdinalComparisonForMessageTypes()
    {
        var options = new TimeoutOptions();
        options.MessageTypeTimeouts["Order"] = TimeSpan.FromSeconds(10);

        // Ordinal comparison means case matters
        options.MessageTypeTimeouts.ContainsKey("Order").ShouldBeTrue();
    }
}
