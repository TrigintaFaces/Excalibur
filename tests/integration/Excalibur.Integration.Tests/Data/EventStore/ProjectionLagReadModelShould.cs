// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infra regression lock for the projection/CDC-lag read-model (W1-9 <c>hn46e1</c>). Author≠impl
/// (TestsDeveloper): binds the <em>emitted</em> <see cref="ProjectionLag"/> — per-stream
/// <c>lag = max(0, head − checkpoint)</c> — through the <strong>real SQL Server event store</strong>
/// global-stream head (<c>IGlobalStreamQuery.GetHeadPositionAsync</c> = <c>MAX(Position)</c>), not by
/// re-testing the event-store engine.
/// </summary>
/// <remarks>
/// <para>
/// The read-model + head accessor are resolved through the real event-sourcing DI graph
/// (<c>UseSqlServer(...).EnableProjectionProcessing()</c>), so the lock exercises the actual wiring, not a
/// hand-constructed impl (the concrete <c>ProjectionLagReadModel</c> and <c>SqlServerGlobalStreamQuery</c>
/// are <c>internal</c>). Head is advanced by appending real events; the checkpoint is seeded through the
/// resolved <see cref="ISubscriptionCheckpointStore"/>, so the read-model reads the same instance.
/// </para>
/// <para>
/// Never skipped: a missing Docker daemon fails the lock (<c>DockerAvailable.ShouldBeTrue</c>). RED on the
/// pre-fix single-named checkpoint surface (no <c>EnumerateCheckpointsAsync</c>, no head accessor, no
/// <c>IProjectionLagReadModel</c> registration): the read-model does not resolve and the lag cannot be
/// computed. The clamp case proves the structural <c>Math.Max(0, …)</c> safe-op (lag can never go negative).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "SqlServer")]
public sealed class ProjectionLagReadModelShould : IClassFixture<SqlServerEventStoreContainerFixture>
{
	private const string AggregateType = "ProjectionLagAggregate";
	private readonly SqlServerEventStoreContainerFixture _fixture;

	public ProjectionLagReadModelShould(SqlServerEventStoreContainerFixture fixture) => _fixture = fixture;

	private async Task<SqlServerEventStore> InitStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra projection-lag lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		// TRUNCATE resets IDENTITY, so MAX(Position) (the global-stream head) restarts at 0 per test.
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		return new SqlServerEventStore(_fixture.ConnectionString, NullLogger<SqlServerEventStore>.Instance, SingleTenantTestContext.Instance);
	}

	// Resolves an IProjectionLagReadModel whose head comes from the real SQL Server global stream
	// (SqlServerGlobalStreamQuery over dbo.EventStoreEvents — the fixture's default schema/table).
	private ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddOptions();

		new ExcaliburEventSourcingBuilder(services)
			.UseSqlServer(sql => sql.ConnectionString(_fixture.ConnectionString))
			.EnableProjectionProcessing(opts => opts.SubscriptionScope = "projection-lag-read-model");

		return services.BuildServiceProvider();
	}

	// The name the host checkpoints under, read from the options rather than hard-coded, so renaming the
	// subscription moves these arms with it instead of stranding a string literal.
	private static string ExpectedSubscription(ServiceProvider provider) =>
		provider.GetRequiredService<IOptions<GlobalStreamProjectionOptions>>().Value.ProjectionName;

	// EVERY ARM BELOW GETS THIS ROW, and that is the contract rather than contamination: the read model
	// reports a CONFIGURED subscription that has never checkpointed, with a null position and a lag of the
	// whole stream, because an absent row is what a dashboard renders as healthy. BuildProvider calls
	// EnableProjectionProcessing, so the subscription is always configured here. It is added BEFORE the
	// recorded ones, so an index-based assertion reads the wrong entry -- look entries up BY NAME.
	private static ProjectionLag NeverStarted(ProjectionLagReport report, ServiceProvider provider)
	{
		var expected = ExpectedSubscription(provider);

		// ProjectionLag is a value type, so an absent entry comes back as a default-valued struct rather
		// than null -- asserting containment first is what makes the absence report itself instead of
		// surfacing later as a zero lag that looks like a caught-up subscription.
		report.Streams.ShouldContain(
			s => s.SubscriptionName == expected,
			customMessage:
				"the configured subscription has never checkpointed, so the report must carry it as "
				+ "maximally behind; omitting it is the absence a health check reads as healthy");

		return report.Streams.Single(s => s.SubscriptionName == expected);
	}

	private async Task AppendEventsAsync(SqlServerEventStore store, int count)
	{
		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var batch = Enumerable.Range(0, count).Select(_ => new TestDomainEvent
		{
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Data = $"data-{Guid.NewGuid():N}",
		}).ToList();

		var result = await store.AppendAsync(aggregateId, AggregateType, batch, expectedVersion: -1, CancellationToken.None)
			.ConfigureAwait(false);
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task EmitPerStreamLagAsHeadMinusCheckpoint()
	{
		var store = await InitStoreAsync().ConfigureAwait(false);
		await using var provider = BuildProvider();

		// head = 5 (five events appended → MAX(Position) = 5); checkpoint behind at 2 → lag = 3.
		await AppendEventsAsync(store, 5).ConfigureAwait(false);
		var checkpoints = provider.GetRequiredService<ISubscriptionCheckpointStore>();
		var member = (await checkpoints.BindSubscriptionAsync("subscription-1",
            [new SubscriptionMember("Proj.A", new string('A', 64), "test/a")], CancellationToken.None)).Members.Single();
        await checkpoints.AdvanceCheckpointAsync("subscription-1", "Proj.A", member.BindingId, null, 2, CancellationToken.None)
			.ConfigureAwait(false);

		var readModel = provider.GetRequiredService<IProjectionLagReadModel>();
		var report = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		report.Availability.ShouldBe(ProjectionLagAvailability.Measured);

		// TWO entries, not one: the recorded subscription AND the configured-but-never-started host. This
		// arm asserted one and read it by index, which encoded the contract as it stood before the read
		// model learned to report a subscription that has never run.
		report.Streams.Count.ShouldBe(2);

		// A cursor is stored per (subscription, projection), and the report renders the pair back into one
		// identity so two projections under one subscription stay distinguishable to a dashboard.
		var entry = report.Streams.Single(
			s => s.SubscriptionName == SubscriptionKeys.CheckpointName("subscription-1", "Proj.A"));
		entry.HeadPosition.ShouldBe(5);
		entry.CheckpointPosition.ShouldBe(2);
		entry.Lag.ShouldBe(3);

		var neverStarted = NeverStarted(report, provider);
		neverStarted.CheckpointPosition.ShouldBeNull(
			"null rather than zero, because zero is a legitimate recorded position and would make a "
			+ "subscription that has never run indistinguishable from one that applied the first event");
		neverStarted.HeadPosition.ShouldBe(5);
		neverStarted.Lag.ShouldBe(5, "it has applied nothing, so it trails the entire stream");
	}

	[Fact]
	public async Task ClampLagToZeroWhenCheckpointIsAheadOfHead()
	{
		var store = await InitStoreAsync().ConfigureAwait(false);
		await using var provider = BuildProvider();

		// head = 3, checkpoint seeded past the head at 10 → structural Math.Max(0, head − cp) clamps lag to 0.
		await AppendEventsAsync(store, 3).ConfigureAwait(false);
		var checkpoints = provider.GetRequiredService<ISubscriptionCheckpointStore>();
		var member = (await checkpoints.BindSubscriptionAsync("ahead-of-head",
            [new SubscriptionMember("Proj.A", new string('A', 64), "test/a")], CancellationToken.None)).Members.Single();
        await checkpoints.AdvanceCheckpointAsync("ahead-of-head", "Proj.A", member.BindingId, null, 10, CancellationToken.None)
			.ConfigureAwait(false);

		var readModel = provider.GetRequiredService<IProjectionLagReadModel>();
		var report = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		// CORRECTED. This arm previously asserted Measured here, which encoded the defect as the
		// requirement: a checkpoint above the head is IMPOSSIBLE while the head source and the event store
		// describe the same stream, so clamping to zero and reporting a clean measurement discards the only
		// observation that proves the wiring is wrong. The clamp itself is right and is still asserted
		// below; what was wrong was reporting the result as if nothing were behind.
		report.Availability.ShouldBe(
			ProjectionLagAvailability.CheckpointAheadOfHead,
			"a checkpoint above the head is a configuration fault and must not be reported as a clean "
			+ "measurement that found nothing behind");
		report.Streams.Count.ShouldBe(2, "the contradicted subscription AND the never-started host");

		// BY NAME, NEVER BY INDEX. The never-started entry is prepended, so Streams[0] is not the
		// subscription this arm seeded -- the old index assertions held only while that row did not exist.
		var contradicted = report.Streams.Single(
			s => s.SubscriptionName == SubscriptionKeys.CheckpointName("ahead-of-head", "Proj.A"));
		contradicted.HeadPosition.ShouldBe(3);
		contradicted.CheckpointPosition.ShouldBe(10);
		contradicted.Lag.ShouldBe(0, "lag is a structural safe-op and can never be negative");

		// The never-started host is still reported alongside the fault, so one bad subscription does not
		// hide the others from an operator reading the report.
		NeverStarted(report, provider).Lag.ShouldBe(3);
	}

	[Fact]
	public async Task ReportTheNeverStartedSubscriptionWhenNothingHasCheckpointed()
	{
		var store = await InitStoreAsync().ConfigureAwait(false);
		await using var provider = BuildProvider();

		// A head exists but no subscription has checkpointed → nothing to report (empty, not a 500 / not a throw).
		await AppendEventsAsync(store, 4).ConfigureAwait(false);

		var readModel = provider.GetRequiredService<IProjectionLagReadModel>();
		var report = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		// STRENGTHENED: empty alone no longer distinguishes "the head was read and nothing is behind"
		// from "there was no head to read". This scenario is the former, so it must report Measured --
		// asserting only emptiness would pass for the unmeasurable case too, which is the defect this
		// contract change exists to remove.
		report.Availability.ShouldBe(
			ProjectionLagAvailability.Measured,
			"a head exists, so this result is a measurement rather than an absence of one");

		// RENAMED AND CORRECTED. This arm was ReturnEmptyWhenNoCheckpointsAreRecorded and asserted an empty
		// list. That was the contract before the read model learned to report a configured subscription that
		// has never run -- and an empty list is precisely what this provider must NOT return, because a
		// projection with the whole stream still to process would then look healthy. The good-news empty
		// still exists and is asserted by the arm below, which configures no subscription at all.
		report.Streams.Count.ShouldBe(1);
		var entry = NeverStarted(report, provider);
		entry.CheckpointPosition.ShouldBeNull();
		entry.HeadPosition.ShouldBe(4);
		entry.Lag.ShouldBe(4, "nothing has been applied, so the lag is the whole stream");
	}

	/// <summary>
	/// The good-news empty, which is the PAIR of the arm above and the only one that still proves the
	/// report can be empty at all.
	/// </summary>
	/// <remarks>
	/// Without this, "nothing is behind" would be unrepresentable and the arm above could be satisfied by a
	/// read model that always invents a row. It builds the read model with no expected subscription -- the
	/// shape of a process that stores events and leaves projections to another host -- against the same real
	/// SQL Server head and the same real checkpoint store.
	/// </remarks>
	[Fact]
	public async Task ReturnEmptyWhenNothingIsCheckpointedAndNoSubscriptionIsExpected()
	{
		var store = await InitStoreAsync().ConfigureAwait(false);
		await using var provider = BuildProvider();

		await AppendEventsAsync(store, 4).ConfigureAwait(false);

		var readModel = new ProjectionLagReadModel(
			provider.GetRequiredService<ISubscriptionCheckpointStore>(),
			provider.GetRequiredService<IGlobalStreamQuery>(),
			expectedSubscription: null);

		var report = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		report.Availability.ShouldBe(
			ProjectionLagAvailability.Measured,
			"the head WAS read, so this is a measurement that found nothing behind");
		report.Streams.ShouldBeEmpty(
			"no subscription recorded progress and none was expected, so there is genuinely nothing to "
			+ "report -- this is the empty the arm above must no longer return");
	}

	/// <summary>
	/// The arm that was MISSING when the availability distinction shipped, and the only one that can tell
	/// the fixed read model from the broken one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every other arm in this file asserts <c>Measured</c>. That is correct for each of them and jointly
	/// insufficient: the defect being guarded against is the read model reporting <c>Measured</c> with an
	/// empty stream list when it could not read a head at all, and <b>not one of those arms fails if the
	/// code regresses to exactly that.</b> The sibling above is the closest, and it is the trap -- it
	/// legitimately expects <c>Measured</c> plus empty, because there a head really was read. Emptiness is
	/// shared by both states, so only the availability value separates them, and only in THIS scenario.
	/// </para>
	/// <para>
	/// The named red input, so this is not a check that cannot fail: return
	/// <c>new ProjectionLagReport(ProjectionLagAvailability.Measured, [])</c> from the
	/// <c>_globalStream is null</c> branch of <c>ProjectionLagReadModel.GetLagAsync</c> -- the pre-fix
	/// behaviour -- and this arm reddens while the other three stay green.
	/// </para>
	/// <para>
	/// No container, and deliberately so: the condition is a DI-graph property, not an engine property.
	/// Projection processing is enabled without a provider, so <c>IGlobalStreamQuery</c> has no
	/// registration and the read model is constructed with the null its own registration anticipates via
	/// <c>GetService</c>. Substituting a fake head source that returns zero would test a different thing --
	/// a head of zero IS a measurement -- which is how this case gets missed.
	/// </para>
	/// <para>
	/// Why it matters beyond the enum: a dashboard renders "no streams are behind" as all-clear. Before the
	/// distinction existed, an event-sourcing application with no global-stream provider configured served
	/// a healthy-looking lag panel forever, and the one state a lag panel must never silently claim is that
	/// it is watching something it cannot see.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReportNoHeadSourceRatherThanAMeasuredEmptyWhenNoGlobalStreamIsRegistered()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddOptions();

		// Projection processing WITHOUT a provider: no UseSqlServer, so nothing registers IGlobalStreamQuery.
		// The subscription scope is required and has no default, so it is supplied here as well; this arm
		// is about the absent head source, not about the descriptor, and leaving it blank would fail this
		// arm for a reason it is not testing.
		_ = new ExcaliburEventSourcingBuilder(services)
			.EnableProjectionProcessing(opts => opts.SubscriptionScope = "projection-lag-no-head-source");

		await using var provider = services.BuildServiceProvider();

		// The precondition this arm exists for. If a provider ever starts registering a head source by
		// default, this stops being the no-head-source case and the arm would silently start asserting
		// something else -- so it is checked rather than assumed.
		provider.GetService<IGlobalStreamQuery>().ShouldBeNull(
			"this arm measures the no-head-source branch, so a registered head source invalidates it");

		var readModel = provider.GetRequiredService<IProjectionLagReadModel>();
		var report = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		report.Availability.ShouldBe(
			ProjectionLagAvailability.NoHeadSource,
			"there is no head to subtract a checkpoint from, so the result is an absence of measurement "
			+ "and must not be reported as a measurement that found nothing");

		// Empty is asserted too, but it is NOT the discriminator -- it holds in the Measured case above as
		// well. Stated so nobody later reads the emptiness as the property under test.
		report.Streams.ShouldBeEmpty();
	}
}
