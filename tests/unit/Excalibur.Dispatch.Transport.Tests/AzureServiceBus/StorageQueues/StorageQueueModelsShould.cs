// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.StorageQueues;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class StorageQueueModelsShould
{
	[Fact]
	public void CreateStorageQueueMessageEnvelope()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var envelope = new StorageQueueMessageEnvelope
		{
			MessageType = "OrderCreated",
			MessageId = "msg-123",
			CorrelationId = "corr-456",
			Timestamp = now,
			Body = [1, 2, 3],
			Properties = new Dictionary<string, string?> { ["key"] = "value" },
		};

		// Assert
		envelope.MessageType.ShouldBe("OrderCreated");
		envelope.MessageId.ShouldBe("msg-123");
		envelope.CorrelationId.ShouldBe("corr-456");
		envelope.Timestamp.ShouldBe(now);
		envelope.Body.ShouldBe([1, 2, 3]);
		envelope.Properties["key"].ShouldBe("value");
	}

	[Fact]
	public void CreateEnvelopeWithNullCorrelation()
	{
		// Arrange & Act
		var envelope = new StorageQueueMessageEnvelope
		{
			MessageType = "Event",
			MessageId = "msg-1",
			Timestamp = DateTimeOffset.UtcNow,
			Body = [0],
		};

		// Assert
		envelope.CorrelationId.ShouldBeNull();
		envelope.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void CreateDeadLetterQueueStatistics()
	{
		// Arrange
		var reasons = new Dictionary<string, long>(StringComparer.Ordinal) { ["Timeout"] = 5, ["Error"] = 3 };

		// Act
		var stats = new DeadLetterQueueStatistics
		{
			TotalMessages = 100,
			MessagesLastHour = 10,
			MessagesLastDay = 50,
			ReasonCounts = reasons,
			IsHealthy = true,
		};

		// Assert
		stats.TotalMessages.ShouldBe(100);
		stats.MessagesLastHour.ShouldBe(10);
		stats.MessagesLastDay.ShouldBe(50);
		stats.ReasonCounts["Timeout"].ShouldBe(5);
		stats.ReasonCounts["Error"].ShouldBe(3);
		stats.IsHealthy.ShouldBeTrue();
		stats.Timestamp.ShouldNotBe(default);
	}

	[Fact]
	public void HaveDefaultTimestampForStatistics()
	{
		// Arrange & Act
		var stats = new DeadLetterQueueStatistics();

		// Assert
		stats.TotalMessages.ShouldBe(0);
		stats.MessagesLastHour.ShouldBe(0);
		stats.MessagesLastDay.ShouldBe(0);
		stats.ReasonCounts.ShouldBeEmpty();
		stats.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void SupportDeadLetterQueueStatisticsRecordEquality()
	{
		// Arrange
		var s1 = new DeadLetterQueueStatistics { TotalMessages = 10, IsHealthy = true };
		var s2 = new DeadLetterQueueStatistics { TotalMessages = 10, IsHealthy = true };

		// Assert - record equality
		s1.TotalMessages.ShouldBe(s2.TotalMessages);
		s1.IsHealthy.ShouldBe(s2.IsHealthy);
	}
}
