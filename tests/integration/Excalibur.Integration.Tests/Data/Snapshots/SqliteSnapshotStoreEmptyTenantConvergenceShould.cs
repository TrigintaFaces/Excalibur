// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
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
/// Binds the convergence of snapshot rows that store the untenanted partition as the empty string
/// onto the reserved untenanted key the current queries look for.
/// </summary>
/// <remarks>
/// <para>
/// This is a narrower population than the no-tenant-column upgrade: it is the databases written while
/// the store emitted the empty string as its untenanted representation. Those rows have a tenant
/// column, so they do not fail loudly - the equality predicate simply never matches them and the
/// aggregate replays from scratch with nothing reported on either path.
/// </para>
/// <para>
/// The collision arm is the reason convergence is guarded rather than unconditional. The table
/// constrains UNIQUE(AggregateId, AggregateType, TenantId), so an aggregate holding BOTH
/// representations has two rows that a blind UPDATE would collapse onto one key. Half-applying that
/// UPDATE would leave the table in a state neither representation describes, so it must refuse and say
/// which aggregate is at fault.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteSnapshotStoreEmptyTenantConvergenceShould : IAsyncLifetime
{
	private const string AggregateType = "ConvergenceAggregate";

	private string _databasePath = string.Empty;
	private string _connectionString = string.Empty;

	/// <summary>Provisions an empty temporary database file.</summary>
	/// <returns>A task that represents the asynchronous initialization.</returns>
	public ValueTask InitializeAsync()
	{
		_databasePath = Path.Combine(
			Path.GetTempPath(),
			$"excalibur-sqlite-converge-{Guid.NewGuid():N}.db");
		_connectionString = $"Data Source={_databasePath}";

		return ValueTask.CompletedTask;
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
	/// SAFETY: a snapshot stored under the empty-string representation is reachable again.
	/// </summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReadASnapshotStoredUnderTheEmptyStringRepresentation()
	{
		await SeedTenantedTableAsync([("aggregate-with-empty-tenant", 3L, "written-under-empty-string", "")])
			.ConfigureAwait(false);

		var store = CreateStore();

		ISnapshot? converged = await store
			.GetLatestSnapshotAsync("aggregate-with-empty-tenant", AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		_ = converged.ShouldNotBeNull(
			"a snapshot written when the untenanted partition was stored as the empty string must be " +
			"reachable after convergence - unconverged it is invisible to the equality predicate and the " +
			"aggregate silently replays its whole event stream");
		converged.Version.ShouldBe(3L);
		Encoding.UTF8.GetString(converged.Data.ToArray()).ShouldBe("written-under-empty-string");
	}

	/// <summary>
	/// LIVENESS: a fresh database still initializes and accepts a write. A convergence step that broke
	/// first-run would satisfy the safety arm above perfectly.
	/// </summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task InitializeAndWriteOnAFreshDatabase()
	{
		var store = CreateStore();

		await store.SaveSnapshotAsync(
			new ConvergenceSnapshot(
				Guid.NewGuid().ToString(),
				"aggregate-on-a-fresh-database",
				AggregateType,
				1L,
				DateTimeOffset.UtcNow,
				Encoding.UTF8.GetBytes("first-write"),
				null,
				null),
			CancellationToken.None).ConfigureAwait(false);

		ISnapshot? written = await store
			.GetLatestSnapshotAsync("aggregate-on-a-fresh-database", AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		_ = written.ShouldNotBeNull("a fresh database must still initialize and accept its first write");
		Encoding.UTF8.GetString(written.Data.ToArray()).ShouldBe("first-write");
	}

	/// <summary>
	/// An aggregate holding both representations cannot be converged, so the store refuses and names the
	/// table and the colliding aggregate rather than half-applying the update.
	/// </summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task RefuseToConvergeAnAggregateHoldingBothRepresentations()
	{
		await SeedTenantedTableAsync(
		[
			("aggregate-in-collision", 1L, "stored-as-empty-string", ""),
			("aggregate-in-collision", 2L, "stored-as-the-sentinel", "__untenanted__"),
		]).ConfigureAwait(false);

		var store = CreateStore();

		var refusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store
				.GetLatestSnapshotAsync("aggregate-in-collision", AggregateType, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		refusal.Message.ShouldContain(
			"Snapshots",
			customMessage: "the refusal must name the table so an operator knows where to look");
		refusal.Message.ShouldContain(
			"aggregate-in-collision",
			customMessage: "the refusal must name the colliding aggregate, not merely report that a " +
				"collision exists somewhere in the table");

		// The refusal must also be non-destructive: both rows are still present for the operator to
		// resolve. A guard that threw AFTER partially updating would leave nothing to reconcile.
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		var remaining = await connection.ExecuteScalarAsync<long>(
			"SELECT COUNT(*) FROM [Snapshots] WHERE AggregateId = 'aggregate-in-collision';")
			.ConfigureAwait(false);
		remaining.ShouldBe(2L, "the guard must refuse before changing anything");
	}

	private async Task SeedTenantedTableAsync(
		(string AggregateId, long Version, string Data, string TenantId)[] rows)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// The shape written by versions that had a tenant column but stored the untenanted partition as
		// the empty string.
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
				TenantId TEXT NOT NULL DEFAULT '',
				UNIQUE(AggregateId, AggregateType, TenantId)
			);
			""").ConfigureAwait(false);

		foreach (var row in rows)
		{
			_ = await connection.ExecuteAsync(
				"""
				INSERT INTO [Snapshots]
					(SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, TenantId)
				VALUES (@SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt, @TenantId);
				""",
				new
				{
					SnapshotId = Guid.NewGuid().ToString(),
					row.AggregateId,
					AggregateType,
					row.Version,
					Data = Encoding.UTF8.GetBytes(row.Data),
					CreatedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
					row.TenantId,
				}).ConfigureAwait(false);
		}
	}

	// The context a real single-tenant host receives from AddDefaultTenantContext(). Convergence is a
	// two-step on this path -- empty string to the reserved sentinel, then sentinel to the single-tenant
	// identity -- and only the second step is gated on the deployment mode. A store resolving the sentinel
	// would observe the row only between those two steps, so it cannot assert that convergence completed.
	private SqliteSnapshotStore CreateStore() =>
		new(_connectionString,
			NullLogger<SqliteSnapshotStore>.Instance,
			SingleTenantTestContext.Instance,
			// RequireTenant = false: this arm asserts the SINGLE-TENANT convergence itself, and the second of
			// its two steps is gated on exactly this setting. Passing the multi-tenant value would skip the
			// step under test and the arm would pass by never running it.
			Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	private sealed record ConvergenceSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
