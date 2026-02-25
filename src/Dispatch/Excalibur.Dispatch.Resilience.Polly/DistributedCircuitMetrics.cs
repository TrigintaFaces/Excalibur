// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Distributed circuit metrics stored in cache.
/// </summary>
internal sealed class DistributedCircuitMetrics
{
	/// <summary>
	/// Gets or sets the total number of successful operations.
	/// </summary>
	/// <value>The total count of successful operations.</value>
	public long SuccessCount { get; set; }

	/// <summary>
	/// Gets or sets the total number of failed operations.
	/// </summary>
	/// <value>The total count of failed operations.</value>
	public long FailureCount { get; set; }

	/// <summary>
	/// Gets or sets the current count of consecutive failures.
	/// </summary>
	/// <value>The number of consecutive failures without an intervening success.</value>
	public int ConsecutiveFailures { get; set; }

	/// <summary>
	/// Gets or sets the current count of consecutive successes.
	/// </summary>
	/// <value>The number of consecutive successes without an intervening failure.</value>
	public int ConsecutiveSuccesses { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last successful operation.
	/// </summary>
	/// <value>The timestamp when the last successful operation completed.</value>
	public DateTimeOffset LastSuccess { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last failed operation.
	/// </summary>
	/// <value>The timestamp when the last failed operation occurred.</value>
	public DateTimeOffset LastFailure { get; set; }

	/// <summary>
	/// Gets or sets the reason for the last failure.
	/// </summary>
	/// <value>A description of why the last operation failed.</value>
	public string LastFailureReason { get; set; } = string.Empty;
}
