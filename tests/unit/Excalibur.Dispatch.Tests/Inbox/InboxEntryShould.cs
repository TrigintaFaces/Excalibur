// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Inbox;

/// <summary>
/// Unit tests for <see cref="InboxEntry"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class InboxEntryShould
{
	#region Constructor Tests

	[Fact]
	public void Create_WithDefaultConstructor_SetsDefaults()
	{
		// Arrange & Act
		var entry = new InboxEntry();

		// Assert
		entry.MessageId.ShouldBe(string.Empty);
		entry.HandlerType.ShouldBe(string.Empty);
		entry.MessageType.ShouldBe(string.Empty);
		entry.Payload.ShouldBeEmpty();
		entry.Status.ShouldBe(InboxStatus.Received);
		entry.ReceivedAt.ShouldNotBe(default);
	}

	[Fact]
	public void Create_WithAllParameters_SetsValues()
	{
		// Arrange
		var messageId = "msg-123";
		var handlerType = "MyHandler";
		var messageType = "MyMessage";
		var payload = new byte[] { 1, 2, 3 };
		var metadata = new Dictionary<string, object> { ["key"] = "value" };

		// Act
		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);

		// Assert
		entry.MessageId.ShouldBe(messageId);
		entry.HandlerType.ShouldBe(handlerType);
		entry.MessageType.ShouldBe(messageType);
		entry.Payload.ShouldBe(payload);
		entry.Metadata["key"].ShouldBe("value");
		entry.Status.ShouldBe(InboxStatus.Received);
	}

	[Fact]
	public void Create_WithoutMetadata_CreatesEmptyDictionary()
	{
		// Arrange & Act
		var entry = new InboxEntry("msg", "handler", "type", [1, 2, 3]);

		// Assert
		entry.Metadata.ShouldNotBeNull();
		entry.Metadata.Count.ShouldBe(0);
	}

	[Fact]
	public void Create_WithNullMessageId_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new InboxEntry(null!, "handler", "type", [1]));
	}

	[Fact]
	public void Create_WithNullHandlerType_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new InboxEntry("msg", null!, "type", [1]));
	}

	[Fact]
	public void Create_WithNullMessageType_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new InboxEntry("msg", "handler", null!, [1]));
	}

	[Fact]
	public void Create_WithNullPayload_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new InboxEntry("msg", "handler", "type", null!));
	}

	#endregion

	#region MarkProcessing Tests

	[Fact]
	public void MarkProcessing_SetsStatusToProcessing()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);

		// Act
		entry.MarkProcessing();

		// Assert
		entry.Status.ShouldBe(InboxStatus.Processing);
	}

	[Fact]
	public void MarkProcessing_SetsLastAttemptAt()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		var before = DateTimeOffset.UtcNow;

		// Act
		entry.MarkProcessing();
		var after = DateTimeOffset.UtcNow;

		// Assert
		entry.LastAttemptAt.ShouldNotBeNull();
		entry.LastAttemptAt.Value.ShouldBeInRange(before, after);
	}

	#endregion

	#region MarkProcessed Tests

	[Fact]
	public void MarkProcessed_SetsStatusToProcessed()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);

		// Act
		entry.MarkProcessed();

		// Assert
		entry.Status.ShouldBe(InboxStatus.Processed);
	}

	[Fact]
	public void MarkProcessed_SetsProcessedAt()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		var before = DateTimeOffset.UtcNow;

		// Act
		entry.MarkProcessed();
		var after = DateTimeOffset.UtcNow;

		// Assert
		entry.ProcessedAt.ShouldNotBeNull();
		entry.ProcessedAt.Value.ShouldBeInRange(before, after);
	}

	[Fact]
	public void MarkProcessed_ClearsLastError()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		entry.MarkFailed("Previous error");

		// Act
		entry.MarkProcessed();

		// Assert
		entry.LastError.ShouldBeNull();
	}

	#endregion

	#region MarkFailed Tests

	[Fact]
	public void MarkFailed_SetsStatusToFailed()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);

		// Act
		entry.MarkFailed("Error occurred");

		// Assert
		entry.Status.ShouldBe(InboxStatus.Failed);
	}

	[Fact]
	public void MarkFailed_SetsLastError()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);

		// Act
		entry.MarkFailed("Processing failed");

		// Assert
		entry.LastError.ShouldBe("Processing failed");
	}

	[Fact]
	public void MarkFailed_IncrementsRetryCount()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		entry.RetryCount.ShouldBe(0);

		// Act
		entry.MarkFailed("Error 1");
		entry.MarkFailed("Error 2");

		// Assert
		entry.RetryCount.ShouldBe(2);
	}

	[Fact]
	public void MarkFailed_SetsLastAttemptAt()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		var before = DateTimeOffset.UtcNow;

		// Act
		entry.MarkFailed("Error");
		var after = DateTimeOffset.UtcNow;

		// Assert
		entry.LastAttemptAt.ShouldNotBeNull();
		entry.LastAttemptAt.Value.ShouldBeInRange(before, after);
	}

	[Fact]
	public void MarkFailed_WithNullError_ThrowsArgumentException()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);

		// Act & Assert
		Should.Throw<ArgumentException>(() => entry.MarkFailed(null!));
	}

	[Fact]
	public void MarkFailed_WithEmptyError_ThrowsArgumentException()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);

		// Act & Assert
		Should.Throw<ArgumentException>(() => entry.MarkFailed(""));
	}

	#endregion

	#region IsEligibleForRetry Tests

	[Fact]
	public void IsEligibleForRetry_WhenNotFailed_ReturnsFalse()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		entry.Status = InboxStatus.Received;

		// Act & Assert
		entry.IsEligibleForRetry().ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_WhenProcessed_ReturnsFalse()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		entry.MarkProcessed();

		// Act & Assert
		entry.IsEligibleForRetry().ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_WhenFailedAndNoAttempts_ReturnsTrue()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		entry.Status = InboxStatus.Failed;
		entry.RetryCount = 0;

		// Act & Assert
		entry.IsEligibleForRetry().ShouldBeTrue();
	}

	[Fact]
	public void IsEligibleForRetry_WhenMaxRetriesExceeded_ReturnsFalse()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		entry.Status = InboxStatus.Failed;
		entry.RetryCount = 3;

		// Act & Assert
		entry.IsEligibleForRetry(maxRetries: 3).ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_WhenDelayNotMet_ReturnsFalse()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		entry.Status = InboxStatus.Failed;
		entry.RetryCount = 1;
		entry.LastAttemptAt = DateTimeOffset.UtcNow; // Just now

		// Act & Assert
		entry.IsEligibleForRetry(maxRetries: 3, retryDelayMinutes: 5).ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_WhenDelayMet_ReturnsTrue()
	{
		// Arrange
		var entry = new InboxEntry("msg", "handler", "type", [1]);
		entry.Status = InboxStatus.Failed;
		entry.RetryCount = 1;
		entry.LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-10); // 10 minutes ago

		// Act & Assert
		entry.IsEligibleForRetry(maxRetries: 3, retryDelayMinutes: 5).ShouldBeTrue();
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var entry = new InboxEntry("msg-123", "MyHandler", "MyMessage", [1]);

		// Act
		var result = entry.ToString();

		// Assert
		result.ShouldContain("msg-123");
		result.ShouldContain("MyHandler");
		result.ShouldContain("MyMessage");
		result.ShouldContain("Received");
	}

	#endregion

	#region Optional Properties Tests

	[Fact]
	public void OptionalProperties_DefaultToNull()
	{
		// Arrange & Act
		var entry = new InboxEntry();

		// Assert
		entry.ProcessedAt.ShouldBeNull();
		entry.LastError.ShouldBeNull();
		entry.LastAttemptAt.ShouldBeNull();
		entry.CorrelationId.ShouldBeNull();
		entry.TenantId.ShouldBeNull();
		entry.Source.ShouldBeNull();
	}

	[Fact]
	public void OptionalProperties_CanBeSet()
	{
		// Arrange
		var entry = new InboxEntry();

		// Act
		entry.CorrelationId = "corr-123";
		entry.TenantId = "tenant-456";
		entry.Source = "RabbitMQ";

		// Assert
		entry.CorrelationId.ShouldBe("corr-123");
		entry.TenantId.ShouldBe("tenant-456");
		entry.Source.ShouldBe("RabbitMQ");
	}

	#endregion
}
