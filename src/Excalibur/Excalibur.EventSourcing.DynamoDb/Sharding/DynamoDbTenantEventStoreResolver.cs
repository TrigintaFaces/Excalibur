// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBStreams;

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DynamoDb.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for DynamoDB.
/// </summary>
/// <remarks>
/// Each shard gets its own <see cref="IAmazonDynamoDB"/> client. Table name prefix
/// is derived from <see cref="ShardInfo.IndexPrefix"/>.
/// </remarks>
internal sealed class DynamoDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly DynamoDbEventStoreOptions _defaultOptions;
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);

	internal DynamoDbTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<DynamoDbEventStoreOptions> defaultOptions)
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
		var config = new AmazonDynamoDBConfig();
		if (shardInfo.Region is not null)
		{
			config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(shardInfo.Region);
		}

		var client = new AmazonDynamoDBClient(config);
		var streamsConfig = new AmazonDynamoDBStreamsConfig();
		if (shardInfo.Region is not null)
		{
			streamsConfig.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(shardInfo.Region);
		}

		var streamsClient = new AmazonDynamoDBStreamsClient(streamsConfig);

		var tablePrefix = shardInfo.IndexPrefix ?? "";
		var options = Options.Create(new DynamoDbEventStoreOptions
		{
			EventsTableName = $"{tablePrefix}{_defaultOptions.EventsTableName}",
		});

		return new DynamoDbEventStore(
			client,
			streamsClient,
			options,
			_loggerFactory.CreateLogger<DynamoDbEventStore>());
	}
}
