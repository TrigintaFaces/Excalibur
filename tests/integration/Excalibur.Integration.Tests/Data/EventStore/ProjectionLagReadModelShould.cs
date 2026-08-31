// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
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
		await checkpoints.StoreCheckpointAsync("subscription-1", 2, CancellationToken.None).ConfigureAwait(false);

		var readModel = provider.GetRequiredService<IProjectionLagReadModel>();
		var lags = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		lags.Count.ShouldBe(1);
		var entry = lags[0];
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
		await checkpoints.StoreCheckpointAsync("ahead-of-head", 10, CancellationToken.None).ConfigureAwait(false);

		var readModel = provider.GetRequiredService<IProjectionLagReadModel>();
		var lags = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		lags.Count.ShouldBe(1);
		lags[0].HeadPosition.ShouldBe(3);
		lags[0].CheckpointPosition.ShouldBe(10);
		lags[0].Lag.ShouldBe(0, "lag is a structural safe-op and can never be negative");
	}

	[Fact]
	public async Task ReturnEmptyWhenNoCheckpointsAreRecorded()
	{
		var store = await InitStoreAsync().ConfigureAwait(false);
		await using var provider = BuildProvider();

		// A head exists but no subscription has checkpointed → nothing to report (empty, not a 500 / not a throw).
		await AppendEventsAsync(store, 4).ConfigureAwait(false);

		var readModel = provider.GetRequiredService<IProjectionLagReadModel>();
		var lags = await readModel.GetLagAsync(CancellationToken.None).ConfigureAwait(false);

		lags.ShouldBeEmpty();
	}
}
