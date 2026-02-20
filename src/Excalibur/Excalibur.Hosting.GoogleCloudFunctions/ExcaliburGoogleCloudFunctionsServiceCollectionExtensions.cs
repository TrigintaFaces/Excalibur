// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur Google Cloud Functions serverless hosting services.
/// Composes on top of the Dispatch Google Cloud Functions bridge by calling
/// <see cref="GoogleCloudFunctionsServiceCollectionExtensions.AddGoogleCloudFunctionsServerless(IServiceCollection)"/> first,
/// then registering Excalibur-specific services (saga wiring, event sourcing integration, outbox hooks).
/// </summary>
public static class ExcaliburGoogleCloudFunctionsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur Google Cloud Functions serverless hosting services to the specified service collection.
	/// This registers the Dispatch bridge (host provider, cold-start optimizer) and then layers
	/// Excalibur-specific services on top.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddExcaliburGoogleCloudFunctionsServerless(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Dispatch bridge: registers IServerlessHostProvider, IColdStartOptimizer
		_ = services.AddGoogleCloudFunctionsServerless();

		// Excalibur-specific services (future: saga wiring, ES integration, outbox hooks)

		return services;
	}

	/// <summary>
	/// Adds Excalibur Google Cloud Functions serverless hosting services to the specified service collection with configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configureOptions"> An action to configure the serverless host options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configureOptions is null. </exception>
	public static IServiceCollection AddExcaliburGoogleCloudFunctionsServerless(
		this IServiceCollection services,
		Action<ServerlessHostOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddExcaliburGoogleCloudFunctionsServerless();
		_ = services.Configure(configureOptions);

		return services;
	}
}
