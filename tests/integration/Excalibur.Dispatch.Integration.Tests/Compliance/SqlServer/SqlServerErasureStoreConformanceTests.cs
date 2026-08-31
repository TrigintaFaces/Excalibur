// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch.Integration.Tests.Compliance;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Testing.Conformance;


namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Runs the shared erasure conformance kit against the REAL SqlServer store.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS EXISTS.</b> Before this class, no SQL erasure store on either engine derived from
/// <see cref="ErasureStoreConformanceTestKit"/> — all 24 arms ran against the in-memory store alone. The
/// store had unit tests and one tenant-isolation integration test, so the gap was not visible as an
/// absence of files; it was visible only as an absence of <i>derivations</i>, which is why a file count
/// did not surface it. Erasure is the right-to-be-forgotten path: a defect in its status transitions or
/// its certificate retention is the difference between honouring an erasure request and silently
/// dropping it, and nothing held this store to the shared contract.
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
/// arm becomes a visible omission in this file rather than an absence nobody can see. That is the
/// property which makes a published conformance count checkable rather than trusted.
/// </para>
/// <para>
/// <b>No hand-written DDL, deliberately.</b> The store self-initialises its schema
/// (<c>AutoCreateSchema</c>), so this fixture asks the production code to create its tables rather than
/// declaring a copy of them. A fixture that restates the schema can drift from the shipped one in either
/// direction: stale, and the suite fails loudly on a column it no longer has; <i>ahead</i>, and the suite
/// passes green against a schema no consumer will ever provision — concealing the very defect it was
/// written to catch.
/// </para>
/// <para>
/// <b>Per-instance tables, for determinism.</b> xUnit constructs one instance per test, so the suffix
/// below isolates every arm into its own pair of tables on the shared container. The arms key their
/// assertions on their own identifiers rather than on row counts, so they would mostly tolerate shared
/// state — but <c>GetScheduledRequestsAsync</c> reads a bounded page, and residue accumulated by earlier
/// arms could eventually push a due request past that bound. That failure would be order-dependent and
/// intermittent, which is the class of flake that gets a genuine red dismissed as noise.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class SqlServerErasureStoreConformanceTests : ErasureStoreConformanceTestKit
{

	/// <summary>
	/// Exposes the kit's own wiring check to the runner. The check is an arm like any other, so a
	/// suite that omits THIS member disables it silently -- the one gap it cannot report itself.
	/// </summary>
	/// <returns>A completed task when every arm in the kit is wired.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
		ConformanceSuite_ShouldWireEveryArm();

	private readonly SqlServerFixture _fixture;

	/// <summary>
	/// Isolates this instance's tables. One xUnit instance per test, so this is per-arm isolation.
	/// </summary>
	private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

	public SqlServerErasureStoreConformanceTests(SqlServerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override IErasureStore CreateStore()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RequestsTableName = $"ErasureRequests_{_suffix}",
			CertificatesTableName = $"ErasureCertificates_{_suffix}",
			CommandTimeoutSeconds = 30,

			// The store provisions its own tables. See the class remarks: a fixture-declared copy of the
			// schema is the drift hazard this avoids.
			AutoCreateSchema = true
		};

		// Fully qualified: this file's namespace makes a bare `Options` bind to Excalibur.Dispatch.Options.
		return new SqlServerErasureStore(
			Microsoft.Extensions.Options.Options.Create(options),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<SqlServerErasureStore>(),
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
