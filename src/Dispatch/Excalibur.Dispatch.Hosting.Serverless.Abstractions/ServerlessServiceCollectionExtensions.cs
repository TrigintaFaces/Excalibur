// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Hosting.Serverless;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring serverless hosting services.
/// </summary>
public static class ServerlessServiceCollectionExtensions
{
	/// <summary>
	/// Adds serverless hosting services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration action for serverless host options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddServerlessHosting(
		this IServiceCollection services,
		Action<ServerlessHostOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register core services
		_ = services.AddSingleton<IServerlessHostProviderFactory, ServerlessHostProviderFactory>();

		// Configure options if provided
		if (configureOptions != null)
		{
			_ = services.Configure(configureOptions);
		}

		return services;
	}

	/// <summary>
	/// Adds AWS Lambda hosting support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration action for AWS Lambda options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsLambdaHosting(
		this IServiceCollection services,
		Action<AwsLambdaOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AwsLambda;
			if (configureOptions != null)
			{
				configureOptions(options.AwsLambda);
			}
		});

		return services;
	}

	/// <summary>
	/// Adds Azure Functions hosting support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration action for Azure Functions options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAzureFunctionsHosting(
		this IServiceCollection services,
		Action<AzureFunctionsOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AzureFunctions;
			if (configureOptions != null)
			{
				configureOptions(options.AzureFunctions);
			}
		});

		return services;
	}

	/// <summary>
	/// Adds Google Cloud Functions hosting support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration action for Google Cloud Functions options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGoogleCloudFunctionsHosting(
		this IServiceCollection services,
		Action<GoogleCloudFunctionsOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.GoogleCloudFunctions;
			if (configureOptions != null)
			{
				configureOptions(options.GoogleCloudFunctions);
			}
		});

		return services;
	}

	/// <summary>
	/// Adds a custom serverless host provider to the factory.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="provider"> The custom provider to register. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddCustomServerlessProvider(
		this IServiceCollection services,
		IServerlessHostProvider provider)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(provider);

		_ = services.AddSingleton(provider);

		return services;
	}
}
