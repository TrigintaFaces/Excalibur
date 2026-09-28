// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy .Returns stores ValueTask)

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Binds what a projection host does when another reader has advanced the subscription checkpoint.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong.</b> Losing the compare-and-set returned from the background service, which ends it
/// permanently. Standing down is only correct if the winner is guaranteed to keep running, and nothing
/// guarantees that: after a rolling deploy the surviving process is the one that quit, no reader is
/// processing the subscription, and the application still reports healthy. A permanent exit converts a
/// transient overlap into a permanent stall, which is worse precisely because nothing downstream can
/// detect it.
/// </para>
/// <para>
/// There was no test for the superseded path at all, which is why the exit shipped.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionHostStandsByWhenSupersededShould
{
	private readonly IGlobalStreamQuery _globalStreamQuery = A.Fake<IGlobalStreamQuery>();

	private readonly IGlobalStreamProjection<GlobalStreamTestState> _projection =
		A.Fake<IGlobalStreamProjection<GlobalStreamTestState>>();

	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();
	private readonly ISubscriptionCheckpointStore _checkpointStore = A.Fake<ISubscriptionCheckpointStore>();
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
	private readonly IServiceScopeFactory _scopeFactory = A.Fake<IServiceScopeFactory>();

	/// <summary>
	/// SAFETY: a superseded reader keeps polling, so it can take over if the winner later stops.
	/// </summary>
	[Fact]
	public async Task Keep_polling_after_another_reader_takes_the_checkpoint()
	{
		var readsAfterSupersession = 0;
		var superseded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var polledAgain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		ArrangeOneEventPerPoll(() =>
		{
			if (!superseded.Task.IsCompleted)
			{
				return;
			}

			if (Interlocked.Increment(ref readsAfterSupersession) >= 2)
			{
				polledAgain.TrySetResult();
			}
		});

		// The mark is always taken by someone else.
		A.CallTo(() => _checkpointStore.AdvanceCheckpointAsync(
				A<string>._, A<long?>._, A<long>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				superseded.TrySetResult();
				return Task.FromResult(CheckpointAdvanceOutcome.Superseded);
			});

		var host = CreateHost();
		using var cts = new CancellationTokenSource();

		await host.StartAsync(cts.Token);
		await AwaitSignalAsync(superseded.Task);

		// The assertion: the host is STILL READING. Before the fix it returned here, so this never
		// completes, the wait times out, and the arm is RED.
		await AwaitSignalAsync(polledAgain.Task);

		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		readsAfterSupersession.ShouldBeGreaterThanOrEqualTo(
			2,
			"a reader that loses the race must keep polling, or a rolling deploy leaves the surviving "
			+ "process as the one that stood down and nothing processes the subscription.");
	}

	/// <summary>
	/// LIVENESS: it re-reads the checkpoint after losing, rather than continuing from its own stale mark.
	/// </summary>
	/// <remarks>
	/// Without this the safety arm above would be satisfied by a host that ignored the outcome entirely
	/// and kept re-reading from its own position, which is the duplicate delivery the fix exists to avoid.
	/// </remarks>
	[Fact]
	public async Task Adopt_the_mark_of_the_reader_that_won_rather_than_its_own()
	{
		var reReadAfterSupersession = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var supersededOnce = false;

		ArrangeOneEventPerPoll(null);

		A.CallTo(() => _checkpointStore.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				if (supersededOnce)
				{
					reReadAfterSupersession.TrySetResult();
				}

				return Task.FromResult<long?>(500);
			});

		A.CallTo(() => _checkpointStore.AdvanceCheckpointAsync(
				A<string>._, A<long?>._, A<long>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				supersededOnce = true;
				return Task.FromResult(CheckpointAdvanceOutcome.Superseded);
			});

		var host = CreateHost();
		using var cts = new CancellationTokenSource();

		await host.StartAsync(cts.Token);
		await AwaitSignalAsync(reReadAfterSupersession.Task);

		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		// Re-reading the checkpoint AFTER losing is what adopting the other reader's mark looks like
		// from outside the host.
		A.CallTo(() => _checkpointStore.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.OrMore);
	}

	private void ArrangeOneEventPerPoll(Action? onPoll)
	{
		var storedEvent = new StoredEvent(
			"evt-1", "agg-1", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, 5, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 10,
		};

		var domainEvent = A.Fake<IDomainEvent>();

		A.CallTo(() => _checkpointStore.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(null));

		A.CallTo(() => _globalStreamQuery.ReadAllAsync(
				A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				onPoll?.Invoke();
				return new ValueTask<IReadOnlyList<StoredEvent>>(new[] { storedEvent });
			});

		A.CallTo(() => _globalStreamQuery.GetHeadPositionAsync(A<CancellationToken>._))
			.Returns(new ValueTask<long>(10L));
		A.CallTo(() => _eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(domainEvent);
		A.CallTo(() => _projection.ApplyAsync(domainEvent, A<GlobalStreamTestState>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);
	}

	private GlobalStreamProjectionHost<GlobalStreamTestState> CreateHost()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_globalStreamQuery);
		_ = services.AddSingleton(_projection);
		_ = services.AddSingleton(_checkpointStore);

		var scopedProvider = services.BuildServiceProvider();
		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(scopedProvider);
		A.CallTo(() => _scopeFactory.CreateScope()).Returns(scope);

		return new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_scopeFactory,
			_eventSerializer,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions
			{
				IdlePollingInterval = TimeSpan.FromMilliseconds(10),
				CheckpointInterval = 1,
			}),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance,
			_serviceProvider);
	}

	private static Task AwaitSignalAsync(Task signal) =>
		global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			signal,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)),
			cancellationToken: CancellationToken.None);
}

#pragma warning restore CA2012
