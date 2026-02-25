// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides scheduling and execution of automatic key rotation operations.
/// </summary>
/// <remarks>
/// <para>
/// Automatic key rotation is required for compliance with SOC 2
/// and GDPR standards. This interface supports:
/// - Policy-based rotation (e.g., 90-day default)
/// - Per-key-purpose rotation schedules
/// - Zero-downtime rotation with versioning
/// </para>
/// </remarks>
public interface IKeyRotationScheduler
{
	/// <summary>
	/// Checks all managed keys and rotates any that are due according to their policies.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A result summarizing the rotation operations performed.</returns>
	Task<KeyRotationBatchResult> CheckAndRotateAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a specific key is due for rotation according to its policy.
	/// </summary>
	/// <param name="keyId">The key identifier to check.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>True if the key should be rotated; otherwise false.</returns>
	Task<bool> IsRotationDueAsync(string keyId, CancellationToken cancellationToken);

	/// <summary>
	/// Forces immediate rotation of a specific key regardless of its schedule.
	/// </summary>
	/// <param name="keyId">The key identifier to rotate.</param>
	/// <param name="reason">The reason for the forced rotation (for audit purposes).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The result of the rotation operation.</returns>
	Task<KeyRotationResult> ForceRotateAsync(
		string keyId,
		string reason,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the next scheduled rotation time for a specific key.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The next rotation time, or null if the key is not found or has no rotation schedule.</returns>
	Task<DateTimeOffset?> GetNextRotationTimeAsync(
		string keyId,
		CancellationToken cancellationToken);
}
