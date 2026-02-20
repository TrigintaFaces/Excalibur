// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="OutboundMessage"/> covering construction, properties,
/// status transitions, multi-transport delivery, and delivery eligibility checks.
/// </summary>
/// <remarks>
/// Sprint 410 - Foundation Coverage Tests (T410.8).
/// Target: Increase OutboundMessage coverage from 43.4% to 80%.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class OutboundMessageShould
{
	#region Default Constructor Tests

	[Fact]
	public void DefaultConstructor_Should_Generate_New_Id()
	{
		// Act
		var message = new OutboundMessage();

		// Assert
		message.Id.ShouldNotBeNullOrWhiteSpace();
		Guid.TryParse(message.Id, out _).ShouldBeTrue();
	}

	[Fact]
	public void DefaultConstructor_Should_Set_CreatedAt_To_Now()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var message = new OutboundMessage();

		// Assert
		var after = DateTimeOffset.UtcNow;
		message.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		message.CreatedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void DefaultConstructor_Should_Set_Status_To_Staged()
	{
		// Act
		var message = new OutboundMessage();

		// Assert
		message.Status.ShouldBe(OutboxStatus.Staged);
	}

	[Fact]
	public void DefaultConstructor_Should_Initialize_Headers_As_Empty()
	{
		// Act
		var message = new OutboundMessage();

		// Assert
		_ = message.Headers.ShouldNotBeNull();
		message.Headers.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultConstructor_Should_Initialize_TransportDeliveries_As_Empty()
	{
		// Act
		var message = new OutboundMessage();

		// Assert
		_ = message.TransportDeliveries.ShouldNotBeNull();
		message.TransportDeliveries.ShouldBeEmpty();
	}

	#endregion

	#region Parameterized Constructor Tests

	[Fact]
	public void ParameterizedConstructor_Should_Set_MessageType()
	{
		// Arrange
		var payload = new byte[] { 0x01, 0x02 };

		// Act
		var message = new OutboundMessage("Order.Created", payload, "orders-queue");

		// Assert
		message.MessageType.ShouldBe("Order.Created");
	}

	[Fact]
	public void ParameterizedConstructor_Should_Set_Payload()
	{
		// Arrange
		var payload = new byte[] { 0x01, 0x02, 0x03 };

		// Act
		var message = new OutboundMessage("Order.Created", payload, "orders-queue");

		// Assert
		message.Payload.ShouldBe(payload);
	}

	[Fact]
	public void ParameterizedConstructor_Should_Set_Destination()
	{
		// Arrange
		var payload = new byte[] { 0x01 };

		// Act
		var message = new OutboundMessage("Order.Created", payload, "orders-queue");

		// Assert
		message.Destination.ShouldBe("orders-queue");
	}

	[Fact]
	public void ParameterizedConstructor_Should_Set_Headers_When_Provided()
	{
		// Arrange
		var payload = new byte[] { 0x01 };
		var headers = new Dictionary<string, object> { ["correlation-id"] = "abc-123" };

		// Act
		var message = new OutboundMessage("Order.Created", payload, "orders-queue", headers);

		// Assert
		message.Headers.ShouldBe(headers);
	}

	[Fact]
	public void ParameterizedConstructor_Should_Initialize_Empty_Headers_When_Null()
	{
		// Arrange
		var payload = new byte[] { 0x01 };

		// Act
		var message = new OutboundMessage("Order.Created", payload, "orders-queue", null);

		// Assert
		_ = message.Headers.ShouldNotBeNull();
		message.Headers.ShouldBeEmpty();
	}

	[Fact]
	public void ParameterizedConstructor_Should_Generate_New_Id()
	{
		// Arrange
		var payload = new byte[] { 0x01 };

		// Act
		var message = new OutboundMessage("Order.Created", payload, "orders-queue");

		// Assert
		message.Id.ShouldNotBeNullOrWhiteSpace();
		Guid.TryParse(message.Id, out _).ShouldBeTrue();
	}

	[Fact]
	public void ParameterizedConstructor_Should_Throw_When_MessageType_Is_Null()
	{
		// Arrange
		var payload = new byte[] { 0x01 };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage(null!, payload, "orders-queue"));
	}

	[Fact]
	public void ParameterizedConstructor_Should_Throw_When_Payload_Is_Null()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage("Order.Created", null!, "orders-queue"));
	}

	[Fact]
	public void ParameterizedConstructor_Should_Throw_When_Destination_Is_Null()
	{
		// Arrange
		var payload = new byte[] { 0x01 };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new OutboundMessage("Order.Created", payload, null!));
	}

	#endregion

	#region Property Get/Set Tests

	[Fact]
	public void Should_Get_And_Set_MessageType()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.MessageType = "Order.Updated";

		// Assert
		message.MessageType.ShouldBe("Order.Updated");
	}

	[Fact]
	public void Should_Get_And_Set_Payload()
	{
		// Arrange
		var message = new OutboundMessage();
		var payload = new byte[] { 0xAA, 0xBB };

		// Act
		message.Payload = payload;

		// Assert
		message.Payload.ShouldBe(payload);
	}

	[Fact]
	public void Should_Get_And_Set_Destination()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.Destination = "events-topic";

		// Assert
		message.Destination.ShouldBe("events-topic");
	}

	[Fact]
	public void Should_Get_And_Set_ScheduledAt()
	{
		// Arrange
		var message = new OutboundMessage();
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(1);

		// Act
		message.ScheduledAt = scheduledTime;

		// Assert
		message.ScheduledAt.ShouldBe(scheduledTime);
	}

	[Fact]
	public void Should_Get_And_Set_CorrelationId()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.CorrelationId = "corr-123";

		// Assert
		message.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void Should_Get_And_Set_CausationId()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.CausationId = "cause-456";

		// Assert
		message.CausationId.ShouldBe("cause-456");
	}

	[Fact]
	public void Should_Get_And_Set_TenantId()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.TenantId = "tenant-abc";

		// Assert
		message.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void Should_Get_And_Set_Priority()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.Priority = 10;

		// Assert
		message.Priority.ShouldBe(10);
	}

	#endregion

	#region MarkSending Tests

	[Fact]
	public void MarkSending_Should_Set_Status_To_Sending()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.MarkSending();

		// Assert
		message.Status.ShouldBe(OutboxStatus.Sending);
	}

	[Fact]
	public void MarkSending_Should_Set_LastAttemptAt()
	{
		// Arrange
		var message = new OutboundMessage();
		var before = DateTimeOffset.UtcNow;

		// Act
		message.MarkSending();

		// Assert
		_ = message.LastAttemptAt.ShouldNotBeNull();
		message.LastAttemptAt.Value.ShouldBeGreaterThanOrEqualTo(before);
	}

	#endregion

	#region MarkSent Tests

	[Fact]
	public void MarkSent_Should_Set_Status_To_Sent()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.MarkSent();

		// Assert
		message.Status.ShouldBe(OutboxStatus.Sent);
	}

	[Fact]
	public void MarkSent_Should_Set_SentAt()
	{
		// Arrange
		var message = new OutboundMessage();
		var before = DateTimeOffset.UtcNow;

		// Act
		message.MarkSent();

		// Assert
		_ = message.SentAt.ShouldNotBeNull();
		message.SentAt.Value.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public void MarkSent_Should_Clear_LastError()
	{
		// Arrange
		var message = new OutboundMessage { LastError = "Previous error" };

		// Act
		message.MarkSent();

		// Assert
		message.LastError.ShouldBeNull();
	}

	#endregion

	#region MarkFailed Tests

	[Fact]
	public void MarkFailed_Should_Set_Status_To_Failed()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.MarkFailed("Connection timeout");

		// Assert
		message.Status.ShouldBe(OutboxStatus.Failed);
	}

	[Fact]
	public void MarkFailed_Should_Set_LastError()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.MarkFailed("Connection timeout");

		// Assert
		message.LastError.ShouldBe("Connection timeout");
	}

	[Fact]
	public void MarkFailed_Should_Increment_RetryCount()
	{
		// Arrange
		var message = new OutboundMessage { RetryCount = 2 };

		// Act
		message.MarkFailed("Error");

		// Assert
		message.RetryCount.ShouldBe(3);
	}

	[Fact]
	public void MarkFailed_Should_Set_LastAttemptAt()
	{
		// Arrange
		var message = new OutboundMessage();
		var before = DateTimeOffset.UtcNow;

		// Act
		message.MarkFailed("Error");

		// Assert
		_ = message.LastAttemptAt.ShouldNotBeNull();
		message.LastAttemptAt.Value.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public void MarkFailed_Should_Throw_When_Error_Is_Null()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => message.MarkFailed(null!));
	}

	[Fact]
	public void MarkFailed_Should_Throw_When_Error_Is_Empty()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => message.MarkFailed(string.Empty));
	}

	#endregion

	#region IsReadyForDelivery Tests

	[Fact]
	public void IsReadyForDelivery_Should_Return_True_When_Staged_And_Not_Scheduled()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		message.IsReadyForDelivery().ShouldBeTrue();
	}

	[Fact]
	public void IsReadyForDelivery_Should_Return_False_When_Not_Staged()
	{
		// Arrange
		var message = new OutboundMessage();
		message.MarkSent();

		// Act & Assert
		message.IsReadyForDelivery().ShouldBeFalse();
	}

	[Fact]
	public void IsReadyForDelivery_Should_Return_False_When_ScheduledAt_Is_Future()
	{
		// Arrange
		var message = new OutboundMessage { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) };

		// Act & Assert
		message.IsReadyForDelivery().ShouldBeFalse();
	}

	[Fact]
	public void IsReadyForDelivery_Should_Return_True_When_ScheduledAt_Is_Past()
	{
		// Arrange
		var message = new OutboundMessage { ScheduledAt = DateTimeOffset.UtcNow.AddHours(-1) };

		// Act & Assert
		message.IsReadyForDelivery().ShouldBeTrue();
	}

	#endregion

	#region IsEligibleForRetry Tests

	[Fact]
	public void IsEligibleForRetry_Should_Return_False_When_Not_Failed()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		message.IsEligibleForRetry().ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_Should_Return_False_When_MaxRetries_Exceeded()
	{
		// Arrange
		var message = new OutboundMessage { Status = OutboxStatus.Failed, RetryCount = 3 };

		// Act & Assert
		message.IsEligibleForRetry(maxRetries: 3).ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_Should_Return_True_When_No_LastAttempt()
	{
		// Arrange
		var message = new OutboundMessage { Status = OutboxStatus.Failed, RetryCount = 0 };

		// Act & Assert
		message.IsEligibleForRetry().ShouldBeTrue();
	}

	[Fact]
	public void IsEligibleForRetry_Should_Return_False_When_Within_RetryDelay()
	{
		// Arrange
		var message = new OutboundMessage
		{
			Status = OutboxStatus.Failed,
			RetryCount = 1,
			LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-2)
		};

		// Act & Assert
		message.IsEligibleForRetry(maxRetries: 3, retryDelayMinutes: 5).ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_Should_Return_True_When_Past_RetryDelay()
	{
		// Arrange
		var message = new OutboundMessage
		{
			Status = OutboxStatus.Failed,
			RetryCount = 1,
			LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-10)
		};

		// Act & Assert
		message.IsEligibleForRetry(maxRetries: 3, retryDelayMinutes: 5).ShouldBeTrue();
	}

	#endregion

	#region GetAge Tests

	[Fact]
	public void GetAge_Should_Return_Time_Since_Creation()
	{
		// Arrange
		var message = new OutboundMessage { CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30) };

		// Act
		var age = message.GetAge();

		// Assert
		age.TotalMinutes.ShouldBeGreaterThan(29);
		age.TotalMinutes.ShouldBeLessThan(31);
	}

	#endregion

	#region GetTimeSinceLastAttempt Tests

	[Fact]
	public void GetTimeSinceLastAttempt_Should_Return_Null_When_Never_Attempted()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		message.GetTimeSinceLastAttempt().ShouldBeNull();
	}

	[Fact]
	public void GetTimeSinceLastAttempt_Should_Return_TimeSpan_When_Attempted()
	{
		// Arrange
		var message = new OutboundMessage { LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-15) };

		// Act
		var timeSince = message.GetTimeSinceLastAttempt();

		// Assert
		_ = timeSince.ShouldNotBeNull();
		timeSince.Value.TotalMinutes.ShouldBeGreaterThan(14);
		timeSince.Value.TotalMinutes.ShouldBeLessThan(16);
	}

	#endregion

	#region AddTransport Tests

	[Fact]
	public void AddTransport_Should_Add_Transport_Delivery()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "default-queue" };

		// Act
		var delivery = message.AddTransport("rabbitmq");

		// Assert
		message.TransportDeliveries.Count.ShouldBe(1);
		delivery.TransportName.ShouldBe("rabbitmq");
	}

	[Fact]
	public void AddTransport_Should_Set_IsMultiTransport_To_True()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "default-queue" };

		// Act
		_ = message.AddTransport("rabbitmq");

		// Assert
		message.IsMultiTransport.ShouldBeTrue();
	}

	[Fact]
	public void AddTransport_Should_Use_Default_Destination_When_Not_Specified()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "default-queue" };

		// Act
		var delivery = message.AddTransport("rabbitmq");

		// Assert
		delivery.Destination.ShouldBe("default-queue");
	}

	[Fact]
	public void AddTransport_Should_Use_Custom_Destination_When_Specified()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "default-queue" };

		// Act
		var delivery = message.AddTransport("rabbitmq", "custom-queue");

		// Assert
		delivery.Destination.ShouldBe("custom-queue");
	}

	[Fact]
	public void AddTransport_Should_Update_TargetTransports_String()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "default-queue" };

		// Act
		_ = message.AddTransport("rabbitmq");
		_ = message.AddTransport("kafka");

		// Assert
		message.TargetTransports.ShouldContain("rabbitmq");
		message.TargetTransports.ShouldContain("kafka");
	}

	[Fact]
	public void AddTransport_Should_Throw_When_TransportName_Is_Null()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => message.AddTransport(null!));
	}

	[Fact]
	public void AddTransport_Should_Throw_When_TransportName_Is_Empty()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => message.AddTransport(string.Empty));
	}

	[Fact]
	public void AddTransport_Should_Throw_When_TransportName_Is_Whitespace()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => message.AddTransport("   "));
	}

	#endregion

	#region GetTransportDelivery Tests

	[Fact]
	public void GetTransportDelivery_Should_Return_Delivery_When_Found()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		_ = message.AddTransport("rabbitmq");

		// Act
		var delivery = message.GetTransportDelivery("rabbitmq");

		// Assert
		_ = delivery.ShouldNotBeNull();
		delivery.TransportName.ShouldBe("rabbitmq");
	}

	[Fact]
	public void GetTransportDelivery_Should_Return_Null_When_Not_Found()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		_ = message.AddTransport("rabbitmq");

		// Act
		var delivery = message.GetTransportDelivery("kafka");

		// Assert
		delivery.ShouldBeNull();
	}

	[Fact]
	public void GetTransportDelivery_Should_Be_Case_Insensitive()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		_ = message.AddTransport("RabbitMQ");

		// Act
		var delivery = message.GetTransportDelivery("rabbitmq");

		// Assert
		_ = delivery.ShouldNotBeNull();
	}

	#endregion

	#region GetPendingTransportDeliveries Tests

	[Fact]
	public void GetPendingTransportDeliveries_Should_Return_Only_Pending()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		var delivery1 = message.AddTransport("rabbitmq");
		var delivery2 = message.AddTransport("kafka");
		delivery1.MarkSent();

		// Act
		var pending = message.GetPendingTransportDeliveries().ToList();

		// Assert
		pending.Count.ShouldBe(1);
		pending[0].TransportName.ShouldBe("kafka");
	}

	#endregion

	#region GetFailedTransportDeliveries Tests

	[Fact]
	public void GetFailedTransportDeliveries_Should_Return_Only_Failed()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		var delivery1 = message.AddTransport("rabbitmq");
		var delivery2 = message.AddTransport("kafka");
		delivery1.MarkFailed("Connection error");

		// Act
		var failed = message.GetFailedTransportDeliveries().ToList();

		// Assert
		failed.Count.ShouldBe(1);
		failed[0].TransportName.ShouldBe("rabbitmq");
	}

	#endregion

	#region AreAllTransportsComplete Tests

	[Fact]
	public void AreAllTransportsComplete_Should_Return_True_When_Not_MultiTransport_And_Sent()
	{
		// Arrange
		var message = new OutboundMessage();
		message.MarkSent();

		// Act & Assert
		message.AreAllTransportsComplete().ShouldBeTrue();
	}

	[Fact]
	public void AreAllTransportsComplete_Should_Return_False_When_Not_MultiTransport_And_Not_Sent()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act & Assert
		message.AreAllTransportsComplete().ShouldBeFalse();
	}

	[Fact]
	public void AreAllTransportsComplete_Should_Return_True_When_All_Transports_Sent()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		var delivery1 = message.AddTransport("rabbitmq");
		var delivery2 = message.AddTransport("kafka");
		delivery1.MarkSent();
		delivery2.MarkSent();

		// Act & Assert
		message.AreAllTransportsComplete().ShouldBeTrue();
	}

	[Fact]
	public void AreAllTransportsComplete_Should_Return_False_When_Some_Pending()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		var delivery1 = message.AddTransport("rabbitmq");
		_ = message.AddTransport("kafka");
		delivery1.MarkSent();

		// Act & Assert
		message.AreAllTransportsComplete().ShouldBeFalse();
	}

	#endregion

	#region UpdateAggregateStatus Tests

	[Fact]
	public void UpdateAggregateStatus_Should_Do_Nothing_When_Not_MultiTransport()
	{
		// Arrange
		var message = new OutboundMessage();

		// Act
		message.UpdateAggregateStatus();

		// Assert
		message.Status.ShouldBe(OutboxStatus.Staged);
	}

	[Fact]
	public void UpdateAggregateStatus_Should_Set_Sent_When_All_Complete()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		var delivery = message.AddTransport("rabbitmq");
		delivery.MarkSent();

		// Act
		message.UpdateAggregateStatus();

		// Assert
		message.Status.ShouldBe(OutboxStatus.Sent);
		_ = message.SentAt.ShouldNotBeNull();
	}

	[Fact]
	public void UpdateAggregateStatus_Should_Set_Sending_When_Any_Sending()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		var delivery1 = message.AddTransport("rabbitmq");
		_ = message.AddTransport("kafka");
		delivery1.MarkSending();

		// Act
		message.UpdateAggregateStatus();

		// Assert
		message.Status.ShouldBe(OutboxStatus.Sending);
	}

	[Fact]
	public void UpdateAggregateStatus_Should_Set_Failed_When_All_Failed()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		var delivery1 = message.AddTransport("rabbitmq");
		var delivery2 = message.AddTransport("kafka");
		delivery1.MarkFailed("Error 1");
		delivery2.MarkFailed("Error 2");

		// Act
		message.UpdateAggregateStatus();

		// Assert
		message.Status.ShouldBe(OutboxStatus.Failed);
		message.LastError.ShouldBe("All transport deliveries failed");
	}

	[Fact]
	public void UpdateAggregateStatus_Should_Set_PartiallyFailed_When_Some_Failed()
	{
		// Arrange
		var message = new OutboundMessage { Destination = "queue" };
		var delivery1 = message.AddTransport("rabbitmq");
		var delivery2 = message.AddTransport("kafka");
		delivery1.MarkFailed("Error");
		delivery2.MarkSent();

		// Act
		message.UpdateAggregateStatus();

		// Assert
		message.Status.ShouldBe(OutboxStatus.PartiallyFailed);
		message.LastError.ShouldContain("1 of 2 transports failed");
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_Should_Include_Id_MessageType_Destination_And_Status()
	{
		// Arrange
		var message = new OutboundMessage
		{
			Id = "test-id",
			MessageType = "Order.Created",
			Destination = "orders-queue"
		};

		// Act
		var result = message.ToString();

		// Assert
		result.ShouldContain("test-id");
		result.ShouldContain("Order.Created");
		result.ShouldContain("orders-queue");
		result.ShouldContain("Staged");
	}

	#endregion

	#region Headers Dictionary Tests

	[Fact]
	public void Headers_Should_Use_Ordinal_String_Comparer()
	{
		// Arrange
		var headers = new Dictionary<string, object> { ["Key"] = "value1" };
		var payload = new byte[] { 0x01 };
		var message = new OutboundMessage("Type", payload, "dest", headers);

		// Act
		message.Headers["key"] = "value2";

		// Assert - Ordinal comparer means case-sensitive
		message.Headers.Count.ShouldBe(2);
	}

	#endregion
}
