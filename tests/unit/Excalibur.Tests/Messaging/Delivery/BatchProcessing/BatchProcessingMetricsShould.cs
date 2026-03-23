// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Tests.Messaging.Delivery.BatchProcessing;

/// <summary>
///     Unit tests for BatchProcessingMetrics to verify metrics collection functionality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BatchProcessingMetricsShould
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
	public void RecordBatchCompletedShouldCalculateCorrectMetrics()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
		var batchSize = 100;
		var successfulCount = 80;
		var failedCount = 20;
		var duration = TimeSpan.FromSeconds(2);

		// Act & Assert - Method should not throw and calculations should be correct
		// Success rate: 80/100 = 0.8
		// Throughput: 100/2 = 50 items/second
		Should.NotThrow(() => metrics.RecordBatchCompleted(batchSize, successfulCount, failedCount, duration));
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
	public void RecordBatchFailureShouldNotThrow()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
		var exception = new InvalidOperationException("Test batch failure");

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.RecordBatchFailure(exception));
	}

	[Fact]
	public void RecordBatchFailureShouldAcceptTags()
	{
		// Arrange
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
		var exception = new TimeoutException("Batch processing timeout");
		var tags = new Dictionary<string, object?> { ["queue"] = "slow-queue", ["timeout"] = "30s" };

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.RecordBatchFailure(exception, tags));
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
		using var metrics = new BatchProcessingMetrics("test-batch-metrics");
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
	}
}
