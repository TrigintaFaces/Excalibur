// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerSnapshotStore"/> using TestContainers.
/// Tests real SQL Server database operations for aggregate snapshot persistence.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 177 - Provider Testing Epic Phase 3.
/// bd-6c8qw: SqlServer SnapshotStore Tests (5 tests).
/// </para>
/// <para>
/// These tests verify the SqlServerSnapshotStore implementation against a real SQL Server
/// database using TestContainers. Tests cover save, load, update, delete, and version behavior.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", "SnapshotStore")]
[Trait("Provider", "SqlServer")]
public sealed class SqlServerSnapshotStoreIntegrationShould : IntegrationTestBase
{
	private const string TestAggregateType = "TestAggregate";
	private readonly SqlServerFixture _sqlFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSnapshotStoreIntegrationShould"/> class.
	/// </summary>
	/// <param name="sqlFixture">The SQL Server container fixture.</param>
	public SqlServerSnapshotStoreIntegrationShould(SqlServerFixture sqlFixture)
	{
		_sqlFixture = sqlFixture;
	}

	/// <summary>
	/// Tests that a snapshot can be saved and loaded.
	/// </summary>
	[Fact]
	public async Task SaveAndLoadSnapshot()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();
		var snapshot = CreateSnapshot(aggregateId, version: 1);

		// Act
		await store.SaveSnapshotAsync(snapshot, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.AggregateId.ShouldBe(aggregateId);
		loaded.AggregateType.ShouldBe(TestAggregateType);
		loaded.Version.ShouldBe(1);
		loaded.Data.ShouldBe(snapshot.Data);
	}

	/// <summary>
	/// Tests that an existing snapshot can be updated with a new version.
	/// </summary>
	[Fact]
	public async Task UpdateExistingSnapshot()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();
		var snapshot1 = CreateSnapshot(aggregateId, version: 1, data: "state-v1");
		var snapshot2 = CreateSnapshot(aggregateId, version: 10, data: "state-v10");

		await store.SaveSnapshotAsync(snapshot1, TestCancellationToken).ConfigureAwait(true);

		// Act - Update with new version
		await store.SaveSnapshotAsync(snapshot2, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.Version.ShouldBe(10);
		loaded.Data.ShouldBe(snapshot2.Data);
	}

	/// <summary>
	/// Tests that a snapshot can be deleted.
	/// </summary>
	[Fact]
	public async Task DeleteSnapshot()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();
		var snapshot = CreateSnapshot(aggregateId, version: 5);

		await store.SaveSnapshotAsync(snapshot, TestCancellationToken).ConfigureAwait(true);

		// Act
		await store.DeleteSnapshotsAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Tests that loading a non-existent snapshot returns null.
	/// </summary>
	[Fact]
	public async Task HandleMissingSnapshot()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Act
		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Tests that version consistency is validated through save/load cycle.
	/// </summary>
	[Fact]
	public async Task ValidateSnapshotVersion()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Save multiple snapshots with different versions (each update replaces the previous)
		var snapshot1 = CreateSnapshot(aggregateId, version: 1);
		var snapshot2 = CreateSnapshot(aggregateId, version: 5);
		var snapshot3 = CreateSnapshot(aggregateId, version: 100);

		// Act
		await store.SaveSnapshotAsync(snapshot1, TestCancellationToken).ConfigureAwait(true);
		await store.SaveSnapshotAsync(snapshot2, TestCancellationToken).ConfigureAwait(true);
		await store.SaveSnapshotAsync(snapshot3, TestCancellationToken).ConfigureAwait(true);

		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert - Should have the latest version
		_ = loaded.ShouldNotBeNull();
		loaded.Version.ShouldBe(100);
	}

	#region Extended Tests (Sprint 214 - 6c8qw)

	/// <summary>
	/// Tests that a large snapshot (>1MB) can be saved and loaded correctly.
	/// Verifies VARBINARY(MAX) handles large payloads without truncation.
	/// </summary>
	/// <remarks>
	/// Sprint 214 - bd-6c8qw: Extended test 1 of 5.
	/// </remarks>
	[Fact]
	public async Task SaveAndLoadLargeSnapshot_Over1MB_Succeeds()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Create 1MB + 1 byte of deterministic data
		var largeData = new byte[1024 * 1024 + 1];
		new Random(42).NextBytes(largeData); // Deterministic seed for reproducibility

		var snapshot = CreateSnapshotWithData(aggregateId, version: 1, largeData);

		// Act
		await store.SaveSnapshotAsync(snapshot, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.Data.Length.ShouldBe(largeData.Length, "Large payload should not be truncated");
		loaded.Data.ShouldBe(largeData, "Large payload should match byte-for-byte");
	}

	/// <summary>
	/// Tests the behavior when saving an older version over a newer version.
	/// Documents the actual MERGE (upsert) semantics of SqlServerSnapshotStore.
	/// </summary>
	/// <remarks>
	/// Sprint 214 - bd-6c8qw: Extended test 2 of 5.
	/// Current implementation uses last-write-wins semantics.
	/// </remarks>
	[Fact]
	public async Task SaveOlderVersion_WhenNewerExists_ReplacesWithLastWrite()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		var snapshotV10 = CreateSnapshot(aggregateId, version: 10, data: "state-v10");
		var snapshotV5 = CreateSnapshot(aggregateId, version: 5, data: "state-v5");

		await store.SaveSnapshotAsync(snapshotV10, TestCancellationToken).ConfigureAwait(true);

		// Act - Save older version (last-write-wins behavior)
		await store.SaveSnapshotAsync(snapshotV5, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert - Documents current MERGE behavior: last write wins
		_ = loaded.ShouldNotBeNull();
		loaded.Version.ShouldBe(5, "Current impl uses last-write-wins (MERGE upsert)");
		System.Text.Encoding.UTF8.GetString(loaded.Data).ShouldBe("state-v5");
	}

	/// <summary>
	/// Tests concurrent save operations to the same aggregate.
	/// Verifies no deadlocks or data corruption under race conditions.
	/// </summary>
	/// <remarks>
	/// Sprint 214 - bd-6c8qw: Extended test 3 of 5.
	/// </remarks>
	[Fact]
	public async Task ConcurrentSaves_ToSameAggregate_HandleRaceCondition()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Act - Concurrent saves with different versions
		var tasks = Enumerable.Range(1, 5)
			.Select(v => Task.Run(async () =>
			{
				var snapshot = CreateSnapshot(aggregateId, version: v, data: $"state-v{v}");
				await store.SaveSnapshotAsync(snapshot, TestCancellationToken).ConfigureAwait(false);
			}));

		await Task.WhenAll(tasks).ConfigureAwait(true);
		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert - One version wins (no corruption), exact winner is non-deterministic
		_ = loaded.ShouldNotBeNull("Should have a valid snapshot after concurrent saves");
		loaded.Version.ShouldBeInRange(1, 5, "Final version should be one of the saved versions");
		loaded.AggregateId.ShouldBe(aggregateId);
	}

	/// <summary>
	/// Tests that deleting a non-existent snapshot completes without error.
	/// Follows the idempotent delete pattern.
	/// </summary>
	/// <remarks>
	/// Sprint 214 - bd-6c8qw: Extended test 4 of 5.
	/// </remarks>
	[Fact]
	public async Task DeleteNonExistentSnapshot_CompletesWithoutError()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Act - Delete aggregate that was never saved (should be idempotent)
		var act = async () => await store.DeleteSnapshotsAsync(
			aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert - Should NOT throw (idempotent delete pattern)
		await act.ShouldNotThrowAsync();
	}

	/// <summary>
	/// Tests that binary data with special byte values is preserved exactly.
	/// Verifies SQL Server VARBINARY handles null bytes and boundary values.
	/// </summary>
	/// <remarks>
	/// Sprint 214 - bd-6c8qw: Extended test 5 of 5.
	/// Critical for MemoryPack/Protobuf serialized aggregates.
	/// </remarks>
	[Fact]
	public async Task BinaryDataRoundTrip_WithSpecialBytes_PreservesExactData()
	{
		// Arrange
		await InitializeSnapshotTableAsync().ConfigureAwait(true);
		var store = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		// Include edge cases: null bytes, high bytes, common serialization boundaries
		var binaryData = new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF };
		var snapshot = CreateSnapshotWithData(aggregateId, version: 1, binaryData);

		// Act
		await store.SaveSnapshotAsync(snapshot, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetLatestSnapshotAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert - Byte-for-byte equality
		_ = loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe(binaryData, "Binary data with special bytes should be preserved exactly");
	}

	#endregion Extended Tests (Sprint 214 - 6c8qw)

	private static Snapshot CreateSnapshot(string aggregateId, long version, string data = "test-state")
	{
		return new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = TestAggregateType,
			Version = version,
			Data = System.Text.Encoding.UTF8.GetBytes(data),
			CreatedAt = DateTime.UtcNow
		};
	}

	private static Snapshot CreateSnapshotWithData(string aggregateId, long version, byte[] data)
	{
		return new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = TestAggregateType,
			Version = version,
			Data = data,
			CreatedAt = DateTime.UtcNow
		};
	}

	private ISnapshotStore CreateSnapshotStore()
	{
		var logger = NullLogger<SqlServerSnapshotStore>.Instance;
		return new SqlServerSnapshotStore(_sqlFixture.ConnectionString, logger);
	}

	private async Task InitializeSnapshotTableAsync()
	{
		const string createTableSql = """
			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EventStoreSnapshots]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE dbo.EventStoreSnapshots (
			        SnapshotId NVARCHAR(100) NOT NULL,
			        AggregateId NVARCHAR(100) NOT NULL,
			        AggregateType NVARCHAR(500) NOT NULL,
			        Version BIGINT NOT NULL,
			        Data VARBINARY(MAX) NOT NULL,
			        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
			        CONSTRAINT PK_EventStoreSnapshots PRIMARY KEY (AggregateId, AggregateType)
			    );
			END
			""";

		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(createTableSql).ConfigureAwait(true);
	}
}
