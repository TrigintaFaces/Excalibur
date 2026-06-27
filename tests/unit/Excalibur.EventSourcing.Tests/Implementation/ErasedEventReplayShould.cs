// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;
using Excalibur.EventSourcing.InMemory;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Implementation;

/// <summary>
/// Author≠impl regression lock for <c>p6trri</c> (ES: erased (GDPR-tombstoned) events make aggregate load
/// hard-fail PERMANENTLY on replay — SA seam #3).
/// </summary>
/// <remarks>
/// <para>
/// Authored by TestsDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>). The grounded seam is the replay loop of
/// <c>EventSourcedRepository.GetByIdAsync</c>
/// (<c>src/Excalibur/Excalibur.EventSourcing/Implementation/EventSourcedRepository.cs:337-362</c>): an event
/// whose <c>EventType</c> equals <see cref="ErasedEventMarker.EventType"/> (<c>"$erased"</c>) is recognized
/// INLINE — positively and BEFORE any deserialization attempt (:345) — and the repository returns a defined
/// erased sentinel (<c>CreateErasedSentinel</c>, :382). ANY OTHER deserialize failure (unregistered type /
/// corrupt payload) STILL throws via <c>DeserializeEvent</c> (:679-704), so genuine corruption is never
/// masked as erasure. This lock asserts BOTH halves.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> the pre-fix replay loop had no marker check and ran every
/// stored event through <c>DeserializeEvent</c>, which deliberately THROWS on a tombstone it cannot
/// deserialize — so loading an erased aggregate threw permanently. The first fact
/// (<c>ReturnSentinelForFullyErasedStream</c>) fails RED there (it expects a non-null sentinel, not a throw).
/// The second fact (<c>StillThrowOnGenuinelyCorruptEvent</c>) is GREEN on both surfaces and guards the fix
/// against over-reach (it must NOT degrade the no-skip integrity guarantee for real corruption).
/// </para>
/// <para>
/// <b>Real-infra vs unit:</b> deterministic in-process round-trip against the REAL
/// <see cref="InMemoryEventStore"/> and its REAL <see cref="IEventStoreErasure"/> write path (append → erase
/// → reload), not a hand-stubbed tombstone. The marker-recognition branch under test lives in the
/// repository's replay loop and is driven entirely by what <c>LoadAsync</c> returns; the actual tombstone is
/// produced by the store's own <c>EraseEventsAsync</c>, so the erase→reload round-trip is faithfully
/// exercised without any external (Docker) infrastructure — the GDPR tombstone semantics here are
/// in-process, not server-side. Production RED-proof against the live impl is deferred post-commit (impl
/// reserved by another lane).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasedEventReplayShould
{
	private const string AggregateTypeName = "ErasedReplayAggregate";

	internal sealed class ErasedReplayAggregate : AggregateRoot
	{
		public ErasedReplayAggregate() { }
		public ErasedReplayAggregate(string id) : base(id) { }

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No-op: this lock asserts load-time recognition, not applied state.
		}
	}

	private sealed class ReplayTestEvent : IDomainEvent
	{
		public required string EventId { get; init; }
		public required string AggregateId { get; init; }
		public required long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(ReplayTestEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Fact]
	public async Task ReturnSentinelForFullyErasedStream()
	{
		// Arrange — real store, real erasure write path: append a genuine event, then GDPR-erase it.
		var store = new InMemoryEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		_ = await store.AppendAsync(
			aggregateId,
			AggregateTypeName,
			new List<IDomainEvent>
			{
				new ReplayTestEvent { EventId = Guid.NewGuid().ToString(), AggregateId = aggregateId, Version = 0 },
			},
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var erasedCount = await ((IEventStoreErasure)store).EraseEventsAsync(
			aggregateId, AggregateTypeName, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		erasedCount.ShouldBe(1, "the appended event should be tombstoned ($erased)");

		// A serializer that would THROW if asked to resolve/deserialize — proves the marker is recognized
		// BEFORE any deserialization attempt (the serializer must never be invoked for the tombstone).
		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType(A<string>._))
			.Throws(new InvalidOperationException("serializer must not be invoked for an erased tombstone"));

		var repository = new EventSourcedRepository<ErasedReplayAggregate>(
			store,
			serializer,
			id => new ErasedReplayAggregate(id));

		// Act — reload the erased aggregate.
		var result = await repository.GetByIdAsync(aggregateId, CancellationToken.None).ConfigureAwait(false);

		// Assert — defined erased sentinel: non-null, initial state (Version 0), no throw, no deserialize.
		_ = result.ShouldNotBeNull("erased stream must return a defined sentinel, not throw or return null");
		result.Version.ShouldBe(0L);
		A.CallTo(() => serializer.ResolveType(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task StillThrowOnGenuinelyCorruptEvent()
	{
		// Arrange — real store, a genuine (non-erased) event whose type cannot be resolved on load,
		// i.e. real corruption / unregistered type — must NOT be masked as erasure.
		var store = new InMemoryEventStore();
		var aggregateId = Guid.NewGuid().ToString();

		_ = await store.AppendAsync(
			aggregateId,
			AggregateTypeName,
			new List<IDomainEvent>
			{
				new ReplayTestEvent { EventId = Guid.NewGuid().ToString(), AggregateId = aggregateId, Version = 0 },
			},
			-1,
			CancellationToken.None).ConfigureAwait(false);

		// Serializer fails to resolve the (un-erased) event type — simulates an unregistered/corrupt event.
		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType(A<string>._))
			.Throws(new InvalidOperationException("unregistered event type"));

		var repository = new EventSourcedRepository<ErasedReplayAggregate>(
			store,
			serializer,
			id => new ErasedReplayAggregate(id));

		// Act & Assert — strict no-skip integrity is preserved: genuine corruption still fails loud.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.GetByIdAsync(aggregateId, CancellationToken.None)).ConfigureAwait(false);
	}
}
