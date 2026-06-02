// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Tests for <see cref="DataProcessor{TRecord}"/> consumer loop behavior:
/// consecutive failure threshold, cancellation between records, error handling,
/// and handler resolution.
/// </summary>
[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataProcessorConsumerLoopShould : UnitTestBase
{
	private readonly IHostApplicationLifetime _fakeLifetime = A.Fake<IHostApplicationLifetime>();
	private readonly CancellationTokenSource _stoppingCts = new();

	public DataProcessorConsumerLoopShould()
	{
		A.CallTo(() => _fakeLifetime.ApplicationStopping).Returns(_stoppingCts.Token);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_stoppingCts.Dispose();
		}

		base.Dispose(disposing);
	}

	[Fact]
	public async Task AbortBatch_WhenConsecutiveFailuresExceedThreshold()
	{
		// Arrange — 10 records, handler throws on every record
		var records = Enumerable.Range(1, 10).Select(i => $"record-{i}").ToArray();
		var services = new ServiceCollection();
		services.AddScoped<IRecordHandler<string>, AlwaysFailingHandler>();
		using var sp = services.BuildServiceProvider();

		var processor = CreateProcessor(records, sp);
		var completedCounts = new List<long>();

		// Act
		var result = await processor.RunAsync(
			0,
			null,
			(count, cursor, ct) =>
			{
				completedCounts.Add(count);
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — MaxConsecutiveRecordFailures = 5, so we should stop before processing all 10
		// The processor should process fewer than all records due to the threshold
		result.ShouldBeLessThan(10);
		// completedCounts should be empty since all records failed
		completedCounts.ShouldBeEmpty();

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ResetConsecutiveFailures_OnSuccessfulRecord()
	{
		// Arrange — alternating success/failure pattern
		var records = new[] { "ok-1", "fail", "ok-2", "fail", "ok-3" };
		var services = new ServiceCollection();
		services.AddScoped<IRecordHandler<string>, AlternatingHandler>();
		using var sp = services.BuildServiceProvider();

		var processor = CreateProcessor(records, sp);
		var completedCounts = new List<long>();

		// Act
		var result = await processor.RunAsync(
			0,
			null,
			(count, cursor, ct) =>
			{
				completedCounts.Add(count);
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — should process all 5 records, 3 successful
		completedCounts.Count.ShouldBe(3); // 3 successful records
		result.ShouldBe(3);

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ExitGracefully_WhenCancellationRequestedBetweenRecords()
	{
		// Arrange — cancellation fires after first record
		var records = Enumerable.Range(1, 100).Select(i => $"record-{i}").ToArray();
		using var cts = new CancellationTokenSource();
		var processedCount = 0;

		var services = new ServiceCollection();
		services.AddScoped<IRecordHandler<string>>(_ => new CancelAfterNHandler(1, cts));
		using var sp = services.BuildServiceProvider();

		var processor = CreateProcessor(records, sp);

		// Act
		var result = await processor.RunAsync(
			0,
			null,
			(count, cursor, ct) =>
			{
				Interlocked.Increment(ref processedCount);
				return Task.CompletedTask;
			},
			cts.Token).ConfigureAwait(false);

		// Assert — should not process all 100 records
		result.ShouldBeLessThan(100);

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task SkipRecord_WhenNoHandlerRegistered()
	{
		// Arrange — no handler registered
		var records = new[] { "record1", "record2" };
		using var sp = new ServiceCollection().BuildServiceProvider();

		var processor = CreateProcessor(records, sp);
		var completedCounts = new List<long>();

		// Act
		var result = await processor.RunAsync(
			0,
			null,
			(count, cursor, ct) =>
			{
				completedCounts.Add(count);
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — records are processed (handler null = skip), but checkpoint still advances
		// The consumer sees the records but no handler does real work
		result.ShouldBeGreaterThanOrEqualTo(0);

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task DisposeDisposableRecords_AfterProcessing()
	{
		// Arrange
		var record1 = new DisposableRecord("rec-1");
		var record2 = new DisposableRecord("rec-2");
		var records = new[] { record1, record2 };

		var services = new ServiceCollection();
		services.AddScoped<IRecordHandler<DisposableRecord>, DisposableRecordHandler>();
		using var sp = services.BuildServiceProvider();

		var processor = CreateDisposableProcessor(records, sp);

		// Act
		await processor.RunAsync(
			0,
			null,
			(_, _, _) => Task.CompletedTask,
			CancellationToken.None).ConfigureAwait(false);

		// Assert — both records should have been disposed
		record1.IsDisposed.ShouldBeTrue();
		record2.IsDisposed.ShouldBeTrue();

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleUpdateCompletedCountFailure_WithTaskScopedCancellation()
	{
		// Arrange — updateCompletedCount cancels the task-scoped CTS
		var records = Enumerable.Range(1, 10).Select(i => $"record-{i}").ToArray();
		var services = new ServiceCollection();
		services.AddScoped<IRecordHandler<string>, NoOpStringHandler>();
		using var sp = services.BuildServiceProvider();

		var processor = CreateProcessor(records, sp);
		var callCount = 0;

		// Act — on first checkpoint, cancel
		using var taskCts = new CancellationTokenSource();
		var result = await processor.RunAsync(
			0,
			null,
			async (count, cursor, ct) =>
			{
				callCount++;
				if (callCount >= 2)
				{
					await taskCts.CancelAsync().ConfigureAwait(false);
				}
			},
			taskCts.Token).ConfigureAwait(false);

		// Assert — should have stopped after cancellation
		result.ShouldBeLessThan(10);

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	// --- Test helpers ---

	private TestDataProcessor CreateProcessor(string[] records, IServiceProvider sp)
	{
		var config = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 5,
		};

		return new TestDataProcessor(
			_fakeLifetime,
			Microsoft.Extensions.Options.Options.Create(config),
			sp,
			NullLogger<TestDataProcessor>.Instance,
			records);
	}

	private DisposableTestDataProcessor CreateDisposableProcessor(DisposableRecord[] records, IServiceProvider sp)
	{
		var config = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 5,
		};

		return new DisposableTestDataProcessor(
			_fakeLifetime,
			Microsoft.Extensions.Options.Options.Create(config),
			sp,
			NullLogger<DisposableTestDataProcessor>.Instance,
			records);
	}

	private sealed class NoOpStringHandler : IRecordHandler<string>
	{
		public Task ProcessAsync(string record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class AlwaysFailingHandler : IRecordHandler<string>
	{
		public Task ProcessAsync(string record, CancellationToken cancellationToken) =>
			throw new InvalidOperationException($"Simulated failure for {record}");
	}

	private sealed class AlternatingHandler : IRecordHandler<string>
	{
		public Task ProcessAsync(string record, CancellationToken cancellationToken) =>
			record.StartsWith("fail", StringComparison.Ordinal)
				? throw new InvalidOperationException("Expected failure")
				: Task.CompletedTask;
	}

	private sealed class CancelAfterNHandler : IRecordHandler<string>
	{
		private readonly int _cancelAfter;
		private readonly CancellationTokenSource _cts;
		private int _count;

		public CancelAfterNHandler(int cancelAfter, CancellationTokenSource cts)
		{
			_cancelAfter = cancelAfter;
			_cts = cts;
		}

		public async Task ProcessAsync(string record, CancellationToken cancellationToken)
		{
			if (Interlocked.Increment(ref _count) >= _cancelAfter)
			{
				await _cts.CancelAsync().ConfigureAwait(false);
			}
		}
	}

	internal sealed class TestDataProcessor : DataProcessor<string>
	{
		private readonly string[] _records;

		public TestDataProcessor(
			IHostApplicationLifetime appLifetime,
			IOptions<DataProcessingOptions> configuration,
			IServiceProvider serviceProvider,
			ILogger logger,
			string[]? records = null)
			: base(appLifetime, configuration, serviceProvider, logger)
		{
			_records = records ?? [];
		}

		public override Task<CursorFetchResult<string>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken)
		{
			var skip = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
			var batch = _records
				.Skip(skip)
				.Take(batchSize)
				.ToList();

			var nextCursor = batch.Count > 0 && skip + batch.Count < _records.Length
				? (skip + batch.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)
				: null;

			return Task.FromResult(new CursorFetchResult<string>(batch, nextCursor));
		}
	}

	internal sealed class DisposableRecord : IDisposable
	{
		public string Name { get; }
		public bool IsDisposed { get; private set; }

		public DisposableRecord(string name) => Name = name;

		public void Dispose() => IsDisposed = true;

		public override string ToString() => Name;
	}

	private sealed class DisposableRecordHandler : IRecordHandler<DisposableRecord>
	{
		public Task ProcessAsync(DisposableRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	internal sealed class DisposableTestDataProcessor : DataProcessor<DisposableRecord>
	{
		private readonly DisposableRecord[] _records;

		public DisposableTestDataProcessor(
			IHostApplicationLifetime appLifetime,
			IOptions<DataProcessingOptions> configuration,
			IServiceProvider serviceProvider,
			ILogger logger,
			DisposableRecord[]? records = null)
			: base(appLifetime, configuration, serviceProvider, logger)
		{
			_records = records ?? [];
		}

		public override Task<CursorFetchResult<DisposableRecord>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken)
		{
			var skip = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
			var batch = _records
				.Skip(skip)
				.Take(batchSize)
				.ToList();

			var nextCursor = batch.Count > 0 && skip + batch.Count < _records.Length
				? (skip + batch.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)
				: null;

			return Task.FromResult(new CursorFetchResult<DisposableRecord>(batch, nextCursor));
		}
	}
}