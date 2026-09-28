// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Conformance kit for <see cref="IPositionedProjectionStore{TProjection}"/> — the contract that makes a
/// re-delivered event refusable.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant every arm below is about.</b> For a projection <c>x</c> with stored position
/// <c>P(x)</c>: <c>state(x) = fold(apply, init, { e : pos(e) &lt;= P(x) })</c>. The position is not a
/// number the writer picks — it asserts WHICH PREFIX of the stream is folded into the state, and a write
/// is admissible only if it makes that true at the instant it commits.
/// </para>
/// <para>
/// <b>Two conjuncts, and a store that enforces only one passes half this kit.</b> A write must advance
/// FROM the position the caller read at (which orders concurrent writers) AND advance FORWARDS (which
/// refuses a re-delivery). They are independent: a store conditioning only on a version token accepts a
/// stale writer that happens to name a high position, and a store conditioning only on monotonicity
/// accepts a writer that folded a state it never read. <c>Refuse_a_position_that_does_not_advance</c> and
/// <c>Refuse_a_stale_expected_position</c> are the arms that separate them.
/// </para>
/// <para>
/// <b>Run this against real infrastructure.</b> Every interesting arm here is about what the ENGINE does
/// when two writers collide, and a mocked client returns whatever it was told — it cannot reproduce a
/// conditional write the server refuses. A kit satisfied by a mock certifies nothing.
/// </para>
/// <para>
/// <b>No arm here skips.</b> Unlike kits whose subject has optional capabilities, the capability IS the
/// subject: a store that does not provide it fails rather than reporting an unverified pass.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyStorePositionedProjectionConformanceTests : PositionedProjectionStoreConformanceTestKit
/// {
///     protected override Task&lt;IProjectionStore&lt;ConformanceProjection&gt;&gt; CreateStoreAsync() =&gt; ...;
///
///     [Fact]
///     public Task Create_when_nothing_is_stored_Test() =&gt; Create_when_nothing_is_stored();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class PositionedProjectionStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a store for the kit's projection type, backed by real infrastructure.
	/// </summary>
	/// <returns>The store under test.</returns>
	/// <remarks>
	/// Return the store as the consumer's apply path receives it — through any decorators it would
	/// normally be wrapped in. A kit run against the bare inner store certifies the provider and says
	/// nothing about the chain, and the chain is where a capability gets silently dropped.
	/// </remarks>
	protected abstract Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync();

	/// <summary>
	/// Resolves the positioned capability, failing if it is absent.
	/// </summary>
	/// <returns>The positioned view of the store under test.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the store does not provide the capability.</exception>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	protected async Task<IPositionedProjectionStore<ConformanceProjection>> CreatePositionedStoreAsync()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false)
			?? throw new InvalidOperationException($"{nameof(CreateStoreAsync)} returned null.");

		return store.GetService(typeof(IPositionedProjectionStore<ConformanceProjection>))
			as IPositionedProjectionStore<ConformanceProjection>
			?? throw new InvalidOperationException(
				$"The store '{store.GetType().Name}' does not provide "
				+ "IPositionedProjectionStore<T>. Without it the apply path writes unconditionally, so an "
				+ "event re-delivered after a restart is folded a second time and an accumulating "
				+ "projection double-counts silently. This kit certifies that capability; a store that "
				+ "lacks it cannot pass, and must not be reported as unverified.");
	}

	/// <summary>A fresh identifier, so arms never collide in a shared backing store.</summary>
	/// <returns>An identifier unique to one arm's run.</returns>
	protected static string NewId() => "conformance-" + Guid.NewGuid().ToString("N");

	/// <summary>
	/// LIVENESS. A caller that read nothing creates the projection.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The arm that stops every safety arm below being satisfied by a store that refuses everything —
	/// the cheapest way never to double-apply, and the most expensive way to be wrong.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Create_when_nothing_is_stored()
	{
		RecordArmExecuted(nameof(Create_when_nothing_is_stored));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		var result = await store
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 10, CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(result, ProjectionAdvanceOutcome.Applied, nameof(Create_when_nothing_is_stored));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(projection is not null, "the created projection must be readable back");
		Require(position.ExpectedPositionOrNull == 10, $"the stored position must be the one written; was {Describe(position)}");
	}

	/// <summary>
	/// SAFETY. A row the unconditional surface left unplaceable must never be adopted.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// This is the arm that separates the two states a single "no position" sentinel used to collapse.
	/// Its sibling, <see cref="Adopt_a_row_that_carries_no_position"/>, requires that a COMPLETE FOLD
	/// with no position number IS adopted. Together they pin the distinction; either alone is
	/// satisfiable by a store that treats both the same, which is exactly what every provider did
	/// before this arm existed.
	/// </para>
	/// <para>
	/// <b>What goes wrong without it.</b> The unconditional write replaces the state with something not
	/// folded from any known prefix. If the next positioned write ADOPTS that row, it folds its batch
	/// onto unknown state and stamps its own position -- and the row then asserts "everything up to P
	/// is folded into me" about a state that does not contain it. Every event below P is missing from
	/// the read model and the stored position says otherwise. It is not bounded either: any later
	/// unconditional write puts the row back, so the miscount recurs without limit.
	/// </para>
	/// <para>
	/// <b>The refusal must be distinguishable from a supersede.</b> A superseded caller re-reads and
	/// retries; re-reading this row yields the same value and the same refusal, so reporting
	/// <see cref="ProjectionAdvanceOutcome.Superseded"/> here is an unbounded redelivery loop. The
	/// projection has to be rebuilt, and only a distinct outcome can say so.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable()
	{
		RecordArmExecuted(nameof(Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var positioned = store.GetService(typeof(IPositionedProjectionStore<ConformanceProjection>))
			as IPositionedProjectionStore<ConformanceProjection>
			?? throw new InvalidOperationException("the store does not provide the positioned capability");

		var id = NewId();

		// POSITION IT FIRST, and this is the half that makes the arm non-vacuous. The defect lives in
		// the transition FROM positioned TO unconditionally-written, and on a store whose upsert is an
		// insert-or-update the two arms are different statements. An arm that only ever writes a fresh
		// id exercises the INSERT arm alone and passes straight over a store that drops the position on
		// UPDATE -- measured: with the update arm reverted to the old sentinel, the fresh-id version of
		// this arm still reported PASS.
		var seeded = await positioned
			.UpsertAtPositionAsync(
				id, Projection(id, 1), expectedPosition: null, newPosition: 5, CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(
			seeded,
			ProjectionAdvanceOutcome.Applied,
			nameof(Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable) + " (precondition)");

		// The blind surface, now landing on a row that HAD a position: the store cannot relate this
		// state to the stream, and the position it used to hold is destroyed.
		await store.UpsertAsync(id, Projection(id, 3), CancellationToken.None).ConfigureAwait(false);

		var (_, blind) = await positioned.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			blind.Kind == ProjectionPositionKind.Unplaceable,
			"an unconditional write must record that the state is UNPLACEABLE, not merely that it has "
			+ "no number. Dropping the position instead makes this row indistinguishable from one "
			+ $"holding a complete fold, which is the defect; read {Describe(blind)}");

		var refused = await positioned
			.UpsertAtPositionAsync(
				id, Projection(id, 4), blind.ExpectedPositionOrNull, newPosition: 7, CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(
			refused,
			ProjectionAdvanceOutcome.Unplaceable,
			nameof(Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable));

		var (projection, after) = await positioned.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			after.Kind == ProjectionPositionKind.Unplaceable,
			$"a refused write must not have moved the position; read {Describe(after)}");
		Require(
			projection?.Total == 3,
			"a refused write must not have changed the state either -- a partial application would be "
			+ "worse than the refusal it accompanies");
	}

	/// <summary>
	/// SAFETY. A caller that read nothing may not overwrite a projection another writer is advancing.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The late starter: a processor that begins after another has been folding for a while reads the
	/// projection before it exists, and must not be allowed to reset it.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Refuse_a_late_starter_that_claims_absence()
	{
		RecordArmExecuted(nameof(Refuse_a_late_starter_that_claims_absence));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 5), expectedPosition: null, newPosition: 50, CancellationToken.None)
			.ConfigureAwait(false);

		var late = await store
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 99, CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(late, ProjectionAdvanceOutcome.Superseded, nameof(Refuse_a_late_starter_that_claims_absence));
		Require(
			late.CurrentPosition == 50,
			$"a refusal must report the position that beat it, so the caller can tell whether it is behind "
			+ $"or already done; reported {Describe(late.CurrentPosition)}");

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(position.ExpectedPositionOrNull == 50, $"the refused write must not have landed; position is {Describe(position)}");
		Require(projection?.Total == 5, "the refused write must not have replaced the state");
	}

	/// <summary>
	/// LIVENESS. A row carrying no position at all is adopted rather than refused forever.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <b>This arm exists because refusing here is a silent PERMANENT STALL, not a conflict.</b> A
	/// projection written through the unconditional surface — a rebuild, a recovery, a row written before
	/// the store recorded positions — carries no position. The caller reads none, so it claims none, so a
	/// create-only branch refuses it; and the next attempt reads none again and is refused identically,
	/// forever. The store must distinguish "a positioned writer owns this" from "nobody has ever
	/// positioned this".
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Adopt_a_row_that_carries_no_position()
	{
		RecordArmExecuted(nameof(Adopt_a_row_that_carries_no_position));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var positioned = store.GetService(typeof(IPositionedProjectionStore<ConformanceProjection>))
			as IPositionedProjectionStore<ConformanceProjection>
			?? throw new InvalidOperationException("the store does not provide the positioned capability");

		var id = NewId();

		// Written the way a rebuild or the save path writes it: a COMPLETE FOLD whose prefix has no
		// global position number. This used to go through the blind UpsertAsync, and that is precisely
		// the conflation this suite now exists to catch -- the blind surface means "not a fold over any
		// prefix", which is the opposite claim and must NOT be adopted. The two cases are separate arms.
		await positioned.UpsertUnnumberedAsync(id, Projection(id, 3), CancellationToken.None)
			.ConfigureAwait(false);

		var (_, beforeAdoption) = await positioned.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			beforeAdoption.Kind == ProjectionPositionKind.Unnumbered,
			"a state written as a complete fold with no position NUMBER must read back as UNNUMBERED -- "
			+ "adoptable. Reading it as unplaceable would refuse adoption on exactly the rows where "
			+ $"adoption is correct; read {Describe(beforeAdoption)}");

		var adopted = await positioned
			.UpsertAtPositionAsync(
				id, Projection(id, 4), beforeAdoption.ExpectedPositionOrNull, newPosition: 7,
				CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(adopted, ProjectionAdvanceOutcome.Applied, nameof(Adopt_a_row_that_carries_no_position));

		var (projection, position) = await positioned.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(position.ExpectedPositionOrNull == 7, $"the adopted row must now carry the written position; was {Describe(position)}");
		Require(projection?.Total == 4, "the adopted row must carry the written state");
	}

	/// <summary>
	/// LIVENESS. A caller that advances from the position it read succeeds.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Advance_from_the_position_it_read()
	{
		RecordArmExecuted(nameof(Advance_from_the_position_it_read));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 20, CancellationToken.None)
			.ConfigureAwait(false);

		var (_, readAt) = await store.GetWithPositionAsync(id, CancellationToken.None).ConfigureAwait(false);

		var result = await store
			.UpsertAtPositionAsync(id, Projection(id, 2), readAt.ExpectedPositionOrNull, newPosition: 21, CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(result, ProjectionAdvanceOutcome.Applied, nameof(Advance_from_the_position_it_read));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(position.ExpectedPositionOrNull == 21, $"the position must advance to the written value; was {Describe(position)}");
		Require(projection?.Total == 2, "the state written alongside the position must be the one stored");
	}

	/// <summary>
	/// SAFETY, first conjunct. A writer holding a stale read is refused even though its position is higher.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <b>The arm a monotonicity-only store fails.</b> Elasticsearch's external versioning, and any
	/// "write if newer" rule, accepts this write: 30 is greater than 25, so it lands — and the state it
	/// lands was folded from position 20, so every event between 20 and 25 is silently dropped from the
	/// projection while its position claims they are in it.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Refuse_a_stale_expected_position()
	{
		RecordArmExecuted(nameof(Refuse_a_stale_expected_position));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 20, CancellationToken.None)
			.ConfigureAwait(false);

		var (_, staleRead) = await store.GetWithPositionAsync(id, CancellationToken.None).ConfigureAwait(false);

		// Another writer advances it while the first holds its read.
		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 2), staleRead.ExpectedPositionOrNull, newPosition: 25, CancellationToken.None)
			.ConfigureAwait(false);

		// The first writer now writes a HIGHER position from its STALE read.
		var stale = await store
			.UpsertAtPositionAsync(id, Projection(id, 99), staleRead.ExpectedPositionOrNull, newPosition: 30, CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(stale, ProjectionAdvanceOutcome.Superseded, nameof(Refuse_a_stale_expected_position));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			position.ExpectedPositionOrNull == 25,
			"a higher position written from a stale read must NOT land: the state it carries was folded "
			+ $"without the events in between. Position is {Describe(position)}");
		Require(projection?.Total == 2, "the stale writer's state must not have replaced the current state");
	}

	/// <summary>
	/// SAFETY, second conjunct. A re-delivery that recomputes the same position is refused.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <b>The arm a compare-and-set-only store fails.</b> The caller obtained its expected value BY
	/// READING IT, so a re-delivered batch satisfies the comparison by construction. Only monotonicity
	/// refuses it — which is precisely why both conjuncts are required and neither substitutes.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Refuse_a_position_that_does_not_advance()
	{
		RecordArmExecuted(nameof(Refuse_a_position_that_does_not_advance));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 4), expectedPosition: null, newPosition: 40, CancellationToken.None)
			.ConfigureAwait(false);

		var (_, readAt) = await store.GetWithPositionAsync(id, CancellationToken.None).ConfigureAwait(false);

		// The batch arrives again. The caller reads 40, folds it, and recomputes 40.
		var replay = await store
			.UpsertAtPositionAsync(id, Projection(id, 8), readAt.ExpectedPositionOrNull, newPosition: 40, CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(replay, ProjectionAdvanceOutcome.Superseded, nameof(Refuse_a_position_that_does_not_advance));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(position.ExpectedPositionOrNull == 40, $"the refused replay must not move the position; it is {Describe(position)}");
		Require(
			projection?.Total == 4,
			"the re-delivered batch must not have been folded a second time -- this is the double-count "
			+ "the whole contract exists to make impossible");
	}

	/// <summary>
	/// SAFETY. A deleted projection is reported as vanished and is NOT recreated.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Deletion is how erasure removes personal data. A store that recreates the row from a replay
	/// reinstates a subject's data after it was erased, which is a compliance failure and not merely a
	/// correctness one.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Report_a_deleted_projection_as_vanished_without_recreating_it()
	{
		RecordArmExecuted(nameof(Report_a_deleted_projection_as_vanished_without_recreating_it));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var positioned = store.GetService(typeof(IPositionedProjectionStore<ConformanceProjection>))
			as IPositionedProjectionStore<ConformanceProjection>
			?? throw new InvalidOperationException("the store does not provide the positioned capability");

		var id = NewId();

		_ = await positioned
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 60, CancellationToken.None)
			.ConfigureAwait(false);

		await store.DeleteAsync(id, CancellationToken.None).ConfigureAwait(false);

		var result = await positioned
			.UpsertAtPositionAsync(id, Projection(id, 2), expectedPosition: 60, newPosition: 61, CancellationToken.None)
			.ConfigureAwait(false);

		AssertOutcome(
			result,
			ProjectionAdvanceOutcome.Vanished,
			nameof(Report_a_deleted_projection_as_vanished_without_recreating_it));

		var (projection, _) = await positioned.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			projection is null,
			"an erased projection must stay erased: a write against a deleted row must report Vanished "
			+ "and must not resurrect it");
	}

	/// <summary>
	/// SAFETY, third obligation. Exactly one of two writers racing from the same read succeeds.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The property the whole contract is for, stated directly. Both writers read the same position and
	/// both name a higher one, so a store enforcing only monotonicity accepts BOTH and the loser's fold
	/// silently replaces the winner's.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Admit_exactly_one_of_two_writers_racing_from_one_read()
	{
		RecordArmExecuted(nameof(Admit_exactly_one_of_two_writers_racing_from_one_read));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 70, CancellationToken.None)
			.ConfigureAwait(false);

		var (_, readAt) = await store.GetWithPositionAsync(id, CancellationToken.None).ConfigureAwait(false);

		var first = store.UpsertAtPositionAsync(
			id, Projection(id, 2), readAt.ExpectedPositionOrNull, newPosition: 71, CancellationToken.None);
		var second = store.UpsertAtPositionAsync(
			id, Projection(id, 3), readAt.ExpectedPositionOrNull, newPosition: 72, CancellationToken.None);

		var outcomes = await Task.WhenAll(first, second).ConfigureAwait(false);
		var applied = outcomes.Count(static o => o.Outcome == ProjectionAdvanceOutcome.Applied);

		Require(
			applied == 1,
			"exactly one writer advancing from the same read may succeed; "
			+ $"{applied.ToString(System.Globalization.CultureInfo.InvariantCulture)} did. Both folded from "
			+ "the same state, so admitting both loses whatever the loser overwrote.");
	}

	/// <summary>
	/// The state and the position a read returns describe the same write.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// <b>Read what this arm does NOT establish before relying on it.</b> The property worth having is
	/// ATOMICITY: a store that fetches state and position in two round trips can return a state from
	/// after a write paired with the position from before it, and the caller then re-folds events
	/// already present with no individual value having been wrong. This arm cannot detect that. It
	/// drives the store sequentially, with no writer interleaved between the two fetches, so a
	/// two-round-trip store passes it.
	/// </para>
	/// <para>
	/// What it does establish is the weaker pairing property: after a write, a read returns THAT
	/// write's state together with THAT write's position, rather than mixing one write's state with
	/// another's. That catches a store which stamps the position from somewhere other than the write it
	/// just performed, which is a real and easy mistake — but it is not atomicity, and the kit does not
	/// claim it. Establishing atomicity needs a writer interleaved between the two fetches, which is
	/// not expressible against an arbitrary store through this contract.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Read_the_state_and_its_position_as_one_observation()
	{
		RecordArmExecuted(nameof(Read_the_state_and_its_position_as_one_observation));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 80, CancellationToken.None)
			.ConfigureAwait(false);

		var (_, first) = await store.GetWithPositionAsync(id, CancellationToken.None).ConfigureAwait(false);

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 2), first.ExpectedPositionOrNull, newPosition: 81, CancellationToken.None)
			.ConfigureAwait(false);

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			projection?.Total == 2 && position.ExpectedPositionOrNull == 81,
			"the state and the position must come from the same instant: read "
			+ $"Total={Describe(projection?.Total)} with position {Describe(position)}, which pairs one "
			+ "write's state with another write's position");
	}

	/// <summary>
	/// SAFETY. An UNCONDITIONAL write onto a positioned row invalidates the position.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// <b>This is the obligation everyone forgets, and the two ways of getting it wrong fail
	/// differently.</b> `UpsertAsync` replaces the state with something the store cannot place in the
	/// stream — it was not folded from any known prefix. If the stored position survives that write,
	/// the row now asserts "everything up to P is folded into me" about a state that does not contain
	/// it, and the very next conditional write FILTERS OUT events as already-applied that were never
	/// applied. Those events are gone from the projection permanently and nothing downstream can tell:
	/// the row is well-formed and every value in it was written correctly.
	/// </para>
	/// <para>
	/// The other failure is benign by comparison and is what a whole-document replacement does for
	/// free: the position is dropped, the row reads as unpositioned, and the next conditional write
	/// adopts it and re-folds one batch, which over-counts for an accumulating projection. That is not
	/// a bounded cost -- a row returns to unpositioned every time an unconditional write lands on it,
	/// so the re-fold recurs. The reason the dropped position is still the better of the two failures
	/// is that the row then describes itself honestly as "prefix unknown", where a surviving stale
	/// position destroys the only evidence that anything is wrong. So the required end state is NO ESTABLISHED POSITION — either absent or
	/// the store's sentinel — never the stale one.
	/// </para>
	/// <para>
	/// Note this is a different case from the adoption arm above, which writes unconditionally to an id
	/// that was never positioned. The defect lives specifically in the transition FROM positioned TO
	/// unconditionally-written, and an arm that only covers a fresh id passes straight over it.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public virtual async Task Invalidate_the_position_when_an_unconditional_write_replaces_the_state()
	{
		RecordArmExecuted(nameof(Invalidate_the_position_when_an_unconditional_write_replaces_the_state));

		var store = await CreateStoreAsync().ConfigureAwait(false)
			?? throw new InvalidOperationException($"{nameof(CreateStoreAsync)} returned null.");

		var positioned = store.GetService(typeof(IPositionedProjectionStore<ConformanceProjection>))
			as IPositionedProjectionStore<ConformanceProjection>
			?? throw new InvalidOperationException("the store does not provide the positioned capability");

		var id = NewId();

		// Establish a position first. THIS is what makes the arm different from the adoption one.
		_ = await positioned
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 90, CancellationToken.None)
			.ConfigureAwait(false);

		var (_, established) = await positioned.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			established.ExpectedPositionOrNull == 90,
			$"the arm cannot test anything unless a position was established first; read {Describe(established)}");

		// Now the unconditional surface writes over it.
		await store.UpsertAsync(id, Projection(id, 7), CancellationToken.None).ConfigureAwait(false);

		var (projection, after) = await positioned.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			projection?.Total == 7,
			"the unconditional write must actually have replaced the state, or this arm proves nothing");

		// TWO assertions, and the second is the one that survives a change to the sentinel. The first
		// pins the shape a caller sees today. The second pins the PROPERTY -- the stale position must
		// not outlive the state it described -- and stays meaningful whatever value a store adopts to
		// mean "no established position", because it names the specific value that must be gone.
		Require(
			after.ExpectedPositionOrNull is null,
			"an unconditional write must leave NO established position: the state it wrote was not folded "
			+ "from any known prefix, so a surviving position asserts a prefix the state does not contain "
			+ $"and the next conditional write silently skips real events. Read {Describe(after)}.");

		Require(
			after != established,
			$"the position {Describe(established)} survived an unconditional write that replaced the "
			+ "state. It now certifies a prefix the stored state does not contain, and the next "
			+ "conditional write will find its equality conjunct satisfied by construction and accept -- "
			+ "filtering out events as already-applied that were never applied. Those events are lost "
			+ "from the projection permanently and nothing downstream can observe it.");
	}

	/// <summary>
	/// LIVENESS. A re-fold rewrites the state and leaves the position exactly where it was.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// <b>Why the operation exists at all.</b> Erasure mutates events IN PLACE, so the fold beneath a
	/// position changes while the position stays fixed. The advancing write cannot express that — it
	/// requires the new position to exceed the stored one, so a caught-up row is refused and the refusal
	/// reads as "someone is ahead, you are done". This arm is the one that fails against a store which
	/// simply forwards a re-fold to the advancing statement.
	/// </para>
	/// <para>
	/// It is the liveness arm for the whole re-fold set: without it, every safety arm below is satisfied
	/// by a store that refuses every re-fold.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Refold_the_state_at_the_position_the_row_already_holds()
	{
		RecordArmExecuted(nameof(Refold_the_state_at_the_position_the_row_already_holds));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 5), expectedPosition: null, newPosition: 100, CancellationToken.None)
			.ConfigureAwait(false);

		// The events at or below 100 changed -- an erasure tombstoned one of them -- so the same prefix
		// now folds to a different state.
		var result = await store
			.RefoldAtPositionAsync(id, Projection(id, 2), atPosition: 100, CancellationToken.None)
			.ConfigureAwait(false);

		AssertRefold(result, ProjectionRefoldOutcome.Applied, nameof(Refold_the_state_at_the_position_the_row_already_holds));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			projection?.Total == 2,
			$"the re-folded state must have replaced the stored one; read Total={Describe(projection?.Total)}. "
			+ "A store that refuses here leaves an erased subject's data in the projection while the "
			+ "caller is told the recovery succeeded.");
		Require(
			position.ExpectedPositionOrNull == 100,
			$"a re-fold must not move the position; it is {Describe(position)}. The prefix the state "
			+ "covers is unchanged -- only its contents moved -- and retreating the position makes the "
			+ "row look behind, so the next batch re-folds events it already has.");
	}

	/// <summary>
	/// SAFETY. A re-fold is refused when another writer advanced the row in the meantime.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <b>This is the arm that proves dropping monotonicity did not drop the ordering conjunct.</b> A
	/// re-fold does not advance, so the only condition left is that the row still holds the position
	/// named. A store that implements the re-fold as an unconditional replace passes every other arm in
	/// this set and fails this one.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Refuse_a_refold_when_the_row_advanced_after_the_read()
	{
		RecordArmExecuted(nameof(Refuse_a_refold_when_the_row_advanced_after_the_read));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 110, CancellationToken.None)
			.ConfigureAwait(false);

		var (_, readAt) = await store.GetWithPositionAsync(id, CancellationToken.None).ConfigureAwait(false);

		// A live processor advances the row between the caller's read and its re-fold.
		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 3), readAt.ExpectedPositionOrNull, newPosition: 111, CancellationToken.None)
			.ConfigureAwait(false);

		var refold = await store
			.RefoldAtPositionAsync(id, Projection(id, 9), atPosition: 110, CancellationToken.None)
			.ConfigureAwait(false);

		AssertRefold(refold, ProjectionRefoldOutcome.Superseded, nameof(Refuse_a_refold_when_the_row_advanced_after_the_read));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(position.ExpectedPositionOrNull == 111, $"the refused re-fold must not move the position; it is {Describe(position)}");
		Require(
			projection?.Total == 3,
			$"the refused re-fold must not have written; read Total={Describe(projection?.Total)}");
		Require(
			refold.CurrentPosition == 111,
			"a refusal must report what the row holds now so the caller can replay against it; "
			+ $"reported {Describe(refold.CurrentPosition)}");
	}

	/// <summary>
	/// SAFETY. A re-fold naming a position the row does not hold is refused.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Distinct from the arm above: there the caller was overtaken in flight, here it arrived stale. Both
	/// must be refused, and a store that compares against the wrong column passes one and fails the other.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Refuse_a_refold_at_a_position_the_row_does_not_hold()
	{
		RecordArmExecuted(nameof(Refuse_a_refold_at_a_position_the_row_does_not_hold));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 4), expectedPosition: null, newPosition: 120, CancellationToken.None)
			.ConfigureAwait(false);

		var refold = await store
			.RefoldAtPositionAsync(id, Projection(id, 8), atPosition: 119, CancellationToken.None)
			.ConfigureAwait(false);

		AssertRefold(refold, ProjectionRefoldOutcome.Superseded, nameof(Refuse_a_refold_at_a_position_the_row_does_not_hold));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(position.ExpectedPositionOrNull == 120, $"the refused re-fold must not move the position; it is {Describe(position)}");
		Require(projection?.Total == 4, $"the refused re-fold must not have written; read Total={Describe(projection?.Total)}");
	}

	/// <summary>
	/// SAFETY. A row carrying no established position yields RequiresRebuild, not Superseded.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The distinction is the whole point of the fourth outcome. <c>Superseded</c> tells the caller to
	/// re-read and try again, which here loops forever: nothing about an unpositioned row changes on its
	/// own, so there is never a position to match. <c>RequiresRebuild</c> is terminal and the caller
	/// escalates.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public virtual async Task Report_requires_rebuild_for_a_row_with_no_established_position()
	{
		RecordArmExecuted(nameof(Report_requires_rebuild_for_a_row_with_no_established_position));

		var store = await CreateStoreAsync().ConfigureAwait(false)
			?? throw new InvalidOperationException($"{nameof(CreateStoreAsync)} returned null.");

		var positioned = store.GetService(typeof(IPositionedProjectionStore<ConformanceProjection>))
			as IPositionedProjectionStore<ConformanceProjection>
			?? throw new InvalidOperationException("the store does not provide the positioned capability");

		var id = NewId();

		_ = await positioned
			.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: 130, CancellationToken.None)
			.ConfigureAwait(false);

		// The unconditional surface invalidates the position -- the established route to an unpositioned
		// row, and the one the framework itself produces.
		await store.UpsertAsync(id, Projection(id, 7), CancellationToken.None).ConfigureAwait(false);

		var (_, invalidated) = await positioned.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			invalidated.Kind == ProjectionPositionKind.Unplaceable,
			"the arm cannot test anything unless the unconditional write left the row UNPLACEABLE first. "
			+ "Reading merely 'no number' here would let a store pass by dropping the position, which is "
			+ $"the conflation this suite exists to catch; read {Describe(invalidated)}");

		var refold = await positioned
			.RefoldAtPositionAsync(id, Projection(id, 2), atPosition: 130, CancellationToken.None)
			.ConfigureAwait(false);

		AssertRefold(
			refold,
			ProjectionRefoldOutcome.RequiresRebuild,
			nameof(Report_requires_rebuild_for_a_row_with_no_established_position));
	}

	/// <summary>
	/// SAFETY. Re-folding twice at the same position is idempotent.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// This arm is what discharges the safety argument for an operation that does not advance. Without
	/// it, "repeating a re-fold is harmless because it writes the same bytes" is prose; with it, it is a
	/// measured property of every provider.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Apply_the_same_refold_twice_without_changing_the_result()
	{
		RecordArmExecuted(nameof(Apply_the_same_refold_twice_without_changing_the_result));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		_ = await store
			.UpsertAtPositionAsync(id, Projection(id, 6), expectedPosition: null, newPosition: 140, CancellationToken.None)
			.ConfigureAwait(false);

		var first = await store
			.RefoldAtPositionAsync(id, Projection(id, 3), atPosition: 140, CancellationToken.None)
			.ConfigureAwait(false);

		var second = await store
			.RefoldAtPositionAsync(id, Projection(id, 3), atPosition: 140, CancellationToken.None)
			.ConfigureAwait(false);

		AssertRefold(first, ProjectionRefoldOutcome.Applied, nameof(Apply_the_same_refold_twice_without_changing_the_result));
		AssertRefold(second, ProjectionRefoldOutcome.Applied, nameof(Apply_the_same_refold_twice_without_changing_the_result));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			projection?.Total == 3 && position.ExpectedPositionOrNull == 140,
			$"a repeated re-fold must leave the row identical; read Total={Describe(projection?.Total)} at "
			+ $"position {Describe(position)}");
	}

	/// <summary>
	/// SAFETY. A re-fold against an ABSENT row reports Vanished and creates nothing.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <b>The highest-value arm in this set.</b> An absent row means the projection was DELETED, deletion
	/// is how erasure removes personal data, and a store that creates the row here reinstates a subject's
	/// data after the erasure removed it — while reporting success. That is a compliance failure, not a
	/// correctness one, and it is invisible from outside: the row is well-formed and every value in it
	/// was written correctly.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Report_a_refold_against_an_absent_row_as_vanished_without_creating_it()
	{
		RecordArmExecuted(nameof(Report_a_refold_against_an_absent_row_as_vanished_without_creating_it));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		var refold = await store
			.RefoldAtPositionAsync(id, Projection(id, 1), atPosition: 150, CancellationToken.None)
			.ConfigureAwait(false);

		AssertRefold(
			refold,
			ProjectionRefoldOutcome.Vanished,
			nameof(Report_a_refold_against_an_absent_row_as_vanished_without_creating_it));

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			projection is null,
			"a re-fold must NEVER create a row: an absent row was deleted, and recreating it reinstates "
			+ "the personal data the deletion removed");
		Require(position.ExpectedPositionOrNull is null, $"nothing must have been written; the row reports position {Describe(position)}");
	}

	private static void AssertRefold(ProjectionRefoldResult result, ProjectionRefoldOutcome expected, string arm) =>
		Require(
			result.Outcome == expected,
			$"{arm}: expected {expected} but the store reported {result.Outcome} "
			+ $"(CurrentPosition {Describe(result.CurrentPosition)})");

	/// <summary>
	/// SAFETY. A negative position is REFUSED at the entry point, not stored.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// A negative in the position field is not merely meaningless — it is unreadable back. Only two of
	/// the eight providers fold a negative to "no position" on read; the other six return the stored
	/// number verbatim, so a planted negative comes back as an ORDINARY POSITION and the advancing
	/// write's own filter matches it. Four of the eight then classify a negative as superseded rather
	/// than requiring a rebuild, which makes recovery retry until it exhausts its attempts and throw a
	/// message that is false — it reports another writer as having advanced the row when none did.
	/// </para>
	/// <para>
	/// Guarding the input is what makes that state inexpressible. Defending the output would leave it
	/// expressible and handled correctly in four places out of eight.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Projection serialization is reflective; a conformance kit exercises the store the consumer configured, whose serializer the kit does not choose.")]
	public virtual async Task Refuse_a_negative_position_argument()
	{
		RecordArmExecuted(nameof(Refuse_a_negative_position_argument));

		var store = await CreatePositionedStoreAsync().ConfigureAwait(false);
		var id = NewId();

		var advanceRefused = false;
		try
		{
			_ = await store
				.UpsertAtPositionAsync(id, Projection(id, 1), expectedPosition: null, newPosition: -2, CancellationToken.None)
				.ConfigureAwait(false);
		}
		catch (ArgumentOutOfRangeException)
		{
			advanceRefused = true;
		}

		Require(advanceRefused, "an advancing write must refuse a negative newPosition rather than store it");

		var refoldRefused = false;
		try
		{
			_ = await store
				.RefoldAtPositionAsync(id, Projection(id, 1), atPosition: -2, CancellationToken.None)
				.ConfigureAwait(false);
		}
		catch (ArgumentOutOfRangeException)
		{
			refoldRefused = true;
		}

		Require(refoldRefused, "a re-fold must refuse a negative atPosition rather than match against it");

		var (projection, position) = await store.GetWithPositionAsync(id, CancellationToken.None)
			.ConfigureAwait(false);

		Require(
			projection is null,
			"a refused call must not have created the row; a negative planted on a create is the shape "
			+ "that comes back as an ordinary position on six of the eight providers");
		Require(position.ExpectedPositionOrNull is null, $"nothing must have been stored; the row reports position {Describe(position)}");
	}

	private static ConformanceProjection Projection(string id, int total) => new() { Id = id, Total = total };

	private static string Describe(long? value) =>
		value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";

	/// <summary>
	/// Renders a position for a failure message, naming the KIND when there is no number.
	/// </summary>
	/// <remarks>
	/// A bare "null" cannot distinguish the two no-number states, and those are exactly the states these
	/// arms exist to keep apart -- a failure message that collapses them sends the reader looking in the
	/// wrong place.
	/// </remarks>
	private static string Describe(ProjectionPosition value) => value.ToString();

	private static string Describe(int? value) =>
		value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";

	private static void AssertOutcome(ProjectionAdvanceResult result, ProjectionAdvanceOutcome expected, string arm) =>
		Require(
			result.Outcome == expected,
			$"{arm}: expected {expected} but the store reported {result.Outcome} "
			+ $"(CurrentPosition {Describe(result.CurrentPosition)})");

	private static void Require(bool condition, string because)
	{
		if (!condition)
		{
			throw new ConformanceAssertionException(because);
		}
	}
}

/// <summary>
/// The projection type <see cref="PositionedProjectionStoreConformanceTestKit"/> stores.
/// </summary>
/// <remarks>
/// <b>It ACCUMULATES on purpose.</b> An assigning projection reaches the same final state whether or not a
/// batch was folded twice, so it passes every arm of this kit against a store with no conditional write at
/// all. The double-count has to be observable for the kit to be measuring anything.
/// </remarks>
public sealed class ConformanceProjection
{
	/// <summary>Gets or sets the projection identifier.</summary>
	/// <value>The identifier the store keys this projection by.</value>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the accumulated total.</summary>
	/// <value>A running total, so a second fold of the same events is visible in the value.</value>
	public int Total { get; set; }
}

/// <summary>
/// Thrown when a conformance arm's expectation does not hold.
/// </summary>
/// <remarks>
/// The kit carries its own failure type rather than depending on an assertion library, so it can be
/// consumed from xUnit, NUnit or MSTest without dragging one of them into a shipped package.
/// </remarks>
public sealed class ConformanceAssertionException : Exception
{
	/// <summary>Initializes a new instance of the <see cref="ConformanceAssertionException"/> class.</summary>
	public ConformanceAssertionException()
	{
	}

	/// <summary>Initializes a new instance of the <see cref="ConformanceAssertionException"/> class.</summary>
	/// <param name="message">What was expected, and what the store did instead.</param>
	public ConformanceAssertionException(string message)
		: base(message)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="ConformanceAssertionException"/> class.</summary>
	/// <param name="message">What was expected, and what the store did instead.</param>
	/// <param name="innerException">The underlying failure.</param>
	public ConformanceAssertionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
