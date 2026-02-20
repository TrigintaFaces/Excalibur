// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Perfolizer.Horology;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

internal sealed class DiagnosticsBenchmarkConfig : ManualConfig
{
	private static Job CreateDefaultJob() =>
		Job.Default
			.WithId("diag-default")
			.WithToolchain(InProcessEmitToolchain.Instance)
			.WithLaunchCount(1)
			.WithWarmupCount(3)
			.WithIterationCount(8)
			.WithUnrollFactor(1);

	public DiagnosticsBenchmarkConfig()
	{
		AddJob(CreateDefaultJob());
	}

	public static Job CreateColdStartJob() =>
		CreateDefaultJob()
			.WithId("diag-cold")
			.WithStrategy(RunStrategy.ColdStart)
			.WithWarmupCount(0)
			.WithIterationCount(20)
			.WithIterationTime(TimeInterval.FromMilliseconds(100));
}
