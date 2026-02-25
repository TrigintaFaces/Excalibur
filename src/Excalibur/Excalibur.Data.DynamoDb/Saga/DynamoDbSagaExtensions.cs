// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb.Saga;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB saga store services.
/// </summary>
public static class DynamoDbSagaExtensions
{
	/// <summary>
	/// Adds the DynamoDB saga store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="DynamoDbSagaStore"/> as the implementation of <see cref="ISagaStore"/>.
	/// The store uses DynamoDB single-table design with PK/SK composite keys.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddDynamoDbSagaStore(options =>
	/// {
	///     options.Region = "us-east-1";
	///     options.TableName = "sagas";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDynamoDbSagaStore(
		this IServiceCollection services,
		Action<DynamoDbSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<DynamoDbSagaOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DynamoDbSagaOptions>, DynamoDbSagaOptionsValidator>());

		services.TryAddSingleton<DynamoDbSagaStore>();
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<DynamoDbSagaStore>());

		return services;
	}

	/// <summary>
	/// Adds the DynamoDB saga store to the service collection with a service URL.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="serviceUrl">The DynamoDB service URL (for local development).</param>
	/// <param name="tableName">The table name. Defaults to "sagas".</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Simplified registration for local development scenarios with DynamoDB Local or LocalStack.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddDynamoDbSagaStore("http://localhost:8000");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDynamoDbSagaStore(
		this IServiceCollection services,
		string serviceUrl,
		string tableName = "sagas")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);

		return services.AddDynamoDbSagaStore(options =>
		{
			options.ServiceUrl = serviceUrl;
			options.TableName = tableName;
		});
	}

	/// <summary>
	/// Adds the DynamoDB saga store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides a DynamoDB client.</param>
	/// <param name="configureOptions">Action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom credentials management, or integration with existing DynamoDB infrastructure.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddDynamoDbSagaStore(
	///     sp => sp.GetRequiredService&lt;IAmazonDynamoDB&gt;(),
	///     options => options.TableName = "sagas");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDynamoDbSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, IAmazonDynamoDB> clientFactory,
		Action<DynamoDbSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<DynamoDbSagaOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DynamoDbSagaOptions>, DynamoDbSagaOptionsValidator>());

		services.TryAddSingleton(sp =>
		{
			var client = clientFactory(sp);
			var options = sp.GetRequiredService<IOptions<DynamoDbSagaOptions>>();
			var logger = sp.GetRequiredService<ILogger<DynamoDbSagaStore>>();
			var serializer = sp.GetRequiredService<IJsonSerializer>();
			return new DynamoDbSagaStore(client, options, logger, serializer);
		});
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<DynamoDbSagaStore>());

		return services;
	}
}
