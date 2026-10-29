// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly -- fakes store ValueTask in setup
#pragma warning disable CA1506 // Avoid excessive class coupling -- engage-tests require many DI types

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

// The projection checkpoint is a PREFIX CLAIM: advancing it to position P asserts that every position
// at or below P has been folded. A page containing a gap breaks that claim silently and
// unrecoverably -- the skipped position sits below the cursor forever, so nothing redelivers it and
// nothing downstream ever learns it was missed. That is the catastrophic-by-silence shape.
//
// Contiguity was supplied only by ContiguousGlobalStreamQuery, an OUTER DECORATOR applied with TryAdd
// on a public interface. TryAdd means the first registration wins, so a consumer -- or a second
// UseXxx() call -- displaces it and the guarantee is then silently absent, while the host's own check
// accepted any strictly-increasing page. These arms bind the assertion at the host, where the
// checkpoint is actually written, so the bad state is unreachable regardless of who registered what.
//
// RED-detects the absence of that assertion: with the host accepting a merely-advancing page, the
// SAFETY arm below sees the checkpoint advance to 2 while position 1 was never folded.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionCheckpointContiguityShould
{
	// SAFETY: a page whose first position skips one must NOT advance the checkpoint.
	[Fact]
	public async Task RefuseToAdvanceTheCheckpointOverAGappedPage()
	{
		// Checkpoint is empty, so the contiguous run begins at 1. This page begins at 2.
		var checkpointStore = await RunOnePageAsync(firstPosition: 2).ConfigureAwait(false);

		A.CallTo(() => checkpointStore.AdvanceCheckpointAsync(
				A<string>._, A<string>._, A<Guid>._, A<long?>._, A<long>.That.IsGreaterThan(0), A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	// LIVENESS: the same host, same shape, a CONTIGUOUS page -- must advance. Without this the safety
	// arm above is satisfied by a host that never does anything at all, which is the failure mode a
	// safety-only assertion cannot distinguish from a working guard.
	[Fact]
	public async Task AdvanceTheCheckpointOverAContiguousPage()
	{
		var checkpointStore = await RunOnePageAsync(firstPosition: 1).ConfigureAwait(false);

		A.CallTo(() => checkpointStore.AdvanceCheckpointAsync(
				A<string>._, A<string>._, A<Guid>._, A<long?>._, A<long>._, A<CancellationToken>._))
			.MustHaveHappenedOnceOrMore();
	}

	private static async Task<ISubscriptionCheckpointStore> RunOnePageAsync(long firstPosition)
	{
		var stored = new StoredEvent(
			"evt-1", "agg-1", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow)
		{
			GlobalPosition = firstPosition,
		};

		var registry = new InMemoryProjectionRegistry();
		registry.Register(new ProjectionRegistration(
			typeof(RebuildPoisonProjection),
			ProjectionMode.Async,
			projection: new object(),
			inlineApply: static (_, _, _, _) => Task.CompletedTask));

		var globalStreamQuery = A.Fake<IGlobalStreamQuery>();
		var readCount = 0;
		var firstPageCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (Interlocked.Increment(ref readCount) == 1)
				{
					return new ValueTask<IReadOnlyList<StoredEvent>>(new[] { stored });
				}

				// With one registration and one background driver, another read follows completion
				// (or rejection) of the first page, including its awaited checkpoint operation.
				firstPageCompleted.TrySetResult();
				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		var eventSerializer = A.Fake<IEventSerializer>();
		A.CallTo(() => eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(A.Fake<IDomainEvent>());

		var checkpointStore = SubscriptionCheckpointTestSetup.CreateFake();
		A.CallTo(() => checkpointStore.GetCheckpointAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(null));
		A.CallTo(() => checkpointStore.AdvanceCheckpointAsync(
				A<string>._, A<string>._, A<Guid>._, A<long?>._, A<long>._, A<CancellationToken>._))
			.Returns(Task.FromResult(CheckpointAdvanceOutcome.Advanced));

		using var serviceProvider = new ServiceCollection().AddSingleton(globalStreamQuery).BuildServiceProvider();

		using var host = new AsyncProjectionProcessingHost(
			registry,
			eventSerializer,
			checkpointStore,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions
			{
				SubscriptionScope = "unit-test",
				IdlePollingInterval = TimeSpan.FromMilliseconds(10),
				CheckpointInterval = 1,
			}),
			serviceProvider,
			NullLogger<AsyncProjectionProcessingHost>.Instance);

		using var cts = new CancellationTokenSource();
		await host.StartAsync(cts.Token).ConfigureAwait(false);

		try
		{
			await firstPageCompleted.Task.WaitAsync(
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
		}
		finally
		{
			await cts.CancelAsync().ConfigureAwait(false);
			await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}

		return checkpointStore;
	}
}
