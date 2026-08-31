// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.EventSourcing.Upcasting;

/// <summary>
/// Resolves the shortest chain of upgraders that carries a payload from one version to another, caching
/// each answer so a replay does not repeat the search for every record it reads.
/// </summary>
/// <typeparam name="TUpgrader"> The upgrader type; a single step from one version to the next. </typeparam>
/// <remarks>
/// The search is a breadth-first walk over the version graph, so the chain returned always has the fewest
/// hops. Candidates are ordered by target version, which makes the choice between two equally short chains
/// deterministic rather than dependent on the order upgraders happened to be registered.
/// </remarks>
internal sealed class UpgradePathResolver<TUpgrader>(
	Func<TUpgrader, int> fromVersion,
	Func<TUpgrader, int> toVersion)
	where TUpgrader : class
{
	/// <summary>
	/// Caches both found and absent paths. A miss is as worth caching as a hit: an unreachable target is
	/// re-requested for every record of that version, and the search cost is identical.
	/// </summary>
	private readonly ConcurrentDictionary<(string Key, int From, int To), IReadOnlyList<TUpgrader>?> _cache =
		new();

	/// <summary>
	/// Discards every cached path. Called when the set of upgraders changes, since a newly registered
	/// upgrader can shorten — or newly complete — a chain already answered.
	/// </summary>
	internal void Invalidate() => _cache.Clear();

	/// <summary>
	/// Finds the shortest chain from one version to another.
	/// </summary>
	/// <param name="key"> The type the upgraders apply to, used to scope the cache. </param>
	/// <param name="upgraders"> The upgraders registered for that type. </param>
	/// <param name="from"> The version the payload is at. </param>
	/// <param name="to"> The version the payload is wanted at. </param>
	/// <returns> The chain to apply in order, or <see langword="null" /> when no chain exists. </returns>
	internal IReadOnlyList<TUpgrader>? Resolve(
		string key,
		IReadOnlyList<TUpgrader> upgraders,
		int from,
		int to) =>
		_cache.GetOrAdd((key, from, to), _ => Search(upgraders, from, to));

	private IReadOnlyList<TUpgrader>? Search(IReadOnlyList<TUpgrader> upgraders, int from, int to)
	{
		var queue = new Queue<(int Version, List<TUpgrader> Path)>();
		var visited = new HashSet<int> { from };

		queue.Enqueue((from, []));

		while (queue.Count > 0)
		{
			var (currentVersion, currentPath) = queue.Dequeue();

			if (currentVersion == to)
			{
				return currentPath;
			}

			foreach (var upgrader in upgraders
				.Where(u => fromVersion(u) == currentVersion)
				.OrderBy(toVersion))
			{
				if (visited.Add(toVersion(upgrader)))
				{
					queue.Enqueue((toVersion(upgrader), [.. currentPath, upgrader]));
				}
			}
		}

		return null;
	}
}
