// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Data.CosmosDb.Snapshots;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB snapshot store services.
/// </summary>
public static class CosmosDbSnapshotStoreExtensions
{
	/// <summary>
	/// Adds the Cosmos DB snapshot store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbSnapshotStore(
		this IServiceCollection services,
		Action<CosmosDbSnapshotStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddDefaultTenantContext();
		_ = services.AddOptions<CosmosDbSnapshotStoreOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbSnapshotStoreOptions>, CosmosDbSnapshotStoreOptionsValidator>());

		// Register snapshot store
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store (injecting ITenantContext, since this store's constructor
		// declares one) AND emits the ITenantScopingCapability<ISnapshotStore> marker inseparably. A bare
		// TryAddScoped here registered a store that honors the ambient tenant while attesting nothing, so
		// RowDiscriminator rejected a snapshot store that was in fact tenant-scoped.
		_ = services.AddTenantAwareStore<ISnapshotStore, CosmosDbSnapshotStore>(sp =>
			new CosmosDbSnapshotStore(
				sp.GetRequiredService<IOptions<CosmosDbSnapshotStoreOptions>>(),
				sp.GetRequiredService<ILogger<CosmosDbSnapshotStore>>(),
				sp.GetRequiredService<ITenantContext>()));

		// The seam registers the store under its own concrete type, so the contract needs an alias to stay
		// resolvable. It keeps the scoped lifetime this registration always had rather than being promoted.
		// Singleton, matching the lifetime the tenant-aware seam gives the concrete store and every other
		// snapshot provider. A scoped alias here would hand back the seam's root-owned singleton from a
		// child scope, and MS.DI captures a factory-returned IAsyncDisposable in the RESOLVING scope — so
		// disposing one request scope would dispose the shared store and every later scope would fault.
		// The store holds no per-scope state: it reads the ambient tenant per call via ITenantContext.
		services.TryAddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<CosmosDbSnapshotStore>());

		return services;
	}

}
