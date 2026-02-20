using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

public class DelegatingTransportSenderShould
{
    private sealed class TestDelegatingSender : DelegatingTransportSender
    {
        public TestDelegatingSender(ITransportSender innerSender) : base(innerSender) { }

        public ITransportSender ExposedInnerSender => InnerSender;
    }

    [Fact]
    public void Should_Throw_On_Null_InnerSender()
    {
        Should.Throw<ArgumentNullException>(() => new TestDelegatingSender(null!));
    }

    [Fact]
    public void Should_Expose_InnerSender()
    {
        var inner = A.Fake<ITransportSender>();
        var delegating = new TestDelegatingSender(inner);

        delegating.ExposedInnerSender.ShouldBe(inner);
    }

    [Fact]
    public void Destination_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.Destination).Returns("my-queue");
        var delegating = new TestDelegatingSender(inner);

        delegating.Destination.ShouldBe("my-queue");
    }

    [Fact]
    public async Task SendAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        var expected = SendResult.Success("msg-1");
        var message = new TransportMessage();
        A.CallTo(() => inner.SendAsync(message, A<CancellationToken>._)).Returns(expected);

        var delegating = new TestDelegatingSender(inner);
        var result = await delegating.SendAsync(message, CancellationToken.None);

        result.ShouldBe(expected);
        A.CallTo(() => inner.SendAsync(message, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendBatchAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        var expected = new BatchSendResult { TotalMessages = 2, SuccessCount = 2 };
        var messages = new List<TransportMessage> { new(), new() };
        A.CallTo(() => inner.SendBatchAsync(messages, A<CancellationToken>._)).Returns(expected);

        var delegating = new TestDelegatingSender(inner);
        var result = await delegating.SendBatchAsync(messages, CancellationToken.None);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task FlushAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        var delegating = new TestDelegatingSender(inner);

        await delegating.FlushAsync(CancellationToken.None);

        A.CallTo(() => inner.FlushAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GetService_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        var service = new object();
        A.CallTo(() => inner.GetService(typeof(object))).Returns(service);

        var delegating = new TestDelegatingSender(inner);
        var result = delegating.GetService(typeof(object));

        result.ShouldBe(service);
    }

    [Fact]
    public async Task DisposeAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        var delegating = new TestDelegatingSender(inner);

        await delegating.DisposeAsync();

        A.CallTo(() => inner.DisposeAsync()).MustHaveHappenedOnceExactly();
    }
}
