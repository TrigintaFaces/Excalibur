// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DynamoDb.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for DynamoDB.
/// </summary>
/// <remarks>
/// <para>
/// Each shard gets its own <see cref="IAmazonDynamoDB"/> client. Table name prefix
/// is derived from <see cref="ShardInfo.IndexPrefix"/>.
/// </para>
/// <para>
/// The resolver constructs those SDK clients itself, so it owns their lifetime: disposing the
/// resolver disposes every store and every client it created, releasing the per-client connection
/// pool and its sockets. A host that never disposed the resolver would hold one DynamoDB client and
/// one Streams client per shard open for the life of the process.
/// </para>
/// </remarks>
internal sealed class DynamoDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, IAsyncDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly DynamoDbEventStoreOptions _defaultOptions;
	private readonly ITenantContext _tenantContext;
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);

	// Everything CreateStore has ever built, not merely what won the cache slot. ConcurrentDictionary
	// documents its factory as callable more than once for the same key under contention, so a losing
	// racer's store and clients are dropped from the cache while still holding their sockets. Recording
	// them at construction is what makes disposal total rather than "whatever the cache happens to hold".
	private readonly ConcurrentBag<IEventStore> _createdStores = [];
	private readonly ConcurrentBag<IDisposable> _createdClients = [];
	private volatile bool _disposed;

	internal DynamoDbTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<DynamoDbEventStoreOptions> defaultOptions,
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
		_storeCache.Clear();

		// Stores first: a store may use its client while shutting down, and the client must outlive that.
		while (_createdStores.TryTake(out var store))
		{
			switch (store)
			{
				case IAsyncDisposable asyncDisposable:
					await asyncDisposable.DisposeAsync().ConfigureAwait(false);
					break;
				case IDisposable disposable:
					disposable.Dispose();
					break;
				default:
					break;
			}
		}

		while (_createdClients.TryTake(out var client))
		{
			client.Dispose();
		}
	}

	private IEventStore CreateStore(ShardInfo shardInfo)
	{
		var config = new AmazonDynamoDBConfig();
		if (shardInfo.Region is not null)
		{
			config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(shardInfo.Region);
		}

		var client = new AmazonDynamoDBClient(config);
		_createdClients.Add(client);

		var streamsConfig = new AmazonDynamoDBStreamsConfig();
		if (shardInfo.Region is not null)
		{
			streamsConfig.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(shardInfo.Region);
		}

		var streamsClient = new AmazonDynamoDBStreamsClient(streamsConfig);
		_createdClients.Add(streamsClient);

		var tablePrefix = shardInfo.RequireCoordinate(shardInfo.IndexPrefix, nameof(ShardInfo.IndexPrefix));
		var options = Options.Create(new DynamoDbEventStoreOptions
		{
			EventsTableName = $"{tablePrefix}{_defaultOptions.EventsTableName}",
		});

		// A shard may host more than one tenant, so the store still composes the ambient tenant into its
		// partition key. Routing to a shard is the physical half of confinement; the key is the logical
		// half, and a co-located pair of tenants needs both.
		var store = new DynamoDbEventStore(
			client,
			streamsClient,
			options,
			_loggerFactory.CreateLogger<DynamoDbEventStore>(),
			_tenantContext);

		_createdStores.Add(store);
		return store;
	}
}
