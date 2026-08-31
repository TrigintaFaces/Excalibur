// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Data.MongoDB.Snapshots;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB snapshot store services.
/// </summary>
public static class MongoDbSnapshotStoreExtensions
{
	/// <summary>
	/// Adds the MongoDB snapshot store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbSnapshotStore(
		this IServiceCollection services,
		Action<MongoDbSnapshotStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddDefaultTenantContext();
		_ = services.AddOptions<MongoDbSnapshotStoreOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbSnapshotStoreOptions>, MongoDbSnapshotStoreOptionsValidator>());

		// Register snapshot store
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructor
		// declares one) AND emits the ITenantScopingCapability<ISnapshotStore> marker inseparably. A bare
		// TryAddScoped here registered a store that honors the ambient tenant while attesting nothing, so
		// RowDiscriminator rejected a snapshot store that was in fact tenant-scoped.
		_ = services.AddTenantAwareStore<ISnapshotStore, MongoDbSnapshotStore>(sp =>
			new MongoDbSnapshotStore(
				sp.GetRequiredService<IOptions<MongoDbSnapshotStoreOptions>>(),
				sp.GetRequiredService<ILogger<MongoDbSnapshotStore>>(),
				sp.GetRequiredService<ITenantContext>()));

		// The seam registers the store under its own concrete type, so the contract needs an alias to stay
		// resolvable. It keeps the scoped lifetime this registration always had rather than being promoted.
		// Singleton, matching the lifetime the tenant-aware seam gives the concrete store and every other
		// snapshot provider. A scoped alias here would hand back the seam's root-owned singleton from a
		// child scope, and MS.DI captures a factory-returned IAsyncDisposable in the RESOLVING scope — so
		// disposing one request scope would dispose the shared store and every later scope would fault.
		// The store holds no per-scope state: it reads the ambient tenant per call via ITenantContext.
		services.TryAddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<MongoDbSnapshotStore>());

		return services;
	}

	/// <summary>
	/// Adds the MongoDB snapshot store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <param name="configureOptions">Optional action to further configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbSnapshotStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<MongoDbSnapshotStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddMongoDbSnapshotStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the MongoDB snapshot store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides a MongoDB client.</param>
	/// <param name="configureOptions">Action to configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing MongoDB infrastructure.
	/// </remarks>
	public static IServiceCollection AddMongoDbSnapshotStore(
		this IServiceCollection services,
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<MongoDbSnapshotStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddDefaultTenantContext();
		_ = services.AddOptions<MongoDbSnapshotStoreOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbSnapshotStoreOptions>, MongoDbSnapshotStoreOptionsValidator>());

		// Register snapshot store with client factory
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructor
		// declares one) AND emits the ITenantScopingCapability<ISnapshotStore> marker inseparably. The
		// consumer-supplied client factory is why this is the factory overload rather than the constructing
		// one. A bare TryAddScoped here registered a store that honors the ambient tenant while attesting
		// nothing, so RowDiscriminator rejected a snapshot store that was in fact tenant-scoped.
		_ = services.AddTenantAwareStore<ISnapshotStore, MongoDbSnapshotStore>(sp =>
			new MongoDbSnapshotStore(
				clientFactory(sp),
				sp.GetRequiredService<IOptions<MongoDbSnapshotStoreOptions>>(),
				sp.GetRequiredService<ILogger<MongoDbSnapshotStore>>(),
				sp.GetRequiredService<ITenantContext>()));

		// The seam registers the store under its own concrete type, so the contract needs an alias to stay
		// resolvable. It keeps the scoped lifetime this registration always had rather than being promoted.
		// Singleton, matching the lifetime the tenant-aware seam gives the concrete store and every other
		// snapshot provider. A scoped alias here would hand back the seam's root-owned singleton from a
		// child scope, and MS.DI captures a factory-returned IAsyncDisposable in the RESOLVING scope — so
		// disposing one request scope would dispose the shared store and every later scope would fault.
		// The store holds no per-scope state: it reads the ambient tenant per call via ITenantContext.
		services.TryAddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<MongoDbSnapshotStore>());

		return services;
	}
}
