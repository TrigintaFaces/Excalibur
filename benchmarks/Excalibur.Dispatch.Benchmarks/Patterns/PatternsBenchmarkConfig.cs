// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

internal sealed class PatternsBenchmarkConfig : ManualConfig
{
	public PatternsBenchmarkConfig()
	{
		AddJob(Job.Default
			.WithId("patterns-inproc")
			.WithToolchain(InProcessEmitToolchain.Instance)
			.WithInvocationCount(1)
			.WithUnrollFactor(1)
			.WithLaunchCount(1)
			.WithWarmupCount(2)
			.WithIterationCount(5)
			.DontEnforcePowerPlan());
	}
}
