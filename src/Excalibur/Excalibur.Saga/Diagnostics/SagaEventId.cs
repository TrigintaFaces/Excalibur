// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Diagnostics;

/// <summary>
/// Event IDs for saga/process manager infrastructure (120000-123999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>120000-120999: Saga Core</item>
/// <item>121000-121999: Saga State Management</item>
/// <item>122000-122999: Saga Coordination</item>
/// <item>123000-123999: Saga Storage</item>
/// </list>
/// </remarks>
public static class SagaEventId
{
	// ========================================
	// 120000-120099: Saga Core
	// ========================================

	/// <summary>Saga manager created.</summary>
	public const int SagaManagerCreated = 120000;

	/// <summary>Saga started.</summary>
	public const int SagaStarted = 120001;

	/// <summary>Saga completed.</summary>
	public const int SagaCompleted = 120002;

	/// <summary>Saga failed.</summary>
	public const int SagaFailed = 120003;

	/// <summary>Saga compensating.</summary>
	public const int SagaCompensating = 120004;

	/// <summary>Saga compensated.</summary>
	public const int SagaCompensated = 120005;

	// ========================================
	// 120100-120199: Saga Lifecycle
	// ========================================

	/// <summary>Saga step started.</summary>
	public const int SagaStepStarted = 120100;

	/// <summary>Saga step completed.</summary>
	public const int SagaStepCompleted = 120101;

	/// <summary>Saga step failed.</summary>
	public const int SagaStepFailed = 120102;

	/// <summary>Saga step compensating.</summary>
	public const int SagaStepCompensating = 120103;

	/// <summary>Saga step compensated.</summary>
	public const int SagaStepCompensated = 120104;

	/// <summary>Saga step skipped.</summary>
	public const int SagaStepSkipped = 120105;

	// ========================================
	// 121000-121099: Saga State Management
	// ========================================

	/// <summary>Saga state loaded.</summary>
	public const int SagaStateLoaded = 121000;

	/// <summary>Saga state saved.</summary>
	public const int SagaStateSaved = 121001;

	/// <summary>Saga state transitioned.</summary>
	public const int SagaStateTransitioned = 121002;

	/// <summary>Saga state not found.</summary>
	public const int SagaStateNotFound = 121003;

	/// <summary>Saga state deleted.</summary>
	public const int SagaStateDeleted = 121004;

	// ========================================
	// 121100-121199: Saga Message Handling
	// ========================================

	/// <summary>Saga message received.</summary>
	public const int SagaMessageReceived = 121100;

	/// <summary>Saga message handled.</summary>
	public const int SagaMessageHandled = 121101;

	/// <summary>Saga message deferred.</summary>
	public const int SagaMessageDeferred = 121102;

	/// <summary>Saga timeout scheduled.</summary>
	public const int SagaTimeoutScheduled = 121103;

	/// <summary>Saga timeout triggered.</summary>
	public const int SagaTimeoutTriggered = 121104;

	// ========================================
	// 122000-122099: Saga Coordination
	// ========================================

	/// <summary>Saga coordinator created.</summary>
	public const int SagaCoordinatorCreated = 122000;

	/// <summary>Saga command dispatched.</summary>
	public const int SagaCommandDispatched = 122001;

	/// <summary>Saga event published.</summary>
	public const int SagaEventPublished = 122002;

	/// <summary>Saga reply received.</summary>
	public const int SagaReplyReceived = 122003;

	/// <summary>Saga correlation established.</summary>
	public const int SagaCorrelationEstablished = 122004;

	// ========================================
	// 122100-122199: Saga Recovery
	// ========================================

	/// <summary>Saga recovery started.</summary>
	public const int SagaRecoveryStarted = 122100;

	/// <summary>Saga recovery completed.</summary>
	public const int SagaRecoveryCompleted = 122101;

	/// <summary>Saga retry scheduled.</summary>
	public const int SagaRetryScheduled = 122102;

	/// <summary>Saga retry executed.</summary>
	public const int SagaRetryExecuted = 122103;

	/// <summary>Saga dead letter stored.</summary>
	public const int SagaDeadLetterStored = 122104;

	// ========================================
	// 123000-123099: Saga Storage Core
	// ========================================

	/// <summary>Saga store created.</summary>
	public const int SagaStoreCreated = 123000;

	/// <summary>Saga persisted.</summary>
	public const int SagaPersisted = 123001;

	/// <summary>Saga loaded from store.</summary>
	public const int SagaLoadedFromStore = 123002;

	/// <summary>Saga removed from store.</summary>
	public const int SagaRemovedFromStore = 123003;

	/// <summary>Saga store queried.</summary>
	public const int SagaStoreQueried = 123004;

	// ========================================
	// 123100-123199: Saga Storage Providers
	// ========================================

	/// <summary>SQL Server saga store created.</summary>
	public const int SqlServerSagaStoreCreated = 123100;

	/// <summary>In-memory saga store created.</summary>
	public const int InMemorySagaStoreCreated = 123101;

	/// <summary>Saga concurrency conflict.</summary>
	public const int SagaConcurrencyConflict = 123102;

	/// <summary>Saga store connection error.</summary>
	public const int SagaStoreConnectionError = 123103;

	// ========================================
	// 120200-120299: Conditional Saga Steps
	// ========================================

	/// <summary>Condition evaluation started.</summary>
	public const int ConditionEvaluationStarted = 120200;

	/// <summary>Condition evaluation completed.</summary>
	public const int ConditionEvaluationCompleted = 120201;

	/// <summary>Condition evaluation error.</summary>
	public const int ConditionEvaluationError = 120202;

	/// <summary>Conditional step execution started.</summary>
	public const int ConditionalStepExecutionStarted = 120203;

	/// <summary>No step to execute for condition.</summary>
	public const int NoStepToExecute = 120204;

	/// <summary>Branch execution.</summary>
	public const int BranchExecution = 120205;

	/// <summary>Conditional step execution failed.</summary>
	public const int ConditionalStepExecutionFailed = 120206;

	/// <summary>Conditional step compensation started.</summary>
	public const int ConditionalStepCompensationStarted = 120207;

	/// <summary>No step executed, skipping compensation.</summary>
	public const int NoStepExecutedSkippingCompensation = 120208;

	/// <summary>Executed step cannot be compensated.</summary>
	public const int ExecutedStepCannotBeCompensated = 120209;

	/// <summary>Compensating executed step.</summary>
	public const int CompensatingExecutedStep = 120210;

	/// <summary>Conditional step compensation failed.</summary>
	public const int ConditionalStepCompensationFailed = 120211;

	// ========================================
	// 120300-120399: Parallel Saga Steps
	// ========================================

	/// <summary>Parallel step execution started.</summary>
	public const int ParallelStepExecutionStarted = 120300;

	/// <summary>Starting parallel step execution.</summary>
	public const int StartingParallelStepExecution = 120301;

	/// <summary>Parallel step completed.</summary>
	public const int ParallelStepCompleted = 120302;

	/// <summary>Parallel step failed.</summary>
	public const int ParallelStepFailed = 120303;

	/// <summary>Parallel execution completed.</summary>
	public const int ParallelExecutionCompleted = 120304;

	/// <summary>Parallel execution failed.</summary>
	public const int ParallelExecutionFailed = 120305;

	/// <summary>Parallel step compensation started.</summary>
	public const int ParallelStepCompensationStarted = 120306;

	/// <summary>Compensating parallel step.</summary>
	public const int CompensatingParallelStep = 120307;

	/// <summary>Parallel compensation completed.</summary>
	public const int ParallelCompensationCompleted = 120308;

	/// <summary>Parallel compensation failed.</summary>
	public const int ParallelCompensationFailed = 120309;

	// ========================================
	// 121200-121299: Saga Timeout Delivery
	// ========================================

	/// <summary>Timeout delivery started.</summary>
	public const int TimeoutDeliveryStarted = 121200;

	/// <summary>Timeout processing started.</summary>
	public const int TimeoutProcessingStarted = 121201;

	/// <summary>Timeout delivered successfully.</summary>
	public const int TimeoutDeliveredSuccessfully = 121202;

	/// <summary>Timeout delivery failed.</summary>
	public const int TimeoutDeliveryFailed = 121203;

	/// <summary>Timeout batch processing completed.</summary>
	public const int TimeoutBatchCompleted = 121204;

	/// <summary>Timeout service stopped.</summary>
	public const int TimeoutServiceStopped = 121205;

	/// <summary>Timeout type resolution failed.</summary>
	public const int TimeoutTypeResolutionFailed = 121206;

	/// <summary>Timeout message creation failed.</summary>
	public const int TimeoutMessageCreationFailed = 121207;

	/// <summary>Timeout message type invalid.</summary>
	public const int TimeoutMessageTypeInvalid = 121208;

	// ========================================
	// 122200-122299: Saga Coordinator
	// ========================================

	/// <summary>Saga execution starting.</summary>
	public const int SagaExecutionStarting = 122200;

	/// <summary>Saga step executing.</summary>
	public const int SagaStepExecuting = 122201;

	/// <summary>Saga execution completed successfully.</summary>
	public const int SagaExecutionCompletedSuccessfully = 122202;

	/// <summary>Saga step failed, starting compensation.</summary>
	public const int SagaStepFailedStartingCompensation = 122203;

	/// <summary>Saga compensation completed.</summary>
	public const int SagaCompensationCompleted = 122204;

	/// <summary>Saga execution failed.</summary>
	public const int SagaExecutionFailed = 122205;

	// ========================================
	// 120400-120499: Saga Base/Middleware
	// ========================================

	/// <summary>Saga handling started.</summary>
	public const int SagaHandlingStarted = 120400;

	/// <summary>Saga handling completed.</summary>
	public const int SagaHandlingCompleted = 120401;

	/// <summary>Saga handling failed.</summary>
	public const int SagaHandlingFailed = 120402;

	/// <summary>Saga initialization started.</summary>
	public const int SagaInitializationStarted = 120403;

	/// <summary>Saga middleware processing.</summary>
	public const int SagaMiddlewareProcessing = 120404;

	// ========================================
	// 123200-123299: Saga SqlServer Timeout Store
	// ========================================

	/// <summary>Timeout scheduled for saga.</summary>
	public const int TimeoutScheduled = 123200;

	/// <summary>Timeout cancelled for saga.</summary>
	public const int TimeoutCancelled = 123201;

	/// <summary>All timeouts cancelled for saga.</summary>
	public const int AllTimeoutsCancelled = 123202;

	/// <summary>Timeout marked as delivered.</summary>
	public const int TimeoutMarkedDelivered = 123203;

	// ========================================
	// 123300-123399: Saga SqlServer Monitoring
	// ========================================

	/// <summary>Running sagas count retrieved.</summary>
	public const int RunningCountRetrieved = 123300;

	/// <summary>Completed sagas count retrieved.</summary>
	public const int CompletedCountRetrieved = 123301;

	/// <summary>Stuck sagas retrieved.</summary>
	public const int StuckSagasRetrieved = 123302;

	/// <summary>Failed sagas retrieved.</summary>
	public const int FailedSagasRetrieved = 123303;

	/// <summary>Average completion time retrieved.</summary>
	public const int AverageCompletionTimeRetrieved = 123304;

	// ========================================
	// 120500-120599: Multi-Conditional Saga Steps
	// ========================================

	/// <summary>Evaluating branch for multi-conditional step.</summary>
	public const int MultiCondEvaluatingBranch = 120500;

	/// <summary>Multi-conditional step branch evaluated.</summary>
	public const int MultiCondBranchEvaluated = 120501;

	/// <summary>Error evaluating branch for multi-conditional step.</summary>
	public const int MultiCondBranchEvaluationError = 120502;

	/// <summary>Executing multi-conditional saga step.</summary>
	public const int MultiCondExecutingStep = 120503;

	/// <summary>Executing branch in multi-conditional step.</summary>
	public const int MultiCondExecutingBranch = 120504;

	/// <summary>Executing default step in multi-conditional step.</summary>
	public const int MultiCondExecutingDefaultStep = 120505;

	/// <summary>No branch found in multi-conditional step.</summary>
	public const int MultiCondNoBranchFound = 120506;

	/// <summary>Multi-conditional saga step failed.</summary>
	public const int MultiCondStepFailed = 120507;

	/// <summary>Compensating multi-conditional saga step.</summary>
	public const int MultiCondCompensatingStep = 120508;

	/// <summary>No step executed, skipping compensation.</summary>
	public const int MultiCondSkippingCompensation = 120509;

	/// <summary>Executed step cannot be compensated.</summary>
	public const int MultiCondStepCannotCompensate = 120510;

	/// <summary>Compensating executed step from branch.</summary>
	public const int MultiCondCompensatingExecutedStep = 120511;

	/// <summary>Failed to compensate multi-conditional saga step.</summary>
	public const int MultiCondCompensationFailed = 120512;

	// ========================================
	// 122300-122399: Saga Outbox
	// ========================================

	/// <summary>Publishing events through outbox for saga.</summary>
	public const int SagaOutboxPublishing = 122300;

	/// <summary>Events published through outbox for saga.</summary>
	public const int SagaOutboxPublished = 122301;

	/// <summary>Saga outbox publish delegate is not configured.</summary>
	public const int SagaOutboxDelegateNotConfigured = 122302;
}
