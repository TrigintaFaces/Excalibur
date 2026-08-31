// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Serialization.Tests;

/// <summary>
/// Binds the lifetime contract of <see cref="PooledSerializationResult"/>: a copy whose buffer has been
/// returned to the pool fails loudly rather than reading whatever the pool handed out next.
/// </summary>
/// <remarks>
/// <para>
/// The type is a <c>readonly struct</c> over a pooled array, so it is trivially copyable and every copy
/// shares one buffer. That makes a late read a real possibility: a consumer stores a copy, another copy
/// is disposed, and the array is back in the pool and possibly rented to someone else. The only safe
/// behaviours are "throws" or "still valid"; the one behaviour that must never happen is a silent read
/// of another renter's bytes, because the caller cannot detect it.
/// </para>
/// <para>
/// Both arms are needed. The throwing arm alone is satisfied by accessors that throw always, which is a
/// type that cannot be used. The liveness arm establishes that a copy taken before disposal reads the
/// same bytes as the original, so the throw is specifically about lifetime and not about copying.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class PooledSerializationResultLifetimeShould
{
	[Fact]
	[RequiresUnreferencedCode("Exercises the generic JSON serialization path")]
	[RequiresDynamicCode("Exercises the generic JSON serialization path")]
	public void ReadTheSameBytesFromACopyWhileTheBufferIsStillHeld()
	{
		var serializer = new DispatchJsonSerializer();

		using var result = serializer.SerializeToPooledBuffer(new Payload { Value = "still-held" });
		var copy = result;

		copy.Length.ShouldBe(result.Length);
		copy.WrittenMemory.ToArray().ShouldBe(
			result.WrittenMemory.ToArray(),
			"liveness: a copy taken while the buffer is held is a usable view of the same data -- if this "
			+ "fails, the arm below is not measuring lifetime");
	}

	[Fact]
	[RequiresUnreferencedCode("Exercises the generic JSON serialization path")]
	[RequiresDynamicCode("Exercises the generic JSON serialization path")]
	public void ThrowFromACopyOnceASiblingCopyHasBeenDisposed()
	{
		var serializer = new DispatchJsonSerializer();

		var result = serializer.SerializeToPooledBuffer(new Payload { Value = "returned-to-the-pool" });
		var copy = result;

		// Disposing either copy returns the one shared buffer to the pool, so the surviving copy is now
		// pointing at an array the pool is free to hand to somebody else.
		result.Dispose();

		_ = Should.Throw<ObjectDisposedException>(() => copy.WrittenMemory.Length);
		_ = Should.Throw<ObjectDisposedException>(() => CopySpanLength(copy));
		_ = Should.Throw<ObjectDisposedException>(() => copy.CopyTo(new ArrayBufferWriter<byte>()));

		copy.Length.ShouldBe(
			result.Length,
			"the written length is a value on the struct, not a read through the buffer, so it stays "
			+ "readable -- the documented contract says exactly this");
	}

	[Fact]
	[RequiresUnreferencedCode("Exercises the generic JSON serialization path")]
	[RequiresDynamicCode("Exercises the generic JSON serialization path")]
	public void ReturnTheBufferExactlyOnceWhenEveryCopyIsDisposed()
	{
		var pool = new CountingBufferService();
		var serializer = new DispatchJsonSerializer(bufferManager: pool);

		var result = serializer.SerializeToPooledBuffer(new Payload { Value = "disposed-twice" });
		var copy = result;

		result.Dispose();
		copy.Dispose();

		pool.Returns.ShouldBe(
			1,
			"two copies of one struct share one buffer, so disposing both must return it once -- a second "
			+ "return would hand one array to two renters");
	}

	private static int CopySpanLength(PooledSerializationResult result) => result.WrittenSpan.Length;

	private sealed class Payload
	{
		public string Value { get; init; } = string.Empty;
	}

	private sealed class CountingBufferService : IPooledBufferService
	{
		private int _returns;
		private int _rented;
		private int _largest;

		public int Returns => Volatile.Read(ref _returns);

		public int RentedBuffers => Volatile.Read(ref _rented);

		public int LargestBufferRequested => Volatile.Read(ref _largest);

		public IDisposablePooledBuffer RentBuffer(int minimumLength, bool clearBuffer = false)
		{
			_ = Interlocked.Increment(ref _rented);
			if (minimumLength > Volatile.Read(ref _largest))
			{
				Volatile.Write(ref _largest, minimumLength);
			}

			return new PooledBuffer(this, ArrayPool<byte>.Shared.Rent(minimumLength));
		}

		public void ReturnBuffer(IPooledBuffer buffer, bool clearBuffer = true)
		{
			ArgumentNullException.ThrowIfNull(buffer);

			_ = Interlocked.Increment(ref _returns);
			_ = Interlocked.Decrement(ref _rented);
			ArrayPool<byte>.Shared.Return(((PooledBuffer)buffer).Buffer, clearBuffer);
		}

		public BufferPoolStatistics GetStatistics() => new();
	}
}
