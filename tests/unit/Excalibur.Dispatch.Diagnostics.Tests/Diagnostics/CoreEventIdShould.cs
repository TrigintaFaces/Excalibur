// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="CoreEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait("Priority", "0")]
public sealed class CoreEventIdShould : UnitTestBase
{
	#region Dispatcher Infrastructure Event IDs (10000-10099)

	[Fact]
	public void HaveDispatcherStartingInInfrastructureRange()
	{
		CoreEventId.DispatcherStarting.ShouldBe(10000);
	}

	[Fact]
	public void HaveDispatcherStartedInInfrastructureRange()
	{
		CoreEventId.DispatcherStarted.ShouldBe(10001);
	}

	[Fact]
	public void HaveDispatcherStoppingInInfrastructureRange()
	{
		CoreEventId.DispatcherStopping.ShouldBe(10002);
	}

	[Fact]
	public void HaveDispatcherStoppedInInfrastructureRange()
	{
		CoreEventId.DispatcherStopped.ShouldBe(10003);
	}

	[Fact]
	public void HavePipelineConfiguredInInfrastructureRange()
	{
		CoreEventId.PipelineConfigured.ShouldBe(10010);
	}

	[Fact]
	public void HaveProfileSynthesizedInInfrastructureRange()
	{
		CoreEventId.ProfileSynthesized.ShouldBe(10011);
	}

	[Fact]
	public void HaveSynthesisBeginningInInfrastructureRange()
	{
		CoreEventId.SynthesisBeginning.ShouldBe(10012);
	}

	[Fact]
	public void HaveSynthesizingDefaultProfileInInfrastructureRange()
	{
		CoreEventId.SynthesizingDefaultProfile.ShouldBe(10013);
	}

	[Fact]
	public void HaveMiddlewareIncludedInInfrastructureRange()
	{
		CoreEventId.MiddlewareIncluded.ShouldBe(10014);
	}

	[Fact]
	public void HaveMiddlewareOmittedInInfrastructureRange()
	{
		CoreEventId.MiddlewareOmitted.ShouldBe(10015);
	}

	[Fact]
	public void HaveSynthesisCompleteInInfrastructureRange()
	{
		CoreEventId.SynthesisComplete.ShouldBe(10016);
	}

	[Fact]
	public void HaveOmittedMiddlewareWarningInInfrastructureRange()
	{
		CoreEventId.OmittedMiddlewareWarning.ShouldBe(10017);
	}

	[Fact]
	public void HaveSynthesisSuccessInInfrastructureRange()
	{
		CoreEventId.SynthesisSuccess.ShouldBe(10018);
	}

	[Fact]
	public void HaveMappedMessageKindsInInfrastructureRange()
	{
		CoreEventId.MappedMessageKinds.ShouldBe(10019);
	}

	[Fact]
	public void HaveSynthesisErrorInInfrastructureRange()
	{
		CoreEventId.SynthesisError.ShouldBe(10020);
	}

	[Fact]
	public void HaveSynthesisResultInInfrastructureRange()
	{
		CoreEventId.SynthesisResult.ShouldBe(10021);
	}

	[Fact]
	public void HaveProfileHandlesKindsInInfrastructureRange()
	{
		CoreEventId.ProfileHandlesKinds.ShouldBe(10022);
	}

	[Fact]
	public void HaveSynthesisWarningInInfrastructureRange()
	{
		CoreEventId.SynthesisWarning.ShouldBe(10023);
	}

	#endregion Dispatcher Infrastructure Event IDs (10000-10099)

	#region Message Bus Event IDs (10100-10199)

	[Fact]
	public void HaveMessageBusConnectedInMessageBusRange()
	{
		CoreEventId.MessageBusConnected.ShouldBe(10100);
	}

	[Fact]
	public void HaveMessageBusDisconnectedInMessageBusRange()
	{
		CoreEventId.MessageBusDisconnected.ShouldBe(10101);
	}

	[Fact]
	public void HaveMessagePublishedInMessageBusRange()
	{
		CoreEventId.MessagePublished.ShouldBe(10102);
	}

	[Fact]
	public void HaveMessageReceivedInMessageBusRange()
	{
		CoreEventId.MessageReceived.ShouldBe(10103);
	}

	[Fact]
	public void HaveSubscribedInMessageBusRange()
	{
		CoreEventId.Subscribed.ShouldBe(10104);
	}

	[Fact]
	public void HaveUnsubscribedInMessageBusRange()
	{
		CoreEventId.Unsubscribed.ShouldBe(10105);
	}

	[Fact]
	public void HaveNoMessageBusFoundInMessageBusRange()
	{
		CoreEventId.NoMessageBusFound.ShouldBe(10106);
	}

	[Fact]
	public void HaveMessageBusInitializingInMessageBusRange()
	{
		CoreEventId.MessageBusInitializing.ShouldBe(10107);
	}

	[Fact]
	public void HavePublishingMessageInMessageBusRange()
	{
		CoreEventId.PublishingMessage.ShouldBe(10108);
	}

	[Fact]
	public void HaveFailedToPublishMessageInMessageBusRange()
	{
		CoreEventId.FailedToPublishMessage.ShouldBe(10109);
	}

	#endregion Message Bus Event IDs (10100-10199)

	#region Message Routing Event IDs (10200-10299)

	[Fact]
	public void HaveRouteResolvedInRoutingRange()
	{
		CoreEventId.RouteResolved.ShouldBe(10200);
	}

	[Fact]
	public void HaveNoRouteFoundInRoutingRange()
	{
		CoreEventId.NoRouteFound.ShouldBe(10201);
	}

	[Fact]
	public void HaveRoutingToHandlerInRoutingRange()
	{
		CoreEventId.RoutingToHandler.ShouldBe(10202);
	}

	[Fact]
	public void HaveHandlerRouteRegisteredInRoutingRange()
	{
		CoreEventId.HandlerRouteRegistered.ShouldBe(10203);
	}

	[Fact]
	public void HaveRouteEvaluationStartedInRoutingRange()
	{
		CoreEventId.RouteEvaluationStarted.ShouldBe(10204);
	}

	[Fact]
	public void HaveRouteEvaluationCompletedInRoutingRange()
	{
		CoreEventId.RouteEvaluationCompleted.ShouldBe(10205);
	}

	#endregion Message Routing Event IDs (10200-10299)

	#region Message Processing Event IDs (10300-10399)

	[Fact]
	public void HaveDispatchingMessageInProcessingRange()
	{
		CoreEventId.DispatchingMessage.ShouldBe(10300);
	}

	[Fact]
	public void HaveHandlerExecutedInProcessingRange()
	{
		CoreEventId.HandlerExecuted.ShouldBe(10301);
	}

	[Fact]
	public void HaveHandlerFailedInProcessingRange()
	{
		CoreEventId.HandlerFailed.ShouldBe(10302);
	}

	[Fact]
	public void HaveDispatchHandlerFailedInProcessingRange()
	{
		CoreEventId.DispatchHandlerFailed.ShouldBe(10303);
	}

	[Fact]
	public void HaveUnhandledExceptionDuringDispatchInProcessingRange()
	{
		CoreEventId.UnhandledExceptionDuringDispatch.ShouldBe(10304);
	}

	[Fact]
	public void HaveCacheHitCheckInProcessingRange()
	{
		CoreEventId.CacheHitCheck.ShouldBe(10305);
	}

	#endregion Message Processing Event IDs (10300-10399)

	#region Message Channels Event IDs (10400-10499)

	[Fact]
	public void HaveChannelCreatedInChannelsRange()
	{
		CoreEventId.ChannelCreated.ShouldBe(10400);
	}

	[Fact]
	public void HaveChannelClosedInChannelsRange()
	{
		CoreEventId.ChannelClosed.ShouldBe(10401);
	}

	[Fact]
	public void HaveMessagePumpStartingInChannelsRange()
	{
		CoreEventId.MessagePumpStarting.ShouldBe(10402);
	}

	[Fact]
	public void HaveMessagePumpStoppingInChannelsRange()
	{
		CoreEventId.MessagePumpStopping.ShouldBe(10403);
	}

	[Fact]
	public void HaveChannelMessageReceivedInChannelsRange()
	{
		CoreEventId.ChannelMessageReceived.ShouldBe(10404);
	}

	[Fact]
	public void HaveChannelMessageProcessedInChannelsRange()
	{
		CoreEventId.ChannelMessageProcessed.ShouldBe(10405);
	}

	[Fact]
	public void HaveMessagePumpStartedInChannelsRange()
	{
		CoreEventId.MessagePumpStarted.ShouldBe(10406);
	}

	[Fact]
	public void HaveMessagePumpStoppedInChannelsRange()
	{
		CoreEventId.MessagePumpStopped.ShouldBe(10407);
	}

	[Fact]
	public void HaveProducerFailedInChannelsRange()
	{
		CoreEventId.ProducerFailed.ShouldBe(10408);
	}

	[Fact]
	public void HaveProducerTimeoutInChannelsRange()
	{
		CoreEventId.ProducerTimeout.ShouldBe(10409);
	}

	[Fact]
	public void HaveMessageAcknowledgedInChannelsRange()
	{
		CoreEventId.MessageAcknowledged.ShouldBe(10410);
	}

	[Fact]
	public void HaveMessageRejectedInChannelsRange()
	{
		CoreEventId.MessageRejected.ShouldBe(10411);
	}

	[Fact]
	public void HaveBatchProducedInChannelsRange()
	{
		CoreEventId.BatchProduced.ShouldBe(10412);
	}

	[Fact]
	public void HaveChannelFullInChannelsRange()
	{
		CoreEventId.ChannelFull.ShouldBe(10413);
	}

	[Fact]
	public void HaveMessageProcessingErrorInChannelsRange()
	{
		CoreEventId.MessageProcessingError.ShouldBe(10414);
	}

	[Fact]
	public void HaveMessagePumpErrorInChannelsRange()
	{
		CoreEventId.MessagePumpError.ShouldBe(10415);
	}

	#endregion Message Channels Event IDs (10400-10499)

	#region CloudNative/CircuitBreaker Event IDs (10500-10599)

	[Fact]
	public void HaveCircuitBreakerCreatedInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerCreated.ShouldBe(10500);
	}

	[Fact]
	public void HaveCircuitBreakerRemovedInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerRemoved.ShouldBe(10501);
	}

	[Fact]
	public void HaveCircuitBreakerInitializingInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerInitializing.ShouldBe(10502);
	}

	[Fact]
	public void HaveCircuitBreakerStartingInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerStarting.ShouldBe(10503);
	}

	[Fact]
	public void HaveCircuitBreakerStoppingInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerStopping.ShouldBe(10504);
	}

	[Fact]
	public void HaveCircuitBreakerOpenExecutingFallbackInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerOpenExecutingFallback.ShouldBe(10505);
	}

	[Fact]
	public void HaveOperationFailedInCircuitBreakerRange()
	{
		CoreEventId.OperationFailed.ShouldBe(10506);
	}

	[Fact]
	public void HaveCircuitBreakerResetInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerReset.ShouldBe(10507);
	}

	[Fact]
	public void HaveCircuitBreakerOpenTransitionInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerOpenTransition.ShouldBe(10508);
	}

	[Fact]
	public void HaveCircuitBreakerHalfOpenTransitionInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerHalfOpenTransition.ShouldBe(10509);
	}

	[Fact]
	public void HaveCircuitBreakerClosedTransitionInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerClosedTransition.ShouldBe(10510);
	}

	[Fact]
	public void HaveObserverNotificationErrorInCircuitBreakerRange()
	{
		CoreEventId.ObserverNotificationError.ShouldBe(10511);
	}

	[Fact]
	public void HaveObserverSubscribedInCircuitBreakerRange()
	{
		CoreEventId.ObserverSubscribed.ShouldBe(10512);
	}

	[Fact]
	public void HaveObserverUnsubscribedInCircuitBreakerRange()
	{
		CoreEventId.ObserverUnsubscribed.ShouldBe(10513);
	}

	[Fact]
	public void HaveCircuitBreakerStopErrorInCircuitBreakerRange()
	{
		CoreEventId.CircuitBreakerStopError.ShouldBe(10514);
	}

	#endregion CloudNative/CircuitBreaker Event IDs (10500-10599)

	#region CloudEvents Event IDs (10600-10699)

	[Fact]
	public void HaveCloudEventReceivedInCloudEventsRange()
	{
		CoreEventId.CloudEventReceived.ShouldBe(10600);
	}

	[Fact]
	public void HaveCloudEventProcessedInCloudEventsRange()
	{
		CoreEventId.CloudEventProcessed.ShouldBe(10601);
	}

	[Fact]
	public void HaveCloudEventWithoutTypeInCloudEventsRange()
	{
		CoreEventId.CloudEventWithoutType.ShouldBe(10602);
	}

	[Fact]
	public void HaveSchemaNotFoundInCloudEventsRange()
	{
		CoreEventId.SchemaNotFound.ShouldBe(10603);
	}

	[Fact]
	public void HaveSchemaValidatedInCloudEventsRange()
	{
		CoreEventId.SchemaValidated.ShouldBe(10604);
	}

	#endregion CloudEvents Event IDs (10600-10699)

	#region Batch Processing Event IDs (10700-10799)

	[Fact]
	public void HaveMicroBatchStartedInBatchProcessingRange()
	{
		CoreEventId.MicroBatchStarted.ShouldBe(10700);
	}

	[Fact]
	public void HaveMicroBatchCompletedInBatchProcessingRange()
	{
		CoreEventId.MicroBatchCompleted.ShouldBe(10701);
	}

	[Fact]
	public void HaveBackpressureDetectedInBatchProcessingRange()
	{
		CoreEventId.BackpressureDetected.ShouldBe(10702);
	}

	[Fact]
	public void HaveBackpressureRelievedInBatchProcessingRange()
	{
		CoreEventId.BackpressureRelieved.ShouldBe(10703);
	}

	[Fact]
	public void HaveBatchProcessingStartedInBatchProcessingRange()
	{
		CoreEventId.BatchProcessingStarted.ShouldBe(10704);
	}

	[Fact]
	public void HaveBatchProcessingCompletedInBatchProcessingRange()
	{
		CoreEventId.BatchProcessingCompleted.ShouldBe(10705);
	}

	[Fact]
	public void HaveMicroBatchErrorInBatchProcessingRange()
	{
		CoreEventId.MicroBatchError.ShouldBe(10706);
	}

	[Fact]
	public void HaveBatchProcessingErrorInBatchProcessingRange()
	{
		CoreEventId.BatchProcessingError.ShouldBe(10707);
	}

	[Fact]
	public void HaveBatchFlushErrorInBatchProcessingRange()
	{
		CoreEventId.BatchFlushError.ShouldBe(10708);
	}

	#endregion Batch Processing Event IDs (10700-10799)

	#region Object Pooling Event IDs (10800-10899)

	[Fact]
	public void HavePoolCreatedInPoolingRange()
	{
		CoreEventId.PoolCreated.ShouldBe(10800);
	}

	[Fact]
	public void HaveObjectAcquiredInPoolingRange()
	{
		CoreEventId.ObjectAcquired.ShouldBe(10801);
	}

	[Fact]
	public void HaveObjectReturnedInPoolingRange()
	{
		CoreEventId.ObjectReturned.ShouldBe(10802);
	}

	[Fact]
	public void HavePoolLeakDetectedInPoolingRange()
	{
		CoreEventId.PoolLeakDetected.ShouldBe(10803);
	}

	[Fact]
	public void HavePoolExhaustedInPoolingRange()
	{
		CoreEventId.PoolExhausted.ShouldBe(10804);
	}

	[Fact]
	public void HaveConnectionPoolCreatedInPoolingRange()
	{
		CoreEventId.ConnectionPoolCreated.ShouldBe(10805);
	}

	[Fact]
	public void HaveConnectionAcquiredInPoolingRange()
	{
		CoreEventId.ConnectionAcquired.ShouldBe(10806);
	}

	[Fact]
	public void HaveConnectionReturnedInPoolingRange()
	{
		CoreEventId.ConnectionReturned.ShouldBe(10807);
	}

	[Fact]
	public void HaveConnectionPoolInitializedInPoolingRange()
	{
		CoreEventId.ConnectionPoolInitialized.ShouldBe(10808);
	}

	[Fact]
	public void HaveConnectionAcquisitionFailedInPoolingRange()
	{
		CoreEventId.ConnectionAcquisitionFailed.ShouldBe(10809);
	}

	[Fact]
	public void HaveConnectionReturnedToPoolInPoolingRange()
	{
		CoreEventId.ConnectionReturnedToPool.ShouldBe(10810);
	}

	[Fact]
	public void HaveConnectionReturnErrorInPoolingRange()
	{
		CoreEventId.ConnectionReturnError.ShouldBe(10811);
	}

	[Fact]
	public void HaveConnectionHealthCheckFailedInPoolingRange()
	{
		CoreEventId.ConnectionHealthCheckFailed.ShouldBe(10812);
	}

	[Fact]
	public void HaveWarmingUpPoolInPoolingRange()
	{
		CoreEventId.WarmingUpPool.ShouldBe(10813);
	}

	[Fact]
	public void HavePoolWarmUpCompletedInPoolingRange()
	{
		CoreEventId.PoolWarmUpCompleted.ShouldBe(10814);
	}

	[Fact]
	public void HaveCleanupRemovedConnectionsInPoolingRange()
	{
		CoreEventId.CleanupRemovedConnections.ShouldBe(10815);
	}

	[Fact]
	public void HaveResizingPoolInPoolingRange()
	{
		CoreEventId.ResizingPool.ShouldBe(10816);
	}

	[Fact]
	public void HaveDisposingPoolInPoolingRange()
	{
		CoreEventId.DisposingPool.ShouldBe(10817);
	}

	[Fact]
	public void HaveConnectionDisposalErrorInPoolingRange()
	{
		CoreEventId.ConnectionDisposalError.ShouldBe(10818);
	}

	[Fact]
	public void HavePoolDisposedSuccessfullyInPoolingRange()
	{
		CoreEventId.PoolDisposedSuccessfully.ShouldBe(10819);
	}

	[Fact]
	public void HaveWarmUpConnectionFailedInPoolingRange()
	{
		CoreEventId.WarmUpConnectionFailed.ShouldBe(10820);
	}

	[Fact]
	public void HaveConnectionDisposedFromPoolInPoolingRange()
	{
		CoreEventId.ConnectionDisposedFromPool.ShouldBe(10821);
	}

	[Fact]
	public void HaveConnectionDisposalErrorFromPoolInPoolingRange()
	{
		CoreEventId.ConnectionDisposalErrorFromPool.ShouldBe(10822);
	}

	[Fact]
	public void HaveHealthCheckFailedCallbackInPoolingRange()
	{
		CoreEventId.HealthCheckFailedCallback.ShouldBe(10823);
	}

	[Fact]
	public void HaveCleanupFailedCallbackInPoolingRange()
	{
		CoreEventId.CleanupFailedCallback.ShouldBe(10824);
	}

	[Fact]
	public void HaveBufferPoolingDisabledInPoolingRange()
	{
		CoreEventId.BufferPoolingDisabled.ShouldBe(10830);
	}

	[Fact]
	public void HavePoolTrimmedInPoolingRange()
	{
		CoreEventId.PoolTrimmed.ShouldBe(10831);
	}

	[Fact]
	public void HavePoolTrimErrorInPoolingRange()
	{
		CoreEventId.PoolTrimError.ShouldBe(10832);
	}

	[Fact]
	public void HavePoolRegisteredForManagementInPoolingRange()
	{
		CoreEventId.PoolRegisteredForManagement.ShouldBe(10833);
	}

	[Fact]
	public void HavePoolConfigurationChangedInPoolingRange()
	{
		CoreEventId.PoolConfigurationChanged.ShouldBe(10834);
	}

	[Fact]
	public void HavePoolAdaptedInPoolingRange()
	{
		CoreEventId.PoolAdapted.ShouldBe(10835);
	}

	[Fact]
	public void HavePoolAdaptationErrorInPoolingRange()
	{
		CoreEventId.PoolAdaptationError.ShouldBe(10836);
	}

	[Fact]
	public void HavePoolAdaptationGeneralErrorInPoolingRange()
	{
		CoreEventId.PoolAdaptationGeneralError.ShouldBe(10837);
	}

	[Fact]
	public void HaveMemoryPressureDetectedInPoolingRange()
	{
		CoreEventId.MemoryPressureDetected.ShouldBe(10838);
	}

	[Fact]
	public void HaveMemoryPressureRelievedInPoolingRange()
	{
		CoreEventId.MemoryPressureRelieved.ShouldBe(10839);
	}

	[Fact]
	public void HaveMemoryPressureCheckErrorInPoolingRange()
	{
		CoreEventId.MemoryPressureCheckError.ShouldBe(10840);
	}

	[Fact]
	public void HavePoolCreatedWithCapacityInPoolingRange()
	{
		CoreEventId.PoolCreatedWithCapacity.ShouldBe(10841);
	}

	[Fact]
	public void HavePoolManagerInitializedInPoolingRange()
	{
		CoreEventId.PoolManagerInitialized.ShouldBe(10842);
	}

	[Fact]
	public void HaveObjectNotRentedFromPoolInPoolingRange()
	{
		CoreEventId.ObjectNotRentedFromPool.ShouldBe(10850);
	}

	[Fact]
	public void HavePoolDisposedStatisticsInPoolingRange()
	{
		CoreEventId.PoolDisposedStatistics.ShouldBe(10851);
	}

	[Fact]
	public void HaveObjectLeakOnDisposalInPoolingRange()
	{
		CoreEventId.ObjectLeakOnDisposal.ShouldBe(10852);
	}

	[Fact]
	public void HavePotentialObjectLeakDetectedInPoolingRange()
	{
		CoreEventId.PotentialObjectLeakDetected.ShouldBe(10853);
	}

	[Fact]
	public void HaveArrayNotRentedFromPoolInPoolingRange()
	{
		CoreEventId.ArrayNotRentedFromPool.ShouldBe(10854);
	}

	[Fact]
	public void HavePotentialArrayLeakInPoolingRange()
	{
		CoreEventId.PotentialArrayLeak.ShouldBe(10855);
	}

	[Fact]
	public void HavePoolHealthReportInPoolingRange()
	{
		CoreEventId.PoolHealthReport.ShouldBe(10860);
	}

	#endregion Object Pooling Event IDs (10800-10899)

	#region Threading/Background Tasks Event IDs (10900-10999)

	[Fact]
	public void HaveBackgroundTaskStartedInThreadingRange()
	{
		CoreEventId.BackgroundTaskStarted.ShouldBe(10900);
	}

	[Fact]
	public void HaveBackgroundTaskCompletedInThreadingRange()
	{
		CoreEventId.BackgroundTaskCompleted.ShouldBe(10901);
	}

	[Fact]
	public void HaveBackgroundTaskFailedInThreadingRange()
	{
		CoreEventId.BackgroundTaskFailed.ShouldBe(10902);
	}

	[Fact]
	public void HaveBackgroundTaskCancelledInThreadingRange()
	{
		CoreEventId.BackgroundTaskCancelled.ShouldBe(10903);
	}

	[Fact]
	public void HaveThreadPoolTaskScheduledInThreadingRange()
	{
		CoreEventId.ThreadPoolTaskScheduled.ShouldBe(10904);
	}

	[Fact]
	public void HaveDedicatedThreadStartedInThreadingRange()
	{
		CoreEventId.DedicatedThreadStarted.ShouldBe(10905);
	}

	[Fact]
	public void HaveDedicatedThreadStoppedInThreadingRange()
	{
		CoreEventId.DedicatedThreadStopped.ShouldBe(10906);
	}

	[Fact]
	public void HaveDedicatedProcessorStartedInThreadingRange()
	{
		CoreEventId.DedicatedProcessorStarted.ShouldBe(10907);
	}

	[Fact]
	public void HaveDedicatedProcessorStoppedInThreadingRange()
	{
		CoreEventId.DedicatedProcessorStopped.ShouldBe(10908);
	}

	[Fact]
	public void HaveDedicatedProcessorErrorInThreadingRange()
	{
		CoreEventId.DedicatedProcessorError.ShouldBe(10909);
	}

	[Fact]
	public void HaveDedicatedProcessorFatalErrorInThreadingRange()
	{
		CoreEventId.DedicatedProcessorFatalError.ShouldBe(10910);
	}

	[Fact]
	public void HaveUnhandledBackgroundExceptionInThreadingRange()
	{
		CoreEventId.UnhandledBackgroundException.ShouldBe(10911);
	}

	[Fact]
	public void HaveBackgroundExecutionInvalidInThreadingRange()
	{
		CoreEventId.BackgroundExecutionInvalid.ShouldBe(10912);
	}

	[Fact]
	public void HaveBackgroundExecutionFailedInThreadingRange()
	{
		CoreEventId.BackgroundExecutionFailed.ShouldBe(10913);
	}

	[Fact]
	public void HaveBackgroundExecutionCriticalInThreadingRange()
	{
		CoreEventId.BackgroundExecutionCritical.ShouldBe(10914);
	}

	[Fact]
	public void HaveBackgroundExceptionNotPropagatedInThreadingRange()
	{
		CoreEventId.BackgroundExceptionNotPropagated.ShouldBe(10915);
	}

	#endregion Threading/Background Tasks Event IDs (10900-10999)

	#region Event ID Range Validation

	[Fact]
	public void HaveAllInfrastructureEventIdsInExpectedRange()
	{
		CoreEventId.DispatcherStarting.ShouldBeInRange(10000, 10099);
		CoreEventId.DispatcherStarted.ShouldBeInRange(10000, 10099);
		CoreEventId.DispatcherStopping.ShouldBeInRange(10000, 10099);
		CoreEventId.DispatcherStopped.ShouldBeInRange(10000, 10099);
		CoreEventId.PipelineConfigured.ShouldBeInRange(10000, 10099);
		CoreEventId.SynthesisWarning.ShouldBeInRange(10000, 10099);
	}

	[Fact]
	public void HaveAllMessageBusEventIdsInExpectedRange()
	{
		CoreEventId.MessageBusConnected.ShouldBeInRange(10100, 10199);
		CoreEventId.MessageBusDisconnected.ShouldBeInRange(10100, 10199);
		CoreEventId.MessagePublished.ShouldBeInRange(10100, 10199);
		CoreEventId.MessageReceived.ShouldBeInRange(10100, 10199);
		CoreEventId.FailedToPublishMessage.ShouldBeInRange(10100, 10199);
	}

	[Fact]
	public void HaveAllRoutingEventIdsInExpectedRange()
	{
		CoreEventId.RouteResolved.ShouldBeInRange(10200, 10299);
		CoreEventId.NoRouteFound.ShouldBeInRange(10200, 10299);
		CoreEventId.RoutingToHandler.ShouldBeInRange(10200, 10299);
		CoreEventId.HandlerRouteRegistered.ShouldBeInRange(10200, 10299);
	}

	[Fact]
	public void HaveAllProcessingEventIdsInExpectedRange()
	{
		CoreEventId.DispatchingMessage.ShouldBeInRange(10300, 10399);
		CoreEventId.HandlerExecuted.ShouldBeInRange(10300, 10399);
		CoreEventId.HandlerFailed.ShouldBeInRange(10300, 10399);
		CoreEventId.DispatchHandlerFailed.ShouldBeInRange(10300, 10399);
	}

	[Fact]
	public void HaveAllChannelEventIdsInExpectedRange()
	{
		CoreEventId.ChannelCreated.ShouldBeInRange(10400, 10499);
		CoreEventId.ChannelClosed.ShouldBeInRange(10400, 10499);
		CoreEventId.MessagePumpStarting.ShouldBeInRange(10400, 10499);
		CoreEventId.MessagePumpError.ShouldBeInRange(10400, 10499);
	}

	[Fact]
	public void HaveAllCircuitBreakerEventIdsInExpectedRange()
	{
		CoreEventId.CircuitBreakerCreated.ShouldBeInRange(10500, 10599);
		CoreEventId.CircuitBreakerRemoved.ShouldBeInRange(10500, 10599);
		CoreEventId.CircuitBreakerOpenExecutingFallback.ShouldBeInRange(10500, 10599);
		CoreEventId.CircuitBreakerStopError.ShouldBeInRange(10500, 10599);
	}

	[Fact]
	public void HaveAllCloudEventsEventIdsInExpectedRange()
	{
		CoreEventId.CloudEventReceived.ShouldBeInRange(10600, 10699);
		CoreEventId.CloudEventProcessed.ShouldBeInRange(10600, 10699);
		CoreEventId.CloudEventWithoutType.ShouldBeInRange(10600, 10699);
		CoreEventId.SchemaValidated.ShouldBeInRange(10600, 10699);
	}

	[Fact]
	public void HaveAllBatchProcessingEventIdsInExpectedRange()
	{
		CoreEventId.MicroBatchStarted.ShouldBeInRange(10700, 10799);
		CoreEventId.MicroBatchCompleted.ShouldBeInRange(10700, 10799);
		CoreEventId.BackpressureDetected.ShouldBeInRange(10700, 10799);
		CoreEventId.BatchFlushError.ShouldBeInRange(10700, 10799);
	}

	[Fact]
	public void HaveAllPoolingEventIdsInExpectedRange()
	{
		CoreEventId.PoolCreated.ShouldBeInRange(10800, 10899);
		CoreEventId.ObjectAcquired.ShouldBeInRange(10800, 10899);
		CoreEventId.PoolHealthReport.ShouldBeInRange(10800, 10899);
	}

	[Fact]
	public void HaveAllThreadingEventIdsInExpectedRange()
	{
		CoreEventId.BackgroundTaskStarted.ShouldBeInRange(10900, 10999);
		CoreEventId.BackgroundTaskCompleted.ShouldBeInRange(10900, 10999);
		CoreEventId.BackgroundExceptionNotPropagated.ShouldBeInRange(10900, 10999);
	}

	#endregion Event ID Range Validation

	#region Event ID Uniqueness

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllCoreEventIds();

		allEventIds.Distinct().Count().ShouldBe(allEventIds.Length,
			"All CoreEventId constants should have unique values");
	}

	[Fact]
	public void HaveCorrectTotalNumberOfEventIds()
	{
		var allEventIds = GetAllCoreEventIds();

		// CoreEventId has a large number of event IDs, verify count
		allEventIds.Length.ShouldBeGreaterThan(100);
	}

	#endregion Event ID Uniqueness

	#region Helper Methods

	private static int[] GetAllCoreEventIds()
	{
		return
		[
			// Infrastructure
			CoreEventId.DispatcherStarting,
			CoreEventId.DispatcherStarted,
			CoreEventId.DispatcherStopping,
			CoreEventId.DispatcherStopped,
			CoreEventId.PipelineConfigured,
			CoreEventId.ProfileSynthesized,
			CoreEventId.SynthesisBeginning,
			CoreEventId.SynthesizingDefaultProfile,
			CoreEventId.MiddlewareIncluded,
			CoreEventId.MiddlewareOmitted,
			CoreEventId.SynthesisComplete,
			CoreEventId.OmittedMiddlewareWarning,
			CoreEventId.SynthesisSuccess,
			CoreEventId.MappedMessageKinds,
			CoreEventId.SynthesisError,
			CoreEventId.SynthesisResult,
			CoreEventId.ProfileHandlesKinds,
			CoreEventId.SynthesisWarning,

			// Message Bus
			CoreEventId.MessageBusConnected,
			CoreEventId.MessageBusDisconnected,
			CoreEventId.MessagePublished,
			CoreEventId.MessageReceived,
			CoreEventId.Subscribed,
			CoreEventId.Unsubscribed,
			CoreEventId.NoMessageBusFound,
			CoreEventId.MessageBusInitializing,
			CoreEventId.PublishingMessage,
			CoreEventId.FailedToPublishMessage,

			// Routing
			CoreEventId.RouteResolved,
			CoreEventId.NoRouteFound,
			CoreEventId.RoutingToHandler,
			CoreEventId.HandlerRouteRegistered,
			CoreEventId.RouteEvaluationStarted,
			CoreEventId.RouteEvaluationCompleted,

			// Processing
			CoreEventId.DispatchingMessage,
			CoreEventId.HandlerExecuted,
			CoreEventId.HandlerFailed,
			CoreEventId.DispatchHandlerFailed,
			CoreEventId.UnhandledExceptionDuringDispatch,
			CoreEventId.CacheHitCheck,

			// Channels
			CoreEventId.ChannelCreated,
			CoreEventId.ChannelClosed,
			CoreEventId.MessagePumpStarting,
			CoreEventId.MessagePumpStopping,
			CoreEventId.ChannelMessageReceived,
			CoreEventId.ChannelMessageProcessed,
			CoreEventId.MessagePumpStarted,
			CoreEventId.MessagePumpStopped,
			CoreEventId.ProducerFailed,
			CoreEventId.ProducerTimeout,
			CoreEventId.MessageAcknowledged,
			CoreEventId.MessageRejected,
			CoreEventId.BatchProduced,
			CoreEventId.ChannelFull,
			CoreEventId.MessageProcessingError,
			CoreEventId.MessagePumpError,

			// Circuit Breaker
			CoreEventId.CircuitBreakerCreated,
			CoreEventId.CircuitBreakerRemoved,
			CoreEventId.CircuitBreakerInitializing,
			CoreEventId.CircuitBreakerStarting,
			CoreEventId.CircuitBreakerStopping,
			CoreEventId.CircuitBreakerOpenExecutingFallback,
			CoreEventId.OperationFailed,
			CoreEventId.CircuitBreakerReset,
			CoreEventId.CircuitBreakerOpenTransition,
			CoreEventId.CircuitBreakerHalfOpenTransition,
			CoreEventId.CircuitBreakerClosedTransition,
			CoreEventId.ObserverNotificationError,
			CoreEventId.ObserverSubscribed,
			CoreEventId.ObserverUnsubscribed,
			CoreEventId.CircuitBreakerStopError,

			// CloudEvents
			CoreEventId.CloudEventReceived,
			CoreEventId.CloudEventProcessed,
			CoreEventId.CloudEventWithoutType,
			CoreEventId.SchemaNotFound,
			CoreEventId.SchemaValidated,

			// High Performance
			CoreEventId.MicroBatchStarted,
			CoreEventId.MicroBatchCompleted,
			CoreEventId.BackpressureDetected,
			CoreEventId.BackpressureRelieved,
			CoreEventId.BatchProcessingStarted,
			CoreEventId.BatchProcessingCompleted,
			CoreEventId.MicroBatchError,
			CoreEventId.BatchProcessingError,
			CoreEventId.BatchFlushError,

			// Pooling
			CoreEventId.PoolCreated,
			CoreEventId.ObjectAcquired,
			CoreEventId.ObjectReturned,
			CoreEventId.PoolLeakDetected,
			CoreEventId.PoolExhausted,
			CoreEventId.ConnectionPoolCreated,
			CoreEventId.ConnectionAcquired,
			CoreEventId.ConnectionReturned,
			CoreEventId.ConnectionPoolInitialized,
			CoreEventId.ConnectionAcquisitionFailed,
			CoreEventId.ConnectionReturnedToPool,
			CoreEventId.ConnectionReturnError,
			CoreEventId.ConnectionHealthCheckFailed,
			CoreEventId.WarmingUpPool,
			CoreEventId.PoolWarmUpCompleted,
			CoreEventId.CleanupRemovedConnections,
			CoreEventId.ResizingPool,
			CoreEventId.DisposingPool,
			CoreEventId.ConnectionDisposalError,
			CoreEventId.PoolDisposedSuccessfully,
			CoreEventId.WarmUpConnectionFailed,
			CoreEventId.ConnectionDisposedFromPool,
			CoreEventId.ConnectionDisposalErrorFromPool,
			CoreEventId.HealthCheckFailedCallback,
			CoreEventId.CleanupFailedCallback,
			CoreEventId.BufferPoolingDisabled,
			CoreEventId.PoolTrimmed,
			CoreEventId.PoolTrimError,
			CoreEventId.PoolRegisteredForManagement,
			CoreEventId.PoolConfigurationChanged,
			CoreEventId.PoolAdapted,
			CoreEventId.PoolAdaptationError,
			CoreEventId.PoolAdaptationGeneralError,
			CoreEventId.MemoryPressureDetected,
			CoreEventId.MemoryPressureRelieved,
			CoreEventId.MemoryPressureCheckError,
			CoreEventId.PoolCreatedWithCapacity,
			CoreEventId.PoolManagerInitialized,
			CoreEventId.ObjectNotRentedFromPool,
			CoreEventId.PoolDisposedStatistics,
			CoreEventId.ObjectLeakOnDisposal,
			CoreEventId.PotentialObjectLeakDetected,
			CoreEventId.ArrayNotRentedFromPool,
			CoreEventId.PotentialArrayLeak,
			CoreEventId.PoolHealthReport,

			// Threading
			CoreEventId.BackgroundTaskStarted,
			CoreEventId.BackgroundTaskCompleted,
			CoreEventId.BackgroundTaskFailed,
			CoreEventId.BackgroundTaskCancelled,
			CoreEventId.ThreadPoolTaskScheduled,
			CoreEventId.DedicatedThreadStarted,
			CoreEventId.DedicatedThreadStopped,
			CoreEventId.DedicatedProcessorStarted,
			CoreEventId.DedicatedProcessorStopped,
			CoreEventId.DedicatedProcessorError,
			CoreEventId.DedicatedProcessorFatalError,
			CoreEventId.UnhandledBackgroundException,
			CoreEventId.BackgroundExecutionInvalid,
			CoreEventId.BackgroundExecutionFailed,
			CoreEventId.BackgroundExecutionCritical,
			CoreEventId.BackgroundExceptionNotPropagated
		];
	}

	#endregion Helper Methods
}
