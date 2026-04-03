// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;
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
	/// the current tenant (via <see cref="Excalibur.Dispatch.Abstractions.ITenantId"/>).
	/// </para>
	/// <para>
	/// Consumers must also register an <see cref="ITenantShardMap"/> implementation
	/// (e.g., <c>InMemoryTenantShardMap</c>) and provider-specific
	/// <see cref="ITenantStoreResolver{TStore}"/> implementations.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =&gt;
	/// {
	///     builder.EnableTenantSharding(options =&gt;
	///     {
	///         options.EnableTenantSharding = true;
	///         options.DefaultShardId = "shard-default";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder EnableTenantSharding(
		this IEventSourcingBuilder builder,
		Action<ShardMapOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ShardMapOptions();
		configure(options);

		if (!options.EnableTenantSharding)
		{
			return builder;
		}

		builder.Services.Configure(configure);
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ShardMapOptions>, ShardMapOptionsValidator>());
		builder.Services.AddOptionsWithValidateOnStart<ShardMapOptions>();

		// Register tenant-routing decorators as Scoped (per-request tenant resolution)
		builder.Services.AddScoped<IEventStore, TenantRoutingEventStore>();

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
	/// Unlike the <see cref="Action{T}"/>-based overload, this always registers the
	/// tenant-routing decorator. Set <see cref="ShardMapOptions.EnableTenantSharding"/>
	/// to <see langword="false"/> in configuration to disable sharding at runtime
	/// via <c>ValidateOnStart</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =&gt;
	/// {
	///     builder.EnableTenantSharding(configuration.GetSection("TenantSharding"));
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder EnableTenantSharding(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		builder.Services.AddOptions<ShardMapOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ShardMapOptions>, ShardMapOptionsValidator>());

		// Register tenant-routing decorators as Scoped (per-request tenant resolution)
		builder.Services.AddScoped<IEventStore, TenantRoutingEventStore>();

		return builder;
	}
}
