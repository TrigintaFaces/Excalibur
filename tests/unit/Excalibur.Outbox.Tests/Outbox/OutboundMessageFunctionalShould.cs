// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Outbox.Tests.Outbox;

[Trait("Category", "Unit")]
public class OutboundMessageFunctionalShould
{
	private static readonly byte[] TestPayload = [0x01, 0x02, 0x03];

	[Fact]
	public void DefaultConstructor_ShouldInitializeDefaults()
	{
		var message = new OutboundMessage();

		message.Id.ShouldNotBeNullOrWhiteSpace();
		message.Status.ShouldBe(OutboxStatus.Staged);
		message.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		message.RetryCount.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithValues_ShouldSetProperties()
	{
		var headers = new Dictionary<string, object> { ["key"] = "value" };
		var message = new OutboundMessage("OrderCreated", TestPayload, "orders-topic", headers);

		message.MessageType.ShouldBe("OrderCreated");
		message.Payload.ShouldBe(TestPayload);
		message.Destination.ShouldBe("orders-topic");
		message.Headers["key"].ShouldBe("value");
	}

	[Fact]
	public void Constructor_WithNullMessageType_ShouldThrow()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage(null!, TestPayload, "dest"));
	}

	[Fact]
	public void Constructor_WithNullPayload_ShouldThrow()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage("Type", null!, "dest"));
	}

	[Fact]
	public void Constructor_WithNullDestination_ShouldThrow()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage("Type", TestPayload, null!));
	}

	[Fact]
	public void MarkSending_ShouldUpdateStatus()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		message.MarkSending();

		message.Status.ShouldBe(OutboxStatus.Sending);
		message.LastAttemptAt.ShouldNotBeNull();
	}

	[Fact]
	public void MarkSent_ShouldUpdateStatus()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		message.MarkSent();

		message.Status.ShouldBe(OutboxStatus.Sent);
		message.SentAt.ShouldNotBeNull();
		message.LastError.ShouldBeNull();
	}

	[Fact]
	public void MarkFailed_ShouldUpdateStatusAndIncrementRetry()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		message.MarkFailed("Connection refused");

		message.Status.ShouldBe(OutboxStatus.Failed);
		message.LastError.ShouldBe("Connection refused");
		message.RetryCount.ShouldBe(1);
		message.LastAttemptAt.ShouldNotBeNull();
	}

	[Fact]
	public void MarkFailed_MultipleTimes_ShouldIncrementRetryCount()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		message.MarkFailed("Error 1");
		message.MarkFailed("Error 2");
		message.MarkFailed("Error 3");

		message.RetryCount.ShouldBe(3);
		message.LastError.ShouldBe("Error 3");
	}

	[Fact]
	public void MarkFailed_WithNullError_ShouldThrow()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		Should.Throw<ArgumentException>(() => message.MarkFailed(null!));
	}

	[Fact]
	public void IsReadyForDelivery_Staged_ShouldReturnTrue()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		message.IsReadyForDelivery().ShouldBeTrue();
	}

	[Fact]
	public void IsReadyForDelivery_Sent_ShouldReturnFalse()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		message.MarkSent();

		message.IsReadyForDelivery().ShouldBeFalse();
	}

	[Fact]
	public void IsReadyForDelivery_ScheduledInFuture_ShouldReturnFalse()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest")
		{
			ScheduledAt = DateTimeOffset.UtcNow.AddHours(1)
		};

		message.IsReadyForDelivery().ShouldBeFalse();
	}

	[Fact]
	public void IsReadyForDelivery_ScheduledInPast_ShouldReturnTrue()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest")
		{
			ScheduledAt = DateTimeOffset.UtcNow.AddHours(-1)
		};

		message.IsReadyForDelivery().ShouldBeTrue();
	}

	[Fact]
	public void IsEligibleForRetry_FailedWithinLimit_ShouldReturnTrue()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		message.MarkFailed("Error");
		// LastAttemptAt is set but we need it to be before the retry delay
		message.LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-10);

		message.IsEligibleForRetry(maxRetries: 3, retryDelayMinutes: 5).ShouldBeTrue();
	}

	[Fact]
	public void IsEligibleForRetry_ExceededMaxRetries_ShouldReturnFalse()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		message.MarkFailed("Error 1");
		message.MarkFailed("Error 2");
		message.MarkFailed("Error 3");

		message.IsEligibleForRetry(maxRetries: 3).ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_NotFailed_ShouldReturnFalse()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		message.IsEligibleForRetry().ShouldBeFalse();
	}

	[Fact]
	public void AddTransport_ShouldCreateDeliveryRecord()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		var delivery = message.AddTransport("kafka");

		delivery.ShouldNotBeNull();
		delivery.TransportName.ShouldBe("kafka");
		message.TransportDeliveries.Count.ShouldBe(1);
		message.IsMultiTransport.ShouldBeTrue();
	}

	[Fact]
	public void AddTransport_Multiple_ShouldTrackAll()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		message.AddTransport("kafka");
		message.AddTransport("rabbitmq");

		message.TransportDeliveries.Count.ShouldBe(2);
		message.TargetTransports.ShouldContain("kafka");
		message.TargetTransports.ShouldContain("rabbitmq");
	}

	[Fact]
	public void AddTransport_WithNullName_ShouldThrow()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		Should.Throw<ArgumentException>(() => message.AddTransport(null!));
	}

	[Fact]
	public void GetTransportDelivery_Existing_ShouldReturn()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		message.AddTransport("kafka");

		var delivery = message.GetTransportDelivery("kafka");

		delivery.ShouldNotBeNull();
		delivery!.TransportName.ShouldBe("kafka");
	}

	[Fact]
	public void GetTransportDelivery_NonExisting_ShouldReturnNull()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		var delivery = message.GetTransportDelivery("kafka");

		delivery.ShouldBeNull();
	}

	[Fact]
	public void AreAllTransportsComplete_AllSent_ShouldReturnTrue()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		var d1 = message.AddTransport("kafka");
		var d2 = message.AddTransport("rabbitmq");

		d1.Status = TransportDeliveryStatus.Sent;
		d2.Status = TransportDeliveryStatus.Sent;

		message.AreAllTransportsComplete().ShouldBeTrue();
	}

	[Fact]
	public void AreAllTransportsComplete_SomePending_ShouldReturnFalse()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		var d1 = message.AddTransport("kafka");
		var d2 = message.AddTransport("rabbitmq");

		d1.Status = TransportDeliveryStatus.Sent;
		d2.Status = TransportDeliveryStatus.Pending;

		message.AreAllTransportsComplete().ShouldBeFalse();
	}

	[Fact]
	public void GetAge_ShouldReturnPositiveTimeSpan()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");

		var age = message.GetAge();

		age.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void ToString_ShouldContainMessageInfo()
	{
		var message = new OutboundMessage("OrderCreated", TestPayload, "orders-topic");

		var str = message.ToString();

		str.ShouldContain("OrderCreated");
		str.ShouldContain("orders-topic");
	}

	[Fact]
	public void UpdateAggregateStatus_AllFailed_ShouldSetFailed()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		var d1 = message.AddTransport("kafka");
		var d2 = message.AddTransport("rabbitmq");

		d1.Status = TransportDeliveryStatus.Failed;
		d2.Status = TransportDeliveryStatus.Failed;

		message.UpdateAggregateStatus();

		message.Status.ShouldBe(OutboxStatus.Failed);
	}

	[Fact]
	public void UpdateAggregateStatus_PartialFailed_ShouldSetPartiallyFailed()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		var d1 = message.AddTransport("kafka");
		var d2 = message.AddTransport("rabbitmq");

		d1.Status = TransportDeliveryStatus.Sent;
		d2.Status = TransportDeliveryStatus.Failed;

		message.UpdateAggregateStatus();

		message.Status.ShouldBe(OutboxStatus.PartiallyFailed);
	}

	[Fact]
	public void UpdateAggregateStatus_AllSent_ShouldSetSent()
	{
		var message = new OutboundMessage("Type", TestPayload, "dest");
		var d1 = message.AddTransport("kafka");
		var d2 = message.AddTransport("rabbitmq");

		d1.Status = TransportDeliveryStatus.Sent;
		d2.Status = TransportDeliveryStatus.Sent;

		message.UpdateAggregateStatus();

		message.Status.ShouldBe(OutboxStatus.Sent);
		message.SentAt.ShouldNotBeNull();
	}
}
