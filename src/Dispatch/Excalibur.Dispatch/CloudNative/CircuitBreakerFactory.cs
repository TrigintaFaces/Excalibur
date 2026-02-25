// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Collections.Concurrent;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Default implementation of circuit breaker factory.
/// </summary>
/// <param name="defaultOptions">The default circuit breaker options to apply when none are specified.</param>
/// <param name="logger">The logger used for circuit breaker lifecycle events.</param>
public sealed partial class CircuitBreakerFactory(
	CircuitBreakerOptions? defaultOptions = null,
	ILogger<CircuitBreakerFactory>? logger = null) : ICircuitBreakerFactory, IAsyncDisposable
{
	private readonly ConcurrentDictionary<string, CircuitBreakerPattern> _circuitBreakers = new(StringComparer.Ordinal);
	private readonly CircuitBreakerOptions _defaultOptions = defaultOptions ?? new CircuitBreakerOptions();
	private readonly ILogger<CircuitBreakerFactory> _logger = logger ?? NullLogger<CircuitBreakerFactory>.Instance;

	/// <summary>
	/// Gets an existing circuit breaker or creates a new one with the specified name and options.
	/// </summary>
	/// <param name="name">The name of the circuit breaker.</param>
	/// <param name="options">The circuit breaker options, or default options if not specified.</param>
	/// <returns>A circuit breaker instance.</returns>
	public CircuitBreakerPattern GetOrCreate(string name, CircuitBreakerOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(name);

		return _circuitBreakers.GetOrAdd(
			name,
			(key, state) =>
			{
				var effectiveOptions = state.Options ?? state.Self._defaultOptions;
				var breaker = new CircuitBreakerPattern(key, effectiveOptions, state.Self._logger);
				LogCircuitBreakerCreated(state.Self._logger, key);
				return breaker;
			},
			(Self: this, Options: options));
	}

	/// <summary>
	/// Gets metrics for all registered circuit breakers.
	/// </summary>
	/// <returns>A dictionary containing circuit breaker names and their associated metrics.</returns>
	public Dictionary<string, CircuitBreakerMetrics> GetAllMetrics() =>
		_circuitBreakers.ToDictionary(
			static kvp => kvp.Key,
			static kvp => kvp.Value.GetCircuitBreakerMetrics(),
			StringComparer.Ordinal);

	/// <summary>
	/// Removes a circuit breaker from the factory and disposes it.
	/// </summary>
	/// <param name="name">The name of the circuit breaker to remove.</param>
	/// <returns>True if the circuit breaker was removed; otherwise, false.</returns>
	public bool Remove(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		if (_circuitBreakers.TryRemove(name, out var circuitBreaker))
		{
			// Note: ValueTask disposal is fire-and-forget to avoid blocking the caller. This is acceptable for cleanup operations.
			// R0.8: Use ValueTasks correctly - intentionally fire-and-forget for cleanup.
#pragma warning disable CA2012
			_ = circuitBreaker.DisposeAsync();
#pragma warning restore CA2012
			LogCircuitBreakerRemoved(_logger, name);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Disposes all circuit breakers and clears the factory.
	/// </summary>
	/// <returns>A task representing the asynchronous disposal operation.</returns>
	public async ValueTask DisposeAsync()
	{
		var disposeTasks = _circuitBreakers.Values.Select(static cb => cb.DisposeAsync().AsTask());
		await Task.WhenAll(disposeTasks).ConfigureAwait(false);

		_circuitBreakers.Clear();
		GC.SuppressFinalize(this);
	}

	#region LoggerMessage Definitions

	[LoggerMessage(CoreEventId.CircuitBreakerCreated, LogLevel.Information, "Created circuit breaker {Name}")]
	private static partial void LogCircuitBreakerCreated(ILogger logger, string name);

	[LoggerMessage(CoreEventId.CircuitBreakerRemoved, LogLevel.Information, "Removed circuit breaker {Name}")]
	private static partial void LogCircuitBreakerRemoved(ILogger logger, string name);

	#endregion
}
