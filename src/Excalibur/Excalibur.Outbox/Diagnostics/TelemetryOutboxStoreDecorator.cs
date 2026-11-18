// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;

using System.Diagnostics.CodeAnalysis;

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
internal sealed partial class TelemetryOutboxStoreDecorator(IOutboxStore inner) : OutboxStoreDecorator(inner), IDisposable
{
	/// <summary>
	/// The meter name for outbox store metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Outbox.Store";

	private readonly Meter _meter = new(MeterName, "1.0.0");
	private Counter<long>? _operationsCounter;
	private Histogram<double>? _operationDuration;
	private Counter<long>? _messagesCounter;
	private volatile bool _disposed;

	private Counter<long> Operations => _operationsCounter ??= _meter.CreateCounter<long>(
		"excalibur.outbox.store.operations", "{operations}", "Total outbox store operations by type.");

	private Histogram<double> Duration => _operationDuration ??= _meter.CreateHistogram<double>(
		"excalibur.outbox.store.operation_duration", "ms", "Duration of outbox store operations in milliseconds.");

	private Counter<long> Messages => _messagesCounter ??= _meter.CreateCounter<long>(
		"excalibur.outbox.store.messages", "{messages}", "Total messages processed by outbox store operations.");

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
        if (serviceType == typeof(ITransactionalOutboxPreparer))
        {
            return Inner.GetService(serviceType) is ITransactionalOutboxPreparer prepare
                ? new MeasuredTransactionalPreparation(prepare, this) : null;
        }
        if (serviceType == typeof(ITransactionalOutboxPayloadTransformPreparer))
        {
            return Inner.GetService(serviceType) is ITransactionalOutboxPayloadTransformPreparer transform
                ? new MeasuredTransactionalTransform(transform, this) : null;
        }


		if (serviceType == typeof(IOutboxPayloadTransformStore))
		{
			return Inner.GetService(typeof(IOutboxPayloadTransformStore)) is IOutboxPayloadTransformStore transform
				? new MeasuredPayloadTransform(transform, this)
				: null;
		}

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

		if (serviceType == typeof(IClaimScopedOutboxStore))
		{
			return Inner.GetService(typeof(IClaimScopedOutboxStore)) is IClaimScopedOutboxStore capability
				? new MeasuredClaimScoped(capability, this)
				: null;
		}

		if (serviceType == typeof(IFencedClaimScopedOutboxStore))
		{
			return Inner.GetService(typeof(IFencedClaimScopedOutboxStore)) is IFencedClaimScopedOutboxStore capability
				? new MeasuredFencedClaimScoped(capability, this)
				: null;
		}

		if (serviceType == typeof(IClaimScopedDeadLetterableOutboxStore))
		{
			return Inner.GetService(typeof(IClaimScopedDeadLetterableOutboxStore)) is IClaimScopedDeadLetterableOutboxStore capability
				? new MeasuredClaimDeadLetterable(capability, this)
				: null;
		}

		if (serviceType == typeof(IFencedDeadLetterableOutboxStore))
		{
			return Inner.GetService(typeof(IFencedDeadLetterableOutboxStore)) is IFencedDeadLetterableOutboxStore capability
				? new MeasuredFencedDeadLetterable(capability, this)
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
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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

	private sealed class MeasuredPayloadTransform(IOutboxPayloadTransformStore inner, TelemetryOutboxStoreDecorator owner)
		: IOutboxPayloadTransformStore
	{
		[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public async ValueTask EnqueueWithPayloadTransformAsync(IDispatchMessage message, IMessageContext context,
			Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload,
			CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			await inner.EnqueueWithPayloadTransformAsync(message, context, transformPayload, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("enqueue", 1, sw.Elapsed.TotalMilliseconds);
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
		public async ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();

			// Forwarded, not swallowed. A decorator that awaits the inner store and returns nothing
			// reinstates the defect the outcome exists to prevent, for every host that enables telemetry
			// only -- and it does so invisibly, because the decorated path still compiles and still logs.
			var outcome = await inner.MarkDeadLetteredAsync(messageId, reason, attemptCount, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_dead_lettered", 1, sw.Elapsed.TotalMilliseconds);
			return outcome;
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
	private sealed class MeasuredClaimScoped(IClaimScopedOutboxStore inner, TelemetryOutboxStoreDecorator owner) : IClaimScopedOutboxStore
	{
		public async ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, string claimIdentity, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var outcome = await inner.MarkSentAsync(messageId, claimIdentity, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_sent", outcome is OutboxCompletionOutcome.Applied ? 1 : 0, sw.Elapsed.TotalMilliseconds);
			return outcome;
		}

		public async ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount, string claimIdentity, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var outcome = await inner.MarkFailedAsync(messageId, errorMessage, retryCount, claimIdentity, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_failed", outcome is OutboxCompletionOutcome.Applied ? 1 : 0, sw.Elapsed.TotalMilliseconds);
			return outcome;
		}

		public async ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount, DateTimeOffset nextAttemptAt, string claimIdentity, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var outcome = await inner.MarkFailedWithBackoffAsync(messageId, errorMessage, retryCount, nextAttemptAt, claimIdentity, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_failed_with_backoff", outcome is OutboxCompletionOutcome.Applied ? 1 : 0, sw.Elapsed.TotalMilliseconds);
			return outcome;
		}

	}

	private sealed class MeasuredFencedClaimScoped(IFencedClaimScopedOutboxStore inner, TelemetryOutboxStoreDecorator owner) : IFencedClaimScopedOutboxStore
	{
		public async ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, OutboxWriteAuthority authority, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var outcome = await inner.MarkSentAsync(messageId, authority, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_sent", outcome is OutboxCompletionOutcome.Applied ? 1 : 0, sw.Elapsed.TotalMilliseconds);
			return outcome;
		}

		public async ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount, DateTimeOffset? nextAttemptAt, OutboxWriteAuthority authority, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var outcome = await inner.MarkFailedAsync(messageId, errorMessage, retryCount, nextAttemptAt, authority, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_failed", outcome is OutboxCompletionOutcome.Applied ? 1 : 0, sw.Elapsed.TotalMilliseconds);
			return outcome;
		}

	}

	private sealed class MeasuredClaimDeadLetterable(IClaimScopedDeadLetterableOutboxStore inner, TelemetryOutboxStoreDecorator owner) : IClaimScopedDeadLetterableOutboxStore
	{
		public async ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, string claimIdentity, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var outcome = await inner.MarkDeadLetteredAsync(messageId, reason, attemptCount, claimIdentity, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_dead_lettered", outcome is OutboxCompletionOutcome.Applied ? 1 : 0, sw.Elapsed.TotalMilliseconds);
			return outcome;
		}

	}

	private sealed class MeasuredFencedDeadLetterable(IFencedDeadLetterableOutboxStore inner, TelemetryOutboxStoreDecorator owner) : IFencedDeadLetterableOutboxStore
	{
		public async ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, long fencingToken, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var outcome = await inner.MarkDeadLetteredAsync(messageId, reason, attemptCount, fencingToken, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_dead_lettered", outcome is OutboxCompletionOutcome.Applied ? 1 : 0, sw.Elapsed.TotalMilliseconds);
			return outcome;
		}

		public async ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, OutboxWriteAuthority authority, CancellationToken cancellationToken)
		{
			var sw = ValueStopwatch.StartNew();
			var outcome = await inner.MarkDeadLetteredAsync(messageId, reason, attemptCount, authority, cancellationToken).ConfigureAwait(false);
			owner.RecordOperation("mark_dead_lettered", outcome is OutboxCompletionOutcome.Applied ? 1 : 0, sw.Elapsed.TotalMilliseconds);
			return outcome;
		}

	}

}
