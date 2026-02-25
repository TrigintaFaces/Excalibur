// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Centralized catalog of error codes used throughout the Excalibur framework. Error codes follow a pattern: [CATEGORY][NUMBER] where
/// CATEGORY is a 3-letter prefix.
/// </summary>
public static class ErrorCodes
{
	// Unknown/General errors (UNK)

	/// <summary>
	/// Unknown or unspecified error.
	/// </summary>
	public const string UnknownError = "UNK001";

	// Configuration errors (CFG)

	/// <summary>
	/// Required configuration is missing.
	/// </summary>
	public const string ConfigurationMissing = "CFG001";

	/// <summary>
	/// Configuration value is invalid.
	/// </summary>
	public const string ConfigurationInvalid = "CFG002";

	/// <summary>
	/// Configuration section not found.
	/// </summary>
	public const string ConfigurationSectionNotFound = "CFG003";

	/// <summary>
	/// Configuration cannot be loaded.
	/// </summary>
	public const string ConfigurationLoadFailed = "CFG004";

	// Validation errors (VAL)

	/// <summary>
	/// Input validation failed.
	/// </summary>
	public const string ValidationFailed = "VAL001";

	/// <summary>
	/// Required field is missing.
	/// </summary>
	public const string ValidationRequiredFieldMissing = "VAL002";

	/// <summary>
	/// Field value is out of valid range.
	/// </summary>
	public const string ValidationOutOfRange = "VAL003";

	/// <summary>
	/// Field format is invalid.
	/// </summary>
	public const string ValidationInvalidFormat = "VAL004";

	/// <summary>
	/// Business rule validation failed.
	/// </summary>
	public const string ValidationBusinessRuleFailed = "VAL005";

	// Messaging errors (MSG)

	/// <summary>
	/// Message could not be sent.
	/// </summary>
	public const string MessageSendFailed = "MSG001";

	/// <summary>
	/// Message could not be received.
	/// </summary>
	public const string MessageReceiveFailed = "MSG002";

	/// <summary>
	/// Message queue is unavailable.
	/// </summary>
	public const string MessageQueueUnavailable = "MSG003";

	/// <summary>
	/// Message broker connection failed.
	/// </summary>
	public const string MessageBrokerConnectionFailed = "MSG004";

	/// <summary>
	/// Message handler not found.
	/// </summary>
	public const string MessageHandlerNotFound = "MSG005";

	/// <summary>
	/// Message routing failed.
	/// </summary>
	public const string MessageRoutingFailed = "MSG006";

	/// <summary>
	/// Message is a duplicate.
	/// </summary>
	public const string MessageDuplicate = "MSG007";

	/// <summary>
	/// Message exceeded retry limit.
	/// </summary>
	public const string MessageRetryLimitExceeded = "MSG008";

	// Serialization errors (SER)

	/// <summary>
	/// Serialization failed.
	/// </summary>
	public const string SerializationFailed = "SER001";

	/// <summary>
	/// Deserialization failed.
	/// </summary>
	public const string DeserializationFailed = "SER002";

	/// <summary>
	/// Unsupported serialization format.
	/// </summary>
	public const string SerializationFormatUnsupported = "SER003";

	/// <summary>
	/// Schema validation failed.
	/// </summary>
	public const string SerializationSchemaValidationFailed = "SER004";

	// Network errors (NET)

	/// <summary>
	/// Network connection failed.
	/// </summary>
	public const string NetworkConnectionFailed = "NET001";

	/// <summary>
	/// Network timeout occurred.
	/// </summary>
	public const string NetworkTimeout = "NET002";

	/// <summary>
	/// DNS resolution failed.
	/// </summary>
	public const string NetworkDnsResolutionFailed = "NET003";

	/// <summary>
	/// Network is unavailable.
	/// </summary>
	public const string NetworkUnavailable = "NET004";

	// Security errors (SEC)

	/// <summary>
	/// Authentication failed.
	/// </summary>
	public const string SecurityAuthenticationFailed = "SEC001";

	/// <summary>
	/// Authorization failed.
	/// </summary>
	public const string SecurityAuthorizationFailed = "SEC002";

	/// <summary>
	/// Token is invalid or expired.
	/// </summary>
	public const string SecurityTokenInvalid = "SEC003";

	/// <summary>
	/// Access denied.
	/// </summary>
	public const string SecurityAccessDenied = "SEC004";

	/// <summary>
	/// Encryption/decryption failed.
	/// </summary>
	public const string SecurityEncryptionFailed = "SEC005";

	/// <summary>
	/// Forbidden - the user is authenticated but lacks permission for the requested operation.
	/// </summary>
	public const string SecurityForbidden = "SEC006";

	// Data errors (DAT)

	/// <summary>
	/// Data not found.
	/// </summary>
	public const string DataNotFound = "DAT001";

	/// <summary>
	/// Data already exists.
	/// </summary>
	public const string DataAlreadyExists = "DAT002";

	/// <summary>
	/// Data concurrency conflict.
	/// </summary>
	public const string DataConcurrencyConflict = "DAT003";

	/// <summary>
	/// Database connection failed.
	/// </summary>
	public const string DataConnectionFailed = "DAT004";

	/// <summary>
	/// Data transaction failed.
	/// </summary>
	public const string DataTransactionFailed = "DAT005";

	/// <summary>
	/// Data integrity violation.
	/// </summary>
	public const string DataIntegrityViolation = "DAT006";

	// Timeout errors (TIM)

	/// <summary>
	/// Operation timeout.
	/// </summary>
	public const string TimeoutOperation = "TIM001";

	/// <summary>
	/// Request timeout.
	/// </summary>
	public const string TimeoutRequest = "TIM002";

	/// <summary>
	/// Lock acquisition timeout.
	/// </summary>
	public const string TimeoutLockAcquisition = "TIM003";

	/// <summary>
	/// Operation exceeded its time limit.
	/// </summary>
	public const string TimeoutOperationExceeded = "TIM004";

	// Resource errors (RES)

	/// <summary>
	/// Resource not found.
	/// </summary>
	public const string ResourceNotFound = "RES001";

	/// <summary>
	/// Resource exhausted.
	/// </summary>
	public const string ResourceExhausted = "RES002";

	/// <summary>
	/// Resource unavailable.
	/// </summary>
	public const string ResourceUnavailable = "RES003";

	/// <summary>
	/// Resource locked.
	/// </summary>
	public const string ResourceLocked = "RES004";

	/// <summary>
	/// Resource quota exceeded.
	/// </summary>
	public const string ResourceQuotaExceeded = "RES005";

	/// <summary>
	/// Resource conflict - the operation cannot be completed due to a conflict with the current state.
	/// </summary>
	public const string ResourceConflict = "RES006";

	/// <summary>
	/// Concurrency conflict - optimistic locking failure due to version mismatch.
	/// </summary>
	public const string ResourceConcurrency = "RES007";

	// System errors (SYS)

	/// <summary>
	/// Out of memory.
	/// </summary>
	public const string SystemOutOfMemory = "SYS001";

	/// <summary>
	/// IO operation failed.
	/// </summary>
	public const string SystemIOFailed = "SYS002";

	/// <summary>
	/// System is shutting down.
	/// </summary>
	public const string SystemShuttingDown = "SYS003";

	/// <summary>
	/// System component failed.
	/// </summary>
	public const string SystemComponentFailed = "SYS004";

	// Resilience errors (RSL)

	/// <summary>
	/// Circuit breaker is open.
	/// </summary>
	public const string ResilienceCircuitBreakerOpen = "RSL001";

	/// <summary>
	/// Retry policy exhausted.
	/// </summary>
	public const string ResilienceRetryExhausted = "RSL002";

	/// <summary>
	/// Fallback failed.
	/// </summary>
	public const string ResilienceFallbackFailed = "RSL003";

	/// <summary>
	/// Bulkhead rejected.
	/// </summary>
	public const string ResilienceBulkheadRejected = "RSL004";

	// Concurrency errors (CON)

	/// <summary>
	/// Deadlock detected.
	/// </summary>
	public const string ConcurrencyDeadlock = "CON001";

	/// <summary>
	/// Race condition detected.
	/// </summary>
	public const string ConcurrencyRaceCondition = "CON002";

	/// <summary>
	/// Lock contention.
	/// </summary>
	public const string ConcurrencyLockContention = "CON003";

	/// <summary>
	/// Optimistic concurrency failure.
	/// </summary>
	public const string ConcurrencyOptimisticLockFailed = "CON004";

	// Result errors (RST)

	/// <summary>
	/// Result unwrap failed because the result was not successful or had no value.
	/// </summary>
	public const string ResultUnwrapFailed = "RST001";
}
