// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Hosting.GoogleCloud;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Google Cloud Functions serverless hosting services.
/// </summary>
public static class GoogleCloudFunctionsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Google Cloud Functions serverless hosting services to the specified service collection.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddGoogleCloudFunctionsServerless(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register Google Cloud Functions specific services
		services.TryAddSingleton<IServerlessHostProvider, GoogleCloudFunctionsHostProvider>();
		services.TryAddSingleton<IColdStartOptimizer, GoogleCloudFunctionsColdStartOptimizer>();

		return services;
	}

	/// <summary>
	/// Adds Google Cloud Functions serverless hosting services to the specified service collection with configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configureOptions"> An action to configure the serverless host options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configureOptions is null. </exception>
	public static IServiceCollection AddGoogleCloudFunctionsServerless(
		this IServiceCollection services,
		Action<ServerlessHostOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddGoogleCloudFunctionsServerless();
		_ = services.Configure(configureOptions);

		return services;
	}
}
