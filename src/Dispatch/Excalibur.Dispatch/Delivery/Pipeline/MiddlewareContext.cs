// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Zero-allocation middleware context that avoids delegate allocations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MiddlewareContext" /> struct. </remarks>
[StructLayout(LayoutKind.Auto)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public struct MiddlewareContext(IDispatchMiddleware[] middleware) : IEquatable<MiddlewareContext>
{
	private readonly int _totalCount = middleware.Length;

	/// <summary>
	/// Gets the current middleware index.
	/// </summary>
	/// <value>
	/// The current middleware index.
	/// </value>
	public int CurrentIndex
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		private set;
	}

	= -1;

	/// <summary>
	/// Gets a value indicating whether there are more middleware to execute.
	/// </summary>
	/// <value>
	/// A value indicating whether there are more middleware to execute.
	/// </value>
	public readonly bool HasNext
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => CurrentIndex < _totalCount - 1;
	}

	/// <summary>
	/// Moves to the next middleware.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public IDispatchMiddleware? MoveNext()
	{
		if (CurrentIndex >= _totalCount - 1)
		{
			return null;
		}

		CurrentIndex++;
		return middleware[CurrentIndex];
	}

	/// <summary>
	/// Resets the context to the beginning.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Reset() => CurrentIndex = -1;

	/// <summary>
	/// Determines whether the specified context is equal to the current context.
	/// </summary>
	public readonly bool Equals(MiddlewareContext other) =>
		_totalCount == other._totalCount &&
		CurrentIndex == other.CurrentIndex;

	/// <summary>
	/// Determines whether the specified object is equal to the current context.
	/// </summary>
	public override readonly bool Equals(object? obj) => obj is MiddlewareContext other && Equals(other);

	/// <summary>
	/// Returns the hash code for this context.
	/// </summary>
	public override readonly int GetHashCode() => HashCode.Combine(_totalCount, CurrentIndex);

	/// <summary>
	/// Determines whether two contexts are equal.
	/// </summary>
	public static bool operator ==(MiddlewareContext left, MiddlewareContext right) => left.Equals(right);

	/// <summary>
	/// Determines whether two contexts are not equal.
	/// </summary>
	public static bool operator !=(MiddlewareContext left, MiddlewareContext right) => !left.Equals(right);
}
