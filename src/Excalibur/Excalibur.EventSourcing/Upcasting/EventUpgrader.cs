// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Upcasting;

/// <summary>
/// Base implementation of <see cref="IEventUpgrader"/> for typed events.
/// </summary>
/// <typeparam name="TFrom">The source event type.</typeparam>
/// <typeparam name="TTo">The target event type.</typeparam>
/// <remarks>
/// <para>
/// Provides a type-safe base class for implementing event upgraders.
/// Derived classes only need to implement the <see cref="UpgradeEvent"/> method
/// with strongly-typed parameters.
/// </para>
/// </remarks>
public abstract class EventUpgrader<TFrom, TTo> : IEventUpgrader
	where TFrom : class
	where TTo : class
{
	/// <inheritdoc />
	public abstract string EventType { get; }

	/// <inheritdoc />
	public abstract int FromVersion { get; }

	/// <inheritdoc />
	public abstract int ToVersion { get; }

	/// <inheritdoc />
	public bool CanUpgrade(string eventType, int fromVersion) =>
		string.Equals(eventType, EventType, StringComparison.Ordinal) && fromVersion == FromVersion;

	/// <inheritdoc />
	public object Upgrade(object oldEvent)
	{
		if (oldEvent is not TFrom typedEvent)
		{
			throw new ArgumentException(
				$"Expected event of type {typeof(TFrom).Name}, but got {oldEvent?.GetType().Name ?? "null"}",
				nameof(oldEvent));
		}

		return UpgradeEvent(typedEvent);
	}

	/// <summary>
	/// Upgrades a typed event from the old version to the new version.
	/// </summary>
	/// <param name="oldEvent">The old event.</param>
	/// <returns>The upgraded event.</returns>
	protected abstract TTo UpgradeEvent(TFrom oldEvent);
}
