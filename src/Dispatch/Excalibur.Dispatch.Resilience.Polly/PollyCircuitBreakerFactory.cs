// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Factory for creating Polly-based circuit breaker instances.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PollyCircuitBreakerFactory" /> class. </remarks>
/// <param name="defaultOptions"> Default circuit breaker options. </param>
/// <param name="logger"> Optional logger instance. </param>
public partial class PollyCircuitBreakerFactory(CircuitBreakerOptions? defaultOptions = null, ILogger<PollyCircuitBreakerFactory>? logger = null)
	: ICircuitBreakerFactory, IAsyncDisposable
{
	private readonly ConcurrentDictionary<string, PollyCircuitBreakerAdapter> _circuitBreakers = new(StringComparer.Ordinal);
	private readonly CircuitBreakerOptions _defaultOptions = defaultOptions ?? new CircuitBreakerOptions();
	private readonly ILogger<PollyCircuitBreakerFactory> _logger = logger ?? NullLogger<PollyCircuitBreakerFactory>.Instance;

	/// <inheritdoc />
	public CircuitBreakerPattern GetOrCreate(string name, CircuitBreakerOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(name);

		var effectiveOptions = options ?? _defaultOptions;

		var adapter = _circuitBreakers.GetOrAdd(
			name,
			static (key, state) =>
			{
				var circuitBreaker = new PollyCircuitBreakerAdapter(
					key,
					state.options,
					state.logger);

				state.factory.LogCircuitBreakerCreated(key);
				return circuitBreaker;
			},
			(options: effectiveOptions, logger: _logger, factory: this));

		// Return a wrapper that implements CircuitBreakerPattern interface
		return new PollyCircuitBreakerWrapper(adapter);
	}

	/// <inheritdoc />
	public Dictionary<string, CircuitBreakerMetrics> GetAllMetrics() =>
		_circuitBreakers.ToDictionary(
			static kvp => kvp.Key,
			static kvp => kvp.Value.GetCircuitBreakerMetrics(),
			StringComparer.Ordinal);

	/// <inheritdoc />
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Circuit breaker disposal is intentionally fire-and-forget to avoid blocking Remove operation")]
	public bool Remove(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		if (_circuitBreakers.TryRemove(name, out var circuitBreaker))
		{
			_ = Task.Run(() => circuitBreaker.DisposeAsync().AsTask());
			LogCircuitBreakerRemoved(name);
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		var disposeTasks = _circuitBreakers.Values.Select(static cb => cb.DisposeAsync().AsTask());
		await Task.WhenAll(disposeTasks).ConfigureAwait(false);

		_circuitBreakers.Clear();
		GC.SuppressFinalize(this);
	}

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.PollyCircuitBreakerCreated, LogLevel.Debug,
		"Created Polly circuit breaker {Name}")]
	private partial void LogCircuitBreakerCreated(string name);

	[LoggerMessage(ResilienceEventId.PollyCircuitBreakerRemoved, LogLevel.Debug,
		"Removed Polly circuit breaker {Name}")]
	private partial void LogCircuitBreakerRemoved(string name);
}
