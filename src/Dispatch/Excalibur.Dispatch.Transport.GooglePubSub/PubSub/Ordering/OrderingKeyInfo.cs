// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Information about an ordering key.
/// </summary>
public sealed class OrderingKeyInfo
{
	/// <summary>
	/// Gets the ordering key.
	/// </summary>
	/// <value>
	/// The ordering key.
	/// </value>
	public required string OrderingKey { get; init; }

	/// <summary>
	/// Gets the total message count.
	/// </summary>
	/// <value>
	/// The total message count.
	/// </value>
	public long MessageCount { get; init; }

	/// <summary>
	/// Gets the last sequence number.
	/// </summary>
	/// <value>
	/// The last sequence number.
	/// </value>
	public long LastSequence { get; init; }

	/// <summary>
	/// Gets the expected next sequence number.
	/// </summary>
	/// <value>
	/// The expected next sequence number.
	/// </value>
	public long ExpectedSequence { get; init; }

	/// <summary>
	/// Gets a value indicating whether gets or sets whether the ordering key is in a failed state.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether the ordering key is in a failed state.
	/// </value>
	public bool IsFailed { get; init; }

	/// <summary>
	/// Gets the failure reason.
	/// </summary>
	/// <value>
	/// The failure reason.
	/// </value>
	public string? FailureReason { get; init; }

	/// <summary>
	/// Gets the last activity time.
	/// </summary>
	/// <value>
	/// The last activity time.
	/// </value>
	public DateTimeOffset LastActivity { get; init; }

	/// <summary>
	/// Gets the out-of-sequence message count.
	/// </summary>
	/// <value>
	/// The out-of-sequence message count.
	/// </value>
	public long OutOfSequenceCount { get; init; }
}
