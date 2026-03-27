// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Represents a reviewer's decision on a specific grant within an access review campaign.
/// </summary>
/// <remarks>
/// <para>
/// Captures who reviewed what, when, and why. The <see cref="DelegateToReviewerId"/> is only
/// populated when <see cref="Outcome"/> is <see cref="AccessReviewOutcome.Delegated"/>.
/// </para>
/// </remarks>
/// <param name="CampaignId">The campaign this decision belongs to.</param>
/// <param name="GrantUserId">The user whose grant is being reviewed.</param>
/// <param name="GrantScope">The scope of the grant being reviewed.</param>
/// <param name="ReviewerId">The reviewer who made this decision.</param>
/// <param name="Outcome">The review outcome.</param>
/// <param name="Justification">The reviewer's justification for their decision.</param>
/// <param name="DecisionTimestamp">When the decision was made.</param>
/// <param name="DelegateToReviewerId">
/// The reviewer to delegate to, when <see cref="Outcome"/> is <see cref="AccessReviewOutcome.Delegated"/>.
/// </param>
public sealed record AccessReviewDecision(
	string CampaignId,
	string GrantUserId,
	string GrantScope,
	string ReviewerId,
	AccessReviewOutcome Outcome,
	string? Justification,
	DateTimeOffset DecisionTimestamp,
	string? DelegateToReviewerId);
