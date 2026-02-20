// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PoisonMessageTypesShould
{
	// --- PoisonDetectionResult ---

	[Fact]
	public void PoisonDetectionResult_PoisonFactory_SetPropertiesCorrectly()
	{
		// Arrange
		var details = new Dictionary<string, object> { ["retries"] = 5 };

		// Act
		var result = PoisonDetectionResult.Poison("Too many retries", "RetryDetector", details);

		// Assert
		result.IsPoison.ShouldBeTrue();
		result.Reason.ShouldBe("Too many retries");
		result.DetectorName.ShouldBe("RetryDetector");
		result.Details.ShouldContainKey("retries");
		result.Details["retries"].ShouldBe(5);
	}

	[Fact]
	public void PoisonDetectionResult_PoisonFactory_DefaultDetailsToEmptyDictionary()
	{
		// Act
		var result = PoisonDetectionResult.Poison("reason", "detector");

		// Assert
		result.IsPoison.ShouldBeTrue();
		result.Details.ShouldNotBeNull();
		result.Details.ShouldBeEmpty();
	}

	[Fact]
	public void PoisonDetectionResult_NotPoisonFactory_SetIsNotPoison()
	{
		// Act
		var result = PoisonDetectionResult.NotPoison();

		// Assert
		result.IsPoison.ShouldBeFalse();
		result.Reason.ShouldBeNull();
		result.DetectorName.ShouldBeNull();
	}

	[Fact]
	public void PoisonDetectionResult_DefaultConstructor_DefaultValues()
	{
		// Act
		var result = new PoisonDetectionResult();

		// Assert
		result.IsPoison.ShouldBeFalse();
		result.Reason.ShouldBeNull();
		result.DetectorName.ShouldBeNull();
		result.Details.ShouldNotBeNull();
		result.Details.ShouldBeEmpty();
	}

	[Fact]
	public void PoisonDetectionResult_SetAllProperties()
	{
		// Act
		var result = new PoisonDetectionResult
		{
			IsPoison = true,
			Reason = "test reason",
			DetectorName = "TestDetector",
			Details = new Dictionary<string, object> { ["key"] = "value" },
		};

		// Assert
		result.IsPoison.ShouldBeTrue();
		result.Reason.ShouldBe("test reason");
		result.DetectorName.ShouldBe("TestDetector");
		result.Details["key"].ShouldBe("value");
	}

	// --- PoisonMessageStatistics ---

	[Fact]
	public void PoisonMessageStatistics_DefaultValues()
	{
		// Act
		var stats = new PoisonMessageStatistics();

		// Assert
		stats.TotalCount.ShouldBe(0);
		stats.RecentCount.ShouldBe(0);
		stats.TimeWindow.ShouldBe(TimeSpan.Zero);
		stats.MessagesByType.ShouldNotBeNull();
		stats.MessagesByType.ShouldBeEmpty();
		stats.MessagesByReason.ShouldNotBeNull();
		stats.MessagesByReason.ShouldBeEmpty();
		stats.OldestMessageDate.ShouldBeNull();
		stats.NewestMessageDate.ShouldBeNull();
	}

	[Fact]
	public void PoisonMessageStatistics_SetAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var earlier = now.AddHours(-2);

		// Act
		var stats = new PoisonMessageStatistics
		{
			TotalCount = 42,
			RecentCount = 5,
			TimeWindow = TimeSpan.FromHours(1),
			MessagesByType = new Dictionary<string, int> { ["OrderCreated"] = 3, ["PaymentFailed"] = 2 },
			MessagesByReason = new Dictionary<string, int> { ["MaxRetries"] = 4, ["Timeout"] = 1 },
			OldestMessageDate = earlier,
			NewestMessageDate = now,
		};

		// Assert
		stats.TotalCount.ShouldBe(42);
		stats.RecentCount.ShouldBe(5);
		stats.TimeWindow.ShouldBe(TimeSpan.FromHours(1));
		stats.MessagesByType.Count.ShouldBe(2);
		stats.MessagesByType["OrderCreated"].ShouldBe(3);
		stats.MessagesByReason.Count.ShouldBe(2);
		stats.OldestMessageDate.ShouldBe(earlier);
		stats.NewestMessageDate.ShouldBe(now);
	}

	// --- ProcessingAttempt ---

	[Fact]
	public void ProcessingAttempt_DefaultValues()
	{
		// Act
		var attempt = new ProcessingAttempt();

		// Assert
		attempt.AttemptNumber.ShouldBe(0);
		attempt.AttemptTime.ShouldBe(default);
		attempt.Duration.ShouldBe(TimeSpan.Zero);
		attempt.Succeeded.ShouldBeFalse();
		attempt.ErrorMessage.ShouldBeNull();
		attempt.ExceptionType.ShouldBeNull();
	}

	[Fact]
	public void ProcessingAttempt_SetAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var attempt = new ProcessingAttempt
		{
			AttemptNumber = 3,
			AttemptTime = now,
			Duration = TimeSpan.FromMilliseconds(150),
			Succeeded = false,
			ErrorMessage = "Connection timed out",
			ExceptionType = "System.TimeoutException",
		};

		// Assert
		attempt.AttemptNumber.ShouldBe(3);
		attempt.AttemptTime.ShouldBe(now);
		attempt.Duration.ShouldBe(TimeSpan.FromMilliseconds(150));
		attempt.Succeeded.ShouldBeFalse();
		attempt.ErrorMessage.ShouldBe("Connection timed out");
		attempt.ExceptionType.ShouldBe("System.TimeoutException");
	}

	[Fact]
	public void ProcessingAttempt_SuccessfulAttempt()
	{
		// Act
		var attempt = new ProcessingAttempt
		{
			AttemptNumber = 1,
			Succeeded = true,
			Duration = TimeSpan.FromMilliseconds(50),
		};

		// Assert
		attempt.Succeeded.ShouldBeTrue();
		attempt.ErrorMessage.ShouldBeNull();
		attempt.ExceptionType.ShouldBeNull();
	}

	// --- MessageProcessingInfo ---

	[Fact]
	public void MessageProcessingInfo_DefaultValues()
	{
		// Act
		var info = new MessageProcessingInfo();

		// Assert
		info.AttemptCount.ShouldBe(0);
		info.FirstAttemptTime.ShouldBe(default);
		info.CurrentAttemptTime.ShouldBe(default);
		info.TotalProcessingTime.ShouldBe(TimeSpan.Zero);
		info.ProcessingHistory.ShouldNotBeNull();
		info.ProcessingHistory.ShouldBeEmpty();
	}

	[Fact]
	public void MessageProcessingInfo_SetAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var firstAttempt = now.AddMinutes(-10);

		// Act
		var info = new MessageProcessingInfo
		{
			AttemptCount = 3,
			FirstAttemptTime = firstAttempt,
			CurrentAttemptTime = now,
			TotalProcessingTime = TimeSpan.FromSeconds(5),
		};

		info.ProcessingHistory.Add(new ProcessingAttempt { AttemptNumber = 1 });
		info.ProcessingHistory.Add(new ProcessingAttempt { AttemptNumber = 2 });
		info.ProcessingHistory.Add(new ProcessingAttempt { AttemptNumber = 3 });

		// Assert
		info.AttemptCount.ShouldBe(3);
		info.FirstAttemptTime.ShouldBe(firstAttempt);
		info.CurrentAttemptTime.ShouldBe(now);
		info.TotalProcessingTime.ShouldBe(TimeSpan.FromSeconds(5));
		info.ProcessingHistory.Count.ShouldBe(3);
	}

	// --- DeadLetterMessage ---

	[Fact]
	public void DeadLetterMessage_SetRequiredAndOptionalProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var msg = new DeadLetterMessage
		{
			MessageId = "msg-001",
			MessageType = "OrderCreated",
			MessageBody = "{\"orderId\":1}",
			MessageMetadata = "{\"source\":\"api\"}",
			Reason = "MaxRetriesExceeded",
			ExceptionDetails = "Timeout at Handler.Execute()",
			ProcessingAttempts = 5,
			FirstAttemptAt = now.AddMinutes(-30),
			LastAttemptAt = now.AddMinutes(-1),
			IsReplayed = false,
			ReplayedAt = null,
			SourceSystem = "orders-api",
			CorrelationId = "corr-001",
		};

		msg.Properties["custom-key"] = "custom-value";

		// Assert
		msg.Id.ShouldNotBeNullOrWhiteSpace();
		msg.MessageId.ShouldBe("msg-001");
		msg.MessageType.ShouldBe("OrderCreated");
		msg.MessageBody.ShouldBe("{\"orderId\":1}");
		msg.MessageMetadata.ShouldBe("{\"source\":\"api\"}");
		msg.Reason.ShouldBe("MaxRetriesExceeded");
		msg.ExceptionDetails.ShouldBe("Timeout at Handler.Execute()");
		msg.ProcessingAttempts.ShouldBe(5);
		msg.FirstAttemptAt.ShouldNotBeNull();
		msg.LastAttemptAt.ShouldNotBeNull();
		msg.IsReplayed.ShouldBeFalse();
		msg.ReplayedAt.ShouldBeNull();
		msg.SourceSystem.ShouldBe("orders-api");
		msg.CorrelationId.ShouldBe("corr-001");
		msg.Properties["custom-key"].ShouldBe("custom-value");
	}

	[Fact]
	public void DeadLetterMessage_DefaultId_IsNotEmpty()
	{
		// Act
		var msg = new DeadLetterMessage
		{
			MessageId = "test",
			MessageType = "Test",
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",
		};

		// Assert
		msg.Id.ShouldNotBeNullOrWhiteSpace();
		msg.Id.Length.ShouldBe(32); // Guid.ToString("N") = 32 hex chars
	}

	[Fact]
	public void DeadLetterMessage_DefaultMovedToDeadLetterAt_IsRecent()
	{
		// Act
		var msg = new DeadLetterMessage
		{
			MessageId = "test",
			MessageType = "Test",
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",
		};

		// Assert
		msg.MovedToDeadLetterAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
	}

	[Fact]
	public void DeadLetterMessage_DefaultProperties_IsEmptyDictionary()
	{
		// Act
		var msg = new DeadLetterMessage
		{
			MessageId = "test",
			MessageType = "Test",
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",
		};

		// Assert
		msg.Properties.ShouldNotBeNull();
		msg.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void DeadLetterMessage_ReplayedMessage_SetReplayFields()
	{
		// Arrange
		var replayedAt = DateTimeOffset.UtcNow;

		// Act
		var msg = new DeadLetterMessage
		{
			MessageId = "test",
			MessageType = "Test",
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "test",
			IsReplayed = true,
			ReplayedAt = replayedAt,
		};

		// Assert
		msg.IsReplayed.ShouldBeTrue();
		msg.ReplayedAt.ShouldBe(replayedAt);
	}

	// --- DeadLetterFilter ---

	[Fact]
	public void DeadLetterFilter_DefaultValues()
	{
		// Act
		var filter = new DeadLetterFilter();

		// Assert
		filter.MessageType.ShouldBeNull();
		filter.Reason.ShouldBeNull();
		filter.FromDate.ShouldBeNull();
		filter.ToDate.ShouldBeNull();
		filter.IsReplayed.ShouldBeNull();
		filter.SourceSystem.ShouldBeNull();
		filter.CorrelationId.ShouldBeNull();
		filter.MaxResults.ShouldBe(100);
		filter.Skip.ShouldBe(0);
	}

	[Fact]
	public void DeadLetterFilter_SetAllProperties()
	{
		// Arrange
		var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var to = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

		// Act
		var filter = new DeadLetterFilter
		{
			MessageType = "OrderCreated",
			Reason = "Timeout",
			FromDate = from,
			ToDate = to,
			IsReplayed = false,
			SourceSystem = "orders-api",
			CorrelationId = "corr-123",
			MaxResults = 50,
			Skip = 10,
		};

		// Assert
		filter.MessageType.ShouldBe("OrderCreated");
		filter.Reason.ShouldBe("Timeout");
		filter.FromDate.ShouldBe(from);
		filter.ToDate.ShouldBe(to);
		filter.IsReplayed.ShouldBe(false);
		filter.SourceSystem.ShouldBe("orders-api");
		filter.CorrelationId.ShouldBe("corr-123");
		filter.MaxResults.ShouldBe(50);
		filter.Skip.ShouldBe(10);
	}
}
