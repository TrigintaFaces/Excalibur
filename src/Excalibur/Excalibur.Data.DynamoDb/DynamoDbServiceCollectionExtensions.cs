// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon.DynamoDBv2;

using Excalibur.Data.CloudNative;
using Excalibur.Data.DynamoDb;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB data services.
/// </summary>
public static class DynamoDbServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS DynamoDB data provider to the service collection using the fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the DynamoDB data builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburDynamoDb(dynamo =&gt;
	/// {
	///     dynamo.ServiceUrl("http://localhost:8000")
	///           .TableName("data");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddExcaliburDynamoDb(
		this IServiceCollection services,
		Action<IDynamoDBDataBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var dynamoBuilder = new DynamoDBDataBuilder();
		configure(dynamoBuilder);

		var hasBuilderClient = dynamoBuilder.ClientInstance is not null
			|| dynamoBuilder.ClientFactoryFunc is not null;

		RegisterOptionsAndServices(services, dynamoBuilder, hasBuilderClient);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		DynamoDBDataBuilder dynamoBuilder,
		bool hasBuilderClient)
	{
		// Register store-specific options from builder state
		_ = services.Configure<DynamoDbOptions>(opt =>
		{
			if (dynamoBuilder.TableNameValue is not null)
			{
				opt.DefaultTableName = dynamoBuilder.TableNameValue;
			}

			if (dynamoBuilder.ServiceUrlValue is not null)
			{
				opt.Connection.ServiceUrl = dynamoBuilder.ServiceUrlValue;
			}

			if (dynamoBuilder.RegionValue is not null)
			{
				opt.Connection.Region = dynamoBuilder.RegionValue.SystemName;
			}
		});

		// Register BindConfiguration if set
		if (dynamoBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<DynamoDbOptions>()
				.BindConfiguration(dynamoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		services.AddOptions<DynamoDbOptions>().ValidateOnStart();

		// Register validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DynamoDbOptions>, DynamoDbOptionsValidator>());

		// Register IAmazonDynamoDB based on connection path
		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(services, dynamoBuilder);
		}
		else if (dynamoBuilder.ServiceUrlValue is not null)
		{
			var serviceUrl = dynamoBuilder.ServiceUrlValue;
			services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl }));
		}
		else if (dynamoBuilder.RegionValue is not null)
		{
			var region = dynamoBuilder.RegionValue;
			services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(region));
		}

		// Register core services
		RegisterCoreServices(services);
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		DynamoDBDataBuilder dynamoBuilder)
	{
		if (dynamoBuilder.ClientInstance is not null)
		{
			var client = dynamoBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (dynamoBuilder.ClientFactoryFunc is not null)
		{
			var factory = dynamoBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
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
