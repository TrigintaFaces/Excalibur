// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="OutboundMessageTransport"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboundMessageTransportShould
{
	#region Constructor Tests

	[Fact]
	public void DefaultConstructor_InitializesWithPendingStatus()
	{
		// Act
		var transport = new OutboundMessageTransport();

		// Assert
		transport.Status.ShouldBe(TransportDeliveryStatus.Pending);
		transport.Id.ShouldNotBeNullOrEmpty();
		transport.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void ParameterizedConstructor_SetsMessageIdAndTransportName()
	{
		// Act
		var transport = new OutboundMessageTransport("msg-123", "rabbitmq");

		// Assert
		transport.MessageId.ShouldBe("msg-123");
		transport.TransportName.ShouldBe("rabbitmq");
		transport.Status.ShouldBe(TransportDeliveryStatus.Pending);
	}

	[Fact]
	public void ParameterizedConstructor_ThrowsOnNullMessageId()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new OutboundMessageTransport(null!, "rabbitmq"));
	}

	[Fact]
	public void ParameterizedConstructor_ThrowsOnNullTransportName()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new OutboundMessageTransport("msg-1", null!));
	}

	#endregion

	#region MarkSending Tests

	[Fact]
	public void MarkSending_SetsStatusToSending()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act
		transport.MarkSending();

		// Assert
		transport.Status.ShouldBe(TransportDeliveryStatus.Sending);
		transport.AttemptedAt.ShouldNotBeNull();
	}

	#endregion

	#region MarkSent Tests

	[Fact]
	public void MarkSent_SetsStatusToSent()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act
		transport.MarkSent();

		// Assert
		transport.Status.ShouldBe(TransportDeliveryStatus.Sent);
		transport.SentAt.ShouldNotBeNull();
		transport.LastError.ShouldBeNull();
	}

	[Fact]
	public void MarkSent_ClearsLastError()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");
		transport.MarkFailed("Previous error");

		// Act
		transport.MarkSent();

		// Assert
		transport.LastError.ShouldBeNull();
	}

	#endregion

	#region MarkFailed Tests

	[Fact]
	public void MarkFailed_SetsStatusToFailed()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act
		transport.MarkFailed("Connection timeout");

		// Assert
		transport.Status.ShouldBe(TransportDeliveryStatus.Failed);
		transport.LastError.ShouldBe("Connection timeout");
		transport.RetryCount.ShouldBe(1);
		transport.AttemptedAt.ShouldNotBeNull();
	}

	[Fact]
	public void MarkFailed_IncrementsRetryCount()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act
		transport.MarkFailed("Error 1");
		transport.MarkFailed("Error 2");
		transport.MarkFailed("Error 3");

		// Assert
		transport.RetryCount.ShouldBe(3);
	}

	[Fact]
	public void MarkFailed_ThrowsOnNullOrEmptyError()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act & Assert
		Should.Throw<ArgumentException>(() => transport.MarkFailed(null!));
		Should.Throw<ArgumentException>(() => transport.MarkFailed(""));
	}

	#endregion

	#region MarkSkipped Tests

	[Fact]
	public void MarkSkipped_SetsStatusToSkipped()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act
		transport.MarkSkipped("Not applicable");

		// Assert
		transport.Status.ShouldBe(TransportDeliveryStatus.Skipped);
		transport.LastError.ShouldBe("Not applicable");
	}

	[Fact]
	public void MarkSkipped_WithNullReason_SetsStatusToSkipped()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act
		transport.MarkSkipped();

		// Assert
		transport.Status.ShouldBe(TransportDeliveryStatus.Skipped);
		transport.LastError.ShouldBeNull();
	}

	#endregion

	#region IsEligibleForRetry Tests

	[Fact]
	public void IsEligibleForRetry_ReturnsFalse_WhenNotFailed()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act & Assert
		transport.IsEligibleForRetry().ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_ReturnsFalse_WhenMaxRetriesExceeded()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");
		transport.MarkFailed("err1");
		transport.MarkFailed("err2");
		transport.MarkFailed("err3");

		// Act & Assert (default maxRetries=3, retryCount is now 3)
		transport.IsEligibleForRetry(maxRetries: 3).ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_ReturnsTrue_WhenFailedAndUnderMaxRetries()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");
		transport.MarkFailed("err1");

		// Force the AttemptedAt to the past
		transport.AttemptedAt = DateTimeOffset.UtcNow.AddMinutes(-10);

		// Act & Assert
		transport.IsEligibleForRetry(maxRetries: 3, retryDelayMinutes: 5).ShouldBeTrue();
	}

	[Fact]
	public void IsEligibleForRetry_ReturnsFalse_WhenRetryDelayNotElapsed()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");
		transport.MarkFailed("err1");
		// AttemptedAt is set to now by MarkFailed

		// Act & Assert - delay not elapsed
		transport.IsEligibleForRetry(maxRetries: 3, retryDelayMinutes: 5).ShouldBeFalse();
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ContainsRelevantInfo()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-123", "rabbitmq");

		// Act
		var str = transport.ToString();

		// Assert
		str.ShouldContain("msg-123");
		str.ShouldContain("rabbitmq");
		str.ShouldContain("Pending");
	}

	#endregion

	#region Properties Tests

	[Fact]
	public void Destination_CanBeSet()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act
		transport.Destination = "orders-topic";

		// Assert
		transport.Destination.ShouldBe("orders-topic");
	}

	[Fact]
	public void TransportMetadata_CanBeSet()
	{
		// Arrange
		var transport = new OutboundMessageTransport("msg-1", "kafka");

		// Act
		transport.TransportMetadata = "{\"partition\":3}";

		// Assert
		transport.TransportMetadata.ShouldBe("{\"partition\":3}");
	}

	#endregion
}
