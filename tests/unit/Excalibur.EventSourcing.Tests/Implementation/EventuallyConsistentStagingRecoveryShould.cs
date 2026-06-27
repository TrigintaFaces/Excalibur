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
/// Author≠impl regression lock for <c>fqf2xj</c> (ES: eventually-consistent outbox staging is non-atomic
/// with append → partial failure strands integration events).
/// </summary>
/// <remarks>
/// <para>
/// Authored by TestsDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>). The grounded seam is the non-transactional save path of
/// <c>EventSourcedRepository.SaveAsync</c>
/// (<c>src/Excalibur/Excalibur.EventSourcing/Implementation/EventSourcedRepository.cs:434-471</c>). The fix
/// records an appended-but-not-yet-staged breadcrumb (<c>TrackPendingStage</c>, :459) BEFORE staging, so a
/// staging failure leaves a retry trail. A retried <c>SaveAsync</c> for the SAME events matches the
/// breadcrumb (<c>EventIdsMatch</c>, :442) and SKIPS the re-append (<c>alreadyAppended</c>, :444) — the
/// re-append would otherwise raise a stale-version <see cref="ConcurrencyException"/> and orphan the already
/// appended events — then idempotently re-stages and clears the breadcrumb (:470).
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> the pre-fix code had no breadcrumb and always re-appended
/// on retry. This lock arms the faked event store to return success on the first append and a
/// concurrency-conflict on any second append; the pre-fix retry would call <c>AppendAsync</c> a second time
/// → <see cref="ConcurrencyException"/> thrown (RED) and the integration events permanently stranded. On the
/// fixed surface the retry never re-appends (<c>AppendAsync</c> happens exactly once) and completes without
/// throwing (GREEN).
/// </para>
/// <para>
/// <b>Real-infra vs unit:</b> deterministic unit lock. The recovery logic (breadcrumb record / match / skip
/// re-append / idempotent re-stage) lives entirely in the repository orchestration; the boundary collaborators
/// (<see cref="IEventStore"/>, <see cref="IOutboxStore"/>) are exercised only for their call sequence, which
/// faked doubles model faithfully (success-then-conflict append; throw-then-succeed stage). Production
/// RED-proof against the live impl is deferred post-commit (impl reserved by another lane).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventuallyConsistentStagingRecoveryShould
{
	internal sealed record StagingIntegrationEvent : DomainEvent, IIntegrationEvent
	{
		public string Payload { get; init; } = string.Empty;
	}

	internal sealed class StagingAggregate : AggregateRoot
	{
		public StagingAggregate() { }
		public StagingAggregate(string id) : base(id) { }

		public void DoWork(string payload)
		{
			RaiseEvent(new StagingIntegrationEvent { AggregateId = Id, Version = Version, Payload = payload });
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No state mutation needed for this lock.
		}
	}

	[Fact]
	public async Task NotReAppendOnRetryAfterPostAppendStagingFailure()
	{
		// Arrange
		var aggregateId = "agg-staging-recovery";
		var aggregate = new StagingAggregate(aggregateId);
		aggregate.DoWork("order-placed");
		aggregate.GetUncommittedEvents().Count.ShouldBe(1, "one integration event should be uncommitted");

		// Event store: first append succeeds; a SECOND append (the pre-fix re-append) is a concurrency
		// conflict — proving the pre-fix retry would throw and strand the events.
		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0)).Once()
			.Then.Returns(AppendResult.CreateConcurrencyConflict(0, 1));

		// Outbox store: first stage throws (post-append staging failure); subsequent stages succeed
		// (default completed ValueTask), modelling the recoverable re-stage on retry.
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Throws(new TimeoutException("transient outbox failure")).Once();

		var repository = new EventSourcedRepository<StagingAggregate>(
			eventStore,
			A.Fake<IEventSerializer>(),
			id => new StagingAggregate(id),
			outboxStore: outboxStore,
			outboxStagingStrategy: OutboxStagingStrategy.EventuallyConsistent);

		// Act 1 — first SaveAsync: append succeeds, staging fails => failure propagates, breadcrumb left,
		// events remain uncommitted for the retry.
		_ = await Should.ThrowAsync<TimeoutException>(
			() => repository.SaveAsync(aggregate, CancellationToken.None)).ConfigureAwait(false);
		aggregate.GetUncommittedEvents().Count.ShouldBe(1, "events must remain uncommitted after staging failure");

		// Act 2 — retried SaveAsync for the SAME events must NOT re-append (no ConcurrencyException) and
		// must complete by re-staging idempotently.
		await Should.NotThrowAsync(
			() => repository.SaveAsync(aggregate, CancellationToken.None)).ConfigureAwait(false);

		// Assert — core regression: AppendAsync happened exactly ONCE across both attempts (the retry
		// skipped the re-append). Pre-fix this would be twice → the 2nd a concurrency conflict.
		A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Staging was retried (1 failed + 1 succeeded) — the stranded integration event was recovered.
		A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();

		// The retry committed cleanly.
		aggregate.GetUncommittedEvents().Count.ShouldBe(0, "retry should commit the events");
	}
}
