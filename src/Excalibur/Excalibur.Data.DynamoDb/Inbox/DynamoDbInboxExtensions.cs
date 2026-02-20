// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb.Inbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB inbox store.
/// </summary>
public static class DynamoDbInboxExtensions
{
	/// <summary>
	/// Adds DynamoDB inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbInboxStore(
		this IServiceCollection services,
		Action<DynamoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbInboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<DynamoDbInboxStore>();
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<DynamoDbInboxStore>());

		return services;
	}

	/// <summary>
	/// Adds DynamoDB inbox store to the service collection with region.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="region">The AWS region.</param>
	/// <param name="tableName">The DynamoDB table name.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbInboxStore(
		this IServiceCollection services,
		string region,
		string tableName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		return services.AddDynamoDbInboxStore(options =>
		{
			options.Region = region;
			options.TableName = tableName;
		});
	}

	/// <summary>
	/// Adds DynamoDB inbox store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientProvider">A factory function that provides the DynamoDB client.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbInboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, IAmazonDynamoDB> clientProvider,
		Action<DynamoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DynamoDbInboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton(sp =>
		{
			var client = clientProvider(sp);
			var options = sp.GetRequiredService<IOptions<DynamoDbInboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<DynamoDbInboxStore>>();
			return new DynamoDbInboxStore(client, options, logger);
		});
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<DynamoDbInboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use DynamoDB inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseDynamoDbInboxStore(
		this IDispatchBuilder builder,
		Action<DynamoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddDynamoDbInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use DynamoDB inbox store with region.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="region">The AWS region.</param>
	/// <param name="tableName">The DynamoDB table name.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseDynamoDbInboxStore(
		this IDispatchBuilder builder,
		string region,
		string tableName)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		return builder.UseDynamoDbInboxStore(options =>
		{
			options.Region = region;
			options.TableName = tableName;
		});
	}

	/// <summary>
	/// Configures the dispatch builder to use DynamoDB inbox store with an existing client.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="clientProvider">A factory function that provides the DynamoDB client.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseDynamoDbInboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, IAmazonDynamoDB> clientProvider,
		Action<DynamoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(clientProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddDynamoDbInboxStore(clientProvider, configure);

		return builder;
	}
}
