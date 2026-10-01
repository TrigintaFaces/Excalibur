// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CosmosDb.Projections;
using Excalibur.Dispatch.Integration.Tests.Cdc;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Runs the positioned-projection conformance kit against the REAL Cosmos DB emulator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this provider needs the emulator more than most.</b> Two independent things can only be
/// settled by the service. First, the conditional write is an ETag compare, and whether the ETag read
/// still identifies the same document version when the write lands is the server's decision. Second —
/// and this is the one that has bitten this framework before — the document is a dictionary whose values
/// came out of one JSON stack, and it is handed to a client whose DEFAULT serializer is a different one.
/// Whether those values survive the round trip as values, rather than as the public properties of
/// whatever type happens to carry them, is a property of the client's configured serializer and nothing
/// in our C# reveals it.
/// </para>
/// <para>
/// The emulator is slow to start, which is a reason to give it a long timeout, not a reason to skip it:
/// an arm that passes by being skipped is indistinguishable from one that passed by working.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Infrastructure", TestInfrastructure.CosmosDb)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CosmosDbPositionedProjectionConformanceTests
	: PositionedProjectionStoreConformanceTestKit, IClassFixture<CosmosDbCdcContainerFixture>
{
	private readonly CosmosDbCdcContainerFixture _fixture;
	private readonly string _containerName = $"positioned{Guid.NewGuid():N}";

	public CosmosDbPositionedProjectionConformanceTests(CosmosDbCdcContainerFixture fixture) =>
		_fixture = fixture;

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
	protected override Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a Cosmos DB emulator must be available -- positioned-projection conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working, and both the ETag condition and the serializer round trip are decided "
			+ "by the service.");

		var options = Microsoft.Extensions.Options.Options.Create(new CosmosDbProjectionStoreOptions
		{
			DatabaseName = CosmosDbCdcContainerFixture.DatabaseId,
			ContainerName = _containerName,
			CreateContainerIfNotExists = true,
			// Supplied even though the fixture's client is injected: the options validate themselves in
			// the constructor and refuse to be constructed without one, independently of whether a
			// client is going to be handed in.
			Client = { ConnectionString = _fixture.ConnectionString },
		});

		return Task.FromResult<IProjectionStore<ConformanceProjection>>(
			new CosmosDbProjectionStore<ConformanceProjection>(
				options,
				NullLogger<CosmosDbProjectionStore<ConformanceProjection>>.Instance,
				_fixture.Client));
	}
}
