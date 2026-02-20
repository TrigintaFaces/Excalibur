// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Configuration options for cache resilience features including circuit breaker and memory limits.
/// </summary>
public sealed class CacheResilienceOptions
{
	/// <summary>
	/// Gets or sets the circuit breaker configuration for cache operations.
	/// </summary>
	/// <value>Circuit breaker configuration for cache operations.</value>
	public CacheCircuitBreakerOptions CircuitBreaker { get; set; } = new();

	/// <summary>
	/// Gets or sets the type name cache configuration.
	/// </summary>
	/// <value>Type name cache configuration.</value>
	public CacheTypeNameOptions TypeNameCache { get; set; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether to enable fallback to direct routing when cache fails. Default is true.
	/// </summary>
	/// <value><see langword="true"/> if fallback to direct routing should be enabled when cache fails; otherwise, <see langword="false"/>.</value>
	public bool EnableFallback { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to log performance metrics on disposal. Default is true.
	/// </summary>
	/// <value><see langword="true"/> if performance metrics should be logged on disposal; otherwise, <see langword="false"/>.</value>
	public bool LogMetricsOnDisposal { get; set; } = true;
}

/// <summary>
/// Circuit breaker configuration for cache resilience.
/// </summary>
public sealed class CacheCircuitBreakerOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the circuit breaker is enabled. Default is true.
	/// </summary>
	/// <value><see langword="true"/> if the circuit breaker is enabled; otherwise, <see langword="false"/>.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the number of consecutive failures before opening the circuit. Default is 5.
	/// </summary>
	/// <value>The number of consecutive failures before opening the circuit.</value>
	[Range(1, 1000)]
	public int FailureThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets the time window for counting failures. Default is 1 minute.
	/// </summary>
	/// <value>The time window for counting failures.</value>
	public TimeSpan FailureWindow { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets how long the circuit stays open before attempting recovery. Default is 30 seconds.
	/// </summary>
	/// <value>How long the circuit stays open before attempting recovery.</value>
	public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum number of test requests allowed in half-open state. Default is 3.
	/// </summary>
	/// <value>The maximum number of test requests allowed in half-open state.</value>
	[Range(1, 100)]
	public int HalfOpenTestLimit { get; set; } = 3;

	/// <summary>
	/// Gets or sets the number of successful requests needed to close the circuit from half-open. Default is 2.
	/// </summary>
	/// <value>The number of successful requests needed to close the circuit from half-open.</value>
	[Range(1, 100)]
	public int HalfOpenSuccessThreshold { get; set; } = 2;
}

/// <summary>
/// Type name cache configuration for cache resilience.
/// </summary>
public sealed class CacheTypeNameOptions
{
	/// <summary>
	/// Gets or sets the maximum number of type names to cache. Prevents unbounded memory growth. Default is 10,000.
	/// </summary>
	/// <value>The maximum number of type names to cache.</value>
	[Range(100, 1_000_000)]
	public int MaxCacheSize { get; set; } = 10_000;

	/// <summary>
	/// Gets or sets the TTL for cached type names. Default is 1 hour.
	/// </summary>
	/// <value>The TTL for cached type names.</value>
	public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);
}
