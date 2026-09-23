// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// <para>
/// <b>Failure behavior.</b> Changes are taken from the store one at a time, immediately before each
/// is handed to the handler. When a handler throws, or processing is cancelled, the change being
/// handled is consumed: it is not redelivered, and the failure is reported through the propagating
/// exception. Every change that had not yet been handed to a handler remains in the store and is
/// delivered, in the order it was added, by the next call. No change leaves the store without either
/// being handled successfully or being reported through an exception.
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
	public async Task<int> ProcessBatchAsync(
		Func<InMemoryCdcChange, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventHandler);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var totalProcessed = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			// THE CLAIM IS ONE CHANGE AT A TIME, AND THAT IS THE FIX.
			//
			// GetPendingChanges is a DESTRUCTIVE dequeue: everything it hands back has already left
			// the store before the first handler runs, and nothing in this type can put a change back.
			// Claiming BatchSize changes at once therefore parked the whole remainder in a local list,
			// and the rethrow below discarded it -- a handler throwing on change 3 of 10 lost changes
			// 4..10 permanently, never redelivered. The cancellation check that used to sit inside the
			// per-item loop lost them the same way, and MarkAsProcessed was never reached.
			//
			// Claiming one change at a time REMOVES the loss window rather than compensating for it:
			// nothing beyond the change in flight is ever outside the store, so there is no remainder
			// to lose and nothing to put back. The change in flight is still consumed exactly once, but
			// it is REPORTED -- it rides out on the propagating exception and is logged below -- so no
			// change ever leaves silently, which was the defect.
			//
			// A requeue-the-remainder fix was considered and REJECTED, because it trades this safety
			// violation for three others: inserting at the head races AddChange (which takes no lock),
			// so a concurrent producer can overtake the changes being restored; a restore can resurrect
			// changes after Clear() has returned, breaking its postcondition; and putting the failing
			// change back at the head turns a deterministically failing handler into permanent
			// head-of-line blocking, where nothing behind it ever drains again.
			var claimed = _store.GetPendingChanges(1);
			if (claimed.Count == 0)
			{
				break;
			}

			if (totalProcessed == 0)
			{
				LogProcessingBatch(claimed.Count, _options.ProcessorId);
			}

			// A store is asked for ONE change, but a substitute may hand back more. Process every change
			// it actually returned rather than dropping the extras -- dropping them would be precisely
			// the silent loss this fix exists to remove, reintroduced at a different line.
			foreach (var change in claimed)
			{
				try
				{
					await eventHandler(change, cancellationToken).ConfigureAwait(false);
					totalProcessed++;
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					LogProcessingError(ex, change);
					throw;
				}
			}

			_store.MarkAsProcessed(claimed);
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
