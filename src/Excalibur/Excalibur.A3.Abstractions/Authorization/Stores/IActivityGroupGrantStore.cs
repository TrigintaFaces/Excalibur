// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Activity-group grant operations that span the grant store.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IUserRoleStore&lt;TUser&gt;</c> pattern:
/// a bridging ISP sub-interface for operations that span grant and activity-group stores.
/// </para>
/// <para>
/// Access via <see cref="IGrantStore.GetService(Type)"/> or
/// <see cref="IActivityGroupStore.GetService(Type)"/>.
/// </para>
/// <para>
/// Replaces <c>IActivityGroupGrantService</c> from <c>Excalibur.A3.Abstractions</c>.
/// </para>
/// </remarks>
public interface IActivityGroupGrantStore
{
	/// <summary>
	/// Deletes all activity-group grants for a user.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> DeleteActivityGroupGrantsByUserIdAsync(string userId, string grantType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes all activity-group grants.
	/// </summary>
	/// <param name="grantType">The grant type.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> DeleteAllActivityGroupGrantsAsync(string grantType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Inserts an activity-group grant.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="fullName">Optional display name.</param>
	/// <param name="tenantId">Optional tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier/scope.</param>
	/// <param name="expiresOn">Optional expiration timestamp (UTC).</param>
	/// <param name="grantedBy">The actor who grants.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> InsertActivityGroupGrantAsync(string userId, string fullName,
		string? tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken);

	/// <summary>
	/// Returns distinct user IDs with activity-group grants.
	/// </summary>
	/// <param name="grantType">The grant type.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Distinct user identifiers.</returns>
	Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
		string grantType, CancellationToken cancellationToken);
}
