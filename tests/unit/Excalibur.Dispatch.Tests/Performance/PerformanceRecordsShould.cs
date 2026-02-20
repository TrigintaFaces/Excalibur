using Excalibur.Dispatch.Performance;

namespace Excalibur.Dispatch.Tests.Performance;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PerformanceRecordsShould
{
	// --- ComponentMetrics ---

	[Fact]
	public void ComponentMetrics_CreateWithRequiredProperties()
	{
		var metrics = new ComponentMetrics
		{
			ExecutionCount = 100,
			TotalDuration = TimeSpan.FromSeconds(10),
			AverageDuration = TimeSpan.FromMilliseconds(100),
			MinDuration = TimeSpan.FromMilliseconds(10),
			MaxDuration = TimeSpan.FromMilliseconds(500),
			SuccessCount = 95,
			FailureCount = 5,
			SuccessRate = 0.95,
		};

		metrics.ExecutionCount.ShouldBe(100);
		metrics.TotalDuration.ShouldBe(TimeSpan.FromSeconds(10));
		metrics.AverageDuration.ShouldBe(TimeSpan.FromMilliseconds(100));
		metrics.MinDuration.ShouldBe(TimeSpan.FromMilliseconds(10));
		metrics.MaxDuration.ShouldBe(TimeSpan.FromMilliseconds(500));
		metrics.SuccessCount.ShouldBe(95);
		metrics.FailureCount.ShouldBe(5);
		metrics.SuccessRate.ShouldBe(0.95);
	}

	// --- PipelineMetrics ---

	[Fact]
	public void PipelineMetrics_CreateWithRequiredProperties()
	{
		var metrics = new PipelineMetrics
		{
			TotalExecutions = 1000,
			TotalDuration = TimeSpan.FromMinutes(5),
			AverageDuration = TimeSpan.FromMilliseconds(300),
			AverageMiddlewareCount = 4.5,
			TotalMemoryAllocated = 1024 * 1024,
			AverageMemoryPerExecution = 1024,
		};

		metrics.TotalExecutions.ShouldBe(1000);
		metrics.TotalDuration.ShouldBe(TimeSpan.FromMinutes(5));
		metrics.AverageDuration.ShouldBe(TimeSpan.FromMilliseconds(300));
		metrics.AverageMiddlewareCount.ShouldBe(4.5);
		metrics.TotalMemoryAllocated.ShouldBe(1024 * 1024);
		metrics.AverageMemoryPerExecution.ShouldBe(1024);
	}

	// --- HandlerRegistryMetrics ---

	[Fact]
	public void HandlerRegistryMetrics_CreateWithRequiredProperties()
	{
		var metrics = new HandlerRegistryMetrics
		{
			TotalLookups = 500,
			TotalLookupTime = TimeSpan.FromMilliseconds(250),
			AverageLookupTime = TimeSpan.FromTicks(500),
			AverageHandlersPerLookup = 1.2,
			CacheHits = 450,
			CacheMisses = 50,
			CacheHitRate = 0.90,
		};

		metrics.TotalLookups.ShouldBe(500);
		metrics.TotalLookupTime.ShouldBe(TimeSpan.FromMilliseconds(250));
		metrics.AverageLookupTime.ShouldBe(TimeSpan.FromTicks(500));
		metrics.AverageHandlersPerLookup.ShouldBe(1.2);
		metrics.CacheHits.ShouldBe(450);
		metrics.CacheMisses.ShouldBe(50);
		metrics.CacheHitRate.ShouldBe(0.90);
	}

	// --- BatchProcessingMetrics ---

	[Fact]
	public void BatchProcessingMetrics_CreateWithRequiredProperties()
	{
		var metrics = new BatchProcessingMetrics
		{
			TotalBatches = 50,
			TotalItemsProcessed = 5000,
			TotalProcessingTime = TimeSpan.FromSeconds(30),
			AverageBatchSize = 100.0,
			AverageProcessingTimePerBatch = TimeSpan.FromMilliseconds(600),
			AverageProcessingTimePerItem = TimeSpan.FromMilliseconds(6),
			AverageParallelDegree = 4.0,
			OverallSuccessRate = 0.99,
			ThroughputItemsPerSecond = 166.7,
		};

		metrics.TotalBatches.ShouldBe(50);
		metrics.TotalItemsProcessed.ShouldBe(5000);
		metrics.TotalProcessingTime.ShouldBe(TimeSpan.FromSeconds(30));
		metrics.AverageBatchSize.ShouldBe(100.0);
		metrics.AverageProcessingTimePerBatch.ShouldBe(TimeSpan.FromMilliseconds(600));
		metrics.AverageProcessingTimePerItem.ShouldBe(TimeSpan.FromMilliseconds(6));
		metrics.AverageParallelDegree.ShouldBe(4.0);
		metrics.OverallSuccessRate.ShouldBe(0.99);
		metrics.ThroughputItemsPerSecond.ShouldBe(166.7);
	}

	// --- QueueMetrics ---

	[Fact]
	public void QueueMetrics_CreateWithRequiredProperties()
	{
		var opMetrics = new Dictionary<string, ComponentMetrics>
		{
			["enqueue"] = new ComponentMetrics
			{
				ExecutionCount = 100,
				TotalDuration = TimeSpan.FromSeconds(1),
				AverageDuration = TimeSpan.FromMilliseconds(10),
				MinDuration = TimeSpan.FromMilliseconds(1),
				MaxDuration = TimeSpan.FromMilliseconds(50),
				SuccessCount = 100,
				FailureCount = 0,
				SuccessRate = 1.0,
			},
		};

		var metrics = new QueueMetrics
		{
			OperationMetrics = opMetrics,
			CurrentDepth = 10,
			MaxDepthReached = 100,
			AverageDepth = 25.5,
			ThroughputOperationsPerSecond = 1000.0,
		};

		metrics.OperationMetrics.Count.ShouldBe(1);
		metrics.CurrentDepth.ShouldBe(10);
		metrics.MaxDepthReached.ShouldBe(100);
		metrics.AverageDepth.ShouldBe(25.5);
		metrics.ThroughputOperationsPerSecond.ShouldBe(1000.0);
	}

	// --- BenchmarkResults ---

	[Fact]
	public void BenchmarkResults_CreateWithRequiredProperties()
	{
		var testDate = DateTimeOffset.UtcNow;
		var results = new BenchmarkResults
		{
			TestDate = testDate,
			Iterations = 10000,
			TotalDuration = TimeSpan.FromSeconds(5),
			MessagesPerSecond = 2000.0,
			AverageLatencyMs = 0.5,
		};

		results.TestDate.ShouldBe(testDate);
		results.Iterations.ShouldBe(10000);
		results.TotalDuration.ShouldBe(TimeSpan.FromSeconds(5));
		results.MessagesPerSecond.ShouldBe(2000.0);
		results.AverageLatencyMs.ShouldBe(0.5);
		results.PerformanceSnapshot.ShouldBeNull();
	}

	// --- CacheFreezeStatus ---

	[Fact]
	public void CacheFreezeStatus_Unfrozen()
	{
		var status = CacheFreezeStatus.Unfrozen;

		status.HandlerInvokerFrozen.ShouldBeFalse();
		status.HandlerRegistryFrozen.ShouldBeFalse();
		status.HandlerActivatorFrozen.ShouldBeFalse();
		status.ResultFactoryFrozen.ShouldBeFalse();
		status.MiddlewareEvaluatorFrozen.ShouldBeFalse();
		status.FrozenAt.ShouldBeNull();
		status.AllFrozen.ShouldBeFalse();
	}

	[Fact]
	public void CacheFreezeStatus_AllFrozen()
	{
		var frozenAt = DateTimeOffset.UtcNow;
		var status = new CacheFreezeStatus(true, true, true, true, true, frozenAt);

		status.AllFrozen.ShouldBeTrue();
		status.FrozenAt.ShouldBe(frozenAt);
	}

	[Fact]
	public void CacheFreezeStatus_PartiallyFrozen()
	{
		var status = new CacheFreezeStatus(true, true, false, true, true, null);

		status.AllFrozen.ShouldBeFalse();
	}

	[Fact]
	public void CacheFreezeStatus_RecordEquality()
	{
		var s1 = new CacheFreezeStatus(true, true, true, true, true, null);
		var s2 = new CacheFreezeStatus(true, true, true, true, true, null);

		s1.ShouldBe(s2);
	}
}
