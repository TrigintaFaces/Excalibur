// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB services.
/// </summary>
public static class CosmosDbServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Cosmos DB data provider to the service collection using the fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the CosmosDb data builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburCosmosDb(cosmos =&gt;
	/// {
	///     cosmos.ConnectionString(connectionString)
	///           .DatabaseName("myapp")
	///           .ContainerName("data");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddExcaliburCosmosDb(
		this IServiceCollection services,
		Action<ICosmosDbDataBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new CosmosDbOptions();
		var cosmosBuilder = new CosmosDbDataBuilder(options);
		configure(cosmosBuilder);

		var hasBuilderConnection = cosmosBuilder.ClientInstance is not null
			|| cosmosBuilder.ClientFactoryFunc is not null;

		RegisterOptionsAndServices(services, cosmosBuilder, options, hasBuilderConnection);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		CosmosDbDataBuilder cosmosBuilder,
		CosmosDbOptions options,
		bool hasBuilderConnection)
	{
		// Register store-specific options from builder state
		_ = services.Configure<CosmosDbOptions>(opt =>
		{
			opt.DatabaseName = options.DatabaseName;
			opt.DefaultContainerName = options.DefaultContainerName;
		});

		// Register BindConfiguration if set
		if (cosmosBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<CosmosDbOptions>()
				.BindConfiguration(cosmosBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		services.AddOptions<CosmosDbOptions>().ValidateOnStart();

		// Register validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>());

		// Register CosmosClient based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedClient(services, cosmosBuilder, options);
		}
		else if (cosmosBuilder.EndpointValue is not null)
		{
			var endpoint = cosmosBuilder.EndpointValue;
			var authKey = cosmosBuilder.AuthKeyValue!;
			services.TryAddSingleton(_ => new CosmosClient(endpoint, authKey, CreateStjClientOptions()));

			// Map to options so downstream code can read connection info
			_ = services.Configure<CosmosDbOptions>(opt =>
			{
				opt.Client.AccountEndpoint = endpoint;
				opt.Client.AccountKey = authKey;
			});
		}
		else if (cosmosBuilder.ConnectionStringValue is not null)
		{
			var connStr = cosmosBuilder.ConnectionStringValue;
			services.TryAddSingleton(_ => new CosmosClient(connStr, CreateStjClientOptions()));

			// Map to options so downstream code can read connection info
			_ = services.Configure<CosmosDbOptions>(opt =>
			{
				opt.Client.ConnectionString = connStr;
			});
		}

		// Register core services
		RegisterCoreServices(services);
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		CosmosDbDataBuilder cosmosBuilder,
		CosmosDbOptions options)
	{
		// Set sentinel so the options validation passes
		const string sentinel =
			"AccountEndpoint=https://builder-managed.documents.azure.com:443/;AccountKey=YnVpbGRlci1tYW5hZ2VkLWtleQ==;";

		options.Client.ConnectionString = sentinel;

		_ = services.Configure<CosmosDbOptions>(opt =>
		{
			opt.Client.ConnectionString = sentinel;
		});

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

	// fmjwqy (SA HYBRID ruling): every framework-BUILT Cosmos client must use the System.Text.Json
	// serializer. Cosmos SDK v3's default serializer is Newtonsoft, which ignores the persisted
	// documents' [JsonPropertyName] attributes and emits PascalCase — bricking lowercase point-reads
	// and partition-key paths. Configuring STJ here makes every STJ-only document correct by
	// construction on the framework-built path (consumer-supplied clients are defended via dual-mapping).
	private static CosmosClientOptions CreateStjClientOptions() =>
		new()
		{
			UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
			{
				PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
			},
		};

	/// <summary>
	/// Registers the durable Cosmos DB-backed <see cref="IChangeFeedCheckpointStore"/>, replacing the
	/// default in-memory store so change-feed continuation survives process restarts.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="containerFactory">
	/// Factory that resolves the Cosmos <see cref="Container"/> used to persist checkpoint documents
	/// (partition key path <c>/subscriptionId</c>); the caller owns the container's creation.
	/// </param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <remarks>
	/// Call after <c>AddCosmosDb</c>: this replaces the in-memory default registered by the core services,
	/// so the persistence provider flows the durable store into every change-feed subscription.
	/// </remarks>
	public static IServiceCollection AddCosmosDbChangeFeedCheckpointStore(
		this IServiceCollection services,
		Func<IServiceProvider, Container> containerFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(containerFactory);

		services.Replace(ServiceDescriptor.Singleton<IChangeFeedCheckpointStore>(
			sp => new CosmosDbChangeFeedCheckpointStore(containerFactory(sp))));

		return services;
	}

	/// <summary>
	/// Registers the shared change-feed durability default: the process-local (non-durable)
	/// <see cref="IChangeFeedCheckpointStore"/>. Idempotent — safe to call from every Cosmos entry point
	/// (data provider, event store, outbox) so an ES-only or Outbox-only consumer always resolves a
	/// checkpoint store rather than silently flowing <see langword="null"/> into its change-feed
	/// subscriptions.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <remarks>
	/// The default <see cref="InMemoryChangeFeedCheckpointStore"/> emits a LOUD non-durable warning once on
	/// construction, so "silently non-durable" cannot re-create the advertised-but-inert bug FR-B1 fixes.
	/// Registering a durable store via <see cref="AddCosmosDbChangeFeedCheckpointStore"/> replaces this
	/// default (so the in-memory store is never constructed and the warning never fires).
	/// </remarks>
	public static IServiceCollection AddCosmosDbChangeFeedDurabilityDefaults(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IChangeFeedCheckpointStore, InMemoryChangeFeedCheckpointStore>();

		return services;
	}

	private static void RegisterCoreServices(IServiceCollection services)
	{
		services.TryAddSingleton<CosmosDbPersistenceProvider>();
		services.TryAddSingleton<ICloudNativePersistenceProvider>(sp =>
			sp.GetRequiredService<CosmosDbPersistenceProvider>());

		// Register health check
		services.TryAddSingleton<CosmosDbHealthCheck>();

		// Default change-feed checkpoint store + non-durable startup warning (shared across Cosmos entry points).
		_ = services.AddCosmosDbChangeFeedDurabilityDefaults();
	}
}
