// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines a contract for upgrading snapshots from older versions to newer versions.
/// </summary>
/// <remarks>
/// <para>
/// Snapshot upgraders are used in event sourcing systems to handle snapshot schema evolution.
/// When a snapshot's schema changes between versions, upgraders transform old snapshot
/// formats to the current version during aggregate hydration from snapshots.
/// </para>
/// <para>
/// This is the snapshot counterpart to <see cref="IEventUpgrader"/>, following the same
/// versioning pattern. Register upgraders in DI to enable automatic snapshot migration.
/// </para>
/// </remarks>
public interface ISnapshotUpgrader
{
	/// <summary>
	/// Gets the aggregate type whose snapshots this upgrader handles.
	/// </summary>
	/// <value>The fully-qualified aggregate type name.</value>
	string AggregateType { get; }

	/// <summary>
	/// Gets the source version this upgrader upgrades from.
	/// </summary>
	/// <value>The source snapshot version.</value>
	int FromVersion { get; }

	/// <summary>
	/// Gets the target version this upgrader upgrades to.
	/// </summary>
	/// <value>The target snapshot version.</value>
	int ToVersion { get; }

	/// <summary>
	/// Determines whether this upgrader can upgrade snapshots for the specified aggregate type and version.
	/// </summary>
	/// <param name="aggregateType">The type of the aggregate.</param>
	/// <param name="fromVersion">The snapshot version to upgrade from.</param>
	/// <returns>True if this upgrader can handle the upgrade, otherwise false.</returns>
	bool CanUpgrade(string aggregateType, int fromVersion);

	/// <summary>
	/// Upgrades a snapshot from an older version to a newer version.
	/// </summary>
	/// <param name="oldSnapshotData">The old snapshot data.</param>
	/// <returns>The upgraded snapshot data.</returns>
	byte[] Upgrade(byte[] oldSnapshotData);
}
