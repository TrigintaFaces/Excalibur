// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;
using Polly.CircuitBreaker;

using ICoreCircuitBreakerPolicy = Excalibur.Dispatch.Resilience.ICircuitBreakerPolicy;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Polly-based circuit breaker policy adapter that wraps Polly's resilience pipeline
/// and implements the core <see cref="ICoreCircuitBreakerPolicy"/> interface.
/// </summary>
/// <remarks>
/// <para>
/// This adapter bridges Polly's circuit breaker capabilities with Dispatch's zero-dependency
/// circuit breaker abstraction. Use this when you want Polly's advanced features like
/// health metrics, telemetry integration, or advanced circuit breaker strategies.
/// </para>
/// <para>
/// For consumers who don't need Polly's advanced features, the core package provides
/// <c>DefaultCircuitBreakerPolicy</c> which has no external dependencies.
/// </para>
/// </remarks>
public sealed partial class PollyCircuitBreakerPolicyAdapter : ICoreCircuitBreakerPolicy, ICircuitBreakerDiagnostics, IDisposable
{
	private readonly ResiliencePipeline _pipeline;
	private readonly CircuitBreakerManualControl _manualControl;
	private readonly ILogger _logger;
	private readonly string _circuitName;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	private CircuitState _currentState = CircuitState.Closed;
	private int _consecutiveFailures;
	private DateTimeOffset? _lastOpenedAt;

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyCircuitBreakerPolicyAdapter"/> class.
	/// </summary>
	/// <param name="options">Circuit breaker configuration options.</param>
	/// <param name="circuitName">Optional name for the circuit breaker (used in logging and events).</param>
	/// <param name="logger">Optional logger instance.</param>
	public PollyCircuitBreakerPolicyAdapter(
		CircuitBreakerOptions options,
		string? circuitName = null,
		ILogger? logger = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		_circuitName = circuitName ?? "default";
		_logger = logger ?? NullLogger.Instance;
		_manualControl = new CircuitBreakerManualControl();

		// Create Polly resilience pipeline with circuit breaker strategy
		_pipeline = new ResiliencePipelineBuilder()
			.AddCircuitBreaker(new CircuitBreakerStrategyOptions
			{
				FailureRatio = 0.5,
				SamplingDuration = TimeSpan.FromSeconds(10),
				MinimumThroughput = options.FailureThreshold,
				BreakDuration = options.OpenDuration,
				ManualControl = _manualControl,
				ShouldHandle = new PredicateBuilder()
					.Handle<Exception>(ex => ex is not (OperationCanceledException or TaskCanceledException)),
				OnOpened = args =>
				{
					lock (_lock)
					{
						var previousState = _currentState;
						_currentState = CircuitState.Open;
						_lastOpenedAt = DateTimeOffset.UtcNow;
						LogCircuitOpened(_circuitName, args.Outcome.Exception);
						RaiseStateChanged(previousState, CircuitState.Open, args.Outcome.Exception);
					}

					return ValueTask.CompletedTask;
				},
				OnClosed = args =>
				{
					lock (_lock)
					{
						var previousState = _currentState;
						_currentState = CircuitState.Closed;
						_consecutiveFailures = 0;
						LogCircuitClosed(_circuitName);
						RaiseStateChanged(previousState, CircuitState.Closed, null);
					}

					return ValueTask.CompletedTask;
				},
				OnHalfOpened = args =>
				{
					lock (_lock)
					{
						var previousState = _currentState;
						_currentState = CircuitState.HalfOpen;
						LogCircuitHalfOpen(_circuitName);
						RaiseStateChanged(previousState, CircuitState.HalfOpen, null);
					}

					return ValueTask.CompletedTask;
				},
			})
			.Build();
	}

	/// <inheritdoc />
	public CircuitState State
	{
		get
		{
			lock (_lock)
			{
				return _currentState;
			}
		}
	}

	/// <inheritdoc />
	public int ConsecutiveFailures
	{
		get
		{
			lock (_lock)
			{
				return _consecutiveFailures;
			}
		}
	}

	/// <inheritdoc />
	public DateTimeOffset? LastOpenedAt
	{
		get
		{
			lock (_lock)
			{
				return _lastOpenedAt;
			}
		}
	}

	/// <inheritdoc />
	public event EventHandler<CircuitStateChangedEventArgs>? StateChanged;

	/// <inheritdoc />
	/// <remarks>
	/// Executes the action through Polly's resilience pipeline with circuit breaker protection.
	/// If the circuit is open, throws <see cref="CircuitBreakerOpenException"/>.
	/// </remarks>
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		try
		{
			var result = await _pipeline.ExecuteAsync(
				async ct => await action(ct).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			RecordSuccess();
			return result;
		}
		catch (BrokenCircuitException ex)
		{
			// Convert Polly's BrokenCircuitException to our CircuitBreakerOpenException
			throw new CircuitBreakerOpenException(
				$"Circuit breaker '{_circuitName}' is open and rejecting calls.",
				ex);
		}
		catch (Exception)
		{
			lock (_lock)
			{
				_consecutiveFailures++;
			}

			throw;
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// Executes the action through Polly's resilience pipeline with circuit breaker protection.
	/// If the circuit is open, throws <see cref="CircuitBreakerOpenException"/>.
	/// </remarks>
	public async Task ExecuteAsync(
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		try
		{
			await _pipeline.ExecuteAsync(
				async ct => await action(ct).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			RecordSuccess();
		}
		catch (BrokenCircuitException ex)
		{
			// Convert Polly's BrokenCircuitException to our CircuitBreakerOpenException
			throw new CircuitBreakerOpenException(
				$"Circuit breaker '{_circuitName}' is open and rejecting calls.",
				ex);
		}
		catch (Exception)
		{
			lock (_lock)
			{
				_consecutiveFailures++;
			}

			throw;
		}
	}

	/// <inheritdoc />
	public void RecordSuccess()
	{
		lock (_lock)
		{
			_consecutiveFailures = 0;
		}
	}

	/// <inheritdoc />
	public void RecordFailure(Exception? exception = null)
	{
		lock (_lock)
		{
			_consecutiveFailures++;
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// This method uses synchronous blocking because the <see cref="ICoreCircuitBreakerPolicy"/> interface
	/// defines <see cref="Reset"/> as synchronous. The underlying Polly 8.x API only provides async
	/// circuit control via <see cref="CircuitBreakerManualControl.CloseAsync"/>.
	/// </remarks>
	[SuppressMessage("AsyncUsage", "VSTHRD002:Avoid problematic synchronous waits",
		Justification = "ICircuitBreakerPolicy.Reset() is synchronous by design, but Polly 8.x only provides async circuit control.")]
	public void Reset()
	{
		// Use manual control to close the circuit
		_manualControl.CloseAsync().GetAwaiter().GetResult();

		lock (_lock)
		{
			var previousState = _currentState;
			_currentState = CircuitState.Closed;
			_consecutiveFailures = 0;

			if (previousState != CircuitState.Closed)
			{
				RaiseStateChanged(previousState, CircuitState.Closed, null);
			}
		}
	}

	/// <summary>
	/// Disposes the circuit breaker policy adapter.
	/// </summary>
	public void Dispose() =>
		// ResiliencePipeline in Polly 8.x doesn't implement IDisposable, so suppression is sufficient.
		GC.SuppressFinalize(this);

	private void RaiseStateChanged(CircuitState previousState, CircuitState newState, Exception? triggeringException)
	{
		StateChanged?.Invoke(this, new CircuitStateChangedEventArgs
		{
			PreviousState = previousState,
			NewState = newState,
			Timestamp = DateTimeOffset.UtcNow,
			CircuitName = _circuitName,
			TriggeringException = triggeringException,
		});
	}

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.CircuitBreakerOpened, LogLevel.Warning,
		"Circuit breaker opened: {CircuitName}")]
	private partial void LogCircuitOpened(string circuitName, Exception? ex);

	[LoggerMessage(ResilienceEventId.CircuitBreakerClosed, LogLevel.Information,
		"Circuit breaker closed: {CircuitName}")]
	private partial void LogCircuitClosed(string circuitName);

	[LoggerMessage(ResilienceEventId.CircuitBreakerHalfOpen, LogLevel.Information,
		"Circuit breaker half-open: {CircuitName}")]
	private partial void LogCircuitHalfOpen(string circuitName);
}
