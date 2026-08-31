// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for <see cref="IInboxStore"/> implementations that support an atomic
/// claim-before-execute idempotency protocol: reserve a message for processing, then either finalize
/// it on success or release it on failure.
/// </summary>
/// <remarks>
/// <para>
/// This is a segregated capability interface (composition, NOT inheritance of <see cref="IInboxStore"/>)
/// so that <see cref="IInboxStore"/> stays within the Interface Segregation threshold. Inbox stores that
/// support atomic claiming implement this interface in addition to <see cref="IInboxStore"/>.
/// </para>
/// <para>
/// The protocol makes idempotent handling correct under concurrent duplicate delivery <b>without</b>
/// dropping a message whose handler fails:
/// </para>
/// <list type="number">
/// <item><description><see cref="TryClaimAsync(string, string, CancellationToken)"/> atomically before the handler runs. <see langword="false"/> means another caller already holds the claim (duplicate) — skip.</description></item>
/// <item><description>On handler success, finalize the claim via <see cref="IInboxStore.MarkProcessedAsync"/> (the entry becomes terminal <see cref="InboxStatus.Processed"/>).</description></item>
/// <item><description>On handler failure, <see cref="ReleaseAsync"/> the claim so a redelivery can re-admit the message. Leaving a terminal entry on failure would silently drop the message.</description></item>
/// </list>
/// <para>
/// The steps above are the whole of this protocol: the claim never auto-expires, and the caller governs
/// its TTL. A store that instead supports a self-expiring lease — where admission and expired-lease
/// reclaim collapse into one atomic compare-and-set, and a handler failure needs no explicit release —
/// declares <see cref="ILeasedInboxStore"/>. The two are separate interfaces so a caller can tell which
/// protocol it holds before it calls; a store may declare one, the other, both, or neither.
/// </para>
/// </remarks>
public interface IClaimableInboxStore
{
	/// <summary>
	/// Atomically claims a message for a specific handler by inserting a non-terminal
	/// (<see cref="InboxStatus.Processing"/>) entry if and only if none already exists.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a single atomic "first writer wins" operation (e.g. <c>INSERT … ON CONFLICT DO NOTHING</c>,
	/// <c>INSERT … WHERE NOT EXISTS</c>, or an atomic add). It replaces the racy check-then-act of a
	/// separate <see cref="IInboxStore.IsProcessedAsync"/> followed by a later mark.
	/// </para>
	/// <para>
	/// <b>Admission is decided by the presence of an entry, not by its status.</b> The claim is refused
	/// whenever <em>any</em> entry already exists for the key — <see cref="InboxStatus.Processing"/>,
	/// terminal <see cref="InboxStatus.Processed"/>, and <see cref="InboxStatus.Failed"/> alike. Retry on
	/// this protocol is reached by <see cref="ReleaseAsync"/>, which removes the row: the next redelivery
	/// finds no entry and the claim admits it again. A caller therefore releases on every handler failure
	/// it intends to retry, and records a failure only for an attempt it is handing to the retry drain.
	/// </para>
	/// <para>
	/// <b>A <see cref="InboxStatus.Failed"/> entry is deliberately not re-claimable.</b> It belongs to the
	/// estate-wide retry drain, which reads failed entries and dispatches them itself. This protocol
	/// carries no term — the claim never auto-expires and nothing identifies its holder — so there is
	/// nothing with which to fence a redelivery against the drain. Admitting a failed entry here would let
	/// a redelivery enter the handler while the drain is dispatching the same entry, running it twice.
	/// </para>
	/// <para>
	/// <b>This diverges from <see cref="ILeasedInboxStore"/>, and the divergence is deliberate.</b>
	/// <see cref="ILeasedInboxStore.TryAcquireLeaseAsync"/> <em>does</em> re-admit a
	/// <see cref="InboxStatus.Failed"/> entry for retry, because recording a failure there clears the
	/// lease term — a failed entry has no holder — and every subsequent write is fenced by the term the
	/// acquisition returned, so the retrying processor and any concurrent one cannot both act. The two
	/// protocols reach retry by different means: removal of the row here, re-acquisition under a fresh
	/// term there. A consumer holding one must not assume the other's behaviour.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the caller created the claim (first writer, proceed to handle);
	/// <see langword="false"/> if an entry already exists for the key — already claimed, already processed,
	/// or previously failed (duplicate, skip).
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken);


	/// <summary>
	/// Releases a previously acquired claim for a specific handler by removing the entry, so that a
	/// redelivery of the same message can be re-admitted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Call this when the handler fails after a successful <see cref="TryClaimAsync(string, string, CancellationToken)"/>. Releasing an
	/// already-removed or never-claimed entry is a no-op, and so is releasing it twice: a failure path that
	/// is itself retried must not be turned into a second failure.
	/// </para>
	/// <para>
	/// The removal is restricted to non-terminal entries. A caller is not expected to release a claim it has
	/// already finalized through <see cref="IInboxStore.MarkProcessedAsync"/>, but one whose claim lapsed can
	/// arrive here after a replacement processor took the message over and finalized it, and an
	/// implementation MUST leave that terminal entry in place rather than delete it. Erasing the processed
	/// record would re-admit the message on its next delivery and run the handler a second time, repeating
	/// every side effect, with no duplicate visible to any caller.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous release operation.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken);
}
