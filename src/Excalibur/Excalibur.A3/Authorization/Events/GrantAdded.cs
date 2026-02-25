// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Events;

namespace Excalibur.A3.Authorization.Events;

/// <summary>
/// Represents an event that occurs when a grant is added to a user.
/// </summary>
public sealed class GrantAdded : DomainEventBase, IGrantAdded
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GrantAdded" /> class with the specified details.
	/// </summary>
	/// <param name="userId"> The ID of the user to whom the grant was added. </param>
	/// <param name="fullName"> The full name of the user. </param>
	/// <param name="applicationName"> The name of the application associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> Additional qualifiers for the grant. </param>
	/// <param name="expiresOn"> The expiration date of the grant, if applicable. </param>
	/// <param name="grantedBy"> The identifier of the entity or user that granted the access. </param>
	/// <param name="grantedOn"> The date and time the grant was issued. </param>
	[SetsRequiredMembers]
	public GrantAdded(
		string userId,
		string fullName,
		string applicationName,
		string tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		DateTimeOffset grantedOn)
	{
		UserId = userId;
		FullName = fullName;
		ApplicationName = applicationName;
		TenantId = tenantId;
		GrantType = grantType;
		Qualifier = qualifier;
		ExpiresOn = expiresOn;
		GrantedBy = grantedBy;
		GrantedOn = grantedOn;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GrantAdded" /> class for deserialization or manual population.
	/// </summary>
	public GrantAdded()
	{
	}

	/// <inheritdoc />
	public required string ApplicationName { get; init; }

	/// <inheritdoc />
	public DateTimeOffset? ExpiresOn { get; init; }

	/// <inheritdoc />
	public required string FullName { get; init; }

	/// <inheritdoc />
	public required string GrantedBy { get; init; }

	/// <inheritdoc />
	public DateTimeOffset GrantedOn { get; init; }

	/// <inheritdoc />
	public required string GrantType { get; init; }

	/// <inheritdoc />
	public required string Qualifier { get; init; }

	/// <inheritdoc />
	public required string TenantId { get; init; }

	/// <inheritdoc />
	public required string UserId { get; init; }
}
