// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — fakes store ValueTask in setup
#pragma warning disable CA1506 // Avoid excessive class coupling — engage-tests require many DI types

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

// --- bd-red2ha (S841, ADR-336 Amendment 3a): FR-6 / AC-8, AC-8a — projection poison-halt parity ---
//
// Two live projection hosts still SKIP-AND-ADVANCE past poison events — the same silent read-model
// data-loss class S840 `c3jdco` extinguished on GlobalStreamProjectionHost:
//   * AsyncProjectionProcessingHost.DeserializeEvents drops a null/undeserializable event, then the
//     main loop advances _currentPosition to GlobalPosition+1 and checkpoints PAST it.
//   * ProjectionRebuildService catches per-event apply/deserialize failure + continue, then advances
//     past the batch and persists the "rebuilt" state as Completed.
//
// These are INDEPENDENT engage-tests (author≠implementer, TestsDeveloper). They bind the same invariant
// the GlobalStreamProjectionHost fix locks: on a poison event the host MUST halt at the failed event and
// MUST NOT advance the checkpoint/position (or persist a complete rebuild) past it — so the poison event
// is reprocessed/quarantined on restart, never silently skipped. A null deserialization is treated as an
// ERROR (halt), not a skip. RED on the pre-fix skip-and-advance behavior; GREEN on the halt-at-failure fix.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionPoisonHaltParityShould
{
	// AC-8: AsyncProjectionProcessingHost reading a batch with a poison (null-deserialized) event MUST NOT
	// advance the checkpoint past it. RED today: the host drops the null event and checkpoints GlobalPosition+1.
	[Fact]
	public async Task NotAdvanceCheckpointPastAPoisonEvent_AsyncProjectionProcessingHost()
	{
		// Arrange — a single poison event that deserializes to null. CheckpointInterval=1 so a (buggy)
		// skip-and-advance persists a checkpoint immediately — making the lock non-vacuous.
		var poison = new StoredEvent(
			"evt-poison", "agg-1", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 5,
		};
		var deserializeObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Real registry double (internal IProjectionRegistry is not FakeItEasy-proxyable).
		var registry = new InMemoryProjectionRegistry();
		registry.Register(new ProjectionRegistration(
			typeof(RebuildPoisonProjection), ProjectionMode.Async, projection: new object(), inlineApply: null));

		var globalStreamQuery = A.Fake<IGlobalStreamQuery>();
		var readCount = 0;
		A.CallTo(() => globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var c = Interlocked.Increment(ref readCount);
				return c == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new[] { poison })
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		var eventSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.Invokes(() => deserializeObserved.TrySetResult())
			.Returns((IDomainEvent)null!); // poison: null deserialization (the silently-dropped case)

		var checkpointStore = A.Fake<ISubscriptionCheckpointStore>();
		A.CallTo(() => checkpointStore.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(null));

		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(IGlobalStreamQuery))).Returns(globalStreamQuery);

		var host = new AsyncProjectionProcessingHost(
			registry,
			eventSerializer,
			checkpointStore,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions
			{
				IdlePollingInterval = TimeSpan.FromMilliseconds(10),
				CheckpointInterval = 1,
			}),
			serviceProvider,
			NullLogger<AsyncProjectionProcessingHost>.Instance);

		using var cts = new CancellationTokenSource();

		// Act — start, wait until the poison is deserialized (to null), give a halt/advance a window, then stop.
		await host.StartAsync(cts.Token);
		await AwaitSignalAsync(deserializeObserved.Task);
		await Task.Delay(global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromMilliseconds(250)))
			.ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		// Assert — the checkpoint must NEVER be persisted past the unapplied poison event.
		A.CallTo(() => checkpointStore.StoreCheckpointAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	// AC-8a: ProjectionRebuildService applying a batch with a poison (null-deserialized) event MUST halt and
	// MUST NOT advance past it — so it never persists the rebuilt state as Completed. RED today: it catches the
	// poison + continues, advances past the batch, persists via UpsertAsync, and reports Completed.
	[Fact]
	public async Task HaltAndNotPersistACompleteRebuildPastAPoisonEvent_ProjectionRebuildService()
	{
		// Arrange — a single poison event that deserializes to null in the rebuild stream.
		var poison = new StoredEvent(
			"evt-poison", "agg-1", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 5,
		};

		var globalStreamQuery = A.Fake<IGlobalStreamQuery>();
		var readCount = 0;
		A.CallTo(() => globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var c = Interlocked.Increment(ref readCount);
				return c == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new[] { poison })
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		var eventSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.Returns((IDomainEvent)null!); // poison: null deserialization

		// Real store double — lets us assert that NO rebuilt state was persisted past the poison.
		var store = new InMemoryProjectionStore<RebuildPoisonProjection>();
		var projection = new MultiStreamProjection<RebuildPoisonProjection>();

		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(IGlobalStreamQuery))).Returns(globalStreamQuery);
		A.CallTo(() => serviceProvider.GetService(typeof(MultiStreamProjection<RebuildPoisonProjection>)))
			.Returns(projection);
		A.CallTo(() => serviceProvider.GetService(typeof(IProjectionStore<RebuildPoisonProjection>)))
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

		// Act — rebuild over a stream containing a poison event. A halt-via-throw is an acceptable halt
		// mechanism (mirrors the GlobalStreamProjectionHost fix), so we tolerate a throw and verify the
		// structural invariant below.
		try
		{
			await service.RebuildAsync<RebuildPoisonProjection>(CancellationToken.None).ConfigureAwait(false);
		}
#pragma warning disable CA1031 // Catch general exceptions — halt-by-throw is an acceptable halt mechanism
		catch (Exception)
#pragma warning restore CA1031
		{
			// Intentionally swallowed: the assertions below verify the rebuild did not advance past the poison.
		}

		// Assert — the rebuild must NOT persist a "complete" rebuilt state past the poison, nor report Completed.
		var persisted = await store.GetByIdAsync(nameof(RebuildPoisonProjection), CancellationToken.None)
			.ConfigureAwait(false);
		persisted.ShouldBeNull("the rebuild must halt at the poison event and not persist a completed projection");

		var status = await service.GetStatusAsync<RebuildPoisonProjection>(CancellationToken.None)
			.ConfigureAwait(false);
		status.State.ShouldNotBe(ProjectionRebuildState.Completed);
	}

	private static Task AwaitSignalAsync(Task signal)
	{
		return global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			signal,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)),
			cancellationToken: CancellationToken.None);
	}
}

/// <summary>Projection state type used to drive the poison-halt engage-tests.</summary>
internal sealed class RebuildPoisonProjection
{
	public int Applied { get; set; }
}

#pragma warning restore CA1506
#pragma warning restore CA2012
