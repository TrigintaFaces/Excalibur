// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents grant data as retrieved from SQL Server database. Used for Dapper query result mapping.
/// </summary>
internal sealed record GrantData
{
	/// <summary>
	/// Gets the user identifier.
	/// </summary>
	public required string UserId { get; init; }

	/// <summary>
	/// Gets the full name of the user.
	/// </summary>
	public required string FullName { get; init; }

	/// <summary>
	/// Gets the tenant identifier.
	/// </summary>
	public required string TenantId { get; init; }

	/// <summary>
	/// Gets the type of grant.
	/// </summary>
	public required string GrantType { get; init; }

	/// <summary>
	/// Gets the grant qualifier.
	/// </summary>
	public required string Qualifier { get; init; }

	/// <summary>
	/// Gets the expiration date and time of the grant, if applicable.
	/// </summary>
	public DateTimeOffset? ExpiresOn { get; init; }

	/// <summary>
	/// Gets the identifier of the user who granted this permission.
	/// </summary>
	public required string GrantedBy { get; init; }

	/// <summary>
	/// Gets the date and time when the grant was created.
	/// </summary>
	public DateTimeOffset? GrantedOn { get; init; }
}
