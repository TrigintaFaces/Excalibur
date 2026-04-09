// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Outbox;
using Excalibur.Outbox.Partitioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring partitioned outbox processing on <see cref="IOutboxBuilder"/>.
/// </summary>
public static class PartitionedOutboxBuilderExtensions
{
	/// <summary>
	/// Enables partitioned outbox processing for improved throughput.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure partitioning options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IOutboxBuilder UsePartitionedProcessing(
		this IOutboxBuilder builder,
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
				builder.Services.TryAddSingleton<IOutboxPartitioner>(sp =>
				{
					var shardMap = sp.GetRequiredService<ITenantShardMap>();
					return new ShardOutboxPartitioner(shardMap, options.ShardIds);
				});
				break;
		}

		// The OutboxBackgroundService detects IOutboxPartitioner via DI and
		// automatically runs N parallel processor loops when registered.

		return builder;
	}

	/// <summary>
	/// Enables partitioned outbox processing with options bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configuration">The configuration section to bind <see cref="OutboxPartitionOptions"/> from.</param>
	/// <returns>The builder for fluent chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IOutboxBuilder UsePartitionedProcessing(
		this IOutboxBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		builder.Services.AddOptions<OutboxPartitionOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OutboxPartitionOptions>, OutboxPartitionOptionsValidator>());

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

		// The OutboxBackgroundService detects IOutboxPartitioner via DI and
		// automatically runs N parallel processor loops when registered.

		return builder;
	}
}
