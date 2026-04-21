using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MultiTransportOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new MultiTransportOptions();

        options.Transports.ShouldNotBeNull();
        options.Transports.Count.ShouldBe(0);
        options.DefaultTransport.ShouldBeNull();
        options.EnableFailover.ShouldBeTrue();
    }

    [Fact]
    public void AllowAddingTransports()
    {
        var options = new MultiTransportOptions();
        options.Transports["rabbitmq"] = new TransportOptions { Name = "rabbitmq", Priority = 1 };
        options.Transports["kafka"] = new TransportOptions { Name = "kafka", Priority = 2 };

        options.Transports.Count.ShouldBe(2);
        options.Transports["rabbitmq"].Name.ShouldBe("rabbitmq");
        options.Transports["kafka"].Priority.ShouldBe(2);
    }

    [Fact]
    public void AllowSettingDefaultTransport()
    {
        var options = new MultiTransportOptions
        {
            DefaultTransport = "rabbitmq",
        };

        options.DefaultTransport.ShouldBe("rabbitmq");
    }

    [Fact]
    public void AllowDisablingFailover()
    {
        var options = new MultiTransportOptions
        {
            EnableFailover = false,
        };

        options.EnableFailover.ShouldBeFalse();
    }
}
