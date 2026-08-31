// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Sends notifications related to access review campaigns.
/// </summary>
/// <remarks>
/// <para>
/// Consumers implement this interface to integrate with their notification infrastructure
/// (e.g., email, Slack, Teams). There is no default implementation: the
/// <see cref="AccessReviewExpiryPolicy.NotifyAndExtend" /> policy requires a registered notifier,
/// and a campaign configured with that policy is left untouched — and the failure logged — when
/// none is registered. An access review is an audit surface, so the framework will not record a
/// notification it did not send.
/// </para>
/// </remarks>
public interface IAccessReviewNotifier
{
	/// <summary>
	/// Notifies reviewers that a campaign deadline has been extended because the campaign expired
	/// with items still unreviewed.
	/// </summary>
	/// <param name="campaignId">The campaign identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes when reviewers have been notified.</returns>
	/// <remarks>
	/// Called before the deadline is extended. If this throws, the extension does not happen and the
	/// campaign is left at its existing deadline for the next expiry sweep to retry.
	/// </remarks>
	Task NotifyCampaignExtendedAsync(string campaignId, CancellationToken cancellationToken);
}
