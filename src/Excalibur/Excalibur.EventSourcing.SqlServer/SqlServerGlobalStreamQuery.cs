// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Data;

using Excalibur.Dispatch.Extensions;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IGlobalStreamQuery"/> that reads events
/// from the event store in global order using the <c>GlobalPosition</c> column.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <c>UseSqlServer()</c> when the SQL Server event store
/// provider is configured. Uses the same schema/table settings as the event store.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"The global stream is cross-aggregate and cross-tenant by definition: it is the substrate that " +
	"cross-tenant projections, materialized views, projection rebuilds and the lag read-model are built " +
	"from. Its result carries each event's own tenant on the row, so a consumer that needs confinement " +
	"filters there. Adding a tenant predicate here would not harden the statement -- it would silently " +
	"narrow the stream to one partition and make every projection built on it incomplete.")]
internal sealed class SqlServerGlobalStreamQuery : IGlobalStreamQuery, IProjectionDestinationScope
{
    string IProjectionDestinationScope.Scope
    {
        get
        {
            using var connection = _connectionFactory();
            return string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.Database)
                ? string.Empty
                : "sqlserver/" + Uri.EscapeDataString(connection.DataSource) + "/" + Uri.EscapeDataString(connection.Database);
        }
    }

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _qualifiedTable;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerGlobalStreamQuery"/> class.
	/// </summary>
	internal SqlServerGlobalStreamQuery(
		Func<SqlConnection> connectionFactory,
		IOptions<SqlServerEventSourcingOptions> options)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);

		_connectionFactory = connectionFactory;
		_qualifiedTable = SqlTableName.Format(
			options.Value.EventStoreSchema,
			options.Value.EventStoreTable);
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT TOP (@MaxCount)
			       EventId, AggregateId, AggregateType, EventType,
			       EventData, Metadata, Version, Timestamp, Position, ArchivedAt,
			       COALESCE(TenantId, '__untenanted__') AS TenantId
			FROM {_qualifiedTable}
			WHERE Position > @Position
			ORDER BY Position
			""";
#pragma warning restore CA2100

		using var command = Parameters(position.Position, maxCount).CreateCommand(connection,
			new CommandDefinition(sql, cancellationToken: cancellationToken));
		using var rows = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		// NOTE: the contiguity guard that used to live here is now ContiguousGlobalStreamQuery, applied
		// as a decorator by this provider's registration. It is one correctness rule over an interface
		// with several implementations, and a copy per provider is how the next provider comes to lack
		// it. Do not reintroduce it here.
		var result = new List<StoredEvent>();
		while (await rows.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			result.Add(new StoredEvent(rows.GetString(0), rows.GetString(1), rows.GetString(2), rows.GetString(3),
				await rows.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false) ? null : (byte[])rows.GetValue(4), await rows.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : (byte[])rows.GetValue(5),
				rows.GetInt64(6), (DateTimeOffset)rows.GetValue(7))
			{
				GlobalPosition = rows.GetInt64(8),
				ArchivedAt = await rows.IsDBNullAsync(9, cancellationToken).ConfigureAwait(false) ? null : (DateTimeOffset)rows.GetValue(9),
				TenantId = rows.GetString(10),
			});
		}

		return result.AsReadOnlyList();
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
		string eventType,
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventType);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT TOP (@MaxCount)
			       EventId, AggregateId, AggregateType, EventType,
			       EventData, Metadata, Version, Timestamp, Position, ArchivedAt,
			       COALESCE(TenantId, '__untenanted__') AS TenantId
			FROM {_qualifiedTable}
			WHERE Position > @Position AND EventType = @EventType
			ORDER BY Position
			""";
#pragma warning restore CA2100

		using var command = Parameters(position.Position, maxCount, eventType).CreateCommand(connection,
			new CommandDefinition(sql, cancellationToken: cancellationToken));
		using var rows = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		// NOT PROTECTED BY THE CONTIGUITY RULE APPLIED IN ReadAllAsync, and it cannot be: filtering
		// by event type returns a legitimately SPARSE set of positions, so a gap here carries no
		// information about whether a transaction is in flight. A caller that advances a high-water
		// mark from this read is exposed to the same skip ReadAllAsync now defends against. Tracked;
		// do not assume parity with the unfiltered read.
		var result = new List<StoredEvent>();
		while (await rows.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			result.Add(new StoredEvent(rows.GetString(0), rows.GetString(1), rows.GetString(2), rows.GetString(3),
				await rows.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false) ? null : (byte[])rows.GetValue(4), await rows.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : (byte[])rows.GetValue(5),
				rows.GetInt64(6), (DateTimeOffset)rows.GetValue(7))
			{
				GlobalPosition = rows.GetInt64(8),
				ArchivedAt = await rows.IsDBNullAsync(9, cancellationToken).ConfigureAwait(false) ? null : (DateTimeOffset)rows.GetValue(9),
				TenantId = rows.GetString(10),
			});
		}

		return result.AsReadOnlyList();
	}

	/// <inheritdoc />
	public async ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"SELECT ISNULL(MAX(Position), 0) FROM {_qualifiedTable}";
#pragma warning restore CA2100

		return await SqlServerParameterSet.ExecuteScalarAsync<long>(connection,
			new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	private static SqlServerParameterSet Parameters(long position, int maxCount, string? eventType = null)
	{
		var parameters = new SqlServerParameterSet();
		parameters.Add("Position", position);
		parameters.Add("MaxCount", maxCount);
		if (eventType is not null) { parameters.Add("EventType", eventType); }
		return parameters;
	}
}
