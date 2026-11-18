// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// Provides fencing tokens for leader election scenarios to prevent stale leader operations.
/// </summary>
/// <remarks>
/// <para>
/// A fencing token is a monotonically increasing value issued when a leader acquires a lock.
/// Downstream resources validate the token to reject operations from stale leaders that
/// have lost their lease but haven't yet detected the loss.
/// </para>
/// <para>
/// This follows the distributed systems fencing token pattern described by Martin Kleppmann.
/// </para>
/// <para>
/// All contenders for the same protected resource must use the same mark authority and counter history
/// as the election they participate in. The authority is shared within the provider's supported scope;
/// an in-memory shared-state instance is process-local, not a distributed authority. Independent custom
/// reader/issuer registrations must preserve that same authority relationship.
/// </para>
/// <para>
/// Reads must observe the authoritative mark in real-time order with completed generations. A lagging
/// replica, secondary or standby is not an acceptable fallback: reading an older mark could admit a
/// superseded token. Unsupported targets or unavailable consistency must fail rather than return an
/// older mark. This requirement assumes retained counter history; it does not detect resets or restores.
/// </para>
/// <para>
/// <b>EVERY FENCING TOKEN IS STRICTLY POSITIVE. ZERO IS UNSET, NOT A VALUE.</b> This binds every member
/// here and every implementation. <see cref="GetTokenAsync"/> returns <see langword="null"/> -- never zero
/// -- when no token has been issued for the resource, including when the backing store's counter exists
/// but has never been advanced. <see cref="IFencingTokenIssuer.IssueTokenAsync"/> never returns zero; where it cannot produce
/// a positive token it fails closed rather than returning one. <see cref="ValidateTokenAsync"/> refuses
/// every token when no mark has been recorded, and refuses a non-positive presented token.
/// </para>
/// <para>
/// The reason this is stated rather than left to each implementation: validation is a high-water
/// comparison of the form <c>token &gt;= mark</c>, so a mark reported as zero accepts <em>every</em>
/// non-negative token presented against it, including a fabricated one. The state that must refuse
/// everything is exactly the state that would otherwise admit everything, which is why "no mark" must be
/// representable as something other than a low real-looking value.
/// </para>
/// </remarks>
public interface IFencingTokenProvider
{
	/// <summary>
	/// Gets the current fencing token for the active leader.
	/// </summary>
	/// <param name="resourceId">The identifier of the protected resource.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>
	/// The current fencing token value, or <see langword="null"/> if no token has ever been issued for
	/// this resource.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Three outcomes are distinguishable and all three must stay distinguishable:</b> a value,
	/// <see langword="null"/> meaning no token was ever issued, and a <em>failure</em>. An implementation
	/// that cannot read or cannot parse the stored mark MUST throw. It may not substitute a value, and it
	/// may not report <see langword="null"/>.
	/// </para>
	/// <para>
	/// <b>Reporting <see langword="null"/> for an unreadable mark is the tempting answer and it is wrong.</b>
	/// <see langword="null"/> means no token was ever issued, which licenses the <em>first</em> mint to
	/// start from the bottom of the domain. So treating an unreadable mark as "none" authorises a mint that
	/// moves the high-water mark BACKWARD, after which every token any previous leader held validates again.
	/// Absent and unreadable differ in exactly one respect — whether a predecessor may exist — and that is
	/// the whole question a fence asks.
	/// </para>
	/// <para>
	/// A failure here is not a degraded answer to be worked around: the caller must relinquish rather than
	/// proceed on a fence it could not establish. Delaying a leadership acquisition is recoverable;
	/// admitting a superseded leader's write is not.
	/// </para>
	/// <para>
	/// <b>Every reported mark must be an actually generated token for this resource.</b> An unused
	/// sequence START WITH value or a merely reserved cache boundary is not a generated token. Reporting a
	/// reservation above the live incumbent's token would incorrectly reject that incumbent.
	/// </para>
	/// <para>
	/// Generation and receipt are separate events. The store may generate a token before the issuing
	/// call returns, and a connection failure or cancellation may prevent that call from returning its
	/// result. This method may still observe that generated token. It must not rewind the mark or report
	/// <see langword="null"/> merely because an issuer did not receive the result.
	/// </para>
	/// <para>
	/// For a native-counter provider, a committed ownership transition generates the token. The
	/// requirement concerns actual underlying generation, not acknowledgment by a particular caller;
	/// observation of that counter does not imply the separate minting capability.
	/// </para>
	/// <para>Store, consistency and parse failures propagate. A pre-canceled caller token is observed before store access.</para>
	/// </remarks>
	/// <exception cref="ArgumentException">The resource identifier is null, empty or whitespace.</exception>
	/// <exception cref="OperationCanceledException">Cancellation was observed; no absent-mark value is substituted.</exception>
	ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken);

	/// <summary>
	/// Validates that the provided fencing token is current and not stale.
	/// </summary>
	/// <param name="resourceId">The identifier of the protected resource.</param>
	/// <param name="token">The fencing token to validate.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>
	/// <see langword="true"/> if the token is valid and current; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Consumer pattern: capture-pin the token at the leadership transition.</b> A consumer
	/// must capture the authoritative token associated with the acquired <see cref="Leadership"/> tenure
	/// and <em>pin</em> that captured value for that tenure, attaching the same fixed token to
	/// every protected operation. Do <em>not</em> call <see cref="GetTokenAsync"/> per-operation: after a
	/// successor advances the high-water mark, a per-operation read would return the successor's <em>newer</em>
	/// token, so a stale leader would fence its own writes <em>through</em> instead of being rejected — defeating
	/// the pattern. The acquisition path captures the authoritative token at that transition; mint/acquisition
	/// co-atomicity is provider-specific. The consumer holds the captured token fixed.
	/// On leadership loss, discard the pin and stop admitting new protected work for that tenure.
	/// Do not obtain a successor's token by rereading the mutable mark. Already in-flight work still
	/// requires atomic enforcement at its protected resource; local loss is not remote revocation.
	/// </para>
	/// <para>
	/// <b>The relation has two terms, and both are required: a token is current when a mark has been
	/// recorded <em>and</em> the token is greater than or equal to it.</b> Where a mark exists, only a
	/// strictly lower token is stale — that is the same at-or-above relation
	/// <see cref="IFencedResource"/> enforces, and it is stated here because minting and validating use
	/// different relations: <see cref="IFencingTokenIssuer.IssueTokenAsync"/> is strictly monotonic across tenures, while
	/// validation must admit the incumbent's pinned token every time it is re-presented within one
	/// tenure. Strengthening validation to strictly greater refuses the live leader's second and every
	/// subsequent operation.
	/// </para>
	/// <para>
	/// <b>The at-or-above comparison presupposes a recorded mark. Where none has been recorded, every
	/// token is refused — including one above any value the resource could ever have issued.</b> A
	/// mark-bearing artifact that exists but was never advanced is <em>not</em> a recorded mark, and it
	/// is the reachable case: the issue path can create that artifact and advance it in separate steps,
	/// so an interruption between them leaves it existing and unadvanced, and an implementation reading
	/// a column that cannot express never-advanced reports the artifact's starting value as a
	/// high-water mark. The
	/// comparison alone does not express that state: with no mark there is nothing for a token to be
	/// lower than, so a relation stated as a comparison on its own admits everything in precisely the
	/// one state that must admit nothing. That failure is an inversion rather than an off-by-one, which
	/// is why the largest representable token is a required case for an implementation to refuse and not
	/// a decorative one.
	/// </para>
	/// <para>
	/// <b>THIS PROMISES NOTHING ABOUT THE INTERVAL AFTER IT RETURNS, and that is the clause that makes
	/// the abstraction honest.</b> The result describes an instant that has already passed by the time
	/// the caller reads it: a successor may advance the mark between this method returning
	/// <see langword="true" /> and the caller's next instruction. So a <see langword="true" /> here is
	/// not permission to proceed -- it is an observation about the past.
	/// </para>
	/// <para>
	/// A caller that needs the guarantee to hold DURING its operation cannot get it from a pre-flight
	/// check, however the relation is defined. The enforcement has to be part of the write itself, and
	/// in practice that means a CONDITIONAL UPDATE IN THE CALLER'S OWN STORE — an update predicated on a
	/// stored high-water column, so the store either applies the write or rejects it in one step. That is
	/// outside this framework, because only the store holding the data can make it atomic with the write.
	/// </para>
	/// <para>
	/// <b><see cref="IFencedResource" /> is the in-process approximation of that, not a substitute for
	/// it, and the distinction is load-bearing.</b> Its <c>GuardAsync</c> takes a token and returns
	/// nothing — the operation is not a parameter — so a caller necessarily guards and then writes in two
	/// steps, and a successor can advance the mark in between. It exists for a resource with no external
	/// enforcement point at all (an in-memory aggregate, a cache entry, a connection), where a two-step
	/// guard is better than none. Reach for it there, and do NOT read it as the at-the-write enforcement
	/// this paragraph calls for.
	/// </para>
	/// <para>
	/// This is also why the choice of relation here is the secondary question: no relation makes a
	/// check-then-act sequence safe, and a caller who believes otherwise has a race that no change to
	/// this method can close.
	/// </para>
	/// <para>
	/// A completed negative comparison returns false. An unreadable authority, unsupported consistency or
	/// store fault propagates instead; callers must not admit work after such a failure. Preserve the
	/// original fault for retry/classification and honor caller cancellation. A pre-canceled token is
	/// observed before store access. Failure to read is not proof that the presented token is stale.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">The resource identifier is null, empty or whitespace.</exception>
	/// <exception cref="OperationCanceledException">Cancellation was observed; it is not a stale-token answer.</exception>
	ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken);

}
