// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines a contract for upgrading events from older versions to newer versions.
/// </summary>
/// <remarks>
/// <para>
/// Event upgraders are used in event sourcing systems to handle schema evolution.
/// When an event's schema changes between versions, upgraders transform old event
/// formats to the current version during aggregate hydration.
/// </para>
/// </remarks>
public interface IEventUpgrader
{
	/// <summary>
	/// Gets the event type this upgrader handles.
	/// </summary>
	/// <value>The event type this upgrader handles.</value>
	string EventType { get; }

	/// <summary>
	/// Gets the source version this upgrader upgrades from.
	/// </summary>
	/// <value>The source version this upgrader upgrades from.</value>
	int FromVersion { get; }

	/// <summary>
	/// Gets the target version this upgrader upgrades to.
	/// </summary>
	/// <value>The target version this upgrader upgrades to.</value>
	int ToVersion { get; }

	/// <summary>
	/// Determines whether this upgrader can upgrade the specified event.
	/// </summary>
	/// <param name="eventType">The type of the event.</param>
	/// <param name="fromVersion">The version to upgrade from.</param>
	/// <returns>True if this upgrader can handle the upgrade, otherwise false.</returns>
	bool CanUpgrade(string eventType, int fromVersion);

	/// <summary>
	/// Upgrades an event from an older version to a newer version.
	/// </summary>
	/// <param name="oldEvent">The old event data.</param>
	/// <returns>The upgraded event.</returns>
	object Upgrade(object oldEvent);
}
