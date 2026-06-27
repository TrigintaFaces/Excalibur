// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>
/// Author≠impl regression lock for the consume-side restore of W3C <c>baggage</c> across the outbox
/// (FR-D6, bead <c>4desh8</c>). The producer's baggage is written into the staged envelope by
/// <c>OutboxStagingMiddleware.GetBaggageHeader</c> (percent-encoded members, header <c>"baggage"</c>),
/// but before the fix it was silently dropped on the outbox publish hop — never restored onto the
/// rebuilt context, so it never reached the next consumer. The fix adds
/// <c>MessageBusOutboxPublisher.RestoreBaggage</c>, invoked symmetric with <c>RestoreTraceParent</c> at
/// BOTH publish paths (per-transport <c>PublishToTransportAsync</c> and single-bus
/// <c>PublishSingleTransportMessageAsync</c>). Each <c>name=value</c> member is percent-DECODED
/// (folds the 7npc0q W3C percent-encoding so values carrying <c>,</c>/<c>=</c>/<c>%</c> survive the
/// round-trip) and written back into <c>context.Items["baggage.{name}"]</c> — the exact slot the
/// capture (<c>DispatchContextInitializer</c>) and the stage-write read.
/// </summary>
/// <remarks>
/// Non-vacuity: RED on the pre-fix code (no <c>RestoreBaggage</c> → the rebuilt context's
/// <c>Items</c> has no <c>baggage.*</c> keys, so every assertion below fails); GREEN on the
/// symmetric-restore fix. Production RED-proof (mutation of the impl) deferred to post-commit because
/// the impl file is reserved by the implementer lane; this lock is author≠impl and deterministic
/// (no real infrastructure — a faked bus / in-memory transport drives the restore path).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Feature", "Tracing")]
public sealed class OutboxBaggageRestoreShould
{
	// A realistic staged baggage header exactly as OutboxStagingMiddleware.GetBaggageHeader would emit it:
	// members joined by ',', each "name=value" with both halves Uri.EscapeDataString-encoded. Members:
	//   key1 = "val 1"  (space → %20, requires percent-decoding — locks 7npc0q)
	//   key2 = "val2"   (plain, no decoding needed)
	//   k,3  = "a,b"    (delimiter chars encoded as %2C so they survive the split — locks the round-trip)
	private const string StagedBaggage = "key1=val%201,key2=val2,k%2C3=a%2Cb";

	// ----- single-bus publish path (PublishSingleTransportMessageAsync) -----

	[Fact]
	public async Task Publish_RestoresPercentDecodedBaggageOntoSingleBusContext()
	{
		// Arrange -- a stored outbox message whose headers carry a percent-encoded baggage string. The
		// single-transport publish path must restore each member onto the rebuilt context's Items, decoded.
		var store = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		var messageBus = A.Fake<IMessageBusAdapter>();
		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), messageBus, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("TestMessage", [1], "queue-1", BaggageHeaders(StagedBaggage));
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		IMessageContext? publishedContext = null;
		A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext c, CancellationToken _) => publishedContext = c)
			.Returns(A.Fake<IMessageResult>());

		// Act
		await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert -- non-vacuity: pre-fix the rebuilt context had no baggage.* items (RED, key missing).
		publishedContext.ShouldNotBeNull();
		publishedContext.Items["baggage.key1"].ShouldBe("val 1"); // %20 percent-decoded to a space
		publishedContext.Items["baggage.key2"].ShouldBe("val2");
		publishedContext.Items["baggage.k,3"].ShouldBe("a,b");    // %2C-encoded delimiters survived the split
	}

	// ----- per-transport publish path (PublishToTransportAsync) -----

	[Fact]
	public async Task Publish_RestoresPercentDecodedBaggageOntoMultiTransportContext()
	{
		// Arrange -- the multi-transport per-transport publish path must ALSO restore baggage (the fix
		// is symmetric at both call sites; neither path may be left severed).
		var multiStoreBase = A.Fake<IOutboxStore>(o =>
		{
			_ = o.Implements<IMultiTransportOutboxStore>();
			_ = o.Implements<IMultiTransportOutboxStoreAdmin>();
		});
		var multiStoreAdmin = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStoreAdmin>();

		var adapter = A.Fake<ITransportAdapter>();
		IMessageContext? sentContext = null;
		A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, string _, IMessageContext c, CancellationToken _) => sentContext = c)
			.Returns(Task.CompletedTask);

		var transportRegistry = new TransportRegistry();
		transportRegistry.RegisterTransport("kafka", adapter, "Kafka");

		var publisher = new MessageBusOutboxPublisher(
			multiStoreBase, A.Fake<IPayloadSerializer>(), transportRegistry, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("OrderCreated", [1, 2, 3], "orders-default", BaggageHeaders(StagedBaggage));
		var transport = new OutboundMessageTransport(message.Id, "kafka") { Destination = "orders-topic" };
		A.CallTo(() => multiStoreAdmin.GetPendingTransportDeliveriesAsync("kafka", 10, A<CancellationToken>._))
			.Returns(new[] { (message, transport) });

		// Act
		await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		// Assert
		sentContext.ShouldNotBeNull();
		sentContext.Items["baggage.key1"].ShouldBe("val 1");
		sentContext.Items["baggage.key2"].ShouldBe("val2");
		sentContext.Items["baggage.k,3"].ShouldBe("a,b");
	}

	// ----- best-effort / negative paths -----

	[Fact]
	public async Task Publish_SkipsMalformedBaggageMembers_WithoutThrowing()
	{
		// Arrange -- a header mixing malformed members (no '=', and an empty key) with one valid member.
		// RestoreBaggage is best-effort: malformed members are skipped, the valid one still restores, and
		// no exception escapes (which would have failed the publish).
		var store = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		var messageBus = A.Fake<IMessageBusAdapter>();
		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), messageBus, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("TestMessage", [1], "queue-1", BaggageHeaders("noequals,=emptykey,good=value"));
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		IMessageContext? publishedContext = null;
		A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext c, CancellationToken _) => publishedContext = c)
			.Returns(A.Fake<IMessageResult>());

		// Act
		var result = await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert -- the publish succeeded (no throw) and only the valid member was restored.
		result.IsSuccess.ShouldBeTrue();
		publishedContext.ShouldNotBeNull();
		publishedContext.Items["baggage.good"].ShouldBe("value");
		publishedContext.Items.ShouldNotContainKey("baggage.");
	}

	[Fact]
	public async Task Publish_WithNoBaggageHeader_LeavesContextWithoutBaggageItems()
	{
		// Arrange -- a stored message without a baggage header must not fabricate baggage items and must
		// not throw (the restore is additive / only-if-present).
		var store = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		var messageBus = A.Fake<IMessageBusAdapter>();
		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), messageBus, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("TestMessage", [1], "queue-1");
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		IMessageContext? publishedContext = null;
		A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext c, CancellationToken _) => publishedContext = c)
			.Returns(A.Fake<IMessageResult>());

		// Act
		await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert
		publishedContext.ShouldNotBeNull();
		publishedContext.Items.Keys.ShouldNotContain(k => k.StartsWith("baggage.", StringComparison.Ordinal));
	}

	// ----- helpers -----

	private static Dictionary<string, object> BaggageHeaders(string baggage) =>
		new(StringComparer.Ordinal) { ["baggage"] = baggage };
}
