// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Snapshots.Upgrading;

/// <summary>
/// Represents a registered snapshot upgrader with its metadata for the registry.
/// </summary>
/// <remarks>
/// <para>
/// This is an internal wrapper that bridges the generic <see cref="ISnapshotUpgrader{TFrom,TTo}"/>
/// to the registry's non-generic storage, while preserving the serialization/deserialization
/// delegate chain.
/// </para>
/// </remarks>
internal sealed class SnapshotUpgraderRegistration
{
	/// <summary>
	/// Gets the aggregate type this upgrader handles.
	/// </summary>
	internal string AggregateType { get; }

	/// <summary>
	/// Gets the source version.
	/// </summary>
	internal int FromVersion { get; }

	/// <summary>
	/// Gets the target version.
	/// </summary>
	internal int ToVersion { get; }

	/// <summary>
	/// Gets the upgrade delegate that transforms serialized data.
	/// </summary>
	internal Func<byte[], byte[]> UpgradeFunc { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="SnapshotUpgraderRegistration"/> class.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <param name="fromVersion">The source version.</param>
	/// <param name="toVersion">The target version.</param>
	/// <param name="upgradeFunc">The upgrade delegate.</param>
	internal SnapshotUpgraderRegistration(
		string aggregateType,
		int fromVersion,
		int toVersion,
		Func<byte[], byte[]> upgradeFunc)
	{
		AggregateType = aggregateType;
		FromVersion = fromVersion;
		ToVersion = toVersion;
		UpgradeFunc = upgradeFunc;
	}
}
