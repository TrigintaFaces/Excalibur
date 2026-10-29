// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// A rebuild resets the projection's cursor, so the documents and the cursor describe one point in
/// history.
/// </summary>
/// <remarks>
/// <para>
/// MEASURED DEFECT, not a hypothetical: the rebuild service referenced the checkpoint store NOWHERE. It
/// replayed the global stream and rewrote the read model while leaving the projection's cursor exactly
/// where it was, so the two halves of one projection described different points in history. The live
/// processor then resumed from a cursor predating the rebuild and re-applied events the rebuild had
/// already folded, against a destination whose positioned writes refuse precisely those positions.
/// </para>
/// <para>
/// Before the roster this was not fixable here: nothing could unbind a member, so a rebuild had no way
/// to reset its own cursor. Releasing the member removes the roster row and the checkpoint in one step.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class ProjectionRebuildReleasesMemberShould
{
	private sealed class Counter
	{
		public int Count { get; set; }
	}

	private const string Subscription = "rebuild-subject";

	private static SubscriptionMember Member(string key) => new(key, new string('A', 64), "dest-a") { DestinationScope = "estate/test-cluster" };

	private static ServiceProvider BuildProvider(ISubscriptionCheckpointStore checkpoints)
	{
        var services = new ServiceCollection();
        services.AddSingleton(A.Fake<Excalibur.EventSourcing.Queries.IGlobalStreamQuery>());
        services.AddSingleton(new MultiStreamProjection<Counter>());
        services.AddSingleton<IProjectionStore<Counter>>(new Excalibur.EventSourcing.Tests.Projections.TestProjectionStore<Counter>("dest-a"));
		_ = services.AddSingleton(checkpoints);
		_ = services.AddSingleton<IOptions<GlobalStreamProjectionOptions>>(
			Options.Create(new GlobalStreamProjectionOptions { ProjectionName = Subscription }));
		_ = services.AddSingleton<IOptions<ProjectionRebuildOptions>>(
			Options.Create(new ProjectionRebuildOptions { OfflineRebuildAcknowledged = true }));
		return services.BuildServiceProvider();
	}

	private static ProjectionRebuildService CreateService(ServiceProvider provider) =>
		new(provider,
			A.Fake<IEventSerializer>(),
			provider.GetRequiredService<IOptions<ProjectionRebuildOptions>>(),
			NullLogger<ProjectionRebuildService>.Instance);

    [Fact]
    public async Task RefuseInPlaceRebuildWithoutOfflineAcknowledgementBeforeReleasingAnything()
    {
        var checkpoints = new InMemorySubscriptionCheckpointStore();
        var key = typeof(Counter).FullName!;
        var member = (await checkpoints.BindSubscriptionAsync(Subscription, [Member(key)], CancellationToken.None)).Members.Single();
        await using var provider = BuildProvider(checkpoints);
        var service = new ProjectionRebuildService(provider, A.Fake<IEventSerializer>(),
            Options.Create(new ProjectionRebuildOptions()), NullLogger<ProjectionRebuildService>.Instance);
        await Should.ThrowAsync<InvalidOperationException>(() => service.RebuildAsync<Counter>(CancellationToken.None));
        (await checkpoints.ReadSubscriptionAsync(Subscription, CancellationToken.None)).Members.Single().BindingId.ShouldBe(member.BindingId);
    }

	[Fact]
	public async Task ReleaseTheProjectionsMemberAndCheckpointBeforeReplaying()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var key = typeof(Counter).FullName!;

		_ = await checkpoints.BindSubscriptionAsync(Subscription, [Member(key)], CancellationToken.None);
		_ = await checkpoints.AdvanceBoundCheckpointAsync(Subscription, key, null, 500, CancellationToken.None);

		await using var provider = BuildProvider(checkpoints);
		await CreateService(provider).RebuildAsync<Counter>(CancellationToken.None);

		// The cursor is gone, so the projection folds from the beginning rather than resuming at 500 and
		// re-applying events the rebuild already wrote.
		(await checkpoints.GetCheckpointAsync(Subscription, key, CancellationToken.None)).ShouldBeNull();

		// And the member is unbound, so it re-binds on the next cycle rather than colliding with its own
		// previous fingerprint.
		var roster = await checkpoints.BindSubscriptionAsync(
			Subscription, [new SubscriptionMember(key, new string('B', 64), "dest-b")], CancellationToken.None);
		roster.Members.ShouldHaveSingleItem().DefinitionHash[0].ShouldBe('B');
	}

	/// <summary>
	/// A sibling projection's cursor is untouched — the liveness half.
	/// </summary>
	/// <remarks>
	/// A release that cleared the whole subscription would satisfy the arm above completely while
	/// destroying every unrelated projection's progress. That is the cheapest way to pass the safety
	/// assertion and the most expensive way to be wrong.
	/// </remarks>
	[Fact]
	public async Task LeaveASiblingProjectionsCursorUntouched()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var key = typeof(Counter).FullName!;

		_ = await checkpoints.BindSubscriptionAsync(
			Subscription, [Member(key), Member("Other.Projection")], CancellationToken.None);
		_ = await checkpoints.AdvanceBoundCheckpointAsync(Subscription, "Other.Projection", null, 77, CancellationToken.None);

		await using var provider = BuildProvider(checkpoints);
		await CreateService(provider).RebuildAsync<Counter>(CancellationToken.None);

		(await checkpoints.GetCheckpointAsync(Subscription, "Other.Projection", CancellationToken.None)).ShouldBe(77);
	}

	/// <summary>
	/// A rebuild in a host with no checkpoint store registered still proceeds.
	/// </summary>
	/// <remarks>
	/// The release is best-effort by design. A consumer may rebuild in a host that registers no
	/// checkpoint store, or before the projection has ever bound; neither is an error, and neither should
	/// abort a rebuild that is otherwise able to run.
	/// </remarks>
	[Fact]
	public async Task ReportFailedWhenRebuildDependenciesAreMissing()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IOptions<ProjectionRebuildOptions>>(
			Options.Create(new ProjectionRebuildOptions { OfflineRebuildAcknowledged = true }));
		await using var provider = services.BuildServiceProvider();

		var service = CreateService(provider);
        await service.RebuildAsync<Counter>(CancellationToken.None);
        (await service.GetStatusAsync<Counter>(CancellationToken.None)).State.ShouldBe(ProjectionRebuildState.Failed);
	}
}
