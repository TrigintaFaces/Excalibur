// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy .Returns stores ValueTask)

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

// --- bd-kv82ls (S848, Lane P3, MS-3): GlobalStreamProjectionHost checkpoint + cursor map MUST be
// atomic & consistent. Independent author≠impl regression lock (TestsDeveloper). The store seam is
// TWO separate injectable interfaces (ISubscriptionCheckpointStore + ICursorMapStore) with NO single
// transaction, so the fix MUST use ordering + source-of-truth: the checkpoint must never be persisted
// ahead of (without) its cursor map. Pre-fix ExecuteAsync (L208-219) stores the checkpoint FIRST then
// the cursor map AFTER, and only clears _pendingCursorUpdates on the success path — so a SaveCursorMap
// failure leaves the checkpoint advanced past the cursor map (divergence, EC-P3.1/AC-P3.2) and grows
// _pendingCursorUpdates unbounded across repeated errors (AC-P3.3). RED on the pre-fix ordering;
// GREEN on the cursor-first/atomic-rollback fix. Coupled set: pairs with the Backend kv82ls fix in
// src/Excalibur/Excalibur.EventSourcing/Projections/GlobalStreamProjectionHost.cs.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class GlobalStreamProjectionHostAtomicCheckpointShould
{
	private readonly IGlobalStreamQuery _globalStreamQuery = A.Fake<IGlobalStreamQuery>();
	private readonly IGlobalStreamProjection<GlobalStreamTestState> _projection = A.Fake<IGlobalStreamProjection<GlobalStreamTestState>>();
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();
	private readonly ISubscriptionCheckpointStore _checkpointStore = A.Fake<ISubscriptionCheckpointStore>();
	private readonly ICursorMapStore _cursorMapStore = A.Fake<ICursorMapStore>();
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();

	// --- AC-P3.2 / FR-P3.2 / EC-P3.1 (headline): SaveCursorMapAsync throwing AFTER the checkpoint
	// would be stored MUST NOT leave the checkpoint advanced ahead of the cursor map. The post-fix
	// contract is "cursor map is the source of truth / saved first" — so the checkpoint is persisted
	// ONLY once the cursor map has been durably saved. Equivalently: there must NEVER be a successful
	// StoreCheckpointAsync without a preceding successful SaveCursorMapAsync for the same flush.
	[Fact]
#pragma warning disable CA1506 // Avoid excessive class coupling - regression lock requires multiple fakes
	public async Task NotAdvanceCheckpointAheadOfCursorMapWhenCursorSaveFails()
	{
#pragma warning restore CA1506
		// Arrange — one good multi-stream event; CheckpointInterval=1 forces a flush after it. The
		// cursor-map store throws on SaveCursorMapAsync (EC-P3.1: the failure window). A cursor-map
		// store is supplied so the host runs the multi-stream cursor path.
		var storedEvent = new StoredEvent(
			"evt-1", "agg-1", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, 5, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 10,
		};
		var domainEvent = A.Fake<IDomainEvent>();
		var cursorSaveAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => _checkpointStore.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(null));

		var callCount = 0;
		A.CallTo(() => _globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				callCount++;
				return callCount == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new[] { storedEvent })
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		A.CallTo(() => _eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(domainEvent);
		A.CallTo(() => _projection.ApplyAsync(domainEvent, A<GlobalStreamTestState>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// The cursor-map save FAILS (the durable cursor write never lands).
		A.CallTo(() => _cursorMapStore.SaveCursorMapAsync(A<string>._, A<IReadOnlyDictionary<string, long>>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				cursorSaveAttempted.TrySetResult();
				return Task.FromException(new InvalidOperationException("cursor-map store unavailable"));
			});

		var host = CreateHost(checkpointInterval: 1, withCursorMap: true);

		using var cts = new CancellationTokenSource();

		// Act — run until the cursor save is attempted, give the (buggy) checkpoint write a window, stop.
		await host.StartAsync(cts.Token);
		await AwaitSignalAsync(cursorSaveAttempted.Task);
		await Task.Delay(global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromMilliseconds(250)))
			.ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		// Assert — the cursor-map save was attempted and FAILED (never durably saved). Therefore the
		// checkpoint MUST NOT have been persisted: persisting it would advance the checkpoint past the
		// (failed) cursor map, the exact divergence FR-P3.2 forbids. Pre-fix code stores the checkpoint
		// FIRST then throws on cursor save → StoreCheckpointAsync happened → this assertion is RED.
		A.CallTo(() => _cursorMapStore.SaveCursorMapAsync(A<string>._, A<IReadOnlyDictionary<string, long>>._, A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => _checkpointStore.StoreCheckpointAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	// --- AC-P3.3 / FR-P3.4: under REPEATED batch errors the per-stream _pendingCursorUpdates map MUST
	// NOT grow unbounded — it must be cleared/rebuilt each failed iteration. Pre-fix only clears on the
	// success path (after SaveCursorMapAsync succeeds); when the cursor save always throws, every flush
	// re-adds the (distinct) stream keys without ever clearing → the map grows across iterations.
	[Fact]
#pragma warning disable CA1506 // Avoid excessive class coupling - regression lock requires multiple fakes
	public async Task NotGrowPendingCursorUpdatesUnboundedUnderRepeatedCursorSaveErrors()
	{
#pragma warning restore CA1506
		// Arrange — each poll returns ONE distinct, successfully-applied event (a new stream key each
		// iteration) and CheckpointInterval=1 forces a flush every iteration whose cursor save throws.
		// So on the pre-fix code _pendingCursorUpdates accumulates a new key per failed iteration.
		var domainEvent = A.Fake<IDomainEvent>();
		var saveAttempts = 0;

		A.CallTo(() => _checkpointStore.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(null));

		var iteration = 0;
		A.CallTo(() => _globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				var i = Interlocked.Increment(ref iteration);
				// A fresh stream key per poll so a non-clearing map keeps growing.
				var evt = new StoredEvent(
					$"evt-{i}", $"agg-{i}", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, i, DateTimeOffset.UtcNow)
				{
					GlobalPosition = i,
				};
				return new ValueTask<IReadOnlyList<StoredEvent>>(new[] { evt });
			});

		A.CallTo(() => _eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(domainEvent);
		A.CallTo(() => _projection.ApplyAsync(domainEvent, A<GlobalStreamTestState>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Cursor save ALWAYS throws — the success-path Clear() can never run on the pre-fix code.
		A.CallTo(() => _cursorMapStore.SaveCursorMapAsync(A<string>._, A<IReadOnlyDictionary<string, long>>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				Interlocked.Increment(ref saveAttempts);
				return Task.FromException(new InvalidOperationException("cursor-map store unavailable"));
			});

		var host = CreateHost(checkpointInterval: 1, withCursorMap: true);

		using var cts = new CancellationTokenSource();

		// Act — let many failed iterations run (poll for a healthy number of save attempts), then stop.
		await host.StartAsync(cts.Token);
		const int targetIterations = 20;
		await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref saveAttempts) >= targetIterations,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)),
			pollInterval: TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		// Assert — _pendingCursorUpdates must stay bounded (cleared/rebuilt per iteration), NOT grow with
		// the number of failed iterations. A correct fix keeps it at ~one pending entry per in-flight
		// batch; the pre-fix non-clearing path grows it ~1 per iteration (≫ the bound). We allow generous
		// headroom (one batch worth) but assert it is far below the number of failed save attempts.
		var attempts = Volatile.Read(ref saveAttempts);
		var pendingCount = GetPendingCursorUpdatesCount(host);

		attempts.ShouldBeGreaterThanOrEqualTo(targetIterations,
			"the host must have looped through many failed cursor-save iterations");
		pendingCount.ShouldBeLessThanOrEqualTo(2,
			$"_pendingCursorUpdates must be bounded (cleared/rebuilt each failed iteration), but held {pendingCount} entries after {attempts} failed save attempts — unbounded growth (AC-P3.3 regression).");
	}

	// --- AC-P3.4 / FR-P3.3: an event that fails the INNER catch (deserialize/apply failure) MUST NOT
	// contribute a cursor entry. Mix one good event then one poison event in a single batch; only the
	// good stream key may ever reach the cursor map — the poison key must never appear.
	[Fact]
#pragma warning disable CA1506 // Avoid excessive class coupling - regression lock requires multiple fakes
	public async Task NotRecordCursorEntryForAnInnerCatchFailedEvent()
	{
#pragma warning restore CA1506
		// Arrange — a 2-event batch: evt-good applies successfully, evt-poison fails ApplyAsync. With a
		// cursor-map store supplied, the saved map must contain ONLY the good stream key. CheckpointInterval
		// stays at 1 so the (good) flush attempts a cursor save we can inspect; the cursor save SUCCEEDS so
		// we capture the exact map persisted.
		var good = new StoredEvent(
			"evt-good", "agg-good", "TestAggregate", "GoodEvent", "g"u8.ToArray(), null, 1, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 1,
		};
		var poison = new StoredEvent(
			"evt-poison", "agg-poison", "TestAggregate", "PoisonEvent", "p"u8.ToArray(), null, 2, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 2,
		};
		var goodEvent = A.Fake<IDomainEvent>();
		var poisonEvent = A.Fake<IDomainEvent>();
		var goodStreamKey = $"{good.AggregateType}:{good.AggregateId}";
		var poisonStreamKey = $"{poison.AggregateType}:{poison.AggregateId}";

		IReadOnlyDictionary<string, long>? savedMap = null;
		var cursorSaved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => _checkpointStore.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(null));

		var callCount = 0;
		A.CallTo(() => _globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				callCount++;
				return callCount == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new[] { good, poison })
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		A.CallTo(() => _eventSerializer.ResolveType("GoodEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => _eventSerializer.ResolveType("PoisonEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(good.EventData, A<Type>._)).Returns(goodEvent);
		A.CallTo(() => _eventSerializer.DeserializeEvent(poison.EventData, A<Type>._)).Returns(poisonEvent);

		A.CallTo(() => _projection.ApplyAsync(goodEvent, A<GlobalStreamTestState>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);
		A.CallTo(() => _projection.ApplyAsync(poisonEvent, A<GlobalStreamTestState>._, A<CancellationToken>._))
			.Returns(Task.FromException(new InvalidOperationException("poison: apply failed")));

		A.CallTo(() => _cursorMapStore.SaveCursorMapAsync(A<string>._, A<IReadOnlyDictionary<string, long>>._, A<CancellationToken>._))
			.Invokes((string _, IReadOnlyDictionary<string, long> map, CancellationToken _) =>
			{
				savedMap = new Dictionary<string, long>(map, StringComparer.Ordinal);
				cursorSaved.TrySetResult();
			})
			.Returns(Task.CompletedTask);

		var host = CreateHost(checkpointInterval: 1, withCursorMap: true);

		using var cts = new CancellationTokenSource();

		// Act
		await host.StartAsync(cts.Token);
		await AwaitSignalAsync(cursorSaved.Task);
		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		// Assert — the persisted cursor map must include the GOOD stream key but MUST NOT include the
		// poison stream key (no cursor entry for an inner-catch-failed event, FR-P3.3).
		savedMap.ShouldNotBeNull();
		savedMap!.ShouldContainKey(goodStreamKey);
		savedMap.ShouldNotContainKey(poisonStreamKey);
	}

	private GlobalStreamProjectionHost<GlobalStreamTestState> CreateHost(int checkpointInterval, bool withCursorMap)
	{
		return new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_globalStreamQuery,
			_projection,
			_eventSerializer,
			_checkpointStore,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions
			{
				IdlePollingInterval = TimeSpan.FromMilliseconds(10),
				CheckpointInterval = checkpointInterval,
			}),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance,
			_serviceProvider,
			withCursorMap ? _cursorMapStore : null);
	}

	// Reflection over the private _pendingCursorUpdates map (same-assembly access via reflection is
	// unrestricted) to assert the AC-P3.3 boundedness invariant without widening production visibility.
	private static int GetPendingCursorUpdatesCount(GlobalStreamProjectionHost<GlobalStreamTestState> host)
	{
		var field = typeof(GlobalStreamProjectionHost<GlobalStreamTestState>)
			.GetField("_pendingCursorUpdates", BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull("expected private field _pendingCursorUpdates on the projection host");
		var map = (System.Collections.IDictionary)field!.GetValue(host)!;
		return map.Count;
	}

	private static Task AwaitSignalAsync(Task signal)
	{
		return global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			signal,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)),
			cancellationToken: CancellationToken.None);
	}
}

#pragma warning restore CA2012
