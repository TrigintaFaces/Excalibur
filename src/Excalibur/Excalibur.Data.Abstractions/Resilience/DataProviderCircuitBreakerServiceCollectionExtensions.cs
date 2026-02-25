// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Resilience;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering data provider circuit breaker services.
/// </summary>
public static class DataProviderCircuitBreakerServiceCollectionExtensions
{
	/// <summary>
	/// Adds a circuit breaker decorator for data provider operations.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for circuit breaker options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDataProviderCircuitBreaker(
		this IServiceCollection services,
		Action<DataProviderCircuitBreakerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DataProviderCircuitBreakerOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddSingleton<CircuitBreakerDataProvider>();

		return services;
	}
}
