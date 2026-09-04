// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
public sealed partial class PollyCircuitBreakerPolicyAdapter : ICoreCircuitBreakerPolicy, ICircuitBreakerEvents, ICircuitBreakerDiagnostics, IDisposable
{
	private readonly ResiliencePipeline _pipeline;
	private readonly CircuitBreakerManualControl _manualControl;
	private readonly ILogger _logger;
	private readonly string _circuitName;
	private readonly Lock _lock = new();

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

		PollyCircuitBreakerConstraints.ThrowIfNotExpressible(options, _circuitName);

		// Create Polly resilience pipeline with circuit breaker strategy
		_pipeline = new ResiliencePipelineBuilder()
			.AddCircuitBreaker(new CircuitBreakerStrategyOptions
			{
				FailureRatio = options.FailureRatio,
				SamplingDuration = options.SamplingDuration,
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
			// Inside the breaker on purpose: the first strategy added is the outermost, so an
			// operation that overruns its budget surfaces as a failure the circuit counts rather
			// than a hang the circuit never sees. CircuitBreakerOptions.OperationTimeout is
			// documented and validated, and until now only an adapter nothing resolved applied it.
			.AddTimeout(options.OperationTimeout)
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

	private void RecordSuccess()
	{
		lock (_lock)
		{
			_consecutiveFailures = 0;
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// <see cref="ICoreCircuitBreakerPolicy.Reset"/> is synchronous while Polly's manual control API is
	/// asynchronous; this method triggers close without blocking.
	/// </remarks>
	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		Func<TResult, bool> isFailure,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(isFailure);

		try
		{
			// Polly's strategy counts exceptions, so a failed RESULT is signalled as one and unwrapped
			// on the way out. The alternative -- inspecting the result after the pipeline returned --
			// would leave the circuit blind to every failure that does not throw.
			return await ExecuteAsync(
				async token =>
				{
					var result = await action(token).ConfigureAwait(false);
					return isFailure(result) ? throw new OutcomeFailureSignal<TResult>(result) : result;
				},
				cancellationToken).ConfigureAwait(false);
		}
		catch (OutcomeFailureSignal<TResult> signal)
		{
			return signal.Result;
		}
	}

	/// <summary>
	/// Carries a failed result through Polly, which counts exceptions rather than outcomes.
	/// </summary>
	/// <typeparam name="T">The result type being carried.</typeparam>
	/// <param name="result">The failed result.</param>
	/// <remarks>
	/// Deliberately private and never observable: it exists only to cross the pipeline boundary and is
	/// unwrapped immediately on the other side, so no caller can catch or depend on it.
	/// </remarks>
#pragma warning disable CA1064 // Not a public exception; it never leaves this type.
	private sealed class OutcomeFailureSignal<T>(T result)
		: Exception("The operation returned a failed result.")
#pragma warning restore CA1064
	{
		/// <summary>Gets the failed result being carried.</summary>
		/// <value>The result the predicate judged to be a failure.</value>
		public T Result { get; } = result;
	}

	/// <inheritdoc />
	public void Reset()
	{
		lock (_lock)
		{
			_consecutiveFailures = 0;
		}

		// ManualControl.CloseAsync is asynchronous and this member is not, so the close is observed
		// rather than discarded. Writing Closed here optimistically would report a circuit that had
		// not closed yet, and would hide a fault in the close entirely; Polly's own OnClosed callback
		// updates the state and raises the transition once the circuit has actually closed.
		_ = ObserveResetAsync();
	}

	private async Task ObserveResetAsync()
	{
		try
		{
			await _manualControl.CloseAsync().ConfigureAwait(false);
		}
		catch (ObjectDisposedException)
		{
			// The adapter was disposed while the close was in flight; nothing left to close.
		}
		catch (Exception ex)
		{
			LogResetFailed(ex);
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

	[LoggerMessage(ResilienceEventId.CircuitBreakerResetFailed, LogLevel.Warning,
		"Circuit breaker reset did not complete; the circuit may still be open")]
	private partial void LogResetFailed(Exception exception);
}
