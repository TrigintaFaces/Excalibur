// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing;
using Excalibur.EventSourcing.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering in-memory event sourcing services.
/// </summary>
public static class InMemoryEventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds the in-memory event store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="InMemoryEventStore"/> as a singleton implementation of <see cref="IEventStore"/>.
	/// </para>
	/// <para>
	/// <b>Warning:</b> The in-memory event store is intended for testing and development only.
	/// Data is lost when the process restarts.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddInMemoryEventStore(this IServiceCollection services)
		=> AddInMemoryEventStore(services, "inmemory");

	/// <summary>
	/// Adds the in-memory event store to the service collection with a specific store name.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="storeName">The store name used as the keyed service key.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="InMemoryEventStore"/> as a keyed singleton implementation of <see cref="IEventStore"/>.
	/// </para>
	/// <para>
	/// <b>Warning:</b> The in-memory event store is intended for testing and development only.
	/// Data is lost when the process restarts.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddInMemoryEventStore(this IServiceCollection services, string storeName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(storeName);

		// Registers IOptions<InMemoryEventStoreOptions> so the store's constructor can be handed the host's
		// configuration. A host opts into reflection-free serialization with
		// Configure<InMemoryEventStoreOptions>(o => o.EventTypeInfoResolver = MyContext.Default); with nothing
		// configured this resolves the default instance and the store serializes through reflection as before.
		_ = services.AddOptions<InMemoryEventStoreOptions>();

		// TryAdd, so a host that established its own tenancy keeps it and a single-tenant host still gets a
		// context. The store's constructor requires ITenantContext -- the tenant term is part of its stream
		// key -- so without this the registration is not self-sufficient.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore constructs the store (injecting ITenantContext, since its constructor declares
		// one) AND emits the ITenantScopingCapability<IEventStore> marker inseparably, so the attestation
		// cannot exist without the wiring it describes. This store genuinely scopes -- the tenant term is part
		// of its stream key -- but a bare AddSingleton attested nothing, so RowDiscriminator refused every host
		// that used it, including the test hosts this provider exists to serve.
		_ = services.AddTenantAwareStore<IEventStore, InMemoryEventStore>();
		services.AddKeyedSingleton<IEventStore>(storeName, (sp, _) => sp.GetRequiredService<InMemoryEventStore>());
		services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>(storeName));

		return services;
	}
}
