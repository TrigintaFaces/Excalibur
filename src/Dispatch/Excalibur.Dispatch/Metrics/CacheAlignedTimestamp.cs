// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// High-performance timestamp aligned to cache line.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct CacheAlignedTimestamp : IEquatable<CacheAlignedTimestamp>
{
	[FieldOffset(0)]
	private long _ticks;

	[FieldOffset(8)]
	private long _performanceTimestamp;

	/// <summary>
	/// Gets the timestamp value.
	/// </summary>
	/// <value>
	/// The timestamp value.
	/// </value>
	public long Ticks
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Volatile.Read(ref _ticks);
	}

	/// <summary>
	/// Gets the performance counter timestamp.
	/// </summary>
	/// <value>
	/// The performance counter timestamp.
	/// </value>
	public long PerformanceTimestamp
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Volatile.Read(ref _performanceTimestamp);
	}

	/// <summary>
	/// Gets the timestamp as DateTime.
	/// </summary>
	/// <value>
	/// The timestamp as DateTime.
	/// </value>
	public readonly DateTime DateTime
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => new(Ticks);
	}

	/// <summary>
	/// Updates the timestamp to current time.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateNow()
	{
		var perfTimestamp = ValueStopwatch.GetTimestamp();
		_ = Interlocked.Exchange(ref _performanceTimestamp, perfTimestamp);

		// Convert performance counter to ticks for compatibility
		var elapsedTicks = (long)(perfTimestamp * 10_000_000.0 / ValueStopwatch.GetFrequency());
		var baseDateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var currentTicks = baseDateTime.Ticks + elapsedTicks;
		_ = Interlocked.Exchange(ref _ticks, currentTicks);
	}

	/// <summary>
	/// Updates the timestamp with high-resolution timer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateHighResolution()
	{
		var perfTimestamp = ValueStopwatch.GetTimestamp();
		_ = Interlocked.Exchange(ref _performanceTimestamp, perfTimestamp);

		// Convert performance counter to ticks for compatibility
		var elapsedTicks = (long)(perfTimestamp * 10_000_000.0 / ValueStopwatch.GetFrequency());
		var baseDateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var currentTicks = baseDateTime.Ticks + elapsedTicks;
		_ = Interlocked.Exchange(ref _ticks, currentTicks);
	}

	/// <summary>
	/// Gets elapsed milliseconds since this timestamp using high-resolution timer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly double GetElapsedMilliseconds()
	{
		var currentTimestamp = ValueStopwatch.GetTimestamp();
		var elapsed = currentTimestamp - _performanceTimestamp;
		return elapsed * 1000.0 / ValueStopwatch.GetFrequency();
	}

	/// <summary>
	/// Creates a new timestamp with current time.
	/// </summary>
	public static CacheAlignedTimestamp Now()
	{
		var timestamp = default(CacheAlignedTimestamp);
		timestamp.UpdateNow();
		return timestamp;
	}

	/// <summary>
	/// Determines whether the specified timestamp is equal to the current timestamp.
	/// </summary>
	/// <param name="other"> The timestamp to compare with the current timestamp. </param>
	/// <returns> true if the specified timestamp is equal to the current timestamp; otherwise, false. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Equals(CacheAlignedTimestamp other) => _ticks == other._ticks && _performanceTimestamp == other._performanceTimestamp;

	/// <summary>
	/// Determines whether the specified object is equal to the current timestamp.
	/// </summary>
	/// <param name="obj"> The object to compare with the current timestamp. </param>
	/// <returns> true if the specified object is equal to the current timestamp; otherwise, false. </returns>
	public override readonly bool Equals(object? obj) => obj is CacheAlignedTimestamp other && Equals(other);

	/// <summary>
	/// Returns the hash code for this timestamp.
	/// </summary>
	/// <returns> A hash code for the current timestamp. </returns>
	public override readonly int GetHashCode() => HashCode.Combine(_ticks, _performanceTimestamp);

	/// <summary>
	/// Determines whether two timestamps are equal.
	/// </summary>
	/// <param name="left"> The first timestamp to compare. </param>
	/// <param name="right"> The second timestamp to compare. </param>
	/// <returns> true if the timestamps are equal; otherwise, false. </returns>
	public static bool operator ==(CacheAlignedTimestamp left, CacheAlignedTimestamp right) => left.Equals(right);

	/// <summary>
	/// Determines whether two timestamps are not equal.
	/// </summary>
	/// <param name="left"> The first timestamp to compare. </param>
	/// <param name="right"> The second timestamp to compare. </param>
	/// <returns> true if the timestamps are not equal; otherwise, false. </returns>
	public static bool operator !=(CacheAlignedTimestamp left, CacheAlignedTimestamp right) => !left.Equals(right);
}
