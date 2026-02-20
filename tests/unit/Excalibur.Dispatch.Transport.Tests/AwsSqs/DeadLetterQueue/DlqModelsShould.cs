// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DlqModelsShould
{
	[Fact]
	public void DlqActionEnumHaveCorrectValues()
	{
		// Assert
		((int)DlqAction.None).ShouldBe(0);
		((int)DlqAction.Redriven).ShouldBe(1);
		((int)DlqAction.RetryFailed).ShouldBe(2);
		((int)DlqAction.Archived).ShouldBe(3);
		((int)DlqAction.Deleted).ShouldBe(4);
		((int)DlqAction.Skipped).ShouldBe(5);
	}

	[Fact]
	public void DlqAnalysisResultHaveCorrectDefaults()
	{
		// Arrange & Act
		var result = new DlqAnalysisResult();

		// Assert
		result.ShouldMoveToDeadLetter.ShouldBeFalse();
		result.Reason.ShouldBeNull();
		result.IsRecoverable.ShouldBeFalse();
		result.RecommendedAction.ShouldBe(DlqAction.None);
		result.SuggestedRetryDelay.ShouldBeNull();
	}

	[Fact]
	public void DlqAnalysisResultAllowSettingAllProperties()
	{
		// Arrange & Act
		var result = new DlqAnalysisResult
		{
			ShouldMoveToDeadLetter = true,
			Reason = "Max retries exceeded",
			IsRecoverable = true,
			RecommendedAction = DlqAction.Redriven,
			SuggestedRetryDelay = TimeSpan.FromSeconds(30),
		};

		// Assert
		result.ShouldMoveToDeadLetter.ShouldBeTrue();
		result.Reason.ShouldBe("Max retries exceeded");
		result.IsRecoverable.ShouldBeTrue();
		result.RecommendedAction.ShouldBe(DlqAction.Redriven);
		result.SuggestedRetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void DlqMessageHaveCorrectDefaults()
	{
		// Arrange & Act
		var msg = new DlqMessage { MessageId = "msg-1", Body = "body" };

		// Assert
		msg.MessageId.ShouldBe("msg-1");
		msg.Body.ShouldBe("body");
		msg.ReceiptHandle.ShouldBeNull();
		msg.SourceQueueUrl.ShouldBeNull();
		msg.AttemptCount.ShouldBe(0);
		msg.Attributes.ShouldBeEmpty();
		msg.Metadata.ShouldBeEmpty();
		msg.LastError.ShouldBeNull();
		msg.DlqReason.ShouldBeNull();
	}

	[Fact]
	public void DlqMessageAllowSettingAllProperties()
	{
		// Arrange
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123/dlq");
		var now = DateTime.UtcNow;

		// Act
		var msg = new DlqMessage
		{
			MessageId = "msg-2",
			Body = "{\"key\":\"value\"}",
			ReceiptHandle = "receipt-123",
			SourceQueueUrl = queueUrl,
			AttemptCount = 3,
			FirstSentTimestamp = now,
			MovedToDlqTimestamp = now,
			LastError = "Timeout",
			DlqReason = "Exceeded max retries",
		};
		msg.Attributes["type"] = "order.created";
		msg.Metadata["correlationId"] = "corr-1";

		// Assert
		msg.SourceQueueUrl.ShouldBe(queueUrl);
		msg.AttemptCount.ShouldBe(3);
		msg.FirstSentTimestamp.ShouldBe(now);
		msg.MovedToDlqTimestamp.ShouldBe(now);
		msg.LastError.ShouldBe("Timeout");
		msg.DlqReason.ShouldBe("Exceeded max retries");
		msg.Attributes.Count.ShouldBe(1);
		msg.Metadata.Count.ShouldBe(1);
	}

	[Fact]
	public void DlqProcessingResultHaveCorrectDefaults()
	{
		// Arrange & Act
		var result = new DlqProcessingResult { MessageId = "msg-1" };

		// Assert
		result.Success.ShouldBeFalse();
		result.MessageId.ShouldBe("msg-1");
		result.Action.ShouldBe(DlqAction.None);
		result.ErrorMessage.ShouldBeNull();
		result.ProcessedAt.ShouldNotBe(default);
		result.RetryAttempts.ShouldBe(0);
		result.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void DlqProcessingResultAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var result = new DlqProcessingResult
		{
			MessageId = "msg-2",
			Success = true,
			Action = DlqAction.Redriven,
			ErrorMessage = "Transient failure",
			ProcessedAt = now,
			RetryAttempts = 2,
		};
		result.Metadata["destination"] = "original-queue";

		// Assert
		result.Success.ShouldBeTrue();
		result.Action.ShouldBe(DlqAction.Redriven);
		result.ErrorMessage.ShouldBe("Transient failure");
		result.ProcessedAt.ShouldBe(now);
		result.RetryAttempts.ShouldBe(2);
		result.Metadata.Count.ShouldBe(1);
	}

	[Fact]
	public void DlqStatisticsHaveCorrectDefaults()
	{
		// Arrange & Act
		var stats = new DlqStatistics();

		// Assert
		stats.TotalMessages.ShouldBe(0);
		stats.MessagesByAge.ShouldBeEmpty();
		stats.MessagesByErrorType.ShouldBeEmpty();
		stats.OldestMessageTimestamp.ShouldBeNull();
		stats.NewestMessageTimestamp.ShouldBeNull();
		stats.AverageRetryCount.ShouldBe(0.0);
		stats.RedrivenToday.ShouldBe(0);
		stats.ArchivedToday.ShouldBe(0);
		stats.MessagesProcessed.ShouldBe(0);
		stats.MessagesRequeued.ShouldBe(0);
		stats.MessagesDiscarded.ShouldBe(0);
	}

	[Fact]
	public void DlqStatisticsAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var stats = new DlqStatistics
		{
			TotalMessages = 100,
			OldestMessageTimestamp = now.AddDays(-7),
			NewestMessageTimestamp = now,
			AverageRetryCount = 2.5,
			RedrivenToday = 10,
			ArchivedToday = 5,
			MessagesProcessed = 50,
			MessagesRequeued = 30,
			MessagesDiscarded = 15,
			GeneratedAt = DateTimeOffset.UtcNow,
		};
		stats.MessagesByAge["< 1h"] = 20;
		stats.MessagesByErrorType["Timeout"] = 40;

		// Assert
		stats.TotalMessages.ShouldBe(100);
		stats.OldestMessageTimestamp.ShouldBe(now.AddDays(-7));
		stats.AverageRetryCount.ShouldBe(2.5);
		stats.MessagesByAge.Count.ShouldBe(1);
		stats.MessagesByErrorType.Count.ShouldBe(1);
	}
}
