// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Runtime;
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
			builder.Services.TryAddSingleton<IAmazonDynamoDB>(sp =>
			{
				var config = new AmazonDynamoDBConfig { ServiceURL = serviceUrl };

				// Honour configured credentials on this path too. The store's own client factory already
				// does (DynamoDbSagaStore builds BasicAWSCredentials from the same two options), so a
				// consumer who sets Connection.AccessKey/SecretKey had them applied by one client and
				// silently dropped by the one registered here -- the two disagreed about the same
				// configuration. Where they are absent the SDK's default credential chain still applies,
				// which is what an AWS-hosted consumer relies on.
				var connection = sp.GetService<IOptions<DynamoDbSagaOptions>>()?.Value.Connection;
				return !string.IsNullOrWhiteSpace(connection?.AccessKey)
					&& !string.IsNullOrWhiteSpace(connection.SecretKey)
						? new AmazonDynamoDBClient(
							new BasicAWSCredentials(connection.AccessKey, connection.SecretKey), config)
						: new AmazonDynamoDBClient(config);
			});
		}
		else if (dynamoBuilder.RegionValue is not null)
		{
			var region = dynamoBuilder.RegionValue;
			builder.Services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(region));
		}

		// Register store services
		_ = builder.Services.AddDefaultTenantContext();
		// AddTenantAwareStore emits ITenantScopingCapability<ISagaStore> as part of THIS registration, so
		// the attestation cannot exist without the store it describes. This store's constructor declares an
		// ITenantContext, so the seam resolves it fail-closed before the factory runs and emits the ambient-
		// scoped marker. Without it, row-discriminator multi-tenancy refuses every host that reaches the
		// store through THIS path, while the sibling entry point in the same package looks done.
		_ = builder.Services.AddTenantAwareStore<ISagaStore, DynamoDbSagaStore>();
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
