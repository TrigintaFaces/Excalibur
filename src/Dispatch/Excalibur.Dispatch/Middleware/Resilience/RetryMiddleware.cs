// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.ExceptionServices;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Middleware that implements retry logic for failed message processing.
/// </summary>
/// <remarks>
/// <para>
/// This middleware decides how many attempts a fault gets, and nothing else about it. A fault it declines
/// to retry is raised after one attempt, and a transient one after the configured attempts — in both cases
/// the original exception propagates with its type and message intact, so an exception mapper or typed
/// exception handler registered above still matches on the exception the handler actually threw.
/// </para>
/// <para>
/// When the downstream returns failed results rather than throwing, exhaustion returns the last of those
/// results unchanged: there is no exception to raise, and the failure the pipeline below produced is what
/// the caller should see.
/// </para>
/// </remarks>
/// <param name="options"> The retry options. </param>
/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
/// <param name="logger"> The logger. </param>
/// <param name="classifier">
/// Optional shared failure classifier used to decide whether an unfiltered exception is retryable.
/// If null, defaults to <see cref="DefaultMessageFailureClassifier"/>.
/// </param>
[AppliesTo(MessageKinds.All)]
public sealed partial class RetryMiddleware(IOptions<RetryOptions> options, ITelemetrySanitizer sanitizer, ILogger<RetryMiddleware> logger, IMessageFailureClassifier? classifier = null) : IDispatchMiddleware
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.RetryMiddleware, "1.0.0");

	// L5: retry metrics. Static library Meter mirroring the ActivitySource name.
	private static readonly Meter RetryMeter = new(DispatchTelemetryConstants.Meters.RetryMiddleware, "1.0.0");
	private static readonly Counter<long> RetryAttemptsCounter = RetryMeter.CreateCounter<long>(
		"dispatch.retry.attempts",
		unit: "{attempts}",
		description: "Number of retry attempts performed (excludes the initial attempt).");
	private static readonly Counter<long> RetryExhaustionsCounter = RetryMeter.CreateCounter<long>(
		"dispatch.retry.exhausted",
		unit: "{exhaustions}",
		description: "Number of times all retry attempts were exhausted, yielding a terminal failure.");

	private const int MaxCachedAttributeOptions = 1024;
	private static readonly ConcurrentDictionary<Type, RetryOptions?> AttributeOptionsCache = new();

	private readonly RetryOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
	private readonly ILogger<RetryMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IMessageFailureClassifier _classifier = classifier ?? new DefaultMessageFailureClassifier();

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.ErrorHandling;

	/// <summary>
	/// Gets the effective retry options for a message type, checking for [Retry] attribute.
	/// </summary>
	/// <param name="messageType">The message type to get options for.</param>
	/// <returns>Message-specific options if [Retry] attribute is present, otherwise global options.</returns>
	private RetryOptions GetEffectiveOptions(Type messageType)
	{
		if (AttributeOptionsCache.TryGetValue(messageType, out var cached))
		{
			return cached ?? _options;
		}

		var attr = messageType.GetCustomAttribute<RetryAttribute>(inherit: true);
		var attributeOptions = attr is null
			? null
			: new RetryOptions
			{
				MaxAttempts = attr.MaxAttempts,
				BaseDelay = TimeSpan.FromMilliseconds(attr.BaseDelayMs),
				MaxDelay = TimeSpan.FromMilliseconds(attr.MaxDelayMs),
				BackoffStrategy = attr.BackoffStrategy,
				JitterFactor = attr.JitterFactor,
				UseJitter = attr.UseJitter
			};

		// Bounded cache: skip caching when full to prevent unbounded memory growth
		if (AttributeOptionsCache.Count < MaxCachedAttributeOptions)
		{
			AttributeOptionsCache.TryAdd(messageType, attributeOptions);
		}

		return attributeOptions ?? _options;
	}

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Get effective options (attribute takes precedence over global options)
		var effectiveOptions = GetEffectiveOptions(message.GetType());

		using var activity = ActivitySource.StartActivity("RetryMiddleware.Invoke");
		_ = (activity?.SetTag("message.id", context.MessageId ?? string.Empty));
		_ = (activity?.SetTag("message.type", message.GetType().Name));
		_ = (activity?.SetTag("retry.max_attempts", effectiveOptions.MaxAttempts));

		var attempt = 0;
		Exception? lastException = null;
		IMessageResult? lastFailedResult = null;

		// One calculator for this retry sequence, not one per attempt: DecorrelatedJitter derives each
		// delay from the previous one, so a fresh calculator each time would restart the ladder. Built
		// on first use rather than up front, because a dispatch that never retries must not pay for --
		// or be failed by -- constructing a ladder it will not walk.
		IBackoffCalculator? backoff = null;

		while (attempt < effectiveOptions.MaxAttempts)
		{
			attempt++;

			try
			{
				using var attemptActivity = ActivitySource.StartActivity($"RetryMiddleware.Attempt.{attempt}");
				_ = (attemptActivity?.SetTag("retry.attempt", attempt));

				LogAttemptingMessage(attempt, effectiveOptions.MaxAttempts, context.MessageId ?? string.Empty);

				var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

				if (result.IsSuccess)
				{
					if (attempt > 1)
					{
						LogMessageSucceeded(context.MessageId ?? string.Empty, attempt);
					}

					_ = (activity?.SetTag("retry.final_attempt", attempt));
					_ = (activity?.SetStatus(ActivityStatusCode.Ok));
					return result;
				}

				// Failed result: retry only while the policy allows it AND attempts remain.
				if (ShouldRetry(effectiveOptions, result, attempt))
				{
					LogMessageFailedWillRetry(context.MessageId ?? string.Empty, attempt);
				}
				else
				{
					LogRetryPolicyDecision(context.MessageId ?? string.Empty, attempt);
					_ = (activity?.SetTag("retry.final_attempt", attempt));
					_ = (activity?.SetTag("retry.abandoned", value: true));

					// Genuine exhaustion via the failed-result path on the final
					// attempt converges on the SINGLE post-loop exhaustion terminal (which emits the
					// exhausted counter once) — no longer returns here.
					if (attempt >= effectiveOptions.MaxAttempts)
					{
						lastFailedResult = result;
						break;
					}

					// Permanent (non-transient) failure before the cap — abandon immediately. This is NOT
					// an exhaustion (no exhausted-count); the handler's own failure result is returned.
					return result;
				}
			}
			catch (OperationCanceledException)
			{
				// cooperative cancellation is never a retry-exhaustion. Propagate it (mirrors
				// DefaultRetryPolicy.IsCancellation) — it must not be retried, and must never increment
				// dispatch.retry.exhausted nor reach the exhaustion terminal.
				throw;
			}
			catch (HandlerNotRegisteredException)
			{
				// A missing handler registration is a configuration fault, not a transient one: it fails identically on every
				// attempt and converting it to a failed result hands the caller a request-shaped error for an operator's omission.
				throw;
			}
			catch (Exception ex) when (IsExceptionRetryable(effectiveOptions, ex))
			{
				// Retryable exception. At the cap this is genuine exhaustion → converge on the single
				// post-loop terminal; otherwise record it and fall through to the backoff delay.
				lastException = ex;
				if (attempt >= effectiveOptions.MaxAttempts)
				{
					break;
				}

				LogExceptionWillRetry(context.MessageId ?? string.Empty, attempt, ex);
			}
			catch (Exception ex)
			{
				// Non-retryable exception → abandon immediately (NOT an exhaustion, no exhausted-count).
				// It PROPAGATES rather than being converted to a failed result: declining to retry is a
				// statement about attempts, not about the fault. Converting it here would hide the original
				// exception type from the mapping and typed-handler middleware above — which match on exactly
				// that type — and hand them a retry-shaped substitute they cannot map.
				LogNonRetryableException(context.MessageId ?? string.Empty, attempt, ex);
				activity?.SetSanitizedErrorStatus(ex, _sanitizer);
				throw;
			}

			// Don't delay after the last attempt
			if (attempt < effectiveOptions.MaxAttempts)
			{
				RetryAttemptsCounter.Add(1, new KeyValuePair<string, object?>("message.type", message.GetType().Name));
				backoff ??= BackoffCalculatorFactory.Create(
					effectiveOptions.BackoffStrategy,
					ToPolicyOptions(effectiveOptions));

				var delay = backoff.CalculateDelay(attempt);
				LogWaitingBeforeRetry(delay.TotalMilliseconds, attempt + 1, context.MessageId ?? string.Empty);

				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}

		// The loop body never ran, which is only possible when MaxAttempts is non-positive: the message was
		// never dispatched. That is a configuration fault, not an exhaustion — no exhausted-count, and raised
		// rather than reported as a message-level failure the caller cannot distinguish from a real one.
		if (attempt == 0)
		{
			throw new InvalidOperationException(
				$"RetryOptions.MaxAttempts must be at least 1, but was {effectiveOptions.MaxAttempts}; the message was never dispatched.");
		}

		// ── Single retry-exhaustion terminal ──
		// Reached ONLY on genuine attempt-cap exhaustion, via EITHER the failed-result path
		// (lastFailedResult set) OR the retryable-exception path (lastException set). Both paths converge
		// here, so dispatch.retry.exhausted is emitted exactly once on EVERY exhaustion code path, BEFORE
		// either path leaves — a counter placed after the rethrow below would be unreachable.
		RetryExhaustionsCounter.Add(1, new KeyValuePair<string, object?>("message.type", message.GetType().Name));

		var errorMessage = lastException is not null
			? lastException.GetSanitizedErrorDescription(_sanitizer)
			: lastFailedResult?.ProblemDetails?.Detail ?? "All retry attempts exhausted";
		LogRetriesExhausted(context.MessageId ?? string.Empty, effectiveOptions.MaxAttempts, errorMessage);

		_ = (activity?.SetTag("retry.exhausted", value: true));
		_ = (activity?.SetTag("retry.final_attempt", attempt));
		_ = (activity?.SetStatus(ActivityStatusCode.Error, errorMessage));

		// Record the exhaustion as a fact on the context BEFORE leaving. The two exhaustion sub-paths below
		// leave by different mechanisms — one throws, one returns — so no returned problem type can carry the
		// fact on both. An upstream decorator (dead-letter-on-exhaustion) reads it either way.
		context.MarkRetryExhausted();

		if (lastException is not null)
		{
			activity?.RecordSanitizedException(lastException, _sanitizer);

			// Rethrow the ORIGINAL exception, never a wrapper. A consumer's exception mapper and typed
			// exception handler match on their own exception type and read its message; wrapping it puts
			// this middleware's type in front of theirs and defeats both.
			ExceptionDispatchInfo.Capture(lastException).Throw();
		}

		// Exhausted by repeatedly returned failures: there is no exception to raise, and the downstream's own
		// failure result is what the caller should see — returning a retry-shaped substitute would replace a
		// result the pipeline below deliberately produced.
		return lastFailedResult!;
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RetryAttemptStarted, LogLevel.Debug,
		"Attempting message processing (attempt {Attempt}/{MaxAttempts}) for message {MessageId}")]
	private partial void LogAttemptingMessage(int attempt, int maxAttempts, string messageId);

	[LoggerMessage(MiddlewareEventId.RetrySucceeded, LogLevel.Information,
		"Message {MessageId} succeeded on attempt {Attempt}")]
	private partial void LogMessageSucceeded(string messageId, int attempt);

	[LoggerMessage(MiddlewareEventId.RetryFailed, LogLevel.Warning,
		"Retry policy determined not to retry message {MessageId} after attempt {Attempt}")]
	private partial void LogRetryPolicyDecision(string messageId, int attempt);

	[LoggerMessage(MiddlewareEventId.RetryFailed + 4, LogLevel.Warning,
		"Message {MessageId} failed on attempt {Attempt}, will retry")]
	private partial void LogMessageFailedWillRetry(string messageId, int attempt);

	[LoggerMessage(MiddlewareEventId.RetryFailed + 5, LogLevel.Warning,
		"Message {MessageId} threw exception on attempt {Attempt}, will retry")]
	private partial void LogExceptionWillRetry(string messageId, int attempt, Exception ex);

	[LoggerMessage(MiddlewareEventId.NonRetryableException, LogLevel.Error,
		"Message {MessageId} threw non-retryable exception on attempt {Attempt}")]
	private partial void LogNonRetryableException(string messageId, int attempt, Exception ex);

	[LoggerMessage(MiddlewareEventId.RetryWaiting, LogLevel.Debug,
		"Waiting {Delay}ms before retry attempt {NextAttempt} for message {MessageId}")]
	private partial void LogWaitingBeforeRetry(double delay, int nextAttempt, string messageId);

	[LoggerMessage(MiddlewareEventId.RetryExhausted, LogLevel.Error,
		"Message {MessageId} failed after {MaxAttempts} attempts. Final error: {Error}")]
	private partial void LogRetriesExhausted(string messageId, int maxAttempts, string error);

	private static bool ShouldRetry(RetryOptions options, IMessageResult result, int attempt)
	{
		if (attempt >= options.MaxAttempts)
		{
			return false;
		}

		// Don't retry successful results
		if (result.IsSuccess)
		{
			return false;
		}

		// Classify the returned failure by its RFC 7807 status code, matching Polly / HttpClientFactory
		// HandleTransientHttpError semantics:
		//  - transient (retry): 5xx server errors, plus 408 Request Timeout and 429 Too Many Requests.
		//  - permanent (no retry): 4xx client errors other than 408/429 — retrying cannot fix them and
		//    risks a non-idempotent re-run.
		//  - unclassified (no ProblemDetails, or no Status): no retry. A deliberately returned failure with
		//    no transient signal is a handler statement that retry won't help; genuine transient faults
		//    surface as exceptions and are handled by ShouldRetryException (which is intentionally untouched).
		var status = result.ProblemDetails?.Status;

		return status is 408 or 429 or (>= 500 and <= 599);
	}

	// Whether an exception is retryable IN PRINCIPLE (independent of the attempt cap). The cap decision
	// lives in the catch body so a retryable exception on the FINAL attempt is caught here and converges
	// on the exhaustion terminal rather than falling through to the non-retryable catch.
	private bool IsExceptionRetryable(RetryOptions options, Exception exception)
	{
		var exceptionType = exception.GetType();

		// The non-retryable floor takes PRECEDENCE over the retryable allowlist and walks the type hierarchy
		// (IsAssignableFrom) rather than an exact-type match: a derived exception whose base is registered
		// non-retryable — e.g. TenantIsolationViolationException, which derives from InvalidOperationException
		// in the default set — is NEVER retried, even when a RetryableExceptions allowlist is configured.
		// Checking the floor FIRST mirrors the AND-composed floor in the Polly adapters
		// (PollyRetryPolicyAdapter/RetryPolicy: !IsNeverRetryable(ex) && …) and Polly's Handle<T>() semantics;
		// retrying a permanent cross-tenant violation only multiplies the isolation exposure.
		foreach (var nonRetryable in options.NonRetryableExceptions)
		{
			if (nonRetryable.IsAssignableFrom(exceptionType))
			{
				return false;
			}
		}

		// Explicit retryable allowlist (if configured): only these types are retried. Reached only after the
		// non-retryable floor above, so the floor can never be bypassed by configuring an allowlist.
		if (options.RetryableExceptions.Count > 0)
		{
			return options.RetryableExceptions.Contains(exceptionType);
		}

		// No explicit filter matched: defer to the shared failure classifier (S-A) so the
		// retry-vs-dead-letter decision is consistent across every component. Only transient failures
		// are retried; permanent and poison failures (deserialization, validation, argument, auth, …)
		// are abandoned immediately rather than retried to the attempt cap.
		return _classifier.Classify(exception) == MessageFailureKind.Transient;
	}

	/// <summary>
	/// Converts a raw delay expressed in milliseconds into a bounded <see cref="TimeSpan" />, guaranteeing
	/// the result is finite and never exceeds <paramref name="maxDelay" />.
	/// </summary>
	/// <param name="milliseconds"> The raw delay in milliseconds, which may have overflowed to a non-finite value. </param>
	/// <param name="maxDelay"> The maximum permitted delay. </param>
	/// <returns> A <see cref="TimeSpan" /> in the range <c>[TimeSpan.Zero, maxDelay]</c>. </returns>
	private static TimeSpan ClampMs(double milliseconds, TimeSpan maxDelay)
	{
		// Exponential growth (Math.Pow) can overflow to PositiveInfinity / NaN before any cap is applied;
		// collapsing that to the cap avoids the OverflowException that TimeSpan.FromMilliseconds would throw
		// on a non-finite input.
		if (!double.IsFinite(milliseconds))
		{
			return maxDelay;
		}

		var capped = Math.Min(milliseconds, maxDelay.TotalMilliseconds);
		return TimeSpan.FromMilliseconds(Math.Max(0d, capped));
	}


	/// <summary>
	/// Projects this middleware's retry options onto the shape the shared backoff calculators read.
	/// </summary>
	/// <param name="options">The middleware's retry options.</param>
	/// <returns>The equivalent policy options.</returns>
	/// <remarks>
	/// Two option types describe one idea, which is a problem of its own; until they are reconciled
	/// this keeps the ladder in one place rather than reimplementing it beside them.
	/// </remarks>
	private static RetryPolicyOptions ToPolicyOptions(RetryOptions options) => new()
	{
		MaxRetryAttempts = options.MaxAttempts,
		Backoff = new RetryBackoffOptions
		{
			BaseDelay = options.BaseDelay,
			MaxDelay = options.MaxDelay,
			BackoffMultiplier = options.BackoffMultiplier,
			JitterFactor = options.JitterFactor,
			EnableJitter = options.UseJitter,
		},
	};
}
