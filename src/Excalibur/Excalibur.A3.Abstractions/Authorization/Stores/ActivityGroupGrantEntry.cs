// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// One activity-group grant: a single row of an activity-group grant snapshot.
/// </summary>
/// <param name="UserId">The user the grant is held by, as a bare value.</param>
/// <param name="FullName">The user's display name. May be empty when the authority sends none.</param>
/// <param name="TenantId">The tenant the grant applies within, as a bare value.</param>
/// <param name="GrantType">The grant type this row carries.</param>
/// <param name="Qualifier">What is granted — for an activity-group grant, the group's name.</param>
/// <param name="ExpiresOn">When the grant stops conferring anything, or <see langword="null"/> to never expire.</param>
/// <param name="GrantedBy">The actor the grant is recorded against.</param>
/// <remarks>
/// An entry carries no validation of its own, because <see langword="default"/> can always construct one.
/// It is validated when it is placed into an <see cref="ActivityGroupGrantSnapshot"/>, which is the only form
/// a store accepts for a grant replace.
/// </remarks>
public readonly record struct ActivityGroupGrantEntry(
	string UserId,
	string FullName,
	string TenantId,
	string GrantType,
	string Qualifier,
	DateTimeOffset? ExpiresOn,
	string GrantedBy);
