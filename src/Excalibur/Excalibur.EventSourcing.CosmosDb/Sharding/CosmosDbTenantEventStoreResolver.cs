// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.CosmosDb.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for Cosmos DB.
/// </summary>
/// <remarks>
/// <para>
/// Each shard gets its own <see cref="CosmosClient"/> and database, routed via
/// <see cref="ShardInfo.DatabaseName"/> or <see cref="ShardInfo.IndexPrefix"/> (container).
/// </para>
/// <para>
/// The resolver constructs those clients itself, so it owns their lifetime: disposing the resolver
/// disposes every store and every client it created, releasing the per-client connection pool and its
/// sockets. A host that never disposed the resolver would hold one Cosmos client per shard open for
/// the life of the process.
/// </para>
/// </remarks>
internal sealed class CosmosDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, IAsyncDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly CosmosDbEventStoreOptions _defaultOptions;
	private readonly ITenantContext _tenantContext;
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);

	// Everything CreateStore has ever built, not merely what won the cache slot. ConcurrentDictionary
	// documents its factory as callable more than once for the same key under contention, so a losing
	// racer's store and client are dropped from the cache while still holding their sockets. Recording
	// them at construction is what makes disposal total rather than "whatever the cache happens to hold".
	private readonly ConcurrentBag<IEventStore> _createdStores = [];
	private readonly ConcurrentBag<IDisposable> _createdClients = [];
	private volatile bool _disposed;

	internal CosmosDbTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<CosmosDbEventStoreOptions> defaultOptions,
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
		var cosmosClient = new CosmosClient(shardInfo.ConnectionString, new CosmosClientOptions { UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase } });
		_createdClients.Add(cosmosClient);

		var options = Options.Create(new CosmosDbEventStoreOptions
		{
			DatabaseName = _defaultOptions.DatabaseName,
			EventsContainerName = shardInfo.RequireCoordinate(shardInfo.IndexPrefix, nameof(ShardInfo.IndexPrefix)),
			PartitionKeyPath = _defaultOptions.PartitionKeyPath,
			UseTransactionalBatch = _defaultOptions.UseTransactionalBatch,
			CreateContainerIfNotExists = _defaultOptions.CreateContainerIfNotExists,
		});

		// A shard may host more than one tenant, so the store still composes the ambient tenant into its
		// key. Routing to a shard is the physical half of confinement; the key is the logical half, and a
		// co-located pair of tenants needs both.
		var store = new CosmosDbEventStore(
			cosmosClient,
			options,
			_loggerFactory.CreateLogger<CosmosDbEventStore>(),
			_tenantContext);

		_createdStores.Add(store);
		return store;
	}
}
