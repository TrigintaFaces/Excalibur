// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Read-model summary of an access review campaign for query and reporting purposes.
/// </summary>
/// <remarks>
/// <para>
/// Projected from <c>AccessReviewCampaign</c> aggregate events. Used by
/// <c>IAccessReviewStore</c> for dashboards, reports, and reviewer UIs.
/// </para>
/// </remarks>
/// <param name="CampaignId">Unique campaign identifier.</param>
/// <param name="TenantId">
/// The tenant whose grants the campaign reviews. Every grant it reviews and every grant it revokes belongs
/// to this tenant; there is no estate-wide campaign. A single-tenant host uses the untenanted sentinel.
/// </param>
/// <param name="CampaignName">Display name of the campaign.</param>
/// <param name="Scope">The scope of grants being reviewed.</param>
/// <param name="CreatedBy">The actor who created the campaign.</param>
/// <param name="StartsAt">When the campaign starts or started.</param>
/// <param name="ExpiresAt">When the campaign expires or expired.</param>
/// <param name="ExpiryPolicy">The policy to apply on expiry.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="TotalItems">Total number of grants to review.</param>
/// <param name="DecidedItems">Number of grants that have been reviewed.</param>
public sealed record AccessReviewCampaignSummary(
	string CampaignId,
	string TenantId,
	string CampaignName,
	AccessReviewScope Scope,
	string CreatedBy,
	DateTimeOffset StartsAt,
	DateTimeOffset ExpiresAt,
	AccessReviewExpiryPolicy ExpiryPolicy,
	AccessReviewState State,
	int TotalItems,
	int DecidedItems);
