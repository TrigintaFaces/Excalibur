// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;

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
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);

	internal FirestoreTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<FirestoreEventStoreOptions> defaultOptions)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(defaultOptions);

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
		var projectId = shardInfo.DatabaseName ?? _defaultOptions.ProjectId;
		var db = FirestoreDb.Create(projectId);

		var options = Options.Create(new FirestoreEventStoreOptions
		{
			ProjectId = projectId,
			EventsCollectionName = _defaultOptions.EventsCollectionName,
		});

		return new FirestoreEventStore(
			db,
			options,
			_loggerFactory.CreateLogger<FirestoreEventStore>());
	}
}
