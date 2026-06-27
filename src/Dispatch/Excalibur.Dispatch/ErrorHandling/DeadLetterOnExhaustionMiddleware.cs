// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Middleware.Resilience;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Optional opt-in middleware that auto-dead-letters an in-process dispatch once <see cref="RetryMiddleware"/>
/// has exhausted every retry attempt (8o3c3p). It is placed <em>upstream</em> of <see cref="RetryMiddleware"/>
/// so the retry middleware runs as its <c>next</c> delegate; it then observes the returned
/// <see cref="IMessageResult"/> and routes a genuine retry-exhaustion terminal to the dead-letter queue.
/// </summary>
/// <remarks>
/// <para>
/// This decorator <em>composes</em> the retry-exhaustion terminal — it does not re-implement attempt counting
/// or exhaustion detection (<see cref="RetryMiddleware"/> is the single source of truth). It routes
/// <strong>only</strong> the distinct <see cref="RetryProblemTypes.RetryExhausted"/> terminal — never
/// <see cref="RetryProblemTypes.RetryError"/> (a non-retryable exception abandoned immediately) nor a handler's
/// own failed result (a permanent failure before the retry cap), neither of which is an exhaustion.
/// </para>
/// <para>
/// It is intentionally distinct from <see cref="PoisonMessageMiddleware"/> (which owns
/// <see cref="DeadLetterReason.PoisonMessage"/>/<see cref="DeadLetterReason.DeserializationFailed"/>): this
/// decorator owns <see cref="DeadLetterReason.MaxRetriesExceeded"/> only, so the two compose rather than
/// duplicate.
/// </para>
/// <para>
/// The dead-letter write is a best-effort <em>side effect</em>: the original exhausted result always flows up
/// unchanged (the failure stays terminal and visible — never swallowed into a fake success), and a failure of
/// the dead-letter enqueue itself is logged and swallowed (<strong>fail-open</strong>) so an unavailable
/// dead-letter queue can never mask or crash the original exhaustion.
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
	/// — the retry middleware executes within this decorator's <c>next</c> delegate and its exhaustion terminal
	/// is observed in the returned result.
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

		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		// Route ONLY the distinct retry-exhaustion terminal — not RetryError, not a handler's own failed result.
		if (result is { IsSuccess: false }
			&& string.Equals(result.ProblemDetails?.Type, RetryProblemTypes.RetryExhausted, StringComparison.Ordinal))
		{
			var messageId = context.MessageId ?? string.Empty;
			try
			{
				var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["messageId"] = messageId,
				};

				// The terminal sanitizes the live exception into ProblemDetails.Detail (the result no longer
				// carries the Exception object), so we pass exception: null and preserve the sanitized detail
				// for inspection/replay.
				var detail = result.ProblemDetails?.Detail;
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

		// The dead-letter write is a side effect; the original (exhausted) result always flows up unchanged.
		return result;
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
}
