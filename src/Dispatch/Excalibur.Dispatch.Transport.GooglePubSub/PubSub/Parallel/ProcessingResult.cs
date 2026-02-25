// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Result of parallel message processing.
/// </summary>
public sealed class ProcessingResult
{
	/// <summary>
	/// Gets a value indicating whether gets or sets whether processing was successful.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether processing was successful.
	/// </value>
	public bool Success { get; init; }

	/// <summary>
	/// Gets the ID of the worker that processed the message.
	/// </summary>
	/// <value>
	/// The ID of the worker that processed the message.
	/// </value>
	public int WorkerId { get; init; }

	/// <summary>
	/// Gets the processing duration.
	/// </summary>
	/// <value>
	/// The processing duration.
	/// </value>
	public TimeSpan ProcessingTime { get; init; }
}
