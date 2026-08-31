// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Middleware.Ordering;

/// <summary>
/// Enforces strictly-increasing per-key ordering for messages that carry a stamped ordering sequence.
/// </summary>
/// <remarks>
/// <para>
/// This middleware is <strong>opt-in and fail-closed</strong>. Ordering enforcement is opted into by
/// registering it (<c>AddOrderingValidation()</c>); once active it requires <em>every</em> message it
/// sees to carry an ordering sequence — stamped automatically at a first-party transport receive
/// boundary (Kafka offset, Azure Service Bus <c>SequenceNumber</c>) or explicitly by the consumer via
/// <see cref="OrderingContextExtensions.SetOrderingSequence(IMessageContext, long, string?)"/> (the
/// opt-in arm for transports without a native monotonic sequence, e.g. Pub/Sub).
/// </para>
/// <para>
/// <strong>Fail-closed completeness:</strong> a message reaching this active middleware with <em>no</em>
/// resolvable sequence is a misconfiguration (ordering advertised but unfed) and is <em>rejected</em> by
/// throwing <see cref="OutOfOrderMessageException"/> — never silently passed. A missing sequence being
/// expressible as a silent pass would re-open the original advertised-but-unwired degrade.
/// </para>
/// <para>
/// When a stamped sequence is not strictly greater than the last sequence already accepted for the same
/// ordering key, the message is likewise <em>rejected</em> by throwing
/// <see cref="OutOfOrderMessageException"/> — it is never processed out of order.
/// </para>
/// </remarks>
internal sealed partial class OrderingValidationMiddleware : DispatchMiddlewareBase
{
	// Per-ordering-key high-water sequence. A partition/stream is processed sequentially, so contention
	// is low; the CAS loop below keeps the check-and-advance atomic without a lock.
	private readonly ConcurrentDictionary<string, long> _lastSequences = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public override DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

	/// <inheritdoc />
	protected override async ValueTask<IMessageResult> ProcessAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Fail-closed completeness: this middleware is active, so a message MUST carry a resolvable
		// ordering sequence (auto-stamped by a native-sequence transport or consumer-stamped). A missing
		// sequence is an advertised-but-unfed misconfiguration — reject it rather than silently pass
		// (a silent pass would re-open the original non-enforcing degrade).
		if (!context.TryGetOrderingSequence(out var sequence, out var orderingKey))
		{
			throw new OutOfOrderMessageException(
				"Ordering validation is active but the message carries no ordering sequence. Stamp one via "
				+ "SetOrderingSequence (auto-stamped for Kafka/Azure Service Bus; consumer-stamped otherwise) "
				+ "or do not register AddOrderingValidation for this pipeline.");
		}

		// verify order BEFORE processing, but advance the watermark only AFTER the handler
		// succeeds. Advancing on receipt breaks at-least-once retry: if the handler for seq-N throws and
		// the transport redelivers N, an already-advanced watermark would reject the redelivery as
		// "out of order" and the message would be stuck forever. Advancing on success means a
		// failed-then-redelivered N is re-attempted in order, while N+1 still can't jump ahead of an
		// unprocessed N.
		EnsureInOrder(orderingKey, sequence);

		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		// Handler completed without throwing — commit the new high-water mark for this ordering key.
		AdvanceWatermark(orderingKey, sequence);

		return result;
	}

	// Fail-closed order check: a non-increasing sequence (a duplicate or an out-of-order arrival) throws
	// OutOfOrderMessageException and does NOT invoke the rest of the pipeline. Does not mutate the
	// watermark — that happens only after successful processing.
	private void EnsureInOrder(string orderingKey, long sequence)
	{
		if (_lastSequences.TryGetValue(orderingKey, out var last) && sequence <= last)
		{
			throw new OutOfOrderMessageException(orderingKey, sequence, last);
		}
	}

	// Advances the key's high-water mark to the just-processed sequence. Idempotent and race-safe: if a
	// concurrent processor already advanced past this sequence, this is a no-op.
	private void AdvanceWatermark(string orderingKey, long sequence)
	{
		while (true)
		{
			if (_lastSequences.TryGetValue(orderingKey, out var last))
			{
				if (sequence <= last)
				{
					return; // already at/past this sequence (concurrent advance) — nothing to do
				}

				if (_lastSequences.TryUpdate(orderingKey, sequence, last))
				{
					return;
				}

				// A competing thread advanced the key; re-read and re-check.
				continue;
			}

			if (_lastSequences.TryAdd(orderingKey, sequence))
			{
				return;
			}

			// A competing thread added the key first; re-read and re-check.
		}
	}
}
