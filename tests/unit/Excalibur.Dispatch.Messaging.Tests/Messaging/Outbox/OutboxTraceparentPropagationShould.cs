// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>
/// Author≠impl regression locks for the producer side of distributed-tracing propagation across the
/// outbox (Sprint-846 Lane A, MS-A). Covers the <c>kxksig</c> staging-capture facet
/// (<see cref="OutboxStagingMiddleware"/>) and the <c>5l0da9</c> publish-restore facet
/// (<see cref="MessageBusOutboxPublisher"/>). The <c>3wlvav</c> re-parent facet and the full
/// staging→publish→dispatch end-to-end lock (AC-A4) live in the Observability test project because
/// the dispatch span is created by the internal <c>TracingMiddleware</c> (different InternalsVisibleTo
/// boundary; the internal <see cref="OutboxContext"/> driven here is only visible to this project).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Feature", "Tracing")]
public sealed class OutboxTraceparentPropagationShould
{
	// A syntactically valid W3C traceparent distinct from any ambient Activity id, used to prove
	// caller-set precedence (FR-A4) and the publish-side restore (FR-A2).
	private const string CallerTraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

	// ----- kxksig (staging capture) -- FR-A1 / AC-A1 / AC-A5 / EC-A1 -----

	[Fact]
	public async Task Stage_CapturesAmbientActivityTraceparent_IntoStagedHeaders()
	{
		// Arrange -- a real ambient Activity (W3C id) is current while staging runs; no caller-set
		// traceparent, so the capture must fall back to Activity.Current.Id (FR-A1).
		var store = A.Fake<IOutboxStore>();
		OutboundMessage? staged = null;
		A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => staged = m);

		var middleware = CreateStagingMiddleware(store);
		var context = CreateStagingContext();

		using var activity = StartW3CActivity("producer");

		// Act -- drive the pipeline; the handler stages an outbound message into the OutboxContext.
		await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(),
			context,
			StageOneMessageDelegate(),
			CancellationToken.None);

		// Assert -- non-vacuity: the pre-fix CreateMessageHeaders never wrote a traceparent header (RED).
		staged.ShouldNotBeNull();
		staged.Headers.ShouldContainKey("traceparent");
		staged.Headers["traceparent"].ShouldBe(activity.Id);
	}

	[Fact]
	public async Task Stage_PrefersCallerSetTraceParent_OverAmbientActivity()
	{
		// Arrange -- a caller already set TraceParent on the context AND an ambient Activity is current
		// with a different id. FR-A4: the caller's value wins (capture is `GetTraceParent() ?? Activity.Id`).
		var store = A.Fake<IOutboxStore>();
		OutboundMessage? staged = null;
		A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => staged = m);

		var middleware = CreateStagingMiddleware(store);
		var context = CreateStagingContext();
		context.GetOrCreateIdentityFeature().TraceParent = CallerTraceParent;

		using var activity = StartW3CActivity("producer");

		// Act
		await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(),
			context,
			StageOneMessageDelegate(),
			CancellationToken.None);

		// Assert -- caller value preserved, not overwritten by the ambient Activity id.
		staged.ShouldNotBeNull();
		staged.Headers["traceparent"].ShouldBe(CallerTraceParent);
		staged.Headers["traceparent"].ShouldNotBe(activity.Id);
	}

	[Fact]
	public async Task Stage_WithNoAmbientActivityAndNoCallerTraceParent_OmitsTraceparentHeader()
	{
		// Arrange -- neither a caller-set traceparent nor an ambient Activity. EC-A1: no header is written
		// (no empty/garbage value), guarding the fix from emitting a blank traceparent.
		Activity.Current = null;
		var store = A.Fake<IOutboxStore>();
		OutboundMessage? staged = null;
		A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => staged = m);

		var middleware = CreateStagingMiddleware(store);
		var context = CreateStagingContext();

		// Act
		await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(),
			context,
			StageOneMessageDelegate(),
			CancellationToken.None);

		// Assert
		staged.ShouldNotBeNull();
		staged.Headers.ShouldNotContainKey("traceparent");
	}

	// ----- 5l0da9 (publish restore) -- FR-A2 / AC-A2 / EC-A4 -----

	[Fact]
	public async Task Publish_RestoresTraceparentOntoSingleTransportContext()
	{
		// Arrange -- a stored outbox message whose headers carry a traceparent. The single-transport
		// publish path (PublishSingleTransportMessageAsync) must restore it onto the rebuilt context so
		// the transport-serialized envelope carries it (EC-A4: single path).
		var store = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		var messageBus = A.Fake<IMessageBusAdapter>();
		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), messageBus, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		var message = new OutboundMessage("TestMessage", [1], "queue-1", TraceparentHeaders(CallerTraceParent));
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		IMessageContext? publishedContext = null;
		A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext c, CancellationToken _) => publishedContext = c)
			.Returns(A.Fake<IMessageResult>());

		// Act
		await publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert -- non-vacuity: pre-fix the rebuilt context never set TraceParent (RED, null).
		publishedContext.ShouldNotBeNull();
		publishedContext.GetTraceParent().ShouldBe(CallerTraceParent);
	}

	[Fact]
	public async Task Publish_RestoresTraceparentOntoMultiTransportContext()
	{
		// Arrange -- the multi-transport per-transport publish path (PublishToTransportAsync) must ALSO
		// restore the traceparent (EC-A4: neither path left severed).
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

		var message = new OutboundMessage("OrderCreated", [1, 2, 3], "orders-default", TraceparentHeaders(CallerTraceParent));
		var transport = new OutboundMessageTransport(message.Id, "kafka") { Destination = "orders-topic" };
		A.CallTo(() => multiStoreAdmin.GetPendingTransportDeliveriesAsync("kafka", 10, A<CancellationToken>._))
			.Returns(new[] { (message, transport) });

		// Act
		await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		// Assert
		sentContext.ShouldNotBeNull();
		sentContext.GetTraceParent().ShouldBe(CallerTraceParent);
	}

	[Fact]
	public async Task Publish_WithNoTraceparentHeader_LeavesContextTraceParentNull()
	{
		// Arrange -- a stored message without a traceparent header must not fabricate one, and must not
		// throw (the restore is additive/only-if-present).
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
		publishedContext.GetTraceParent().ShouldBeNull();
	}

	// ----- helpers -----

	private static OutboxStagingMiddleware CreateStagingMiddleware(IOutboxStore store) =>
		new(
			Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true }),
			store,
			new DispatchJsonSerializer(),
			NullLogger<OutboxStagingMiddleware>.Instance);

	/// <summary>
	/// Creates an <see cref="IMessageContext"/> backed by real Items and Features dictionaries so the
	/// context extension methods (GetItem/SetItem, GetOrCreateIdentityFeature, GetTraceParent) behave
	/// as in production.
	/// </summary>
	private static IMessageContext CreateStagingContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return context;
	}

	/// <summary>
	/// A pipeline continuation that stages a single outbound message into the active
	/// <see cref="OutboxContext"/>, mimicking a handler that emits an event.
	/// </summary>
	private static DispatchRequestDelegate StageOneMessageDelegate() =>
		(_, ctx, _) =>
		{
			var outboxContext = ctx.GetItem<OutboxContext>("OutboxContext");
			outboxContext.ShouldNotBeNull();
			outboxContext.AddOutboundMessage(new TestMessage { Content = "evt" }, "downstream-queue");
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		};

	private static Dictionary<string, object> TraceparentHeaders(string traceParent) =>
		new(StringComparer.Ordinal) { ["traceparent"] = traceParent };

	private static Activity StartW3CActivity(string name)
	{
		var activity = new Activity(name);
		activity.SetIdFormat(ActivityIdFormat.W3C);
		activity.Start();
		return activity;
	}

	private sealed class TestMessage : IDispatchMessage
	{
		public string Content { get; set; } = string.Empty;
	}
}
