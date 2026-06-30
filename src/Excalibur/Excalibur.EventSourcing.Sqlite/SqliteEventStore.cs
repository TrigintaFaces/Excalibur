// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>
/// SQLite implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Lightweight event store for local development, testing, and embedded scenarios.
/// Auto-creates tables on first use. Zero Docker dependency.
/// </para>
/// <para>
/// SQLite uses WAL mode for concurrent read access while writing.
/// Concurrency is enforced via UNIQUE(AggregateId, AggregateType, Version).
/// </para>
/// </remarks>
public sealed class SqliteEventStore : IEventStore
{
	private readonly string _connectionString;
	private readonly string _table;
	private readonly ILogger<SqliteEventStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteEventStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string (e.g., "Data Source=events.db").</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="table">The event store table name. Default: "Events".</param>
	public SqliteEventStore(
		string connectionString,
		ILogger<SqliteEventStore> logger,
		string table = "Events")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionString = connectionString;
		_logger = logger;
		_table = table;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		return await LoadAsync(aggregateId, aggregateType, -1, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureEventsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

		var sql = $"""
			SELECT EventId, AggregateId, AggregateType, EventType,
			       EventData, Metadata, Version, Timestamp
			FROM [{_table}]
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType AND Version > @FromVersion
			ORDER BY Version ASC
			""";

		var rows = await connection.QueryAsync<StoredEventRow>(
			new CommandDefinition(
				sql,
				new { AggregateId = aggregateId, AggregateType = aggregateType, FromVersion = fromVersion },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		return rows.Select(r => new StoredEvent(
			r.EventId,
			r.AggregateId,
			r.AggregateType,
			r.EventType,
			r.EventData,
			r.Metadata,
			r.Version,
			DateTimeOffset.Parse(r.Timestamp))).ToList();
	}

	/// <inheritdoc/>
	public async ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var eventList = events as IReadOnlyCollection<IDomainEvent> ?? events.ToList();

		if (eventList.Count == 0)
		{
			return AppendResult.CreateSuccess(expectedVersion, 0);
		}

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureEventsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

		// Enable WAL mode for better concurrent access
		await connection.ExecuteAsync("PRAGMA journal_mode=WAL;").ConfigureAwait(false);

		await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
			.ConfigureAwait(false);

		try
		{
			// Check current version
			var currentVersion = await connection.ExecuteScalarAsync<long?>(
				new CommandDefinition(
					$"SELECT MAX(Version) FROM [{_table}] WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType",
					new { AggregateId = aggregateId, AggregateType = aggregateType },
					transaction,
					cancellationToken: cancellationToken)).ConfigureAwait(false) ?? -1;

			if (currentVersion != expectedVersion)
			{
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion);
			}

			var version = currentVersion;
			long firstPosition = 0;

			foreach (var @event in eventList)
			{
				version++;
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
				var eventData = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);
				var metadata = @event.Metadata != null
					? JsonSerializer.SerializeToUtf8Bytes(@event.Metadata, _jsonOptions)
					: null;
#pragma warning restore IL2026, IL3050

				var sql = $"""
					INSERT INTO [{_table}] (EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp)
					VALUES (@EventId, @AggregateId, @AggregateType, @EventType, @EventData, @Metadata, @Version, @Timestamp);
					SELECT last_insert_rowid();
					""";

				var position = await connection.ExecuteScalarAsync<long>(
					new CommandDefinition(
						sql,
						new
						{
							@event.EventId,
							AggregateId = aggregateId,
							AggregateType = aggregateType,
							EventType = @event.GetType().Name,
							EventData = eventData,
							Metadata = metadata,
							Version = version,
							Timestamp = @event.OccurredAt.ToString("O"),
						},
						transaction,
						cancellationToken: cancellationToken)).ConfigureAwait(false);

				if (firstPosition == 0)
				{
					firstPosition = position;
				}
			}

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogDebug(
				"Appended {Count} events to {AggregateType}/{AggregateId} at version {Version}",
				eventList.Count, aggregateType, aggregateId, version);

			return AppendResult.CreateSuccess(version, firstPosition);
		}
		catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
		{
			// The only UNIQUE constraint on the events table is (AggregateId, AggregateType, Version), so a
			// constraint violation here means a concurrent writer committed our target version first. This is a
			// genuine optimistic-concurrency conflict, not a generic failure — classifying it as a conflict lets
			// the repository's retry path (keyed on IsConcurrencyConflict) re-load and retry instead of failing hard.
			// The fresh-connection re-read returns null only if it failed; fall back to a safe estimate then, so a
			// concurrency conflict is never turned into an escaped exception (the exact version is not load-bearing —
			// classification drives the repository retry).
			var actualVersion = await ReadActualVersionOnFreshConnectionAsync(aggregateId, aggregateType, cancellationToken)
				.ConfigureAwait(false) ?? (expectedVersion + eventList.Count);

			return AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);
		}
	}

	/// <summary>
	/// Reads the actual committed <c>MAX(Version)</c> for an aggregate on a FRESH connection, used to populate the
	/// concurrency-conflict result after a UNIQUE-constraint violation during append.
	/// </summary>
	/// <remarks>
	/// A fresh connection is required — NOT the appending connection: that connection holds a pending, failed
	/// transaction, so reusing it without that transaction throws <see cref="InvalidOperationException"/>
	/// ("transaction required"), and reading WITH it would return this writer's own uncommitted state. A separate
	/// connection reads the version the winning writer committed. Any non-cancellation re-read failure returns
	/// <see langword="null"/> (the caller then supplies a fallback) so a concurrency conflict is never turned into an
	/// escaped exception — the exact version is not load-bearing (classification drives the repository retry);
	/// cancellation propagates.
	/// Extracted as <see langword="internal"/> for direct, deterministic unit testing of the conflict re-read.
	/// </remarks>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The committed <c>MAX(Version)</c> for the aggregate (<c>-1</c> when it has no events), or <see langword="null"/>
	/// if the fresh-connection re-read itself failed (a non-cancellation error) — callers supply a fallback in that case.
	/// </returns>
	internal async ValueTask<long?> ReadActualVersionOnFreshConnectionAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var conflictConnection = CreateConnection();
			await conflictConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

			return await conflictConnection.ExecuteScalarAsync<long?>(
				new CommandDefinition(
					$"SELECT MAX(Version) FROM [{_table}] WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType",
					new { AggregateId = aggregateId, AggregateType = aggregateType },
					cancellationToken: cancellationToken)).ConfigureAwait(false) ?? -1;
		}
		catch (Exception reReadEx) when (reReadEx is not OperationCanceledException)
		{
			_logger.LogDebug(reReadEx,
				"Could not re-read current version after constraint violation for {AggregateType}/{AggregateId}",
				aggregateType, aggregateId);
			return null;
		}
	}

	private SqliteConnection CreateConnection() => new(_connectionString);

	private sealed record StoredEventRow(
		string EventId,
		string AggregateId,
		string AggregateType,
		string EventType,
		byte[] EventData,
		byte[]? Metadata,
		long Version,
		string Timestamp);
}
