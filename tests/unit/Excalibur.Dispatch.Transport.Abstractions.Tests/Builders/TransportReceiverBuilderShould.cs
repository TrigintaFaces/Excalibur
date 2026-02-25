using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Builders;

public class TransportReceiverBuilderShould
{
    [Fact]
    public void Should_Throw_On_Null_InnerReceiver()
    {
        Should.Throw<ArgumentNullException>(() => new TransportReceiverBuilder(null!));
    }

    [Fact]
    public void Build_Should_Return_Inner_When_No_Decorators()
    {
        var inner = A.Fake<ITransportReceiver>();
        var builder = new TransportReceiverBuilder(inner);

        var result = builder.Build();

        result.ShouldBe(inner);
    }

    [Fact]
    public void Use_Should_Throw_On_Null_Decorator()
    {
        var inner = A.Fake<ITransportReceiver>();
        var builder = new TransportReceiverBuilder(inner);

        Should.Throw<ArgumentNullException>(() => builder.Use(null!));
    }

    [Fact]
    public void Use_Should_Return_Builder_For_Chaining()
    {
        var inner = A.Fake<ITransportReceiver>();
        var builder = new TransportReceiverBuilder(inner);

        var returned = builder.Use(r => r);

        returned.ShouldBe(builder);
    }

    [Fact]
    public void Build_Should_Apply_Single_Decorator()
    {
        var inner = A.Fake<ITransportReceiver>();
        var decorated = A.Fake<ITransportReceiver>();
        var builder = new TransportReceiverBuilder(inner);

        builder.Use(_ => decorated);
        var result = builder.Build();

        result.ShouldBe(decorated);
    }

    [Fact]
    public void Build_Should_Apply_Decorators_In_Registration_Order()
    {
        var inner = A.Fake<ITransportReceiver>();
        var callOrder = new List<int>();

        var builder = new TransportReceiverBuilder(inner);
        builder.Use(r => { callOrder.Add(1); return r; });
        builder.Use(r => { callOrder.Add(2); return r; });
        builder.Use(r => { callOrder.Add(3); return r; });

        builder.Build();

        callOrder.ShouldBe(new List<int> { 1, 2, 3 });
    }
}
