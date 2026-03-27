// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Store for provisioning request persistence.
/// </summary>
/// <remarks>
/// Follows the same pattern as <c>IAccessReviewStore</c> and <c>ISoDPolicyStore</c>:
/// minimal CRUD (4 methods) + <see cref="GetService"/> for ISP extensions.
/// </remarks>
public interface IProvisioningStore
{
	/// <summary>
	/// Retrieves a provisioning request by ID.
	/// </summary>
	/// <param name="requestId">The request identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The request summary, or <see langword="null"/> if not found.</returns>
	Task<ProvisioningRequestSummary?> GetRequestAsync(string requestId, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves provisioning requests, optionally filtered by status.
	/// </summary>
	/// <param name="status">Optional status filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Matching provisioning requests.</returns>
	Task<IReadOnlyList<ProvisioningRequestSummary>> GetRequestsByStatusAsync(
		ProvisioningRequestStatus? status, CancellationToken cancellationToken);

	/// <summary>
	/// Saves or updates a provisioning request (upsert).
	/// </summary>
	/// <param name="request">The request to persist.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SaveRequestAsync(ProvisioningRequestSummary request, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a provisioning request.
	/// </summary>
	/// <param name="requestId">The request identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the request was found and deleted.</returns>
	Task<bool> DeleteRequestAsync(string requestId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this store.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
