// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Excalibur health checks and UI.
/// </summary>
public static class HealthChecksServiceCollectionExtensions
{
	private const string DefaultEndpointName = "feedback api";
	private const string DefaultEndpointUri = "/.well-known/ready";

	/// <summary>
	/// Adds Excalibur health checks and UI components to the service collection.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="withHealthChecks"> An optional action to configure additional health checks using an <see cref="IHealthChecksBuilder" />. </param>
	/// <param name="endpointName"> The name of the health check endpoint. Defaults to "feedback api". </param>
	/// <param name="endpointUri"> The URI of the health check endpoint. Defaults to "/.well-known/ready". </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburHealthChecks(
		this IServiceCollection services,
		Action<IHealthChecksBuilder>? withHealthChecks = null,
		string endpointName = DefaultEndpointName,
		string endpointUri = DefaultEndpointUri)
	{
		ArgumentNullException.ThrowIfNull(services);

		var healthChecks = services.AddHealthChecks();

		withHealthChecks?.Invoke(healthChecks);

		_ = services.AddHealthChecksUI(options =>
		{
			_ = options.SetEvaluationTimeInSeconds(10);
			_ = options.MaximumHistoryEntriesPerEndpoint(60);
			_ = options.SetApiMaxActiveRequests(1);
			_ = options.AddHealthCheckEndpoint(endpointName, endpointUri);
		});

		return services;
	}
}
