// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Data;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;
using MongoDB.Bson;

namespace Excalibur.EventSourcing.MongoDB.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for MongoDB.
/// </summary>
/// <remarks>
/// Each shard gets its own <see cref="IMongoClient"/>, with the database selected
/// from <see cref="ShardInfo.DatabaseName"/>.
/// </remarks>
[NoTenantTerm(TenantConfinement.EstateWide, "Physical-shard readiness performs a bounded native namespace read for an operator; it selects at most one record identifier, discards it, and does not select event payloads or establish tenant confinement.")]
internal sealed class MongoDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, ITenantShardReadinessProbe, IAsyncDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly TenantShardBindingCache _bindings = new();
	private readonly Func<ShardInfo, ShardReadinessBinding> _bindingFactory;
	private readonly ILoggerFactory _loggerFactory;
	private readonly MongoDbEventStoreOptions _defaultOptions;
	private readonly ISerializer? _serializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly ITenantContext _tenantContext;

	internal MongoDbTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<MongoDbEventStoreOptions> defaultOptions,
		ITenantContext tenantContext,
		ISerializer? serializer,
		IPayloadSerializer? payloadSerializer)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(defaultOptions);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_tenantContext = tenantContext;
		_shardMap = shardMap;
		_bindingFactory = CreateBinding;
		_loggerFactory = loggerFactory;
		_defaultOptions = defaultOptions.Value;
		_serializer = serializer;
		_payloadSerializer = payloadSerializer;
	}

	/// <inheritdoc />
	public IEventStore Resolve(string tenantId)
	{

		var shardInfo = _shardMap.GetShardInfo(tenantId);
		return GetBinding(shardInfo).Store;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => _bindings.DisposeAsync();

	/// <inheritdoc />
	public Task ProbeShardAsync(string shardId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return GetBinding(_shardMap.GetShardById(shardId)).Probe(cancellationToken);
	}

	private ShardReadinessBinding GetBinding(ShardInfo shardInfo) => _bindings.Get(shardInfo, _bindingFactory);


	private ShardReadinessBinding CreateBinding(ShardInfo shardInfo) => ShardReadinessBinding.Construct(resources =>
	{
		var options = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = shardInfo.ConnectionString,
			DatabaseName = shardInfo.RequireCoordinate(shardInfo.DatabaseName, nameof(ShardInfo.DatabaseName)),
			CollectionName = _defaultOptions.CollectionName,
		});

		options.Value.Validate();
		_ = new CollectionNamespace(new DatabaseNamespace(options.Value.DatabaseName), options.Value.CollectionName);
		var client = resources.Own(new MongoClient(shardInfo.ConnectionString));

		// A shard may host more than one tenant, so the store still composes the ambient tenant into its
		// stored stream id. Routing to a shard is the physical half of confinement; the key is the logical
		// half, and a co-located pair of tenants needs both.
		var collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<BsonDocument>(options.Value.CollectionName);
		var store = new MongoDbEventStore(
			client,
			options,
			_loggerFactory.CreateLogger<MongoDbEventStore>(),
			_tenantContext,
			_serializer,
			_payloadSerializer);

		return resources.Complete(store, async token =>
		{
			using var cursor = await collection.FindAsync(Builders<BsonDocument>.Filter.Empty,
				new FindOptions<BsonDocument> { Limit = 1, BatchSize = 1, Projection = Builders<BsonDocument>.Projection.Include("_id") }, token).ConfigureAwait(false);
			_ = await cursor.MoveNextAsync(token).ConfigureAwait(false);
		});
	});
}
