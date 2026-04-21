// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon.DynamoDBv2;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventSourcingBuilderDynamoDbExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use AWS DynamoDB for event storage.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the DynamoDB event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseDynamoDb(dynamo =&gt;
	///     {
	///         dynamo.ServiceUrl("http://localhost:8000")
	///               .TableName("events");
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IEventSourcingBuilder UseDynamoDb(
		this IEventSourcingBuilder builder,
		Action<IDynamoDBEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var dynamoBuilder = new DynamoDBEventSourcingBuilder();
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
		IEventSourcingBuilder builder,
		DynamoDBEventSourcingBuilder dynamoBuilder,
		bool hasBuilderClient)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<DynamoDbEventStoreOptions>(opt =>
		{
			if (dynamoBuilder.TableNameValue is not null)
			{
				opt.EventsTableName = dynamoBuilder.TableNameValue;
			}
		});

		// Register BindConfiguration if set
		if (dynamoBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<DynamoDbEventStoreOptions>()
				.BindConfiguration(dynamoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<DynamoDbEventStoreOptions>().ValidateOnStart();

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
		builder.Services.TryAddSingleton<DynamoDbEventStore>();
		builder.Services.AddKeyedSingleton<IEventStore>("dynamodb", (sp, _) => sp.GetRequiredService<DynamoDbEventStore>());
		builder.Services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("dynamodb"));
		builder.Services.TryAddSingleton<ICloudNativeEventStore>(sp => sp.GetRequiredService<DynamoDbEventStore>());
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		DynamoDBEventSourcingBuilder dynamoBuilder)
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
