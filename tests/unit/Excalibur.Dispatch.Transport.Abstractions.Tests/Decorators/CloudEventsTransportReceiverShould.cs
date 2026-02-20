using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudEventsTransportReceiverShould
{
	[Fact]
	public void Constructor_Throws_WhenMapperIsNull()
	{
		var inner = A.Fake<ITransportReceiver>();
		_ = Should.Throw<ArgumentNullException>(() => new CloudEventsTransportReceiver(inner, null!));
	}

	[Fact]
	public async Task ReceiveAsync_ReturnsEmptyList_WhenInnerReturnsNoMessages()
	{
		var inner = A.Fake<ITransportReceiver>();
		var mapper = A.Fake<ICloudEventMapper<TransportReceivedMessage>>();
		A.CallTo(() => inner.ReceiveAsync(10, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage>());

		var sut = new CloudEventsTransportReceiver(inner, mapper);
		var result = await sut.ReceiveAsync(10, CancellationToken.None);

		result.ShouldBeEmpty();
		A.CallTo(() => mapper.TryDetectModeAsync(A<TransportReceivedMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReceiveAsync_UnwrapsMessage_WhenCloudEventDetectedAndUnwrapperProvided()
	{
		var inner = A.Fake<ITransportReceiver>();
		var mapper = A.Fake<ICloudEventMapper<TransportReceivedMessage>>();
		var message = new TransportReceivedMessage { Id = "m1" };
		var unwrapped = new TransportReceivedMessage { Id = "m1-unwrapped" };

		A.CallTo(() => inner.ReceiveAsync(1, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage> { message });
		A.CallTo(() => mapper.TryDetectModeAsync(message, A<CancellationToken>._))
			.ReturnsLazily(_ => new ValueTask<CloudEventMode?>(CloudEventMode.Structured));

		var sut = new CloudEventsTransportReceiver(inner, mapper, _ => unwrapped);
		var result = await sut.ReceiveAsync(1, CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].Id.ShouldBe("m1-unwrapped");
	}

	[Fact]
	public async Task ReceiveAsync_DoesNotUnwrap_WhenModeNotDetected()
	{
		var inner = A.Fake<ITransportReceiver>();
		var mapper = A.Fake<ICloudEventMapper<TransportReceivedMessage>>();
		var message = new TransportReceivedMessage { Id = "m2" };

		A.CallTo(() => inner.ReceiveAsync(1, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage> { message });
		A.CallTo(() => mapper.TryDetectModeAsync(message, A<CancellationToken>._))
			.ReturnsLazily(_ => new ValueTask<CloudEventMode?>((CloudEventMode?)null));

		var sut = new CloudEventsTransportReceiver(inner, mapper, _ => new TransportReceivedMessage { Id = "ignored" });
		var result = await sut.ReceiveAsync(1, CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].Id.ShouldBe("m2");
	}

	[Fact]
	public async Task ReceiveAsync_DoesNotUnwrap_WhenUnwrapperIsNull()
	{
		var inner = A.Fake<ITransportReceiver>();
		var mapper = A.Fake<ICloudEventMapper<TransportReceivedMessage>>();
		var message = new TransportReceivedMessage { Id = "m3" };

		A.CallTo(() => inner.ReceiveAsync(1, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage> { message });
		A.CallTo(() => mapper.TryDetectModeAsync(message, A<CancellationToken>._))
			.ReturnsLazily(_ => new ValueTask<CloudEventMode?>(CloudEventMode.Binary));

		var sut = new CloudEventsTransportReceiver(inner, mapper);
		var result = await sut.ReceiveAsync(1, CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].Id.ShouldBe("m3");
	}
}
