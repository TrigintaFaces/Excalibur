// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Benchmarks.Comparative;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Answers one question about the published +72 B on every pre-routed remote row: is it an
/// <see cref="System.Threading.ExecutionContext"/> copy-on-write, or a plain object allocation?
/// </summary>
/// <remarks>
/// <para>
/// The discriminator is density. Writing an <see cref="AsyncLocal{T}"/> copies the context's value
/// map, so the bytes that write costs grow with how many async-locals are already live. A plain
/// object allocation does not care. So: vary the number of live async-locals in the caller's
/// context, re-measure the same dispatch, and read the slope.
/// </para>
/// <para>
/// Allocation, not timing: <see cref="GC.GetAllocatedBytesForCurrentThread"/> is exact and
/// indifferent to what else the host is doing, which is why this is a probe and not a benchmark.
/// Two controls bound the reading -- a push arm that MUST scale (if it does not, the densities
/// never took effect and every other row is meaningless) and a fixed-allocation arm that MUST NOT.
/// </para>
/// </remarks>
internal static class AsyncLocalDensityProbe
{
	private static readonly int[] Densities = [0, 1, 3, 7, 15];
	private const int WarmupIterations = 5_000;
	private const int MeasuredIterations = 50_000;

	public static async Task RunAsync()
	{
		var bench = new RoutingFirstParityBenchmarks();
		bench.GlobalSetup();

		var pushTarget = new AsyncLocal<object>();
		object pushValueA = new();
		object pushValueB = new();
		var pushToggle = false;
		object? allocSink = null;

		Console.WriteLine($"AsyncLocal density probe -- {MeasuredIterations} iterations/cell, bytes per operation");
		Console.WriteLine(
			"density | ctl:push (MUST scale) | ctl:new byte[8] (flat) | ctl:await noop (flat) | remote SQS | local cmd");

		foreach (var density in Densities)
		{
			var held = new AsyncLocal<object>[density];
			for (var i = 0; i < density; i++)
			{
				held[i] = new AsyncLocal<object>();
				held[i].Value = new object();
			}

			// Alternate the pushed value: the setter early-outs when the new value is reference-equal
			// to the old one, so pushing a constant measures nothing and reports a flat zero.
			var push = Measure(() =>
			{
				pushToggle = !pushToggle;
				pushTarget.Value = pushToggle ? pushValueA : pushValueB;
			});
			// The push arm leaves a value set, which would silently make every row below it one
			// async-local denser than its own label claims. Clear it before anything else is measured.
			pushTarget.Value = null!;

			var alloc = Measure(() => allocSink = new byte[8]);
			var awaitNoop = await MeasureAsync(NoopAsync).ConfigureAwait(false);
			var remote = await MeasureAsync(bench.Dispatch_PreRoutedRemoteEvent_AwsSqs).ConfigureAwait(false);
			var local = await MeasureAsync(bench.Dispatch_PreRoutedLocalCommand).ConfigureAwait(false);

			Console.WriteLine(
				$"{density,7} | {push,21:F2} | {alloc,23:F2} | {awaitNoop,22:F2} | {remote,10:F2} | {local,9:F2}");

			for (var i = 0; i < density; i++)
			{
				held[i].Value = null!;
			}
		}

		GC.KeepAlive(allocSink);
		bench.GlobalCleanup();
	}

	/// <summary>
	/// The async harness itself, with no dispatch in it. If this scales with density then the
	/// scaling belongs to the probe's own await loop and every dispatch row below is worthless.
	/// </summary>
	private static Task<int> NoopAsync() => Task.FromResult(1);

	private static double Measure(Action op)
	{
		for (var i = 0; i < WarmupIterations; i++)
		{
			op();
		}

		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < MeasuredIterations; i++)
		{
			op();
		}

		return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)MeasuredIterations;
	}

	private static async Task<double> MeasureAsync<T>(Func<Task<T>> op)
	{
		for (var i = 0; i < WarmupIterations; i++)
		{
			_ = await op().ConfigureAwait(false);
		}

		// GetAllocatedBytesForCurrentThread is per-thread, so a continuation that resumed elsewhere
		// would silently drop its allocations out of the reading. Refuse the number if that happened.
		var threadBefore = Environment.CurrentManagedThreadId;
		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < MeasuredIterations; i++)
		{
			_ = await op().ConfigureAwait(false);
		}

		var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
		return Environment.CurrentManagedThreadId == threadBefore
			? bytes / (double)MeasuredIterations
			: double.NaN;
	}
}
