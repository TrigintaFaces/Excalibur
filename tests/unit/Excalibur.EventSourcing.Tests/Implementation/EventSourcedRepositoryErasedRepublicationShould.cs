// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA1506 // Excessive class coupling -- repository wiring needs many DI collaborators by design

using System.Data;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

using IEventStore = Excalibur.EventSourcing.IEventStore;

namespace Excalibur.EventSourcing.Tests.Implementation;

/// <summary>
/// After an erasure, a retried save MUST NOT republish the erased subject's payloads to the outbox or to
/// inline projections.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> An append commits, its acknowledgement is lost, an erasure tombstones that batch, and
/// the caller — still holding the aggregate the payloads came from — retries. The committed-append identity
/// probe asks "are the rows carrying my event ids present", which stays TRUE after an erasure because
/// erasure is an in-place rewrite and never removes a row. So presence still implies the append committed;
/// what stops being true is that <em>present</em> implies <em>retrievable</em>. The append therefore
/// reported plain success, and the repository fell through to <c>StageIntegrationEventsAsync</c> and
/// <c>IEventNotificationBroker.NotifyAsync</c> with the LIVE in-memory payloads — publishing the erased
/// subject's own events to the outbox and to inline projections AFTER the erasure certificate was issued.
/// Silent: the certificate says completed and <c>IsErasedAsync</c> says erased, and nothing downstream ever
/// learns.
/// </para>
/// <para>
/// <b>Why the root cause is a codomain gap.</b> <see cref="AppendResult"/> could express Success,
/// ConcurrencyConflict and Failure, and could NOT express "committed, but recognised rather than written by
/// this call". Because it could not, the repository could not tell a fresh append from a recognised retry
/// and republished on both. No care at the call site recovers a distinction the type does not carry — hence
/// <see cref="AppendOutcome.AlreadyCommitted"/>.
/// </para>
/// <para>
/// <b>Non-vacuity — the mutant per arm.</b> Each arm names one token whose removal reddens it and nothing
/// else; see the per-arm remarks. Both refusal arms drive the REAL <c>InMemoryEventStore</c>, so the
/// identity probe, the tombstoning and <c>IsErasedAsync</c> are the production ones rather than mocked
/// answers — the probe's post-erasure TRUE is measured, not assumed.
/// </para>
/// <para>
/// <b>Scope.</b> These arms bind the repository's refusal on both recognised-retry paths: the store's
/// identity probe (<see cref="AppendOutcome.AlreadyCommitted"/>) and the in-memory pending-stage
/// breadcrumb. The store-side reporting of that outcome under a real relational failure is a provider
/// concern and is not measured here.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcedRepositoryErasedRepublicationShould
{
	private const string AggregateType = nameof(ErasureOutboxAggregate);

	[Fact]
	public async Task PreservePendingEventsAfterTransactionalStagingWithUnknownCommit()
	{
		var store = A.Fake<ITransactionalEventStore>();
		A.CallTo(() => ((IServiceProvider)store).GetService(typeof(ITransactionalEventStore))).Returns(store);
		var transaction = A.Fake<IDbTransaction>();
		var staged = false;
		A.CallTo(() => store.AppendWithOutboxStagingAsync(
			A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._,
			A<Func<IDbTransaction, CancellationToken, ValueTask>>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var stage = call.GetArgument<Func<IDbTransaction, CancellationToken, ValueTask>>(4)!;
				var token = call.GetArgument<CancellationToken>(5);
				await stage(transaction, token);
				staged = true;
				return AppendResult.CreateUnknown("commit acknowledgement unavailable");
			});
		var outbox = A.Fake<IOutboxStore>();
		var broker = A.Fake<IEventNotificationBroker>();
		var writer = A.Fake<ITransactionalOutboxWriter>();
		var repository = CreateTransactionalRepository(store, outbox, broker, writer);
		var aggregate = new ErasureOutboxAggregate("unknown-transactional-outcome");
		aggregate.DoWork("pending", "stable-transactional-event-id");
		var pending = aggregate.GetUncommittedEvents().ToArray();

		await Should.ThrowAsync<AppendOutcomeUnknownException>(() => repository.SaveAsync(aggregate, CancellationToken.None));

		staged.ShouldBeTrue("uncertainty can arise after the staging callback has run");
		aggregate.GetUncommittedEvents().ShouldBe(pending);
		A.CallTo(() => store.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => outbox.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => broker.NotifyAsync(A<IReadOnlyList<IDomainEvent>>._, A<EventNotificationContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PreservePendingIdentityAndRefuseEffectsForUnknownAppendOutcome()
	{
		var store = A.Fake<IEventStore>();
		A.CallTo(() => store.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<AppendResult>(AppendResult.CreateUnknown("commit acknowledgement unavailable")));
		var outbox = A.Fake<IOutboxStore>();
		var broker = A.Fake<IEventNotificationBroker>();
		var repository = CreateRepository(store, outbox, broker);
		var aggregate = new ErasureOutboxAggregate("unknown-outcome");
		aggregate.DoWork("pending", "stable-event-id");
		var pending = aggregate.GetUncommittedEvents().ToArray();

		await Should.ThrowAsync<AppendOutcomeUnknownException>(() => repository.SaveAsync(aggregate, CancellationToken.None));

		aggregate.GetUncommittedEvents().ShouldBe(pending);
		A.CallTo(() => outbox.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => broker.NotifyAsync(A<IReadOnlyList<IDomainEvent>>._, A<EventNotificationContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// SAFETY — the store recognises its own rows by identity, the stream has since been erased, and the
	/// repository refuses: no outbox stage, no notification, and the defined exception.
	/// </summary>
	/// <remarks>
	/// RED before the fix on all three assertions at once: the store reported plain success, the repository
	/// had no outcome to branch on, and staging plus notification ran from the live payloads. Mutant:
	/// delete the <c>RefuseRepublicationIfErasedAsync</c> call guarded by <c>recognisedRetry</c> in
	/// <c>SaveAsync</c> and this arm goes RED on the outbox assertion first.
	/// </remarks>
	[Fact]
	public async Task Refuse_to_stage_or_notify_when_the_store_recognises_an_append_whose_stream_was_erased()
	{
		// Arrange — a real store, a committed append, then an erasure of that same stream.
		var store = new InMemoryEventStore(UntenantedContext.Instance);
		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var eventId = Guid.NewGuid().ToString();

		var outboxStore = A.Fake<IOutboxStore>();
		var broker = A.Fake<IEventNotificationBroker>();
		var repository = CreateRepository(store, outboxStore, broker);

		var first = new ErasureOutboxAggregate(aggregateId);
		first.DoWork("order-placed", eventId);
		await repository.SaveAsync(first, CancellationToken.None).ConfigureAwait(false);

		await ((IEventStoreErasure)store)
			.EraseEventsAsync(aggregateId, AggregateType, Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		// The erasure landed, and the identity probe would still find the rows — which is precisely why
		// the store cannot refuse this on its own and the repository has to.
		(await ((IEventStoreErasure)store)
			.IsErasedAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the arm is worthless unless the stream really is erased before the retry");

		Fake.ClearRecordedCalls(outboxStore);
		Fake.ClearRecordedCalls(broker);

		// A caller that never learned the first save landed, retrying from a live aggregate carrying the
		// SAME event identity — the shape a lost acknowledgement produces.
		var retry = new ErasureOutboxAggregate(aggregateId);
		retry.DoWork("order-placed", eventId);

		// Act + Assert
		_ = await Should.ThrowAsync<ErasedStreamRepublicationException>(
			() => repository.SaveAsync(retry, CancellationToken.None)).ConfigureAwait(false);

		A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => broker.NotifyAsync(
				A<IReadOnlyList<IDomainEvent>>._, A<EventNotificationContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// SAFETY — the same refusal on the OTHER recognised-retry path: the in-memory pending-stage
	/// breadcrumb, which skips the store round trip entirely and is therefore the more exposed of the two.
	/// </summary>
	/// <remarks>
	/// The first save appends, records the breadcrumb, then fails in staging; the stream is erased; the
	/// retry matches the breadcrumb by event id and never calls the store at all. Mutant: change
	/// <c>var recognisedRetry = alreadyAppended;</c> to <c>var recognisedRetry = false;</c> and this arm
	/// goes RED while the probe arm above stays green — which is what makes the two arms separable.
	/// </remarks>
	[Fact]
	public async Task Refuse_to_re_stage_from_the_pending_breadcrumb_when_the_stream_was_erased()
	{
		// Arrange
		var store = new InMemoryEventStore(UntenantedContext.Instance);
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		// The first staging attempt faults AFTER the append landed, which is what leaves the breadcrumb.
		// A disposed store's throw propagates rather than being absorbed as a duplicate-id no-op.
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Throws(new ObjectDisposedException(nameof(IOutboxStore))).Once();

		var broker = A.Fake<IEventNotificationBroker>();
		var repository = CreateRepository(store, outboxStore, broker);

		var aggregate = new ErasureOutboxAggregate(aggregateId);
		aggregate.DoWork("order-placed", Guid.NewGuid().ToString());

		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => repository.SaveAsync(aggregate, CancellationToken.None)).ConfigureAwait(false);

		aggregate.GetUncommittedEvents().Count.ShouldBe(
			1, "the failed save must leave the live payloads on the aggregate — that is the hazard");

		await ((IEventStoreErasure)store)
			.EraseEventsAsync(aggregateId, AggregateType, Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		Fake.ClearRecordedCalls(outboxStore);
		Fake.ClearRecordedCalls(broker);

		// Act + Assert — the retry matches the breadcrumb and must be refused rather than re-staged.
		_ = await Should.ThrowAsync<ErasedStreamRepublicationException>(
			() => repository.SaveAsync(aggregate, CancellationToken.None)).ConfigureAwait(false);

		A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => broker.NotifyAsync(
				A<IReadOnlyList<IDomainEvent>>._, A<EventNotificationContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// LIVENESS — a recognised retry against a stream that has NOT been erased still stages and still
	/// notifies, exactly as before. Without this arm the safety arms above are satisfied by refusing
	/// everything.
	/// </summary>
	/// <remarks>
	/// Mutant: drop the <c>if (!erased) { return; }</c> early return from
	/// <c>RefuseRepublicationIfErasedAsync</c> and this arm goes RED while both safety arms stay green.
	/// </remarks>
	[Fact]
	public async Task Still_stage_and_notify_on_a_recognised_retry_when_the_stream_was_not_erased()
	{
		// Arrange — identical to the probe arm, minus the erasure.
		var store = new InMemoryEventStore(UntenantedContext.Instance);
		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var eventId = Guid.NewGuid().ToString();

		var outboxStore = A.Fake<IOutboxStore>();
		var broker = A.Fake<IEventNotificationBroker>();
		var repository = CreateRepository(store, outboxStore, broker);

		var first = new ErasureOutboxAggregate(aggregateId);
		first.DoWork("order-placed", eventId);
		await repository.SaveAsync(first, CancellationToken.None).ConfigureAwait(false);

		Fake.ClearRecordedCalls(outboxStore);
		Fake.ClearRecordedCalls(broker);

		var retry = new ErasureOutboxAggregate(aggregateId);
		retry.DoWork("order-placed", eventId);

		// Act
		await Should.NotThrowAsync(
			() => repository.SaveAsync(retry, CancellationToken.None)).ConfigureAwait(false);

		// Assert — the recognised retry behaves exactly as it did before the guard existed.
		A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => broker.NotifyAsync(
				A<IReadOnlyList<IDomainEvent>>._, A<EventNotificationContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	// SAFETY, on the TRANSACTIONAL strategy -- the configuration the shipped relational providers
	// actually run. SqlServer, Postgres and Oracle all implement ITransactionalEventStore, so a
	// guarantee proven only on the eventually-consistent path is proven on the path fewer consumers
	// use. On this path the outbox is staged inside the append, and a recognised retry stages nothing
	// because the store returns before the staging callback runs -- so the residual hazard is the
	// notification alone, and that is what this arm pins.
	//
	// Mutant: neuter the transactional guard's condition and this arm names it. Before this arm
	// existed, that mutation left the suite green.
	[Fact]
	public async Task Refuse_to_notify_on_the_transactional_path_when_the_stream_was_erased()
	{
		var inner = new InMemoryEventStore(UntenantedContext.Instance);
		var store = new TransactionalEventStoreOverInMemory(inner);
		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var eventId = Guid.NewGuid().ToString();

		var outboxStore = A.Fake<IOutboxStore>();
		var broker = A.Fake<IEventNotificationBroker>();
		var transactionalWriter = A.Fake<ITransactionalOutboxWriter>();
		var repository = CreateTransactionalRepository(store, outboxStore, broker, transactionalWriter);

		var first = new ErasureOutboxAggregate(aggregateId);
		first.DoWork("order-placed", eventId);
		await repository.SaveAsync(first, CancellationToken.None).ConfigureAwait(false);

		// The erasure is performed by the REAL store, so the probe the guard consults is measured
		// rather than stubbed.
		_ = await inner.EraseEventsAsync(
			aggregateId, nameof(ErasureOutboxAggregate), Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		// CONTROL: without this, an arm asserting "nothing was notified" would pass over a stream the
		// erasure never touched.
		(await inner.IsErasedAsync(aggregateId, nameof(ErasureOutboxAggregate), CancellationToken.None)
			.ConfigureAwait(false))
			.ShouldBeTrue("the arm below is vacuous unless the stream really is erased");

		Fake.ClearRecordedCalls(broker);

		var retry = new ErasureOutboxAggregate(aggregateId);
		retry.DoWork("order-placed", eventId);

		_ = await Should.ThrowAsync<ErasedStreamRepublicationException>(
			() => repository.SaveAsync(retry, CancellationToken.None)).ConfigureAwait(false);

		A.CallTo(() => broker.NotifyAsync(
				A<IReadOnlyList<IDomainEvent>>._, A<EventNotificationContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static EventSourcedRepository<ErasureOutboxAggregate> CreateTransactionalRepository(
		IEventStore store,
		IOutboxStore outboxStore,
		IEventNotificationBroker broker,
		ITransactionalOutboxWriter transactionalWriter) =>
		new(
			store,
			A.Fake<IEventSerializer>(),
			id => new ErasureOutboxAggregate(id),
			Options.Create(new EventSourcedRepositoryOptions
			{
				OutboxStagingStrategy = OutboxStagingStrategy.Transactional
			}),
			outboxStore: outboxStore,
			eventNotificationBroker: broker,
			transactionalOutboxWriter: transactionalWriter);

	/// <summary>
	/// Adds the transactional seam over the REAL in-memory store, so the append outcome and the
	/// erasure probe are both measured rather than stubbed. Only the transaction is simulated -- the
	/// in-memory store has none, and the staging callback does not need one to prove this property.
	/// </summary>
	private sealed class TransactionalEventStoreOverInMemory : IEventStore, ITransactionalEventStore
	{
		private readonly InMemoryEventStore _inner;

		public TransactionalEventStoreOverInMemory(InMemoryEventStore inner) => _inner = inner;

		// The capability probe is how the repository finds the transactional seam and the erasure
		// capability. Answering for BOTH here is what makes the guard reachable on this path.
		object? IServiceProvider.GetService(Type serviceType) =>
			serviceType == typeof(ITransactionalEventStore) ? this : ((IServiceProvider)_inner).GetService(serviceType);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			_inner.LoadAsync(aggregateId, aggregateType, cancellationToken);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
			_inner.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken);

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events,
			long expectedVersion, CancellationToken cancellationToken) =>
			_inner.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken);

		public async ValueTask<AppendResult> AppendWithOutboxStagingAsync(
			string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events,
			long expectedVersion, Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
			CancellationToken cancellationToken)
		{
			var result = await _inner
				.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken)
				.ConfigureAwait(false);

			// Mirrors the relational stores: staging runs only when this call actually wrote the events.
			// A recognised retry therefore stages nothing, which is why the notification is the only
			// republication route left on this path.
			if (result.Outcome == AppendOutcome.Committed)
			{
				await stageOutbox(null!, cancellationToken).ConfigureAwait(false);
			}

			return result;
		}
	}

	private static EventSourcedRepository<ErasureOutboxAggregate> CreateRepository(
		IEventStore store,
		IOutboxStore outboxStore,
		IEventNotificationBroker broker) =>
		new(
			store,
			A.Fake<IEventSerializer>(),
			id => new ErasureOutboxAggregate(id),
			Options.Create(new EventSourcedRepositoryOptions
			{
				OutboxStagingStrategy = OutboxStagingStrategy.EventuallyConsistent
			}),
			outboxStore: outboxStore,
			eventNotificationBroker: broker);

	[MessageName("Test.Es.ErasureRepublicationIntegrationEvent")]
	internal sealed record ErasureRepublicationIntegrationEvent : DomainEvent, IIntegrationEvent
	{
		public string Payload { get; init; } = string.Empty;
	}

	internal sealed class ErasureOutboxAggregate : AggregateRoot
	{
		public ErasureOutboxAggregate() { }

		public ErasureOutboxAggregate(string id) : base(id) { }

		/// <summary>
		/// Raises the event with a CALLER-SUPPLIED identity, so a retry can carry the same event id a lost
		/// acknowledgement would have left in the store.
		/// </summary>
		public void DoWork(string payload, string eventId) =>
			RaiseEvent(new ErasureRepublicationIntegrationEvent { EventId = eventId, Payload = payload });

		protected override bool ApplyEventInternal(IDomainEvent @event) => true;
	}
}
