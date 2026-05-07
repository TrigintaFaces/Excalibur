// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessor{TRecord}"/> abstract class.
/// </summary>
[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataProcessorShould : UnitTestBase
{
	private readonly IHostApplicationLifetime _fakeLifetime = A.Fake<IHostApplicationLifetime>();
	private readonly IServiceProvider _fakeServiceProvider;

	private readonly CancellationTokenSource _stoppingCts = new();

	public DataProcessorShould()
	{
		A.CallTo(() => _fakeLifetime.ApplicationStopping).Returns(_stoppingCts.Token);

		// Set up a real service provider with a handler so ProcessRecordAsync can resolve it
		var services = new ServiceCollection();
		services.AddScoped<IRecordHandler<string>, NoOpStringHandler>();
		_fakeServiceProvider = services.BuildServiceProvider();
	}

	private sealed class NoOpStringHandler : IRecordHandler<string>
	{
		public Task ProcessAsync(string record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_stoppingCts.Dispose();
			(_fakeServiceProvider as IDisposable)?.Dispose();
		}

		base.Dispose(disposing);
	}

	[Fact]
	public void Throw_WhenAppLifetime_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TestDataProcessor(
				null!,
				Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()),
				_fakeServiceProvider,
				NullLogger<TestDataProcessor>.Instance));
	}

	[Fact]
	public void Throw_WhenConfiguration_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TestDataProcessor(
				_fakeLifetime,
				null!,
				_fakeServiceProvider,
				NullLogger<TestDataProcessor>.Instance));
	}

	[Fact]
	public void Throw_WhenServiceProvider_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TestDataProcessor(
				_fakeLifetime,
				Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()),
				null!,
				NullLogger<TestDataProcessor>.Instance));
	}

	[Fact]
	public void Throw_WhenLogger_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TestDataProcessor(
				_fakeLifetime,
				Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()),
				_fakeServiceProvider,
				null!));
	}

	[Fact]
	public async Task RunAsync_ProcessesAllRecords()
	{
		// Arrange
		var records = new[] { "record1", "record2", "record3" };
		var processor = CreateProcessor(records);
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

		// Assert — producer feeds 3 records, consumer processes them
		result.ShouldBeGreaterThan(0);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task RunAsync_ResumesFromCompletedCount()
	{
		// Arrange — 5 records but start from 2 (skip first 2)
		var allRecords = new[] { "skip1", "skip2", "process1", "process2", "process3" };
		var processor = CreateProcessor(allRecords);
		long completedCount = 2;
		var processedValues = new List<long>();

		// Act
		var result = await processor.RunAsync(
			completedCount,
			null,
			(count, cursor, ct) =>
			{
				processedValues.Add(count);
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — should start processing from the 3rd record
		result.ShouldBeGreaterThanOrEqualTo(completedCount);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task RunAsync_ThrowsWhenDisposed()
	{
		// Arrange
		var processor = CreateProcessor(["record1"]);
		await processor.DisposeAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => processor.RunAsync(0, null, (_, _, _) => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RunAsync_ResumesFromProcessedCursor_OnCrashRecovery()
	{
		// Arrange — 5 records, cursor "2" means start from the 3rd record
		var allRecords = new[] { "skip1", "skip2", "process1", "process2", "process3" };
		var processor = CreateProcessor(allRecords);
		var completedCounts = new List<long>();

		// Act — pass processedCursor="2" to simulate crash recovery
		var result = await processor.RunAsync(
			2,
			"2",
			(count, cursor, ct) =>
			{
				completedCounts.Add(count);
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — should process only records after cursor position 2
		result.ShouldBeGreaterThanOrEqualTo(2);
		// The first checkpoint should be 3 (completedCount=2, then increment to 3)
		completedCounts.ShouldNotBeEmpty();
		completedCounts.First().ShouldBe(3);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task RunAsync_PassesNullCursorToDelegate_WhenAllRecordsFitInSinglePage()
	{
		// Arrange — 2 records with ProducerBatchSize=10 means everything fits in one page.
		// The last record IS a page boundary but NextCursor is null (no more pages),
		// so all checkpoints should pass null cursor.
		var records = new[] { "record1", "record2" };
		var processor = CreateProcessor(records);
		var cursorValues = new List<string?>();

		// Act
		await processor.RunAsync(
			0,
			null,
			(count, cursor, ct) =>
			{
				cursorValues.Add(cursor);
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — single page where NextCursor=null → all checkpoints pass null
		cursorValues.ShouldNotBeEmpty();
		cursorValues.ShouldAllBe(c => c == null);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task RunAsync_PassesCursorOnlyAtPageBoundaries_WhenRecordsSpanMultiplePages()
	{
		// Arrange — 6 records with ProducerBatchSize=3 means 2 full pages + 0 remaining.
		// Page 1: records 0-2, NextCursor="3" → last record checkpoints with "3"
		// Page 2: records 3-5, NextCursor=null → last record checkpoints with null
		// Mid-page records should always checkpoint with null cursor.
		var records = new[] { "r0", "r1", "r2", "r3", "r4", "r5" };
		var processor = CreateProcessorWithBatchSizes(records, producerBatchSize: 3, consumerBatchSize: 1);
		var checkpoints = new List<(long Count, string? Cursor)>();

		// Act
		await processor.RunAsync(
			0,
			null,
			(count, cursor, ct) =>
			{
				checkpoints.Add((count, cursor));
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — 6 records = 6 checkpoints
		checkpoints.Count.ShouldBe(6);

		// Mid-page records (indices 0, 1 in page 1) should have null cursor
		checkpoints[0].Cursor.ShouldBeNull(); // r0 (mid-page)
		checkpoints[1].Cursor.ShouldBeNull(); // r1 (mid-page)

		// Page boundary (index 2, last of page 1) should carry cursor "3"
		checkpoints[2].Cursor.ShouldBe("3");

		// Mid-page records (indices 3, 4 in page 2) should have null cursor
		checkpoints[3].Cursor.ShouldBeNull(); // r3 (mid-page)
		checkpoints[4].Cursor.ShouldBeNull(); // r4 (mid-page)

		// Last record of final page — NextCursor is null (no more pages)
		checkpoints[5].Cursor.ShouldBeNull(); // r5 (page boundary but last page)

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task RunAsync_AdvancesCursorAcrossMultiplePages()
	{
		// Arrange — 9 records with ProducerBatchSize=3 = 3 pages.
		// Page 1: records 0-2, NextCursor="3"
		// Page 2: records 3-5, NextCursor="6"
		// Page 3: records 6-8, NextCursor=null
		var records = Enumerable.Range(0, 9).Select(i => $"r{i}").ToArray();
		var processor = CreateProcessorWithBatchSizes(records, producerBatchSize: 3, consumerBatchSize: 1);
		var pageBoundaryCursors = new List<string?>();

		// Act
		await processor.RunAsync(
			0,
			null,
			(count, cursor, ct) =>
			{
				if (cursor is not null)
				{
					pageBoundaryCursors.Add(cursor);
				}

				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — should have exactly 2 non-null cursors from page boundaries
		// (page 3 boundary has null NextCursor, so it doesn't appear)
		pageBoundaryCursors.Count.ShouldBe(2);
		pageBoundaryCursors[0].ShouldBe("3"); // end of page 1
		pageBoundaryCursors[1].ShouldBe("6"); // end of page 2

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task RunAsync_SingleRecordPage_IsAlwaysPageBoundary()
	{
		// Arrange — ProducerBatchSize=1 means every record is its own page boundary.
		// Each record should checkpoint with its cursor.
		var records = new[] { "a", "b", "c" };
		var processor = CreateProcessorWithBatchSizes(records, producerBatchSize: 1, consumerBatchSize: 1);
		var checkpoints = new List<(long Count, string? Cursor)>();

		// Act
		await processor.RunAsync(
			0,
			null,
			(count, cursor, ct) =>
			{
				checkpoints.Add((count, cursor));
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert — 3 records, each is a page boundary
		checkpoints.Count.ShouldBe(3);
		checkpoints[0].Cursor.ShouldBe("1"); // page 1 boundary → next page starts at 1
		checkpoints[1].Cursor.ShouldBe("2"); // page 2 boundary → next page starts at 2
		checkpoints[2].Cursor.ShouldBeNull(); // last page → NextCursor is null

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task NotImplementIDisposable()
	{
		// Assert — IDataProcessor should only implement IAsyncDisposable, not IDisposable
		// This was changed to prevent InvalidOperationException when processors implement
		// IAsyncDisposable via the DataProcessor<T> base class.
		typeof(IDataProcessor).ShouldNotBeAssignableTo<IDisposable>();

		await Task.CompletedTask.ConfigureAwait(false);
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var processor = CreateProcessor([]);

		// Act — dispose twice
		await processor.DisposeAsync().ConfigureAwait(false);
		await processor.DisposeAsync().ConfigureAwait(false);

		// Assert — no exception
	}

	[Fact]
	public async Task RunAsync_HandlesEmptyBatch()
	{
		// Arrange
		var processor = CreateProcessor([]);

		// Act
		var result = await processor.RunAsync(
			0,
			null,
			(_, _, _) => Task.CompletedTask,
			CancellationToken.None).ConfigureAwait(false);

		// Assert — no records = producer exits immediately
		result.ShouldBe(0);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ImplementIDataProcessor()
	{
		// Arrange
		var processor = CreateProcessor([]);

		// Assert
		processor.ShouldBeAssignableTo<IDataProcessor>();
		processor.ShouldBeAssignableTo<IRecordFetcher<string>>();
		processor.ShouldBeAssignableTo<IAsyncDisposable>();

		await processor.DisposeAsync().ConfigureAwait(false);
	}

	private TestDataProcessor CreateProcessor(string[] records)
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
			_fakeServiceProvider,
			NullLogger<TestDataProcessor>.Instance,
			records);
	}

	private TestDataProcessor CreateProcessorWithBatchSizes(string[] records, int producerBatchSize, int consumerBatchSize)
	{
		var config = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = producerBatchSize,
			ConsumerBatchSize = consumerBatchSize,
		};

		return new TestDataProcessor(
			_fakeLifetime,
			Microsoft.Extensions.Options.Options.Create(config),
			_fakeServiceProvider,
			NullLogger<TestDataProcessor>.Instance,
			records);
	}

	/// <summary>
	/// Concrete test implementation of DataProcessor for unit testing.
	/// </summary>
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
}
