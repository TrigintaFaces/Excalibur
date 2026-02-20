// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// A generic value holder that is aligned to prevent false sharing.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
public readonly struct CacheAlignedValue<T> : IEquatable<CacheAlignedValue<T>>
	where T : struct
{
	/// <summary>
	/// Gets or sets the value with volatile semantics.
	/// </summary>
	/// <value>
	/// The value with volatile semantics.
	/// </value>
	[field: FieldOffset(0)]
	public T Value
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		init;
	}

	/// <summary>
	/// Creates a new cache-aligned value.
	/// </summary>
	/// <param name="initialValue"></param>
	/// <returns></returns>
	public static CacheAlignedValue<T> Create(T initialValue = default)
	{
		var aligned = new CacheAlignedValue<T> { Value = initialValue };
		return aligned;
	}

	/// <summary>
	/// Determines whether the specified value is equal to the current value.
	/// </summary>
	/// <param name="other"> The value to compare with the current value. </param>
	/// <returns> true if the specified value is equal to the current value; otherwise, false. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Equals(CacheAlignedValue<T> other) => EqualityComparer<T>.Default.Equals(Value, other.Value);

	/// <summary>
	/// Determines whether the specified object is equal to the current value.
	/// </summary>
	/// <param name="obj"> The object to compare with the current value. </param>
	/// <returns> true if the specified object is equal to the current value; otherwise, false. </returns>
	public override readonly bool Equals(object? obj) => obj is CacheAlignedValue<T> other && Equals(other);

	/// <summary>
	/// Returns the hash code for this value.
	/// </summary>
	/// <returns> A hash code for the current value. </returns>
	public override readonly int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Value);

	/// <summary>
	/// Determines whether two values are equal.
	/// </summary>
	/// <param name="left"> The first value to compare. </param>
	/// <param name="right"> The second value to compare. </param>
	/// <returns> true if the values are equal; otherwise, false. </returns>
	public static bool operator ==(CacheAlignedValue<T> left, CacheAlignedValue<T> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two values are not equal.
	/// </summary>
	/// <param name="left"> The first value to compare. </param>
	/// <param name="right"> The second value to compare. </param>
	/// <returns> true if the values are not equal; otherwise, false. </returns>
	public static bool operator !=(CacheAlignedValue<T> left, CacheAlignedValue<T> right) => !left.Equals(right);
}
