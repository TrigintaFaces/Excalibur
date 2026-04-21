// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;

using GrantAggregate = Excalibur.A3.Authorization.Grants.Grant;
using GrantKey = Excalibur.A3.Authorization.Grants.GrantKey;

namespace Excalibur.A3.Authorization.Roles.Commands;

/// <summary>
/// Service for creating and revoking role-based grant aggregates.
/// </summary>
/// <remarks>
/// <para>
/// Translates role assignment commands into grant aggregate operations.
/// One role assignment = one grant with <c>GrantType = "Role"</c>.
/// The authorization evaluator resolves effective permissions by looking up the
/// role's activity groups at evaluation time.
/// </para>
/// <para>
/// This service creates grant aggregates but does not persist them.
/// The caller (typically a handler in the A3 package) is responsible for
/// persisting via <c>IEventSourcedRepository&lt;Grant&gt;</c>.
/// </para>
/// </remarks>
internal sealed class RoleAssignmentService(IRoleStore roleStore)
{
	/// <summary>
	/// The grant type used for role assignments.
	/// </summary>
	internal const string RoleGrantType = "Role";

	/// <summary>
	/// Creates a grant aggregate for a role assignment after validating the role exists and is active.
	/// </summary>
	/// <param name="command">The role assignment command.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A new grant aggregate with events raised.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the role is not found or not active.</exception>
	internal async Task<GrantAggregate> CreateRoleGrantAsync(
		AddRoleAssignmentCommand command,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(command);

		var role = await roleStore.GetRoleAsync(command.RoleName, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Role '{command.RoleName}' was not found.");

		if (role.State != RoleState.Active)
		{
			throw new InvalidOperationException($"Role '{command.RoleName}' is not active (current state: {role.State}).");
		}

		// One grant per role assignment: GrantType = "Role", Qualifier = roleName
		return new GrantAggregate(
			command.UserId,
			command.FullName,
			command.TenantId,
			RoleGrantType,
			command.RoleName,
			command.ExpiresOn,
			command.AssignedBy);
	}

	/// <summary>
	/// Builds the composite grant key for a role assignment, enabling lookup for revocation.
	/// </summary>
	/// <param name="command">The role removal command.</param>
	/// <returns>The composite grant key in format "{UserId}:{TenantId}:Role:{RoleName}".</returns>
	internal static GrantKey BuildRoleGrantKey(RemoveRoleAssignmentCommand command)
	{
		ArgumentNullException.ThrowIfNull(command);
		return new GrantKey(command.UserId, command.TenantId, RoleGrantType, command.RoleName);
	}
}
