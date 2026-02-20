// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// </remarks>
public interface IFencingTokenProvider
{
	/// <summary>
	/// Gets the current fencing token for the active leader.
	/// </summary>
	/// <param name="resourceId">The identifier of the protected resource.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>
	/// The current fencing token value, or <see langword="null"/> if no active leader exists.
	/// </returns>
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
	ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken);

	/// <summary>
	/// Issues a new fencing token, incrementing the sequence for the resource.
	/// </summary>
	/// <param name="resourceId">The identifier of the protected resource.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>The newly issued fencing token value.</returns>
	ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken);
}
