// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="MiddlewareEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait("Priority", "0")]
public sealed class MiddlewareEventIdShould : UnitTestBase
{
	#region Retry/CircuitBreaker Event IDs (30000-30099)

	[Fact]
	public void HaveRetryMiddlewareExecutingInRetryRange()
	{
		MiddlewareEventId.RetryMiddlewareExecuting.ShouldBe(30000);
	}

	[Fact]
	public void HaveRetryAttemptStartedInRetryRange()
	{
		MiddlewareEventId.RetryAttemptStarted.ShouldBe(30001);
	}

	[Fact]
	public void HaveRetrySucceededInRetryRange()
	{
		MiddlewareEventId.RetrySucceeded.ShouldBe(30002);
	}

	[Fact]
	public void HaveRetryFailedInRetryRange()
	{
		MiddlewareEventId.RetryFailed.ShouldBe(30003);
	}

	[Fact]
	public void HaveRetryExhaustedInRetryRange()
	{
		MiddlewareEventId.RetryExhausted.ShouldBe(30004);
	}

	[Fact]
	public void HaveRetryDelayAppliedInRetryRange()
	{
		MiddlewareEventId.RetryDelayApplied.ShouldBe(30005);
	}

	[Fact]
	public void HaveRetryPolicyExecutedInRetryRange()
	{
		MiddlewareEventId.RetryPolicyExecuted.ShouldBe(30006);
	}

	[Fact]
	public void HaveCircuitBreakerMiddlewareExecutingInRetryRange()
	{
		MiddlewareEventId.CircuitBreakerMiddlewareExecuting.ShouldBe(30010);
	}

	[Fact]
	public void HaveCircuitBreakerBlockedInRetryRange()
	{
		MiddlewareEventId.CircuitBreakerBlocked.ShouldBe(30011);
	}

	[Fact]
	public void HaveCircuitBreakerAllowedInRetryRange()
	{
		MiddlewareEventId.CircuitBreakerAllowed.ShouldBe(30012);
	}

	[Fact]
	public void HaveRetryWaitingInRetryRange()
	{
		MiddlewareEventId.RetryWaiting.ShouldBe(30013);
	}

	[Fact]
	public void HaveNonRetryableExceptionInRetryRange()
	{
		MiddlewareEventId.NonRetryableException.ShouldBe(30014);
	}

	[Fact]
	public void HaveCircuitBreakerStateOpenInRetryRange()
	{
		MiddlewareEventId.CircuitBreakerStateOpen.ShouldBe(30015);
	}

	[Fact]
	public void HaveCircuitBreakerStateHalfOpenInRetryRange()
	{
		MiddlewareEventId.CircuitBreakerStateHalfOpen.ShouldBe(30016);
	}

	[Fact]
	public void HaveCircuitBreakerStateClosedInRetryRange()
	{
		MiddlewareEventId.CircuitBreakerStateClosed.ShouldBe(30017);
	}

	[Fact]
	public void HaveCircuitBreakerTransitionInRetryRange()
	{
		MiddlewareEventId.CircuitBreakerTransition.ShouldBe(30018);
	}

	[Fact]
	public void HaveCircuitBreakerTimeoutInRetryRange()
	{
		MiddlewareEventId.CircuitBreakerTimeout.ShouldBe(30019);
	}

	#endregion Retry/CircuitBreaker Event IDs (30000-30099)

	#region RateLimiting Event IDs (30100-30199)

	[Fact]
	public void HaveRateLimitingMiddlewareExecutingInRateLimitingRange()
	{
		MiddlewareEventId.RateLimitingMiddlewareExecuting.ShouldBe(30100);
	}

	[Fact]
	public void HaveRateLimitPermittedInRateLimitingRange()
	{
		MiddlewareEventId.RateLimitPermitted.ShouldBe(30101);
	}

	[Fact]
	public void HaveRateLimitRejectedInRateLimitingRange()
	{
		MiddlewareEventId.RateLimitRejected.ShouldBe(30102);
	}

	[Fact]
	public void HaveRateLimitThresholdReachedInRateLimitingRange()
	{
		MiddlewareEventId.RateLimitThresholdReached.ShouldBe(30103);
	}

	[Fact]
	public void HaveRateLimiterConfiguredInRateLimitingRange()
	{
		MiddlewareEventId.RateLimiterConfigured.ShouldBe(30104);
	}

	[Fact]
	public void HaveRateLimitSlidingWindowInRateLimitingRange()
	{
		MiddlewareEventId.RateLimitSlidingWindow.ShouldBe(30105);
	}

	[Fact]
	public void HaveRateLimitLeaseAcquiredInRateLimitingRange()
	{
		MiddlewareEventId.RateLimitLeaseAcquired.ShouldBe(30106);
	}

	#endregion RateLimiting Event IDs (30100-30199)

	#region Timeout Event IDs (30200-30299)

	[Fact]
	public void HaveTimeoutMiddlewareExecutingInTimeoutRange()
	{
		MiddlewareEventId.TimeoutMiddlewareExecuting.ShouldBe(30200);
	}

	[Fact]
	public void HaveTimeoutCompletedInTimeoutRange()
	{
		MiddlewareEventId.TimeoutCompleted.ShouldBe(30201);
	}

	[Fact]
	public void HaveTimeoutExceededInTimeoutRange()
	{
		MiddlewareEventId.TimeoutExceeded.ShouldBe(30202);
	}

	[Fact]
	public void HaveTimeoutCancellationRequestedInTimeoutRange()
	{
		MiddlewareEventId.TimeoutCancellationRequested.ShouldBe(30203);
	}

	[Fact]
	public void HaveTimeoutContextOverrideInTimeoutRange()
	{
		MiddlewareEventId.TimeoutContextOverride.ShouldBe(30204);
	}

	[Fact]
	public void HaveTimeoutMessageTypeSpecificInTimeoutRange()
	{
		MiddlewareEventId.TimeoutMessageTypeSpecific.ShouldBe(30205);
	}

	[Fact]
	public void HaveTimeoutMessageKindInTimeoutRange()
	{
		MiddlewareEventId.TimeoutMessageKind.ShouldBe(30206);
	}

	[Fact]
	public void HaveTimeoutProcessingErrorInTimeoutRange()
	{
		MiddlewareEventId.TimeoutProcessingError.ShouldBe(30207);
	}

	#endregion Timeout Event IDs (30200-30299)

	#region Batching Event IDs (30300-30399)

	[Fact]
	public void HaveBatchingMiddlewareExecutingInBatchingRange()
	{
		MiddlewareEventId.BatchingMiddlewareExecuting.ShouldBe(30300);
	}

	[Fact]
	public void HaveBatchCreatedInBatchingRange()
	{
		MiddlewareEventId.BatchCreated.ShouldBe(30301);
	}

	[Fact]
	public void HaveBatchFlushedInBatchingRange()
	{
		MiddlewareEventId.BatchFlushed.ShouldBe(30302);
	}

	[Fact]
	public void HaveMessageAddedToBatchInBatchingRange()
	{
		MiddlewareEventId.MessageAddedToBatch.ShouldBe(30303);
	}

	[Fact]
	public void HaveBatchSizeThresholdReachedInBatchingRange()
	{
		MiddlewareEventId.BatchSizeThresholdReached.ShouldBe(30304);
	}

	[Fact]
	public void HaveBatchTimeThresholdReachedInBatchingRange()
	{
		MiddlewareEventId.BatchTimeThresholdReached.ShouldBe(30305);
	}

	[Fact]
	public void HaveBulkOptimizationAppliedInBatchingRange()
	{
		MiddlewareEventId.BulkOptimizationApplied.ShouldBe(30306);
	}

	[Fact]
	public void HaveBatchProcessingErrorInBatchingRange()
	{
		MiddlewareEventId.BatchProcessingError.ShouldBe(30307);
	}

	[Fact]
	public void HaveBatchCompletedInBatchingRange()
	{
		MiddlewareEventId.BatchCompleted.ShouldBe(30308);
	}

	[Fact]
	public void HaveMessageNotBatchedInBatchingRange()
	{
		MiddlewareEventId.MessageNotBatched.ShouldBe(30309);
	}

	[Fact]
	public void HaveBatchProcessingStartedInBatchingRange()
	{
		MiddlewareEventId.BatchProcessingStarted.ShouldBe(30310);
	}

	#endregion Batching Event IDs (30300-30399)

	#region Authentication Event IDs (30400-30499)

	[Fact]
	public void HaveAuthenticationMiddlewareExecutingInAuthenticationRange()
	{
		MiddlewareEventId.AuthenticationMiddlewareExecuting.ShouldBe(30400);
	}

	[Fact]
	public void HaveAuthenticationSucceededInAuthenticationRange()
	{
		MiddlewareEventId.AuthenticationSucceeded.ShouldBe(30401);
	}

	[Fact]
	public void HaveAuthenticationFailedInAuthenticationRange()
	{
		MiddlewareEventId.AuthenticationFailed.ShouldBe(30402);
	}

	[Fact]
	public void HaveTokenValidatedInAuthenticationRange()
	{
		MiddlewareEventId.TokenValidated.ShouldBe(30403);
	}

	[Fact]
	public void HaveTokenExpiredInAuthenticationRange()
	{
		MiddlewareEventId.TokenExpired.ShouldBe(30404);
	}

	[Fact]
	public void HaveTokenInvalidInAuthenticationRange()
	{
		MiddlewareEventId.TokenInvalid.ShouldBe(30405);
	}

	[Fact]
	public void HaveAnonymousAccessAllowedInAuthenticationRange()
	{
		MiddlewareEventId.AnonymousAccessAllowed.ShouldBe(30406);
	}

	[Fact]
	public void HaveAuthenticationContextExtractedInAuthenticationRange()
	{
		MiddlewareEventId.AuthenticationContextExtracted.ShouldBe(30407);
	}

	[Fact]
	public void HaveNoAuthenticationSchemeMatchedInAuthenticationRange()
	{
		MiddlewareEventId.NoAuthenticationSchemeMatched.ShouldBe(30408);
	}

	[Fact]
	public void HaveAuthenticationProviderInvokedInAuthenticationRange()
	{
		MiddlewareEventId.AuthenticationProviderInvoked.ShouldBe(30409);
	}

	[Fact]
	public void HaveAuthenticationClaimsValidatedInAuthenticationRange()
	{
		MiddlewareEventId.AuthenticationClaimsValidated.ShouldBe(30410);
	}

	[Fact]
	public void HaveAuthenticationErrorDetailsInAuthenticationRange()
	{
		MiddlewareEventId.AuthenticationErrorDetails.ShouldBe(30411);
	}

	#endregion Authentication Event IDs (30400-30499)

	#region Authorization Event IDs (30500-30599)

	[Fact]
	public void HaveAuthorizationMiddlewareExecutingInAuthorizationRange()
	{
		MiddlewareEventId.AuthorizationMiddlewareExecuting.ShouldBe(30500);
	}

	[Fact]
	public void HaveAuthorizationGrantedInAuthorizationRange()
	{
		MiddlewareEventId.AuthorizationGranted.ShouldBe(30501);
	}

	[Fact]
	public void HaveAuthorizationDeniedInAuthorizationRange()
	{
		MiddlewareEventId.AuthorizationDenied.ShouldBe(30502);
	}

	[Fact]
	public void HavePolicyEvaluationStartedInAuthorizationRange()
	{
		MiddlewareEventId.PolicyEvaluationStarted.ShouldBe(30503);
	}

	[Fact]
	public void HavePolicyEvaluationCompletedInAuthorizationRange()
	{
		MiddlewareEventId.PolicyEvaluationCompleted.ShouldBe(30504);
	}

	#endregion Authorization Event IDs (30500-30599)

	#region InputSanitization Event IDs (30600-30699)

	[Fact]
	public void HaveInputSanitizationMiddlewareExecutingInInputSanitizationRange()
	{
		MiddlewareEventId.InputSanitizationMiddlewareExecuting.ShouldBe(30600);
	}

	[Fact]
	public void HaveInputSanitizedInInputSanitizationRange()
	{
		MiddlewareEventId.InputSanitized.ShouldBe(30601);
	}

	[Fact]
	public void HaveDangerousInputDetectedInInputSanitizationRange()
	{
		MiddlewareEventId.DangerousInputDetected.ShouldBe(30602);
	}

	[Fact]
	public void HaveInputValidationPassedInInputSanitizationRange()
	{
		MiddlewareEventId.InputValidationPassed.ShouldBe(30603);
	}

	#endregion InputSanitization Event IDs (30600-30699)

	#region Outbox Middleware Event IDs (30700-30799)

	[Fact]
	public void HaveOutboxMiddlewareExecutingInOutboxRange()
	{
		MiddlewareEventId.OutboxMiddlewareExecuting.ShouldBe(30700);
	}

	[Fact]
	public void HaveMessageStagedInOutboxInOutboxRange()
	{
		MiddlewareEventId.MessageStagedInOutbox.ShouldBe(30701);
	}

	[Fact]
	public void HaveOutboxStagingCompletedInOutboxRange()
	{
		MiddlewareEventId.OutboxStagingCompleted.ShouldBe(30702);
	}

	[Fact]
	public void HaveOutboxStagingFailedInOutboxRange()
	{
		MiddlewareEventId.OutboxStagingFailed.ShouldBe(30703);
	}

	#endregion Outbox Middleware Event IDs (30700-30799)

	#region Inbox Middleware Event IDs (30800-30899)

	[Fact]
	public void HaveInboxMiddlewareExecutingInInboxRange()
	{
		MiddlewareEventId.InboxMiddlewareExecuting.ShouldBe(30800);
	}

	[Fact]
	public void HaveMessageReceivedInInboxInInboxRange()
	{
		MiddlewareEventId.MessageReceivedInInbox.ShouldBe(30801);
	}

	[Fact]
	public void HaveInboxMessageProcessedInInboxRange()
	{
		MiddlewareEventId.InboxMessageProcessed.ShouldBe(30802);
	}

	[Fact]
	public void HaveInboxDuplicateDetectedInInboxRange()
	{
		MiddlewareEventId.InboxDuplicateDetected.ShouldBe(30803);
	}

	#endregion Inbox Middleware Event IDs (30800-30899)

	#region Idempotency Event IDs (30900-30999)

	[Fact]
	public void HaveIdempotencyMiddlewareExecutingInIdempotencyRange()
	{
		MiddlewareEventId.IdempotencyMiddlewareExecuting.ShouldBe(30900);
	}

	[Fact]
	public void HaveIdempotencyCheckPassedInIdempotencyRange()
	{
		MiddlewareEventId.IdempotencyCheckPassed.ShouldBe(30901);
	}

	[Fact]
	public void HaveDuplicateRequestDetectedInIdempotencyRange()
	{
		MiddlewareEventId.DuplicateRequestDetected.ShouldBe(30902);
	}

	[Fact]
	public void HaveIdempotencyKeyGeneratedInIdempotencyRange()
	{
		MiddlewareEventId.IdempotencyKeyGenerated.ShouldBe(30903);
	}

	[Fact]
	public void HaveCachedResponseReturnedInIdempotencyRange()
	{
		MiddlewareEventId.CachedResponseReturned.ShouldBe(30904);
	}

	#endregion Idempotency Event IDs (30900-30999)

	#region Validation Event IDs (31000-31099)

	[Fact]
	public void HaveValidationMiddlewareExecutingInValidationRange()
	{
		MiddlewareEventId.ValidationMiddlewareExecuting.ShouldBe(31000);
	}

	[Fact]
	public void HaveValidationPassedInValidationRange()
	{
		MiddlewareEventId.ValidationPassed.ShouldBe(31001);
	}

	[Fact]
	public void HaveValidationFailedInValidationRange()
	{
		MiddlewareEventId.ValidationFailed.ShouldBe(31002);
	}

	[Fact]
	public void HaveValidationErrorDetailsInValidationRange()
	{
		MiddlewareEventId.ValidationErrorDetails.ShouldBe(31003);
	}

	[Fact]
	public void HaveValidationFailedWithDetailsInValidationRange()
	{
		MiddlewareEventId.ValidationFailedWithDetails.ShouldBe(31004);
	}

	[Fact]
	public void HaveValidationTimedOutInValidationRange()
	{
		MiddlewareEventId.ValidationTimedOut.ShouldBe(31005);
	}

	[Fact]
	public void HaveValidationServiceErrorInValidationRange()
	{
		MiddlewareEventId.ValidationServiceError.ShouldBe(31006);
	}

	[Fact]
	public void HaveProfileValidationFailedInValidationRange()
	{
		MiddlewareEventId.ProfileValidationFailed.ShouldBe(31007);
	}

	[Fact]
	public void HaveProfileValidationAppliedInValidationRange()
	{
		MiddlewareEventId.ProfileValidationApplied.ShouldBe(31008);
	}

	[Fact]
	public void HaveTraceValidationIssuesInValidationRange()
	{
		MiddlewareEventId.TraceValidationIssues.ShouldBe(31009);
	}

	[Fact]
	public void HaveValidationRegexTimeoutInValidationRange()
	{
		MiddlewareEventId.ValidationRegexTimeout.ShouldBe(31010);
	}

	[Fact]
	public void HaveContextValidationFailedInValidationRange()
	{
		MiddlewareEventId.ContextValidationFailed.ShouldBe(31011);
	}

	[Fact]
	public void HaveContextValidationWarningInValidationRange()
	{
		MiddlewareEventId.ContextValidationWarning.ShouldBe(31012);
	}

	[Fact]
	public void HaveContextValidationDetailInValidationRange()
	{
		MiddlewareEventId.ContextValidationDetail.ShouldBe(31013);
	}

	[Fact]
	public void HaveContextValidationErrorInValidationRange()
	{
		MiddlewareEventId.ContextValidationError.ShouldBe(31014);
	}

	#endregion Validation Event IDs (31000-31099)

	#region Transaction Event IDs (31100-31199)

	[Fact]
	public void HaveTransactionMiddlewareExecutingInTransactionRange()
	{
		MiddlewareEventId.TransactionMiddlewareExecuting.ShouldBe(31100);
	}

	[Fact]
	public void HaveTransactionStartedInTransactionRange()
	{
		MiddlewareEventId.TransactionStarted.ShouldBe(31101);
	}

	[Fact]
	public void HaveTransactionCommittedInTransactionRange()
	{
		MiddlewareEventId.TransactionCommitted.ShouldBe(31102);
	}

	[Fact]
	public void HaveTransactionRolledBackInTransactionRange()
	{
		MiddlewareEventId.TransactionRolledBack.ShouldBe(31103);
	}

	[Fact]
	public void HaveTransactionIsolationLevelSetInTransactionRange()
	{
		MiddlewareEventId.TransactionIsolationLevelSet.ShouldBe(31104);
	}

	#endregion Transaction Event IDs (31100-31199)

	#region Tenancy Event IDs (31200-31299)

	[Fact]
	public void HaveTenancyMiddlewareExecutingInTenancyRange()
	{
		MiddlewareEventId.TenancyMiddlewareExecuting.ShouldBe(31200);
	}

	[Fact]
	public void HaveTenantIdentifiedInTenancyRange()
	{
		MiddlewareEventId.TenantIdentified.ShouldBe(31201);
	}

	[Fact]
	public void HaveTenantNotFoundInTenancyRange()
	{
		MiddlewareEventId.TenantNotFound.ShouldBe(31202);
	}

	[Fact]
	public void HaveTenantContextSetInTenancyRange()
	{
		MiddlewareEventId.TenantContextSet.ShouldBe(31203);
	}

	#endregion Tenancy Event IDs (31200-31299)

	#region ContractVersion Event IDs (31300-31399)

	[Fact]
	public void HaveContractVersionMiddlewareExecutingInContractVersionRange()
	{
		MiddlewareEventId.ContractVersionMiddlewareExecuting.ShouldBe(31300);
	}

	[Fact]
	public void HaveContractVersionValidatedInContractVersionRange()
	{
		MiddlewareEventId.ContractVersionValidated.ShouldBe(31301);
	}

	[Fact]
	public void HaveContractVersionMismatchInContractVersionRange()
	{
		MiddlewareEventId.ContractVersionMismatch.ShouldBe(31302);
	}

	[Fact]
	public void HaveContractUpgradeRequiredInContractVersionRange()
	{
		MiddlewareEventId.ContractUpgradeRequired.ShouldBe(31303);
	}

	#endregion ContractVersion Event IDs (31300-31399)

	#region AuditLogging Event IDs (31400-31499)

	[Fact]
	public void HaveAuditLoggingMiddlewareExecutingInAuditLoggingRange()
	{
		MiddlewareEventId.AuditLoggingMiddlewareExecuting.ShouldBe(31400);
	}

	[Fact]
	public void HaveAuditLogEntryCreatedInAuditLoggingRange()
	{
		MiddlewareEventId.AuditLogEntryCreated.ShouldBe(31401);
	}

	[Fact]
	public void HaveAuditLogWriteFailedInAuditLoggingRange()
	{
		MiddlewareEventId.AuditLogWriteFailed.ShouldBe(31402);
	}

	[Fact]
	public void HaveSensitiveDataMaskedInAuditLoggingRange()
	{
		MiddlewareEventId.SensitiveDataMasked.ShouldBe(31403);
	}

	#endregion AuditLogging Event IDs (31400-31499)

	#region ExceptionMapping Event IDs (31500-31599)

	[Fact]
	public void HaveExceptionMappingMiddlewareExecutingInExceptionMappingRange()
	{
		MiddlewareEventId.ExceptionMappingMiddlewareExecuting.ShouldBe(31500);
	}

	[Fact]
	public void HaveExceptionMappedInExceptionMappingRange()
	{
		MiddlewareEventId.ExceptionMapped.ShouldBe(31501);
	}

	[Fact]
	public void HaveUnhandledExceptionCaughtInExceptionMappingRange()
	{
		MiddlewareEventId.UnhandledExceptionCaught.ShouldBe(31502);
	}

	[Fact]
	public void HaveExceptionDetailsLoggedInExceptionMappingRange()
	{
		MiddlewareEventId.ExceptionDetailsLogged.ShouldBe(31503);
	}

	#endregion ExceptionMapping Event IDs (31500-31599)

	#region Routing Event IDs (31600-31699)

	[Fact]
	public void HaveRoutingMiddlewareExecutingInRoutingRange()
	{
		MiddlewareEventId.RoutingMiddlewareExecuting.ShouldBe(31600);
	}

	[Fact]
	public void HaveRouteMatchedInRoutingRange()
	{
		MiddlewareEventId.RouteMatched.ShouldBe(31601);
	}

	[Fact]
	public void HaveRouteNotMatchedInRoutingRange()
	{
		MiddlewareEventId.RouteNotMatched.ShouldBe(31602);
	}

	[Fact]
	public void HaveRoutingDecisionMadeInRoutingRange()
	{
		MiddlewareEventId.RoutingDecisionMade.ShouldBe(31603);
	}

	[Fact]
	public void HaveRoutingFailedInRoutingRange()
	{
		MiddlewareEventId.RoutingFailed.ShouldBe(31604);
	}

	[Fact]
	public void HaveMessageRoutedInRoutingRange()
	{
		MiddlewareEventId.MessageRouted.ShouldBe(31605);
	}

	[Fact]
	public void HaveRoutingNoMatchingRouteInRoutingRange()
	{
		MiddlewareEventId.RoutingNoMatchingRoute.ShouldBe(31606);
	}

	[Fact]
	public void HaveRoutingBusNotRegisteredInRoutingRange()
	{
		MiddlewareEventId.RoutingBusNotRegistered.ShouldBe(31607);
	}

	[Fact]
	public void HaveZeroAllocRoutedCountInRoutingRange()
	{
		MiddlewareEventId.ZeroAllocRoutedCount.ShouldBe(31608);
	}

	[Fact]
	public void HaveRouteSelectedConsistentHashInRoutingRange()
	{
		MiddlewareEventId.RouteSelectedConsistentHash.ShouldBe(31609);
	}

	[Fact]
	public void HaveRouteSelectedLeastConnectionsInRoutingRange()
	{
		MiddlewareEventId.RouteSelectedLeastConnections.ShouldBe(31610);
	}

	[Fact]
	public void HaveRouteSelectedRandomInRoutingRange()
	{
		MiddlewareEventId.RouteSelectedRandom.ShouldBe(31611);
	}

	[Fact]
	public void HaveRouteHealthCheckFailedInRoutingRange()
	{
		MiddlewareEventId.RouteHealthCheckFailed.ShouldBe(31612);
	}

	[Fact]
	public void HaveRouteRegisteredInRoutingRange()
	{
		MiddlewareEventId.RouteRegistered.ShouldBe(31613);
	}

	[Fact]
	public void HaveRouteUnregisteredInRoutingRange()
	{
		MiddlewareEventId.RouteUnregistered.ShouldBe(31614);
	}

	[Fact]
	public void HaveHealthMonitorStartingInRoutingRange()
	{
		MiddlewareEventId.HealthMonitorStarting.ShouldBe(31615);
	}

	[Fact]
	public void HaveHealthMonitorStoppingInRoutingRange()
	{
		MiddlewareEventId.HealthMonitorStopping.ShouldBe(31616);
	}

	[Fact]
	public void HaveHealthChecksCompletedInRoutingRange()
	{
		MiddlewareEventId.HealthChecksCompleted.ShouldBe(31617);
	}

	[Fact]
	public void HaveScheduledHealthCheckErrorInRoutingRange()
	{
		MiddlewareEventId.ScheduledHealthCheckError.ShouldBe(31618);
	}

	[Fact]
	public void HaveUnknownHealthCheckTypeInRoutingRange()
	{
		MiddlewareEventId.UnknownHealthCheckType.ShouldBe(31619);
	}

	[Fact]
	public void HaveRouteSelectedWeightedRoundRobinInRoutingRange()
	{
		MiddlewareEventId.RouteSelectedWeightedRoundRobin.ShouldBe(31620);
	}

	[Fact]
	public void HaveTcpHealthCheckTimeoutInRoutingRange()
	{
		MiddlewareEventId.TcpHealthCheckTimeout.ShouldBe(31621);
	}

	[Fact]
	public void HaveTcpHealthCheckFailedInRoutingRange()
	{
		MiddlewareEventId.TcpHealthCheckFailed.ShouldBe(31622);
	}

	[Fact]
	public void HaveInvalidTcpEndpointInRoutingRange()
	{
		MiddlewareEventId.InvalidTcpEndpoint.ShouldBe(31623);
	}

	[Fact]
	public void HaveMissingQueueConnectionInRoutingRange()
	{
		MiddlewareEventId.MissingQueueConnection.ShouldBe(31624);
	}

	[Fact]
	public void HaveQueueConfigurationValidInRoutingRange()
	{
		MiddlewareEventId.QueueConfigurationValid.ShouldBe(31625);
	}

	[Fact]
	public void HaveUnknownQueueTypeInRoutingRange()
	{
		MiddlewareEventId.UnknownQueueType.ShouldBe(31626);
	}

	#endregion Routing Event IDs (31600-31699)

	#region MetricsLogging Event IDs (31700-31799)

	[Fact]
	public void HaveMetricsLoggingMiddlewareExecutingInMetricsLoggingRange()
	{
		MiddlewareEventId.MetricsLoggingMiddlewareExecuting.ShouldBe(31700);
	}

	[Fact]
	public void HaveMetricsRecordedInMetricsLoggingRange()
	{
		MiddlewareEventId.MetricsRecorded.ShouldBe(31701);
	}

	[Fact]
	public void HaveLatencyMeasuredInMetricsLoggingRange()
	{
		MiddlewareEventId.LatencyMeasured.ShouldBe(31702);
	}

	[Fact]
	public void HaveThroughputMeasuredInMetricsLoggingRange()
	{
		MiddlewareEventId.ThroughputMeasured.ShouldBe(31703);
	}

	[Fact]
	public void HaveErrorRateRecordedInMetricsLoggingRange()
	{
		MiddlewareEventId.ErrorRateRecorded.ShouldBe(31704);
	}

	#endregion MetricsLogging Event IDs (31700-31799)

	#region Event ID Range Validation

	[Fact]
	public void HaveAllRetryEventIdsInExpectedRange()
	{
		MiddlewareEventId.RetryMiddlewareExecuting.ShouldBeInRange(30000, 30099);
		MiddlewareEventId.CircuitBreakerTimeout.ShouldBeInRange(30000, 30099);
	}

	[Fact]
	public void HaveAllRateLimitingEventIdsInExpectedRange()
	{
		MiddlewareEventId.RateLimitingMiddlewareExecuting.ShouldBeInRange(30100, 30199);
		MiddlewareEventId.RateLimitLeaseAcquired.ShouldBeInRange(30100, 30199);
	}

	[Fact]
	public void HaveAllTimeoutEventIdsInExpectedRange()
	{
		MiddlewareEventId.TimeoutMiddlewareExecuting.ShouldBeInRange(30200, 30299);
		MiddlewareEventId.TimeoutProcessingError.ShouldBeInRange(30200, 30299);
	}

	[Fact]
	public void HaveAllValidationEventIdsInExpectedRange()
	{
		MiddlewareEventId.ValidationMiddlewareExecuting.ShouldBeInRange(31000, 31099);
		MiddlewareEventId.ContextValidationError.ShouldBeInRange(31000, 31099);
	}

	[Fact]
	public void HaveAllRoutingEventIdsInExpectedRange()
	{
		MiddlewareEventId.RoutingMiddlewareExecuting.ShouldBeInRange(31600, 31699);
		MiddlewareEventId.UnknownQueueType.ShouldBeInRange(31600, 31699);
	}

	#endregion Event ID Range Validation

	#region Event ID Uniqueness

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllMiddlewareEventIds();

		allEventIds.Distinct().Count().ShouldBe(allEventIds.Length,
			"All MiddlewareEventId constants should have unique values");
	}

	[Fact]
	public void HaveCorrectTotalNumberOfEventIds()
	{
		var allEventIds = GetAllMiddlewareEventIds();
		allEventIds.Length.ShouldBeGreaterThan(100);
	}

	#endregion Event ID Uniqueness

	#region Helper Methods

	private static int[] GetAllMiddlewareEventIds()
	{
		return
		[
			// Retry/CircuitBreaker
			MiddlewareEventId.RetryMiddlewareExecuting,
			MiddlewareEventId.RetryAttemptStarted,
			MiddlewareEventId.RetrySucceeded,
			MiddlewareEventId.RetryFailed,
			MiddlewareEventId.RetryExhausted,
			MiddlewareEventId.RetryDelayApplied,
			MiddlewareEventId.RetryPolicyExecuted,
			MiddlewareEventId.CircuitBreakerMiddlewareExecuting,
			MiddlewareEventId.CircuitBreakerBlocked,
			MiddlewareEventId.CircuitBreakerAllowed,
			MiddlewareEventId.RetryWaiting,
			MiddlewareEventId.NonRetryableException,
			MiddlewareEventId.CircuitBreakerStateOpen,
			MiddlewareEventId.CircuitBreakerStateHalfOpen,
			MiddlewareEventId.CircuitBreakerStateClosed,
			MiddlewareEventId.CircuitBreakerTransition,
			MiddlewareEventId.CircuitBreakerTimeout,

			// RateLimiting
			MiddlewareEventId.RateLimitingMiddlewareExecuting,
			MiddlewareEventId.RateLimitPermitted,
			MiddlewareEventId.RateLimitRejected,
			MiddlewareEventId.RateLimitThresholdReached,
			MiddlewareEventId.RateLimiterConfigured,
			MiddlewareEventId.RateLimitSlidingWindow,
			MiddlewareEventId.RateLimitLeaseAcquired,

			// Timeout
			MiddlewareEventId.TimeoutMiddlewareExecuting,
			MiddlewareEventId.TimeoutCompleted,
			MiddlewareEventId.TimeoutExceeded,
			MiddlewareEventId.TimeoutCancellationRequested,
			MiddlewareEventId.TimeoutContextOverride,
			MiddlewareEventId.TimeoutMessageTypeSpecific,
			MiddlewareEventId.TimeoutMessageKind,
			MiddlewareEventId.TimeoutProcessingError,

			// Batching
			MiddlewareEventId.BatchingMiddlewareExecuting,
			MiddlewareEventId.BatchCreated,
			MiddlewareEventId.BatchFlushed,
			MiddlewareEventId.MessageAddedToBatch,
			MiddlewareEventId.BatchSizeThresholdReached,
			MiddlewareEventId.BatchTimeThresholdReached,
			MiddlewareEventId.BulkOptimizationApplied,
			MiddlewareEventId.BatchProcessingError,
			MiddlewareEventId.BatchCompleted,
			MiddlewareEventId.MessageNotBatched,
			MiddlewareEventId.BatchProcessingStarted,

			// Authentication
			MiddlewareEventId.AuthenticationMiddlewareExecuting,
			MiddlewareEventId.AuthenticationSucceeded,
			MiddlewareEventId.AuthenticationFailed,
			MiddlewareEventId.TokenValidated,
			MiddlewareEventId.TokenExpired,
			MiddlewareEventId.TokenInvalid,
			MiddlewareEventId.AnonymousAccessAllowed,
			MiddlewareEventId.AuthenticationContextExtracted,
			MiddlewareEventId.NoAuthenticationSchemeMatched,
			MiddlewareEventId.AuthenticationProviderInvoked,
			MiddlewareEventId.AuthenticationClaimsValidated,
			MiddlewareEventId.AuthenticationErrorDetails,

			// Authorization
			MiddlewareEventId.AuthorizationMiddlewareExecuting,
			MiddlewareEventId.AuthorizationGranted,
			MiddlewareEventId.AuthorizationDenied,
			MiddlewareEventId.PolicyEvaluationStarted,
			MiddlewareEventId.PolicyEvaluationCompleted,

			// InputSanitization
			MiddlewareEventId.InputSanitizationMiddlewareExecuting,
			MiddlewareEventId.InputSanitized,
			MiddlewareEventId.DangerousInputDetected,
			MiddlewareEventId.InputValidationPassed,

			// Outbox
			MiddlewareEventId.OutboxMiddlewareExecuting,
			MiddlewareEventId.MessageStagedInOutbox,
			MiddlewareEventId.OutboxStagingCompleted,
			MiddlewareEventId.OutboxStagingFailed,

			// Inbox
			MiddlewareEventId.InboxMiddlewareExecuting,
			MiddlewareEventId.MessageReceivedInInbox,
			MiddlewareEventId.InboxMessageProcessed,
			MiddlewareEventId.InboxDuplicateDetected,

			// Idempotency
			MiddlewareEventId.IdempotencyMiddlewareExecuting,
			MiddlewareEventId.IdempotencyCheckPassed,
			MiddlewareEventId.DuplicateRequestDetected,
			MiddlewareEventId.IdempotencyKeyGenerated,
			MiddlewareEventId.CachedResponseReturned,

			// Validation
			MiddlewareEventId.ValidationMiddlewareExecuting,
			MiddlewareEventId.ValidationPassed,
			MiddlewareEventId.ValidationFailed,
			MiddlewareEventId.ValidationErrorDetails,
			MiddlewareEventId.ValidationFailedWithDetails,
			MiddlewareEventId.ValidationTimedOut,
			MiddlewareEventId.ValidationServiceError,
			MiddlewareEventId.ProfileValidationFailed,
			MiddlewareEventId.ProfileValidationApplied,
			MiddlewareEventId.TraceValidationIssues,
			MiddlewareEventId.ValidationRegexTimeout,
			MiddlewareEventId.ContextValidationFailed,
			MiddlewareEventId.ContextValidationWarning,
			MiddlewareEventId.ContextValidationDetail,
			MiddlewareEventId.ContextValidationError,

			// Transaction
			MiddlewareEventId.TransactionMiddlewareExecuting,
			MiddlewareEventId.TransactionStarted,
			MiddlewareEventId.TransactionCommitted,
			MiddlewareEventId.TransactionRolledBack,
			MiddlewareEventId.TransactionIsolationLevelSet,

			// Tenancy
			MiddlewareEventId.TenancyMiddlewareExecuting,
			MiddlewareEventId.TenantIdentified,
			MiddlewareEventId.TenantNotFound,
			MiddlewareEventId.TenantContextSet,

			// ContractVersion
			MiddlewareEventId.ContractVersionMiddlewareExecuting,
			MiddlewareEventId.ContractVersionValidated,
			MiddlewareEventId.ContractVersionMismatch,
			MiddlewareEventId.ContractUpgradeRequired,

			// AuditLogging
			MiddlewareEventId.AuditLoggingMiddlewareExecuting,
			MiddlewareEventId.AuditLogEntryCreated,
			MiddlewareEventId.AuditLogWriteFailed,
			MiddlewareEventId.SensitiveDataMasked,

			// ExceptionMapping
			MiddlewareEventId.ExceptionMappingMiddlewareExecuting,
			MiddlewareEventId.ExceptionMapped,
			MiddlewareEventId.UnhandledExceptionCaught,
			MiddlewareEventId.ExceptionDetailsLogged,

			// Routing
			MiddlewareEventId.RoutingMiddlewareExecuting,
			MiddlewareEventId.RouteMatched,
			MiddlewareEventId.RouteNotMatched,
			MiddlewareEventId.RoutingDecisionMade,
			MiddlewareEventId.RoutingFailed,
			MiddlewareEventId.MessageRouted,
			MiddlewareEventId.RoutingNoMatchingRoute,
			MiddlewareEventId.RoutingBusNotRegistered,
			MiddlewareEventId.ZeroAllocRoutedCount,
			MiddlewareEventId.RouteSelectedConsistentHash,
			MiddlewareEventId.RouteSelectedLeastConnections,
			MiddlewareEventId.RouteSelectedRandom,
			MiddlewareEventId.RouteHealthCheckFailed,
			MiddlewareEventId.RouteRegistered,
			MiddlewareEventId.RouteUnregistered,
			MiddlewareEventId.HealthMonitorStarting,
			MiddlewareEventId.HealthMonitorStopping,
			MiddlewareEventId.HealthChecksCompleted,
			MiddlewareEventId.ScheduledHealthCheckError,
			MiddlewareEventId.UnknownHealthCheckType,
			MiddlewareEventId.RouteSelectedWeightedRoundRobin,
			MiddlewareEventId.TcpHealthCheckTimeout,
			MiddlewareEventId.TcpHealthCheckFailed,
			MiddlewareEventId.InvalidTcpEndpoint,
			MiddlewareEventId.MissingQueueConnection,
			MiddlewareEventId.QueueConfigurationValid,
			MiddlewareEventId.UnknownQueueType,

			// MetricsLogging
			MiddlewareEventId.MetricsLoggingMiddlewareExecuting,
			MiddlewareEventId.MetricsRecorded,
			MiddlewareEventId.LatencyMeasured,
			MiddlewareEventId.ThroughputMeasured,
			MiddlewareEventId.ErrorRateRecorded
		];
	}

	#endregion Helper Methods
}
