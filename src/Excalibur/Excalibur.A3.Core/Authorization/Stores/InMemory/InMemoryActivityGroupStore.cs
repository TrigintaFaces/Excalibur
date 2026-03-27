// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Authorization.Stores.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IActivityGroupStore"/> backed by
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Intended for development, testing, and standalone scenarios where no persistent store
/// (SQL Server, etc.) is configured. Registered as a singleton fallback via
/// <c>TryAddSingleton</c> in <c>AddExcaliburA3()</c>.
/// </para>
/// <para>
/// Activity groups are stored as entries keyed by <c>"{name}:{activityName}"</c>.
/// The <see cref="FindActivityGroupsAsync"/> method returns groups keyed by name,
/// with values containing the list of associated activity names.
/// </para>
/// <para>
/// Thread-safe by design: all mutations use <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// atomic operations with no additional locking.
/// </para>
/// </remarks>
internal sealed class InMemoryActivityGroupStore : IActivityGroupStore
{
	/// <summary>
	/// Stores activity group entries keyed by "{name}:{activityName}".
	/// </summary>
	private readonly ConcurrentDictionary<string, ActivityGroupEntry> _entries = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<bool> ActivityGroupExistsAsync(
		string activityGroupName,
		CancellationToken cancellationToken)
	{
		var exists = _entries.Values
			.Any(e => string.Equals(e.Name, activityGroupName, StringComparison.Ordinal));

		return Task.FromResult(exists);
	}

	/// <inheritdoc />
	public Task<IReadOnlyDictionary<string, object>> FindActivityGroupsAsync(
		CancellationToken cancellationToken)
	{
		var groups = _entries.Values
			.GroupBy(e => e.Name, StringComparer.Ordinal)
			.ToDictionary(
				g => g.Key,
				g => (object)g.Select(e => e.ActivityName).ToList());

		return Task.FromResult<IReadOnlyDictionary<string, object>>(groups);
	}

	/// <inheritdoc />
	public Task<int> DeleteAllActivityGroupsAsync(CancellationToken cancellationToken)
	{
		var count = _entries.Count;
		_entries.Clear();
		return Task.FromResult(count);
	}

	/// <inheritdoc />
	public Task<int> CreateActivityGroupAsync(
		string? tenantId,
		string name,
		string activityName,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(name, activityName);
		var entry = new ActivityGroupEntry(tenantId, name, activityName);

		// TryAdd returns false if already exists (no overwrite)
		return Task.FromResult(_entries.TryAdd(key, entry) ? 1 : 0);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}

	private static string BuildKey(string name, string activityName) =>
		$"{name}:{activityName}";

	/// <summary>
	/// Internal record representing a single activity-group-to-activity mapping.
	/// </summary>
	private sealed record ActivityGroupEntry(string? TenantId, string Name, string ActivityName);
}
