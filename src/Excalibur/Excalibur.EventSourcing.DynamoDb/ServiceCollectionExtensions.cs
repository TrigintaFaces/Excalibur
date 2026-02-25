// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB event store services.
/// </summary>
public static class DynamoDbEventStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the DynamoDB event store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbEventStore(
		this IServiceCollection services,
		Action<DynamoDbEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbEventStoreOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterServices(services);

		return services;
	}

	/// <summary>
	/// Adds the DynamoDB event store to the service collection using configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbEventStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DynamoDbEventStoreOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterServices(services);

		return services;
	}

	/// <summary>
	/// Adds the DynamoDB event store to the service collection using a named configuration section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration.</param>
	/// <param name="sectionName">The configuration section name.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbEventStore(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		_ = services.AddOptions<DynamoDbEventStoreOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterServices(services);

		return services;
	}

	private static void RegisterServices(IServiceCollection services)
	{
		services.TryAddSingleton<DynamoDbEventStore>();
		services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredService<DynamoDbEventStore>());
		services.TryAddSingleton<ICloudNativeEventStore>(sp => sp.GetRequiredService<DynamoDbEventStore>());
	}
}
