// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Event IDs for pipeline middleware components (30000-31999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>30000-30099: Retry/CircuitBreaker</item>
/// <item>30100-30199: RateLimiting</item>
/// <item>30200-30299: Timeout</item>
/// <item>30300-30399: Batching</item>
/// <item>30400-30499: Authentication</item>
/// <item>30500-30599: Authorization</item>
/// <item>30600-30699: InputSanitization</item>
/// <item>30700-30799: Outbox</item>
/// <item>30800-30899: Inbox</item>
/// <item>30900-30999: Idempotency</item>
/// <item>31000-31099: Validation</item>
/// <item>31100-31199: Transactions</item>
/// <item>31200-31299: Tenancy</item>
/// <item>31300-31399: ContractVersion</item>
/// <item>31400-31499: AuditLogging</item>
/// <item>31500-31599: ExceptionMapping</item>
/// <item>31600-31699: Routing</item>
/// <item>31700-31799: MetricsLogging</item>
/// </list>
/// </remarks>
public static class MiddlewareEventId
{
	// ========================================
	// 30000-30099: Retry/CircuitBreaker Middleware
	// ========================================

	/// <summary>Retry middleware executing.</summary>
	public const int RetryMiddlewareExecuting = 30000;

	/// <summary>Retry attempt started.</summary>
	public const int RetryAttemptStarted = 30001;

	/// <summary>Retry succeeded.</summary>
	public const int RetrySucceeded = 30002;

	/// <summary>Retry failed, will retry.</summary>
	public const int RetryFailed = 30003;

	/// <summary>Retry exhausted.</summary>
	public const int RetryExhausted = 30004;

	/// <summary>Retry delay applied.</summary>
	public const int RetryDelayApplied = 30005;

	/// <summary>Retry policy executed.</summary>
	public const int RetryPolicyExecuted = 30006;

	/// <summary>Circuit breaker middleware executing.</summary>
	public const int CircuitBreakerMiddlewareExecuting = 30010;

	/// <summary>Circuit breaker blocked request.</summary>
	public const int CircuitBreakerBlocked = 30011;

	/// <summary>Circuit breaker allowed request.</summary>
	public const int CircuitBreakerAllowed = 30012;

	/// <summary>Waiting before retry.</summary>
	public const int RetryWaiting = 30013;

	/// <summary>Non-retryable exception occurred.</summary>
	public const int NonRetryableException = 30014;

	/// <summary>Circuit breaker state open.</summary>
	public const int CircuitBreakerStateOpen = 30015;

	/// <summary>Circuit breaker state half-open.</summary>
	public const int CircuitBreakerStateHalfOpen = 30016;

	/// <summary>Circuit breaker state closed.</summary>
	public const int CircuitBreakerStateClosed = 30017;

	/// <summary>Circuit breaker transition.</summary>
	public const int CircuitBreakerTransition = 30018;

	/// <summary>Circuit breaker timeout.</summary>
	public const int CircuitBreakerTimeout = 30019;

	// ========================================
	// 30100-30199: RateLimiting Middleware
	// ========================================

	/// <summary>Rate limiting middleware executing.</summary>
	public const int RateLimitingMiddlewareExecuting = 30100;

	/// <summary>Request permitted by rate limiter.</summary>
	public const int RateLimitPermitted = 30101;

	/// <summary>Request rejected by rate limiter.</summary>
	public const int RateLimitRejected = 30102;

	/// <summary>Rate limit threshold reached.</summary>
	public const int RateLimitThresholdReached = 30103;

	/// <summary>Rate limiter configuration applied.</summary>
	public const int RateLimiterConfigured = 30104;

	/// <summary>Rate limit sliding window.</summary>
	public const int RateLimitSlidingWindow = 30105;

	/// <summary>Rate limit lease acquired.</summary>
	public const int RateLimitLeaseAcquired = 30106;

	// ========================================
	// 30200-30299: Timeout Middleware
	// ========================================

	/// <summary>Timeout middleware executing.</summary>
	public const int TimeoutMiddlewareExecuting = 30200;

	/// <summary>Operation completed within timeout.</summary>
	public const int TimeoutCompleted = 30201;

	/// <summary>Operation timed out.</summary>
	public const int TimeoutExceeded = 30202;

	/// <summary>Timeout cancellation requested.</summary>
	public const int TimeoutCancellationRequested = 30203;

	/// <summary>Context override timeout applied.</summary>
	public const int TimeoutContextOverride = 30204;

	/// <summary>Message type specific timeout applied.</summary>
	public const int TimeoutMessageTypeSpecific = 30205;

	/// <summary>Message kind timeout applied.</summary>
	public const int TimeoutMessageKind = 30206;

	/// <summary>Timeout processing error.</summary>
	public const int TimeoutProcessingError = 30207;

	// ========================================
	// 30300-30399: Batching Middleware
	// ========================================

	/// <summary>Batching middleware executing.</summary>
	public const int BatchingMiddlewareExecuting = 30300;

	/// <summary>Batch created.</summary>
	public const int BatchCreated = 30301;

	/// <summary>Batch flushed.</summary>
	public const int BatchFlushed = 30302;

	/// <summary>Message added to batch.</summary>
	public const int MessageAddedToBatch = 30303;

	/// <summary>Batch size threshold reached.</summary>
	public const int BatchSizeThresholdReached = 30304;

	/// <summary>Batch time threshold reached.</summary>
	public const int BatchTimeThresholdReached = 30305;

	/// <summary>Bulk optimization applied.</summary>
	public const int BulkOptimizationApplied = 30306;

	/// <summary>Batch processing error.</summary>
	public const int BatchProcessingError = 30307;

	/// <summary>Batch completed successfully.</summary>
	public const int BatchCompleted = 30308;

	/// <summary>Message not batched.</summary>
	public const int MessageNotBatched = 30309;

	/// <summary>Batch processing started.</summary>
	public const int BatchProcessingStarted = 30310;

	// ========================================
	// 30400-30499: Authentication Middleware
	// ========================================

	/// <summary>Authentication middleware executing.</summary>
	public const int AuthenticationMiddlewareExecuting = 30400;

	/// <summary>Authentication succeeded.</summary>
	public const int AuthenticationSucceeded = 30401;

	/// <summary>Authentication failed.</summary>
	public const int AuthenticationFailed = 30402;

	/// <summary>Authentication token validated.</summary>
	public const int TokenValidated = 30403;

	/// <summary>Authentication token expired.</summary>
	public const int TokenExpired = 30404;

	/// <summary>Authentication token invalid.</summary>
	public const int TokenInvalid = 30405;

	/// <summary>Anonymous access allowed.</summary>
	public const int AnonymousAccessAllowed = 30406;

	/// <summary>Authentication context extracted.</summary>
	public const int AuthenticationContextExtracted = 30407;

	/// <summary>No authentication scheme matched.</summary>
	public const int NoAuthenticationSchemeMatched = 30408;

	/// <summary>Authentication provider invoked.</summary>
	public const int AuthenticationProviderInvoked = 30409;

	/// <summary>Authentication claims validated.</summary>
	public const int AuthenticationClaimsValidated = 30410;

	/// <summary>Authentication error details.</summary>
	public const int AuthenticationErrorDetails = 30411;

	// ========================================
	// 30500-30599: Authorization Middleware
	// ========================================

	/// <summary>Authorization middleware executing.</summary>
	public const int AuthorizationMiddlewareExecuting = 30500;

	/// <summary>Authorization granted.</summary>
	public const int AuthorizationGranted = 30501;

	/// <summary>Authorization denied.</summary>
	public const int AuthorizationDenied = 30502;

	/// <summary>Policy evaluation started.</summary>
	public const int PolicyEvaluationStarted = 30503;

	/// <summary>Policy evaluation completed.</summary>
	public const int PolicyEvaluationCompleted = 30504;

	// ========================================
	// 30600-30699: InputSanitization Middleware
	// ========================================

	/// <summary>Input sanitization middleware executing.</summary>
	public const int InputSanitizationMiddlewareExecuting = 30600;

	/// <summary>Input sanitized.</summary>
	public const int InputSanitized = 30601;

	/// <summary>Dangerous input detected.</summary>
	public const int DangerousInputDetected = 30602;

	/// <summary>Input validation passed.</summary>
	public const int InputValidationPassed = 30603;

	// ========================================
	// 30700-30799: Outbox Middleware
	// ========================================

	/// <summary>Outbox middleware executing.</summary>
	public const int OutboxMiddlewareExecuting = 30700;

	/// <summary>Message staged in outbox.</summary>
	public const int MessageStagedInOutbox = 30701;

	/// <summary>Outbox staging completed.</summary>
	public const int OutboxStagingCompleted = 30702;

	/// <summary>Outbox staging failed.</summary>
	public const int OutboxStagingFailed = 30703;

	// ========================================
	// 30800-30899: Inbox Middleware
	// ========================================

	/// <summary>Inbox middleware executing.</summary>
	public const int InboxMiddlewareExecuting = 30800;

	/// <summary>Message received in inbox.</summary>
	public const int MessageReceivedInInbox = 30801;

	/// <summary>Inbox message processed.</summary>
	public const int InboxMessageProcessed = 30802;

	/// <summary>Inbox duplicate detected.</summary>
	public const int InboxDuplicateDetected = 30803;

	// ========================================
	// 30900-30999: Idempotency Middleware
	// ========================================

	/// <summary>Idempotency middleware executing.</summary>
	public const int IdempotencyMiddlewareExecuting = 30900;

	/// <summary>Idempotency check passed.</summary>
	public const int IdempotencyCheckPassed = 30901;

	/// <summary>Duplicate request detected.</summary>
	public const int DuplicateRequestDetected = 30902;

	/// <summary>Idempotency key generated.</summary>
	public const int IdempotencyKeyGenerated = 30903;

	/// <summary>Cached response returned.</summary>
	public const int CachedResponseReturned = 30904;

	// ========================================
	// 31000-31099: Validation Middleware
	// ========================================

	/// <summary>Validation middleware executing.</summary>
	public const int ValidationMiddlewareExecuting = 31000;

	/// <summary>Validation passed.</summary>
	public const int ValidationPassed = 31001;

	/// <summary>Validation failed.</summary>
	public const int ValidationFailed = 31002;

	/// <summary>Validation error details.</summary>
	public const int ValidationErrorDetails = 31003;

	/// <summary>Validation failed with detailed error information.</summary>
	public const int ValidationFailedWithDetails = 31004;

	/// <summary>Validation operation timed out.</summary>
	public const int ValidationTimedOut = 31005;

	/// <summary>Unexpected error during validation service operation.</summary>
	public const int ValidationServiceError = 31006;

	/// <summary>Profile-specific validation failed.</summary>
	public const int ProfileValidationFailed = 31007;

	/// <summary>Profile validation rules applied.</summary>
	public const int ProfileValidationApplied = 31008;

	/// <summary>Trace context validation found issues.</summary>
	public const int TraceValidationIssues = 31009;

	/// <summary>Regex timeout during field validation.</summary>
	public const int ValidationRegexTimeout = 31010;

	/// <summary>Context validation failed.</summary>
	public const int ContextValidationFailed = 31011;

	/// <summary>Context validation warning (non-strict mode).</summary>
	public const int ContextValidationWarning = 31012;

	/// <summary>Context validation detail information.</summary>
	public const int ContextValidationDetail = 31013;

	/// <summary>Context validation error occurred.</summary>
	public const int ContextValidationError = 31014;

	// ========================================
	// 31100-31199: Transaction Middleware
	// ========================================

	/// <summary>Transaction middleware executing.</summary>
	public const int TransactionMiddlewareExecuting = 31100;

	/// <summary>Transaction started.</summary>
	public const int TransactionStarted = 31101;

	/// <summary>Transaction committed.</summary>
	public const int TransactionCommitted = 31102;

	/// <summary>Transaction rolled back.</summary>
	public const int TransactionRolledBack = 31103;

	/// <summary>Transaction isolation level set.</summary>
	public const int TransactionIsolationLevelSet = 31104;

	// ========================================
	// 31200-31299: Tenancy Middleware
	// ========================================

	/// <summary>Tenancy middleware executing.</summary>
	public const int TenancyMiddlewareExecuting = 31200;

	/// <summary>Tenant identified.</summary>
	public const int TenantIdentified = 31201;

	/// <summary>Tenant not found.</summary>
	public const int TenantNotFound = 31202;

	/// <summary>Tenant context set.</summary>
	public const int TenantContextSet = 31203;

	// ========================================
	// 31300-31399: ContractVersion Middleware
	// ========================================

	/// <summary>Contract version middleware executing.</summary>
	public const int ContractVersionMiddlewareExecuting = 31300;

	/// <summary>Contract version validated.</summary>
	public const int ContractVersionValidated = 31301;

	/// <summary>Contract version mismatch.</summary>
	public const int ContractVersionMismatch = 31302;

	/// <summary>Contract upgrade required.</summary>
	public const int ContractUpgradeRequired = 31303;

	// ========================================
	// 31400-31499: AuditLogging Middleware
	// ========================================

	/// <summary>Audit logging middleware executing.</summary>
	public const int AuditLoggingMiddlewareExecuting = 31400;

	/// <summary>Audit log entry created.</summary>
	public const int AuditLogEntryCreated = 31401;

	/// <summary>Audit log write failed.</summary>
	public const int AuditLogWriteFailed = 31402;

	/// <summary>Sensitive data masked in audit.</summary>
	public const int SensitiveDataMasked = 31403;

	// ========================================
	// 31500-31599: ExceptionMapping Middleware
	// ========================================

	/// <summary>Exception mapping middleware executing.</summary>
	public const int ExceptionMappingMiddlewareExecuting = 31500;

	/// <summary>Exception mapped to result.</summary>
	public const int ExceptionMapped = 31501;

	/// <summary>Unhandled exception caught.</summary>
	public const int UnhandledExceptionCaught = 31502;

	/// <summary>Exception details logged.</summary>
	public const int ExceptionDetailsLogged = 31503;

	// ========================================
	// 31600-31699: Routing Middleware
	// ========================================

	/// <summary>Routing middleware executing.</summary>
	public const int RoutingMiddlewareExecuting = 31600;

	/// <summary>Route matched.</summary>
	public const int RouteMatched = 31601;

	/// <summary>Route not matched.</summary>
	public const int RouteNotMatched = 31602;

	/// <summary>Routing decision made.</summary>
	public const int RoutingDecisionMade = 31603;

	/// <summary>Routing failed.</summary>
	public const int RoutingFailed = 31604;

	/// <summary>Message successfully routed to target.</summary>
	public const int MessageRouted = 31605;

	/// <summary>Routing failed: no matching route for message type.</summary>
	public const int RoutingNoMatchingRoute = 31606;

	/// <summary>Routing failed: message bus not registered.</summary>
	public const int RoutingBusNotRegistered = 31607;

	/// <summary>Zero-allocation router routed message count.</summary>
	public const int ZeroAllocRoutedCount = 31608;

	/// <summary>Route selected using consistent hash.</summary>
	public const int RouteSelectedConsistentHash = 31609;

	/// <summary>Route selected using least connections.</summary>
	public const int RouteSelectedLeastConnections = 31610;

	/// <summary>Route selected using random selection.</summary>
	public const int RouteSelectedRandom = 31611;

	/// <summary>Route health check failed.</summary>
	public const int RouteHealthCheckFailed = 31612;

	/// <summary>Route registered for health monitoring.</summary>
	public const int RouteRegistered = 31613;

	/// <summary>Route unregistered from health monitoring.</summary>
	public const int RouteUnregistered = 31614;

	/// <summary>Health monitor starting.</summary>
	public const int HealthMonitorStarting = 31615;

	/// <summary>Health monitor stopping.</summary>
	public const int HealthMonitorStopping = 31616;

	/// <summary>Health checks completed.</summary>
	public const int HealthChecksCompleted = 31617;

	/// <summary>Scheduled health check error.</summary>
	public const int ScheduledHealthCheckError = 31618;

	/// <summary>Unknown health check type encountered.</summary>
	public const int UnknownHealthCheckType = 31619;

	/// <summary>Route selected using weighted round-robin.</summary>
	public const int RouteSelectedWeightedRoundRobin = 31620;

	/// <summary>TCP health check timed out.</summary>
	public const int TcpHealthCheckTimeout = 31621;

	/// <summary>TCP health check failed.</summary>
	public const int TcpHealthCheckFailed = 31622;

	/// <summary>Invalid TCP endpoint format.</summary>
	public const int InvalidTcpEndpoint = 31623;

	/// <summary>Queue health check missing connection configuration.</summary>
	public const int MissingQueueConnection = 31624;

	/// <summary>Queue configuration validated successfully.</summary>
	public const int QueueConfigurationValid = 31625;

	/// <summary>Unknown queue type encountered.</summary>
	public const int UnknownQueueType = 31626;

	/// <summary>Content-based routes resolved by IMessageRouter.</summary>
	public const int ContentBasedRoutesResolved = 31627;

	/// <summary>Unified routing completed via IDispatchRouter.</summary>
	public const int UnifiedRoutingComplete = 31628;

	// ========================================
	// 31700-31799: MetricsLogging Middleware
	// ========================================

	/// <summary>Metrics logging middleware executing.</summary>
	public const int MetricsLoggingMiddlewareExecuting = 31700;

	/// <summary>Metrics recorded.</summary>
	public const int MetricsRecorded = 31701;

	/// <summary>Latency measured.</summary>
	public const int LatencyMeasured = 31702;

	/// <summary>Throughput measured.</summary>
	public const int ThroughputMeasured = 31703;

	/// <summary>Error rate recorded.</summary>
	public const int ErrorRateRecorded = 31704;

	// ========================================
	// 31800-31899: Logging Middleware
	// ========================================

	/// <summary>Logging middleware started processing.</summary>
	public const int LoggingStarted = 31800;

	/// <summary>Logging middleware started processing with payload.</summary>
	public const int LoggingStartedWithPayload = 31801;

	/// <summary>Logging middleware completed successfully.</summary>
	public const int LoggingCompleted = 31802;

	/// <summary>Logging middleware recorded failure.</summary>
	public const int LoggingFailed = 31803;

	/// <summary>Logging middleware caught exception.</summary>
	public const int LoggingException = 31804;
}
