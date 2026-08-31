// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DynamoDbEventStoreOptions>, DynamoDbEventStoreOptionsValidator>());

		// Registered before the connection paths below so that an explicitly supplied Streams client wins
		// the TryAdd against one this registration would otherwise build for itself.
		RegisterSuppliedStreamsClient(builder.Services, dynamoBuilder);

		// Register IAmazonDynamoDB based on connection path. Where the registration owns the connection it
		// also builds the matching Streams client, so a change feed works without further configuration;
		// where the consumer supplies the client, only an explicitly supplied Streams client is registered,
		// because the endpoint and credentials behind a supplied client are not ours to guess.
		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(builder.Services, dynamoBuilder);
		}
		else if (dynamoBuilder.ServiceUrlValue is not null)
		{
			var serviceUrl = dynamoBuilder.ServiceUrlValue;
			builder.Services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = serviceUrl }));
			builder.Services.TryAddSingleton<IAmazonDynamoDBStreams>(_ =>
				new AmazonDynamoDBStreamsClient(new AmazonDynamoDBStreamsConfig { ServiceURL = serviceUrl }));
		}
		else if (dynamoBuilder.RegionValue is not null)
		{
			var region = dynamoBuilder.RegionValue;
			builder.Services.TryAddSingleton<IAmazonDynamoDB>(_ =>
				new AmazonDynamoDBClient(region));
			builder.Services.TryAddSingleton<IAmazonDynamoDBStreams>(_ =>
				new AmazonDynamoDBStreamsClient(region));
		}

		// The store composes the ambient tenant into its partition key, so the default context is registered
		// before it: a host that never enabled multi-tenancy still resolves the framework single-tenant
		// default rather than failing to construct the store.
		_ = builder.Services.AddDefaultTenantContext();

		// Register store services. The Streams client is resolved optionally: it backs the change feed
		// alone, so a host that never consumes one must still be able to build the store.
		//
		// AddTenantAwareStore, not a bare TryAddSingleton: it registers the store AND emits the
		// ITenantScopingCapability<IEventStore> marker inseparably, derived from the store's own
		// constructor shape. A store that stopped taking ITenantContext would silently lose the marker
		// rather than keep attesting a confinement it no longer provides.
		_ = builder.Services.AddTenantAwareStore<IEventStore, DynamoDbEventStore>(sp =>
		{
			var client = sp.GetRequiredService<IAmazonDynamoDB>();
			var options = sp.GetRequiredService<IOptions<DynamoDbEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<DynamoDbEventStore>>();
			var tenantContext = sp.GetRequiredService<ITenantContext>();
			var streamsClient = sp.GetService<IAmazonDynamoDBStreams>();

			return streamsClient is null
				? new DynamoDbEventStore(client, options, logger, tenantContext)
				: new DynamoDbEventStore(client, streamsClient, options, logger, tenantContext);
		});

		// The store is also registered under ICloudNativeEventStore, which is separately [TenantOwned]. A
		// capability is required per CONTRACT, so attesting IEventStore alone leaves a multi-tenant host
		// refused on the document contract and this store's confinement unreachable through the supported
		// composition. Emitted from the same seam, over the same store, so neither attestation can be
		// present without the ambient tenant the store was built with.
		_ = builder.Services.AddTenantAwareStore<ICloudNativeEventStore, DynamoDbEventStore>(
			sp => sp.GetRequiredService<DynamoDbEventStore>());
		builder.Services.AddKeyedSingleton<IEventStore>("dynamodb", (sp, _) => sp.GetRequiredService<DynamoDbEventStore>());
		builder.Services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("dynamodb"));
		builder.Services.TryAddSingleton<ICloudNativeEventStore>(sp => sp.GetRequiredService<DynamoDbEventStore>());
	}

	private static void RegisterSuppliedStreamsClient(
		IServiceCollection services,
		DynamoDBEventSourcingBuilder dynamoBuilder)
	{
		if (dynamoBuilder.StreamsClientInstance is not null)
		{
			var streamsClient = dynamoBuilder.StreamsClientInstance;
			services.TryAddSingleton(streamsClient);
		}
		else if (dynamoBuilder.StreamsClientFactoryFunc is not null)
		{
			var factory = dynamoBuilder.StreamsClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
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
