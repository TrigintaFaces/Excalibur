// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Performance;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Tests for the <see cref="PerformanceMetricsCollector" /> class.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
public sealed class PerformanceMetricsCollectorShould : IDisposable
{
	private readonly PerformanceMetricsCollector _sut = new();

	[Fact]
	public void RecordMiddlewareExecutionSuccessfully()
	{
		_sut.RecordMiddlewareExecution("ValidationMiddleware", TimeSpan.FromMilliseconds(10));

		var snapshot = _sut.GetSnapshot();
		snapshot.MiddlewareMetrics.ShouldContainKey("ValidationMiddleware");
		snapshot.MiddlewareMetrics["ValidationMiddleware"].ExecutionCount.ShouldBe(1);
	}

	[Fact]
	public void ThrowForNullMiddlewareName() =>
		Should.Throw<ArgumentException>(() => _sut.RecordMiddlewareExecution(null!, TimeSpan.FromMilliseconds(1)));

	[Fact]
	public void ThrowForEmptyMiddlewareName() =>
		Should.Throw<ArgumentException>(() => _sut.RecordMiddlewareExecution(string.Empty, TimeSpan.FromMilliseconds(1)));

	[Fact]
	public void RecordPipelineExecutionSuccessfully()
	{
		_sut.RecordPipelineExecution(middlewareCount: 3, totalDuration: TimeSpan.FromMilliseconds(50), memoryAllocated: 1024);

		var snapshot = _sut.GetSnapshot();
		snapshot.PipelineMetrics.TotalExecutions.ShouldBe(1);
	}

	[Fact]
	public void ThrowForNegativeMiddlewareCount() =>
		Should.Throw<ArgumentOutOfRangeException>(
			() => _sut.RecordPipelineExecution(middlewareCount: -1, totalDuration: TimeSpan.FromMilliseconds(1)));

	[Fact]
	public void ThrowForNegativeMemoryAllocated() =>
		Should.Throw<ArgumentOutOfRangeException>(
			() => _sut.RecordPipelineExecution(middlewareCount: 1, totalDuration: TimeSpan.FromMilliseconds(1), memoryAllocated: -1));

	[Fact]
	public void RecordBatchProcessingSuccessfully()
	{
		_sut.RecordBatchProcessing("Outbox", batchSize: 10, processingTime: TimeSpan.FromMilliseconds(100), parallelDegree: 2,
			successCount: 9, failureCount: 1);

		var snapshot = _sut.GetSnapshot();
		snapshot.BatchMetrics.ShouldContainKey("Outbox");
	}

	[Fact]
	public void ThrowForNullProcessorType() =>
		Should.Throw<ArgumentException>(
			() => _sut.RecordBatchProcessing(null!, 10, TimeSpan.FromMilliseconds(1), 1, 10, 0));

	[Fact]
	public void RecordHandlerLookupSuccessfully()
	{
		_sut.RecordHandlerLookup("OrderCommand", TimeSpan.FromMicroseconds(100), handlersFound: 1);

		var snapshot = _sut.GetSnapshot();
		snapshot.HandlerMetrics.TotalLookups.ShouldBe(1);
	}

	[Fact]
	public void ThrowForNullMessageTypeOnHandlerLookup() =>
		Should.Throw<ArgumentException>(
			() => _sut.RecordHandlerLookup(null!, TimeSpan.FromMicroseconds(100), 1));

	[Fact]
	public void RecordQueueOperationSuccessfully()
	{
		_sut.RecordQueueOperation("main-queue", "enqueue", itemCount: 5, duration: TimeSpan.FromMilliseconds(10), queueDepth: 20);

		var snapshot = _sut.GetSnapshot();
		snapshot.QueueMetrics.ShouldContainKey("main-queue");
	}

	[Fact]
	public void ThrowForNullQueueName() =>
		Should.Throw<ArgumentException>(
			() => _sut.RecordQueueOperation(null!, "enqueue", 1, TimeSpan.FromMilliseconds(1), 1));

	[Fact]
	public void ThrowForNullOperation() =>
		Should.Throw<ArgumentException>(
			() => _sut.RecordQueueOperation("queue", null!, 1, TimeSpan.FromMilliseconds(1), 1));

	[Fact]
	public void GetSnapshotWithTimestamp()
	{
		var before = DateTimeOffset.UtcNow;
		var snapshot = _sut.GetSnapshot();
		var after = DateTimeOffset.UtcNow;

		snapshot.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		snapshot.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void ResetAllMetrics()
	{
		_sut.RecordMiddlewareExecution("Test", TimeSpan.FromMilliseconds(1));
		_sut.RecordPipelineExecution(1, TimeSpan.FromMilliseconds(1));

		_sut.Reset();

		var snapshot = _sut.GetSnapshot();
		snapshot.MiddlewareMetrics.Count.ShouldBe(0);
		snapshot.PipelineMetrics.TotalExecutions.ShouldBe(0);
	}

	[Fact]
	public void AggregateMultipleMiddlewareExecutions()
	{
		_sut.RecordMiddlewareExecution("Auth", TimeSpan.FromMilliseconds(5));
		_sut.RecordMiddlewareExecution("Auth", TimeSpan.FromMilliseconds(15));

		var snapshot = _sut.GetSnapshot();
		snapshot.MiddlewareMetrics["Auth"].ExecutionCount.ShouldBe(2);
	}

	[Fact]
	public void ThrowAfterDispose()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.GetSnapshot());
	}

	[Fact]
	public void DisposeMultipleTimes()
	{
		_sut.Dispose();
		Should.NotThrow(() => _sut.Dispose());
	}

	public void Dispose() => _sut.Dispose();
}
