// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Information about message processing attempts.
/// </summary>
public sealed class MessageProcessingInfo
{
	/// <summary>
	/// Gets or sets the number of processing attempts.
	/// </summary>
	/// <value>The current <see cref="AttemptCount"/> value.</value>
	public int AttemptCount { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the first processing attempt.
	/// </summary>
	/// <value>The current <see cref="FirstAttemptTime"/> value.</value>
	public DateTimeOffset FirstAttemptTime { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the current processing attempt.
	/// </summary>
	/// <value>The current <see cref="CurrentAttemptTime"/> value.</value>
	public DateTimeOffset CurrentAttemptTime { get; set; }

	/// <summary>
	/// Gets or sets the total processing time across all attempts.
	/// </summary>
	/// <value>The current <see cref="TotalProcessingTime"/> value.</value>
	public TimeSpan TotalProcessingTime { get; set; }

	/// <summary>
	/// Gets or sets the processing history.
	/// </summary>
	/// <value>The current <see cref="ProcessingHistory"/> value.</value>
	public Collection<ProcessingAttempt> ProcessingHistory { get; set; } = [];
}
