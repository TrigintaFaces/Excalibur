// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Authorization.Events;

/// <summary>
/// Represents a domain event that occurs when a grant is added to a user.
/// </summary>
public interface IGrantAdded : IDomainEvent
{
	/// <summary>
	/// Gets the name of the application associated with the grant.
	/// </summary>
	/// <value>The name of the application.</value>
	string ApplicationName { get; init; }

	/// <summary>
	/// Gets the expiration date of the grant, if applicable.
	/// </summary>
	/// <value>The expiration date, or <see langword="null"/> if the grant does not expire.</value>
	DateTimeOffset? ExpiresOn { get; init; }

	/// <summary>
	/// Gets the full name of the user to whom the grant was added.
	/// </summary>
	/// <value>The full name of the user.</value>
	string FullName { get; init; }

	/// <summary>
	/// Gets the identifier of the entity or user that granted the access.
	/// </summary>
	/// <value>The identifier of the entity or user.</value>
	string GrantedBy { get; init; }

	/// <summary>
	/// Gets the date and time when the grant was added.
	/// </summary>
	/// <value>The date and time when the grant was added.</value>
	DateTimeOffset GrantedOn { get; init; }

	/// <summary>
	/// Gets the type of the grant that was added.
	/// </summary>
	/// <value>The type of the grant.</value>
	string GrantType { get; init; }

	/// <summary>
	/// Gets any additional qualifier for the grant.
	/// </summary>
	/// <value>The qualifier for the grant.</value>
	string Qualifier { get; init; }

	/// <summary>
	/// Gets the tenant ID associated with the grant.
	/// </summary>
	/// <value>The tenant ID.</value>
	string TenantId { get; init; }

	/// <summary>
	/// Gets the ID of the user to whom the grant was added.
	/// </summary>
	/// <value>The ID of the user.</value>
	string UserId { get; init; }
}
