// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Versioning;

/// <summary>
/// Resolves the shortest chain of upgraders that carries a payload from one version to another, caching
/// each answer so a replay does not repeat the search for every record it reads.
/// </summary>
/// <typeparam name="TUpgrader"> The upgrader type; a single step from one version to the next. </typeparam>
/// <remarks>
/// <para>
/// This is the single implementation of version-graph traversal in the product. It lives here, in the
/// abstractions assembly, because it is the one assembly both the message-upcasting pipeline and the
/// event-sourcing version managers already reference; it is <see langword="internal" /> and reaches them
/// through friend access, so sharing it costs no public surface and no new assembly dependency. It was
/// previously implemented three separate times, and the copies had drifted in two ways that matter: only
/// one cached its answers, and only one rejected a backward edge at registration. Resolve the duplication
/// by adding a caller here, never by copying the search.
/// </para>
/// <para>
/// <b>Termination is by construction and does not depend on the graph's shape.</b> Every version is
/// admitted to the queue at most once, gated by <c>visited</c>, so the queue strictly drains and the walk
/// cannot diverge even when the registered edges contain a cycle. Do not replace that gate with a check
/// at dequeue time.
/// </para>
/// <para>
/// <b>Cost.</b> The adjacency map is built once per search and each bucket sorted once, so a search is
/// O(E log E + V). Building it per dequeued node instead — by scanning the whole upgrader list and sorting
/// it inside the loop — costs O(V . E log E) for the same answer, which is the shape this type exists to
/// avoid. The chain is reconstructed once from predecessor links rather than copied into every enqueued
/// node, which keeps a k-hop answer O(k) instead of O(k squared).
/// </para>
/// </remarks>
internal sealed class UpgradePathResolver<TUpgrader>(
	Func<TUpgrader, int> fromVersion,
	Func<TUpgrader, int> toVersion)
	where TUpgrader : class
{
	// List identity is the registry generation. Values do not keep an obsolete snapshot alive.
	// Callers publish immutable lists of captured metadata, never mutable consumer properties.
	private readonly ConditionalWeakTable<IReadOnlyList<TUpgrader>, SnapshotMemo> _snapshots = new();

	/// <summary> Releases memoized answers; snapshot identity, not clearing, guarantees correctness. </summary>
	internal void Invalidate() => _snapshots.Clear();

	/// <summary>
	/// Finds the shortest chain from one version to another.
	/// </summary>
	/// <param name="key"> The caller type label. Immutable list identity scopes memoization. </param>
	/// <param name="upgraders"> The upgraders registered for that type. </param>
	/// <param name="from"> The version the payload is at. </param>
	/// <param name="to"> The version the payload is wanted at. </param>
	/// <returns>
	/// The chain to apply in order; an empty chain when the payload is already at the wanted version; or
	/// <see langword="null" /> when no chain exists. An empty chain and an absent one are deliberately
	/// distinguishable, because a caller must be able to tell "nothing to do" from "cannot be done".
	/// </returns>
	internal IReadOnlyList<TUpgrader>? Resolve(
		string key,
		IReadOnlyList<TUpgrader> upgraders,
		int from,
		int to)
	{
		// The supplied list already scopes the type; arbitrary key strings must not grow the memo.
		_ = key;
		if (from == to)
		{
			return [];
		}

		if (!_snapshots.TryGetValue(upgraders, out var memo))
		{
			// Read captured framework metadata before entering the table's value factory.
			var candidate = new SnapshotMemo();
			for (var index = 0; index < upgraders.Count; index++)
			{
				candidate.Versions.Add(fromVersion(upgraders[index]));
				candidate.Versions.Add(toVersion(upgraders[index]));
			}
			memo = _snapshots.GetValue(upgraders, _ => candidate);
		}

		// At most V squared version pairs per memo, including unreachable registered pairs.
		if (!memo.Versions.Contains(from) || !memo.Versions.Contains(to))
		{
			return null;
		}

		return memo.Paths.GetOrAdd((from, to),
			static (pair, state) => state.Resolver.Search(state.Upgraders, pair.From, pair.To),
			(Resolver: this, Upgraders: upgraders));
	}

	private sealed class SnapshotMemo
	{
		public HashSet<int> Versions { get; } = [];
		public ConcurrentDictionary<(int From, int To), IReadOnlyList<TUpgrader>?> Paths { get; } = new();
	}

	private IReadOnlyList<TUpgrader>? Search(IReadOnlyList<TUpgrader> upgraders, int from, int to)
	{
		if (from == to)
		{
			return [];
		}

		var adjacency = BuildAdjacency(upgraders);

		// Predecessor links, not accumulated paths: the chain is walked back once on success.
		var cameFrom = new Dictionary<int, (int Version, TUpgrader Upgrader)>();
		var visited = new HashSet<int> { from };
		var queue = new Queue<int>();
		queue.Enqueue(from);

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();

			if (!adjacency.TryGetValue(current, out var outgoing))
			{
				continue;
			}

			foreach (var upgrader in outgoing)
			{
				var next = toVersion(upgrader);

				// Gating the enqueue on first visit is what makes termination independent of the
				// graph's shape. A cycle is harmless; a second route to an already-reached version is
				// never shorter, because this is a breadth-first walk over unweighted hops.
				if (!visited.Add(next))
				{
					continue;
				}

				cameFrom[next] = (current, upgrader);

				if (next == to)
				{
					return Reconstruct(cameFrom, from, to);
				}

				queue.Enqueue(next);
			}
		}

		return null;
	}

	private Dictionary<int, List<TUpgrader>> BuildAdjacency(IReadOnlyList<TUpgrader> upgraders)
	{
		var adjacency = new Dictionary<int, List<TUpgrader>>();

		for (var index = 0; index < upgraders.Count; index++)
		{
			var upgrader = upgraders[index];
			var origin = fromVersion(upgrader);

			if (!adjacency.TryGetValue(origin, out var bucket))
			{
				bucket = [];
				adjacency[origin] = bucket;
			}

			bucket.Add(upgrader);
		}

		// Ordering each bucket by target version makes the choice between two equally short chains the
		// same on every run, rather than dependent on the order upgraders happened to be registered.
		foreach (var bucket in adjacency.Values)
		{
			if (bucket.Count > 1)
			{
				bucket.Sort((left, right) => toVersion(left).CompareTo(toVersion(right)));
			}
		}

		return adjacency;
	}

	private static IReadOnlyList<TUpgrader> Reconstruct(
		Dictionary<int, (int Version, TUpgrader Upgrader)> cameFrom,
		int from,
		int to)
	{
		var chain = new List<TUpgrader>();

		for (var version = to; version != from;)
		{
			var step = cameFrom[version];
			chain.Add(step.Upgrader);
			version = step.Version;
		}

		chain.Reverse();
		return chain;
	}
}
