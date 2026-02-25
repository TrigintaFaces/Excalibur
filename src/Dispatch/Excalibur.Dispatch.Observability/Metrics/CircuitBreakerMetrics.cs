// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Provides centralized metrics collection for Circuit Breaker operations.
/// </summary>
public sealed class CircuitBreakerMetrics : ICircuitBreakerMetrics, IDisposable
{
	/// <summary>
	/// The meter name for Circuit Breaker metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.CircuitBreaker";

	private readonly ConcurrentDictionary<string, int> _circuitStates = new();
	private readonly TagCardinalityGuard _circuitNameGuard = new();
	private readonly TagCardinalityGuard _exceptionTypeGuard = new();
	private Counter<long> _stateChanges = null!;
	private Counter<long> _rejections = null!;
	private Counter<long> _failures = null!;
	private Counter<long> _successes = null!;

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerMetrics"/> class.
	/// </summary>
	public CircuitBreakerMetrics()
	{
		Meter = new Meter(MeterName);
		InitializeInstruments();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerMetrics"/> class using an <see cref="IMeterFactory"/>.
	/// </summary>
	/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
	public CircuitBreakerMetrics(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);
		Meter = meterFactory.Create(MeterName);
		InitializeInstruments();
	}

	/// <inheritdoc />
	public Meter Meter { get; }

	/// <inheritdoc />
	public void RecordStateChange(string circuitName, string previousState, string newState)
	{
		_stateChanges.Add(1,
			new KeyValuePair<string, object?>("circuit_name", _circuitNameGuard.Guard(circuitName)),
			new KeyValuePair<string, object?>("previous_state", previousState),
			new KeyValuePair<string, object?>("new_state", newState));
	}

	/// <inheritdoc />
	public void RecordRejection(string circuitName)
	{
		_rejections.Add(1,
			new KeyValuePair<string, object?>("circuit_name", _circuitNameGuard.Guard(circuitName)));
	}

	/// <inheritdoc />
	public void UpdateState(string circuitName, int state)
	{
		_circuitStates[_circuitNameGuard.Guard(circuitName)] = state;
	}

	/// <inheritdoc />
	public void RecordFailure(string circuitName, string exceptionType)
	{
		_failures.Add(1,
			new KeyValuePair<string, object?>("circuit_name", _circuitNameGuard.Guard(circuitName)),
			new KeyValuePair<string, object?>("exception_type", _exceptionTypeGuard.Guard(exceptionType)));
	}

	/// <inheritdoc />
	public void RecordSuccess(string circuitName)
	{
		_successes.Add(1,
			new KeyValuePair<string, object?>("circuit_name", _circuitNameGuard.Guard(circuitName)));
	}

	/// <inheritdoc />
	public void Dispose() => Meter.Dispose();

	private void InitializeInstruments()
	{
		_stateChanges = Meter.CreateCounter<long>(
			"dispatch.circuitbreaker.state_changes",
			"count",
			"Total number of circuit breaker state transitions");

		_rejections = Meter.CreateCounter<long>(
			"dispatch.circuitbreaker.rejections",
			"count",
			"Total number of operations rejected due to open circuit");

		_failures = Meter.CreateCounter<long>(
			"dispatch.circuitbreaker.failures",
			"count",
			"Total number of failures recorded by circuit breakers");

		_successes = Meter.CreateCounter<long>(
			"dispatch.circuitbreaker.successes",
			"count",
			"Total number of successful operations through circuit breakers");

		// Create observable gauge that reports current state for each circuit
		_ = Meter.CreateObservableGauge(
			"dispatch.circuitbreaker.state",
			observeValues: GetCircuitStates,
			unit: "state",
			description: "Current state of circuit breakers (0=Closed, 1=Open, 2=HalfOpen)");
	}

	private IEnumerable<Measurement<int>> GetCircuitStates()
	{
		foreach (var kvp in _circuitStates)
		{
			yield return new Measurement<int>(
				kvp.Value,
				new KeyValuePair<string, object?>("circuit_name", kvp.Key));
		}
	}
}
