// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Excessive class coupling -- repository wiring needs many DI collaborators by design

using Excalibur.Dispatch;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Shouldly;

using Xunit;

using IEventStore = Excalibur.EventSourcing.IEventStore;
using AppendResult = Excalibur.EventSourcing.AppendResult;

namespace Excalibur.EventSourcing.Tests.Implementation;

/// <summary>
/// Author≠impl regression lock for <c>r09b2d</c> (sprint 855, FR-C1 / EC-5): the eventually-consistent
/// outbox-staging catch in <see cref="EventSourcedRepository{TAggregate}"/> MUST be scoped to the
/// duplicate-id contract only — it MUST NOT swallow a disposed/faulted outbox store as an idempotent
/// no-op (a silent integration-event drop, data-loss-adjacent).
/// </summary>
/// <remarks>
/// <para>
/// Authored by TestsDeveloper independently of the fix (<c>issue-remediation-protocol</c>). Grounded seam:
/// <c>StageIntegrationEventsAsync</c>
/// (<c>src/Excalibur/Excalibur.EventSourcing/Implementation/EventSourcedRepository.cs:867-878</c>) catches
/// the bare <c>InvalidOperationException</c> to treat a duplicate-id re-stage as a no-op (FR-A5). Because
/// <see cref="ObjectDisposedException"/> derives from <see cref="InvalidOperationException"/>, a
/// disposed/faulted outbox store's throw is currently swallowed too — the integration event is dropped
/// silently. <c>IOutboxStore.StageMessageAsync</c>'s contract (<c>IOutboxStore.cs:48</c>) scopes the
/// thrown <see cref="InvalidOperationException"/> to the duplicate-id case only. Fix: filter with
/// <c>when (ex is not ObjectDisposedException)</c> (or a dedicated <c>DuplicateOutboxMessageException</c>).
/// </para>
/// <para>
/// <b>Non-vacuity (Fact 1 RED on the pre-fix surface):</b> with the bare <c>catch (InvalidOperationException)</c>,
/// the disposed-store <see cref="ObjectDisposedException"/> is swallowed and <c>SaveAsync</c> returns
/// normally — so asserting the exception PROPAGATES fails (RED). On the fixed surface the filter lets it
/// through (GREEN). Fact 2 is the EC-5 second-half guard (genuine duplicate-id stays an idempotent no-op);
/// it is GREEN on both surfaces and ensures the fix does not over-narrow the legitimate dedup path.
/// </para>
/// <para>
/// <b>Real-infra vs unit:</b> deterministic unit lock — consistent with the sibling
/// <c>EventuallyConsistentStagingRecoveryShould</c> (fqf2xj) lock for the same repository orchestration.
/// The defect is a purely client-side catch-filter; the boundary collaborator is exercised only for the
/// exact exception type it raises (<see cref="ObjectDisposedException"/> from a disposed store — the real
/// <c>InMemoryOutboxStore.StageMessageAsync</c> throws exactly this via
/// <c>ObjectDisposedException.ThrowIf(_disposed, this)</c>; a plain <see cref="InvalidOperationException"/>
/// for a duplicate id). No server-side semantics are mocked away (<c>verify-against-real-infra-not-mock</c>
/// clause 5 — pure logic). A real-disposed-store variant is available if the integrator prefers it
/// (would add an <c>Excalibur.Outbox.InMemory</c> project reference).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcedRepositoryDisposedOutboxStoreShould
{
	internal sealed record OutboxIntegrationEvent : DomainEvent, IIntegrationEvent
	{
		public string Payload { get; init; } = string.Empty;
	}

	internal sealed class OutboxAggregate : AggregateRoot
	{
		public OutboxAggregate() { }
		public OutboxAggregate(string id) : base(id) { }

		public void DoWork(string payload)
		{
			RaiseEvent(new OutboxIntegrationEvent { AggregateId = Id, Version = Version, Payload = payload });
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No state mutation needed for this lock.
		}
	}

	private static IEventStore FakeEventStoreThatAppendsSuccessfully()
	{
		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));
		return eventStore;
	}

	// ===== Fact 1: a disposed/faulted outbox store's exception PROPAGATES (RED pre-fix) =====

	[Fact]
	public async Task PropagateObjectDisposedExceptionFromStagingInsteadOfSwallowingIt()
	{
		// Arrange
		var aggregate = new OutboxAggregate("agg-disposed-outbox");
		aggregate.DoWork("order-placed");
		aggregate.GetUncommittedEvents().Count.ShouldBe(1, "one integration event should be uncommitted");

		var eventStore = FakeEventStoreThatAppendsSuccessfully();

		// A disposed outbox store throws ObjectDisposedException (: InvalidOperationException) from
		// StageMessageAsync — exactly what the real InMemoryOutboxStore does via ObjectDisposedException.ThrowIf.
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Throws(new ObjectDisposedException(nameof(IOutboxStore)));

		var repository = new EventSourcedRepository<OutboxAggregate>(
			eventStore,
			A.Fake<IEventSerializer>(),
			id => new OutboxAggregate(id),
			outboxStore: outboxStore,
			outboxStagingStrategy: OutboxStagingStrategy.EventuallyConsistent);

		// Act + Assert — the disposed/faulted store must surface, NOT be silently swallowed as a dup-id no-op.
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => repository.SaveAsync(aggregate, CancellationToken.None)).ConfigureAwait(false);

		// The store was actually reached (the failure is on the staging path, not earlier).
		A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	// ===== Fact 2 (EC-5 guard): a genuine duplicate-id stays an idempotent no-op (GREEN pre+post) =====

	[Fact]
	public async Task PreserveIdempotentNoOpForGenuineDuplicateId()
	{
		// Arrange
		var aggregate = new OutboxAggregate("agg-duplicate-id");
		aggregate.DoWork("order-placed");

		var eventStore = FakeEventStoreThatAppendsSuccessfully();

		// A genuine duplicate-id stage throws a plain InvalidOperationException per IOutboxStore's contract
		// (the real InMemoryOutboxStore: "Message with ID '...' already exists in the outbox.").
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Message with ID 'dup' already exists in the outbox."));

		var repository = new EventSourcedRepository<OutboxAggregate>(
			eventStore,
			A.Fake<IEventSerializer>(),
			id => new OutboxAggregate(id),
			outboxStore: outboxStore,
			outboxStagingStrategy: OutboxStagingStrategy.EventuallyConsistent);

		// Act + Assert — the duplicate-id contract is still treated as an idempotent no-op: SaveAsync completes,
		// the append happened, and the events are committed. The fix must NOT over-narrow this legitimate path.
		await Should.NotThrowAsync(
			() => repository.SaveAsync(aggregate, CancellationToken.None)).ConfigureAwait(false);

		A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		aggregate.GetUncommittedEvents().Count.ShouldBe(0, "duplicate-id no-op should let the save commit cleanly");
	}
}
