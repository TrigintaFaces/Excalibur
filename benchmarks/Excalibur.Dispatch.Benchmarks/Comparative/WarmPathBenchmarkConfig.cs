// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

/// <summary>
/// Warm-path benchmark config for published cross-framework comparisons.
/// Uses BDN defaults (auto-calibrated InvocationCount and UnrollFactor)
/// to measure steady-state throughput with warm JIT and warm caches.
/// </summary>
/// <remarks>
/// <para>
/// This config produces numbers representative of production workloads where
/// the JIT has warmed up and caches are populated. Use for published competitor
/// comparisons and documentation.
/// </para>
/// <para>
/// For CI regression gates, use <see cref="ComparativeBenchmarkConfig"/> instead
/// (cold single-dispatch with InvocationCount:1, UnrollFactor:1).
/// </para>
/// </remarks>
internal sealed class WarmPathBenchmarkConfig : ManualConfig
{
	public WarmPathBenchmarkConfig()
	{
		AddJob(Job.Default
			.WithId("warmpath-inproc")
			.WithToolchain(InProcessEmitToolchain.Instance)
			.DontEnforcePowerPlan());
	}
}
