// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="InternalChannelMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InternalChannelMetricsShould
{
	[Fact]
	public void HaveZeroTotalReadsInitially()
	{
		// Arrange & Act
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Assert
		metrics.TotalReads.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalWritesInitially()
	{
		// Arrange & Act
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Assert
		metrics.TotalWrites.ShouldBe(0);
	}

	[Fact]
	public void IncrementTotalReadsOnRecordRead()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Act
		metrics.RecordRead();

		// Assert
		metrics.TotalReads.ShouldBe(1);
	}

	[Fact]
	public void IncrementTotalWritesOnRecordWrite()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Act
		metrics.RecordWrite();

		// Assert
		metrics.TotalWrites.ShouldBe(1);
	}

	[Fact]
	public void IncrementTotalReadsMultipleTimes()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Act
		metrics.RecordRead();
		metrics.RecordRead();
		metrics.RecordRead();

		// Assert
		metrics.TotalReads.ShouldBe(3);
	}

	[Fact]
	public void IncrementTotalWritesMultipleTimes()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Act
		metrics.RecordWrite();
		metrics.RecordWrite();
		metrics.RecordWrite();

		// Assert
		metrics.TotalWrites.ShouldBe(3);
	}

	[Fact]
	public void TrackReadsAndWritesIndependently()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Act
		metrics.RecordRead();
		metrics.RecordWrite();
		metrics.RecordRead();
		metrics.RecordWrite();
		metrics.RecordWrite();

		// Assert
		metrics.TotalReads.ShouldBe(2);
		metrics.TotalWrites.ShouldBe(3);
	}

	[Fact]
	public void ResetBothCountersToZero()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);
		metrics.RecordRead();
		metrics.RecordRead();
		metrics.RecordWrite();
		metrics.RecordWrite();
		metrics.RecordWrite();

		// Act
		metrics.Reset();

		// Assert
		metrics.TotalReads.ShouldBe(0);
		metrics.TotalWrites.ShouldBe(0);
	}

	[Fact]
	public void AllowRecordingAfterReset()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);
		metrics.RecordRead();
		metrics.RecordWrite();
		metrics.Reset();

		// Act
		metrics.RecordRead();
		metrics.RecordWrite();
		metrics.RecordWrite();

		// Assert
		metrics.TotalReads.ShouldBe(1);
		metrics.TotalWrites.ShouldBe(2);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public void AccumulateMultipleReadsCorrectly(int count)
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Act
		for (var i = 0; i < count; i++)
		{
			metrics.RecordRead();
		}

		// Assert
		metrics.TotalReads.ShouldBe(count);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public void AccumulateMultipleWritesCorrectly(int count)
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);

		// Act
		for (var i = 0; i < count; i++)
		{
			metrics.RecordWrite();
		}

		// Assert
		metrics.TotalWrites.ShouldBe(count);
	}

	[Fact]
	public void BeThreadSafeForConcurrentReads()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);
		const int iterations = 1000;
		const int threadCount = 4;

		// Act
		Parallel.For(0, threadCount, _ =>
		{
			for (var i = 0; i < iterations; i++)
			{
				metrics.RecordRead();
			}
		});

		// Assert
		metrics.TotalReads.ShouldBe(iterations * threadCount);
	}

	[Fact]
	public void BeThreadSafeForConcurrentWrites()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("test-channel", 100);
		const int iterations = 1000;
		const int threadCount = 4;

		// Act
		Parallel.For(0, threadCount, _ =>
		{
			for (var i = 0; i < iterations; i++)
			{
				metrics.RecordWrite();
			}
		});

		// Assert
		metrics.TotalWrites.ShouldBe(iterations * threadCount);
	}

	[Fact]
	public void AcceptVariousChannelNames()
	{
		// Arrange & Act - Should not throw
		var metrics1 = new InternalChannelMetrics("", 100);
		var metrics2 = new InternalChannelMetrics("channel-with-dashes", 100);
		var metrics3 = new InternalChannelMetrics("channel.with.dots", 100);
		var metrics4 = new InternalChannelMetrics("channel/with/slashes", 100);

		// Assert - All metrics should work independently
		metrics1.RecordRead();
		metrics2.RecordWrite();
		metrics3.RecordRead();
		metrics4.RecordWrite();

		metrics1.TotalReads.ShouldBe(1);
		metrics2.TotalWrites.ShouldBe(1);
		metrics3.TotalReads.ShouldBe(1);
		metrics4.TotalWrites.ShouldBe(1);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(int.MaxValue)]
	public void AcceptVariousCapacityValues(int capacity)
	{
		// Arrange & Act - Should not throw
		var metrics = new InternalChannelMetrics("test-channel", capacity);

		// Assert - Metrics should work regardless of capacity
		metrics.RecordRead();
		metrics.RecordWrite();

		metrics.TotalReads.ShouldBe(1);
		metrics.TotalWrites.ShouldBe(1);
	}

	[Fact]
	public void SimulateTypicalChannelUsagePattern()
	{
		// Arrange
		var metrics = new InternalChannelMetrics("message-queue", 1000);

		// Act - Simulate typical producer/consumer pattern
		// Producer writes 100 messages
		for (var i = 0; i < 100; i++)
		{
			metrics.RecordWrite();
		}

		// Consumer reads 80 messages
		for (var i = 0; i < 80; i++)
		{
			metrics.RecordRead();
		}

		// Producer writes 50 more
		for (var i = 0; i < 50; i++)
		{
			metrics.RecordWrite();
		}

		// Consumer catches up with 70 reads
		for (var i = 0; i < 70; i++)
		{
			metrics.RecordRead();
		}

		// Assert
		metrics.TotalWrites.ShouldBe(150);
		metrics.TotalReads.ShouldBe(150);
	}
}
