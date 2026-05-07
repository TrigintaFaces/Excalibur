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
	public async Task RunAsync_PassesNullCursorToDelegate_ForPerRecordCheckpoints()
	{
		// Arrange — the consumer passes null cursor on per-record checkpoints
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

		// Assert — all per-record checkpoints should pass null cursor
		cursorValues.ShouldNotBeEmpty();
		cursorValues.ShouldAllBe(c => c == null);
		await processor.DisposeAsync().ConfigureAwait(false);
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
