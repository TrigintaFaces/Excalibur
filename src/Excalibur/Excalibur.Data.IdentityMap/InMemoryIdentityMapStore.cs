// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Data.IdentityMap;

/// <summary>
/// In-memory implementation of <see cref="IIdentityMapStore"/> for testing and development.
/// </summary>
internal sealed class InMemoryIdentityMapStore : IIdentityMapStore
{
	private readonly ConcurrentDictionary<(string ExternalSystem, string ExternalId, string AggregateType), string> _mappings = new(StringTupleComparer.Instance);

	/// <inheritdoc/>
	public ValueTask<string?> ResolveAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		_mappings.TryGetValue((externalSystem, externalId, aggregateType), out var aggregateId);
		return new ValueTask<string?>(aggregateId);
	}

	/// <inheritdoc/>
	public ValueTask BindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		_mappings[(externalSystem, externalId, aggregateType)] = aggregateId;
		return default;
	}

	/// <inheritdoc/>
	public ValueTask<IdentityBindResult> TryBindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		var key = (externalSystem, externalId, aggregateType);

		// If key doesn't exist, add it and return WasCreated=true.
		// If key exists, return the existing value with WasCreated=false.
		if (_mappings.TryAdd(key, aggregateId))
		{
			return new ValueTask<IdentityBindResult>(
				new IdentityBindResult(aggregateId, WasCreated: true));
		}

		var existing = _mappings[key];
		return new ValueTask<IdentityBindResult>(
			new IdentityBindResult(existing, WasCreated: false));
	}

	/// <inheritdoc/>
	public ValueTask<bool> UnbindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var removed = _mappings.TryRemove((externalSystem, externalId, aggregateType), out _);
		return new ValueTask<bool>(removed);
	}

	/// <summary>
	/// Case-insensitive comparer for the string tuple key.
	/// </summary>
	private sealed class StringTupleComparer : IEqualityComparer<(string, string, string)>
	{
		public static readonly StringTupleComparer Instance = new();

		public bool Equals((string, string, string) x, (string, string, string) y)
		{
			return string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase);
		}

		public int GetHashCode((string, string, string) obj)
		{
			return HashCode.Combine(
				StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
				StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2),
				StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item3));
		}
	}
}
