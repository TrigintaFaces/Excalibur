// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.DynamoDb.Cdc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB CDC services.
/// </summary>
public static class DynamoDbCdcServiceCollectionExtensions
{
	/// <summary>
	/// Adds DynamoDB CDC processor services with the specified options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure CDC options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="IDynamoDbCdcProcessor"/> with the
	/// <see cref="DynamoDbCdcProcessor"/> implementation.
	/// </para>
	/// <para>
	/// Requires <c>IAmazonDynamoDB</c> and <c>IAmazonDynamoDBStreams</c> clients
	/// to be registered in the service collection.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDynamoDbCdc(
		this IServiceCollection services,
		Action<DynamoDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbCdcOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IDynamoDbCdcProcessor, DynamoDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds DynamoDB CDC processor services to the service collection using configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="IDynamoDbCdcProcessor"/> with the
	/// <see cref="DynamoDbCdcProcessor"/> implementation.
	/// </para>
	/// <para>
	/// Requires <c>IAmazonDynamoDB</c> and <c>IAmazonDynamoDBStreams</c> clients
	/// to be registered in the service collection.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddDynamoDbCdc(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DynamoDbCdcOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IDynamoDbCdcProcessor, DynamoDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds DynamoDB CDC processor services to the service collection using a named configuration section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration.</param>
	/// <param name="sectionName">The configuration section name.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="IDynamoDbCdcProcessor"/> with the
	/// <see cref="DynamoDbCdcProcessor"/> implementation.
	/// </para>
	/// <para>
	/// Requires <c>IAmazonDynamoDB</c> and <c>IAmazonDynamoDBStreams</c> clients
	/// to be registered in the service collection.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddDynamoDbCdc(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		_ = services.AddOptions<DynamoDbCdcOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IDynamoDbCdcProcessor, DynamoDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds a DynamoDB-backed state store for CDC position tracking.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The table should have a primary key 'pk' (string) for the processor name.
	/// </para>
	/// <para>
	/// Requires <c>IAmazonDynamoDB</c> to be registered in the service collection.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDynamoDbCdcStateStore(
		this IServiceCollection services,
		Action<DynamoDbCdcStateStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterCdcStateStoreOptions(services, configure);
		services.TryAddSingleton<IDynamoDbCdcStateStore, DynamoDbCdcStateStore>();

		return services;
	}

	/// <summary>
	/// Adds a DynamoDB-backed state store for CDC position tracking with the specified table name.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="tableName">The DynamoDB table name for state storage.</param>
	/// <param name="configure">Optional action to configure additional CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The table should have a primary key 'pk' (string) for the processor name.
	/// </para>
	/// <para>
	/// Requires <c>IAmazonDynamoDB</c> to be registered in the service collection.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDynamoDbCdcStateStore(
		this IServiceCollection services,
		string tableName,
		Action<DynamoDbCdcStateStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		RegisterCdcStateStoreOptions(services, options =>
		{
			options.TableName = tableName;
			configure?.Invoke(options);
		});

		services.TryAddSingleton<IDynamoDbCdcStateStore, DynamoDbCdcStateStore>();

		return services;
	}

	/// <summary>
	/// Adds an in-memory state store for DynamoDB CDC position tracking.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This is intended for testing and development. Positions are not
	/// persisted and will be lost when the process exits.
	/// </remarks>
	public static IServiceCollection AddInMemoryDynamoDbCdcStateStore(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IDynamoDbCdcStateStore, InMemoryDynamoDbCdcStateStore>();
		return services;
	}

	private static void RegisterCdcStateStoreOptions(
		IServiceCollection services,
		Action<DynamoDbCdcStateStoreOptions>? configure)
	{
		var optionsBuilder = services.AddOptions<DynamoDbCdcStateStoreOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DynamoDbCdcStateStoreOptions>, DynamoDbCdcStateStoreOptionsValidator>());
	}
}
