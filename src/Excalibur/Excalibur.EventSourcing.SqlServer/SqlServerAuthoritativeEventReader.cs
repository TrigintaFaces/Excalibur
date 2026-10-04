// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Transactions;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>Observes committed event state using an explicitly owned primary connection.</summary>
/// <remarks>
/// Each factory invocation must transfer a fresh, closed connection to the same logical primary store.
/// Routing must not serve cached results, and recovery must preserve acknowledged erasures. The role
/// check rejects read-only endpoints; it cannot identify writable copies or recover lost history.
/// A null authorization scope is reserved for the provider's internal estate-wide composition.
/// </remarks>
internal sealed class SqlServerAuthoritativeEventReader : IEventStoreAuthoritativeReader
{
	private readonly Func<SqlConnection> _ownedConnectionFactory;
	private readonly Lazy<string> _qualifiedTable;
	private readonly Func<KeyedTenantPartition>? _authorizedTenant;

	internal SqlServerAuthoritativeEventReader(Func<SqlConnection> ownedConnectionFactory,
		string schema, string table, Func<KeyedTenantPartition>? authorizedTenant)
	{
		ArgumentNullException.ThrowIfNull(ownedConnectionFactory);
		_ownedConnectionFactory = ownedConnectionFactory;
		_qualifiedTable = new Lazy<string>(() => SqlTableName.Format(schema, table));
		_authorizedTenant = authorizedTenant;
	}

	internal static SqlServerAuthoritativeEventReader CreateConfined(Func<SqlConnection> ownedConnectionFactory,
		string schema, string table, ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);
		return new SqlServerAuthoritativeEventReader(ownedConnectionFactory, schema, table,
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

		// Suppress before invoking the factory, not merely before OpenAsync. Never borrow or repair a
		// caller's existing connection: it may carry an older transaction or snapshot.
		using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
		var candidate = _ownedConnectionFactory()
			?? throw new InvalidOperationException("The owned primary connection factory returned null.");
		if (candidate.State != System.Data.ConnectionState.Closed)
		{
			throw new InvalidOperationException("An authoritative event recheck requires a fresh closed owned connection.");
		}
		await using var connection = candidate;
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(
			System.Data.IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema/table validated by SqlTableName.Format; event locator is parameterized.
		var sql = $"""
			SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Updateability')) AS Updateability,
			       e.EventId, e.AggregateId, e.AggregateType, e.EventType, e.Version,
			       e.Position AS GlobalPosition, e.Timestamp, e.ArchivedAt,
			       CASE WHEN e.EventId IS NULL THEN NULL ELSE COALESCE(e.TenantId, @Untenanted) END AS TenantId
			FROM (VALUES (1)) AS anchor(value)
			LEFT JOIN {qualifiedTable} AS e
			  ON e.AggregateId = @AggregateId AND e.AggregateType = @AggregateType
			 AND e.EventId = @EventId AND e.Version = @Version
			 AND COALESCE(e.TenantId, @Untenanted) = @TenantId
			""";
#pragma warning restore CA2100
		// The anchor preserves the endpoint check even for absence. One statement binds the check to
		// the read; a separately routed probe is not evidence about the endpoint returning the event.
		var row = await connection.QuerySingleAsync<StateRow>(new CommandDefinition(sql,
			new { AggregateId = aggregateId, AggregateType = aggregateType, EventId = eventId, Version = version,
				TenantId = tenant.TenantId, Untenanted = KeyedTenantPartition.Untenanted.TenantId },
			transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		if (!string.Equals(row.Updateability, "READ_WRITE", StringComparison.Ordinal))
		{
			throw new InvalidOperationException("An authoritative event recheck requires a READ_WRITE primary database.");
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

	private sealed record StateRow(string? Updateability, string? EventId, string? AggregateId, string? AggregateType,
		string? EventType, long? Version, long? GlobalPosition, DateTimeOffset? Timestamp, DateTimeOffset? ArchivedAt, string? TenantId);
}
