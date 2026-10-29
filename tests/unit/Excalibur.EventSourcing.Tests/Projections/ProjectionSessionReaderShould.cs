// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// The read half of session consistency: a read carrying a token must observe the write that produced
/// it, or say plainly that it could not confirm so.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionSessionReaderShould
{
    [Fact]
    public async Task RefuseSubscriptionProgressAsProofOfThisProjectionsFreshness()
    {
        var (reader, checkpoints, health, clock) = Build(TimeSpan.FromMilliseconds(100));
        health.RecordApplied(Subscription, 150);
        checkpoints.Checkpoint = 0;
        var pending = reader.ReadAsync<Row>("stale", new SaveResult(1, 100, ProjectionFreshness.NotRequested), CancellationToken.None).AsTask();
        clock.Advance(TimeSpan.FromSeconds(1));
        (await pending).Freshness.ShouldBe(ProjectionFreshness.BoundExpired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Pattern", "Regression")]
    public async Task RefuseFreshnessWhenTheRosterChangesDuringTheDocumentRead(bool rebind)
    {
        var (reader, checkpoints, _, _) = Build(beforeRead: state =>
        {
            if (rebind) { state.BindingId = Guid.NewGuid(); }
            else { state.IncludeSibling = true; }
        });
        checkpoints.Checkpoint = 100;
        var result = await reader.ReadAsync<Row>("row", new SaveResult(1, 100, ProjectionFreshness.NotRequested), CancellationToken.None);
        result.Freshness.ShouldBe(ProjectionFreshness.BoundExpired);
        checkpoints.Reads.ShouldBe(2);
    }

	private const string Subscription = "projections";

	// Real time, not the fake clock: this exists so a wait that never terminates FAILS rather than hangs
	// the suite. Generous, because it is a safety net and not the property under test.
	private static CancellationTokenSource Safety() => new(TimeSpan.FromSeconds(30));

	private sealed class Row
	{
		public string Id { get; set; } = string.Empty;
	}

	private sealed class StubCheckpoints : ISubscriptionCheckpointStore
	{
		private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Subscription, string ProjectionKey), SubscriptionMember> _members = new();

		// Mirrors InMemorySubscriptionCheckpointStore rather than stubbing the contract away: GetOrAdd is
		// the atomic claim, the read-back returns the WHOLE roster for the subscription, and a divergence
		// refuses per member. A double that merely returned an empty roster would make every host under
		// test believe it hosts everything, which is the one belief the roster exists to deny.
		public Task<SubscriptionRoster> BindSubscriptionAsync(
			string subscriptionName,
			IReadOnlyList<SubscriptionMember> members,
			CancellationToken cancellationToken)
		{
			SubscriptionDefinitionValidation.Validate(subscriptionName, members);
			cancellationToken.ThrowIfCancellationRequested();
			foreach (var member in members)
			{
				var stored = _members.GetOrAdd((subscriptionName, member.ProjectionKey), member);
				SubscriptionDefinitionValidation.RequireMemberMatch(subscriptionName, member, stored);
			}

			IReadOnlyList<SubscriptionMember> roster =
			[
				.. _members
					.Where(entry => string.Equals(entry.Key.Subscription, subscriptionName, StringComparison.Ordinal))
					.Select(static entry => entry.Value)
			];
			return Task.FromResult(new SubscriptionRoster(subscriptionName, roster));
		}

		public Task<bool> ReleaseSubscriptionMemberAsync(
			string subscriptionName,
			string projectionKey,
            Guid bindingId,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(_members.TryRemove((subscriptionName, projectionKey), out _));
		}

        public Guid BindingId { get; set; } = Guid.NewGuid();
        public bool IncludeSibling { get; set; }
        public Task<SubscriptionRoster> ReadSubscriptionAsync(string subscriptionName, CancellationToken cancellationToken)
        {
            Reads++;
            var member = new SubscriptionMember(SubscriptionKeys.ProjectionKey(typeof(Row)),
                SubscriptionKeys.DefinitionFingerprint(string.Empty, "1", typeof(Row).Assembly.GetName().Name!, SubscriptionKeys.ProjectionKey(typeof(Row)), "19:estate/test-cluster9:test:rows"), "test:rows")
                    { BindingId = BindingId, Position = Checkpoint, DestinationScope = "estate/test-cluster" };
            return Task.FromResult(new SubscriptionRoster(subscriptionName, IncludeSibling
                ? [member, new SubscriptionMember("Sibling", new string('B', 64), "test:sibling") { BindingId = Guid.Parse("d77d2516-07b6-4612-a1ab-99814c98d75d") }]
                : [member]));
        }

		public long? Checkpoint { get; set; }

		public int Reads { get; private set; }

		public Task<long?> GetCheckpointAsync(
			string subscriptionName, string projectionKey, CancellationToken cancellationToken)
		{
			Reads++;
			return Task.FromResult(Checkpoint);
		}

		public Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
			string subscriptionName, string projectionKey, Guid bindingId, long? expected, long position,
			CancellationToken cancellationToken) =>
			Task.FromResult(CheckpointAdvanceOutcome.Advanced);

		public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<SubscriptionCheckpoint>>([]);
	}

	private static (ProjectionSessionReader Reader, StubCheckpoints Checkpoints, ProjectionHealthState Health, FakeTimeProvider Clock)
		Build(TimeSpan? bound = null, Action<StubCheckpoints>? beforeRead = null)
	{
		var services = new ServiceCollection();
		var checkpoints = new StubCheckpoints();
        var store = new TestProjectionStore<Row>("test:rows") { BeforeRead = () => beforeRead?.Invoke(checkpoints) };
        ProjectionStoreOperations<Row>.Legacy(store).DestinationScope.ShouldBe("estate/test-cluster");
        AsyncProjectionProcessingHost.DestinationDescriptor(ProjectionMode.Async, "test:rows", "estate/test-cluster")
            .ShouldBe("19:estate/test-cluster9:test:rows");
		_ = services.AddSingleton<IProjectionStore<Row>>(store);
		var sp = services.BuildServiceProvider();

		var health = new ProjectionHealthState();
		var clock = new FakeTimeProvider();

		var reader = new ProjectionSessionReader(
			sp.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(new ProjectionSessionOptions
			{
				Bound = bound ?? TimeSpan.FromSeconds(5),
				PollInterval = TimeSpan.FromMilliseconds(25),
			}),
			Options.Create(new GlobalStreamProjectionOptions { ProjectionName = Subscription }),
			checkpoints,
			health,
			clock);

		return (reader, checkpoints, health, clock);
	}

	[Fact]
	public async Task NotWaitAtAll_WhenNoTokenIsSupplied()
	{
		// The whole point of putting the wait on the READ: a caller who does not ask for consistency
		// pays nothing, and must not touch the checkpoint store to find that out.
		var (reader, checkpoints, _, _) = Build();
		using var safety = Safety();

		var read = await reader.ReadAsync<Row>("row-1", new SaveResult(1, SessionToken: null, ProjectionFreshness.NotRequested), safety.Token);

		read.Freshness.ShouldBe(ProjectionFreshness.NotRequested);
		checkpoints.Reads.ShouldBe(0, "a read requesting no consistency must not consult progress at all");
	}

	[Fact]
    public async Task RefuseAnUnfencedInProcessMark()
    {
        var (reader, checkpoints, health, clock) = Build(TimeSpan.FromMilliseconds(100));
        using var safety = Safety();
        health.RecordApplied(AsyncProjectionProcessingHost.CheckpointNameFor(Subscription, typeof(Row)), 150);
        var reading = reader.ReadAsync<Row>("row-1", new SaveResult(1, 100, ProjectionFreshness.NotRequested), safety.Token).AsTask();
        clock.Advance(TimeSpan.FromSeconds(1));
        (await reading).Freshness.ShouldBe(ProjectionFreshness.BoundExpired);
        checkpoints.Reads.ShouldBeGreaterThan(0);
    }

	[Fact]
	public async Task ReportFresh_FromTheDurableCheckpoint_WhenAnotherProcessApplied()
	{
		// The other half of max(): work done by a DIFFERENT process is invisible to the in-process mark
		// and visible only in the shared checkpoint.
		var (reader, checkpoints, _, _) = Build();
		using var safety = Safety();
		checkpoints.Checkpoint = 100;

		var read = await reader.ReadAsync<Row>("row-1", new SaveResult(1, 100, ProjectionFreshness.NotRequested), safety.Token);

		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public async Task ReportBoundExpired_AndStillReturnTheRow_WhenTheProjectionNeverCatchesUp()
	{
		// BoundExpired is a warning, not a failure: the read still answers, because the row may well
		// exist with older content and the caller -- not the framework -- decides if that is acceptable.
		var (reader, _, _, clock) = Build(TimeSpan.FromMilliseconds(100));
		using var safety = Safety();

		var reading = reader.ReadAsync<Row>("row-1", new SaveResult(1, 100, ProjectionFreshness.NotRequested), safety.Token).AsTask();
		clock.Advance(TimeSpan.FromSeconds(1));

		var read = await reading;

		read.Freshness.ShouldBe(ProjectionFreshness.BoundExpired);
	}

	[Fact]
	public async Task ReportFresh_WhenTheProjectionCatchesUpWhileWaiting()
	{
		// LIVENESS. Without this arm an implementation that always reported BoundExpired would satisfy
		// every assertion above, and a wait that never succeeds is indistinguishable from one that works.
		var (reader, checkpoints, _, clock) = Build(TimeSpan.FromSeconds(30));
		using var safety = Safety();

		var reading = reader.ReadAsync<Row>("row-1", new SaveResult(1, 100, ProjectionFreshness.NotRequested), safety.Token).AsTask();

		checkpoints.Checkpoint = 100;
		clock.Advance(TimeSpan.FromMilliseconds(50));

		var read = await reading;

		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	[Fact]
	public void ResolveFromTheContainerThatRegistersIt()
	{
		// THE ARM THAT WAS MISSING. UseEventNotification registers IProjectionSessionReader, but the
		// checkpoint store is registered by EnableProjectionProcessing -- a different call. With the
		// store as a required dependency, resolving the reader from the container that registers it
		// THREW: the seam serving read-your-writes, the documented replacement for inline projections,
		// could not be constructed without a second, unstated call.
		//
		// Nothing asserted resolvability, so nothing caught it. This does.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IProjectionStore<Row>>(new TestProjectionStore<Row>("test:rows"));
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseEventNotification()));

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IProjectionSessionReader>());
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task ExpireAtTheBoundWhenTheCheckpointReadItselfIsSlow()
	{
		// THE AUDIT'S CASE. The elapsed check ran only BETWEEN attempts, so the configured bound could
		// not interrupt a single slow progress read: a five-second bound against a sixty-second
		// checkpoint query waited sixty, and against a query that never returned it waited forever.
		//
		// This stub never completes, which is the unresponsive-store case. The reader must still return
		// BoundExpired -- "we do not know" -- rather than hang.
		using var safety = Safety();
		var services = new ServiceCollection();
		_ = services.AddSingleton<IProjectionStore<Row>>(new TestProjectionStore<Row>("test:rows"));
		var sp = services.BuildServiceProvider();

		var reader = new ProjectionSessionReader(
			sp.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(new ProjectionSessionOptions
			{
				Bound = TimeSpan.FromMilliseconds(200),
				PollInterval = TimeSpan.FromMilliseconds(25),
			}),
			Options.Create(new GlobalStreamProjectionOptions { ProjectionName = Subscription }),
			new NeverCompletingCheckpoints(),
			new ProjectionHealthState(),

			// REAL time, deliberately. The bound has to be enforced against a read that never returns,
			// and a fake clock cannot advance while the test is blocked inside that read.
			TimeProvider.System);

		var result = await reader.ReadAsync<Row>("row-1", new SaveResult(1, 42, ProjectionFreshness.NotRequested), safety.Token)
			.ConfigureAwait(false);

		result.Freshness.ShouldBe(
			ProjectionFreshness.BoundExpired,
			customMessage: "a bound that a single slow read can overrun is not a bound");
		safety.IsCancellationRequested.ShouldBeFalse(
			"the reader must give up at its own bound rather than waiting out the store");
	}

	// Models an unresponsive checkpoint store: the read is accepted and never answers.
	private sealed class NeverCompletingCheckpoints : ISubscriptionCheckpointStore
	{
		private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Subscription, string ProjectionKey), SubscriptionMember> _members = new();

		// Mirrors InMemorySubscriptionCheckpointStore rather than stubbing the contract away: GetOrAdd is
		// the atomic claim, the read-back returns the WHOLE roster for the subscription, and a divergence
		// refuses per member. A double that merely returned an empty roster would make every host under
		// test believe it hosts everything, which is the one belief the roster exists to deny.
		public Task<SubscriptionRoster> BindSubscriptionAsync(
			string subscriptionName,
			IReadOnlyList<SubscriptionMember> members,
			CancellationToken cancellationToken)
		{
			SubscriptionDefinitionValidation.Validate(subscriptionName, members);
			cancellationToken.ThrowIfCancellationRequested();
			foreach (var member in members)
			{
				var stored = _members.GetOrAdd((subscriptionName, member.ProjectionKey), member);
				SubscriptionDefinitionValidation.RequireMemberMatch(subscriptionName, member, stored);
			}

			IReadOnlyList<SubscriptionMember> roster =
			[
				.. _members
					.Where(entry => string.Equals(entry.Key.Subscription, subscriptionName, StringComparison.Ordinal))
					.Select(static entry => entry.Value)
			];
			return Task.FromResult(new SubscriptionRoster(subscriptionName, roster));
		}

		public Task<bool> ReleaseSubscriptionMemberAsync(
			string subscriptionName,
			string projectionKey,
            Guid bindingId,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(_members.TryRemove((subscriptionName, projectionKey), out _));
		}

        public async Task<SubscriptionRoster> ReadSubscriptionAsync(string subscriptionName, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new SubscriptionRoster(subscriptionName, []);
        }

		public Task<long?> GetCheckpointAsync(
			string subscriptionName, string projectionKey, CancellationToken cancellationToken) =>
			Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith(
				static _ => (long?)null, TaskScheduler.Default);

		public Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
			string subscriptionName, string projectionKey, Guid bindingId, long? expected, long position,
			CancellationToken cancellationToken) =>
			Task.FromResult(CheckpointAdvanceOutcome.Advanced);

		public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<SubscriptionCheckpoint>>([]);
	}
}
