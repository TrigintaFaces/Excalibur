// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Subscriptions;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

/// <summary>
/// Regression lock: a live subscription must ADVANCE past a GDPR-erased (tombstoned) event instead of
/// wedging on it permanently.
/// </summary>
/// <remarks>
/// <para>
/// The seam is <c>EventStoreLiveSubscription.DeserializeEventsWithVersionTracking</c> plus the position
/// update in <c>PollForEventsAsync</c>. Erasure rewrites an event's <c>EventType</c> to the reserved
/// <see cref="ErasedEventMarker.EventType"/> marker and nulls its payload; the marker is a closed,
/// framework-owned discriminator that no event serializer can resolve. Pre-fix the subscription handed the
/// tombstone to the serializer, caught the resulting failure, logged it and BROKE out of the loop, leaving
/// the stream position unadvanced. The very next poll re-read the same tombstone and broke again, so the
/// subscription stopped permanently at the first erased event and never delivered anything appended after
/// it: honouring a data subject's erasure request silently killed the consumer's live projections.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> <see cref="DeliverEventsAppendedAfterAnErasedEvent"/>
/// fails on the pre-fix code, because the post-tombstone event is never delivered.
/// <see cref="NotAdvancePastAGenuinelyUnresolvableEvent"/> is GREEN on both surfaces and guards the fix
/// against over-reach: only the reserved marker is skipped, so real corruption still halts the subscription
/// for retry rather than being silently skipped.
/// </para>
/// <para>
/// <b>Real store, real erasure:</b> the tombstone is produced by the store's own <c>EraseEventsAsync</c>
/// write path, not hand-stubbed, so the erase-then-read round-trip is faithfully exercised. Tombstoning is
/// in-process record rewriting, identical in shape across every provider that implements erasure.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasedEventSubscriptionShould
{
	private sealed class SubscriptionTestEvent : IDomainEvent
	{
		public required string EventId { get; init; }

		public required string AggregateId { get; init; }

		public long Version { get; init; }

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public string EventType { get; init; } = nameof(SubscriptionTestEvent);

		public IDictionary<string, object>? Metadata { get; init; }
	}

	private static EventStoreLiveSubscription CreateSubscription(IEventStore store, IEventSerializer serializer) =>
		new(
			store,
			serializer,
			new EventSubscriptionOptions
			{
				PollingInterval = TimeSpan.FromMilliseconds(20),
				MaxBatchSize = 10,
				StartPosition = SubscriptionStartPosition.Beginning,
			},
			NullLogger<EventStoreLiveSubscription>.Instance);

	[Fact]
	public async Task DeliverEventsAppendedAfterAnErasedEvent()
	{
		// Arrange
		var streamId = Guid.NewGuid().ToString();
		var store = new InMemoryEventStore(UntenantedContext.Instance);

		// A data subject's event ...
		var appended = await store.AppendAsync(
			streamId,
			streamId,
			new List<IDomainEvent>
			{
				new SubscriptionTestEvent { EventId = Guid.NewGuid().ToString(), AggregateId = streamId },
			},
			-1,
			CancellationToken.None).ConfigureAwait(false);
		appended.Success.ShouldBeTrue();

		// ... erased on request: version 0 becomes a tombstone, its stream position preserved.
		var erasedCount = await ((IEventStoreErasure)store).EraseEventsAsync(
			streamId, streamId, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		erasedCount.ShouldBe(1, "the appended event must be tombstoned before the subscription reads it");

		// ... and a genuine, non-erased event appended AFTER the tombstone. A live subscription must reach it.
		var afterErasure = await store.AppendAsync(
			streamId,
			streamId,
			new List<IDomainEvent>
			{
				new SubscriptionTestEvent { EventId = "after-erasure", AggregateId = streamId },
			},
			0,
			CancellationToken.None).ConfigureAwait(false);
		afterErasure.Success.ShouldBeTrue();

		var liveEvent = A.Fake<IDomainEvent>();
		var serializer = A.Fake<IEventSerializer>();

		// The tombstone must be recognized STRUCTURALLY, before any deserialization attempt: if it ever
		// reaches the serializer at all, that is the pre-fix behaviour.
		_ = A.CallTo(() => serializer.ResolveType(ErasedEventMarker.EventType))
			.Throws(new InvalidOperationException("the serializer must never be asked to resolve a tombstone"));
		_ = A.CallTo(() => serializer.ResolveType(A<string>.That.Matches(t => t != ErasedEventMarker.EventType)))
			.Returns(typeof(IDomainEvent));
		_ = A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(liveEvent);

		await using var subscription = CreateSubscription(store, serializer);

		var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var received = new List<IDomainEvent>();

		// Act
		await subscription.SubscribeAsync(
			streamId,
			events =>
			{
				lock (received)
				{
					received.AddRange(events);
				}

				_ = delivered.TrySetResult();
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			delivered.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

		// Assert - the post-tombstone event reached the handler, so the position advanced past the tombstone.
		lock (received)
		{
			received.ShouldNotBeEmpty(
				"a live subscription must advance past an erased event, never stop permanently at it");
		}

		A.CallTo(() => serializer.ResolveType(ErasedEventMarker.EventType)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotAdvancePastAGenuinelyUnresolvableEvent()
	{
		// Arrange - no erasure at all: the first event's type simply cannot be resolved (unregistered type or
		// genuine corruption). That must still halt the subscription so the event is retried, never skipped.
		var streamId = Guid.NewGuid().ToString();
		var store = new InMemoryEventStore(UntenantedContext.Instance);

		var appended = await store.AppendAsync(
			streamId,
			streamId,
			new List<IDomainEvent>
			{
				new SubscriptionTestEvent { EventId = "poison", AggregateId = streamId },
				new SubscriptionTestEvent { EventId = "after-poison", AggregateId = streamId },
			},
			-1,
			CancellationToken.None).ConfigureAwait(false);
		appended.Success.ShouldBeTrue();

		var polled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType(A<string>._))
			.Invokes(() => polled.TrySetResult())
			.Throws(new InvalidOperationException("unregistered event type"));

		await using var subscription = CreateSubscription(store, serializer);

		var received = new List<IDomainEvent>();

		// Act
		await subscription.SubscribeAsync(
			streamId,
			events =>
			{
				lock (received)
				{
					received.AddRange(events);
				}

				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			polled.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

		// Assert - nothing is delivered and nothing is skipped: the poison event stays unread for retry.
		lock (received)
		{
			received.ShouldBeEmpty("a genuinely unresolvable event must halt the subscription, never be skipped");
		}
	}
}
