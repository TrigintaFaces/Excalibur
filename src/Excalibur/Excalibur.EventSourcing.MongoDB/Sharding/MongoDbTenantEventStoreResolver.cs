// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch.Serialization;

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
internal sealed class MongoDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, IAsyncDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly MongoDbEventStoreOptions _defaultOptions;
	private readonly ISerializer? _serializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, IMongoClient> _clientCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

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
		ObjectDisposedException.ThrowIf(_disposed, this);

		var shardInfo = _shardMap.GetShardInfo(tenantId);
		return _storeCache.GetOrAdd(shardInfo.ShardId, _ => CreateStore(shardInfo));
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Dispose all cached stores
		foreach (var store in _storeCache.Values)
		{
			if (store is IAsyncDisposable asyncDisposable)
			{
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
			}
			else if (store is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}

		_storeCache.Clear();

		// Dispose all cached MongoClients (MongoClient implements IDisposable in v3)
		foreach (var client in _clientCache.Values)
		{
			if (client is IDisposable disposableClient)
			{
				disposableClient.Dispose();
			}
		}

		_clientCache.Clear();
	}

	private IEventStore CreateStore(ShardInfo shardInfo)
	{
		var client = _clientCache.GetOrAdd(shardInfo.ShardId, _ => new MongoClient(shardInfo.ConnectionString));
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
