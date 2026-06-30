// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Shared fixture for SQLite SnapshotStore real-infrastructure conformance tests.
/// </summary>
/// <remarks>
/// SQLite is an embedded, file-based database, so it IS the real infrastructure - no Docker
/// container is required and the test is inherently non-skipped. The fixture provisions a unique
/// temporary database file per run and exposes a <c>Data Source=...</c> connection string. The
/// <see cref="Excalibur.EventSourcing.Sqlite.SqliteSnapshotStore"/> auto-creates its table on first
/// use, so no DDL is performed here. The temporary file is deleted on disposal.
/// </remarks>
public sealed class SqliteSnapshotStoreFixture : IDisposable
{
	private readonly string _databasePath;
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSnapshotStoreFixture"/> class.
	/// </summary>
	public SqliteSnapshotStoreFixture()
	{
		_databasePath = Path.Combine(
			Path.GetTempPath(),
			$"excalibur-snapshotstore-test-{Guid.NewGuid():N}.db");

		ConnectionString = $"Data Source={_databasePath}";
	}

	/// <summary>
	/// Gets the SQLite connection string targeting the unique temporary database file.
	/// </summary>
	public string ConnectionString { get; }

	/// <summary>
	/// Removes all snapshot rows so each test class run is isolated.
	/// </summary>
	/// <remarks>
	/// The store auto-creates the <c>Snapshots</c> table, so a clean run may not have the table yet;
	/// the delete is therefore tolerant of a missing table.
	/// </remarks>
	public async Task CleanupAsync()
	{
		await using var connection = new SqliteConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM [Snapshots]";

		try
		{
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		catch (SqliteException)
		{
			// Table does not exist yet (no snapshot persisted) - nothing to clean up.
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Release pooled connections so the underlying file is no longer locked, then delete it.
		try
		{
			SqliteConnection.ClearAllPools();
		}
		catch (Exception)
		{
			// Best-effort pool clear; deletion below is still attempted.
		}

		try
		{
			if (File.Exists(_databasePath))
			{
				File.Delete(_databasePath);
			}
		}
		catch (IOException)
		{
			// Best-effort cleanup of the temporary database file.
		}
		catch (UnauthorizedAccessException)
		{
			// Best-effort cleanup of the temporary database file.
		}
	}
}
