// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Dapper;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Binds the UPGRADE path of <see cref="SqliteSnapshotStore"/> against a database created by the
/// last released artifact — the population that actually exists in the field.
/// </summary>
/// <remarks>
/// <para>
/// Every other Sqlite snapshot arm starts from an empty file and lets the store create its own
/// table, so all of them exercise the CURRENT schema. No arm reaches a table that already existed
/// before the tenant column did, which is the only shape a real consumer upgrading a package can
/// have.
/// </para>
/// <para>
/// The schema seeded below is copied verbatim from the released initializer, whose snapshot table
/// has no tenant column at all and constrains <c>UNIQUE(AggregateId, AggregateType)</c>. The
/// initializer at HEAD issues <c>CREATE TABLE IF NOT EXISTS</c> with no migration step, so on an
/// existing database it is a no-op and the table keeps its released shape while the store emits SQL
/// naming a column that is not there.
/// </para>
/// <para>
/// Both arms are required and they fail for different reasons. The read arm is SAFETY-shaped: an
/// upgraded consumer must not silently lose the snapshots it already has. The write arm is the
/// LIVENESS half: a store that answered every read with null would satisfy the read arm perfectly
/// while having bricked the aggregate, so the save must also still work.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteSnapshotStoreReleasedSchemaUpgradeShould : IAsyncLifetime
{
	private const string AggregateType = "UpgradeAggregate";
	private const string AggregateId = "aggregate-upgraded-in-place";

	private string _databasePath = string.Empty;
	private string _connectionString = string.Empty;

	/// <summary>
	/// Provisions a database in the shape the last released artifact would have left behind, with one
	/// snapshot already in it.
	/// </summary>
	/// <returns>A task that represents the asynchronous initialization.</returns>
	public async ValueTask InitializeAsync()
	{
		_databasePath = Path.Combine(
			Path.GetTempPath(),
			$"excalibur-sqlite-upgrade-{Guid.NewGuid():N}.db");
		_connectionString = $"Data Source={_databasePath}";

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// Verbatim from the released initializer: no TenantId column, UNIQUE(AggregateId, AggregateType).
		await connection.ExecuteAsync(
			"""
			CREATE TABLE IF NOT EXISTS [Snapshots] (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				SnapshotId TEXT NOT NULL,
				AggregateId TEXT NOT NULL,
				AggregateType TEXT NOT NULL,
				Version INTEGER NOT NULL,
				Data BLOB NOT NULL,
				CreatedAt TEXT NOT NULL,
				UNIQUE(AggregateId, AggregateType)
			);
			""").ConfigureAwait(false);

		await connection.ExecuteAsync(
			"""
			INSERT INTO [Snapshots] (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt)
			VALUES (@SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt);
			""",
			new
			{
				SnapshotId = Guid.NewGuid().ToString(),
				AggregateId,
				AggregateType,
				Version = 7L,
				Data = Encoding.UTF8.GetBytes("state-written-by-the-released-version"),
				CreatedAt = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
			}).ConfigureAwait(false);
	}

	/// <summary>Releases pooled connections and removes the temporary database file.</summary>
	/// <returns>A task that represents the asynchronous cleanup.</returns>
	public ValueTask DisposeAsync()
	{
		SqliteConnection.ClearAllPools();

		if (File.Exists(_databasePath))
		{
			File.Delete(_databasePath);
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// SAFETY: a snapshot written by the released version is still readable after the upgrade.
	/// </summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReadASnapshotWrittenByTheReleasedVersionAfterUpgrading()
	{
		var store = CreateStore();

		ISnapshot? existing = await store
			.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		_ = existing.ShouldNotBeNull(
			"a snapshot written by the last released version must survive the package upgrade — if this " +
			"read fails or returns null the aggregate silently replays its entire event stream, and " +
			"nothing on either the read or the write path reports it");
		existing.Version.ShouldBe(7L);
		Encoding.UTF8.GetString(existing.Data.ToArray())
			.ShouldBe("state-written-by-the-released-version");
	}

	/// <summary>
	/// LIVENESS: the upgraded store can still write. A store that answered every read with null would
	/// satisfy no safety arm here, but a migration that broke first-write would pass a read-only arm.
	/// </summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task SaveASnapshotIntoADatabaseCreatedByTheReleasedVersion()
	{
		var store = CreateStore();

		await store.SaveSnapshotAsync(
			new UpgradeSnapshot(
				Guid.NewGuid().ToString(),
				AggregateId,
				AggregateType,
				9L,
				DateTimeOffset.UtcNow,
				Encoding.UTF8.GetBytes("state-written-after-the-upgrade"),
				null,
				null),
			CancellationToken.None).ConfigureAwait(false);

		ISnapshot? saved = await store
			.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		_ = saved.ShouldNotBeNull("the upgraded store must still be able to write and read back");
		saved.Version.ShouldBe(9L);
		Encoding.UTF8.GetString(saved.Data.ToArray()).ShouldBe("state-written-after-the-upgrade");
	}

	// The context a real single-tenant host receives from AddDefaultTenantContext(), which is the wiring
	// this upgrade is asserted against. It matters which one: the initializer's single-tenant convergence
	// moves untenanted rows onto TenantDefaults.DefaultTenantId, so a store resolving the reserved sentinel
	// reads the partition those rows have just left and finds nothing -- indistinguishable, from the read
	// side, from the upgrade having lost them.
	private SqliteSnapshotStore CreateStore() =>
		new(_connectionString,
			NullLogger<SqliteSnapshotStore>.Instance,
			SingleTenantTestContext.Instance,
			// RequireTenant = false: the upgrade under test IS the single-tenant convergence that moves
			// untenanted rows onto the default identity. The multi-tenant value skips it, and the arm would
			// then read the partition those rows never left.
			Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	private sealed record UpgradeSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
