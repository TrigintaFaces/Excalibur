// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// Store instances are cached per shard ID using a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// ADO.NET handles per-connection-string pooling, so creating one
/// <see cref="SqlServerEventStore"/> per shard is safe and lightweight.
/// </para>
/// </remarks>
internal sealed class SqlServerTenantEventStoreResolver : ITenantStoreResolver<IEventStore>
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

	internal SqlServerTenantEventStoreResolver(
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
		var shardInfo = _shardMap.GetShardInfo(tenantId);
		return _storeCache.GetOrAdd(shardInfo.ShardId, _ => CreateStore(shardInfo));
	}

	private IEventStore CreateStore(ShardInfo shardInfo)
	{
		var schema = shardInfo.RequireCoordinate(shardInfo.SchemaName, nameof(ShardInfo.SchemaName));
		var connectionString = shardInfo.ConnectionString;

		return new SqlServerEventStore(
				() => new SqlConnection(connectionString),
				_loggerFactory.CreateLogger<SqlServerEventStore>(),
				tenantContext: _tenantContext,
				internalSerializer: _serializer,
				payloadSerializer: _payloadSerializer,
				schema: schema,
				eventTypeInfoResolver: _eventTypeInfoResolver);
	}
}
