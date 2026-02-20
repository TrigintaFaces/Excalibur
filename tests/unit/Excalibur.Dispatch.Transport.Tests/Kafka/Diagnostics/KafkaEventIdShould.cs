// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Diagnostics;

/// <summary>
/// Unit tests for <see cref="KafkaEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport.Kafka")]
[Trait("Priority", "0")]
public sealed class KafkaEventIdShould : UnitTestBase
{
	#region Core Event ID Tests (22000-22099)

	[Fact]
	public void HaveProducerCreatedInCoreRange()
	{
		KafkaEventId.ProducerCreated.ShouldBe(22000);
	}

	[Fact]
	public void HaveProducerDisposedInCoreRange()
	{
		KafkaEventId.ProducerDisposed.ShouldBe(22001);
	}

	[Fact]
	public void HaveConsumerCreatedInCoreRange()
	{
		KafkaEventId.ConsumerCreated.ShouldBe(22002);
	}

	[Fact]
	public void HaveConsumerDisposedInCoreRange()
	{
		KafkaEventId.ConsumerDisposed.ShouldBe(22003);
	}

	[Fact]
	public void HaveAllCoreEventIdsInExpectedRange()
	{
		KafkaEventId.ProducerCreated.ShouldBeInRange(22000, 22099);
		KafkaEventId.ProducerDisposed.ShouldBeInRange(22000, 22099);
		KafkaEventId.ConsumerCreated.ShouldBeInRange(22000, 22099);
		KafkaEventId.ConsumerDisposed.ShouldBeInRange(22000, 22099);
		KafkaEventId.MessageBusInitializing.ShouldBeInRange(22000, 22099);
		KafkaEventId.MessageBusStarting.ShouldBeInRange(22000, 22099);
		KafkaEventId.MessageBusStopping.ShouldBeInRange(22000, 22099);
		KafkaEventId.TransportAdapterInitialized.ShouldBeInRange(22000, 22099);
		KafkaEventId.TransportAdapterStarting.ShouldBeInRange(22000, 22099);
		KafkaEventId.TransportAdapterStopping.ShouldBeInRange(22000, 22099);
		KafkaEventId.TopicCreated.ShouldBeInRange(22000, 22099);
		KafkaEventId.TopicSubscribed.ShouldBeInRange(22000, 22099);
		KafkaEventId.TopicUnsubscribed.ShouldBeInRange(22000, 22099);
		KafkaEventId.TransactionInitialized.ShouldBeInRange(22000, 22099);
		KafkaEventId.TransactionBegin.ShouldBeInRange(22000, 22099);
		KafkaEventId.TransactionCommitted.ShouldBeInRange(22000, 22099);
		KafkaEventId.TransactionAborted.ShouldBeInRange(22000, 22099);
	}

	#endregion

	#region Consumer Event ID Tests (22100-22199)

	[Fact]
	public void HaveConsumerStartedInConsumerRange()
	{
		KafkaEventId.ConsumerStarted.ShouldBe(22100);
	}

	[Fact]
	public void HaveConsumerStoppedInConsumerRange()
	{
		KafkaEventId.ConsumerStopped.ShouldBe(22101);
	}

	[Fact]
	public void HaveMessageReceivedInConsumerRange()
	{
		KafkaEventId.MessageReceived.ShouldBe(22102);
	}

	[Fact]
	public void HaveAllConsumerEventIdsInExpectedRange()
	{
		KafkaEventId.ConsumerStarted.ShouldBeInRange(22100, 22199);
		KafkaEventId.ConsumerStopped.ShouldBeInRange(22100, 22199);
		KafkaEventId.MessageReceived.ShouldBeInRange(22100, 22199);
		KafkaEventId.MessageCommitted.ShouldBeInRange(22100, 22199);
		KafkaEventId.ConsumerPollCompleted.ShouldBeInRange(22100, 22199);
		KafkaEventId.ConsumerRebalance.ShouldBeInRange(22100, 22199);
		KafkaEventId.PartitionsAssigned.ShouldBeInRange(22100, 22199);
		KafkaEventId.PartitionsRevoked.ShouldBeInRange(22100, 22199);
		KafkaEventId.ChannelConsumerStarting.ShouldBeInRange(22100, 22199);
		KafkaEventId.ChannelConsumerStopping.ShouldBeInRange(22100, 22199);
		KafkaEventId.BatchProduced.ShouldBeInRange(22100, 22199);
		KafkaEventId.ProduceError.ShouldBeInRange(22100, 22199);
		KafkaEventId.DeserializationFailure.ShouldBeInRange(22100, 22199);
		KafkaEventId.ContextDeserializationFailure.ShouldBeInRange(22100, 22199);
		KafkaEventId.OffsetsCommitted.ShouldBeInRange(22100, 22199);
		KafkaEventId.CommitOffsetsFailure.ShouldBeInRange(22100, 22199);
		KafkaEventId.CommitOffsetsError.ShouldBeInRange(22100, 22199);
		KafkaEventId.MessageConversionError.ShouldBeInRange(22100, 22199);
		KafkaEventId.MessageRejected.ShouldBeInRange(22100, 22199);
		KafkaEventId.CloudEventMapperResolved.ShouldBeInRange(22100, 22199);
		KafkaEventId.ConsumeError.ShouldBeInRange(22100, 22199);
		KafkaEventId.OffsetCommitFailed.ShouldBeInRange(22100, 22199);
		KafkaEventId.PartitionEof.ShouldBeInRange(22100, 22199);
		KafkaEventId.ConsumerLag.ShouldBeInRange(22100, 22199);
		KafkaEventId.ReceivingMessage.ShouldBeInRange(22100, 22199);
		KafkaEventId.SendingMessage.ShouldBeInRange(22100, 22199);
		KafkaEventId.MessageProcessingFailed.ShouldBeInRange(22100, 22199);
		KafkaEventId.SendFailed.ShouldBeInRange(22100, 22199);
		KafkaEventId.ActionSent.ShouldBeInRange(22100, 22199);
		KafkaEventId.EventPublished.ShouldBeInRange(22100, 22199);
		KafkaEventId.DocumentSent.ShouldBeInRange(22100, 22199);
		KafkaEventId.PublishingMessage.ShouldBeInRange(22100, 22199);
		KafkaEventId.MessagePublishedWithSize.ShouldBeInRange(22100, 22199);
	}

	#endregion

	#region Schema Registry Event ID Tests (22200-22299)

	[Fact]
	public void HaveSchemaRegisteredInSchemaRange()
	{
		KafkaEventId.SchemaRegistered.ShouldBe(22200);
	}

	[Fact]
	public void HaveSchemaRetrievedInSchemaRange()
	{
		KafkaEventId.SchemaRetrieved.ShouldBe(22201);
	}

	[Fact]
	public void HaveSchemaCachedInSchemaRange()
	{
		KafkaEventId.SchemaCached.ShouldBe(22202);
	}

	[Fact]
	public void HaveAllSchemaRegistryEventIdsInExpectedRange()
	{
		KafkaEventId.SchemaRegistered.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaRetrieved.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaCached.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaRegistryClientCreated.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaValidationPassed.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaValidationFailed.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaEvolutionDetected.ShouldBeInRange(22200, 22299);
		KafkaEventId.JsonSerializerCreated.ShouldBeInRange(22200, 22299);
		KafkaEventId.GettingSchemaId.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaIdRetrieved.ShouldBeInRange(22200, 22299);
		KafkaEventId.GettingSchemaById.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaRetrievalError.ShouldBeInRange(22200, 22299);
		KafkaEventId.RegisteringSchema.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaRegistrationError.ShouldBeInRange(22200, 22299);
		KafkaEventId.CheckingCompatibility.ShouldBeInRange(22200, 22299);
		KafkaEventId.CompatibilityResult.ShouldBeInRange(22200, 22299);
		KafkaEventId.CompatibilityCheckError.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaCacheHit.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaCacheMiss.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaCacheHitById.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaCacheMissById.ShouldBeInRange(22200, 22299);
		KafkaEventId.SerializingMessage.ShouldBeInRange(22200, 22299);
		KafkaEventId.SchemaIdResolved.ShouldBeInRange(22200, 22299);
		KafkaEventId.SerializationComplete.ShouldBeInRange(22200, 22299);
		KafkaEventId.ZeroCopySerializationStarted.ShouldBeInRange(22200, 22299);
		KafkaEventId.ZeroCopyHeaderWritten.ShouldBeInRange(22200, 22299);
		KafkaEventId.ZeroCopyPayloadWritten.ShouldBeInRange(22200, 22299);
		KafkaEventId.ZeroCopySerializationComplete.ShouldBeInRange(22200, 22299);
	}

	#endregion

	#region CloudEvents Integration Event ID Tests (22300-22399)

	[Fact]
	public void HaveCloudEventReceivedInCloudEventsRange()
	{
		KafkaEventId.CloudEventReceived.ShouldBe(22300);
	}

	[Fact]
	public void HaveCloudEventPublishedInCloudEventsRange()
	{
		KafkaEventId.CloudEventPublished.ShouldBe(22301);
	}

	[Fact]
	public void HaveAllCloudEventsEventIdsInExpectedRange()
	{
		KafkaEventId.CloudEventReceived.ShouldBeInRange(22300, 22399);
		KafkaEventId.CloudEventPublished.ShouldBeInRange(22300, 22399);
		KafkaEventId.CloudEventAdapterInitialized.ShouldBeInRange(22300, 22399);
		KafkaEventId.CloudEventToTransportError.ShouldBeInRange(22300, 22399);
		KafkaEventId.CloudEventFromTransportError.ShouldBeInRange(22300, 22399);
	}

	#endregion

	#region Error Handling Event ID Tests (22400-22499)

	[Fact]
	public void HaveProducerErrorInErrorRange()
	{
		KafkaEventId.ProducerError.ShouldBe(22400);
	}

	[Fact]
	public void HaveConsumerErrorInErrorRange()
	{
		KafkaEventId.ConsumerError.ShouldBe(22401);
	}

	[Fact]
	public void HaveDeliveryFailedInErrorRange()
	{
		KafkaEventId.DeliveryFailed.ShouldBe(22402);
	}

	[Fact]
	public void HaveAllErrorEventIdsInExpectedRange()
	{
		KafkaEventId.ProducerError.ShouldBeInRange(22400, 22499);
		KafkaEventId.ConsumerError.ShouldBeInRange(22400, 22499);
		KafkaEventId.DeliveryFailed.ShouldBeInRange(22400, 22499);
		KafkaEventId.DeserializationError.ShouldBeInRange(22400, 22499);
		KafkaEventId.SchemaRegistryError.ShouldBeInRange(22400, 22499);
		KafkaEventId.ConnectionError.ShouldBeInRange(22400, 22499);
		KafkaEventId.TransactionError.ShouldBeInRange(22400, 22499);
		KafkaEventId.TransactionAbortFailed.ShouldBeInRange(22400, 22499);
		KafkaEventId.TransactionInitializationFailed.ShouldBeInRange(22400, 22499);
	}

	#endregion

	#region Partitioning Event ID Tests (22500-22599)

	[Fact]
	public void HavePartitionSelectedInPartitioningRange()
	{
		KafkaEventId.PartitionSelected.ShouldBe(22500);
	}

	[Fact]
	public void HaveMessagePublishedInPartitioningRange()
	{
		KafkaEventId.MessagePublished.ShouldBe(22501);
	}

	[Fact]
	public void HaveAllPartitioningEventIdsInExpectedRange()
	{
		KafkaEventId.PartitionSelected.ShouldBeInRange(22500, 22599);
		KafkaEventId.MessagePublished.ShouldBeInRange(22500, 22599);
		KafkaEventId.BatchPublishStarted.ShouldBeInRange(22500, 22599);
		KafkaEventId.BatchPublishCompleted.ShouldBeInRange(22500, 22599);
		KafkaEventId.DeliveryReportReceived.ShouldBeInRange(22500, 22599);
	}

	#endregion

	#region Kafka Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInKafkaReservedRange()
	{
		// Kafka reserved range is 22000-22999
		var allEventIds = GetAllKafkaEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(22000, 22999,
				$"Event ID {eventId} is outside Kafka reserved range (22000-22999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllKafkaEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllKafkaEventIds();
		allEventIds.Length.ShouldBeGreaterThan(70);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllKafkaEventIds()
	{
		return
		[
			// Core (22000-22099)
			KafkaEventId.ProducerCreated,
			KafkaEventId.ProducerDisposed,
			KafkaEventId.ConsumerCreated,
			KafkaEventId.ConsumerDisposed,
			KafkaEventId.MessageBusInitializing,
			KafkaEventId.MessageBusStarting,
			KafkaEventId.MessageBusStopping,
			KafkaEventId.TransportAdapterInitialized,
			KafkaEventId.TransportAdapterStarting,
			KafkaEventId.TransportAdapterStopping,
			KafkaEventId.TopicCreated,
			KafkaEventId.TopicSubscribed,
			KafkaEventId.TopicUnsubscribed,
			KafkaEventId.TransactionInitialized,
			KafkaEventId.TransactionBegin,
			KafkaEventId.TransactionCommitted,
			KafkaEventId.TransactionAborted,

			// Consumer (22100-22199)
			KafkaEventId.ConsumerStarted,
			KafkaEventId.ConsumerStopped,
			KafkaEventId.MessageReceived,
			KafkaEventId.MessageCommitted,
			KafkaEventId.ConsumerPollCompleted,
			KafkaEventId.ConsumerRebalance,
			KafkaEventId.PartitionsAssigned,
			KafkaEventId.PartitionsRevoked,
			KafkaEventId.ChannelConsumerStarting,
			KafkaEventId.ChannelConsumerStopping,
			KafkaEventId.BatchProduced,
			KafkaEventId.ProduceError,
			KafkaEventId.DeserializationFailure,
			KafkaEventId.ContextDeserializationFailure,
			KafkaEventId.OffsetsCommitted,
			KafkaEventId.CommitOffsetsFailure,
			KafkaEventId.CommitOffsetsError,
			KafkaEventId.MessageConversionError,
			KafkaEventId.MessageRejected,
			KafkaEventId.CloudEventMapperResolved,
			KafkaEventId.ConsumeError,
			KafkaEventId.OffsetCommitFailed,
			KafkaEventId.PartitionEof,
			KafkaEventId.ConsumerLag,
			KafkaEventId.ReceivingMessage,
			KafkaEventId.SendingMessage,
			KafkaEventId.MessageProcessingFailed,
			KafkaEventId.SendFailed,
			KafkaEventId.ActionSent,
			KafkaEventId.EventPublished,
			KafkaEventId.DocumentSent,
			KafkaEventId.PublishingMessage,
			KafkaEventId.MessagePublishedWithSize,

			// Schema Registry (22200-22299)
			KafkaEventId.SchemaRegistered,
			KafkaEventId.SchemaRetrieved,
			KafkaEventId.SchemaCached,
			KafkaEventId.SchemaRegistryClientCreated,
			KafkaEventId.SchemaValidationPassed,
			KafkaEventId.SchemaValidationFailed,
			KafkaEventId.SchemaEvolutionDetected,
			KafkaEventId.JsonSerializerCreated,
			KafkaEventId.GettingSchemaId,
			KafkaEventId.SchemaIdRetrieved,
			KafkaEventId.GettingSchemaById,
			KafkaEventId.SchemaRetrievalError,
			KafkaEventId.RegisteringSchema,
			KafkaEventId.SchemaRegistrationError,
			KafkaEventId.CheckingCompatibility,
			KafkaEventId.CompatibilityResult,
			KafkaEventId.CompatibilityCheckError,
			KafkaEventId.SchemaCacheHit,
			KafkaEventId.SchemaCacheMiss,
			KafkaEventId.SchemaCacheHitById,
			KafkaEventId.SchemaCacheMissById,
			KafkaEventId.SerializingMessage,
			KafkaEventId.SchemaIdResolved,
			KafkaEventId.SerializationComplete,
			KafkaEventId.ZeroCopySerializationStarted,
			KafkaEventId.ZeroCopyHeaderWritten,
			KafkaEventId.ZeroCopyPayloadWritten,
			KafkaEventId.ZeroCopySerializationComplete,

			// CloudEvents Integration (22300-22399)
			KafkaEventId.CloudEventReceived,
			KafkaEventId.CloudEventPublished,
			KafkaEventId.CloudEventAdapterInitialized,
			KafkaEventId.CloudEventToTransportError,
			KafkaEventId.CloudEventFromTransportError,

			// Error Handling (22400-22499)
			KafkaEventId.ProducerError,
			KafkaEventId.ConsumerError,
			KafkaEventId.DeliveryFailed,
			KafkaEventId.DeserializationError,
			KafkaEventId.SchemaRegistryError,
			KafkaEventId.ConnectionError,
			KafkaEventId.TransactionError,
			KafkaEventId.TransactionAbortFailed,
			KafkaEventId.TransactionInitializationFailed,

			// Partitioning (22500-22599)
			KafkaEventId.PartitionSelected,
			KafkaEventId.MessagePublished,
			KafkaEventId.BatchPublishStarted,
			KafkaEventId.BatchPublishCompleted,
			KafkaEventId.DeliveryReportReceived
		];
	}

	#endregion
}
