// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Reports;

using Excalibur.Benchmarks;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[CollectionDefinition("BenchmarkValidationLifecycle", DisableParallelization = true)]
public sealed class BenchmarkValidationLifecycleCollection;

[Collection("BenchmarkValidationLifecycle")]
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class BenchmarkValidationLifecycleShould
{
    [Fact]
    public void RejectAnEmptyRun() => BenchmarkExecution.GetExitCode(Array.Empty<Summary>()).ShouldBe(1);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    public void FailTheActualRunnerWhenIterationCleanupFails(int failOnCleanup, int expectedExitCode)
    {
        LifecycleBenchmark.FailOnCleanup = failOnCleanup;
        LifecycleBenchmark.CleanupCalls = 0;
        var artifacts = Path.Combine(Path.GetTempPath(), "es15-bdn", Guid.NewGuid().ToString("N"));
        var exitCode = BenchmarkExecution.Run(typeof(LifecycleBenchmark).Assembly, [
            "--filter", "*LifecycleBenchmark.Measure*", "--artifacts", artifacts,
        ], out var diagnosis);

        // The EXIT CODE is asserted first, and the order is deliberate. The cleanup-count assertion used
        // to come first, so when BenchmarkDotNet declined to run at all the failure read
        // "CleanupCalls should be >= 4 but was 0" -- a count, with no hint that nothing had executed or
        // why. The commonest cause is running this suite in DEBUG: BenchmarkDotNet refuses to measure a
        // non-optimized assembly, and CI always passes --configuration Release. The diagnosis below
        // carries that reason out of the runner, so the next person reads the cause instead of a zero.
        exitCode.ShouldBe(expectedExitCode, diagnosis);
        LifecycleBenchmark.CleanupCalls.ShouldBeGreaterThanOrEqualTo(failOnCleanup == 0 ? 4 : failOnCleanup, diagnosis);
    }
}

[MemoryDiagnoser]
[InProcess]
[WarmupCount(1)]
[IterationCount(1)]
[InvocationCount(1, unrollFactor: 1)]
public class LifecycleBenchmark
{
    public static int FailOnCleanup { get; set; }

    public static int CleanupCalls { get; set; }

    [IterationSetup]
    public void Setup() { }

    [Benchmark]
    public long Measure() => Environment.TickCount64;

    [IterationCleanup]
    public async Task Cleanup()
    {
        await Task.Yield();
        CleanupCalls++;
        // 1: JIT, 2: warmup, 3: actual measurement, 4: allocation diagnostic invocation.
        if (FailOnCleanup > 0 && CleanupCalls >= FailOnCleanup)
        {
            throw new InvalidOperationException("intentional persistence mismatch");
        }
    }
}
