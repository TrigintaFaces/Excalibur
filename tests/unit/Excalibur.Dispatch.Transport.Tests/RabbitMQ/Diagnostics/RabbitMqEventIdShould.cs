// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Diagnostics;

/// <summary>
/// Unit tests for <see cref="RabbitMqEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport.RabbitMQ")]
[Trait("Priority", "0")]
public sealed class RabbitMqEventIdShould : UnitTestBase
{
	#region Core Event ID Tests (21000-21099)

	[Fact]
	public void HaveConnectionEstablishedInCoreRange()
	{
		RabbitMqEventId.ConnectionEstablished.ShouldBe(21000);
	}

	[Fact]
	public void HaveConnectionLostInCoreRange()
	{
		RabbitMqEventId.ConnectionLost.ShouldBe(21001);
	}

	[Fact]
	public void HaveConnectionRecoveredInCoreRange()
	{
		RabbitMqEventId.ConnectionRecovered.ShouldBe(21002);
	}

	[Fact]
	public void HaveChannelCreatedInCoreRange()
	{
		RabbitMqEventId.ChannelCreated.ShouldBe(21003);
	}

	[Fact]
	public void HaveChannelClosedInCoreRange()
	{
		RabbitMqEventId.ChannelClosed.ShouldBe(21004);
	}

	[Fact]
	public void HaveMessageBusInitializingInCoreRange()
	{
		RabbitMqEventId.MessageBusInitializing.ShouldBe(21005);
	}

	[Fact]
	public void HaveAllCoreEventIdsInExpectedRange()
	{
		RabbitMqEventId.ConnectionEstablished.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.ConnectionLost.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.ConnectionRecovered.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.ChannelCreated.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.ChannelClosed.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.MessageBusInitializing.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.MessageBusStarting.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.MessageBusStopping.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.TransportAdapterInitialized.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.TransportAdapterStarting.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.ExchangeDeclared.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.QueueDeclared.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.BindingCreated.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.TransportAdapterStopping.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.ReceivingMessage.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.SendingMessage.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.MessageProcessingFailed.ShouldBeInRange(21000, 21099);
		RabbitMqEventId.SendFailed.ShouldBeInRange(21000, 21099);
	}

	#endregion

	#region Consumer Event ID Tests (21100-21199)

	[Fact]
	public void HaveConsumerStartedInConsumerRange()
	{
		RabbitMqEventId.ConsumerStarted.ShouldBe(21100);
	}

	[Fact]
	public void HaveConsumerStoppedInConsumerRange()
	{
		RabbitMqEventId.ConsumerStopped.ShouldBe(21101);
	}

	[Fact]
	public void HaveMessageReceivedInConsumerRange()
	{
		RabbitMqEventId.MessageReceived.ShouldBe(21102);
	}

	[Fact]
	public void HaveMessageAcknowledgedInConsumerRange()
	{
		RabbitMqEventId.MessageAcknowledged.ShouldBe(21103);
	}

	[Fact]
	public void HaveAllConsumerEventIdsInExpectedRange()
	{
		RabbitMqEventId.ConsumerStarted.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.ConsumerStopped.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.MessageReceived.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.MessageAcknowledged.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.MessageRejected.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.MessageRequeued.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.ConsumerCancelled.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.ChannelConsumerStarting.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.ChannelConsumerStopping.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.BasicConsumeRegistered.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.BatchProduced.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.MessageConversionError.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.ContextDeserializationFailure.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.MessagesAcknowledgedBatch.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.AcknowledgmentError.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.BatchProcessingError.ShouldBeInRange(21100, 21199);
		RabbitMqEventId.AcknowledgmentFailed.ShouldBeInRange(21100, 21199);
	}

	#endregion

	#region Publisher Event ID Tests (21200-21299)

	[Fact]
	public void HaveMessagePublishedInPublisherRange()
	{
		RabbitMqEventId.MessagePublished.ShouldBe(21200);
	}

	[Fact]
	public void HavePublishConfirmedInPublisherRange()
	{
		RabbitMqEventId.PublishConfirmed.ShouldBe(21201);
	}

	[Fact]
	public void HavePublishFailedInPublisherRange()
	{
		RabbitMqEventId.PublishFailed.ShouldBe(21202);
	}

	[Fact]
	public void HaveAllPublisherEventIdsInExpectedRange()
	{
		RabbitMqEventId.MessagePublished.ShouldBeInRange(21200, 21299);
		RabbitMqEventId.PublishConfirmed.ShouldBeInRange(21200, 21299);
		RabbitMqEventId.PublishFailed.ShouldBeInRange(21200, 21299);
		RabbitMqEventId.BatchPublishStarted.ShouldBeInRange(21200, 21299);
		RabbitMqEventId.BatchPublishCompleted.ShouldBeInRange(21200, 21299);
		RabbitMqEventId.ActionSent.ShouldBeInRange(21200, 21299);
		RabbitMqEventId.EventPublished.ShouldBeInRange(21200, 21299);
		RabbitMqEventId.DocumentSent.ShouldBeInRange(21200, 21299);
	}

	#endregion

	#region CloudEvents Integration Event ID Tests (21300-21399)

	[Fact]
	public void HaveCloudEventReceivedInCloudEventsRange()
	{
		RabbitMqEventId.CloudEventReceived.ShouldBe(21300);
	}

	[Fact]
	public void HaveCloudEventPublishedInCloudEventsRange()
	{
		RabbitMqEventId.CloudEventPublished.ShouldBe(21301);
	}

	[Fact]
	public void HaveAllCloudEventsEventIdsInExpectedRange()
	{
		RabbitMqEventId.CloudEventReceived.ShouldBeInRange(21300, 21399);
		RabbitMqEventId.CloudEventPublished.ShouldBeInRange(21300, 21399);
		RabbitMqEventId.CloudEventConversion.ShouldBeInRange(21300, 21399);
	}

	#endregion

	#region Error Handling Event ID Tests (21400-21499)

	[Fact]
	public void HaveConnectionErrorInErrorRange()
	{
		RabbitMqEventId.ConnectionError.ShouldBe(21400);
	}

	[Fact]
	public void HaveChannelErrorInErrorRange()
	{
		RabbitMqEventId.ChannelError.ShouldBe(21401);
	}

	[Fact]
	public void HaveConsumerErrorInErrorRange()
	{
		RabbitMqEventId.ConsumerError.ShouldBe(21402);
	}

	[Fact]
	public void HaveAllErrorEventIdsInExpectedRange()
	{
		RabbitMqEventId.ConnectionError.ShouldBeInRange(21400, 21499);
		RabbitMqEventId.ChannelError.ShouldBeInRange(21400, 21499);
		RabbitMqEventId.ConsumerError.ShouldBeInRange(21400, 21499);
		RabbitMqEventId.PublishError.ShouldBeInRange(21400, 21499);
		RabbitMqEventId.DeserializationError.ShouldBeInRange(21400, 21499);
		RabbitMqEventId.ConnectionBlocked.ShouldBeInRange(21400, 21499);
		RabbitMqEventId.ConnectionUnblocked.ShouldBeInRange(21400, 21499);
	}

	#endregion

	#region RabbitMQ Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInRabbitMqReservedRange()
	{
		// RabbitMQ reserved range is 21000-21999
		var allEventIds = GetAllRabbitMqEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(21000, 21999,
				$"Event ID {eventId} is outside RabbitMQ reserved range (21000-21999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllRabbitMqEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllRabbitMqEventIds();
		allEventIds.Length.ShouldBeGreaterThan(40);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllRabbitMqEventIds()
	{
		return
		[
			// Core (21000-21099)
			RabbitMqEventId.ConnectionEstablished,
			RabbitMqEventId.ConnectionLost,
			RabbitMqEventId.ConnectionRecovered,
			RabbitMqEventId.ChannelCreated,
			RabbitMqEventId.ChannelClosed,
			RabbitMqEventId.MessageBusInitializing,
			RabbitMqEventId.MessageBusStarting,
			RabbitMqEventId.MessageBusStopping,
			RabbitMqEventId.TransportAdapterInitialized,
			RabbitMqEventId.TransportAdapterStarting,
			RabbitMqEventId.ExchangeDeclared,
			RabbitMqEventId.QueueDeclared,
			RabbitMqEventId.BindingCreated,
			RabbitMqEventId.TransportAdapterStopping,
			RabbitMqEventId.ReceivingMessage,
			RabbitMqEventId.SendingMessage,
			RabbitMqEventId.MessageProcessingFailed,
			RabbitMqEventId.SendFailed,

			// Consumer (21100-21199)
			RabbitMqEventId.ConsumerStarted,
			RabbitMqEventId.ConsumerStopped,
			RabbitMqEventId.MessageReceived,
			RabbitMqEventId.MessageAcknowledged,
			RabbitMqEventId.MessageRejected,
			RabbitMqEventId.MessageRequeued,
			RabbitMqEventId.ConsumerCancelled,
			RabbitMqEventId.ChannelConsumerStarting,
			RabbitMqEventId.ChannelConsumerStopping,
			RabbitMqEventId.BasicConsumeRegistered,
			RabbitMqEventId.BatchProduced,
			RabbitMqEventId.MessageConversionError,
			RabbitMqEventId.ContextDeserializationFailure,
			RabbitMqEventId.MessagesAcknowledgedBatch,
			RabbitMqEventId.AcknowledgmentError,
			RabbitMqEventId.BatchProcessingError,
			RabbitMqEventId.AcknowledgmentFailed,

			// Publisher (21200-21299)
			RabbitMqEventId.MessagePublished,
			RabbitMqEventId.PublishConfirmed,
			RabbitMqEventId.PublishFailed,
			RabbitMqEventId.BatchPublishStarted,
			RabbitMqEventId.BatchPublishCompleted,
			RabbitMqEventId.ActionSent,
			RabbitMqEventId.EventPublished,
			RabbitMqEventId.DocumentSent,

			// CloudEvents Integration (21300-21399)
			RabbitMqEventId.CloudEventReceived,
			RabbitMqEventId.CloudEventPublished,
			RabbitMqEventId.CloudEventConversion,

			// Error Handling (21400-21499)
			RabbitMqEventId.ConnectionError,
			RabbitMqEventId.ChannelError,
			RabbitMqEventId.ConsumerError,
			RabbitMqEventId.PublishError,
			RabbitMqEventId.DeserializationError,
			RabbitMqEventId.ConnectionBlocked,
			RabbitMqEventId.ConnectionUnblocked
		];
	}

	#endregion
}
