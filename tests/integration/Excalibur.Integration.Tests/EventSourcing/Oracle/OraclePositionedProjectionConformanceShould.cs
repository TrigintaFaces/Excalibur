// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using global::Oracle.ManagedDataAccess.Client;

namespace Excalibur.Integration.Tests.EventSourcing.Oracle;

/// <summary>
/// Runs the positioned-projection conformance kit against a REAL Oracle database.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this cannot be a unit test, and why Oracle needs its own derivation.</b> Oracle and SQLite share
/// one implementation of the positioned contract, but they do NOT share the statements it issues: the
/// insert-if-absent arm is a <c>MERGE ... WHEN NOT MATCHED THEN INSERT</c> here and an
/// <c>INSERT ... ON CONFLICT DO NOTHING</c> there. Whether a statement actually refuses the write it
/// should refuse is a property of the ENGINE, so a passing derivation on one provider says nothing about
/// the other. A mocked connection returns whatever it was told and would certify a broken predicate.
/// </para>
/// <para>
/// The arms this kit wires are the compare-and-set protocol that keeps a re-delivered event from being
/// folded twice. Each run gets its own table because several arms assert on a projection being absent
/// and the container is shared across the collection.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "Oracle")]
[Trait("Pattern", "STORE")]
public sealed class OraclePositionedProjectionConformanceShould
	: PositionedProjectionStoreConformanceTestKit, IClassFixture<OracleEventStoreContainerFixture>, IAsyncLifetime
{
	private const string ConformanceTenantId = "tenant-positioned-conformance";

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Usage",
		"CA2213:Disposable fields should be disposed",
		Justification = "xUnit owns and disposes the class fixture.")]
	private readonly OracleEventStoreContainerFixture _fixture;

	// Eight hex rather than a full Guid: an Oracle identifier is bounded, and the store appends its own
	// generation segment to whatever it is handed.
	private readonly string _tableName = "POSPROJ_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

	private bool _provisioned;

	public OraclePositionedProjectionConformanceShould(OracleEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <inheritdoc />
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"an Oracle container must be available -- positioned-projection conformance is never skipped, "
			+ "because an arm that passes by being skipped is indistinguishable from one that passed by "
			+ "working, and the property under test is an engine behaviour we cannot simulate.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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

	/// <summary>A positioned write must not resurrect a row an erasure deleted.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_a_deleted_projection_as_vanished_without_recreating_it_Test() =>
		Report_a_deleted_projection_as_vanished_without_recreating_it();

	/// <summary>Two writers racing from one read: exactly one is admitted.</summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>This is the arm the MERGE path exists for, and the one no mock can answer.</remarks>
	[Fact]
	public Task Admit_exactly_one_of_two_writers_racing_from_one_read_Test() =>
		Admit_exactly_one_of_two_writers_racing_from_one_read();

	/// <summary>An unconditional write invalidates the position it replaced.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Invalidate_the_position_when_an_unconditional_write_replaces_the_state_Test() =>
		Invalidate_the_position_when_an_unconditional_write_replaces_the_state();

	/// <summary>The state and its position are read as one observation.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Read_the_state_and_its_position_as_one_observation_Test() =>
		Read_the_state_and_its_position_as_one_observation();

	/// <summary>
	/// The RE-FOLD arms. Erasure mutates events in place, so the fold beneath a position changes while
	/// the position stays fixed -- a state transition the advancing write cannot express.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
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
	/// That arm drives the row through the blind surface, this one writes a complete fold with no number.
	/// A store discriminating on == Unplaceable rather than != Positioned passes one and fails the other.
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

	/// <inheritdoc />
	protected override async Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync()
	{
		var store = new OracleProjectionStore<ConformanceProjection>(
			_fixture.ConnectionString,
			tenantContext: new FixedConformanceTenantContext(ConformanceTenantId),
			tableName: _tableName,
			jsonOptions: null,
			schemaName: _fixture.Schema);

		// The PHYSICAL name is taken from the store itself rather than re-derived here. The constructor
		// qualifies the base name with the projection's generation and upper-cases it, and a second
		// hand-written derivation would be free to drift from it -- provisioning one table while the
		// store addressed another, which every arm would then report as a missing row rather than as the
		// wiring mistake it is. Only the schema qualifier and the quoting are stripped, because the
		// creation script applies both again -- the identity reads oracle:"SCHEMA"."TABLE".
		var identity = ((IProjectionDestinationIdentity)store).Identity;
		var qualified = identity["oracle:".Length..];
		var physical = qualified[(qualified.LastIndexOf('.') + 1)..].Trim('"');

		await EnsureTableAsync(physical).ConfigureAwait(false);

		return store;
	}

	/// <summary>Creates the projection table in the shape the store writes, including the position column.</summary>
	/// <remarks>
	/// THE SHIPPED SCRIPT, not a local copy. A store whose columns changed without every copy changing
	/// would still pass here and fail on a consumer's database, which is the one place no gate of ours
	/// can see. Emitting it from the provider's own schema makes this arm the binding -- and it is the
	/// binding for the position column's CHECK constraint in particular, which must admit a real
	/// position and not only the three no-number sentinels.
	/// </remarks>
	/// <param name="table">The physical table to create.</param>
	private async Task EnsureTableAsync(string table)
	{
		if (_provisioned)
		{
			return;
		}

		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		_ = await connection.ExecuteAsync(
			OracleProjectionSchema.CreateTransactionalTableScript(table, _fixture.Schema)).ConfigureAwait(false);
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
