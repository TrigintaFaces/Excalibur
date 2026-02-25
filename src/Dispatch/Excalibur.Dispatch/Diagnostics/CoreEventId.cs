// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Event IDs for core dispatcher infrastructure (10000-10999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>10000-10099: Dispatcher Infrastructure</item>
/// <item>10100-10199: Message Bus</item>
/// <item>10200-10299: Message Routing</item>
/// <item>10300-10399: Message Processing</item>
/// <item>10400-10499: Message Channels</item>
/// <item>10500-10599: CloudNative/CircuitBreaker</item>
/// <item>10600-10699: CloudEvents</item>
/// <item>10700-10799: High Performance (MicroBatch, Backpressure)</item>
/// <item>10800-10899: Object Pooling</item>
/// <item>10900-10999: Threading/Background Tasks</item>
/// </list>
/// </remarks>
public static class CoreEventId
{
	// ========================================
	// 10000-10099: Dispatcher Infrastructure
	// ========================================

	/// <summary>Dispatcher is starting up.</summary>
	public const int DispatcherStarting = 10000;

	/// <summary>Dispatcher has started successfully.</summary>
	public const int DispatcherStarted = 10001;

	/// <summary>Dispatcher is shutting down.</summary>
	public const int DispatcherStopping = 10002;

	/// <summary>Dispatcher has stopped.</summary>
	public const int DispatcherStopped = 10003;

	/// <summary>Pipeline configuration completed.</summary>
	public const int PipelineConfigured = 10010;

	/// <summary>Pipeline profile synthesized.</summary>
	public const int ProfileSynthesized = 10011;

	/// <summary>Beginning profile synthesis.</summary>
	public const int SynthesisBeginning = 10012;

	/// <summary>Synthesizing default profile.</summary>
	public const int SynthesizingDefaultProfile = 10013;

	/// <summary>Including middleware in profile.</summary>
	public const int MiddlewareIncluded = 10014;

	/// <summary>Omitting middleware from profile.</summary>
	public const int MiddlewareOmitted = 10015;

	/// <summary>Pipeline synthesis complete.</summary>
	public const int SynthesisComplete = 10016;

	/// <summary>Omitted middleware warning.</summary>
	public const int OmittedMiddlewareWarning = 10017;

	/// <summary>Synthesis completed successfully.</summary>
	public const int SynthesisSuccess = 10018;

	/// <summary>Mapped message kinds.</summary>
	public const int MappedMessageKinds = 10019;

	/// <summary>Synthesis encountered errors.</summary>
	public const int SynthesisError = 10020;

	/// <summary>Synthesis result summary.</summary>
	public const int SynthesisResult = 10021;

	/// <summary>Profile handles message kinds.</summary>
	public const int ProfileHandlesKinds = 10022;

	/// <summary>Pipeline synthesis warning.</summary>
	public const int SynthesisWarning = 10023;

	// ========================================
	// 10100-10199: Message Bus
	// ========================================

	/// <summary>Message bus connected.</summary>
	public const int MessageBusConnected = 10100;

	/// <summary>Message bus disconnected.</summary>
	public const int MessageBusDisconnected = 10101;

	/// <summary>Message published to bus.</summary>
	public const int MessagePublished = 10102;

	/// <summary>Message received from bus.</summary>
	public const int MessageReceived = 10103;

	/// <summary>Subscription created.</summary>
	public const int Subscribed = 10104;

	/// <summary>Subscription removed.</summary>
	public const int Unsubscribed = 10105;

	/// <summary>No message bus found.</summary>
	public const int NoMessageBusFound = 10106;

	/// <summary>Initializing message bus.</summary>
	public const int MessageBusInitializing = 10107;

	/// <summary>Publishing message.</summary>
	public const int PublishingMessage = 10108;

	/// <summary>Failed to publish message.</summary>
	public const int FailedToPublishMessage = 10109;

	// ========================================
	// 10200-10299: Message Routing
	// ========================================

	/// <summary>Route resolved for message.</summary>
	public const int RouteResolved = 10200;

	/// <summary>No route found for message.</summary>
	public const int NoRouteFound = 10201;

	/// <summary>Routing to handler.</summary>
	public const int RoutingToHandler = 10202;

	/// <summary>Handler route registered.</summary>
	public const int HandlerRouteRegistered = 10203;

	/// <summary>Route evaluation started.</summary>
	public const int RouteEvaluationStarted = 10204;

	/// <summary>Route evaluation completed.</summary>
	public const int RouteEvaluationCompleted = 10205;

	// ========================================
	// 10300-10399: Message Processing
	// ========================================

	/// <summary>Dispatching message to handler.</summary>
	public const int DispatchingMessage = 10300;

	/// <summary>Handler execution completed.</summary>
	public const int HandlerExecuted = 10301;

	/// <summary>Handler execution failed.</summary>
	public const int HandlerFailed = 10302;

	/// <summary>Dispatch handler failed.</summary>
	public const int DispatchHandlerFailed = 10303;

	/// <summary>Unhandled exception during dispatch.</summary>
	public const int UnhandledExceptionDuringDispatch = 10304;

	/// <summary>Cache hit check performed.</summary>
	public const int CacheHitCheck = 10305;

	// ========================================
	// 10400-10499: Message Channels
	// ========================================

	/// <summary>Channel created.</summary>
	public const int ChannelCreated = 10400;

	/// <summary>Channel closed.</summary>
	public const int ChannelClosed = 10401;

	/// <summary>Message pump starting.</summary>
	public const int MessagePumpStarting = 10402;

	/// <summary>Message pump stopping.</summary>
	public const int MessagePumpStopping = 10403;

	/// <summary>Channel message received.</summary>
	public const int ChannelMessageReceived = 10404;

	/// <summary>Channel message processed.</summary>
	public const int ChannelMessageProcessed = 10405;

	/// <summary>Message pump started successfully.</summary>
	public const int MessagePumpStarted = 10406;

	/// <summary>Message pump stopped with statistics.</summary>
	public const int MessagePumpStopped = 10407;

	/// <summary>Producer task failed.</summary>
	public const int ProducerFailed = 10408;

	/// <summary>Producer task timed out.</summary>
	public const int ProducerTimeout = 10409;

	/// <summary>Message acknowledged.</summary>
	public const int MessageAcknowledged = 10410;

	/// <summary>Message rejected.</summary>
	public const int MessageRejected = 10411;

	/// <summary>Batch of messages produced to channel.</summary>
	public const int BatchProduced = 10412;

	/// <summary>Channel is full, waiting for capacity.</summary>
	public const int ChannelFull = 10413;

	/// <summary>Error processing message in pump.</summary>
	public const int MessageProcessingError = 10414;

	/// <summary>Error in message pump.</summary>
	public const int MessagePumpError = 10415;

	// ========================================
	// 10500-10599: CloudNative/CircuitBreaker
	// ========================================

	/// <summary>Circuit breaker created.</summary>
	public const int CircuitBreakerCreated = 10500;

	/// <summary>Circuit breaker removed.</summary>
	public const int CircuitBreakerRemoved = 10501;

	/// <summary>Circuit breaker initializing.</summary>
	public const int CircuitBreakerInitializing = 10502;

	/// <summary>Circuit breaker starting.</summary>
	public const int CircuitBreakerStarting = 10503;

	/// <summary>Circuit breaker stopping.</summary>
	public const int CircuitBreakerStopping = 10504;

	/// <summary>Circuit breaker open, executing fallback.</summary>
	public const int CircuitBreakerOpenExecutingFallback = 10505;

	/// <summary>Operation failed in circuit breaker context.</summary>
	public const int OperationFailed = 10506;

	/// <summary>Circuit breaker reset to closed state.</summary>
	public const int CircuitBreakerReset = 10507;

	/// <summary>Circuit breaker transitioned to open state.</summary>
	public const int CircuitBreakerOpenTransition = 10508;

	/// <summary>Circuit breaker transitioned to half-open state.</summary>
	public const int CircuitBreakerHalfOpenTransition = 10509;

	/// <summary>Circuit breaker transitioned to closed state.</summary>
	public const int CircuitBreakerClosedTransition = 10510;

	/// <summary>Observer notification error.</summary>
	public const int ObserverNotificationError = 10511;

	/// <summary>Observer subscribed.</summary>
	public const int ObserverSubscribed = 10512;

	/// <summary>Observer unsubscribed.</summary>
	public const int ObserverUnsubscribed = 10513;

	/// <summary>Circuit breaker stop error.</summary>
	public const int CircuitBreakerStopError = 10514;

	// ========================================
	// 10600-10699: CloudEvents
	// ========================================

	/// <summary>CloudEvent received.</summary>
	public const int CloudEventReceived = 10600;

	/// <summary>CloudEvent processed.</summary>
	public const int CloudEventProcessed = 10601;

	/// <summary>CloudEvent without type.</summary>
	public const int CloudEventWithoutType = 10602;

	/// <summary>CloudEvent schema not found.</summary>
	public const int SchemaNotFound = 10603;

	/// <summary>CloudEvent schema validated.</summary>
	public const int SchemaValidated = 10604;

	// ========================================
	// 10700-10799: High Performance (MicroBatch, Backpressure)
	// ========================================

	/// <summary>Micro batch started.</summary>
	public const int MicroBatchStarted = 10700;

	/// <summary>Micro batch completed.</summary>
	public const int MicroBatchCompleted = 10701;

	/// <summary>Backpressure detected.</summary>
	public const int BackpressureDetected = 10702;

	/// <summary>Backpressure relieved.</summary>
	public const int BackpressureRelieved = 10703;

	/// <summary>Batch processing started.</summary>
	public const int BatchProcessingStarted = 10704;

	/// <summary>Batch processing completed.</summary>
	public const int BatchProcessingCompleted = 10705;

	/// <summary>Micro batch processing error.</summary>
	public const int MicroBatchError = 10706;

	/// <summary>Batch processing error.</summary>
	public const int BatchProcessingError = 10707;

	/// <summary>Batch flush error.</summary>
	public const int BatchFlushError = 10708;

	// ========================================
	// 10800-10899: Object Pooling
	// ========================================

	/// <summary>Object pool created.</summary>
	public const int PoolCreated = 10800;

	/// <summary>Object acquired from pool.</summary>
	public const int ObjectAcquired = 10801;

	/// <summary>Object returned to pool.</summary>
	public const int ObjectReturned = 10802;

	/// <summary>Pool leak detected.</summary>
	public const int PoolLeakDetected = 10803;

	/// <summary>Pool exhausted.</summary>
	public const int PoolExhausted = 10804;

	/// <summary>Connection pool created.</summary>
	public const int ConnectionPoolCreated = 10805;

	/// <summary>Connection acquired from pool.</summary>
	public const int ConnectionAcquired = 10806;

	/// <summary>Connection returned to pool.</summary>
	public const int ConnectionReturned = 10807;

	/// <summary>Connection pool initialized with min/max/initial size.</summary>
	public const int ConnectionPoolInitialized = 10808;

	/// <summary>Failed to acquire connection from pool.</summary>
	public const int ConnectionAcquisitionFailed = 10809;

	/// <summary>Connection returned to pool with status.</summary>
	public const int ConnectionReturnedToPool = 10810;

	/// <summary>Error returning connection to pool.</summary>
	public const int ConnectionReturnError = 10811;

	/// <summary>Health check failed for pool connection.</summary>
	public const int ConnectionHealthCheckFailed = 10812;

	/// <summary>Warming up connection pool.</summary>
	public const int WarmingUpPool = 10813;

	/// <summary>Pool warm-up completed.</summary>
	public const int PoolWarmUpCompleted = 10814;

	/// <summary>Cleanup removed expired connections.</summary>
	public const int CleanupRemovedConnections = 10815;

	/// <summary>Resizing connection pool.</summary>
	public const int ResizingPool = 10816;

	/// <summary>Disposing connection pool.</summary>
	public const int DisposingPool = 10817;

	/// <summary>Error disposing connection during shutdown.</summary>
	public const int ConnectionDisposalError = 10818;

	/// <summary>Pool disposed successfully.</summary>
	public const int PoolDisposedSuccessfully = 10819;

	/// <summary>Failed to create connection during warm-up.</summary>
	public const int WarmUpConnectionFailed = 10820;

	/// <summary>Connection disposed from pool.</summary>
	public const int ConnectionDisposedFromPool = 10821;

	/// <summary>Error disposing connection from pool.</summary>
	public const int ConnectionDisposalErrorFromPool = 10822;

	/// <summary>Health check callback failed.</summary>
	public const int HealthCheckFailedCallback = 10823;

	/// <summary>Cleanup callback failed.</summary>
	public const int CleanupFailedCallback = 10824;

	/// <summary>Buffer pooling disabled.</summary>
	public const int BufferPoolingDisabled = 10830;

	/// <summary>Pool trimmed.</summary>
	public const int PoolTrimmed = 10831;

	/// <summary>Error trimming pool.</summary>
	public const int PoolTrimError = 10832;

	/// <summary>Pool registered for management.</summary>
	public const int PoolRegisteredForManagement = 10833;

	/// <summary>Pool configuration changed.</summary>
	public const int PoolConfigurationChanged = 10834;

	/// <summary>Pool adapted.</summary>
	public const int PoolAdapted = 10835;

	/// <summary>Error adapting pool.</summary>
	public const int PoolAdaptationError = 10836;

	/// <summary>General pool adaptation error.</summary>
	public const int PoolAdaptationGeneralError = 10837;

	/// <summary>Memory pressure detected.</summary>
	public const int MemoryPressureDetected = 10838;

	/// <summary>Memory pressure relieved.</summary>
	public const int MemoryPressureRelieved = 10839;

	/// <summary>Error checking memory pressure.</summary>
	public const int MemoryPressureCheckError = 10840;

	/// <summary>Pool created with specific capacity.</summary>
	public const int PoolCreatedWithCapacity = 10841;

	/// <summary>Pool manager initialized.</summary>
	public const int PoolManagerInitialized = 10842;

	/// <summary>Object not rented from pool.</summary>
	public const int ObjectNotRentedFromPool = 10850;

	/// <summary>Pool disposed with statistics.</summary>
	public const int PoolDisposedStatistics = 10851;

	/// <summary>Object leak on disposal.</summary>
	public const int ObjectLeakOnDisposal = 10852;

	/// <summary>Potential object leak detected.</summary>
	public const int PotentialObjectLeakDetected = 10853;

	/// <summary>Array not rented from pool.</summary>
	public const int ArrayNotRentedFromPool = 10854;

	/// <summary>Potential array leak detected.</summary>
	public const int PotentialArrayLeak = 10855;

	/// <summary>Pool health report.</summary>
	public const int PoolHealthReport = 10860;

	// ========================================
	// 10900-10999: Threading/Background Tasks
	// ========================================

	/// <summary>Background task started.</summary>
	public const int BackgroundTaskStarted = 10900;

	/// <summary>Background task completed.</summary>
	public const int BackgroundTaskCompleted = 10901;

	/// <summary>Background task failed.</summary>
	public const int BackgroundTaskFailed = 10902;

	/// <summary>Background task cancelled.</summary>
	public const int BackgroundTaskCancelled = 10903;

	/// <summary>Thread pool task scheduled.</summary>
	public const int ThreadPoolTaskScheduled = 10904;

	/// <summary>Dedicated thread started.</summary>
	public const int DedicatedThreadStarted = 10905;

	/// <summary>Dedicated thread stopped.</summary>
	public const int DedicatedThreadStopped = 10906;

	/// <summary>Dedicated processor started.</summary>
	public const int DedicatedProcessorStarted = 10907;

	/// <summary>Dedicated processor stopped.</summary>
	public const int DedicatedProcessorStopped = 10908;

	/// <summary>Dedicated processor processing error.</summary>
	public const int DedicatedProcessorError = 10909;

	/// <summary>Dedicated processor fatal error.</summary>
	public const int DedicatedProcessorFatalError = 10910;

	/// <summary>Unhandled background task exception.</summary>
	public const int UnhandledBackgroundException = 10911;

	/// <summary>Background execution invalid.</summary>
	public const int BackgroundExecutionInvalid = 10912;

	/// <summary>Background execution failed.</summary>
	public const int BackgroundExecutionFailed = 10913;

	/// <summary>Background execution critical error.</summary>
	public const int BackgroundExecutionCritical = 10914;

	/// <summary>Background exception not propagated.</summary>
	public const int BackgroundExceptionNotPropagated = 10915;
}
