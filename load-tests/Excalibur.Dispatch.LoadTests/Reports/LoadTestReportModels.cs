// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

namespace Excalibur.Dispatch.LoadTests.Reports;

/// <summary>
/// Aggregated load test report data.
/// </summary>
public record LoadTestReport
{
	public required string TestName { get; init; }
	public required string Description { get; init; }
	public required DateTime StartTime { get; init; }
	public required DateTime EndTime { get; init; }
	public required TimeSpan Duration { get; init; }
	public required ScenarioStats[] Scenarios { get; init; }
	public required SlaValidation SlaResults { get; init; }
}

/// <summary>
/// Statistics for a single scenario.
/// </summary>
public record ScenarioStats
{
	public required string Name { get; init; }
	public required int RequestCount { get; init; }
	public required int OkCount { get; init; }
	public required int FailCount { get; init; }
	public required double Rps { get; init; }
	public required LatencyStats Latency { get; init; }
	public required DataTransferStats DataTransfer { get; init; }
	public required StatusCodeStats[] StatusCodes { get; init; }
}

/// <summary>
/// Latency percentile statistics.
/// </summary>
public record LatencyStats
{
	public required double MinMs { get; init; }
	public required double MeanMs { get; init; }
	public required double MaxMs { get; init; }
	public required double StdDev { get; init; }
	public required double P50Ms { get; init; }
	public required double P75Ms { get; init; }
	public required double P95Ms { get; init; }
	public required double P99Ms { get; init; }
}

/// <summary>
/// Data transfer statistics.
/// </summary>
public record DataTransferStats
{
	public required long AllBytes { get; init; }
}

/// <summary>
/// Status code distribution.
/// </summary>
public record StatusCodeStats
{
	public required string StatusCode { get; init; }
	public required int Count { get; init; }
	public required double Percentage { get; init; }
}

/// <summary>
/// SLA validation results.
/// </summary>
public record SlaValidation
{
	public required bool Passed { get; init; }
	public required SlaCheck[] Checks { get; init; }
}

/// <summary>
/// Individual SLA check result.
/// </summary>
public record SlaCheck
{
	public required string Name { get; init; }
	public required string Description { get; init; }
	public required double Threshold { get; init; }
	public required double Actual { get; init; }
	public required bool Passed { get; init; }
}

/// <summary>
/// Configuration for SLA thresholds.
/// </summary>
public class SlaThresholds
{
	public double MaxP95LatencyMs { get; set; } = 100;
	public double MaxP99LatencyMs { get; set; } = 500;
	public double MinSuccessRate { get; set; } = 99.0;
	public double MinRps { get; set; } = 100;
}
