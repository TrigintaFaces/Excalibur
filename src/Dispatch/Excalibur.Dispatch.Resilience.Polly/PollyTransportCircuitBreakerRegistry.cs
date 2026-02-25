// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Polly-based implementation of <see cref="ITransportCircuitBreakerRegistry"/> that manages
/// per-transport circuit breakers using Polly's resilience pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This registry creates and manages <see cref="PollyCircuitBreakerPolicyAdapter"/> instances
/// for each transport, ensuring transport-level isolation of failures. When one transport
/// experiences issues, other transports continue operating normally.
/// </para>
/// <para>
/// The registry is thread-safe and lazily creates circuit breakers on first access.
/// </para>
/// </remarks>
public sealed partial class PollyTransportCircuitBreakerRegistry : ITransportCircuitBreakerRegistry, ITransportCircuitBreakerDiagnostics, IDisposable
{
	private readonly ConcurrentDictionary<string, ICircuitBreakerPolicy> _circuitBreakers = new(StringComparer.OrdinalIgnoreCase);
	private readonly CircuitBreakerOptions _defaultOptions;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyTransportCircuitBreakerRegistry"/> class
	/// with default options.
	/// </summary>
	public PollyTransportCircuitBreakerRegistry()
		: this(new CircuitBreakerOptions(), null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyTransportCircuitBreakerRegistry"/> class.
	/// </summary>
	/// <param name="defaultOptions">The default circuit breaker options for new registrations.</param>
	/// <param name="logger">Optional logger instance.</param>
	public PollyTransportCircuitBreakerRegistry(
		CircuitBreakerOptions defaultOptions,
		ILogger<PollyTransportCircuitBreakerRegistry>? logger)
	{
		_defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
		_logger = logger ?? NullLogger<PollyTransportCircuitBreakerRegistry>.Instance;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyTransportCircuitBreakerRegistry"/> class
	/// using options from dependency injection.
	/// </summary>
	/// <param name="options">The circuit breaker options accessor.</param>
	/// <param name="logger">Optional logger instance.</param>
	public PollyTransportCircuitBreakerRegistry(
		IOptions<CircuitBreakerOptions> options,
		ILogger<PollyTransportCircuitBreakerRegistry>? logger = null)
		: this(options?.Value ?? new CircuitBreakerOptions(), logger)
	{
	}

	/// <inheritdoc />
	public int Count => _circuitBreakers.Count;

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		return _circuitBreakers.GetOrAdd(transportName, name =>
		{
			var circuitBreaker = CreateCircuitBreaker(name, _defaultOptions);
			LogCircuitBreakerCreated(name);
			return circuitBreaker;
		});
	}

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(options);
		ObjectDisposedException.ThrowIf(_disposed, this);

		return _circuitBreakers.GetOrAdd(transportName, name =>
		{
			var circuitBreaker = CreateCircuitBreaker(name, options);
			LogCircuitBreakerCreated(name);
			return circuitBreaker;
		});
	}

	/// <inheritdoc />
	public ICircuitBreakerPolicy? TryGet(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		return _circuitBreakers.TryGetValue(transportName, out var circuitBreaker) ? circuitBreaker : null;
	}

	/// <inheritdoc />
	public bool Remove(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_circuitBreakers.TryRemove(transportName, out var circuitBreaker))
		{
			// Dispose if the circuit breaker implements IDisposable
			(circuitBreaker as IDisposable)?.Dispose();
			LogCircuitBreakerRemoved(transportName);
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public void ResetAll()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var count = 0;
		foreach (var kvp in _circuitBreakers)
		{
			kvp.Value.Reset();
			count++;
		}

		LogAllCircuitBreakersReset(count);
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<string, CircuitState> GetAllStates()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var states = new Dictionary<string, CircuitState>(StringComparer.OrdinalIgnoreCase);
		foreach (var kvp in _circuitBreakers)
		{
			states[kvp.Key] = kvp.Value.State;
		}

		return states;
	}

	/// <inheritdoc />
	public IEnumerable<string> GetTransportNames()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _circuitBreakers.Keys;
	}

	/// <summary>
	/// Disposes all managed circuit breakers and clears the registry.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var kvp in _circuitBreakers)
		{
			(kvp.Value as IDisposable)?.Dispose();
		}

		_circuitBreakers.Clear();
	}

	private PollyCircuitBreakerPolicyAdapter CreateCircuitBreaker(
			string transportName,
			CircuitBreakerOptions options) =>
			new(options, transportName, _logger);

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.TransportCircuitBreakerRegistered, LogLevel.Debug,
		"Created circuit breaker for transport: {TransportName}")]
	private partial void LogCircuitBreakerCreated(string transportName);

	[LoggerMessage(ResilienceEventId.TransportCircuitBreakerUnregistered, LogLevel.Debug,
		"Removed circuit breaker for transport: {TransportName}")]
	private partial void LogCircuitBreakerRemoved(string transportName);

	[LoggerMessage(ResilienceEventId.AllCircuitBreakersReset, LogLevel.Information,
		"Reset {Count} circuit breakers")]
	private partial void LogAllCircuitBreakersReset(int count);
}
