// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.StreamingPull;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class StreamingPullModelsShould
{
	[Fact]
	public void CreateStreamingPullStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new StreamingPullStatistics();

		// Assert
		stats.ActiveStreamCount.ShouldBe(0);
		stats.TargetStreamCount.ShouldBe(0);
		stats.TotalMessagesReceived.ShouldBe(0);
		stats.TotalBytesReceived.ShouldBe(0);
		stats.TotalErrors.ShouldBe(0);
		stats.QueuedMessages.ShouldBe(0);
		stats.ActiveProcessingThreads.ShouldBe(0);
		stats.StreamHealthInfos.ShouldNotBeNull();
		stats.StreamHealthInfos.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingStreamingPullStatisticsProperties()
	{
		// Arrange & Act
		var healthInfo = new StreamHealthInfo("stream-1");
		var stats = new StreamingPullStatistics
		{
			ActiveStreamCount = 4,
			TargetStreamCount = 8,
			TotalMessagesReceived = 100000,
			TotalBytesReceived = 500_000_000,
			TotalErrors = 50,
			QueuedMessages = 100,
			ActiveProcessingThreads = 4,
			StreamHealthInfos = [healthInfo],
		};

		// Assert
		stats.ActiveStreamCount.ShouldBe(4);
		stats.TargetStreamCount.ShouldBe(8);
		stats.TotalMessagesReceived.ShouldBe(100000);
		stats.TotalBytesReceived.ShouldBe(500_000_000);
		stats.TotalErrors.ShouldBe(50);
		stats.QueuedMessages.ShouldBe(100);
		stats.ActiveProcessingThreads.ShouldBe(4);
		stats.StreamHealthInfos.Length.ShouldBe(1);
	}

	[Fact]
	public void CreateProcessorStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new ProcessorStatistics();

		// Assert
		stats.QueuedMessages.ShouldBe(0);
		stats.MaxQueueCapacity.ShouldBe(0);
		stats.ActiveProcessingThreads.ShouldBe(0);
		stats.IsShuttingDown.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingProcessorStatisticsProperties()
	{
		// Arrange & Act
		var stats = new ProcessorStatistics
		{
			QueuedMessages = 50,
			MaxQueueCapacity = 1000,
			ActiveProcessingThreads = 8,
			IsShuttingDown = true,
		};

		// Assert
		stats.QueuedMessages.ShouldBe(50);
		stats.MaxQueueCapacity.ShouldBe(1000);
		stats.ActiveProcessingThreads.ShouldBe(8);
		stats.IsShuttingDown.ShouldBeTrue();
	}

	[Fact]
	public void CreateStreamHealthInfoWithStreamId()
	{
		// Arrange & Act
		var info = new StreamHealthInfo("stream-42");

		// Assert
		info.StreamId.ShouldBe("stream-42");
		info.IsConnected.ShouldBeFalse();
		info.LastError.ShouldBeNull();
		info.MessagesReceived.ShouldBe(0);
		info.BytesReceived.ShouldBe(0);
		info.ErrorCount.ShouldBe(0);
		info.AcknowledgmentsSucceeded.ShouldBe(0);
		info.AcknowledgmentsFailed.ShouldBe(0);
		info.ReconnectCount.ShouldBe(0);
	}

	[Fact]
	public void ThrowWhenStreamHealthInfoStreamIdIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new StreamHealthInfo(null!));
	}

	[Fact]
	public void AllowSettingStreamHealthInfoProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var error = new InvalidOperationException("Connection lost");

		// Act
		var info = new StreamHealthInfo("stream-1")
		{
			IsConnected = true,
			ConnectedTime = now,
			LastMessageTime = now,
			LastError = error,
			MessagesReceived = 5000,
			BytesReceived = 1_000_000,
			ErrorCount = 3,
			AcknowledgmentsSucceeded = 4990,
			AcknowledgmentsFailed = 10,
			ReconnectCount = 2,
		};

		// Assert
		info.IsConnected.ShouldBeTrue();
		info.ConnectedTime.ShouldBe(now);
		info.LastError.ShouldBeSameAs(error);
		info.MessagesReceived.ShouldBe(5000);
		info.BytesReceived.ShouldBe(1_000_000);
		info.ErrorCount.ShouldBe(3);
		info.AcknowledgmentsSucceeded.ShouldBe(4990);
		info.AcknowledgmentsFailed.ShouldBe(10);
		info.ReconnectCount.ShouldBe(2);
	}

	[Fact]
	public void CreateBackoffOptionsWithDefaults()
	{
		// Arrange & Act
		var options = new BackoffOptions();

		// Assert
		options.InitialDelay.ShouldBe(TimeSpan.Zero);
		options.MaxDelay.ShouldBe(TimeSpan.Zero);
		options.Multiplier.ShouldBe(0.0);
		options.MaxAttempts.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingBackoffOptionsProperties()
	{
		// Arrange & Act
		var options = new BackoffOptions
		{
			InitialDelay = TimeSpan.FromMilliseconds(100),
			MaxDelay = TimeSpan.FromSeconds(30),
			Multiplier = 2.0,
			MaxAttempts = 10,
		};

		// Assert
		options.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.Multiplier.ShouldBe(2.0);
		options.MaxAttempts.ShouldBe(10);
	}

	[Fact]
	public void CreateAckDeadlineExtensionEventArgs()
	{
		// Arrange & Act
		var args = new AckDeadlineExtensionEventArgs("ack-123", 30);

		// Assert
		args.AckId.ShouldBe("ack-123");
		args.ExtensionSeconds.ShouldBe(30);
	}

	[Fact]
	public void CreateMessageEnqueuedEventArgs()
	{
		// Arrange & Act
		var args = new MessageEnqueuedEventArgs("stream-1", "msg-42");

		// Assert
		args.StreamId.ShouldBe("stream-1");
		args.MessageId.ShouldBe("msg-42");
	}

	[Fact]
	public void CreateMessageProcessedEventArgsForSuccess()
	{
		// Arrange & Act
		var args = new MessageProcessedEventArgs("msg-1", true, TimeSpan.FromMilliseconds(50));

		// Assert
		args.MessageId.ShouldBe("msg-1");
		args.Success.ShouldBeTrue();
		args.Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
		args.Error.ShouldBeNull();
	}

	[Fact]
	public void CreateMessageProcessedEventArgsForFailure()
	{
		// Arrange
		var error = new InvalidOperationException("Processing failed");

		// Act
		var args = new MessageProcessedEventArgs("msg-2", false, TimeSpan.FromMilliseconds(100), error);

		// Assert
		args.MessageId.ShouldBe("msg-2");
		args.Success.ShouldBeFalse();
		args.Duration.ShouldBe(TimeSpan.FromMilliseconds(100));
		args.Error.ShouldBeSameAs(error);
	}

	[Fact]
	public void CreateAckErrorRecord()
	{
		// Arrange & Act
		var error = new AckError("ack-1", "DEADLINE_EXCEEDED");

		// Assert
		error.AckId.ShouldBe("ack-1");
		error.Message.ShouldBe("DEADLINE_EXCEEDED");
		error.Exception.ShouldBeNull();
	}

	[Fact]
	public void CreateAckErrorRecordWithException()
	{
		// Arrange
		var ex = new TimeoutException("Timed out");

		// Act
		var error = new AckError("ack-2", "Timeout", ex);

		// Assert
		error.AckId.ShouldBe("ack-2");
		error.Message.ShouldBe("Timeout");
		error.Exception.ShouldBeSameAs(ex);
	}

	[Fact]
	public void SupportAckErrorRecordEquality()
	{
		// Arrange
		var e1 = new AckError("ack-1", "msg");
		var e2 = new AckError("ack-1", "msg");

		// Assert
		e1.ShouldBe(e2);
	}
}
