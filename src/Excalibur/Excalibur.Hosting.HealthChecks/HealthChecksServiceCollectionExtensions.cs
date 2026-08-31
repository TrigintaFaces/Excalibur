// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Excalibur health checks.
/// </summary>
public static class HealthChecksServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur health checks to the service collection.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="withHealthChecks"> An optional action to configure additional health checks using an <see cref="IHealthChecksBuilder" />. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburHealthChecks(
		this IServiceCollection services,
		Action<IHealthChecksBuilder>? withHealthChecks = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var healthChecks = services.AddHealthChecks();

		withHealthChecks?.Invoke(healthChecks);

		return services;
	}
}
