// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Defines methods for managing activity groups and synchronizing their associated data.
/// </summary>
public interface IActivityGroupService
{
	/// <summary>
	/// Checks whether <paramref name="tenantId" /> has an activity group with the specified name.
	/// </summary>
	/// <remarks>
	/// The tenant term is part of the question rather than a filter on it: a group name is unique only
	/// within a tenant, so a bare-name check answers estate-wide and reports another tenant's group as
	/// this tenant's. This member is a pass-through, and a pass-through that drops the tenant would leave
	/// the hole one layer above the store that closed it.
	/// </remarks>
	/// <param name="tenantId"> The tenant identifier. Required; must be non-empty. </param>
	/// <param name="activityGroupName"> The name of the activity group to check for existence. </param>
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns>
	/// A <see cref="Task{TResult}" /> that resolves to <c> true </c> if <paramref name="tenantId" /> has a
	/// group so named; otherwise, <c> false </c>.
	/// </returns>
	Task<bool> ExistsAsync(string tenantId, string activityGroupName, CancellationToken cancellationToken);

	/// <summary>
	/// Synchronizes all activity groups by updating or creating them based on the current state of the source data.
	/// </summary>
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	Task SyncActivityGroupsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Synchronizes the grants (permissions or roles) of the activity groups for a specific user.
	/// </summary>
	/// <param name="userId"> The unique identifier of the user whose grants will be synchronized. </param>
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	Task SyncActivityGroupGrantsAsync(string userId, CancellationToken cancellationToken);

	/// <summary>
	/// Synchronizes the grants (permissions or roles) for all users across all activity groups.
	/// </summary>
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	Task SyncAllActivityGroupGrantsAsync(CancellationToken cancellationToken);
}
