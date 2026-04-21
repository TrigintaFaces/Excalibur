// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Microsoft.Data.Sqlite;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>
/// Creates event sourcing tables on first use. Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
internal static class SqliteTableInitializer
{
	private static readonly SemaphoreSlim InitLock = new(1, 1);
	private static volatile bool _eventsInitialized;
	private static volatile bool _snapshotsInitialized;

	internal static async Task EnsureEventsTableAsync(
		SqliteConnection connection,
		string table,
		CancellationToken cancellationToken)
	{
		if (_eventsInitialized)
		{
			return;
		}

		await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_eventsInitialized)
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

			_eventsInitialized = true;
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
		if (_snapshotsInitialized)
		{
			return;
		}

		await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_snapshotsInitialized)
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
					UNIQUE(AggregateId, AggregateType)
				);
				""";

			await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken))
				.ConfigureAwait(false);

			_snapshotsInitialized = true;
		}
		finally
		{
			InitLock.Release();
		}
	}

	/// <summary>
	/// Resets the initialization state. For testing only.
	/// </summary>
	internal static void Reset()
	{
		_eventsInitialized = false;
		_snapshotsInitialized = false;
	}
}
