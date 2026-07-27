// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // ValueTask in FakeItEasy .Returns()

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Middleware.Resilience;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.ErrorHandling;

/// <summary>
/// Author≠impl regression lock for S852 · <c>8o3c3p</c> — <see cref="DeadLetterOnExhaustionMiddleware"/>
/// auto-dead-letters a dispatch ONLY once <see cref="RetryMiddleware"/> has genuinely exhausted its retries.
/// </summary>
/// <remarks>
/// <para>
/// The decorator composes the committed retry-exhaustion terminal — it routes <strong>only</strong> the
/// distinct <c>RetryProblemTypes.RetryExhausted</c> result to <see cref="IDeadLetterQueue"/> with
/// <see cref="DeadLetterReason.MaxRetriesExceeded"/>, and <strong>never</strong> <c>RetryError</c> (non-retryable
/// abandon), a handler's own failed result (permanent-before-cap), or a success. The dead-letter write is a
/// side-effect: the original result always flows up unchanged, and a DLQ enqueue failure is swallowed
/// (<b>fail-open</b>). Authored independently of the implementer (PlatformDeveloper) against the working-tree seam.
/// </para>
/// <para>
/// <b>RED mutants:</b> route on <c>RetryError</c> instead of <c>RetryExhausted</c> ⇒ the
/// routes-exhausted fact records zero enqueues AND the rejects-RetryError fact records one ⇒ both RED;
/// re-counting attempts / routing handler-failures ⇒ the rejects-failure facts RED.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeadLetterOnExhaustionMiddlewareShould
{
	[Fact]
	public async Task RouteRetryExhausted_ToDeadLetterQueue_ExactlyOnce_AndReturnOriginal()
	{
		var dlq = A.Fake<IDeadLetterQueue>();
		var exhausted = Failed(RetryProblemTypes.RetryExhausted, "exhausted after 3 attempts");

		var returned = await Invoke(dlq, exhausted);

		// Exactly one enqueue, with the distinct MaxRetriesExceeded reason.
		A.CallTo(() => dlq.EnqueueAsync<IDispatchMessage>(
				A<IDispatchMessage>._, DeadLetterReason.MaxRetriesExceeded, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
		// Side-effect only: the original exhausted result flows up unchanged.
		returned.ShouldBeSameAs(exhausted);
	}

	[Fact]
	public async Task NotRoute_RetryError()
	{
		var dlq = A.Fake<IDeadLetterQueue>();
		var retryError = Failed(RetryProblemTypes.RetryError, "non-retryable");

		var returned = await Invoke(dlq, retryError);

		AssertNoEnqueue(dlq);
		returned.ShouldBeSameAs(retryError);
	}

	[Fact]
	public async Task NotRoute_HandlerFailure()
	{
		var dlq = A.Fake<IDeadLetterQueue>();
		var handlerFailure = Failed("SomeHandlerError", "permanent before cap");

		var returned = await Invoke(dlq, handlerFailure);

		AssertNoEnqueue(dlq);
		returned.ShouldBeSameAs(handlerFailure);
	}

	[Fact]
	public async Task NotRoute_Success()
	{
		var dlq = A.Fake<IDeadLetterQueue>();
		var success = MessageResult.Success();

		_ = await Invoke(dlq, success);

		AssertNoEnqueue(dlq);
	}

	[Fact]
	public async Task FailOpen_WhenDlqThrows_ReturnOriginal_NoRethrow()
	{
		var dlq = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => dlq.EnqueueAsync<IDispatchMessage>(
				A<IDispatchMessage>._, A<DeadLetterReason>._, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.Throws<InvalidOperationException>();
		var exhausted = Failed(RetryProblemTypes.RetryExhausted, "exhausted");

		// Must NOT rethrow — a DLQ failure can never mask/crash the original exhaustion.
		var returned = await Invoke(dlq, exhausted);

		returned.ShouldBeSameAs(exhausted);
	}

	/// <summary>
	/// S853 · <c>bpgznn</c> regression lock: cooperative cancellation that trips mid-DLQ-enqueue MUST
	/// propagate as an <see cref="OperationCanceledException"/> — it is NOT swallowed by the fail-open catch.
	/// </summary>
	/// <remarks>
	/// Structural RED argument: pre-fix, the fail-open handler was a bare <c>catch (Exception)</c>, which
	/// caught the <see cref="OperationCanceledException"/> and returned the original result instead of
	/// letting cancellation propagate — so <c>Should.ThrowAsync&lt;OperationCanceledException&gt;</c> would
	/// observe NO throw and fail. The fix adds the exception filter
	/// <c>when (ex is not OperationCanceledException)</c>, so the OCE bypasses the catch and propagates.
	/// The companion fail-open half (a NON-cancellation enqueue failure is swallowed and the original
	/// exhausted result flows up) is locked by <see cref="FailOpen_WhenDlqThrows_ReturnOriginal_NoRethrow"/>.
	/// </remarks>
	[Fact]
	public async Task PropagateOperationCanceledException_WhenDlqEnqueueIsCancelled()
	{
		var dlq = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => dlq.EnqueueAsync<IDispatchMessage>(
				A<IDispatchMessage>._, A<DeadLetterReason>._, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.Throws<OperationCanceledException>();
		var exhausted = Failed(RetryProblemTypes.RetryExhausted, "exhausted");

		// The OCE must NOT be swallowed by the fail-open catch — cooperative cancellation propagates.
		_ = await Should.ThrowAsync<OperationCanceledException>(Invoke(dlq, exhausted));
	}

	/// <summary>
	/// SAFETY — a host that opted in to <see cref="NullDeadLetterQueue"/> has its exhausted messages reported
	/// as <em>discarded</em>, never as dead-lettered.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that keeps the fabricated entry id unreachable. Without the guard the middleware enqueues
	/// into the no-op, receives <see cref="Guid.Empty"/>, discards it, and logs "routed message … to the
	/// dead-letter queue" over a message that was dropped — an operator reading that log would go looking for an
	/// entry that was never written.
	/// </para>
	/// <para>
	/// Asserted through the emitted log rather than through a call-count, because <see cref="NullDeadLetterQueue"/>
	/// is sealed with a private constructor and cannot be spied on. The log is also the surface an operator
	/// actually reads, which makes it the honest thing to bind.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReportDiscardRatherThanDeadLettering_WhenTheHostOptedIntoTheNoOpQueue()
	{
		var logger = new RecordingLogger();
		var middleware = new DeadLetterOnExhaustionMiddleware(NullDeadLetterQueue.Instance, logger);

		DispatchRequestDelegate next = (_, _, _) =>
			new ValueTask<IMessageResult>(Failed(RetryProblemTypes.RetryExhausted, "exhausted"));

		_ = await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(), CreateContext(), next, CancellationToken.None);

		logger.EventIds.ShouldContain(
			DeliveryEventId.DeadLetterOnExhaustionDiscarded,
			"an opted-in discard must be reported as a discard");

		logger.EventIds.ShouldNotContain(
			DeliveryEventId.DeadLetterOnExhaustionEnqueued,
			"a discarded message must never be logged as routed to the dead-letter queue");
	}

	/// <summary>
	/// LIVENESS — a real queue still reports the routing. Without this arm a middleware that logged nothing at
	/// all, or that discarded unconditionally, would satisfy the safety arm above.
	/// </summary>
	[Fact]
	public async Task StillReportDeadLettering_WhenARealQueueIsConfigured()
	{
		var logger = new RecordingLogger();
		var dlq = A.Fake<IDeadLetterQueue>();
		var middleware = new DeadLetterOnExhaustionMiddleware(dlq, logger);

		DispatchRequestDelegate next = (_, _, _) =>
			new ValueTask<IMessageResult>(Failed(RetryProblemTypes.RetryExhausted, "exhausted"));

		_ = await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(), CreateContext(), next, CancellationToken.None);

		logger.EventIds.ShouldContain(
			DeliveryEventId.DeadLetterOnExhaustionEnqueued,
			"a real dead-letter queue must still report the routing");

		logger.EventIds.ShouldNotContain(
			DeliveryEventId.DeadLetterOnExhaustionDiscarded,
			"a stored message must never be reported as discarded");

		A.CallTo(() => dlq.EnqueueAsync<IDispatchMessage>(
				A<IDispatchMessage>._, A<DeadLetterReason>._, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// Records emitted event ids. Implements <see cref="ILogger{TCategoryName}"/> directly rather than deriving
	/// from a first-party base, so the assertion binds the logging the middleware actually performs.
	/// </summary>
	private sealed class RecordingLogger : ILogger<DeadLetterOnExhaustionMiddleware>
	{
		private readonly List<int> _eventIds = [];

		public IReadOnlyList<int> EventIds => _eventIds;

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) =>
			_eventIds.Add(eventId.Id);
	}

	private static void AssertNoEnqueue(IDeadLetterQueue dlq) =>
		A.CallTo(() => dlq.EnqueueAsync<IDispatchMessage>(
				A<IDispatchMessage>._, A<DeadLetterReason>._, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();

	private static async Task<IMessageResult> Invoke(IDeadLetterQueue dlq, IMessageResult nextResult)
	{
		var middleware = new DeadLetterOnExhaustionMiddleware(
			dlq, NullLogger<DeadLetterOnExhaustionMiddleware>.Instance);

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(nextResult);

		return await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), CreateContext(), next, CancellationToken.None);
	}

	private static IMessageResult Failed(string type, string detail) =>
		MessageResult.Failed(new MessageProblemDetails
		{
			Type = type,
			Title = type,
			ErrorCode = 500,
			Status = 500,
			Detail = detail,
			Instance = "dlq-exhaustion-msg",
		});

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns("dlq-exhaustion-msg");
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return context;
	}
}
