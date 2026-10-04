// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Data.Validation;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Data.Sqlite;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>
/// A durable <see cref="ISubscriptionCheckpointStore"/> backed by SQLite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a durable store is not optional, even here.</b> SQLite is embedded, so it is tempting to treat
/// its checkpoint as process state — but the process is exactly what restarts. An in-memory checkpoint
/// makes every subscription replay the whole stream on each start, which on an append-only store grows
/// without bound. The database file outlives the process; the dictionary does not.
/// </para>
/// <para>
/// <b>The advance is a compare-and-set.</b> SQLite serializes writers, so the race window is narrower
/// than on a server engine — but it is not absent: two processes can share one database file, and the
/// contract must hold identically across every provider or a consumer's retry logic is correct on some
/// and wrong on others. The predicate carries the caller's expectation in both statements.
/// </para>
/// <para>
/// <b>The two prior states are distinct.</b> <c>ON CONFLICT DO NOTHING</c> expresses "create it only if
/// nobody has" directly. It must never become <c>DO UPDATE</c>: an upsert makes the two prior states
/// interchangeable and lets a late-starting instance reset a live subscription to its own position.
/// </para>
/// </remarks>
internal sealed class SqliteSubscriptionCheckpointStore : ISubscriptionCheckpointStore
{
	private readonly string _connectionString;
	private readonly string _table;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSubscriptionCheckpointStore"/> class.
	/// </summary>
	/// <param name="connectionString">The connection string for the checkpoint database.</param>
	/// <param name="table">The checkpoint table name. Default: "SubscriptionCheckpoints".</param>
	internal SqliteSubscriptionCheckpointStore(
		string connectionString,
		string table = "SubscriptionCheckpoints")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(table);

		// The table name is interpolated directly into DDL and statement text (SQL does not allow a
		// parameterized identifier), so it must be allowlist-validated before it can reach
		// SqliteTableInitializer or any query this store issues. Matches SqliteEventStore.
		SqlIdentifierValidator.ThrowIfInvalid(table, nameof(table));

		_connectionString = connectionString;
		_table = table;
	}

	/// <inheritdoc />
	public async Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureCheckpointsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

#pragma warning disable CA2100 // Table name is a configured identifier, bracketed below
		return await connection.ExecuteScalarAsync<long?>(
			new CommandDefinition(
				$"SELECT Position FROM [{_table}] WHERE SubscriptionName = @Name;",
				new { Name = subscriptionName },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100
	}

	/// <inheritdoc />
	public async Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
		string subscriptionName,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		if (expectedPosition is { } prior)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(prior, nameof(expectedPosition));
			ArgumentOutOfRangeException.ThrowIfLessThan(newPosition, prior);
		}


		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureCheckpointsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

		// One statement either way, the predicate carrying the caller's expectation. Neither is an
		// upsert and neither may become one.
		var sql = expectedPosition is null
			? $"""
				INSERT INTO [{_table}] (SubscriptionName, Position, UpdatedAt)
				VALUES (@Name, @NewPosition, CURRENT_TIMESTAMP)
				ON CONFLICT (SubscriptionName) DO NOTHING;
				"""
			: $"""
				UPDATE [{_table}]
				SET Position = @NewPosition, UpdatedAt = CURRENT_TIMESTAMP
				WHERE SubscriptionName = @Name AND Position = @Expected;
				""";

#pragma warning disable CA2100 // Table name is a configured identifier, bracketed above
		var affected = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { Name = subscriptionName, NewPosition = newPosition, Expected = expectedPosition },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

		return affected == 1 ? CheckpointAdvanceOutcome.Advanced : CheckpointAdvanceOutcome.Superseded;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(
		CancellationToken cancellationToken)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureCheckpointsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

#pragma warning disable CA2100 // Table name is a configured identifier, bracketed below
		var rows = await connection.QueryAsync<CheckpointRow>(
			new CommandDefinition(
				$"SELECT SubscriptionName, Position FROM [{_table}];",
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

		return [.. rows.Select(static r => new SubscriptionCheckpoint(r.SubscriptionName, r.Position))];
	}

	/// <summary>Row shape for <see cref="EnumerateCheckpointsAsync"/>.</summary>
	/// <remarks>
	/// Named explicitly rather than materialized into <see cref="SubscriptionCheckpoint"/> directly: that
	/// type is a positional record struct, and mapping a SELECT onto one depends on column order rather
	/// than column name, which breaks silently the moment the projection changes.
	/// </remarks>
	private sealed record CheckpointRow(string SubscriptionName, long Position);
}
