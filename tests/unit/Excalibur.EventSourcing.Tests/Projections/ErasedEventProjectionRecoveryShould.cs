// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Regression lock: recovering a projection for a GDPR-erased aggregate must succeed and produce the
/// empty initial state, rather than throwing and leaving that projection permanently unrecoverable.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no position to assert here, and that is not an omission.</b> Recovery is a one-shot
/// re-apply over a single aggregate's stream, not a checkpointed loop, so it has no position that could
/// fail to advance. The equivalent permanent failure is that <c>ReapplyAsync</c> threw every time it was
/// called for an erased aggregate: pre-fix the tombstone was handed to the serializer, which cannot
/// resolve the reserved marker, and the exception propagated to the caller. That aggregate's projection
/// could never be recovered again.
/// </para>
/// <para>
/// Skipping is the correct outcome for a single-aggregate reader here because the projection is being
/// rebuilt from scratch: a fully erased stream contributes nothing, so the recovered projection holds
/// none of the erased subject's data. That is the intended post-erasure read model, not a partial one.
/// </para>
/// <para>
/// <b>Over-reach guard.</b> <see cref="StillThrowForAGenuinelyUnresolvableEvent"/> is green on the
/// pre-fix surface too, and proves only the reserved marker is skipped.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasedEventProjectionRecoveryShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly InMemoryProjectionStore<OrderSummary> _projectionStore = new();
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();

	private ProjectionRecoveryService CreateService()
	{
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(_projectionStore)
			.BuildServiceProvider();

		return new ProjectionRecoveryService(
			_registry,
			_eventStore,
			_eventSerializer,
			services,
			NullLogger<ProjectionRecoveryService>.Instance);
	}

	private void RegisterProjection()
	{
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Inline();
		builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.Build();
	}

	private void ReturnStream(IReadOnlyList<StoredEvent> events) =>
#pragma warning disable CA2012 // FakeItEasy .Returns stores the ValueTask
		A.CallTo(() => _eventStore.LoadAsync("order-1", A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));
#pragma warning restore CA2012

	[Fact]
	public async Task RecoverAnErasedAggregateToTheEmptyProjectionState()
	{
		// Arrange - the aggregate's whole stream has been tombstoned by an erasure.
		RegisterProjection();
		ReturnStream(
		[
			new("e1", "order-1", "Order", ErasedEventMarker.EventType, [], null, 1, DateTimeOffset.UtcNow),
			new("e2", "order-1", "Order", ErasedEventMarker.EventType, [], null, 2, DateTimeOffset.UtcNow),
		]);

		// The tombstone must be recognized before any deserialization attempt.
		_ = A.CallTo(() => _eventSerializer.ResolveType(ErasedEventMarker.EventType))
			.Throws(new InvalidOperationException("the serializer must never be asked to resolve a tombstone"));

		var service = CreateService();

		// Act - pre-fix this threw, so recovery for an erased aggregate was impossible: RED.
		await service.ReapplyAsync<OrderSummary>("order-1", "Order", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - the projection was recovered, and it carries none of the erased subject's data.
		var recovered = _projectionStore.Get("order-1");
		_ = recovered.ShouldNotBeNull("an erased aggregate must still be recoverable, to its empty state");
		recovered!.EventCount.ShouldBe(0, "no erased event may reach a projection handler");
		recovered.Total.ShouldBe(0m);

		A.CallTo(() => _eventSerializer.ResolveType(ErasedEventMarker.EventType)).MustNotHaveHappened();
	}

	[Fact]
	public async Task StillThrowForAGenuinelyUnresolvableEvent()
	{
		// Arrange - no erasure: an unregistered or corrupt event type must still fail loud, so real
		// data loss is never quietly reclassified as a lawful erasure.
		RegisterProjection();
		ReturnStream(
		[
			new("e1", "order-1", "Order", "UnregisteredEvent", "data"u8.ToArray(), null, 1, DateTimeOffset.UtcNow),
		]);

		_ = A.CallTo(() => _eventSerializer.ResolveType("UnregisteredEvent"))
			.Throws(new InvalidOperationException("unregistered event type"));

		var service = CreateService();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => service.ReapplyAsync<OrderSummary>("order-1", "Order", CancellationToken.None))
			.ConfigureAwait(false);
	}
}
