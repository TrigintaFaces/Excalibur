// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics for retry policy execution.
/// </summary>
public sealed class RetryPolicyStatistics
{
	/// <summary>
	/// Gets or sets the total number of retry attempts.
	/// </summary>
	/// <value>
	/// The total number of retry attempts.
	/// </value>
	public long TotalAttempts { get; set; }

	/// <summary>
	/// Gets or sets the number of successful attempts.
	/// </summary>
	/// <value>
	/// The number of successful attempts.
	/// </value>
	public long SuccessfulAttempts { get; set; }

	/// <summary>
	/// Gets or sets the number of failed attempts.
	/// </summary>
	/// <value>
	/// The number of failed attempts.
	/// </value>
	public long FailedAttempts { get; set; }

	/// <summary>
	/// Gets or sets the average retry count per message.
	/// </summary>
	/// <value>
	/// The average retry count per message.
	/// </value>
	public double AverageRetryCount { get; set; }

	/// <summary>
	/// Gets or sets when the statistics were last updated.
	/// </summary>
	/// <value>
	/// When the statistics were last updated.
	/// </value>
	public DateTime LastUpdated { get; set; }

	/// <summary>
	/// Gets or sets the number of active policies.
	/// </summary>
	/// <value>
	/// The number of active policies.
	/// </value>
	public int PolicyCount { get; set; }

	/// <summary>
	/// Gets or sets the total retry attempts (same as TotalAttempts for compatibility).
	/// </summary>
	/// <value>
	/// The total retry attempts (same as TotalAttempts for compatibility).
	/// </value>
	public long TotalRetryAttempts { get; set; }

	/// <summary>
	/// Gets or sets the overall success rate.
	/// </summary>
	/// <value>
	/// The overall success rate.
	/// </value>
	public double SuccessRate { get; set; }

	/// <summary>
	/// Gets or sets per-policy statistics.
	/// </summary>
	/// <value>
	/// Per-policy statistics.
	/// </value>
	public List<PolicyStatistic> PolicyStatistics { get; set; } = [];
}
