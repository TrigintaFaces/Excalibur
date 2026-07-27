// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Dapper;

using Microsoft.Data.Sqlite;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>
/// Creates event sourcing tables on first use, tracked per physical (database, table) pair and per
/// logical role so a second store with a different table name, database file, or purpose still creates
/// its own table. Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
internal static class SqliteTableInitializer
{
	private static readonly SemaphoreSlim InitLock = new(1, 1);

	// Keyed by (connection string, table name, role) so initialization is tracked per physical table
	// AND per logical purpose, not once per process. Rationale for each component:
	//  * connection string - distinguishes database files and named shared-cache in-memory DBs (the
	//    same table name in two files must each be created).
	//  * table name - distinguishes tables within one file.
	//  * role - distinguishes the events table from the snapshots table so a consumer that legitimately
	//    configures the SAME table name for both does not have one initialization slot clobber the other
	//    (events and snapshots have different DDL; a single shared slot would create only the first and
	//    leave the second uninitialized, then fail with "no such table" at runtime).
	// A ValueTuple key is used instead of a delimiter-joined string: string joining is ambiguous because
	// a SQLite connection string can itself contain the delimiter (e.g. "Data Source=..."), so distinct
	// inputs could collide into one key. Structural tuple equality (ordinal for each string component)
	// makes the composition unambiguous.
	private static readonly ConcurrentDictionary<(string ConnectionString, string Table, string Role), bool> Initialized =
		new();

	private const string EventsRole = "events";
	private const string SnapshotsRole = "snapshots";

	internal static async Task EnsureEventsTableAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(connection, table, EventsRole);
		if (Initialized.ContainsKey(key))
		{
			return;
		}

		await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (Initialized.ContainsKey(key))
			{
				return;
			}

			var sql = $"""
				CREATE TABLE IF NOT EXISTS [{table}] (
					GlobalPosition INTEGER PRIMARY KEY AUTOINCREMENT,
					EventId TEXT NOT NULL,
					AggregateId TEXT NOT NULL,
					AggregateType TEXT NOT NULL,
					EventType TEXT NOT NULL,
					EventData BLOB NOT NULL,
					Metadata BLOB,
					Version INTEGER NOT NULL,
					Timestamp TEXT NOT NULL,
					UNIQUE(AggregateId, AggregateType, Version)
				);
				CREATE INDEX IF NOT EXISTS IX_{table}_AggregateId
					ON [{table}] (AggregateId, AggregateType, Version);
				""";

			await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			Initialized[key] = true;
		}
		finally
		{
			InitLock.Release();
		}
	}

	internal static async Task EnsureSnapshotsTableAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(connection, table, SnapshotsRole);
		if (Initialized.ContainsKey(key))
		{
			return;
		}

		await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (Initialized.ContainsKey(key))
			{
				return;
			}

			var sql = $"""
				CREATE TABLE IF NOT EXISTS [{table}] (
					Id INTEGER PRIMARY KEY AUTOINCREMENT,
					SnapshotId TEXT NOT NULL,
					AggregateId TEXT NOT NULL,
					AggregateType TEXT NOT NULL,
					Version INTEGER NOT NULL,
					Data BLOB NOT NULL,
					CreatedAt TEXT NOT NULL,
					-- Untenanted rows use the reserved '__untenanted__' tenant key BY DESIGN (see
					-- ARCHITECTURE.md, tenant isolation). NOT NULL is load-bearing: SQLite treats NULLs as
					-- DISTINCT in a UNIQUE constraint, so a nullable tenant would never conflict and every save
					-- would insert a duplicate row instead of updating the snapshot. The sentinel is
					-- collision-proof: Scoped() rejects it, so no real tenant can claim the untenanted
					-- partition. It replaced '', which is not portable — Oracle stores the empty string as
					-- NULL, so the same intent became a different value on that provider.
					--
					-- No DEFAULT, matching the PostgreSQL and Oracle schemas. TenantId is part of the UNIQUE
					-- key, and you do not default a key column: with a default, an INSERT that omitted the
					-- tenant would silently land the row in the untenanted partition, making "I forgot to
					-- supply the tenant" indistinguishable from "this row is deliberately untenanted." The
					-- store writes the sentinel explicitly on every save.
					TenantId TEXT NOT NULL,
					UNIQUE(AggregateId, AggregateType, TenantId)
				);
				""";

			await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			Initialized[key] = true;
		}
		finally
		{
			InitLock.Release();
		}
	}

	// Distinguishes physical tables AND logical roles: the same table name in two different database
	// files (or two distinctly-named in-memory DBs) yields two distinct keys, so each is created; and
	// the same table name used for both events and snapshots yields two distinct keys (different role),
	// so both are initialized. The tuple key avoids the delimiter-ambiguity of a joined string (a
	// connection string can itself contain any chosen delimiter).
	private static (string ConnectionString, string Table, string Role) BuildKey(
		SqliteConnection connection,
		string table,
		string role)
		=> (connection.ConnectionString, table, role);

	/// <summary>
	/// Resets the initialization state. For testing only.
	/// </summary>
	internal static void Reset() => Initialized.Clear();
}
