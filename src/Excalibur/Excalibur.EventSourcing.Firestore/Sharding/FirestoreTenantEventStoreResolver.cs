// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Firestore.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for Firestore.
/// </summary>
/// <remarks>
/// Each shard gets its own <see cref="FirestoreDb"/> instance, with the project/database
/// derived from <see cref="ShardInfo.DatabaseName"/>.
/// </remarks>
internal sealed class FirestoreTenantEventStoreResolver : ITenantStoreResolver<IEventStore>
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly FirestoreEventStoreOptions _defaultOptions;
	private readonly ITenantContext _tenantContext;
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);

	internal FirestoreTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<FirestoreEventStoreOptions> defaultOptions,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(defaultOptions);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_tenantContext = tenantContext;

		_shardMap = shardMap;
		_loggerFactory = loggerFactory;
		_defaultOptions = defaultOptions.Value;
	}

	/// <inheritdoc />
	public IEventStore Resolve(string tenantId)
	{
		var shardInfo = _shardMap.GetShardInfo(tenantId);
		return _storeCache.GetOrAdd(shardInfo.ShardId, _ => CreateStore(shardInfo));
	}

	private IEventStore CreateStore(ShardInfo shardInfo)
	{
		var projectId = shardInfo.RequireCoordinate(shardInfo.DatabaseName, nameof(ShardInfo.DatabaseName));
		var db = FirestoreDb.Create(projectId);

		var options = Options.Create(new FirestoreEventStoreOptions
		{
			ProjectId = projectId,
			EventsCollectionName = _defaultOptions.EventsCollectionName,
		});

		// A shard may host more than one tenant, so the store still composes the ambient tenant into its
		// key. Routing to a shard is the physical half of confinement; the key is the logical half, and a
		// co-located pair of tenants needs both.
		return new FirestoreEventStore(
			db,
			options,
			_loggerFactory.CreateLogger<FirestoreEventStore>(),
			_tenantContext);
	}
}
