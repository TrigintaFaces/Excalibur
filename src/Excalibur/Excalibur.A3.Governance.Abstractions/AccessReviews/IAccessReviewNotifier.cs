// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Sends notifications related to access review campaigns.
/// </summary>
/// <remarks>
/// <para>
/// Consumers implement this interface to integrate with their notification infrastructure
/// (e.g., email, Slack, Teams). The framework registers a no-op default when no implementation
/// is provided.
/// </para>
/// </remarks>
public interface IAccessReviewNotifier
{
	/// <summary>
	/// Notifies stakeholders that a new access review campaign has been created.
	/// </summary>
	/// <param name="campaignId">The campaign identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task NotifyCampaignCreatedAsync(string campaignId, CancellationToken cancellationToken);

	/// <summary>
	/// Notifies reviewers that a campaign deadline has been extended due to expiry.
	/// </summary>
	/// <param name="campaignId">The campaign identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task NotifyCampaignExtendedAsync(string campaignId, CancellationToken cancellationToken);

	/// <summary>
	/// Notifies a reviewer that a decision is required for a specific access review item.
	/// </summary>
	/// <param name="campaignId">The campaign identifier.</param>
	/// <param name="reviewerId">The reviewer who needs to make a decision.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task NotifyDecisionRequiredAsync(string campaignId, string reviewerId, CancellationToken cancellationToken);
}
