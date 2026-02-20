// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

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
/// Middleware that implements circuit breaker pattern to prevent cascading failures.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="CircuitBreakerMiddleware" /> class. </remarks>
/// <param name="options"> The circuit breaker options. </param>
/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
/// <param name="logger"> The logger. </param>
[AppliesTo(MessageKinds.All)]
public sealed partial class CircuitBreakerMiddleware(IOptions<CircuitBreakerOptions> options, ITelemetrySanitizer sanitizer, ILogger<CircuitBreakerMiddleware> logger)
	: IDispatchMiddleware
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.CircuitBreakerMiddleware, "1.0.0");

	private readonly CircuitBreakerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
	private readonly ILogger<CircuitBreakerMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	private const int MaxCircuitStates = 1024;

	private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitStates =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.ErrorHandling;

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

		using var activity = ActivitySource.StartActivity("CircuitBreakerMiddleware.Invoke");
		_ = (activity?.SetTag("message.id", context.MessageId ?? string.Empty));
		_ = (activity?.SetTag("message.type", message.GetType().Name));

		var circuitKey = GetCircuitKey(message);

		// Bounded ConcurrentDictionary pattern (cap=1024) — skip caching when full
		CircuitBreakerState state;
		if (_circuitStates.TryGetValue(circuitKey, out var existingState))
		{
			state = existingState;
		}
		else if (_circuitStates.Count >= MaxCircuitStates)
		{
			// Cache is full, create a transient state (not cached)
			state = new CircuitBreakerState(_options);
		}
		else
		{
			state = _circuitStates.GetOrAdd(circuitKey, (_, options) => new CircuitBreakerState(options), _options);
		}

		_ = (activity?.SetTag("circuit.key", circuitKey));
		_ = (activity?.SetTag("circuit.state", state.State.ToString()));

		// Check if circuit is open
		if (state.State == CircuitState.Open)
		{
			if (CreateTimestamp() < state.NextAttemptTime)
			{
				LogCircuitBreakerOpen(circuitKey, context.MessageId ?? string.Empty);

				_ = (activity?.SetTag("circuit.rejected", value: true));
				_ = (activity?.SetStatus(ActivityStatusCode.Error, "Circuit breaker open"));

				return MessageResult.Failed(new MessageProblemDetails
				{
					Type = "CircuitBreakerOpen",
					Title = "Circuit Breaker Open",
					ErrorCode = 503,
					Status = 503,
					Detail = "Circuit breaker is open - request rejected",
					Instance = context.MessageId ?? string.Empty,
				});
			}

			// Move to half-open state
			state.TransitionToHalfOpen();
			LogCircuitBreakerHalfOpen(circuitKey);
			_ = (activity?.SetTag("circuit.transition", "half_open"));
		}

		try
		{
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			if (result.IsSuccess)
			{
				state.RecordSuccess();
				_ = (activity?.SetTag("circuit.success", value: true));

				if (state.State == CircuitState.HalfOpen)
				{
					LogCircuitBreakerClosed(circuitKey);
					_ = (activity?.SetTag("circuit.recovered", value: true));
				}
			}
			else
			{
				state.RecordFailure();
				_ = (activity?.SetTag("circuit.failure", value: true));

				if (state.State == CircuitState.Open)
				{
					LogCircuitBreakerOpenedFailureThreshold(circuitKey);
					_ = (activity?.SetTag("circuit.opened", value: true));
				}
			}

			_ = (activity?.SetStatus(ActivityStatusCode.Ok));
			return result;
		}
		catch (Exception ex)
		{
			state.RecordFailure();
			_ = (activity?.SetTag("circuit.exception", value: true));
			activity?.SetSanitizedErrorStatus(ex, _sanitizer);

			if (state.State == CircuitState.Open)
			{
				LogCircuitBreakerOpenedExceptionThreshold(circuitKey);
				_ = (activity?.SetTag("circuit.opened", value: true));
			}

			LogCircuitBreakerException(circuitKey, context.MessageId ?? string.Empty, ex);

			var sanitizedDetail = ex.GetSanitizedErrorDescription(_sanitizer);
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "CircuitBreakerFailure",
				Title = "Circuit Breaker Failure",
				ErrorCode = 500,
				Status = 500,
				Detail = $"Circuit breaker recorded failure: {sanitizedDetail}",
				Instance = context.MessageId ?? string.Empty,
			});
		}
	}

	private static DateTimeOffset CreateTimestamp() => DateTimeOffset.UtcNow;

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.CircuitBreakerStateOpen, LogLevel.Warning,
		"Circuit breaker is open for {CircuitKey}, rejecting message {MessageId}")]
	private partial void LogCircuitBreakerOpen(string circuitKey, string messageId);

	[LoggerMessage(MiddlewareEventId.CircuitBreakerStateHalfOpen, LogLevel.Information,
		"Circuit breaker transitioning to half-open for {CircuitKey}")]
	private partial void LogCircuitBreakerHalfOpen(string circuitKey);

	[LoggerMessage(MiddlewareEventId.CircuitBreakerStateClosed, LogLevel.Information,
		"Circuit breaker closed for {CircuitKey} after successful recovery")]
	private partial void LogCircuitBreakerClosed(string circuitKey);

	[LoggerMessage(MiddlewareEventId.CircuitBreakerTransition, LogLevel.Warning,
		"Circuit breaker opened for {CircuitKey} due to failure threshold")]
	private partial void LogCircuitBreakerOpenedFailureThreshold(string circuitKey);

	[LoggerMessage(MiddlewareEventId.CircuitBreakerTransition + 4, LogLevel.Warning,
		"Circuit breaker opened for {CircuitKey} due to exception threshold")]
	private partial void LogCircuitBreakerOpenedExceptionThreshold(string circuitKey);

	[LoggerMessage(MiddlewareEventId.CircuitBreakerTransition + 5, LogLevel.Error,
		"Exception in circuit breaker for {CircuitKey}, message {MessageId}")]
	private partial void LogCircuitBreakerException(string circuitKey, string messageId, Exception ex);

	private string GetCircuitKey(IDispatchMessage message) => _options.CircuitKeySelector?.Invoke(message) ?? message.GetType().Name;
}
