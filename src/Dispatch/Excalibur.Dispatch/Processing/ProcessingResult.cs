// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Result of message processing.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct ProcessingResult(bool success, int responseLength = 0, int errorCode = 0) : IEquatable<ProcessingResult>
{
	/// <summary>
	/// Indicates whether the message processing operation completed successfully.
	/// </summary>
	public readonly bool Success = success;

	/// <summary>
	/// The length of the response data generated during processing, in bytes.
	/// </summary>
	public readonly int ResponseLength = responseLength;

	/// <summary>
	/// An application-specific error code indicating the type of failure that occurred.
	/// </summary>
	public readonly int ErrorCode = errorCode;

	/// <summary>
	/// Creates a successful processing result with an optional response length.
	/// </summary>
	/// <param name="responseLength"> The length of the response data in bytes. </param>
	/// <returns> A processing result indicating successful completion. </returns>
	public static ProcessingResult Ok(int responseLength = 0) => new(success: true, responseLength);

	/// <summary>
	/// Creates a failed processing result with the specified error code.
	/// </summary>
	/// <param name="errorCode"> An application-specific error code for the failure. </param>
	/// <returns> A processing result indicating failure with the specified error code. </returns>
	public static ProcessingResult Error(int errorCode) => new(success: false, 0, errorCode);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	/// <param name="other"> The result to compare with the current result. </param>
	/// <returns> true if the specified result is equal to the current result; otherwise, false. </returns>
	public bool Equals(ProcessingResult other) =>
		Success == other.Success && ResponseLength == other.ResponseLength && ErrorCode == other.ErrorCode;

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	/// <param name="obj"> The object to compare with the current result. </param>
	/// <returns> true if the specified object is equal to the current result; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is ProcessingResult other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	/// <returns> A hash code for the current result. </returns>
	public override int GetHashCode() => HashCode.Combine(Success, ResponseLength, ErrorCode);

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are equal; otherwise, false. </returns>
	public static bool operator ==(ProcessingResult left, ProcessingResult right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are not equal; otherwise, false. </returns>
	public static bool operator !=(ProcessingResult left, ProcessingResult right) => !left.Equals(right);
}
