// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for outbox stores that can scope a failure report to the CLAIM the caller holds,
/// rather than to the process it runs in.
/// </summary>
/// <remarks>
/// <para>
/// A fencing token identifies a TENURE. It cannot discriminate two claim cycles of the same tenure, so a
/// report from an earlier cycle of the same process is indistinguishable from one by the current holder.
/// The claim identity is per-claim, and it travels on the message as
/// <see cref="OutboundMessage.DispatcherId"/> because a map from message id to claim is overwritten by the
/// re-claim it exists to detect.
/// </para>
/// <para>
/// This is a SEGREGATED capability interface — composition, NOT inheritance of <see cref="IOutboxStore"/> —
/// so that a decorator enforcing a confidentiality boundary can forward it whole: no member here accepts or
/// returns a message payload.
/// </para>
/// <para>
/// Discovered through <see cref="IServiceProvider.GetService(System.Type)"/>, never by casting the store. A
/// cast sees only the outermost type and is lossy through any decorator.
/// </para>
/// </remarks>
public interface IClaimScopedOutboxStore
{
	/// <summary>
	/// Records a delivery failure reported by the claim identified by <paramref name="claimIdentity"/>.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="errorMessage">The error describing the failure.</param>
	/// <param name="retryCount">The absolute attempt count for this message.</param>
	/// <param name="claimIdentity">
	/// The claim under which the caller received this message, from
	/// <see cref="OutboundMessage.DispatcherId"/>.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>What the store did, decided by the same atomic action that performed the mutation.</returns>
	/// <remarks>
	/// Implementations MUST evaluate the claim and the mutation in ONE atomic action, and MUST decide
	/// whether to refuse from that same action. A store that reconstructs the decision from a separate
	/// read compares a value captured before the window it must describe, which is false exactly when the
	/// refusal is real.
	/// <para>
	/// <b>A refusal is REPORTED, not thrown, and the reason is the caller's control flow rather than
	/// style.</b> The drain reports a delivery failure from inside its own exception handler; an exception
	/// raised there is not caught by a sibling <c>catch</c> on the same <c>try</c>, so it escapes the whole
	/// cycle and abandons every message the caller still holds. The refusal must cost one message, so it
	/// travels in the return value where the caller reads it without unwinding.
	/// </para>
	/// <para>
	/// <b>A refusal here is ROW-LEVEL, and the distinction decides how much work is abandoned.</b> A fence
	/// refusal means a NEWER TENURE exists and the whole drain cycle must stop, whereas
	/// <see cref="OutboxCompletionOutcome.ClaimLost"/> means only that this message was re-claimed — the
	/// caller stops writing for THIS message and continues with the others it still holds. A caller that
	/// aborts its cycle on a lost claim abandons messages a live tenure owns.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	ValueTask<OutboxCompletionOutcome> MarkFailedAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		string claimIdentity,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records a delivery failure reported by the claim identified by <paramref name="claimIdentity"/>,
	/// applying the per-message backoff schedule.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="errorMessage">The error describing the failure.</param>
	/// <param name="retryCount">The absolute attempt count for this message.</param>
	/// <param name="nextAttemptAt">The time before which the message must NOT be re-claimed.</param>
	/// <param name="claimIdentity">
	/// The claim under which the caller received this message, from
	/// <see cref="OutboundMessage.DispatcherId"/>.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>What the store did, decided by the same atomic action that performed the mutation.</returns>
	/// <remarks>
	/// This member exists because the backoff route is the one a genuine delivery failure takes when
	/// automatic retry is enabled. Scoping only the plain completion would leave the dominant failure path
	/// unscoped while the capability appeared to close it. The same atomicity and reported-refusal
	/// requirements as <see cref="MarkFailedAsync"/> apply here in full.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		string claimIdentity,
		CancellationToken cancellationToken);
}
