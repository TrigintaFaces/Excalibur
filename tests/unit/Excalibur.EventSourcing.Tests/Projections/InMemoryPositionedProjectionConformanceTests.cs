// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.Testing.Conformance;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Runs the positioned-projection conformance kit against <see cref="InMemoryProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this one is a unit test where the other eight are integration tests.</b> The kit's own remarks
/// require real infrastructure because every interesting arm is about what the ENGINE does when two
/// writers collide, and a mocked client returns whatever it was told. Here the engine IS the code under
/// test: the compare-and-set is a dictionary mutation under this store's own write gate, with nothing
/// mocked and nothing stubbed. There is no fidelity gap to close, so there is no container to gate on —
/// and the arms therefore never skip.
/// </para>
/// <para>
/// The store is constructed per arm, so the arms share no state even though the kit's own identifiers are
/// already unique.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryPositionedProjectionConformanceTests : PositionedProjectionStoreConformanceTestKit
{
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

	[Fact]
	public Task Rebuild_repairs_a_row_that_carries_no_position_Test() =>
		Rebuild_repairs_a_row_that_carries_no_position();

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

	[Fact]
	public Task Refold_the_state_at_the_position_the_row_already_holds_Test() =>
		Refold_the_state_at_the_position_the_row_already_holds();

	[Fact]
	public Task Refuse_a_refold_when_the_row_advanced_after_the_read_Test() =>
		Refuse_a_refold_when_the_row_advanced_after_the_read();

	[Fact]
	public Task Refuse_a_refold_at_a_position_the_row_does_not_hold_Test() =>
		Refuse_a_refold_at_a_position_the_row_does_not_hold();

	[Fact]
	public Task Report_requires_rebuild_for_a_row_with_no_established_position_Test() =>
		Report_requires_rebuild_for_a_row_with_no_established_position();

	[Fact]
	public Task Report_requires_rebuild_for_a_refold_against_an_unnumbered_row_Test() =>
		Report_requires_rebuild_for_a_refold_against_an_unnumbered_row();

	[Fact]
	public Task Apply_the_same_refold_twice_without_changing_the_result_Test() =>
		Apply_the_same_refold_twice_without_changing_the_result();

	[Fact]
	public Task Report_a_refold_against_an_absent_row_as_vanished_without_creating_it_Test() =>
		Report_a_refold_against_an_absent_row_as_vanished_without_creating_it();

	[Fact]
	public Task Refuse_a_negative_position_argument_Test() => Refuse_a_negative_position_argument();

	[Fact]
	public Task Rebuild_must_not_resurrect_a_deleted_row_Test() => Rebuild_must_not_resurrect_a_deleted_row();

	/// <inheritdoc />
	protected override Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync() =>
		Task.FromResult<IProjectionStore<ConformanceProjection>>(
			new InMemoryProjectionStore<ConformanceProjection>());

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task NotLetAFailedFoldChangeCommittedState()
	{
		// THE AUDIT'S COUNTEREXAMPLE, and it needs no second writer, no concurrency and no crash.
		//
		// The store used to hold the caller's object and hand that same object back on a read. So a
		// handler that read the row, mutated it and then threw had ALREADY changed committed state. The
		// retry mutated the same object again and advanced the position over a state that had folded one
		// event twice: seed 1, increment, throw, increment, persist -> 3, where 2 is correct.
		//
		// A refused compare-and-set could not undo it either, because the mutation had happened through
		// the shared reference before any write was attempted. Locking the position check-and-replace
		// protected the position and nothing else.
		var store = new InMemoryProjectionStore<Counter>();
		await store.UpsertAtPositionAsync("c1", new Counter { Count = 1 }, expectedPosition: null, newPosition: 10,
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		// A fold that mutates what it read and then fails.
		var (read, position) = await store.GetWithPositionAsync("c1", TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		read!.Count++;

		// Committed state must be untouched: the fold never completed.
		var (afterFailure, positionAfterFailure) = await store
			.GetWithPositionAsync("c1", TestContext.Current.CancellationToken).ConfigureAwait(false);
		afterFailure!.Count.ShouldBe(
			1, "a fold that mutated its copy and then failed must not have changed the stored row");
		positionAfterFailure.Value.ShouldBe(position.Value);

		// The retry folds the SAME event once, from clean state, and advances.
		var (retryState, retryPosition) = await store
			.GetWithPositionAsync("c1", TestContext.Current.CancellationToken).ConfigureAwait(false);
		retryState!.Count++;
		var outcome = await store.UpsertAtPositionAsync("c1", retryState, retryPosition.Value, 11,
			TestContext.Current.CancellationToken).ConfigureAwait(false);
		outcome.Outcome.ShouldBe(ProjectionAdvanceOutcome.Applied);

		var (settled, _) = await store.GetWithPositionAsync("c1", TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		settled!.Count.ShouldBe(2, "one event folded once -- 3 would mean the failed attempt also counted");
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task NotLetTwoReadersOfOneRowReachEachOther()
	{
		// LIVENESS for the same property: detaching is only useful if each reader gets its OWN object.
		// Two readers holding one instance would see each other's half-applied folds.
		var store = new InMemoryProjectionStore<Counter>();
		await store.UpsertAtPositionAsync("c1", new Counter { Count = 5 }, expectedPosition: null, newPosition: 1,
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		var (first, _) = await store.GetWithPositionAsync("c1", TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		var (second, _) = await store.GetWithPositionAsync("c1", TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		first!.Count = 99;

		second!.Count.ShouldBe(5, "two readers must not share one instance");
		first.ShouldNotBeSameAs(second);
	}

	private sealed class Counter
	{
		public int Count { get; set; }
	}
}
