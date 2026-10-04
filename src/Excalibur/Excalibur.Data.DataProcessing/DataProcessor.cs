// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DataProcessing;

/// <summary>Provides an abstract base implementation for a batched data processing pipeline.</summary>
/// <typeparam name="TRecord">The type of records being processed.</typeparam>
/// <remarks>
/// Each instance runs once. A failed handler or checkpoint aborts the run; recovery starts at the
/// last durable page boundary and may repeat effects within that page. Handlers must be idempotent.
/// Fetchers and handlers must observe cancellation for shutdown to complete promptly.
/// </remarks>
public abstract partial class DataProcessor<TRecord> : IDataProcessor, IRecordFetcher<TRecord>
{
    private readonly struct PagedRecord
    {
        public TRecord? Record { get; init; }
        public bool HasRecord { get; init; }
        public bool IsPageBoundary { get; init; }
        public string? PageCursor { get; init; }
    }

    private readonly Channel<PagedRecord> _dataQueue;
    private readonly DataProcessingOptions _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationTokenRegistration _stoppingRegistration;
    private readonly Lock _lifecycle = new();
    private Task<long>? _runTask;
    private Task? _disposeTask;
    private int _disposedFlag;

    /// <summary>Initializes a new instance of the <see cref="DataProcessor{TRecord}"/> class.</summary>
    /// <param name="appLifetime">Application shutdown notifications.</param>
    /// <param name="configuration">Pipeline configuration.</param>
    /// <param name="serviceProvider">Service provider used to create handler scopes.</param>
    /// <param name="logger">Diagnostic logger.</param>
    protected DataProcessor(IHostApplicationLifetime appLifetime, IOptions<DataProcessingOptions> configuration,
        IServiceProvider serviceProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(appLifetime);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _configuration = configuration.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _dataQueue = Channel.CreateBounded<PagedRecord>(new BoundedChannelOptions(_configuration.QueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        });
        _stoppingRegistration = appLifetime.ApplicationStopping.Register(() =>
        {
            try { _stop.Cancel(); }
            catch (Exception ex) { LogDisposeAsyncError(ex); }
        });
    }

    /// <inheritdoc />
    public virtual Task<long> RunAsync(long completedCount, string? processedCursor,
        UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(updateCompletedCount);
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposedFlag != 0, this);
            if (_runTask is not null)
            {
                throw new InvalidOperationException("A data processor instance can only run once. Resolve a new instance to retry.");
            }

            _runTask = RunCoreAsync(completedCount, processedCursor, updateCompletedCount, cancellationToken);
            return _runTask;
        }
    }

    /// <inheritdoc />
    public abstract Task<CursorFetchResult<TRecord>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken);

    /// <summary>Asynchronously disposes the data processor after processing stops.</summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>Cancels and joins active processing before releasing its resources.</summary>
    protected virtual ValueTask DisposeCoreAsync()
    {
        lock (_lifecycle)
        {
            _disposedFlag = 1;
            _disposeTask ??= DisposeResourcesAsync(_runTask);
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeResourcesAsync(Task<long>? run)
    {
        await _stoppingRegistration.DisposeAsync().ConfigureAwait(false);
        try { await _stop.CancelAsync().ConfigureAwait(false); }
        catch (Exception ex) { LogDisposeAsyncError(ex); }
        try
        {
            if (run is not null)
            {
                try { await run.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { LogDisposeAsyncError(ex); }
            }
        }
        finally { _stop.Dispose(); }
    }

    private async Task<long> RunCoreAsync(long completedCount, string? cursor,
        UpdateCompletedCount checkpoint, CancellationToken cancellationToken)
    {
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        var token = runCancellation.Token;
        // Both loops are supervised: either failure must release a sibling blocked on the bounded channel.
        var producer = Task.Factory.StartNew(() => SuperviseAsync(() => ProducerLoopAsync(cursor, token), runCancellation),
            CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
        var consumer = Task.Factory.StartNew(async () =>
        {
            long result = 0;
            await SuperviseAsync(async () => result = await ConsumerLoopAsync(completedCount, checkpoint, token).ConfigureAwait(false),
                runCancellation).ConfigureAwait(false);
            return result;
        }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
        try
        {
            await Task.WhenAll(producer, consumer).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return await consumer.ConfigureAwait(false);
        }
        finally
        {
            // Both tasks have stopped; the run now owns anything left in the queue.
            while (_dataQueue.Reader.TryRead(out var pending))
            {
                if (pending.HasRecord)
                {
                    await DisposeRecordAsync(pending.Record).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task SuperviseAsync(Func<Task> operation, CancellationTokenSource cancellation)
    {
        try { await operation().ConfigureAwait(false); }
        catch
        {
            try { await cancellation.CancelAsync().ConfigureAwait(false); }
            catch (Exception ex) { LogConsumerError(ex); }
            throw;
        }
    }

    private async Task ProducerLoopAsync(string? cursor, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await FetchBatchAsync(cursor, _configuration.ProducerBatchSize, cancellationToken).ConfigureAwait(false);
                var transferred = 0;
                try
                {
                    if (page.NextCursor is not null && string.Equals(page.NextCursor, cursor, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("The data source returned a cursor that did not advance.");
                    }
                    if (page.Records.Count == 0 && page.NextCursor is not null)
                    {
                        // An ordered marker checkpoints an empty page only after all prior records succeeded.
                        await _dataQueue.Writer.WriteAsync(new PagedRecord { IsPageBoundary = true, PageCursor = page.NextCursor },
                            cancellationToken).ConfigureAwait(false);
                    }
                    for (; transferred < page.Records.Count; transferred++)
                    {
                        var record = page.Records[transferred] ?? throw new InvalidOperationException("The data source returned a null record.");
                        var last = transferred == page.Records.Count - 1;
                        await _dataQueue.Writer.WriteAsync(new PagedRecord
                        {
                            Record = record, HasRecord = true, IsPageBoundary = last,
                            PageCursor = last ? page.NextCursor : null,
                        }, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    // A successful write transfers ownership to the channel; failed writes do not.
                    for (; transferred < page.Records.Count; transferred++)
                    {
                        await DisposeRecordAsync(page.Records[transferred]).ConfigureAwait(false);
                    }
                }
                if (page.NextCursor is null)
                {
                    break;
                }
                cursor = page.NextCursor;
            }
        }
        finally { _dataQueue.Writer.TryComplete(); }
    }

    private async Task<long> ConsumerLoopAsync(long count, UpdateCompletedCount checkpoint, CancellationToken cancellationToken)
    {
        while (await _dataQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Rent only after the asynchronous wait succeeds; cancellation cannot strand a rented buffer.
            var batch = ArrayPool<PagedRecord>.Shared.Rent(_configuration.ConsumerBatchSize);
            var length = 0;
            var processed = 0;
            try
            {
                while (length < _configuration.ConsumerBatchSize && _dataQueue.Reader.TryRead(out var record))
                {
                    batch[length++] = record;
                }
                for (; processed < length; processed++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = batch[processed];
                    if (item.HasRecord)
                    {
                        await ProcessRecordAsync(item.Record!, cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    var nextCount = item.HasRecord ? checked(count + 1) : count;
                    await checkpoint(nextCount, item.IsPageBoundary ? item.PageCursor : null, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    count = nextCount;
                    batch[processed] = default;
                    if (item.HasRecord)
                    {
                        await DisposeRecordAsync(item.Record).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                try
                {
                    for (; processed < length; processed++)
                    {
                        if (batch[processed].HasRecord)
                        {
                            await DisposeRecordAsync(batch[processed].Record).ConfigureAwait(false);
                        }
                    }
                }
                finally { ArrayPool<PagedRecord>.Shared.Return(batch, clearArray: true); }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return count;
    }

    private async Task ProcessRecordAsync(TRecord record, CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRecordHandler<TRecord>>();
        await handler.ProcessAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask DisposeRecordAsync(TRecord? record)
    {
        // Cleanup failures are diagnostic; they must neither hide the processing failure nor abandon other owned records.
        try
        {
            if (record is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (record is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex) { LogDisposeAsyncError(ex); }
    }
}
