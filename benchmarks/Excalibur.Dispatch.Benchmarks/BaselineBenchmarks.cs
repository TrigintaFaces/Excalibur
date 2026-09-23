// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
