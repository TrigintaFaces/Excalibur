// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Result of handler execution.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct HandlerResult : IEquatable<HandlerResult>
{
	/// <summary>
	/// Indicates whether the handler execution completed successfully.
	/// </summary>
	public readonly bool Success;

	/// <summary>
	/// An application-specific error code indicating the type of failure that occurred.
	/// </summary>
	public readonly int ErrorCode;

	/// <summary>
	/// The number of bytes written during handler execution.
	/// </summary>
	public readonly int BytesWritten;

	private HandlerResult(bool success, int errorCode, int bytesWritten)
	{
		Success = success;
		ErrorCode = errorCode;
		BytesWritten = bytesWritten;
	}

	/// <summary>
	/// Creates a successful handler result with an optional count of bytes written.
	/// </summary>
	/// <param name="bytesWritten"> The number of bytes written during successful execution. </param>
	/// <returns> A handler result indicating successful execution. </returns>
	public static HandlerResult Ok(int bytesWritten = 0) => new(success: true, 0, bytesWritten);

	/// <summary>
	/// Creates a failed handler result with the specified error code.
	/// </summary>
	/// <param name="errorCode"> An application-specific error code for the failure. </param>
	/// <returns> A handler result indicating failure with the specified error code. </returns>
	public static HandlerResult Error(int errorCode) => new(success: false, errorCode, 0);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	/// <param name="other"> The result to compare with the current result. </param>
	/// <returns> true if the specified result is equal to the current result; otherwise, false. </returns>
	public bool Equals(HandlerResult other) =>
		Success == other.Success && ErrorCode == other.ErrorCode && BytesWritten == other.BytesWritten;

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	/// <param name="obj"> The object to compare with the current result. </param>
	/// <returns> true if the specified object is equal to the current result; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is HandlerResult other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	/// <returns> A hash code for the current result. </returns>
	public override int GetHashCode() => HashCode.Combine(Success, ErrorCode, BytesWritten);

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are equal; otherwise, false. </returns>
	public static bool operator ==(HandlerResult left, HandlerResult right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are not equal; otherwise, false. </returns>
	public static bool operator !=(HandlerResult left, HandlerResult right) => !left.Equals(right);
}
