// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Out-of-process (default toolchain) config for <see cref="DispatchHotPathBreakdownBenchmarks"/>, whose
/// absolute allocation numbers feed the <c>DispatchHotPath</c> performance gate
/// (<c>eng/validate-performance-gates.ps1</c>).
/// </summary>
/// <remarks>
/// The broader diagnostics suite uses <see cref="DiagnosticsBenchmarkConfig"/> with the
/// <c>InProcessEmitToolchain</c> for speed, which is fine for the relative/ratio metrics it is built for.
/// But <c>InProcessEmitToolchain</c> reports <b>unstable absolute allocation</b>: for an identical true
/// allocation (verified 232 B on both <c>main</c> and this branch via the out-of-process Default
/// toolchain) it reported 208 B on one code revision and 672 B on another — the larger post-ADR-335
/// methods trigger JIT-during-measurement allocation that the in-process harness misattributes. Gating an
/// absolute byte threshold on that measurement is therefore flaky. The default out-of-process toolchain
/// isolates the measured process and reports the true, stable allocation, so the gate's byte thresholds
/// stay meaningful. Ratio metrics are unaffected (they are intra-class and toolchain-invariant).
/// </remarks>
internal sealed class DispatchHotPathGateConfig : ManualConfig
{
	public DispatchHotPathGateConfig() =>
		AddJob(Job.Default
			.WithId("hotpath-gate")
			.WithWarmupCount(5)
			.WithIterationCount(10)
			.DontEnforcePowerPlan());
}
