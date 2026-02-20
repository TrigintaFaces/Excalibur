// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PollingMetricsCollectorShould : IDisposable
{
	private readonly PollingMetricsCollector _collector;

	public PollingMetricsCollectorShould()
	{
		_collector = new PollingMetricsCollector(NullLogger<PollingMetricsCollector>.Instance);
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new PollingMetricsCollector(null!));
	}

	[Fact]
	public void RecordPollingAttemptWithMessages()
	{
		// Act
		_collector.RecordPollingAttempt(5, TimeSpan.FromMilliseconds(100));

		// Assert
		var stats = _collector.GetStatistics();
		stats.TotalAttempts.ShouldBe(1);
		stats.SuccessfulAttempts.ShouldBe(1);
		stats.TotalMessagesReceived.ShouldBe(5);
		stats.AverageMessagesPerPoll.ShouldBe(5.0);
	}

	[Fact]
	public void RecordPollingAttemptWithNoMessages()
	{
		// Act
		_collector.RecordPollingAttempt(0, TimeSpan.FromMilliseconds(50));

		// Assert
		var stats = _collector.GetStatistics();
		stats.TotalAttempts.ShouldBe(1);
		stats.SuccessfulAttempts.ShouldBe(0);
		stats.TotalMessagesReceived.ShouldBe(0);
	}

	[Fact]
	public void RecordPollingError()
	{
		// Act
		_collector.RecordPollingError(new InvalidOperationException("test"));

		// Assert
		var stats = _collector.GetStatistics();
		stats.FailedAttempts.ShouldBe(1);
	}

	[Fact]
	public void ThrowWhenRecordingNullException()
	{
		Should.Throw<ArgumentNullException>(() => _collector.RecordPollingError(null!));
	}

	[Fact]
	public void RecordMetricByName()
	{
		// Act
		_collector.RecordMetric("TestMetric", 42.0, MetricUnit.Count);

		// Assert
		var metrics = _collector.GetMetrics();
		metrics.Count.ShouldBe(1);
		metrics[0].Name.ShouldBe("TestMetric");
		metrics[0].Value.ShouldBe(42.0);
		metrics[0].Unit.ShouldBe(MetricUnit.Count);
	}

	[Fact]
	public void RecordMetricWithDimensions()
	{
		// Arrange
		var dimensions = new Dictionary<string, string> { ["QueueName"] = "test-queue" };

		// Act
		_collector.RecordMetric("TestMetric", 1.0, MetricUnit.Count, dimensions);

		// Assert
		var metrics = _collector.GetMetrics();
		metrics.Count.ShouldBe(1);
		metrics[0].Dimensions.ShouldContainKey("QueueName");
	}

	[Fact]
	public void ThrowWhenRecordingMetricWithNullName()
	{
		Should.Throw<ArgumentNullException>(() => _collector.RecordMetric(null!, 1.0));
	}

	[Fact]
	public void ThrowWhenRecordingMetricWithNullDimensions()
	{
		Should.Throw<ArgumentNullException>(() =>
			_collector.RecordMetric("test", 1.0, MetricUnit.Count, null!));
	}

	[Fact]
	public void CalculateAverageStatistics()
	{
		// Act
		_collector.RecordPollingAttempt(10, TimeSpan.FromMilliseconds(100));
		_collector.RecordPollingAttempt(20, TimeSpan.FromMilliseconds(200));

		// Assert
		var stats = _collector.GetStatistics();
		stats.TotalAttempts.ShouldBe(2);
		stats.TotalMessagesReceived.ShouldBe(30);
		stats.AverageMessagesPerPoll.ShouldBe(15.0);
		stats.AveragePollDuration.TotalMilliseconds.ShouldBe(150.0);
	}

	[Fact]
	public void ReturnZeroAveragesWhenNoAttempts()
	{
		// Act
		var stats = _collector.GetStatistics();

		// Assert
		stats.AverageMessagesPerPoll.ShouldBe(0);
		stats.AveragePollDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ClearAllMetrics()
	{
		// Arrange
		_collector.RecordPollingAttempt(5, TimeSpan.FromMilliseconds(100));
		_collector.RecordPollingError(new InvalidOperationException("test"));

		// Act
		_collector.Clear();

		// Assert
		var stats = _collector.GetStatistics();
		stats.TotalAttempts.ShouldBe(0);
		stats.SuccessfulAttempts.ShouldBe(0);
		stats.FailedAttempts.ShouldBe(0);
		stats.TotalMessagesReceived.ShouldBe(0);
	}

	[Fact]
	public void DisposeWithoutThrowing()
	{
		// Act & Assert — double dispose should not throw
		_collector.Dispose();
		_collector.Dispose();
	}

	public void Dispose()
	{
		_collector.Dispose();
	}
}
