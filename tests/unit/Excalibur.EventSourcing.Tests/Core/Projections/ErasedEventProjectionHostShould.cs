// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy .Returns stores ValueTask)
#pragma warning disable CA1506 // Excessive class coupling -- host regression locks require many fakes

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Regression lock: the async projection host must ADVANCE its checkpoint past GDPR-erased
/// (tombstoned) events rather than halting on them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure being locked is the position, not the deserialization.</b> That an erased event fails
/// to deserialize is expected and harmless on its own; the defect is that the host treated that failure
/// as a poison event, halted the batch, and never advanced. Because a tombstone is a permanent part of
/// the stream, the next poll re-read the same event and halted again — the host stopped forever at the
/// first erased event, so a lawful erasure silently killed the projection.
/// </para>
/// <para>
/// Both locks use a batch of <em>only</em> tombstones, which is the shape that made skipping alone
/// insufficient: the pre-fix advance sat inside <c>if (deserialized.Count &gt; 0)</c>, so a batch that
/// deserialized to nothing could not advance even once the tombstone was recognized and skipped. A
/// future reader who reinstates that guard re-breaks the fix, and these locks catch it.
/// </para>
/// <para>
/// <b>Over-reach guard.</b> <see cref="NotAdvanceTheCheckpointPastAGenuinelyUnresolvableEvent"/> is green
/// on the pre-fix surface too. It is what proves the fix was not bought by skipping everything that fails
/// to deserialize: real corruption must still halt for retry, never be silently skipped.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasedEventAsyncProjectionHostShould
{
	private const long TombstoneGlobalPosition = 10;

	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly ISubscriptionCheckpointStore _checkpointStore = A.Fake<ISubscriptionCheckpointStore>();
	private readonly IGlobalStreamQuery _globalStreamQuery = A.Fake<IGlobalStreamQuery>();
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();

	private static StoredEvent Tombstoned(string eventId, long globalPosition) =>
		new(
			EventId: eventId,
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: ErasedEventMarker.EventType,
			EventData: [],
			Metadata: null,
			Version: 5,
			Timestamp: DateTimeOffset.UtcNow)
		{
			GlobalPosition = globalPosition,
		};

	private static StoredEvent Live(string eventId, string eventType, long globalPosition) =>
		new(
			EventId: eventId,
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: eventType,
			EventData: "data"u8.ToArray(),
			Metadata: null,
			Version: 5,
			Timestamp: DateTimeOffset.UtcNow)
		{
			GlobalPosition = globalPosition,
		};

	private void ReturnOneBatchThenIdle(IReadOnlyList<StoredEvent> batch)
	{
		var callCount = 0;
		A.CallTo(() => _globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var n = Interlocked.Increment(ref callCount);
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					n == 1 ? batch : (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
			});
	}

	private void RegisterAsyncProjection() =>
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (events, ctx, sp, ct) => Task.CompletedTask));

	private AsyncProjectionProcessingHost CreateHost()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_globalStreamQuery);

		return new AsyncProjectionProcessingHost(
			_registry,
			_eventSerializer,
			_checkpointStore,
			Options.Create(new GlobalStreamProjectionOptions
			{
				IdlePollingInterval = TimeSpan.FromMilliseconds(10),

				// One processed event is enough to force a checkpoint flush, so the advance is observable
				// through the checkpoint store rather than through private host state.
				CheckpointInterval = 1,
			}),
			services.BuildServiceProvider(),
			NullLogger<AsyncProjectionProcessingHost>.Instance);
	}

	[Fact]
	public async Task AdvanceTheCheckpointPastABatchOfOnlyErasedEvents()
	{
		// Arrange - a batch consisting ENTIRELY of tombstones. Nothing in it can be delivered, yet the
		// host has genuinely made progress and must record it.
		RegisterAsyncProjection();
		ReturnOneBatchThenIdle(
		[
			Tombstoned("erased-1", TombstoneGlobalPosition),
			Tombstoned("erased-2", TombstoneGlobalPosition + 1),
		]);

		// The tombstone must be recognized before any deserialization attempt: reaching the serializer at
		// all is the pre-fix behaviour.
		_ = A.CallTo(() => _eventSerializer.ResolveType(ErasedEventMarker.EventType))
			.Throws(new InvalidOperationException("the serializer must never be asked to resolve a tombstone"));

		var checkpointed = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _checkpointStore.StoreCheckpointAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.Invokes((string _, long position, CancellationToken _) => checkpointed.TrySetResult(position))
			.Returns(Task.CompletedTask);

		var host = CreateHost();
		using var cts = new CancellationTokenSource();

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);
		var position = await WaitHelpers.AwaitSignalAsync(
			checkpointed.Task,
			TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - the checkpoint landed PAST the last tombstone. Pre-fix the host halted on the first
		// tombstone and no checkpoint was ever written, so awaiting one times out: RED.
		position.ShouldBe(
			TombstoneGlobalPosition + 2,
			"the checkpoint must advance past an all-tombstone batch, or the same batch is re-read forever");

		A.CallTo(() => _eventSerializer.ResolveType(ErasedEventMarker.EventType)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotAdvanceTheCheckpointPastAGenuinelyUnresolvableEvent()
	{
		// Arrange - no erasure: the event's type simply cannot be resolved (unregistered or corrupt).
		// That is a poison event and must still halt, so it is retried rather than skipped.
		RegisterAsyncProjection();
		ReturnOneBatchThenIdle([Live("poison", "UnregisteredEvent", TombstoneGlobalPosition)]);

		var resolveAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _eventSerializer.ResolveType("UnregisteredEvent"))
			.Invokes(() => resolveAttempted.TrySetResult())
			.Throws(new InvalidOperationException("unregistered event type"));

		var host = CreateHost();
		using var cts = new CancellationTokenSource();

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);
		await WaitHelpers.AwaitSignalAsync(
			resolveAttempted.Task,
			TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
		await Task.Delay(TestTimeouts.Scale(TimeSpan.FromMilliseconds(250))).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - nothing checkpointed: the poison event stays unread for the next poll.
		A.CallTo(() => _checkpointStore.StoreCheckpointAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}

/// <summary>
/// Regression lock: the global-stream projection host must advance BOTH its checkpoint and its
/// per-stream cursor map past a GDPR-erased (tombstoned) event.
/// </summary>
/// <remarks>
/// <para>
/// The checkpoint half is the same permanent-halt defect locked for the async host above. The cursor-map
/// half is a distinct, quieter failure introduced by fixing only the checkpoint: this host's own ordering
/// contract is <em>cursor map &gt;= checkpoint, never checkpoint &gt; cursor map</em>, because a
/// multi-stream resume from a checkpoint that ran ahead of the cursor map can skip events. Advancing the
/// checkpoint past a tombstone while leaving that stream's cursor behind produces exactly the forbidden
/// direction, so it is locked separately.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasedEventGlobalStreamProjectionHostShould
{
	private const long TombstoneGlobalPosition = 10;
	private const long TombstoneVersion = 5;
	private const string StreamKey = "TestAggregate:agg-1";

	private readonly IGlobalStreamQuery _globalStreamQuery = A.Fake<IGlobalStreamQuery>();
	private readonly IGlobalStreamProjection<GlobalStreamTestState> _projection =
		A.Fake<IGlobalStreamProjection<GlobalStreamTestState>>();
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();
	private readonly ISubscriptionCheckpointStore _checkpointStore = A.Fake<ISubscriptionCheckpointStore>();
	private readonly ICursorMapStore _cursorMapStore = A.Fake<ICursorMapStore>();
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
	private readonly IServiceScopeFactory _scopeFactory = A.Fake<IServiceScopeFactory>();

	private static StoredEvent Tombstoned(string eventId, long globalPosition) =>
		new(
			EventId: eventId,
			AggregateId: "agg-1",
			AggregateType: "TestAggregate",
			EventType: ErasedEventMarker.EventType,
			EventData: [],
			Metadata: null,
			Version: TombstoneVersion,
			Timestamp: DateTimeOffset.UtcNow)
		{
			GlobalPosition = globalPosition,
		};

	private void ReturnOneBatchThenIdle(IReadOnlyList<StoredEvent> batch)
	{
		var callCount = 0;
		A.CallTo(() => _globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var n = Interlocked.Increment(ref callCount);
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					n == 1 ? batch : (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
			});
	}

	private GlobalStreamProjectionHost<GlobalStreamTestState> CreateHost()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_globalStreamQuery);
		_ = services.AddSingleton(_projection);
		_ = services.AddSingleton(_checkpointStore);
		_ = services.AddSingleton(_cursorMapStore);

		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(services.BuildServiceProvider());
		A.CallTo(() => _scopeFactory.CreateScope()).Returns(scope);

		_ = A.CallTo(() => _checkpointStore.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(null));

		return new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_scopeFactory,
			_eventSerializer,
			Options.Create(new GlobalStreamProjectionOptions
			{
				IdlePollingInterval = TimeSpan.FromMilliseconds(10),
				CheckpointInterval = 1,
			}),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance,
			_serviceProvider);
	}

	[Fact]
	public async Task AdvanceTheCheckpointAndCursorMapPastABatchOfOnlyErasedEvents()
	{
		// Arrange - a batch of only tombstones: nothing to apply, but real progress to record.
		ReturnOneBatchThenIdle([Tombstoned("erased-1", TombstoneGlobalPosition)]);

		_ = A.CallTo(() => _eventSerializer.ResolveType(ErasedEventMarker.EventType))
			.Throws(new InvalidOperationException("the serializer must never be asked to resolve a tombstone"));

		IReadOnlyDictionary<string, long>? savedCursorMap = null;
		_ = A.CallTo(() => _cursorMapStore.SaveCursorMapAsync(
				A<string>._, A<IReadOnlyDictionary<string, long>>._, A<CancellationToken>._))
			.Invokes((string _, IReadOnlyDictionary<string, long> map, CancellationToken _) =>
				savedCursorMap = new Dictionary<string, long>(map, StringComparer.Ordinal))
			.Returns(Task.CompletedTask);

		var checkpointed = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _checkpointStore.StoreCheckpointAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.Invokes((string _, long position, CancellationToken _) => checkpointed.TrySetResult(position))
			.Returns(Task.CompletedTask);

		var host = CreateHost();
		using var cts = new CancellationTokenSource();

		// Act
		await host.StartAsync(cts.Token).ConfigureAwait(false);
		var position = await WaitHelpers.AwaitSignalAsync(
			checkpointed.Task,
			TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - checkpoint advanced past the tombstone. Pre-fix the host treated it as poison, broke
		// out of the batch with lastGoodPosition still null, and never checkpointed: RED.
		position.ShouldBe(
			TombstoneGlobalPosition + 1,
			"the checkpoint must advance past an all-tombstone batch");

		// ... and the per-stream cursor advanced WITH it. Advancing the checkpoint alone leaves the
		// cursor map behind the checkpoint, which is the direction this host forbids because a
		// multi-stream resume can then skip events.
		savedCursorMap.ShouldNotBeNull(
			"the cursor map must be saved alongside the checkpoint, never left behind it");
		savedCursorMap!.ShouldContainKeyAndValue(StreamKey, TombstoneVersion);

		A.CallTo(() => _eventSerializer.ResolveType(ErasedEventMarker.EventType)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotAdvanceTheCheckpointPastAGenuinelyUnresolvableEvent()
	{
		// Arrange - no erasure: an unresolvable, non-marker event is poison and must still halt.
		var poison = new StoredEvent(
			"poison", "agg-1", "TestAggregate", "UnregisteredEvent", "data"u8.ToArray(), null,
			TombstoneVersion, DateTimeOffset.UtcNow)
		{
			GlobalPosition = TombstoneGlobalPosition,
		};
		ReturnOneBatchThenIdle([poison]);

		var resolveAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _eventSerializer.ResolveType("UnregisteredEvent"))
			.Invokes(() => resolveAttempted.TrySetResult())
			.Throws(new InvalidOperationException("unregistered event type"));

		var host = CreateHost();
		using var cts = new CancellationTokenSource();

		// Act
		await host.StartAsync(cts.Token).ConfigureAwait(false);
		await WaitHelpers.AwaitSignalAsync(
			resolveAttempted.Task,
			TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
		await Task.Delay(TestTimeouts.Scale(TimeSpan.FromMilliseconds(250))).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - neither surface advanced: the poison event stays unread for the next poll.
		A.CallTo(() => _checkpointStore.StoreCheckpointAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _cursorMapStore.SaveCursorMapAsync(
				A<string>._, A<IReadOnlyDictionary<string, long>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}
