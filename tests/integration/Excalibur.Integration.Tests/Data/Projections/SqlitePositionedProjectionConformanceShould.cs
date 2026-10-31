// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.Testing.Conformance;

using Microsoft.Data.Sqlite;

namespace Excalibur.Integration.Tests.Data.Projections;

/// <summary>
/// Runs the positioned-projection conformance kit against a REAL SQLite database file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this cannot be a unit test.</b> Every interesting arm is about what the ENGINE does when two
/// writers collide. The store expresses its condition as an UPDATE carrying both conjuncts of the
/// invariant, plus an insert-if-absent with a conflict clause; whether those statements actually refuse
/// the write they should refuse is a property of SQLite, not of our C#. A mocked connection returns
/// whatever it was told and would certify a broken predicate.
/// </para>
/// <para>
/// <b>No container.</b> SQLite is an embedded engine, so it IS the real infrastructure and this suite is
/// never skipped for want of Docker. Each run gets its own temporary database file because several arms
/// assert a projection is ABSENT, and the file is deleted on disposal. A private in-memory database is
/// deliberately not used: the store's transaction validation rejects private memory and URI aliases, and
/// a file is both simpler and closer to what a consumer runs.
/// </para>
/// <para>
/// <b>The base kit, not the destination kit.</b> <see cref="SqliteProjectionStore{TProjection}"/> does
/// not implement the generation-reader factory, so the destination-aware kit's extra abstracts are not
/// satisfiable here and claiming them would mean stubbing a capability the provider does not have.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
[Trait("Pattern", "STORE")]
public sealed class SqlitePositionedProjectionConformanceShould
	: PositionedProjectionStoreConformanceTestKit, IAsyncLifetime, IDisposable
{
	private const string ConformanceTenantId = "tenant-positioned-conformance";
	private const string IdentityPrefix = "sqlite:";

	private readonly string _databasePath = Path.Combine(
		Path.GetTempPath(), $"excalibur-positioned-projection-{Guid.NewGuid():N}.db");

	private readonly string _tableName = "positioned_projection_" + Guid.NewGuid().ToString("N");

	private bool _provisioned;
	private bool _disposed;

	// WAL plus an explicit busy timeout, because one arm drives two real writers against one
	// file. In the default rollback-journal mode a reader blocks a writer outright, so the second
	// writer fails with SQLITE_BUSY instead of serialising behind the first -- and the correct
	// repair for that is the fixture's journal mode, never a weakened assertion in the arm. The
	// timeout is spelled out rather than inherited from the provider default so the window this
	// suite depends on is readable where the dependency lives.
	private string ConnectionString => "Data Source=" + _databasePath + ";Default Timeout=30";

	/// <inheritdoc />
	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Scoped to this suite's own connection string, never ClearAllPools(): the pool is keyed by
		// connection string, and a process-global clear disposes handles belonging to every other SQLite
		// suite running alongside this one.
		try
		{
			using var pooled = new SqliteConnection(ConnectionString);
			SqliteConnection.ClearPool(pooled);
		}
		catch (SqliteException)
		{
			// Best-effort pool clear; the delete below is still attempted.
		}

		try
		{
			// The -wal and -shm sidecars exist because this suite runs in WAL mode; deleting only the
			// main file would leave two artefacts per run in the temp directory.
			foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
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

	/// <inheritdoc />
	protected override async Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync()
	{
		var store = new SqliteProjectionStore<ConformanceProjection>(
			ConnectionString,
			tenantContext: new FixedConformanceTenantContext(ConformanceTenantId),
			tableName: _tableName,
			jsonOptions: null);

		// The PHYSICAL name is taken from the store itself rather than re-derived here. The constructor
		// qualifies the base name with the projection's generation, and a second hand-written derivation
		// would be free to drift from it -- provisioning one table while the store addressed another,
		// which every arm would then report as a missing row rather than as the wiring mistake it is.
		await EnsureTableAsync(
			((IProjectionDestinationIdentity)store).Identity[IdentityPrefix.Length..]).ConfigureAwait(false);

		return store;
	}

	/// <summary>Wires the kit's own completeness check, which names any arm this suite forgot.</summary>
	/// <returns>A task representing the check.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	/// <summary>LIVENESS: a caller that read nothing creates the projection.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Create_when_nothing_is_stored_Test() => Create_when_nothing_is_stored();

	/// <summary>A caller claiming absence against an existing row is refused.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_late_starter_that_claims_absence_Test() => Refuse_a_late_starter_that_claims_absence();

	/// <summary>A row carrying no position cannot be advanced from.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_row_that_carries_no_position_Test() => Refuse_a_row_that_carries_no_position();

	/// <summary>The liveness half of the pair above: a rebuild repairs the unnumbered row.</summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>Without it the refusal is satisfied by a store that refuses everything.</remarks>
	[Fact]
	public Task Rebuild_repairs_a_row_that_carries_no_position_Test() =>
		Rebuild_repairs_a_row_that_carries_no_position();

	/// <summary>The READ side of the two no-number states: UNPLACEABLE.</summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Both writes are refused; what differs is which state the row reads back as, and a store that
	/// dropped the position instead of recording the sentinel passes the refusal and fails this.
	/// </remarks>
	[Fact]
	public Task Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable_Test() =>
		Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable();

	/// <summary>A writer advancing from the position it read succeeds.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Advance_from_the_position_it_read_Test() => Advance_from_the_position_it_read();

	/// <summary>A writer naming a position the row no longer holds is refused.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_stale_expected_position_Test() => Refuse_a_stale_expected_position();

	/// <summary>THE DOUBLE-FOLD REFUSAL: a re-delivered event may not be folded twice.</summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The forward-only conjunct, and the only arm that separates it from the compare-and-set conjunct:
	/// on a redelivery the expected position matches by construction, so nothing but monotonicity refuses
	/// the re-application.
	/// </remarks>
	[Fact]
	public Task Refuse_a_position_that_does_not_advance_Test() => Refuse_a_position_that_does_not_advance();

	/// <summary>A deleted projection reports as vanished and is not recreated.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_a_deleted_projection_as_vanished_without_recreating_it_Test() =>
		Report_a_deleted_projection_as_vanished_without_recreating_it();

	/// <summary>THE CONCURRENCY ARM: exactly one of two writers racing from one read is admitted.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Admit_exactly_one_of_two_writers_racing_from_one_read_Test() =>
		Admit_exactly_one_of_two_writers_racing_from_one_read();

	/// <summary>An unconditional write invalidates the stored position.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Invalidate_the_position_when_an_unconditional_write_replaces_the_state_Test() =>
		Invalidate_the_position_when_an_unconditional_write_replaces_the_state();

	/// <summary>State and position come back as one observation.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Read_the_state_and_its_position_as_one_observation_Test() =>
		Read_the_state_and_its_position_as_one_observation();

	/// <summary>A re-fold replaces the state at the position the row already holds.</summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Erasure mutates events in place, so the fold beneath a position changes while the position stays
	/// fixed — a state transition the advancing write cannot express.
	/// </remarks>
	[Fact]
	public Task Refold_the_state_at_the_position_the_row_already_holds_Test() =>
		Refold_the_state_at_the_position_the_row_already_holds();

	/// <summary>Refuses a re-fold the row moved out from under.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_refold_when_the_row_advanced_after_the_read_Test() =>
		Refuse_a_refold_when_the_row_advanced_after_the_read();

	/// <summary>Refuses a re-fold naming a position the row does not hold.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_refold_at_a_position_the_row_does_not_hold_Test() =>
		Refuse_a_refold_at_a_position_the_row_does_not_hold();

	/// <summary>An unpositioned row is terminal, not retryable.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_requires_rebuild_for_a_row_with_no_established_position_Test() =>
		Report_requires_rebuild_for_a_row_with_no_established_position();

	/// <summary>The UNNUMBERED half of the pair above.</summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// That arm drives the row through the blind surface; this one writes a complete fold with no number.
	/// A store discriminating on <c>== Unplaceable</c> rather than <c>!= Positioned</c> passes one and
	/// fails the other.
	/// </remarks>
	[Fact]
	public Task Report_requires_rebuild_for_a_refold_against_an_unnumbered_row_Test() =>
		Report_requires_rebuild_for_a_refold_against_an_unnumbered_row();

	/// <summary>A repeated re-fold leaves the row identical.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Apply_the_same_refold_twice_without_changing_the_result_Test() =>
		Apply_the_same_refold_twice_without_changing_the_result();

	/// <summary>A re-fold must never resurrect a row erasure deleted.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_a_refold_against_an_absent_row_as_vanished_without_creating_it_Test() =>
		Report_a_refold_against_an_absent_row_as_vanished_without_creating_it();

	/// <summary>A negative position is refused at the entry point, not stored.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_negative_position_argument_Test() => Refuse_a_negative_position_argument();

	/// <summary>A rebuild must never resurrect a row an erasure deleted.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Rebuild_must_not_resurrect_a_deleted_row_Test() => Rebuild_must_not_resurrect_a_deleted_row();

	/// <summary>
	/// Creates the projection table in the shape the store writes, INCLUDING the position column.
	/// </summary>
	/// <remarks>
	/// THE SHIPPED SCRIPT, not a local copy. Three test files each carried their own hand-written
	/// CREATE TABLE and they had already drifted. A store whose columns changed without every copy
	/// changing would still pass here and fail on a consumer's database, which is the one place no gate
	/// of ours can see. Emitting it from the provider's own schema makes this arm the binding.
	/// </remarks>
	/// <param name="table">The physical table to create.</param>
	private async Task EnsureTableAsync(string table)
	{
		if (_provisioned)
		{
			return;
		}

		await using var connection = new SqliteConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using (var journal = connection.CreateCommand())
		{
			// Persistent in the database file, so one statement here governs every later connection
			// including the pooled ones the concurrency arm opens.
			journal.CommandText = "PRAGMA journal_mode=WAL;";
			_ = await journal.ExecuteScalarAsync().ConfigureAwait(false);
		}

		await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // The provider validates the identifier it emits.
		command.CommandText = SqliteProjectionSchema.CreateTransactionalTableScript(table);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		_provisioned = true;
	}

	/// <summary>
	/// A fixed tenant context. The production default reads the ambient holder, which is internal, so a
	/// directly-constructed store needs one supplied.
	/// </summary>
	private sealed class FixedConformanceTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
