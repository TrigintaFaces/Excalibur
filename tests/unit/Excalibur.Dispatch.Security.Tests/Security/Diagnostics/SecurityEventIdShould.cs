// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Diagnostics;

namespace Excalibur.Dispatch.Security.Tests.Security.Diagnostics;

/// <summary>
/// Unit tests for <see cref="SecurityEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Security")]
[Trait("Priority", "0")]
public sealed class SecurityEventIdShould : UnitTestBase
{
	#region Authentication Event ID Tests (70000-70099)

	[Fact]
	public void HaveJwtAuthenticationExecutingInAuthRange()
	{
		SecurityEventId.JwtAuthenticationExecuting.ShouldBe(70000);
	}

	[Fact]
	public void HaveJwtTokenValidatedInAuthRange()
	{
		SecurityEventId.JwtTokenValidated.ShouldBe(70001);
	}

	[Fact]
	public void HaveJwtTokenValidationFailedInAuthRange()
	{
		SecurityEventId.JwtTokenValidationFailed.ShouldBe(70002);
	}

	[Fact]
	public void HaveJwtTokenExpiredInAuthRange()
	{
		SecurityEventId.JwtTokenExpired.ShouldBe(70003);
	}

	[Fact]
	public void HaveAuthenticationSucceededInAuthRange()
	{
		SecurityEventId.AuthenticationSucceeded.ShouldBe(70004);
	}

	[Fact]
	public void HaveAuthenticationFailedInAuthRange()
	{
		SecurityEventId.AuthenticationFailed.ShouldBe(70005);
	}

	[Fact]
	public void HaveAuthenticationSkippedAnonymousInAuthRange()
	{
		SecurityEventId.AuthenticationSkippedAnonymous.ShouldBe(70006);
	}

	[Fact]
	public void HaveAuthenticationTokenMissingInAuthRange()
	{
		SecurityEventId.AuthenticationTokenMissing.ShouldBe(70007);
	}

	[Fact]
	public void HaveAuthenticationNoSigningKeyInAuthRange()
	{
		SecurityEventId.AuthenticationNoSigningKey.ShouldBe(70008);
	}

	[Fact]
	public void HaveAuthenticationValidationDebugInAuthRange()
	{
		SecurityEventId.AuthenticationValidationDebug.ShouldBe(70009);
	}

	[Fact]
	public void HaveJwtTokenValidationErrorInAuthRange()
	{
		SecurityEventId.JwtTokenValidationError.ShouldBe(70010);
	}

	[Fact]
	public void HaveAllAuthEventIdsInExpectedRange()
	{
		// Authentication IDs are in range 70000-70099
		SecurityEventId.JwtAuthenticationExecuting.ShouldBeInRange(70000, 70099);
		SecurityEventId.JwtTokenValidated.ShouldBeInRange(70000, 70099);
		SecurityEventId.JwtTokenValidationFailed.ShouldBeInRange(70000, 70099);
		SecurityEventId.JwtTokenExpired.ShouldBeInRange(70000, 70099);
		SecurityEventId.AuthenticationSucceeded.ShouldBeInRange(70000, 70099);
		SecurityEventId.AuthenticationFailed.ShouldBeInRange(70000, 70099);
		SecurityEventId.AuthenticationSkippedAnonymous.ShouldBeInRange(70000, 70099);
		SecurityEventId.AuthenticationTokenMissing.ShouldBeInRange(70000, 70099);
		SecurityEventId.AuthenticationNoSigningKey.ShouldBeInRange(70000, 70099);
		SecurityEventId.AuthenticationValidationDebug.ShouldBeInRange(70000, 70099);
		SecurityEventId.JwtTokenValidationError.ShouldBeInRange(70000, 70099);
	}

	#endregion

	#region Input Validation Event ID Tests (70100-70199)

	[Fact]
	public void HaveInputValidationExecutingInValidationRange()
	{
		SecurityEventId.InputValidationExecuting.ShouldBe(70100);
	}

	[Fact]
	public void HaveInputValidationPassedInValidationRange()
	{
		SecurityEventId.InputValidationPassed.ShouldBe(70101);
	}

	[Fact]
	public void HaveInputValidationFailedInValidationRange()
	{
		SecurityEventId.InputValidationFailed.ShouldBe(70102);
	}

	[Fact]
	public void HaveDangerousInputDetectedInValidationRange()
	{
		SecurityEventId.DangerousInputDetected.ShouldBe(70103);
	}

	[Fact]
	public void HaveInputSanitizedInValidationRange()
	{
		SecurityEventId.InputSanitized.ShouldBe(70104);
	}

	[Fact]
	public void HaveInputValidationDisabledInValidationRange()
	{
		SecurityEventId.InputValidationDisabled.ShouldBe(70105);
	}

	[Fact]
	public void HaveInputValidatorFailedInValidationRange()
	{
		SecurityEventId.InputValidatorFailed.ShouldBe(70106);
	}

	[Fact]
	public void HaveInputValidationUnexpectedErrorInValidationRange()
	{
		SecurityEventId.InputValidationUnexpectedError.ShouldBe(70107);
	}

	[Fact]
	public void HaveAllInputValidationEventIdsInExpectedRange()
	{
		SecurityEventId.InputValidationExecuting.ShouldBeInRange(70100, 70199);
		SecurityEventId.InputValidationPassed.ShouldBeInRange(70100, 70199);
		SecurityEventId.InputValidationFailed.ShouldBeInRange(70100, 70199);
		SecurityEventId.DangerousInputDetected.ShouldBeInRange(70100, 70199);
		SecurityEventId.InputSanitized.ShouldBeInRange(70100, 70199);
		SecurityEventId.InputValidationDisabled.ShouldBeInRange(70100, 70199);
		SecurityEventId.InputValidatorFailed.ShouldBeInRange(70100, 70199);
		SecurityEventId.InputValidationUnexpectedError.ShouldBeInRange(70100, 70199);
	}

	#endregion

	#region Message Signing Event ID Tests (70200-70299)

	[Fact]
	public void HaveMessageSigningExecutingInSigningRange()
	{
		SecurityEventId.MessageSigningExecuting.ShouldBe(70200);
	}

	[Fact]
	public void HaveMessageSignedInSigningRange()
	{
		SecurityEventId.MessageSigned.ShouldBe(70201);
	}

	[Fact]
	public void HaveSignatureVerifiedInSigningRange()
	{
		SecurityEventId.SignatureVerified.ShouldBe(70202);
	}

	[Fact]
	public void HaveSignatureVerificationFailedInSigningRange()
	{
		SecurityEventId.SignatureVerificationFailed.ShouldBe(70203);
	}

	[Fact]
	public void HaveHmacSigningServiceCreatedInSigningRange()
	{
		SecurityEventId.HmacSigningServiceCreated.ShouldBe(70204);
	}

	[Fact]
	public void HaveSecureKeyProviderCreatedInSigningRange()
	{
		SecurityEventId.SecureKeyProviderCreated.ShouldBe(70205);
	}

	[Fact]
	public void HaveSigningKeyRotatedInSigningRange()
	{
		SecurityEventId.SigningKeyRotated.ShouldBe(70206);
	}

	[Fact]
	public void HaveKeyVaultInitializedInSigningRange()
	{
		SecurityEventId.KeyVaultInitialized.ShouldBe(70207);
	}

	[Fact]
	public void HaveKeyVaultInitializationFailedInSigningRange()
	{
		SecurityEventId.KeyVaultInitializationFailed.ShouldBe(70208);
	}

	[Fact]
	public void HaveAllSigningEventIdsInExpectedRange()
	{
		SecurityEventId.MessageSigningExecuting.ShouldBeInRange(70200, 70299);
		SecurityEventId.MessageSigned.ShouldBeInRange(70200, 70299);
		SecurityEventId.SignatureVerified.ShouldBeInRange(70200, 70299);
		SecurityEventId.SignatureVerificationFailed.ShouldBeInRange(70200, 70299);
		SecurityEventId.SigningMiddlewareSignatureVerificationFailed.ShouldBeInRange(70200, 70299);
	}

	#endregion

	#region Encryption Core Event ID Tests (70300-70399)

	[Fact]
	public void HaveMessageEncryptionExecutingInEncryptionRange()
	{
		SecurityEventId.MessageEncryptionExecuting.ShouldBe(70300);
	}

	[Fact]
	public void HaveMessageEncryptedInEncryptionRange()
	{
		SecurityEventId.MessageEncrypted.ShouldBe(70301);
	}

	[Fact]
	public void HaveMessageDecryptedInEncryptionRange()
	{
		SecurityEventId.MessageDecrypted.ShouldBe(70302);
	}

	[Fact]
	public void HaveEncryptionFailedInEncryptionRange()
	{
		SecurityEventId.EncryptionFailed.ShouldBe(70303);
	}

	[Fact]
	public void HaveDecryptionFailedInEncryptionRange()
	{
		SecurityEventId.DecryptionFailed.ShouldBe(70304);
	}

	[Fact]
	public void HaveAllEncryptionCoreEventIdsInExpectedRange()
	{
		SecurityEventId.MessageEncryptionExecuting.ShouldBeInRange(70300, 70399);
		SecurityEventId.MessageEncrypted.ShouldBeInRange(70300, 70399);
		SecurityEventId.MessageDecrypted.ShouldBeInRange(70300, 70399);
		SecurityEventId.EncryptionFailed.ShouldBeInRange(70300, 70399);
		SecurityEventId.DecryptionFailed.ShouldBeInRange(70300, 70399);
	}

	#endregion

	#region Encryption Migration Event ID Tests (70400-70499)

	[Fact]
	public void HaveEncryptionMigrationServiceCreatedInMigrationRange()
	{
		SecurityEventId.EncryptionMigrationServiceCreated.ShouldBe(70400);
	}

	[Fact]
	public void HaveEncryptionMigrationStartedInMigrationRange()
	{
		SecurityEventId.EncryptionMigrationStarted.ShouldBe(70401);
	}

	[Fact]
	public void HaveEncryptionMigrationCompletedInMigrationRange()
	{
		SecurityEventId.EncryptionMigrationCompleted.ShouldBe(70402);
	}

	[Fact]
	public void HaveLazyReEncryptionExecutingInMigrationRange()
	{
		SecurityEventId.LazyReEncryptionExecuting.ShouldBe(70403);
	}

	[Fact]
	public void HaveMessageReEncryptedInMigrationRange()
	{
		SecurityEventId.MessageReEncrypted.ShouldBe(70404);
	}

	[Fact]
	public void HaveAllMigrationEventIdsInExpectedRange()
	{
		SecurityEventId.EncryptionMigrationServiceCreated.ShouldBeInRange(70400, 70499);
		SecurityEventId.EncryptionMigrationStarted.ShouldBeInRange(70400, 70499);
		SecurityEventId.EncryptionMigrationCompleted.ShouldBeInRange(70400, 70499);
		SecurityEventId.LazyReEncryptionExecuting.ShouldBeInRange(70400, 70499);
		SecurityEventId.MessageReEncrypted.ShouldBeInRange(70400, 70499);
		SecurityEventId.LazyReEncryptionError.ShouldBeInRange(70400, 70499);
	}

	#endregion

	#region Rate Limiting Event ID Tests (70500-70599)

	[Fact]
	public void HaveSecurityRateLimitingExecutingInRateLimitRange()
	{
		SecurityEventId.SecurityRateLimitingExecuting.ShouldBe(70500);
	}

	[Fact]
	public void HaveRateLimitCheckPassedInRateLimitRange()
	{
		SecurityEventId.RateLimitCheckPassed.ShouldBe(70501);
	}

	[Fact]
	public void HaveRateLimitExceededInRateLimitRange()
	{
		SecurityEventId.RateLimitExceeded.ShouldBe(70502);
	}

	[Fact]
	public void HaveRateLimitWindowResetInRateLimitRange()
	{
		SecurityEventId.RateLimitWindowReset.ShouldBe(70503);
	}

	[Fact]
	public void HaveRateLimitPermitAcquiredInRateLimitRange()
	{
		SecurityEventId.RateLimitPermitAcquired.ShouldBe(70504);
	}

	[Fact]
	public void HaveAllRateLimitEventIdsInExpectedRange()
	{
		SecurityEventId.SecurityRateLimitingExecuting.ShouldBeInRange(70500, 70599);
		SecurityEventId.RateLimitCheckPassed.ShouldBeInRange(70500, 70599);
		SecurityEventId.RateLimitExceeded.ShouldBeInRange(70500, 70599);
		SecurityEventId.RateLimitWindowReset.ShouldBeInRange(70500, 70599);
		SecurityEventId.RateLimitPermitAcquired.ShouldBeInRange(70500, 70599);
		SecurityEventId.RateLimitCleanupError.ShouldBeInRange(70500, 70599);
	}

	#endregion

	#region Audit Logging Event ID Tests (70600-70699)

	[Fact]
	public void HaveSecurityEventLoggerCreatedInAuditRange()
	{
		SecurityEventId.SecurityEventLoggerCreated.ShouldBe(70600);
	}

	[Fact]
	public void HaveSecurityEventLoggedInAuditRange()
	{
		SecurityEventId.SecurityEventLogged.ShouldBe(70601);
	}

	[Fact]
	public void HaveAuditEntryCreatedInAuditRange()
	{
		SecurityEventId.AuditEntryCreated.ShouldBe(70602);
	}

	[Fact]
	public void HaveSecurityIncidentDetectedInAuditRange()
	{
		SecurityEventId.SecurityIncidentDetected.ShouldBe(70603);
	}

	[Fact]
	public void HaveSecurityAlertRaisedInAuditRange()
	{
		SecurityEventId.SecurityAlertRaised.ShouldBe(70604);
	}

	[Fact]
	public void HaveAllAuditEventIdsInExpectedRange()
	{
		SecurityEventId.SecurityEventLoggerCreated.ShouldBeInRange(70600, 70699);
		SecurityEventId.SecurityEventLogged.ShouldBeInRange(70600, 70699);
		SecurityEventId.AuditEntryCreated.ShouldBeInRange(70600, 70699);
		SecurityEventId.SecurityIncidentDetected.ShouldBeInRange(70600, 70699);
		SecurityEventId.SecurityAlertRaised.ShouldBeInRange(70600, 70699);
		SecurityEventId.SecurityEventLoggedWithDetails.ShouldBeInRange(70600, 70699);
	}

	#endregion

	#region Event Stores Event ID Tests (70700-70799)

	[Fact]
	public void HaveSqlSecurityEventStoreCreatedInEventStoreRange()
	{
		SecurityEventId.SqlSecurityEventStoreCreated.ShouldBe(70700);
	}

	[Fact]
	public void HaveFileSecurityEventStoreCreatedInEventStoreRange()
	{
		SecurityEventId.FileSecurityEventStoreCreated.ShouldBe(70701);
	}

	[Fact]
	public void HaveElasticsearchSecurityEventStoreCreatedInEventStoreRange()
	{
		SecurityEventId.ElasticsearchSecurityEventStoreCreated.ShouldBe(70702);
	}

	[Fact]
	public void HaveSecurityEventStoredInEventStoreRange()
	{
		SecurityEventId.SecurityEventStored.ShouldBe(70703);
	}

	[Fact]
	public void HaveSecurityEventRetrievedInEventStoreRange()
	{
		SecurityEventId.SecurityEventRetrieved.ShouldBe(70704);
	}

	[Fact]
	public void HaveAllEventStoreEventIdsInExpectedRange()
	{
		SecurityEventId.SqlSecurityEventStoreCreated.ShouldBeInRange(70700, 70799);
		SecurityEventId.FileSecurityEventStoreCreated.ShouldBeInRange(70700, 70799);
		SecurityEventId.ElasticsearchSecurityEventStoreCreated.ShouldBeInRange(70700, 70799);
		SecurityEventId.SecurityEventStored.ShouldBeInRange(70700, 70799);
		SecurityEventId.SecurityEventRetrieved.ShouldBeInRange(70700, 70799);
		SecurityEventId.FileStoreCleanupFailed.ShouldBeInRange(70700, 70799);
	}

	#endregion

	#region Credential Stores Event ID Tests (70800-70899)

	[Fact]
	public void HaveSecureCredentialProviderCreatedInCredentialRange()
	{
		SecurityEventId.SecureCredentialProviderCreated.ShouldBe(70800);
	}

	[Fact]
	public void HaveEnvironmentCredentialStoreCreatedInCredentialRange()
	{
		SecurityEventId.EnvironmentCredentialStoreCreated.ShouldBe(70801);
	}

	[Fact]
	public void HaveCredentialRetrievedInCredentialRange()
	{
		SecurityEventId.CredentialRetrieved.ShouldBe(70802);
	}

	[Fact]
	public void HaveCredentialNotFoundInCredentialRange()
	{
		SecurityEventId.CredentialNotFound.ShouldBe(70803);
	}

	[Fact]
	public void HaveCredentialCachedInCredentialRange()
	{
		SecurityEventId.CredentialCached.ShouldBe(70804);
	}

	[Fact]
	public void HaveAllCredentialEventIdsInExpectedRange()
	{
		SecurityEventId.SecureCredentialProviderCreated.ShouldBeInRange(70800, 70899);
		SecurityEventId.EnvironmentCredentialStoreCreated.ShouldBeInRange(70800, 70899);
		SecurityEventId.CredentialRetrieved.ShouldBeInRange(70800, 70899);
		SecurityEventId.CredentialNotFound.ShouldBeInRange(70800, 70899);
		SecurityEventId.CredentialCached.ShouldBeInRange(70800, 70899);
		SecurityEventId.EnvironmentVariableNotFound.ShouldBeInRange(70800, 70899);
	}

	#endregion

	#region Cloud Credential Stores Event ID Tests (70900-70999)

	[Fact]
	public void HaveAzureKeyVaultCredentialStoreCreatedInCloudRange()
	{
		SecurityEventId.AzureKeyVaultCredentialStoreCreated.ShouldBe(70900);
	}

	[Fact]
	public void HaveAwsSecretsManagerCredentialStoreCreatedInCloudRange()
	{
		SecurityEventId.AwsSecretsManagerCredentialStoreCreated.ShouldBe(70901);
	}

	[Fact]
	public void HaveHashiCorpVaultCredentialStoreCreatedInCloudRange()
	{
		SecurityEventId.HashiCorpVaultCredentialStoreCreated.ShouldBe(70902);
	}

	[Fact]
	public void HaveCloudCredentialRetrievedInCloudRange()
	{
		SecurityEventId.CloudCredentialRetrieved.ShouldBe(70903);
	}

	[Fact]
	public void HaveCloudCredentialAccessDeniedInCloudRange()
	{
		SecurityEventId.CloudCredentialAccessDenied.ShouldBe(70904);
	}

	[Fact]
	public void HaveCloudCredentialStoreErrorInCloudRange()
	{
		SecurityEventId.CloudCredentialStoreError.ShouldBe(70905);
	}

	[Fact]
	public void HaveAllCloudEventIdsInExpectedRange()
	{
		SecurityEventId.AzureKeyVaultCredentialStoreCreated.ShouldBeInRange(70900, 70999);
		SecurityEventId.AwsSecretsManagerCredentialStoreCreated.ShouldBeInRange(70900, 70999);
		SecurityEventId.HashiCorpVaultCredentialStoreCreated.ShouldBeInRange(70900, 70999);
		SecurityEventId.CloudCredentialRetrieved.ShouldBeInRange(70900, 70999);
		SecurityEventId.CloudCredentialAccessDenied.ShouldBeInRange(70900, 70999);
		SecurityEventId.CloudCredentialStoreError.ShouldBeInRange(70900, 70999);
		SecurityEventId.AwsSecretsManagerStored.ShouldBeInRange(70900, 70999);
	}

	#endregion

	#region Security Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInSecurityReservedRange()
	{
		// Security reserved range is 70000-70999
		var allEventIds = GetAllSecurityEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(70000, 70999,
				$"Event ID {eventId} is outside Security reserved range (70000-70999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllSecurityEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllSecurityEventIds();

		// Verify a substantial portion of SecurityEventId constants are covered
		// Full file has 184 event IDs; test covers the main categories
		allEventIds.Length.ShouldBeGreaterThan(180);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllSecurityEventIds()
	{
		return
		[
			// Authentication (70000-70099)
			SecurityEventId.JwtAuthenticationExecuting,
			SecurityEventId.JwtTokenValidated,
			SecurityEventId.JwtTokenValidationFailed,
			SecurityEventId.JwtTokenExpired,
			SecurityEventId.AuthenticationSucceeded,
			SecurityEventId.AuthenticationFailed,
			SecurityEventId.AuthenticationSkippedAnonymous,
			SecurityEventId.AuthenticationTokenMissing,
			SecurityEventId.AuthenticationNoSigningKey,
			SecurityEventId.AuthenticationValidationDebug,
			SecurityEventId.JwtTokenValidationError,

			// Input Validation (70100-70199)
			SecurityEventId.InputValidationExecuting,
			SecurityEventId.InputValidationPassed,
			SecurityEventId.InputValidationFailed,
			SecurityEventId.DangerousInputDetected,
			SecurityEventId.InputSanitized,
			SecurityEventId.InputValidationDisabled,
			SecurityEventId.InputValidatorFailed,
			SecurityEventId.InputValidationUnexpectedError,

			// Message Signing (70200-70299)
			SecurityEventId.MessageSigningExecuting,
			SecurityEventId.MessageSigned,
			SecurityEventId.SignatureVerified,
			SecurityEventId.SignatureVerificationFailed,
			SecurityEventId.HmacSigningServiceCreated,
			SecurityEventId.SecureKeyProviderCreated,
			SecurityEventId.SigningKeyRotated,
			SecurityEventId.KeyVaultInitialized,
			SecurityEventId.KeyVaultInitializationFailed,
			SecurityEventId.KeyRetrievedFromCache,
			SecurityEventId.KeyNotFoundGeneratingNew,
			SecurityEventId.NewKeyStored,
			SecurityEventId.KeyNotFoundInKeyVault,
			SecurityEventId.FailedToStoreKeyInKeyVault,
			SecurityEventId.InvalidKeyFormat,
			SecurityEventId.ExpiredKeyRemoved,
			SecurityEventId.ExpiredKeysCleanedUp,
			SecurityEventId.StoringKeyInMemoryOnly,
			SecurityEventId.HmacMessageSigned,
			SecurityEventId.HmacSigningFailed,
			SecurityEventId.HmacVerificationSuccessful,
			SecurityEventId.HmacVerificationFailed,
			SecurityEventId.HmacVerificationError,
			SecurityEventId.SignatureExpired,
			SecurityEventId.SigningMiddlewareInvalidSignature,
			SecurityEventId.SigningMiddlewareOperationFailed,
			SecurityEventId.SigningMiddlewareMessageSigned,
			SecurityEventId.SigningMiddlewareNoSignatureFound,
			SecurityEventId.SigningMiddlewareSignatureVerified,
			SecurityEventId.SigningMiddlewareSignatureVerificationFailed,

			// Encryption Core (70300-70399)
			SecurityEventId.MessageEncryptionExecuting,
			SecurityEventId.MessageEncrypted,
			SecurityEventId.MessageDecrypted,
			SecurityEventId.EncryptionFailed,
			SecurityEventId.DecryptionFailed,
			SecurityEventId.DataProtectionServiceCreated,
			SecurityEventId.DataProtectionEncryptionFailed,
			SecurityEventId.DataProtectionMessageEncrypted,
			SecurityEventId.DataProtectionDecryptionFailed,
			SecurityEventId.DataProtectionMessageDecrypted,
			SecurityEventId.DataProtectionCryptographicError,
			SecurityEventId.DataProtectionKeysRotated,
			SecurityEventId.DataProtectionKeyRotationFailed,
			SecurityEventId.DataProtectionConfigurationValidated,
			SecurityEventId.DataProtectionValidationFailed,
			SecurityEventId.DataProtectionConfigurationValidationFailed,
			SecurityEventId.EncryptionMiddlewareEncryptionFailed,
			SecurityEventId.EncryptionMiddlewareMessageEncrypted,
			SecurityEventId.EncryptionMiddlewareMessageDecrypted,

			// Encryption Migration (70400-70499)
			SecurityEventId.EncryptionMigrationServiceCreated,
			SecurityEventId.EncryptionMigrationStarted,
			SecurityEventId.EncryptionMigrationCompleted,
			SecurityEventId.LazyReEncryptionExecuting,
			SecurityEventId.MessageReEncrypted,
			SecurityEventId.EncryptionKeyVersionUpdated,
			SecurityEventId.EncryptionMigrationSucceeded,
			SecurityEventId.EncryptionMigrationFailed,
			SecurityEventId.BatchMigrationStarted,
			SecurityEventId.BatchMigrationCompleted,
			SecurityEventId.BatchMigrationCancelled,
			SecurityEventId.LazyReEncryptionSucceeded,
			SecurityEventId.LazyReEncryptionFailed,
			SecurityEventId.LazyReEncryptionError,

			// Rate Limiting (70500-70599)
			SecurityEventId.SecurityRateLimitingExecuting,
			SecurityEventId.RateLimitCheckPassed,
			SecurityEventId.RateLimitExceeded,
			SecurityEventId.RateLimitWindowReset,
			SecurityEventId.RateLimitPermitAcquired,
			SecurityEventId.RateLimitInactiveLimiterRemoved,
			SecurityEventId.RateLimitCleanupCompleted,
			SecurityEventId.RateLimitCleanupError,

			// Audit Logging (70600-70699)
			SecurityEventId.SecurityEventLoggerCreated,
			SecurityEventId.SecurityEventLogged,
			SecurityEventId.AuditEntryCreated,
			SecurityEventId.SecurityIncidentDetected,
			SecurityEventId.SecurityAlertRaised,
			SecurityEventId.SecurityEventQueueFailed,
			SecurityEventId.SecurityEventLoggerStarted,
			SecurityEventId.SecurityEventLoggerStopping,
			SecurityEventId.SecurityEventLoggerStopped,
			SecurityEventId.SecurityEventProcessingTimeout,
			SecurityEventId.SecurityEventsStored,
			SecurityEventId.SecurityEventsStoreFailed,
			SecurityEventId.SecurityEventIndividualStoreFailed,
			SecurityEventId.SecurityEventProcessingLoopFailed,
			SecurityEventId.SecurityEventLoggedWithDetails,

			// Event Stores (70700-70799)
			SecurityEventId.SqlSecurityEventStoreCreated,
			SecurityEventId.FileSecurityEventStoreCreated,
			SecurityEventId.ElasticsearchSecurityEventStoreCreated,
			SecurityEventId.SecurityEventStored,
			SecurityEventId.SecurityEventRetrieved,
			SecurityEventId.SqlStoreStoringEvents,
			SecurityEventId.SqlStoreInvalidEvent,
			SecurityEventId.SqlStoreEventsStored,
			SecurityEventId.SqlStoreStoreFailed,
			SecurityEventId.SqlStoreQueryingEvents,
			SecurityEventId.SqlStoreQueryExecuted,
			SecurityEventId.SqlStoreQueryFailed,
			SecurityEventId.ElasticsearchClientInitialized,
			SecurityEventId.ElasticsearchClientInitFailed,
			SecurityEventId.ElasticsearchStoringEvents,
			SecurityEventId.ElasticsearchEventsStored,
			SecurityEventId.ElasticsearchStoreFailed,
			SecurityEventId.ElasticsearchQueryingEvents,
			SecurityEventId.ElasticsearchQueryExecuted,
			SecurityEventId.ElasticsearchQueryFailed,
			SecurityEventId.FileStoreDirectoryCreated,
			SecurityEventId.FileStoreInitFailed,
			SecurityEventId.FileStoreStoringEvents,
			SecurityEventId.FileStoreEventsStored,
			SecurityEventId.FileStoreStoreFailed,
			SecurityEventId.FileStoreLoadingEvents,
			SecurityEventId.FileStoreEventsLoaded,
			SecurityEventId.FileStoreLoadFailed,
			SecurityEventId.FileStoreQueryingEvents,
			SecurityEventId.FileStoreQueryExecuted,
			SecurityEventId.FileStoreQueryFailed,
			SecurityEventId.FileStoreRotationStarted,
			SecurityEventId.FileStoreRotationCompleted,
			SecurityEventId.FileStoreRotationFailed,
			SecurityEventId.FileStoreCleanupCompleted,
			SecurityEventId.FileStoreCleanupFailed,

			// Credential Stores (70800-70899)
			SecurityEventId.SecureCredentialProviderCreated,
			SecurityEventId.EnvironmentCredentialStoreCreated,
			SecurityEventId.CredentialRetrieved,
			SecurityEventId.CredentialNotFound,
			SecurityEventId.CredentialCached,
			SecurityEventId.CredentialProviderRetrieving,
			SecurityEventId.CredentialProviderRetrievedFromStore,
			SecurityEventId.CredentialProviderNotFoundInAny,
			SecurityEventId.CredentialProviderStoreException,
			SecurityEventId.CredentialProviderRetrievedSuccess,
			SecurityEventId.CredentialProviderRetrievalFailed,
			SecurityEventId.CredentialProviderDisposing,
			SecurityEventId.HashiCorpVaultRetrieving,
			SecurityEventId.HashiCorpVaultSecretNotFound,
			SecurityEventId.HashiCorpVaultRetrieved,
			SecurityEventId.HashiCorpVaultHttpRetrieveError,
			SecurityEventId.HashiCorpVaultRetrieveFailed,
			SecurityEventId.HashiCorpVaultStoring,
			SecurityEventId.HashiCorpVaultStored,
			SecurityEventId.HashiCorpVaultHttpStoreError,
			SecurityEventId.HashiCorpVaultStoreFailed,
			SecurityEventId.EnvironmentVariableFound,
			SecurityEventId.EnvironmentVariableNotFound,

			// Cloud Credential Stores (70900-70999)
			SecurityEventId.AzureKeyVaultCredentialStoreCreated,
			SecurityEventId.AwsSecretsManagerCredentialStoreCreated,
			SecurityEventId.HashiCorpVaultCredentialStoreCreated,
			SecurityEventId.CloudCredentialRetrieved,
			SecurityEventId.CloudCredentialAccessDenied,
			SecurityEventId.CloudCredentialStoreError,
			SecurityEventId.AzureKeyVaultRetrieving,
			SecurityEventId.AzureKeyVaultSecretNotFound,
			SecurityEventId.AzureKeyVaultRetrieved,
			SecurityEventId.AzureKeyVaultRequestFailed,
			SecurityEventId.AzureKeyVaultRetrieveFailed,
			SecurityEventId.AzureKeyVaultStoring,
			SecurityEventId.AzureKeyVaultStored,
			SecurityEventId.AwsSecretsManagerRetrieving,
			SecurityEventId.AwsSecretsManagerSecretNotFound,
			SecurityEventId.AwsSecretsManagerRetrieved,
			SecurityEventId.AwsSecretsManagerRequestFailed,
			SecurityEventId.AwsSecretsManagerRetrieveFailed,
			SecurityEventId.AwsSecretsManagerStoring,
			SecurityEventId.AwsSecretsManagerStored
		];
	}

	#endregion
}
