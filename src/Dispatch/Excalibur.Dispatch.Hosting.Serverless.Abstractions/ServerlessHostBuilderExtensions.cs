// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Hosting.Serverless;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring serverless hosting with host builders.
/// </summary>
public static class ServerlessHostBuilderExtensions
{
	/// <summary>
	/// Configures the host builder for serverless execution.
	/// </summary>
	/// <param name="hostBuilder"> The host builder. </param>
	/// <param name="configureOptions"> Optional configuration action for serverless host options. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder UseServerlessHosting(
		this IHostBuilder hostBuilder,
		Action<ServerlessHostOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);

		// Extract configuration values BEFORE creating lambdas to avoid capturing
		// the hostBuilder which may be disposed by the time the factory lambda executes.
		var eagerOptions = new ServerlessHostOptions();
		configureOptions?.Invoke(eagerOptions);

		// Apply host-level configuration immediately while hostBuilder is still valid
		return hostBuilder.ConfigureServices((_, services) =>
		{
			// Add serverless hosting services
			services.AddServerlessHosting(configureOptions);

			// Capture the extracted options, not the hostBuilder
			var capturedOptions = eagerOptions;

			// Configure the appropriate provider based on detected platform
			services.AddSingleton<IHostedService>(sp =>
			{
				var factory = sp.GetRequiredService<IServerlessHostProviderFactory>();
				var logger = sp.GetRequiredService<ILogger<ServerlessHostingService>>();

				var provider = factory.CreateProvider(capturedOptions.PreferredPlatform);

				return new ServerlessHostingService(provider, logger);
			});
		});
	}

	/// <summary>
	/// Configures the host builder for serverless execution
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="hostBuilder"> The host builder. </param>
	/// <param name="configuration"> The configuration section to bind serverless host options from. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder UseServerlessHosting(
		this IHostBuilder hostBuilder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);
		ArgumentNullException.ThrowIfNull(configuration);

		// Bind options eagerly to determine preferred platform
		var eagerOptions = new ServerlessHostOptions();
		configuration.Bind(eagerOptions);

		return hostBuilder.ConfigureServices((_, services) =>
		{
			// Add serverless hosting services with IConfiguration binding
			services.AddServerlessHosting(configuration);

			// Capture the extracted options, not the hostBuilder
			var capturedOptions = eagerOptions;

			// Configure the appropriate provider based on detected platform
			services.AddSingleton<IHostedService>(sp =>
			{
				var factory = sp.GetRequiredService<IServerlessHostProviderFactory>();
				var logger = sp.GetRequiredService<ILogger<ServerlessHostingService>>();

				var provider = factory.CreateProvider(capturedOptions.PreferredPlatform);

				return new ServerlessHostingService(provider, logger);
			});
		});
	}

	/// <summary>
	/// Configures the host builder specifically for AWS Lambda.
	/// </summary>
	/// <param name="hostBuilder"> The host builder. </param>
	/// <param name="configureOptions"> Optional configuration action for AWS Lambda options. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder UseAwsLambda(
		this IHostBuilder hostBuilder,
		Action<AwsLambdaOptions>? configureOptions = null) =>
		hostBuilder.UseServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AwsLambda;
			configureOptions?.Invoke(options.AwsLambda);
		});

	/// <summary>
	/// Configures the host builder specifically for Azure Functions.
	/// </summary>
	/// <param name="hostBuilder"> The host builder. </param>
	/// <param name="configureOptions"> Optional configuration action for Azure Functions options. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder UseAzureFunctions(
		this IHostBuilder hostBuilder,
		Action<AzureFunctionsOptions>? configureOptions = null) =>
		hostBuilder.UseServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AzureFunctions;
			configureOptions?.Invoke(options.AzureFunctions);
		});

	/// <summary>
	/// Configures the host builder specifically for Google Cloud Functions.
	/// </summary>
	/// <param name="hostBuilder"> The host builder. </param>
	/// <param name="configureOptions"> Optional configuration action for Google Cloud Functions options. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder UseGoogleCloudFunctions(
		this IHostBuilder hostBuilder,
		Action<GoogleCloudFunctionsOptions>? configureOptions = null) =>
		hostBuilder.UseServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.GoogleCloudFunctions;
			configureOptions?.Invoke(options.GoogleCloudFunctions);
		});
}
