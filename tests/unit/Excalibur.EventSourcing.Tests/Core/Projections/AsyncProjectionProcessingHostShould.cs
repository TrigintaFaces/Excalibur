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

		// Give generous time for ExecuteAsync to fire-and-forget under CI thread pool load
		await Task.Delay(2000).ConfigureAwait(false);
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
		// Generous delay for CI thread pool scheduling of fire-and-forget ExecuteAsync
		await Task.Delay(2000).ConfigureAwait(false);
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

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var host = CreateHost(sp, options: options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until ReadAllAsync is called — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref readAllCalled) > 0,
			TimeSpan.FromSeconds(4),
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

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var host = CreateHost(sp, fakeSerializer, options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until apply is invoked — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref applyInvoked) > 0,
			TimeSpan.FromSeconds(4),
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

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var host = CreateHost(sp, fakeSerializer, options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until both aggregate groups have been applied — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => applyCallContexts.Count >= 2,
			TimeSpan.FromSeconds(4),
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

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var host = CreateHost(sp, options: options);

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until capturedPosition is set — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => capturedPosition != null,
			TimeSpan.FromSeconds(4),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);
		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — polling started from the checkpointed position (42)
		capturedPosition.ShouldNotBeNull();
		capturedPosition.Position.ShouldBe(42);
	}

	[Fact]
	public async Task SkipUndeserializableEvents_WithoutCrashing()
	{
		// Arrange — serializer throws for one event type
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

		var fakeSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => fakeSerializer.ResolveType("BadEvent"))
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

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var host = CreateHost(sp, fakeSerializer, new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		});

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until the good event is applied — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref appliedCount) >= 1,
			TimeSpan.FromSeconds(4),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — the good event was still processed, bad event was skipped
		appliedCount.ShouldBe(1);
	}

	[Fact]
	public async Task ContinuePolling_AfterProjectionApplyError()
	{
		// Arrange — apply delegate throws on first batch, succeeds on second
		var fakeQuery = A.Fake<IGlobalStreamQuery>();
		var callCount = 0;

		A.CallTo(() => fakeQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref callCount);
				return new ValueTask<IReadOnlyList<StoredEvent>>(
					count <= 2
						? new List<StoredEvent>
						{
							new($"e-{count}", "order-1", "Order", "OrderCreated", Array.Empty<byte>(), null, count, DateTimeOffset.UtcNow),
						}
						: (IReadOnlyList<StoredEvent>)Array.Empty<StoredEvent>());
			});

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

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var host = CreateHost(sp, fakeSerializer, new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(50),
		});

		// Act
		await ((BackgroundService)host).StartAsync(cts.Token).ConfigureAwait(false);

		// Poll until we see at least 2 apply calls (first throws, second succeeds)
		// — avoids fragile fixed-delay timing on CI runners.
		await WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref applyCallCount) >= 2,
			TimeSpan.FromSeconds(4),
			TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		await ((BackgroundService)host).StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — the host continued after the first error and processed more batches
		applyCallCount.ShouldBeGreaterThanOrEqualTo(2);
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
	}
}