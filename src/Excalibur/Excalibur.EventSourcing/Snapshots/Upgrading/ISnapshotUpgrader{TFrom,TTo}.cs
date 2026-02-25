// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Snapshots.Upgrading;

/// <summary>
/// Defines a strongly-typed snapshot upgrader that converts snapshot data from one version to another.
/// </summary>
/// <typeparam name="TFrom">The source snapshot state type.</typeparam>
/// <typeparam name="TTo">The target snapshot state type.</typeparam>
/// <remarks>
/// <para>
/// Provides declarative snapshot version migration by defining explicit type mappings.
/// Register implementations with <see cref="SnapshotUpgraderRegistry"/> to enable
/// automatic chained upgrading through multiple versions.
/// </para>
/// <para>
/// This interface complements the existing <see cref="Abstractions.ISnapshotUpgrader"/>
/// (which operates on raw bytes) by providing a higher-level, type-safe abstraction.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderSnapshotV1ToV2 : ISnapshotUpgrader&lt;OrderSnapshotV1, OrderSnapshotV2&gt;
/// {
///     public string AggregateType =&gt; "Order";
///     public int FromVersion =&gt; 1;
///     public int ToVersion =&gt; 2;
///
///     public OrderSnapshotV2 Upgrade(OrderSnapshotV1 source)
///     {
///         return new OrderSnapshotV2
///         {
///             OrderId = source.OrderId,
///             Status = source.Status,
///             CustomerEmail = "" // New field with default
///         };
///     }
/// }
/// </code>
/// </example>
public interface ISnapshotUpgrader<in TFrom, out TTo>
	where TFrom : class
	where TTo : class
{
	/// <summary>
	/// Gets the aggregate type whose snapshots this upgrader handles.
	/// </summary>
	/// <value>The fully-qualified aggregate type name.</value>
	string AggregateType { get; }

	/// <summary>
	/// Gets the source version this upgrader upgrades from.
	/// </summary>
	/// <value>The source snapshot schema version.</value>
	int FromVersion { get; }

	/// <summary>
	/// Gets the target version this upgrader upgrades to.
	/// </summary>
	/// <value>The target snapshot schema version.</value>
	int ToVersion { get; }

	/// <summary>
	/// Upgrades a snapshot from the source type to the target type.
	/// </summary>
	/// <param name="source">The source snapshot data.</param>
	/// <returns>The upgraded snapshot data.</returns>
	TTo Upgrade(TFrom source);
}
