// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Regression lock: building an on-demand (ephemeral) projection for a GDPR-erased aggregate must
/// succeed and yield the empty initial state, rather than throwing.
/// </summary>
/// <remarks>
/// <para>
/// Like projection recovery, this is a single-aggregate reader with no position that could fail to
/// advance, so the equivalent permanent failure is that <c>BuildAsync</c> threw on every call for an
/// erased aggregate. Any consumer reading that projection got an exception rather than a read model, for
/// as long as the tombstone existed — which is forever.
/// </para>
/// <para>
/// <b>Over-reach guard.</b> <see cref="StillThrowForAGenuinelyUnresolvableEvent"/> is green on the
/// pre-fix surface too, proving only the reserved marker is skipped.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasedEventEphemeralProjectionShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly IEventSerializer _serializer = A.Fake<IEventSerializer>();

	private EphemeralProjectionEngine CreateEngine() =>
		new(_eventStore, _serializer, _registry, NullLogger<EphemeralProjectionEngine>.Instance, cache: null);

	private void RegisterProjection()
	{
		var projection = new MultiStreamProjection<OrderSummary>();
		projection.AddHandler<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Ephemeral,
			projection,
			inlineApply: null));
	}

	private void ReturnStream(params StoredEvent[] events) =>
		A.CallTo(() => _eventStore.LoadAsync("order-1", "Order", A<CancellationToken>._))
			.Returns(events.ToList());

	[Fact]
	public async Task BuildAnErasedAggregateToTheEmptyProjectionState()
	{
		// Arrange - the aggregate's whole stream has been tombstoned by an erasure.
		RegisterProjection();
		ReturnStream(
			new StoredEvent("e1", "order-1", "Order", ErasedEventMarker.EventType, [], null, 1, DateTimeOffset.UtcNow),
			new StoredEvent("e2", "order-1", "Order", ErasedEventMarker.EventType, [], null, 2, DateTimeOffset.UtcNow));

		_ = A.CallTo(() => _serializer.ResolveType(ErasedEventMarker.EventType))
			.Throws(new InvalidOperationException("the serializer must never be asked to resolve a tombstone"));

		var engine = CreateEngine();

		// Act - pre-fix this threw, so the projection was permanently unreadable: RED.
		var result = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - a projection was produced, carrying none of the erased subject's data.
		_ = result.ShouldNotBeNull();
		result.EventCount.ShouldBe(0, "no erased event may reach a projection handler");
		result.Total.ShouldBe(0m);

		A.CallTo(() => _serializer.ResolveType(ErasedEventMarker.EventType)).MustNotHaveHappened();
	}

	[Fact]
	public async Task StillThrowForAGenuinelyUnresolvableEvent()
	{
		// Arrange - no erasure: an unregistered or corrupt event type must still fail loud.
		RegisterProjection();
		ReturnStream(
			new StoredEvent("e1", "order-1", "Order", "UnregisteredEvent", "data"u8.ToArray(), null, 1, DateTimeOffset.UtcNow));

		_ = A.CallTo(() => _serializer.ResolveType("UnregisteredEvent"))
			.Throws(new InvalidOperationException("unregistered event type"));

		var engine = CreateEngine();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None))
			.ConfigureAwait(false);
	}
}
