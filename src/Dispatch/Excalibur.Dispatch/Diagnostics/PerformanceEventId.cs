// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Event IDs for performance benchmarking and metrics (50000-50999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>50000-50099: Benchmark Execution</item>
/// <item>50100-50199: Pipeline Metrics</item>
/// <item>50200-50299: Batch Processing Metrics</item>
/// <item>50300-50399: Handler Registry Metrics</item>
/// </list>
/// </remarks>
public static class PerformanceEventId
{
	// ========================================
	// 50000-50099: Benchmark Execution
	// ========================================

	/// <summary>Performance benchmark starting.</summary>
	public const int BenchmarkStarting = 50000;

	/// <summary>Performance benchmark completed.</summary>
	public const int BenchmarkCompleted = 50001;

	/// <summary>Benchmark total duration.</summary>
	public const int BenchmarkTotalDuration = 50002;

	/// <summary>Benchmark messages per second.</summary>
	public const int BenchmarkMessagesPerSecond = 50003;

	/// <summary>Benchmark average latency.</summary>
	public const int BenchmarkAverageLatency = 50004;

	// ========================================
	// 50100-50199: Pipeline Metrics
	// ========================================

	/// <summary>Pipeline performance metrics header.</summary>
	public const int PipelinePerformanceHeader = 50100;

	/// <summary>Pipeline total executions.</summary>
	public const int PipelineTotalExecutions = 50101;

	/// <summary>Pipeline average duration.</summary>
	public const int PipelineAverageDuration = 50102;

	/// <summary>Pipeline average middleware count.</summary>
	public const int PipelineAverageMiddlewareCount = 50103;

	/// <summary>Pipeline memory per execution.</summary>
	public const int PipelineMemoryPerExecution = 50104;

	// ========================================
	// 50200-50299: Batch Processing Metrics
	// ========================================

	/// <summary>Batch processing metrics header.</summary>
	public const int BatchProcessingHeader = 50200;

	/// <summary>Batch processing throughput.</summary>
	public const int BatchThroughput = 50201;

	/// <summary>Batch processing success rate.</summary>
	public const int BatchSuccessRate = 50202;

	/// <summary>Batch processing average parallel degree.</summary>
	public const int BatchAverageParallelDegree = 50203;

	// ========================================
	// 50300-50399: Handler Registry Metrics
	// ========================================

	/// <summary>Handler registry metrics header.</summary>
	public const int HandlerRegistryHeader = 50300;

	/// <summary>Handler registry cache hit rate.</summary>
	public const int HandlerCacheHitRate = 50301;

	/// <summary>Handler registry average lookup time.</summary>
	public const int HandlerAverageLookupTime = 50302;

	/// <summary>Handler registry handlers per lookup.</summary>
	public const int HandlersPerLookup = 50303;

	// ========================================
	// 50400-50499: Cache Management
	// ========================================

	/// <summary>Dispatch caches already frozen.</summary>
	public const int CachesAlreadyFrozen = 50400;

	/// <summary>Dispatch caches freezing.</summary>
	public const int CachesFreezing = 50401;

	/// <summary>Individual cache frozen.</summary>
	public const int CacheFrozen = 50402;

	/// <summary>All Dispatch caches frozen successfully.</summary>
	public const int CachesFreezeComplete = 50403;

	/// <summary>Cache optimization hosted service started.</summary>
	public const int CacheOptimizationStarted = 50410;

	/// <summary>Auto-freeze disabled via configuration.</summary>
	public const int CacheAutoFreezeDisabled = 50411;

	/// <summary>Hot reload detected, skipping cache freeze.</summary>
	public const int CacheHotReloadDetected = 50412;

	/// <summary>Failed to freeze caches on startup.</summary>
	public const int CacheFreezeFailed = 50413;

	/// <summary>Freeze lock acquisition timed out.</summary>
	public const int CacheFreezeLockTimeout = 50414;
}
