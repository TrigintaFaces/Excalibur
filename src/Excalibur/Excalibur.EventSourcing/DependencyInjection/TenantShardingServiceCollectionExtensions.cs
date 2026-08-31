// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Sharding;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring tenant-aware data sharding on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class TenantShardingServiceCollectionExtensions
{
	/// <summary>
	/// Enables tenant-aware data sharding for event stores and projection stores.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure shard map options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, <see cref="IEventStore"/> and <see cref="IProjectionStore{TProjection}"/>
	/// are re-registered as Scoped services that route to the correct shard based on
	/// the current tenant (via <see cref="Excalibur.Dispatch.ITenantContext"/>).
	/// </para>
	/// <para>
	/// Consumers must also register an <see cref="ITenantShardMap"/> implementation
	/// (e.g., <c>InMemoryTenantShardMap</c>) and provider-specific
	/// <see cref="ITenantStoreResolver{TStore}"/> implementations.
	/// </para>
	/// <para>
	/// <b>Registration semantics:</b> calling this method <b>replaces any previously
	/// registered <see cref="IEventStore"/></b> with <see cref="TenantRoutingEventStore"/>.
	/// Tenant routing is a whole-cloth replacement (not a wrapping decorator) because
	/// <see cref="TenantRoutingEventStore"/> resolves the correct store per tenant via
	/// <see cref="ITenantStoreResolver{TStore}"/>. If your host needs a single-tenant
	/// <see cref="IEventStore"/> alongside sharding, do not call this method — use
	/// provider-specific extensions directly.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(builder =&gt;
	/// {
	///     builder.EnableTenantSharding(options =&gt;
	///     {
	///         options.DefaultShardId = "shard-default";
	///     });
	/// }));
	/// </code>
	/// </example>
	public static IEventSourcingBuilder EnableTenantSharding(
		this IEventSourcingBuilder builder,
		Action<ShardMapOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Calling this method is the opt-in: sharding is always enabled when the
		// fluent extension is invoked.
		builder.Services.Configure(configure);
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ShardMapOptions>, ShardMapOptionsValidator>());
		builder.Services.AddOptionsWithValidateOnStart<ShardMapOptions>();

		// Register tenant-routing decorator as Scoped (per-request tenant resolution).
		// Idempotent: if EnableTenantSharding is invoked more than once, the second call returns
		// without touching the collection rather than re-registering TenantRoutingEventStore.
		RegisterTenantRoutingStores(builder.Services);

		return builder;
	}

	/// <summary>
	/// Enables tenant-aware data sharding for event stores and projection stores,
	/// with options bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configuration">The configuration section to bind <see cref="ShardMapOptions"/> from.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Calling this method is the opt-in: sharding is always enabled when the
	/// extension is invoked. To avoid registering the tenant-routing decorator,
	/// do not call this method.
	/// </para>
	/// <para>
	/// <b>Registration semantics:</b> calling this method <b>replaces any previously
	/// registered <see cref="IEventStore"/></b> with <see cref="TenantRoutingEventStore"/>.
	/// Tenant routing is a whole-cloth replacement (not a wrapping decorator) because
	/// <see cref="TenantRoutingEventStore"/> resolves the correct store per tenant via
	/// <see cref="ITenantStoreResolver{TStore}"/>. If your host needs a single-tenant
	/// <see cref="IEventStore"/> alongside sharding, do not call this method — use
	/// provider-specific extensions directly.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(builder =&gt;
	/// {
	///     builder.EnableTenantSharding(configuration.GetSection("TenantSharding"));
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IEventSourcingBuilder EnableTenantSharding(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		builder.Services.AddOptions<ShardMapOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ShardMapOptions>, ShardMapOptionsValidator>());

		// Register tenant-routing decorator as Scoped (per-request tenant resolution).
		// Idempotent: if EnableTenantSharding is invoked more than once, the second call returns
		// without touching the collection rather than re-registering TenantRoutingEventStore.
		RegisterTenantRoutingStores(builder.Services);

		return builder;
	}

	/// <summary>
	/// Registers <see cref="TenantRoutingEventStore"/> as the
	/// <see cref="IEventStore"/> implementation, removing any prior registration
	/// (including a previous <see cref="TenantRoutingEventStore"/> — which makes
	/// repeated calls a no-op rather than a double-registration).
	/// </summary>
	/// <remarks>
	/// The single tenant-routing wiring point, shared by <see cref="EnableTenantSharding(IEventSourcingBuilder, Action{ShardMapOptions})"/>
	/// (the event-sourcing builder seam) and <c>AddMultiTenancy</c> with the sharding strategy (the
	/// <see cref="IServiceCollection"/> composition seam), so the two do not fork the routing registration.
	/// Callers are responsible for the shard-map options, <c>ITenantShardMap</c>, and
	/// provider-specific <c>ITenantStoreResolver{TStore}</c> registrations this decorator resolves at runtime.
	/// </remarks>
	/// <param name="services">The service collection to register tenant routing into.</param>
	internal static void RegisterTenantRoutingStores(IServiceCollection services)
	{
		// Idempotence guard — if this method has already wired tenant routing, do nothing, so a
		// repeat call neither double-registers nor churns the wiring it already installed.
		//
		// The guard keys on the CONCRETE TenantRoutingEventStore registration, not on the
		// implementation type of the IEventStore descriptor. The latter is what this guard used to
		// read, and it could never match: the IEventStore descriptor below is a FACTORY forwarding
		// to the concrete singleton, and ServiceDescriptor.ImplementationType is null for a factory
		// registration no matter which generic arguments name it. So the guard never fired, every
		// repeat call fell through to the removal loop, and the IEventStore and capability
		// descriptors were torn out and re-appended on each invocation — no duplicates, but not the
		// no-op this method documents, and a positional idempotence check reads those re-appended
		// descriptors as fresh additions.
		//
		// The concrete service type is registered by this method and nowhere else in the framework,
		// in the same act as the contract forward below, so its presence is exactly the fact the
		// guard needs: tenant routing is already wired here.
		for (var i = 0; i < services.Count; i++)
		{
			if (services[i].ServiceType == typeof(TenantRoutingEventStore))
			{
				return;
			}
		}

		// Replace any prior IEventStore descriptor. Tenant routing is a
		// whole-cloth replacement (routes via ITenantStoreResolver<IEventStore>
		// rather than wrapping a single inner store), so no prior descriptor
		// needs to be captured.
		//
		// The prior store's tenancy capability markers go with it, and that is not tidiness. A marker is an
		// assertion about a specific registered store, so leaving one behind after deleting the store it
		// described leaves an attestation with nothing under it -- and the tenant-capability gates read
		// exactly these markers. A host that composed multi-tenancy first (which decorates the provider
		// store and emits its marker) and enabled sharding second would otherwise reach a started host whose
		// IEventStore is the routing store while the container still swears the deleted decorator's
		// guarantee. Removing them here makes that state unreachable rather than merely detectable; the
		// routing store then presents its own, below.
		for (var i = services.Count - 1; i >= 0; i--)
		{
			var serviceType = services[i].ServiceType;
			if (serviceType == typeof(IEventStore)
				|| serviceType == typeof(ITenantScopingCapability<IEventStore>)
				|| serviceType == typeof(ITenantPartitionedCapability<IEventStore>))
			{
				services.RemoveAt(i);
			}
		}

		// Registered through the same door every other tenant-aware store uses, rather than by registering
		// the store here and its capability marker separately. That is the whole point: the seam derives the
		// mechanism from this store's own constructor -- it takes ITenantContext, reads TenantId, and refuses
		// when none is established -- and emits the matching marker in the same act. A marker that could be
		// registered on its own is an attestation nothing structurally ties to the wiring it describes, which
		// is how a store ends up advertising a guarantee it does not implement. Here that is inexpressible.
		//
		// Without an attestation a sharding host would be a multi-tenant host whose IEventStore attests
		// nothing, and the tenant-capability gate would correctly refuse to start it.
		_ = services.AddTenantAwareStore<IEventStore, TenantRoutingEventStore>();

		// The seam registers the CONCRETE store (singleton; both of its dependencies are singletons). The
		// contract is forwarded onto it, keeping IEventStore scoped exactly as before. This is a factory
		// descriptor, so it carries no readable implementation type — which is why the idempotence guard
		// at the top of this method keys on the concrete registration the line above emits rather than on
		// anything read off this descriptor.
		services.Add(ServiceDescriptor.Scoped<IEventStore, TenantRoutingEventStore>(
			static sp => sp.GetRequiredService<TenantRoutingEventStore>()));

		// Route ISagaStore the same way -- but only when a saga provider is actually registered.
		// Unlike IEventStore (always present in an event-sourcing host), sagas are optional: a
		// sharding host with no saga provider must not be forced to carry saga-routing wiring or an
		// unsatisfiable ITenantStoreResolver<ISagaStore> requirement. HasStoreRegistrationFor is the
		// same presence predicate MultiTenancyServiceCollectionExtensions already uses for this exact
		// purpose under RowDiscriminator.
		if (services.HasStoreRegistrationFor(typeof(ISagaStore)))
		{
			for (var i = services.Count - 1; i >= 0; i--)
			{
				var serviceType = services[i].ServiceType;
				if (serviceType == typeof(ISagaStore)
					|| serviceType == typeof(ITenantScopingCapability<ISagaStore>)
					|| serviceType == typeof(ITenantPartitionedCapability<ISagaStore>))
				{
					services.RemoveAt(i);
				}
			}

			_ = services.AddTenantAwareStore<ISagaStore, TenantRoutingSagaStore>();

			services.Add(ServiceDescriptor.Scoped<ISagaStore, TenantRoutingSagaStore>(
				static sp => sp.GetRequiredService<TenantRoutingSagaStore>()));
		}

		// Mark tenant-sharding as active so the event-store erasure startup gate can fail closed on the
		// (currently unsupported) sharding + erasure composition. Both sharding entry points route through
		// this method (EnableTenantSharding and AddMultiTenancy's sharding strategy), so the marker covers
		// every way sharding is enabled; a non-sharding host never registers it and is unaffected.
		services.TryAddSingleton<Excalibur.EventSourcing.Sharding.TenantShardingActiveMarker>();
	}
}
