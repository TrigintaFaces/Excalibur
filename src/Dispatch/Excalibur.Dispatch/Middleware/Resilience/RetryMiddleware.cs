// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
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
/// <remarks> Initializes a new instance of the <see cref="RetryMiddleware" /> class. </remarks>
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

	// l7m7nr / L5: retry metrics. Static library Meter (ADR-142 lifecycle) mirroring the ActivitySource name.
	private static readonly Meter RetryMeter = new(DispatchTelemetryConstants.Meters.RetryMiddleware, "1.0.0");
	private static readonly Counter<long> RetryAttemptsCounter = RetryMeter.CreateCounter<long>(
		"dispatch.retry.attempts",
		unit: "attempts",
		description: "Number of retry attempts performed (excludes the initial attempt).");
	private static readonly Counter<long> RetryExhaustionsCounter = RetryMeter.CreateCounter<long>(
		"dispatch.retry.exhausted",
		unit: "exhaustions",
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

					// jj9gon/qu3182 (S852): genuine exhaustion via the failed-result path on the final
					// attempt converges on the SINGLE post-loop RetryExhausted terminal (which emits the
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
				// EC-1: cooperative cancellation is never a retry-exhaustion. Propagate it (mirrors
				// DefaultRetryPolicy.IsCancellation) — it must not be retried, and must never increment
				// dispatch.retry.exhausted nor reach the RetryExhausted terminal.
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
				LogNonRetryableException(context.MessageId ?? string.Empty, attempt, ex);
				activity?.SetSanitizedErrorStatus(ex, _sanitizer);
				return MessageResult.Failed(new MessageProblemDetails
				{
					Type = RetryProblemTypes.RetryError,
					Title = "Retry Failed",
					ErrorCode = 500,
					Status = 500,
					Detail = ex.GetSanitizedErrorDescription(_sanitizer),
					Instance = context.MessageId ?? string.Empty,
				});
			}

			// Don't delay after the last attempt
			if (attempt < effectiveOptions.MaxAttempts)
			{
				RetryAttemptsCounter.Add(1);
				var delay = CalculateDelay(effectiveOptions, attempt);
				LogWaitingBeforeRetry(delay.TotalMilliseconds, attempt + 1, context.MessageId ?? string.Empty);

				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}

		// ── Single reachable retry-exhaustion terminal (jj9gon make-reachable; qu3182 both paths) ──
		// Reached ONLY on genuine attempt-cap exhaustion, via EITHER the failed-result path
		// (lastFailedResult set) OR the retryable-exception path (lastException set). Both paths converge
		// here, so dispatch.retry.exhausted is emitted exactly once on EVERY exhaustion code path — no
		// undercount (qu3182) — and the distinct RetryExhausted terminal is now reachable (jj9gon).
		RetryExhaustionsCounter.Add(1);

		var errorMessage = lastException is not null
			? lastException.GetSanitizedErrorDescription(_sanitizer)
			: lastFailedResult?.ProblemDetails?.Detail ?? "All retry attempts exhausted";
		LogRetriesExhausted(context.MessageId ?? string.Empty, effectiveOptions.MaxAttempts, errorMessage);

		_ = (activity?.SetTag("retry.exhausted", value: true));
		_ = (activity?.SetTag("retry.final_attempt", attempt));
		_ = (activity?.SetStatus(ActivityStatusCode.Error, errorMessage));

		if (lastException != null)
		{
			activity?.RecordSanitizedException(lastException, _sanitizer);
		}

		return MessageResult.Failed(new MessageProblemDetails
		{
			Type = RetryProblemTypes.RetryExhausted,
			Title = "Retry Exhausted",
			ErrorCode = 500,
			Status = 500,
			Detail = $"Retry attempts exhausted after {effectiveOptions.MaxAttempts} attempts: {errorMessage}",
			Instance = context.MessageId ?? string.Empty,
		});
	}

	private static double GetSecureRandomDouble()
	{
		Span<byte> bytes = stackalloc byte[8];
		System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
		var value = BitConverter.ToUInt64(bytes);
		return (double)value / ulong.MaxValue;
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
	// on the exhaustion terminal (qu3182/jj9gon) rather than falling through to the non-retryable catch.
	private bool IsExceptionRetryable(RetryOptions options, Exception exception)
	{
		// Check against configured exception filters
		if (options.RetryableExceptions.Count > 0)
		{
			return options.RetryableExceptions.Contains(exception.GetType());
		}

		// Check against non-retryable exceptions
		if (options.NonRetryableExceptions.Contains(exception.GetType()))
		{
			return false;
		}

		// No explicit filter matched: defer to the shared failure classifier (shu41d / S-A) so the
		// retry-vs-dead-letter decision is consistent across every component. Only transient failures
		// are retried; permanent and poison failures (deserialization, validation, argument, auth, …)
		// are abandoned immediately rather than retried to the attempt cap.
		return _classifier.Classify(exception) == MessageFailureKind.Transient;
	}

	private static TimeSpan CalculateDelay(RetryOptions options, int attempt)
	{
		var baseMs = options.BaseDelay.TotalMilliseconds;

		var delayMs = options.BackoffStrategy switch
		{
			BackoffStrategy.Fixed => baseMs,
			BackoffStrategy.Linear => baseMs * attempt,
			// 0yum52: use the configured BackoffMultiplier (default 2.0) rather than a hardcoded 2, so the
			// exponential growth matches the documented option and stays consistent with Outbox/Inbox backoff.
			BackoffStrategy.Exponential => baseMs * Math.Pow(options.BackoffMultiplier, attempt - 1),
			BackoffStrategy.ExponentialWithJitter => CalculateExponentialWithJitterMs(options, baseMs, attempt),
			_ => baseMs,
		};

		// Every backoff strategy funnels its raw millisecond delay through ClampMs, the single seam that
		// constructs the resulting TimeSpan. This makes an uncapped or non-finite delay structurally
		// inexpressible: the cap is applied before TimeSpan.FromMilliseconds, never after.
		return ClampMs(delayMs, options.MaxDelay);
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

	private static double CalculateExponentialWithJitterMs(RetryOptions options, double baseMs, int attempt)
	{
		var exponentialDelay = baseMs * Math.Pow(options.BackoffMultiplier, attempt - 1);
		var jitter = GetSecureRandomDouble() * options.JitterFactor;
		return exponentialDelay * (1 + jitter);
	}
}
