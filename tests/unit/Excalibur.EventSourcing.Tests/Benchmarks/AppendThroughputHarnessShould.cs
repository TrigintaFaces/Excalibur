// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Text;
using Excalibur.Benchmarks.EventSourcing;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class AppendThroughputHarnessShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncludeFailedOperationDrainAfterEarlierSuccess(bool cancelled)
    {
        long tick = 0;
        var calls = 0;
        var result = await AppendThroughputHarness.DriveOperationsAsync(1, TimeSpan.FromSeconds(2), _ =>
        {
            if (++calls == 1)
            {
                tick = Stopwatch.Frequency;
                return Task.CompletedTask;
            }
            tick = 30 * Stopwatch.Frequency;
            return Task.FromException(cancelled ? new OperationCanceledException("acknowledgement unknown") : new TimeoutException("acknowledgement unknown"));
        }, () => tick);

        result.Samples.Count.ShouldBe(1);
        result.Failures.ShouldBe(1);
        result.Seconds.ShouldBe(30);
        result.Samples[0].LatencyMs.ShouldBe(1000);
    }

    [Fact]
    public async Task CountCleanupFailureWithoutAlsoCountingSuccessfulOperation()
    {
        long tick = 0;
        var acknowledged = false;
        var result = await AppendThroughputHarness.DriveOperationsAsync(1, TimeSpan.FromSeconds(1), async _ =>
        {
            await using var resource = new ThrowingCleanup();
            acknowledged = true;
            tick = 2 * Stopwatch.Frequency;
        }, () => tick);

        acknowledged.ShouldBeTrue();
        result.Samples.ShouldBeEmpty();
        result.Failures.ShouldBe(1);
        result.Seconds.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RetainWarmupFailuresAndAllMeasuredCells(bool failWarmup)
    {
        using var raw = new MemoryStream();
        using var sliceRaw = new MemoryStream();
        await using var csv = new StreamWriter(raw, Encoding.UTF8, leaveOpen: true);
        await using var slices = new StreamWriter(sliceRaw, Encoding.UTF8, leaveOpen: true);
        var calls = 0;
        var order = new List<string>();
        var result = await AppendThroughputHarness.MeasureBlockAsync(1, [1], [1], 1, 1,
            TimeSpan.FromSeconds(1), 0, TimeSpan.Zero, csv, slices,
            () => Task.CompletedTask,
            (arm, _, _, _, _, _) =>
            {
                order.Add(arm);
                var failed = ++calls == 1 && failWarmup;
                return Task.FromResult(new AppendThroughputHarness.SliceResult(
                    failed ? [] : [(1000, 1)], 1, failed ? 1 : 0, failed ? "warmup failure" : null));
            });
        await csv.FlushAsync();
        await slices.FlushAsync();

        result.Count.ShouldBe(3);
        result.Sum(c => c.Appends).ShouldBe(3);
        result.Sum(c => c.Failures).ShouldBe(failWarmup ? 1 : 0);
        AppendThroughputHarness.ExecutionCompleted(result, 3).ShouldBe(!failWarmup);
        order.ShouldBe(["identity", "counter", "counter-batched", "counter", "counter-batched", "identity"]);
        Encoding.UTF8.GetString(sliceRaw.ToArray()).ShouldContain(failWarmup
            ? "1,identity,1,1,0,0,1,0,1,operation" : "1,identity,1,1,0,0,1,1,0,operation");
        Encoding.UTF8.GetString(raw.ToArray()).ShouldContain("1,identity,1,1,1,1,");
    }

    [Fact]
    public async Task RejectMissingEmptyNonfiniteAndDuplicateCells()
    {
        using var raw = new MemoryStream();
        await using var csv = new StreamWriter(raw);
        var result = await AppendThroughputHarness.MeasureBlockAsync(1, [1], [1], 0, 1,
            TimeSpan.FromSeconds(1), 0, TimeSpan.Zero, csv, csv,
            () => Task.CompletedTask,
            (_, _, _, _, _, _) => Task.FromResult(new AppendThroughputHarness.SliceResult([], 0, 0, null)));
        AppendThroughputHarness.ExecutionCompleted(result, 3).ShouldBeFalse();
        AppendThroughputHarness.ExecutionCompleted([], 0).ShouldBeFalse();
        var valid = result[0] with { Appends = 1, AppendsPerSecond = 1 };
        AppendThroughputHarness.ExecutionCompleted([valid], 2).ShouldBeFalse();
        AppendThroughputHarness.ExecutionCompleted([valid, valid], 2).ShouldBeFalse();
        AppendThroughputHarness.ExecutionCompleted([valid with { AppendsPerSecond = double.NaN }], 1).ShouldBeFalse();
        AppendThroughputHarness.ExecutionCompleted([valid], 1).ShouldBeTrue();
    }

    [Fact]
    public async Task PreserveCancelledWarmupResetAsIncompleteExecution()
    {
        using var raw = new MemoryStream();
        using var sliceRaw = new MemoryStream();
        await using var csv = new StreamWriter(raw);
        await using var slices = new StreamWriter(sliceRaw, Encoding.UTF8, leaveOpen: true);
        var resets = 0;
        var cells = await AppendThroughputHarness.MeasureBlockAsync(1, [1], [1], 1, 1,
            TimeSpan.FromSeconds(1), 0, TimeSpan.Zero, csv, slices,
            () => ++resets == 1 ? Task.FromException(new OperationCanceledException("reset cancelled")) : Task.CompletedTask,
            (_, _, _, _, _, _) => Task.FromResult(new AppendThroughputHarness.SliceResult([(1000, 1)], 1, 0, null)));
        await slices.FlushAsync();

        cells.Sum(c => c.Appends).ShouldBe(3);
        cells.Sum(c => c.Failures).ShouldBe(1);
        AppendThroughputHarness.ExecutionCompleted(cells, 3).ShouldBeFalse();
        var rows = Encoding.UTF8.GetString(sliceRaw.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        rows.Length.ShouldBe(6);
        rows[0].ShouldContain(",0,1,reset");
    }

    [Theory]
    [InlineData("--runs", "1.5")]
    [InlineData("--runs", "0")]
    [InlineData("--runs", "999999999999999999999999")]
    [InlineData("--window", "NaN")]
    [InlineData("--window", "Infinity")]
    [InlineData("--window", "1e300")]
    [InlineData("--slice", "0")]
    [InlineData("--warmup", "-1")]
    [InlineData("--writers", "")]
    [InlineData("--writers", "0")]
    [InlineData("--writers", "1,1")]
    [InlineData("--events", "-1")]
    [InlineData("--events", "garbage")]
    public async Task RejectInvalidConfigurationWithDocumentedExit(string option, string value)
    {
        using var errors = new StringWriter();
        (await AppendThroughputHarness.RunAsync([option, value], errors)).ShouldBe(2);
        errors.ToString().ShouldStartWith("INVALID CONFIGURATION:");
    }

    [Fact]
    public async Task RejectMissingOptionValueWithDocumentedExit()
    {
        using var errors = new StringWriter();
        (await AppendThroughputHarness.RunAsync(["--runs"], errors)).ShouldBe(2);
        errors.ToString().ShouldStartWith("INVALID CONFIGURATION:");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectInfrastructureFailureAtExecutionBoundary(bool cancelled)
    {
        using var errors = new StringWriter();
        var code = await AppendThroughputHarness.ExecuteDiagnosticAsync(
            () => Task.FromException<int>(cancelled ? new OperationCanceledException("setup interrupted") : new IOException("output unavailable")), errors);
        code.ShouldBe(2);
        errors.ToString().ShouldStartWith("EXECUTION INCOMPLETE:");
    }

    private sealed class ThrowingCleanup : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.FromException(new IOException("cleanup failed"));
    }
}
