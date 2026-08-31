// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly -- fakes store ValueTask in setup

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// y0robr Half 2: for projections, the rebuild path IS the erasure path -- a projection rebuilt from a
/// stream containing a GDPR-tombstoned event must show no trace of the erased subject, and a different
/// subject's data on the same projection must survive.
/// </summary>
/// <remarks>
/// Before this fix, <see cref="ProjectionRebuildService"/> had no structural recognition of
/// <see cref="ErasedEventMarker"/> (unlike <c>EventSourcedRepository</c>'s aggregate rehydration path, which
/// does). A tombstoned event's EventType ("$erased") does not resolve via <c>IEventSerializer</c>, so the
/// rebuild's poison-event halt (bd-red2ha / ADR-336 Amendment 3a -- deliberate, tested in
/// <see cref="ProjectionPoisonHaltParityShould"/>) treated every tombstone as corruption: erasing ANY
/// subject whose aggregate fed a projection made that projection permanently unrebuildable, never merely
/// silent on the erased subject.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionRebuildErasureShould
{
	[Fact]
	public async Task OmitTheErasedSubjectButRetainAnotherSubjectAndCompleteTheRebuild()
	{
		// Arrange -- one tombstoned event for the erased subject's aggregate (as EraseEventsAsync leaves
		// it: EventType overwritten to the reserved marker, payload replaced) and one live event for a
		// different subject's aggregate.
		var erasedSubjectEvent = new StoredEvent(
			"evt-erased", "agg-subject-a", "TestAggregate", ErasedEventMarker.EventType,
			"ERASED"u8.ToArray(), null, 0, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 1,
		};
		var survivingSubjectEvent = new StoredEvent(
			"evt-live", "agg-subject-b", "TestAggregate", "TestEvent",
			"data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 2,
		};

		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var readCount = 0;
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var call = Interlocked.Increment(ref readCount);
				return call == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new[] { erasedSubjectEvent, survivingSubjectEvent })
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		var eventSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => eventSerializer.ResolveType("TestEvent")).Returns(typeof(SubjectTouchedEvent));
		A.CallTo(() => eventSerializer.DeserializeEvent(A<byte[]>._, typeof(SubjectTouchedEvent)))
			.ReturnsLazily(() => new SubjectTouchedEvent { AggregateId = "agg-subject-b" });
		// "$erased" is never a registered event type (ErasedEventMarker.EventType's own contract), so a
		// real IEventSerializer cannot resolve it. Without this stub, FakeItEasy's lenient unstubbed
		// default (a benign fake Type/IDomainEvent) would mask the pre-fix defect entirely -- the rebuild
		// would silently "succeed" against a call that should never happen, defeating the RED proof.
		A.CallTo(() => eventSerializer.ResolveType(ErasedEventMarker.EventType))
			.Throws(() => new InvalidOperationException(
				$"'{ErasedEventMarker.EventType}' is a reserved erasure marker, not a registered event type."));

		var store = new InMemoryProjectionStore<SubjectTrackingProjection>();
		var projection = new MultiStreamProjection<SubjectTrackingProjection>();
		projection.AddContextHandler<SubjectTouchedEvent>(
			static (state, evt, ctx) => state.SeenAggregateIds.Add(ctx.AggregateId));

		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(IGlobalStreamQuery))).Returns(globalQuery);
		A.CallTo(() => serviceProvider.GetService(typeof(MultiStreamProjection<SubjectTrackingProjection>)))
			.Returns(projection);
		A.CallTo(() => serviceProvider.GetService(typeof(IProjectionStore<SubjectTrackingProjection>)))
			.Returns(store);

		var service = new ProjectionRebuildService(
			serviceProvider,
			eventSerializer,
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions
			{
				BatchSize = 500,
				BatchDelay = TimeSpan.Zero,
			}),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		await service.RebuildAsync<SubjectTrackingProjection>(CancellationToken.None);

		// Assert -- LIVENESS first: the rebuild must actually complete, not halt at the tombstone (the
		// defect this test guards: pre-fix, the tombstone was treated as poison and the rebuild never
		// reached the surviving subject's event at all).
		var status = await service.GetStatusAsync<SubjectTrackingProjection>(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Completed);

		var persisted = await store.GetByIdAsync(nameof(SubjectTrackingProjection), CancellationToken.None);
		persisted.ShouldNotBeNull();

		// SAFETY: the erased subject's aggregate id never reached the projection.
		persisted.SeenAggregateIds.ShouldNotContain("agg-subject-a");

		// LIVENESS: a different subject's data on the same projection survives the erasure untouched.
		persisted.SeenAggregateIds.ShouldContain("agg-subject-b");

		// The tombstone must never reach the serializer -- recognized structurally, not by a failed
		// deserialization attempt.
		A.CallTo(() => eventSerializer.ResolveType(ErasedEventMarker.EventType)).MustNotHaveHappened();
	}

	private sealed class SubjectTouchedEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public string AggregateId { get; init; } = string.Empty;

		public long Version { get; init; }

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public string EventType { get; init; } = nameof(SubjectTouchedEvent);

		public IDictionary<string, object>? Metadata { get; init; }
	}
}

/// <summary>Projection state used to prove which subjects' data actually reached a rebuilt projection.</summary>
public sealed class SubjectTrackingProjection
{
	public List<string> SeenAggregateIds { get; } = [];
}

#pragma warning restore CA2012
