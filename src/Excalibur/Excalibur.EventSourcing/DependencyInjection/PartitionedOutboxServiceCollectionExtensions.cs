// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring partitioned outbox processing.
/// </summary>
public static class PartitionedOutboxServiceCollectionExtensions
{
	/// <summary>
	/// Enables partitioned outbox processing for improved throughput.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure partitioning options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IEventSourcingBuilder UsePartitionedOutbox(
		this IEventSourcingBuilder builder,
		Action<OutboxPartitionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new OutboxPartitionOptions();
		configure(options);

		if (options.Strategy == OutboxPartitionStrategy.None)
		{
			return builder;
		}

		builder.Services.Configure(configure);
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxPartitionOptions>, OutboxPartitionOptionsValidator>());
		builder.Services.AddOptionsWithValidateOnStart<OutboxPartitionOptions>();

		// Register appropriate partitioner based on strategy
		switch (options.Strategy)
		{
			case OutboxPartitionStrategy.ByTenantHash:
				builder.Services.TryAddSingleton<IOutboxPartitioner>(
					new HashOutboxPartitioner(options.PartitionCount));
				break;

			case OutboxPartitionStrategy.PerShard:
				// PerShard resolves ITenantShardMap from DI at runtime to build
				// the shard-to-partition mapping. Requires EnableTenantSharding
				// and ShardIds configured in OutboxPartitionOptions.
				builder.Services.TryAddSingleton<IOutboxPartitioner>(sp =>
				{
					var shardMap = sp.GetRequiredService<ITenantShardMap>();
					return new ShardOutboxPartitioner(shardMap, options.ShardIds);
				});
				break;
		}

		// Register the partitioned processor background service
		builder.Services.AddHostedService<PartitionedOutboxProcessorService>();

		return builder;
	}

	/// <summary>
	/// Enables partitioned outbox processing for improved throughput,
	/// with options bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configuration">The configuration section to bind <see cref="OutboxPartitionOptions"/> from.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Unlike the <see cref="Action{T}"/>-based overload, this always registers the
	/// partitioned processor. The <see cref="OutboxPartitionOptions.Strategy"/> is
	/// resolved at runtime from bound configuration. Use <c>ValidateOnStart</c> to
	/// catch misconfiguration early.
	/// </para>
	/// <para>
	/// The <see cref="IOutboxPartitioner"/> is resolved at runtime based on the bound
	/// <see cref="OutboxPartitionOptions.Strategy"/> value.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IEventSourcingBuilder UsePartitionedOutbox(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		builder.Services.AddOptions<OutboxPartitionOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxPartitionOptions>, OutboxPartitionOptionsValidator>());

		// Register the partitioner using deferred resolution based on bound options
		builder.Services.TryAddSingleton<IOutboxPartitioner>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<OutboxPartitionOptions>>().Value;
			return options.Strategy switch
			{
				OutboxPartitionStrategy.ByTenantHash =>
					new HashOutboxPartitioner(options.PartitionCount),
				OutboxPartitionStrategy.PerShard =>
					new ShardOutboxPartitioner(
						sp.GetRequiredService<ITenantShardMap>(),
						options.ShardIds),
				_ => new HashOutboxPartitioner(options.PartitionCount),
			};
		});

		// Register the partitioned processor background service
		builder.Services.AddHostedService<PartitionedOutboxProcessorService>();

		return builder;
	}
}
