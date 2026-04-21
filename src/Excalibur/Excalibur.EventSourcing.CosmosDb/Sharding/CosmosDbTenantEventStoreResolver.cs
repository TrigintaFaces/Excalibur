// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.CosmosDb.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for Cosmos DB.
/// </summary>
/// <remarks>
/// Each shard gets its own <see cref="CosmosClient"/> and database, routed via
/// <see cref="ShardInfo.DatabaseName"/> or <see cref="ShardInfo.IndexPrefix"/> (container).
/// </remarks>
internal sealed class CosmosDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly CosmosDbEventStoreOptions _defaultOptions;
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);

	internal CosmosDbTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<CosmosDbEventStoreOptions> defaultOptions)
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
		var cosmosClient = new CosmosClient(shardInfo.ConnectionString);
		var options = Options.Create(new CosmosDbEventStoreOptions
		{
			EventsContainerName = shardInfo.IndexPrefix ?? _defaultOptions.EventsContainerName,
			PartitionKeyPath = _defaultOptions.PartitionKeyPath,
			UseTransactionalBatch = _defaultOptions.UseTransactionalBatch,
			CreateContainerIfNotExists = _defaultOptions.CreateContainerIfNotExists,
		});

		return new CosmosDbEventStore(
			cosmosClient,
			options,
			_loggerFactory.CreateLogger<CosmosDbEventStore>());
	}
}
