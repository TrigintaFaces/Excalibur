// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Result of ordered message processing.
/// </summary>
public sealed class OrderedProcessingResult
{
	/// <summary>
	/// Gets a value indicating whether gets or sets whether processing was successful.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether processing was successful.
	/// </value>
	public bool Success { get; init; }

	/// <summary>
	/// Gets the worker ID that processed the message.
	/// </summary>
	/// <value>
	/// The worker ID that processed the message.
	/// </value>
	public int WorkerId { get; init; }

	/// <summary>
	/// Gets the processing time.
	/// </summary>
	/// <value>
	/// The processing time.
	/// </value>
	public TimeSpan ProcessingTime { get; init; }

	/// <summary>
	/// Gets a value indicating whether gets or sets whether the message was processed with ordering.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether the message was processed with ordering.
	/// </value>
	public bool WasOrdered { get; init; }

	/// <summary>
	/// Gets the time spent in ordering queue.
	/// </summary>
	/// <value>
	/// The time spent in ordering queue.
	/// </value>
	public TimeSpan QueueTime { get; init; }
}
