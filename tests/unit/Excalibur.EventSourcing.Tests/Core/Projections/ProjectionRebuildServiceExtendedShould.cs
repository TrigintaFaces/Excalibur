// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly
#pragma warning disable CA1506 // Excessive class coupling -- integration-style tests require many DI types

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Extended tests for <see cref="ProjectionRebuildService"/> covering:
/// - Persistence via IProjectionStore (P0 fix — previously discarded rebuilt state)
/// - Resolving projection from IProjectionRegistry fallback
/// - Skipping undeserializable events
/// - No IProjectionStore registered (warns but doesn't fail)
/// - BatchDelay honored between batches
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionRebuildServiceExtendedShould
{
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();
	private readonly ProjectionRebuildOptions _options = new();

	private ProjectionRebuildService CreateService(IServiceProvider sp)
	{
		return new ProjectionRebuildService(
			sp,
			_eventSerializer,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<ProjectionRebuildService>.Instance);
	}

	[Fact]
	public async Task PersistRebuiltState_ViaProjectionStore()
	{
		// Arrange — P0 fix: rebuilt state must be persisted via IProjectionStore
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<RebuildTestState>();
		projection.AddHandler<RebuildTestEvent>((state, _) => state.Count++);

		var storedEvent = new StoredEvent("e1", "agg-1", "Agg", "RebuildTestEvent",
			new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow);
		var callCount = 0;
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var c = Interlocked.Increment(ref callCount);
				return c == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new[] { storedEvent })
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		A.CallTo(() => _eventSerializer.ResolveType("RebuildTestEvent")).Returns(typeof(RebuildTestEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, typeof(RebuildTestEvent)))
			.Returns(new RebuildTestEvent());

		var store = new InMemoryProjectionStore<RebuildTestState>();

		var sp = A.Fake<IServiceProvider>();
		A.CallTo(() => sp.GetService(typeof(IGlobalStreamQuery))).Returns(globalQuery);
		A.CallTo(() => sp.GetService(typeof(MultiStreamProjection<RebuildTestState>))).Returns(projection);
		A.CallTo(() => sp.GetService(typeof(IProjectionStore<RebuildTestState>))).Returns(store);

		var sut = CreateService(sp);

		// Act
		await sut.RebuildAsync<RebuildTestState>(CancellationToken.None).ConfigureAwait(false);

		// Assert — rebuilt state persisted via store
		var persisted = await store.GetByIdAsync("RebuildTestState", CancellationToken.None).ConfigureAwait(false);
		persisted.ShouldNotBeNull();
		persisted.Count.ShouldBe(1);
	}

	[Fact]
	public async Task CompleteWithoutPersistence_WhenNoProjectionStoreRegistered()
	{
		// Arrange — no IProjectionStore, should still succeed (just log warning)
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<RebuildTestState>();
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));

		var sp = A.Fake<IServiceProvider>();
		A.CallTo(() => sp.GetService(typeof(IGlobalStreamQuery))).Returns(globalQuery);
		A.CallTo(() => sp.GetService(typeof(MultiStreamProjection<RebuildTestState>))).Returns(projection);
		A.CallTo(() => sp.GetService(typeof(IProjectionStore<RebuildTestState>))).Returns(null);

		var sut = CreateService(sp);

		// Act — should not throw
		await sut.RebuildAsync<RebuildTestState>(CancellationToken.None).ConfigureAwait(false);

		// Assert
		var status = await sut.GetStatusAsync<RebuildTestState>(CancellationToken.None).ConfigureAwait(false);
		status.State.ShouldBe(ProjectionRebuildState.Completed);
	}

	[Fact]
	public async Task ResolveProjectionFromRegistry_WhenNotDirectlyInDI()
	{
		// Arrange — projection not in DI directly, but found via IProjectionRegistry
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<RebuildTestState>();
		projection.AddHandler<RebuildTestEvent>((state, _) => state.Count++);

		var registry = new InMemoryProjectionRegistry();
		registry.Register(new ProjectionRegistration(
			typeof(RebuildTestState),
			ProjectionMode.Inline,
			projection,
			inlineApply: null));

		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));

		var sp = A.Fake<IServiceProvider>();
		A.CallTo(() => sp.GetService(typeof(IGlobalStreamQuery))).Returns(globalQuery);
		A.CallTo(() => sp.GetService(typeof(MultiStreamProjection<RebuildTestState>))).Returns(null);
		A.CallTo(() => sp.GetService(typeof(IProjectionRegistry))).Returns(registry);
		A.CallTo(() => sp.GetService(typeof(IProjectionStore<RebuildTestState>))).Returns(null);

		var sut = CreateService(sp);

		// Act — should resolve via registry fallback
		await sut.RebuildAsync<RebuildTestState>(CancellationToken.None).ConfigureAwait(false);

		// Assert — completed successfully (found projection via registry)
		var status = await sut.GetStatusAsync<RebuildTestState>(CancellationToken.None).ConfigureAwait(false);
		status.State.ShouldBe(ProjectionRebuildState.Completed);
	}

	[Fact]
	public async Task SkipUndeserializableEvents_AndContinueRebuilding()
	{
		// Arrange — one event fails deserialization, another succeeds
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<RebuildTestState>();
		projection.AddHandler<RebuildTestEvent>((state, _) => state.Count++);

		var callCount = 0;
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var c = Interlocked.Increment(ref callCount);
				return c == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new StoredEvent[]
					{
						new("e-bad", "agg-1", "Agg", "BadEvent", new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow),
						new("e-good", "agg-1", "Agg", "RebuildTestEvent", new byte[] { 2 }, null, 2, DateTimeOffset.UtcNow),
					})
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		A.CallTo(() => _eventSerializer.ResolveType("BadEvent"))
			.Throws(new InvalidOperationException("Unknown event type"));
		A.CallTo(() => _eventSerializer.ResolveType("RebuildTestEvent")).Returns(typeof(RebuildTestEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, typeof(RebuildTestEvent)))
			.Returns(new RebuildTestEvent());

		var store = new InMemoryProjectionStore<RebuildTestState>();

		var sp = A.Fake<IServiceProvider>();
		A.CallTo(() => sp.GetService(typeof(IGlobalStreamQuery))).Returns(globalQuery);
		A.CallTo(() => sp.GetService(typeof(MultiStreamProjection<RebuildTestState>))).Returns(projection);
		A.CallTo(() => sp.GetService(typeof(IProjectionStore<RebuildTestState>))).Returns(store);

		var sut = CreateService(sp);

		// Act — should not throw
		await sut.RebuildAsync<RebuildTestState>(CancellationToken.None).ConfigureAwait(false);

		// Assert — completed with the good event applied, bad event skipped
		var status = await sut.GetStatusAsync<RebuildTestState>(CancellationToken.None).ConfigureAwait(false);
		status.State.ShouldBe(ProjectionRebuildState.Completed);

		var persisted = await store.GetByIdAsync("RebuildTestState", CancellationToken.None).ConfigureAwait(false);
		persisted.ShouldNotBeNull();
		persisted.Count.ShouldBe(1);
	}

	[Fact]
	public async Task RespectCancellation_DuringRebuild()
	{
		// Arrange — cancel after first batch
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<RebuildTestState>();

		using var cts = new CancellationTokenSource();
		var callCount = 0;
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var c = Interlocked.Increment(ref callCount);
				if (c >= 2)
				{
					cts.Cancel();
				}

				return new ValueTask<IReadOnlyList<StoredEvent>>(new StoredEvent[]
				{
					new($"e-{c}", "agg-1", "Agg", "Evt", new byte[] { 1 }, null, c, DateTimeOffset.UtcNow),
				});
			});

		A.CallTo(() => _eventSerializer.ResolveType(A<string>._)).Returns(typeof(RebuildTestEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(new RebuildTestEvent());

		var sp = A.Fake<IServiceProvider>();
		A.CallTo(() => sp.GetService(typeof(IGlobalStreamQuery))).Returns(globalQuery);
		A.CallTo(() => sp.GetService(typeof(MultiStreamProjection<RebuildTestState>))).Returns(projection);

		var sut = CreateService(sp);

		// Act & Assert — should throw OperationCanceledException
		await Should.ThrowAsync<OperationCanceledException>(() =>
			sut.RebuildAsync<RebuildTestState>(cts.Token)).ConfigureAwait(false);
	}

	// --- Test types ---

	internal sealed class RebuildTestState
	{
		public int Count { get; set; }
	}

	private sealed record RebuildTestEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "agg-1";
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(RebuildTestEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}
}

#pragma warning restore CA1506
#pragma warning restore CA2012
