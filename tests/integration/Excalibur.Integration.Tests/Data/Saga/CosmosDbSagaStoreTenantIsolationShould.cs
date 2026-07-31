// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Author≠implementer real-Cosmos (emulator) lock for the saga tenant-isolation keystone (bead
/// <c>8rqzdl</c>): the <see cref="CosmosDbSagaStore"/> isolates tenants by a <b>point read + client-side
/// ownership check</b> (SA-ruled seam), so one tenant can NEVER load another tenant's saga by its id.
/// </summary>
/// <remarks>
/// <para>
/// <b>The seam (per SA ruling):</b> <c>LoadAsync</c> does a point read <c>ReadItemAsync(documentId,
/// PartitionKey(sagaType))</c> — no server-side tenant predicate — then applies a CLIENT-SIDE
/// <c>if (!OwnedByCurrentScope(document)) return null;</c>. So a cross-tenant load DOES fetch the other
/// tenant's document, and the ownership check is what returns <see langword="null"/>. This lock binds that
/// exact behaviour (not a server-side query).
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — tenant B's <c>LoadAsync</c> for tenant A's saga id
/// returns <see langword="null"/> (RED against the pre-<c>ec993cbbf</c> shape, before the document carried
/// a first-class tenant / the ownership check existed). LIVENESS — the owning tenant still loads its own
/// saga (the ownership check accepts an in-scope document).
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real Cosmos emulator (TestContainers). NON-SKIPPED. Both
/// stores share one per-run container so the cross-tenant point read has a real document to fetch and then
/// reject on ownership.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbSagaStoreTenantIsolationShould : IClassFixture<CosmosDbSagaStoreContainerFixture>
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly CosmosDbSagaStoreContainerFixture _fixture;
	private readonly string _containerName = "sagas_iso_" + Guid.NewGuid().ToString("N");

	public CosmosDbSagaStoreTenantIsolationShould(CosmosDbSagaStoreContainerFixture fixture) => _fixture = fixture;

	private CosmosDbSagaStore CreateStore(string? tenantId) =>
		new(
			_fixture.Client,
			Options.Create(new CosmosDbSagaOptions
			{
				Client = new CosmosDbClientOptions
				{
					ConnectionString = _fixture.ConnectionString,
					HttpClientFactory = () => _fixture.EmulatorHttpClient,
				},
				DatabaseName = _fixture.DatabaseName,
				ContainerName = _containerName,
				PartitionKeyPath = "/sagaType",
				CreateContainerIfNotExists = true,
				ContainerThroughput = 400,
			}),
			NullLogger<CosmosDbSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));

	[Fact]
	public async Task Not_let_one_tenant_load_another_tenants_saga()
	{
		// SAFETY. A saga written under tenant A must not be loadable by tenant B via its id — the point read
		// fetches A's document, and OwnedByCurrentScope returns null under tenant B's scope.
		var sagaId = Guid.NewGuid();

		await CreateStore(TenantA)
			.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		var crossTenantLoad = await CreateStore(TenantB)
			.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		crossTenantLoad.ShouldBeNull(
			"tenant B must not load tenant A's saga by its id — the point read fetches A's document but the "
			+ "client-side OwnedByCurrentScope check rejects it under tenant B's scope; without the ownership "
			+ "check a keyed point read would disclose another tenant's saga");
	}

	[Fact]
	public async Task Load_a_saga_within_the_owning_tenant()
	{
		// LIVENESS. The owning tenant still loads its own saga (OwnedByCurrentScope accepts an in-scope doc).
		var sagaId = Guid.NewGuid();
		var storeA = CreateStore(TenantA);

		await storeA
			.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await storeA
			.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull("the owning tenant loads its own saga — the ownership check is not reject-all");
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
