// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory inbox store.
/// </summary>
public static class InMemoryInboxExtensions
{
	/// <summary>
	/// Adds in-memory inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddInMemoryInboxStore(
		this IServiceCollection services,
		Action<InMemoryInboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<InMemoryInboxOptions>()
			.Configure(configure ?? (_ => { }))
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InMemoryInboxOptions>, InMemoryInboxOptionsValidator>());

		// TryAdd, so a host that established its own tenancy keeps it and a single-tenant host still gets a
		// context. AddTenantAwareStore resolves ITenantContext with GetRequiredService, so without this the
		// registration is not self-sufficient.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store injecting ITenantContext -- so the dedup key and every keyed
		// read scope per tenant, since this store's constructor declares one -- AND emits the
		// ITenantScopingCapability<IInboxStore> marker inseparably from that wiring. Registering the store
		// and the marker separately is what lets an unwired store carry a truthful-looking marker, which a
		// capability requirement then passes on.
		_ = services.AddTenantAwareStore<IInboxStore, InMemoryInboxStore>();
		services.AddKeyedSingleton<IInboxStore>("inmemory", (sp, _) => sp.GetRequiredService<InMemoryInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("inmemory"));

		// Non-keyed convenience alias, so a host that calls only this extension can inject
		// <see cref="IInboxStore"/> without [FromKeyedServices("default")]. AddTenantAwareStore above
		// registers the CONCRETE store -- it infers the service type from the factory, not from the
		// contract -- so without this line the contract this method is named for resolves to nothing and
		// the consumer meets it at startup, after wiring.
		//
		// Forwards to keyed "default" rather than to the concrete type, so the keyed and non-keyed views
		// never disagree: if another provider already holds "default", both resolve to that provider.
		// TryAdd, so the composition extension and a consumer's own store both keep winning.
		services.TryAddSingleton<IInboxStore>(static sp => sp.GetRequiredKeyedService<IInboxStore>("default"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use in-memory inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseInMemoryInboxStore(
		this IDispatchBuilder builder,
		Action<InMemoryInboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddInMemoryInboxStore(configure);

		return builder;
	}
}
