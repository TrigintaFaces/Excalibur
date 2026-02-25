// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Diagnostics;

/// <summary>
/// Event IDs for security components (70000-70999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>70000-70099: Authentication</item>
/// <item>70100-70199: Input Validation</item>
/// <item>70200-70299: Message Signing</item>
/// <item>70300-70399: Encryption Core</item>
/// <item>70400-70499: Encryption Migration</item>
/// <item>70500-70599: Rate Limiting</item>
/// <item>70600-70699: Audit Logging</item>
/// <item>70700-70799: Event Stores</item>
/// <item>70800-70899: Credential Stores</item>
/// <item>70900-70999: Cloud Credential Stores</item>
/// </list>
/// </remarks>
public static class SecurityEventId
{
	// ========================================
	// 70000-70099: Authentication
	// ========================================

	/// <summary>JWT authentication middleware executing.</summary>
	public const int JwtAuthenticationExecuting = 70000;

	/// <summary>JWT token validated.</summary>
	public const int JwtTokenValidated = 70001;

	/// <summary>JWT token validation failed.</summary>
	public const int JwtTokenValidationFailed = 70002;

	/// <summary>JWT token expired.</summary>
	public const int JwtTokenExpired = 70003;

	/// <summary>Authentication succeeded.</summary>
	public const int AuthenticationSucceeded = 70004;

	/// <summary>Authentication failed.</summary>
	public const int AuthenticationFailed = 70005;

	/// <summary>Skipping authentication for anonymous message type.</summary>
	public const int AuthenticationSkippedAnonymous = 70006;

	/// <summary>No authentication token found.</summary>
	public const int AuthenticationTokenMissing = 70007;

	/// <summary>No signing key found for key ID.</summary>
	public const int AuthenticationNoSigningKey = 70008;

	/// <summary>Token validation debug information.</summary>
	public const int AuthenticationValidationDebug = 70009;

	/// <summary>JWT token validation error (exception occurred).</summary>
	public const int JwtTokenValidationError = 70010;

	// ========================================
	// 70100-70199: Input Validation
	// ========================================

	/// <summary>Input validation middleware executing.</summary>
	public const int InputValidationExecuting = 70100;

	/// <summary>Input validation passed.</summary>
	public const int InputValidationPassed = 70101;

	/// <summary>Input validation failed.</summary>
	public const int InputValidationFailed = 70102;

	/// <summary>Dangerous input detected.</summary>
	public const int DangerousInputDetected = 70103;

	/// <summary>Input sanitized.</summary>
	public const int InputSanitized = 70104;

	/// <summary>Input validation disabled warning.</summary>
	public const int InputValidationDisabled = 70105;

	/// <summary>Custom validator failed.</summary>
	public const int InputValidatorFailed = 70106;

	/// <summary>Unexpected error during input validation.</summary>
	public const int InputValidationUnexpectedError = 70107;

	// ========================================
	// 70200-70299: Message Signing
	// ========================================

	/// <summary>Message signing middleware executing.</summary>
	public const int MessageSigningExecuting = 70200;

	/// <summary>Message signed.</summary>
	public const int MessageSigned = 70201;

	/// <summary>Message signature verified.</summary>
	public const int SignatureVerified = 70202;

	/// <summary>Message signature verification failed.</summary>
	public const int SignatureVerificationFailed = 70203;

	/// <summary>HMAC signing service created.</summary>
	public const int HmacSigningServiceCreated = 70204;

	/// <summary>Secure key provider created.</summary>
	public const int SecureKeyProviderCreated = 70205;

	/// <summary>Signing key rotated.</summary>
	public const int SigningKeyRotated = 70206;

	/// <summary>Key Vault initialized.</summary>
	public const int KeyVaultInitialized = 70207;

	/// <summary>Key Vault initialization failed.</summary>
	public const int KeyVaultInitializationFailed = 70208;

	/// <summary>Key retrieved from cache.</summary>
	public const int KeyRetrievedFromCache = 70209;

	/// <summary>Key not found, generating new key.</summary>
	public const int KeyNotFoundGeneratingNew = 70210;

	/// <summary>New key stored.</summary>
	public const int NewKeyStored = 70211;

	/// <summary>Key not found in Key Vault.</summary>
	public const int KeyNotFoundInKeyVault = 70212;

	/// <summary>Failed to store key in Key Vault.</summary>
	public const int FailedToStoreKeyInKeyVault = 70213;

	/// <summary>Invalid key format in configuration.</summary>
	public const int InvalidKeyFormat = 70214;

	/// <summary>Expired key removed from cache.</summary>
	public const int ExpiredKeyRemoved = 70215;

	/// <summary>Expired keys cleaned up from cache.</summary>
	public const int ExpiredKeysCleanedUp = 70216;

	/// <summary>Storing key in memory only.</summary>
	public const int StoringKeyInMemoryOnly = 70217;

	/// <summary>HMAC message signed.</summary>
	public const int HmacMessageSigned = 70218;

	/// <summary>HMAC signing failed.</summary>
	public const int HmacSigningFailed = 70219;

	/// <summary>Signature verification successful.</summary>
	public const int HmacVerificationSuccessful = 70220;

	/// <summary>Signature verification failed.</summary>
	public const int HmacVerificationFailed = 70221;

	/// <summary>Signature verification error.</summary>
	public const int HmacVerificationError = 70222;

	/// <summary>Signature expired.</summary>
	public const int SignatureExpired = 70223;

	/// <summary>Message signing middleware invalid signature.</summary>
	public const int SigningMiddlewareInvalidSignature = 70224;

	/// <summary>Message signing middleware operation failed.</summary>
	public const int SigningMiddlewareOperationFailed = 70225;

	/// <summary>Message signing middleware signed.</summary>
	public const int SigningMiddlewareMessageSigned = 70226;

	/// <summary>Message signing middleware no signature found.</summary>
	public const int SigningMiddlewareNoSignatureFound = 70227;

	/// <summary>Message signing middleware signature verified.</summary>
	public const int SigningMiddlewareSignatureVerified = 70228;

	/// <summary>Message signing middleware signature verification failed.</summary>
	public const int SigningMiddlewareSignatureVerificationFailed = 70229;

	// ========================================
	// 70300-70399: Encryption Core
	// ========================================

	/// <summary>Message encryption middleware executing.</summary>
	public const int MessageEncryptionExecuting = 70300;

	/// <summary>Message encrypted.</summary>
	public const int MessageEncrypted = 70301;

	/// <summary>Message decrypted.</summary>
	public const int MessageDecrypted = 70302;

	/// <summary>Encryption failed.</summary>
	public const int EncryptionFailed = 70303;

	/// <summary>Decryption failed.</summary>
	public const int DecryptionFailed = 70304;

	/// <summary>Data protection service created.</summary>
	public const int DataProtectionServiceCreated = 70305;

	/// <summary>Data protection encryption failed.</summary>
	public const int DataProtectionEncryptionFailed = 70306;

	/// <summary>Data protection message encrypted.</summary>
	public const int DataProtectionMessageEncrypted = 70307;

	/// <summary>Data protection decryption failed.</summary>
	public const int DataProtectionDecryptionFailed = 70308;

	/// <summary>Data protection message decrypted.</summary>
	public const int DataProtectionMessageDecrypted = 70309;

	/// <summary>Data protection cryptographic error.</summary>
	public const int DataProtectionCryptographicError = 70310;

	/// <summary>Data protection keys rotated.</summary>
	public const int DataProtectionKeysRotated = 70311;

	/// <summary>Data protection key rotation failed.</summary>
	public const int DataProtectionKeyRotationFailed = 70312;

	/// <summary>Data protection configuration validated.</summary>
	public const int DataProtectionConfigurationValidated = 70313;

	/// <summary>Data protection validation failed.</summary>
	public const int DataProtectionValidationFailed = 70314;

	/// <summary>Data protection configuration validation failed.</summary>
	public const int DataProtectionConfigurationValidationFailed = 70315;

	/// <summary>Message encryption middleware encryption failed.</summary>
	public const int EncryptionMiddlewareEncryptionFailed = 70316;

	/// <summary>Message encryption middleware message encrypted.</summary>
	public const int EncryptionMiddlewareMessageEncrypted = 70317;

	/// <summary>Message encryption middleware message decrypted.</summary>
	public const int EncryptionMiddlewareMessageDecrypted = 70318;

	// ========================================
	// 70400-70499: Encryption Migration
	// ========================================

	/// <summary>Encryption migration service created.</summary>
	public const int EncryptionMigrationServiceCreated = 70400;

	/// <summary>Encryption migration started.</summary>
	public const int EncryptionMigrationStarted = 70401;

	/// <summary>Encryption migration completed.</summary>
	public const int EncryptionMigrationCompleted = 70402;

	/// <summary>Lazy re-encryption middleware executing.</summary>
	public const int LazyReEncryptionExecuting = 70403;

	/// <summary>Message re-encrypted.</summary>
	public const int MessageReEncrypted = 70404;

	/// <summary>Encryption key version updated.</summary>
	public const int EncryptionKeyVersionUpdated = 70405;

	/// <summary>Encryption migration succeeded.</summary>
	public const int EncryptionMigrationSucceeded = 70406;

	/// <summary>Encryption migration failed.</summary>
	public const int EncryptionMigrationFailed = 70407;

	/// <summary>Batch migration started.</summary>
	public const int BatchMigrationStarted = 70408;

	/// <summary>Batch migration completed.</summary>
	public const int BatchMigrationCompleted = 70409;

	/// <summary>Batch migration cancelled.</summary>
	public const int BatchMigrationCancelled = 70410;

	/// <summary>Lazy re-encryption succeeded.</summary>
	public const int LazyReEncryptionSucceeded = 70411;

	/// <summary>Lazy re-encryption failed.</summary>
	public const int LazyReEncryptionFailed = 70412;

	/// <summary>Lazy re-encryption error.</summary>
	public const int LazyReEncryptionError = 70413;

	// ========================================
	// 70500-70599: Rate Limiting
	// ========================================

	/// <summary>Security rate limiting middleware executing.</summary>
	public const int SecurityRateLimitingExecuting = 70500;

	/// <summary>Rate limit check passed.</summary>
	public const int RateLimitCheckPassed = 70501;

	/// <summary>Rate limit exceeded.</summary>
	public const int RateLimitExceeded = 70502;

	/// <summary>Rate limit window reset.</summary>
	public const int RateLimitWindowReset = 70503;

	/// <summary>Rate limit permit acquired.</summary>
	public const int RateLimitPermitAcquired = 70504;

	/// <summary>Inactive rate limiter removed.</summary>
	public const int RateLimitInactiveLimiterRemoved = 70505;

	/// <summary>Rate limiter cleanup completed.</summary>
	public const int RateLimitCleanupCompleted = 70506;

	/// <summary>Rate limiter cleanup error.</summary>
	public const int RateLimitCleanupError = 70507;

	// ========================================
	// 70600-70699: Audit Logging (SecurityEventLogger)
	// ========================================

	/// <summary>Security event logger created.</summary>
	public const int SecurityEventLoggerCreated = 70600;

	/// <summary>Security event logged.</summary>
	public const int SecurityEventLogged = 70601;

	/// <summary>Security audit entry created.</summary>
	public const int AuditEntryCreated = 70602;

	/// <summary>Security incident detected.</summary>
	public const int SecurityIncidentDetected = 70603;

	/// <summary>Security alert raised.</summary>
	public const int SecurityAlertRaised = 70604;

	/// <summary>Failed to queue security event - channel closed.</summary>
	public const int SecurityEventQueueFailed = 70605;

	/// <summary>Security event logger started.</summary>
	public const int SecurityEventLoggerStarted = 70606;

	/// <summary>Security event logger stopping.</summary>
	public const int SecurityEventLoggerStopping = 70607;

	/// <summary>Security event logger stopped.</summary>
	public const int SecurityEventLoggerStopped = 70608;

	/// <summary>Security event processing timeout.</summary>
	public const int SecurityEventProcessingTimeout = 70609;

	/// <summary>Security events stored.</summary>
	public const int SecurityEventsStored = 70610;

	/// <summary>Failed to store security events.</summary>
	public const int SecurityEventsStoreFailed = 70611;

	/// <summary>Failed to store individual security event.</summary>
	public const int SecurityEventIndividualStoreFailed = 70612;

	/// <summary>Security event processing loop failed.</summary>
	public const int SecurityEventProcessingLoopFailed = 70613;

	/// <summary>Security event logged with details.</summary>
	public const int SecurityEventLoggedWithDetails = 70614;

	// ========================================
	// 70700-70799: Event Stores
	// ========================================

	/// <summary>SQL security event store created.</summary>
	public const int SqlSecurityEventStoreCreated = 70700;

	/// <summary>File security event store created.</summary>
	public const int FileSecurityEventStoreCreated = 70701;

	/// <summary>Elasticsearch security event store created.</summary>
	public const int ElasticsearchSecurityEventStoreCreated = 70702;

	/// <summary>Security event stored.</summary>
	public const int SecurityEventStored = 70703;

	/// <summary>Security event retrieved.</summary>
	public const int SecurityEventRetrieved = 70704;

	// SQL Security Event Store (70705-70714)

	/// <summary>Storing security events in SQL database.</summary>
	public const int SqlStoreStoringEvents = 70705;

	/// <summary>Invalid security event detected.</summary>
	public const int SqlStoreInvalidEvent = 70706;

	/// <summary>Security events stored in SQL database.</summary>
	public const int SqlStoreEventsStored = 70707;

	/// <summary>Failed to store events in SQL database.</summary>
	public const int SqlStoreStoreFailed = 70708;

	/// <summary>Querying events from SQL database.</summary>
	public const int SqlStoreQueryingEvents = 70709;

	/// <summary>SQL query executed successfully.</summary>
	public const int SqlStoreQueryExecuted = 70710;

	/// <summary>Failed to query events from SQL database.</summary>
	public const int SqlStoreQueryFailed = 70711;

	// Elasticsearch Security Event Store (70715-70729)

	/// <summary>Elasticsearch client initialized.</summary>
	public const int ElasticsearchClientInitialized = 70715;

	/// <summary>Elasticsearch client initialization failed.</summary>
	public const int ElasticsearchClientInitFailed = 70716;

	/// <summary>Storing events in Elasticsearch.</summary>
	public const int ElasticsearchStoringEvents = 70717;

	/// <summary>Events stored in Elasticsearch.</summary>
	public const int ElasticsearchEventsStored = 70718;

	/// <summary>Failed to store events in Elasticsearch.</summary>
	public const int ElasticsearchStoreFailed = 70719;

	/// <summary>Querying events from Elasticsearch.</summary>
	public const int ElasticsearchQueryingEvents = 70720;

	/// <summary>Query executed in Elasticsearch.</summary>
	public const int ElasticsearchQueryExecuted = 70721;

	/// <summary>Failed to query events from Elasticsearch.</summary>
	public const int ElasticsearchQueryFailed = 70722;

	// File Security Event Store (70730-70759)

	/// <summary>File store directory created.</summary>
	public const int FileStoreDirectoryCreated = 70730;

	/// <summary>File store initialization failed.</summary>
	public const int FileStoreInitFailed = 70731;

	/// <summary>Storing events to file.</summary>
	public const int FileStoreStoringEvents = 70732;

	/// <summary>Events stored to file.</summary>
	public const int FileStoreEventsStored = 70733;

	/// <summary>Failed to store events to file.</summary>
	public const int FileStoreStoreFailed = 70734;

	/// <summary>Loading events from file.</summary>
	public const int FileStoreLoadingEvents = 70735;

	/// <summary>Events loaded from file.</summary>
	public const int FileStoreEventsLoaded = 70736;

	/// <summary>Failed to load events from file.</summary>
	public const int FileStoreLoadFailed = 70737;

	/// <summary>Querying events from file store.</summary>
	public const int FileStoreQueryingEvents = 70738;

	/// <summary>Query executed in file store.</summary>
	public const int FileStoreQueryExecuted = 70739;

	/// <summary>Failed to query events from file store.</summary>
	public const int FileStoreQueryFailed = 70740;

	/// <summary>Event file rotation started.</summary>
	public const int FileStoreRotationStarted = 70741;

	/// <summary>Event file rotation completed.</summary>
	public const int FileStoreRotationCompleted = 70742;

	/// <summary>Event file rotation failed.</summary>
	public const int FileStoreRotationFailed = 70743;

	/// <summary>Old event files cleaned up.</summary>
	public const int FileStoreCleanupCompleted = 70744;

	/// <summary>File store cleanup failed.</summary>
	public const int FileStoreCleanupFailed = 70745;

	// ========================================
	// 70800-70899: Credential Stores
	// ========================================

	/// <summary>Secure credential provider created.</summary>
	public const int SecureCredentialProviderCreated = 70800;

	/// <summary>Environment variable credential store created.</summary>
	public const int EnvironmentCredentialStoreCreated = 70801;

	/// <summary>Credential retrieved.</summary>
	public const int CredentialRetrieved = 70802;

	/// <summary>Credential not found.</summary>
	public const int CredentialNotFound = 70803;

	/// <summary>Credential cached.</summary>
	public const int CredentialCached = 70804;

	// Secure Credential Provider (70805-70814)

	/// <summary>Attempting to retrieve credential from stores.</summary>
	public const int CredentialProviderRetrieving = 70805;

	/// <summary>Credential retrieved from store.</summary>
	public const int CredentialProviderRetrievedFromStore = 70806;

	/// <summary>Credential not found in any store.</summary>
	public const int CredentialProviderNotFoundInAny = 70807;

	/// <summary>Credential store threw exception.</summary>
	public const int CredentialProviderStoreException = 70808;

	/// <summary>Credential retrieved successfully.</summary>
	public const int CredentialProviderRetrievedSuccess = 70809;

	/// <summary>Credential retrieval failed.</summary>
	public const int CredentialProviderRetrievalFailed = 70810;

	/// <summary>Credential provider disposing.</summary>
	public const int CredentialProviderDisposing = 70811;

	// HashiCorp Vault Credential Store (70815-70829)

	/// <summary>Retrieving credential from HashiCorp Vault.</summary>
	public const int HashiCorpVaultRetrieving = 70815;

	/// <summary>Secret not found in HashiCorp Vault.</summary>
	public const int HashiCorpVaultSecretNotFound = 70816;

	/// <summary>Credential retrieved from HashiCorp Vault.</summary>
	public const int HashiCorpVaultRetrieved = 70817;

	/// <summary>HTTP error retrieving from HashiCorp Vault.</summary>
	public const int HashiCorpVaultHttpRetrieveError = 70818;

	/// <summary>Failed to retrieve from HashiCorp Vault.</summary>
	public const int HashiCorpVaultRetrieveFailed = 70819;

	/// <summary>Storing credential in HashiCorp Vault.</summary>
	public const int HashiCorpVaultStoring = 70820;

	/// <summary>Credential stored in HashiCorp Vault.</summary>
	public const int HashiCorpVaultStored = 70821;

	/// <summary>HTTP error storing in HashiCorp Vault.</summary>
	public const int HashiCorpVaultHttpStoreError = 70822;

	/// <summary>Failed to store in HashiCorp Vault.</summary>
	public const int HashiCorpVaultStoreFailed = 70823;

	// Environment Variable Credential Store (70830-70839)

	/// <summary>Environment variable found.</summary>
	public const int EnvironmentVariableFound = 70830;

	/// <summary>Environment variable not found.</summary>
	public const int EnvironmentVariableNotFound = 70831;

	// ========================================
	// 70900-70999: Cloud Credential Stores
	// ========================================

	/// <summary>Azure Key Vault credential store created.</summary>
	public const int AzureKeyVaultCredentialStoreCreated = 70900;

	/// <summary>AWS Secrets Manager credential store created.</summary>
	public const int AwsSecretsManagerCredentialStoreCreated = 70901;

	/// <summary>HashiCorp Vault credential store created.</summary>
	public const int HashiCorpVaultCredentialStoreCreated = 70902;

	/// <summary>Cloud credential retrieved.</summary>
	public const int CloudCredentialRetrieved = 70903;

	/// <summary>Cloud credential access denied.</summary>
	public const int CloudCredentialAccessDenied = 70904;

	/// <summary>Cloud credential store connection error.</summary>
	public const int CloudCredentialStoreError = 70905;

	// Azure Key Vault Credential Store (70906-70919)

	/// <summary>Retrieving credential from Azure Key Vault.</summary>
	public const int AzureKeyVaultRetrieving = 70906;

	/// <summary>Secret not found in Azure Key Vault.</summary>
	public const int AzureKeyVaultSecretNotFound = 70907;

	/// <summary>Credential retrieved from Azure Key Vault.</summary>
	public const int AzureKeyVaultRetrieved = 70908;

	/// <summary>Request failed for Azure Key Vault.</summary>
	public const int AzureKeyVaultRequestFailed = 70909;

	/// <summary>Failed to retrieve from Azure Key Vault.</summary>
	public const int AzureKeyVaultRetrieveFailed = 70910;

	/// <summary>Storing credential in Azure Key Vault.</summary>
	public const int AzureKeyVaultStoring = 70911;

	/// <summary>Credential stored in Azure Key Vault.</summary>
	public const int AzureKeyVaultStored = 70912;

	// AWS Secrets Manager Credential Store (70920-70939)

	/// <summary>Retrieving credential from AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerRetrieving = 70920;

	/// <summary>Secret not found in AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerSecretNotFound = 70921;

	/// <summary>Credential retrieved from AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerRetrieved = 70922;

	/// <summary>Request failed for AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerRequestFailed = 70923;

	/// <summary>Failed to retrieve from AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerRetrieveFailed = 70924;

	/// <summary>Storing credential in AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerStoring = 70925;

	/// <summary>Credential stored in AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerStored = 70926;
}
