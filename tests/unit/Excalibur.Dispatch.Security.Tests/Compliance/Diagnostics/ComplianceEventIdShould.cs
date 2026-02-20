// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Diagnostics;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ComplianceEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
[Trait("Priority", "0")]
public sealed class ComplianceEventIdShould : UnitTestBase
{
	#region Data Retention Event ID Tests (92000-92099)

	[Fact]
	public void HaveRetentionPolicyServiceCreatedInRetentionRange()
	{
		ComplianceEventId.RetentionPolicyServiceCreated.ShouldBe(92000);
	}

	[Fact]
	public void HaveRetentionPolicyEvaluatedInRetentionRange()
	{
		ComplianceEventId.RetentionPolicyEvaluated.ShouldBe(92001);
	}

	[Fact]
	public void HaveRetentionPeriodExpiredInRetentionRange()
	{
		ComplianceEventId.RetentionPeriodExpired.ShouldBe(92002);
	}

	[Fact]
	public void HaveDataPurgeScheduledInRetentionRange()
	{
		ComplianceEventId.DataPurgeScheduled.ShouldBe(92003);
	}

	[Fact]
	public void HaveDataPurgeCompletedInRetentionRange()
	{
		ComplianceEventId.DataPurgeCompleted.ShouldBe(92004);
	}

	[Fact]
	public void HaveRetentionExceptionAppliedInRetentionRange()
	{
		ComplianceEventId.RetentionExceptionApplied.ShouldBe(92005);
	}

	[Fact]
	public void HaveAllRetentionEventIdsInExpectedRange()
	{
		ComplianceEventId.RetentionPolicyServiceCreated.ShouldBeInRange(92000, 92099);
		ComplianceEventId.RetentionPolicyEvaluated.ShouldBeInRange(92000, 92099);
		ComplianceEventId.RetentionPeriodExpired.ShouldBeInRange(92000, 92099);
		ComplianceEventId.DataPurgeScheduled.ShouldBeInRange(92000, 92099);
		ComplianceEventId.DataPurgeCompleted.ShouldBeInRange(92000, 92099);
		ComplianceEventId.RetentionExceptionApplied.ShouldBeInRange(92000, 92099);
	}

	#endregion

	#region Data Sovereignty Event ID Tests (92100-92199)

	[Fact]
	public void HaveDataSovereigntyValidatorCreatedInSovereigntyRange()
	{
		ComplianceEventId.DataSovereigntyValidatorCreated.ShouldBe(92100);
	}

	[Fact]
	public void HaveDataResidencyValidatedInSovereigntyRange()
	{
		ComplianceEventId.DataResidencyValidated.ShouldBe(92101);
	}

	[Fact]
	public void HaveDataResidencyViolationInSovereigntyRange()
	{
		ComplianceEventId.DataResidencyViolation.ShouldBe(92102);
	}

	[Fact]
	public void HaveCrossBorderTransferBlockedInSovereigntyRange()
	{
		ComplianceEventId.CrossBorderTransferBlocked.ShouldBe(92103);
	}

	[Fact]
	public void HaveRegionRoutingAppliedInSovereigntyRange()
	{
		ComplianceEventId.RegionRoutingApplied.ShouldBe(92104);
	}

	[Fact]
	public void HaveAllSovereigntyEventIdsInExpectedRange()
	{
		ComplianceEventId.DataSovereigntyValidatorCreated.ShouldBeInRange(92100, 92199);
		ComplianceEventId.DataResidencyValidated.ShouldBeInRange(92100, 92199);
		ComplianceEventId.DataResidencyViolation.ShouldBeInRange(92100, 92199);
		ComplianceEventId.CrossBorderTransferBlocked.ShouldBeInRange(92100, 92199);
		ComplianceEventId.RegionRoutingApplied.ShouldBeInRange(92100, 92199);
	}

	#endregion

	#region Field-Level Encryption Event ID Tests (92200-92299)

	[Fact]
	public void HaveFieldEncryptionServiceCreatedInFieldEncryptionRange()
	{
		ComplianceEventId.FieldEncryptionServiceCreated.ShouldBe(92200);
	}

	[Fact]
	public void HaveFieldEncryptedInFieldEncryptionRange()
	{
		ComplianceEventId.FieldEncrypted.ShouldBe(92201);
	}

	[Fact]
	public void HaveFieldDecryptedInFieldEncryptionRange()
	{
		ComplianceEventId.FieldDecrypted.ShouldBe(92202);
	}

	[Fact]
	public void HaveFieldEncryptionFailedInFieldEncryptionRange()
	{
		ComplianceEventId.FieldEncryptionFailed.ShouldBe(92203);
	}

	[Fact]
	public void HaveFieldEncryptionKeyRotatedInFieldEncryptionRange()
	{
		ComplianceEventId.FieldEncryptionKeyRotated.ShouldBe(92204);
	}

	[Fact]
	public void HaveAllFieldEncryptionEventIdsInExpectedRange()
	{
		ComplianceEventId.FieldEncryptionServiceCreated.ShouldBeInRange(92200, 92299);
		ComplianceEventId.FieldEncrypted.ShouldBeInRange(92200, 92299);
		ComplianceEventId.FieldDecrypted.ShouldBeInRange(92200, 92299);
		ComplianceEventId.FieldEncryptionFailed.ShouldBeInRange(92200, 92299);
		ComplianceEventId.FieldEncryptionKeyRotated.ShouldBeInRange(92200, 92299);
		ComplianceEventId.EncryptionMigrationBatchCompleted.ShouldBeInRange(92200, 92299);
	}

	#endregion

	#region Compliance Validation Event ID Tests (92300-92399)

	[Fact]
	public void HaveComplianceValidationExecutingInValidationRange()
	{
		ComplianceEventId.ComplianceValidationExecuting.ShouldBe(92300);
	}

	[Fact]
	public void HaveComplianceCheckPassedInValidationRange()
	{
		ComplianceEventId.ComplianceCheckPassed.ShouldBe(92301);
	}

	[Fact]
	public void HaveComplianceCheckFailedInValidationRange()
	{
		ComplianceEventId.ComplianceCheckFailed.ShouldBe(92302);
	}

	[Fact]
	public void HavePiiDetectedInValidationRange()
	{
		ComplianceEventId.PiiDetected.ShouldBe(92303);
	}

	[Fact]
	public void HavePiiMaskedInValidationRange()
	{
		ComplianceEventId.PiiMasked.ShouldBe(92304);
	}

	[Fact]
	public void HaveComplianceRuleEvaluatedInValidationRange()
	{
		ComplianceEventId.ComplianceRuleEvaluated.ShouldBe(92305);
	}

	[Fact]
	public void HaveAllValidationEventIdsInExpectedRange()
	{
		ComplianceEventId.ComplianceValidationExecuting.ShouldBeInRange(92300, 92399);
		ComplianceEventId.ComplianceCheckPassed.ShouldBeInRange(92300, 92399);
		ComplianceEventId.ComplianceCheckFailed.ShouldBeInRange(92300, 92399);
		ComplianceEventId.PiiDetected.ShouldBeInRange(92300, 92399);
		ComplianceEventId.PiiMasked.ShouldBeInRange(92300, 92399);
		ComplianceEventId.ComplianceRuleEvaluated.ShouldBeInRange(92300, 92399);
	}

	#endregion

	#region Regulatory Reporting Event ID Tests (92400-92499)

	[Fact]
	public void HaveRegulatoryReportGeneratorCreatedInReportingRange()
	{
		ComplianceEventId.RegulatoryReportGeneratorCreated.ShouldBe(92400);
	}

	[Fact]
	public void HaveRegulatoryReportGeneratedInReportingRange()
	{
		ComplianceEventId.RegulatoryReportGenerated.ShouldBe(92401);
	}

	[Fact]
	public void HaveAuditTrailExportedInReportingRange()
	{
		ComplianceEventId.AuditTrailExported.ShouldBe(92402);
	}

	[Fact]
	public void HaveComplianceCertificateGeneratedInReportingRange()
	{
		ComplianceEventId.ComplianceCertificateGenerated.ShouldBe(92403);
	}

	[Fact]
	public void HaveRegulatorySubmissionCompletedInReportingRange()
	{
		ComplianceEventId.RegulatorySubmissionCompleted.ShouldBe(92404);
	}

	[Fact]
	public void HaveAllReportingEventIdsInExpectedRange()
	{
		ComplianceEventId.RegulatoryReportGeneratorCreated.ShouldBeInRange(92400, 92499);
		ComplianceEventId.RegulatoryReportGenerated.ShouldBeInRange(92400, 92499);
		ComplianceEventId.AuditTrailExported.ShouldBeInRange(92400, 92499);
		ComplianceEventId.ComplianceCertificateGenerated.ShouldBeInRange(92400, 92499);
		ComplianceEventId.RegulatorySubmissionCompleted.ShouldBeInRange(92400, 92499);
	}

	#endregion

	#region Key Management Event ID Tests (92500-92599)

	[Fact]
	public void HaveKeyManagementServiceCreatedInKeyManagementRange()
	{
		ComplianceEventId.KeyManagementServiceCreated.ShouldBe(92500);
	}

	[Fact]
	public void HaveEncryptionKeyCreatedInKeyManagementRange()
	{
		ComplianceEventId.EncryptionKeyCreated.ShouldBe(92501);
	}

	[Fact]
	public void HaveEncryptionKeyRotatedInKeyManagementRange()
	{
		ComplianceEventId.EncryptionKeyRotated.ShouldBe(92502);
	}

	[Fact]
	public void HaveEncryptionKeyRevokedInKeyManagementRange()
	{
		ComplianceEventId.EncryptionKeyRevoked.ShouldBe(92503);
	}

	[Fact]
	public void HaveKeyAccessLoggedInKeyManagementRange()
	{
		ComplianceEventId.KeyAccessLogged.ShouldBe(92504);
	}

	[Fact]
	public void HaveAllKeyManagementEventIdsInExpectedRange()
	{
		ComplianceEventId.KeyManagementServiceCreated.ShouldBeInRange(92500, 92599);
		ComplianceEventId.EncryptionKeyCreated.ShouldBeInRange(92500, 92599);
		ComplianceEventId.EncryptionKeyRotated.ShouldBeInRange(92500, 92599);
		ComplianceEventId.EncryptionKeyRevoked.ShouldBeInRange(92500, 92599);
		ComplianceEventId.KeyAccessLogged.ShouldBeInRange(92500, 92599);
		ComplianceEventId.KeyRotationForceFailed.ShouldBeInRange(92500, 92599);
	}

	#endregion

	#region Cloud Provider Compliance Event ID Tests (92600-92699)

	[Fact]
	public void HaveCloudComplianceAdapterCreatedInCloudRange()
	{
		ComplianceEventId.CloudComplianceAdapterCreated.ShouldBe(92600);
	}

	[Fact]
	public void HaveAwsComplianceCheckCompletedInCloudRange()
	{
		ComplianceEventId.AwsComplianceCheckCompleted.ShouldBe(92601);
	}

	[Fact]
	public void HaveAzureComplianceCheckCompletedInCloudRange()
	{
		ComplianceEventId.AzureComplianceCheckCompleted.ShouldBe(92602);
	}

	[Fact]
	public void HaveVaultIntegrationConfiguredInCloudRange()
	{
		ComplianceEventId.VaultIntegrationConfigured.ShouldBe(92603);
	}

	[Fact]
	public void HaveCloudKmsConfiguredInCloudRange()
	{
		ComplianceEventId.CloudKmsConfigured.ShouldBe(92604);
	}

	[Fact]
	public void HaveAllCloudEventIdsInExpectedRange()
	{
		ComplianceEventId.CloudComplianceAdapterCreated.ShouldBeInRange(92600, 92699);
		ComplianceEventId.AwsComplianceCheckCompleted.ShouldBeInRange(92600, 92699);
		ComplianceEventId.AzureComplianceCheckCompleted.ShouldBeInRange(92600, 92699);
		ComplianceEventId.VaultIntegrationConfigured.ShouldBeInRange(92600, 92699);
		ComplianceEventId.CloudKmsConfigured.ShouldBeInRange(92600, 92699);
	}

	#endregion

	#region Erasure & Legal Hold Event ID Tests (92700-92799)

	[Fact]
	public void HaveErasureRequestProcessingInErasureRange()
	{
		ComplianceEventId.ErasureRequestProcessing.ShouldBe(92700);
	}

	[Fact]
	public void HaveErasureBlockedByLegalHoldInErasureRange()
	{
		ComplianceEventId.ErasureBlockedByLegalHold.ShouldBe(92701);
	}

	[Fact]
	public void HaveErasureScheduledInErasureRange()
	{
		ComplianceEventId.ErasureScheduled.ShouldBe(92702);
	}

	[Fact]
	public void HaveLegalHoldCreatedInErasureRange()
	{
		ComplianceEventId.LegalHoldCreated.ShouldBe(92790);
	}

	[Fact]
	public void HaveLegalHoldReleasedInErasureRange()
	{
		ComplianceEventId.LegalHoldReleased.ShouldBe(92791);
	}

	[Fact]
	public void HaveAllErasureEventIdsInExpectedRange()
	{
		ComplianceEventId.ErasureRequestProcessing.ShouldBeInRange(92700, 92799);
		ComplianceEventId.ErasureBlockedByLegalHold.ShouldBeInRange(92700, 92799);
		ComplianceEventId.ErasureScheduled.ShouldBeInRange(92700, 92799);
		ComplianceEventId.LegalHoldCreated.ShouldBeInRange(92700, 92799);
		ComplianceEventId.LegalHoldReleased.ShouldBeInRange(92700, 92799);
		ComplianceEventId.LegalHoldAutoReleaseCompleted.ShouldBeInRange(92700, 92799);
	}

	#endregion

	#region Compliance Monitoring & Alerts Event ID Tests (92800-92899)

	[Fact]
	public void HaveComplianceGapAlertCriticalInMonitoringRange()
	{
		ComplianceEventId.ComplianceGapAlertCritical.ShouldBe(92800);
	}

	[Fact]
	public void HaveComplianceGapAlertHighInMonitoringRange()
	{
		ComplianceEventId.ComplianceGapAlertHigh.ShouldBe(92801);
	}

	[Fact]
	public void HaveComplianceGapAlertMediumInMonitoringRange()
	{
		ComplianceEventId.ComplianceGapAlertMedium.ShouldBe(92802);
	}

	[Fact]
	public void HaveComplianceGapAlertLowInMonitoringRange()
	{
		ComplianceEventId.ComplianceGapAlertLow.ShouldBe(92803);
	}

	[Fact]
	public void HaveComplianceRestoredInMonitoringRange()
	{
		ComplianceEventId.ComplianceRestored.ShouldBe(92820);
	}

	[Fact]
	public void HaveComplianceLostInMonitoringRange()
	{
		ComplianceEventId.ComplianceLost.ShouldBe(92821);
	}

	[Fact]
	public void HaveAllMonitoringEventIdsInExpectedRange()
	{
		ComplianceEventId.ComplianceGapAlertCritical.ShouldBeInRange(92800, 92899);
		ComplianceEventId.ComplianceGapAlertHigh.ShouldBeInRange(92800, 92899);
		ComplianceEventId.ComplianceGapAlertMedium.ShouldBeInRange(92800, 92899);
		ComplianceEventId.ComplianceGapAlertLow.ShouldBeInRange(92800, 92899);
		ComplianceEventId.ComplianceRestored.ShouldBeInRange(92800, 92899);
		ComplianceEventId.ComplianceLost.ShouldBeInRange(92800, 92899);
	}

	#endregion

	#region Compliance Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInComplianceReservedRange()
	{
		// Compliance reserved range is 92000-92999
		var allEventIds = GetAllComplianceEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(92000, 92999,
				$"Event ID {eventId} is outside Compliance reserved range (92000-92999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllComplianceEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllComplianceEventIds();

		// Verify a substantial portion of ComplianceEventId constants are covered
		// Full file has 141 event IDs; test covers the main categories
		allEventIds.Length.ShouldBeGreaterThan(100);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllComplianceEventIds()
	{
		return
		[
			// Data Retention (92000-92099)
			ComplianceEventId.RetentionPolicyServiceCreated,
			ComplianceEventId.RetentionPolicyEvaluated,
			ComplianceEventId.RetentionPeriodExpired,
			ComplianceEventId.DataPurgeScheduled,
			ComplianceEventId.DataPurgeCompleted,
			ComplianceEventId.RetentionExceptionApplied,

			// Data Sovereignty (92100-92199)
			ComplianceEventId.DataSovereigntyValidatorCreated,
			ComplianceEventId.DataResidencyValidated,
			ComplianceEventId.DataResidencyViolation,
			ComplianceEventId.CrossBorderTransferBlocked,
			ComplianceEventId.RegionRoutingApplied,

			// Field-Level Encryption (92200-92299)
			ComplianceEventId.FieldEncryptionServiceCreated,
			ComplianceEventId.FieldEncrypted,
			ComplianceEventId.FieldDecrypted,
			ComplianceEventId.FieldEncryptionFailed,
			ComplianceEventId.FieldEncryptionKeyRotated,
			ComplianceEventId.BulkDecryptionCompleted,
			ComplianceEventId.DecryptionErrorForField,
			ComplianceEventId.ExportCompleted,
			ComplianceEventId.ReEncryptionSucceeded,
			ComplianceEventId.ReEncryptionFailed,
			ComplianceEventId.ReEncryptionFailedForItem,
			ComplianceEventId.BatchReEncryptionCompleted,
			ComplianceEventId.EncryptionMigrationSucceeded,
			ComplianceEventId.EncryptionMigrationFailed,
			ComplianceEventId.EncryptionHealthCheckFailed,
			ComplianceEventId.EncryptionHealthCheckDegraded,
			ComplianceEventId.EncryptionHealthCheckPassed,
			ComplianceEventId.EncryptionHealthCheckRoundTripFailed,
			ComplianceEventId.EncryptionHealthCheckKeyManagementFailed,
			ComplianceEventId.EncryptionMigrationBatchCompleted,

			// Compliance Validation (92300-92399)
			ComplianceEventId.ComplianceValidationExecuting,
			ComplianceEventId.ComplianceCheckPassed,
			ComplianceEventId.ComplianceCheckFailed,
			ComplianceEventId.PiiDetected,
			ComplianceEventId.PiiMasked,
			ComplianceEventId.ComplianceRuleEvaluated,

			// Regulatory Reporting (92400-92499)
			ComplianceEventId.RegulatoryReportGeneratorCreated,
			ComplianceEventId.RegulatoryReportGenerated,
			ComplianceEventId.AuditTrailExported,
			ComplianceEventId.ComplianceCertificateGenerated,
			ComplianceEventId.RegulatorySubmissionCompleted,

			// Key Management (92500-92599)
			ComplianceEventId.KeyManagementServiceCreated,
			ComplianceEventId.EncryptionKeyCreated,
			ComplianceEventId.EncryptionKeyRotated,
			ComplianceEventId.EncryptionKeyRevoked,
			ComplianceEventId.KeyAccessLogged,
			ComplianceEventId.DevEncryptionWarning,
			ComplianceEventId.KeyRotationFailureReported,
			ComplianceEventId.KeyRotationSuccessReported,
			ComplianceEventId.KeyExpirationWarningReported,
			ComplianceEventId.KeyRotationAlertNotifyFailed,
			ComplianceEventId.KeyRotationFailureAlertCriticalLogged,
			ComplianceEventId.KeyRotationFailureAlertHighLogged,
			ComplianceEventId.KeyRotationFailureAlertMediumLogged,
			ComplianceEventId.KeyRotationFailureAlertLowLogged,
			ComplianceEventId.KeyExpirationWarningAlertCriticalLogged,
			ComplianceEventId.KeyExpirationWarningAlertHighLogged,
			ComplianceEventId.KeyExpirationWarningAlertMediumLogged,
			ComplianceEventId.KeyExpirationWarningAlertLowLogged,
			ComplianceEventId.KeyRotationSuccessAlertLogged,
			ComplianceEventId.KeyRotationServiceDisabled,
			ComplianceEventId.KeyRotationServiceStarted,
			ComplianceEventId.KeyRotationCheckCompleted,
			ComplianceEventId.KeyRotationCheckNoKeys,
			ComplianceEventId.KeyRotationCheckError,
			ComplianceEventId.KeyRotationServiceStopped,
			ComplianceEventId.KeyRotationApproaching,
			ComplianceEventId.KeyRotationRetryDelayed,
			ComplianceEventId.KeyRotationStarted,
			ComplianceEventId.KeyRotationSucceeded,
			ComplianceEventId.KeyRotationFailed,
			ComplianceEventId.KeyRotationException,
			ComplianceEventId.KeyRotationForceMissingKey,
			ComplianceEventId.KeyRotationForceStarted,
			ComplianceEventId.KeyRotationForceSucceeded,
			ComplianceEventId.KeyRotationForceFailed,

			// Cloud Provider Compliance (92600-92699)
			ComplianceEventId.CloudComplianceAdapterCreated,
			ComplianceEventId.AwsComplianceCheckCompleted,
			ComplianceEventId.AzureComplianceCheckCompleted,
			ComplianceEventId.VaultIntegrationConfigured,
			ComplianceEventId.CloudKmsConfigured,

			// Erasure & Legal Hold (92700-92799)
			ComplianceEventId.ErasureRequestProcessing,
			ComplianceEventId.ErasureBlockedByLegalHold,
			ComplianceEventId.ErasureScheduled,
			ComplianceEventId.ErasureRequestFailed,
			ComplianceEventId.ErasureCancellationNotFound,
			ComplianceEventId.ErasureCancellationNotAllowed,
			ComplianceEventId.ErasureCancelled,
			ComplianceEventId.ErasureCertificateGenerated,
			ComplianceEventId.ErasureKeyDeletionFailed,
			ComplianceEventId.ErasureRequestCompleted,
			ComplianceEventId.ErasureExecutionFailed,
			ComplianceEventId.ErasureGracePeriodBelowMinimum,
			ComplianceEventId.ErasureGracePeriodExceedsMaximum,
			ComplianceEventId.ErasureVerificationStarted,
			ComplianceEventId.ErasureVerificationFailed,
			ComplianceEventId.ErasureVerificationPassed,
			ComplianceEventId.ErasureVerificationError,
			ComplianceEventId.ErasureKeyDeletionVerificationStarted,
			ComplianceEventId.ErasureKeyDeletionConfirmedNotFound,
			ComplianceEventId.ErasureKeyDeletionConfirmedStatus,
			ComplianceEventId.ErasureKeyDeletionNotDeleted,
			ComplianceEventId.ErasureKeyDeletionConfirmedException,
			ComplianceEventId.ErasureKeyDeletionError,
			ComplianceEventId.ErasureKeyDeletionExpectedError,
			ComplianceEventId.ErasureSchedulerDisabled,
			ComplianceEventId.ErasureSchedulerStarting,
			ComplianceEventId.ErasureSchedulerProcessingError,
			ComplianceEventId.ErasureSchedulerStopped,
			ComplianceEventId.ErasureSchedulerNoScheduledRequests,
			ComplianceEventId.ErasureSchedulerProcessingBatch,
			ComplianceEventId.ErasureSchedulerExecutingRequest,
			ComplianceEventId.ErasureSchedulerRequestCompleted,
			ComplianceEventId.ErasureSchedulerRequestFailed,
			ComplianceEventId.ErasureSchedulerExecutionError,
			ComplianceEventId.ErasureSchedulerMarkedFailed,
			ComplianceEventId.ErasureSchedulerCertificatesCleaned,
			ComplianceEventId.ErasureSchedulerCertificateCleanupFailed,
			ComplianceEventId.DataInventoryDiscoveryStarted,
			ComplianceEventId.DataInventoryKeyInfoFailed,
			ComplianceEventId.DataInventoryDiscoveryCompleted,
			ComplianceEventId.DataInventoryRegistrationAdded,
			ComplianceEventId.DataInventoryRegistrationRemoved,
			ComplianceEventId.DataInventoryRegistrationNotFound,
			ComplianceEventId.LegalHoldCreated,
			ComplianceEventId.LegalHoldReleased,
			ComplianceEventId.LegalHoldCheckCompleted,
			ComplianceEventId.LegalHoldAutoReleaseFailed,
			ComplianceEventId.LegalHoldAutoReleaseCompleted,

			// Compliance Monitoring & Alerts (92800-92899)
			ComplianceEventId.ComplianceGapAlertCritical,
			ComplianceEventId.ComplianceGapAlertHigh,
			ComplianceEventId.ComplianceGapAlertMedium,
			ComplianceEventId.ComplianceGapAlertLow,
			ComplianceEventId.ComplianceGapRemediationGuidance,
			ComplianceEventId.ControlValidationFailureCritical,
			ComplianceEventId.ControlValidationFailureHigh,
			ComplianceEventId.ControlValidationFailureMedium,
			ComplianceEventId.ControlValidationFailureLow,
			ComplianceEventId.ComplianceRestored,
			ComplianceEventId.ComplianceLost
		];
	}

	#endregion
}
