// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// In-memory implementation of the CDC processor for testing scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This processor retrieves changes from an <see cref="IInMemoryCdcStore"/> and
/// processes them through user-provided handlers.
/// </para>
/// </remarks>
public sealed partial class InMemoryCdcProcessor : IInMemoryCdcProcessor
{
	private readonly IInMemoryCdcStore _store;
	private readonly InMemoryCdcOptions _options;
	private readonly ILogger<InMemoryCdcProcessor> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCdcProcessor"/> class.
	/// </summary>
	/// <param name="store">The in-memory CDC store.</param>
	/// <param name="options">The in-memory CDC options.</param>
	/// <param name="logger">The logger instance.</param>
	public InMemoryCdcProcessor(
		IInMemoryCdcStore store,
		IOptions<InMemoryCdcOptions> options,
		ILogger<InMemoryCdcProcessor> logger)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<int> ProcessChangesAsync(
		Func<InMemoryCdcChange, CancellationToken, Task> changeHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeHandler);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var totalProcessed = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			var batch = _store.GetPendingChanges(_options.BatchSize);
			if (batch.Count == 0)
			{
				break;
			}

			LogProcessingBatch(batch.Count, _options.ProcessorId);

			foreach (var change in batch)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					await changeHandler(change, cancellationToken).ConfigureAwait(false);
					totalProcessed++;
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					LogProcessingError(ex, change);
					throw;
				}
			}

			_store.MarkAsProcessed(batch);
		}

		if (totalProcessed > 0)
		{
			LogProcessingComplete(totalProcessed, _options.ProcessorId);
		}

		return totalProcessed;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	[LoggerMessage(CdcProcessingEventId.InMemoryCdcProcessingBatch, LogLevel.Debug,
		"Processing batch of {Count} CDC changes for processor {ProcessorId}")]
	private partial void LogProcessingBatch(int count, string? processorId);

	[LoggerMessage(CdcProcessingEventId.InMemoryCdcProcessingError, LogLevel.Error,
		"Error processing CDC change: {Change}")]
	private partial void LogProcessingError(Exception exception, InMemoryCdcChange change);

	[LoggerMessage(CdcProcessingEventId.InMemoryCdcProcessingComplete, LogLevel.Information,
		"Processed {TotalCount} CDC changes for processor {ProcessorId}")]
	private partial void LogProcessingComplete(int totalCount, string? processorId);
}
