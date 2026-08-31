// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Middleware.Resilience;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Optional opt-in middleware that auto-dead-letters an in-process dispatch once <see cref="RetryMiddleware"/>
/// has exhausted every retry attempt. It is placed <em>upstream</em> of <see cref="RetryMiddleware"/>
/// so the retry middleware runs as its <c>next</c> delegate; it then routes a genuine retry-exhaustion to
/// the dead-letter queue, whether the exhaustion arrives as a returned <see cref="IMessageResult"/> or as a
/// propagating exception.
/// </summary>
/// <remarks>
/// <para>
/// This decorator <em>composes</em> the retry-exhaustion terminal — it does not re-implement attempt counting
/// or exhaustion detection (<see cref="RetryMiddleware"/> is the single source of truth). It routes
/// <strong>only</strong> a genuine exhaustion, which the retry middleware records on the message context —
/// never a fault it declined to retry, and never a handler's own failed result from before the retry cap,
/// neither of which is an exhaustion.
/// </para>
/// <para>
/// It is intentionally distinct from <see cref="PoisonMessageMiddleware"/> (which owns
/// <see cref="DeadLetterReason.PoisonMessage"/>/<see cref="DeadLetterReason.DeserializationFailed"/>): this
/// decorator owns <see cref="DeadLetterReason.MaxRetriesExceeded"/> only, so the two compose rather than
/// duplicate.
/// </para>
/// <para>
/// The dead-letter write is a best-effort <em>side effect</em>: the original exhaustion always flows up
/// unchanged — a returned failure stays terminal and visible (never swallowed into a fake success), and a
/// propagating exception is rethrown untouched. A failure of the dead-letter enqueue itself is logged and
/// swallowed (<strong>fail-open</strong>) so an unavailable dead-letter queue can never mask or crash the
/// original exhaustion.
/// </para>
/// </remarks>
public sealed partial class DeadLetterOnExhaustionMiddleware : IDispatchMiddleware
{
	private readonly IDeadLetterQueue _deadLetterQueue;
	private readonly ILogger<DeadLetterOnExhaustionMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DeadLetterOnExhaustionMiddleware"/> class.
	/// </summary>
	/// <param name="deadLetterQueue">The dead-letter queue exhausted dispatches are routed to.</param>
	/// <param name="logger">The logger instance.</param>
	public DeadLetterOnExhaustionMiddleware(
		IDeadLetterQueue deadLetterQueue,
		ILogger<DeadLetterOnExhaustionMiddleware> logger)
	{
		_deadLetterQueue = deadLetterQueue ?? throw new ArgumentNullException(nameof(deadLetterQueue));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <see cref="DispatchMiddlewareStage.PostProcessing"/> (700) is numerically <em>below</em>
	/// <see cref="RetryMiddleware"/>'s <see cref="DispatchMiddlewareStage.ErrorHandling"/> (800), and the
	/// pipeline runs lower stages as the outer wrappers, so this decorator runs upstream of the retry middleware
	/// — the retry middleware executes within this decorator's <c>next</c> delegate, so its exhaustion is
	/// observed here either as the result it returns or as the exception it lets through.
	/// </remarks>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

	/// <inheritdoc/>
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		IMessageResult result;
		try
		{
			result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (context.RetryExhausted())
		{
			// Exhaustion that left the retry middleware by throwing. Both arms are needed and neither
			// double-routes: this one runs only when the dispatch unwound, the one below only when it
			// returned. Which of the two happens for a given consumer also depends on registration order —
			// exception mapping shares this decorator's stage, so it may be composed either inside or outside
			// it — and the recorded fact is correct in either position, where a returned problem type would
			// be correct in only one.
			await RouteToDeadLetterAsync(message, exception.Message, context, cancellationToken).ConfigureAwait(false);
			throw;
		}

		// Exhaustion that left the retry middleware by returning the downstream's own failure.
		if (context.RetryExhausted())
		{
			await RouteToDeadLetterAsync(message, result.ProblemDetails?.Detail, context, cancellationToken).ConfigureAwait(false);
		}

		// The dead-letter write is a side effect; the original (exhausted) result always flows up unchanged.
		return result;
	}

	private async ValueTask RouteToDeadLetterAsync(
		IDispatchMessage message,
		string? detail,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		var messageId = context.MessageId ?? string.Empty;

		// A host may opt in to discarding exhausted messages by registering NullDeadLetterQueue itself.
		// That choice is honoured, but it is NOT reported as a dead-letter routing: enqueueing here would
		// return Guid.Empty — an entry id naming no entry — and LogDeadLetteredOnExhaustion would claim
		// the message was "routed to the dead-letter queue" when it was dropped. Both processors already
		// take this branch (InboxProcessor:936, OutboxProcessor:798); this composes with that shape rather
		// than forking a second, less honest one. The no-op's EnqueueAsync therefore has no caller.
		if (_deadLetterQueue is NullDeadLetterQueue)
		{
			LogDiscardedNoDeadLetterQueue(messageId);
			return;
		}

		try
		{
			var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["messageId"] = messageId,
			};

			// Preserved for inspection/replay. The exception object is deliberately not stored: the
			// returned-failure path has none, so passing one on only the other path would make the
			// dead-letter entry's shape depend on which way the retry middleware happened to leave.
			if (!string.IsNullOrEmpty(detail))
			{
				metadata["detail"] = detail;
			}

			_ = await _deadLetterQueue.EnqueueAsync(
					message,
					DeadLetterReason.MaxRetriesExceeded,
					cancellationToken,
					exception: null,
					metadata: metadata)
				.ConfigureAwait(false);

			LogDeadLetteredOnExhaustion(messageId);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Fail-open: a best-effort dead-letter capture must never mask or crash the original exhaustion.
			// OperationCanceledException is deliberately excluded from the filter so that cooperative
			// cancellation (the caller's token tripping mid-enqueue) propagates instead of being swallowed —
			// matching RetryMiddleware's OCE discipline and CdcFatalClassifier (OCE => not a fault).
			LogDeadLetterEnqueueFailed(ex, messageId);
		}
	}

	[LoggerMessage(
		DeliveryEventId.DeadLetterOnExhaustionEnqueued,
		LogLevel.Warning,
		"Retry attempts exhausted; routed message {MessageId} to the dead-letter queue (MaxRetriesExceeded).")]
	private partial void LogDeadLetteredOnExhaustion(string messageId);

	[LoggerMessage(
		DeliveryEventId.DeadLetterOnExhaustionEnqueueFailed,
		LogLevel.Error,
		"Best-effort dead-letter enqueue failed for exhausted message {MessageId}; the original exhausted result still flows up (fail-open).")]
	private partial void LogDeadLetterEnqueueFailed(Exception exception, string messageId);

	[LoggerMessage(
		DeliveryEventId.DeadLetterOnExhaustionDiscarded,
		LogLevel.Warning,
		"Retry attempts exhausted; DISCARDED message {MessageId} because the host registered the no-op dead-letter queue. The message was not stored and cannot be replayed.")]
	private partial void LogDiscardedNoDeadLetterQueue(string messageId);
}
