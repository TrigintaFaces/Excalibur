using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Builders;

public class TransportSubscriberBuilderShould
{
    [Fact]
    public void Should_Throw_On_Null_InnerSubscriber()
    {
        Should.Throw<ArgumentNullException>(() => new TransportSubscriberBuilder(null!));
    }

    [Fact]
    public void Build_Should_Return_Inner_When_No_Decorators()
    {
        var inner = A.Fake<ITransportSubscriber>();
        var builder = new TransportSubscriberBuilder(inner);

        var result = builder.Build();

        result.ShouldBe(inner);
    }

    [Fact]
    public void Use_Should_Throw_On_Null_Decorator()
    {
        var inner = A.Fake<ITransportSubscriber>();
        var builder = new TransportSubscriberBuilder(inner);

        Should.Throw<ArgumentNullException>(() => builder.Use(null!));
    }

    [Fact]
    public void Use_Should_Return_Builder_For_Chaining()
    {
        var inner = A.Fake<ITransportSubscriber>();
        var builder = new TransportSubscriberBuilder(inner);

        var returned = builder.Use(s => s);

        returned.ShouldBe(builder);
    }

    [Fact]
    public void Build_Should_Apply_Decorators_In_Registration_Order()
    {
        var inner = A.Fake<ITransportSubscriber>();
        var callOrder = new List<int>();

        var builder = new TransportSubscriberBuilder(inner);
        builder.Use(s => { callOrder.Add(1); return s; });
        builder.Use(s => { callOrder.Add(2); return s; });

        builder.Build();

        callOrder.ShouldBe(new List<int> { 1, 2 });
    }
}
