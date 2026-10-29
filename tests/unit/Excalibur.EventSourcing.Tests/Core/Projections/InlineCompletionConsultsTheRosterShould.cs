// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// A save may report "nothing was owed" only over subscription members this process hosts.
/// </summary>
/// <remarks>
/// <para>
/// MEASURED DEFECT, not a hypothetical. Two returns in the foreground completion path answered
/// NotRequested without consulting the roster at all: one when no Inline projection was registered
/// locally, one when no locally-registered projection consumed the committed event types. Both decided
/// from the LOCAL registry, which is silent about a member bound by another deployment, so neither
/// could reach the quantifier the processing loop enforces.
/// </para>
/// <para>
/// The interleaving: a rolling deployment where the new host binds members {A, C} and hosts C, while the
/// old host hosts only {A} and commits an event A does not handle. The old host answers NotRequested,
/// whose contract tells the caller nothing is outstanding, and the caller reads C -- which is in the
/// roster, is behind, and is hosted only by the new process. A silent stale read, with the caller
/// explicitly told not to re-check.
/// </para>
/// <para>
/// Both arms below have a LIVENESS twin. Returning BoundExpired unconditionally satisfies every safety
/// assertion here while making read-your-own-write permanently unavailable -- safe, inert and useless.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class InlineCompletionConsultsTheRosterShould
{
	/// <summary>
	/// The subscription the host actually binds: "Inline:" prefixed to the configured projection name,
	/// whose default is the async host's own type name.
	/// </summary>
	/// <remarks>
	/// Load-bearing. A member planted under any other name binds a roster the host never reads, and
	/// every safety arm here would pass vacuously while measuring nothing.
	/// </remarks>
	private const string Subscription = "Inline:AsyncProjectionProcessingHost";

	/// <summary>A projection key bound to the subscription that this process does not host.</summary>
	private const string Unhosted = "Other.Deployment.Projection";

	/// <summary>Projection state for the one projection this process hosts.</summary>
	internal sealed class RosterCounter
	{
		public int Count { get; set; }
	}

	private static SubscriptionMember Member(string key) => new(key, new string('A', 64), string.Empty);

	private static EventNotificationContext Context() =>
		new("row", "Order", 1, DateTimeOffset.UtcNow, GlobalPosition: 1);

	/// <summary>Counts roster binds so an empty-roster liveness arm can prove the roster was READ.</summary>
	/// <remarks>
	/// With nothing hosted here an empty roster writes no member, so there is nothing in the store to
	/// observe afterwards. Without this counter the liveness arm would pass for an implementation that
	/// never consults the roster at all -- the same vacuity the subscription-name constant guards against.
	/// </remarks>
	private sealed class BindCountingCheckpointStore(ISubscriptionCheckpointStore inner) : ISubscriptionCheckpointStore
	{
		public int Binds { get; private set; }

		public Task<SubscriptionRoster> BindSubscriptionAsync(
			string subscriptionName, IReadOnlyList<SubscriptionMember> members, CancellationToken cancellationToken)
		{
			Binds++;
			return inner.BindSubscriptionAsync(subscriptionName, members, cancellationToken);
		}

		public Task<bool> ReleaseSubscriptionMemberAsync(
            string subscriptionName, string projectionKey, Guid bindingId, CancellationToken cancellationToken) =>
            inner.ReleaseSubscriptionMemberAsync(subscriptionName, projectionKey, bindingId, cancellationToken);

        public Task<SubscriptionRoster> ReadSubscriptionAsync(string subscriptionName, CancellationToken cancellationToken) =>
            inner.ReadSubscriptionAsync(subscriptionName, cancellationToken);

		public Task<long?> GetCheckpointAsync(
			string subscriptionName, string projectionKey, CancellationToken cancellationToken) =>
			inner.GetCheckpointAsync(subscriptionName, projectionKey, cancellationToken);

		public Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
			string subscriptionName, string projectionKey, Guid bindingId, long? expectedPosition, long newPosition,
			CancellationToken cancellationToken) =>
			inner.AdvanceCheckpointAsync(
				subscriptionName, projectionKey, bindingId, expectedPosition, newPosition, cancellationToken);

		public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken) =>
			inner.EnumerateCheckpointsAsync(cancellationToken);
	}

	private static ServiceProvider BuildProvider(
		ISubscriptionCheckpointStore checkpoints,
		bool withLocalInlineProjection)
	{
		var query = A.Fake<IGlobalStreamQuery>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(query);
		_ = services.AddSingleton(A.Fake<IEventSerializer>());

		// The pull entry point resolves the broker FIRST, to materialize the deferred closed-generic
		// projection definitions before any registry read. This fixture registers its projections
		// directly, so a stand-in satisfies that step without changing what the registry holds.
		_ = services.AddSingleton(A.Fake<IEventNotificationBroker>());
		_ = services.AddSingleton(new InlineProjectionSource("test-source", checkpoints, query));
		_ = services.Configure<GlobalStreamProjectionOptions>(o => o.SubscriptionScope = "test");

		var registry = new InMemoryProjectionRegistry();
		if (withLocalInlineProjection)
		{
			var builder = new ProjectionBuilder<RosterCounter>(registry);
			_ = builder.Inline().When<TestOrderPlaced>((state, _) => state.Count++);
			builder.Build();
		}

		_ = services.AddSingleton<IProjectionRegistry>(registry);
		return services.BuildServiceProvider();
	}

	private static InlineProjectionCoordinator Coordinator(ServiceProvider provider) =>
		new(provider,
			Options.Create(new EventNotificationOptions()),
			NullLogger<InlineProjectionCoordinator>.Instance);

	/// <summary>SAFETY, the no-local-Inline-projection return.</summary>
	[Fact]
	public async Task RefuseNothingOwedWithNoLocalProjectionWhileTheRosterHasAnUnhostedMember()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		_ = await checkpoints.BindSubscriptionAsync(Subscription, [Member(Unhosted)], CancellationToken.None);

		await using var provider = BuildProvider(checkpoints, withLocalInlineProjection: false);
		await using var coordinator = Coordinator(provider);

		(await coordinator.CompleteAsync(Context(), [new TestOrderPlaced()]))
			.Freshness.ShouldBe(ProjectionFreshness.BoundExpired,
				"a process hosting no Inline projection cannot attest a roster that has members, and "
				+ "whether an absent member consumes these events is not recorded anywhere");
	}

	/// <summary>
	/// LIVENESS, the no-local-Inline-projection return.
	/// </summary>
	/// <remarks>
	/// With nothing hosted here, "empty" and "fully hosted" are the same roster, so this is the whole
	/// liveness half of that path. Without it, answering BoundExpired unconditionally would satisfy the
	/// safety arm above.
	/// </remarks>
	[Fact]
	public async Task StillReportNothingOwedWithNoLocalProjectionWhenTheRosterIsEmpty()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();

		await using var provider = BuildProvider(checkpoints, withLocalInlineProjection: false);
		await using var coordinator = Coordinator(provider);

		(await coordinator.CompleteAsync(Context(), [new TestOrderPlaced()]))
			.Freshness.ShouldBe(ProjectionFreshness.NotRequested,
				"an empty roster leaves nothing unattested, which is the narrow case NotRequested means");
	}

	/// <summary>SAFETY, the no-projection-consumes-these-events return.</summary>
	[Fact]
	public async Task RefuseNothingOwedForAnUnconsumedEventWhileTheRosterHasAnUnhostedMember()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		_ = await checkpoints.BindSubscriptionAsync(Subscription, [Member(Unhosted)], CancellationToken.None);

		await using var provider = BuildProvider(checkpoints, withLocalInlineProjection: true);
		await using var coordinator = Coordinator(provider);

		// The hosted projection consumes TestOrderPlaced, so a committed TestOrderShipped leaves the
		// required set empty -- the second return, reached with the host already in hand.
		(await coordinator.CompleteAsync(Context(), [new TestOrderShipped()]))
			.Freshness.ShouldBe(ProjectionFreshness.BoundExpired,
				"an empty required set says only that no projection THIS process hosts consumes these "
				+ "events; it says nothing about a member bound elsewhere");
	}

	/// <summary>
	/// LIVENESS, the no-projection-consumes-these-events return, over a NON-EMPTY roster.
	/// </summary>
	/// <remarks>
	/// The roster here holds exactly the member this process hosts, which is the case the safety arm
	/// above must not catch: a non-empty roster that is fully hosted leaves nothing unattested.
	/// </remarks>
	[Fact]
	public async Task StillReportNothingOwedForAnUnconsumedEventWhenTheRosterIsFullyHostedHere()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();

		await using var provider = BuildProvider(checkpoints, withLocalInlineProjection: true);
		await using var coordinator = Coordinator(provider);

		(await coordinator.CompleteAsync(Context(), [new TestOrderShipped()]))
			.Freshness.ShouldBe(ProjectionFreshness.NotRequested,
				"this process hosts every member bound to the subscription, so nothing is unattested");

		// And the roster really was non-empty, so the arm above is not the empty-roster case again.
		var roster = await checkpoints.BindSubscriptionAsync(Subscription, [], CancellationToken.None);
		roster.Members.ShouldHaveSingleItem().ProjectionKey.ShouldBe(typeof(RosterCounter).FullName);
	}

	/// <summary>SAFETY, the pull entry point's no-local-Inline-projection return.</summary>
	/// <remarks>
	/// The third site of the same quantifier bypass. This one reported CaughtUp -- a vacuous success in
	/// the one method a host with no background loop relies on to advance the Inline cursor at all.
	/// </remarks>
	[Fact]
	public async Task RefuseCaughtUpWithNoLocalProjectionWhileTheRosterHasAnUnhostedMember()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		_ = await checkpoints.BindSubscriptionAsync(Subscription, [Member(Unhosted)], CancellationToken.None);

		await using var provider = BuildProvider(checkpoints, withLocalInlineProjection: false);
		await using var coordinator = Coordinator(provider);

		var result = await coordinator.ProcessPendingAsync(CancellationToken.None);

		result.Outcome.ShouldBe(ProjectionProcessingOutcome.Halted,
			"a process hosting no Inline projection cannot report the cursor caught up while the "
			+ "subscription binds a member it does not host");
		result.HaltReason.ShouldBe(ProjectionHaltReason.SubscriptionMembersNotHosted,
			"the halt is terminal and names its cause: the member advances only where it is hosted, so "
			+ "no existing reason describes it and PrefixUnavailable would invite a pointless retry");
	}

	/// <summary>
	/// LIVENESS, the pull entry point's no-local-Inline-projection return.
	/// </summary>
	/// <remarks>
	/// Without this, halting unconditionally satisfies the safety arm above while making the pull entry
	/// point permanently useless -- safe, inert, and the most expensive way to be wrong. The bind count
	/// is the second half: an implementation that never reads the roster also reports caught-up here.
	/// </remarks>
	[Fact]
	public async Task StillReportCaughtUpWithNoLocalProjectionWhenTheRosterIsEmpty()
	{
		var checkpoints = new BindCountingCheckpointStore(new InMemorySubscriptionCheckpointStore());

		await using var provider = BuildProvider(checkpoints, withLocalInlineProjection: false);
		await using var coordinator = Coordinator(provider);

		var result = await coordinator.ProcessPendingAsync(CancellationToken.None);

		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp,
			"an empty roster leaves nothing unattested, so there is nothing to halt for");
		result.HaltReason.ShouldBe(ProjectionHaltReason.None);
		checkpoints.Binds.ShouldBeGreaterThan(0,
			"the verdict must come from the durable roster, not from the local registry: an empty roster "
			+ "writes no member, so the bind count is the only evidence the roster was consulted");
	}

	/// <summary>
	/// A composition with no checkpoint store reports the read model may be behind, and never throws.
	/// </summary>
	/// <remarks>
	/// The events are already durable at this point, so an exception would tell the caller a committed
	/// save failed when it did not. The roster could not be read, so freshness cannot be established,
	/// which is what BoundExpired says.
	/// </remarks>
	[Fact]
	public async Task ReportBehindRatherThanThrowWhenNoCheckpointStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IGlobalStreamQuery>());
		_ = services.AddSingleton(A.Fake<IEventSerializer>());
		_ = services.AddSingleton<IProjectionRegistry>(new InMemoryProjectionRegistry());
		await using var provider = services.BuildServiceProvider();
		await using var coordinator = Coordinator(provider);

		(await coordinator.CompleteAsync(Context(), [new TestOrderPlaced()]))
			.Freshness.ShouldBe(ProjectionFreshness.BoundExpired,
				"an unreadable roster is reported, not thrown: the events are committed either way");
	}
}
