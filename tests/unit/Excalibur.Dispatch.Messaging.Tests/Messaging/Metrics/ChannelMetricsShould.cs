// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="ChannelMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class ChannelMetricsShould : UnitTestBase
{
	#region Property Tests

	[Fact]
	public void SetAndGetMessagesPerSecond()
	{
		// Arrange & Act
		var metrics = new ChannelMetrics { MessagesPerSecond = 1000.5 };

		// Assert
		metrics.MessagesPerSecond.ShouldBe(1000.5);
	}

	[Fact]
	public void SetAndGetAverageLatencyMs()
	{
		// Arrange & Act
		var metrics = new ChannelMetrics { AverageLatencyMs = 25.3 };

		// Assert
		metrics.AverageLatencyMs.ShouldBe(25.3);
	}

	[Fact]
	public void SetAndGetP99LatencyMs()
	{
		// Arrange & Act
		var metrics = new ChannelMetrics { P99LatencyMs = 150.75 };

		// Assert
		metrics.P99LatencyMs.ShouldBe(150.75);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultMessagesPerSecondIsZero()
	{
		// Act
		var metrics = new ChannelMetrics();

		// Assert
		metrics.MessagesPerSecond.ShouldBe(0);
	}

	[Fact]
	public void DefaultAverageLatencyMsIsZero()
	{
		// Act
		var metrics = new ChannelMetrics();

		// Assert
		metrics.AverageLatencyMs.ShouldBe(0);
	}

	[Fact]
	public void DefaultP99LatencyMsIsZero()
	{
		// Act
		var metrics = new ChannelMetrics();

		// Assert
		metrics.P99LatencyMs.ShouldBe(0);
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void AllowZeroValues()
	{
		// Arrange & Act
		var metrics = new ChannelMetrics
		{
			MessagesPerSecond = 0,
			AverageLatencyMs = 0,
			P99LatencyMs = 0
		};

		// Assert
		metrics.MessagesPerSecond.ShouldBe(0);
		metrics.AverageLatencyMs.ShouldBe(0);
		metrics.P99LatencyMs.ShouldBe(0);
	}

	[Fact]
	public void AllowNegativeValues()
	{
		// Arrange & Act - Negative values might not be typical but should be allowed
		var metrics = new ChannelMetrics
		{
			MessagesPerSecond = -1,
			AverageLatencyMs = -10,
			P99LatencyMs = -100
		};

		// Assert
		metrics.MessagesPerSecond.ShouldBe(-1);
		metrics.AverageLatencyMs.ShouldBe(-10);
		metrics.P99LatencyMs.ShouldBe(-100);
	}

	[Fact]
	public void AllowLargeValues()
	{
		// Arrange & Act
		var metrics = new ChannelMetrics
		{
			MessagesPerSecond = 1_000_000,
			AverageLatencyMs = 10_000,
			P99LatencyMs = 100_000
		};

		// Assert
		metrics.MessagesPerSecond.ShouldBe(1_000_000);
		metrics.AverageLatencyMs.ShouldBe(10_000);
		metrics.P99LatencyMs.ShouldBe(100_000);
	}

	[Fact]
	public void AllowPrecisionValues()
	{
		// Arrange & Act
		var metrics = new ChannelMetrics
		{
			MessagesPerSecond = 123.456789,
			AverageLatencyMs = 0.001234,
			P99LatencyMs = 99.999999
		};

		// Assert
		metrics.MessagesPerSecond.ShouldBe(123.456789);
		metrics.AverageLatencyMs.ShouldBe(0.001234);
		metrics.P99LatencyMs.ShouldBe(99.999999);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void RepresentHighThroughputChannel()
	{
		// Arrange & Act - High throughput, low latency
		var metrics = new ChannelMetrics
		{
			MessagesPerSecond = 50_000,
			AverageLatencyMs = 2.5,
			P99LatencyMs = 15.0
		};

		// Assert
		metrics.MessagesPerSecond.ShouldBe(50_000);
		metrics.AverageLatencyMs.ShouldBe(2.5);
		metrics.P99LatencyMs.ShouldBe(15.0);
	}

	[Fact]
	public void RepresentLowThroughputChannel()
	{
		// Arrange & Act - Low throughput, variable latency
		var metrics = new ChannelMetrics
		{
			MessagesPerSecond = 10,
			AverageLatencyMs = 500,
			P99LatencyMs = 2000
		};

		// Assert
		metrics.MessagesPerSecond.ShouldBe(10);
		metrics.AverageLatencyMs.ShouldBe(500);
		metrics.P99LatencyMs.ShouldBe(2000);
	}

	[Fact]
	public void AllowUpdateAfterCreation()
	{
		// Arrange
		var metrics = new ChannelMetrics
		{
			MessagesPerSecond = 100,
			AverageLatencyMs = 10,
			P99LatencyMs = 50
		};

		// Act - Update metrics
		metrics.MessagesPerSecond = 200;
		metrics.AverageLatencyMs = 5;
		metrics.P99LatencyMs = 25;

		// Assert
		metrics.MessagesPerSecond.ShouldBe(200);
		metrics.AverageLatencyMs.ShouldBe(5);
		metrics.P99LatencyMs.ShouldBe(25);
	}

	#endregion
}
