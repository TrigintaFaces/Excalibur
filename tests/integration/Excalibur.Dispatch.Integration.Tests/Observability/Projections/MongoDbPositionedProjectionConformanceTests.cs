// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.MongoDB.Projections;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Runs the positioned-projection conformance kit against a REAL MongoDB server.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this cannot be a unit test.</b> Every arm in the kit is about what the ENGINE does when two
/// writers collide. This store expresses all three admissible states in ONE statement — a filter pinning
/// the id and requiring that no position exists, plus an upsert — and relies on the unique <c>_id</c> to
/// turn the inadmissible third state into a duplicate-key refusal. Whether the driver actually emits
/// that filter, and whether the server actually refuses, are properties of MongoDB and its driver, not
/// of our C#. A mocked collection returns whatever it was told and would certify a broken filter.
/// </para>
/// <para>
/// Each run gets its own table, because several arms assert on a projection being absent and the
/// container is shared across the collection.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.MongoDB)]
[Trait("Infrastructure", TestInfrastructure.MongoDB)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MongoDbPositionedProjectionConformanceTests : PositionedProjectionStoreConformanceTestKit
{
	private readonly MongoDbContainerFixture _fixture;
	private readonly string _tableName = $"positioned_projection_{Guid.NewGuid():N}";

	public MongoDbPositionedProjectionConformanceTests(MongoDbContainerFixture fixture) => _fixture = fixture;

	/// <summary>Wires the kit's own completeness check, which names any arm this suite forgot.</summary>
	/// <returns>A task representing the check.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	[Fact]
	public Task Create_when_nothing_is_stored_Test() => Create_when_nothing_is_stored();

	[Fact]
	public Task Refuse_a_late_starter_that_claims_absence_Test() => Refuse_a_late_starter_that_claims_absence();

	[Fact]
	public Task Refuse_a_row_that_carries_no_position_Test() => Refuse_a_row_that_carries_no_position();

	// The liveness half of the pair above, and what makes that refusal legitimate rather than a stall:
	// without it the refusal is satisfied by a store that refuses everything.
	[Fact]
	public Task Rebuild_repairs_a_row_that_carries_no_position_Test() =>
		Rebuild_repairs_a_row_that_carries_no_position();

	// The READ side of the two no-number states. Both writes are refused; what differs is which state
	// the row reads back as -- UNPLACEABLE here, UNNUMBERED above -- and a store that dropped the
	// position instead of recording the sentinel passes the refusal and fails this.
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

	// The UNNUMBERED half of the pair above: that arm drives the row through the blind surface, this one
	// writes a complete fold with no number. A store discriminating on == Unplaceable rather than
	// != Positioned passes one and fails the other.
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
		_fixture.DockerAvailable.ShouldBeTrue(
			"a MongoDB container must be available -- positioned-projection conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working, and the property under test is an engine behaviour we cannot simulate.");

		await Task.CompletedTask.ConfigureAwait(false);

		// The store provisions its own collection and indexes, so there is no external DDL to keep in
		// step -- which is also why this provider has no analogue of the relational position column.
		var options = Microsoft.Extensions.Options.Options.Create(new MongoDbProjectionStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = "positioned_conformance",
			CollectionName = _tableName,
		});

		return new MongoDbProjectionStore<ConformanceProjection>(
			options,
			NullLogger<MongoDbProjectionStore<ConformanceProjection>>.Instance);
	}

}
