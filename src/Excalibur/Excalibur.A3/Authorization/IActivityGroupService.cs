// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Defines methods for managing activity groups and synchronizing their associated data.
/// </summary>
public interface IActivityGroupService
{
	/// <summary>
	/// Checks whether an activity group with the specified name exists.
	/// </summary>
	/// <param name="activityGroupName"> The name of the activity group to check for existence. </param>
	/// <returns>
	/// A <see cref="Task{TResult}" /> that resolves to <c> true </c> if the activity group exists; otherwise, <c> false </c>.
	/// </returns>
	Task<bool> ExistsAsync(string activityGroupName);

	/// <summary>
	/// Synchronizes all activity groups by updating or creating them based on the current state of the source data.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON deserialization may require unreferenced types for reflection-based operations")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON deserialization uses reflection to dynamically create and populate types")]
	Task SyncActivityGroupsAsync();

	/// <summary>
	/// Synchronizes the grants (permissions or roles) of the activity groups for a specific user.
	/// </summary>
	/// <param name="userId"> The unique identifier of the user whose grants will be synchronized. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON deserialization may require unreferenced types for reflection-based operations")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON deserialization uses reflection to dynamically create and populate types")]
	Task SyncActivityGroupGrantsAsync(string userId);

	/// <summary>
	/// Synchronizes the grants (permissions or roles) for all users across all activity groups.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON deserialization may require unreferenced types for reflection-based operations")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON deserialization uses reflection to dynamically create and populate types")]
	Task SyncAllActivityGroupGrantsAsync();
}
