// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="ChannelMessagePumpMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelMessagePumpMetricsShould
{
	[Fact]
	public void HaveDefaultZeroValues()
	{
		// Arrange & Act
		var metrics = new ChannelMessagePumpMetrics();

		// Assert
		metrics.MessagesProduced.ShouldBe(0);
		metrics.MessagesConsumed.ShouldBe(0);
		metrics.CurrentQueueDepth.ShouldBe(0);
		metrics.MaxQueueDepth.ShouldBe(0);
		metrics.MessagesFailed.ShouldBe(0);
		metrics.MessagesAcknowledged.ShouldBe(0);
		metrics.MessagesRejected.ShouldBe(0);
		metrics.AverageProcessingTimeMs.ShouldBe(0);
	}

	[Fact]
	public void HaveNullTimestampsByDefault()
	{
		// Arrange & Act
		var metrics = new ChannelMessagePumpMetrics();

		// Assert
		metrics.StartedAt.ShouldBeNull();
		metrics.LastProducedAt.ShouldBeNull();
		metrics.LastConsumedAt.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMessagesProduced()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.MessagesProduced = 1000;

		// Assert
		metrics.MessagesProduced.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingMessagesConsumed()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.MessagesConsumed = 950;

		// Assert
		metrics.MessagesConsumed.ShouldBe(950);
	}

	[Fact]
	public void AllowSettingCurrentQueueDepth()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.CurrentQueueDepth = 50;

		// Assert
		metrics.CurrentQueueDepth.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingMaxQueueDepth()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.MaxQueueDepth = 500;

		// Assert
		metrics.MaxQueueDepth.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingMessagesFailed()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.MessagesFailed = 10;

		// Assert
		metrics.MessagesFailed.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingMessagesAcknowledged()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.MessagesAcknowledged = 900;

		// Assert
		metrics.MessagesAcknowledged.ShouldBe(900);
	}

	[Fact]
	public void AllowSettingMessagesRejected()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.MessagesRejected = 40;

		// Assert
		metrics.MessagesRejected.ShouldBe(40);
	}

	[Fact]
	public void AllowSettingAverageProcessingTimeMs()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.AverageProcessingTimeMs = 25.5;

		// Assert
		metrics.AverageProcessingTimeMs.ShouldBe(25.5);
	}

	[Fact]
	public void AllowSettingStartedAt()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

		// Act
		metrics.StartedAt = startTime;

		// Assert
		metrics.StartedAt.ShouldBe(startTime);
	}

	[Fact]
	public void AllowSettingLastProducedAt()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();
		var lastProducedTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		metrics.LastProducedAt = lastProducedTime;

		// Assert
		metrics.LastProducedAt.ShouldBe(lastProducedTime);
	}

	[Fact]
	public void AllowSettingLastConsumedAt()
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();
		var lastConsumedTime = new DateTimeOffset(2026, 1, 15, 12, 0, 5, TimeSpan.Zero);

		// Act
		metrics.LastConsumedAt = lastConsumedTime;

		// Assert
		metrics.LastConsumedAt.ShouldBe(lastConsumedTime);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var metrics = new ChannelMessagePumpMetrics
		{
			MessagesProduced = 10000,
			MessagesConsumed = 9500,
			CurrentQueueDepth = 100,
			MaxQueueDepth = 500,
			MessagesFailed = 50,
			MessagesAcknowledged = 9400,
			MessagesRejected = 50,
			AverageProcessingTimeMs = 15.3,
			StartedAt = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero),
			LastProducedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
			LastConsumedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 1, TimeSpan.Zero),
		};

		// Assert
		metrics.MessagesProduced.ShouldBe(10000);
		metrics.MessagesConsumed.ShouldBe(9500);
		metrics.CurrentQueueDepth.ShouldBe(100);
		metrics.MaxQueueDepth.ShouldBe(500);
		metrics.MessagesFailed.ShouldBe(50);
		metrics.MessagesAcknowledged.ShouldBe(9400);
		metrics.MessagesRejected.ShouldBe(50);
		metrics.AverageProcessingTimeMs.ShouldBe(15.3);
		metrics.StartedAt.ShouldNotBeNull();
		metrics.LastProducedAt.ShouldNotBeNull();
		metrics.LastConsumedAt.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	[InlineData(long.MaxValue)]
	public void AcceptVariousMessagesProducedValues(long value)
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.MessagesProduced = value;

		// Assert
		metrics.MessagesProduced.ShouldBe(value);
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(10.5)]
	[InlineData(1000.999)]
	public void AcceptVariousAverageProcessingTimeValues(double value)
	{
		// Arrange
		var metrics = new ChannelMessagePumpMetrics();

		// Act
		metrics.AverageProcessingTimeMs = value;

		// Assert
		metrics.AverageProcessingTimeMs.ShouldBe(value);
	}

	[Fact]
	public void TrackTypicalMessagePumpScenario()
	{
		// Arrange & Act - Simulate message pump metrics over time
		var metrics = new ChannelMessagePumpMetrics
		{
			MessagesProduced = 10000,
			MessagesConsumed = 9500,
			CurrentQueueDepth = 100,
			MaxQueueDepth = 250,
			MessagesFailed = 100,
			MessagesAcknowledged = 9200,
			MessagesRejected = 200,
			AverageProcessingTimeMs = 12.5,
			StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
			LastProducedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
			LastConsumedAt = DateTimeOffset.UtcNow,
		};

		// Assert - Verify expected relationships
		metrics.MessagesProduced.ShouldBeGreaterThan(metrics.MessagesConsumed);
		metrics.CurrentQueueDepth.ShouldBeLessThanOrEqualTo(metrics.MaxQueueDepth);
		metrics.MessagesAcknowledged.ShouldBeLessThanOrEqualTo(metrics.MessagesConsumed);
		metrics.MessagesFailed.ShouldBeLessThanOrEqualTo(metrics.MessagesConsumed);
		metrics.MessagesRejected.ShouldBeGreaterThan(0);
	}
}
