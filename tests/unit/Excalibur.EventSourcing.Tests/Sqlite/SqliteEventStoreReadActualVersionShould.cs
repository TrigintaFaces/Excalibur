// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA2100 // SQL strings are safe — table name is a test-controlled unique constant, never user input

namespace Excalibur.EventSourcing.Tests.Sqlite;

/// <summary>
/// Unit tests for <see cref="SqliteEventStore"/>'s internal conflict re-read,
/// <c>ReadActualVersionOnFreshConnectionAsync</c> — the method extracted from the code-19
/// (SQLITE_CONSTRAINT) catch as the B1 fix.
/// </summary>
/// <remarks>
/// <para>
/// B1 regression: the original re-read reused the SAME connection that still held the failed, pending
/// local transaction (without passing it), so Microsoft.Data.Sqlite threw
/// <see cref="InvalidOperationException"/> ("transaction required") and a narrow
/// <c>catch (SqliteException)</c> let it escape — turning a concurrency conflict into a hard failure. The
/// fix reads on a <em>fresh</em> connection (no pending transaction) under a broad
/// <c>catch (Exception) when (not OperationCanceledException)</c> that returns <see langword="null"/> on
/// any non-cancellation re-read failure.
/// </para>
/// <para>
/// These tests are deterministic and race-free (the method opens its own connection): they assert it
/// reads committed state (incl. while another connection holds an uncommitted transaction — the
/// isolation the buggy same-connection read failed) and that the BROAD catch swallows a non-SQLite
/// re-read failure into <see langword="null"/> (the primary non-vacuity guard: narrowing the catch to
/// <c>SqliteException</c> would let that non-SQLite exception escape → RED).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteEventStoreReadActualVersionShould : IDisposable
{
	private const string AggregateType = "ReadActualVersionAggregate";

	private readonly string _databasePath;
	private readonly string _connectionString;

	// Unique table name per test instance. SqliteTableInitializer caches "table created" by table NAME
	// globally, so parallel tests sharing the default "Events" name (on different files) would collide —
	// the first creates+caches it, the rest skip the CREATE on their own empty file. A unique name avoids it.
	private readonly string _tableName = $"Events_{Guid.NewGuid():N}";
	private readonly SqliteConnection _keepAlive;
	private bool _disposed;

	public SqliteEventStoreReadActualVersionShould()
	{
		_databasePath = Path.Combine(Path.GetTempPath(), $"excalibur-readversion-test-{Guid.NewGuid():N}.db");
		_connectionString = $"Data Source={_databasePath}";

		// Hold one connection open for the whole test so the file (and its WAL) stays alive: otherwise the
		// seed connection's WAL-created Events table can vanish for a later fresh connection when the WAL is
		// torn down on close. A real file (not a shared in-memory cache) preserves transaction isolation,
		// which the pending-transaction test relies on.
		_keepAlive = new SqliteConnection(_connectionString);
		_keepAlive.Open();
		using var wal = _keepAlive.CreateCommand();
		wal.CommandText = "PRAGMA journal_mode=WAL;";
		_ = wal.ExecuteScalar();

		// Create the events table here, not via the store. SqliteTableInitializer.EnsureEventsTableAsync
		// gates on a PROCESS-GLOBAL static flag (not keyed by table/connection/file), so after any test
		// has initialized once it no-ops for every other database — leaving this test's unique table
		// uncreated. Provisioning it directly makes each test self-contained regardless of run order.
		using var create = _keepAlive.CreateCommand();
		create.CommandText = $"""
			CREATE TABLE IF NOT EXISTS [{_tableName}] (
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
			""";
		_ = create.ExecuteNonQuery();
	}

	[Fact]
	public async Task ReturnCommittedMaxVersion_ForSeededAggregate()
	{
		// Arrange — seed versions 0, 1, 2 via the store (creates the Events table).
		var store = new SqliteEventStore(_connectionString, NullLogger<SqliteEventStore>.Instance, _tableName);
		var aggregateId = Guid.NewGuid().ToString();
		await SeedAsync(store, aggregateId, count: 3).ConfigureAwait(false); // -> versions 0,1,2

		// Act
		var actual = await store.ReadActualVersionOnFreshConnectionAsync(
			aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		// Assert
		actual.ShouldBe(2L);
	}

	[Fact]
	public async Task ReturnMinusOne_ForAggregateWithNoEvents()
	{
		// Arrange — table exists (seeded for a different aggregate), but the queried aggregate has none.
		var store = new SqliteEventStore(_connectionString, NullLogger<SqliteEventStore>.Instance, _tableName);
		await SeedAsync(store, Guid.NewGuid().ToString(), count: 1).ConfigureAwait(false);

		// Act
		var actual = await store.ReadActualVersionOnFreshConnectionAsync(
			Guid.NewGuid().ToString(), AggregateType, CancellationToken.None).ConfigureAwait(false);

		// Assert — no rows for this aggregate -> MAX is NULL -> the method maps it to -1.
		actual.ShouldBe(-1L);
	}

	[Fact]
	public async Task ReturnCommittedVersion_NotUncommitted_AndNeverThrow_WhileAnotherTransactionIsPending()
	{
		// The B1-specific proof: the fresh-connection re-read must read COMMITTED state and must not throw,
		// even while another connection holds an open (uncommitted) transaction that has written a higher
		// version. The pre-fix same-connection re-read threw InvalidOperationException on the pending
		// transaction; the fresh-connection method reads the committed value cleanly.
		// Arrange — seed version 0 (committed).
		var store = new SqliteEventStore(_connectionString, NullLogger<SqliteEventStore>.Instance, _tableName);
		var aggregateId = Guid.NewGuid().ToString();
		await SeedAsync(store, aggregateId, count: 1).ConfigureAwait(false); // -> version 0 committed

		// A side connection holds an UNCOMMITTED version-1 row (open transaction).
		await using var pendingConnection = new SqliteConnection(_connectionString);
		await pendingConnection.OpenAsync().ConfigureAwait(false);
		await ExecutePragmaWalAsync(pendingConnection).ConfigureAwait(false);
		await using var pendingTx = (SqliteTransaction)await pendingConnection.BeginTransactionAsync().ConfigureAwait(false);
		await InsertEventRowAsync(pendingConnection, pendingTx, _tableName, aggregateId, version: 1).ConfigureAwait(false);

		// Act — fresh-connection re-read while the version-1 write is still uncommitted.
		var actual = await store.ReadActualVersionOnFreshConnectionAsync(
			aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		// Assert — sees the COMMITTED version 0, NOT the uncommitted version 1, and did not throw.
		actual.ShouldBe(0L);

		await pendingTx.RollbackAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ReturnNull_WhenTheReReadFailsWithANonSqliteException()
	{
		// Primary non-vacuity guard: the BROAD catch (Exception when not OperationCanceledException) must
		// swallow a NON-SqliteException re-read failure into null. An invalid connection string makes
		// `new SqliteConnection(...)`/open throw an ArgumentException (not a SqliteException). Narrowing the
		// method's catch to `catch (SqliteException)` would let that escape -> the method would throw -> RED.
		var store = new SqliteEventStore(
			"Data Source=:memory:;Cache=NotAValidCacheMode",
			NullLogger<SqliteEventStore>.Instance);

		// Act
		var actual = await store.ReadActualVersionOnFreshConnectionAsync(
			Guid.NewGuid().ToString(), AggregateType, CancellationToken.None).ConfigureAwait(false);

		// Assert — non-cancellation re-read failure is swallowed to null (callers fall back).
		actual.ShouldBeNull();
	}

	private static async Task SeedAsync(SqliteEventStore store, string aggregateId, int count)
	{
		var events = new IDomainEvent[count];
		for (var i = 0; i < count; i++)
		{
			events[i] = new TestDomainEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				OccurredAt = DateTimeOffset.UtcNow,
				Data = $"seed-{i}",
			};
		}

		var result = await store.AppendAsync(aggregateId, AggregateType, events, expectedVersion: -1, CancellationToken.None)
			.ConfigureAwait(false);
		result.Success.ShouldBeTrue("seed append must succeed");
	}

	private static async Task ExecutePragmaWalAsync(SqliteConnection connection)
	{
		await using var pragma = connection.CreateCommand();
		pragma.CommandText = "PRAGMA journal_mode=WAL;";
		_ = await pragma.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private static async Task InsertEventRowAsync(
		SqliteConnection connection, SqliteTransaction transaction, string tableName, string aggregateId, long version)
	{
		await using var insert = connection.CreateCommand();
		insert.Transaction = transaction;
		insert.CommandText = $"""
			INSERT INTO [{tableName}] (EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp)
			VALUES ($eventId, $aggId, $aggType, $eventType, $data, NULL, $version, $ts);
			""";
		_ = insert.Parameters.AddWithValue("$eventId", Guid.NewGuid().ToString());
		_ = insert.Parameters.AddWithValue("$aggId", aggregateId);
		_ = insert.Parameters.AddWithValue("$aggType", AggregateType);
		_ = insert.Parameters.AddWithValue("$eventType", "PendingEvent");
		_ = insert.Parameters.AddWithValue("$data", new byte[] { 0x01 });
		_ = insert.Parameters.AddWithValue("$version", version);
		_ = insert.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
		_ = await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			_keepAlive.Dispose();
		}
		catch (Exception)
		{
			// Best-effort.
		}

		try
		{
			SqliteConnection.ClearAllPools();
		}
		catch (Exception)
		{
			// Best-effort pool clear so the file is unlocked for deletion.
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
