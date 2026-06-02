// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon.DynamoDBv2;

using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.DynamoDb;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderDynamoDbExtensions
{
	/// <summary>
	/// Configures the saga builder to use AWS DynamoDB for saga state storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Configuration action for the DynamoDB saga builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddSagas(saga =&gt;
	/// {
	///     saga.UseDynamoDb(dynamo =&gt;
	///     {
	///         dynamo.ServiceUrl("http://localhost:8000")
	///               .TableName("sagas");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static ISagaBuilder UseDynamoDb(
		this ISagaBuilder builder,
		Action<IDynamoDBSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var dynamoBuilder = new DynamoDBSagaBuilder();
		configure(dynamoBuilder);

		var hasBuilderClient = dynamoBuilder.ClientInstance is not null
			|| dynamoBuilder.ClientFactoryFunc is not null;

		RegisterOptionsAndServices(builder, dynamoBuilder, hasBuilderClient);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		ISagaBuilder builder,
		DynamoDBSagaBuilder dynamoBuilder,
		bool hasBuilderClient)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<DynamoDbSagaOptions>(opt =>
		{
			if (dynamoBuilder.TableNameValue is not null)
			{
				opt.TableName = dynamoBuilder.TableNameValue;
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
			builder.Services.AddOptions<DynamoDbSagaOptions>()
				.BindConfiguration(dynamoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<DynamoDbSagaOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DynamoDbSagaOptions>, DynamoDbSagaOptionsValidator>());

		// Register IAmazonDynamoDB based on connection path
		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(builder.Services, dynamoBuilder);
		}
		else if (dynamoBuilder.ServiceUrlValue is not null)
		{
			var serviceUrl = dynamoBuilder.ServiceUrlValue;
			builder.Services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl }));
		}
		else if (dynamoBuilder.RegionValue is not null)
		{
			var region = dynamoBuilder.RegionValue;
			builder.Services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(region));
		}

		// Register store services
		builder.Services.TryAddSingleton<DynamoDbSagaStore>();
		builder.Services.AddKeyedSingleton<ISagaStore>("dynamodb", (sp, _) => sp.GetRequiredService<DynamoDbSagaStore>());
		builder.Services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("dynamodb"));
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		DynamoDBSagaBuilder dynamoBuilder)
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
}
