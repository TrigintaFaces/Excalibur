using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageRoutingOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new MessageRoutingOptions();

        options.MessageTypeRouting.ShouldNotBeNull();
        options.MessageTypeRouting.Count.ShouldBe(0);
        options.DefaultRoutingPattern.ShouldBe("{MessageType}");
        options.UseMessageTypeAsRoutingKey.ShouldBeTrue();
        options.RoutingKeyGenerators.ShouldNotBeNull();
        options.RoutingKeyGenerators.Count.ShouldBe(0);
    }

    [Fact]
    public void AllowAddingMessageTypeRouting()
    {
        var options = new MessageRoutingOptions();
        options.MessageTypeRouting["OrderCreated"] = "orders-topic";
        options.MessageTypeRouting["PaymentProcessed"] = "payments-topic";

        options.MessageTypeRouting.Count.ShouldBe(2);
        options.MessageTypeRouting["OrderCreated"].ShouldBe("orders-topic");
    }

    [Fact]
    public void AllowSettingDefaultRoutingPattern()
    {
        var options = new MessageRoutingOptions
        {
            DefaultRoutingPattern = "{Namespace}.{MessageType}",
        };

        options.DefaultRoutingPattern.ShouldBe("{Namespace}.{MessageType}");
    }

    [Fact]
    public void AllowAddingRoutingKeyGenerators()
    {
        var options = new MessageRoutingOptions();
        options.RoutingKeyGenerators["OrderCreated"] = msg => "custom-key";

        options.RoutingKeyGenerators.Count.ShouldBe(1);
        options.RoutingKeyGenerators["OrderCreated"](new object()).ShouldBe("custom-key");
    }

    [Fact]
    public void AllowDisablingMessageTypeAsRoutingKey()
    {
        var options = new MessageRoutingOptions
        {
            UseMessageTypeAsRoutingKey = false,
        };

        options.UseMessageTypeAsRoutingKey.ShouldBeFalse();
    }
}
