// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.EventSourcing.Upcasting;

using Microsoft.Extensions.Logging;

using Excalibur.Dispatch.Versioning;
namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Manages snapshot version upgrades by maintaining a registry of upgraders
/// and finding optimal upgrade paths between versions using BFS.
/// </summary>
/// <remarks>
/// <para>
/// This is the snapshot counterpart to <see cref="Upcasting.EventVersionManager"/>,
/// following the same BFS-based path finding pattern for version migrations.
/// </para>
/// </remarks>
public sealed partial class SnapshotVersionManager
{
	// Copy-on-write: the value is an immutable snapshot replaced (never mutated in place) under
	// _registrationLock, so read paths enumerate a stable list without a lock even if a registration races.
	private readonly ConcurrentDictionary<string, IReadOnlyList<RegisteredUpgrader>> _upgraders = new(StringComparer.Ordinal);

	private readonly UpgradePathResolver<RegisteredUpgrader> _paths =
		new(static u => u.FromVersion, static u => u.ToVersion);
	private readonly Lock _registrationLock = new();
	private readonly ILogger<SnapshotVersionManager> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SnapshotVersionManager"/> class.
	/// </summary>
	/// <param name="upgraders">The upgraders to register.</param>
	/// <param name="logger">The logger.</param>
	public SnapshotVersionManager(
		IEnumerable<ISnapshotUpgrader> upgraders,
		ILogger<SnapshotVersionManager> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(upgraders);

		foreach (var upgrader in upgraders)
		{
			RegisterUpgrader(upgrader);
		}
	}

	/// <summary>
	/// Registers a snapshot upgrader.
	/// </summary>
	/// <param name="upgrader">The upgrader to register.</param>
	/// <exception cref="ArgumentNullException">Thrown when upgrader is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when an upgrader for the same aggregate type and version range already exists.</exception>
	/// <remarks>Type and version metadata is captured once at registration. Later changes to consumer
	/// getters do not remap an existing registration. The original upgrader performs the transformation.</remarks>
	public void RegisterUpgrader(ISnapshotUpgrader upgrader)
	{
		ArgumentNullException.ThrowIfNull(upgrader);

		var type = upgrader.AggregateType;
		var fromVersion = upgrader.FromVersion;
		var toVersion = upgrader.ToVersion;

		if (fromVersion >= toVersion)
		{
			throw new ArgumentException(
				$"Invalid upgrader: FromVersion ({fromVersion}) must be less than ToVersion " +
				$"({toVersion}). Only forward upgrading is supported.",
				nameof(upgrader));
		}

		// Top-level lock eliminates the TOCTOU between GetOrAdd (which may create or return
		// an existing list) and the subsequent mutation. RegisterUpgrader is called during
		// startup only, so contention is negligible.
		lock (_registrationLock)
		{
			var existing = _upgraders.TryGetValue(type, out var current) ? current : [];

			var existingUpgrader = existing.FirstOrDefault(u =>
				u.FromVersion == fromVersion && u.ToVersion == toVersion);

			if (existingUpgrader is not null)
			{
				throw new InvalidOperationException(
					$"A snapshot upgrader for aggregate type '{type}' from version {fromVersion} to {toVersion} is already registered.");
			}

			// Replace with a fresh immutable snapshot rather than mutating a list a reader may enumerate.
			_upgraders[type] = [.. existing, new RegisteredUpgrader(upgrader, fromVersion, toVersion)];
			_paths.Invalidate();
		}

		LogUpgraderRegistered(type, fromVersion, toVersion);
	}

	/// <summary>
	/// Upgrades snapshot data to the specified version.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <param name="snapshotData">The snapshot data bytes.</param>
	/// <param name="fromVersion">The current version of the snapshot.</param>
	/// <param name="toVersion">The target version.</param>
	/// <returns>The upgraded snapshot data bytes.</returns>
	/// <exception cref="ArgumentException">Thrown when the aggregate type is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when snapshotData is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when no upgrade path is found.</exception>
	/// <remarks>The manager performs typed byte-array calls. Registered upgraders and their serializers
	/// must use operations compatible with the application's trimming and NativeAOT configuration.</remarks>
	public byte[] UpgradeSnapshot(string aggregateType, byte[] snapshotData, int fromVersion, int toVersion)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		ArgumentNullException.ThrowIfNull(snapshotData);

		if (fromVersion == toVersion)
		{
			return snapshotData;
		}

		if (!_upgraders.TryGetValue(aggregateType, out var upgraderList))
		{
			throw new InvalidOperationException(
				$"No snapshot upgraders registered for aggregate type '{aggregateType}'.");
		}

		var upgradePath = _paths.Resolve(aggregateType, upgraderList, fromVersion, toVersion);
		if (upgradePath is null || upgradePath.Count == 0)
		{
			throw new InvalidOperationException(
				$"No snapshot upgrade path found for aggregate type '{aggregateType}' from version {fromVersion} to {toVersion}.");
		}

		var currentData = snapshotData;
		foreach (var upgrader in upgradePath)
		{
			LogSnapshotUpgrading(aggregateType, upgrader.FromVersion, upgrader.ToVersion);
			currentData = upgrader.Upgrader.Upgrade(currentData);
		}

		return currentData;
	}

	/// <summary>
	/// Determines whether an upgrade path exists for the specified aggregate type and version.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <param name="fromVersion">The source version.</param>
	/// <param name="toVersion">The target version.</param>
	/// <returns>True if an upgrade path exists; otherwise, false.</returns>
	public bool CanUpgrade(string aggregateType, int fromVersion, int toVersion)
	{
		if (fromVersion == toVersion)
		{
			return true;
		}

		if (!_upgraders.TryGetValue(aggregateType, out var upgraderList))
		{
			return false;
		}

		var path = _paths.Resolve(aggregateType, upgraderList, fromVersion, toVersion);
		return path is not null && path.Count > 0;
	}

	/// <summary>
	/// Gets all registered aggregate types.
	/// </summary>
	/// <returns>A collection of registered aggregate types.</returns>
	public IEnumerable<string> GetRegisteredAggregateTypes() => _upgraders.Keys;

	/// <summary>
	/// Gets all upgraders for a specific aggregate type.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <returns>A collection of upgraders for the aggregate type.</returns>
	public IEnumerable<ISnapshotUpgrader> GetUpgradersForAggregateType(string aggregateType) =>
		_upgraders.TryGetValue(aggregateType, out var upgraders)
			? upgraders.Select(static registration => registration.Upgrader)
			: [];


	[LoggerMessage(Diagnostics.EventSourcingEventId.SnapshotUpgraderRegistered, LogLevel.Information,
		"Registered snapshot upgrader for {AggregateType} from version {FromVersion} to {ToVersion}")]
	private partial void LogUpgraderRegistered(string aggregateType, int fromVersion, int toVersion);

	[LoggerMessage(Diagnostics.EventSourcingEventId.SnapshotUpgrading, LogLevel.Debug,
		"Upgrading snapshot for {AggregateType} from version {FromVersion} to {ToVersion}")]
	private partial void LogSnapshotUpgrading(string aggregateType, int fromVersion, int toVersion);
	private sealed record RegisteredUpgrader(ISnapshotUpgrader Upgrader, int FromVersion, int ToVersion);

}
