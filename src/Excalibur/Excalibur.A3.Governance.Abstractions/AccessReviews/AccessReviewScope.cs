// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Defines the scope of an access review campaign, specifying which grants to review.
/// </summary>
/// <remarks>
/// <para>
/// A scope combines a <see cref="AccessReviewScopeType"/> with an optional filter value,
/// enabling targeted reviews such as "all grants for role Admin" or "all grants for user jdoe".
/// </para>
/// </remarks>
/// <param name="Type">The type of scope filter.</param>
/// <param name="FilterValue">
/// The filter value for the scope (role name, user ID, or tenant ID).
/// Required for <see cref="AccessReviewScopeType.ByRole"/>,
/// <see cref="AccessReviewScopeType.ByUser"/>, and
/// <see cref="AccessReviewScopeType.ByTenant"/>.
/// </param>
public sealed record AccessReviewScope(
	AccessReviewScopeType Type,
	string? FilterValue);
