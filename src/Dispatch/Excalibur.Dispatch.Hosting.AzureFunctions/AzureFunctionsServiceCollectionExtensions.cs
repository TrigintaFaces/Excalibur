// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Hosting.AzureFunctions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure Functions serverless hosting services.
/// </summary>
public static class AzureFunctionsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Functions serverless hosting services to the specified service collection.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddAzureFunctionsServerless(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register Azure Functions specific services
		services.TryAddSingleton<IServerlessHostProvider, AzureFunctionsHostProvider>();
		services.TryAddSingleton<IColdStartOptimizer, AzureFunctionsColdStartOptimizer>();

		return services;
	}

	/// <summary>
	/// Adds Azure Functions serverless hosting services to the specified service collection with configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configureOptions"> An action to configure the serverless host options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configureOptions is null. </exception>
	public static IServiceCollection AddAzureFunctionsServerless(
		this IServiceCollection services,
		Action<ServerlessHostOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddAzureFunctionsServerless();
		_ = services.Configure(configureOptions);

		return services;
	}

	/// <summary>
	/// Adds Azure Functions serverless hosting services to the specified service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configuration"> The configuration section to bind serverless host options from. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAzureFunctionsServerless(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddAzureFunctionsServerless();
		_ = services.AddOptions<ServerlessHostOptions>().Bind(configuration).ValidateOnStart();

		return services;
	}
}
