// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Middleware.Ordering;

/// <summary>
/// Enforces strictly-increasing per-key ordering for messages that carry a stamped ordering sequence.
/// </summary>
/// <remarks>
/// <para>
/// This middleware is <strong>opt-in and fail-closed</strong>, and it enforces only on messages that
/// arrived through a receive path where ordering applies. It is registered once for the whole process
/// and sees every dispatch, so an outbound send or an in-process command — neither of which has an
/// ordering sequence, or any reason to — passes through untouched.
/// </para>
/// <para>
/// A message enters that scope when the bridge from receiving to dispatching calls
/// <c>TransportOrderingMetadata.TryStampOrdering</c>, which marks the context and lifts the transport's
/// native sequence (a Kafka offset, an Azure Service Bus <c>SequenceNumber</c>) onto it. Where the
/// transport has no native monotonic sequence — Pub/Sub among them — the consumer stamps it with
/// <see cref="OrderingContextExtensions.SetOrderingSequence(IMessageContext, long, string?)"/> and marks
/// the context with <see cref="OrderingContextExtensions.MarkOrderingEnforced(IMessageContext)"/>.
/// Nothing stamps this automatically: the framework has no receive-to-dispatch boundary of its own.
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

		// Fail-closed, but only within scope. A message that entered a receive path where ordering is
		// enforced MUST carry a resolvable sequence; arriving without one means the transport did not
		// supply it and nothing stamped it, so reject rather than pass silently. A message outside that
		// scope has no sequence to check and is none of this middleware's business.
		if (!context.TryGetOrderingSequence(out var sequence, out var orderingKey))
		{
			if (!context.IsOrderingEnforced())
			{
				// This message never entered an ordered receive path, so there is no sequence it was
				// ever going to carry. This middleware is registered once for the whole process and
				// sees every dispatch -- outbound sends and plain in-process commands included -- so
				// without this check, registering it would reject every message in the application.
				return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			}

			throw new OutOfOrderMessageException(
				"This message arrived through a receive path where ordering is enforced, but carries no "
				+ "ordering sequence. The transport did not supply one and none was stamped. Stamp it in "
				+ "the bridge that turns a received message into a dispatch, by calling "
				+ "TransportOrderingMetadata.TryStampOrdering for a transport with a native sequence, or "
				+ "SetOrderingSequence directly for one without.");
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
