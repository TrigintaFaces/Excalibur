// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Transactions;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres.Requests;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>Observes event state on an owned connection, independently of ambient transactions.</summary>
/// <remarks>
/// The source must be the one bound to the writer/global reader. Routing must preserve logical store
/// identity and must not serve cached query results. The role check rejects replicas, not misconfigured
/// writable copies. A null scope is reserved for the provider's internal estate-wide composition.
/// </remarks>
internal sealed class PostgresAuthoritativeEventReader : IEventStoreAuthoritativeReader
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly Lazy<string> _qualifiedTable;
	private readonly Func<KeyedTenantPartition>? _authorizedTenant;

	internal PostgresAuthoritativeEventReader(NpgsqlDataSource dataSource, string schema, string table,
		Func<KeyedTenantPartition>? authorizedTenant)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		_dataSource = dataSource;
		_qualifiedTable = new Lazy<string>(() => PgTableName.Format(schema, table));
		_authorizedTenant = authorizedTenant;
	}

	internal static PostgresAuthoritativeEventReader CreateConfined(NpgsqlDataSource dataSource,
		string schema, string table, ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);
		return new PostgresAuthoritativeEventReader(dataSource, schema, table,
			() => KeyedTenantPartition.FromContext(tenantContext));
	}

	public async ValueTask<EventStoreEventState?> ReadCurrentAsync(KeyedTenantPartition tenant,
		string aggregateId, string aggregateType, string eventId, long version, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentOutOfRangeException.ThrowIfNegative(version);
		cancellationToken.ThrowIfCancellationRequested();
		if (_authorizedTenant is not null
			&& !string.Equals(_authorizedTenant().TenantId, tenant.TenantId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The requested event is outside the reader's authorized tenant.");
		}
		var qualifiedTable = _qualifiedTable.Value;

		// Suppression spans opening, reading and disposing the connection. Never enlist into a caller's
		// older snapshot. Explicit isolation also overrides a custom session's default isolation level.
		using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(
			System.Data.IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema/table validated by PgTableName.Format; event locator is parameterized.
		var sql = $"""
			SELECT pg_catalog.pg_is_in_recovery() AS IsReplica,
			       e.event_id AS EventId, e.aggregate_id AS AggregateId, e.aggregate_type AS AggregateType,
			       e.event_type AS EventType, e.version AS Version, e.position AS GlobalPosition,
			       e.timestamp AS "Timestamp", e.archived_at AS ArchivedAt,
			       CASE WHEN e.event_id IS NULL THEN NULL ELSE COALESCE(e.tenant_id, @Untenanted) END AS TenantId
			FROM (VALUES (1)) AS anchor(value)
			LEFT JOIN {qualifiedTable} AS e
			  ON e.aggregate_id = @AggregateId AND e.aggregate_type = @AggregateType
			 AND e.event_id = @EventId AND e.version = @Version
			 AND COALESCE(e.tenant_id, @Untenanted) = @TenantId
			""";
#pragma warning restore CA2100
		// One statement returns the authority flag even for an absent event. A separate role probe
		// could be routed to a different backend by a statement-balancing database proxy.
		var row = await connection.QuerySingleAsync<StateRow>(new CommandDefinition(sql,
			new { AggregateId = aggregateId, AggregateType = aggregateType, EventId = eventId, Version = version,
				TenantId = tenant.TenantId, Untenanted = KeyedTenantPartition.Untenanted.TenantId },
			transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		if (row.IsReplica)
		{
			throw new InvalidOperationException("An authoritative event recheck cannot use a recovering replica.");
		}
		if (row.EventId is null)
		{
			return null;
		}
		if (row.TenantId is null || row.AggregateId is null || row.AggregateType is null
			|| row.AggregateId != aggregateId || row.AggregateType != aggregateType
			|| row.EventId != eventId || row.Version != version || row.GlobalPosition is null or < 0
			|| row.Timestamp is null || string.IsNullOrWhiteSpace(row.EventType)
			|| !string.Equals(row.TenantId, tenant.TenantId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The authoritative event state has invalid recorded identity.");
		}
		return new EventStoreEventState(KeyedTenantPartition.FromStoredValue(row.TenantId), row.AggregateId,
			row.AggregateType, row.EventId, row.Version.Value, row.GlobalPosition.Value, row.EventType,
			row.Timestamp.Value, row.ArchivedAt);
	}

	private sealed record StateRow(bool IsReplica, string? EventId, string? AggregateId, string? AggregateType,
		string? EventType, long? Version, long? GlobalPosition, DateTime? Timestamp, DateTime? ArchivedAt, string? TenantId);
}
