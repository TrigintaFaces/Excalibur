// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Author≠impl regression locks for the consumer side of distributed-tracing propagation across the
/// outbox (Sprint-846 Lane A, MS-A) — the <c>3wlvav</c> re-parent facet (<see cref="TracingMiddleware"/>)
/// and the end-to-end AC-A4 lock that proves <c>5l0da9</c> (publish restore) is distinct from
/// <c>kxksig</c> (staging capture).
///
/// AC-A3/A6/A7/EC-A3 exercise the dispatch span directly; AC-A4 chains the public
/// <see cref="MessageBusOutboxPublisher"/> restore into the internal <see cref="TracingMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Tracing")]
public sealed class TracingMiddlewareReparentShould : IDisposable
{
	private readonly ITelemetrySanitizer _fakeSanitizer = A.Fake<ITelemetrySanitizer>();
	private readonly ActivityListener _listener;
	private readonly ConcurrentBag<Activity> _capturedActivities = [];

	private static IOptions<ObservabilityOptions> DefaultOptions =>
		Microsoft.Extensions.Options.Options.Create(new ObservabilityOptions { IncludeSensitiveData = true });

	public TracingMiddlewareReparentShould()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == DispatchActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => _capturedActivities.Add(activity),
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		foreach (var activity in _capturedActivities)
		{
			activity.Dispose();
		}
	}

	// ----- 3wlvav (re-parent) -- FR-A3 / AC-A3 / AC-A6 / AC-A7 / EC-A3 -----

	[Fact]
	public async Task ReparentDispatchSpanToRestoredContext_WhenNoAmbientActivity()
	{
		// Arrange (AC-A3) -- a restored inbound traceparent is present and Activity.Current is null (the
		// dominant outbox-consumer case). The dispatch span MUST be a CHILD of the restored context
		// (same TraceId, ParentSpanId = restored span), an ActivityKind.Consumer span, NOT a new root.
		Activity.Current = null;
		var (traceParent, parentTraceId, parentSpanId) = NewRemoteParent();
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateContext(uniqueId, traceParent);

		// Act
		await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), context, SuccessNext(), CancellationToken.None);

		// Assert -- non-vacuity: the pre-fix `StartActivity(name, Internal)` (no parent) creates a new ROOT
		// when Activity.Current is null -> its TraceId differs and ParentSpanId is default (RED).
		var activity = FindActivityByMessageId(uniqueId);
		activity.TraceId.ToHexString().ShouldBe(parentTraceId);
		activity.ParentSpanId.ToHexString().ShouldBe(parentSpanId);
		activity.Kind.ShouldBe(ActivityKind.Consumer);
	}

	[Fact]
	public async Task StartNewRootWithoutThrowing_WhenRestoredTraceparentIsMalformed()
	{
		// Arrange (AC-A6 / EC-A2) -- a malformed restored traceparent must fail open: no throw, a new root
		// span (cross-cutting telemetry fails open, Microsoft-first).
		Activity.Current = null;
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateContext(uniqueId, "this-is-not-a-valid-traceparent");

		// Act + Assert -- must not throw.
		await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), context, SuccessNext(), CancellationToken.None);

		var activity = FindActivityByMessageId(uniqueId);
		activity.Parent.ShouldBeNull();
		activity.ParentSpanId.ToHexString().ShouldBe("0000000000000000");
	}

	[Fact]
	public async Task KeepAmbientAsParentAndLinkRestoredContext_WhenAmbientActivityPresent()
	{
		// Arrange (AC-A7) -- a restored traceparent AND a competing ambient Activity.Current. OTel: do not
		// hijack the ambient trace -> the ambient span stays the parent (same TraceId) and the restored
		// context is attached as an ActivityLink.
		// The remote parent MUST be minted in isolation (no ambient current) so it gets a DISTINCT trace;
		// the ambient is then started as its own separate root trace.
		Activity.Current = null;
		var (traceParent, restoredTraceId, _) = NewRemoteParent();
		using var ambient = StartManualActivity("ambient");
		var ambientTraceId = ambient.TraceId.ToHexString();
		restoredTraceId.ShouldNotBe(ambientTraceId); // guard: the two traces are genuinely distinct
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateContext(uniqueId, traceParent);

		// Act
		await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), context, SuccessNext(), CancellationToken.None);

		// Assert -- parent is the ambient (same TraceId, NOT the restored one); restored ctx present as a
		// link. Non-vacuity: the pre-fix `StartActivity(name, Internal)` adds NO link (RED on the link
		// assertion).
		var activity = FindActivityByMessageId(uniqueId);
		activity.TraceId.ToHexString().ShouldBe(ambientTraceId);
		activity.TraceId.ToHexString().ShouldNotBe(restoredTraceId);
		activity.Links.ShouldContain(link => link.Context.TraceId.ToHexString() == restoredTraceId);
	}

	[Fact]
	public async Task PreserveNoListenerFastPath_WhenNoActivityListenerRegistered()
	{
		// Arrange (EC-A3) -- with no listener, StartActivity returns null and the middleware takes the
		// no-overhead fast path: the pipeline still runs, no span is created, no parse is attempted.
		_listener.Dispose();
		Activity.Current = null;
		var (traceParent, _, _) = NewRemoteParent();
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateContext(uniqueId, traceParent);
		var nextRan = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextRan = true;
			return new ValueTask<IMessageResult>(SuccessResult());
		};

		// Act + Assert -- no throw, pipeline ran, no captured span for this id.
		await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), context, next, CancellationToken.None);

		nextRan.ShouldBeTrue();
		var capturedForThisId = _capturedActivities.Any(a => a.GetTagItem("message.id")?.ToString() == uniqueId);
		capturedForThisId.ShouldBeFalse();
	}

	// ----- AC-A4 (END-TO-END) -- the 5l0da9-is-distinct proof -----

	[Fact]
	public async Task PropagateTraceId_EndToEnd_StagedHeaderThroughPublishToDispatchSpan()
	{
		// Arrange -- a message staged under trace T (the kxksig staging output: a `traceparent` header,
		// proven independently in OutboxTraceparentPropagationShould). Drive the REAL publisher restore
		// (5l0da9), carry the restored traceparent across the transport boundary, then run the REAL
		// dispatch span creation (3wlvav). The consumer dispatch span MUST share TraceId == T.
		var (producerTraceParent, producerTraceId) = NewProducerTrace();

		// (1) kxksig output: the staged envelope carries the producer traceparent.
		var stored = new OutboundMessage("TestMessage", [1], "queue-1",
			new Dictionary<string, object>(StringComparer.Ordinal) { ["traceparent"] = producerTraceParent });

		var store = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { stored });
		var messageBus = A.Fake<IMessageBusAdapter>();
		IMessageContext? publishContext = null;
		A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext c, CancellationToken _) => publishContext = c)
			.Returns(SuccessResult());
		var publisher = new MessageBusOutboxPublisher(
			store, A.Fake<IPayloadSerializer>(), messageBus, A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);

		// (2) 5l0da9: publishing restores the traceparent onto the rebuilt publish context.
		await publisher.PublishPendingMessagesAsync(CancellationToken.None);
		publishContext.ShouldNotBeNull();
		var transportedTraceParent = publishContext.GetTraceParent();

		// (3) transport boundary -> the consumer rebuilds a fresh context and restores the traceparent
		// from the serialized envelope (the existing DispatchContextInitializer machinery). When 5l0da9 is
		// absent, `transportedTraceParent` is null here and this fresh context has no parent.
		Activity.Current = null;
		var uniqueId = Guid.NewGuid().ToString();
		var dispatchContext = CreateContext(uniqueId, transportedTraceParent);
		var middleware = new TracingMiddleware(DefaultOptions, _fakeSanitizer);

		// Act (4) 3wlvav: the dispatch span is created from the restored context.
		await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), dispatchContext, SuccessNext(), CancellationToken.None);

		// Assert -- single connected trace end-to-end. RED if 5l0da9 is reverted (publish drops the
		// traceparent -> new root) OR if 3wlvav is reverted (Internal, no parent -> new root).
		var activity = FindActivityByMessageId(uniqueId);
		activity.TraceId.ToHexString().ShouldBe(producerTraceId);
		activity.Kind.ShouldBe(ActivityKind.Consumer);
	}

	// ----- helpers -----

	private static IMessageContext CreateContext(string messageId, string? traceParent)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		A.CallTo(() => context.MessageId).Returns(messageId);
		if (!string.IsNullOrEmpty(traceParent))
		{
			context.GetOrCreateIdentityFeature().TraceParent = traceParent;
		}
		return context;
	}

	private static DispatchRequestDelegate SuccessNext() =>
		(_, _, _) => new ValueTask<IMessageResult>(SuccessResult());

	private static IMessageResult SuccessResult()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		return result;
	}

	/// <summary>
	/// Creates a started-then-stopped W3C activity and returns its id (a valid traceparent) plus the
	/// trace/span ids, modelling a remote parent restored on the consumer.
	/// </summary>
	private static (string traceParent, string traceId, string spanId) NewRemoteParent()
	{
		var parent = StartManualActivity("remote-parent");
		var traceParent = parent.Id!;
		var traceId = parent.TraceId.ToHexString();
		var spanId = parent.SpanId.ToHexString();
		parent.Stop();
		return (traceParent, traceId, spanId);
	}

	private static (string traceParent, string traceId) NewProducerTrace()
	{
		var producer = StartManualActivity("producer");
		var traceParent = producer.Id!;
		var traceId = producer.TraceId.ToHexString();
		producer.Stop();
		return (traceParent, traceId);
	}

	private static Activity StartManualActivity(string name)
	{
		var activity = new Activity(name);
		activity.SetIdFormat(ActivityIdFormat.W3C);
		activity.Start();
		return activity;
	}

	private Activity FindActivityByMessageId(string messageId)
	{
		var matching = _capturedActivities
			.Where(a => a.GetTagItem("message.id")?.ToString() == messageId)
			.ToList();
		matching.ShouldHaveSingleItem();
		return matching[0];
	}
}
