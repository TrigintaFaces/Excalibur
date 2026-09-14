// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Real-DI + real-SQL-Server lock for the saga-tenant-isolation manifest row (1v98gz). Second reference shape
/// after erasure: real infrastructure, resolved through the ACTUAL public registration
/// (<c>AddSqlServerSagaStore</c> + <c>AddMultiTenancy(RowDiscriminator)</c>), never a hand-built store.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this differs from the existing retention conformance suite.</b>
/// <see cref="Tests.Shared.Conformance.Saga.SagaStoreRetentionConformanceTestBase"/> already proves tenant
/// confinement on <c>PurgeCompletedBeforeAsync</c> — but its <c>CreateStoreAsync</c> does <c>new
/// SqlServerSagaStore(...)</c> directly. That proves the STORE'S own predicate; it does not prove the
/// production WIRING — that <c>AddMultiTenancy</c> actually decorates the DI-registered <c>ISagaStore</c>
/// with the fail-closed tenant-scoping guard a real host gets. This lock resolves <c>ISagaStore</c> from a
/// real <see cref="ServiceProvider"/> exactly as a consumer's composition root does.
/// </para>
/// <para>
/// <b>Same saga id, two tenants — the sharp case.</b> Per <c>Excalibur.Saga/ARCHITECTURE.md</c>, a saga id is
/// a business correlation key, so two tenants legitimately run a saga at the same one; the identity now
/// carries the tenant, so a cross-tenant load must be UNADDRESSABLE, not merely refused. Seeding two
/// tenants at the SAME <see cref="SagaState.SagaId"/> is the test that actually distinguishes "confined" from
/// "reachable but declined" — distinct ids would let a bug that resolves the wrong row's tenant column pass.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreTenantConfinementEndToEndShould
{
	private const string TenantA = "e2e-saga-tenant-a";
	private const string TenantB = "e2e-saga-tenant-b";

	private readonly SqlServerSagaStoreContainerFixture _fixture;

	public SqlServerSagaStoreTenantConfinementEndToEndShould(SqlServerSagaStoreContainerFixture fixture) =>
		_fixture = fixture;

	private sealed class E2ESagaState : SagaState
	{
		public string Payload { get; set; } = string.Empty;
	}

	private async Task<ServiceProvider> BuildProviderAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"tenant isolation on the saga store is a real guarantee — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerSagaStore(o =>
		{
			o.ConnectionString = _fixture.ConnectionString;
			o.SchemaName = _fixture.SchemaName;
			o.TableName = _fixture.TableName;
		});
		_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task NotRetrieveAnotherTenantsSaga_AtTheSameSagaId_ThroughTheRealDIRegistration()
	{
		await using var provider = await BuildProviderAsync().ConfigureAwait(false);
		var store = provider.GetRequiredKeyedService<ISagaStore>("default");

		var sagaId = Guid.NewGuid();

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveAsync(
				new E2ESagaState { SagaId = sagaId, Payload = "tenant-a-payload" },
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			// SAFETY. Tenant B never saved this saga id; a store whose identity does not carry the tenant
			// would resolve tenant A's row here.
			var crossTenantRead = await store.LoadAsync<E2ESagaState>(sagaId, CancellationToken.None)
				.ConfigureAwait(false);

			crossTenantRead.ShouldBeNull(
				"a saga saved under tenant A must be unaddressable to tenant B at the same saga id, "
				+ "through the real DI-registered store — not merely refused, UNREACHABLE");
		}

		using (TenantContextHolder.BeginScope(TenantA))
		{
			// LIVENESS. The owning tenant must still read its own saga through the same real registration.
			var ownRead = await store.LoadAsync<E2ESagaState>(sagaId, CancellationToken.None)
				.ConfigureAwait(false);

			ownRead.ShouldNotBeNull(
				"the owning tenant must read its own saga; a store confined so tightly it returns nothing "
				+ "to anybody would pass the safety arm above while being useless");
			ownRead.Payload.ShouldBe("tenant-a-payload");
		}
	}

	[Fact]
	public async Task LetBothTenantsIndependentlyRunASagaAtTheSameId_ThroughTheRealDIRegistration()
	{
		// LIVENESS, the other direction. The identity carries the tenant, so two tenants legitimately run a
		// saga at the same business id — this must not degenerate into an estate-wide uniqueness conflict.
		await using var provider = await BuildProviderAsync().ConfigureAwait(false);
		var store = provider.GetRequiredKeyedService<ISagaStore>("default");

		var sagaId = Guid.NewGuid();

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveAsync(
				new E2ESagaState { SagaId = sagaId, Payload = "a" }, CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			// Must NOT throw a duplicate/concurrency conflict — tenant B is creating ITS OWN saga at this id.
			await store.SaveAsync(
				new E2ESagaState { SagaId = sagaId, Payload = "b" }, CancellationToken.None).ConfigureAwait(false);

			var ownRead = await store.LoadAsync<E2ESagaState>(sagaId, CancellationToken.None)
				.ConfigureAwait(false);
			ownRead.ShouldNotBeNull();
			ownRead.Payload.ShouldBe("b", "tenant B's own row at this id must survive, not be rejected as a duplicate of tenant A's");
		}

		using (TenantContextHolder.BeginScope(TenantA))
		{
			var ownRead = await store.LoadAsync<E2ESagaState>(sagaId, CancellationToken.None)
				.ConfigureAwait(false);
			ownRead.ShouldNotBeNull();
			ownRead.Payload.ShouldBe("a", "tenant A's own row must be untouched by tenant B's later write at the same id");
		}
	}
}
