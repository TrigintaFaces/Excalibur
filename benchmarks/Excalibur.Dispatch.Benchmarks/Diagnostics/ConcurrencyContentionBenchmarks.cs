// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Measures throughput and p95 latency under parallel dispatch to identify lock/cache contention breakpoints.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class ConcurrencyContentionBenchmarks
{
	private static readonly double NanosecondsPerTick = 1_000_000_000.0 / Stopwatch.Frequency;

	private DiagnosticBenchmarkFixture? _fixture;
	private readonly DiagnosticCommand _command = new(42);

	[Params(1, 2, 4, 8, 16, 32)]
	public int WorkerCount { get; set; }

	[Params(256)]
	public int OperationsPerWorker { get; set; }

	[Benchmark(Baseline = true, Description = "Parallel throughput ops/sec")]
	public async Task<double> ParallelThroughputOpsPerSecond()
	{
		var stats = await RunParallelDispatchAsync().ConfigureAwait(false);
		return stats.ThroughputOpsPerSecond;
	}

	[Benchmark(Description = "Parallel dispatch p95 latency (ns)")]
	public async Task<double> ParallelP95LatencyNanoseconds()
	{
		var stats = await RunParallelDispatchAsync().ConfigureAwait(false);
		return stats.P95Nanoseconds;
	}

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

	private async Task<ConcurrencyRunStats> RunParallelDispatchAsync()
	{
		var sampledLatenciesByWorker = new double[WorkerCount][];
		var workerTasks = new Task[WorkerCount];
		var startedAt = Stopwatch.GetTimestamp();
		for (var workerIndex = 0; workerIndex < WorkerCount; workerIndex++)
		{
			var capturedWorkerIndex = workerIndex;
			workerTasks[capturedWorkerIndex] = Task.Run(async () =>
			{
				var localSamples = new List<double>(OperationsPerWorker / 8 + 4);
				for (var operation = 0; operation < OperationsPerWorker; operation++)
				{
					var operationStart = Stopwatch.GetTimestamp();
					_ = await _fixture!.Dispatcher
						.DispatchAsync(_command, _fixture.CreateContext(), CancellationToken.None)
						.ConfigureAwait(false);

					if ((operation & 0b111) == 0)
					{
						var elapsedTicks = Stopwatch.GetTimestamp() - operationStart;
						localSamples.Add(elapsedTicks * NanosecondsPerTick);
					}
				}

				sampledLatenciesByWorker[capturedWorkerIndex] = [.. localSamples];
			});
		}

		await Task.WhenAll(workerTasks).ConfigureAwait(false);
		var elapsedTicks = Stopwatch.GetTimestamp() - startedAt;

		var sampleCount = sampledLatenciesByWorker.Sum(static samples => samples?.Length ?? 0);
		var mergedSamples = new double[sampleCount];
		var offset = 0;
		for (var i = 0; i < sampledLatenciesByWorker.Length; i++)
		{
			var workerSamples = sampledLatenciesByWorker[i];
			if (workerSamples is null || workerSamples.Length == 0)
			{
				continue;
			}

			Array.Copy(workerSamples, 0, mergedSamples, offset, workerSamples.Length);
			offset += workerSamples.Length;
		}

		Array.Sort(mergedSamples);
		var p95Index = mergedSamples.Length == 0
			? 0
			: (int)Math.Ceiling((mergedSamples.Length - 1) * 0.95);
		var p95 = mergedSamples.Length == 0 ? 0 : mergedSamples[p95Index];

		var elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
		var totalOperations = WorkerCount * OperationsPerWorker;
		return new ConcurrencyRunStats(
			ThroughputOpsPerSecond: totalOperations / Math.Max(elapsedSeconds, double.Epsilon),
			P95Nanoseconds: p95);
	}

	[StructLayout(LayoutKind.Auto)]
	private readonly record struct ConcurrencyRunStats(
		double ThroughputOpsPerSecond,
		double P95Nanoseconds);
}
