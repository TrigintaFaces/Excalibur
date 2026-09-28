// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.Data.SqlClient;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Runs the positioned-projection conformance kit against a REAL SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this cannot be a unit test.</b> Every arm in the kit is about what the ENGINE does when two
/// writers collide. The store expresses its condition as a single MERGE whose WHEN MATCHED clause carries
/// both halves of the contract and whose WHEN NOT MATCHED clause fires only for a caller that claimed
/// absence; whether that statement actually refuses the write it should refuse is a property of SQL
/// Server, not of our C#. A mocked connection returns whatever it was told and would certify a broken
/// predicate.
/// </para>
/// <para>
/// Each run gets its own table, because several arms assert on a projection being absent and the
/// container is shared across the collection.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class SqlServerPositionedProjectionConformanceTests : PositionedProjectionStoreConformanceTestKit
{
	private const string ConformanceTenantId = "tenant-positioned-conformance";

	private readonly SqlServerFixture _fixture;
	private readonly string _tableName = $"PositionedProjection_{Guid.NewGuid():N}";

	public SqlServerPositionedProjectionConformanceTests(SqlServerFixture fixture) => _fixture = fixture;

	/// <summary>Wires the kit's own completeness check, which names any arm this suite forgot.</summary>
	/// <returns>A task representing the check.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	[Fact]
	public Task Create_when_nothing_is_stored_Test() => Create_when_nothing_is_stored();

	[Fact]
	public Task Refuse_a_late_starter_that_claims_absence_Test() => Refuse_a_late_starter_that_claims_absence();

	[Fact]
	public Task Adopt_a_row_that_carries_no_position_Test() => Adopt_a_row_that_carries_no_position();

	// The safety half of the pair above: that arm requires a COMPLETE FOLD with no number to be
	// adopted, this one requires a row the blind surface left UNPLACEABLE to be refused. Either
	// alone is satisfiable by a store that treats both the same, which is what every provider did.
	[Fact]
	public Task Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable_Test() =>
		Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable();

	[Fact]
	public Task Advance_from_the_position_it_read_Test() => Advance_from_the_position_it_read();

	[Fact]
	public Task Refuse_a_stale_expected_position_Test() => Refuse_a_stale_expected_position();

	[Fact]
	public Task Refuse_a_position_that_does_not_advance_Test() => Refuse_a_position_that_does_not_advance();

	[Fact]
	public Task Report_a_deleted_projection_as_vanished_without_recreating_it_Test() =>
		Report_a_deleted_projection_as_vanished_without_recreating_it();

	[Fact]
	public Task Admit_exactly_one_of_two_writers_racing_from_one_read_Test() =>
		Admit_exactly_one_of_two_writers_racing_from_one_read();

	[Fact]
	public Task Invalidate_the_position_when_an_unconditional_write_replaces_the_state_Test() =>
		Invalidate_the_position_when_an_unconditional_write_replaces_the_state();

	[Fact]
	public Task Read_the_state_and_its_position_as_one_observation_Test() =>
		Read_the_state_and_its_position_as_one_observation();

	/// <summary>
	/// The RE-FOLD arms. Erasure mutates events in place, so the fold beneath a position changes while
	/// the position stays fixed — a state transition the advancing write cannot express.
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

	/// <inheritdoc />
	protected override async Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a SQL Server container must be available -- positioned-projection conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working, and the property under test is an engine behaviour we cannot simulate.");

		await EnsureTableAsync().ConfigureAwait(false);

		return new SqlServerProjectionStore<ConformanceProjection>(
			_fixture.ConnectionString,
			NullLogger<SqlServerProjectionStore<ConformanceProjection>>.Instance,
			tenantContext: new FixedConformanceTenantContext(ConformanceTenantId),
			tableName: _tableName);
	}

	/// <summary>
	/// Creates the projection table in the shape the store writes, INCLUDING the position column.
	/// </summary>
	/// <remarks>
	/// <b>The default is load-bearing and both halves of it are.</b> <c>NOT NULL</c> stops a row existing
	/// in a state the conditional predicate cannot compare against; <c>-1</c> rather than <c>0</c> because
	/// zero is a legitimate stream position, so a zero default would make a brand-new row claim it had
	/// already folded the first event. The sentinel is what lets a row written through the unconditional
	/// surface be adopted rather than refused forever.
	/// </remarks>
	private async Task EnsureTableAsync()
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		_ = await connection.ExecuteAsync($"""
			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{_tableName}')
			BEGIN
				CREATE TABLE [{_tableName}] (
					TenantId NVARCHAR(200) NOT NULL,
					Id NVARCHAR(450) NOT NULL,
					Data NVARCHAR(MAX) NOT NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					UpdatedAt DATETIMEOFFSET NOT NULL,
					LastAppliedPosition BIGINT NOT NULL CONSTRAINT [DF_{_tableName}_Pos] DEFAULT (-1),
					-- Composite (TenantId, Id), never Id alone: two tenants may legitimately hold the
					-- same projection id, and keying on id alone lets one tenant's upsert overwrite
					-- another's row.
					CONSTRAINT [PK_{_tableName}] PRIMARY KEY (TenantId, Id)
				)
			END
			""").ConfigureAwait(false);
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
