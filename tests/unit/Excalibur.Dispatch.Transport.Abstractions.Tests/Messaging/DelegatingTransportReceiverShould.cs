using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

public class DelegatingTransportReceiverShould
{
    private sealed class TestDelegatingReceiver : DelegatingTransportReceiver
    {
        public TestDelegatingReceiver(ITransportReceiver innerReceiver) : base(innerReceiver) { }

        public ITransportReceiver ExposedInnerReceiver => InnerReceiver;
    }

    [Fact]
    public void Should_Throw_On_Null_InnerReceiver()
    {
        Should.Throw<ArgumentNullException>(() => new TestDelegatingReceiver(null!));
    }

    [Fact]
    public void Should_Expose_InnerReceiver()
    {
        var inner = A.Fake<ITransportReceiver>();
        var delegating = new TestDelegatingReceiver(inner);

        delegating.ExposedInnerReceiver.ShouldBe(inner);
    }

    [Fact]
    public void Source_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("my-queue");
        var delegating = new TestDelegatingReceiver(inner);

        delegating.Source.ShouldBe("my-queue");
    }

    [Fact]
    public async Task ReceiveAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        var expected = new List<TransportReceivedMessage> { new() { Id = "msg-1" } };
        A.CallTo(() => inner.ReceiveAsync(10, A<CancellationToken>._)).Returns(expected);

        var delegating = new TestDelegatingReceiver(inner);
        var result = await delegating.ReceiveAsync(10, CancellationToken.None);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task AcknowledgeAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        var message = new TransportReceivedMessage { Id = "msg-1" };
        var delegating = new TestDelegatingReceiver(inner);

        await delegating.AcknowledgeAsync(message, CancellationToken.None);

        A.CallTo(() => inner.AcknowledgeAsync(message, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RejectAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        var message = new TransportReceivedMessage { Id = "msg-1" };
        var delegating = new TestDelegatingReceiver(inner);

        await delegating.RejectAsync(message, "bad message", true, CancellationToken.None);

        A.CallTo(() => inner.RejectAsync(message, "bad message", true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GetService_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.GetService(typeof(string))).Returns("test");

        var delegating = new TestDelegatingReceiver(inner);
        delegating.GetService(typeof(string)).ShouldBe("test");
    }

    [Fact]
    public async Task DisposeAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        var delegating = new TestDelegatingReceiver(inner);

        await delegating.DisposeAsync();

        A.CallTo(() => inner.DisposeAsync()).MustHaveHappenedOnceExactly();
    }
}
