// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon.DynamoDBv2;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.DynamoDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB services.
/// </summary>
public static class DynamoDbServiceCollectionExtensions

{
	/// <summary>
	/// Adds AWS DynamoDB data provider to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> The configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDynamoDb(
		this IServiceCollection services,
		Action<DynamoDbOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds AWS DynamoDB data provider to the service collection using configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddDynamoDb(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DynamoDbOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds AWS DynamoDB data provider to the service collection using a named configuration section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration. </param>
	/// <param name="sectionName"> The configuration section name. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddDynamoDb(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		_ = services.AddOptions<DynamoDbOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds AWS DynamoDB data provider with an existing DynamoDB client.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> The configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDynamoDbWithClient(
		this IServiceCollection services,
		Action<DynamoDbOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register the provider using the IAmazonDynamoDB from DI
		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<IAmazonDynamoDB>();
			var options = sp.GetRequiredService<Options.IOptions<DynamoDbOptions>>();
			var logger = sp.GetRequiredService<Logging.ILogger<DynamoDbPersistenceProvider>>();
			return new DynamoDbPersistenceProvider(client, options, logger);
		});

		services.TryAddSingleton<ICloudNativePersistenceProvider>(sp =>
			sp.GetRequiredService<DynamoDbPersistenceProvider>());

		services.TryAddSingleton<DynamoDbHealthCheck>();

		return services;
	}

	private static void RegisterCoreServices(IServiceCollection services)
	{
		services.TryAddSingleton<DynamoDbPersistenceProvider>();
		services.TryAddSingleton<ICloudNativePersistenceProvider>(sp =>
			sp.GetRequiredService<DynamoDbPersistenceProvider>());

		// Register health check
		services.TryAddSingleton<DynamoDbHealthCheck>();
	}
}
