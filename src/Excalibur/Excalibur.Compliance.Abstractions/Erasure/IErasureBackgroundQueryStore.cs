// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>Estate-wide discovery for trusted erasure background workers.</summary>
/// <remarks>
/// Exposed through <see cref="IErasureStore.GetService"/> as a separate capability. These queries
/// intentionally ignore ambient tenancy; callers must authorize trusted-worker access themselves.
/// This interface is not an authorization boundary. Ordinary consumer listing remains tenant scoped.
/// Custom stores supporting background completion must expose this capability; absence is refused
/// rather than falling back to consumer listing.
/// </remarks>
public interface IErasureBackgroundQueryStore
{
	/// <summary>Discovers requests awaiting provider-confirmed key destruction across all tenants.</summary>
	/// <remarks>
	/// Returns full stored ownership, including legacy null ownership for the untenanted partition.
	/// Results use ascending requested time and a unique request-ID tie-breaker, stable within each
	/// provider for an unchanged set. Paging is not a snapshot or a claim; workers revalidate each row
	/// under its recorded tenant, and subsequent polls revisit survivors of concurrent changes.
	/// </remarks>
	/// <param name="pageNumber">The 1-based page number, at least one.</param>
	/// <param name="pageSize">The page size, from one through 1000.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Only requests in <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/>.</returns>
	Task<IReadOnlyList<ErasureStatus>> GetAwaitingKeyDestructionRequestsAsync(
		int pageNumber, int pageSize, CancellationToken cancellationToken);
}
