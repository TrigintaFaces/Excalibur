// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Firestore;
using Excalibur.EventSourcing.Firestore.Sharding;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Firestore tenant shard providers.
/// </summary>
public static class FirestoreTenantShardingExtensions
{
	/// <summary>
	/// Registers the Firestore <see cref="ITenantStoreResolver{TStore}"/> for
	/// <see cref="IEventStore"/>, enabling tenant-aware event store routing
	/// to Firestore databases.
	/// </summary>
	public static IEventSourcingBuilder UseFirestoreTenantEventStore(
		this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ITenantStoreResolver<IEventStore>>(sp =>
			new FirestoreTenantEventStoreResolver(
				sp.GetRequiredService<ITenantShardMap>(),
				sp.GetRequiredService<ILoggerFactory>(),
				sp.GetRequiredService<IOptions<FirestoreEventStoreOptions>>()));

		return builder;
	}
}
