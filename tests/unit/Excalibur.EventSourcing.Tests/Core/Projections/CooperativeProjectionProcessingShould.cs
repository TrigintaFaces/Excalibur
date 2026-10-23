// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Safety: checkpoint progress never acknowledges a different workset, and concurrent drivers do not
/// duplicate positioned state. Liveness: queued page requests run in FIFO order without worker starvation.
/// Task signals establish the interleaving; timeouts only bound a broken test.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class CooperativeProjectionProcessingShould
{
	[Fact]
	public async Task AdmitQueuedManualDriversInFifoOrder()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		using var firstToken = CancellationTokenSource.CreateLinkedTokenSource(safety.Token);
		using var secondToken = CancellationTokenSource.CreateLinkedTokenSource(safety.Token);
		using var thirdToken = CancellationTokenSource.CreateLinkedTokenSource(safety.Token);
		var entered = Signal();
		var release = Signal();
		var admitted = new ConcurrentQueue<int>();
		var query = new Stream(1, async (position, _, token) =>
		{
			admitted.Enqueue(token == firstToken.Token ? 1 : token == secondToken.Token ? 2 : 3);
			if (token == firstToken.Token)
			{
				entered.TrySetResult();
				await release.Task.WaitAsync(token).ConfigureAwait(false);
			}
			return Events(position, 1);
		});
		using var provider = Provider(query);
		using var host = Host(Registry<Counter>(), new InMemorySubscriptionCheckpointStore(), provider);
		var first = host.ProcessPendingAsync(firstToken.Token);
		await entered.Task.WaitAsync(safety.Token).ConfigureAwait(false);
		// Head reads complete synchronously. Each invocation reaches the occupied page gate before
		// returning its incomplete task, so these calls establish queue order without a timing guess.
		var second = host.ProcessPendingAsync(secondToken.Token);
		var third = host.ProcessPendingAsync(thirdToken.Token);
		second.IsCompleted.ShouldBeFalse();
		third.IsCompleted.ShouldBeFalse();
		release.TrySetResult();
		var results = await Task.WhenAll(first, second, third).WaitAsync(safety.Token).ConfigureAwait(false);
		admitted.ToArray().ShouldBe([1, 2, 3]);
		results.ShouldAllBe(result => result.Outcome == ProjectionProcessingOutcome.CaughtUp);
	}

	[Fact]
	public async Task CancelAQueuedDriverWithoutPreventingTheNextOneFromRunning()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(safety.Token);
		var entered = Signal();
		var release = Signal();
		var reads = 0;
		var query = new Stream(1, async (position, _, token) =>
		{
			if (Interlocked.Increment(ref reads) == 1)
			{
				entered.TrySetResult();
				await release.Task.WaitAsync(token).ConfigureAwait(false);
			}
			return Events(position, 1);
		});
		using var provider = Provider(query);
		using var host = Host(Registry<Counter>(), new InMemorySubscriptionCheckpointStore(), provider);
		var holder = host.ProcessPendingAsync(safety.Token);
		await entered.Task.WaitAsync(safety.Token).ConfigureAwait(false);
		var abandoned = host.ProcessPendingAsync(cancelled.Token);
		var successor = host.ProcessPendingAsync(safety.Token);
		await cancelled.CancelAsync().ConfigureAwait(false);
		(await abandoned.WaitAsync(safety.Token).ConfigureAwait(false)).Outcome.ShouldBe(ProjectionProcessingOutcome.Cancelled);
		release.TrySetResult();
		await holder.WaitAsync(safety.Token).ConfigureAwait(false);
		(await successor.WaitAsync(safety.Token).ConfigureAwait(false)).Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		reads.ShouldBe(2, "the cancelled waiter must never enter the event scan");
	}

	[Fact]
	public async Task GiveAQueuedManualPageATurnBeforeTheBackgroundWorkerReacquires()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		using var manualToken = CancellationTokenSource.CreateLinkedTokenSource(safety.Token);
		var workerEntered = Signal();
		var releaseWorker = Signal();
		var admitted = new ConcurrentQueue<string>();
		var reads = 0;
		var query = new Stream(2, async (position, _, token) =>
		{
			admitted.Enqueue(token == manualToken.Token ? "manual" : "worker");
			if (Interlocked.Increment(ref reads) == 1)
			{
				workerEntered.TrySetResult();
				await releaseWorker.Task.WaitAsync(token).ConfigureAwait(false);
			}
			return Events(position, 2).Take(1).ToArray();
		});
		using var provider = Provider(query);
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		using var host = Host(Registry<Counter>(), checkpoints, provider);
		await host.StartAsync(safety.Token).ConfigureAwait(false);
		try
		{
			await workerEntered.Task.WaitAsync(safety.Token).ConfigureAwait(false);
			var manual = host.ProcessPendingAsync(manualToken.Token);
			manual.IsCompleted.ShouldBeFalse();
			releaseWorker.TrySetResult();
			var result = await manual.WaitAsync(safety.Token).ConfigureAwait(false);
			result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
			result.EventsApplied.ShouldBe(1, "the request must process page two while the worker remains live");
			admitted.Take(2).ShouldBe(["worker", "manual"]);
			(await checkpoints.GetCheckpointAsync("cooperative", safety.Token).ConfigureAwait(false)).ShouldBe(2);
		}
		finally
		{
			releaseWorker.TrySetResult();
			await host.StopAsync(safety.Token).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task ReconcileTwoHostsSharingACheckpointWithoutDuplicatingPositionedState()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var bothRead = Signal();
		var initialReads = 0;
		var query = new Stream(2, async (position, _, token) =>
		{
			if (position.Position == 0)
			{
				if (Interlocked.Increment(ref initialReads) == 2) bothRead.TrySetResult();
				await bothRead.Task.WaitAsync(token).ConfigureAwait(false);
			}
			return Events(position, 2);
		});
		var registry = new InMemoryProjectionRegistry();
		var builder = new ProjectionBuilder<Counter>(registry);
		builder.Async().When<TestOrderPlaced>((projection, _) => projection.Count++);
		builder.Build();
		var store = new InMemoryProjectionStore<Counter>();
		using var provider = Provider(query, store);
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		using var first = Host(registry, checkpoints, provider);
		using var second = Host(registry, checkpoints, provider);
		var results = await Task.WhenAll(first.ProcessPendingAsync(safety.Token), second.ProcessPendingAsync(safety.Token))
			.WaitAsync(safety.Token).ConfigureAwait(false);
		results.ShouldAllBe(result => result.Outcome == ProjectionProcessingOutcome.CaughtUp);
		var held = await store.GetWithPositionAsync("order-1", safety.Token).ConfigureAwait(false);
		held.Projection.ShouldNotBeNull().Count.ShouldBe(2);
		held.Position.Value.ShouldBe(2);
		(await checkpoints.GetCheckpointAsync("cooperative", safety.Token).ConfigureAwait(false)).ShouldBe(2);
		initialReads.ShouldBe(2, "both hosts must really race from the same durable starting point");
	}

	[Theory]
	[InlineData("workset")]
	[InlineData("scope")]
	[InlineData("revision")]
	public async Task RefuseADifferentSubscriptionDefinitionBeforeScanningOrEffects(string changed)
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var reads = 0;
		var effects = 0;
		var query = new Stream(1, (position, _, _) =>
		{
			Interlocked.Increment(ref reads);
			return ValueTask.FromResult(Events(position, 1));
		});
		using var provider = Provider(query);
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		using var original = Host(Registry<Counter>(), checkpoints, provider);
		await original.ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		var otherRegistry = Registry<Counter>(() => effects++);
		if (changed == "workset")
		{
			otherRegistry.Register(new ProjectionRegistration(typeof(OtherCounter), ProjectionMode.Async,
				new MultiStreamProjection<OtherCounter>(), inlineApply: (_, _, _, _) =>
				{
					effects++;
					return Task.CompletedTask;
				}));
		}
		var options = Configuration();
		if (changed == "scope") options.SubscriptionScope = "different-database";
		if (changed == "revision") options.SubscriptionRevision = "2";
		using var incompatible = Host(otherRegistry, checkpoints, provider, options);
		await Should.ThrowAsync<InvalidOperationException>(() => incompatible.ProcessPendingAsync(safety.Token));
		reads.ShouldBe(1, "the mismatching host must be refused before reading a page");
		effects.ShouldBe(0);
		(await checkpoints.GetCheckpointAsync("cooperative", safety.Token).ConfigureAwait(false)).ShouldBe(1);
	}

	private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
	private static GlobalStreamProjectionOptions Configuration() => new()
	{
		ProjectionName = "cooperative", SubscriptionScope = "unit-source-and-destination", BatchSize = 1,
		IdlePollingInterval = TimeSpan.FromHours(1),
	};

	private static AsyncProjectionProcessingHost Host(InMemoryProjectionRegistry registry,
		ISubscriptionCheckpointStore checkpoints, IServiceProvider provider, GlobalStreamProjectionOptions? options = null)
	{
		var serializer = A.Fake<IEventSerializer>();
		A.CallTo(() => serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(new TestOrderPlaced());
		return new AsyncProjectionProcessingHost(registry, serializer, checkpoints,
			Options.Create(options ?? Configuration()), provider, NullLogger<AsyncProjectionProcessingHost>.Instance);
	}

	private static ServiceProvider Provider(IGlobalStreamQuery query, InMemoryProjectionStore<Counter>? store = null)
	{
		var services = new ServiceCollection();
		services.AddSingleton(query);
		if (store is not null) services.AddSingleton<IProjectionStore<Counter>>(store);
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
	}

	private static InMemoryProjectionRegistry Registry<T>(Action? applied = null) where T : class, new()
	{
		var registry = new InMemoryProjectionRegistry();
		registry.Register(new ProjectionRegistration(typeof(T), ProjectionMode.Async, new MultiStreamProjection<T>(),
			inlineApply: (_, _, _, _) => { applied?.Invoke(); return Task.CompletedTask; }));
		return registry;
	}

	private static IReadOnlyList<StoredEvent> Events(GlobalStreamPosition after, int head) =>
		[.. Enumerable.Range(1, head).Where(position => position > after.Position)
			.Select(position => new StoredEvent($"event-{position}", "order-1", "Order", "TestOrderPlaced",
				[1], null, position, DateTimeOffset.UnixEpoch) { GlobalPosition = position })];

	private sealed class Stream(long head,
		Func<GlobalStreamPosition, int, CancellationToken, ValueTask<IReadOnlyList<StoredEvent>>> read) : IGlobalStreamQuery
	{
		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) => ValueTask.FromResult(head);
		public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(GlobalStreamPosition position, int maxCount,
			CancellationToken cancellationToken) =>
			(await read(position, maxCount, cancellationToken).ConfigureAwait(false)).Take(maxCount).ToArray();
		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(string eventType, GlobalStreamPosition position,
			int maxCount, CancellationToken cancellationToken) => ReadAllAsync(position, maxCount, cancellationToken);
	}

	internal sealed class Counter { public int Count { get; set; } }
	internal sealed class OtherCounter { public int Count { get; set; } }
}
