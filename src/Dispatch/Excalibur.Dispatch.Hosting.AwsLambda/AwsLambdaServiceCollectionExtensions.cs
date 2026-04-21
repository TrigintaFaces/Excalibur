// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Hosting.AwsLambda;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AWS Lambda serverless hosting services.
/// </summary>
public static class AwsLambdaServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS Lambda serverless hosting services to the specified service collection.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "AOT-safe: DefaultLambdaJsonSerializer is a TryAdd fallback -- AOT consumers register SourceGeneratorLambdaJsonSerializer first, which takes precedence")]
	public static IServiceCollection AddAwsLambdaServerless(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register AWS Lambda specific services
		services.TryAddSingleton<IServerlessHostProvider, AwsLambdaHostProvider>();
		services.TryAddSingleton<IColdStartOptimizer, AwsLambdaColdStartOptimizer>();
		services.TryAddSingleton<DefaultLambdaJsonSerializer>();

		return services;
	}

	/// <summary>
	/// Adds AWS Lambda serverless hosting services to the specified service collection with configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configureOptions"> An action to configure the serverless host options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configureOptions is null. </exception>
	public static IServiceCollection AddAwsLambdaServerless(
		this IServiceCollection services,
		Action<ServerlessHostOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddAwsLambdaServerless();
		_ = services.AddOptions<ServerlessHostOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds AWS Lambda serverless hosting services to the specified service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configuration"> The configuration section to bind serverless host options from. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	[RequiresUnreferencedCode("IConfiguration binding uses reflection. AOT consumers should use the Action<ServerlessHostOptions> overload instead.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "AOT-safe: IConfiguration.Bind() requires reflection -- see Action<T> overload as AOT alternative")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "AOT-safe: IConfiguration.Bind() requires dynamic code -- see Action<T> overload as AOT alternative")]
	public static IServiceCollection AddAwsLambdaServerless(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddAwsLambdaServerless();
		_ = services.AddOptions<ServerlessHostOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		return services;
	}
}
