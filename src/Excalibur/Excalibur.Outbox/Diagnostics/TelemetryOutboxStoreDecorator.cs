// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Outbox.Diagnostics;

/// <summary>
/// Decorates an <see cref="IOutboxStore"/> with OpenTelemetry metrics instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// This decorator adds operation-level metrics (counters, histograms) to any outbox store
/// implementation, providing consistent observability regardless of the underlying provider
/// (SqlServer, Postgres, InMemory, etc.).
/// </para>
/// <para>
/// Metrics emitted:
/// <list type="bullet">
/// <item><c>excalibur.outbox.store.operations</c> - Counter of store operations by type</item>
/// <item><c>excalibur.outbox.store.operation_duration</c> - Histogram of operation durations in milliseconds</item>
/// <item><c>excalibur.outbox.store.messages</c> - Counter of messages processed by operation type</item>
/// </list>
/// </para>
/// <para>
/// To enable collection, register the meter with your OpenTelemetry provider:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(m => m.AddMeter(TelemetryOutboxStoreDecorator.MeterName));
/// </code>
/// </para>
/// </remarks>
internal sealed class TelemetryOutboxStoreDecorator : IOutboxStore, IOutboxStoreBatch, IDisposable
{
	/// <summary>
	/// The meter name for outbox store metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Outbox.Store";

	private readonly IOutboxStore _inner;
	private readonly Meter _meter;
	private readonly Counter<long> _operationsCounter;
	private readonly Histogram<double> _operationDuration;
	private readonly Counter<long> _messagesCounter;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryOutboxStoreDecorator"/> class.
	/// </summary>
	/// <param name="inner">The inner outbox store to decorate.</param>
	public TelemetryOutboxStoreDecorator(IOutboxStore inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
		_meter = new Meter(MeterName, "1.0.0");

		_operationsCounter = _meter.CreateCounter<long>(
			"excalibur.outbox.store.operations",
			"operations",
			"Total outbox store operations by type.");

		_operationDuration = _meter.CreateHistogram<double>(
			"excalibur.outbox.store.operation_duration",
			"ms",
			"Duration of outbox store operations in milliseconds.");

		_messagesCounter = _meter.CreateCounter<long>(
			"excalibur.outbox.store.messages",
			"messages",
			"Total messages processed by outbox store operations.");
	}

	/// <inheritdoc />
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		await _inner.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);
		RecordOperation("stage", 1, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		await _inner.EnqueueAsync(message, context, cancellationToken).ConfigureAwait(false);
		RecordOperation("enqueue", 1, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		var result = await _inner.GetUnsentMessagesAsync(batchSize, cancellationToken).ConfigureAwait(false);
		var messages = result as ICollection<OutboundMessage> ?? result.ToList();
		RecordOperation("get_unsent", messages.Count, sw.Elapsed.TotalMilliseconds);
		return messages;
	}

	/// <inheritdoc />
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		await _inner.MarkSentAsync(messageId, cancellationToken).ConfigureAwait(false);
		RecordOperation("mark_sent", 1, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		await _inner.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken).ConfigureAwait(false);
		RecordOperation("mark_failed", 1, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public async ValueTask MarkBatchSentAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		if (_inner is IOutboxStoreBatch batch)
		{
			await batch.MarkBatchSentAsync(messageIds, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			foreach (var id in messageIds)
			{
				await _inner.MarkSentAsync(id, cancellationToken).ConfigureAwait(false);
			}
		}

		RecordOperation("mark_batch_sent", messageIds.Count, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public async ValueTask MarkBatchFailedAsync(IReadOnlyList<string> messageIds, string reason, int retryCount, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		if (_inner is IOutboxStoreBatch batch)
		{
			await batch.MarkBatchFailedAsync(messageIds, reason, retryCount, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			foreach (var id in messageIds)
			{
				await _inner.MarkFailedAsync(id, reason, retryCount, cancellationToken).ConfigureAwait(false);
			}
		}

		RecordOperation("mark_batch_failed", messageIds.Count, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public async ValueTask<bool> TryMarkSentAndReceivedAsync(string messageId, InboxEntry inboxEntry, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		bool result;
		if (_inner is IOutboxStoreBatch batch)
		{
			result = await batch.TryMarkSentAndReceivedAsync(messageId, inboxEntry, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			result = false;
		}

		RecordOperation("try_mark_sent_received", 1, sw.Elapsed.TotalMilliseconds);
		return result;
	}

	/// <inheritdoc />
	public void Dispose() => _meter.Dispose();

	private void RecordOperation(string operation, int messageCount, double durationMs)
	{
		var tag = new KeyValuePair<string, object?>("operation", operation);
		_operationsCounter.Add(1, tag);
		_operationDuration.Record(durationMs, tag);
		if (messageCount > 0)
		{
			_messagesCounter.Add(messageCount, tag);
		}
	}
}
