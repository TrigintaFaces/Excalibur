// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="PerformanceEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait("Priority", "0")]
public sealed class PerformanceEventIdShould : UnitTestBase
{
	#region Benchmark Execution Event ID Tests (50000-50099)

	[Fact]
	public void HaveBenchmarkStartingInBenchmarkRange()
	{
		PerformanceEventId.BenchmarkStarting.ShouldBe(50000);
	}

	[Fact]
	public void HaveBenchmarkCompletedInBenchmarkRange()
	{
		PerformanceEventId.BenchmarkCompleted.ShouldBe(50001);
	}

	[Fact]
	public void HaveBenchmarkTotalDurationInBenchmarkRange()
	{
		PerformanceEventId.BenchmarkTotalDuration.ShouldBe(50002);
	}

	[Fact]
	public void HaveBenchmarkMessagesPerSecondInBenchmarkRange()
	{
		PerformanceEventId.BenchmarkMessagesPerSecond.ShouldBe(50003);
	}

	[Fact]
	public void HaveBenchmarkAverageLatencyInBenchmarkRange()
	{
		PerformanceEventId.BenchmarkAverageLatency.ShouldBe(50004);
	}

	[Fact]
	public void HaveAllBenchmarkEventIdsInExpectedRange()
	{
		// Benchmark Execution IDs are in range 50000-50099
		PerformanceEventId.BenchmarkStarting.ShouldBeInRange(50000, 50099);
		PerformanceEventId.BenchmarkCompleted.ShouldBeInRange(50000, 50099);
		PerformanceEventId.BenchmarkTotalDuration.ShouldBeInRange(50000, 50099);
		PerformanceEventId.BenchmarkMessagesPerSecond.ShouldBeInRange(50000, 50099);
		PerformanceEventId.BenchmarkAverageLatency.ShouldBeInRange(50000, 50099);
	}

	#endregion

	#region Pipeline Metrics Event ID Tests (50100-50199)

	[Fact]
	public void HavePipelinePerformanceHeaderInPipelineRange()
	{
		PerformanceEventId.PipelinePerformanceHeader.ShouldBe(50100);
	}

	[Fact]
	public void HavePipelineTotalExecutionsInPipelineRange()
	{
		PerformanceEventId.PipelineTotalExecutions.ShouldBe(50101);
	}

	[Fact]
	public void HavePipelineAverageDurationInPipelineRange()
	{
		PerformanceEventId.PipelineAverageDuration.ShouldBe(50102);
	}

	[Fact]
	public void HavePipelineAverageMiddlewareCountInPipelineRange()
	{
		PerformanceEventId.PipelineAverageMiddlewareCount.ShouldBe(50103);
	}

	[Fact]
	public void HavePipelineMemoryPerExecutionInPipelineRange()
	{
		PerformanceEventId.PipelineMemoryPerExecution.ShouldBe(50104);
	}

	[Fact]
	public void HaveAllPipelineEventIdsInExpectedRange()
	{
		// Pipeline Metrics IDs are in range 50100-50199
		PerformanceEventId.PipelinePerformanceHeader.ShouldBeInRange(50100, 50199);
		PerformanceEventId.PipelineTotalExecutions.ShouldBeInRange(50100, 50199);
		PerformanceEventId.PipelineAverageDuration.ShouldBeInRange(50100, 50199);
		PerformanceEventId.PipelineAverageMiddlewareCount.ShouldBeInRange(50100, 50199);
		PerformanceEventId.PipelineMemoryPerExecution.ShouldBeInRange(50100, 50199);
	}

	#endregion

	#region Batch Processing Metrics Event ID Tests (50200-50299)

	[Fact]
	public void HaveBatchProcessingHeaderInBatchRange()
	{
		PerformanceEventId.BatchProcessingHeader.ShouldBe(50200);
	}

	[Fact]
	public void HaveBatchThroughputInBatchRange()
	{
		PerformanceEventId.BatchThroughput.ShouldBe(50201);
	}

	[Fact]
	public void HaveBatchSuccessRateInBatchRange()
	{
		PerformanceEventId.BatchSuccessRate.ShouldBe(50202);
	}

	[Fact]
	public void HaveBatchAverageParallelDegreeInBatchRange()
	{
		PerformanceEventId.BatchAverageParallelDegree.ShouldBe(50203);
	}

	[Fact]
	public void HaveAllBatchEventIdsInExpectedRange()
	{
		// Batch Processing Metrics IDs are in range 50200-50299
		PerformanceEventId.BatchProcessingHeader.ShouldBeInRange(50200, 50299);
		PerformanceEventId.BatchThroughput.ShouldBeInRange(50200, 50299);
		PerformanceEventId.BatchSuccessRate.ShouldBeInRange(50200, 50299);
		PerformanceEventId.BatchAverageParallelDegree.ShouldBeInRange(50200, 50299);
	}

	#endregion

	#region Handler Registry Metrics Event ID Tests (50300-50399)

	[Fact]
	public void HaveHandlerRegistryHeaderInHandlerRange()
	{
		PerformanceEventId.HandlerRegistryHeader.ShouldBe(50300);
	}

	[Fact]
	public void HaveHandlerCacheHitRateInHandlerRange()
	{
		PerformanceEventId.HandlerCacheHitRate.ShouldBe(50301);
	}

	[Fact]
	public void HaveHandlerAverageLookupTimeInHandlerRange()
	{
		PerformanceEventId.HandlerAverageLookupTime.ShouldBe(50302);
	}

	[Fact]
	public void HaveHandlersPerLookupInHandlerRange()
	{
		PerformanceEventId.HandlersPerLookup.ShouldBe(50303);
	}

	[Fact]
	public void HaveAllHandlerEventIdsInExpectedRange()
	{
		// Handler Registry Metrics IDs are in range 50300-50399
		PerformanceEventId.HandlerRegistryHeader.ShouldBeInRange(50300, 50399);
		PerformanceEventId.HandlerCacheHitRate.ShouldBeInRange(50300, 50399);
		PerformanceEventId.HandlerAverageLookupTime.ShouldBeInRange(50300, 50399);
		PerformanceEventId.HandlersPerLookup.ShouldBeInRange(50300, 50399);
	}

	#endregion

	#region Performance Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInPerformanceReservedRange()
	{
		// Performance reserved range is 50000-50999
		var allEventIds = GetAllPerformanceEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(50000, 50999,
				$"Event ID {eventId} is outside Performance reserved range (50000-50999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllPerformanceEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		// Verify all event IDs are accounted for
		var allEventIds = GetAllPerformanceEventIds();

		// Total: 5 Benchmark + 5 Pipeline + 4 Batch + 4 Handler = 18 event IDs
		allEventIds.Length.ShouldBe(18);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllPerformanceEventIds()
	{
		return
		[
			// Benchmark Execution (50000-50099)
			PerformanceEventId.BenchmarkStarting,
			PerformanceEventId.BenchmarkCompleted,
			PerformanceEventId.BenchmarkTotalDuration,
			PerformanceEventId.BenchmarkMessagesPerSecond,
			PerformanceEventId.BenchmarkAverageLatency,

			// Pipeline Metrics (50100-50199)
			PerformanceEventId.PipelinePerformanceHeader,
			PerformanceEventId.PipelineTotalExecutions,
			PerformanceEventId.PipelineAverageDuration,
			PerformanceEventId.PipelineAverageMiddlewareCount,
			PerformanceEventId.PipelineMemoryPerExecution,

			// Batch Processing Metrics (50200-50299)
			PerformanceEventId.BatchProcessingHeader,
			PerformanceEventId.BatchThroughput,
			PerformanceEventId.BatchSuccessRate,
			PerformanceEventId.BatchAverageParallelDegree,

			// Handler Registry Metrics (50300-50399)
			PerformanceEventId.HandlerRegistryHeader,
			PerformanceEventId.HandlerCacheHitRate,
			PerformanceEventId.HandlerAverageLookupTime,
			PerformanceEventId.HandlersPerLookup
		];
	}

	#endregion
}
