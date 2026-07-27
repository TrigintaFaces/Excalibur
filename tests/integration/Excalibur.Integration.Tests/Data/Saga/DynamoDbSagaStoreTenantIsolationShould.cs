// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Author≠implementer real-DynamoDb (LocalStack) lock for the saga tenant-isolation keystone (bead
/// <c>8rqzdl</c>): the <see cref="DynamoDbSagaStore"/> derives a tenant scope from the ambient
/// <see cref="ITenantContext"/> and applies it to every keyed <c>LoadAsync</c>, so one tenant can NEVER
/// load another tenant's saga by its id.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — tenant B's <c>LoadAsync</c> for tenant A's saga id
/// returns <see langword="null"/> (RED against the pre-<c>ec993cbbf</c> shape, whose <c>LoadAsync</c> keyed
/// on the saga id alone and returned another tenant's saga). LIVENESS — the owning tenant still loads its
/// own saga.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real DynamoDb via LocalStack (TestContainers). NON-SKIPPED.
/// Both stores share one per-run table so a cross-tenant read has a real row to (not) match.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbSagaStoreTenantIsolationShould : IClassFixture<DynamoDbSagaStoreContainerFixture>
{
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly DynamoDbSagaStoreContainerFixture _fixture;
	private readonly string _tableName = "sagas_iso_" + Guid.NewGuid().ToString("N");

	public DynamoDbSagaStoreTenantIsolationShould(DynamoDbSagaStoreContainerFixture fixture) => _fixture = fixture;

	private DynamoDbSagaStore CreateStore(string? tenantId) =>
		new(
			Options.Create(new DynamoDbSagaOptions
			{
				Connection = new DynamoDbConnectionOptions
				{
					ServiceUrl = _fixture.ServiceUrl,
					Region = "us-east-1",
					AccessKey = "test",
					SecretKey = "test",
				},
				TableName = _tableName,
				CreateTableIfNotExists = true,
				UseConsistentReads = true,
			}),
			NullLogger<DynamoDbSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));

	[Fact]
	public async Task Not_let_one_tenant_load_another_tenants_saga()
	{
		// SAFETY. A saga written under tenant A must not be loadable by tenant B via its id.

		var sagaId = Guid.NewGuid();

		await CreateStore(TenantA)
			.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		var crossTenantLoad = await CreateStore(TenantB)
			.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		crossTenantLoad.ShouldBeNull(
			"tenant B must not load tenant A's saga by its id — the ambient tenant scope on LoadAsync isolates "
			+ "it; without the tenant predicate a keyed load matches the id alone and discloses another tenant's "
			+ "saga");
	}

	[Fact]
	public async Task Load_a_saga_within_the_owning_tenant()
	{
		// LIVENESS. The owning tenant still loads its own saga (the scope isolates, it is not reject-all).

		var sagaId = Guid.NewGuid();
		var storeA = CreateStore(TenantA);

		await storeA
			.SaveAsync(new TestSagaState { SagaId = sagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		var loaded = await storeA
			.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull("the owning tenant loads its own saga — the tenant scope is not reject-all");
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
