// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Result of batch dispatch operation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct BatchDispatchResult(int successful, int failed, double totalLatencyUs) : IEquatable<BatchDispatchResult>
{
	/// <summary>
	/// The number of messages that were successfully dispatched in the batch operation.
	/// </summary>
	public readonly int SuccessfulCount = successful;

	/// <summary>
	/// The number of messages that failed to be dispatched in the batch operation.
	/// </summary>
	public readonly int FailedCount = failed;

	/// <summary>
	/// The total latency for all operations in the batch, measured in microseconds.
	/// </summary>
	public readonly double TotalLatencyUs = totalLatencyUs;

	/// <summary>
	/// Gets the average latency per operation in the batch, calculated as total latency divided by total operations.
	/// </summary>
	/// <value>
	/// The average latency per operation in the batch, calculated as total latency divided by total operations.
	/// </value>
	public double AverageLatencyUs => TotalLatencyUs / (SuccessfulCount + FailedCount);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	/// <param name="other"> The result to compare with the current result. </param>
	/// <returns> true if the specified result is equal to the current result; otherwise, false. </returns>
	public bool Equals(BatchDispatchResult other) =>
		SuccessfulCount == other.SuccessfulCount &&
		FailedCount == other.FailedCount &&
		TotalLatencyUs.Equals(other.TotalLatencyUs);

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	/// <param name="obj"> The object to compare with the current result. </param>
	/// <returns> true if the specified object is equal to the current result; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is BatchDispatchResult other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	/// <returns> A hash code for the current result. </returns>
	public override int GetHashCode() => HashCode.Combine(SuccessfulCount, FailedCount, TotalLatencyUs);

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are equal; otherwise, false. </returns>
	public static bool operator ==(BatchDispatchResult left, BatchDispatchResult right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are not equal; otherwise, false. </returns>
	public static bool operator !=(BatchDispatchResult left, BatchDispatchResult right) => !left.Equals(right);
}
