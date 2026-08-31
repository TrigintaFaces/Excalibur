// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DynamoDb;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Runs the SHIPPED <see cref="SagaStoreConformanceTestKit"/> against the real DynamoDB saga store
/// (LocalStack), through the provider's OWN public registration path.
/// </summary>
/// <remarks>
/// <para>
/// The companion concurrency suite in this directory derives a conformance base that lives under
/// <c>tests/</c> and is obtainable by nobody outside this repository. That base takes a store the test
/// constructs by hand; this kit takes an <see cref="IServiceCollection"/> and resolves the store the
/// provider's own registration produces. The difference is not stylistic: a hand-built store is whatever
/// the test author assembled, so the arms certify an object no consumer receives, and the four tenant arms
/// below have no counterpart in the private base at all.
/// </para>
/// <para>
/// <strong>This suite is expected to fail at store resolution, and that failure is the point.</strong>
/// <c>UseDynamoDb</c> registers the concrete <c>DynamoDbSagaStore</c> plus two KEYED <c>ISagaStore</c>
/// aliases (<c>"dynamodb"</c> and <c>"default"</c>), but never a non-keyed <c>ISagaStore</c> — so
/// <c>GetRequiredService&lt;ISagaStore&gt;()</c>, which is what a consumer injecting <c>ISagaStore</c>
/// receives, has nothing to resolve. Registering that alias here would turn this suite green against a
/// registration no consumer can reproduce, which is the same defect wearing a different hat. The fix
/// belongs in the provider's registration extension.
/// </para>
/// <para>
/// Every arm the kit declares is wired here. An arm nobody wires never runs, and an arm that never runs is
/// indistinguishable in a test report from one that passed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "DynamoDb")]
[Trait("Pattern", "STORE")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Conformance arm naming convention")]
public sealed class DynamoDbSagaStoreKitConformanceShould : SagaStoreConformanceTestKit,
	IClassFixture<DynamoDbSagaStoreContainerFixture>, IAsyncLifetime
{
	private readonly DynamoDbSagaStoreContainerFixture _fixture;
	private readonly string _tableName = $"sagas_kit_{Guid.NewGuid():N}";

	public DynamoDbSagaStoreKitConformanceShould(DynamoDbSagaStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// NON-SKIPPED. Real infrastructure is a hard requirement here: a skip-gated infra arm passes by not
	/// running, which is exactly the reporting shape this suite exists to remove.
	/// </remarks>
	public ValueTask InitializeAsync()
	{
		_fixture.IsInitialized.ShouldBeTrue(
			"LocalStack DynamoDB must be available for real-infra conformance (never skipped).");

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc/>
	/// <remarks>
	/// The provider's own shipped saga-builder extension and nothing else. Constructing the store here, or
	/// adding the missing non-keyed <c>ISagaStore</c> alias, would reintroduce exactly the hole this seam
	/// closes. The lambda parameter is typed explicitly because the <c>AddSagas</c> overloads are otherwise
	/// ambiguous for a bare lambda and the compiler binds <c>Action&lt;SagaOptions&gt;</c>.
	/// </remarks>
	protected override void ConfigureProvider(IServiceCollection services)
	{
		// LocalStack accepts any credentials but the AWS SDK refuses to send NONE: with nothing
		// configured it walks its default chain (environment, web identity, profile, EC2 metadata) and
		// throws when every source is absent, which is the state of a CI runner and of any developer
		// machine without an AWS profile. Supplying throwaway values here is what makes this suite
		// runnable off an AWS-configured box; they are never sent anywhere real.
		_ = services.Configure<DynamoDbSagaOptions>(options =>
		{
			options.Connection.AccessKey = "test";
			options.Connection.SecretKey = "test";
		});

		_ = services.AddExcalibur(x => x.AddSagas((ISagaBuilder saga) =>
			_ = saga.UseDynamoDb(dynamo =>
				_ = dynamo
					.ServiceUrl(_fixture.ServiceUrl)
					.TableName(_tableName))));
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Data-only, and a no-op by construction: this instance owns a private table name, so an arm never
	/// starts against another arm's data. The kit calls this before every arm, so it must not dispose
	/// anything the arm is about to use; the container fixture owns the LocalStack lifetime.
	/// </remarks>
	protected override Task ResetDataAsync() => Task.CompletedTask;

	/// <inheritdoc/>
	/// <remarks>Data-only. The throwaway per-instance table goes away with the container.</remarks>
	protected override Task CleanupAsync() => Task.CompletedTask;

	/// <inheritdoc/>
	/// <remarks>
	/// <c>DynamoDbSagaStore.SaveAsync</c> is a version-gated conditional write:
	/// <c>attribute_not_exists(#pk)</c> for a new saga (DynamoDbSagaStore.cs:266) and
	/// <c>#v = :expectedVersion AND #t = :tenantId</c> for an update (DynamoDbSagaStore.cs:278), with
	/// <c>ConditionalCheckFailedException</c> surfaced as <c>ConcurrencyException</c>
	/// (DynamoDbSagaStore.cs:296-303). Both the no-lost-update and the no-resurrect halves are enforced by
	/// the database, so the optimistic-concurrency arms run rather than early-return.
	/// </remarks>
	protected override bool SupportsOptimisticConcurrency => true;

	#region Save

	[Fact]
	public Task SaveAsync_NewSaga_ShouldSucceed_Test() => SaveAsync_NewSaga_ShouldSucceed();

	[Fact]
	public Task SaveAsync_ExistingSaga_ShouldUpdate_Test() => SaveAsync_ExistingSaga_ShouldUpdate();

	[Fact]
	public Task SaveAsync_CompletedSaga_ShouldPersistCompletedFlag_Test() =>
		SaveAsync_CompletedSaga_ShouldPersistCompletedFlag();

	#endregion Save

	#region Load

	[Fact]
	public Task LoadAsync_NonExistent_ShouldReturnNull_Test() => LoadAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task LoadAsync_ExistingSaga_ShouldReturnState_Test() => LoadAsync_ExistingSaga_ShouldReturnState();

	[Fact]
	public Task LoadAsync_AfterMultipleUpdates_ShouldReturnLatest_Test() =>
		LoadAsync_AfterMultipleUpdates_ShouldReturnLatest();

	#endregion Load

	#region Round-trip

	[Fact]
	public Task SaveAndLoad_ShouldPreserveAllProperties_Test() => SaveAndLoad_ShouldPreserveAllProperties();

	[Fact]
	public Task SaveAndLoad_ShouldPreserveDateTimeValues_Test() => SaveAndLoad_ShouldPreserveDateTimeValues();

	#endregion Round-trip

	#region Isolation

	[Fact]
	public Task Sagas_ShouldIsolateBySagaId_Test() => Sagas_ShouldIsolateBySagaId();

	[Fact]
	public Task UpdateOneSaga_ShouldNotAffectOthers_Test() => UpdateOneSaga_ShouldNotAffectOthers();

	#endregion Isolation

	#region Edge cases

	[Fact]
	public Task SaveAsync_WithDefaultValues_ShouldSucceed_Test() => SaveAsync_WithDefaultValues_ShouldSucceed();

	#endregion Edge cases

	#region Optimistic concurrency

	[Fact]
	public Task StaleSave_ThrowsConcurrencyException_NoLostUpdate_Test() =>
		StaleSave_ThrowsConcurrencyException_NoLostUpdate();

	[Fact]
	public Task StaleSave_OnMissingSaga_DoesNotResurrect_Test() => StaleSave_OnMissingSaga_DoesNotResurrect();

	[Fact]
	public Task LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds_Test() =>
		LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds();

	#endregion Optimistic concurrency

	#region Tenant confinement

	[Fact]
	public Task TenantScopedLoad_MustNotSeeAnotherTenantsSaga_Test() =>
		TenantScopedLoad_MustNotSeeAnotherTenantsSaga();

	[Fact]
	public Task TenantScopedLoad_MustSeeItsOwnSaga_Test() => TenantScopedLoad_MustSeeItsOwnSaga();

	[Fact]
	public Task TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId_Test() =>
		TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId();

	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnSaga_Test() => UntenantedPartition_MustRoundTripItsOwnSaga();

	#endregion Tenant confinement

	#region Suite wiring

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	#endregion Suite wiring
}
