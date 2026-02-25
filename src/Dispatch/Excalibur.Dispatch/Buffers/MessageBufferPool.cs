// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Specialized buffer pool for message serialization and deserialization operations.
/// </summary>
/// <remarks> Initializes a new instance of MessageBufferPool. </remarks>
/// <param name="bufferManager"> The underlying buffer manager. </param>
/// <param name="encoding"> The text encoding to use (defaults to UTF-8). </param>
public sealed class MessageBufferPool(
	IPooledBufferService? bufferManager = null,
	Encoding? encoding = null)
{
	/// <summary>
	/// Gets the underlying buffer manager.
	/// </summary>
	/// <value>The current <see cref="BufferManager"/> value.</value>
	public IPooledBufferService BufferManager { get; } =
		bufferManager ?? new PooledBufferService(useShared: true, clearBuffersByDefault: true);

	/// <summary>
	/// Gets the text encoding used by this pool.
	/// </summary>
	/// <value>The current <see cref="Encoding"/> value.</value>
	public Encoding Encoding { get; } = encoding ?? Encoding.UTF8;

	/// <summary>
	/// Rents a buffer suitable for small messages.
	/// </summary>
	public IPooledBuffer RentSmallMessageBuffer() =>
		BufferManager.RentBuffer(DefaultSizes.SmallMessage);

	/// <summary>
	/// Rents a buffer suitable for medium messages.
	/// </summary>
	public IPooledBuffer RentMediumMessageBuffer() =>
		BufferManager.RentBuffer(DefaultSizes.MediumMessage);

	/// <summary>
	/// Rents a buffer suitable for large messages.
	/// </summary>
	public IPooledBuffer RentLargeMessageBuffer() =>
		BufferManager.RentBuffer(DefaultSizes.LargeMessage);

	/// <summary>
	/// Rents a buffer suitable for JSON serialization.
	/// </summary>
	public IPooledBuffer RentJsonBuffer() =>
		BufferManager.RentBuffer(DefaultSizes.JsonBuffer, clearBuffer: true);

	/// <summary>
	/// Rents a buffer suitable for message headers.
	/// </summary>
	public IPooledBuffer RentHeaderBuffer() =>
		BufferManager.RentBuffer(DefaultSizes.HeaderBuffer, clearBuffer: true);

	/// <summary>
	/// Estimates the required buffer size based on string length.
	/// </summary>
	public int EstimateBufferSize(string content)
	{
		if (string.IsNullOrEmpty(content))
		{
			return DefaultSizes.SmallMessage;
		}

		// Estimate based on UTF-8 encoding (1-4 bytes per char)
		var estimatedSize = Encoding.GetByteCount(content);

		if (estimatedSize <= DefaultSizes.SmallMessage)
		{
			return DefaultSizes.SmallMessage;
		}

		if (estimatedSize <= DefaultSizes.MediumMessage)
		{
			return DefaultSizes.MediumMessage;
		}

		if (estimatedSize <= DefaultSizes.LargeMessage)
		{
			return DefaultSizes.LargeMessage;
		}

		// Round up to nearest power of 2 for very large messages
		return GetNextPowerOfTwo(estimatedSize);
	}

	private static int GetNextPowerOfTwo(int value)
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

	/// <summary>
	/// Default buffer sizes for different message operations.
	/// </summary>
	internal static class DefaultSizes
	{
		/// <summary>
		/// Default size for small messages (1 KB).
		/// </summary>
		public const int SmallMessage = 1024;

		/// <summary>
		/// Default size for medium messages (4 KB).
		/// </summary>
		public const int MediumMessage = 4096;

		/// <summary>
		/// Default size for large messages (16 KB).
		/// </summary>
		public const int LargeMessage = 16384;

		/// <summary>
		/// Default size for very large messages (64 KB).
		/// </summary>
		public const int VeryLargeMessage = 65536;

		/// <summary>
		/// Default size for JSON serialization buffers (4 KB).
		/// </summary>
		public const int JsonBuffer = 4096;

		/// <summary>
		/// Default size for header buffers (256 bytes).
		/// </summary>
		public const int HeaderBuffer = 256;
	}
}
