// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Provider-neutral contract for querying and mutating authorization grants.
/// </summary>
/// <remarks>
/// <para>
/// Core CRUD operations for grant management. For advanced query operations, use
/// <see cref="GetService"/> with <c>typeof(IGrantQueryProvider)</c>.
/// </para>
/// <para>
/// <strong>ISP Split (Sprint 551):</strong> GetMatchingGrantsAsync and FindUserGrantsAsync
/// moved to <see cref="IGrantQueryProvider"/> to keep the core interface at or below 5 methods.
/// </para>
/// </remarks>
public interface IGrantRequestProvider
{
	/// <summary>Deletes a specific grant.</summary>
	/// <param name="userId">Subject id.</param>
	/// <param name="tenantId">Tenant id.</param>
	/// <param name="grantType">Grant type.</param>
	/// <param name="qualifier">Qualifier/scope.</param>
	/// <param name="revokedBy">Optional actor id revoking the grant.</param>
	/// <param name="revokedOn">Optional revoke timestamp (UTC).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType, string qualifier, string? revokedBy,
		DateTimeOffset? revokedOn, CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether a specific grant exists.
	/// </summary>
	/// <param name="userId">Subject id.</param>
	/// <param name="tenantId">Tenant id.</param>
	/// <param name="grantType">Grant type.</param>
	/// <param name="qualifier">Qualifier/scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True when the grant exists; otherwise false.</returns>
	Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves a specific grant.
	/// </summary>
	/// <param name="userId">Subject id.</param>
	/// <param name="tenantId">Tenant id.</param>
	/// <param name="grantType">Grant type.</param>
	/// <param name="qualifier">Qualifier/scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The grant or null if not found.</returns>
	Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all grants for a user.
	/// </summary>
	/// <param name="userId">Subject id.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>All grants for the subject.</returns>
	Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken);

	/// <summary>
	/// Saves or updates a grant.
	/// </summary>
	/// <param name="grant">The grant to persist.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this provider.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g., <c>typeof(IGrantQueryProvider)</c>).</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
