// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="CloudEventsTransportSender"/>.
/// Verifies CloudEvent encoding on SendAsync and SendBatchAsync.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudEventsTransportSenderShould
{
	private readonly ITransportSender _innerSender = A.Fake<ITransportSender>();
	private readonly ICloudEventMapper<TransportMessage> _mapper = A.Fake<ICloudEventMapper<TransportMessage>>();
	private readonly CloudEventOptions _options = new() { DefaultMode = CloudEventMode.Structured };

	public CloudEventsTransportSenderShould()
	{
		A.CallTo(() => _innerSender.Destination).Returns("test-topic");
		A.CallTo(() => _mapper.Options).Returns(_options);
	}

	[Fact]
	public async Task SendAsync_Converts_Message_To_CloudEvent_Format()
	{
		var originalMessage = TransportMessage.FromString("hello");
		var encodedMessage = TransportMessage.FromString("ce-encoded");
		var cloudEvent = CreateTestCloudEvent();

		Func<TransportMessage, CloudEvent> factory = _ => cloudEvent;

		A.CallTo(() => _mapper.ToTransportMessageAsync(cloudEvent, CloudEventMode.Structured, A<CancellationToken>._))
			.Returns(encodedMessage);
		A.CallTo(() => _innerSender.SendAsync(encodedMessage, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));

		var sut = new CloudEventsTransportSender(_innerSender, _mapper, factory);
		var result = await sut.SendAsync(originalMessage, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => _innerSender.SendAsync(encodedMessage, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SendBatchAsync_Converts_Each_Message()
	{
		var messages = new[]
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
		};

		var encodedA = TransportMessage.FromString("ce-a");
		var encodedB = TransportMessage.FromString("ce-b");
		var cloudEventA = CreateTestCloudEvent("event-a");
		var cloudEventB = CreateTestCloudEvent("event-b");

		var callIndex = 0;
		Func<TransportMessage, CloudEvent> factory = msg =>
		{
			return callIndex++ == 0 ? cloudEventA : cloudEventB;
		};

		A.CallTo(() => _mapper.ToTransportMessageAsync(cloudEventA, CloudEventMode.Structured, A<CancellationToken>._))
			.Returns(encodedA);
		A.CallTo(() => _mapper.ToTransportMessageAsync(cloudEventB, CloudEventMode.Structured, A<CancellationToken>._))
			.Returns(encodedB);
		A.CallTo(() => _innerSender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Returns(new BatchSendResult { TotalMessages = 2, SuccessCount = 2 });

		var sut = new CloudEventsTransportSender(_innerSender, _mapper, factory);
		var result = await sut.SendBatchAsync(messages, CancellationToken.None);

		result.TotalMessages.ShouldBe(2);
		A.CallTo(() => _innerSender.SendBatchAsync(
				A<IReadOnlyList<TransportMessage>>.That.Matches(list => list.Count == 2),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Uses_Mapper_To_Apply_CloudEvent_Attributes()
	{
		var originalMessage = TransportMessage.FromString("hello");
		var cloudEvent = CreateTestCloudEvent();
		var encodedMessage = TransportMessage.FromString("encoded");

		Func<TransportMessage, CloudEvent> factory = _ => cloudEvent;

		A.CallTo(() => _mapper.ToTransportMessageAsync(cloudEvent, CloudEventMode.Structured, A<CancellationToken>._))
			.Returns(encodedMessage);
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));

		var sut = new CloudEventsTransportSender(_innerSender, _mapper, factory);
		await sut.SendAsync(originalMessage, CancellationToken.None);

		A.CallTo(() => _mapper.ToTransportMessageAsync(
				cloudEvent, CloudEventMode.Structured, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Uses_Factory_To_Create_CloudEvent()
	{
		var originalMessage = TransportMessage.FromString("hello");
		var encodedMessage = TransportMessage.FromString("encoded");
		TransportMessage? capturedInput = null;

		var cloudEvent = CreateTestCloudEvent();
		Func<TransportMessage, CloudEvent> factory = msg =>
		{
			capturedInput = msg;
			return cloudEvent;
		};

		A.CallTo(() => _mapper.ToTransportMessageAsync(A<CloudEvent>._, A<CloudEventMode>._, A<CancellationToken>._))
			.Returns(encodedMessage);
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));

		var sut = new CloudEventsTransportSender(_innerSender, _mapper, factory);
		await sut.SendAsync(originalMessage, CancellationToken.None);

		capturedInput.ShouldBeSameAs(originalMessage);
	}

	[Fact]
	public async Task Delegates_To_Inner_Sender()
	{
		var encodedMessage = TransportMessage.FromString("encoded");
		var cloudEvent = CreateTestCloudEvent();

		A.CallTo(() => _mapper.ToTransportMessageAsync(A<CloudEvent>._, A<CloudEventMode>._, A<CancellationToken>._))
			.Returns(encodedMessage);
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));

		var sut = new CloudEventsTransportSender(_innerSender, _mapper, _ => cloudEvent);
		await sut.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);

		A.CallTo(() => _innerSender.SendAsync(encodedMessage, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Throw_When_Mapper_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new CloudEventsTransportSender(_innerSender, null!, _ => CreateTestCloudEvent()));
	}

	[Fact]
	public void Throw_When_CloudEventFactory_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new CloudEventsTransportSender(_innerSender, _mapper, null!));
	}

	private static CloudEvent CreateTestCloudEvent(string? id = null) =>
		new(CloudEventsSpecVersion.V1_0)
		{
			Id = id ?? "ce-test-1",
			Type = "test.event",
			Source = new Uri("urn:test"),
		};
}
