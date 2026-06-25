// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Provider-neutral authorization grant assigned to a subject for a specific qualifier.
/// </summary>
/// <param name="UserId">The user/subject identifier.</param>
/// <param name="FullName">Optional display name.</param>
/// <param name="TenantId">Optional tenant identifier.</param>
/// <param name="GrantType">The type of grant (e.g., role, activity-group).</param>
/// <param name="Qualifier">The qualifier or scope for the grant.</param>
/// <param name="ExpiresOn">Optional expiration timestamp (UTC).</param>
/// <param name="GrantedBy">Identifier that issued the grant.</param>
/// <param name="GrantedOn">Timestamp when the grant was issued (UTC).</param>
public sealed record Grant(
	string UserId,
	string? FullName,
	string? TenantId,
	string GrantType,
	string Qualifier,
	DateTimeOffset? ExpiresOn,
	string GrantedBy,
	DateTimeOffset GrantedOn)
{
	/// <summary>
	/// Determines whether this grant has expired as of the supplied instant.
	/// </summary>
	/// <param name="asOf">The instant to evaluate expiry against (typically the current UTC time).</param>
	/// <returns>
	/// <see langword="true"/> if the grant has an <see cref="ExpiresOn"/> value at or before
	/// <paramref name="asOf"/>; otherwise <see langword="false"/>. A <see langword="null"/>
	/// <see cref="ExpiresOn"/> never expires.
	/// </returns>
	/// <remarks>
	/// The boundary is inclusive (<c>ExpiresOn &lt;= asOf</c> is expired), matching the
	/// SQL store precedent (<c>ExpiresOn &gt; GETUTCDATE()</c> is active). The predicate is
	/// pure and clock-free — callers pass the instant so behavior is deterministic and testable.
	/// </remarks>
	public bool IsExpired(DateTimeOffset asOf) => ExpiresOn is not null && ExpiresOn.Value <= asOf;

	/// <summary>
	/// Determines whether this grant is active (not expired) as of the supplied instant.
	/// </summary>
	/// <param name="asOf">The instant to evaluate against (typically the current UTC time).</param>
	/// <returns><see langword="true"/> if the grant is not expired as of <paramref name="asOf"/>.</returns>
	public bool IsActive(DateTimeOffset asOf) => !IsExpired(asOf);
}
