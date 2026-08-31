// Copyright (c) Excalibur contributors. All rights reserved.

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
/// testing the type:
/// </para>
/// <code>
/// if (store is IFencedOutboxStore fenced &amp;&amp; currentToken is { } token)
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
	/// Implementations MUST claim only rows whose stored fencing high-water mark is less than or equal to
	/// <paramref name="fencingToken"/>, and MUST atomically advance the stored high-water mark to the maximum
	/// of its current value and <paramref name="fencingToken"/> as part of the same claim operation. A
	/// presented token below the stored high-water mark indicates a superseded (stale) leader; the store MUST
	/// exclude those rows from the claim rather than returning them. This is a set-based operation, so a stale
	/// token simply yields fewer or zero claimable rows — it MUST NOT throw.
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
