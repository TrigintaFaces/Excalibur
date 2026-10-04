// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Data.SqlClient;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>Descriptive diagnostics for synthetic SQL allocation/insert arms.</summary>
/// <remarks>
/// This is not a complete event-store or subscriber benchmark. Exit zero means all requested
/// diagnostic operations completed; it does not certify performance, correctness or reproducibility.
/// Exit two indicates invalid/incomplete execution. Successful-operation samples, including warmup,
/// are retained without trimming. Failures may have committed before acknowledgement was lost.
/// Rotated slice order reduces fixed-order bias but cannot eliminate drift or carryover effects.
/// Empty-table resets equalize starting contents, not growth within each timed slice.
/// </remarks>
internal static class AppendThroughputHarness
{
	private static readonly int[] DefaultWriters = [1, 8, 16, 32];
	private static readonly int[] DefaultEvents = [1, 5];
	private static readonly string[] Arms = ["identity", "counter", "counter-batched"];

	/// <summary>Runs the matrix and prints the report. See the type remarks for the exit codes.</summary>
	internal static Task<int> RunAsync(string[] args, TextWriter? errors = null)
	{
		errors ??= Console.Error;
		return ExecuteDiagnosticAsync(() => RunCoreAsync(args, errors), errors);
	}

	internal static async Task<int> ExecuteDiagnosticAsync(Func<Task<int>> execute, TextWriter errors)
	{
		try
		{
			return await execute().ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
		{
			await errors.WriteLineAsync("INVALID CONFIGURATION: " + ex.GetType().Name).ConfigureAwait(false);
			return 2;
		}
		catch (Exception ex)
		{
			await errors.WriteLineAsync("EXECUTION INCOMPLETE: " + ex.GetType().Name
				+ ". Retain partial artifacts; no successful diagnostic result was produced.").ConfigureAwait(false);
			return 2;
		}
	}

	internal static bool ExecutionCompleted(List<Cell> results, long expectedCells) =>
		expectedCells > 0 && results.Count == expectedCells
		&& results.Select(r => (r.Run, r.Arm, r.Writers, r.Events)).Distinct().Take(results.Count + 1).Count() == results.Count
		&& results.TrueForAll(r => r.Failures == 0 && r.Appends > 0
			&& double.IsFinite(r.AppendsPerSecond) && r.AppendsPerSecond > 0);

	/// <summary>Measures each combination once per slice round, rotating the starting position.</summary>
	internal static async Task<List<Cell>> MeasureBlockAsync(
		int run,
		int[] writers,
		int[] events,
		int warmupSlices,
		int measuredSlices,
		TimeSpan slice,
		int slowWriters,
		TimeSpan slowHold,
		StreamWriter csv, StreamWriter slices,
		Func<Task>? reset = null,
		Func<string, int, int, TimeSpan, int, TimeSpan, Task<SliceResult>>? drive = null)
	{
		var combinations = (from arm in Arms
							from writerCount in writers
							from eventCount in events
							select (Arm: arm, Writers: writerCount, Events: eventCount)).ToList();

		var latencies = new Dictionary<(string Arm, int Writers, int Events), List<double>>();
		var observed = new Dictionary<(string Arm, int Writers, int Events), double>();
		var failed = new Dictionary<(string Arm, int Writers, int Events), int>();
		foreach (var combination in combinations)
		{
			latencies[combination] = [];
			observed[combination] = 0;
			failed[combination] = 0;
		}

		for (var sliceIndex = 0; sliceIndex < warmupSlices + measuredSlices; sliceIndex++)
		{
			var counted = sliceIndex >= warmupSlices;

			for (var order = 0; order < combinations.Count; order++)
			{
				var combination = combinations[(order + sliceIndex + run - 1) % combinations.Count];
				// Before EVERY slice, not once per block: see the type remarks. Two arms share the counter
				// table, so anything coarser lets one of them measure the other's rows.
				var resetStarted = Stopwatch.GetTimestamp();
				try
				{
					await (reset ?? AppendAllocationStrategyBenchmarks.ResetAsync)().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// A failed TRUNCATE means this slice would start from an unknown table, so it is not
					// measured at all -- and it is recorded, so the run refuses rather than quietly
					// reporting a cell with fewer slices than its siblings.
					failed[combination]++;
					await slices.WriteLineAsync(Inv($"{run},{combination.Arm},{combination.Writers},{combination.Events},")
						+ Inv($"{sliceIndex},{(counted ? 1 : 0)},{Stopwatch.GetElapsedTime(resetStarted).TotalSeconds:R},0,1,reset")).ConfigureAwait(false);
					await slices.FlushAsync().ConfigureAwait(false);
					Console.WriteLine(
						Inv($"  !! reset failed for {combination.Arm} W={combination.Writers} ")
						+ Inv($"ev={combination.Events} slice {sliceIndex}: ")
						+ ex.Message.Split('\n')[0].Trim());
					continue;
				}

				var slice3 = await (drive ?? DriveSliceAsync)(
						combination.Arm, combination.Writers, combination.Events, slice, slowWriters, slowHold)
					.ConfigureAwait(false);

				await slices.WriteLineAsync(Inv($"{run},{combination.Arm},{combination.Writers},{combination.Events},")
					+ Inv($"{sliceIndex},{(counted ? 1 : 0)},{slice3.Seconds:R},{slice3.Samples.Count},{slice3.Failures},operation")).ConfigureAwait(false);
				await slices.FlushAsync().ConfigureAwait(false);
				failed[combination] += slice3.Failures;
				if (slice3.Failures > 0)
				{
					Console.WriteLine(Inv($"  !! {slice3.Failures} failed operation(s) for {combination.Arm} ")
						+ Inv($"W={combination.Writers} ev={combination.Events} slice={sliceIndex} counted={counted}: ")
						+ slice3.FirstFailure);
				}

				if (counted)
				{
					observed[combination] += slice3.Seconds;
				}

				foreach (var (latencyMs, offsetSeconds) in slice3.Samples)
				{
					if (counted)
					{
						latencies[combination].Add(latencyMs);
					}

					await csv.WriteLineAsync(
						Inv($"{run},{combination.Arm},{combination.Writers},{combination.Events},")
						+ Inv($"{sliceIndex},{(counted ? 1 : 0)},{latencyMs:0.####},{offsetSeconds:0.####}"))
						.ConfigureAwait(false);
				}
			}
		}

		var cells = new List<Cell>(combinations.Count);
		foreach (var combination in combinations)
		{
			var sorted = latencies[combination];
			sorted.Sort();
			var seconds = observed[combination];

			cells.Add(new Cell(
				run,
				combination.Arm,
				combination.Writers,
				combination.Events,
				sorted.Count,
				seconds <= 0 ? double.NaN : sorted.Count / seconds,
				Percentile(sorted, 0.50),
				Percentile(sorted, 0.95),
				Percentile(sorted, 0.99),
				sorted.Count == 0 ? double.NaN : sorted[^1],
				sorted,
				failed[combination]));
		}

		return cells;
	}

	/// <summary>Times whole-operation completion, including asynchronous resource cleanup.</summary>
	internal static async Task<SliceResult> DriveOperationsAsync(
		int writerCount, TimeSpan slice, Func<int, Task> operation, Func<long>? timestamp = null)
	{
		timestamp ??= Stopwatch.GetTimestamp;

		var collected = new ConcurrentBag<(long CompletedAt, long Ticks)>();
		var failures = 0;
		var firstFailure = (string?)null;

		var start = timestamp();
		var deadline = start + (long)(slice.TotalSeconds * Stopwatch.Frequency);

		var work = new Task[writerCount];
		for (var i = 0; i < writerCount; i++)
		{
			var writer = i;

			work[i] = Task.Run(async () =>
			{
				// Stops STARTING work at the deadline; the append already in flight is allowed to finish
				// and is counted. Nothing is cut off at the boundary, so nothing is censored -- and the
				// slice's measured duration below extends to that last completion, so the overrun is paid
				// for in the denominator rather than hidden.
				while (timestamp() < deadline)
				{
					var began = timestamp();
					try
					{
						await operation(writer).ConfigureAwait(false);
						var completed = timestamp();
						collected.Add((completed, completed - began));
					}
					catch (Exception ex)
					{
						// Record lifecycle failure without inferring whether the transaction committed.
						_ = Interlocked.Increment(ref failures);
						_ = Interlocked.CompareExchange(ref firstFailure, ex.Message.Split('\n')[0].Trim(), null);

						// This writer stops for the rest of the slice. Retrying into a wedged server just
						// burns the slice collecting more timeouts.
						return;
					}
				}
			});
		}

		await Task.WhenAll(work).ConfigureAwait(false);
		var finished = timestamp();

		var samples = new List<(double, double)>(collected.Count);
		foreach (var (completedAt, ticks) in collected)
		{
			samples.Add((
				ticks * 1000.0 / Stopwatch.Frequency,
				(completedAt - start) / (double)Stopwatch.Frequency));
		}

		return new SliceResult(samples, (finished - start) / (double)Stopwatch.Frequency, failures, firstFailure);
	}

	/// <summary>One slice of one arm: what completed, how long it really took, and what failed.</summary>
	internal sealed record SliceResult(
		List<(double LatencyMs, double OffsetSeconds)> Samples,
		double Seconds,
		int Failures,
		string? FirstFailure);

	private static async Task<int> RunCoreAsync(string[] args, TextWriter errors)
	{
		var writers = ParseInts(args, "--writers", DefaultWriters);
		var events = ParseInts(args, "--events", DefaultEvents);
		var window = TimeSpan.FromSeconds(ParseDouble(args, "--window", 10));
		var warmup = TimeSpan.FromSeconds(ParseDouble(args, "--warmup", 2));
		var slice = TimeSpan.FromSeconds(ParseDouble(args, "--slice", 2));
		var runs = int.Parse(Value(args, "--runs") ?? "1", CultureInfo.InvariantCulture);

		// The same prefix of writers receives an optional pre-commit delay in every arm.
		var slowHold = TimeSpan.FromMilliseconds(ParseDouble(args, "--slow-ms", 0));
		var slowWriters = int.Parse(Value(args, "--slow-writers") ?? "0", CultureInfo.InvariantCulture);

		if (writers.Length == 0 || events.Length == 0 || writers.Any(w => w is <= 0 or > (int.MaxValue / 4))
			|| events.Any(e => e <= 0) || writers.Distinct().Take(writers.Length + 1).Count() != writers.Length
			|| events.Distinct().Take(events.Length + 1).Count() != events.Length || runs <= 0 || slowWriters < 0
			|| slice <= TimeSpan.Zero || window < slice || warmup < TimeSpan.Zero || slowHold < TimeSpan.Zero
			|| warmup / slice + window / slice > int.MaxValue - 1)
		{
			await errors.WriteLineAsync("INVALID CONFIGURATION: provide distinct positive writer/event counts, "
				+ "positive runs and slice, window >= slice, and nonnegative warmup/slow settings.").ConfigureAwait(false);
			return 2;
		}

		if (string.IsNullOrWhiteSpace(AppendAllocationStrategyBenchmarks.ConnectionString))
		{
			await errors.WriteLineAsync(
				"REFUSE: BENCHMARK_SQL_CONNECTIONSTRING is not set, so nothing was measured. This harness "
				+ "needs a real SQL Server for the synthetic allocation/insert operations.").ConfigureAwait(false);
			return 2;
		}

		// Record scheduling configuration without claiming interference has been eliminated.
		var wantedWorkers = Math.Max(writers.Max() * 4, 256);
		ThreadPool.GetMinThreads(out var hadWorkers, out var hadPorts);
		var poolRaised = ThreadPool.SetMinThreads(Math.Max(wantedWorkers, hadWorkers), hadPorts);

		var warmupSlices = (int)Math.Ceiling(warmup / slice);
		var measuredSlices = Math.Max(1, (int)Math.Round(window / slice));

		Console.WriteLine(
			Inv($"fixed-window append throughput: slice={slice.TotalSeconds:0.#}s ")
			+ Inv($"measured={measuredSlices} slices/arm warmup={warmupSlices} slices/arm runs={runs} ")
			+ Inv($"writers=[{string.Join(',', writers)}] events=[{string.Join(',', events)}]")
			+ Inv($" minworkers={hadWorkers}->{wantedWorkers}{(poolRaised ? string.Empty : " (RAISE FAILED)")}")
			+ (slowWriters > 0 && slowHold > TimeSpan.Zero
				? Inv($" slow={slowWriters} writer(s) holding {slowHold.TotalMilliseconds:0}ms before commit")
				: " slow=none"));
		Console.WriteLine(
			"synthetic SQL diagnostics; rotated slice order, empty starting tables, untrimmed successful "
			+ "samples. Environment effects and within-slice growth remain possible.");
		Console.WriteLine();

		await AppendAllocationStrategyBenchmarks.EnsureSchemaAsync().ConfigureAwait(false);

		var results = new List<Cell>();
		var samplePath = Path.Combine(
			"BenchmarkDotNet.Artifacts",
			"samples",
			Inv($"append-throughput-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv"));
		_ = Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);

		await using (var csv = new StreamWriter(samplePath, append: false))
		await using (var slices = new StreamWriter(samplePath + ".slices.csv", append: false))
		{
			await csv.WriteLineAsync("run,arm,writers,events,slice,counted,latency_ms,offset_s")
				.ConfigureAwait(false);

			await slices.WriteLineAsync("run,arm,writers,events,slice,counted,elapsed_s,successes,failures,stage").ConfigureAwait(false);
			for (var run = 1; run <= runs; run++)
			{
				var block = await MeasureBlockAsync(
						run, writers, events, warmupSlices, measuredSlices, slice, slowWriters, slowHold, csv, slices)
					.ConfigureAwait(false);
				results.AddRange(block);

				foreach (var cell in block)
				{
					Console.WriteLine(
						Inv($"  run {run} {cell.Arm,-16} W={cell.Writers,-3} ev={cell.Events} ")
						+ Inv($"{cell.AppendsPerSecond,8:0.0} appends/s  n={cell.Appends,-6} ")
						+ Inv($"p50={cell.P50Ms,7:0.00}ms p95={cell.P95Ms,8:0.00}ms ")
						+ Inv($"p99={cell.P99Ms,8:0.00}ms max={cell.MaxMs,8:0.00}ms"));
				}
			}
		}

		Console.WriteLine();
		Console.WriteLine(Inv($"raw samples: {samplePath}"));
		Console.WriteLine();

		ReportThroughput(results, writers, events);
		var failures = results.Sum(r => r.Failures);
		if (!ExecutionCompleted(results, (long)runs * writers.Length * events.Length * Arms.Length))
		{
			Console.WriteLine(Inv($"EXECUTION INCOMPLETE: {failures} failed operations (including warmup/reset). ")
				+ "Retain these observations, but do not use the run as a successful comparative result. "
				+ "A failed acknowledgement does not establish rollback.");
			return 2;
		}

		Console.WriteLine("DIAGNOSTIC COMPLETED. No performance acceptance decision was made. "
			+ "Raw observations do not establish production capacity or equivalent feed guarantees.");
		return 0;
	}

	/// <summary>Drives one slice, including connection disposal and the complete drain in timing.</summary>
	private static Task<SliceResult> DriveSliceAsync(
		string arm,
		int writerCount,
		int eventCount,
		TimeSpan slice,
		int slowWriters,
		TimeSpan slowHold)
	{
		return DriveOperationsAsync(writerCount, slice, async writer =>
		{
			var append = AppendFor(arm, eventCount, writer < slowWriters ? slowHold : TimeSpan.Zero);
			await using var connection = new SqlConnection(AppendAllocationStrategyBenchmarks.ConnectionString);
			await connection.OpenAsync().ConfigureAwait(false);
			await append(connection).ConfigureAwait(false);
		});
	}

	/// <summary>Reports descriptive run-level spread without assuming independent append samples.</summary>
	private static void ReportThroughput(List<Cell> results, int[] writers, int[] events)
	{
		Console.WriteLine("DESCRIPTIVE THROUGHPUT: min/median/max across runs; not confidence bounds. "
			+ "A single run has no between-run evidence. Failed cells remain visible.");
		foreach (var writerCount in writers)
		{
			foreach (var eventCount in events)
			{
				foreach (var arm in Arms)
				{
					var cells = results.Where(r => r.Arm == arm && r.Writers == writerCount && r.Events == eventCount).ToList();
					var rates = cells.Select(c => c.AppendsPerSecond).Order().ToList();
					Console.WriteLine(Inv($"  W={writerCount} ev={eventCount} {arm}: runs={rates.Count} ")
						+ Inv($"min={Percentile(rates, 0):0.0} median={Percentile(rates, 0.5):0.0} ")
						+ Inv($"max={Percentile(rates, 1):0.0} appends/s failures={cells.Sum(c => c.Failures)}"));
				}
			}
		}
		Console.WriteLine();
	}

	private static Func<SqlConnection, Task> AppendFor(string arm, int eventCount, TimeSpan hold) => arm switch
	{
		"identity" => c => AppendAllocationStrategyBenchmarks.AppendIdentityAsync(c, eventCount, hold),
		"counter" => c =>
			AppendAllocationStrategyBenchmarks.AppendCounterAsync(c, eventCount, batched: false, hold),
		"counter-batched" => c =>
			AppendAllocationStrategyBenchmarks.AppendCounterAsync(c, eventCount, batched: true, hold),
		_ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "unknown arm"),
	};

	/// <summary>Nearest-rank percentile over the SORTED, untrimmed sample.</summary>
	private static double Percentile(List<double> sorted, double fraction)
	{
		if (sorted.Count == 0)
		{
			return double.NaN;
		}

		var rank = (int)Math.Ceiling(fraction * sorted.Count) - 1;
		return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
	}

	private static int[] ParseInts(string[] args, string name, int[] fallback)
	{
		var raw = Value(args, name);
		return raw is null
			? fallback
			: raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(p => int.Parse(p, CultureInfo.InvariantCulture))
				.ToArray();
	}

	private static double ParseDouble(string[] args, string name, double fallback)
	{
		var raw = Value(args, name);
		var value = raw is null ? fallback : double.Parse(raw, CultureInfo.InvariantCulture);
		return double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(name, "Value must be finite.");
	}

	private static string? Value(string[] args, string name)
	{
		for (var i = 0; i < args.Length; i++)
		{
			if (string.Equals(args[i], name, StringComparison.Ordinal))
			{
				return i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("Missing value for " + name, nameof(args));
			}
		}

		return null;
	}

	private static string Inv(FormattableString text) => FormattableString.Invariant(text);

	/// <summary>One arm, at one writer count and event count, from one run of the matrix.</summary>
	internal sealed record Cell(
		int Run,
		string Arm,
		int Writers,
		int Events,
		int Appends,
		double AppendsPerSecond,
		double P50Ms,
		double P95Ms,
		double P99Ms,
		double MaxMs,
		List<double> Latencies,
		int Failures);
}
