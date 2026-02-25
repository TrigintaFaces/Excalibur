// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures resilience patterns for Elasticsearch operations including retries and circuit breakers.
/// </summary>
public sealed class ElasticsearchResilienceOptions
{
	/// <summary>
	/// Gets a value indicating whether resilience features are enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether resilience patterns are active. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the retry policy configuration.
	/// </summary>
	/// <value> The retry policy settings for handling transient failures. </value>
	public RetryPolicyOptions Retry { get; init; } = new();

	/// <summary>
	/// Gets the circuit breaker configuration.
	/// </summary>
	/// <value> The circuit breaker settings for preventing cascading failures. </value>
	public CircuitBreakerOptions CircuitBreaker { get; init; } = new();

	/// <summary>
	/// Gets the timeout configuration for operations.
	/// </summary>
	/// <value> The timeout settings for different operation types. </value>
	public TimeoutOptions Timeouts { get; init; } = new();
}
