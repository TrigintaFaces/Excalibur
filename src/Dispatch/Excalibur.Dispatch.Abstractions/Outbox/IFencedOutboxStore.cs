// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
	/// <returns>What the store did, decided by the same atomic action that performed the mutation.</returns>
	/// <remarks>
	/// Implementations MUST apply the mutation as a single atomic compare-and-swap: only apply it when
	/// <paramref name="fencingToken"/> is greater than or equal to the stored high-water mark, and atomically
	/// advance the stored high-water mark to the maximum of its current value and
	/// <paramref name="fencingToken"/>. A presented token below the stored high-water mark identifies a
	/// superseded leader attempting to complete work it no longer owns, and the store MUST NOT apply the
	/// mutation.
	/// <para>
	/// <b>A REFUSAL IS REPORTED IN THE RETURN VALUE, NEVER SMUGGLED THROUGH A SUCCESS OR A FAILURE.</b> This
	/// member used to return <c>ValueTask</c>, and a void-returning mutation can express exactly two
	/// outcomes: it threw, or it did not. This seam needs three — applied, refused, and an infrastructure
	/// fault — so refusal had to travel through an exception, and implementations reached independently for
	/// four different wrong encodings of it. One reported a refusal as already-sent; one typed it as a
	/// generic failure that a catch-all then dead-lettered; one summed rowcounts across a multi-statement
	/// batch so the caller's own refusal test could never fire. A refusal indistinguishable from a failure
	/// gets disposed of as a failure, and one indistinguishable from a success gets disposed of as a
	/// success — and the second marks a message sent that was never sent. Returning the outcome is what
	/// makes both unrepresentable rather than merely unlikely.
	/// </para>
	/// <para>
	/// <b>ROW-SCOPED AND TENURE-SCOPED REFUSALS DIFFER, and the distinction decides how much
	/// work the caller abandons.</b> <see cref="OutboxCompletionOutcome.FenceRefused"/> means a NEWER TENURE
	/// exists, so every remaining write this caller would make is unfenced and it must stop draining
	/// altogether. <see cref="OutboxCompletionOutcome.ClaimLost"/>,
	/// <see cref="OutboxCompletionOutcome.AlreadyTerminal"/> and
	/// <see cref="OutboxCompletionOutcome.MessageNotFound"/> concern THIS MESSAGE ONLY: the caller records
	/// them and carries on with the rest of its batch. A caller that stands down on a row-scoped refusal
	/// strands messages a live tenure still owns; one that carries on after a fence refusal lets a
	/// superseded tenure keep writing.
	/// </para>
	/// <para>
	/// <b>Callers MUST test for <see cref="OutboxCompletionOutcome.Applied"/> BY NAME.</b> Deciding success
	/// by excluding the refusals known at compile time makes every value added to the enumeration later
	/// begin life as a silent success, which is the way this contract fails open.
	/// </para>
	/// <para>
	/// Infrastructure faults still throw. A refusal is an expected outcome of losing leadership and is not
	/// an error; a connection that dropped is.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException"> Thrown when messageId is null or empty. </exception>
	ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken);
}
