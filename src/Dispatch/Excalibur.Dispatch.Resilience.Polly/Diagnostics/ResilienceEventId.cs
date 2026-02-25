// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Event IDs for resilience patterns using Polly (60000-60999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>60000-60099: Circuit Breaker Core</item>
/// <item>60100-60199: Circuit Breaker Policy</item>
/// <item>60200-60299: Circuit Breaker Registry</item>
/// <item>60300-60399: Retry Policy</item>
/// <item>60400-60499: Bulkhead</item>
/// <item>60500-60599: Timeout</item>
/// <item>60600-60699: Graceful Degradation</item>
/// <item>60700-60799: Default Retry</item>
/// <item>60800-60899: Hedging</item>
/// <item>60900-60999: Resilience Telemetry</item>
/// </list>
/// </remarks>
public static class ResilienceEventId
{
	// ========================================
	// 60000-60099: Circuit Breaker Core
	// ========================================

	/// <summary>Polly circuit breaker factory created.</summary>
	public const int CircuitBreakerFactoryCreated = 60000;

	/// <summary>Polly circuit breaker adapter created.</summary>
	public const int CircuitBreakerAdapterCreated = 60001;

	/// <summary>Circuit breaker state changed.</summary>
	public const int CircuitBreakerStateChanged = 60002;

	/// <summary>Circuit breaker opened.</summary>
	public const int CircuitBreakerOpened = 60003;

	/// <summary>Circuit breaker closed.</summary>
	public const int CircuitBreakerClosed = 60004;

	/// <summary>Circuit breaker half-open.</summary>
	public const int CircuitBreakerHalfOpen = 60005;

	/// <summary>Circuit breaker reset.</summary>
	public const int CircuitBreakerReset = 60006;

	/// <summary>Circuit breaker rejected request due to open state.</summary>
	public const int CircuitBreakerRejected = 60007;

	/// <summary>Circuit breaker fallback executed.</summary>
	public const int CircuitBreakerFallbackExecuted = 60008;

	/// <summary>Circuit breaker operation failed.</summary>
	public const int CircuitBreakerOperationFailed = 60009;

	/// <summary>Circuit breaker threshold exceeded.</summary>
	public const int CircuitBreakerThresholdExceeded = 60010;

	/// <summary>Circuit breaker coordination error.</summary>
	public const int CircuitBreakerCoordinationError = 60011;

	/// <summary>Circuit breaker reset requested.</summary>
	public const int CircuitBreakerResetRequested = 60012;

	/// <summary>Circuit breaker initializing.</summary>
	public const int CircuitBreakerInitializing = 60013;

	/// <summary>Circuit breaker starting.</summary>
	public const int CircuitBreakerStarting = 60014;

	/// <summary>Circuit breaker stopping.</summary>
	public const int CircuitBreakerStopping = 60015;

	/// <summary>Circuit breaker observer subscribed.</summary>
	public const int CircuitBreakerObserverSubscribed = 60016;

	/// <summary>Circuit breaker observer unsubscribed.</summary>
	public const int CircuitBreakerObserverUnsubscribed = 60017;

	/// <summary>Circuit breaker stop error during disposal.</summary>
	public const int CircuitBreakerStopError = 60018;

	// ========================================
	// 60100-60199: Circuit Breaker Policy
	// ========================================

	/// <summary>Polly circuit breaker policy created.</summary>
	public const int CircuitBreakerPolicyCreated = 60100;

	/// <summary>Polly circuit breaker policy adapter created.</summary>
	public const int CircuitBreakerPolicyAdapterCreated = 60101;

	/// <summary>Distributed circuit breaker state synced.</summary>
	public const int DistributedCircuitBreakerSynced = 60102;

	/// <summary>Distributed circuit breaker state conflict.</summary>
	public const int DistributedCircuitBreakerConflict = 60103;

	/// <summary>Polly circuit breaker created.</summary>
	public const int PollyCircuitBreakerCreated = 60104;

	/// <summary>Polly circuit breaker removed.</summary>
	public const int PollyCircuitBreakerRemoved = 60105;

	// ========================================
	// 60200-60299: Circuit Breaker Registry
	// ========================================

	/// <summary>Transport circuit breaker registered.</summary>
	public const int TransportCircuitBreakerRegistered = 60200;

	/// <summary>Transport circuit breaker unregistered.</summary>
	public const int TransportCircuitBreakerUnregistered = 60201;

	/// <summary>Transport circuit breaker retrieved.</summary>
	public const int TransportCircuitBreakerRetrieved = 60202;

	/// <summary>Transport circuit breaker not found.</summary>
	public const int TransportCircuitBreakerNotFound = 60203;

	/// <summary>All circuit breakers reset.</summary>
	public const int AllCircuitBreakersReset = 60204;

	// ========================================
	// 60300-60399: Retry Policy
	// ========================================

	/// <summary>Retry policy created.</summary>
	public const int RetryPolicyCreated = 60300;

	/// <summary>Polly retry policy factory created.</summary>
	public const int RetryPolicyFactoryCreated = 60301;

	/// <summary>Polly retry policy adapter created.</summary>
	public const int RetryPolicyAdapterCreated = 60302;

	/// <summary>Retry attempt started.</summary>
	public const int RetryAttemptStarted = 60303;

	/// <summary>Retry succeeded.</summary>
	public const int RetrySucceeded = 60304;

	/// <summary>Retry exhausted.</summary>
	public const int RetryExhausted = 60305;

	/// <summary>Retry delay calculated.</summary>
	public const int RetryDelayCalculated = 60306;

	/// <summary>Retry with jitter applied.</summary>
	public const int RetryWithJitter = 60307;

	/// <summary>Max retries exceeded.</summary>
	public const int RetryMaxExceeded = 60308;

	/// <summary>Jitter strategy used.</summary>
	public const int JitterStrategyUsed = 60309;

	/// <summary>Retry operation timed out.</summary>
	public const int RetryOperationTimeout = 60310;

	// ========================================
	// 60400-60499: Bulkhead
	// ========================================

	/// <summary>Bulkhead policy created.</summary>
	public const int BulkheadPolicyCreated = 60400;

	/// <summary>Bulkhead execution allowed.</summary>
	public const int BulkheadExecutionAllowed = 60401;

	/// <summary>Bulkhead execution rejected.</summary>
	public const int BulkheadExecutionRejected = 60402;

	/// <summary>Bulkhead queue full.</summary>
	public const int BulkheadQueueFull = 60403;

	/// <summary>Bulkhead slot acquired.</summary>
	public const int BulkheadSlotAcquired = 60404;

	/// <summary>Bulkhead slot released.</summary>
	public const int BulkheadSlotReleased = 60405;

	/// <summary>Bulkhead executing operation.</summary>
	public const int BulkheadExecuting = 60406;

	/// <summary>Bulkhead operation completed.</summary>
	public const int BulkheadCompleted = 60407;

	/// <summary>Bulkhead queueing operation.</summary>
	public const int BulkheadQueueing = 60408;

	// ========================================
	// 60500-60599: Timeout
	// ========================================

	/// <summary>Timeout manager created.</summary>
	public const int TimeoutManagerCreated = 60500;

	/// <summary>Timeout configured.</summary>
	public const int TimeoutConfigured = 60501;

	/// <summary>Timeout started.</summary>
	public const int TimeoutStarted = 60502;

	/// <summary>Timeout completed.</summary>
	public const int TimeoutCompleted = 60503;

	/// <summary>Timeout exceeded.</summary>
	public const int TimeoutExceeded = 60504;

	/// <summary>Timeout cancelled.</summary>
	public const int TimeoutCancelled = 60505;

	/// <summary>Timeout retrieved for operation.</summary>
	public const int TimeoutRetrieved = 60506;

	/// <summary>Custom timeout registered.</summary>
	public const int TimeoutRegistered = 60507;

	/// <summary>Slow operation detected (approaching timeout).</summary>
	public const int SlowOperationDetected = 60508;

	// ========================================
	// 60600-60699: Graceful Degradation
	// ========================================

	/// <summary>Graceful degradation service created.</summary>
	public const int GracefulDegradationCreated = 60600;

	/// <summary>Graceful degradation activated.</summary>
	public const int GracefulDegradationActivated = 60601;

	/// <summary>Graceful degradation deactivated.</summary>
	public const int GracefulDegradationDeactivated = 60602;

	/// <summary>Fallback executed.</summary>
	public const int FallbackExecuted = 60603;

	/// <summary>Degraded response returned.</summary>
	public const int DegradedResponseReturned = 60604;

	/// <summary>Degradation level changed.</summary>
	public const int DegradationLevelChanged = 60605;

	/// <summary>Operation rejected due to degradation policy.</summary>
	public const int DegradationOperationRejected = 60606;

	/// <summary>Health metrics updated.</summary>
	public const int DegradationHealthMetricsUpdated = 60607;

	/// <summary>Primary operation failed.</summary>
	public const int DegradationPrimaryOperationFailed = 60608;

	/// <summary>Health check error.</summary>
	public const int DegradationHealthCheckError = 60609;

	// ========================================
	// 60700-60799: Default Retry
	// ========================================

	/// <summary>Default retry policy applied.</summary>
	public const int DefaultRetryPolicyApplied = 60700;

	/// <summary>Default retry configuration loaded.</summary>
	public const int DefaultRetryConfigurationLoaded = 60701;

	/// <summary>Default retry attempt.</summary>
	public const int DefaultRetryAttempt = 60702;

	/// <summary>Default retry completed.</summary>
	public const int DefaultRetryCompleted = 60703;

	// ========================================
	// 60800-60899: Hedging
	// ========================================

	/// <summary>Hedging attempt launched.</summary>
	public const int HedgingAttemptLaunched = 60800;

	/// <summary>Hedging completed successfully.</summary>
	public const int HedgingCompleted = 60801;

	/// <summary>Hedging exhausted all attempts.</summary>
	public const int HedgingExhausted = 60802;

	// ========================================
	// 60900-60999: Resilience Telemetry
	// ========================================

	/// <summary>Resilience telemetry retry attempt recorded.</summary>
	public const int TelemetryRetryAttemptRecorded = 60900;

	/// <summary>Resilience telemetry circuit breaker transition recorded.</summary>
	public const int TelemetryCircuitBreakerTransition = 60901;
}
