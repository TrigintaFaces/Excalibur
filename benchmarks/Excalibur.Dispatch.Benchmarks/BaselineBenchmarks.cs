// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Excalibur.Dispatch.Benchmarks;

/// <summary>
/// Baseline benchmarks to verify BenchmarkDotNet setup (Sprint 26 - bd-bench-setup).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class BaselineBenchmarks
{
	private readonly string _data = "Hello, BenchmarkDotNet!";

	/// <summary>
	/// Simple string concatenation benchmark (baseline sanity check).
	/// </summary>
	[Benchmark(Baseline = true)]
	public string StringConcatenation()
	{
		return _data + " " + "Sprint 26";
	}

	/// <summary>
	/// String interpolation benchmark (baseline sanity check).
	/// </summary>
	[Benchmark]
	public string StringInterpolation()
	{
		return $"{_data} Sprint 26";
	}

	/// <summary>
	/// String.Format benchmark (baseline sanity check).
	/// </summary>
	[Benchmark]
	public string StringFormat()
	{
		return string.Format("{0} {1}", _data, "Sprint 26");
	}
}
