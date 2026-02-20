// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
	private readonly int _queueSize;

	private readonly DataProcessingConfiguration _configuration;

	private readonly IServiceProvider _serviceProvider;

	private readonly ILogger _logger;

	private readonly CancellationTokenSource _producerCancellationTokenSource = new();

	private readonly OrderedEventProcessor _orderedEventProcessor = new();

	private long _skipCount;

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
		IOptions<DataProcessingConfiguration> configuration,
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
		_queueSize = _configuration.QueueSize;
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

	private bool ShouldWaitForProducer => !_producerStopped && !(_producerTask?.IsCompleted ?? true) && _dataQueue.Reader.Count == 0;

	/// <inheritdoc />
	public virtual async Task<long> RunAsync(
		long completedCount,
		UpdateCompletedCount updateCompletedCount,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		_ = Interlocked.Exchange(ref _skipCount, completedCount);
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
	public abstract Task<IEnumerable<TRecord>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken);

	/// <summary>
	/// Asynchronously disposes the data processor.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCoreAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the data processor.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes of resources used by the DataProcessor.
	/// </summary>
	protected virtual async ValueTask DisposeAsyncCoreAsync()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		LogDisposeAsync();

		try
		{
			await _producerCancellationTokenSource.CancelAsync().ConfigureAwait(false);

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
			}

			if (_producerTask is not null)
			{
				await SafeDisposeAsync(_producerTask).ConfigureAwait(false);
			}

			if (_consumerTask is not null)
			{
				await SafeDisposeAsync(_consumerTask).ConfigureAwait(false);
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
	/// Disposes of resources used by the DataProcessor.
	/// </summary>
	/// <param name="disposing">
	/// True to dispose both managed and unmanaged resources; false to dispose only unmanaged resources.
	/// </param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing || Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		LogDisposeSync();

		_producerCancellationTokenSource.Cancel();

		if (_consumerTask is { IsCompleted: false })
		{
			LogConsumerNotCompletedSync();
			var completed = ((IAsyncResult)_consumerTask).AsyncWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
			if (!completed)
			{
				LogConsumerTimeoutSync();
			}
		}

		_producerCancellationTokenSource.Dispose();

		_producerTask?.Dispose();
		_consumerTask?.Dispose();
		_ = _dataQueue.Writer.TryComplete();
		_orderedEventProcessor.Dispose();
	}

	private static async ValueTask SafeDisposeAsync(object resource)
	{
		switch (resource)
		{
			case IAsyncDisposable resourceAsyncDisposable:
				await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
				break;

			case IDisposable disposable:
				disposable.Dispose();
				break;
		}
	}

	/// <summary>
	/// Helper method to dequeue a batch of records from a Channel.
	/// </summary>
	private static async ValueTask<TRecord[]> DequeueBatchFromChannelAsync(
		ChannelReader<TRecord> reader,
		int batchSize,
		CancellationToken cancellationToken)
	{
		// Performance optimization: AD-250-3 - use array directly instead of List
		var batch = new TRecord[batchSize];
		var count = 0;

		// Read immediately available items
		while (count < batchSize && reader.TryRead(out var record))
		{
			batch[count++] = record;
		}

		// If we got items, return them (trimmed to actual count)
		if (count > 0)
		{
			return count == batchSize ? batch : batch[..count];
		}

		// Otherwise, wait for at least one item
		if (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			while (count < batchSize && reader.TryRead(out var record))
			{
				batch[count++] = record;
			}
		}

		return count == 0 ? [] : count == batchSize ? batch : batch[..count];
	}

	private async Task ProducerLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ProducerCancellationToken);
			var combinedToken = combinedTokenSource.Token;

			while (!combinedToken.IsCancellationRequested)
			{
				var availableSlots = Math.Max(0, _queueSize - _dataQueue.Reader.Count);
				if (availableSlots < 1)
				{
					await Task.Delay(10, combinedToken).ConfigureAwait(false);
					continue;
				}

				var batchSize = Math.Min(_configuration.ProducerBatchSize, availableSlots);
				// Performance optimization: AD-250-3 - avoid ToList() when IEnumerable is sufficient
				var fetchedRecords = await FetchBatchAsync(_skipCount, batchSize, combinedToken).ConfigureAwait(false);
				var batch = fetchedRecords as IReadOnlyCollection<TRecord> ?? fetchedRecords.ToList();

				if (batch.Count == 0)
				{
					if (_logger.IsEnabled(LogLevel.Information))
					{
						LogNoMoreRecordsProducerExit();
					}

					break;
				}

				if (_logger.IsEnabled(LogLevel.Information))
				{
					LogEnqueuingRecords(batch.Count);
				}

				foreach (var record in batch)
				{
					await _dataQueue.Writer.WriteAsync(record, combinedToken).ConfigureAwait(false);
				}

				_ = Interlocked.Add(ref _skipCount, batch.Count);

				if (_logger.IsEnabled(LogLevel.Information))
				{
					LogSuccessfullyEnqueued(batch.Count);
				}
			}
		}
		catch (OperationCanceledException)
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

				if (ShouldWaitForProducer)
				{
					if (_logger.IsEnabled(LogLevel.Information))
					{
						LogQueueEmptyWaiting();
					}

					var waitTime = 10;
					for (var i = 0; i < 10; i++)
					{
						await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
						waitTime = Math.Min(waitTime * 2, 100);
					}

					continue;
				}

				var batch = await DequeueBatchFromChannelAsync(_dataQueue.Reader, _configuration.ConsumerBatchSize, cancellationToken)
					.ConfigureAwait(false);
				if (_logger.IsEnabled(LogLevel.Information))
				{
					LogProcessingBatch(batch.Length);
				}

				foreach (var record in batch)
				{
					if (record is null)
					{
						continue;
					}

					try
					{
						// Ensures events are dispatched in order. Critical when using Mediator to publish domain events that must be
						// processed sequentially.
						await _orderedEventProcessor
							.ProcessAsync(async () => await ProcessRecordAsync(record, cancellationToken).ConfigureAwait(false))
							.ConfigureAwait(false);

						var updatedTotal = Interlocked.Increment(ref _processedTotal);
						await updateCompletedCount(updatedTotal, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						if (_logger.IsEnabled(LogLevel.Error))
						{
							LogProcessingRecordError("unknown", typeof(TRecord).Name, ex);
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

				totalProcessedCount += batch.Length;
				if (_logger.IsEnabled(LogLevel.Debug))
				{
					LogCompletedBatch(batch.Length);
				}
			}

			if (_logger.IsEnabled(LogLevel.Information))
			{
				LogCompletedProcessing(totalProcessedCount);
			}
		}
		catch (OperationCanceledException)
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

		using var scope = _serviceProvider.CreateScope();
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
