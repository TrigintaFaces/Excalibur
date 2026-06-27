// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;
using System.Security.Claims;

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Focused value type grouping the security and tenancy metadata for a message.
/// </summary>
/// <remarks>
/// Composed onto <see cref="MessageMetadata"/>. Carries the user, role, claim and tenant fields.
/// Holds at most ten properties to satisfy the Microsoft-first focused-value-type design guideline.
/// </remarks>
public readonly record struct MessageSecurity
{
	private static readonly IReadOnlyCollection<string> EmptyRoles = new ReadOnlyCollection<string>([]);
	private static readonly IReadOnlyCollection<Claim> EmptyClaims = new ReadOnlyCollection<Claim>([]);

	/// <summary>
	/// Gets the identifier of the user associated with the message.
	/// </summary>
	/// <value> The user identifier or <see langword="null"/>. </value>
	public string? UserId { get; init; }

	/// <summary>
	/// Gets the collection of user roles associated with the message.
	/// </summary>
	/// <value> The user roles. Never <see langword="null"/>; defaults to an empty collection. </value>
	public IReadOnlyCollection<string> Roles { get; init; } = EmptyRoles;

	/// <summary>
	/// Gets the collection of security claims associated with the message.
	/// </summary>
	/// <value> The security claims. Never <see langword="null"/>; defaults to an empty collection. </value>
	public IReadOnlyCollection<Claim> Claims { get; init; } = EmptyClaims;

	/// <summary>
	/// Gets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <value> The tenant identifier or <see langword="null"/>. </value>
	public string? TenantId { get; init; }

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageSecurity"/> struct with empty role and claim collections.
	/// </summary>
	public MessageSecurity()
	{
	}
}
