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
/// registered <c>Inline</c>, so none was ever going to be. <see cref="ProjectionFreshness.Fresh"/> when
/// every <c>Inline</c> projection folded the committed prefix through the token, which is the read-your-
/// writes promise. <see cref="ProjectionFreshness.BoundExpired"/> when the bounded wait ended first:
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
/// <b>Projections are not WRITTEN by the save.</b> A read model is a replica, and replicas are updated by
/// replication rather than by the writer dual-writing into them — the same reason Cosmos, readable SQL
/// secondaries and search indexers all converge on this shape. The save never folds a projection itself.
/// </para>
/// <para>
/// <b>It may, however, WAIT for one, and <see cref="Projections"/> reports whether that wait succeeded.</b>
/// A projection registered <c>Async</c> is replicated behind the save and a caller needing
/// read-your-writes passes <see cref="SessionToken"/> to the read, paying for consistency only where it
/// asks for it. A projection registered <c>Inline</c> inverts that: the save drives the same ordered
/// processor and waits for it, so read-your-writes needs no token at the read — but the wait is bounded,
/// and when the bound expires the events are still committed while the read model is behind. That is what
/// <see cref="Projections"/> says, and it is why the save reports it instead of throwing.
/// </para>
/// <para>
/// <b><see langword="null"/> is a real answer, not a missing one.</b> It means no global ordering exists
/// to wait on, so a consistent read cannot be requested against this write — not that the save failed.
/// </para>
/// </remarks>
public readonly record struct SaveResult(
	long CommittedVersion,
	long? SessionToken,
	ProjectionFreshness Projections);
