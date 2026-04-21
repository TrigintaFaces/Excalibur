// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Authorization.Stores.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IGrantStore"/>, <see cref="IGrantQueryStore"/>,
/// and <see cref="IActivityGroupGrantStore"/> backed by <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Intended for development, testing, and standalone scenarios where no persistent store
/// (SQL Server, etc.) is configured. Registered as a singleton fallback via
/// <c>TryAddSingleton</c> in <c>AddExcaliburA3()</c>.
/// </para>
/// <para>
/// Thread-safe by design: all mutations use <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// atomic operations with no additional locking.
/// </para>
/// </remarks>
internal sealed class InMemoryGrantStore : IGrantStore, IGrantQueryStore, IActivityGroupGrantStore
{
	private readonly ConcurrentDictionary<string, Grant> _grants = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<Grant?> GetGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(userId, tenantId, grantType, qualifier);
		_grants.TryGetValue(key, out var grant);
		return Task.FromResult(grant);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(
		string userId,
		CancellationToken cancellationToken)
	{
		var results = _grants.Values
			.Where(g => string.Equals(g.UserId, userId, StringComparison.Ordinal))
			.ToList();

		return Task.FromResult<IReadOnlyList<Grant>>(results);
	}

	/// <inheritdoc />
	public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(grant);

		var key = BuildKey(grant.UserId, grant.TenantId ?? string.Empty, grant.GrantType, grant.Qualifier);
		_grants[key] = grant;
		return Task.FromResult(1);
	}

	/// <inheritdoc />
	public Task<int> DeleteGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		string? revokedBy,
		DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(userId, tenantId, grantType, qualifier);
		return Task.FromResult(_grants.TryRemove(key, out _) ? 1 : 0);
	}

	/// <inheritdoc />
	public Task<bool> GrantExistsAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(userId, tenantId, grantType, qualifier);
		return Task.FromResult(_grants.ContainsKey(key));
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IGrantQueryStore))
		{
			return this;
		}

		if (serviceType == typeof(IActivityGroupGrantStore))
		{
			return this;
		}

		return null;
	}

	// -- IGrantQueryStore --

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(
		string? userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		var results = _grants.Values
			.Where(g =>
				(userId is null || string.Equals(g.UserId, userId, StringComparison.Ordinal)) &&
				string.Equals(g.TenantId, tenantId, StringComparison.Ordinal) &&
				string.Equals(g.GrantType, grantType, StringComparison.Ordinal) &&
				string.Equals(g.Qualifier, qualifier, StringComparison.Ordinal))
			.ToList();

		return Task.FromResult<IReadOnlyList<Grant>>(results);
	}

	/// <inheritdoc />
	public Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(
		string userId,
		CancellationToken cancellationToken)
	{
		var results = _grants.Values
			.Where(g => string.Equals(g.UserId, userId, StringComparison.Ordinal))
			.ToDictionary(
				g => BuildScopeKey(g.TenantId ?? string.Empty, g.GrantType, g.Qualifier),
				g => (object)g,
				StringComparer.Ordinal);

		return Task.FromResult<IReadOnlyDictionary<string, object>>(results);
	}

	// -- IActivityGroupGrantStore --

	/// <inheritdoc />
	public Task<int> DeleteActivityGroupGrantsByUserIdAsync(
		string userId,
		string grantType,
		CancellationToken cancellationToken)
	{
		var removed = 0;

		foreach (var kvp in _grants)
		{
			if (string.Equals(kvp.Value.UserId, userId, StringComparison.Ordinal) &&
				string.Equals(kvp.Value.GrantType, grantType, StringComparison.Ordinal))
			{
				if (_grants.TryRemove(kvp.Key, out _))
				{
					removed++;
				}
			}
		}

		return Task.FromResult(removed);
	}

	/// <inheritdoc />
	public Task<int> DeleteAllActivityGroupGrantsAsync(
		string grantType,
		CancellationToken cancellationToken)
	{
		var removed = 0;

		foreach (var kvp in _grants)
		{
			if (string.Equals(kvp.Value.GrantType, grantType, StringComparison.Ordinal))
			{
				if (_grants.TryRemove(kvp.Key, out _))
				{
					removed++;
				}
			}
		}

		return Task.FromResult(removed);
	}

	/// <inheritdoc />
	public Task<int> InsertActivityGroupGrantAsync(
		string userId,
		string fullName,
		string? tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		CancellationToken cancellationToken)
	{
		var grant = new Grant(
			userId,
			fullName,
			tenantId,
			grantType,
			qualifier,
			expiresOn,
			grantedBy,
			DateTimeOffset.UtcNow);

		var key = BuildKey(userId, tenantId ?? string.Empty, grantType, qualifier);
		_grants[key] = grant;
		return Task.FromResult(1);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
		string grantType,
		CancellationToken cancellationToken)
	{
		var userIds = _grants.Values
			.Where(g => string.Equals(g.GrantType, grantType, StringComparison.Ordinal))
			.Select(g => g.UserId)
			.Distinct(StringComparer.Ordinal)
			.ToList();

		return Task.FromResult<IReadOnlyList<string>>(userIds);
	}

	private static string BuildKey(string userId, string tenantId, string grantType, string qualifier) =>
		$"{userId}:{tenantId}:{grantType}:{qualifier}";

	private static string BuildScopeKey(string tenantId, string grantType, string qualifier) =>
		$"{tenantId}:{grantType}:{qualifier}";
}
