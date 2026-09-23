// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch;

/// <summary>
/// An outbox store that enforces leadership fencing on the drain path.
/// </summary>
/// <remarks>
/// <para>
/// Fencing protects against a superseded leader completing work it no longer owns. A caller that holds a
/// leadership tenure presents its fencing token with every claim and every mark-sent; the store compares the
/// presented token against a stored high-water mark and refuses to act on a stale one.
/// </para>
/// <para>
/// Fencing is a <b>capability</b>, not an option. A store that cannot enforce it implements only
/// <see cref="IOutboxStore"/>, whose methods take no token — so a non-fencing store has no way to
/// <i>receive</i> a token, and therefore no way to silently discard one. Callers select the fenced drain by
/// asking the store for the capability — never by casting it, which sees only the outermost type and so
/// reports the capability absent whenever the store sits behind a decorator:
/// </para>
/// <code>
/// if (store.GetService(typeof(IFencedOutboxStore)) is IFencedOutboxStore fenced &amp;&amp; currentToken is { } token)
/// {
///     messages = await fenced.GetUnsentMessagesAsync(batchSize, token, ct);
/// }
/// else
/// {
///     messages = await store.GetUnsentMessagesAsync(batchSize, ct);
/// }
/// </code>
/// <para>
/// The token is non-nullable. "No fencing applies" is expressed by calling the unfenced
/// <see cref="IOutboxStore"/> members, never by presenting a null or zero token.
/// </para>
/// </remarks>
public interface IFencedOutboxStore : IOutboxStore
{
	/// <summary>
	/// Claims a batch of unsent messages on behalf of the leadership tenure identified by
	/// <paramref name="fencingToken"/>.
	/// </summary>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="fencingToken"> The fencing token for the caller's current leadership tenure. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Collection of unsent messages ready for delivery. </returns>
	/// <remarks>
	/// <b>The high-water mark is stored once per outbox SCOPE, not per message row.</b> Implementations
	/// MUST claim rows only when <paramref name="fencingToken"/> is greater than or equal to the scope's
	/// stored mark, and MUST advance that mark to the maximum of its current value and
	/// <paramref name="fencingToken"/> within the same atomic action as the claim. A presented token below
	/// the mark identifies a superseded tenure: the claim yields zero rows and MUST NOT throw — this is a
	/// set-based operation, not an error.
	/// <para>
	/// <b>The mark answers which TENURE may act on this scope. It records nothing about an individual row,
	/// and a per-row column does not implement this contract.</b> A per-row mark cannot see a tenure that
	/// touched a different row in the same scope, so it admits a superseded leader on every row the current
	/// leader has not yet reached — which is most rows, on every handover. It would also pass a
	/// single-row test, which is why the distinction is stated here rather than left to the reader.
	/// </para>
	/// <para>
	/// A token identifies a tenure and cannot discriminate two claim cycles of the same tenure, so this
	/// mark is necessary and not sufficient on its own: completing a message additionally requires the
	/// claim identity the row carries. See the claim-scoped capability for that half.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when batchSize is less than 1. </exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
		int batchSize,
		long fencingToken,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as successfully sent on behalf of the leadership tenure identified by
	/// <paramref name="fencingToken"/>.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message to mark as sent. </param>
	/// <param name="fencingToken"> The fencing token for the caller's current leadership tenure. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous mark-sent operation. </returns>
	/// <remarks>
	/// Implementations MUST apply the mutation as a single atomic compare-and-swap: only apply it when
	/// <paramref name="fencingToken"/> is greater than or equal to the stored high-water mark, and atomically
	/// advance the stored high-water mark to the maximum of its current value and
	/// <paramref name="fencingToken"/>. A presented token below the stored high-water mark indicates a
	/// superseded (stale) leader attempting to complete work it no longer owns; the store MUST reject the
	/// mutation and fail closed by throwing <see cref="StaleOutboxFencingTokenException"/> rather than
	/// silently ignoring it.
	/// </remarks>
	/// <exception cref="ArgumentException"> Thrown when messageId is null or empty. </exception>
	/// <exception cref="StaleOutboxFencingTokenException">
	/// Thrown when <paramref name="fencingToken"/> is below the stored high-water mark.
	/// </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the message does not exist or is already marked as sent. </exception>
	ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken);
}
