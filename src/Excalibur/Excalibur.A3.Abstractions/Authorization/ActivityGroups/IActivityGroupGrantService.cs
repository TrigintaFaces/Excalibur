// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Provider-neutral operations for activity-group grants.
/// </summary>
public interface IActivityGroupGrantService
{
	/// <summary>
	/// Deletes all activity-group grants for a given user.
	/// </summary>
	/// <param name="userId">Subject id.</param>
	/// <param name="grantType">Grant type.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> DeleteActivityGroupGrantsByUserIdAsync(string userId, string grantType, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes all activity-group grants.
	/// </summary>
	/// <param name="grantType">Grant type.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> DeleteAllActivityGroupGrantsAsync(string grantType, CancellationToken cancellationToken);

	/// <summary>
	/// Inserts an activity-group grant.
	/// </summary>
	/// <param name="userId">Subject id.</param>
	/// <param name="fullName">Optional display name.</param>
	/// <param name="tenantId">Optional tenant id.</param>
	/// <param name="grantType">Grant type.</param>
	/// <param name="qualifier">Qualifier/scope.</param>
	/// <param name="expiresOn">Optional expiration timestamp (UTC).</param>
	/// <param name="grantedBy">Actor who grants.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> InsertActivityGroupGrantAsync(string userId, string fullName, string? tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken);

	/// <summary>
	/// Returns distinct user ids that have activity-group grants for the specified type.
	/// </summary>
	/// <param name="grantType">Grant type.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Distinct user ids.</returns>
	Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(string grantType, CancellationToken cancellationToken);
}
