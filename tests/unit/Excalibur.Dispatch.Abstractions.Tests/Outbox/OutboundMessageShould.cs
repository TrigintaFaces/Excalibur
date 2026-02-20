// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for the <see cref="OutboundMessage"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class OutboundMessageShould
{
	[Fact]
	public void DefaultConstructor_Should_SetIdAndStatus()
	{
		// Act
		var msg = new OutboundMessage();

		// Assert
		msg.Id.ShouldNotBeNullOrEmpty();
		msg.Status.ShouldBe(OutboxStatus.Staged);
		msg.CreatedAt.ShouldNotBe(default);
	}

	[Fact]
	public void ParameterizedConstructor_Should_SetAllValues()
	{
		// Arrange
		var payload = new byte[] { 1, 2, 3 };
		var headers = new Dictionary<string, object>(StringComparer.Ordinal) { ["key"] = "val" };

		// Act
		var msg = new OutboundMessage("OrderCreated", payload, "orders-queue", headers);

		// Assert
		msg.MessageType.ShouldBe("OrderCreated");
		msg.Payload.ShouldBe(payload);
		msg.Destination.ShouldBe("orders-queue");
		msg.Headers["key"].ShouldBe("val");
		msg.Status.ShouldBe(OutboxStatus.Staged);
	}

	[Fact]
	public void ParameterizedConstructor_Should_ThrowOnNullMessageType()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage(null!, [1], "dest"));
	}

	[Fact]
	public void ParameterizedConstructor_Should_ThrowOnNullPayload()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage("Type", null!, "dest"));
	}

	[Fact]
	public void ParameterizedConstructor_Should_ThrowOnNullDestination()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage("Type", [1], null!));
	}

	[Fact]
	public void ParameterizedConstructor_Should_CreateDefaultHeaders_WhenNull()
	{
		// Act
		var msg = new OutboundMessage("Type", [1], "dest", null);

		// Assert
		msg.Headers.ShouldNotBeNull();
		msg.Headers.Count.ShouldBe(0);
	}

	[Fact]
	public void MarkSending_Should_UpdateStatusAndTimestamp()
	{
		// Arrange
		var msg = new OutboundMessage();

		// Act
		msg.MarkSending();

		// Assert
		msg.Status.ShouldBe(OutboxStatus.Sending);
		msg.LastAttemptAt.ShouldNotBeNull();
	}

	[Fact]
	public void MarkSent_Should_UpdateStatusAndClearError()
	{
		// Arrange
		var msg = new OutboundMessage { LastError = "previous error" };
		msg.MarkFailed("test error");

		// Act
		msg.MarkSent();

		// Assert
		msg.Status.ShouldBe(OutboxStatus.Sent);
		msg.SentAt.ShouldNotBeNull();
		msg.LastError.ShouldBeNull();
	}

	[Fact]
	public void MarkFailed_Should_IncrementRetryCount()
	{
		// Arrange
		var msg = new OutboundMessage();
		msg.Status = OutboxStatus.Failed; // Needed for MarkFailed logic

		// Act
		msg.MarkFailed("connection timeout");

		// Assert
		msg.Status.ShouldBe(OutboxStatus.Failed);
		msg.LastError.ShouldBe("connection timeout");
		msg.RetryCount.ShouldBe(1);
		msg.LastAttemptAt.ShouldNotBeNull();
	}

	[Fact]
	public void MarkFailed_Should_ThrowOnNullOrEmptyError()
	{
		// Arrange
		var msg = new OutboundMessage();

		// Act & Assert
		Should.Throw<ArgumentException>(() => msg.MarkFailed(null!));
		Should.Throw<ArgumentException>(() => msg.MarkFailed(string.Empty));
	}

	[Fact]
	public void IsReadyForDelivery_Should_ReturnTrue_WhenStaged()
	{
		// Arrange
		var msg = new OutboundMessage();

		// Act & Assert
		msg.IsReadyForDelivery().ShouldBeTrue();
	}

	[Fact]
	public void IsReadyForDelivery_Should_ReturnFalse_WhenNotStaged()
	{
		// Arrange
		var msg = new OutboundMessage { Status = OutboxStatus.Sent };

		// Act & Assert
		msg.IsReadyForDelivery().ShouldBeFalse();
	}

	[Fact]
	public void IsReadyForDelivery_Should_ReturnFalse_WhenScheduledInFuture()
	{
		// Arrange
		var msg = new OutboundMessage { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) };

		// Act & Assert
		msg.IsReadyForDelivery().ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_Should_ReturnFalse_WhenNotFailed()
	{
		// Arrange
		var msg = new OutboundMessage();

		// Act & Assert
		msg.IsEligibleForRetry().ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_Should_ReturnFalse_WhenMaxRetriesReached()
	{
		// Arrange
		var msg = new OutboundMessage { Status = OutboxStatus.Failed, RetryCount = 3 };

		// Act & Assert
		msg.IsEligibleForRetry(maxRetries: 3).ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_Should_ReturnTrue_WhenFailedAndUnderMax()
	{
		// Arrange
		var msg = new OutboundMessage { Status = OutboxStatus.Failed, RetryCount = 1 };

		// Act & Assert
		msg.IsEligibleForRetry(maxRetries: 3).ShouldBeTrue();
	}

	[Fact]
	public void AddTransport_Should_CreateTransportDelivery()
	{
		// Arrange
		var msg = new OutboundMessage("Type", [1], "default-dest");

		// Act
		var delivery = msg.AddTransport("kafka");

		// Assert
		delivery.ShouldNotBeNull();
		delivery.TransportName.ShouldBe("kafka");
		delivery.Destination.ShouldBe("default-dest");
		msg.IsMultiTransport.ShouldBeTrue();
		msg.TransportDeliveries.Count.ShouldBe(1);
		msg.TargetTransports.ShouldBe("kafka");
	}

	[Fact]
	public void AddTransport_Should_ThrowOnNullOrWhitespace()
	{
		// Arrange
		var msg = new OutboundMessage();

		// Act & Assert
		Should.Throw<ArgumentException>(() => msg.AddTransport(null!));
		Should.Throw<ArgumentException>(() => msg.AddTransport("  "));
	}

	[Fact]
	public void GetTransportDelivery_Should_FindByName()
	{
		// Arrange
		var msg = new OutboundMessage("Type", [1], "dest");
		msg.AddTransport("kafka");
		msg.AddTransport("rabbitmq");

		// Act
		var delivery = msg.GetTransportDelivery("kafka");

		// Assert
		delivery.ShouldNotBeNull();
		delivery.TransportName.ShouldBe("kafka");
	}

	[Fact]
	public void GetTransportDelivery_Should_ReturnNull_WhenNotFound()
	{
		// Arrange
		var msg = new OutboundMessage();

		// Act
		var delivery = msg.GetTransportDelivery("nonexistent");

		// Assert
		delivery.ShouldBeNull();
	}

	[Fact]
	public void AreAllTransportsComplete_Should_ReturnTrue_WhenAllSent()
	{
		// Arrange
		var msg = new OutboundMessage("Type", [1], "dest");
		var t1 = msg.AddTransport("kafka");
		var t2 = msg.AddTransport("rabbitmq");
		t1.MarkSent();
		t2.MarkSent();

		// Act & Assert
		msg.AreAllTransportsComplete().ShouldBeTrue();
	}

	[Fact]
	public void AreAllTransportsComplete_Should_ReturnFalse_WhenSomePending()
	{
		// Arrange
		var msg = new OutboundMessage("Type", [1], "dest");
		var t1 = msg.AddTransport("kafka");
		msg.AddTransport("rabbitmq");
		t1.MarkSent();

		// Act & Assert
		msg.AreAllTransportsComplete().ShouldBeFalse();
	}

	[Fact]
	public void UpdateAggregateStatus_Should_SetSent_WhenAllComplete()
	{
		// Arrange
		var msg = new OutboundMessage("Type", [1], "dest");
		var t1 = msg.AddTransport("kafka");
		var t2 = msg.AddTransport("rabbitmq");
		t1.MarkSent();
		t2.MarkSent();

		// Act
		msg.UpdateAggregateStatus();

		// Assert
		msg.Status.ShouldBe(OutboxStatus.Sent);
	}

	[Fact]
	public void UpdateAggregateStatus_Should_SetFailed_WhenAllFailed()
	{
		// Arrange
		var msg = new OutboundMessage("Type", [1], "dest");
		var t1 = msg.AddTransport("kafka");
		var t2 = msg.AddTransport("rabbitmq");
		t1.MarkFailed("error1");
		t2.MarkFailed("error2");

		// Act
		msg.UpdateAggregateStatus();

		// Assert
		msg.Status.ShouldBe(OutboxStatus.Failed);
	}

	[Fact]
	public void GetAge_Should_ReturnPositiveTimeSpan()
	{
		// Arrange
		var msg = new OutboundMessage();

		// Act
		var age = msg.GetAge();

		// Assert
		age.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void GetTimeSinceLastAttempt_Should_ReturnNull_WhenNeverAttempted()
	{
		// Arrange
		var msg = new OutboundMessage();

		// Act & Assert
		msg.GetTimeSinceLastAttempt().ShouldBeNull();
	}

	[Fact]
	public void ToString_Should_IncludeRelevantInfo()
	{
		// Arrange
		var msg = new OutboundMessage("OrderCreated", [1], "orders-queue");

		// Act
		var result = msg.ToString();

		// Assert
		result.ShouldContain("OrderCreated");
		result.ShouldContain("orders-queue");
		result.ShouldContain("Staged");
	}
}
