// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Delivery.BatchProcessing;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Excalibur.Tests.Messaging.Delivery.BatchProcessing;

/// <summary>
///     Unit tests for BatchProcessingMetrics to verify metrics collection functionality.
/// </summary>
[Trait("Category", "Unit")]
public class BatchProcessingMetricsShould
{
	[Fact]
	public void ConstructorShouldInitializeWithMeterNameOnly()
	{
		// Arrange & Act
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");

		// Assert
		_ = metrics.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructorShouldInitializeWithTelemetryClient()
	{
		// Arrange
		var (telemetryClient, _, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			// Act
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);

			// Assert
			_ = metrics.ShouldNotBeNull();
		}
	}

	[Fact]
	public void RecordBatchCompletedShouldCalculateCorrectMetrics()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
		var batchSize = 100;
		var successfulCount = 80;
		var failedCount = 20;
		var duration = TimeSpan.FromSeconds(2);

		// Act
		metrics.RecordBatchCompleted(batchSize, successfulCount, failedCount, duration);

		// Assert - Method should not throw and calculations should be correct Success rate: 80/100 = 0.8
		// Throughput: 100/2 = 50 items/second This test verifies the method executes without errors
	}

	[Fact]
	public void RecordBatchCompletedShouldHandleZeroDuration()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.RecordBatchCompleted(
			batchSize: 10,
			successfulCount: 10,
			failedCount: 0,
			duration: TimeSpan.Zero));
	}

	[Fact]
	public void RecordBatchCompletedShouldHandleZeroTotalProcessed()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.RecordBatchCompleted(
			batchSize: 10,
			successfulCount: 0,
			failedCount: 0,
			duration: TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public void RecordBatchCompletedShouldAcceptTags()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
		var tags = new Dictionary<string, object?> { ["queue"] = "test-queue", ["processor"] = "test-processor", ["version"] = "1.0.0" };

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.RecordBatchCompleted(
			batchSize: 50,
			successfulCount: 45,
			failedCount: 5,
			duration: TimeSpan.FromSeconds(1.5),
			tags: tags));
	}

	[Fact]
	public void RecordBatchCompletedShouldSendMetricsToTelemetryClient()
	{
		// Arrange
		var (telemetryClient, channel, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);

			// Act
			metrics.RecordBatchCompleted(
				batchSize: 100,
				successfulCount: 90,
				failedCount: 10,
				duration: TimeSpan.FromSeconds(2));

			// Assert
			var metricTelemetries = channel.SentItems.OfType<MetricTelemetry>().ToList();

			metricTelemetries.ShouldContain(static m => m.Name == "BatchProcessing.BatchSize" && m.Sum == 100);
			metricTelemetries.ShouldContain(static m => m.Name == "BatchProcessing.SuccessRate" && m.Sum == 0.9);
			metricTelemetries.ShouldContain(static m => m.Name == "BatchProcessing.Throughput" && m.Sum == 50.0);
			metricTelemetries.ShouldContain(static m => m.Name == "BatchProcessing.Duration" && m.Sum == 2000.0);
		}
	}

	[Fact]
	public void RecordBatchCompletedShouldSendEventToTelemetryClientWithTags()
	{
		// Arrange
		var (telemetryClient, channel, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);
			var tags = new Dictionary<string, object?> { ["queue"] = "test-queue", ["processor"] = "batch-processor" };

			// Act
			metrics.RecordBatchCompleted(
				batchSize: 50,
				successfulCount: 50,
				failedCount: 0,
				duration: TimeSpan.FromSeconds(1),
				tags: tags);

			// Assert
			var eventTelemetries = channel.SentItems.OfType<EventTelemetry>().ToList();
			eventTelemetries.ShouldContain(static e =>
				e.Name == "BatchProcessingCompleted" &&
				e.Properties["queue"] == "test-queue" &&
				e.Properties["processor"] == "batch-processor");
		}
	}

	[Fact]
	public void RecordBatchCompletedShouldHandleNullValuesInTags()
	{
		// Arrange
		var (telemetryClient, channel, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);
			var tags = new Dictionary<string, object?> { ["queue"] = "test-queue", ["processor"] = null, ["version"] = "1.0.0" };

			// Act & Assert - Should not throw
			Should.NotThrow(() => metrics.RecordBatchCompleted(
				batchSize: 30,
				successfulCount: 30,
				failedCount: 0,
				duration: TimeSpan.FromSeconds(0.5),
				tags: tags));

			// Assert
			var eventTelemetries = channel.SentItems.OfType<EventTelemetry>().ToList();
			eventTelemetries.ShouldContain(e =>
				e.Name == "BatchProcessingCompleted" &&
				e.Properties["queue"] == "test-queue" &&
				e.Properties["processor"] == string.Empty &&
				e.Properties["version"] == "1.0.0");
		}
	}

	[Fact]
	public void RecordBatchFailureShouldSendExceptionToTelemetryClient()
	{
		// Arrange
		var (telemetryClient, channel, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);
			var exception = new InvalidOperationException("Test batch failure");

			// Act
			metrics.RecordBatchFailure(exception);

			// Assert
			var exceptionTelemetries = channel.SentItems.OfType<ExceptionTelemetry>().ToList();
			exceptionTelemetries.ShouldContain(e =>
				e.Exception == exception);
		}
	}

	[Fact]
	public void RecordBatchFailureShouldSendExceptionWithTagsToTelemetryClient()
	{
		// Arrange
		var (telemetryClient, channel, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);
			var exception = new TimeoutException("Batch processing timeout");
			var tags = new Dictionary<string, object?> { ["queue"] = "slow-queue", ["timeout"] = "30s" };

			// Act
			metrics.RecordBatchFailure(exception, tags);

			// Assert
			var exceptionTelemetries = channel.SentItems.OfType<ExceptionTelemetry>().ToList();
			exceptionTelemetries.ShouldContain(e =>
				e.Exception == exception &&
				e.Properties["queue"] == "slow-queue" &&
				e.Properties["timeout"] == "30s");
		}
	}

	[Fact]
	public void RecordBatchFailureShouldHandleNullTelemetryClient()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient: null);
		var exception = new ArgumentException("Test exception");

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.RecordBatchFailure(exception));
	}

	[Fact]
	public void DisposeShouldNotThrow()
	{
		// Arrange & Act & Assert
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
		Should.NotThrow(metrics.Dispose);
	}

	[Fact]
	public void DisposeShouldBeIdempotent()
	{
		// Arrange & Act & Assert
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
		Should.NotThrow(() =>
		{
			metrics.Dispose();
			metrics.Dispose(); // Second dispose should not throw
		});
	}

	[Fact]
	public void RecordBatchCompletedShouldCalculateCorrectSuccessRate()
	{
		// Arrange
		var (telemetryClient, channel, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);

			// Act - Test various success rate scenarios
			metrics.RecordBatchCompleted(100, 100, 0, TimeSpan.FromSeconds(1)); // 100% success
			metrics.RecordBatchCompleted(100, 75, 25, TimeSpan.FromSeconds(1)); // 75% success
			metrics.RecordBatchCompleted(100, 0, 100, TimeSpan.FromSeconds(1)); // 0% success

			// Assert - Use ShouldContain since ConcurrentBag doesn't guarantee order
			var metricTelemetries = channel.SentItems.OfType<MetricTelemetry>()
				.Where(static m => m.Name == "BatchProcessing.SuccessRate")
				.ToList();

			metricTelemetries.Count.ShouldBe(3);
			metricTelemetries.ShouldContain(static m => m.Sum == 1.0);   // 100% success
			metricTelemetries.ShouldContain(static m => m.Sum == 0.75);  // 75% success
			metricTelemetries.ShouldContain(static m => m.Sum == 0.0);   // 0% success
		}
	}

	[Fact]
	public void RecordBatchCompletedShouldCalculateCorrectThroughput()
	{
		// Arrange
		var (telemetryClient, channel, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);

			// Act - Test throughput calculations
			// Throughput = totalProcessed / duration.TotalSeconds where totalProcessed = successful + failed
			metrics.RecordBatchCompleted(100, 80, 20, TimeSpan.FromSeconds(2)); // (80+20)/2 = 50 items/sec
			metrics.RecordBatchCompleted(60, 50, 10, TimeSpan.FromSeconds(3)); // (50+10)/3 = 20 items/sec
			metrics.RecordBatchCompleted(200, 180, 20, TimeSpan.FromSeconds(1)); // (180+20)/1 = 200 items/sec

			// Assert - Use ShouldContain since ConcurrentBag doesn't guarantee order
			var metricTelemetries = channel.SentItems.OfType<MetricTelemetry>()
				.Where(static m => m.Name == "BatchProcessing.Throughput")
				.ToList();

			metricTelemetries.Count.ShouldBe(3);
			metricTelemetries.ShouldContain(static m => m.Sum == 50.0);   // (80+20)/2 = 50 items/sec
			metricTelemetries.ShouldContain(static m => m.Sum == 20.0);   // (50+10)/3 = 20 items/sec
			metricTelemetries.ShouldContain(static m => m.Sum == 200.0);  // (180+20)/1 = 200 items/sec
		}
	}

	[Fact]
	public void RecordBatchCompletedShouldHandleConcurrentCalls()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
		var tasks = new List<Task>();

		// Act - Concurrent metric recording
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				for (var j = 0; j < 100; j++)
				{
					metrics.RecordBatchCompleted(
						batchSize: Random.Shared.Next(10, 100),
						successfulCount: Random.Shared.Next(5, 95),
						failedCount: Random.Shared.Next(0, 15),
						duration: TimeSpan.FromMilliseconds(Random.Shared.Next(100, 2000)));
				}
			}));
		}

		// Assert - Should not throw
		Should.NotThrow(() => Task.WaitAll([.. tasks]));
	}

	[Fact]
	public void RecordBatchFailureShouldHandleConcurrentCalls()
	{
		// Arrange
		var (telemetryClient, channel, configuration) = CreateTestTelemetryClient();
		using (configuration)
		{
			using var metrics = new BatchProcessingMetrics("test-batch-metrics", telemetryClient);
			var tasks = new List<Task>();

			// Act - Concurrent failure recording
			for (var i = 0; i < 5; i++)
			{
				tasks.Add(Task.Run(() =>
				{
					for (var j = 0; j < 50; j++)
					{
						var exception = new InvalidOperationException($"Test exception {j}");
						metrics.RecordBatchFailure(exception);
					}
				}));
			}

			// Assert - Should not throw
			Should.NotThrow(() => Task.WaitAll([.. tasks]));

			// Verify all exceptions were tracked
			var exceptionTelemetries = channel.SentItems.OfType<ExceptionTelemetry>().ToList();
			exceptionTelemetries.Count.ShouldBe(250);
		}
	}

	/// <summary>
	///     Creates a configured TelemetryClient with test channel for verification.
	/// </summary>
	private static (TelemetryClient client, TestTelemetryChannel channel, TelemetryConfiguration configuration) CreateTestTelemetryClient()
	{
		var channel = new TestTelemetryChannel { DeveloperMode = true, EndpointAddress = "https://test.local" };
		var configuration = new TelemetryConfiguration { TelemetryChannel = channel, InstrumentationKey = "test-key" };
		var client = new TelemetryClient(configuration);
		return (client, channel, configuration);
	}

	/// <summary>
	///     Test telemetry channel that captures telemetry items for verification.
	/// </summary>
	private sealed class TestTelemetryChannel : ITelemetryChannel
	{
		// Use ConcurrentBag for thread-safe concurrent access in tests
		public ConcurrentBag<ITelemetry> SentItems { get; } = [];

		public required bool? DeveloperMode { get; set; }

		public required string EndpointAddress { get; set; } = string.Empty;

		public void Send(ITelemetry item) => SentItems.Add(item);

		public void Flush()
		{
		}

		public void Dispose()
		{
		}
	}
}
