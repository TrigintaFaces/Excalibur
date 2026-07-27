// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Shouldly;

using Tests.Shared.Conformance.Saga;
using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Author≠implementer real-Mongo lock for the saga tenant-isolation keystone (bead <c>8rqzdl</c>): the
/// <see cref="MongoDbSagaStore"/> derives a tenant scope from the ambient <see cref="ITenantContext"/> and
/// filters every keyed <c>LoadAsync</c> by it, so one tenant can NEVER load another tenant's saga by its id.
/// </summary>
/// <remarks>
/// <para>
/// The impl fix (committed) makes the Mongo saga document carry a first-class <c>TenantId</c> and adds a
/// <c>TenantFilter()</c> to <c>LoadAsync</c>'s <c>Filter.And(...)</c>. Before it, an unscoped/other-tenant
/// <c>LoadAsync(sagaId)</c> matched purely on <c>SagaId</c> + <c>SagaType</c> and returned another tenant's
/// saga — a cross-tenant disclosure.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — tenant B's <c>LoadAsync</c> for tenant A's saga id
/// returns <see langword="null"/> (RED on the mutant that drops the tenant filter, which would return A's
/// saga). LIVENESS — the owning tenant still loads its own saga (the filter isolates, it does not reject
/// everything).
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real Mongo (TestContainers, replica set) so the
/// <c>TenantFilter</c> is evaluated by the real engine. NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbSagaStoreTenantIsolationShould : IClassFixture<MongoDbContainerFixture>
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly MongoDbContainerFixture _fixture;

	// A fresh collection per test class run so no other suite's documents can bleed in.
	private readonly string _collection = "saga_tenant_iso_" + Guid.NewGuid().ToString("N");

	public MongoDbSagaStoreTenantIsolationShould(MongoDbContainerFixture fixture) => _fixture = fixture;

	private MongoDbSagaStore CreateStore(string? tenantId) =>
		new(
			new MongoClient(_fixture.ConnectionString),
			Options.Create(new MongoDbSagaOptions
			{
				ConnectionString = _fixture.ConnectionString,
				DatabaseName = "excalibur_saga_iso",
				CollectionName = _collection,
			}),
			NullLogger<MongoDbSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));

	[Fact]
	public async Task Not_let_one_tenant_load_another_tenants_saga()
	{
		// SAFETY. A saga written under tenant A must not be loadable by tenant B via its id.
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant saga isolation is a security boundary — this real-Mongo lock must never be skipped");

		var sagaId = Guid.NewGuid();

		await CreateStore(TenantA)
			.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		var crossTenantLoad = await CreateStore(TenantB)
			.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		crossTenantLoad.ShouldBeNull(
			"tenant B must not load tenant A's saga by its id — the ambient TenantFilter on LoadAsync isolates "
			+ "it; without the tenant predicate an unscoped/other-tenant load matches on SagaId+SagaType alone "
			+ "and discloses another tenant's saga");
	}

	[Fact]
	public async Task Load_a_saga_within_the_owning_tenant()
	{
		// LIVENESS. The owning tenant still loads its own saga (the filter isolates, it is not reject-all).
		_fixture.DockerAvailable.ShouldBeTrue(
			"the owning tenant must still load its own saga — this real-Mongo lock must never be skipped");

		var sagaId = Guid.NewGuid();
		var storeA = CreateStore(TenantA);

		await storeA
			.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await storeA
			.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull("the owning tenant loads its own saga — the tenant filter is not reject-all");
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
