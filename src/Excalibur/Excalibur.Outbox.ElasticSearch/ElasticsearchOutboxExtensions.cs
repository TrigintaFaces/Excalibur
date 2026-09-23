// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Outbox.ElasticSearch;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch outbox store.
/// </summary>
public static class ElasticsearchOutboxExtensions
{
	/// <summary>
	/// Adds Elasticsearch outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticsearchOutboxStore(
		this IServiceCollection services,
		Action<ElasticsearchOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<ElasticsearchOutboxOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ElasticsearchOutboxOptions>, ElasticsearchOutboxOptionsValidator>());
		// AddTenantAwareStore emits ITenantPartitionedCapability<IOutboxStore> as part of THIS registration,
		// so the attestation cannot exist without the store it describes. It is the partitioned seam rather
		// than the scoped one because this store reads no ambient tenant on any path: it records the tenant
		// on each document and returns it on drain, so the owning tenant is re-established from the document.
		// That seam takes no ITenantContext, so there is no dependency here to be handed over and silently
		// discarded. Without it, row-discriminator multi-tenancy refuses every host that selects this provider.
		_ = services.AddTenantAwareStore<IOutboxStore, ElasticsearchOutboxStore>();
		services.AddKeyedSingleton<IOutboxStore>("elasticsearch", (sp, _) => sp.GetRequiredService<ElasticsearchOutboxStore>());
		// Non-keyed convenience alias, so a host that calls only this extension can inject
		// IOutboxStore without [FromKeyedServices]. AddTenantAwareStore registers the CONCRETE store --
		// it infers the service type from the factory, not from the contract -- so without this the
		// contract this method is named for resolves to nothing, and the consumer meets it at startup.
		// Forwards to keyed "default" so the keyed and non-keyed views can never disagree; TryAdd so a
		// consumer's own registration still wins.
		services.TryAddSingleton<IOutboxStore>(static sp => sp.GetRequiredKeyedService<IOutboxStore>("default"));

		services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("elasticsearch"));
		services.AddKeyedSingleton<IOutboxStoreAdmin>("elasticsearch", (sp, _) => sp.GetRequiredService<ElasticsearchOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStoreAdmin>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStoreAdmin>("elasticsearch"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use Elasticsearch outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseElasticsearchOutboxStore(
		this IDispatchBuilder builder,
		Action<ElasticsearchOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddElasticsearchOutboxStore(configure);

		return builder;
	}
}
