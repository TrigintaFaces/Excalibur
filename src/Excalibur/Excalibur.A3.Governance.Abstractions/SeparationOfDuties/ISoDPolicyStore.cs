// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.SeparationOfDuties;

/// <summary>
/// Provider-neutral store for Separation of Duties policy persistence.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IRoleStore&lt;TRole&gt;</c> pattern:
/// minimal CRUD surface (4 methods) with <see cref="GetService"/> for ISP extensions.
/// </para>
/// </remarks>
public interface ISoDPolicyStore
{
	/// <summary>
	/// Retrieves a policy by its identifier.
	/// </summary>
	/// <param name="policyId">The policy identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The policy, or <see langword="null"/> if not found.</returns>
	Task<SoDPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all SoD policies.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>All stored policies.</returns>
	Task<IReadOnlyList<SoDPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Saves or updates a policy (upsert).
	/// </summary>
	/// <param name="policy">The policy to persist.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SavePolicyAsync(SoDPolicy policy, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a policy by its identifier.
	/// </summary>
	/// <param name="policyId">The policy identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the policy was deleted; <see langword="false"/> if not found.</returns>
	Task<bool> DeletePolicyAsync(string policyId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this store.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
