// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryErasureStore"/> validating IErasureStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryErasureStore uses instance-level ConcurrentDictionary collections with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> IErasureStore implements GDPR Article 17 "Right to Erasure".
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>SaveRequestAsync THROWS DuplicateErasureRequestException on duplicate RequestId</description></item>
/// <item><description>DataSubjectId automatically SHA256-hashed for privacy</description></item>
/// <item><description>STATE MACHINE: RecordCancellationAsync only works for Pending/Scheduled status</description></item>
/// <item><description>RecordCompletionAsync THROWS KeyNotFoundException if request not found</description></item>
/// <item><description>SaveCertificateAsync THROWS DuplicateErasureCertificateException on duplicate CertificateId</description></item>
/// <item><description>UpdateStatusAsync sets ExecutedAt when status changes to InProgress</description></item>
/// <item><description>GetScheduledRequestsAsync returns requests where ScheduledExecutionAt &lt;= now</description></item>
/// <item><description>CleanupExpiredCertificatesAsync removes where RetainUntil &lt; now</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "STORE")]
public sealed class InMemoryErasureStoreConformanceTests : ErasureStoreConformanceTestKit
{

	/// <summary>
	/// Exposes the kit's own wiring check to the runner. The check is an arm like any other, so a
	/// suite that omits THIS member disables it silently -- the one gap it cannot report itself.
	/// </summary>
	/// <returns>A completed task when every arm in the kit is wired.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
		ConformanceSuite_ShouldWireEveryArm();

	/// <inheritdoc />
	protected override IErasureStore CreateStore() => CreateStore(requireTenant: false);

	/// <inheritdoc />
	/// <remarks>
	/// The same store, with tenancy REQUIRED. One arm needs it: an ambient-tenant term copied from a
	/// neighbouring statement is guarded by that flag, so under optional tenancy the term compiles to
	/// nothing and an arm cannot tell the defect from the fix.
	/// </remarks>
	protected override IErasureStore? CreateTenantRequiringStore() => CreateStore(requireTenant: true);

	private static InMemoryErasureStore CreateStore(bool requireTenant) => new(
		TestDataSubjectHasher.Instance,
		TenantContextHolder.AmbientContext,
		Options.Create(new TenantContextOptions { RequireTenant = requireTenant }), TimeProvider.System);

	#region Request Lifecycle Tests

	[Fact]
	public Task SaveRequestAsync_ShouldPersistRequest_Test() =>
		SaveRequestAsync_ShouldPersistRequest();

	[Fact]
	public Task SaveRequestAsync_DuplicateId_ShouldThrowDuplicateErasureRequestException_Test() =>
		SaveRequestAsync_DuplicateId_ShouldThrowDuplicateErasureRequestException();

	[Fact]
	public Task SaveRequestAsync_NonDuplicateFailure_ShouldNotTranslateToDuplicate_Test() =>
		SaveRequestAsync_NonDuplicateFailure_ShouldNotTranslateToDuplicate();

	[Fact]
	public Task SaveRequestAsync_ShouldHashDataSubjectId_Test() =>
		SaveRequestAsync_ShouldHashDataSubjectId();

	[Fact]
	public Task GetStatusAsync_NonExistent_ShouldReturnNull_Test() =>
		GetStatusAsync_NonExistent_ShouldReturnNull();

	#endregion Request Lifecycle Tests

	#region Status Update Tests

	[Fact]
	public Task UpdateStatusAsync_ShouldUpdateStatus_Test() =>
		UpdateStatusAsync_ShouldUpdateStatus();

	[Fact]
	public Task ClaimForExecutionAsync_ShouldSetExecutedAt_Test() =>
		ClaimForExecutionAsync_ShouldSetExecutedAt();

	[Fact]
	public Task UpdateStatusAsync_NonExistent_ShouldReturnFalse_Test() =>
		UpdateStatusAsync_NonExistent_ShouldReturnFalse();

	[Fact]
	public Task RecordCompletionAsync_PresentingASupersededClaim_ShouldBeRefused_Test() =>
		RecordCompletionAsync_PresentingASupersededClaim_ShouldBeRefused();

	[Fact]
	public Task RecordCompletionAsync_FromALapsedButUnsupersededHolder_ShouldSucceed_Test() =>
		RecordCompletionAsync_FromALapsedButUnsupersededHolder_ShouldSucceed();

	[Fact]
	public Task UpdateStatusAsync_ALegalHoldUnderALiveClaim_ShouldBeAdmitted_Test() =>
		UpdateStatusAsync_ALegalHoldUnderALiveClaim_ShouldBeAdmitted();

	[Fact]
	public Task ClaimForExecutionAsync_SecondClaimOfTheSameRequest_ShouldBeRefused_Test() =>
		ClaimForExecutionAsync_SecondClaimOfTheSameRequest_ShouldBeRefused();

	[Fact]
	public Task UpdateStatusAsync_TerminalTransitionAfterClaim_ShouldSucceed_Test() =>
		UpdateStatusAsync_TerminalTransitionAfterClaim_ShouldSucceed();

	[Fact]
	public Task UpdateStatusAsync_ResetToScheduledOfAClaimedRequest_ShouldBeRefused_Test() =>
		UpdateStatusAsync_ResetToScheduledOfAClaimedRequest_ShouldBeRefused();

	[Fact]
	public Task UpdateStatusAsync_ResetToScheduledOfAFailedRequest_ShouldSucceed_Test() =>
		UpdateStatusAsync_ResetToScheduledOfAFailedRequest_ShouldSucceed();

	[Fact]
	public Task UpdateStatusAsync_ResetToAwaitingKeyDestructionOfACancelledRequest_ShouldBeRefused_Test() =>
		UpdateStatusAsync_ResetToAwaitingKeyDestructionOfACancelledRequest_ShouldBeRefused();

	[Fact]
	public Task UpdateStatusAsync_AwaitingKeyDestructionFromTheClaimedWindow_ShouldSucceed_Test() =>
		UpdateStatusAsync_AwaitingKeyDestructionFromTheClaimedWindow_ShouldSucceed();

	[Fact]
	public Task AwaitingKeyDestruction_ShouldBeListable_NotRescheduled_AndNotCancellable_Test() =>
		AwaitingKeyDestruction_ShouldBeListable_NotRescheduled_AndNotCancellable();

	[Fact]
	public Task BackgroundDiscovery_AcrossOwners_ShouldReturnEveryRowOfItsOwnStatusUnscoped_Test() =>
		BackgroundDiscovery_AcrossOwners_ShouldReturnEveryRowOfItsOwnStatusUnscoped();

	#endregion Status Update Tests

	#region Completion Tests

	[Fact]
	public Task RecordCompletionAsync_ShouldMarkCompleted_Test() =>
		RecordCompletionAsync_ShouldMarkCompleted();

	[Fact]
	public Task RecordCompletionAsync_NonExistent_ShouldThrowKeyNotFoundException_Test() =>
		RecordCompletionAsync_NonExistent_ShouldThrowKeyNotFoundException();

	[Fact]
	public Task RecordKeyDestroyedAsync_ShouldAppendIdempotentlyAndSurvive_Test() =>
		RecordKeyDestroyedAsync_ShouldAppendIdempotentlyAndSurvive();

	[Fact]
	public Task RecordKeyDestroyedAsync_NonExistentRequest_ShouldThrowKeyNotFoundException_Test() =>
		RecordKeyDestroyedAsync_NonExistentRequest_ShouldThrowKeyNotFoundException();

	[Fact]
	public Task RecordKeyDestroyedAsync_ShouldTreatHandlesDifferingOnlyInCaseAsDistinct_Test() =>
		RecordKeyDestroyedAsync_ShouldTreatHandlesDifferingOnlyInCaseAsDistinct();

	[Fact]
	public Task RecordKeyDestroyedAsync_TwoGenerationsAtOneHandle_ShouldRecordBoth_Test() =>
		RecordKeyDestroyedAsync_TwoGenerationsAtOneHandle_ShouldRecordBoth();
	[Fact]
	public Task IsGenerationDestroyedAsync_ShouldNotReportOneHandlesDestructionForAnother_Test() =>
		IsGenerationDestroyedAsync_ShouldNotReportOneHandlesDestructionForAnother();


	[Fact]
	public Task StageKeyDestructionAsync_ShouldNotReportTheGenerationAsDestroyed_Test() =>
		StageKeyDestructionAsync_ShouldNotReportTheGenerationAsDestroyed();

	[Fact]
	public Task RecordKeyDestroyedAsync_ShouldClearOnlyTheRecordedIntent_Test() =>
		RecordKeyDestroyedAsync_ShouldClearOnlyTheRecordedIntent();

	#endregion Completion Tests

	#region Cancellation Tests (STATE MACHINE)

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

	#endregion Cancellation Tests (STATE MACHINE)

	#region Scheduled Query Tests

	[Fact]
	public Task GetScheduledRequestsAsync_ShouldReturnDueRequests_Test() =>
		GetScheduledRequestsAsync_ShouldReturnDueRequests();

	[Fact]
	public Task GetScheduledRequestsAsync_ShouldOrderByScheduledTime_Test() =>
		GetScheduledRequestsAsync_ShouldOrderByScheduledTime();

	#endregion Scheduled Query Tests

	#region List Query Tests

	[Fact]
	public Task ListRequestsAsync_WithStatusFilter_ShouldFilterByStatus_Test() =>
		ListRequestsAsync_WithStatusFilter_ShouldFilterByStatus();

	/// <inheritdoc />
	/// <remarks>
	/// The store under test resolves its owner from the ambient tenant context this suite supplies, so a
	/// scope over that context is all the cross-owner arms need -- no second store and no second host.
	/// Outside any scope the ambient context resolves the untenanted partition, which is the term every
	/// other arm here already ran under, so supplying this changes nothing for them.
	/// </remarks>
	protected override IDisposable? BeginOwnerScope(string tenantId) =>
		TenantContextHolder.BeginScope(tenantId);

	[Fact]
	public Task ListRequestsAsync_AcrossOwners_ShouldNotReturnAnotherOwnersRequest_Test() =>
		ListRequestsAsync_AcrossOwners_ShouldNotReturnAnotherOwnersRequest();

	[Fact]
	public Task ListRequestsAsync_TenantFilterIsOrdinal_ShouldNotFoldCaseOrPadding_Test() =>
		ListRequestsAsync_TenantFilterIsOrdinal_ShouldNotFoldCaseOrPadding();

	[Fact]
	public Task ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant_Test() =>
		ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant();

	[Fact]
	public Task ListRequestsAsync_WithDateRange_ShouldFilterByDates_Test() =>
		ListRequestsAsync_WithDateRange_ShouldFilterByDates();

	#endregion List Query Tests

	#region Certificate Tests

	[Fact]
	public Task SaveCertificateAsync_ShouldPersistCertificate_Test() =>
		SaveCertificateAsync_ShouldPersistCertificate();

	[Fact]
	public Task GetCertificateAsync_WhenRequestHasTwoCertificates_ShouldReturnTheNewest_Test() =>
		GetCertificateAsync_WhenRequestHasTwoCertificates_ShouldReturnTheNewest();

	[Fact]
	public Task SaveCertificateAsync_ShouldRoundTripACertificateThatStillVerifies_Test() =>
		SaveCertificateAsync_ShouldRoundTripACertificateThatStillVerifies();

	[Fact]
	public Task SaveCertificateAsync_ShouldNotVerifyACertificateWhoseClaimsWereAltered_Test() =>
		SaveCertificateAsync_ShouldNotVerifyACertificateWhoseClaimsWereAltered();

	[Fact]
	public Task SaveCertificateAsync_DuplicateId_ShouldThrowDuplicateErasureCertificateException_Test() =>
		SaveCertificateAsync_DuplicateId_ShouldThrowDuplicateErasureCertificateException();

	[Fact]
	public Task GetCertificateAsync_ByRequestId_ShouldReturnCertificate_Test() =>
		GetCertificateAsync_ByRequestId_ShouldReturnCertificate();

	[Fact]
	public Task GetCertificateByIdAsync_ShouldReturnCertificate_Test() =>
		GetCertificateByIdAsync_ShouldReturnCertificate();

	#endregion Certificate Tests

	#region Cleanup Tests

	[Fact]
	public Task CleanupExpiredCertificatesAsync_ShouldRemoveExpired_Test() =>
		CleanupExpiredCertificatesAsync_ShouldRemoveExpired();

	[Fact]
	public Task CleanupExpiredCertificatesAsync_ShouldKeepValid_Test() =>
		CleanupExpiredCertificatesAsync_ShouldKeepValid();

	#endregion Cleanup Tests

	#region Mint Ledger Tests

	[Fact]
	public Task MintLedger_ShouldEnumerateEveryHandleRecordedForTheSubject_Test() =>
		MintLedger_ShouldEnumerateEveryHandleRecordedForTheSubject();

	[Fact]
	public Task MintLedger_ShouldNotEnumerateAnotherOwnersHandle_Test() =>
		MintLedger_ShouldNotEnumerateAnotherOwnersHandle();

	[Fact]
	public Task MintLedger_ShouldMatchTheOwnerOrdinally_Test() =>
		MintLedger_ShouldMatchTheOwnerOrdinally();

	[Fact]
	public Task MintLedger_RecordingTheSameHandleTwice_ShouldBeANoOp_Test() =>
		MintLedger_RecordingTheSameHandleTwice_ShouldBeANoOp();

	[Fact]
	public Task MintLedger_RerecordingAHandleUnderAnotherOwner_ShouldBeRefused_Test() =>
		MintLedger_RerecordingAHandleUnderAnotherOwner_ShouldBeRefused();

	[Fact]
	public Task MintLedger_UnknownSubject_ShouldEnumerateEmpty_Test() =>
		MintLedger_UnknownSubject_ShouldEnumerateEmpty();

	[Fact]
	public Task MintLedger_AnUnconfirmedHandle_ShouldBeReportedAsPending_Test() =>
		MintLedger_AnUnconfirmedHandle_ShouldBeReportedAsPending();

	[Fact]
	public Task MintLedger_ConfirmingAMint_ShouldBeIdempotentAndRefuseAnAbsentRow_Test() =>
		MintLedger_ConfirmingAMint_ShouldBeIdempotentAndRefuseAnAbsentRow();

	#endregion Mint Ledger Tests
}
