// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Static helper for easy buffer management.
/// </summary>
public static class BufferManager
{
	private static readonly BufferPool Instance = BufferPool.Default;

	/// <summary>
	/// Rents a byte array buffer of at least the specified minimum size from the buffer pool.
	/// </summary>
	/// <param name="minimumSize"> The minimum required size of the buffer. </param>
	/// <returns> A rented byte array buffer that meets or exceeds the minimum size. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] Rent(int minimumSize) => Instance.Rent(minimumSize);

	/// <summary>
	/// Returns a previously rented buffer back to the buffer pool for reuse.
	/// </summary>
	/// <param name="buffer"> The buffer to return to the pool. </param>
	/// <param name="clearBuffer"> Whether to clear the buffer contents before returning to pool. </param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Return(byte[] buffer, bool clearBuffer = false) => Instance.Return(buffer, clearBuffer);

	/// <summary>
	/// Rents a managed buffer wrapper that automatically returns the buffer when disposed.
	/// </summary>
	/// <param name="size"> The required size of the buffer. </param>
	/// <returns> A managed buffer wrapper that implements <see cref="IDisposable" />. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static RentedBuffer RentBuffer(int size) => Instance.RentBuffer(size);
}
