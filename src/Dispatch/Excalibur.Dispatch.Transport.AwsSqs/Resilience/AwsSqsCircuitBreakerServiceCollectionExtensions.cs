// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS SQS circuit breaker integration with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Integrates circuit breaker protection with AWS SQS operations. When the
/// failure threshold is exceeded within the sampling duration, subsequent
/// operations fail fast without calling the AWS SDK, reducing load on
/// degraded downstream services.
/// </para>
/// <para>
/// The circuit breaker integrates with the existing <c>IDistributedCircuitBreaker</c>
/// from <c>Excalibur.Dispatch.Resilience.Polly</c> for distributed state coordination.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsCircuitBreaker(options =>
/// {
///     options.FailureThreshold = 5;
///     options.BreakDuration = TimeSpan.FromSeconds(30);
///     options.SamplingDuration = TimeSpan.FromSeconds(60);
/// });
/// </code>
/// </example>
public static class AwsSqsCircuitBreakerServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS SQS circuit breaker integration with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure circuit breaker options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="AwsSqsCircuitBreakerOptions"/> in the DI container with data annotation
	/// validation and startup validation. Requires <c>IDistributedCircuitBreaker</c> to be
	/// registered separately (e.g., via <c>AddDistributedCircuitBreaker</c> from Resilience.Polly).
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAwsSqsCircuitBreaker(
		this IServiceCollection services,
		Action<AwsSqsCircuitBreakerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<AwsSqsCircuitBreakerOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}
