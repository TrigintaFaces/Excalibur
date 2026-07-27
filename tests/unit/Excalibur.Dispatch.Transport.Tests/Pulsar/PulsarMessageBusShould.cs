// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotPulsar;
using DotPulsar.Abstractions;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Pulsar;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Pulsar;

/// <summary>
/// Unit locks for <see cref="PulsarMessageBus"/> — each typed <c>PublishAsync</c> overload serializes the
/// runtime concrete type via <see cref="IPayloadSerializer"/> and sends the produced payload to the Pulsar
/// producer. Non-vacuous: the fake serializer returns a distinct payload per message and the send is
/// asserted to carry exactly that payload.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PulsarMessageBusShould
{
	private static readonly MessageId FakeMessageId = new(0UL, 0UL, 0, 0, "t");

	[Fact]
	public async Task Publish_Action_SerializesRuntimeTypeAndSendsPayload()
	{
		var producer = A.Fake<IProducer<byte[]>>();
		var serializer = A.Fake<IPayloadSerializer>();
		var context = A.Fake<IMessageContext>();
		var action = new TestAction();
		var payload = new byte[] { 0x01, 0x02, 0x03 };

		_ = A.CallTo(() => serializer.SerializeObject(action, action.GetType())).Returns(payload);
		byte[]? sent = null;
		_ = A.CallTo(() => producer.Send(A<MessageMetadata>._, A<byte[]>._, A<CancellationToken>._))
			.Invokes((MessageMetadata _, byte[] data, CancellationToken _) => sent = data)
			.Returns(new ValueTask<MessageId>(FakeMessageId));

		await using var bus = new PulsarMessageBus(producer, serializer, "orders", NullLogger<PulsarMessageBus>.Instance);

		await bus.PublishAsync(action, context, CancellationToken.None);

		_ = A.CallTo(() => serializer.SerializeObject(action, action.GetType())).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => producer.Send(A<MessageMetadata>._, A<byte[]>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		sent.ShouldBe(payload);
	}

	[Fact]
	public async Task Publish_Event_SerializesRuntimeTypeAndSendsPayload()
	{
		var producer = A.Fake<IProducer<byte[]>>();
		var serializer = A.Fake<IPayloadSerializer>();
		var context = A.Fake<IMessageContext>();
		var evt = new TestEvent();
		var payload = new byte[] { 0x0A, 0x0B };

		_ = A.CallTo(() => serializer.SerializeObject(evt, evt.GetType())).Returns(payload);
		byte[]? sent = null;
		_ = A.CallTo(() => producer.Send(A<MessageMetadata>._, A<byte[]>._, A<CancellationToken>._))
			.Invokes((MessageMetadata _, byte[] data, CancellationToken _) => sent = data)
			.Returns(new ValueTask<MessageId>(FakeMessageId));

		await using var bus = new PulsarMessageBus(producer, serializer, "orders", NullLogger<PulsarMessageBus>.Instance);

		await bus.PublishAsync(evt, context, CancellationToken.None);

		_ = A.CallTo(() => producer.Send(A<MessageMetadata>._, A<byte[]>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		sent.ShouldBe(payload);
	}

	[Fact]
	public async Task Publish_Document_SerializesRuntimeTypeAndSendsPayload()
	{
		var producer = A.Fake<IProducer<byte[]>>();
		var serializer = A.Fake<IPayloadSerializer>();
		var context = A.Fake<IMessageContext>();
		var doc = new TestDocument();
		var payload = new byte[] { 0xFF };

		_ = A.CallTo(() => serializer.SerializeObject(doc, doc.GetType())).Returns(payload);
		byte[]? sent = null;
		_ = A.CallTo(() => producer.Send(A<MessageMetadata>._, A<byte[]>._, A<CancellationToken>._))
			.Invokes((MessageMetadata _, byte[] data, CancellationToken _) => sent = data)
			.Returns(new ValueTask<MessageId>(FakeMessageId));

		await using var bus = new PulsarMessageBus(producer, serializer, "orders", NullLogger<PulsarMessageBus>.Instance);

		await bus.PublishAsync(doc, context, CancellationToken.None);

		_ = A.CallTo(() => producer.Send(A<MessageMetadata>._, A<byte[]>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		sent.ShouldBe(payload);
	}

	[Fact]
	public async Task Publish_Action_PropagatesCorrelationIdAsPartitionKey()
	{
		var producer = A.Fake<IProducer<byte[]>>();
		var serializer = A.Fake<IPayloadSerializer>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.CorrelationId).Returns("corr-42");
		var action = new TestAction();
		_ = A.CallTo(() => serializer.SerializeObject(action, action.GetType())).Returns(new byte[] { 0x01 });

		MessageMetadata? captured = null;
		_ = A.CallTo(() => producer.Send(A<MessageMetadata>._, A<byte[]>._, A<CancellationToken>._))
			.Invokes((MessageMetadata md, byte[] _, CancellationToken _) => captured = md)
			.Returns(new ValueTask<MessageId>(FakeMessageId));

		await using var bus = new PulsarMessageBus(producer, serializer, "orders", NullLogger<PulsarMessageBus>.Instance);

		await bus.PublishAsync(action, context, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured!.Key.ShouldBe("corr-42");
	}

	[Fact]
	public async Task Publish_Action_ThrowsOnNullAction()
	{
		var bus = new PulsarMessageBus(
			A.Fake<IProducer<byte[]>>(),
			A.Fake<IPayloadSerializer>(),
			"orders",
			NullLogger<PulsarMessageBus>.Instance);

		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => bus.PublishAsync((IDispatchAction)null!, A.Fake<IMessageContext>(), CancellationToken.None));
	}

	private sealed class TestAction : IDispatchAction
	{
	}

	private sealed class TestEvent : IDispatchEvent
	{
	}

	private sealed class TestDocument : IDispatchDocument
	{
	}
}
