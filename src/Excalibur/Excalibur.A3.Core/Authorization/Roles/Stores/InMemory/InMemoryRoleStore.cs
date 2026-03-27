// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Authorization.Roles.Stores.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IRoleStore"/> backed by
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Intended for development, testing, and standalone scenarios where no persistent store
/// is configured. Registered as a singleton fallback via <c>TryAddSingleton</c> in
/// <c>AddRoles()</c>.
/// </para>
/// </remarks>
internal sealed class InMemoryRoleStore : IRoleStore
{
	private readonly ConcurrentDictionary<string, RoleSummary> _roles = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<RoleSummary?> GetRoleAsync(string roleId, CancellationToken cancellationToken)
	{
		_roles.TryGetValue(roleId, out var role);
		return Task.FromResult(role);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<RoleSummary>> GetRolesAsync(string? tenantId, CancellationToken cancellationToken)
	{
		var results = tenantId is null
			? _roles.Values.ToList()
			: _roles.Values
				.Where(r => string.Equals(r.TenantId, tenantId, StringComparison.Ordinal))
				.ToList();

		return Task.FromResult<IReadOnlyList<RoleSummary>>(results);
	}

	/// <inheritdoc />
	public Task SaveRoleAsync(RoleSummary role, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(role);
		_roles[role.RoleId] = role;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> DeleteRoleAsync(string roleId, CancellationToken cancellationToken)
	{
		return Task.FromResult(_roles.TryRemove(roleId, out _));
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}
}
