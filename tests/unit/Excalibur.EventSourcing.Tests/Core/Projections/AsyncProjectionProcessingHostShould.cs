// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
		options ??= new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test" };

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
				Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test" }),
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
				Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test" }),
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
				Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test" }),
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
				Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test" }),
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
				Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test" }),
				A.Fake<IServiceProvider>(),
				null!));
	}

	[Fact]
	public async Task RefuseMissingGlobalStreamQueryRatherThanReportCaughtUp()
	{
		_registry.Register(CreateAsyncRegistration());
		using var host = CreateHost();
		await Should.ThrowAsync<InvalidOperationException>(
			() => host.ProcessPendingAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ReturnCaughtUpWithoutScanningWhenNoAsyncRegistrationsExist()
	{
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		_services.AddSingleton(fakeQuery);
		using var provider = _services.BuildServiceProvider();
		using var host = CreateHost(provider);
		var result = await host.ProcessPendingAsync(CancellationToken.None);
		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		result.EventsApplied.ShouldBe(0);
		A.CallTo(() => fakeQuery.ReadAllAsync(
			A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
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
		{ SubscriptionScope = "unit-test",
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
			new("evt-1", "order-1", "Order", "OrderCreated", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow) { GlobalPosition = 1 },
			new("evt-2", "order-1", "Order", "OrderShipped", Array.Empty<byte>(), null, 2, DateTimeOffset.UtcNow) { GlobalPosition = 2 },
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
		{ SubscriptionScope = "unit-test",
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
			CheckpointInterval = 100, // custom-host option; built-in processing checkpoints each page
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
	public async Task Dispatch_the_whole_batch_once_in_stream_order_carrying_each_events_own_aggregate()
	{
		// Arrange — events from 2 different aggregates in one batch
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		var callCount = 0;
		var storedEvents = new List<StoredEvent>
		{
			new("e1", "order-1", "Order", "OrderCreated", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow) { GlobalPosition = 1 },
			new("e2", "order-2", "Order", "OrderCreated", Array.Empty<byte>(), null, 2, DateTimeOffset.UtcNow) { GlobalPosition = 2 },
			new("e3", "order-1", "Order", "OrderShipped", Array.Empty<byte>(), null, 3, DateTimeOffset.UtcNow) { GlobalPosition = 3 },
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
				// Record the aggregate carried by EACH event, in the order delivered.
				applyCallContexts.Add(string.Join(",", events.Select(static e => e.AggregateId)));
				return Task.CompletedTask;
			}));

		_services.AddSingleton(fakeQuery);
		var sp = _services.BuildServiceProvider();

		var options = new GlobalStreamProjectionOptions
		{ SubscriptionScope = "unit-test",
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		};

		using var cts = new CancellationTokenSource(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		var host = CreateHost(sp, fakeSerializer, options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		await WaitHelpers.WaitUntilAsync(
			() => applyCallContexts.Count >= 1,
			TestTimeouts.Scale(TimeSpan.FromSeconds(4)),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// ONE call, carrying every event in GLOBAL ORDER, each with its own aggregate.
		//
		// This replaces an assertion that the host dispatched once PER AGGREGATE. That grouping was the
		// defect: a projection keyed by anything other than the aggregate maps events from many
		// aggregates onto one projection id, so it was written once per group -- and group order is
		// first-seen order, not stream order. The same id could be written at a later position and then
		// an earlier one, inside a single batch, with no concurrency involved.
		//
		// Strengthened rather than relaxed: the old arm only counted calls, this one pins the ORDER,
		// which is the property that was actually broken.
		applyCallContexts.Count.ShouldBe(
			1,
			"the batch must be delivered as one ordered fold, not split into per-aggregate calls.");
		applyCallContexts[0].ShouldBe(
			"order-1,order-2,order-1",
			"events must arrive in global stream order, each carrying the aggregate it came from.");
	}

	[Theory]
	[InlineData(null)]
	[InlineData(2L)]
	[Trait("Pattern", "Regression")]
	public async Task ReportAReApplicationOnlyForEventsItsOwnCheckpointSaysItApplied(long? effectedThrough)
	{
		// THE PROPERTY, not the mechanism. The previous version of this arm asserted that classification
		// followed the stream head captured at start -- which was a true statement about the
		// implementation and said nothing about what a consumer is promised. IsReplay = true tells a
		// handler an effect has ALREADY been performed, so it may skip one; the only durable evidence
		// that anything was ever delivered to this subscription is its own checkpoint. The head says an
		// event was committed before this run, which is a fact about the STORE.
		//
		// The null case is the one the head-based version could not express: a subscription with no
		// checkpoint has no evidence of any delivery, so NOTHING is a re-application. Under the old
		// predicate every event at or below the head -- all of them -- was reported as already applied,
		// and because the advance is independent of this flag they were marked done with their effects
		// suppressed.
		var query = A.Fake<IGlobalStreamQuery>();
		if (effectedThrough is { } seeded)
		{
			_ = await _checkpointStore.AdvanceCheckpointAsync(
				"AsyncProjectionProcessingHost", null, seeded, TestContext.Current.CancellationToken)
				.ConfigureAwait(false);
		}
		var stored = Enumerable.Range(1, 4).Select(i => new StoredEvent(
			$"e{i}", $"order-{i % 2}", "Order", "OrderCreated", [], null, i, DateTimeOffset.UtcNow)
			{ GlobalPosition = i }).ToArray();
		A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((GlobalStreamPosition position, int _, CancellationToken _) =>
				new ValueTask<IReadOnlyList<StoredEvent>>(stored.Where(e => e.GlobalPosition > position.Position).ToArray()));
		var serializer = A.Fake<IEventSerializer>();
		A.CallTo(() => serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(new TestOrderPlaced());
		var observed = new List<(long? Position, bool Replay)>();
		var batchHighWaterMarks = new List<long?>();
		var expectedDeliveries = stored.Count(e => e.GlobalPosition > (effectedThrough ?? 0));
		var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_registry.Register(new ProjectionRegistration(typeof(OrderSummary), ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(), inlineApply: (events, context, _, _) =>
			{
				observed.AddRange(events.Select(e => (e.GlobalPosition, context.IsReplay)));
				batchHighWaterMarks.Add(context.GlobalPosition);

				// A seeded checkpoint means the host resumes ABOVE it, so only the events past it are
				// ever delivered. Waiting for the whole stream here would time out rather than fail.
				if (observed.Count >= expectedDeliveries)
				{
					applied.TrySetResult();
				}
				return Task.CompletedTask;
			}));
		_services.AddSingleton(query);
		using var provider = _services.BuildServiceProvider();
		using var host = CreateHost(provider, serializer, new GlobalStreamProjectionOptions
		{ SubscriptionScope = "unit-test",
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
			CheckpointInterval = 1,
		});
		await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
		try
		{
			await applied.Task.WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
		}
		finally
		{
			await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
		observed.ShouldBe(stored
			.Where(e => e.GlobalPosition > (effectedThrough ?? 0))
			.Select(e => ((long?)e.GlobalPosition, effectedThrough is { } m && e.GlobalPosition <= m))
			.ToList());
		observed.ShouldAllBe(
			o => !o.Replay,
			customMessage: "an event above the run-start checkpoint has had no effect performed for it");

		// The batch-level context must carry the LAST event's position, not the first. This is the only
		// host-level cover for that wiring -- the context type is covered in isolation elsewhere, but
		// nothing else asserts the host populates it from the right end of the page, and it is shipped
		// public surface that handlers read as the batch's high-water mark.
		batchHighWaterMarks.ShouldAllBe(
			m => m == 4L,
			customMessage: "the batch context's position is the page's last event");
		var checkpoint = await _checkpointStore.GetCheckpointAsync("AsyncProjectionProcessingHost", CancellationToken.None)
			.ConfigureAwait(false);
		checkpoint.ShouldBe(4L);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Regression")]
	public async Task RetryTheFailedBatchWithoutAdvancingPastIt(bool recover)
	{
		// REWRITTEN, and the reason is the point. This arm used to pick which run to fail by its REPLAY
		// CLASSIFICATION -- head = 1 made event 1 a "replay run" and event 2 a "live run" inside one
		// batch. That split was an artefact of classifying against the stream head. A re-application is
		// now decided by the subscription's own checkpoint, and the host never reads at or below its own
		// checkpoint, so a batch cannot contain both kinds and the old theory had no cases left.
		//
		// What that arm was really protecting is unchanged and is asserted here directly: a failed apply
		// must NOT advance the checkpoint past the events it failed on, and the retry must re-read them.
		// Asserting the classification instead was asserting the mechanism.
		var query = A.Fake<IGlobalStreamQuery>();
		var stored = Enumerable.Range(1, 2).Select(i => new StoredEvent(
			$"e{i}", "order-1", "Order", "OrderCreated", [], null, i, DateTimeOffset.UtcNow)
			{ GlobalPosition = i }).ToArray();
		var advanced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((GlobalStreamPosition position, int _, CancellationToken _) =>
			{
				if (position.Position == 2)
				{
					advanced.TrySetResult();
				}
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					stored.Where(e => e.GlobalPosition > position.Position).ToArray());
			});
		var serializer = A.Fake<IEventSerializer>();
		A.CallTo(() => serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(new TestOrderPlaced());
		var observed = new List<(long? Position, bool Replay)>();
		var applies = 0;
		var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_registry.Register(new ProjectionRegistration(typeof(OrderSummary), ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(), inlineApply: (events, context, _, _) =>
			{
				observed.AddRange(events.Select(e => (e.GlobalPosition, context.IsReplay)));
				applies++;
				if (recover && applies > 1)
				{
					return Task.CompletedTask;
				}

				if (applies >= 2)
				{
					retried.TrySetResult();
				}

				throw new InvalidOperationException("Injected apply failure");
			}));
		_services.AddSingleton(query);
		using var provider = _services.BuildServiceProvider();
		using var host = CreateHost(provider, serializer, new GlobalStreamProjectionOptions
		{ SubscriptionScope = "unit-test",
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
			CheckpointInterval = 1,
		});
		await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
		try
		{
			await (recover ? advanced.Task : retried.Task)
				.WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
		}
		finally
		{
			await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}

		var checkpoints = await _checkpointStore.EnumerateCheckpointsAsync(CancellationToken.None)
			.ConfigureAwait(false);
		if (recover)
		{
			checkpoints.ShouldHaveSingleItem().Position.ShouldBe(2L);
		}
		else
		{
			checkpoints.ShouldBeEmpty("a batch whose apply threw must not move the mark past it");
		}

		// The retry re-reads the SAME events rather than skipping them.
		observed.Take(4).Select(e => e.Position).ShouldBe(new long?[] { 1, 2, 1, 2 });

		// And every one of them is a first delivery: the host resumes ABOVE its checkpoint, so it never
		// hands a handler an event its own durable mark says was already applied. A true here would be
		// telling a handler to skip an effect that was never performed.
		observed.ShouldAllBe(
			e => !e.Replay,
			customMessage: "the host delivers only events above its checkpoint, so none is a re-application");
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task SaveNoCheckpointWhenStoppedPartWayThroughABatch()
	{
		// The safety property, kept. This arm used to pass for an incidental reason: the stream head
		// split one page into a "replay run" and a "live run", and the SECOND run's cancellation check
		// was what stopped the advance. Classification now reads the subscription's own checkpoint, the
		// host never reads at or below it, and a page is therefore ONE run -- so the accident is gone
		// and the property had to be made real. It is now enforced where it belongs: the advance itself
		// refuses to move when the token is cancelled.
		//
		// The handler below catches OperationCanceledException and returns, which is the case that
		// matters -- the host cannot distinguish it from a clean apply, so the only safe reading of a
		// cancelled apply is that it did not complete. Advancing would mark these events done and the
		// shutdown flush would persist it, after which nothing redelivers them.
		var query = A.Fake<IGlobalStreamQuery>();
		var stored = Enumerable.Range(1, 2).Select(i => new StoredEvent(
			$"e{i}", "order-1", "Order", "OrderCreated", [], null, i, DateTimeOffset.UtcNow)
			{ GlobalPosition = i }).ToArray();
		A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(stored));
		var serializer = A.Fake<IEventSerializer>();
		A.CallTo(() => serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(new TestOrderPlaced());
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var observed = new List<long?>();
		_registry.Register(new ProjectionRegistration(typeof(OrderSummary), ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(), inlineApply: async (events, _, _, token) =>
			{
				observed.AddRange(events.Select(e => e.GlobalPosition));
				entered.TrySetResult();
				// Hold the run until StopAsync cancels it, then finish normally. Shutdown must not
				// persist a checkpoint for it.
				try
				{
					await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (token.IsCancellationRequested)
				{
				}
			}));
		_services.AddSingleton(query);
		using var provider = _services.BuildServiceProvider();
		using var host = CreateHost(provider, serializer, new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test", CheckpointInterval = 1 });
		await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
		try
		{
			await entered.Task.WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
		}
		finally
		{
			await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
		// One run, so the whole page reaches the apply in a single call -- never split, never partly
		// dispatched.
		observed.ShouldBe(new long?[] { 1, 2 });

		// THE SAFETY PROPERTY: nothing is persisted for a run shutdown cancelled, so the next start
		// re-reads it. A duplicate delivery is declared and handlers owe idempotency; an event marked
		// done that nobody applied is neither.
		var checkpoints = await _checkpointStore.EnumerateCheckpointsAsync(CancellationToken.None).ConfigureAwait(false);
		checkpoints.ShouldBeEmpty("a run cancelled by shutdown must not leave a checkpoint behind");
	}

	[Fact]
	public async Task RestoreCheckpoint_OnStartup()
	{
		// Arrange — store a checkpoint so the host resumes from it
		await _checkpointStore.AdvanceCheckpointAsync("AsyncProjectionProcessingHost", null, 42, CancellationToken.None)
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
		{ SubscriptionScope = "unit-test",
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
			new("e1", "order-1", "Order", "BadEvent", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow) { GlobalPosition = 1 },
			new("e2", "order-1", "Order", "GoodEvent", Array.Empty<byte>(), null, 2, DateTimeOffset.UtcNow) { GlobalPosition = 2 },
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
		{ SubscriptionScope = "unit-test",
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
	public async Task LatchTheHaltIntoHealthStateSoAStoppedHostIsNotReportedHealthy()
	{
		// THE WIRING HALF, and it is the half that was missing from the field this replaces.
		// ProjectionHealthState.AsyncLag existed, was published in the API baseline, was compared against
		// thresholds by the health check -- and NO host ever assigned it, so the check reported healthy at
		// every lag. Arms that set the field themselves could not see that. So this arm asserts the HOST
		// writes the latch, driving the real poison path, rather than calling RecordHalt itself.
		var healthState = new ProjectionHealthState();

		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		var callCount = 0;
		var storedEvents = new List<StoredEvent>
		{
			new("e1", "order-1", "Order", "BadEvent", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow) { GlobalPosition = 1 },
		};

		A.CallTo(() => fakeQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref callCount);
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					count == 1 ? storedEvents : (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
			});

		var poisonObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var fakeSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => fakeSerializer.ResolveType("BadEvent"))
			.Invokes(() => poisonObserved.TrySetResult())
			.Throws(new InvalidOperationException("Unknown event type"));

		_registry.Register(CreateAsyncRegistration());
		_ = _services.AddSingleton(fakeQuery);
		_ = _services.AddSingleton(healthState);
		var sp = _services.BuildServiceProvider();

		using var cts = new CancellationTokenSource(TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		using var host = CreateHost(sp, fakeSerializer, new GlobalStreamProjectionOptions
		{ SubscriptionScope = "unit-test",
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		});

		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);
		await poisonObserved.Task.WaitAsync(TestTimeouts.Scale(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
		// Poll the condition this test asserts rather than sleeping toward it. A fixed window is both
		// slower than it needs to be and flaky under a loaded agent: the halt is recorded by the host
		// loop a moment after the poison is observed, and 250ms was a guess at how long that takes.
		_ = await WaitHelpers.WaitUntilAsync(
			() => healthState.HaltedSubscription is not null,
			TestTimeouts.Scale(TimeSpan.FromSeconds(30)),
			cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Asserted unconditionally, NOT via the poll's return value: a timed-out poll must fail on the
		// value the test is about, naming what was read, rather than on a bare "returned false".
		healthState.HaltedSubscription.ShouldBe(
			GlobalStreamProjectionOptions.DefaultProjectionName,
			customMessage: "the host must record its own halt, or the health check reads a field nobody writes");
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
					// EXCLUSIVE: served while the cursor has not yet DELIVERED position 1. Under <= the stub
					// re-serves the event after the host has processed it, and the host loops forever.
					position.Position < pendingEvent.GlobalPosition
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
		{ SubscriptionScope = "unit-test",
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
					pos.Position < 1
						? new List<StoredEvent>
						{
							new("e1", "order-1", "Order", "OrderCreated", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow) { GlobalPosition = 1 },
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
		{ SubscriptionScope = "unit-test",
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
		private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _definitions = new(StringComparer.Ordinal);

		public Task BindSubscriptionAsync(string subscriptionName, string definitionHash, CancellationToken cancellationToken)
		{
			SubscriptionDefinitionValidation.Validate(subscriptionName, definitionHash);
			cancellationToken.ThrowIfCancellationRequested();
			var actual = _definitions.GetOrAdd(subscriptionName, definitionHash);
			SubscriptionDefinitionValidation.RequireMatch(subscriptionName, definitionHash, actual);
			return Task.CompletedTask;
		}

		private readonly Dictionary<string, long> _checkpoints = new();

		public Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken)
		{
			_checkpoints.TryGetValue(subscriptionName, out var pos);
			return Task.FromResult(pos == 0 && !_checkpoints.ContainsKey(subscriptionName)
				? (long?)null
				: pos);
		}

		public Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
			string subscriptionName,
			long? expectedPosition,
			long newPosition,
			CancellationToken cancellationToken)
		{
			// Honours the compare-and-set the contract requires. A fake that just assigned would let a
			// host with a stale expected value pass here and fail against every real implementation.
			var present = _checkpoints.TryGetValue(subscriptionName, out var current);
			var matches = expectedPosition is null ? !present : present && current == expectedPosition.Value;
			if (!matches)
			{
				return Task.FromResult(CheckpointAdvanceOutcome.Superseded);
			}

			_checkpoints[subscriptionName] = newPosition;
			return Task.FromResult(CheckpointAdvanceOutcome.Advanced);
		}

		public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken)
		{
			IReadOnlyList<SubscriptionCheckpoint> checkpoints =
				[.. _checkpoints.Select(kvp => new SubscriptionCheckpoint(kvp.Key, kvp.Value))];
			return Task.FromResult(checkpoints);
		}
	}
}
