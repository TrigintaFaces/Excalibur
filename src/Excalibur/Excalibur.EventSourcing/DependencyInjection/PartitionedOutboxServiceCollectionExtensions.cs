// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Outbox;
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
}
