// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Result of synchronous dispatch operation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct DispatchResult : IEquatable<DispatchResult>
{
	/// <summary>
	/// Indicates whether the dispatch operation completed successfully.
	/// </summary>
	public readonly bool Success;

	/// <summary>
	/// The zero-based index of the handler that failed, or -1 if no specific handler failed.
	/// </summary>
	public readonly int FailedHandlerIndex;

	/// <summary>
	/// An application-specific error code indicating the type of failure that occurred.
	/// </summary>
	public readonly int ErrorCode;

	private DispatchResult(bool success, int failedHandlerIndex, int errorCode)
	{
		Success = success;
		FailedHandlerIndex = failedHandlerIndex;
		ErrorCode = errorCode;
	}

	/// <summary>
	/// A pre-allocated successful dispatch result instance for performance optimization.
	/// </summary>
	public static readonly DispatchResult SuccessResult = new(success: true, -1, 0);

	/// <summary>
	/// A pre-allocated exception result instance used when an unhandled exception is thrown.
	/// </summary>
	public static readonly DispatchResult ExceptionThrown = new(success: false, -1, -1);

	/// <summary>
	/// Creates a failed dispatch result for a specific handler with an associated error code.
	/// </summary>
	/// <param name="handlerIndex"> The zero-based index of the handler that failed. </param>
	/// <param name="errorCode"> An application-specific error code for the failure. </param>
	/// <returns> A dispatch result indicating handler failure with context information. </returns>
	public static DispatchResult HandlerFailed(int handlerIndex, int errorCode) =>
		new(success: false, handlerIndex, errorCode);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	/// <param name="other"> The result to compare with the current result. </param>
	/// <returns> true if the specified result is equal to the current result; otherwise, false. </returns>
	public bool Equals(DispatchResult other) =>
		Success == other.Success && FailedHandlerIndex == other.FailedHandlerIndex && ErrorCode == other.ErrorCode;

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	/// <param name="obj"> The object to compare with the current result. </param>
	/// <returns> true if the specified object is equal to the current result; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is DispatchResult other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	/// <returns> A hash code for the current result. </returns>
	public override int GetHashCode() => HashCode.Combine(Success, FailedHandlerIndex, ErrorCode);

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are equal; otherwise, false. </returns>
	public static bool operator ==(DispatchResult left, DispatchResult right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are not equal; otherwise, false. </returns>
	public static bool operator !=(DispatchResult left, DispatchResult right) => !left.Equals(right);
}
