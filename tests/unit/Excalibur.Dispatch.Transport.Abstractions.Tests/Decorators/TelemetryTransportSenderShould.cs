using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

public sealed class TelemetryTransportSenderShould : IDisposable
{
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;

    public TelemetryTransportSenderShould()
    {
        _meter = new Meter("test.sender");
        _activitySource = new ActivitySource("test.sender");
    }

    public void Dispose()
    {
        _meter.Dispose();
        _activitySource.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Should_Throw_On_Null_Meter()
    {
        var inner = A.Fake<ITransportSender>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportSender(inner, null!, _activitySource, "Kafka"));
    }

    [Fact]
    public void Should_Throw_On_Null_ActivitySource()
    {
        var inner = A.Fake<ITransportSender>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportSender(inner, _meter, null!, "Kafka"));
    }

    [Fact]
    public void Should_Throw_On_Null_TransportName()
    {
        var inner = A.Fake<ITransportSender>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportSender(inner, _meter, _activitySource, null!));
    }

    [Fact]
    public async Task SendAsync_Should_Delegate_To_Inner_On_Success()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.Destination).Returns("test-queue");
        var expected = SendResult.Success("msg-1");
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._)).Returns(expected);

        var sender = new TelemetryTransportSender(inner, _meter, _activitySource, "TestTransport");
        var result = await sender.SendAsync(new TransportMessage(), CancellationToken.None);

        result.ShouldBe(expected);
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendAsync_Should_Propagate_Failure_Result()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.Destination).Returns("test-queue");
        var error = new SendError { Code = "TIMEOUT", Message = "Timed out" };
        var expected = SendResult.Failure(error);
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._)).Returns(expected);

        var sender = new TelemetryTransportSender(inner, _meter, _activitySource, "TestTransport");
        var result = await sender.SendAsync(new TransportMessage(), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("TIMEOUT");
    }

    [Fact]
    public async Task SendAsync_Should_Rethrow_Exception()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.Destination).Returns("test-queue");
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Connection lost"));

        var sender = new TelemetryTransportSender(inner, _meter, _activitySource, "TestTransport");

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sender.SendAsync(new TransportMessage(), CancellationToken.None));
        ex.Message.ShouldBe("Connection lost");
    }

    [Fact]
    public async Task SendBatchAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.Destination).Returns("test-queue");
        var expected = new BatchSendResult { TotalMessages = 3, SuccessCount = 3 };
        A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
            .Returns(expected);

        var sender = new TelemetryTransportSender(inner, _meter, _activitySource, "TestTransport");
        var messages = new List<TransportMessage> { new(), new(), new() };
        var result = await sender.SendBatchAsync(messages, CancellationToken.None);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task SendBatchAsync_Should_Propagate_Failure_Count()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.Destination).Returns("test-queue");
        var expected = new BatchSendResult { TotalMessages = 4, SuccessCount = 2, FailureCount = 2 };
        A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
            .Returns(expected);

        var sender = new TelemetryTransportSender(inner, _meter, _activitySource, "TestTransport");
        var result = await sender.SendBatchAsync([new TransportMessage(), new TransportMessage(), new TransportMessage(), new TransportMessage()], CancellationToken.None);

        result.ShouldBe(expected);
        result.FailureCount.ShouldBe(2);
    }

    [Fact]
    public async Task SendBatchAsync_Should_Rethrow_Exception()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.Destination).Returns("test-queue");
        A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
            .Throws(new TimeoutException("batch timeout"));

        var sender = new TelemetryTransportSender(inner, _meter, _activitySource, "TestTransport");

        await Should.ThrowAsync<TimeoutException>(
            () => sender.SendBatchAsync([new TransportMessage()], CancellationToken.None));
    }
}
