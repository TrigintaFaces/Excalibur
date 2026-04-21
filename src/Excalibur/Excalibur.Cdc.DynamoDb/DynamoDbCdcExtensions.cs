// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Cdc.DynamoDb;

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
	public static IServiceCollection AddDynamoDbCdc(
		this IServiceCollection services,
		Action<DynamoDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbCdcOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddSingleton<IDynamoDbCdcProcessor, DynamoDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds DynamoDB CDC processor services to the service collection using configuration.
	/// </summary>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDynamoDbCdc(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DynamoDbCdcOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddSingleton<IDynamoDbCdcProcessor, DynamoDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds DynamoDB CDC processor services to the service collection using a named configuration section.
	/// </summary>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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
			.ValidateOnStart();
		services.TryAddSingleton<IDynamoDbCdcProcessor, DynamoDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds a DynamoDB-backed state store for CDC position tracking.
	/// </summary>
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
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DynamoDbCdcStateStoreOptions>, DynamoDbCdcStateStoreOptionsValidator>());
	}
}
