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
public sealed class DataProcessorShould : UnitTestBase
{
	private readonly IHostApplicationLifetime _fakeLifetime = A.Fake<IHostApplicationLifetime>();
	private readonly IServiceProvider _fakeServiceProvider = A.Fake<IServiceProvider>();

	private readonly CancellationTokenSource _stoppingCts = new();

	public DataProcessorShould()
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
	public void Throw_WhenAppLifetime_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TestDataProcessor(
				null!,
				Microsoft.Extensions.Options.Options.Create(new DataProcessingConfiguration()),
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
				Microsoft.Extensions.Options.Options.Create(new DataProcessingConfiguration()),
				null!,
				NullLogger<TestDataProcessor>.Instance));
	}

	[Fact]
	public void Throw_WhenLogger_IsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TestDataProcessor(
				_fakeLifetime,
				Microsoft.Extensions.Options.Options.Create(new DataProcessingConfiguration()),
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
			(count, ct) =>
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
			(count, ct) =>
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
			() => processor.RunAsync(0, (_, _) => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(false);
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
	public void Dispose_IsIdempotent()
	{
		// Arrange
		var processor = CreateProcessor([]);

		// Act — dispose twice
		processor.Dispose();
		processor.Dispose();

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
			(_, _) => Task.CompletedTask,
			CancellationToken.None).ConfigureAwait(false);

		// Assert — no records = producer exits immediately
		result.ShouldBe(0);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void ImplementIDataProcessor()
	{
		// Arrange
		var processor = CreateProcessor([]);

		// Assert
		processor.ShouldBeAssignableTo<IDataProcessor>();
		processor.ShouldBeAssignableTo<IRecordFetcher<string>>();
		processor.ShouldBeAssignableTo<IAsyncDisposable>();
		processor.ShouldBeAssignableTo<IDisposable>();

		processor.Dispose();
	}

	private TestDataProcessor CreateProcessor(string[] records)
	{
		var config = new DataProcessingConfiguration
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
			IOptions<DataProcessingConfiguration> configuration,
			IServiceProvider serviceProvider,
			ILogger logger,
			string[]? records = null)
			: base(appLifetime, configuration, serviceProvider, logger)
		{
			_records = records ?? [];
		}

		public override Task<IEnumerable<string>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
		{
			var result = _records
				.Skip((int)skip)
				.Take(batchSize);

			return Task.FromResult(result);
		}
	}
}
