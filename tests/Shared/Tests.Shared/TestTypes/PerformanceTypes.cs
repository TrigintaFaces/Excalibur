// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.TestTypes;

/// <summary>
/// Performance report for test analysis.
/// </summary>
public class PerformanceReport
{
	/// <summary>Gets or sets when the report was generated.</summary>
	public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets the number of results analyzed.</summary>
	public int ResultCount { get; set; }

	/// <summary>Gets or sets the output directory.</summary>
	public string OutputDirectory { get; set; } = string.Empty;

	/// <summary>Gets or sets the summary.</summary>
	public string Summary { get; set; } = string.Empty;

	/// <summary>Gets or sets the trend analysis.</summary>
	public PerformanceTrendAnalysis? TrendAnalysis { get; set; }
}

/// <summary>
/// Performance trend analysis result.
/// </summary>
public class PerformanceTrendAnalysis
{
	/// <summary>Gets or sets the analysis timestamp.</summary>
	public DateTimeOffset AnalysisTimestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets the result count.</summary>
	public int ResultCount { get; set; }

	/// <summary>Gets or sets the time span analyzed.</summary>
	public TimeSpan TimeSpan { get; set; }

	/// <summary>Gets or sets the memory trend.</summary>
	public TrendValue MemoryTrend { get; set; } = new();

	/// <summary>Gets or sets the CPU trend.</summary>
	public TrendValue CpuTrend { get; set; } = new();

	/// <summary>Gets or sets the GC trend.</summary>
	public TrendValue GcTrend { get; set; } = new();

	/// <summary>Gets or sets the success rate trend.</summary>
	public TrendValue SuccessRateTrend { get; set; } = new();

	/// <summary>Gets or sets any anomalies detected.</summary>
	public List<string> Anomalies { get; set; } = new();

	/// <summary>Gets or sets the stability score.</summary>
	public double StabilityScore { get; set; }
}

/// <summary>
/// A trend value with slope and direction.
/// </summary>
public class TrendValue
{
	/// <summary>Gets or sets the slope.</summary>
	public double Slope { get; set; }

	/// <summary>Gets or sets the direction.</summary>
	public string Direction { get; set; } = "stable";

	/// <summary>Gets or sets whether the trend is significant.</summary>
	public bool IsSignificant { get; set; }
}

/// <summary>
/// Stress test result for performance analysis.
/// </summary>
public class StressTestResult
{
	/// <summary>Gets or sets when the test started.</summary>
	public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets when the test ended.</summary>
	public DateTimeOffset EndTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets whether the test succeeded.</summary>
	public bool Success { get; set; } = true;

	/// <summary>Gets or sets the error message if failed.</summary>
	public string? ErrorMessage { get; set; }

	/// <summary>Gets or sets the test name.</summary>
	public string TestName { get; set; } = string.Empty;

	/// <summary>Gets the test duration.</summary>
	public TimeSpan Duration => EndTime - StartTime;

	/// <summary>Gets the performance statistics.</summary>
	public PerformanceStatistics GetPerformanceStatistics() => new();
}

/// <summary>
/// Performance statistics from a stress test.
/// </summary>
public class PerformanceStatistics
{
	/// <summary>Gets or sets memory growth in MB.</summary>
	public double MemoryGrowthMb { get; set; }

	/// <summary>Gets or sets average CPU usage.</summary>
	public double AverageCpuUsage { get; set; }

	/// <summary>Gets or sets total GC collections.</summary>
	public int TotalGcCollections { get; set; }

	/// <summary>Gets or sets throughput in messages per second.</summary>
	public double ThroughputPerSecond { get; set; }

	/// <summary>Gets or sets average latency in milliseconds.</summary>
	public double AverageLatencyMs { get; set; }
}

/// <summary>
/// Metrics snapshot for performance tracking.
/// </summary>
public class MetricsSnapshot
{
	/// <summary>Gets or sets the timestamp.</summary>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets the metrics values.</summary>
	public Dictionary<string, double> Values { get; set; } = new();

	/// <summary>Gets or sets the counters.</summary>
	public Dictionary<string, long> Counters { get; set; } = new();
}
