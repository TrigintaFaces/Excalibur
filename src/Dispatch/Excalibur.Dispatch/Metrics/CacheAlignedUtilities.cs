// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Utilities for cache-aligned operations.
/// </summary>
public static class CacheAlignedUtilities
{
	/// <summary>
	/// Allocates memory aligned to cache line boundaries.
	/// </summary>
	public static IntPtr AllocateAligned(int size)
	{
		const int alignment = CacheLineSize.Size;
		var totalSize = size + alignment - 1;
		var ptr = Marshal.AllocHGlobal(totalSize);
		var aligned = (ptr.ToInt64() + alignment - 1) & ~(alignment - 1);
		return new IntPtr(aligned);
	}

	/// <summary>
	/// Frees aligned memory.
	/// </summary>
	public static void FreeAligned(IntPtr ptr) =>

		// Note: This is simplified. In production, we'd need to track the original pointer.
		Marshal.FreeHGlobal(ptr);

	/// <summary>
	/// Ensures a type is properly padded for cache alignment.
	/// </summary>
	[Conditional("DEBUG")]
	public static void ValidateAlignment<T>()
		where T : struct
	{
		var size = Marshal.SizeOf<T>();
		Debug.Assert(
			size >= CacheLineSize.Size,
			$"Type {typeof(T).Name} is not properly padded for cache alignment. Size: {size}, Required: {CacheLineSize.Size}");
	}
}
