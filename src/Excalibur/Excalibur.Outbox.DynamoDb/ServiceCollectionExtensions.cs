// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Outbox.DynamoDb;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB outbox store in dependency injection.
/// </summary>
public static class DynamoDbOutboxServiceCollectionExtensions
{
	/// <summary>
	/// Adds the DynamoDB outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbOutboxStore(
		this IServiceCollection services,
		Action<DynamoDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<DynamoDbOutboxStore>();
		_ = services.AddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<DynamoDbOutboxStore>());

		return services;
	}

	/// <summary>
	/// Adds the DynamoDB outbox store to the service collection with configuration from a section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section containing the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbOutboxStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DynamoDbOutboxOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<DynamoDbOutboxStore>();
		_ = services.AddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<DynamoDbOutboxStore>());

		return services;
	}

	/// <summary>
	/// Adds the DynamoDB outbox store to the service collection with options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The pre-configured options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbOutboxStore(
		this IServiceCollection services,
		DynamoDbOutboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		_ = services.AddOptions<DynamoDbOutboxOptions>()
			.Configure(o =>
		{
			o.ServiceUrl = options.ServiceUrl;
			o.Region = options.Region;
			o.AccessKey = options.AccessKey;
			o.SecretKey = options.SecretKey;
			o.TableName = options.TableName;
			o.PartitionKeyAttribute = options.PartitionKeyAttribute;
			o.SortKeyAttribute = options.SortKeyAttribute;
			o.TtlAttribute = options.TtlAttribute;
			o.DefaultTimeToLiveSeconds = options.DefaultTimeToLiveSeconds;
			o.MaxRetryAttempts = options.MaxRetryAttempts;
			o.CreateTableIfNotExists = options.CreateTableIfNotExists;
			o.EnableStreams = options.EnableStreams;
		})
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton<DynamoDbOutboxStore>();
		_ = services.AddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<DynamoDbOutboxStore>());

		return services;
	}
}
