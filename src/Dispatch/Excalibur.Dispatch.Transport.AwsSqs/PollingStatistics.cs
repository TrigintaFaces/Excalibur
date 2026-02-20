// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Polling statistics.
/// </summary>
public sealed class PollingStatistics
{
	/// <summary>
	/// Gets or sets the total polling attempts.
	/// </summary>
	/// <value>
	/// The total polling attempts.
	/// </value>
	public long TotalAttempts { get; set; }

	/// <summary>
	/// Gets or sets the successful attempts.
	/// </summary>
	/// <value>
	/// The successful attempts.
	/// </value>
	public long SuccessfulAttempts { get; set; }

	/// <summary>
	/// Gets or sets the failed attempts.
	/// </summary>
	/// <value>
	/// The failed attempts.
	/// </value>
	public long FailedAttempts { get; set; }

	/// <summary>
	/// Gets or sets the total messages received.
	/// </summary>
	/// <value>
	/// The total messages received.
	/// </value>
	public long TotalMessagesReceived { get; set; }

	/// <summary>
	/// Gets or sets the average messages per poll.
	/// </summary>
	/// <value>
	/// The average messages per poll.
	/// </value>
	public double AverageMessagesPerPoll { get; set; }

	/// <summary>
	/// Gets or sets the average poll duration.
	/// </summary>
	/// <value>
	/// The average poll duration.
	/// </value>
	public TimeSpan AveragePollDuration { get; set; }
}
