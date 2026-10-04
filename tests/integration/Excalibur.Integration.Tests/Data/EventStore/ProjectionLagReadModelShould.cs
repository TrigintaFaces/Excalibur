// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
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
			.EnableProjectionProcessing();

		return services.BuildServiceProvider();
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
		await checkpoints.AdvanceCheckpointAsync("subscription-1", null, 2, CancellationToken.None).ConfigureAwait(false);

		var readModel = provider.GetRequiredService<IProjectionLagReadModel>();
		var report = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		report.Availability.ShouldBe(ProjectionLagAvailability.Measured);
		report.Streams.Count.ShouldBe(1);
		var entry = report.Streams[0];
		entry.SubscriptionName.ShouldBe("subscription-1");
		entry.HeadPosition.ShouldBe(5);
		entry.CheckpointPosition.ShouldBe(2);
		entry.Lag.ShouldBe(3);
	}

	[Fact]
	public async Task ClampLagToZeroWhenCheckpointIsAheadOfHead()
	{
		var store = await InitStoreAsync().ConfigureAwait(false);
		await using var provider = BuildProvider();

		// head = 3, checkpoint seeded past the head at 10 → structural Math.Max(0, head − cp) clamps lag to 0.
		await AppendEventsAsync(store, 3).ConfigureAwait(false);
		var checkpoints = provider.GetRequiredService<ISubscriptionCheckpointStore>();
		await checkpoints.AdvanceCheckpointAsync("ahead-of-head", null, 10, CancellationToken.None).ConfigureAwait(false);

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
		report.Streams.Count.ShouldBe(1);
		report.Streams[0].HeadPosition.ShouldBe(3);
		report.Streams[0].CheckpointPosition.ShouldBe(10);
		report.Streams[0].Lag.ShouldBe(0, "lag is a structural safe-op and can never be negative");

		// The entries are still reported, so an operator can see WHICH subscription is implicated rather
		// than only that something is wrong.
		report.Streams[0].SubscriptionName.ShouldBe("ahead-of-head");
	}

	[Fact]
	public async Task ReturnEmptyWhenNoCheckpointsAreRecorded()
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
			"a head exists, so this empty result is a measurement rather than an absence of one");
		report.Streams.ShouldBeEmpty();
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
		_ = new ExcaliburEventSourcingBuilder(services).EnableProjectionProcessing();

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
