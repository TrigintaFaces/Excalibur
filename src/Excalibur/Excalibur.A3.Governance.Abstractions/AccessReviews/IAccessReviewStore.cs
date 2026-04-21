// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Read-model query store for access review campaigns and decisions.
/// </summary>
/// <remarks>
/// <para>
/// This is NOT the aggregate's persistence store. The <c>AccessReviewCampaign</c> aggregate
/// is event-sourced via <c>IEventSourcedRepository&lt;AccessReviewCampaign&gt;</c>.
/// This store provides query access for dashboards, reports, and reviewer UIs.
/// </para>
/// <para>
/// Follows the <see cref="Excalibur.A3.Abstractions.Authorization.IRoleStore"/> pattern:
/// a focused query interface backed by <see cref="AccessReviewCampaignSummary"/> DTOs.
/// </para>
/// </remarks>
public interface IAccessReviewStore
{
	/// <summary>
	/// Retrieves a campaign summary by its identifier.
	/// </summary>
	/// <param name="campaignId">The campaign identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The campaign summary, or <see langword="null"/> if not found.</returns>
	Task<AccessReviewCampaignSummary?> GetCampaignAsync(string campaignId, CancellationToken cancellationToken);

	/// <summary>
	/// Saves or updates a campaign summary (upsert).
	/// </summary>
	/// <param name="campaign">The campaign summary to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SaveCampaignAsync(AccessReviewCampaignSummary campaign, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all campaigns matching the specified state filter.
	/// </summary>
	/// <param name="state">The state to filter by, or <see langword="null"/> for all campaigns.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Campaigns matching the filter.</returns>
	Task<IReadOnlyList<AccessReviewCampaignSummary>> GetCampaignsByStateAsync(AccessReviewState? state, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a campaign summary.
	/// </summary>
	/// <param name="campaignId">The campaign identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the campaign was found and deleted; otherwise <see langword="false"/>.</returns>
	Task<bool> DeleteCampaignAsync(string campaignId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a service of the specified type, or <see langword="null"/> if not available.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/>.</returns>
	object? GetService(Type serviceType);
}
