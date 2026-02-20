using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudEventsTransportSenderShould
{
	[Fact]
	public void Constructor_Throws_WhenMapperIsNull()
	{
		var inner = A.Fake<ITransportSender>();
		_ = Should.Throw<ArgumentNullException>(() =>
			new CloudEventsTransportSender(inner, null!, _ => new CloudEvent()));
	}

	[Fact]
	public void Constructor_Throws_WhenCloudEventFactoryIsNull()
	{
		var inner = A.Fake<ITransportSender>();
		var mapper = A.Fake<ICloudEventMapper<TransportMessage>>();
		_ = Should.Throw<ArgumentNullException>(() =>
			new CloudEventsTransportSender(inner, mapper, null!));
	}

	[Fact]
	public async Task SendAsync_MapsMessageToCloudEvent_AndDelegatesToInnerSender()
	{
		var inner = A.Fake<ITransportSender>();
		var mapper = A.Fake<ICloudEventMapper<TransportMessage>>();
		var options = new CloudEventOptions { DefaultMode = CloudEventMode.Binary };
		var input = TransportMessage.FromString("payload");
		var encoded = TransportMessage.FromString("encoded");
		var expected = SendResult.Success("msg-1");

		A.CallTo(() => mapper.Options).Returns(options);
		A.CallTo(() => mapper.ToTransportMessageAsync(A<CloudEvent>._, CloudEventMode.Binary, A<CancellationToken>._))
			.Returns(Task.FromResult(encoded));
		A.CallTo(() => inner.SendAsync(encoded, A<CancellationToken>._))
			.Returns(Task.FromResult(expected));

		var sut = new CloudEventsTransportSender(
			inner,
			mapper,
			m =>
			{
				var cloudEvent = new CloudEvent
				{
					Id = m.Id,
					Type = "dispatch.test",
					Source = new Uri("urn:test")
				};
				return cloudEvent;
			});

		var result = await sut.SendAsync(input, CancellationToken.None);

		result.ShouldBe(expected);
		A.CallTo(() => mapper.ToTransportMessageAsync(A<CloudEvent>._, CloudEventMode.Binary, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => inner.SendAsync(encoded, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SendBatchAsync_MapsEachMessage_AndDelegatesBatchToInnerSender()
	{
		var inner = A.Fake<ITransportSender>();
		var mapper = A.Fake<ICloudEventMapper<TransportMessage>>();
		var options = new CloudEventOptions { DefaultMode = CloudEventMode.Structured };
		var messages = new List<TransportMessage> { TransportMessage.FromString("a"), TransportMessage.FromString("b") };
		var encodedMessages = new List<TransportMessage> { TransportMessage.FromString("ea"), TransportMessage.FromString("eb") };
		var expected = new BatchSendResult { TotalMessages = 2, SuccessCount = 2 };

		A.CallTo(() => mapper.Options).Returns(options);
		A.CallTo(() => mapper.ToTransportMessageAsync(A<CloudEvent>._, CloudEventMode.Structured, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				Task.FromResult(encodedMessages[0]),
				Task.FromResult(encodedMessages[1]));
		A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.ReturnsLazily((IReadOnlyList<TransportMessage> batch, CancellationToken _) =>
			{
				batch.Count.ShouldBe(2);
				batch[0].Body.ToArray().ShouldBe(encodedMessages[0].Body.ToArray());
				batch[1].Body.ToArray().ShouldBe(encodedMessages[1].Body.ToArray());
				return Task.FromResult(expected);
			});

		var sut = new CloudEventsTransportSender(
			inner,
			mapper,
			_ =>
			{
				var cloudEvent = new CloudEvent
				{
					Type = "dispatch.batch",
					Source = new Uri("urn:test")
				};
				return cloudEvent;
			});

		var result = await sut.SendBatchAsync(messages, CancellationToken.None);

		result.ShouldBe(expected);
		A.CallTo(() => mapper.ToTransportMessageAsync(A<CloudEvent>._, CloudEventMode.Structured, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}
}
