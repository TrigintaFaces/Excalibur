// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// Extension methods for <see cref="IOutboxStore"/>.
/// </summary>
public static class OutboxStoreExtensions
{
	/// <summary>Marks a batch of messages as successfully sent.</summary>
	public static async ValueTask MarkBatchSentAsync(this IOutboxStore store, IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store.GetService(typeof(IOutboxStoreBatch)) is IOutboxStoreBatch batch)
		{
			await batch.MarkBatchSentAsync(messageIds, cancellationToken).ConfigureAwait(false);
			return;
		}

		foreach (var messageId in messageIds)
		{
			await store.MarkSentAsync(messageId, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Marks a batch of messages as failed.</summary>
	public static async ValueTask MarkBatchFailedAsync(this IOutboxStore store, IReadOnlyList<string> messageIds, string reason, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store.GetService(typeof(IOutboxStoreBatch)) is IOutboxStoreBatch batch)
		{
			await batch.MarkBatchFailedAsync(messageIds, reason, retryCount, cancellationToken).ConfigureAwait(false);
			return;
		}

		foreach (var messageId in messageIds)
		{
			await store.MarkFailedAsync(messageId, reason, retryCount, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Marks a batch of messages as failed UNDER A CLAIM, reporting per message whether the store applied it.
	/// </summary>
	/// <param name="store">The outbox store. Must expose <see cref="IClaimScopedOutboxStore"/>.</param>
	/// <param name="messageIds">The messages to complete.</param>
	/// <param name="reason">The failure reason recorded against each message.</param>
	/// <param name="retryCount">The attempt count recorded against each message.</param>
	/// <param name="claimIdentity">The claim the caller holds and is reporting under.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>
	/// One outcome per message id, in the same order. Test for
	/// <see cref="OutboxCompletionOutcome.Applied"/> BY NAME; every other value, defined or not, means the
	/// store did not apply that message's completion.
	/// </returns>
	/// <exception cref="NotSupportedException">
	/// Thrown when <paramref name="store"/> does not expose <see cref="IClaimScopedOutboxStore"/>. A caller
	/// holding a claim cannot be silently served an unscoped completion: that is the defect this overload
	/// exists to remove, and returning a success-shaped answer would reintroduce it one layer up.
	/// </exception>
	/// <remarks>
	/// <para>
	/// <b>Prefer this over the unscoped overload whenever the caller holds a claim.</b> The unscoped one
	/// completes on <see cref="IOutboxStore.MarkFailedAsync"/> with no ownership check, so a caller whose
	/// claim has lapsed — because its dispatch hung past the reservation window and a later cycle re-claimed
	/// the message — has its report accepted anyway, and overwrites a decision it is no longer entitled to
	/// make.
	/// </para>
	/// <para>
	/// <b>It deliberately does NOT take the batch fast path, and that is a capability limit rather than an
	/// oversight.</b> <see cref="IOutboxStoreBatch.MarkBatchFailedAsync"/> returns no value and carries no
	/// claim term, so it can neither honour a claim nor report a refusal; routing a claim-scoped completion
	/// through it would discard the guarantee this overload exists to provide. Until that member can express
	/// an outcome, per-message completion is the only honest implementation, and the cost is stated here
	/// rather than hidden.
	/// </para>
	/// <para>
	/// <b>Outcomes are per message because a lost claim is ROW-scoped.</b> One message being re-claimed says
	/// nothing about the others: the caller still owns the rest of the batch and must still complete them. A
	/// single aggregate result would force the caller to treat one re-claimed row as a whole-batch failure,
	/// which strands messages it still holds.
	/// </para>
	/// </remarks>
	public static async ValueTask<IReadOnlyList<OutboxCompletionOutcome>> MarkBatchFailedAsync(
		this IOutboxStore store,
		IReadOnlyList<string> messageIds,
		string reason,
		int retryCount,
		string claimIdentity,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(messageIds);
		ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);

		if (store.GetService(typeof(IClaimScopedOutboxStore)) is not IClaimScopedOutboxStore claimScoped)
		{
			throw new NotSupportedException(
				"This outbox store does not support claim-scoped completion, so a batch failure reported "
				+ "under a claim cannot be verified against it. Use the overload without a claim identity if "
				+ "the caller genuinely holds no claim, or configure a store that implements "
				+ nameof(IClaimScopedOutboxStore) + ".");
		}

		var outcomes = new OutboxCompletionOutcome[messageIds.Count];

		for (var i = 0; i < messageIds.Count; i++)
		{
			outcomes[i] = await claimScoped
				.MarkFailedAsync(messageIds[i], reason, retryCount, claimIdentity, cancellationToken)
				.ConfigureAwait(false);
		}

		return outcomes;
	}

	/// <summary>Atomically marks message as sent and creates the inbox entry in one transaction, so a redelivery is skipped rather than reprocessed (effectively-once processing; delivery remains at-least-once).</summary>
	public static ValueTask<bool> TryMarkSentAndReceivedAsync(this IOutboxStore store, string messageId, InboxEntry inboxEntry, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store.GetService(typeof(IOutboxStoreBatch)) is IOutboxStoreBatch batch)
		{
			return batch.TryMarkSentAndReceivedAsync(messageId, inboxEntry, cancellationToken);
		}
		return ValueTask.FromResult(false);
	}
}
