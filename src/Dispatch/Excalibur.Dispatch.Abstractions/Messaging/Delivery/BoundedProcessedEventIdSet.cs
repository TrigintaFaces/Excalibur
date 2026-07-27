// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// An insertion-ordered, capacity-bounded set of strings used for the saga's best-effort recent-event
/// idempotency guard (<see cref="SagaState.ProcessedEventIds"/>).
/// </summary>
/// <remarks>
/// <para>
/// Composes a <see cref="Queue{T}"/> (insertion order) with a <see cref="HashSet{T}"/> (O(1)
/// <see cref="Contains"/>). When <see cref="Add(string)"/> pushes the count past the capacity, the
/// <b>oldest</b> (first-inserted) id is evicted — never an arbitrary hash-order entry. Enumeration is in
/// insertion order, so serializing the set to a JSON string array preserves eviction order across a
/// serialize→deserialize round-trip (deserialization repopulates via <see cref="Add(string)"/> in array
/// order).
/// </para>
/// <para>
/// This is a bounded, in-memory guard, not a durable exactly-once store — durable dedup is the inbox
/// store's responsibility. .NET has no built-in ordered set, so this composes the two BCL primitives.
/// </para>
/// </remarks>
internal sealed class BoundedProcessedEventIdSet : ISet<string>
{
	private readonly int _capacity;
	private readonly HashSet<string> _set;
	private readonly Queue<string> _order;

	/// <summary>
	/// Initializes a new instance of the <see cref="BoundedProcessedEventIdSet"/> class.
	/// </summary>
	/// <param name="capacity">The maximum number of ids retained before oldest-eviction begins.</param>
	public BoundedProcessedEventIdSet(int capacity)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
		_capacity = capacity;
		_set = new HashSet<string>(StringComparer.Ordinal);
		_order = new Queue<string>();
	}

	/// <inheritdoc />
	public int Count => _set.Count;

	/// <inheritdoc />
	public bool IsReadOnly => false;

	/// <summary>
	/// Adds an id. Returns <see langword="false"/> if already present. When adding pushes the count past the
	/// capacity, the oldest id is evicted first (FIFO) — the only entry an over-capacity add can remove.
	/// </summary>
	public bool Add(string item)
	{
		ArgumentNullException.ThrowIfNull(item);

		if (!_set.Add(item))
		{
			return false;
		}

		_order.Enqueue(item);

		if (_set.Count > _capacity)
		{
			// Evict the oldest still-present id. Skip any stale queue entries left by a prior Remove.
			while (_order.Count > 0)
			{
				var oldest = _order.Dequeue();
				if (_set.Remove(oldest))
				{
					break;
				}
			}
		}

		return true;
	}

	/// <inheritdoc />
	void ICollection<string>.Add(string item) => Add(item);

	/// <inheritdoc />
	public bool Contains(string item) => _set.Contains(item);

	/// <inheritdoc />
	public bool Remove(string item)
	{
		// Rare path (the dedup guard is append-mostly). Rebuild the order queue without the id so
		// enumeration stays clean; the set is authoritative for membership/count.
		if (!_set.Remove(item))
		{
			return false;
		}

		var retained = _order.Where(id => _set.Contains(id)).ToArray();
		_order.Clear();
		foreach (var id in retained)
		{
			_order.Enqueue(id);
		}

		return true;
	}

	/// <inheritdoc />
	public void Clear()
	{
		_set.Clear();
		_order.Clear();
	}

	/// <inheritdoc />
	public void CopyTo(string[] array, int arrayIndex)
	{
		foreach (var id in this)
		{
			array[arrayIndex++] = id;
		}
	}

	/// <summary>Enumerates the ids in insertion order (oldest first).</summary>
	public IEnumerator<string> GetEnumerator()
	{
		// _order may hold stale entries removed via Remove(); filter to current members, in order.
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var id in _order)
		{
			if (_set.Contains(id) && seen.Add(id))
			{
				yield return id;
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	// Set-algebra operations are not part of the idempotency-guard use case; the guard only Adds,
	// Contains-checks, enumerates, and evicts. They are intentionally unsupported.
	bool ISet<string>.IsProperSubsetOf(IEnumerable<string> other) => throw new NotSupportedException();

	bool ISet<string>.IsProperSupersetOf(IEnumerable<string> other) => throw new NotSupportedException();

	bool ISet<string>.IsSubsetOf(IEnumerable<string> other) => throw new NotSupportedException();

	bool ISet<string>.IsSupersetOf(IEnumerable<string> other) => throw new NotSupportedException();

	bool ISet<string>.Overlaps(IEnumerable<string> other) => throw new NotSupportedException();

	bool ISet<string>.SetEquals(IEnumerable<string> other) => throw new NotSupportedException();

	void ISet<string>.ExceptWith(IEnumerable<string> other) => throw new NotSupportedException();

	void ISet<string>.IntersectWith(IEnumerable<string> other) => throw new NotSupportedException();

	void ISet<string>.SymmetricExceptWith(IEnumerable<string> other) => throw new NotSupportedException();

	void ISet<string>.UnionWith(IEnumerable<string> other) => throw new NotSupportedException();
}
