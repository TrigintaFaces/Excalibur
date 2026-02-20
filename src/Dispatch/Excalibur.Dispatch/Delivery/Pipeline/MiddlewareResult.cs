// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Result struct for middleware execution.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MiddlewareResult" /> struct. </remarks>
[StructLayout(LayoutKind.Auto)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct MiddlewareResult(bool continueExecution, bool success, string? error = null) : IEquatable<MiddlewareResult>
{
	// R0.8: Make property static - these properties access primary constructor parameters (instance state)
#pragma warning disable MA0041
	/// <summary>
	/// Gets a value indicating whether to continue executing the pipeline.
	/// </summary>
	/// <value>
	/// A value indicating whether to continue executing the pipeline.
	/// </value>
	public bool ContinueExecution
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => continueExecution;
	}

	/// <summary>
	/// Gets a value indicating whether the operation was successful.
	/// </summary>
	/// <value>
	/// A value indicating whether the operation was successful.
	/// </value>
	public bool Success
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => success;
	}

	/// <summary>
	/// Gets the error message if any.
	/// </summary>
	/// <value>
	/// The error message if any.
	/// </value>
	public string? Error
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => error;
	}

#pragma warning restore MA0041

	/// <summary>
	/// Creates a result that continues execution.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MiddlewareResult Continue() => new(continueExecution: true, success: true);

	/// <summary>
	/// Creates a result that stops execution with success.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MiddlewareResult StopWithSuccess() => new(continueExecution: false, success: true);

	/// <summary>
	/// Creates a result that stops execution with an error.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MiddlewareResult StopWithError(string error) => new(continueExecution: false, success: false, error);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(MiddlewareResult other) =>
		ContinueExecution == other.ContinueExecution &&
		Success == other.Success &&
string.Equals(Error, other.Error, StringComparison.Ordinal);

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	public override bool Equals(object? obj) => obj is MiddlewareResult other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	public override int GetHashCode() => HashCode.Combine(ContinueExecution, Success, Error);

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(MiddlewareResult left, MiddlewareResult right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(MiddlewareResult left, MiddlewareResult right) => !left.Equals(right);
}
