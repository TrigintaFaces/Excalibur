// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Abstractions;

/// <summary>
/// Event IDs for transport abstractions (20000-20999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>20000-20099: Core Abstractions</item>
/// <item>20100-20199: Infrastructure/Kubernetes</item>
/// <item>20200-20299: Infrastructure/ServiceMesh</item>
/// <item>20300-20399: Service Discovery</item>
/// <item>20400-20499: Traffic Management</item>
/// <item>20500-20599: Security (TLS)</item>
/// <item>20600-20699: Connection Pooling</item>
/// <item>20700-20799: Session Management</item>
/// <item>20800-20899: Batch Processing</item>
/// <item>20900-20999: Dead Letter Queue</item>
/// </list>
/// </remarks>
public static class TransportAbstractionsEventId
{
	// ========================================
	// 20000-20099: Core Abstractions
	// ========================================

	/// <summary>Cloud message broker factory created.</summary>
	public const int BrokerFactoryCreated = 20000;

	/// <summary>Cloud provider registered.</summary>
	public const int CloudProviderRegistered = 20001;

	/// <summary>Cloud provider unregistered.</summary>
	public const int CloudProviderUnregistered = 20002;

	/// <summary>Transport adapter initialized.</summary>
	public const int TransportAdapterInitialized = 20003;

	/// <summary>Transport adapter disposed.</summary>
	public const int TransportAdapterDisposed = 20004;

	/// <summary>Returning cached broker for provider.</summary>
	public const int ReturningCachedBroker = 20005;

	/// <summary>Creating new broker for provider.</summary>
	public const int CreatingNewBroker = 20006;

	/// <summary>Broker created successfully.</summary>
	public const int BrokerCreatedSuccessfully = 20007;

	/// <summary>Error disposing broker.</summary>
	public const int BrokerDisposeError = 20008;

	/// <summary>Provider already registered (updating).</summary>
	public const int ProviderAlreadyRegistered = 20009;

	/// <summary>Provider unregistered.</summary>
	public const int ProviderUnregistered = 20010;

	/// <summary>Provider unregister attempt failed.</summary>
	public const int ProviderUnregisterFailed = 20011;

	/// <summary>Sending action via gRPC.</summary>
	public const int GrpcSendingAction = 20012;

	/// <summary>Publishing event via gRPC.</summary>
	public const int GrpcPublishingEvent = 20013;

	/// <summary>Sending document via gRPC.</summary>
	public const int GrpcSendingDocument = 20014;

	/// <summary>Flush metrics error.</summary>
	public const int FlushMetricsError = 20015;

	/// <summary>Flush timer error.</summary>
	public const int FlushTimerError = 20016;

	/// <summary>Local metric entry logged.</summary>
	public const int LocalMetricEntry = 20017;

	// ========================================
	// 20100-20199: Infrastructure/Kubernetes
	// ========================================

	// --- PodLifecycleManager (20100-20110) ---

	/// <summary>Pod lifecycle manager started.</summary>
	public const int PodLifecycleStarted = 20100;

	/// <summary>Running outside Kubernetes.</summary>
	public const int RunningOutsideKubernetes = 20101;

	/// <summary>Lifecycle hook registered.</summary>
	public const int LifecycleHookRegistered = 20102;

	/// <summary>Pod readiness updated.</summary>
	public const int PodReadinessUpdated = 20103;

	/// <summary>Pod readiness update failed.</summary>
	public const int PodReadinessUpdateFailed = 20104;

	/// <summary>Executing hooks for lifecycle event.</summary>
	public const int ExecutingHooks = 20105;

	/// <summary>Executing individual hook.</summary>
	public const int ExecutingHook = 20106;

	/// <summary>Hook execution error.</summary>
	public const int HookExecutionError = 20107;

	/// <summary>Pod marked for deletion.</summary>
	public const int PodMarkedForDeletion = 20108;

	/// <summary>Pod monitoring error.</summary>
	public const int PodMonitoringError = 20109;

	/// <summary>Pod event occurred.</summary>
	public const int PodEvent = 20110;

	// --- GracefulShutdownHandler (20111-20119) ---

	/// <summary>Graceful shutdown handler started.</summary>
	public const int GracefulShutdownStarted = 20111;

	/// <summary>Beginning shutdown sequence.</summary>
	public const int BeginningShutdownSequence = 20112;

	/// <summary>Graceful shutdown completed.</summary>
	public const int GracefulShutdownCompleted = 20113;

	/// <summary>Operation registered for shutdown.</summary>
	public const int OperationRegistered = 20114;

	/// <summary>Operation completed.</summary>
	public const int OperationCompleted = 20115;

	/// <summary>Waiting for in-flight operations.</summary>
	public const int WaitingForInFlightOperations = 20116;

	/// <summary>Shutdown timeout exceeded.</summary>
	public const int ShutdownTimeoutExceeded = 20117;

	/// <summary>Executing shutdown hook.</summary>
	public const int ExecutingShutdownHook = 20118;

	/// <summary>Shutdown hook error.</summary>
	public const int ShutdownHookError = 20119;

	// --- HpaMetricsProvider (20120-20126) ---

	/// <summary>HPA metrics provider started.</summary>
	public const int HpaMetricsProviderStarted = 20120;

	/// <summary>HPA metric registered.</summary>
	public const int HpaMetricRegistered = 20121;

	/// <summary>HPA metrics update error.</summary>
	public const int HpaMetricsUpdateError = 20122;

	/// <summary>Cgroup v1 memory limit read error.</summary>
	public const int CgroupV1MemoryLimitReadError = 20123;

	/// <summary>Cgroup v1 memory limit access denied.</summary>
	public const int CgroupV1MemoryLimitAccessDenied = 20124;

	/// <summary>Cgroup v2 memory limit read error.</summary>
	public const int CgroupV2MemoryLimitReadError = 20125;

	/// <summary>Cgroup v2 memory limit access denied.</summary>
	public const int CgroupV2MemoryLimitAccessDenied = 20126;

	// --- ConfigMapReloader (20127-20131) ---

	/// <summary>ConfigMap event occurred.</summary>
	public const int ConfigMapEvent = 20127;

	/// <summary>Secret event occurred.</summary>
	public const int SecretEvent = 20128;

	/// <summary>Configuration change detected.</summary>
	public const int ConfigurationChangeDetected = 20129;

	/// <summary>Reload callback error.</summary>
	public const int ReloadCallbackError = 20130;

	/// <summary>Watch error occurred.</summary>
	public const int WatchError = 20131;

	// ========================================
	// 20200-20299: Infrastructure/ServiceMesh
	// ========================================

	// --- ServiceMeshMiddleware (20200-20203) ---

	/// <summary>Service mesh middleware error.</summary>
	public const int ServiceMeshMiddlewareError = 20200;

	/// <summary>Service topology information logged.</summary>
	public const int ServiceTopologyLogged = 20201;

	/// <summary>Failed to record service topology.</summary>
	public const int TopologyRecordingFailed = 20202;

	/// <summary>Message processed through service mesh.</summary>
	public const int ServiceMeshMessageProcessed = 20203;

	// --- TrafficManager (20204-20206) ---

	/// <summary>Request completed by traffic manager.</summary>
	public const int TrafficRequestCompleted = 20204;

	/// <summary>Default traffic policy set.</summary>
	public const int TrafficDefaultPolicySet = 20205;

	/// <summary>Traffic split routing applied.</summary>
	public const int TrafficSplitRouting = 20206;

	// --- EnvoySidecarIntegration (20207-20211) ---

	/// <summary>Failed to get Envoy cluster info.</summary>
	public const int EnvoyGetClusterInfoFailed = 20207;

	/// <summary>Envoy runtime config updated.</summary>
	public const int EnvoyRuntimeConfigUpdated = 20208;

	/// <summary>Failed to update Envoy runtime config.</summary>
	public const int EnvoyUpdateRuntimeConfigFailed = 20209;

	/// <summary>Failed to flush Envoy stats.</summary>
	public const int EnvoyFlushStatsFailed = 20210;

	/// <summary>Failed to get load balancing stats.</summary>
	public const int EnvoyGetLoadBalancingStatsFailed = 20211;

	// --- CircuitBreakerProvider (20212-20219) ---

	/// <summary>Circuit breaker manually opened.</summary>
	public const int CircuitManuallyOpened = 20212;

	/// <summary>Circuit breaker reset.</summary>
	public const int CircuitReset = 20213;

	/// <summary>Request succeeded through circuit breaker.</summary>
	public const int CircuitRequestSucceeded = 20214;

	/// <summary>Circuit breaker is open, rejecting request.</summary>
	public const int CircuitBreakerOpen = 20215;

	/// <summary>Request failed through circuit breaker.</summary>
	public const int CircuitRequestFailed = 20216;

	/// <summary>Circuit breaker opened due to failures.</summary>
	public const int CircuitBreakerOpened = 20217;

	/// <summary>Circuit breaker reset after recovery.</summary>
	public const int CircuitBreakerReset = 20218;

	/// <summary>Circuit breaker transitioned to half-open.</summary>
	public const int CircuitBreakerHalfOpen = 20219;

	// ========================================
	// 20300-20399: Service Discovery
	// ========================================

	// --- ServiceDiscoveryProvider (20300-20308) ---

	/// <summary>No endpoints found for service.</summary>
	public const int NoEndpointsFound = 20300;

	/// <summary>No healthy endpoints found for service.</summary>
	public const int NoHealthyEndpointsFound = 20301;

	/// <summary>Discovered endpoints for service.</summary>
	public const int DiscoveredEndpoints = 20302;

	/// <summary>Service registered.</summary>
	public const int ServiceRegistered = 20303;

	/// <summary>Service deregistered.</summary>
	public const int ServiceDeregistered = 20304;

	/// <summary>Health status updated.</summary>
	public const int HealthStatusUpdated = 20305;

	/// <summary>Health check error.</summary>
	public const int HealthCheckError = 20306;

	/// <summary>Endpoint health changed.</summary>
	public const int EndpointHealthChanged = 20307;

	/// <summary>Health check failed.</summary>
	public const int HealthCheckFailed = 20308;

	// --- ConsulServiceRegistry (20309-20322) ---

	/// <summary>Retrieved service endpoints from Consul.</summary>
	public const int ConsulRetrievedServiceEndpoints = 20309;

	/// <summary>Failed to get service endpoints from Consul.</summary>
	public const int ConsulFailedToGetServiceEndpoints = 20310;

	/// <summary>Retrieved all endpoints from Consul.</summary>
	public const int ConsulRetrievedAllEndpoints = 20311;

	/// <summary>Failed to get all endpoints from Consul.</summary>
	public const int ConsulFailedToGetAllEndpoints = 20312;

	/// <summary>Service registration not found in Consul.</summary>
	public const int ConsulServiceRegistrationNotFound = 20313;

	/// <summary>Failed to get service registration from Consul.</summary>
	public const int ConsulFailedToGetServiceRegistration = 20314;

	/// <summary>Service registered with Consul.</summary>
	public const int ConsulServiceRegistered = 20315;

	/// <summary>Failed to register service with Consul.</summary>
	public const int ConsulFailedToRegisterService = 20316;

	/// <summary>Service deregistered from Consul.</summary>
	public const int ConsulServiceDeregistered = 20317;

	/// <summary>Failed to deregister service from Consul.</summary>
	public const int ConsulFailedToDeregisterService = 20318;

	/// <summary>Health status updated in Consul.</summary>
	public const int ConsulHealthStatusUpdated = 20319;

	/// <summary>Failed to update health status in Consul.</summary>
	public const int ConsulFailedToUpdateHealthStatus = 20320;

	/// <summary>Service changed in Consul.</summary>
	public const int ConsulServiceChanged = 20321;

	/// <summary>Error watching service in Consul.</summary>
	public const int ConsulWatchingServiceError = 20322;

	// ========================================
	// 20400-20499: Traffic Management
	// ========================================

	/// <summary>Traffic manager started.</summary>
	public const int TrafficManagerStarted = 20400;

	/// <summary>Traffic routed.</summary>
	public const int TrafficRouted = 20401;

	/// <summary>Circuit breaker provider created.</summary>
	public const int CircuitBreakerProviderCreated = 20402;

	/// <summary>Traffic throttled.</summary>
	public const int TrafficThrottled = 20403;

	// ========================================
	// 20500-20599: Security (TLS)
	// ========================================

	// --- MutualTlsProvider (20500-20516) ---

	/// <summary>HttpClient configured for mTLS.</summary>
	public const int MtlsHttpClientConfigured = 20500;

	/// <summary>Client certificate added.</summary>
	public const int MtlsClientCertificateAdded = 20501;

	/// <summary>Validating peer certificate.</summary>
	public const int MtlsValidatingPeerCertificate = 20502;

	/// <summary>SSL policy errors detected.</summary>
	public const int MtlsSslPolicyErrors = 20503;

	/// <summary>Chain validation failed.</summary>
	public const int MtlsChainValidationFailed = 20504;

	/// <summary>Attribute validation failed.</summary>
	public const int MtlsAttributeValidationFailed = 20505;

	/// <summary>Custom validation failed.</summary>
	public const int MtlsCustomValidationFailed = 20506;

	/// <summary>Validation successful.</summary>
	public const int MtlsValidationSuccessful = 20507;

	/// <summary>Refreshing certificate.</summary>
	public const int MtlsRefreshingCertificate = 20508;

	/// <summary>Certificate refreshed.</summary>
	public const int MtlsCertificateRefreshed = 20509;

	/// <summary>Certificate refresh failed.</summary>
	public const int MtlsRefreshFailed = 20510;

	/// <summary>No certificate presented by peer.</summary>
	public const int MtlsNoCertificatePresented = 20511;

	/// <summary>Chain validation error.</summary>
	public const int MtlsChainValidationError = 20512;

	/// <summary>Certificate is expired.</summary>
	public const int MtlsCertificateExpired = 20513;

	/// <summary>Certificate is not yet valid.</summary>
	public const int MtlsCertificateNotYetValid = 20514;

	/// <summary>Validating SAN.</summary>
	public const int MtlsValidatingSan = 20515;

	/// <summary>Missing digital signature key usage.</summary>
	public const int MtlsMissingDigitalSignature = 20516;

	// ========================================
	// 20600-20699: Connection Pooling
	// ========================================

	// --- GenericConnectionPool (20600-20614) ---

	/// <summary>Connection pool initialized.</summary>
	public const int ConnectionPoolInitialized = 20600;

	/// <summary>Connection acquired from pool.</summary>
	public const int ConnectionAcquired = 20601;

	/// <summary>Connection returned to pool.</summary>
	public const int ConnectionReturned = 20602;

	/// <summary>Connection pool warmup started.</summary>
	public const int ConnectionPoolWarmupStart = 20603;

	/// <summary>Connection pool warmup completed.</summary>
	public const int ConnectionPoolWarmupComplete = 20604;

	/// <summary>Connection pool disposing.</summary>
	public const int ConnectionPoolDisposing = 20605;

	/// <summary>Connection pool disposed.</summary>
	public const int ConnectionPoolDisposed = 20606;

	/// <summary>Connection created.</summary>
	public const int ConnectionCreated = 20607;

	/// <summary>Connection creation error.</summary>
	public const int ConnectionCreationError = 20608;

	/// <summary>Connection destroyed.</summary>
	public const int ConnectionDestroyed = 20609;

	/// <summary>Connection destroy error.</summary>
	public const int ConnectionDestroyError = 20610;

	/// <summary>Connection idle timeout.</summary>
	public const int ConnectionIdleTimeout = 20611;

	/// <summary>Connection health check error.</summary>
	public const int ConnectionHealthCheckError = 20612;

	/// <summary>Health check connection creation error.</summary>
	public const int HealthCheckConnectionCreationError = 20613;

	/// <summary>Health check error.</summary>
	public const int PoolHealthCheckError = 20614;

	// ========================================
	// 20700-20799: Session Management
	// ========================================

	// --- CloudSessionManager (20700-20715) ---

	/// <summary>Session lock acquired.</summary>
	public const int SessionLockAcquired = 20700;

	/// <summary>Session lock extended.</summary>
	public const int SessionLockExtended = 20701;

	/// <summary>Session lock released.</summary>
	public const int SessionLockReleased = 20702;

	/// <summary>Session created.</summary>
	public const int SessionCreated = 20703;

	/// <summary>Session opened.</summary>
	public const int SessionOpened = 20704;

	/// <summary>Session closed.</summary>
	public const int SessionClosed = 20705;

	/// <summary>Session renewed.</summary>
	public const int SessionRenewed = 20706;

	/// <summary>Session abandoned.</summary>
	public const int SessionAbandoned = 20707;

	/// <summary>Sessions cleaned up.</summary>
	public const int SessionsCleanedUp = 20708;

	/// <summary>Session state set.</summary>
	public const int SessionStateSet = 20709;

	/// <summary>Session state deleted.</summary>
	public const int SessionStateDeleted = 20710;

	/// <summary>Session checkpoint created.</summary>
	public const int SessionCheckpointCreated = 20711;

	/// <summary>Session checkpoint restored.</summary>
	public const int SessionCheckpointRestored = 20712;

	/// <summary>Session lock upgraded.</summary>
	public const int SessionLockUpgraded = 20713;

	/// <summary>Session lock downgraded.</summary>
	public const int SessionLockDowngraded = 20714;

	/// <summary>Session lock broken.</summary>
	public const int SessionLockBroken = 20715;

	// ========================================
	// 20800-20899: Batch Processing
	// ========================================

	// --- BatchProcessor (20800-20806) ---

	/// <summary>Batch processing failed.</summary>
	public const int BatchProcessingFailed = 20800;

	/// <summary>Batch processed successfully.</summary>
	public const int BatchProcessed = 20801;

	/// <summary>Batch processor configured.</summary>
	public const int BatchProcessorConfigured = 20802;

	/// <summary>Batch channel closed.</summary>
	public const int BatchChannelClosed = 20803;

	/// <summary>Batch processing stopped on error.</summary>
	public const int BatchStoppingOnError = 20804;

	/// <summary>Message failed after retries.</summary>
	public const int BatchMessageFailedAfterRetries = 20805;

	/// <summary>Message retrying.</summary>
	public const int BatchMessageRetrying = 20806;

	// ========================================
	// 20900-20999: Dead Letter Queue
	// ========================================

	/// <summary>Message moved to dead letter queue.</summary>
	public const int MessageMovedToDeadLetter = 20900;

	/// <summary>Dead letter queue processed.</summary>
	public const int DeadLetterQueueProcessed = 20901;

	/// <summary>Dead letter queue reprocessing started.</summary>
	public const int DeadLetterReprocessingStarted = 20902;

	/// <summary>Dead letter queue cleared.</summary>
	public const int DeadLetterQueueCleared = 20903;

	/// <summary>Exception that caused dead lettering.</summary>
	public const int DeadLetterException = 20904;

	/// <summary>Failed to parse dead letter message.</summary>
	public const int DeadLetterParseFailed = 20905;

	/// <summary>Retrieved messages from dead letter queue.</summary>
	public const int DeadLetterMessagesRetrieved = 20906;

	/// <summary>Failed to reprocess dead letter message.</summary>
	public const int DeadLetterReprocessFailed = 20907;

	/// <summary>Dead letter reprocessing summary.</summary>
	public const int DeadLetterReprocessSummary = 20908;

	/// <summary>Failed to purge dead letter message.</summary>
	public const int DeadLetterPurgeFailed = 20909;

	/// <summary>Dead letter queue purge completed.</summary>
	public const int DeadLetterPurgeCompleted = 20910;

	/// <summary>Message reprocessed from dead letter queue.</summary>
	public const int DeadLetterMessageReprocessed = 20911;
}
