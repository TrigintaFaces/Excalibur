// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Authorization.Events;

/// <summary>
/// Represents a domain event that occurs when a grant is revoked from a user.
/// </summary>
public interface IGrantRevoked : IDomainEvent
{
	/// <summary>
	/// Gets the name of the application associated with the grant.
	/// </summary>
	/// <value>The name of the application.</value>
	string ApplicationName { get; init; }

	/// <summary>
	/// Gets the expiration date of the revoked grant, if applicable.
	/// </summary>
	/// <value>The expiration date, or <see langword="null"/> if the grant did not have an expiration.</value>
	DateTimeOffset? ExpiresOn { get; init; }

	/// <summary>
	/// Gets the full name of the user from whom the grant was revoked.
	/// </summary>
	/// <value>The full name of the user.</value>
	string FullName { get; init; }

	/// <summary>
	/// Gets the type of the grant that was revoked.
	/// </summary>
	/// <value>The type of the grant.</value>
	string GrantType { get; init; }

	/// <summary>
	/// Gets any additional qualifier for the grant.
	/// </summary>
	/// <value>The qualifier for the grant.</value>
	string Qualifier { get; init; }

	/// <summary>
	/// Gets the identifier of the entity or user who revoked the grant.
	/// </summary>
	/// <value>The identifier of the entity or user.</value>
	string RevokedBy { get; init; }

	/// <summary>
	/// Gets the date and time when the grant was revoked.
	/// </summary>
	/// <value>The date and time when the grant was revoked.</value>
	DateTimeOffset RevokedOn { get; init; }

	/// <summary>
	/// Gets the tenant ID associated with the revoked grant.
	/// </summary>
	/// <value>The tenant ID.</value>
	string TenantId { get; init; }

	/// <summary>
	/// Gets the ID of the user from whom the grant was revoked.
	/// </summary>
	/// <value>The ID of the user.</value>
	string UserId { get; init; }
}
