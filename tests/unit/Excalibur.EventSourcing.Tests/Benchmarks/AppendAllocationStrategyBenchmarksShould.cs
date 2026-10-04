// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Globalization;

using Excalibur.Benchmarks.EventSourcing;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class AppendAllocationStrategyBenchmarksShould
{
	[Fact]
	public async Task RecordSuccessfulWaveIncludingCleanupWithItsIdentity()
	{
		var benchmark = new AppendAllocationStrategyBenchmarks { WriterCount = 8, EventsPerAppend = 5 };
		long tick = 0;
		await benchmark.MeasureWaveAsync("Identity", async () =>
		{
			await using var cleanup = new TimestampCleanup(() => tick = 3 * Stopwatch.Frequency);
			tick = Stopwatch.Frequency;
		}, () => tick);

		var csv = benchmark.CreateSamplesCsv();
		csv.ShouldNotBeNull();
		csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(2);
		csv.ShouldContain("\"Identity\",8,5,1,unclassified,completed,3000.0000,\"\"");
	}

	[Fact]
	public async Task RecordFailureOnlyAfterOtherWritersDrainAndRethrowOriginalFailure()
	{
		var benchmark = new AppendAllocationStrategyBenchmarks { WriterCount = 2, EventsPerAppend = 1 };
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var original = new InvalidOperationException("An append may already have committed");
		long tick = 0;
		var pending = benchmark.MeasureWaveAsync("CounterRow", () =>
			Task.WhenAll(Task.FromException(original), release.Task), () => tick);

		pending.IsCompleted.ShouldBeFalse();
		benchmark.CreateSamplesCsv().ShouldBeNull();
		tick = 7 * Stopwatch.Frequency;
		release.SetResult();
		var observed = await Should.ThrowAsync<InvalidOperationException>(() => pending);
		observed.ShouldBeSameAs(original);
		var csv = benchmark.CreateSamplesCsv();
		csv.ShouldNotBeNull();
		csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(2);
		csv.ShouldContain("\"CounterRow\",2,1,1,unclassified,failed,7000.0000,\"System.InvalidOperationException\"");
		csv.ShouldNotContain(original.Message);
	}

	[Fact]
	public async Task RetainCancelledWaveAndLaterSuccessfulWaveAsDistinctObservations()
	{
		var benchmark = new AppendAllocationStrategyBenchmarks { WriterCount = 1, EventsPerAppend = 1 };
		var cancelled = new OperationCanceledException();
		await Should.ThrowAsync<OperationCanceledException>(() =>
			benchmark.MeasureWaveAsync("Identity", () => Task.FromException(cancelled), () => 0));
		await benchmark.MeasureWaveAsync("CounterRowBatched", () => Task.CompletedTask, () => 0);

		var csv = benchmark.CreateSamplesCsv();
		csv.ShouldNotBeNull();
		csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(3);
		csv.ShouldContain("\"Identity\",1,1,1,unclassified,failed,0.0000,\"System.OperationCanceledException\"");
		csv.ShouldContain("\"CounterRowBatched\",1,1,2,unclassified,completed,0.0000,\"\"");
	}

	[Fact]
	public async Task UseInvariantCsvFormattingAndEscapeTextFields()
	{
		var previousCulture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			var benchmark = new AppendAllocationStrategyBenchmarks { WriterCount = 1, EventsPerAppend = 5 };
			long tick = 0;
			await benchmark.MeasureWaveAsync("arm,\"quoted\"", () =>
			{
				tick = Stopwatch.Frequency;
				return Task.CompletedTask;
			}, () => tick);
			benchmark.CreateSamplesCsv().ShouldContain("\"arm,\"\"quoted\"\"\",1,5,1,unclassified,completed,1000.0000,\"\"");
		}
		finally
		{
			CultureInfo.CurrentCulture = previousCulture;
		}
	}

	private sealed class TimestampCleanup(Action dispose) : IAsyncDisposable
	{
		public ValueTask DisposeAsync()
		{
			dispose();
			return ValueTask.CompletedTask;
		}
	}
}
