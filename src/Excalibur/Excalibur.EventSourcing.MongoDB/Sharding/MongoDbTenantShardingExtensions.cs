// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.MongoDB;
using Excalibur.EventSourcing.MongoDB.Sharding;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB tenant shard providers.
/// </summary>
public static class MongoDbTenantShardingExtensions
{
	/// <summary>
	/// Registers the MongoDB <see cref="ITenantStoreResolver{TStore}"/> for
	/// <see cref="IEventStore"/>, enabling tenant-aware event store routing
	/// to MongoDB databases.
	/// </summary>
	public static IEventSourcingBuilder UseMongoDbTenantEventStore(
		this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ITenantStoreResolver<IEventStore>>(sp =>
			new MongoDbTenantEventStoreResolver(
				sp.GetRequiredService<ITenantShardMap>(),
				sp.GetRequiredService<ILoggerFactory>(),
				sp.GetRequiredService<IOptions<MongoDbEventStoreOptions>>(),
				sp.GetService<ISerializer>(),
				sp.GetService<IPayloadSerializer>()));

		return builder;
	}
}
