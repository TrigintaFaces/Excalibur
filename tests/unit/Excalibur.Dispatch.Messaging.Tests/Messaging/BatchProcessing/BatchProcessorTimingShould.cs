// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Messaging.BatchProcessing;

/// <summary>
///     Timing-sensitive tests for the <see cref="BatchProcessor{T}" /> class.
///     These tests rely on internal timer-based batch flushing and must run
///     sequentially to avoid CPU contention causing flaky failures.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BatchProcessorTimingShould : IAsyncDisposable
{
	private static readonly string[] TwoItemBatch = ["item1", "item2"];

	private readonly ILogger<BatchProcessor<string>> _logger;
	private readonly ConcurrentBag<BatchProcessor<string>> _disposables = [];

	public BatchProcessorTimingShould()
	{
		_logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BatchProcessor<string>>.Instance;
	}

	[Fact]
	public async Task FlushBatchBasedOnTimeDelay()
	{
		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(250) };

		var processedBatches = new ConcurrentQueue<IReadOnlyList<string>>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				var processedBatch = batch.ToArray();
				processedBatches.Enqueue(processedBatch);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);

		var observedBatch = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => processedBatches.Count == 1,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60)),
			TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);

		observedBatch.ShouldBeTrue();
		processedBatches.Count.ShouldBe(1);
		var batch = processedBatches.TryPeek(out var processedBatch) ? processedBatch : null;
		batch.ShouldNotBeNull();
		batch.Count.ShouldBe(2);
		batch.ShouldBe(TwoItemBatch);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ValidateBatchingLatency()
	{
		var processedBatches = new ConcurrentQueue<IReadOnlyList<string>>();
		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(100) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				var processedBatch = batch.ToArray();
				processedBatches.Enqueue(processedBatch);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add 3 items (less than max batch size) to trigger time-based batching
		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item3", CancellationToken.None).ConfigureAwait(false);

		var observedBatch = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => processedBatches.Count == 1,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60)),
			TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);

		observedBatch.ShouldBeTrue();
		processedBatches.Count.ShouldBe(1);
		var batch = processedBatches.TryPeek(out var processedBatch) ? processedBatch : null;
		batch.ShouldNotBeNull();
		batch.Count.ShouldBe(3);
		batch.ShouldContain("item1");
		batch.ShouldContain("item2");
		batch.ShouldContain("item3");
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		foreach (var disposable in _disposables)
		{
			if (disposable is not null)
			{
				await disposable.DisposeAsync().ConfigureAwait(false);
			}
		}
	}
}
