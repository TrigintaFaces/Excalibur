// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Diagnostics;

namespace Excalibur.Saga.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="SagaEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Priority", "0")]
public sealed class SagaEventIdShould
{
	#region Saga Core Event IDs (120000-120099)

	[Fact]
	public void HaveSagaManagerCreatedInCoreRange()
	{
		SagaEventId.SagaManagerCreated.ShouldBe(120000);
	}

	[Fact]
	public void HaveSagaStartedInCoreRange()
	{
		SagaEventId.SagaStarted.ShouldBe(120001);
	}

	[Fact]
	public void HaveSagaCompletedInCoreRange()
	{
		SagaEventId.SagaCompleted.ShouldBe(120002);
	}

	[Fact]
	public void HaveSagaFailedInCoreRange()
	{
		SagaEventId.SagaFailed.ShouldBe(120003);
	}

	[Fact]
	public void HaveSagaCompensatingInCoreRange()
	{
		SagaEventId.SagaCompensating.ShouldBe(120004);
	}

	[Fact]
	public void HaveSagaCompensatedInCoreRange()
	{
		SagaEventId.SagaCompensated.ShouldBe(120005);
	}

	#endregion

	#region Saga Lifecycle Event IDs (120100-120199)

	[Fact]
	public void HaveSagaStepStartedInLifecycleRange()
	{
		SagaEventId.SagaStepStarted.ShouldBe(120100);
	}

	[Fact]
	public void HaveSagaStepCompletedInLifecycleRange()
	{
		SagaEventId.SagaStepCompleted.ShouldBe(120101);
	}

	[Fact]
	public void HaveSagaStepFailedInLifecycleRange()
	{
		SagaEventId.SagaStepFailed.ShouldBe(120102);
	}

	[Fact]
	public void HaveSagaStepCompensatingInLifecycleRange()
	{
		SagaEventId.SagaStepCompensating.ShouldBe(120103);
	}

	[Fact]
	public void HaveSagaStepCompensatedInLifecycleRange()
	{
		SagaEventId.SagaStepCompensated.ShouldBe(120104);
	}

	[Fact]
	public void HaveSagaStepSkippedInLifecycleRange()
	{
		SagaEventId.SagaStepSkipped.ShouldBe(120105);
	}

	#endregion

	#region Conditional Saga Steps Event IDs (120200-120299)

	[Fact]
	public void HaveConditionEvaluationStartedInConditionalRange()
	{
		SagaEventId.ConditionEvaluationStarted.ShouldBe(120200);
	}

	[Fact]
	public void HaveConditionEvaluationCompletedInConditionalRange()
	{
		SagaEventId.ConditionEvaluationCompleted.ShouldBe(120201);
	}

	[Fact]
	public void HaveConditionEvaluationErrorInConditionalRange()
	{
		SagaEventId.ConditionEvaluationError.ShouldBe(120202);
	}

	[Fact]
	public void HaveConditionalStepExecutionStartedInConditionalRange()
	{
		SagaEventId.ConditionalStepExecutionStarted.ShouldBe(120203);
	}

	[Fact]
	public void HaveNoStepToExecuteInConditionalRange()
	{
		SagaEventId.NoStepToExecute.ShouldBe(120204);
	}

	[Fact]
	public void HaveBranchExecutionInConditionalRange()
	{
		SagaEventId.BranchExecution.ShouldBe(120205);
	}

	[Fact]
	public void HaveConditionalStepExecutionFailedInConditionalRange()
	{
		SagaEventId.ConditionalStepExecutionFailed.ShouldBe(120206);
	}

	[Fact]
	public void HaveConditionalStepCompensationStartedInConditionalRange()
	{
		SagaEventId.ConditionalStepCompensationStarted.ShouldBe(120207);
	}

	[Fact]
	public void HaveNoStepExecutedSkippingCompensationInConditionalRange()
	{
		SagaEventId.NoStepExecutedSkippingCompensation.ShouldBe(120208);
	}

	[Fact]
	public void HaveExecutedStepCannotBeCompensatedInConditionalRange()
	{
		SagaEventId.ExecutedStepCannotBeCompensated.ShouldBe(120209);
	}

	[Fact]
	public void HaveCompensatingExecutedStepInConditionalRange()
	{
		SagaEventId.CompensatingExecutedStep.ShouldBe(120210);
	}

	[Fact]
	public void HaveConditionalStepCompensationFailedInConditionalRange()
	{
		SagaEventId.ConditionalStepCompensationFailed.ShouldBe(120211);
	}

	#endregion

	#region Parallel Saga Steps Event IDs (120300-120399)

	[Fact]
	public void HaveParallelStepExecutionStartedInParallelRange()
	{
		SagaEventId.ParallelStepExecutionStarted.ShouldBe(120300);
	}

	[Fact]
	public void HaveStartingParallelStepExecutionInParallelRange()
	{
		SagaEventId.StartingParallelStepExecution.ShouldBe(120301);
	}

	[Fact]
	public void HaveParallelStepCompletedInParallelRange()
	{
		SagaEventId.ParallelStepCompleted.ShouldBe(120302);
	}

	[Fact]
	public void HaveParallelStepFailedInParallelRange()
	{
		SagaEventId.ParallelStepFailed.ShouldBe(120303);
	}

	[Fact]
	public void HaveParallelExecutionCompletedInParallelRange()
	{
		SagaEventId.ParallelExecutionCompleted.ShouldBe(120304);
	}

	[Fact]
	public void HaveParallelExecutionFailedInParallelRange()
	{
		SagaEventId.ParallelExecutionFailed.ShouldBe(120305);
	}

	[Fact]
	public void HaveParallelStepCompensationStartedInParallelRange()
	{
		SagaEventId.ParallelStepCompensationStarted.ShouldBe(120306);
	}

	[Fact]
	public void HaveCompensatingParallelStepInParallelRange()
	{
		SagaEventId.CompensatingParallelStep.ShouldBe(120307);
	}

	[Fact]
	public void HaveParallelCompensationCompletedInParallelRange()
	{
		SagaEventId.ParallelCompensationCompleted.ShouldBe(120308);
	}

	[Fact]
	public void HaveParallelCompensationFailedInParallelRange()
	{
		SagaEventId.ParallelCompensationFailed.ShouldBe(120309);
	}

	#endregion

	#region Saga Base/Middleware Event IDs (120400-120499)

	[Fact]
	public void HaveSagaHandlingStartedInBaseRange()
	{
		SagaEventId.SagaHandlingStarted.ShouldBe(120400);
	}

	[Fact]
	public void HaveSagaHandlingCompletedInBaseRange()
	{
		SagaEventId.SagaHandlingCompleted.ShouldBe(120401);
	}

	[Fact]
	public void HaveSagaHandlingFailedInBaseRange()
	{
		SagaEventId.SagaHandlingFailed.ShouldBe(120402);
	}

	[Fact]
	public void HaveSagaInitializationStartedInBaseRange()
	{
		SagaEventId.SagaInitializationStarted.ShouldBe(120403);
	}

	[Fact]
	public void HaveSagaMiddlewareProcessingInBaseRange()
	{
		SagaEventId.SagaMiddlewareProcessing.ShouldBe(120404);
	}

	#endregion

	#region Multi-Conditional Saga Steps Event IDs (120500-120599)

	[Fact]
	public void HaveMultiCondEvaluatingBranchInMultiCondRange()
	{
		SagaEventId.MultiCondEvaluatingBranch.ShouldBe(120500);
	}

	[Fact]
	public void HaveMultiCondBranchEvaluatedInMultiCondRange()
	{
		SagaEventId.MultiCondBranchEvaluated.ShouldBe(120501);
	}

	[Fact]
	public void HaveMultiCondBranchEvaluationErrorInMultiCondRange()
	{
		SagaEventId.MultiCondBranchEvaluationError.ShouldBe(120502);
	}

	[Fact]
	public void HaveMultiCondExecutingStepInMultiCondRange()
	{
		SagaEventId.MultiCondExecutingStep.ShouldBe(120503);
	}

	[Fact]
	public void HaveMultiCondExecutingBranchInMultiCondRange()
	{
		SagaEventId.MultiCondExecutingBranch.ShouldBe(120504);
	}

	[Fact]
	public void HaveMultiCondExecutingDefaultStepInMultiCondRange()
	{
		SagaEventId.MultiCondExecutingDefaultStep.ShouldBe(120505);
	}

	[Fact]
	public void HaveMultiCondNoBranchFoundInMultiCondRange()
	{
		SagaEventId.MultiCondNoBranchFound.ShouldBe(120506);
	}

	[Fact]
	public void HaveMultiCondStepFailedInMultiCondRange()
	{
		SagaEventId.MultiCondStepFailed.ShouldBe(120507);
	}

	[Fact]
	public void HaveMultiCondCompensatingStepInMultiCondRange()
	{
		SagaEventId.MultiCondCompensatingStep.ShouldBe(120508);
	}

	[Fact]
	public void HaveMultiCondSkippingCompensationInMultiCondRange()
	{
		SagaEventId.MultiCondSkippingCompensation.ShouldBe(120509);
	}

	[Fact]
	public void HaveMultiCondStepCannotCompensateInMultiCondRange()
	{
		SagaEventId.MultiCondStepCannotCompensate.ShouldBe(120510);
	}

	[Fact]
	public void HaveMultiCondCompensatingExecutedStepInMultiCondRange()
	{
		SagaEventId.MultiCondCompensatingExecutedStep.ShouldBe(120511);
	}

	[Fact]
	public void HaveMultiCondCompensationFailedInMultiCondRange()
	{
		SagaEventId.MultiCondCompensationFailed.ShouldBe(120512);
	}

	#endregion

	#region Saga State Management Event IDs (121000-121099)

	[Fact]
	public void HaveSagaStateLoadedInStateRange()
	{
		SagaEventId.SagaStateLoaded.ShouldBe(121000);
	}

	[Fact]
	public void HaveSagaStateSavedInStateRange()
	{
		SagaEventId.SagaStateSaved.ShouldBe(121001);
	}

	[Fact]
	public void HaveSagaStateTransitionedInStateRange()
	{
		SagaEventId.SagaStateTransitioned.ShouldBe(121002);
	}

	[Fact]
	public void HaveSagaStateNotFoundInStateRange()
	{
		SagaEventId.SagaStateNotFound.ShouldBe(121003);
	}

	[Fact]
	public void HaveSagaStateDeletedInStateRange()
	{
		SagaEventId.SagaStateDeleted.ShouldBe(121004);
	}

	#endregion

	#region Saga Message Handling Event IDs (121100-121199)

	[Fact]
	public void HaveSagaMessageReceivedInMessageRange()
	{
		SagaEventId.SagaMessageReceived.ShouldBe(121100);
	}

	[Fact]
	public void HaveSagaMessageHandledInMessageRange()
	{
		SagaEventId.SagaMessageHandled.ShouldBe(121101);
	}

	[Fact]
	public void HaveSagaMessageDeferredInMessageRange()
	{
		SagaEventId.SagaMessageDeferred.ShouldBe(121102);
	}

	[Fact]
	public void HaveSagaTimeoutScheduledInMessageRange()
	{
		SagaEventId.SagaTimeoutScheduled.ShouldBe(121103);
	}

	[Fact]
	public void HaveSagaTimeoutTriggeredInMessageRange()
	{
		SagaEventId.SagaTimeoutTriggered.ShouldBe(121104);
	}

	#endregion

	#region Saga Timeout Delivery Event IDs (121200-121299)

	[Fact]
	public void HaveTimeoutDeliveryStartedInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutDeliveryStarted.ShouldBe(121200);
	}

	[Fact]
	public void HaveTimeoutProcessingStartedInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutProcessingStarted.ShouldBe(121201);
	}

	[Fact]
	public void HaveTimeoutDeliveredSuccessfullyInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutDeliveredSuccessfully.ShouldBe(121202);
	}

	[Fact]
	public void HaveTimeoutDeliveryFailedInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutDeliveryFailed.ShouldBe(121203);
	}

	[Fact]
	public void HaveTimeoutBatchCompletedInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutBatchCompleted.ShouldBe(121204);
	}

	[Fact]
	public void HaveTimeoutServiceStoppedInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutServiceStopped.ShouldBe(121205);
	}

	[Fact]
	public void HaveTimeoutTypeResolutionFailedInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutTypeResolutionFailed.ShouldBe(121206);
	}

	[Fact]
	public void HaveTimeoutMessageCreationFailedInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutMessageCreationFailed.ShouldBe(121207);
	}

	[Fact]
	public void HaveTimeoutMessageTypeInvalidInTimeoutDeliveryRange()
	{
		SagaEventId.TimeoutMessageTypeInvalid.ShouldBe(121208);
	}

	#endregion

	#region Saga Coordination Event IDs (122000-122099)

	[Fact]
	public void HaveSagaCoordinatorCreatedInCoordinationRange()
	{
		SagaEventId.SagaCoordinatorCreated.ShouldBe(122000);
	}

	[Fact]
	public void HaveSagaCommandDispatchedInCoordinationRange()
	{
		SagaEventId.SagaCommandDispatched.ShouldBe(122001);
	}

	[Fact]
	public void HaveSagaEventPublishedInCoordinationRange()
	{
		SagaEventId.SagaEventPublished.ShouldBe(122002);
	}

	[Fact]
	public void HaveSagaReplyReceivedInCoordinationRange()
	{
		SagaEventId.SagaReplyReceived.ShouldBe(122003);
	}

	[Fact]
	public void HaveSagaCorrelationEstablishedInCoordinationRange()
	{
		SagaEventId.SagaCorrelationEstablished.ShouldBe(122004);
	}

	#endregion

	#region Saga Recovery Event IDs (122100-122199)

	[Fact]
	public void HaveSagaRecoveryStartedInRecoveryRange()
	{
		SagaEventId.SagaRecoveryStarted.ShouldBe(122100);
	}

	[Fact]
	public void HaveSagaRecoveryCompletedInRecoveryRange()
	{
		SagaEventId.SagaRecoveryCompleted.ShouldBe(122101);
	}

	[Fact]
	public void HaveSagaRetryScheduledInRecoveryRange()
	{
		SagaEventId.SagaRetryScheduled.ShouldBe(122102);
	}

	[Fact]
	public void HaveSagaRetryExecutedInRecoveryRange()
	{
		SagaEventId.SagaRetryExecuted.ShouldBe(122103);
	}

	[Fact]
	public void HaveSagaDeadLetterStoredInRecoveryRange()
	{
		SagaEventId.SagaDeadLetterStored.ShouldBe(122104);
	}

	#endregion

	#region Saga Coordinator Event IDs (122200-122299)

	[Fact]
	public void HaveSagaExecutionStartingInCoordinatorRange()
	{
		SagaEventId.SagaExecutionStarting.ShouldBe(122200);
	}

	[Fact]
	public void HaveSagaStepExecutingInCoordinatorRange()
	{
		SagaEventId.SagaStepExecuting.ShouldBe(122201);
	}

	[Fact]
	public void HaveSagaExecutionCompletedSuccessfullyInCoordinatorRange()
	{
		SagaEventId.SagaExecutionCompletedSuccessfully.ShouldBe(122202);
	}

	[Fact]
	public void HaveSagaStepFailedStartingCompensationInCoordinatorRange()
	{
		SagaEventId.SagaStepFailedStartingCompensation.ShouldBe(122203);
	}

	[Fact]
	public void HaveSagaCompensationCompletedInCoordinatorRange()
	{
		SagaEventId.SagaCompensationCompleted.ShouldBe(122204);
	}

	[Fact]
	public void HaveSagaExecutionFailedInCoordinatorRange()
	{
		SagaEventId.SagaExecutionFailed.ShouldBe(122205);
	}

	#endregion

	#region Saga Storage Core Event IDs (123000-123099)

	[Fact]
	public void HaveSagaStoreCreatedInStorageCoreRange()
	{
		SagaEventId.SagaStoreCreated.ShouldBe(123000);
	}

	[Fact]
	public void HaveSagaPersistedInStorageCoreRange()
	{
		SagaEventId.SagaPersisted.ShouldBe(123001);
	}

	[Fact]
	public void HaveSagaLoadedFromStoreInStorageCoreRange()
	{
		SagaEventId.SagaLoadedFromStore.ShouldBe(123002);
	}

	[Fact]
	public void HaveSagaRemovedFromStoreInStorageCoreRange()
	{
		SagaEventId.SagaRemovedFromStore.ShouldBe(123003);
	}

	[Fact]
	public void HaveSagaStoreQueriedInStorageCoreRange()
	{
		SagaEventId.SagaStoreQueried.ShouldBe(123004);
	}

	#endregion

	#region Saga Storage Providers Event IDs (123100-123199)

	[Fact]
	public void HaveSqlServerSagaStoreCreatedInStorageProviderRange()
	{
		SagaEventId.SqlServerSagaStoreCreated.ShouldBe(123100);
	}

	[Fact]
	public void HaveInMemorySagaStoreCreatedInStorageProviderRange()
	{
		SagaEventId.InMemorySagaStoreCreated.ShouldBe(123101);
	}

	[Fact]
	public void HaveSagaConcurrencyConflictInStorageProviderRange()
	{
		SagaEventId.SagaConcurrencyConflict.ShouldBe(123102);
	}

	[Fact]
	public void HaveSagaStoreConnectionErrorInStorageProviderRange()
	{
		SagaEventId.SagaStoreConnectionError.ShouldBe(123103);
	}

	#endregion

	#region Saga SqlServer Timeout Store Event IDs (123200-123299)

	[Fact]
	public void HaveTimeoutScheduledInSqlServerTimeoutRange()
	{
		SagaEventId.TimeoutScheduled.ShouldBe(123200);
	}

	[Fact]
	public void HaveTimeoutCancelledInSqlServerTimeoutRange()
	{
		SagaEventId.TimeoutCancelled.ShouldBe(123201);
	}

	[Fact]
	public void HaveAllTimeoutsCancelledInSqlServerTimeoutRange()
	{
		SagaEventId.AllTimeoutsCancelled.ShouldBe(123202);
	}

	[Fact]
	public void HaveTimeoutMarkedDeliveredInSqlServerTimeoutRange()
	{
		SagaEventId.TimeoutMarkedDelivered.ShouldBe(123203);
	}

	#endregion

	#region Saga SqlServer Monitoring Event IDs (123300-123399)

	[Fact]
	public void HaveRunningCountRetrievedInMonitoringRange()
	{
		SagaEventId.RunningCountRetrieved.ShouldBe(123300);
	}

	[Fact]
	public void HaveCompletedCountRetrievedInMonitoringRange()
	{
		SagaEventId.CompletedCountRetrieved.ShouldBe(123301);
	}

	[Fact]
	public void HaveStuckSagasRetrievedInMonitoringRange()
	{
		SagaEventId.StuckSagasRetrieved.ShouldBe(123302);
	}

	[Fact]
	public void HaveFailedSagasRetrievedInMonitoringRange()
	{
		SagaEventId.FailedSagasRetrieved.ShouldBe(123303);
	}

	[Fact]
	public void HaveAverageCompletionTimeRetrievedInMonitoringRange()
	{
		SagaEventId.AverageCompletionTimeRetrieved.ShouldBe(123304);
	}

	#endregion

	#region Event ID Range Validation Tests

	[Theory]
	[InlineData(nameof(SagaEventId.SagaManagerCreated), 120000, 120099)]
	[InlineData(nameof(SagaEventId.SagaStarted), 120000, 120099)]
	[InlineData(nameof(SagaEventId.SagaCompleted), 120000, 120099)]
	[InlineData(nameof(SagaEventId.SagaFailed), 120000, 120099)]
	[InlineData(nameof(SagaEventId.SagaCompensating), 120000, 120099)]
	[InlineData(nameof(SagaEventId.SagaCompensated), 120000, 120099)]
	public void HaveCoreEventIdsInCoreRange(string eventName, int minRange, int maxRange)
	{
		var value = GetEventIdValue(eventName);
		value.ShouldBeInRange(minRange, maxRange);
	}

	[Theory]
	[InlineData(nameof(SagaEventId.SagaStepStarted), 120100, 120199)]
	[InlineData(nameof(SagaEventId.SagaStepCompleted), 120100, 120199)]
	[InlineData(nameof(SagaEventId.SagaStepFailed), 120100, 120199)]
	[InlineData(nameof(SagaEventId.SagaStepCompensating), 120100, 120199)]
	[InlineData(nameof(SagaEventId.SagaStepCompensated), 120100, 120199)]
	[InlineData(nameof(SagaEventId.SagaStepSkipped), 120100, 120199)]
	public void HaveLifecycleEventIdsInLifecycleRange(string eventName, int minRange, int maxRange)
	{
		var value = GetEventIdValue(eventName);
		value.ShouldBeInRange(minRange, maxRange);
	}

	[Theory]
	[InlineData(nameof(SagaEventId.SagaStateLoaded), 121000, 121099)]
	[InlineData(nameof(SagaEventId.SagaStateSaved), 121000, 121099)]
	[InlineData(nameof(SagaEventId.SagaStateTransitioned), 121000, 121099)]
	[InlineData(nameof(SagaEventId.SagaStateNotFound), 121000, 121099)]
	[InlineData(nameof(SagaEventId.SagaStateDeleted), 121000, 121099)]
	public void HaveStateEventIdsInStateRange(string eventName, int minRange, int maxRange)
	{
		var value = GetEventIdValue(eventName);
		value.ShouldBeInRange(minRange, maxRange);
	}

	[Theory]
	[InlineData(nameof(SagaEventId.SagaCoordinatorCreated), 122000, 122099)]
	[InlineData(nameof(SagaEventId.SagaCommandDispatched), 122000, 122099)]
	[InlineData(nameof(SagaEventId.SagaEventPublished), 122000, 122099)]
	[InlineData(nameof(SagaEventId.SagaReplyReceived), 122000, 122099)]
	[InlineData(nameof(SagaEventId.SagaCorrelationEstablished), 122000, 122099)]
	public void HaveCoordinationEventIdsInCoordinationRange(string eventName, int minRange, int maxRange)
	{
		var value = GetEventIdValue(eventName);
		value.ShouldBeInRange(minRange, maxRange);
	}

	[Theory]
	[InlineData(nameof(SagaEventId.SagaStoreCreated), 123000, 123099)]
	[InlineData(nameof(SagaEventId.SagaPersisted), 123000, 123099)]
	[InlineData(nameof(SagaEventId.SagaLoadedFromStore), 123000, 123099)]
	[InlineData(nameof(SagaEventId.SagaRemovedFromStore), 123000, 123099)]
	[InlineData(nameof(SagaEventId.SagaStoreQueried), 123000, 123099)]
	public void HaveStorageCoreEventIdsInStorageCoreRange(string eventName, int minRange, int maxRange)
	{
		var value = GetEventIdValue(eventName);
		value.ShouldBeInRange(minRange, maxRange);
	}

	#endregion

	#region Overall Range Validation

	[Fact]
	public void HaveAllEventIdsWithinSagaPackageRange()
	{
		// Saga package owns range 120000-123999
		var allEventIds = GetAllEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(120000, 123999,
				$"Event ID {eventId} is outside Saga package range (120000-123999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIdsForAllDefinedEventIds()
	{
		var allEventIds = GetAllEventIds();

		allEventIds.Distinct().Count().ShouldBe(
			allEventIds.Length,
			"All event IDs must be unique");
	}

	[Fact]
	public void HaveNoDuplicateCoreEventIds()
	{
		var coreEventIds = new[]
		{
			SagaEventId.SagaManagerCreated,
			SagaEventId.SagaStarted,
			SagaEventId.SagaCompleted,
			SagaEventId.SagaFailed,
			SagaEventId.SagaCompensating,
			SagaEventId.SagaCompensated
		};

		coreEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateLifecycleEventIds()
	{
		var lifecycleEventIds = new[]
		{
			SagaEventId.SagaStepStarted,
			SagaEventId.SagaStepCompleted,
			SagaEventId.SagaStepFailed,
			SagaEventId.SagaStepCompensating,
			SagaEventId.SagaStepCompensated,
			SagaEventId.SagaStepSkipped
		};

		lifecycleEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateConditionalStepEventIds()
	{
		var conditionalEventIds = new[]
		{
			SagaEventId.ConditionEvaluationStarted,
			SagaEventId.ConditionEvaluationCompleted,
			SagaEventId.ConditionEvaluationError,
			SagaEventId.ConditionalStepExecutionStarted,
			SagaEventId.NoStepToExecute,
			SagaEventId.BranchExecution,
			SagaEventId.ConditionalStepExecutionFailed,
			SagaEventId.ConditionalStepCompensationStarted,
			SagaEventId.NoStepExecutedSkippingCompensation,
			SagaEventId.ExecutedStepCannotBeCompensated,
			SagaEventId.CompensatingExecutedStep,
			SagaEventId.ConditionalStepCompensationFailed
		};

		conditionalEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateParallelStepEventIds()
	{
		var parallelEventIds = new[]
		{
			SagaEventId.ParallelStepExecutionStarted,
			SagaEventId.StartingParallelStepExecution,
			SagaEventId.ParallelStepCompleted,
			SagaEventId.ParallelStepFailed,
			SagaEventId.ParallelExecutionCompleted,
			SagaEventId.ParallelExecutionFailed,
			SagaEventId.ParallelStepCompensationStarted,
			SagaEventId.CompensatingParallelStep,
			SagaEventId.ParallelCompensationCompleted,
			SagaEventId.ParallelCompensationFailed
		};

		parallelEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateMultiCondEventIds()
	{
		var multiCondEventIds = new[]
		{
			SagaEventId.MultiCondEvaluatingBranch,
			SagaEventId.MultiCondBranchEvaluated,
			SagaEventId.MultiCondBranchEvaluationError,
			SagaEventId.MultiCondExecutingStep,
			SagaEventId.MultiCondExecutingBranch,
			SagaEventId.MultiCondExecutingDefaultStep,
			SagaEventId.MultiCondNoBranchFound,
			SagaEventId.MultiCondStepFailed,
			SagaEventId.MultiCondCompensatingStep,
			SagaEventId.MultiCondSkippingCompensation,
			SagaEventId.MultiCondStepCannotCompensate,
			SagaEventId.MultiCondCompensatingExecutedStep,
			SagaEventId.MultiCondCompensationFailed
		};

		multiCondEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateStateManagementEventIds()
	{
		var stateEventIds = new[]
		{
			SagaEventId.SagaStateLoaded,
			SagaEventId.SagaStateSaved,
			SagaEventId.SagaStateTransitioned,
			SagaEventId.SagaStateNotFound,
			SagaEventId.SagaStateDeleted
		};

		stateEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateMessageHandlingEventIds()
	{
		var messageEventIds = new[]
		{
			SagaEventId.SagaMessageReceived,
			SagaEventId.SagaMessageHandled,
			SagaEventId.SagaMessageDeferred,
			SagaEventId.SagaTimeoutScheduled,
			SagaEventId.SagaTimeoutTriggered
		};

		messageEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateTimeoutDeliveryEventIds()
	{
		var timeoutEventIds = new[]
		{
			SagaEventId.TimeoutDeliveryStarted,
			SagaEventId.TimeoutProcessingStarted,
			SagaEventId.TimeoutDeliveredSuccessfully,
			SagaEventId.TimeoutDeliveryFailed,
			SagaEventId.TimeoutBatchCompleted,
			SagaEventId.TimeoutServiceStopped,
			SagaEventId.TimeoutTypeResolutionFailed,
			SagaEventId.TimeoutMessageCreationFailed,
			SagaEventId.TimeoutMessageTypeInvalid
		};

		timeoutEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateStorageEventIds()
	{
		var storageEventIds = new[]
		{
			SagaEventId.SagaStoreCreated,
			SagaEventId.SagaPersisted,
			SagaEventId.SagaLoadedFromStore,
			SagaEventId.SagaRemovedFromStore,
			SagaEventId.SagaStoreQueried,
			SagaEventId.SqlServerSagaStoreCreated,
			SagaEventId.InMemorySagaStoreCreated,
			SagaEventId.SagaConcurrencyConflict,
			SagaEventId.SagaStoreConnectionError
		};

		storageEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateMonitoringEventIds()
	{
		var monitoringEventIds = new[]
		{
			SagaEventId.RunningCountRetrieved,
			SagaEventId.CompletedCountRetrieved,
			SagaEventId.StuckSagasRetrieved,
			SagaEventId.FailedSagasRetrieved,
			SagaEventId.AverageCompletionTimeRetrieved
		};

		monitoringEventIds.ShouldBeUnique();
	}

	#endregion

	#region Helper Methods

	private static int GetEventIdValue(string eventName)
	{
		var field = typeof(SagaEventId).GetField(eventName);
		return (int)field!.GetValue(null)!;
	}

	private static int[] GetAllEventIds()
	{
		return new[]
		{
			// Core (120000-120099)
			SagaEventId.SagaManagerCreated,
			SagaEventId.SagaStarted,
			SagaEventId.SagaCompleted,
			SagaEventId.SagaFailed,
			SagaEventId.SagaCompensating,
			SagaEventId.SagaCompensated,

			// Lifecycle (120100-120199)
			SagaEventId.SagaStepStarted,
			SagaEventId.SagaStepCompleted,
			SagaEventId.SagaStepFailed,
			SagaEventId.SagaStepCompensating,
			SagaEventId.SagaStepCompensated,
			SagaEventId.SagaStepSkipped,

			// Conditional (120200-120299)
			SagaEventId.ConditionEvaluationStarted,
			SagaEventId.ConditionEvaluationCompleted,
			SagaEventId.ConditionEvaluationError,
			SagaEventId.ConditionalStepExecutionStarted,
			SagaEventId.NoStepToExecute,
			SagaEventId.BranchExecution,
			SagaEventId.ConditionalStepExecutionFailed,
			SagaEventId.ConditionalStepCompensationStarted,
			SagaEventId.NoStepExecutedSkippingCompensation,
			SagaEventId.ExecutedStepCannotBeCompensated,
			SagaEventId.CompensatingExecutedStep,
			SagaEventId.ConditionalStepCompensationFailed,

			// Parallel (120300-120399)
			SagaEventId.ParallelStepExecutionStarted,
			SagaEventId.StartingParallelStepExecution,
			SagaEventId.ParallelStepCompleted,
			SagaEventId.ParallelStepFailed,
			SagaEventId.ParallelExecutionCompleted,
			SagaEventId.ParallelExecutionFailed,
			SagaEventId.ParallelStepCompensationStarted,
			SagaEventId.CompensatingParallelStep,
			SagaEventId.ParallelCompensationCompleted,
			SagaEventId.ParallelCompensationFailed,

			// Base/Middleware (120400-120499)
			SagaEventId.SagaHandlingStarted,
			SagaEventId.SagaHandlingCompleted,
			SagaEventId.SagaHandlingFailed,
			SagaEventId.SagaInitializationStarted,
			SagaEventId.SagaMiddlewareProcessing,

			// Multi-Conditional (120500-120599)
			SagaEventId.MultiCondEvaluatingBranch,
			SagaEventId.MultiCondBranchEvaluated,
			SagaEventId.MultiCondBranchEvaluationError,
			SagaEventId.MultiCondExecutingStep,
			SagaEventId.MultiCondExecutingBranch,
			SagaEventId.MultiCondExecutingDefaultStep,
			SagaEventId.MultiCondNoBranchFound,
			SagaEventId.MultiCondStepFailed,
			SagaEventId.MultiCondCompensatingStep,
			SagaEventId.MultiCondSkippingCompensation,
			SagaEventId.MultiCondStepCannotCompensate,
			SagaEventId.MultiCondCompensatingExecutedStep,
			SagaEventId.MultiCondCompensationFailed,

			// State Management (121000-121099)
			SagaEventId.SagaStateLoaded,
			SagaEventId.SagaStateSaved,
			SagaEventId.SagaStateTransitioned,
			SagaEventId.SagaStateNotFound,
			SagaEventId.SagaStateDeleted,

			// Message Handling (121100-121199)
			SagaEventId.SagaMessageReceived,
			SagaEventId.SagaMessageHandled,
			SagaEventId.SagaMessageDeferred,
			SagaEventId.SagaTimeoutScheduled,
			SagaEventId.SagaTimeoutTriggered,

			// Timeout Delivery (121200-121299)
			SagaEventId.TimeoutDeliveryStarted,
			SagaEventId.TimeoutProcessingStarted,
			SagaEventId.TimeoutDeliveredSuccessfully,
			SagaEventId.TimeoutDeliveryFailed,
			SagaEventId.TimeoutBatchCompleted,
			SagaEventId.TimeoutServiceStopped,
			SagaEventId.TimeoutTypeResolutionFailed,
			SagaEventId.TimeoutMessageCreationFailed,
			SagaEventId.TimeoutMessageTypeInvalid,

			// Coordination (122000-122099)
			SagaEventId.SagaCoordinatorCreated,
			SagaEventId.SagaCommandDispatched,
			SagaEventId.SagaEventPublished,
			SagaEventId.SagaReplyReceived,
			SagaEventId.SagaCorrelationEstablished,

			// Recovery (122100-122199)
			SagaEventId.SagaRecoveryStarted,
			SagaEventId.SagaRecoveryCompleted,
			SagaEventId.SagaRetryScheduled,
			SagaEventId.SagaRetryExecuted,
			SagaEventId.SagaDeadLetterStored,

			// Coordinator (122200-122299)
			SagaEventId.SagaExecutionStarting,
			SagaEventId.SagaStepExecuting,
			SagaEventId.SagaExecutionCompletedSuccessfully,
			SagaEventId.SagaStepFailedStartingCompensation,
			SagaEventId.SagaCompensationCompleted,
			SagaEventId.SagaExecutionFailed,

			// Storage Core (123000-123099)
			SagaEventId.SagaStoreCreated,
			SagaEventId.SagaPersisted,
			SagaEventId.SagaLoadedFromStore,
			SagaEventId.SagaRemovedFromStore,
			SagaEventId.SagaStoreQueried,

			// Storage Providers (123100-123199)
			SagaEventId.SqlServerSagaStoreCreated,
			SagaEventId.InMemorySagaStoreCreated,
			SagaEventId.SagaConcurrencyConflict,
			SagaEventId.SagaStoreConnectionError,

			// SqlServer Timeout Store (123200-123299)
			SagaEventId.TimeoutScheduled,
			SagaEventId.TimeoutCancelled,
			SagaEventId.AllTimeoutsCancelled,
			SagaEventId.TimeoutMarkedDelivered,

			// SqlServer Monitoring (123300-123399)
			SagaEventId.RunningCountRetrieved,
			SagaEventId.CompletedCountRetrieved,
			SagaEventId.StuckSagasRetrieved,
			SagaEventId.FailedSagasRetrieved,
			SagaEventId.AverageCompletionTimeRetrieved
		};
	}

	#endregion
}
