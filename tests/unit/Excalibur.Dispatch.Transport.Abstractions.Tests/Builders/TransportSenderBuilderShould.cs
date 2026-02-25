using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Builders;

public class TransportSenderBuilderShould
{
    [Fact]
    public void Should_Throw_On_Null_InnerSender()
    {
        Should.Throw<ArgumentNullException>(() => new TransportSenderBuilder(null!));
    }

    [Fact]
    public void Build_Should_Return_Inner_When_No_Decorators()
    {
        var inner = A.Fake<ITransportSender>();
        var builder = new TransportSenderBuilder(inner);

        var result = builder.Build();

        result.ShouldBe(inner);
    }

    [Fact]
    public void Use_Should_Throw_On_Null_Decorator()
    {
        var inner = A.Fake<ITransportSender>();
        var builder = new TransportSenderBuilder(inner);

        Should.Throw<ArgumentNullException>(() => builder.Use(null!));
    }

    [Fact]
    public void Use_Should_Return_Builder_For_Chaining()
    {
        var inner = A.Fake<ITransportSender>();
        var builder = new TransportSenderBuilder(inner);

        var returned = builder.Use(s => s);

        returned.ShouldBe(builder);
    }

    [Fact]
    public void Build_Should_Apply_Single_Decorator()
    {
        var inner = A.Fake<ITransportSender>();
        var decorated = A.Fake<ITransportSender>();
        var builder = new TransportSenderBuilder(inner);

        builder.Use(_ => decorated);
        var result = builder.Build();

        result.ShouldBe(decorated);
    }

    [Fact]
    public void Build_Should_Apply_Decorators_In_Order()
    {
        var inner = A.Fake<ITransportSender>();
        var callOrder = new List<string>();

        var builder = new TransportSenderBuilder(inner);
        builder.Use(s =>
        {
            callOrder.Add("first");
            return s;
        });
        builder.Use(s =>
        {
            callOrder.Add("second");
            return s;
        });

        builder.Build();

        callOrder.ShouldBe(new List<string> { "first", "second" });
    }

    [Fact]
    public void Build_Should_Chain_Decorators_Correctly()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.Destination).Returns("inner");

        ITransportSender? receivedBySecond = null;

        var builder = new TransportSenderBuilder(inner);
        builder.Use(s =>
        {
            // First decorator wraps inner
            s.Destination.ShouldBe("inner");
            return s;
        });
        builder.Use(s =>
        {
            receivedBySecond = s;
            return s;
        });

        builder.Build();

        receivedBySecond.ShouldNotBeNull();
    }
}
