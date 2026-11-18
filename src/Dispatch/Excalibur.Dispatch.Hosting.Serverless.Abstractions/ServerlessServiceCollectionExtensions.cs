// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Hosting.Serverless;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
		services.TryAddSingleton<IServerlessHostProviderFactory, ServerlessHostProviderFactory>();

		// Fail fast at startup if the options are invalid (parity with the IConfiguration overload).
		// All provider Add* methods (AddAwsLambdaHosting/AddAzureFunctionsHosting/AddGoogleCloudFunctionsHosting)
		// delegate here, so without this they would never validate-on-start.
		_ = services.AddValidatedServerlessHostOptions();

		// Configure options if provided
		if (configureOptions != null)
		{
			_ = services.Configure(configureOptions);
		}

		return services;
	}

	/// <summary>
	/// Adds serverless hosting services to the service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind serverless host options from. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Binding configuration to the options type reflects over its members, which trimming may remove. Configure the options in code instead of binding IConfiguration.")]
	[RequiresDynamicCode("Binding configuration to the options type can require runtime code generation, which native AOT does not support. Configure the options in code instead of binding IConfiguration.")]
	public static IServiceCollection AddServerlessHosting(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Register core services
		services.TryAddSingleton<IServerlessHostProviderFactory, ServerlessHostProviderFactory>();

		// Bind options from configuration
		_ = services.AddValidatedServerlessHostOptions().Bind(configuration);

		return services;
	}

	/// <summary>
	/// Adds AWS Lambda hosting support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsLambdaHosting(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AwsLambda;
		});

		return services;
	}

	/// <summary>
	/// Adds Azure Functions hosting support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAzureFunctionsHosting(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AzureFunctions;
		});

		return services;
	}

	/// <summary>
	/// Adds Google Cloud Functions hosting support to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGoogleCloudFunctionsHosting(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.GoogleCloudFunctions;
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

	/// <summary>Registers shared options validation without adding a provider factory or selecting a platform.</summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The validated options builder for optional configuration binding.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	internal static OptionsBuilder<ServerlessHostOptions> AddValidatedServerlessHostOptions(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ServerlessHostOptions>, ServerlessHostOptionsValidator>());
		return services.AddOptions<ServerlessHostOptions>().ValidateOnStart();
	}
}
