// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.CloudNative;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Extension methods for configuring Cosmos DB event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventSourcingBuilderCosmosDbExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Azure Cosmos DB for event storage.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the CosmosDb event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseCosmosDb(cosmos =&gt;
	///     {
	///         cosmos.ConnectionString(connectionString)
	///               .DatabaseName("events")
	///               .ContainerName("event-store");
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IEventSourcingBuilder UseCosmosDb(
		this IEventSourcingBuilder builder,
		Action<ICosmosDbEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new CosmosDbEventStoreOptions();
		var cosmosBuilder = new CosmosDbEventSourcingBuilder(options);
		configure(cosmosBuilder);

		var hasBuilderConnection = cosmosBuilder.ClientInstance is not null
			|| cosmosBuilder.ClientFactoryFunc is not null;

		RegisterOptionsAndServices(builder, cosmosBuilder, options, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		CosmosDbEventSourcingBuilder cosmosBuilder,
		CosmosDbEventStoreOptions options,
		bool hasBuilderConnection)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<CosmosDbEventStoreOptions>(opt =>
		{
			opt.EventsContainerName = options.EventsContainerName;
			opt.DatabaseName = options.DatabaseName;
		});

		// Register BindConfiguration if set
		if (cosmosBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<CosmosDbEventStoreOptions>()
				.BindConfiguration(cosmosBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<CosmosDbEventStoreOptions>().ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbEventStoreOptions>, CosmosDbEventStoreOptionsValidator>());

		// Register CosmosClient based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedClient(builder.Services, cosmosBuilder);
		}
		else if (cosmosBuilder.EndpointValue is not null)
		{
			var endpoint = cosmosBuilder.EndpointValue;
			var authKey = cosmosBuilder.AuthKeyValue!;
			// Intentional bespoke-interop deviation (NOT the event canonical contract): the Cosmos SDK's document
			// serializer is configured camelCase because Cosmos's own document model (id, _etag, _ts, partition
			// key) is camelCase-shaped and this options object governs the SDK's document (de)serialization, not
			// the event payload. The event payload itself uses the canonical serializer; this is the SDK boundary.
			builder.Services.TryAddSingleton(_ => new CosmosClient(endpoint, authKey, new CosmosClientOptions { UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase } }));
		}
		else if (cosmosBuilder.ConnectionStringValue is not null)
		{
			var connStr = cosmosBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton(_ => new CosmosClient(connStr, new CosmosClientOptions { UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase } }));
		}

		// The store composes the ambient tenant into its partition key, so the default context is registered
		// before it: a host that never enabled multi-tenancy still resolves the framework single-tenant
		// default rather than failing to construct the store.
		_ = builder.Services.AddDefaultTenantContext();

		// Register store services. AddTenantAwareStore, not a bare TryAddSingleton: it registers the store
		// AND emits the ITenantScopingCapability<IEventStore> marker inseparably, derived from the store's
		// own constructor shape. A store that stopped taking ITenantContext would silently lose the marker
		// rather than keep attesting a confinement it no longer provides.
		_ = builder.Services.AddTenantAwareStore<IEventStore, CosmosDbEventStore>();

		// The store is also registered under ICloudNativeEventStore, which is separately [TenantOwned]. A
		// capability is required per CONTRACT, so attesting IEventStore alone leaves a multi-tenant host
		// refused on the document contract and this store's confinement unreachable through the supported
		// composition. Emitted from the same seam, over the same store, so neither attestation can be
		// present without the ambient tenant the store was built with.
		_ = builder.Services.AddTenantAwareStore<ICloudNativeEventStore, CosmosDbEventStore>();
		builder.Services.AddKeyedSingleton<IEventStore>("cosmosdb", (sp, _) => sp.GetRequiredService<CosmosDbEventStore>());
		builder.Services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("cosmosdb"));
		builder.Services.TryAddSingleton<ICloudNativeEventStore>(sp => sp.GetRequiredService<CosmosDbEventStore>());

		// Change-feed durability default + non-durable startup warning, shared with the Cosmos data provider.
		// Registering here means an event-store-only consumer (no AddExcaliburCosmosDb) still gets the default
		// checkpoint store and is warned when continuation is non-durable, instead of silently replaying from
		// the start position on every restart.
		_ = builder.Services.AddCosmosDbChangeFeedDurabilityDefaults();
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		CosmosDbEventSourcingBuilder cosmosBuilder)
	{
		if (cosmosBuilder.ClientInstance is not null)
		{
			var client = cosmosBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (cosmosBuilder.ClientFactoryFunc is not null)
		{
			var factory = cosmosBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}
}
