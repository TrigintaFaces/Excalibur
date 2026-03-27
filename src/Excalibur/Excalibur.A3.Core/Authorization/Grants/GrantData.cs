// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents data for a grant as retrieved from the database.
/// </summary>
public sealed record GrantData
{
	/// <summary>
	/// Gets or initializes the user ID associated with the grant.
	/// </summary>
	/// <value>The user ID.</value>
	public required string UserId { get; init; }

	/// <summary>
	/// Gets or initializes the full name of the user or entity associated with the grant.
	/// </summary>
	/// <value>The full name of the user.</value>
	public required string FullName { get; init; }

	/// <summary>
	/// Gets or initializes the tenant ID associated with the grant.
	/// </summary>
	/// <value>The tenant ID.</value>
	public required string TenantId { get; init; }

	/// <summary>
	/// Gets or initializes the type of the grant.
	/// </summary>
	/// <value>The type of the grant.</value>
	public required string GrantType { get; init; }

	/// <summary>
	/// Gets or initializes the qualifier of the grant.
	/// </summary>
	/// <value>The qualifier of the grant.</value>
	public required string Qualifier { get; init; }

	/// <summary>
	/// Gets or initializes the expiration date of the grant, if applicable.
	/// </summary>
	/// <value>The expiration date, or <see langword="null"/> if the grant does not expire.</value>
	public DateTimeOffset? ExpiresOn { get; init; }

	/// <summary>
	/// Gets or initializes the name of the entity granting the permission.
	/// </summary>
	/// <value>The name of the entity granting the permission.</value>
	public required string GrantedBy { get; init; }

	/// <summary>
	/// Gets or initializes the date and time the grant was issued.
	/// </summary>
	/// <value>The date and time the grant was issued, or <see langword="null"/> if not available.</value>
	public DateTimeOffset? GrantedOn { get; init; }
}
