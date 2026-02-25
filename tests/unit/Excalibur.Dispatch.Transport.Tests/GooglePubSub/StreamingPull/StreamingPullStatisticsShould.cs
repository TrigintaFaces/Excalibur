// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.StreamingPull;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class StreamingPullStatisticsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
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
		stats.StreamHealthInfos.Length.ShouldBe(0);
	}

	[Fact]
	public void SetAllProperties()
	{
		// Arrange
		var healthInfos = new[]
		{
			new StreamHealthInfo("stream-1") { IsConnected = true, MessagesReceived = 100 },
			new StreamHealthInfo("stream-2") { IsConnected = false, ErrorCount = 5 },
		};

		// Act
		var stats = new StreamingPullStatistics
		{
			ActiveStreamCount = 2,
			TargetStreamCount = 3,
			TotalMessagesReceived = 5000,
			TotalBytesReceived = 10_000_000,
			TotalErrors = 12,
			QueuedMessages = 50,
			ActiveProcessingThreads = 4,
			StreamHealthInfos = healthInfos,
		};

		// Assert
		stats.ActiveStreamCount.ShouldBe(2);
		stats.TargetStreamCount.ShouldBe(3);
		stats.TotalMessagesReceived.ShouldBe(5000);
		stats.TotalBytesReceived.ShouldBe(10_000_000);
		stats.TotalErrors.ShouldBe(12);
		stats.QueuedMessages.ShouldBe(50);
		stats.ActiveProcessingThreads.ShouldBe(4);
		stats.StreamHealthInfos.Length.ShouldBe(2);
	}

	[Fact]
	public void CreateStreamHealthInfoWithStreamId()
	{
		// Act
		var info = new StreamHealthInfo("stream-abc");

		// Assert
		info.StreamId.ShouldBe("stream-abc");
		info.IsConnected.ShouldBeFalse();
		info.ConnectedTime.ShouldBe(default);
		info.DisconnectedTime.ShouldBe(default);
		info.LastError.ShouldBeNull();
		info.MessagesReceived.ShouldBe(0);
		info.BytesReceived.ShouldBe(0);
		info.ErrorCount.ShouldBe(0);
		info.AcknowledgmentsSucceeded.ShouldBe(0);
		info.AcknowledgmentsFailed.ShouldBe(0);
		info.ReconnectCount.ShouldBe(0);
	}

	[Fact]
	public void ThrowOnNullStreamId()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new StreamHealthInfo(null!));
	}

	[Fact]
	public void SetStreamHealthInfoProperties()
	{
		// Arrange
		var connectedTime = DateTimeOffset.UtcNow.AddMinutes(-30);
		var lastMsgTime = DateTimeOffset.UtcNow.AddSeconds(-5);
		var lastErrorTime = DateTimeOffset.UtcNow.AddMinutes(-2);
		var error = new InvalidOperationException("Connection reset");

		// Act
		var info = new StreamHealthInfo("stream-xyz")
		{
			IsConnected = true,
			ConnectedTime = connectedTime,
			LastMessageTime = lastMsgTime,
			LastErrorTime = lastErrorTime,
			LastError = error,
			MessagesReceived = 10000,
			BytesReceived = 50_000_000,
			ErrorCount = 3,
			AcknowledgmentsSucceeded = 9990,
			AcknowledgmentsFailed = 10,
			ReconnectCount = 2,
		};

		// Assert
		info.IsConnected.ShouldBeTrue();
		info.ConnectedTime.ShouldBe(connectedTime);
		info.LastMessageTime.ShouldBe(lastMsgTime);
		info.LastErrorTime.ShouldBe(lastErrorTime);
		info.LastError.ShouldBe(error);
		info.MessagesReceived.ShouldBe(10000);
		info.BytesReceived.ShouldBe(50_000_000);
		info.ErrorCount.ShouldBe(3);
		info.AcknowledgmentsSucceeded.ShouldBe(9990);
		info.AcknowledgmentsFailed.ShouldBe(10);
		info.ReconnectCount.ShouldBe(2);
	}

	[Fact]
	public void CreateProcessorStatisticsWithDefaults()
	{
		// Act
		var stats = new ProcessorStatistics();

		// Assert
		stats.QueuedMessages.ShouldBe(0);
		stats.MaxQueueCapacity.ShouldBe(0);
		stats.ActiveProcessingThreads.ShouldBe(0);
		stats.IsShuttingDown.ShouldBeFalse();
	}

	[Fact]
	public void SetProcessorStatisticsProperties()
	{
		// Act
		var stats = new ProcessorStatistics
		{
			QueuedMessages = 25,
			MaxQueueCapacity = 1000,
			ActiveProcessingThreads = 8,
			IsShuttingDown = true,
		};

		// Assert
		stats.QueuedMessages.ShouldBe(25);
		stats.MaxQueueCapacity.ShouldBe(1000);
		stats.ActiveProcessingThreads.ShouldBe(8);
		stats.IsShuttingDown.ShouldBeTrue();
	}
}
