// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// An outbox store that can evaluate BOTH the leadership fence and the claim in the one atomic action that
/// records a delivery failure.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the intersection of two capabilities, not a member added to either of them.</b> A fenced
/// completion needs a fencing token AND a claim identity, so it can only be honoured by a store that has
/// both. Declaring it on the fencing contract would oblige every fenced store to implement a member most of
/// them cannot honour — a precondition strengthened on an existing contract, imposed on implementors that
/// never asked for it. Keeping the obligation on its own contract lets a store that has only one of the two
/// remain honest rather than throwing from a member its interface claims it has.
/// </para>
/// <para>
/// <b>It inherits NOTHING, and that is a confidentiality requirement rather than a style choice.</b> The
/// obvious spelling — deriving from the fencing and claim-scoped contracts to make the product explicit to
/// the type system — pulls in the payload-bearing store surface transitively. A decorator that enforces a
/// confidentiality boundary can forward a capability unmediated only when no member of it, inherited
/// members included, carries a message payload; a capability that fails that test is denied instead, and a
/// denied capability is silently absent for exactly the consumers who enabled encryption. So the product is
/// carried by the MEMBER — which cannot be called without both a tenure token and a claim identity — and
/// not by the base list. Nothing here moves a payload: identifiers, an error string, a retry count, an
/// optional instant, and two opaque tokens.
/// </para>
/// <para>
/// <b>Discovered through <see cref="IServiceProvider.GetService(System.Type)"/>, never by casting the
/// store.</b> A cast sees only the outermost type and is lossy through any decorator, so a decorated store
/// that has this capability would answer "absent" while the startup guard, which probes correctly, answers
/// "present".
/// </para>
/// <para>
/// <b>Why this closes what the fence alone does not.</b> The fence covers the claim and the mark-sent; a
/// failure report carrying no token is a write a superseded tenure can still perform, and a report carrying
/// no claim is one an earlier cycle of the SAME tenure can still perform. Only a member holding both terms,
/// compared inside one statement, refuses both.
/// </para>
/// </remarks>
public interface IFencedClaimScopedOutboxStore
{
	/// <summary>
	/// Records a delivery failure, applying it only if the caller still holds BOTH the tenure and the claim.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="errorMessage">The error describing the failure.</param>
	/// <param name="retryCount">The absolute attempt count for this message.</param>
	/// <param name="nextAttemptAt">
	/// The time before which the message must NOT be re-claimed, or <see langword="null"/> to record the
	/// failure without a computed backoff schedule.
	/// </param>
	/// <param name="authority">The tenure and claim the caller is acting under.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>What the store did, decided by the same atomic action that performed the mutation.</returns>
	/// <remarks>
	/// <para>
	/// <b>ONE member rather than two, because the backoff schedule is a VALUE the caller already has or does
	/// not.</b> The unfenced surface splits the plain and scheduled completions across two members; here the
	/// caller has already decided which it wants and holds the computed instant, so a second member would
	/// differ only in whether one argument was supplied. The nullable schedule is not the omitted-guard shape
	/// that the authority deliberately avoids: "there is no backoff schedule" is a real state with a real
	/// meaning to the statement, whereas "there is no tenure" cannot arise on a fenced member at all.
	/// </para>
	/// <para>
	/// <b>Implementations MUST evaluate the fence, the claim and the mutation in ONE atomic action</b>, and
	/// MUST decide the returned outcome from that same action. A store that re-reads the row afterwards to
	/// classify what happened compares a value captured outside the window it must describe, which is wrong
	/// exactly when the refusal is real.
	/// </para>
	/// <para>
	/// <b>Implementations MUST persist the high-water mark such that it never decreases across process
	/// restart, failover or replica promotion.</b> A store whose high-water is only as durable as an
	/// asynchronously-replicated value does not satisfy this and MUST NOT implement this interface.
	/// </para>
	/// <para>
	/// <b>Atomicity is necessary and not sufficient, and the second half is why this clause exists.</b> A
	/// fence is two properties: the comparison and the mutation are one indivisible action, AND the mark is
	/// monotone non-decreasing under every fault in the deployment's model. A store can satisfy the first
	/// perfectly and still be unsafe — if a failover, an eviction or a restore can roll the mark backwards,
	/// a superseded tenure presents a token that is no longer below it and is admitted. <b>A monotonic
	/// counter that can decrease is not a fence</b>, and a store providing only the atomicity half satisfies
	/// the letter of this interface while breaking the guarantee it exists to give.
	/// </para>
	/// <para>
	/// <b>The refusals are REPORTED, never thrown.</b> The drains report a delivery failure from inside their
	/// own exception handlers, where a sibling handler on the same block cannot run, so an exception raised
	/// here escapes the whole cycle and abandons every message the caller still legitimately holds. The two
	/// refusals also differ in how much work they cost:
	/// <see cref="OutboxCompletionOutcome.FenceRefused"/> means a NEWER TENURE exists and the caller must
	/// stop draining entirely, while <see cref="OutboxCompletionOutcome.ClaimLost"/> concerns this one
	/// message and the caller continues with the rest.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	ValueTask<OutboxCompletionOutcome> MarkFailedAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset? nextAttemptAt,
		OutboxWriteAuthority authority,
		CancellationToken cancellationToken);
}
