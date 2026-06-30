// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Sharding;
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
	/// the current tenant (via <see cref="Excalibur.Dispatch.ITenantId"/>).
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
		// fluent extension is invoked. [bd-51k0mc]
		builder.Services.Configure(configure);
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ShardMapOptions>, ShardMapOptionsValidator>());
		builder.Services.AddOptionsWithValidateOnStart<ShardMapOptions>();

		// Register tenant-routing decorator as Scoped (per-request tenant resolution).
		// Idempotent: if EnableTenantSharding is invoked more than once, the second
		// call is a no-op rather than double-registering TenantRoutingEventStore.
		// [S792 bd-a38h4t]
		RegisterTenantRoutingEventStore(builder.Services);

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
		// Idempotent: if EnableTenantSharding is invoked more than once, the second
		// call is a no-op rather than double-registering TenantRoutingEventStore.
		// [S792 bd-a38h4t]
		RegisterTenantRoutingEventStore(builder.Services);

		return builder;
	}

	/// <summary>
	/// Registers <see cref="TenantRoutingEventStore"/> as the
	/// <see cref="IEventStore"/> implementation, removing any prior registration
	/// (including a previous <see cref="TenantRoutingEventStore"/> — which makes
	/// repeated <c>EnableTenantSharding</c> calls a no-op rather than a
	/// double-registration).
	/// </summary>
	private static void RegisterTenantRoutingEventStore(IServiceCollection services)
	{
		// Idempotence guard — if TenantRoutingEventStore is already the registered
		// IEventStore, do nothing. This prevents duplicate enumerable resolutions
		// and matches the spec-§5.2 idempotence pin.
		for (var i = 0; i < services.Count; i++)
		{
			var descriptor = services[i];
			if (descriptor.ServiceType == typeof(IEventStore)
				&& descriptor.GetImplementationType() == typeof(TenantRoutingEventStore))
			{
				return;
			}
		}

		// Replace any prior IEventStore descriptor. Tenant routing is a
		// whole-cloth replacement (routes via ITenantStoreResolver<IEventStore>
		// rather than wrapping a single inner store), so no prior descriptor
		// needs to be captured.
		for (var i = services.Count - 1; i >= 0; i--)
		{
			if (services[i].ServiceType == typeof(IEventStore))
			{
				services.RemoveAt(i);
			}
		}

		services.Add(ServiceDescriptor.Scoped<IEventStore, TenantRoutingEventStore>());
	}
}
