// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// What a save produced: the version the aggregate reached, and the token a caller passes to a read
/// that must observe this write.
/// </summary>
/// <param name="CommittedVersion">The aggregate's version after the save.</param>
/// <param name="SessionToken">
/// The global position of the LAST event this save wrote, or <see langword="null"/> when the event store
/// assigns no global ordering or this save wrote nothing.
/// </param>
/// <param name="Projections">
/// Whether this save's <c>Inline</c> projections reached <paramref name="SessionToken"/> before returning.
/// <see cref="ProjectionFreshness.NotRequested"/> when nothing was waited for — no projection is
/// registered <c>Inline</c>, so none was ever going to be. <see cref="ProjectionFreshness.Attested"/> when
/// every required <c>Inline</c> projection was OBSERVED to have folded the committed prefix through the token,
/// against the durable roster as it stood at that moment — a receipt, not a standing promise, because the
/// roster can change before you read. For read-your-own-writes, hand <paramref name="SessionToken"/> to
/// <c>IProjectionSessionReader</c> and require <see cref="ProjectionFreshness.Fresh"/>, which is issued
/// only after re-validating roster identity, binding, position and destination around the data read.
/// <see cref="ProjectionFreshness.BoundExpired"/> when the bounded wait ended first:
/// <b>the events ARE durably committed</b> and only the read model is behind, so the correct response is
/// to answer from the stale read model or wait on <c>IProjectionSessionReader</c> — never to repeat the
/// command, which would append the events a second time.
/// </param>
/// <remarks>
/// <para>
/// <b>The token rides the response of the operation that produced it</b>, which is how Azure Cosmos DB's
/// session consistency works: a write returns a session token, the caller carries it, and a subsequent
/// read honours it. The alternatives were considered and rejected — an out-parameter puts transport
/// state on the aggregate, and an ambient value makes "which save produced this" unanswerable the moment
/// two saves interleave.
/// </para>
/// <para>
/// <c>Async</c> projections are applied by the ordered processor after the append commits.
/// <c>Inline</c> drives that same processor and waits within a bound after commit. Its
/// <see cref="ProjectionFreshness.Attested"/> result records an observation at save time; callers
/// requiring read-your-writes still pass the token to a session reader for revalidation.
/// A bounded wait expiring does not roll back the events or authorize repeating the command.
/// </para>
/// <para>
/// <c>TransactionalInline</c> projections are written in the event append's database transaction.
/// A successful coordinated append commits both together. This aggregate-local guarantee does not
/// produce a global-prefix attestation: <see cref="Projections"/> and <see cref="Attestation"/>
/// continue to describe only the separate <c>Inline</c> processor, if configured.
/// </para>
/// <para>
/// <b><see langword="null"/> is a real answer, not a missing one.</b> It means no global ordering exists
/// to wait on, so a consistent read cannot be requested against this write — not that the save failed.
/// </para>
/// </remarks>
public readonly record struct SaveResult(
	long CommittedVersion,
	long? SessionToken,
	ProjectionFreshness Projections)
{
	/// <summary>Gets the roster receipt for the inline freshness observation.</summary>
	/// <value>The receipt, or null when no roster was attested.</value>
	public ProjectionAttestation? Attestation { get; init; }
}
