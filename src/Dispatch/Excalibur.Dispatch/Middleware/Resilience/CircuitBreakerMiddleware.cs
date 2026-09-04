// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

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
/// Middleware that implements circuit breaker pattern to prevent cascading failures.
/// </summary>
/// <remarks>
/// A fault from downstream is recorded against the circuit and then propagates unchanged, so an exception
/// mapper or typed exception handler registered above still sees the original exception. The one failure
/// this middleware produces itself is the rejection it returns while the circuit is open, which is its own
/// outcome rather than a restatement of somebody else's fault.
/// </remarks>
/// <param name="options"> The circuit breaker options. </param>
/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
/// <param name="registry">
/// Shared per-key circuit registry, so this middleware and the outbox and inbox drains see one
/// circuit per dependency rather than three private ones.
/// </param>
/// <param name="logger"> The logger. </param>
[AppliesTo(MessageKinds.All)]
public sealed partial class CircuitBreakerMiddleware(IOptions<CircuitBreakerOptions> options, ITransportCircuitBreakerRegistry registry, ITelemetrySanitizer sanitizer, ILogger<CircuitBreakerMiddleware> logger)
	: IDispatchMiddleware
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.CircuitBreakerMiddleware, "1.0.0");

	// Static library Meter — process-lifetime, fixed name mirroring ActivitySource.
	private static readonly Meter Meter = new(DispatchTelemetryConstants.Meters.CircuitBreakerMiddleware, "1.0.0");

	private static readonly Counter<long> TransitionsCounter = Meter.CreateCounter<long>(
		"dispatch.circuit_breaker.transitions",
		unit: "{transitions}",
		description: "Number of circuit breaker state transitions, tagged with circuit.key, from_state and to_state.");

	private static readonly Counter<long> RejectionsCounter = Meter.CreateCounter<long>(
		"dispatch.circuit_breaker.rejections",
		unit: "{rejections}",
		description: "Number of requests rejected because the circuit breaker was open, tagged with circuit.key.");

	private readonly CircuitBreakerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
	private readonly ITransportCircuitBreakerRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
	private readonly ILogger<CircuitBreakerMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));


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

		// One breaker per key, shared through the registry, so the outbox, inbox and this
		// middleware all see the same circuit for the same dependency instead of three private ones.
		// The registry owns the bound on distinct keys (the key can be message-derived here).
		var breaker = _registry.GetOrCreate(circuitKey, _options);

		_ = (activity?.SetTag("circuit.key", circuitKey));
		_ = (activity?.SetTag("circuit.state", breaker.State.ToString()));

		// Check if circuit is open
		try
		{
			// The breaker owns the decision and the state machine. A returned failure counts as a
			// failure too -- a pipeline that answers "failed" without throwing would otherwise look
			// healthy to the circuit forever.
			return await breaker.ExecuteAsync(
				async ct => await nextDelegate(message, context, ct).ConfigureAwait(false),
				static result => !result.IsSuccess,
				cancellationToken).ConfigureAwait(false);
		}
		catch (CircuitBreakerOpenException)
		{
			LogCircuitBreakerOpen(circuitKey, context.MessageId ?? string.Empty);

			_ = (activity?.SetTag("circuit.rejected", value: true));
			_ = (activity?.SetStatus(ActivityStatusCode.Error, "Circuit breaker open"));

			RejectionsCounter.Add(1, new KeyValuePair<string, object?>("circuit.key", circuitKey));

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
		catch (HandlerNotRegisteredException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_ = (activity?.SetTag("circuit.exception", value: true));
			activity?.SetSanitizedErrorStatus(ex, _sanitizer);
			LogCircuitBreakerException(circuitKey, context.MessageId ?? string.Empty, ex);

			throw;
		}
	}


	/// <summary>
	/// Emits the circuit-breaker transition counter when the state actually changed, tagged
	/// with <c>circuit.key</c>, <c>from_state</c> and <c>to_state</c>. Additive to the existing logs/Activity
	/// tags. A no-op when <paramref name="from"/> equals <paramref name="to"/> (no real transition).
	/// </summary>
	private static void EmitTransition(string circuitKey, CircuitState from, CircuitState to)
	{
		if (from == to)
		{
			return;
		}

		TransitionsCounter.Add(
			1,
			new KeyValuePair<string, object?>("circuit.key", circuitKey),
			new KeyValuePair<string, object?>("from_state", ToStateTag(from)),
			new KeyValuePair<string, object?>("to_state", ToStateTag(to)));
	}

	private static string ToStateTag(CircuitState state) => state switch
	{
		CircuitState.Closed => "closed",
		CircuitState.Open => "open",
		CircuitState.HalfOpen => "half_open",
		_ => "unknown",
	};

	// Source-generated logging methods
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
