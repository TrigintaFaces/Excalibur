// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Represents a single grant item within an access review campaign.
/// </summary>
/// <remarks>
/// <para>
/// Each item corresponds to a grant that needs to be reviewed. The <see cref="CurrentDecision"/>
/// is <see langword="null"/> until a reviewer makes a decision.
/// </para>
/// </remarks>
/// <param name="GrantUserId">The user who holds the grant.</param>
/// <param name="GrantScope">The scope of the grant (e.g., "{TenantId}:Role:{RoleName}").</param>
/// <param name="GrantedOn">When the grant was originally issued.</param>
/// <param name="CurrentDecision">The review decision, or <see langword="null"/> if not yet reviewed.</param>
public sealed record AccessReviewItem(
	string GrantUserId,
	string GrantScope,
	DateTimeOffset GrantedOn,
	AccessReviewOutcome? CurrentDecision);
