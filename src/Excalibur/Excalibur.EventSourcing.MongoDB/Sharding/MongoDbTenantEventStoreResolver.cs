// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.EventSourcing.MongoDB.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for MongoDB.
/// </summary>
/// <remarks>
/// Each shard gets its own <see cref="IMongoClient"/>, with the database selected
/// from <see cref="ShardInfo.DatabaseName"/>.
/// </remarks>
internal sealed class MongoDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly MongoDbEventStoreOptions _defaultOptions;
	private readonly ISerializer? _serializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);

	internal MongoDbTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<MongoDbEventStoreOptions> defaultOptions,
		ISerializer? serializer,
		IPayloadSerializer? payloadSerializer)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(defaultOptions);

		_shardMap = shardMap;
		_loggerFactory = loggerFactory;
		_defaultOptions = defaultOptions.Value;
		_serializer = serializer;
		_payloadSerializer = payloadSerializer;
	}

	/// <inheritdoc />
	public IEventStore Resolve(string tenantId)
	{
		var shardInfo = _shardMap.GetShardInfo(tenantId);
		return _storeCache.GetOrAdd(shardInfo.ShardId, _ => CreateStore(shardInfo));
	}

	private IEventStore CreateStore(ShardInfo shardInfo)
	{
		var client = new MongoClient(shardInfo.ConnectionString);
		var options = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = shardInfo.ConnectionString,
			DatabaseName = shardInfo.DatabaseName ?? _defaultOptions.DatabaseName,
			CollectionName = _defaultOptions.CollectionName,
		});

		return new MongoDbEventStore(
			client,
			options,
			_loggerFactory.CreateLogger<MongoDbEventStore>(),
			_serializer,
			_payloadSerializer);
	}
}
