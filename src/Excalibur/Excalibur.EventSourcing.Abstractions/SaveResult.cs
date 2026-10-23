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
/// <remarks>
/// <para>
/// <b>The token rides the response of the operation that produced it</b>, which is how Azure Cosmos DB's
/// session consistency works: a write returns a session token, the caller carries it, and a subsequent
/// read honours it. The alternatives were considered and rejected — an out-parameter puts transport
/// state on the aggregate, and an ambient value makes "which save produced this" unanswerable the moment
/// two saves interleave.
/// </para>
/// <para>
/// <b>Projections are not written by the save.</b> A read model is a replica, and replicas are updated by
/// replication rather than by the writer dual-writing into them — the same reason Cosmos, readable SQL
/// secondaries and search indexers all converge on this shape. So the save does not wait for any
/// projection; a caller that needs read-your-writes passes this token to the read, and only that caller
/// pays for it.
/// </para>
/// <para>
/// <b><see langword="null"/> is a real answer, not a missing one.</b> It means no global ordering exists
/// to wait on, so a consistent read cannot be requested against this write — not that the save failed.
/// </para>
/// </remarks>
public readonly record struct SaveResult(long CommittedVersion, long? SessionToken);
