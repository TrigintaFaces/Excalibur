// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Runs the SHIPPED <see cref="InboxStoreConformanceTestKit"/> against the real Oracle inbox store.
/// </summary>
/// <remarks>
/// <para>
/// The companion <c>OracleInboxStoreConformanceShould</c> in this directory runs a conformance base that
/// lives under <c>tests/</c> and is therefore obtainable by nobody outside this repository. Deriving the
/// published kit is what puts this backend under the same contract we impose on a consumer who writes
/// their own inbox provider.
/// </para>
/// <para>
/// The store is RESOLVED from <c>AddOracleInboxStore</c> rather than constructed here. That is not a
/// stylistic preference: the registration threads <see cref="ITenantContext"/> into the store and emits
/// the tenant-scoping capability marker in the same act, so a hand-built store would certify an object no
/// consumer receives, and would leave that marker attesting wiring the test had performed for it.
/// </para>
/// </remarks>
[Collection(OracleInboxTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
[Trait("Pattern", "STORE")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Conformance arm naming convention")]
public sealed class OracleInboxStoreKitConformanceShould : InboxStoreConformanceTestKit, IAsyncLifetime
{
	private readonly OracleInboxStoreContainerFixture _fixture;
	private ServiceProvider? _provider;
	private IInboxStore? _store;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleInboxStoreKitConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Oracle container fixture, shared by the collection.</param>
	public OracleInboxStoreKitConformanceShould(OracleInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Not implementable synchronously: the store cannot be built before the container and its schema are
	/// awaited. <see cref="CreateStoreAsync"/> is the override this suite uses; this member exists only to
	/// satisfy the kit's abstract seam, and names its own replacement so a failure here is not mistaken for
	/// a store defect.
	/// </remarks>
	protected override IInboxStore CreateStore() =>
		throw new NotSupportedException(
			$"{nameof(OracleInboxStoreKitConformanceShould)} overrides {nameof(CreateStoreAsync)}; "
			+ "the store requires an awaited container and schema and cannot be built synchronously.");

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		if (_store is not null)
		{
			return _store;
		}

		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - real-infra conformance is never skipped. "
			+ _fixture.InitializationError);

		await _fixture.EnsureMultiTenantTableAsync().ConfigureAwait(false);

		var services = new ServiceCollection();

		// Registered BEFORE the provider extension, which reaches the framework default through
		// TryAddSingleton and so will not displace this one. The AMBIENT context, not a fixed one: the
		// kit's isolation arms use ONE store and vary the ambient tenant around it, which is the topology
		// a host runs. A fixed context would address one partition for every arm and pass those arms
		// without exercising isolation at all.
		services.AddSingleton<ITenantContext>(new ConformanceAmbientTenantContext());

		// RequireTenant = true is REQUIRED, and is the same decision as the table shape rather than a
		// second one. The multi-tenant DDL declares TenantId NOT NULL inside PRIMARY KEY (MessageId,
		// HandlerType, TenantId), and the store verifies its table against the mode it was constructed in,
		// so a single-tenant store would be refused by the schema contract -- correctly, since it would
		// ignore TenantId and read across partitions. It is also what keeps the ambient context above
		// legal: the fail-closed guard reached through AddDefaultTenantContext rejects a custom resolving
		// context while RequireTenant is false.
		_ = services.Configure<TenantContextOptions>(options => options.RequireTenant = true);

		// The provider's own shipped registration extension and nothing else.
		_ = services.AddOracleInboxStore(options =>
		{
			options.ConnectionString = _fixture.ConnectionString;
			options.SchemaName = _fixture.SchemaName;
			options.TableName = _fixture.MultiTenantTableName;
			options.CommandTimeoutSeconds = 30;
		});

		_provider = services.BuildServiceProvider();
		// KEYED, not plain. No inbox provider registers an unkeyed IInboxStore: AddTenantAwareStore
		// registers the CONCRETE store, and the contract is exposed only under the provider key plus a
		// "default" alias. "default" is the framework's own resolution path -- InboxPrerequisiteValidator
		// .cs:48 reads GetKeyedService<IInboxStore>("default") -- so it is what a consumer actually gets.
		_store = _provider.GetRequiredKeyedService<IInboxStore>("default");

		return _store;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Data-only (DELETE), never disposal. The kit calls this through <c>ResetDataAsync</c> AFTER building
	/// the store and BEFORE each arm, so a cleanup that also disposed the provider would hand every arm a
	/// dead handle.
	/// </remarks>
	protected override Task CleanupAsync() => _fixture.CleanupMultiTenantTableAsync();

	/// <inheritdoc/>
	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_provider is not null)
		{
			await _provider.DisposeAsync().ConfigureAwait(false);
		}

		_provider = null;
		_store = null;
	}

	#region Create arms

	[Fact]
	public Task CreateEntryAsync_NewEntry_ShouldSucceed_Test() =>
		CreateEntryAsync_NewEntry_ShouldSucceed();

	[Fact]
	public Task CreateEntryAsync_DuplicateEntry_ShouldThrow_Test() =>
		CreateEntryAsync_DuplicateEntry_ShouldThrow();

	[Fact]
	public Task CreateEntryAsync_WithAllMetadata_ShouldPreserve_Test() =>
		CreateEntryAsync_WithAllMetadata_ShouldPreserve();

	#endregion Create arms

	#region Process arms

	[Fact]
	public Task MarkProcessedAsync_ExistingEntry_ShouldSucceed_Test() =>
		MarkProcessedAsync_ExistingEntry_ShouldSucceed();

	[Fact]
	public Task TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue_Test() =>
		TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue();

	[Fact]
	public Task TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse_Test() =>
		TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse();

	[Fact]
	public Task IsProcessedAsync_ProcessedMessage_ShouldReturnTrue_Test() =>
		IsProcessedAsync_ProcessedMessage_ShouldReturnTrue();

	[Fact]
	public Task IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse_Test() =>
		IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse();

	#endregion Process arms

	#region Fail arms

	[Fact]
	public Task MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError_Test() =>
		MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError();

	[Fact]
	public Task MarkFailedAsync_ShouldIncrementRetryCount_Test() =>
		MarkFailedAsync_ShouldIncrementRetryCount();

	[Fact]
	public Task GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries_Test() =>
		GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries();

	[Fact]
	public Task GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries_Test() =>
		GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries();


	#endregion Fail arms

	#region Query arms

	[Fact]
	public Task GetEntryAsync_Existing_ShouldReturnEntry_Test() =>
		GetEntryAsync_Existing_ShouldReturnEntry();

	[Fact]
	public Task GetEntryAsync_NonExistent_ShouldReturnNull_Test() =>
		GetEntryAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts_Test() =>
		GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts();

	#endregion Query arms

	#region Cleanup arms

	[Fact]
	public Task CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove_Test() =>
		CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove();

	[Fact]
	public Task CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent_Test() =>
		CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent();

	#endregion Cleanup arms

	#region Key-isolation arms

	[Fact]
	public Task Entries_ShouldIsolateByMessageIdAndHandlerType_Test() =>
		Entries_ShouldIsolateByMessageIdAndHandlerType();

	[Fact]
	public Task SameMessageId_DifferentHandlers_ShouldBeIndependent_Test() =>
		SameMessageId_DifferentHandlers_ShouldBeIndependent();

	#endregion Key-isolation arms

	#region Edge cases

	[Fact]
	public Task GetAllTenantsEntriesAsync_ShouldReturnAllEntries_Test() =>
		GetAllTenantsEntriesAsync_ShouldReturnAllEntries();

	#endregion Edge cases

	#region Tenant-isolation arms

	[Fact]
	public Task TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate_Test() =>
		TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate();

	[Fact]
	public Task IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed_Test() =>
		IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed();

	[Fact]
	public Task CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide_Test() =>
		CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide();

	#endregion Tenant-isolation arms

	#region Concurrency arm

	[Fact]
	public Task ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker_Test() =>
		ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker();

	[Fact]
	public Task ExpiredLease_MustBeReclaimableByAnotherProcessor_Test() =>
		ExpiredLease_MustBeReclaimableByAnotherProcessor();

	[Fact]
	public Task LiveLease_MustNotBeReclaimableByAnotherProcessor_Test() =>
		LiveLease_MustNotBeReclaimableByAnotherProcessor();

	[Fact]
	public Task ExpiredLease_MustBeReadmittedByTheRetryDrainRead_Test() =>
		ExpiredLease_MustBeReadmittedByTheRetryDrainRead();

	[Fact]
	public Task LiveLease_MustNotBeReadmittedByTheRetryDrainRead_Test() =>
		LiveLease_MustNotBeReadmittedByTheRetryDrainRead();

	[Fact]
	public Task LeaselessClaim_MustNotBeReclaimableByTheLeasePath_Test() =>
		LeaselessClaim_MustNotBeReclaimableByTheLeasePath();

	[Fact]
	public Task ReleasedClaim_MustBeReadmittedForRedelivery_Test() =>
		ReleasedClaim_MustBeReadmittedForRedelivery();

	[Fact]
	public Task Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord_Test() =>
		Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord();

	#endregion Concurrency arm

	#region Suite wiring

	[Fact]
	public Task ProcessedEntry_MustNotBeReadmittedByTheClaimPath_Test() =>
		ProcessedEntry_MustNotBeReadmittedByTheClaimPath();

	[Fact]
	public Task ProcessedEntry_MustNotBeDemotedByTheProcessingMark_Test() =>
		ProcessedEntry_MustNotBeDemotedByTheProcessingMark();

	[Fact]
	public Task FailedEntry_MustBeReAdmittedByTheLeasePath_Test() =>
		FailedEntry_MustBeReAdmittedByTheLeasePath();

	[Fact]
	public Task FailedEntry_MustNotBeReadmittedByTheClaimPath_Test() =>
		FailedEntry_MustNotBeReadmittedByTheClaimPath();

	/// <summary>
	/// Fails if this suite stops exposing an arm the shipped kit declares.
	/// </summary>
	/// <remarks>
	/// An arm nobody wires never executes, and an arm that never executes cannot fail -- in the results it
	/// is indistinguishable from one that passed. Wiring is therefore checked rather than asserted: an arm
	/// added to the shipped kit turns this red here instead of going silently unrun.
	/// </remarks>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
		ConformanceSuite_ShouldWireEveryArm();

	#endregion Suite wiring
}
