// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Provider-neutral store for authorization grant persistence.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IUserStore&lt;TUser&gt;</c> pattern:
/// minimal CRUD surface (5 methods) with <see cref="GetService"/> for ISP extensions.
/// </para>
/// <para>
/// Replaces both the internal <c>IGrantRequestProvider</c> (11-method, SQL-coupled) and the
/// abstractions-level <c>IGrantRequestProvider</c> (5 methods + GetService).
/// </para>
/// </remarks>
public interface IGrantStore
{
	/// <summary>
	/// Retrieves a specific grant.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier/scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The grant, or <see langword="null"/> if not found.</returns>
	Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all grants for a user.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>All grants for the specified user.</returns>
	Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken);

	/// <summary>
	/// Saves or updates a grant (upsert).
	/// </summary>
	/// <param name="grant">The grant to persist.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a grant with optional revocation metadata.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier/scope.</param>
	/// <param name="revokedBy">Optional actor identifier who revoked the grant.</param>
	/// <param name="revokedOn">Optional revocation timestamp (UTC).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether a specific grant exists.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier/scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the grant exists; otherwise <see langword="false"/>.</returns>
	Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this store.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g., <c>typeof(IGrantQueryStore)</c>).</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
