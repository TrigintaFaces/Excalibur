// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for Postgres.
/// </summary>
/// <remarks>
/// <para>
/// Store instances are cached per shard ID using a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Each shard gets its own <see cref="NpgsqlDataSource"/> which manages connection pooling
/// per the Npgsql recommended pattern.
/// </para>
/// </remarks>
internal sealed class PostgresTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, IAsyncDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ISerializer? _serializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// The host's optional source-generated event type-info resolver, applied to every per-shard store
	/// this resolver builds so a sharded host is reflection-free on the same terms as a single-shard one.
	/// </summary>
	private readonly System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? _eventTypeInfoResolver;
	private readonly ConcurrentDictionary<string, IEventStore> _storeCache = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, NpgsqlDataSource> _dataSourceCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	internal PostgresTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		ISerializer? serializer,
		IPayloadSerializer? payloadSerializer,
		ITenantContext tenantContext,
		System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? eventTypeInfoResolver = null)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_shardMap = shardMap;
		_loggerFactory = loggerFactory;
		_serializer = serializer;
		_payloadSerializer = payloadSerializer;
		_tenantContext = tenantContext;
		_eventTypeInfoResolver = eventTypeInfoResolver;
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

		foreach (var kvp in _dataSourceCache)
		{
			await kvp.Value.DisposeAsync().ConfigureAwait(false);
		}

		_dataSourceCache.Clear();
		_storeCache.Clear();
	}

	private IEventStore CreateStore(ShardInfo shardInfo)
	{
		var schema = shardInfo.RequireCoordinate(shardInfo.SchemaName, nameof(ShardInfo.SchemaName));
		var dataSource = NpgsqlDataSource.Create(shardInfo.ConnectionString);
		_dataSourceCache.TryAdd(shardInfo.ShardId, dataSource);

		return new PostgresEventStore(
				dataSource,
				_loggerFactory.CreateLogger<PostgresEventStore>(),
				tenantContext: _tenantContext,
				internalSerializer: _serializer,
				payloadSerializer: _payloadSerializer,
				schema: schema,
				eventTypeInfoResolver: _eventTypeInfoResolver);
	}
}
