// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware that implements retry logic for failed message processing.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RetryMiddleware" /> class. </remarks>
/// <param name="options"> The retry options. </param>
/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
/// <param name="logger"> The logger. </param>
[AppliesTo(MessageKinds.All)]
public sealed partial class RetryMiddleware(IOptions<RetryOptions> options, ITelemetrySanitizer sanitizer, ILogger<RetryMiddleware> logger) : IDispatchMiddleware
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.RetryMiddleware, "1.0.0");
	private const int MaxCachedAttributeOptions = 1024;
	private static readonly ConcurrentDictionary<Type, RetryOptions?> AttributeOptionsCache = new();

	private readonly RetryOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
	private readonly ILogger<RetryMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

				// Check if we should retry based on the result
				if (!ShouldRetry(effectiveOptions, result, attempt))
				{
					LogRetryPolicyDecision(context.MessageId ?? string.Empty, attempt);
					_ = (activity?.SetTag("retry.final_attempt", attempt));
					_ = (activity?.SetTag("retry.abandoned", value: true));
					return result;
				}

				LogMessageFailedWillRetry(context.MessageId ?? string.Empty, attempt);
			}
			catch (Exception ex) when (ShouldRetryException(effectiveOptions, ex, attempt))
			{
				lastException = ex;
				LogExceptionWillRetry(context.MessageId ?? string.Empty, attempt, ex);
			}
			catch (Exception ex)
			{
				LogNonRetryableException(context.MessageId ?? string.Empty, attempt, ex);
				activity?.SetSanitizedErrorStatus(ex, _sanitizer);
				return MessageResult.Failed(new MessageProblemDetails
				{
					Type = "RetryError",
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
				var delay = CalculateDelay(effectiveOptions, attempt);
				LogWaitingBeforeRetry(delay.TotalMilliseconds, attempt + 1, context.MessageId ?? string.Empty);

				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}

		// All retry attempts exhausted
		var errorMessage = lastException is not null
			? lastException.GetSanitizedErrorDescription(_sanitizer)
			: "All retry attempts exhausted";
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
			Type = "RetryExhausted",
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

		// Check if result indicates a retryable failure For now, we retry all failures unless specifically marked as non-retryable
		return true;
	}

	private static bool ShouldRetryException(RetryOptions options, Exception exception, int attempt)
	{
		if (attempt >= options.MaxAttempts)
		{
			return false;
		}

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

		// Default: retry most exceptions except argument and invalid operation
		return exception is not (ArgumentException or ArgumentNullException or InvalidOperationException);
	}

	private static TimeSpan CalculateDelay(RetryOptions options, int attempt)
	{
		var baseDelay = options.BaseDelay;

		var delay = options.BackoffStrategy switch
		{
			BackoffStrategy.Fixed => baseDelay,
			BackoffStrategy.Linear => TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * attempt),
			BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)),
			BackoffStrategy.ExponentialWithJitter => CalculateExponentialWithJitter(options, baseDelay, attempt),
			_ => baseDelay,
		};

		// Apply maximum delay limit
		if (delay > options.MaxDelay)
		{
			delay = options.MaxDelay;
		}

		return delay;
	}

	private static TimeSpan CalculateExponentialWithJitter(RetryOptions options, TimeSpan baseDelay, int attempt)
	{
		var exponentialDelay = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
		var jitter = GetSecureRandomDouble() * options.JitterFactor;
		var jitteredDelay = exponentialDelay * (1 + jitter);
		return TimeSpan.FromMilliseconds(jitteredDelay);
	}
}
