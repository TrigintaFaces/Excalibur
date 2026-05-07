// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Threading.Channels;

using Excalibur.Domain;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Provides an abstract base implementation for a batched data processing pipeline.
/// </summary>
/// <typeparam name="TRecord"> The type of records being processed. </typeparam>
public abstract partial class DataProcessor<TRecord> : IDataProcessor, IRecordFetcher<TRecord>
{
	private readonly Channel<TRecord> _dataQueue;

	private readonly DataProcessingOptions _configuration;

	private readonly IServiceProvider _serviceProvider;

	private readonly ILogger _logger;

	private readonly CancellationTokenSource _producerCancellationTokenSource = new();

	private readonly OrderedEventProcessor _orderedEventProcessor = new();

	/// <summary>
	/// The current fetch cursor — advances at page granularity after each batch is enqueued.
	/// On crash recovery this resets to the last durably persisted <c>ProcessedCursor</c>.
	/// </summary>
	private volatile string? _fetchCursor;

	private long _processedTotal;

	private int _disposedFlag;

	private Task? _producerTask;

	private Task<long>? _consumerTask;

	private volatile bool _producerStopped;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataProcessor{TRecord}" /> class.
	/// </summary>
	/// <param name="appLifetime"> Provides notifications about application lifetime events. </param>
	/// <param name="configuration"> The data processing configuration options. </param>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="logger"> The logger used for diagnostic information. </param>
	protected DataProcessor(
		IHostApplicationLifetime appLifetime,
		IOptions<DataProcessingOptions> configuration,
		IServiceProvider serviceProvider,
		ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_configuration = configuration.Value;
		_serviceProvider = serviceProvider;
		_logger = logger;
		_dataQueue = Channel.CreateBounded<TRecord>(new BoundedChannelOptions(_configuration.QueueSize)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,  // Only ConsumerLoopAsync reads from the channel
			SingleWriter = true,  // Only ProducerLoopAsync writes to the channel
			AllowSynchronousContinuations = false,
		});

		_ = appLifetime.ApplicationStopping.Register(() =>
			Task.Factory.StartNew(
					OnApplicationStoppingAsync,
					CancellationToken.None,
					TaskCreationOptions.DenyChildAttach,
					TaskScheduler.Default)
				.Unwrap());
	}

	private CancellationToken ProducerCancellationToken => _producerCancellationTokenSource.Token;

	/// <inheritdoc />
	public virtual async Task<long> RunAsync(
		long completedCount,
		string? processedCursor,
		UpdateCompletedCount updateCompletedCount,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		// On startup / crash recovery, the fetch cursor resets to the last durably
		// persisted processed cursor so we never skip records that were fetched but
		// not yet processed.
		_fetchCursor = processedCursor;
		_ = Interlocked.Exchange(ref _processedTotal, completedCount);

		_producerTask = Task.Factory.StartNew(
				() => ProducerLoopAsync(cancellationToken),
				cancellationToken,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Default)
			.Unwrap();
		_consumerTask = Task.Factory.StartNew(
				() => ConsumerLoopAsync(completedCount, updateCompletedCount, cancellationToken),
				cancellationToken,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Default)
			.Unwrap();

		await _producerTask.ConfigureAwait(false);
		var consumerResult = await _consumerTask.ConfigureAwait(false);

		return consumerResult;
	}

	/// <inheritdoc />
	public abstract Task<CursorFetchResult<TRecord>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken);

	/// <summary>
	/// Asynchronously disposes the data processor.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await DisposeCoreAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes of resources used by the DataProcessor.
	/// </summary>
	protected virtual async ValueTask DisposeCoreAsync()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		LogDisposeAsync();

		try
		{
			await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false);

			// Wait for the producer task to complete before disposing resources
			if (_producerTask is { IsCompleted: false })
			{
				try
				{
					await _producerTask.WaitAsync(TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
				}
				catch (TimeoutException)
				{
					// Producer did not complete in time — proceed with disposal
				}
				catch (OperationCanceledException)
				{
					// Expected — producer was cancelled
				}
				catch (Exception ex)
				{
					LogDisposeAsyncError(ex);
				}
			}

			if (_consumerTask is { IsCompleted: false })
			{
				LogConsumerNotCompletedAsync();
				try
				{
					_ = await _consumerTask.WaitAsync(TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(false);
				}
				catch (TimeoutException)
				{
					LogConsumerTimeoutAsync();
				}
				catch (OperationCanceledException)
				{
					// Expected — consumer was cancelled
				}
				catch (Exception ex)
				{
					LogDisposeAsyncError(ex);
				}
			}

			_ = _dataQueue.Writer.TryComplete();
			await _orderedEventProcessor.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogDisposeAsyncError(ex);
		}
		finally
		{
			_producerCancellationTokenSource.Dispose();
		}
	}

	/// <summary>
	/// Dequeues a batch of records from the channel into a pooled buffer.
	/// The caller MUST return the buffer via <see cref="ArrayPool{T}.Shared"/> after processing.
	/// </summary>
	/// <returns>A tuple of the rented buffer and the actual count of items read.</returns>
	private static async ValueTask<(TRecord[] Buffer, int Count)> DequeueBatchFromChannelAsync(
		ChannelReader<TRecord> reader,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var batch = ArrayPool<TRecord>.Shared.Rent(batchSize);
		var count = 0;

		// Read immediately available items
		while (count < batchSize && reader.TryRead(out var record))
		{
			batch[count++] = record;
		}

		// If we got items, return them
		if (count > 0)
		{
			return (batch, count);
		}

		// Otherwise, wait for at least one item
		if (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			while (count < batchSize && reader.TryRead(out var record))
			{
				batch[count++] = record;
			}
		}

		if (count == 0)
		{
			ArrayPool<TRecord>.Shared.Return(batch, clearArray: true);
			return ([], 0);
		}

		return (batch, count);
	}

	private async Task ProducerLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProducerCancellationToken);
			var combinedToken = combinedTokenSource.Token;

			while (!combinedToken.IsCancellationRequested)
			{
				var batchSize = _configuration.ProducerBatchSize;
				var result = await FetchBatchAsync(_fetchCursor, batchSize, combinedToken).ConfigureAwait(false);

				if (result.Records.Count == 0)
				{
					if (_logger.IsEnabled(LogLevel.Information))
					{
						LogNoMoreRecordsProducerExit();
					}

					break;
				}

				if (_logger.IsEnabled(LogLevel.Information))
				{
					LogEnqueuingRecords(result.Records.Count);
				}

				foreach (var record in result.Records)
				{
					// WriteAsync applies backpressure when the bounded channel is full
					await _dataQueue.Writer.WriteAsync(record, combinedToken).ConfigureAwait(false);
				}

				// Advance the fetch cursor to the next page position.
				// This is a volatile write — visible to the consumer but NOT persisted.
				// On crash, _fetchCursor resets to the last durable ProcessedCursor.
				_fetchCursor = result.NextCursor;

				if (_logger.IsEnabled(LogLevel.Information))
				{
					LogSuccessfullyEnqueued(result.Records.Count);
				}

				// A null NextCursor means the data source has no more pages.
				if (result.NextCursor is null)
				{
					if (_logger.IsEnabled(LogLevel.Information))
					{
						LogNoMoreRecordsProducerExit();
					}

					break;
				}
			}
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			if (_logger.IsEnabled(LogLevel.Debug))
			{
				LogProducerCanceled();
			}
		}
		catch (Exception ex)
		{
			if (_logger.IsEnabled(LogLevel.Error))
			{
				LogProducerError(ex);
			}

			throw;
		}
		finally
		{
			_producerStopped = true;
			_dataQueue.Writer.Complete();

			if (_logger.IsEnabled(LogLevel.Information))
			{
				LogProducerCompleted();
			}
		}
	}

	/// <summary>
	/// Maximum number of consecutive per-record failures before the consumer aborts the
	/// current batch. Prevents burning through an entire batch when the database is
	/// unavailable or a systemic error is occurring.
	/// </summary>
	private const int MaxConsecutiveRecordFailures = 5;

	private async Task<long> ConsumerLoopAsync(long completedCount, UpdateCompletedCount updateCompletedCount,
		CancellationToken cancellationToken)
	{
		var totalProcessedCount = completedCount;

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (_disposedFlag == 1)
				{
					if (_logger.IsEnabled(LogLevel.Warning))
					{
						LogConsumerDisposalRequested();
					}

					break;
				}

				if (_producerStopped && _dataQueue.Reader.Count == 0)
				{
					if (_logger.IsEnabled(LogLevel.Information))
					{
						LogNoMoreRecordsConsumerExit();
					}

					break;
				}

				// DequeueBatchFromChannelAsync uses WaitToReadAsync internally,
				// which efficiently blocks until data is available — no busy-wait needed.
				var (batch, batchCount) = await DequeueBatchFromChannelAsync(
					_dataQueue.Reader, _configuration.ConsumerBatchSize, cancellationToken)
					.ConfigureAwait(false);

				if (batchCount == 0)
				{
					// Channel completed with no more data
					break;
				}

				if (_logger.IsEnabled(LogLevel.Information))
				{
					LogProcessingBatch(batchCount);
				}

				try
				{
					var batchSuccessCount = 0;
					var consecutiveFailures = 0;

					for (var i = 0; i < batchCount; i++)
					{
						// Check for cancellation between records — critical for stale-task
						// detection where the task-scoped CTS fires after a checkpoint
						// returns 0 affected rows (e.g., after a database restore).
						if (cancellationToken.IsCancellationRequested)
						{
							if (_logger.IsEnabled(LogLevel.Warning))
							{
								LogConsumerAbortedStaleTask();
							}

							break;
						}

						var record = batch[i];
						if (record is null)
						{
							continue;
						}

						try
						{
							// Ensures events are dispatched in order. Critical when using Mediator to publish domain events that must be
							// processed sequentially.
							await _orderedEventProcessor
								.ProcessAsync(async () => await ProcessRecordAsync(record, cancellationToken).ConfigureAwait(false), cancellationToken)
								.ConfigureAwait(false);

							// Checkpoint to the database BEFORE incrementing in-memory counters.
							// If the checkpoint fails, the in-memory state stays consistent with
							// the persisted state — on restart, processing resumes from the last
							// successfully persisted CompletedCount/ProcessedCursor.
							// Pass null for processedCursor: the cursor advances at page
							// granularity, not per-record. The SQL UPDATE uses COALESCE to
							// preserve the existing cursor when null is passed.
							await updateCompletedCount(Interlocked.Read(ref _processedTotal) + 1, null, cancellationToken).ConfigureAwait(false);

							_ = Interlocked.Increment(ref _processedTotal);
							batchSuccessCount++;
							consecutiveFailures = 0;
						}
						catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
						{
							// Task-scoped or host-scoped cancellation — exit the inner loop.
							// The outer while-loop or the catch block will handle it.
							break;
						}
						catch (Exception ex)
						{
							var recordId = record.ToString() ?? record.GetHashCode().ToString();

							if (_logger.IsEnabled(LogLevel.Error))
							{
								LogProcessingRecordError(recordId, typeof(TRecord).Name, ex);
							}

							consecutiveFailures++;
							if (consecutiveFailures >= MaxConsecutiveRecordFailures)
							{
								LogConsecutiveFailureThresholdExceeded(consecutiveFailures);
								break;
							}
						}
						finally
						{
							if (record is IDisposable disposable)
							{
								disposable.Dispose();
							}
						}
					}

					totalProcessedCount += batchSuccessCount;
					if (_logger.IsEnabled(LogLevel.Debug))
					{
						LogCompletedBatch(batchCount);
					}
				}
				finally
				{
					ArrayPool<TRecord>.Shared.Return(batch, clearArray: true);
				}
			}

			if (_logger.IsEnabled(LogLevel.Information))
			{
				LogCompletedProcessing(totalProcessedCount);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			if (_logger.IsEnabled(LogLevel.Debug))
			{
				LogConsumerCanceled();
			}
		}
		catch (Exception ex)
		{
			if (_logger.IsEnabled(LogLevel.Error))
			{
				LogConsumerError(ex);
			}

			throw;
		}

		return totalProcessedCount;
	}

	private async Task ProcessRecordAsync(TRecord record, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);

		await using var scope = _serviceProvider.CreateAsyncScope();

		// Resolve handler via DI — both explicit AddRecordHandler<T,R>() and
		// assembly-scanned handlers register as IRecordHandler<TRecord>.
		var handler = scope.ServiceProvider.GetService<IRecordHandler<TRecord>>();

		if (handler is null)
		{
			if (_logger.IsEnabled(LogLevel.Error))
			{
				LogNoHandlerFound(typeof(TRecord).Name);
			}

			return;
		}

		await handler.ProcessAsync(record, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Handles cleanup when the application is stopping.
	/// </summary>
	private async Task OnApplicationStoppingAsync()
	{
		if (_logger.IsEnabled(LogLevel.Information))
		{
			LogApplicationStopping();
		}

		_producerStopped = true;
		await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false);

		if (_logger.IsEnabled(LogLevel.Information))
		{
			LogProducerCancellationRequested();
			LogWaitingForConsumer();
		}

		try
		{
			await DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			if (_logger.IsEnabled(LogLevel.Error))
			{
				LogDisposeError(ex);
			}
		}
	}
}
