// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Snapshots.Upgrading;

/// <summary>
/// Registry that chains <see cref="ISnapshotUpgrader{TFrom,TTo}"/> implementations to upgrade
/// snapshot data through multiple version steps.
/// </summary>
/// <remarks>
/// <para>
/// The registry maintains a graph of upgraders per aggregate type and uses BFS to find
/// the shortest upgrade path between any two versions, consistent with
/// <see cref="SnapshotVersionManager"/>.
/// </para>
/// <para>
/// Each upgrader is registered with a serialization bridge so that the registry
/// can deserialize from the source type, invoke the typed upgrader, and re-serialize
/// to the target type.
/// </para>
/// </remarks>
public sealed partial class SnapshotUpgraderRegistry
{
	private readonly Dictionary<string, List<SnapshotUpgraderRegistration>> _upgraders = [];
	private readonly ILogger<SnapshotUpgraderRegistry> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SnapshotUpgraderRegistry"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public SnapshotUpgraderRegistry(ILogger<SnapshotUpgraderRegistry> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Registers a typed snapshot upgrader with serialization support.
	/// </summary>
	/// <typeparam name="TFrom">The source snapshot state type.</typeparam>
	/// <typeparam name="TTo">The target snapshot state type.</typeparam>
	/// <param name="upgrader">The typed upgrader to register.</param>
	/// <param name="serializer">The serializer for snapshot data conversion.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="upgrader"/> or <paramref name="serializer"/> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when a duplicate upgrader is registered for the same aggregate type and version range.</exception>
	public void Register<TFrom, TTo>(
		ISnapshotUpgrader<TFrom, TTo> upgrader,
		ISnapshotDataSerializer serializer)
		where TFrom : class
		where TTo : class
	{
		ArgumentNullException.ThrowIfNull(upgrader);
		ArgumentNullException.ThrowIfNull(serializer);

		if (!_upgraders.TryGetValue(upgrader.AggregateType, out var registrations))
		{
			registrations = [];
			_upgraders[upgrader.AggregateType] = registrations;
		}

		var existing = registrations.Find(r =>
			r.FromVersion == upgrader.FromVersion && r.ToVersion == upgrader.ToVersion);

		if (existing is not null)
		{
			throw new InvalidOperationException(
				$"A snapshot upgrader for aggregate type '{upgrader.AggregateType}' from version {upgrader.FromVersion} to {upgrader.ToVersion} is already registered.");
		}

		// Create a bridge delegate that handles serialization/deserialization
		byte[] UpgradeFunc(byte[] data)
		{
			var source = serializer.Deserialize<TFrom>(data)
				?? throw new InvalidOperationException(
					$"Failed to deserialize snapshot data as {typeof(TFrom).Name}.");
			var target = upgrader.Upgrade(source);
			return serializer.Serialize(target);
		}

		registrations.Add(new SnapshotUpgraderRegistration(
			upgrader.AggregateType,
			upgrader.FromVersion,
			upgrader.ToVersion,
			UpgradeFunc));

		LogUpgraderRegistered(upgrader.AggregateType, upgrader.FromVersion, upgrader.ToVersion);
	}

	/// <summary>
	/// Upgrades snapshot data from one version to another using the shortest chain of registered upgraders.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <param name="snapshotData">The snapshot data bytes.</param>
	/// <param name="fromVersion">The current version of the snapshot.</param>
	/// <param name="toVersion">The target version.</param>
	/// <returns>The upgraded snapshot data bytes.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="aggregateType"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshotData"/> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when no upgrade path is found.</exception>
	public byte[] Upgrade(string aggregateType, byte[] snapshotData, int fromVersion, int toVersion)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		ArgumentNullException.ThrowIfNull(snapshotData);

		if (fromVersion == toVersion)
		{
			return snapshotData;
		}

		if (!_upgraders.TryGetValue(aggregateType, out var registrations))
		{
			throw new InvalidOperationException(
				$"No snapshot upgraders registered for aggregate type '{aggregateType}'.");
		}

		var path = FindUpgradePath(registrations, fromVersion, toVersion);
		if (path is null || path.Count == 0)
		{
			throw new InvalidOperationException(
				$"No snapshot upgrade path found for aggregate type '{aggregateType}' from version {fromVersion} to {toVersion}.");
		}

		var currentData = snapshotData;
		foreach (var registration in path)
		{
			LogUpgrading(aggregateType, registration.FromVersion, registration.ToVersion);
			currentData = registration.UpgradeFunc(currentData);
		}

		return currentData;
	}

	/// <summary>
	/// Determines whether an upgrade path exists for the specified aggregate type and versions.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <param name="fromVersion">The source version.</param>
	/// <param name="toVersion">The target version.</param>
	/// <returns><see langword="true"/> if an upgrade path exists; otherwise, <see langword="false"/>.</returns>
	public bool CanUpgrade(string aggregateType, int fromVersion, int toVersion)
	{
		if (fromVersion == toVersion)
		{
			return true;
		}

		if (!_upgraders.TryGetValue(aggregateType, out var registrations))
		{
			return false;
		}

		var path = FindUpgradePath(registrations, fromVersion, toVersion);
		return path is not null && path.Count > 0;
	}

	/// <summary>
	/// Finds the shortest upgrade path between versions using BFS.
	/// </summary>
	private static List<SnapshotUpgraderRegistration>? FindUpgradePath(
		List<SnapshotUpgraderRegistration> registrations,
		int fromVersion,
		int toVersion)
	{
		var queue = new Queue<(int Version, List<SnapshotUpgraderRegistration> Path)>();
		var visited = new HashSet<int>();

		queue.Enqueue((fromVersion, []));
		_ = visited.Add(fromVersion);

		while (queue.Count > 0)
		{
			var (currentVersion, currentPath) = queue.Dequeue();

			if (currentVersion == toVersion)
			{
				return currentPath;
			}

			foreach (var registration in registrations.Where(r => r.FromVersion == currentVersion))
			{
				if (!visited.Contains(registration.ToVersion))
				{
					_ = visited.Add(registration.ToVersion);
					var newPath = new List<SnapshotUpgraderRegistration>(currentPath) { registration };
					queue.Enqueue((registration.ToVersion, newPath));
				}
			}
		}

		return null;
	}

	[LoggerMessage(Diagnostics.EventSourcingEventId.SnapshotUpgraderRegistered, LogLevel.Information,
		"Registered typed snapshot upgrader for {AggregateType} from version {FromVersion} to {ToVersion}")]
	private partial void LogUpgraderRegistered(string aggregateType, int fromVersion, int toVersion);

	[LoggerMessage(Diagnostics.EventSourcingEventId.SnapshotUpgrading, LogLevel.Debug,
		"Upgrading snapshot for {AggregateType} from version {FromVersion} to {ToVersion}")]
	private partial void LogUpgrading(string aggregateType, int fromVersion, int toVersion);
}
