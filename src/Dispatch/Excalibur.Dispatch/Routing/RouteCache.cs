// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Simple LRU cache for routing decisions.
/// </summary>
internal sealed class RouteCache
{
	private readonly int _mask;
	private readonly CacheEntry[] _entries;

	public RouteCache(int capacity)
	{
		// Round up to power of 2
		capacity = RoundUpToPowerOf2(capacity);
		_mask = capacity - 1;
		_entries = new CacheEntry[capacity];

		for (var i = 0; i < _entries.Length; i++)
		{
			_entries[i].RouteId = -1;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetRoute(int key, out int routeId)
	{
		var index = key & _mask;
		ref var entry = ref _entries[index];

		if (entry.RouteId != -1 && entry.Key == key)
		{
			entry.LastAccess = Stopwatch.GetTimestamp();
			routeId = entry.RouteId;
			return true;
		}

		routeId = -1;
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void AddRoute(int key, int routeId)
	{
		var index = key & _mask;
		ref var entry = ref _entries[index];

		entry.Key = key;
		entry.RouteId = routeId;
		entry.LastAccess = Stopwatch.GetTimestamp();
	}

	public void Clear()
	{
		Array.Clear(_entries, 0, _entries.Length);
		for (var i = 0; i < _entries.Length; i++)
		{
			_entries[i].RouteId = -1;
		}
	}

	private static int RoundUpToPowerOf2(int value)
	{
		value--;
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		value++;
		return value;
	}

	[StructLayout(LayoutKind.Explicit, Size = 16)]
	private struct CacheEntry
	{
		[FieldOffset(0)]
		public int Key;

		[FieldOffset(4)]
		public int RouteId;

		[FieldOffset(8)]
		public long LastAccess;
	}
}
