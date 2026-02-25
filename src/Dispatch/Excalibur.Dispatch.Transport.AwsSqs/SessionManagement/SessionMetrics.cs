// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Metrics for a session.
/// </summary>
public sealed class SessionMetrics
{
	/// <summary>
	/// Gets or sets the total processing time.
	/// </summary>
	/// <value>
	/// The total processing time.
	/// </value>
	public TimeSpan TotalProcessingTime { get; set; }

	/// <summary>
	/// Gets or sets the average message processing time.
	/// </summary>
	/// <value>
	/// The average message processing time.
	/// </value>
	public TimeSpan AverageMessageProcessingTime { get; set; }

	/// <summary>
	/// Gets or sets the number of successful messages.
	/// </summary>
	/// <value>
	/// The number of successful messages.
	/// </value>
	public int SuccessfulMessages { get; set; }

	/// <summary>
	/// Gets or sets the number of failed messages.
	/// </summary>
	/// <value>
	/// The number of failed messages.
	/// </value>
	public int FailedMessages { get; set; }

	/// <summary>
	/// Gets or sets the number of retried messages.
	/// </summary>
	/// <value>
	/// The number of retried messages.
	/// </value>
	public int RetriedMessages { get; set; }

	/// <summary>
	/// Gets or sets the number of lock renewals.
	/// </summary>
	/// <value>
	/// The number of lock renewals.
	/// </value>
	public int LockRenewals { get; set; }
}
