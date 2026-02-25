// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Registry for managing per-transport circuit breaker instances.
/// </summary>
/// <remarks>
/// Each transport (e.g., RabbitMQ, Azure Service Bus, Kafka) gets its own circuit breaker
/// to prevent failures in one transport from affecting others.
/// </remarks>
public sealed class TransportCircuitBreakerRegistry : ITransportCircuitBreakerRegistry, ITransportCircuitBreakerDiagnostics
{
	private readonly ConcurrentDictionary<string, ICircuitBreakerPolicy> _breakers = new(StringComparer.OrdinalIgnoreCase);
	private readonly CircuitBreakerOptions _defaultOptions;
	private readonly ILoggerFactory? _loggerFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportCircuitBreakerRegistry"/> class.
	/// </summary>
	/// <param name="defaultOptions">Default options for circuit breakers when not explicitly configured.</param>
	/// <param name="loggerFactory">Optional logger factory for creating circuit breaker loggers.</param>
	public TransportCircuitBreakerRegistry(
		CircuitBreakerOptions? defaultOptions = null,
		ILoggerFactory? loggerFactory = null)
	{
		_defaultOptions = defaultOptions ?? new CircuitBreakerOptions();
		_loggerFactory = loggerFactory;
	}

	/// <inheritdoc />
	public int Count => _breakers.Count;

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName)
	{
		return GetOrCreate(transportName, _defaultOptions);
	}

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(options);

		return _breakers.GetOrAdd(transportName, name =>
		{
			var logger = _loggerFactory?.CreateLogger<CircuitBreakerPolicy>();
			return new CircuitBreakerPolicy(options, name, logger);
		});
	}

	/// <inheritdoc />
	public ICircuitBreakerPolicy? TryGet(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		return _breakers.TryGetValue(transportName, out var breaker) ? breaker : null;
	}

	/// <inheritdoc />
	public bool Remove(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		return _breakers.TryRemove(transportName, out _);
	}

	/// <inheritdoc />
	public void ResetAll()
	{
		foreach (var breaker in _breakers.Values)
		{
			breaker.Reset();
		}
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<string, CircuitState> GetAllStates()
	{
		return _breakers.ToDictionary(
			kvp => kvp.Key,
			kvp => kvp.Value.State,
			StringComparer.OrdinalIgnoreCase);
	}

	/// <inheritdoc />
	public IEnumerable<string> GetTransportNames()
	{
		return _breakers.Keys.ToList();
	}
}
