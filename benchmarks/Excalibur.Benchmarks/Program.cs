// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using Excalibur.Benchmarks.EventSourcing;

namespace Excalibur.Benchmarks;

public static class Program
{
	/// <summary>
	/// Entry point. Dispatches <c>throughput</c> to the allocation diagnostic and <c>feed-spike</c>
	/// to the isolated end-to-end feed experiment. Other arguments go to BenchmarkDotNet.
	/// </summary>
	/// <remarks>
	/// The verb exists because the two instruments answer different questions and cannot be merged.
	/// BenchmarkDotNet measures per-operation cost under its own invocation model; the throughput harness
	/// counts completed appends inside a wall clock. A question about a rate under contention is the
	/// second kind, and forcing it through the first is what produced figures whose direction was an
	/// artifact. Those allocation instruments call the same append methods. The separate feed spike
	/// compares research protocols including outbox, publication and durable projection completion;
	/// it does not measure shipping-provider capacity.
	/// </remarks>
	public static int Main(string[] args)
	{
		if (args.Length > 0 && string.Equals(args[0], "feed-spike", StringComparison.OrdinalIgnoreCase))
		{
			return ResolvedFeedComparison.RunAsync(args[1..]).GetAwaiter().GetResult();
		}

		if (args.Length > 0 && string.Equals(args[0], "throughput", StringComparison.OrdinalIgnoreCase))
		{
			return AppendThroughputHarness.RunAsync(args[1..]).GetAwaiter().GetResult();
		}

		return BenchmarkExecution.Run(typeof(Program).Assembly, args);
	}
}
