// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Represents a job execution history entry.
/// </summary>
public sealed class JobExecutionHistory
{
	/// <summary>
	/// Gets or sets the job ID.
	/// </summary>
	/// <value>The current <see cref="JobId"/> value.</value>
	public string JobId { get; set; } = null!;

	/// <summary>
	/// Gets or sets when the execution started.
	/// </summary>
	/// <value>The current <see cref="StartedUtc"/> value.</value>
	public DateTimeOffset StartedUtc { get; set; }

	/// <summary>
	/// Gets or sets when the execution completed.
	/// </summary>
	/// <value>The current <see cref="CompletedUtc"/> value.</value>
	public DateTimeOffset? CompletedUtc { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the execution was successful.
	/// </summary>
	/// <value>The current <see cref="Success"/> value.</value>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets the error message if the execution failed.
	/// </summary>
	/// <value>The current <see cref="Error"/> value.</value>
	public string? Error { get; set; }

	/// <summary>
	/// Gets the execution duration.
	/// </summary>
	/// <value>The current <see cref="Duration"/> value.</value>
	public TimeSpan? Duration => CompletedUtc.HasValue ? CompletedUtc.Value - StartedUtc : null;
}
