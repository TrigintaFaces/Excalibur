// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Excalibur.EventSourcing.SqlServer.Requests;
using MsOptions = Microsoft.Extensions.Options.Options;
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
internal sealed class SqlServerTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, ITenantShardReadinessProbe, IDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly TenantShardBindingCache _bindings = new();
	private readonly Func<ShardInfo, ShardReadinessBinding> _bindingFactory;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ISerializer? _serializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// The host's optional source-generated event type-info resolver, applied to every per-shard store
	/// this resolver builds so a sharded host is reflection-free on the same terms as a single-shard one.
	/// </summary>
	private readonly System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? _eventTypeInfoResolver;

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
		_bindingFactory = CreateBinding;
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
		return GetBinding(shardInfo).Store;
	}

	/// <inheritdoc />
	public void Dispose() => _bindings.Dispose();

	/// <inheritdoc />
	public Task ProbeShardAsync(string shardId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return GetBinding(_shardMap.GetShardById(shardId)).Probe(cancellationToken);
	}

	private ShardReadinessBinding GetBinding(ShardInfo shardInfo) => _bindings.Get(shardInfo, _bindingFactory);


	private ShardReadinessBinding CreateBinding(ShardInfo shardInfo) => ShardReadinessBinding.Construct(resources =>
	{
		var schema = shardInfo.RequireCoordinate(shardInfo.SchemaName, nameof(ShardInfo.SchemaName));
		var connectionString = shardInfo.ConnectionString;
		_ = SqlTableName.Format(schema, "EventStoreEvents");
		_ = new SqlConnectionStringBuilder(connectionString);

		var reader = new SqlServerGlobalStreamQuery(() => new SqlConnection(connectionString),
			MsOptions.Create(new SqlServerEventSourcingOptions { EventStoreSchema = schema, EventStoreTable = "EventStoreEvents" }));
		var store = new SqlServerEventStore(
				() => new SqlConnection(connectionString),
				_loggerFactory.CreateLogger<SqlServerEventStore>(),
				tenantContext: _tenantContext,
				internalSerializer: _serializer,
				payloadSerializer: _payloadSerializer,
				schema: schema,
				eventTypeInfoResolver: _eventTypeInfoResolver);

		return resources.Complete(store, async token =>
		{
			_ = await reader.ReadAllAsync(GlobalStreamPosition.Start, 1, token).ConfigureAwait(false);
		});
	});
}
