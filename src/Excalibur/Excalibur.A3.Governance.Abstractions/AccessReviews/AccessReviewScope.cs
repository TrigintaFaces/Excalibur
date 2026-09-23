// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Defines the scope of an access review campaign, specifying which grants to review.
/// </summary>
/// <remarks>
/// <para>
/// A scope combines a <see cref="AccessReviewScopeType"/> with an optional filter value,
/// enabling targeted reviews such as "all grants for role Admin" or "all grants for user jdoe".
/// A scope never names a tenant: the tenant is the campaign's own
/// <see cref="AccessReviewCampaignSummary.TenantId"/>, so every grant a campaign reviews or revokes
/// belongs to that one tenant.
/// </para>
/// </remarks>
/// <param name="Type">The type of scope filter.</param>
/// <param name="FilterValue">
/// The filter value for the scope: the role name for <see cref="AccessReviewScopeType.ByRole"/>, the
/// user ID for <see cref="AccessReviewScopeType.ByUser"/>. Required for both; a campaign whose scope
/// lacks it revokes nothing rather than widening to every grant in the tenant.
/// Ignored for <see cref="AccessReviewScopeType.AllGrants"/>.
/// </param>
public sealed record AccessReviewScope(
	AccessReviewScopeType Type,
	string? FilterValue);
