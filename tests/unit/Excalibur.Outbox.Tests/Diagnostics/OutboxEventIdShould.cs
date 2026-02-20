// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Diagnostics;

namespace Excalibur.Outbox.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="OutboxEventId"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxEventIdShould : UnitTestBase
{
	#region Outbox Core Event IDs (130000-130099)

	[Fact]
	public void HaveOutboxServiceCreatedInCoreRange()
	{
		// Assert
		OutboxEventId.OutboxServiceCreated.ShouldBe(130000);
	}

	[Fact]
	public void HaveOutboxMessageStoredInCoreRange()
	{
		// Assert
		OutboxEventId.OutboxMessageStored.ShouldBe(130001);
	}

	[Fact]
	public void HaveOutboxMessagePublishedInCoreRange()
	{
		// Assert
		OutboxEventId.OutboxMessagePublished.ShouldBe(130002);
	}

	[Fact]
	public void HaveOutboxMessageFailedInCoreRange()
	{
		// Assert
		OutboxEventId.OutboxMessageFailed.ShouldBe(130003);
	}

	[Fact]
	public void HaveOutboxBatchStoredInCoreRange()
	{
		// Assert
		OutboxEventId.OutboxBatchStored.ShouldBe(130004);
	}

	#endregion Outbox Core Event IDs (130000-130099)

	#region Outbox Configuration Event IDs (130100-130199)

	[Fact]
	public void HaveOutboxConfigurationLoadedInConfigurationRange()
	{
		// Assert
		OutboxEventId.OutboxConfigurationLoaded.ShouldBe(130100);
	}

	[Fact]
	public void HaveOutboxPublisherConfiguredInConfigurationRange()
	{
		// Assert
		OutboxEventId.OutboxPublisherConfigured.ShouldBe(130101);
	}

	[Fact]
	public void HaveOutboxRetryPolicyConfiguredInConfigurationRange()
	{
		// Assert
		OutboxEventId.OutboxRetryPolicyConfigured.ShouldBe(130102);
	}

	[Fact]
	public void HaveOutboxBatchSizeConfiguredInConfigurationRange()
	{
		// Assert
		OutboxEventId.OutboxBatchSizeConfigured.ShouldBe(130103);
	}

	#endregion Outbox Configuration Event IDs (130100-130199)

	#region Outbox Processing Core Event IDs (131000-131099)

	[Fact]
	public void HaveOutboxProcessorStartedInProcessingCoreRange()
	{
		// Assert
		OutboxEventId.OutboxProcessorStarted.ShouldBe(131000);
	}

	[Fact]
	public void HaveOutboxProcessorStoppedInProcessingCoreRange()
	{
		// Assert
		OutboxEventId.OutboxProcessorStopped.ShouldBe(131001);
	}

	[Fact]
	public void HaveOutboxBatchProcessingStartedInProcessingCoreRange()
	{
		// Assert
		OutboxEventId.OutboxBatchProcessingStarted.ShouldBe(131002);
	}

	[Fact]
	public void HaveOutboxBatchProcessingCompletedInProcessingCoreRange()
	{
		// Assert
		OutboxEventId.OutboxBatchProcessingCompleted.ShouldBe(131003);
	}

	[Fact]
	public void HaveOutboxProcessingCycleCompletedInProcessingCoreRange()
	{
		// Assert
		OutboxEventId.OutboxProcessingCycleCompleted.ShouldBe(131004);
	}

	#endregion Outbox Processing Core Event IDs (131000-131099)

	#region Outbox Processing Operations Event IDs (131100-131199)

	[Fact]
	public void HaveOutboxMessageRetryScheduledInOperationsRange()
	{
		// Assert
		OutboxEventId.OutboxMessageRetryScheduled.ShouldBe(131100);
	}

	[Fact]
	public void HaveOutboxMessageRetryingInOperationsRange()
	{
		// Assert
		OutboxEventId.OutboxMessageRetrying.ShouldBe(131101);
	}

	[Fact]
	public void HaveOutboxMessageDeadLetteredInOperationsRange()
	{
		// Assert
		OutboxEventId.OutboxMessageDeadLettered.ShouldBe(131102);
	}

	[Fact]
	public void HaveOutboxMessageAcknowledgedInOperationsRange()
	{
		// Assert
		OutboxEventId.OutboxMessageAcknowledged.ShouldBe(131103);
	}

	[Fact]
	public void HaveOutboxLockAcquiredInOperationsRange()
	{
		// Assert
		OutboxEventId.OutboxLockAcquired.ShouldBe(131104);
	}

	[Fact]
	public void HaveOutboxLockReleasedInOperationsRange()
	{
		// Assert
		OutboxEventId.OutboxLockReleased.ShouldBe(131105);
	}

	#endregion Outbox Processing Operations Event IDs (131100-131199)

	#region Outbox Background Service Event IDs (131300-131399)

	[Fact]
	public void HaveOutboxBackgroundServiceDisabledInBackgroundServiceRange()
	{
		// Assert
		OutboxEventId.OutboxBackgroundServiceDisabled.ShouldBe(131300);
	}

	[Fact]
	public void HaveOutboxBackgroundServiceStartingInBackgroundServiceRange()
	{
		// Assert
		OutboxEventId.OutboxBackgroundServiceStarting.ShouldBe(131301);
	}

	[Fact]
	public void HaveOutboxBackgroundServiceErrorInBackgroundServiceRange()
	{
		// Assert
		OutboxEventId.OutboxBackgroundServiceError.ShouldBe(131302);
	}

	[Fact]
	public void HaveOutboxBackgroundServiceStoppedInBackgroundServiceRange()
	{
		// Assert
		OutboxEventId.OutboxBackgroundServiceStopped.ShouldBe(131303);
	}

	#endregion Outbox Background Service Event IDs (131300-131399)

	#region Inbox Core Event IDs (132000-132099)

	[Fact]
	public void HaveInboxServiceCreatedInInboxCoreRange()
	{
		// Assert
		OutboxEventId.InboxServiceCreated.ShouldBe(132000);
	}

	[Fact]
	public void HaveInboxMessageReceivedInInboxCoreRange()
	{
		// Assert
		OutboxEventId.InboxMessageReceived.ShouldBe(132001);
	}

	[Fact]
	public void HaveInboxMessageProcessedInInboxCoreRange()
	{
		// Assert
		OutboxEventId.InboxMessageProcessed.ShouldBe(132002);
	}

	[Fact]
	public void HaveInboxDuplicateDetectedInInboxCoreRange()
	{
		// Assert
		OutboxEventId.InboxDuplicateDetected.ShouldBe(132003);
	}

	[Fact]
	public void HaveInboxMessageAcknowledgedInInboxCoreRange()
	{
		// Assert
		OutboxEventId.InboxMessageAcknowledged.ShouldBe(132004);
	}

	#endregion Inbox Core Event IDs (132000-132099)

	#region Storage Provider Event IDs (133100-133199)

	[Fact]
	public void HaveSqlServerOutboxStoreCreatedInStorageProviderRange()
	{
		// Assert
		OutboxEventId.SqlServerOutboxStoreCreated.ShouldBe(133100);
	}

	[Fact]
	public void HaveCosmosDbOutboxStoreCreatedInStorageProviderRange()
	{
		// Assert
		OutboxEventId.CosmosDbOutboxStoreCreated.ShouldBe(133101);
	}

	[Fact]
	public void HaveDynamoDbOutboxStoreCreatedInStorageProviderRange()
	{
		// Assert
		OutboxEventId.DynamoDbOutboxStoreCreated.ShouldBe(133102);
	}

	[Fact]
	public void HaveFirestoreOutboxStoreCreatedInStorageProviderRange()
	{
		// Assert
		OutboxEventId.FirestoreOutboxStoreCreated.ShouldBe(133103);
	}

	#endregion Storage Provider Event IDs (133100-133199)

	#region Outbox Cleanup Event IDs (134000-134099)

	[Fact]
	public void HaveOutboxCleanupStartedInCleanupRange()
	{
		// Assert
		OutboxEventId.OutboxCleanupStarted.ShouldBe(134000);
	}

	[Fact]
	public void HaveOutboxCleanupCompletedInCleanupRange()
	{
		// Assert
		OutboxEventId.OutboxCleanupCompleted.ShouldBe(134001);
	}

	[Fact]
	public void HaveOutboxMessagesPurgedInCleanupRange()
	{
		// Assert
		OutboxEventId.OutboxMessagesPurged.ShouldBe(134002);
	}

	[Fact]
	public void HaveOutboxRetentionPolicyAppliedInCleanupRange()
	{
		// Assert
		OutboxEventId.OutboxRetentionPolicyApplied.ShouldBe(134003);
	}

	[Fact]
	public void HaveInboxCleanupCompletedInCleanupRange()
	{
		// Assert
		OutboxEventId.InboxCleanupCompleted.ShouldBe(134004);
	}

	#endregion Outbox Cleanup Event IDs (134000-134099)

	#region Event ID Range Validation

	[Fact]
	public void HaveAllOutboxCoreEventIdsInExpectedRange()
	{
		// Assert - All core event IDs should be between 130000-130999
		OutboxEventId.OutboxServiceCreated.ShouldBeInRange(130000, 130999);
		OutboxEventId.OutboxMessageStored.ShouldBeInRange(130000, 130999);
		OutboxEventId.OutboxMessagePublished.ShouldBeInRange(130000, 130999);
		OutboxEventId.OutboxMessageFailed.ShouldBeInRange(130000, 130999);
		OutboxEventId.OutboxBatchStored.ShouldBeInRange(130000, 130999);
		OutboxEventId.OutboxConfigurationLoaded.ShouldBeInRange(130000, 130999);
	}

	[Fact]
	public void HaveAllOutboxProcessingEventIdsInExpectedRange()
	{
		// Assert - All processing event IDs should be between 131000-131999
		OutboxEventId.OutboxProcessorStarted.ShouldBeInRange(131000, 131999);
		OutboxEventId.OutboxProcessorStopped.ShouldBeInRange(131000, 131999);
		OutboxEventId.OutboxBatchProcessingStarted.ShouldBeInRange(131000, 131999);
		OutboxEventId.OutboxBatchProcessingCompleted.ShouldBeInRange(131000, 131999);
		OutboxEventId.OutboxMessageRetryScheduled.ShouldBeInRange(131000, 131999);
		OutboxEventId.OutboxBackgroundServiceDisabled.ShouldBeInRange(131000, 131999);
	}

	[Fact]
	public void HaveAllInboxEventIdsInExpectedRange()
	{
		// Assert - All inbox event IDs should be between 132000-132999
		OutboxEventId.InboxServiceCreated.ShouldBeInRange(132000, 132999);
		OutboxEventId.InboxMessageReceived.ShouldBeInRange(132000, 132999);
		OutboxEventId.InboxMessageProcessed.ShouldBeInRange(132000, 132999);
		OutboxEventId.InboxDuplicateDetected.ShouldBeInRange(132000, 132999);
		OutboxEventId.InboxIdempotencyCheckPassed.ShouldBeInRange(132000, 132999);
	}

	[Fact]
	public void HaveAllStorageEventIdsInExpectedRange()
	{
		// Assert - All storage event IDs should be between 133000-133999
		OutboxEventId.OutboxStoreCreated.ShouldBeInRange(133000, 133999);
		OutboxEventId.OutboxMessagesRetrieved.ShouldBeInRange(133000, 133999);
		OutboxEventId.SqlServerOutboxStoreCreated.ShouldBeInRange(133000, 133999);
		OutboxEventId.CosmosDbOutboxStoreCreated.ShouldBeInRange(133000, 133999);
		OutboxEventId.DynamoDbOutboxStoreCreated.ShouldBeInRange(133000, 133999);
		OutboxEventId.FirestoreOutboxStoreCreated.ShouldBeInRange(133000, 133999);
	}

	[Fact]
	public void HaveAllCleanupEventIdsInExpectedRange()
	{
		// Assert - All cleanup event IDs should be between 134000-134999
		OutboxEventId.OutboxCleanupStarted.ShouldBeInRange(134000, 134999);
		OutboxEventId.OutboxCleanupCompleted.ShouldBeInRange(134000, 134999);
		OutboxEventId.OutboxMessagesPurged.ShouldBeInRange(134000, 134999);
		OutboxEventId.OutboxRetentionPolicyApplied.ShouldBeInRange(134000, 134999);
		OutboxEventId.InboxCleanupCompleted.ShouldBeInRange(134000, 134999);
	}

	#endregion Event ID Range Validation

	#region Event ID Uniqueness

	[Fact]
	public void HaveUniqueEventIdsAcrossCategories()
	{
		// Arrange - Collect a sampling of event IDs from different categories
		var eventIds = new[]
		{
			OutboxEventId.OutboxServiceCreated,
			OutboxEventId.OutboxMessageStored,
			OutboxEventId.OutboxProcessorStarted,
			OutboxEventId.OutboxMessageRetryScheduled,
			OutboxEventId.OutboxBackgroundServiceDisabled,
			OutboxEventId.InboxServiceCreated,
			OutboxEventId.InboxMessageReceived,
			OutboxEventId.OutboxStoreCreated,
			OutboxEventId.SqlServerOutboxStoreCreated,
			OutboxEventId.OutboxCleanupStarted,
		};

		// Assert - All event IDs should be unique
		eventIds.Distinct().Count().ShouldBe(eventIds.Length);
	}

	#endregion Event ID Uniqueness

	#region MessageOutbox Event IDs (130200-130299)

	[Fact]
	public void HaveMessageOutboxStartedInMessageOutboxRange()
	{
		// Assert
		OutboxEventId.MessageOutboxStarted.ShouldBe(130200);
	}

	[Fact]
	public void HaveMessageOutboxErrorInMessageOutboxRange()
	{
		// Assert
		OutboxEventId.MessageOutboxError.ShouldBe(130201);
	}

	[Fact]
	public void HaveMessageOutboxStoppedInMessageOutboxRange()
	{
		// Assert
		OutboxEventId.MessageOutboxStopped.ShouldBe(130202);
	}

	[Fact]
	public void HaveNoMessagesToSaveInMessageOutboxRange()
	{
		// Assert
		OutboxEventId.NoMessagesToSave.ShouldBe(130203);
	}

	[Fact]
	public void HaveCouldNotResolveMessageTypeInMessageOutboxRange()
	{
		// Assert
		OutboxEventId.CouldNotResolveMessageType.ShouldBe(130204);
	}

	[Fact]
	public void HaveFailedToDeserializeMessageInMessageOutboxRange()
	{
		// Assert
		OutboxEventId.FailedToDeserializeMessage.ShouldBe(130205);
	}

	#endregion MessageOutbox Event IDs (130200-130299)

	#region OutboxProcessor Event IDs (131200-131399)

	[Fact]
	public void HaveOutboxDispatchFailedInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxDispatchFailed.ShouldBe(131200);
	}

	[Fact]
	public void HaveOutboxDisposingResourcesInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxDisposingResources.ShouldBe(131201);
	}

	[Fact]
	public void HaveOutboxConsumerNotCompletedInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxConsumerNotCompleted.ShouldBe(131202);
	}

	[Fact]
	public void HaveOutboxConsumerTimeoutDuringDisposalInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxConsumerTimeoutDuringDisposal.ShouldBe(131203);
	}

	[Fact]
	public void HaveOutboxErrorDisposingAsyncResourcesInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxErrorDisposingAsyncResources.ShouldBe(131204);
	}

	[Fact]
	public void HaveOutboxProducerIdleExitingInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxProducerIdleExiting.ShouldBe(131205);
	}

	[Fact]
	public void HaveOutboxEnqueuingBatchRecordsInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxEnqueuingBatchRecords.ShouldBe(131206);
	}

	[Fact]
	public void HaveOutboxProducerCanceledInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxProducerCanceled.ShouldBe(131207);
	}

	[Fact]
	public void HaveOutboxErrorInProducerLoopInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxErrorInProducerLoop.ShouldBe(131208);
	}

	[Fact]
	public void HaveOutboxProducerCompletedInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxProducerCompleted.ShouldBe(131209);
	}

	[Fact]
	public void HaveOutboxConsumerExitingInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxConsumerExiting.ShouldBe(131210);
	}

	[Fact]
	public void HaveOutboxConsumerCanceledInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxConsumerCanceled.ShouldBe(131211);
	}

	[Fact]
	public void HaveOutboxErrorInConsumerLoopInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxErrorInConsumerLoop.ShouldBe(131212);
	}

	[Fact]
	public void HaveOutboxProcessingCompletedInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxProcessingCompleted.ShouldBe(131213);
	}

	[Fact]
	public void HaveDispatchingOutboxRecordInProcessorRange()
	{
		// Assert
		OutboxEventId.DispatchingOutboxRecord.ShouldBe(131214);
	}

	[Fact]
	public void HaveSuccessfullyDispatchedOutboxRecordInProcessorRange()
	{
		// Assert
		OutboxEventId.SuccessfullyDispatchedOutboxRecord.ShouldBe(131215);
	}

	[Fact]
	public void HaveMarkedOutboxRecordSentInProcessorRange()
	{
		// Assert
		OutboxEventId.MarkedOutboxRecordSent.ShouldBe(131216);
	}

	[Fact]
	public void HaveErrorDispatchingOutboxRecordInProcessorRange()
	{
		// Assert
		OutboxEventId.ErrorDispatchingOutboxRecord.ShouldBe(131217);
	}

	[Fact]
	public void HaveOutboxDisposalRequestedExitingDataInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxDisposalRequestedExitingData.ShouldBe(131218);
	}

	[Fact]
	public void HaveOutboxMessageRoutedToDlqInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxMessageRoutedToDlq.ShouldBe(131219);
	}

	[Fact]
	public void HaveOutboxCircuitBreakerOpenInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxCircuitBreakerOpen.ShouldBe(131220);
	}

	[Fact]
	public void HaveOutboxRetryWithBackoffInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxRetryWithBackoff.ShouldBe(131221);
	}

	[Fact]
	public void HaveOutboxTransactionalFallbackInProcessorRange()
	{
		// Assert
		OutboxEventId.OutboxTransactionalFallback.ShouldBe(131222);
	}

	#endregion OutboxProcessor Event IDs (131200-131399)

	#region InboxProcessor Event IDs (132200-132399)

	[Fact]
	public void HaveInboxNoRecordInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxNoRecord.ShouldBe(132200);
	}

	[Fact]
	public void HaveInboxEnqueuingBatchInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxEnqueuingBatch.ShouldBe(132201);
	}

	[Fact]
	public void HaveInboxProducerCanceledInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxProducerCanceled.ShouldBe(132202);
	}

	[Fact]
	public void HaveInboxProducerErrorInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxProducerError.ShouldBe(132203);
	}

	[Fact]
	public void HaveInboxProducerCompletedInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxProducerCompleted.ShouldBe(132204);
	}

	[Fact]
	public void HaveInboxConsumerDisposalRequestedInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxConsumerDisposalRequested.ShouldBe(132205);
	}

	[Fact]
	public void HaveInboxConsumerExitingInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxConsumerExiting.ShouldBe(132206);
	}

	[Fact]
	public void HaveInboxConsumerCanceledInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxConsumerCanceled.ShouldBe(132207);
	}

	[Fact]
	public void HaveInboxConsumerErrorInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxConsumerError.ShouldBe(132208);
	}

	[Fact]
	public void HaveInboxProcessingCompleteInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxProcessingComplete.ShouldBe(132209);
	}

	[Fact]
	public void HaveInboxDispatchingMessageInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxDispatchingMessage.ShouldBe(132210);
	}

	[Fact]
	public void HaveInboxDispatchSuccessInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxDispatchSuccess.ShouldBe(132211);
	}

	[Fact]
	public void HaveInboxDispatchErrorInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxDispatchError.ShouldBe(132212);
	}

	[Fact]
	public void HaveInboxDisposingResourcesInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxDisposingResources.ShouldBe(132213);
	}

	[Fact]
	public void HaveInboxConsumerNotCompletedInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxConsumerNotCompleted.ShouldBe(132214);
	}

	[Fact]
	public void HaveInboxConsumerTimeoutInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxConsumerTimeout.ShouldBe(132215);
	}

	[Fact]
	public void HaveInboxDisposeErrorInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxDisposeError.ShouldBe(132216);
	}

	[Fact]
	public void HaveInboxMessageRoutedToDlqInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxMessageRoutedToDlq.ShouldBe(132217);
	}

	[Fact]
	public void HaveInboxCircuitBreakerOpenInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxCircuitBreakerOpen.ShouldBe(132218);
	}

	[Fact]
	public void HaveInboxRetryWithBackoffInInboxProcessorRange()
	{
		// Assert
		OutboxEventId.InboxRetryWithBackoff.ShouldBe(132219);
	}

	#endregion InboxProcessor Event IDs (132200-132399)

	#region Cloud Provider Event IDs (133200-133499)

	[Fact]
	public void HaveCosmosDbOutboxStoreInitializingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.CosmosDbOutboxStoreInitializing.ShouldBe(133200);
	}

	[Fact]
	public void HaveCosmosDbOutboxOperationCompletedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.CosmosDbOutboxOperationCompleted.ShouldBe(133201);
	}

	[Fact]
	public void HaveCosmosDbOutboxOperationFailedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.CosmosDbOutboxOperationFailed.ShouldBe(133202);
	}

	[Fact]
	public void HaveCosmosDbChangeFeedStartingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.CosmosDbChangeFeedStarting.ShouldBe(133203);
	}

	[Fact]
	public void HaveCosmosDbChangeFeedStoppingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.CosmosDbChangeFeedStopping.ShouldBe(133204);
	}

	[Fact]
	public void HaveCosmosDbChangeFeedBatchReceivedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.CosmosDbChangeFeedBatchReceived.ShouldBe(133205);
	}

	[Fact]
	public void HaveDynamoDbOutboxStoreInitializingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.DynamoDbOutboxStoreInitializing.ShouldBe(133300);
	}

	[Fact]
	public void HaveDynamoDbOutboxOperationCompletedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.DynamoDbOutboxOperationCompleted.ShouldBe(133301);
	}

	[Fact]
	public void HaveDynamoDbOutboxOperationFailedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.DynamoDbOutboxOperationFailed.ShouldBe(133302);
	}

	[Fact]
	public void HaveDynamoDbStreamsStartingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.DynamoDbStreamsStarting.ShouldBe(133303);
	}

	[Fact]
	public void HaveDynamoDbStreamsStoppingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.DynamoDbStreamsStopping.ShouldBe(133304);
	}

	[Fact]
	public void HaveDynamoDbStreamsBatchReceivedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.DynamoDbStreamsBatchReceived.ShouldBe(133305);
	}

	[Fact]
	public void HaveFirestoreOutboxStoreInitializingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.FirestoreOutboxStoreInitializing.ShouldBe(133400);
	}

	[Fact]
	public void HaveFirestoreOutboxOperationCompletedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.FirestoreOutboxOperationCompleted.ShouldBe(133401);
	}

	[Fact]
	public void HaveFirestoreOutboxOperationFailedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.FirestoreOutboxOperationFailed.ShouldBe(133402);
	}

	[Fact]
	public void HaveFirestoreListenerStartingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.FirestoreListenerStarting.ShouldBe(133403);
	}

	[Fact]
	public void HaveFirestoreListenerStoppingInCloudProviderRange()
	{
		// Assert
		OutboxEventId.FirestoreListenerStopping.ShouldBe(133404);
	}

	[Fact]
	public void HaveFirestoreListenerBatchReceivedInCloudProviderRange()
	{
		// Assert
		OutboxEventId.FirestoreListenerBatchReceived.ShouldBe(133405);
	}

	#endregion Cloud Provider Event IDs (133200-133499)

	#region MessageInbox Event IDs (132400-132499)

	[Fact]
	public void HaveMessageInboxDispatchStartingInMessageInboxRange()
	{
		// Assert
		OutboxEventId.MessageInboxDispatchStarting.ShouldBe(132400);
	}

	[Fact]
	public void HaveMessageInboxDispatchCompletedInMessageInboxRange()
	{
		// Assert
		OutboxEventId.MessageInboxDispatchCompleted.ShouldBe(132401);
	}

	#endregion MessageInbox Event IDs (132400-132499)

	#region Additional Background Service Event IDs

	[Fact]
	public void HaveOutboxBackgroundProcessedPendingInBackgroundServiceRange()
	{
		// Assert
		OutboxEventId.OutboxBackgroundProcessedPending.ShouldBe(131304);
	}

	[Fact]
	public void HaveOutboxBackgroundProcessedScheduledInBackgroundServiceRange()
	{
		// Assert
		OutboxEventId.OutboxBackgroundProcessedScheduled.ShouldBe(131305);
	}

	[Fact]
	public void HaveOutboxBackgroundRetriedFailedInBackgroundServiceRange()
	{
		// Assert
		OutboxEventId.OutboxBackgroundRetriedFailed.ShouldBe(131306);
	}

	[Fact]
	public void HaveOutboxBackgroundServiceDrainTimeoutInBackgroundServiceRange()
	{
		// Assert
		OutboxEventId.OutboxBackgroundServiceDrainTimeout.ShouldBe(131307);
	}

	[Fact]
	public void HaveInboxBackgroundServiceDrainTimeoutInInboxRange()
	{
		// Assert
		OutboxEventId.InboxBackgroundServiceDrainTimeout.ShouldBe(132005);
	}

	#endregion Additional Background Service Event IDs

	#region Inbox Operations Event IDs (132100-132199)

	[Fact]
	public void HaveInboxIdempotencyCheckPassedInInboxOperationsRange()
	{
		// Assert
		OutboxEventId.InboxIdempotencyCheckPassed.ShouldBe(132100);
	}

	[Fact]
	public void HaveInboxIdempotencyCheckFailedInInboxOperationsRange()
	{
		// Assert
		OutboxEventId.InboxIdempotencyCheckFailed.ShouldBe(132101);
	}

	[Fact]
	public void HaveInboxMessageStoredInInboxOperationsRange()
	{
		// Assert
		OutboxEventId.InboxMessageStored.ShouldBe(132102);
	}

	[Fact]
	public void HaveInboxEntryExpiredInInboxOperationsRange()
	{
		// Assert
		OutboxEventId.InboxEntryExpired.ShouldBe(132103);
	}

	#endregion Inbox Operations Event IDs (132100-132199)

	#region Storage Core Event IDs (133000-133099)

	[Fact]
	public void HaveOutboxStoreCreatedInStorageCoreRange()
	{
		// Assert
		OutboxEventId.OutboxStoreCreated.ShouldBe(133000);
	}

	[Fact]
	public void HaveOutboxMessagesRetrievedInStorageCoreRange()
	{
		// Assert
		OutboxEventId.OutboxMessagesRetrieved.ShouldBe(133001);
	}

	[Fact]
	public void HaveOutboxMessageMarkedProcessedInStorageCoreRange()
	{
		// Assert
		OutboxEventId.OutboxMessageMarkedProcessed.ShouldBe(133002);
	}

	[Fact]
	public void HaveOutboxMessageDeletedInStorageCoreRange()
	{
		// Assert
		OutboxEventId.OutboxMessageDeleted.ShouldBe(133003);
	}

	#endregion Storage Core Event IDs (133000-133099)

	#region Comprehensive Uniqueness Test

	[Fact]
	public void HaveUniqueEventIdsForAllDefinedEventIds()
	{
		// Arrange - Collect ALL event IDs
		var allEventIds = new[]
		{
			// Core
			OutboxEventId.OutboxServiceCreated,
			OutboxEventId.OutboxMessageStored,
			OutboxEventId.OutboxMessagePublished,
			OutboxEventId.OutboxMessageFailed,
			OutboxEventId.OutboxBatchStored,
			// Configuration
			OutboxEventId.OutboxConfigurationLoaded,
			OutboxEventId.OutboxPublisherConfigured,
			OutboxEventId.OutboxRetryPolicyConfigured,
			OutboxEventId.OutboxBatchSizeConfigured,
			// Processing Core
			OutboxEventId.OutboxProcessorStarted,
			OutboxEventId.OutboxProcessorStopped,
			OutboxEventId.OutboxBatchProcessingStarted,
			OutboxEventId.OutboxBatchProcessingCompleted,
			OutboxEventId.OutboxProcessingCycleCompleted,
			// Processing Operations
			OutboxEventId.OutboxMessageRetryScheduled,
			OutboxEventId.OutboxMessageRetrying,
			OutboxEventId.OutboxMessageDeadLettered,
			OutboxEventId.OutboxMessageAcknowledged,
			OutboxEventId.OutboxLockAcquired,
			OutboxEventId.OutboxLockReleased,
			// Background Service
			OutboxEventId.OutboxBackgroundServiceDisabled,
			OutboxEventId.OutboxBackgroundServiceStarting,
			OutboxEventId.OutboxBackgroundServiceError,
			OutboxEventId.OutboxBackgroundServiceStopped,
			OutboxEventId.OutboxBackgroundProcessedPending,
			OutboxEventId.OutboxBackgroundProcessedScheduled,
			OutboxEventId.OutboxBackgroundRetriedFailed,
			OutboxEventId.OutboxBackgroundServiceDrainTimeout,
			// Inbox Core
			OutboxEventId.InboxServiceCreated,
			OutboxEventId.InboxMessageReceived,
			OutboxEventId.InboxMessageProcessed,
			OutboxEventId.InboxDuplicateDetected,
			OutboxEventId.InboxMessageAcknowledged,
			OutboxEventId.InboxBackgroundServiceDrainTimeout,
			// Inbox Operations
			OutboxEventId.InboxIdempotencyCheckPassed,
			OutboxEventId.InboxIdempotencyCheckFailed,
			OutboxEventId.InboxMessageStored,
			OutboxEventId.InboxEntryExpired,
			// Storage
			OutboxEventId.OutboxStoreCreated,
			OutboxEventId.OutboxMessagesRetrieved,
			OutboxEventId.OutboxMessageMarkedProcessed,
			OutboxEventId.OutboxMessageDeleted,
			OutboxEventId.SqlServerOutboxStoreCreated,
			OutboxEventId.CosmosDbOutboxStoreCreated,
			OutboxEventId.DynamoDbOutboxStoreCreated,
			OutboxEventId.FirestoreOutboxStoreCreated,
			// Cleanup
			OutboxEventId.OutboxCleanupStarted,
			OutboxEventId.OutboxCleanupCompleted,
			OutboxEventId.OutboxMessagesPurged,
			OutboxEventId.OutboxRetentionPolicyApplied,
			OutboxEventId.InboxCleanupCompleted,
			// MessageOutbox
			OutboxEventId.MessageOutboxStarted,
			OutboxEventId.MessageOutboxError,
			OutboxEventId.MessageOutboxStopped,
			OutboxEventId.NoMessagesToSave,
			OutboxEventId.CouldNotResolveMessageType,
			OutboxEventId.FailedToDeserializeMessage,
			// OutboxProcessor
			OutboxEventId.OutboxDispatchFailed,
			OutboxEventId.OutboxDisposingResources,
			OutboxEventId.OutboxConsumerNotCompleted,
			OutboxEventId.OutboxConsumerTimeoutDuringDisposal,
			OutboxEventId.OutboxErrorDisposingAsyncResources,
			OutboxEventId.OutboxProducerIdleExiting,
			OutboxEventId.OutboxEnqueuingBatchRecords,
			OutboxEventId.OutboxProducerCanceled,
			OutboxEventId.OutboxErrorInProducerLoop,
			OutboxEventId.OutboxProducerCompleted,
			OutboxEventId.OutboxConsumerExiting,
			OutboxEventId.OutboxConsumerCanceled,
			OutboxEventId.OutboxErrorInConsumerLoop,
			OutboxEventId.OutboxProcessingCompleted,
			OutboxEventId.DispatchingOutboxRecord,
			OutboxEventId.SuccessfullyDispatchedOutboxRecord,
			OutboxEventId.MarkedOutboxRecordSent,
			OutboxEventId.ErrorDispatchingOutboxRecord,
			OutboxEventId.OutboxDisposalRequestedExitingData,
			OutboxEventId.OutboxMessageRoutedToDlq,
			OutboxEventId.OutboxCircuitBreakerOpen,
			OutboxEventId.OutboxRetryWithBackoff,
			OutboxEventId.OutboxTransactionalFallback,
			// InboxProcessor
			OutboxEventId.InboxNoRecord,
			OutboxEventId.InboxEnqueuingBatch,
			OutboxEventId.InboxProducerCanceled,
			OutboxEventId.InboxProducerError,
			OutboxEventId.InboxProducerCompleted,
			OutboxEventId.InboxConsumerDisposalRequested,
			OutboxEventId.InboxConsumerExiting,
			OutboxEventId.InboxConsumerCanceled,
			OutboxEventId.InboxConsumerError,
			OutboxEventId.InboxProcessingComplete,
			OutboxEventId.InboxDispatchingMessage,
			OutboxEventId.InboxDispatchSuccess,
			OutboxEventId.InboxDispatchError,
			OutboxEventId.InboxDisposingResources,
			OutboxEventId.InboxConsumerNotCompleted,
			OutboxEventId.InboxConsumerTimeout,
			OutboxEventId.InboxDisposeError,
			OutboxEventId.InboxMessageRoutedToDlq,
			OutboxEventId.InboxCircuitBreakerOpen,
			OutboxEventId.InboxRetryWithBackoff,
			// Cloud Providers
			OutboxEventId.CosmosDbOutboxStoreInitializing,
			OutboxEventId.CosmosDbOutboxOperationCompleted,
			OutboxEventId.CosmosDbOutboxOperationFailed,
			OutboxEventId.CosmosDbChangeFeedStarting,
			OutboxEventId.CosmosDbChangeFeedStopping,
			OutboxEventId.CosmosDbChangeFeedBatchReceived,
			OutboxEventId.DynamoDbOutboxStoreInitializing,
			OutboxEventId.DynamoDbOutboxOperationCompleted,
			OutboxEventId.DynamoDbOutboxOperationFailed,
			OutboxEventId.DynamoDbStreamsStarting,
			OutboxEventId.DynamoDbStreamsStopping,
			OutboxEventId.DynamoDbStreamsBatchReceived,
			OutboxEventId.FirestoreOutboxStoreInitializing,
			OutboxEventId.FirestoreOutboxOperationCompleted,
			OutboxEventId.FirestoreOutboxOperationFailed,
			OutboxEventId.FirestoreListenerStarting,
			OutboxEventId.FirestoreListenerStopping,
			OutboxEventId.FirestoreListenerBatchReceived,
			// MessageInbox
			OutboxEventId.MessageInboxDispatchStarting,
			OutboxEventId.MessageInboxDispatchCompleted
		};

		// Assert - All event IDs should be unique
		allEventIds.Distinct().Count().ShouldBe(allEventIds.Length);
	}

	#endregion Comprehensive Uniqueness Test
}
