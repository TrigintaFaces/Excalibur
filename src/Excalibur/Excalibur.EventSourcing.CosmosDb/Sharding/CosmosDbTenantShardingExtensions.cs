// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.CosmosDb.Sharding;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB tenant shard providers.
/// </summary>
public static class CosmosDbTenantShardingExtensions
{
	/// <summary>
	/// Registers the Cosmos DB <see cref="ITenantStoreResolver{TStore}"/> for
	/// <see cref="IEventStore"/>, enabling tenant-aware event store routing
	/// to Cosmos DB databases/containers.
	/// </summary>
	public static IEventSourcingBuilder UseCosmosDbTenantEventStore(
		this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ITenantStoreResolver<IEventStore>>(sp =>
			new CosmosDbTenantEventStoreResolver(
				sp.GetRequiredService<ITenantShardMap>(),
				sp.GetRequiredService<ILoggerFactory>(),
				sp.GetRequiredService<IOptions<CosmosDbEventStoreOptions>>()));

		return builder;
	}
}
