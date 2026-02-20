// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Diagnostics;

/// <summary>
/// Event IDs for compliance components (92000-92999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>92000-92099: Data Retention</item>
/// <item>92100-92199: Data Sovereignty</item>
/// <item>92200-92299: Field-Level Encryption</item>
/// <item>92300-92399: Compliance Validation</item>
/// <item>92400-92499: Regulatory Reporting</item>
/// <item>92500-92599: Key Management</item>
/// <item>92600-92699: Cloud Provider Compliance</item>
/// <item>92700-92799: Erasure &amp; Legal Hold</item>
/// <item>92800-92899: Compliance Monitoring &amp; Alerts</item>
/// </list>
/// </remarks>
public static class ComplianceEventId
{
	// ========================================
	// 92000-92099: Data Retention
	// ========================================

	/// <summary>Retention policy service created.</summary>
	public const int RetentionPolicyServiceCreated = 92000;

	/// <summary>Retention policy evaluated.</summary>
	public const int RetentionPolicyEvaluated = 92001;

	/// <summary>Retention period expired.</summary>
	public const int RetentionPeriodExpired = 92002;

	/// <summary>Data purge scheduled.</summary>
	public const int DataPurgeScheduled = 92003;

	/// <summary>Data purge completed.</summary>
	public const int DataPurgeCompleted = 92004;

	/// <summary>Retention exception applied.</summary>
	public const int RetentionExceptionApplied = 92005;

	// ========================================
	// 92100-92199: Data Sovereignty
	// ========================================

	/// <summary>Data sovereignty validator created.</summary>
	public const int DataSovereigntyValidatorCreated = 92100;

	/// <summary>Data residency validated.</summary>
	public const int DataResidencyValidated = 92101;

	/// <summary>Data residency violation detected.</summary>
	public const int DataResidencyViolation = 92102;

	/// <summary>Cross-border transfer blocked.</summary>
	public const int CrossBorderTransferBlocked = 92103;

	/// <summary>Region routing applied.</summary>
	public const int RegionRoutingApplied = 92104;

	// ========================================
	// 92200-92299: Field-Level Encryption
	// ========================================

	/// <summary>Field encryption service created.</summary>
	public const int FieldEncryptionServiceCreated = 92200;

	/// <summary>Field encrypted.</summary>
	public const int FieldEncrypted = 92201;

	/// <summary>Field decrypted.</summary>
	public const int FieldDecrypted = 92202;

	/// <summary>Field encryption failed.</summary>
	public const int FieldEncryptionFailed = 92203;

	/// <summary>Field encryption key rotated.</summary>
	public const int FieldEncryptionKeyRotated = 92204;

	/// <summary>Bulk decryption completed.</summary>
	public const int BulkDecryptionCompleted = 92205;

	/// <summary>Decryption error for field.</summary>
	public const int DecryptionErrorForField = 92206;

	/// <summary>Export completed.</summary>
	public const int ExportCompleted = 92207;

	/// <summary>Re-encryption succeeded.</summary>
	public const int ReEncryptionSucceeded = 92208;

	/// <summary>Re-encryption failed.</summary>
	public const int ReEncryptionFailed = 92209;

	/// <summary>Re-encryption failed for item.</summary>
	public const int ReEncryptionFailedForItem = 92210;

	/// <summary>Batch re-encryption completed.</summary>
	public const int BatchReEncryptionCompleted = 92211;

	/// <summary>Encryption migration completed.</summary>
	public const int EncryptionMigrationSucceeded = 92212;

	/// <summary>Encryption migration failed.</summary>
	public const int EncryptionMigrationFailed = 92213;

	/// <summary>Encryption health check failed.</summary>
	public const int EncryptionHealthCheckFailed = 92214;

	/// <summary>Encryption health check degraded.</summary>
	public const int EncryptionHealthCheckDegraded = 92215;

	/// <summary>Encryption health check passed.</summary>
	public const int EncryptionHealthCheckPassed = 92216;

	/// <summary>Encryption round-trip verification failed.</summary>
	public const int EncryptionHealthCheckRoundTripFailed = 92217;

	/// <summary>Key management verification failed.</summary>
	public const int EncryptionHealthCheckKeyManagementFailed = 92218;

	/// <summary>Batch migration completed.</summary>
	public const int EncryptionMigrationBatchCompleted = 92219;

	/// <summary>Data encrypted with old key version, consider re-encryption.</summary>
	public const int EncryptionReencryptionHint = 92220;

	/// <summary>Data already encrypted with active key.</summary>
	public const int EncryptionAlreadyActiveKey = 92221;

	/// <summary>Data re-encrypted from old key to new key.</summary>
	public const int EncryptionReencrypted = 92222;

	/// <summary>Key age exceeds maximum, initiating rotation.</summary>
	public const int EncryptionKeyAgeExceedsMax = 92223;

	// ========================================
	// 92300-92399: Compliance Validation
	// ========================================

	/// <summary>Compliance validation middleware executing.</summary>
	public const int ComplianceValidationExecuting = 92300;

	/// <summary>Compliance check passed.</summary>
	public const int ComplianceCheckPassed = 92301;

	/// <summary>Compliance check failed.</summary>
	public const int ComplianceCheckFailed = 92302;

	/// <summary>PII detected.</summary>
	public const int PiiDetected = 92303;

	/// <summary>PII masked.</summary>
	public const int PiiMasked = 92304;

	/// <summary>Compliance rule evaluated.</summary>
	public const int ComplianceRuleEvaluated = 92305;

	// ========================================
	// 92400-92499: Regulatory Reporting
	// ========================================

	/// <summary>Regulatory report generator created.</summary>
	public const int RegulatoryReportGeneratorCreated = 92400;

	/// <summary>Regulatory report generated.</summary>
	public const int RegulatoryReportGenerated = 92401;

	/// <summary>Audit trail exported.</summary>
	public const int AuditTrailExported = 92402;

	/// <summary>Compliance certificate generated.</summary>
	public const int ComplianceCertificateGenerated = 92403;

	/// <summary>Regulatory submission completed.</summary>
	public const int RegulatorySubmissionCompleted = 92404;

	// ========================================
	// 92500-92599: Key Management
	// ========================================

	/// <summary>Key management service created.</summary>
	public const int KeyManagementServiceCreated = 92500;

	/// <summary>Encryption key created.</summary>
	public const int EncryptionKeyCreated = 92501;

	/// <summary>Encryption key rotated.</summary>
	public const int EncryptionKeyRotated = 92502;

	/// <summary>Encryption key revoked.</summary>
	public const int EncryptionKeyRevoked = 92503;

	/// <summary>Key access logged.</summary>
	public const int KeyAccessLogged = 92504;

	/// <summary>Development encryption warning emitted.</summary>
	public const int DevEncryptionWarning = 92510;

	/// <summary>Key rotation failure reported.</summary>
	public const int KeyRotationFailureReported = 92520;

	/// <summary>Key rotation success reported.</summary>
	public const int KeyRotationSuccessReported = 92521;

	/// <summary>Key expiration warning reported.</summary>
	public const int KeyExpirationWarningReported = 92522;

	/// <summary>Key rotation alert notification failed.</summary>
	public const int KeyRotationAlertNotifyFailed = 92523;

	/// <summary>Key rotation failure alert logged (critical).</summary>
	public const int KeyRotationFailureAlertCriticalLogged = 92530;

	/// <summary>Key rotation failure alert logged (high).</summary>
	public const int KeyRotationFailureAlertHighLogged = 92531;

	/// <summary>Key rotation failure alert logged (medium).</summary>
	public const int KeyRotationFailureAlertMediumLogged = 92532;

	/// <summary>Key rotation failure alert logged (low).</summary>
	public const int KeyRotationFailureAlertLowLogged = 92533;

	/// <summary>Key expiration warning alert logged (critical).</summary>
	public const int KeyExpirationWarningAlertCriticalLogged = 92534;

	/// <summary>Key expiration warning alert logged (high).</summary>
	public const int KeyExpirationWarningAlertHighLogged = 92535;

	/// <summary>Key expiration warning alert logged (medium).</summary>
	public const int KeyExpirationWarningAlertMediumLogged = 92536;

	/// <summary>Key expiration warning alert logged (low).</summary>
	public const int KeyExpirationWarningAlertLowLogged = 92537;

	/// <summary>Key rotation success alert logged.</summary>
	public const int KeyRotationSuccessAlertLogged = 92538;

	/// <summary>Key rotation service disabled.</summary>
	public const int KeyRotationServiceDisabled = 92540;

	/// <summary>Key rotation service started.</summary>
	public const int KeyRotationServiceStarted = 92541;

	/// <summary>Key rotation check completed.</summary>
	public const int KeyRotationCheckCompleted = 92542;

	/// <summary>Key rotation check completed with no work.</summary>
	public const int KeyRotationCheckNoKeys = 92543;

	/// <summary>Key rotation check error.</summary>
	public const int KeyRotationCheckError = 92544;

	/// <summary>Key rotation service stopped.</summary>
	public const int KeyRotationServiceStopped = 92545;

	/// <summary>Key approaching rotation.</summary>
	public const int KeyRotationApproaching = 92546;

	/// <summary>Skipping recently failed key rotation.</summary>
	public const int KeyRotationRetryDelayed = 92547;

	/// <summary>Key rotation started.</summary>
	public const int KeyRotationStarted = 92548;

	/// <summary>Key rotation succeeded.</summary>
	public const int KeyRotationSucceeded = 92549;

	/// <summary>Key rotation failed.</summary>
	public const int KeyRotationFailed = 92550;

	/// <summary>Key rotation exception.</summary>
	public const int KeyRotationException = 92551;

	/// <summary>Force rotation key not found.</summary>
	public const int KeyRotationForceMissingKey = 92552;

	/// <summary>Force rotation started.</summary>
	public const int KeyRotationForceStarted = 92553;

	/// <summary>Force rotation succeeded.</summary>
	public const int KeyRotationForceSucceeded = 92554;

	/// <summary>Force rotation failed.</summary>
	public const int KeyRotationForceFailed = 92555;

	// ========================================
	// 92600-92699: Cloud Provider Compliance
	// ========================================

	/// <summary>Cloud compliance adapter created.</summary>
	public const int CloudComplianceAdapterCreated = 92600;

	/// <summary>AWS compliance check completed.</summary>
	public const int AwsComplianceCheckCompleted = 92601;

	/// <summary>Azure compliance check completed.</summary>
	public const int AzureComplianceCheckCompleted = 92602;

	/// <summary>Vault integration configured.</summary>
	public const int VaultIntegrationConfigured = 92603;

	/// <summary>Cloud KMS configured.</summary>
	public const int CloudKmsConfigured = 92604;

	// ========================================
	// 92700-92799: Erasure & Legal Hold
	// ========================================

	/// <summary>Erasure request processing started.</summary>
	public const int ErasureRequestProcessing = 92700;

	/// <summary>Erasure request blocked by legal hold.</summary>
	public const int ErasureBlockedByLegalHold = 92701;

	/// <summary>Erasure request scheduled.</summary>
	public const int ErasureScheduled = 92702;

	/// <summary>Erasure request processing failed.</summary>
	public const int ErasureRequestFailed = 92703;

	/// <summary>Erasure cancellation requested for missing request.</summary>
	public const int ErasureCancellationNotFound = 92704;

	/// <summary>Erasure cancellation not allowed.</summary>
	public const int ErasureCancellationNotAllowed = 92705;

	/// <summary>Erasure request cancelled.</summary>
	public const int ErasureCancelled = 92706;

	/// <summary>Erasure certificate generated.</summary>
	public const int ErasureCertificateGenerated = 92707;

	/// <summary>Erasure key deletion failed.</summary>
	public const int ErasureKeyDeletionFailed = 92708;

	/// <summary>Erasure request completed.</summary>
	public const int ErasureRequestCompleted = 92709;

	/// <summary>Erasure execution failed.</summary>
	public const int ErasureExecutionFailed = 92710;

	/// <summary>Requested grace period below minimum.</summary>
	public const int ErasureGracePeriodBelowMinimum = 92711;

	/// <summary>Requested grace period exceeds maximum.</summary>
	public const int ErasureGracePeriodExceedsMaximum = 92712;

	/// <summary>Erasure verification started.</summary>
	public const int ErasureVerificationStarted = 92713;

	/// <summary>Erasure verification failed.</summary>
	public const int ErasureVerificationFailed = 92714;

	/// <summary>Erasure verification passed.</summary>
	public const int ErasureVerificationPassed = 92715;

	/// <summary>Erasure verification error.</summary>
	public const int ErasureVerificationError = 92716;

	/// <summary>Erasure key deletion verification started.</summary>
	public const int ErasureKeyDeletionVerificationStarted = 92717;

	/// <summary>Erasure key deletion confirmed by missing key.</summary>
	public const int ErasureKeyDeletionConfirmedNotFound = 92718;

	/// <summary>Erasure key deletion confirmed by status.</summary>
	public const int ErasureKeyDeletionConfirmedStatus = 92719;

	/// <summary>Erasure key deletion not confirmed.</summary>
	public const int ErasureKeyDeletionNotDeleted = 92720;

	/// <summary>Erasure key deletion confirmed by exception.</summary>
	public const int ErasureKeyDeletionConfirmedException = 92721;

	/// <summary>Erasure key deletion verification error.</summary>
	public const int ErasureKeyDeletionError = 92722;

	/// <summary>Expected error accessing deleted key.</summary>
	public const int ErasureKeyDeletionExpectedError = 92723;

	/// <summary>Keys discovered for erasure via data inventory.</summary>
	public const int ErasureKeysDiscovered = 92724;

	/// <summary>Erasure contributor completed successfully.</summary>
	public const int ErasureContributorCompleted = 92725;

	/// <summary>Erasure contributor reported failure.</summary>
	public const int ErasureContributorFailed = 92726;

	/// <summary>Erasure contributor threw an exception.</summary>
	public const int ErasureContributorException = 92727;

	/// <summary>Erasure scheduler disabled.</summary>
	public const int ErasureSchedulerDisabled = 92750;

	/// <summary>Erasure scheduler starting.</summary>
	public const int ErasureSchedulerStarting = 92751;

	/// <summary>Erasure scheduler processing cycle error.</summary>
	public const int ErasureSchedulerProcessingError = 92752;

	/// <summary>Erasure scheduler stopped.</summary>
	public const int ErasureSchedulerStopped = 92753;

	/// <summary>No scheduled erasures ready.</summary>
	public const int ErasureSchedulerNoScheduledRequests = 92754;

	/// <summary>Processing scheduled erasure batch.</summary>
	public const int ErasureSchedulerProcessingBatch = 92755;

	/// <summary>Executing scheduled erasure.</summary>
	public const int ErasureSchedulerExecutingRequest = 92756;

	/// <summary>Scheduled erasure completed.</summary>
	public const int ErasureSchedulerRequestCompleted = 92757;

	/// <summary>Scheduled erasure failed.</summary>
	public const int ErasureSchedulerRequestFailed = 92758;

	/// <summary>Error executing scheduled erasure.</summary>
	public const int ErasureSchedulerExecutionError = 92759;

	/// <summary>Erasure request marked as failed.</summary>
	public const int ErasureSchedulerMarkedFailed = 92760;

	/// <summary>Erasure certificates cleaned.</summary>
	public const int ErasureSchedulerCertificatesCleaned = 92761;

	/// <summary>Certificate cleanup failed.</summary>
	public const int ErasureSchedulerCertificateCleanupFailed = 92762;

	/// <summary>Data inventory discovery started.</summary>
	public const int DataInventoryDiscoveryStarted = 92770;

	/// <summary>Data inventory key info retrieval failed.</summary>
	public const int DataInventoryKeyInfoFailed = 92771;

	/// <summary>Data inventory discovery completed.</summary>
	public const int DataInventoryDiscoveryCompleted = 92772;

	/// <summary>Data inventory registration added.</summary>
	public const int DataInventoryRegistrationAdded = 92773;

	/// <summary>Data inventory registration removed.</summary>
	public const int DataInventoryRegistrationRemoved = 92774;

	/// <summary>Data inventory registration not found.</summary>
	public const int DataInventoryRegistrationNotFound = 92775;

	/// <summary>Legal hold expiration service disabled.</summary>
	public const int LegalHoldExpirationDisabled = 92780;

	/// <summary>Legal hold expiration service starting.</summary>
	public const int LegalHoldExpirationStarting = 92781;

	/// <summary>Legal hold expiration service stopped.</summary>
	public const int LegalHoldExpirationStopped = 92782;

	/// <summary>Legal hold expiration processing error.</summary>
	public const int LegalHoldExpirationProcessingError = 92783;

	/// <summary>No expired legal holds found.</summary>
	public const int LegalHoldExpirationNoExpiredHolds = 92784;

	/// <summary>Processing expired legal holds batch.</summary>
	public const int LegalHoldExpirationProcessingBatch = 92785;

	/// <summary>Expired legal hold auto-released.</summary>
	public const int LegalHoldExpirationAutoReleased = 92786;

	/// <summary>Failed to auto-release expired legal hold.</summary>
	public const int LegalHoldExpirationReleaseFailed = 92787;

	/// <summary>Legal hold created.</summary>
	public const int LegalHoldCreated = 92790;

	/// <summary>Legal hold released.</summary>
	public const int LegalHoldReleased = 92791;

	/// <summary>Legal hold check completed.</summary>
	public const int LegalHoldCheckCompleted = 92792;

	/// <summary>Legal hold auto-release failed.</summary>
	public const int LegalHoldAutoReleaseFailed = 92793;

	/// <summary>Legal hold auto-release completed.</summary>
	public const int LegalHoldAutoReleaseCompleted = 92794;

	// ========================================
	// 92800-92899: Compliance Monitoring & Alerts
	// ========================================

	/// <summary>Compliance gap alert logged (critical).</summary>
	public const int ComplianceGapAlertCritical = 92800;

	/// <summary>Compliance gap alert logged (high).</summary>
	public const int ComplianceGapAlertHigh = 92801;

	/// <summary>Compliance gap alert logged (medium).</summary>
	public const int ComplianceGapAlertMedium = 92802;

	/// <summary>Compliance gap alert logged (low).</summary>
	public const int ComplianceGapAlertLow = 92803;

	/// <summary>Compliance gap remediation guidance logged.</summary>
	public const int ComplianceGapRemediationGuidance = 92804;

	/// <summary>Control validation failure logged (critical).</summary>
	public const int ControlValidationFailureCritical = 92810;

	/// <summary>Control validation failure logged (high).</summary>
	public const int ControlValidationFailureHigh = 92811;

	/// <summary>Control validation failure logged (medium).</summary>
	public const int ControlValidationFailureMedium = 92812;

	/// <summary>Control validation failure logged (low).</summary>
	public const int ControlValidationFailureLow = 92813;

	/// <summary>Erasure store health check passed.</summary>
	public const int ErasureHealthCheckPassed = 92830;

	/// <summary>Erasure store health check degraded.</summary>
	public const int ErasureHealthCheckDegraded = 92831;

	/// <summary>Erasure store health check failed.</summary>
	public const int ErasureHealthCheckFailed = 92832;

	/// <summary>Compliance restored notification logged.</summary>
	public const int ComplianceRestored = 92820;

	/// <summary>Compliance lost notification logged.</summary>
	public const int ComplianceLost = 92821;

	// ========================================
	// 92900-92999: Cascade Erasure, Portability, SAR, Breach, Retention, Consent
	// ========================================

	/// <summary>Cascade erasure started.</summary>
	public const int CascadeErasureStarted = 92900;

	/// <summary>Cascade erasure completed.</summary>
	public const int CascadeErasureCompleted = 92901;

	/// <summary>Cascade erasure failed.</summary>
	public const int CascadeErasureFailed = 92902;

	/// <summary>Cascade erasure related subject discovered.</summary>
	public const int CascadeErasureRelatedSubjectDiscovered = 92903;

	/// <summary>Data portability export started.</summary>
	public const int DataPortabilityExportStarted = 92910;

	/// <summary>Data portability export completed.</summary>
	public const int DataPortabilityExportCompleted = 92911;

	/// <summary>Data portability export failed.</summary>
	public const int DataPortabilityExportFailed = 92912;

	/// <summary>Subject access request created.</summary>
	public const int SubjectAccessRequestCreated = 92920;

	/// <summary>Subject access request fulfilled.</summary>
	public const int SubjectAccessRequestFulfilled = 92921;

	/// <summary>Subject access request failed.</summary>
	public const int SubjectAccessRequestFailed = 92922;

	/// <summary>Audit log encryption completed.</summary>
	public const int AuditLogEncryptionCompleted = 92930;

	/// <summary>Audit log decryption completed.</summary>
	public const int AuditLogDecryptionCompleted = 92931;

	/// <summary>Audit log encryption failed.</summary>
	public const int AuditLogEncryptionFailed = 92932;

	/// <summary>Key escrow backup completed.</summary>
	public const int KeyEscrowBackupCompleted = 92940;

	/// <summary>Key escrow recovery completed.</summary>
	public const int KeyEscrowRecoveryCompleted = 92941;

	/// <summary>Key escrow operation failed.</summary>
	public const int KeyEscrowOperationFailed = 92942;

	/// <summary>Breach notification reported.</summary>
	public const int BreachNotificationReported = 92950;

	/// <summary>Breach notification sent to subjects.</summary>
	public const int BreachNotificationSent = 92951;

	/// <summary>Breach notification failed.</summary>
	public const int BreachNotificationFailed = 92952;

	/// <summary>Retention enforcement scan started.</summary>
	public const int RetentionEnforcementStarted = 92960;

	/// <summary>Retention enforcement scan completed.</summary>
	public const int RetentionEnforcementCompleted = 92961;

	/// <summary>Retention enforcement scan failed.</summary>
	public const int RetentionEnforcementFailed = 92962;

	/// <summary>Retention enforcement service disabled.</summary>
	public const int RetentionEnforcementDisabled = 92963;

	/// <summary>Retention enforcement service starting.</summary>
	public const int RetentionEnforcementServiceStarting = 92964;

	/// <summary>Retention enforcement service stopped.</summary>
	public const int RetentionEnforcementServiceStopped = 92965;

	/// <summary>Retention enforcement processing error.</summary>
	public const int RetentionEnforcementProcessingError = 92966;

	/// <summary>Consent recorded.</summary>
	public const int ConsentRecorded = 92970;

	/// <summary>Consent withdrawn.</summary>
	public const int ConsentWithdrawn = 92971;

	/// <summary>Consent operation failed.</summary>
	public const int ConsentOperationFailed = 92972;
}
