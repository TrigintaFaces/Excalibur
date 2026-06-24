// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch.Extensions;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;
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
internal sealed class SqlServerGlobalStreamQuery : IGlobalStreamQuery
{
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
			       EventData, Metadata, Version, Timestamp, Position
			FROM {_qualifiedTable}
			WHERE Position >= @Position
			ORDER BY Position
			""";
#pragma warning restore CA2100

		var rows = await connection.QueryAsync<StoredEventRow>(
			new CommandDefinition(
				sql,
				new { Position = position.Position, MaxCount = maxCount },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		var result = new List<StoredEvent>();
		foreach (var row in rows)
		{
			result.Add(new StoredEvent(
				row.EventId,
				row.AggregateId,
				row.AggregateType,
				row.EventType,
				row.EventData,
				row.Metadata,
				row.Version,
				row.Timestamp)
			{
				GlobalPosition = row.Position,
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
			       EventData, Metadata, Version, Timestamp, Position
			FROM {_qualifiedTable}
			WHERE Position >= @Position AND EventType = @EventType
			ORDER BY Position
			""";
#pragma warning restore CA2100

		var rows = await connection.QueryAsync<StoredEventRow>(
			new CommandDefinition(
				sql,
				new { Position = position.Position, MaxCount = maxCount, EventType = eventType },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		var result = new List<StoredEvent>();
		foreach (var row in rows)
		{
			result.Add(new StoredEvent(
				row.EventId,
				row.AggregateId,
				row.AggregateType,
				row.EventType,
				row.EventData,
				row.Metadata,
				row.Version,
				row.Timestamp)
			{
				GlobalPosition = row.Position,
			});
		}

		return result;
	}

	/// <summary>
	/// Row mapping for Dapper query results.
	/// </summary>
	private sealed record StoredEventRow(
		string EventId,
		string AggregateId,
		string AggregateType,
		string EventType,
		byte[] EventData,
		byte[]? Metadata,
		long Version,
		DateTimeOffset Timestamp,
		long Position);
}
