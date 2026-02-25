// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Base implementation of <see cref="ISnapshotUpgrader"/> for typed snapshot transformations.
/// </summary>
/// <typeparam name="TFrom">The source snapshot data type (deserialized from old version).</typeparam>
/// <typeparam name="TTo">The target snapshot data type (for new version).</typeparam>
/// <remarks>
/// <para>
/// Provides a type-safe base class for implementing snapshot upgraders.
/// Derived classes only need to implement the <see cref="UpgradeSnapshot"/> method
/// with strongly-typed parameters.
/// </para>
/// <para>
/// This is the snapshot counterpart to <see cref="Upcasting.EventUpgrader{TFrom, TTo}"/>,
/// following the same versioning pattern.
/// </para>
/// </remarks>
public abstract class SnapshotUpgrader<TFrom, TTo> : ISnapshotUpgrader
	where TFrom : class
	where TTo : class
{
	private readonly ISnapshotDataSerializer _serializer;

	/// <summary>
	/// Initializes a new instance of the <see cref="SnapshotUpgrader{TFrom, TTo}"/> class.
	/// </summary>
	/// <param name="serializer">The serializer for snapshot data conversion.</param>
	protected SnapshotUpgrader(ISnapshotDataSerializer serializer)
	{
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
	}

	/// <inheritdoc />
	public abstract string AggregateType { get; }

	/// <inheritdoc />
	public abstract int FromVersion { get; }

	/// <inheritdoc />
	public abstract int ToVersion { get; }

	/// <inheritdoc />
	public bool CanUpgrade(string aggregateType, int fromVersion) =>
		string.Equals(aggregateType, AggregateType, StringComparison.Ordinal) && fromVersion == FromVersion;

	/// <inheritdoc />
	public byte[] Upgrade(byte[] oldSnapshotData)
	{
		ArgumentNullException.ThrowIfNull(oldSnapshotData);

		var oldSnapshot = _serializer.Deserialize<TFrom>(oldSnapshotData)
			?? throw new InvalidOperationException(
				$"Failed to deserialize snapshot data as {typeof(TFrom).Name}");

		var newSnapshot = UpgradeSnapshot(oldSnapshot);

		return _serializer.Serialize(newSnapshot);
	}

	/// <summary>
	/// Upgrades a typed snapshot from the old version to the new version.
	/// </summary>
	/// <param name="oldSnapshot">The old snapshot data.</param>
	/// <returns>The upgraded snapshot data.</returns>
	protected abstract TTo UpgradeSnapshot(TFrom oldSnapshot);
}
