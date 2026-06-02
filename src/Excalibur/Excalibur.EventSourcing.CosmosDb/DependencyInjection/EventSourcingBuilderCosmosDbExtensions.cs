// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.CloudNative;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

		// Register CosmosClient based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedClient(builder.Services, cosmosBuilder);
		}
		else if (cosmosBuilder.EndpointValue is not null)
		{
			var endpoint = cosmosBuilder.EndpointValue;
			var authKey = cosmosBuilder.AuthKeyValue!;
			builder.Services.TryAddSingleton(_ => new CosmosClient(endpoint, authKey));
		}
		else if (cosmosBuilder.ConnectionStringValue is not null)
		{
			var connStr = cosmosBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton(_ => new CosmosClient(connStr));
		}

		// Register store services
		builder.Services.TryAddSingleton<CosmosDbEventStore>();
		builder.Services.AddKeyedSingleton<IEventStore>("cosmosdb", (sp, _) => sp.GetRequiredService<CosmosDbEventStore>());
		builder.Services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("cosmosdb"));
		builder.Services.TryAddSingleton<ICloudNativeEventStore>(sp => sp.GetRequiredService<CosmosDbEventStore>());
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
