// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Provider-neutral store for role persistence.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IRoleStore&lt;TRole&gt;</c> pattern:
/// minimal CRUD surface (5 methods) with <see cref="GetService"/> for ISP extensions.
/// </para>
/// </remarks>
public interface IRoleStore
{
	/// <summary>
	/// Retrieves a role by its identifier.
	/// </summary>
	/// <param name="roleId">The role identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The role, or <see langword="null"/> if not found.</returns>
	Task<RoleSummary?> GetRoleAsync(string roleId, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all roles for a tenant, or all roles if tenant is null.
	/// </summary>
	/// <param name="tenantId">Optional tenant filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Matching roles.</returns>
	Task<IReadOnlyList<RoleSummary>> GetRolesAsync(string? tenantId, CancellationToken cancellationToken);

	/// <summary>
	/// Saves or updates a role (upsert).
	/// </summary>
	/// <param name="role">The role to persist.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SaveRoleAsync(RoleSummary role, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a role by its identifier.
	/// </summary>
	/// <param name="roleId">The role identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the role was deleted; <see langword="false"/> if not found.</returns>
	Task<bool> DeleteRoleAsync(string roleId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this store.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
