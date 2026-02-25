// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics for a specific policy.
/// </summary>
public sealed class PolicyStatistic
{
	/// <summary>
	/// Gets or sets the policy key.
	/// </summary>
	/// <value>
	/// The policy key.
	/// </value>
	public string PolicyKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the total attempts.
	/// </summary>
	/// <value>
	/// The total attempts.
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
	/// Gets or sets the average duration.
	/// </summary>
	/// <value>
	/// The average duration.
	/// </value>
	public TimeSpan AverageDuration { get; set; }

	/// <summary>
	/// Gets or sets the success rate.
	/// </summary>
	/// <value>
	/// The success rate.
	/// </value>
	public double SuccessRate { get; set; }
}
