using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

public class DelegatingTransportSubscriberShould
{
    private sealed class TestDelegatingSubscriber : DelegatingTransportSubscriber
    {
        public TestDelegatingSubscriber(ITransportSubscriber innerSubscriber) : base(innerSubscriber) { }

        public ITransportSubscriber ExposedInnerSubscriber => InnerSubscriber;
    }

    [Fact]
    public void Should_Throw_On_Null_InnerSubscriber()
    {
        Should.Throw<ArgumentNullException>(() => new TestDelegatingSubscriber(null!));
    }

    [Fact]
    public void Should_Expose_InnerSubscriber()
    {
        var inner = A.Fake<ITransportSubscriber>();
        var delegating = new TestDelegatingSubscriber(inner);

        delegating.ExposedInnerSubscriber.ShouldBe(inner);
    }

    [Fact]
    public void Source_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSubscriber>();
        A.CallTo(() => inner.Source).Returns("my-topic");
        var delegating = new TestDelegatingSubscriber(inner);

        delegating.Source.ShouldBe("my-topic");
    }

    [Fact]
    public async Task SubscribeAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSubscriber>();
        var delegating = new TestDelegatingSubscriber(inner);
        Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler =
            (_, _) => Task.FromResult(MessageAction.Acknowledge);

        await delegating.SubscribeAsync(handler, CancellationToken.None);

        A.CallTo(() => inner.SubscribeAsync(handler, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GetService_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSubscriber>();
        A.CallTo(() => inner.GetService(typeof(int))).Returns(42);

        var delegating = new TestDelegatingSubscriber(inner);
        delegating.GetService(typeof(int)).ShouldBe(42);
    }

    [Fact]
    public async Task DisposeAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSubscriber>();
        var delegating = new TestDelegatingSubscriber(inner);

        await delegating.DisposeAsync();

        A.CallTo(() => inner.DisposeAsync()).MustHaveHappenedOnceExactly();
    }
}
