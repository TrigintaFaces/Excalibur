// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Postgres.DependencyInjection;
using MsOptions = Microsoft.Extensions.Options.Options;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;

using Npgsql;
using Excalibur.EventSourcing.Postgres.Requests;

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
internal sealed class PostgresTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, ITenantShardReadinessProbe, IAsyncDisposable
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
		var schema = shardInfo.RequireCoordinate(shardInfo.SchemaName, nameof(ShardInfo.SchemaName));
		_ = PgTableName.Format(schema, "events");
		_ = new NpgsqlConnectionStringBuilder(shardInfo.ConnectionString);
		var dataSource = resources.Own(NpgsqlDataSource.Create(shardInfo.ConnectionString));

		var reader = new PostgresGlobalStreamQuery(dataSource,
			MsOptions.Create(new PostgresEventSourcingOptions { EventStoreSchema = schema, EventStoreTable = "events" }));
		var store = new PostgresEventStore(
				dataSource,
				_loggerFactory.CreateLogger<PostgresEventStore>(),
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
