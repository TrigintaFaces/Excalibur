// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Queries;

/// <summary>
/// Binds the contiguity rule that stops a subscriber silently skipping a committed event.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> The store guarantees that at every INSTANT the committed global positions form
/// a contiguous prefix. That is a predicate on a STATE, and a scan SPANS states — it examines each
/// slot at a different moment and never returns to one it has passed. One writer is enough: the scan
/// passes slot <c>N</c> while <c>N</c> is uncommitted, <c>N</c> commits, <c>N+1</c> commits, the scan
/// reaches <c>N+1</c> and returns it. A caller that advances its high-water mark then never sees
/// <c>N</c> again.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The mutant is deleting the gap check in
/// <see cref="ContiguousGlobalStreamQuery.ReadAllAsync"/> so every row is returned. The first two arms
/// go RED; <see cref="Deliver_every_event_when_the_run_is_contiguous"/> stays green, which is the
/// point — liveness must not depend on the guard.
/// </para>
/// <para>
/// The fake returns positions directly, because what is under test is the DECISION about which
/// prefix to hand back, not any provider's SQL.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ContiguousGlobalStreamQueryShould
{
	/// <summary>SAFETY: a gap truncates the read, so nothing above the hole is delivered.</summary>
	[Fact]
	public async Task Stop_at_the_first_gap()
	{
		var sut = Sut(1, 2, 4, 5);

		var read = await sut.ReadAllAsync(At(0), 100, CancellationToken.None);

		read.Select(static e => e.GlobalPosition).ShouldBe(
			[1, 2],
			"position 3 is missing, so it belongs to an append that has not committed. Delivering 4 and "
			+ "5 would let the caller advance its high-water mark past 3, and no later read from that "
			+ "checkpoint revisits it.");
	}

	/// <summary>
	/// SAFETY, and the case that is easiest to miss: the FIRST returned position must be the one
	/// immediately after the caller's, or nothing may be delivered at all.
	/// </summary>
	/// <remarks>
	/// A guard that only compares adjacent rows passes the arm above and fails this one: 7 and 8 are
	/// contiguous with each other while 6 is still in flight.
	/// </remarks>
	[Fact]
	public async Task Deliver_nothing_when_the_run_does_not_start_at_the_callers_position()
	{
		var sut = Sut(7, 8);

		var read = await sut.ReadAllAsync(At(5), 100, CancellationToken.None);

		read.ShouldBeEmpty(
			"the caller is at 5, so the next deliverable position is 6. It is absent, so 7 and 8 sit "
			+ "above an uncommitted slot however contiguous they are with each other.");
	}

	/// <summary>LIVENESS: a contiguous run is delivered whole.</summary>
	/// <remarks>
	/// Without this the safety arms are satisfied by a decorator that returns nothing at all — the
	/// cheapest way never to skip an event and the most expensive way to be wrong. This arm is also
	/// the one that stays GREEN under the mutant, which is how the mutant proof distinguishes the
	/// guard from the plumbing.
	/// </remarks>
	[Fact]
	public async Task Deliver_every_event_when_the_run_is_contiguous()
	{
		var sut = Sut(4, 5, 6);

		var read = await sut.ReadAllAsync(At(3), 100, CancellationToken.None);

		read.Select(static e => e.GlobalPosition).ShouldBe(
			[4, 5, 6],
			"an unbroken run starting at the caller's position + 1 is fully committed and must be "
			+ "delivered. A guard that withholds it stalls the subscriber permanently.");
	}

	/// <summary>An empty read stays empty rather than throwing.</summary>
	[Fact]
	public async Task Return_empty_when_the_provider_has_nothing()
	{
		var read = await Sut().ReadAllAsync(At(0), 100, CancellationToken.None);

		read.ShouldBeEmpty("no events is not a gap");
	}

	/// <summary>
	/// A filtered read is passed straight through: its positions are LEGITIMATELY sparse.
	/// </summary>
	/// <remarks>
	/// This arm exists to stop a well-meaning change applying the contiguity rule here too. Filtering
	/// by event type means a gap carries no information about whether anything is in flight, so
	/// truncating would discard events that are committed and will never be redelivered.
	/// </remarks>
	[Fact]
	public async Task Not_filter_a_read_that_is_scoped_to_one_event_type()
	{
		var sut = Sut(1, 4, 9);

		var read = await sut.ReadByEventTypeAsync("Whatever", At(0), 100, CancellationToken.None);

		read.Select(static e => e.GlobalPosition).ShouldBe(
			[1, 4, 9],
			"a type-filtered stream is sparse by construction; the gaps carry no information about "
			+ "in-flight transactions and must not truncate the read.");
	}

	/// <summary>
	/// A position BELOW the expected one is a provider CONTRACT VIOLATION and must throw, not be
	/// mistaken for a gap.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that separates two faults with the same surface. A gap means "the next position
	/// is in flight, wait"; an out-of-order or duplicate read means "the provider is broken". Treating
	/// the second as the first stalls every subscriber FOREVER at a position that will never fill, and
	/// it does so quietly, because a stall is what a gap is supposed to produce.
	/// </para>
	/// <para>
	/// <b>Non-vacuity.</b> Delete the <c>actual &lt; expectedNext</c> branch and this arm goes RED while
	/// every other arm stays GREEN -- the deleted branch is reachable only by this input shape.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Refuse_a_read_whose_positions_are_not_ascending()
	{
		// The descending pair must come AFTER a contiguous match, and the first input I wrote did not.
		// Sut(3, 1) from position 0 expects 1, sees 3, and 3 is ABOVE it -- an ordinary gap, so the read
		// truncates to empty and returns before ever reaching the out-of-order element. The arm passed
		// its own deletion check and still never entered the branch. Starting at 1 makes the first
		// element contiguous, so the second is evaluated and is genuinely backwards.
		var sut = Sut(2, 1);

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			async () => await sut.ReadAllAsync(At(1), 100, CancellationToken.None));

		// The caller has to be able to tell a broken provider from an in-flight append: reporting this
		// as a gap would stall them permanently on a position that can never arrive.
		thrown.Message.ShouldContain("ascending");
	}

	/// <summary>A duplicate position is the same contract violation and is refused the same way.</summary>
	[Fact]
	public async Task Refuse_a_read_that_repeats_a_position()
	{
		var sut = Sut(1, 1);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await sut.ReadAllAsync(At(0), 100, CancellationToken.None));
	}

	/// <summary>
	/// A negative caller position is refused, because it would truncate every read to empty forever.
	/// </summary>
	/// <remarks>
	/// The checkpoint column is <c>BIGINT</c> with no <c>CHECK</c> constraint and the stream starts at
	/// 0, so a negative value is reachable through a corrupted or hand-edited checkpoint rather than
	/// through the API. Left unguarded it makes <c>expectedNext</c> non-positive, which no real position
	/// can equal, so the caller stalls permanently and silently -- the exact failure this type exists to
	/// prevent, reached from the other direction.
	/// </remarks>
	[Fact]
	public async Task Refuse_a_negative_caller_position()
	{
		var sut = Sut(1, 2, 3);

		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			async () => await sut.ReadAllAsync(At(-1), 100, CancellationToken.None));
	}

	private static ContiguousGlobalStreamQuery Sut(params long[] positions) =>
		new(new FakeProviderQuery(positions), NullLogger<ContiguousGlobalStreamQuery>.Instance);

	private static GlobalStreamPosition At(long position) => new(position, DateTimeOffset.UnixEpoch);

	/// <summary>
	/// Implements the provider contract DIRECTLY, inheriting no first-party base that could supply the
	/// behaviour under test.
	/// </summary>
	private sealed class FakeProviderQuery(long[] positions) : IGlobalStreamQuery
	{
		private readonly IReadOnlyList<StoredEvent> _events =
			[.. positions.Select(static p => new StoredEvent(
				$"evt-{p}", "agg", "Agg", "Test", [], null, 1, DateTimeOffset.UnixEpoch)
			{
				GlobalPosition = p,
			})];

		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult(_events);

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
			string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult(_events);

		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(_events.Count == 0 ? 0 : _events[^1].GlobalPosition);
	}
}
