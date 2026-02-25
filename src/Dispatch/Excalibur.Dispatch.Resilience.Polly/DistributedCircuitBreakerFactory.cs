// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Factory for creating distributed circuit breakers.
/// </summary>
/// <param name="cache">The distributed cache used for state coordination.</param>
/// <param name="options">The distributed circuit breaker options.</param>
/// <param name="loggerFactory">The logger factory used to create circuit breaker loggers.</param>
public sealed class DistributedCircuitBreakerFactory(
	IDistributedCache cache,
	IOptions<DistributedCircuitBreakerOptions> options,
	ILoggerFactory loggerFactory)
{
	private readonly IDistributedCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
	private readonly IOptions<DistributedCircuitBreakerOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

	private readonly ConcurrentDictionary<string, IDistributedCircuitBreaker> _breakers =
		new(StringComparer.Ordinal);

	/// <summary>
	/// Gets or creates a distributed circuit breaker.
	/// </summary>
	/// <param name="name">The name of the circuit breaker to retrieve.</param>
	/// <returns>The existing or newly created distributed circuit breaker.</returns>
	public IDistributedCircuitBreaker GetOrCreate(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		return _breakers.GetOrAdd(
			name,
			static (key, state) => new DistributedCircuitBreaker(
				key,
				state.cache,
				state.options,
				state.loggerFactory.CreateLogger<DistributedCircuitBreaker>()),
			(cache: _cache, options: _options, loggerFactory: _loggerFactory));
	}
}
