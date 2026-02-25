// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.StorageQueues;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class StorageQueueAbstractionsShould
{
	[Fact]
	public void DeadLetterQueueStatisticsHaveCorrectDefaults()
	{
		// Arrange & Act
		var stats = new DeadLetterQueueStatistics();

		// Assert
		stats.TotalMessages.ShouldBe(0);
		stats.MessagesLastHour.ShouldBe(0);
		stats.MessagesLastDay.ShouldBe(0);
		stats.ReasonCounts.ShouldBeEmpty();
		stats.Timestamp.ShouldNotBe(default);
		stats.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void DeadLetterQueueStatisticsAllowSettingAllProperties()
	{
		// Arrange
		var reasons = new Dictionary<string, long>(StringComparer.Ordinal)
		{
			["MaxDeliveryExceeded"] = 10,
			["Timeout"] = 5,
		};
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var stats = new DeadLetterQueueStatistics
		{
			TotalMessages = 100,
			MessagesLastHour = 15,
			MessagesLastDay = 50,
			ReasonCounts = reasons,
			Timestamp = timestamp,
			IsHealthy = true,
		};

		// Assert
		stats.TotalMessages.ShouldBe(100);
		stats.MessagesLastHour.ShouldBe(15);
		stats.MessagesLastDay.ShouldBe(50);
		stats.ReasonCounts.Count.ShouldBe(2);
		stats.ReasonCounts["MaxDeliveryExceeded"].ShouldBe(10);
		stats.Timestamp.ShouldBe(timestamp);
		stats.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void DeadLetterQueueStatisticsSupportRecordEquality()
	{
		// Arrange
		var reasons = new Dictionary<string, long>(StringComparer.Ordinal);
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var s1 = new DeadLetterQueueStatistics
		{
			TotalMessages = 10,
			ReasonCounts = reasons,
			Timestamp = timestamp,
		};
		var s2 = new DeadLetterQueueStatistics
		{
			TotalMessages = 10,
			ReasonCounts = reasons,
			Timestamp = timestamp,
		};

		// Assert — record reference equality for collections
		s1.ShouldBe(s2);
	}

	[Fact]
	public void QueueMetricsSnapshotHaveCorrectDefaults()
	{
		// Arrange & Act
		var snapshot = new QueueMetricsSnapshot();

		// Assert
		snapshot.TotalMessagesProcessed.ShouldBe(0);
		snapshot.SuccessfulMessages.ShouldBe(0);
		snapshot.FailedMessages.ShouldBe(0);
		snapshot.AverageProcessingTimeMs.ShouldBe(0.0);
		snapshot.TotalBatchesProcessed.ShouldBe(0);
		snapshot.AverageBatchSize.ShouldBe(0.0);
		snapshot.TotalReceiveOperations.ShouldBe(0);
		snapshot.AverageReceiveTimeMs.ShouldBe(0.0);
		snapshot.TotalDeleteOperations.ShouldBe(0);
		snapshot.SuccessfulDeletes.ShouldBe(0);
		snapshot.TotalVisibilityUpdates.ShouldBe(0);
		snapshot.SuccessfulVisibilityUpdates.ShouldBe(0);
		snapshot.Timestamp.ShouldNotBe(default);
	}

	[Fact]
	public void QueueMetricsSnapshotAllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var snapshot = new QueueMetricsSnapshot
		{
			TotalMessagesProcessed = 1000,
			SuccessfulMessages = 980,
			FailedMessages = 20,
			AverageProcessingTimeMs = 45.5,
			TotalBatchesProcessed = 100,
			AverageBatchSize = 10.0,
			TotalReceiveOperations = 200,
			AverageReceiveTimeMs = 5.0,
			TotalDeleteOperations = 980,
			SuccessfulDeletes = 975,
			TotalVisibilityUpdates = 50,
			SuccessfulVisibilityUpdates = 48,
			Timestamp = timestamp,
		};

		// Assert
		snapshot.TotalMessagesProcessed.ShouldBe(1000);
		snapshot.SuccessfulMessages.ShouldBe(980);
		snapshot.FailedMessages.ShouldBe(20);
		snapshot.AverageProcessingTimeMs.ShouldBe(45.5);
		snapshot.TotalBatchesProcessed.ShouldBe(100);
		snapshot.AverageBatchSize.ShouldBe(10.0);
		snapshot.TotalReceiveOperations.ShouldBe(200);
		snapshot.AverageReceiveTimeMs.ShouldBe(5.0);
		snapshot.TotalDeleteOperations.ShouldBe(980);
		snapshot.SuccessfulDeletes.ShouldBe(975);
		snapshot.TotalVisibilityUpdates.ShouldBe(50);
		snapshot.SuccessfulVisibilityUpdates.ShouldBe(48);
		snapshot.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void StorageQueueMessageEnvelopeStoreAllFields()
	{
		// Arrange
		var body = new byte[] { 1, 2, 3 };
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var envelope = new StorageQueueMessageEnvelope
		{
			MessageType = "OrderCreated",
			MessageId = "msg-123",
			CorrelationId = "corr-456",
			Timestamp = timestamp,
			Body = body,
		};

		// Assert
		envelope.MessageType.ShouldBe("OrderCreated");
		envelope.MessageId.ShouldBe("msg-123");
		envelope.CorrelationId.ShouldBe("corr-456");
		envelope.Timestamp.ShouldBe(timestamp);
		envelope.Body.ShouldBe(body);
		envelope.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void StorageQueueMessageEnvelopeAllowCustomProperties()
	{
		// Arrange & Act
		var envelope = new StorageQueueMessageEnvelope
		{
			MessageType = "Event",
			MessageId = "msg-1",
			Timestamp = DateTimeOffset.UtcNow,
			Body = [],
			Properties = { ["key1"] = "val1", ["key2"] = null },
		};

		// Assert
		envelope.Properties.Count.ShouldBe(2);
		envelope.Properties["key1"].ShouldBe("val1");
		envelope.Properties["key2"].ShouldBeNull();
	}

	[Fact]
	public void StorageQueueMessageEnvelopeAllowNullCorrelationId()
	{
		// Act
		var envelope = new StorageQueueMessageEnvelope
		{
			MessageType = "Event",
			MessageId = "msg-1",
			Timestamp = DateTimeOffset.UtcNow,
			Body = [],
		};

		// Assert
		envelope.CorrelationId.ShouldBeNull();
	}
}
