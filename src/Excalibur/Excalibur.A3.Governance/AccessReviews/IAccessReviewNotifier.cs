// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.AccessReviews;

namespace Excalibur.A3.Governance;

/// <summary>
/// No-op notifier for when no notification provider is configured.
/// </summary>
internal sealed class NullAccessReviewNotifier : IAccessReviewNotifier
{
	/// <summary>
	/// Shared singleton instance.
	/// </summary>
	internal static NullAccessReviewNotifier Instance { get; } = new();

	/// <inheritdoc />
	public Task NotifyCampaignCreatedAsync(string campaignId, CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task NotifyCampaignExtendedAsync(string campaignId, CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task NotifyDecisionRequiredAsync(string campaignId, string reviewerId, CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
