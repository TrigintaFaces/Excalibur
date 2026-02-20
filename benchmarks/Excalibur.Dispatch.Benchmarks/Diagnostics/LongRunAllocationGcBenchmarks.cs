// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Sustained-dispatch diagnostics for latency distribution, allocation rate, and GC pressure.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class LongRunAllocationGcBenchmarks
{
	private const int MaxLatencySamples = 200_000;
	private const int SampleEveryOperations = 32;
	private static readonly double NanosecondsPerTick = 1_000_000_000.0 / Stopwatch.Frequency;

	private DiagnosticBenchmarkFixture? _fixture;
	private readonly DiagnosticCommand _command = new(42);

	[Params(10_000, 50_000, 100_000)]
	public int OperationCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_fixture = new DiagnosticBenchmarkFixture(middlewareCount: 1, eventHandlerCount: 3);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_fixture?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Long-run throughput (ops/sec)")]
	public async Task<double> ThroughputOpsPerSecond()
	{
		var stats = await RunBatchAsync().ConfigureAwait(false);
		return stats.OperationsPerSecond;
	}

	[Benchmark(Description = "Long-run window duration (ms)")]
	public async Task<double> WindowDurationMilliseconds()
	{
		var stats = await RunBatchAsync().ConfigureAwait(false);
		return stats.WindowDurationMilliseconds;
	}

	[Benchmark(Description = "Long-run latency p50 (ns)")]
	public async Task<double> LatencyP50Nanoseconds()
	{
		var stats = await RunBatchAsync().ConfigureAwait(false);
		return stats.P50Nanoseconds;
	}

	[Benchmark(Description = "Long-run latency p95 (ns)")]
	public async Task<double> LatencyP95Nanoseconds()
	{
		var stats = await RunBatchAsync().ConfigureAwait(false);
		return stats.P95Nanoseconds;
	}

	[Benchmark(Description = "Long-run latency p99 (ns)")]
	public async Task<double> LatencyP99Nanoseconds()
	{
		var stats = await RunBatchAsync().ConfigureAwait(false);
		return stats.P99Nanoseconds;
	}

	[Benchmark(Description = "Long-run allocated bytes/op")]
	public async Task<double> AllocatedBytesPerOperation()
	{
		var stats = await RunBatchAsync().ConfigureAwait(false);
		return stats.AllocatedBytesPerOperation;
	}

	[Benchmark(Description = "Long-run Gen0 collections/sec")]
	public async Task<double> Gen0CollectionsPerSecond()
	{
		var stats = await RunBatchAsync().ConfigureAwait(false);
		return stats.Gen0CollectionsPerSecond;
	}

	private async Task<LongRunBatchStats> RunBatchAsync()
	{
		var estimatedSamples = Math.Clamp(OperationCount / SampleEveryOperations, 256, MaxLatencySamples);
		var latencySamples = new List<double>(capacity: estimatedSamples);

		var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
		var gen0Start = GC.CollectionCount(0);
		var gen1Start = GC.CollectionCount(1);
		var gen2Start = GC.CollectionCount(2);

		var wallClockStart = Stopwatch.GetTimestamp();
		for (var operation = 1; operation <= OperationCount; operation++)
		{
			var operationStart = Stopwatch.GetTimestamp();
			_ = await _fixture!.Dispatcher
				.DispatchAsync(_command, _fixture.CreateContext(), CancellationToken.None)
				.ConfigureAwait(false);

			if ((operation % SampleEveryOperations) == 0 && latencySamples.Count < MaxLatencySamples)
			{
				var operationElapsedTicks = Stopwatch.GetTimestamp() - operationStart;
				latencySamples.Add(ToNanoseconds(operationElapsedTicks));
			}
		}
		var elapsedTicks = Stopwatch.GetTimestamp() - wallClockStart;

		var allocatedEnd = GC.GetAllocatedBytesForCurrentThread();
		var gen0End = GC.CollectionCount(0);
		var gen1End = GC.CollectionCount(1);
		var gen2End = GC.CollectionCount(2);

		latencySamples.Sort();
		var elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
		var operations = OperationCount;

		return new LongRunBatchStats(
			OperationsPerSecond: operations / Math.Max(elapsedSeconds, double.Epsilon),
			WindowDurationMilliseconds: elapsedSeconds * 1000,
			P50Nanoseconds: Percentile(latencySamples, 0.50),
			P95Nanoseconds: Percentile(latencySamples, 0.95),
			P99Nanoseconds: Percentile(latencySamples, 0.99),
			AllocatedBytesPerOperation: (allocatedEnd - allocatedStart) / Math.Max((double)operations, 1),
			Gen0CollectionsPerSecond: (gen0End - gen0Start) / Math.Max(elapsedSeconds, double.Epsilon),
			Gen1CollectionsPerSecond: (gen1End - gen1Start) / Math.Max(elapsedSeconds, double.Epsilon),
			Gen2CollectionsPerSecond: (gen2End - gen2Start) / Math.Max(elapsedSeconds, double.Epsilon));
	}

	private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
	{
		if (sortedValues.Count == 0)
		{
			return 0;
		}

		var clamped = Math.Clamp(percentile, 0, 1);
		var index = (int)Math.Round((sortedValues.Count - 1) * clamped, MidpointRounding.AwayFromZero);
		return sortedValues[index];
	}

	private static double ToNanoseconds(long elapsedTicks)
	{
		return elapsedTicks * NanosecondsPerTick;
	}

	[StructLayout(LayoutKind.Auto)]
	private readonly record struct LongRunBatchStats(
		double OperationsPerSecond,
		double WindowDurationMilliseconds,
		double P50Nanoseconds,
		double P95Nanoseconds,
		double P99Nanoseconds,
		double AllocatedBytesPerOperation,
		double Gen0CollectionsPerSecond,
		double Gen1CollectionsPerSecond,
		double Gen2CollectionsPerSecond);
}
