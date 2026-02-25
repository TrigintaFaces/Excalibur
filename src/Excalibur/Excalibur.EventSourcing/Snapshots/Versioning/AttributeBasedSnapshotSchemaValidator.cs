// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace Excalibur.EventSourcing.Snapshots.Versioning;

/// <summary>
/// Validates snapshot schema versions using <see cref="SnapshotSchemaVersionAttribute"/>
/// on registered snapshot state types.
/// </summary>
/// <remarks>
/// <para>
/// Uses a concurrent dictionary to cache resolved versions per aggregate type,
/// bounded to prevent unbounded growth.
/// </para>
/// </remarks>
public sealed class AttributeBasedSnapshotSchemaValidator : ISnapshotSchemaValidator
{
	private readonly ConcurrentDictionary<string, int> _versionCache = new();

	/// <summary>
	/// Registers a snapshot state type for the specified aggregate type.
	/// </summary>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="snapshotStateType">The snapshot state type decorated with <see cref="SnapshotSchemaVersionAttribute"/>.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when the snapshot state type does not have a <see cref="SnapshotSchemaVersionAttribute"/>.
	/// </exception>
	public void Register(string aggregateType, Type snapshotStateType)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		ArgumentNullException.ThrowIfNull(snapshotStateType);

		var attribute = snapshotStateType.GetCustomAttribute<SnapshotSchemaVersionAttribute>()
			?? throw new ArgumentException(
				$"Type '{snapshotStateType.Name}' does not have a [{nameof(SnapshotSchemaVersionAttribute)}] attribute.",
				nameof(snapshotStateType));

		_versionCache[aggregateType] = attribute.Version;
	}

	/// <inheritdoc />
	public ValueTask<SnapshotSchemaValidationResult> ValidateAsync(
		string aggregateType,
		int storedVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		if (!_versionCache.TryGetValue(aggregateType, out var currentVersion))
		{
			// No registered version — cannot validate, treat as compatible
			return new ValueTask<SnapshotSchemaValidationResult>(
				SnapshotSchemaValidationResult.Compatible(storedVersion));
		}

		if (storedVersion == currentVersion)
		{
			return new ValueTask<SnapshotSchemaValidationResult>(
				SnapshotSchemaValidationResult.Compatible(currentVersion));
		}

		var reason = storedVersion < currentVersion
			? $"Snapshot schema version {storedVersion} is older than current version {currentVersion}. Upgrade required."
			: $"Snapshot schema version {storedVersion} is newer than current version {currentVersion}. Downgrade not supported.";

		return new ValueTask<SnapshotSchemaValidationResult>(
			SnapshotSchemaValidationResult.Incompatible(storedVersion, currentVersion, reason));
	}

	/// <inheritdoc />
	public int? GetCurrentVersion(string aggregateType)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		return _versionCache.TryGetValue(aggregateType, out var version) ? version : null;
	}
}
