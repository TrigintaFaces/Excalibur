// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;

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
/// Optional capabilities are neither declared nor absorbed. They are resolved from the decorated store on
/// demand and returned wrapped so their operations are measured too. A capability the decorated store does
/// not provide resolves to <see langword="null"/>, exactly as it would without this decorator: the metrics
/// layer never invents a capability, and never hides one.
/// </para>
/// <para>
/// To enable collection, register the meter with your OpenTelemetry provider:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(m => m.AddMeter(TelemetryOutboxStoreDecorator.MeterName));
/// </code>
/// </para>
/// </remarks>
/// <param name="inner"> The inner outbox store to decorate. </param>
internal sealed class TelemetryOutboxStoreDecorator(IOutboxStore inner) : OutboxStoreDecorator(inner), IDisposable
{
	/// <summary>
	/// The meter name for outbox store metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Outbox.Store";

	private readonly Meter _meter = new(MeterName, "1.0.0");
	private Counter<long>? _operationsCounter;
	private Histogram<double>? _operationDuration;
	private Counter<long>? _messagesCounter;
	private bool _disposed;

	private Counter<long> Operations => _operationsCounter ??= _meter.CreateCounter<long>(
		"excalibur.outbox.store.operations", "operations", "Total outbox store operations by type.");

	private Histogram<double> Duration => _operationDuration ??= _meter.CreateHistogram<double>(
		"excalibur.outbox.store.operation_duration", "ms", "Duration of outbox store operations in milliseconds.");

	private Counter<long> Messages => _messagesCounter ??= _meter.CreateCounter<long>(
		"excalibur.outbox.store.messages", "messages", "Total messages processed by outbox store operations.");

	/// <summary>
	/// Resolves a capability from the decorated store, wrapped so that its operations are measured.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve. </param>
	/// <returns> A measured view of the capability, or <see langword="null"/> when the store lacks it. </returns>
	/// <remarks>
	/// The decorator declares none of the optional capability interfaces. It cannot advertise a capability
	/// the decorated store does not have, and it cannot hide one the store does have.
	/// </remarks>
	public override object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IOutboxStoreBatch))
		{
			return Inner.GetService(typeof(IOutboxStoreBatch)) is IOutboxStoreBatch batch
				? new MeasuredBatch(batch, this)
				: null;
		}

		if (serviceType == typeof(IDeadLetterableOutboxStore))
		{
			return Inner.GetService(typeof(IDeadLetterableOutboxStore)) is IDeadLetterableOutboxStore dead
				? new MeasuredDeadLetterable(dead, this)
				: null;
		}

		if (serviceType == typeof(IBackoffSchedulableOutboxStore))
		{
			return Inner.GetService(typeof(IBackoffSchedulableOutboxStore)) is IBackoffSchedulableOutboxStore backoff
				? new MeasuredBackoffSchedulable(backoff, this)
				: null;
		}

		return base.GetService(serviceType);
	}

	/// <inheritdoc />
	public override async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		await Inner.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);
		RecordOperation("stage", 1, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public override async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		await Inner.EnqueueAsync(message, context, cancellationToken).ConfigureAwait(false);
		RecordOperation("enqueue", 1, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public override async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		var result = await Inner.GetUnsentMessagesAsync(batchSize, cancellationToken).ConfigureAwait(false);
		var messages = result as ICollection<OutboundMessage> ?? result.ToList();
		RecordOperation("get_unsent", messages.Count, sw.Elapsed.TotalMilliseconds);
		return messages;
	}

	/// <inheritdoc />
	public override async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		await Inner.MarkSentAsync(messageId, cancellationToken).ConfigureAwait(false);
		RecordOperation("mark_sent", 1, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public override async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		var sw = ValueStopwatch.StartNew();
		await Inner.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken).ConfigureAwait(false);
		RecordOperation("mark_failed", 1, sw.Elapsed.TotalMilliseconds);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_meter.Dispose();
	}

	private void RecordOperation(string operation, int messageCount, double durationMs)
	{
		var tag = new KeyValuePair<string, object?>("operation", operation);
		Operations.Add(1, tag);
		Duration.Record(durationMs, tag);
		if (messageCount > 0)
		{
			Messages.Add(messageCount, tag);
		}
	}

	private sealed class MeasuredBatch(IOutboxStoreBatch inner, TelemetryOutboxStoreDecorator owner) : IOutboxStoreBatch
	{
		public async ValueTask MarkBatchSentAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			await inner.MarkBatchSentAsync(messageIds, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_batch_sent", messageIds.Count, sw.Elapsed.TotalMilliseconds);
		}

		public async ValueTask MarkBatchFailedAsync(IReadOnlyList<string> messageIds, string reason, int retryCount, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			await inner.MarkBatchFailedAsync(messageIds, reason, retryCount, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_batch_failed", messageIds.Count, sw.Elapsed.TotalMilliseconds);
		}

		public async ValueTask<bool> TryMarkSentAndReceivedAsync(string messageId, InboxEntry inboxEntry, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var result = await inner.TryMarkSentAndReceivedAsync(messageId, inboxEntry, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("try_mark_sent_received", 1, sw.Elapsed.TotalMilliseconds);
			return result;
		}
	}

	private sealed class MeasuredDeadLetterable(IDeadLetterableOutboxStore inner, TelemetryOutboxStoreDecorator owner) : IDeadLetterableOutboxStore
	{
		public async ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			await inner.MarkDeadLetteredAsync(messageId, reason, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_dead_lettered", 1, sw.Elapsed.TotalMilliseconds);
		}
	}

	private sealed class MeasuredBackoffSchedulable(IBackoffSchedulableOutboxStore inner, TelemetryOutboxStoreDecorator owner) : IBackoffSchedulableOutboxStore
	{
		public async ValueTask MarkFailedWithBackoffAsync(
			string messageId,
			string errorMessage,
			int retryCount,
			DateTimeOffset nextAttemptAt,
			CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			await inner.MarkFailedWithBackoffAsync(messageId, errorMessage, retryCount, nextAttemptAt, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_failed_with_backoff", 1, sw.Elapsed.TotalMilliseconds);
		}
	}
}
