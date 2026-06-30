// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Upcasting;

/// <summary>
/// Manages event version upgrades by maintaining a registry of upgraders
/// and finding optimal upgrade paths between versions.
/// </summary>
/// <remarks>
/// <para>
/// The EventVersionManager uses a breadth-first search algorithm to find
/// the shortest upgrade path when multiple intermediate versions exist.
/// This ensures events are upgraded efficiently through the minimal number
/// of transformations.
/// </para>
/// </remarks>
public partial class EventVersionManager
{
	private static readonly CompositeFormat UpgraderAlreadyExistsFormat =
			CompositeFormat.Parse(Resources.EventVersionManager_UpgraderAlreadyExistsFormat);
	private static readonly CompositeFormat NoUpgradersRegisteredFormat =
			CompositeFormat.Parse(Resources.EventVersionManager_NoUpgradersRegisteredFormat);
	private static readonly CompositeFormat NoUpgradePathFoundFormat =
			CompositeFormat.Parse(Resources.EventVersionManager_NoUpgradePathFoundFormat);

	private readonly ConcurrentDictionary<string, List<IEventUpgrader>> _upgraders = new(StringComparer.Ordinal);
	private readonly Lock _registrationLock = new();
	private readonly ILogger<EventVersionManager> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventVersionManager"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
	public EventVersionManager(ILogger<EventVersionManager> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Registers an event upgrader.
	/// </summary>
	/// <param name="upgrader">The upgrader to register.</param>
	/// <exception cref="ArgumentNullException">Thrown when upgrader is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when an upgrader for the same event type and version range already exists.</exception>
	public void RegisterUpgrader(IEventUpgrader upgrader)
	{
		ArgumentNullException.ThrowIfNull(upgrader);

		// Top-level lock eliminates the TOCTOU between GetOrAdd (which may create or return an existing
		// list) and the subsequent conflict-check + mutation, and serializes concurrent registrations.
		// Mirrors the hardened sibling SnapshotVersionManager so the two parallel managers are consistent.
		// RegisterUpgrader is a startup-time call, so contention is negligible.
		lock (_registrationLock)
		{
			var upgraderList = _upgraders.GetOrAdd(upgrader.EventType, static _ => []);

			// Check for conflicts
			var existingUpgrader = upgraderList.FirstOrDefault(u =>
				u.FromVersion == upgrader.FromVersion && u.ToVersion == upgrader.ToVersion);

			if (existingUpgrader is not null)
			{
				throw new InvalidOperationException(
						string.Format(
								CultureInfo.CurrentCulture,
								UpgraderAlreadyExistsFormat,
								upgrader.EventType,
								upgrader.FromVersion,
								upgrader.ToVersion));
			}

			upgraderList.Add(upgrader);
		}

		LogUpgraderRegistered(upgrader.EventType, upgrader.FromVersion, upgrader.ToVersion);
	}

	/// <summary>
	/// Upgrades an event to the specified version.
	/// </summary>
	/// <param name="eventType">The event type.</param>
	/// <param name="eventData">The event data.</param>
	/// <param name="fromVersion">The current version of the event.</param>
	/// <param name="toVersion">The target version.</param>
	/// <returns>The upgraded event.</returns>
	/// <exception cref="ArgumentException">Thrown when the event type is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when eventData is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when no upgraders are registered for the event type or when no upgrade path is found.</exception>
	public object UpgradeEvent(string eventType, object eventData, int fromVersion, int toVersion)
	{
		if (string.IsNullOrEmpty(eventType))
		{
			throw new ArgumentException(
					Resources.EventVersionManager_EventTypeCannotBeNullOrEmpty,
					nameof(eventType));
		}

		ArgumentNullException.ThrowIfNull(eventData);

		if (fromVersion == toVersion)
		{
			return eventData;
		}

		if (!_upgraders.TryGetValue(eventType, out var upgraderList))
		{
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.CurrentCulture,
							NoUpgradersRegisteredFormat,
							eventType));
		}

		// Find upgrade path using BFS
		var upgradePath = FindUpgradePath(upgraderList, fromVersion, toVersion);
		if (upgradePath == null || upgradePath.Count == 0)
		{
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.CurrentCulture,
							NoUpgradePathFoundFormat,
							eventType,
							fromVersion,
							toVersion));
		}

		// Apply upgrades in sequence
		var currentEvent = eventData;
		foreach (var upgrader in upgradePath)
		{
			LogEventUpgrading(eventType, upgrader.FromVersion, upgrader.ToVersion);
			currentEvent = upgrader.Upgrade(currentEvent);
		}

		return currentEvent;
	}

	/// <summary>
	/// Gets all registered event types.
	/// </summary>
	/// <returns>A collection of registered event types.</returns>
	public IEnumerable<string> GetRegisteredEventTypes() => _upgraders.Keys;

	/// <summary>
	/// Gets all upgraders for a specific event type.
	/// </summary>
	/// <param name="eventType">The event type.</param>
	/// <returns>A collection of upgraders for the event type.</returns>
	public IEnumerable<IEventUpgrader> GetUpgradersForEventType(string eventType) =>
		_upgraders.TryGetValue(eventType, out var upgraders)
			? upgraders
			: [];

	/// <summary>
	/// Finds the shortest upgrade path between versions using BFS.
	/// </summary>
	/// <param name="upgraders">The list of available event upgraders.</param>
	/// <param name="fromVersion">The starting version.</param>
	/// <param name="toVersion">The target version.</param>
	/// <returns>A list of upgraders representing the shortest path, or null if no path exists.</returns>
	private static List<IEventUpgrader>? FindUpgradePath(
		List<IEventUpgrader> upgraders,
		int fromVersion,
		int toVersion)
	{
		// Use BFS to find shortest path
		var queue = new Queue<(int Version, List<IEventUpgrader> Path)>();
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

			// Find all possible next steps. Order candidates by ToVersion for a canonical, registration-order-
			// independent tie-break: when two equal-length upgrade paths exist, the chosen chain must not depend
			// on DI registration order.
			foreach (var upgrader in upgraders.Where(u => u.FromVersion == currentVersion).OrderBy(u => u.ToVersion))
			{
				if (!visited.Contains(upgrader.ToVersion))
				{
					_ = visited.Add(upgrader.ToVersion);
					var newPath = new List<IEventUpgrader>(currentPath) { upgrader };
					queue.Enqueue((upgrader.ToVersion, newPath));
				}
			}
		}

		return null;
	}
}
