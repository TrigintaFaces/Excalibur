// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Buffer pool using ArrayPool&lt;byte&gt; with additional optimizations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="BufferPool" /> class. </remarks>
/// <param name="pool"> The underlying array pool to use. </param>
public sealed class BufferPool(ArrayPool<byte> pool)
{
	private readonly ArrayPool<byte> _pool = pool ?? throw new ArgumentNullException(nameof(pool));

	/// <summary>
	/// Gets the default instance of BufferPool.
	/// </summary>
	/// <value>The default instance of BufferPool.</value>
	public static BufferPool Default { get; } = new(ArrayPool<byte>.Shared);

	/// <summary>
	/// Rents a buffer from the pool.
	/// </summary>
	/// <param name="minimumSize"> The minimum size of the buffer to rent. </param>
	/// <returns> A byte array at least as large as the requested size. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] Rent(int minimumSize)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(minimumSize);

		return _pool.Rent(minimumSize);
	}

	/// <summary>
	/// Returns a buffer to the pool.
	/// </summary>
	/// <param name="buffer"> The buffer to return. </param>
	/// <param name="clearBuffer"> Whether to clear the buffer before returning it. </param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Return(byte[] buffer, bool clearBuffer = false)
	{
		ArgumentNullException.ThrowIfNull(buffer);

		_pool.Return(buffer, clearBuffer);
	}

	/// <summary>
	/// Rents a buffer wrapped in a disposable RentedBuffer structure.
	/// </summary>
	/// <param name="size"> The minimum size of the buffer to rent. </param>
	/// <returns> A RentedBuffer that will automatically return the buffer when disposed. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public RentedBuffer RentBuffer(int size)
	{
		var buffer = Rent(size);
		return new RentedBuffer(buffer, buffer.Length, _pool);
	}
}
