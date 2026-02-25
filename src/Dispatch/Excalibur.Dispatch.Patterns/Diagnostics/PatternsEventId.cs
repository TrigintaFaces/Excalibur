// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.Diagnostics;

/// <summary>
/// Event IDs for messaging patterns (90000-91999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>90000-90099: ClaimCheck Core</item>
/// <item>90100-90199: ClaimCheck InMemory</item>
/// <item>90200-90299: Routing Patterns</item>
/// <item>90300-90399: Load Balancing</item>
/// <item>90400-90499: Route Health</item>
/// <item>90500-90599: Compliance Encryption</item>
/// <item>90600-90699: Validation Services</item>
/// <item>90700-90799: Context Validation</item>
/// <item>90800-90899: Poison Message Core</item>
/// <item>90900-90999: Poison Message Storage</item>
/// <item>91000-91099: Pool Configuration</item>
/// <item>91100-91199: Performance</item>
/// </list>
/// </remarks>
public static class PatternsEventId
{
	// ========================================
	// 90000-90099: ClaimCheck Core
	// ========================================

	/// <summary>ClaimCheck cleanup service created.</summary>
	public const int ClaimCheckCleanupServiceCreated = 90000;

	/// <summary>ClaimCheck cleanup started.</summary>
	public const int ClaimCheckCleanupStarted = 90001;

	/// <summary>ClaimCheck cleanup completed.</summary>
	public const int ClaimCheckCleanupCompleted = 90002;

	/// <summary>Claim stored.</summary>
	public const int ClaimStored = 90003;

	/// <summary>Claim retrieved.</summary>
	public const int ClaimRetrieved = 90004;

	/// <summary>Claim deleted.</summary>
	public const int ClaimDeleted = 90005;

	/// <summary>Claim expired.</summary>
	public const int ClaimExpired = 90006;

	/// <summary>Claim check cleanup is disabled.</summary>
	public const int ClaimCheckCleanupDisabled = 90007;

	/// <summary>Claim check cleanup service started with interval.</summary>
	public const int ClaimCheckCleanupStartedInterval = 90008;

	/// <summary>Claim check cleanup task running.</summary>
	public const int ClaimCheckCleanupTaskRunning = 90009;

	/// <summary>Claim check cleanup error occurred.</summary>
	public const int ClaimCheckCleanupError = 90010;

	/// <summary>Claim check cleanup service stopped.</summary>
	public const int ClaimCheckCleanupServiceStopped = 90011;

	/// <summary>Claim check cleanup completed with expired entries removed.</summary>
	public const int ClaimCheckCleanupExpiredRemoved = 90012;

	/// <summary>Claim check cleanup completed with no expired entries found.</summary>
	public const int ClaimCheckCleanupNoExpiredEntries = 90013;

	/// <summary>Claim check cleanup provider not available (provider does not implement IClaimCheckCleanupProvider).</summary>
	public const int ClaimCheckCleanupProviderNotAvailable = 90014;

	// ========================================
	// 90100-90199: ClaimCheck InMemory
	// ========================================

	/// <summary>In-memory ClaimCheck cleanup service created.</summary>
	public const int InMemoryClaimCheckCleanupCreated = 90100;

	/// <summary>In-memory claim stored.</summary>
	public const int InMemoryClaimStored = 90101;

	/// <summary>In-memory claim retrieved.</summary>
	public const int InMemoryClaimRetrieved = 90102;

	/// <summary>In-memory cleanup completed.</summary>
	public const int InMemoryCleanupCompleted = 90103;

	/// <summary>In-memory cleanup is disabled.</summary>
	public const int InMemoryCleanupDisabled = 90104;

	/// <summary>In-memory cleanup service started with interval.</summary>
	public const int InMemoryCleanupStartedInterval = 90105;

	/// <summary>In-memory cleanup service stopped.</summary>
	public const int InMemoryCleanupServiceStopped = 90106;

	/// <summary>In-memory cleanup task running.</summary>
	public const int InMemoryCleanupTaskRunning = 90107;

	/// <summary>In-memory cleanup error occurred.</summary>
	public const int InMemoryCleanupError = 90108;

	/// <summary>In-memory expired claims removed.</summary>
	public const int InMemoryExpiredClaimsRemoved = 90109;

	// ========================================
	// 90200-90299: Routing Patterns
	// ========================================

	/// <summary>Routing policy evaluator created.</summary>
	public const int RoutingPolicyEvaluatorCreated = 90200;

	/// <summary>Routing policy evaluated.</summary>
	public const int RoutingPolicyEvaluated = 90201;

	/// <summary>Route selected.</summary>
	public const int RouteSelected = 90202;

	/// <summary>TimeZone-aware router created.</summary>
	public const int TimeZoneAwareRouterCreated = 90203;

	/// <summary>TimeZone-aware routing applied.</summary>
	public const int TimeZoneAwareRoutingApplied = 90204;

	/// <summary>No route matched.</summary>
	public const int NoRouteMatched = 90205;

	/// <summary>Routing rules evaluated.</summary>
	public const int RoutingRulesEvaluated = 90206;

	/// <summary>Rule not applicable - outside active time ranges.</summary>
	public const int RuleNotApplicableTimeRange = 90207;

	/// <summary>Rule not applicable - not an active day of week.</summary>
	public const int RuleNotApplicableDayOfWeek = 90208;

	/// <summary>Rule not applicable - outside active date ranges.</summary>
	public const int RuleNotApplicableDateRange = 90209;

	/// <summary>Rule not applicable - special conditions not met.</summary>
	public const int RuleNotApplicableConditions = 90210;

	/// <summary>Rule is applicable.</summary>
	public const int RuleIsApplicable = 90211;

	/// <summary>Timezone not found - using local time.</summary>
	public const int TimezoneNotFoundLocalTime = 90212;

	/// <summary>Timezone not found - using UTC.</summary>
	public const int TimezoneNotFoundUtc = 90213;

	/// <summary>Route is in business hours.</summary>
	public const int RouteInBusinessHours = 90214;

	/// <summary>Route is outside business hours.</summary>
	public const int RouteOutsideBusinessHours = 90215;

	/// <summary>No routes in business hours - fallback.</summary>
	public const int NoRoutesInBusinessHoursFallback = 90216;

	// ========================================
	// 90300-90399: Load Balancing
	// ========================================

	/// <summary>Weighted round robin load balancer created.</summary>
	public const int WeightedRoundRobinCreated = 90300;

	/// <summary>Least connections load balancer created.</summary>
	public const int LeastConnectionsCreated = 90301;

	/// <summary>Consistent hash load balancer created.</summary>
	public const int ConsistentHashCreated = 90302;

	/// <summary>Random load balancer created.</summary>
	public const int RandomLoadBalancerCreated = 90303;

	/// <summary>Load balancer endpoint selected.</summary>
	public const int EndpointSelected = 90304;

	/// <summary>Load balancer endpoint unavailable.</summary>
	public const int EndpointUnavailable = 90305;

	/// <summary>Load balancer rebalancing.</summary>
	public const int LoadBalancerRebalancing = 90306;

	// ========================================
	// 90400-90499: Route Health
	// ========================================

	/// <summary>Route health monitor created.</summary>
	public const int RouteHealthMonitorCreated = 90400;

	/// <summary>Route health check performed.</summary>
	public const int RouteHealthCheckPerformed = 90401;

	/// <summary>Route marked healthy.</summary>
	public const int RouteMarkedHealthy = 90402;

	/// <summary>Route marked unhealthy.</summary>
	public const int RouteMarkedUnhealthy = 90403;

	/// <summary>Route health status changed.</summary>
	public const int RouteHealthStatusChanged = 90404;

	// ========================================
	// 90500-90599: Compliance Encryption
	// ========================================

	/// <summary>Encryption/decryption service created.</summary>
	public const int EncryptionDecryptionServiceCreated = 90500;

	/// <summary>Re-encryption service created.</summary>
	public const int ReEncryptionServiceCreated = 90501;

	/// <summary>Compliance encryption applied.</summary>
	public const int ComplianceEncryptionApplied = 90502;

	/// <summary>Compliance decryption applied.</summary>
	public const int ComplianceDecryptionApplied = 90503;

	/// <summary>Re-encryption completed.</summary>
	public const int ReEncryptionCompleted = 90504;

	// ========================================
	// 90600-90699: Validation Services
	// ========================================

	/// <summary>Default validation service created.</summary>
	public const int DefaultValidationServiceCreated = 90600;

	/// <summary>Profile-specific validation middleware executing.</summary>
	public const int ProfileSpecificValidationExecuting = 90601;

	/// <summary>Validation rule applied.</summary>
	public const int ValidationRuleApplied = 90602;

	/// <summary>Validation passed.</summary>
	public const int ValidationPassed = 90603;

	/// <summary>Validation failed.</summary>
	public const int ValidationFailed = 90604;

	// ========================================
	// 90700-90799: Context Validation
	// ========================================

	/// <summary>Context validation middleware executing.</summary>
	public const int ContextValidationExecuting = 90700;

	/// <summary>Default context validator created.</summary>
	public const int DefaultContextValidatorCreated = 90701;

	/// <summary>Trace context validator created.</summary>
	public const int TraceContextValidatorCreated = 90702;

	/// <summary>Context validation passed.</summary>
	public const int ContextValidationPassed = 90703;

	/// <summary>Context validation failed.</summary>
	public const int ContextValidationFailed = 90704;

	// ========================================
	// 90800-90899: Poison Message Core
	// ========================================

	/// <summary>Poison message middleware executing.</summary>
	public const int PoisonMessageMiddlewareExecuting = 90800;

	/// <summary>Poison message detected.</summary>
	public const int PoisonMessageDetected = 90801;

	/// <summary>Poison message handler executing.</summary>
	public const int PoisonMessageHandlerExecuting = 90802;

	/// <summary>Poison message handled.</summary>
	public const int PoisonMessageHandled = 90803;

	/// <summary>Message quarantined.</summary>
	public const int MessageQuarantined = 90804;

	// ========================================
	// 90900-90999: Poison Message Storage
	// ========================================

	/// <summary>In-memory dead letter store created.</summary>
	public const int InMemoryDeadLetterStoreCreated = 90900;

	/// <summary>Poison message cleanup service created.</summary>
	public const int PoisonMessageCleanupServiceCreated = 90901;

	/// <summary>Composite poison detector created.</summary>
	public const int CompositePoisonDetectorCreated = 90902;

	/// <summary>Dead letter message stored.</summary>
	public const int DeadLetterMessageStored = 90903;

	/// <summary>Dead letter message retrieved.</summary>
	public const int DeadLetterMessageRetrieved = 90904;

	/// <summary>Dead letter cleanup completed.</summary>
	public const int DeadLetterCleanupCompleted = 90905;

	// ========================================
	// 91000-91099: Pool Configuration
	// ========================================

	/// <summary>Pool configuration applied.</summary>
	public const int PoolConfigurationApplied = 91000;

	/// <summary>Pool service collection extensions executed.</summary>
	public const int PoolServiceCollectionExtended = 91001;

	/// <summary>Pool size configured.</summary>
	public const int PoolSizeConfigured = 91002;

	/// <summary>Pool timeout configured.</summary>
	public const int PoolTimeoutConfigured = 91003;

	// ========================================
	// 91100-91199: Performance
	// ========================================

	/// <summary>Performance benchmark started.</summary>
	public const int PerformanceBenchmarkStarted = 91100;

	/// <summary>Performance benchmark completed.</summary>
	public const int PerformanceBenchmarkCompleted = 91101;

	/// <summary>Performance metrics collected.</summary>
	public const int PerformanceMetricsCollected = 91102;

	/// <summary>Performance threshold exceeded.</summary>
	public const int PerformanceThresholdExceeded = 91103;

	// ========================================
	// 91200-91299: Azure Blob ClaimCheck Provider (Sprint 373)
	// ========================================

	/// <summary>Azure Blob ClaimCheck payload stored.</summary>
	public const int AzureBlobPayloadStored = 91200;

	/// <summary>Azure Blob ClaimCheck payload retrieved.</summary>
	public const int AzureBlobPayloadRetrieved = 91201;

	/// <summary>Azure Blob ClaimCheck not found.</summary>
	public const int AzureBlobClaimCheckNotFound = 91202;

	/// <summary>Azure Blob ClaimCheck deleted.</summary>
	public const int AzureBlobClaimCheckDeleted = 91203;

	/// <summary>Azure Blob ClaimCheck delete error.</summary>
	public const int AzureBlobClaimCheckDeleteError = 91204;
}
