// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory outbox store.
/// </summary>
public static class InMemoryOutboxExtensions
{
	/// <summary>
	/// Adds in-memory outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddInMemoryOutboxStore(
		this IServiceCollection services,
		Action<InMemoryOutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<InMemoryOutboxOptions>()
			.Configure(configure ?? (_ => { }))
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InMemoryOutboxOptions>, InMemoryOutboxOptionsValidator>());

		// The store's constructor is internal (it is not part of the consumer contract), and
		// ActivatorUtilities only considers public constructors -- so the type must be created by an
		// explicit factory here rather than by TryAddSingleton<T>()'s implementation-type activation.
		// AddTenantAwareStore emits the ITenantPartitionedCapability<IOutboxStore> marker inseparably from
		// the store registration: InMemoryOutboxStore implements ITenantPartitionedStore because it persists
		// TenantId on every message and hands it back on drain rather than reading an ambient ITenantContext.
		services.AddTenantAwareStore<IOutboxStore, InMemoryOutboxStore>(static sp => new InMemoryOutboxStore(
			sp.GetRequiredService<IOptions<InMemoryOutboxOptions>>(),
			sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InMemoryOutboxStore>>()));
		services.AddKeyedSingleton<IOutboxStore>("inmemory", (sp, _) => sp.GetRequiredService<InMemoryOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("inmemory"));

		// Non-keyed convenience alias, so a host that calls only this extension can inject
		// <see cref="IOutboxStore"/> without [FromKeyedServices("default")]. AddTenantAwareStore above
		// registers the CONCRETE store -- it infers the service type from the factory, not from the
		// contract -- so without this line the contract this method is named for resolves to nothing and
		// the consumer meets it at startup, after wiring.
		//
		// Forwards to keyed "default" rather than to the concrete type, so the keyed and non-keyed views
		// never disagree: if another provider already holds "default", both resolve to that provider.
		// TryAdd, so the composition extension and a consumer's own store both keep winning.
		services.TryAddSingleton<IOutboxStore>(static sp => sp.GetRequiredKeyedService<IOutboxStore>("default"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use in-memory outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseInMemoryOutboxStore(
		this IDispatchBuilder builder,
		Action<InMemoryOutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddInMemoryOutboxStore(configure);

		return builder;
	}
}
