// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// A high-performance counter optimized for multi-threaded scenarios through cache line alignment to eliminate false sharing.
/// </summary>
/// <remarks>
/// This counter is designed to maximize performance in high-contention, multi-threaded environments by:
/// <para> <strong> Cache Line Alignment: </strong> </para>
/// The structure is padded to exactly 64 bytes (typical CPU cache line size) to ensure each counter instance occupies its own cache line,
/// preventing false sharing between CPU cores.
/// <para> <strong> False Sharing Prevention: </strong> </para>
/// When multiple threads access different counters stored close in memory, CPU cache coherency protocols can cause unnecessary cache line
/// invalidations. This alignment eliminates such performance degradation in scenarios like per-thread metrics or distributed counters.
/// <para> <strong> Atomic Operations: </strong> </para>
/// All operations use atomic primitives (Interlocked) ensuring thread-safety while maintaining high performance through lock-free algorithms.
/// <para> <strong> Performance Characteristics: </strong> </para>
/// - Zero allocation after creation (value type)
/// - Lock-free operations for maximum concurrency
/// - Minimal memory footprint per instance (64 bytes)
/// - Optimized for high-frequency increment/decrement patterns.
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = CacheLineSize.Size)]
public struct CacheAlignedCounter : IEquatable<CacheAlignedCounter>
{
	[FieldOffset(0)]
	private long _value;

	/// <summary>
	/// Gets the current value of the counter using a volatile read operation.
	/// </summary>
	/// <value>
	/// The current counter value. This property provides a consistent view of the counter state across threads without requiring synchronization.
	/// </value>
	/// <remarks>
	/// The volatile read ensures memory ordering and prevents compiler optimizations that could cache stale values in registers. In
	/// high-contention scenarios, the value may change immediately after reading.
	/// </remarks>
	public long Value
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Volatile.Read(ref _value);
	}

	/// <summary>
	/// Atomically increments the counter by 1 and returns the new value.
	/// </summary>
	/// <returns> The new counter value after incrementing. </returns>
	/// <remarks>
	/// This operation is thread-safe and lock-free, using CPU-level atomic instructions for maximum performance. The operation is
	/// guaranteed to be atomic even under high contention from multiple threads.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Increment() => Interlocked.Increment(ref _value);

	/// <summary>
	/// Atomically decrements the counter by 1 and returns the new value.
	/// </summary>
	/// <returns> The new counter value after decrementing. </returns>
	/// <remarks>
	/// This operation is thread-safe and lock-free, using CPU-level atomic instructions for maximum performance. The operation is
	/// guaranteed to be atomic even under high contention from multiple threads.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Decrement() => Interlocked.Decrement(ref _value);

	/// <summary>
	/// Atomically adds the specified value to the counter and returns the new total.
	/// </summary>
	/// <param name="value"> The value to add to the counter. Can be positive or negative. </param>
	/// <returns> The new counter value after adding the specified value. </returns>
	/// <remarks>
	/// This operation is thread-safe and lock-free, using CPU-level atomic instructions. Negative values will subtract from the counter,
	/// making this method suitable for both increment and decrement operations with arbitrary amounts.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Add(long value) => Interlocked.Add(ref _value, value);

	/// <summary>
	/// Atomically sets the counter to the specified value and returns the previous value.
	/// </summary>
	/// <param name="value"> The new value to set. </param>
	/// <returns> The previous counter value before the exchange. </returns>
	/// <remarks>
	/// This operation atomically replaces the counter value regardless of its current state. It's useful for resetting counters or
	/// implementing lock-free algorithms that require atomic state transitions.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Exchange(long value) => Interlocked.Exchange(ref _value, value);

	/// <summary>
	/// Atomically compares the counter value with a comparand and, if equal, replaces it with a new value.
	/// </summary>
	/// <param name="value"> The value to set if the comparison succeeds. </param>
	/// <param name="comparand"> The value to compare against the current counter value. </param>
	/// <returns> The original counter value before the operation. </returns>
	/// <remarks>
	/// This is the foundation operation for many lock-free algorithms. The exchange only occurs if the current value equals the comparand,
	/// making it safe for implementing conditional updates without race conditions.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long CompareExchange(long value, long comparand) => Interlocked.CompareExchange(ref _value, value, comparand);

	/// <summary>
	/// Atomically resets the counter to zero.
	/// </summary>
	/// <remarks>
	/// This operation is equivalent to <c> Exchange(0) </c> and is thread-safe. Useful for clearing accumulated counts or reinitializing
	/// metrics without creating new instances.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Reset() => _ = Interlocked.Exchange(ref _value, 0);

	/// <summary>
	/// Creates a new cache-aligned counter with the specified initial value.
	/// </summary>
	/// <param name="initialValue"> The initial counter value. Defaults to 0. </param>
	/// <returns> A new <see cref="CacheAlignedCounter" /> instance initialized with the specified value. </returns>
	/// <remarks>
	/// This factory method ensures proper initialization of the cache-aligned structure. Each created counter will occupy its own cache
	/// line to prevent false sharing in multi-threaded scenarios.
	/// </remarks>
	public static CacheAlignedCounter Create(long initialValue = 0)
	{
		var counter = new CacheAlignedCounter { _value = initialValue };
		return counter;
	}

	/// <summary>
	/// Determines whether the specified counter is equal to the current counter.
	/// </summary>
	/// <param name="other"> The counter to compare with the current counter. </param>
	/// <returns> true if the specified counter is equal to the current counter; otherwise, false. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Equals(CacheAlignedCounter other) => _value == other._value;

	/// <summary>
	/// Determines whether the specified object is equal to the current counter.
	/// </summary>
	/// <param name="obj"> The object to compare with the current counter. </param>
	/// <returns> true if the specified object is equal to the current counter; otherwise, false. </returns>
	public override readonly bool Equals(object? obj) => obj is CacheAlignedCounter other && Equals(other);

	/// <summary>
	/// Returns the hash code for this counter.
	/// </summary>
	/// <returns> A hash code for the current counter. </returns>
	public override readonly int GetHashCode() => _value.GetHashCode();

	/// <summary>
	/// Determines whether two counters are equal.
	/// </summary>
	/// <param name="left"> The first counter to compare. </param>
	/// <param name="right"> The second counter to compare. </param>
	/// <returns> true if the counters are equal; otherwise, false. </returns>
	public static bool operator ==(CacheAlignedCounter left, CacheAlignedCounter right) => left.Equals(right);

	/// <summary>
	/// Determines whether two counters are not equal.
	/// </summary>
	/// <param name="left"> The first counter to compare. </param>
	/// <param name="right"> The second counter to compare. </param>
	/// <returns> true if the counters are not equal; otherwise, false. </returns>
	public static bool operator !=(CacheAlignedCounter left, CacheAlignedCounter right) => !left.Equals(right);
}
