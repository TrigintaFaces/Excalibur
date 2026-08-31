// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Excessive class coupling -- integration-style tests for BackgroundService require many DI types

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.Hosting;
using Tests.Shared.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Tests for <see cref="AsyncProjectionProcessingHost"/>:
/// constructor null guards, ExecuteAsync behavior (no IGlobalStreamQuery,
/// no async registrations, event processing, checkpointing, error resilience,
/// graceful shutdown).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class AsyncProjectionProcessingHostShould : IDisposable
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly InMemorySubscriptionCheckpointStore _checkpointStore = new();
	private readonly ServiceCollection _services = new();

	public void Dispose()
	{
		// no-op
	}

	private AsyncProjectionProcessingHost CreateHost(
		IServiceProvider? sp = null,
		IEventSerializer? serializer = null,
		GlobalStreamProjectionOptions? options = null)
	{
		serializer ??= A.Fake<IEventSerializer>();
		options ??= new GlobalStreamProjectionOptions();

		if (sp == null)
		{
			sp = _services.BuildServiceProvider();
		}

		return new AsyncProjectionProcessingHost(
			_registry,
			serializer,
			_checkpointStore,
			Options.Create(options),
			sp,
			NullLogger<AsyncProjectionProcessingHost>.Instance);
	}

	[Fact]
	public void ThrowOnNullRegistry()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AsyncProjectionProcessingHost(
				null!,
				A.Fake<IEventSerializer>(),
				_checkpointStore,
				Options.Create(new GlobalStreamProjectionOptions()),
				A.Fake<IServiceProvider>(),
				NullLogger<AsyncProjectionProcessingHost>.Instance));
	}

	[Fact]
	public void ThrowOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AsyncProjectionProcessingHost(
				_registry,
				null!,
				_checkpointStore,
				Options.Create(new GlobalStreamProjectionOptions()),
				A.Fake<IServiceProvider>(),
				NullLogger<AsyncProjectionProcessingHost>.Instance));
	}

	[Fact]
	public void ThrowOnNullCheckpointStore()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AsyncProjectionProcessingHost(
				_registry,
				A.Fake<IEventSerializer>(),
				null!,
				Options.Create(new GlobalStreamProjectionOptions()),
				A.Fake<IServiceProvider>(),
				NullLogger<AsyncProjectionProcessingHost>.Instance));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AsyncProjectionProcessingHost(
				_registry,
				A.Fake<IEventSerializer>(),
				_checkpointStore,
				null!,
				A.Fake<IServiceProvider>(),
				NullLogger<AsyncProjectionProcessingHost>.Instance));
	}

	[Fact]
	public void ThrowOnNullServiceProvider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AsyncProjectionProcessingHost(
				_registry,
				A.Fake<IEventSerializer>(),
				_checkpointStore,
				Options.Create(new GlobalStreamProjectionOptions()),
				null!,
				NullLogger<AsyncProjectionProcessingHost>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AsyncProjectionProcessingHost(
				_registry,
				A.Fake<IEventSerializer>(),
				_checkpointStore,
				Options.Create(new GlobalStreamProjectionOptions()),
				A.Fake<IServiceProvider>(),
				null!));
	}

	[Fact]
	public async Task ExitImmediately_WhenNoGlobalStreamQueryRegistered()
	{
		// Arrange — no IGlobalStreamQuery in DI
		_registry.Register(CreateAsyncRegistration());
		using var cts = new CancellationTokenSource();
		var host = CreateHost();

		// Act — start and let it run; it should log warning and exit
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll for the condition instead of sleeping through it: the host is expected to EXIT, and
		// ExecuteTask completing is that event. A fixed delay is both slower than it needs to be and
		// still too short under a loaded runner, which is the shape that flakes.
		await WaitHelpers.WaitUntilAsync(
			() => ((BackgroundService)host).ExecuteTask is { IsCompleted: true },
			TestTimeouts.Scale(TimeSpan.FromSeconds(4)),
			TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — should not throw, just exits gracefully
	}

	[Fact]
	public async Task ExitImmediately_WhenNoAsyncRegistrations()
	{
		// Arrange — IGlobalStreamQuery registered but no async projections
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();

		using var cts = new CancellationTokenSource();
		var host = CreateHost(sp);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);
		// Same condition as above: the host has no async registrations, so it exits, and ExecuteTask
		// completing is the observable event rather than an assumed duration.
		await WaitHelpers.WaitUntilAsync(
			() => ((BackgroundService)host).ExecuteTask is { IsCompleted: true },
			TestTimeouts.Scale(TimeSpan.FromSeconds(4)),
			TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — ReadAllAsync never called since there are no async registrations
		A.CallTo(() => fakeQuery.ReadAllAsync(
			A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PollGlobalStream_WhenAsyncRegistrationsExist()
	{
		// Arrange
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		var readAllCalled = 0;
		A.CallTo(() => fakeQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				Interlocked.Increment(ref readAllCalled);
				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();

		_registry.Register(CreateAsyncRegistration());

		var options = new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		};

		using var cts = new CancellationTokenSource(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		var host = CreateHost(sp, options: options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until ReadAllAsync is called — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref readAllCalled) > 0,
			TestTimeouts.Scale(TimeSpan.FromSeconds(4)),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — ReadAllAsync was called at least once (polling loop ran)
		A.CallTo(() => fakeQuery.ReadAllAsync(
			A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ProcessEvents_AndAdvancePosition()
	{
		// Arrange
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		var callCount = 0;
		var storedEvents = new List<StoredEvent>
		{
			new("evt-1", "order-1", "Order", "OrderCreated", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow),
			new("evt-2", "order-1", "Order", "OrderShipped", Array.Empty<byte>(), null, 2, DateTimeOffset.UtcNow),
		};

		A.CallTo(() => fakeQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref callCount);
				// Return events on first call, empty on subsequent calls (so loop idles)
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					count == 1
						? storedEvents
						: (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
			});

		var fakeSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => fakeSerializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => fakeSerializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.Returns(new TestOrderPlaced());

		var applyInvoked = 0;
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (events, ctx, sp, ct) =>
			{
				Interlocked.Add(ref applyInvoked, events.Count);
				return Task.CompletedTask;
			}));

		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();

		var options = new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
			CheckpointInterval = 100, // won't reach threshold in this test
		};

		using var cts = new CancellationTokenSource(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		var host = CreateHost(sp, fakeSerializer, options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until apply is invoked — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref applyInvoked) > 0,
			TestTimeouts.Scale(TimeSpan.FromSeconds(4)),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — events were dispatched to projection apply delegate
		applyInvoked.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task GroupEventsByAggregate_BeforeDispatching()
	{
		// Arrange — events from 2 different aggregates in one batch
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		var callCount = 0;
		var storedEvents = new List<StoredEvent>
		{
			new("e1", "order-1", "Order", "OrderCreated", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow),
			new("e2", "order-2", "Order", "OrderCreated", Array.Empty<byte>(), null, 2, DateTimeOffset.UtcNow),
			new("e3", "order-1", "Order", "OrderShipped", Array.Empty<byte>(), null, 3, DateTimeOffset.UtcNow),
		};

		A.CallTo(() => fakeQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref callCount);
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					count == 1
						? storedEvents
						: (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
			});

		var fakeSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => fakeSerializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => fakeSerializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.Returns(new TestOrderPlaced());

		var applyCallContexts = new List<string>();
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (events, ctx, sp, ct) =>
			{
				// Record which aggregate this apply was for
				applyCallContexts.Add($"{ctx.AggregateId}:{events.Count}");
				return Task.CompletedTask;
			}));

		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();

		var options = new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		};

		using var cts = new CancellationTokenSource(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		var host = CreateHost(sp, fakeSerializer, options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until both aggregate groups have been applied — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => applyCallContexts.Count >= 2,
			TestTimeouts.Scale(TimeSpan.FromSeconds(4)),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — apply was called per-aggregate group (2 groups: order-1 with 2 events, order-2 with 1)
		applyCallContexts.Count.ShouldBe(2);
		applyCallContexts.ShouldContain("order-1:2");
		applyCallContexts.ShouldContain("order-2:1");
	}

	[Fact]
	public async Task RestoreCheckpoint_OnStartup()
	{
		// Arrange — store a checkpoint so the host resumes from it
		await _checkpointStore.StoreCheckpointAsync("AsyncProjectionProcessingHost", 42, CancellationToken.None)
			.ConfigureAwait(false);

		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		GlobalStreamPosition? capturedPosition = null;

		A.CallTo(() => fakeQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((GlobalStreamPosition pos, int _, CancellationToken _) =>
			{
				capturedPosition ??= pos;
				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();
		_registry.Register(CreateAsyncRegistration());

		var options = new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		};

		using var cts = new CancellationTokenSource(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		var host = CreateHost(sp, options: options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until capturedPosition is set — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => capturedPosition != null,
			TestTimeouts.Scale(TimeSpan.FromSeconds(4)),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — polling started from the checkpointed position (42)
		capturedPosition.ShouldNotBeNull();
		capturedPosition.Position.ShouldBe(42);
	}

	[Fact]
	public async Task HaltOnUndeserializablePoisonEvent_WithoutApplyingPastIt()
	{
		// Arrange — bd-red2ha (S841): an undeserializable (poison) event now HALTS the host instead of
		// skip-and-advance. The poison event is FIRST, so the good event after it must NOT be applied.
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		var callCount = 0;
		var storedEvents = new List<StoredEvent>
		{
			new("e1", "order-1", "Order", "BadEvent", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow),
			new("e2", "order-1", "Order", "GoodEvent", Array.Empty<byte>(), null, 2, DateTimeOffset.UtcNow),
		};

		A.CallTo(() => fakeQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref callCount);
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					count == 1
						? storedEvents
						: (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
			});

		var poisonObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var fakeSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => fakeSerializer.ResolveType("BadEvent"))
			.Invokes(() => poisonObserved.TrySetResult())
			.Throws(new InvalidOperationException("Unknown event type"));
		A.CallTo(() => fakeSerializer.ResolveType("GoodEvent"))
			.Returns(typeof(TestOrderPlaced));
		A.CallTo(() => fakeSerializer.DeserializeEvent(A<byte[]>._, typeof(TestOrderPlaced)))
			.Returns(new TestOrderPlaced());

		var appliedCount = 0;
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (events, ctx, sp, ct) =>
			{
				Interlocked.Add(ref appliedCount, events.Count);
				return Task.CompletedTask;
			}));

		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();

		using var cts = new CancellationTokenSource(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		var host = CreateHost(sp, fakeSerializer, new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		});

		// Act — start, wait until the host reaches the poison event, give halt-vs-advance a window, then stop.
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);
		await poisonObserved.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — the host HALTED at the poison; the good event AFTER it was NOT applied (no skip-and-advance,
		// so the poison is reprocessed on restart rather than silently skipped).
		appliedCount.ShouldBe(0);
	}

	[Fact]
	public async Task ContinuePolling_AfterProjectionApplyError()
	{
		// Arrange — apply delegate throws on first batch, succeeds on second
		var fakeQuery = A.Fake<IGlobalStreamQuery>();

		// The store is faked on POSITION, not on how many times it is read, and that is the whole
		// point of this arrangement.
		//
		// It used to ration events by call count -- the first two reads returned an event, everything
		// after returned empty -- which made the test depend on the host reading exactly twice. Any
		// third read, from a poll landing between operations on a loaded agent, spent the budget: the
		// retry then received an empty batch, the second apply never happened, and the test failed
		// having proved nothing about the host. That is what went red on CI while passing 20 times
		// locally.
		//
		// It was also modelling the wrong behaviour. On an apply fault the host deliberately does NOT
		// advance the checkpoint, so the next read returns THE SAME BATCH and the apply is retried --
		// at-least-once reprocessing, which is the property this test exists to check. Handing the
		// retry a different event instead tested "the host read again and got new data", which is not
		// the same claim and is not the one in the test name.
		//
		// Keying on the position models the real store: the batch is served until the host advances
		// past it, and then the stream is empty. The host reads as many or as few times as it likes.
		var pendingEvent = new StoredEvent(
			"e-1", "order-1", "Order", "OrderCreated", Array.Empty<byte>(), null, 1, DateTimeOffset.UnixEpoch)
		{
			GlobalPosition = 1,
		};

		A.CallTo(() => fakeQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((GlobalStreamPosition position, int _, CancellationToken _) =>
				new ValueTask<IReadOnlyList<StoredEvent>>(
					position.Position <= pendingEvent.GlobalPosition
						? new List<StoredEvent> { pendingEvent }
						: (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>()));

		var fakeSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => fakeSerializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => fakeSerializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.Returns(new TestOrderPlaced());

		var applyCallCount = 0;
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (events, ctx, sp, ct) =>
			{
				var c = Interlocked.Increment(ref applyCallCount);
				if (c == 1)
				{
					throw new InvalidOperationException("Transient projection failure");
				}

				return Task.CompletedTask;
			}));

		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();

		// No deadline on this token. It is handed to StartAsync, and a token that expires part-way
		// through the wait below can only ever add a second failure mode to a test that already has
		// its own bound. A genuine hang is caught by the harness's --blame-hang-timeout, with a dump
		// naming what is stuck rather than a stopwatch expiring.
		using var cts = new CancellationTokenSource();
		var host = CreateHost(sp, fakeSerializer, new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		});

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Wait GENEROUSLY, because the bound costs nothing when the test passes.
		//
		// WaitUntilAsync returns the moment the condition holds, so on a healthy run this returns in
		// milliseconds whether the bound is four seconds or forty. The bound is therefore a
		// failure-detection timeout, not a performance assertion, and sizing it tightly buys nothing
		// while making the test fail on a loaded agent.
		//
		// It failed on CI at the previous bound: one apply seen instead of two. The host's fault path
		// waits IdlePollingInterval -- 50ms here -- before re-reading, so the retry is milliseconds
		// away, and missing it for twelve seconds means the loop's continuation was never scheduled.
		// That is thread-pool starvation on a busy agent, not the behaviour under test.
		//
		// The RESULT IS NOW CHECKED, which it was not. A timed-out wait used to fall through to the
		// assertion below, so starvation was reported as "saw 1 apply" -- a statement about the host,
		// blamed on the host, when the test had simply stopped waiting. The two failures need
		// different fixes and must not read alike.
		var sawRetry = await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref applyCallCount) >= 2,
			TestTimeouts.Scale(TimeSpan.FromSeconds(30)),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		sawRetry.ShouldBeTrue(
			$"the retry never arrived within the wait; applyCallCount was {Volatile.Read(ref applyCallCount)}. "
			+ "The host re-reads IdlePollingInterval (50ms) after an apply fault, so on a healthy agent "
			+ "the second apply is milliseconds away. Waiting this long and seeing nothing means either "
			+ "the loop stopped after the fault -- the defect this test exists to catch -- or the agent "
			+ "never scheduled its continuation.");

		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — the host retried the SAME batch after the fault and then advanced past it.
		// Exactly two: the apply that threw, and the retry that succeeded. A lower bound would also
		// pass if the host spun on the batch forever, which is the opposite of the behaviour claimed.
		applyCallCount.ShouldBe(
			2,
			$"expected one faulting apply and one successful retry of the same batch, saw {applyCallCount}. "
			+ "One means the host never reprocessed the unadvanced batch; more than two means it never "
			+ "advanced past it and is reprocessing indefinitely.");
	}

	[Fact]
	public async Task NotAdvanceCheckpointPastAFailedApply_ReprocessingTheSameBatch()
	{
		// Arrange — h9nlsf (Dijkstra D6), the SAFETY arm paired with the liveness arm
		// (ContinuePolling_AfterProjectionApplyError above): on a projection APPLY fault the host must
		// HALT-at-failure — it must NOT advance the checkpoint past the failed batch, so the SAME batch is
		// reprocessed (at-least-once; applies are idempotent) rather than silently skipped. The read model
		// can therefore never drift from the event log. The prior test only proves the host keeps polling;
		// this one proves it does not advance past the fault.
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		var readCount = 0;

		// Position-aware: return the SAME single event at global position 1 while the host's position has
		// NOT advanced past it. If the host ever (incorrectly) advanced past position 1, it would ask from a
		// higher position and get an empty batch — so a continuously re-served e1 is direct evidence the
		// checkpoint was never advanced past the failed apply.
		A.CallTo(() => fakeQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((GlobalStreamPosition pos, int batchSize, CancellationToken ct) =>
			{
				Interlocked.Increment(ref readCount);
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					pos.Position <= 1
						? new List<StoredEvent>
						{
							new("e1", "order-1", "Order", "OrderCreated", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow),
						}
						: (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
			});

		var fakeSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => fakeSerializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => fakeSerializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.Returns(new TestOrderPlaced());

		// Permanent apply fault — every apply throws, so the batch can never succeed and must be reprocessed.
		var applyCount = 0;
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (events, ctx, sp, ct) =>
			{
				Interlocked.Increment(ref applyCount);
				throw new InvalidOperationException("Permanent projection failure");
			}));

		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();

		// Bounds are SCALED for CI, and then widened again because scaling alone was not enough. The host
		// re-applies within milliseconds locally (this test runs in ~0.3s, 3 for 3), but on a loaded runner
		// it has now twice exceeded its window: first an unscaled 4s, and then the SCALED 4s window, which
		// expired after a single apply at ~13s. Both times it reported applyCount == 1 as though the host
		// had advanced past a failed batch -- the failure looks exactly like the data-loss defect this test
		// exists to catch, which is the worst kind of flake because it accuses the very invariant it guards.
		//
		// So the window is generous rather than tight, which costs nothing when the host is healthy (it
		// returns in a third of a second) and costs a false accusation when it is not. The two bounds are
		// COUPLED and must stay ordered: the host runs under the token below, so a wait longer than the
		// token is inert -- the host would be cancelled before it could satisfy it.
		using var cts = new CancellationTokenSource(TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		var host = CreateHost(sp, fakeSerializer, new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
			CheckpointInterval = 1, // would persist a checkpoint after a SINGLE event IF the host advanced past it
		});

		// Act — run until the same failing event has been reprocessed at least twice, then stop.
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);
		await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref applyCount) >= 2,
			TestTimeouts.Scale(TimeSpan.FromSeconds(20)),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert (SAFETY) — the checkpoint was NEVER advanced past the failed batch:
		//   (1) the SAME event was re-read and re-applied (reprocess, not skip), and
		//   (2) no checkpoint position was ever persisted (CheckpointInterval=1 would have persisted one
		//       immediately had the host advanced past the fault).
		applyCount.ShouldBeGreaterThanOrEqualTo(2);
		var persisted = await _checkpointStore.EnumerateCheckpointsAsync(CancellationToken.None).ConfigureAwait(false);
		persisted.ShouldBeEmpty();
	}

	[Fact]
	public void ImplementIHostedService()
	{
		var host = CreateHost();
		host.ShouldBeAssignableTo<IHostedService>();
		host.ShouldBeAssignableTo<BackgroundService>();
	}

	// --- Helpers ---

	private static ProjectionRegistration CreateAsyncRegistration()
	{
		return new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) => Task.CompletedTask);
	}

	/// <summary>
	/// Minimal in-memory checkpoint store for testing.
	/// </summary>
	private sealed class InMemorySubscriptionCheckpointStore : ISubscriptionCheckpointStore
	{
		private readonly Dictionary<string, long> _checkpoints = new();

		public Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken)
		{
			_checkpoints.TryGetValue(subscriptionName, out var pos);
			return Task.FromResult(pos == 0 && !_checkpoints.ContainsKey(subscriptionName)
				? (long?)null
				: pos);
		}

		public Task StoreCheckpointAsync(string subscriptionName, long position, CancellationToken cancellationToken)
		{
			_checkpoints[subscriptionName] = position;
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken)
		{
			IReadOnlyList<SubscriptionCheckpoint> checkpoints =
				[.. _checkpoints.Select(kvp => new SubscriptionCheckpoint(kvp.Key, kvp.Value))];
			return Task.FromResult(checkpoints);
		}
	}
}