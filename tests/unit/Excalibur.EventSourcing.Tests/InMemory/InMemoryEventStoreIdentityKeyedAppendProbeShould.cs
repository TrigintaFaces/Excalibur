// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// The committed-append probe must be keyed by IDENTITY, not by a version slot.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> The probe read the row sitting at <c>expectedVersion + 1</c> and compared its event
/// id. That asks "is our event at the version we expected", which is blind to an append that committed at
/// SOME OTHER version: it reads that as absent and returns a concurrency conflict, and the documented
/// remedy for a conflict is reload-and-retry, which appends the same business event again at the next
/// version — the duplicate the probe exists to prevent. <c>MongoDbEventStore</c>'s own probe says so in as
/// many words; this store did the opposite while being the reference the provider conformance suites are
/// read against, which made every other provider's arm look stronger than it was.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The safety arm seeds two appends so the recognised event sits at version 1 while the
/// retry passes <c>expectedVersion: -1</c> — the slot the old probe would read is index 0, which holds a
/// DIFFERENT event. On the pre-fix surface that arm is RED with a concurrency conflict. Restore the slot
/// lookup and it reddens again. The liveness arm keeps the store able to report a genuine conflict, so a
/// probe that answered "recognised" to everything fails it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class InMemoryEventStoreIdentityKeyedAppendProbeShould
{
	private const string AggregateType = "Order";

	/// <summary>
	/// SAFETY — an append that committed at a version other than the one expected is found BY IDENTITY and
	/// reported as already committed, not as a conflict.
	/// </summary>
	[Fact]
	public async Task Recognise_an_append_that_committed_at_another_version()
	{
		// Arrange — two separate appends, so the second event lands at version 1.
		var store = new InMemoryEventStore(UntenantedContext.Instance);
		var aggregateId = Guid.NewGuid().ToString();
		var strandedEventId = Guid.NewGuid().ToString();

		_ = await store.AppendAsync(
			aggregateId, AggregateType, [Event(aggregateId, Guid.NewGuid().ToString())], -1, CancellationToken.None)
			.ConfigureAwait(false);

		_ = await store.AppendAsync(
			aggregateId, AggregateType, [Event(aggregateId, strandedEventId)], 0, CancellationToken.None)
			.ConfigureAwait(false);

		// Act — retry the SECOND append as a caller would after losing its acknowledgement AND its place:
		// it asks for -1, so the slot the old probe would read (index 0) holds a different event entirely.
		var retry = await store.AppendAsync(
			aggregateId, AggregateType, [Event(aggregateId, strandedEventId)], -1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		retry.IsConcurrencyConflict.ShouldBeFalse(
			"the rows carrying this call's event id are durably present, so a conflict would send the caller "
			+ "to reload-and-retry and write the same business event a second time at the next version");
		retry.Outcome.ShouldBe(AppendOutcome.AlreadyCommitted);
		retry.NextExpectedVersion.ShouldBe(
			1, "the probe reports the version the batch ACTUALLY reached, read from the row");

		// And nothing was written a second time.
		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);
		loaded.Count.ShouldBe(2);
	}

	/// <summary>
	/// LIVENESS — a genuine conflict is still a conflict. Without this, a probe that recognised everything
	/// would satisfy the arm above.
	/// </summary>
	[Fact]
	public async Task Still_report_a_conflict_for_an_append_that_never_landed()
	{
		var store = new InMemoryEventStore(UntenantedContext.Instance);
		var aggregateId = Guid.NewGuid().ToString();

		_ = await store.AppendAsync(
			aggregateId, AggregateType, [Event(aggregateId, Guid.NewGuid().ToString())], -1, CancellationToken.None)
			.ConfigureAwait(false);

		// A different writer's event, at a version somebody else already took.
		var loser = await store.AppendAsync(
			aggregateId, AggregateType, [Event(aggregateId, Guid.NewGuid().ToString())], -1, CancellationToken.None)
			.ConfigureAwait(false);

		loser.Success.ShouldBeFalse();
		loser.Outcome.ShouldBe(AppendOutcome.ConcurrencyConflict);
	}

	/// <summary>
	/// LIVENESS — an ordinary, uncontended append still reports that THIS call wrote the events, so the
	/// discriminator distinguishes the two successes rather than collapsing them.
	/// </summary>
	[Fact]
	public async Task Report_a_fresh_append_as_committed_by_this_call()
	{
		var store = new InMemoryEventStore(UntenantedContext.Instance);
		var aggregateId = Guid.NewGuid().ToString();

		var result = await store.AppendAsync(
			aggregateId, AggregateType, [Event(aggregateId, Guid.NewGuid().ToString())], -1, CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeTrue();
		result.Outcome.ShouldBe(AppendOutcome.Committed);
	}

	private static IDomainEvent Event(string aggregateId, string eventId) =>
		new IdentityProbeTestDomainEvent { EventId = eventId, AggregateId = aggregateId };
}

[MessageName("Test.Es.IdentityProbeTestDomainEvent")]
public sealed class IdentityProbeTestDomainEvent : IDomainEvent
{
	public required string EventId { get; init; }

	public required string AggregateId { get; init; }

	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

	public IDictionary<string, object>? Metadata { get; init; }
}
