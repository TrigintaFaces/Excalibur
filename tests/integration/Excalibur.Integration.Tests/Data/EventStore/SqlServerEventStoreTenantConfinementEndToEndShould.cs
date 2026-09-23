// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-DI + real-SQL-Server arm for the <c>event-store-tenant-confinement</c> manifest row.
/// </summary>
/// <remarks>
/// <para>
/// <b>Guarantee under test.</b> <c>Excalibur.EventSourcing/ARCHITECTURE.md</c> - "A store is confining when
/// an operation performed under tenant partition P observes and mutates only rows written under P."
/// </para>
/// <para>
/// <b>Same aggregate id, two tenants - the sharp case.</b> An aggregate id is a business identifier, so two
/// tenants legitimately hold a stream at the same one. Seeding both at the SAME id is what distinguishes
/// "confined" from "reachable but empty": distinct ids let a store that resolves the wrong row's tenant
/// column pass, because the ids alone already separate the streams.
/// </para>
/// <para>
/// <b>Why through the real registration.</b> A store's own predicate can be correct while the production
/// wiring never applies it - <c>AddMultiTenancy</c> is what decorates the DI-registered store with the
/// scoping guard a real host gets. This arm resolves <see cref="IEventStore"/> from a real
/// <see cref="ServiceProvider"/> composed the way a consumer composes it, so a correct store that is never
/// decorated fails here.
/// </para>
/// <para>
/// <b>Both halves.</b> SAFETY - tenant B cannot observe tenant A's stream at the shared id. LIVENESS -
/// tenant A still reads its own; a store confined so tightly it returns nothing to anybody satisfies the
/// safety arm while being useless, and that is the failure the safety assertion alone cannot see.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
public sealed class SqlServerEventStoreTenantConfinementEndToEndShould
	: IClassFixture<SqlServerEventStoreContainerFixture>
{
	private const string TenantA = "e2e-es-tenant-a";
	private const string TenantB = "e2e-es-tenant-b";
	private const string AggregateType = "ConfinementAggregate";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreTenantConfinementEndToEndShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	[MessageName("Test.SqlServerEventStoreTenantConfinement.OrderPlaced")]
	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Fact]
	public async Task NotObserveAnotherTenantsStream_AtTheSameAggregateId_ThroughTheRealDIRegistration()
	{
		var cancellationToken = TestContext.Current.CancellationToken;

		_fixture.DockerAvailable.ShouldBeTrue(
			"tenant confinement on the event store is an advertised guarantee - this real-SQL-Server arm "
			+ "must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildConsumerComposition();
		var store = provider.GetRequiredService<IEventStore>();

		// One id, two tenants. This is the case that distinguishes confinement from mere separation.
		var aggregateId = Guid.NewGuid().ToString();

		using (TenantContextHolder.BeginScope(TenantA))
		{
			_ = await store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { new OrderPlaced(aggregateId, 0), new OrderPlaced(aggregateId, 1) },
				-1,
				cancellationToken).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			// SAFETY. Tenant B wrote nothing at this id. A store whose read is not partitioned by tenant
			// resolves tenant A's events here.
			var crossTenantRead = await store.LoadAsync(aggregateId, AggregateType, cancellationToken)
				.ConfigureAwait(false);

			crossTenantRead.ShouldBeEmpty(
				"events appended under tenant A must be unobservable to tenant B at the same aggregate id, "
				+ "through the real DI-registered store - the guarantee is that an operation under partition "
				+ "P observes only rows written under P");
		}

		using (TenantContextHolder.BeginScope(TenantA))
		{
			// LIVENESS. Without this, a store that returns nothing to anyone passes the arm above.
			var ownRead = await store.LoadAsync(aggregateId, AggregateType, cancellationToken)
				.ConfigureAwait(false);

			ownRead.Count.ShouldBe(
				2,
				"the owning tenant must read its own stream; a store confined so tightly it returns nothing "
				+ "to anybody would satisfy the safety arm above while being useless");
		}
	}

	/// <summary>
	/// Composes the event store the way a consumer does: the provider registration plus the multi-tenancy
	/// decoration that supplies the scoping guard.
	/// </summary>
	private ServiceProvider BuildConsumerComposition()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburEventSourcing(_ => { });
		_ = services.AddSqlServerEventStore(
			() => _fixture.CreateConnection(), _fixture.SchemaName, _fixture.TableName);
		_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		return services.BuildServiceProvider();
	}
}
