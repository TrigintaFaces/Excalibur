// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Provider-neutral store for activity group persistence.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IRoleStore&lt;TRole&gt;</c> pattern:
/// minimal CRUD surface (4 methods) with <see cref="GetService"/> for ISP extensions.
/// </para>
/// <para>
/// Replaces <c>IActivityGroupRequestProvider</c> (4-method, SQL-coupled interface).
/// </para>
/// </remarks>
public interface IActivityGroupStore
{
	/// <summary>
	/// Checks whether an activity group exists.
	/// </summary>
	/// <param name="activityGroupName">The name of the activity group.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the activity group exists; otherwise <see langword="false"/>.</returns>
	Task<bool> ActivityGroupExistsAsync(string activityGroupName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all activity groups.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Provider-specific projection of activity groups.</returns>
	Task<IReadOnlyDictionary<string, object>> FindActivityGroupsAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes all activity groups.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> DeleteAllActivityGroupsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Creates a new activity group entry.
	/// </summary>
	/// <param name="tenantId">Optional tenant identifier.</param>
	/// <param name="name">The activity group name.</param>
	/// <param name="activityName">The activity name to associate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> CreateActivityGroupAsync(string? tenantId, string name,
		string activityName, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this store.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g., <c>typeof(IActivityGroupGrantStore)</c>).</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
