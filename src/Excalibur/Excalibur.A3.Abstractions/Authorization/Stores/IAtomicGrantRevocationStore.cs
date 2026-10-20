// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>Optional atomic revocation and authoritative revision capability of an <see cref="IGrantStore"/>.</summary>
/// <remarks>
/// Every grant writer, including activity-group writers, must advance the affected user's revision in the
/// same commit as its mutation. Revisions never decrease or reset during a store's lifetime. Reads are
/// linearizable; snapshots pair grants with their actual revision. Unsupported providers must not expose
/// this capability. No per-row or chunked deletion fallback satisfies this contract.
/// </remarks>
public interface IAtomicGrantRevocationStore
{
	/// <summary>Atomically removes all grant types in the explicit scope and commits the revision and retry ledger.</summary>
	/// <remarks>
	/// The same subject and operation identifier with an identical payload returns its original outcome,
	/// preserving grants added after that commit. Conflicting payloads are rejected before mutation.
	/// Failure before commit leaves state unchanged; cancellation or transport failure after commit may
	/// have an unknown outcome. Retry with the same identifier to resolve that outcome.
	/// </remarks>
	/// <param name="request">Stable, explicitly scoped operation.</param>
	/// <param name="cancellationToken">Cancellation before commit.</param>
	/// <returns>The committed result.</returns>
	Task<GrantRevocationResult> RevokeAllAsync(GrantRevocationRequest request, CancellationToken cancellationToken);
	/// <summary>Reads the authoritative user revision.</summary>
	/// <param name="userId">Exact subject identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The authoritative user revision.</returns>
	Task<long> ReadUserRevisionAsync(string userId, CancellationToken cancellationToken);
	/// <summary>Reads grants and their revision from one committed state.</summary>
	/// <param name="userId">Exact subject identifier.</param>
	/// <param name="includeExpired">Whether to include expired grants.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The coherent snapshot.</returns>
	Task<UserGrantSnapshot> ReadUserSnapshotAsync(string userId, bool includeExpired, CancellationToken cancellationToken);
}
