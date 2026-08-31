// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Compliance;
using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Dispatch.Integration.Tests.Compliance;
using Excalibur.Testing.Conformance;


namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Runs the shared erasure conformance kit against the REAL Postgres store.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS EXISTS.</b> Before this class, <c>PostgresErasureStore</c> was referenced by exactly one
/// test file in the repository — a disposal test shared with several other stores — and derived from
/// <see cref="ErasureStoreConformanceTestKit"/> nowhere. All 24 arms ran against the in-memory store
/// alone. Of the two SQL engines this was the thinner side: SqlServer at least had unit tests and a
/// tenant-isolation integration test, and Postgres had neither.
/// </para>
/// <para>
/// <b>Why an engine-specific class rather than a shared generic one.</b> The two engines diverge in the
/// things most likely to break: identifier casing (Postgres folds unquoted identifiers to lower case, so
/// the table names below are lower-case where the SqlServer fixture is Pascal), parameter binding, and
/// timestamp handling. A shared harness parameterised over a connection string would hide exactly the
/// divergence class this pair exists to detect.
/// </para>
/// <para>
/// <b>What a green run here does and does not prove.</b> It proves this store satisfies the arms the kit
/// declares, against a real database engine. It does <b>not</b> prove the arms are sufficient — the kit
/// is the contract, and a contract can be incomplete. One arm below names a tenant filter; whether it
/// detects a cross-tenant disclosure rather than merely exercising the parameter is a property of the
/// kit, not of this class, and is not asserted here.
/// </para>
/// <para>
/// <b>Every arm is surfaced deliberately.</b> The arms are inherited wholesale and each is wrapped as a
/// <c>[Fact]</c> below, so adding an arm to the kit does not silently skip this provider — an un-wrapped
/// arm becomes a visible omission in this file rather than an absence nobody can see.
/// </para>
/// <para>
/// <b>No hand-written DDL, and per-instance tables.</b> The store provisions its own schema
/// (<c>AutoCreateSchema</c>), which keeps a fixture-declared copy from drifting ahead of or behind the
/// shipped one. The suffix isolates each arm into its own pair of tables on the shared container, so a
/// bounded read such as <c>GetScheduledRequestsAsync</c> cannot be pushed past its page bound by residue
/// from an earlier arm.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class PostgresErasureStoreConformanceTests : ErasureStoreConformanceTestKit
{

	/// <summary>
	/// Exposes the kit's own wiring check to the runner. The check is an arm like any other, so a
	/// suite that omits THIS member disables it silently -- the one gap it cannot report itself.
	/// </summary>
	/// <returns>A completed task when every arm in the kit is wired.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
		ConformanceSuite_ShouldWireEveryArm();

	private readonly PostgresFixture _fixture;

	/// <summary>
	/// Isolates this instance's tables. One xUnit instance per test, so this is per-arm isolation.
	/// </summary>
	private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

	public PostgresErasureStoreConformanceTests(PostgresFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override IErasureStore CreateStore()
	{
		var options = new PostgresErasureStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",

			// Lower-case deliberately: Postgres folds unquoted identifiers, and matching that convention
			// here is part of what distinguishes this fixture from its SqlServer sibling.
			RequestsTableName = $"erasure_requests_{_suffix}",
			CertificatesTableName = $"erasure_certificates_{_suffix}",
			CommandTimeoutSeconds = 30,

			// The store provisions its own tables. See the class remarks.
			AutoCreateSchema = true
		};

		// Fully qualified: this file's namespace makes a bare `Options` bind to Excalibur.Dispatch.Options.
		return new PostgresErasureStore(
			Microsoft.Extensions.Options.Options.Create(options),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<PostgresErasureStore>(),
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
	}

	#region Save

	[Fact]
	public Task SaveRequestAsync_ShouldPersistRequest_Test() => SaveRequestAsync_ShouldPersistRequest();

	[Fact]
	public Task SaveRequestAsync_DuplicateId_ShouldThrowDuplicateErasureRequestException_Test() =>
		SaveRequestAsync_DuplicateId_ShouldThrowDuplicateErasureRequestException();

	[Fact]
	public Task SaveRequestAsync_NonDuplicateFailure_ShouldNotTranslateToDuplicate_Test() =>
		SaveRequestAsync_NonDuplicateFailure_ShouldNotTranslateToDuplicate();

	[Fact]
	public Task SaveRequestAsync_ShouldHashDataSubjectId_Test() => SaveRequestAsync_ShouldHashDataSubjectId();

	#endregion Save

	#region Status

	[Fact]
	public Task GetStatusAsync_NonExistent_ShouldReturnNull_Test() => GetStatusAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task UpdateStatusAsync_ShouldUpdateStatus_Test() => UpdateStatusAsync_ShouldUpdateStatus();

	[Fact]
	public Task UpdateStatusAsync_ToInProgress_ShouldSetExecutedAt_Test() =>
		UpdateStatusAsync_ToInProgress_ShouldSetExecutedAt();

	[Fact]
	public Task UpdateStatusAsync_NonExistent_ShouldReturnFalse_Test() =>
		UpdateStatusAsync_NonExistent_ShouldReturnFalse();

	#endregion Status

	#region Completion

	[Fact]
	public Task RecordCompletionAsync_ShouldMarkCompleted_Test() => RecordCompletionAsync_ShouldMarkCompleted();

	[Fact]
	public Task RecordCompletionAsync_NonExistent_ShouldThrowKeyNotFoundException_Test() =>
		RecordCompletionAsync_NonExistent_ShouldThrowKeyNotFoundException();

	#endregion Completion

	#region Cancellation

	[Fact]
	public Task RecordCancellationAsync_Scheduled_ShouldCancel_Test() =>
		RecordCancellationAsync_Scheduled_ShouldCancel();

	[Fact]
	public Task RecordCancellationAsync_Pending_ShouldCancel_Test() =>
		RecordCancellationAsync_Pending_ShouldCancel();

	[Fact]
	public Task RecordCancellationAsync_InProgress_ShouldReturnFalse_Test() =>
		RecordCancellationAsync_InProgress_ShouldReturnFalse();

	[Fact]
	public Task RecordCancellationAsync_NonExistent_ShouldReturnFalse_Test() =>
		RecordCancellationAsync_NonExistent_ShouldReturnFalse();

	#endregion Cancellation

	#region Scheduling — the predicates a background eraser depends on

	[Fact]
	public Task GetScheduledRequestsAsync_ShouldReturnDueRequests_Test() =>
		GetScheduledRequestsAsync_ShouldReturnDueRequests();

	[Fact]
	public Task GetScheduledRequestsAsync_ShouldOrderByScheduledTime_Test() =>
		GetScheduledRequestsAsync_ShouldOrderByScheduledTime();

	#endregion Scheduling

	#region Query

	[Fact]
	public Task ListRequestsAsync_WithStatusFilter_ShouldFilterByStatus_Test() =>
		ListRequestsAsync_WithStatusFilter_ShouldFilterByStatus();

	[Fact]
	public Task ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant_Test() =>
		ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant();

	[Fact]
	public Task ListRequestsAsync_WithDateRange_ShouldFilterByDates_Test() =>
		ListRequestsAsync_WithDateRange_ShouldFilterByDates();

	#endregion Query

	#region Certificates — the erasure evidence a consumer shows an auditor

	[Fact]
	public Task SaveCertificateAsync_ShouldPersistCertificate_Test() =>
		SaveCertificateAsync_ShouldPersistCertificate();

	[Fact]
	public Task SaveCertificateAsync_DuplicateId_ShouldThrowDuplicateErasureCertificateException_Test() =>
		SaveCertificateAsync_DuplicateId_ShouldThrowDuplicateErasureCertificateException();

	[Fact]
	public Task GetCertificateAsync_ByRequestId_ShouldReturnCertificate_Test() =>
		GetCertificateAsync_ByRequestId_ShouldReturnCertificate();

	[Fact]
	public Task GetCertificateByIdAsync_ShouldReturnCertificate_Test() =>
		GetCertificateByIdAsync_ShouldReturnCertificate();

	[Fact]
	public Task CleanupExpiredCertificatesAsync_ShouldRemoveExpired_Test() =>
		CleanupExpiredCertificatesAsync_ShouldRemoveExpired();

	[Fact]
	public Task CleanupExpiredCertificatesAsync_ShouldKeepValid_Test() =>
		CleanupExpiredCertificatesAsync_ShouldKeepValid();

	#endregion Certificates
}
