// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Represents a recurring job scheduled with a cron expression.
/// </summary>
public sealed class RecurringCronJob
{
	/// <summary>
	/// Gets or sets the unique identifier for this job.
	/// </summary>
	/// <value>
	/// The unique identifier for this job.
	/// </value>
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the job name for display purposes.
	/// </summary>
	/// <value>The current <see cref="Name"/> value.</value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the job description.
	/// </summary>
	/// <value>The current <see cref="Description"/> value.</value>
	public string? Description { get; set; }

	/// <summary>
	/// Gets or sets the cron expression for this job.
	/// </summary>
	/// <value>The current <see cref="CronExpression"/> value.</value>
	public string CronExpression { get; set; } = null!;

	/// <summary>
	/// Gets or sets the timezone ID for evaluating the cron expression.
	/// </summary>
	/// <value>The current <see cref="TimeZoneId"/> value.</value>
	public string TimeZoneId { get; set; } = TimeZoneInfo.Utc.Id;

	/// <summary>
	/// Gets or sets the message type to dispatch when the job runs.
	/// </summary>
	/// <value>The current <see cref="MessageTypeName"/> value.</value>
	public string MessageTypeName { get; set; } = null!;

	/// <summary>
	/// Gets or sets the serialized message payload.
	/// </summary>
	/// <value>The current <see cref="MessagePayload"/> value.</value>
	public string MessagePayload { get; set; } = null!;

	/// <summary>
	/// Gets or sets metadata associated with this job.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public Dictionary<string, string> Metadata { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether this job is enabled.
	/// </summary>
	/// <value>The current <see cref="IsEnabled"/> value.</value>
	public bool IsEnabled { get; set; } = true;

	/// <summary>
	/// Gets or sets when this job was created.
	/// </summary>
	/// <value>The current <see cref="CreatedUtc"/> value.</value>
	public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets when this job was last modified.
	/// </summary>
	/// <value>The current <see cref="LastModifiedUtc"/> value.</value>
	public DateTimeOffset? LastModifiedUtc { get; set; }

	/// <summary>
	/// Gets or sets when this job last ran successfully.
	/// </summary>
	/// <value>The current <see cref="LastRunUtc"/> value.</value>
	public DateTimeOffset? LastRunUtc { get; set; }

	/// <summary>
	/// Gets or sets when this job is scheduled to run next.
	/// </summary>
	/// <value>The current <see cref="NextRunUtc"/> value.</value>
	public DateTimeOffset? NextRunUtc { get; set; }

	/// <summary>
	/// Gets or sets the number of times this job has run.
	/// </summary>
	/// <value>The current <see cref="RunCount"/> value.</value>
	public long RunCount { get; set; }

	/// <summary>
	/// Gets or sets the number of times this job has failed.
	/// </summary>
	/// <value>The current <see cref="FailureCount"/> value.</value>
	public long FailureCount { get; set; }

	/// <summary>
	/// Gets or sets the last error message if the job failed.
	/// </summary>
	/// <value>The current <see cref="LastError"/> value.</value>
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets tags for categorizing and filtering jobs.
	/// </summary>
	/// <value>The current <see cref="Tags"/> value.</value>
	public HashSet<string> Tags { get; set; } = [];

	/// <summary>
	/// Gets or sets the priority of this job (higher values = higher priority).
	/// </summary>
	/// <value>The current <see cref="Priority"/> value.</value>
	public int Priority { get; set; }

	/// <summary>
	/// Gets or sets the maximum runtime allowed for this job.
	/// </summary>
	/// <value>The current <see cref="MaxRuntime"/> value.</value>
	public TimeSpan? MaxRuntime { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to retry on failure.
	/// </summary>
	/// <value>The current <see cref="RetryOnFailure"/> value.</value>
	public bool RetryOnFailure { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>The current <see cref="MaxRetryAttempts"/> value.</value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets optional start date - job won't run before this date.
	/// </summary>
	/// <value>The current <see cref="StartDate"/> value.</value>
	public DateTimeOffset? StartDate { get; set; }

	/// <summary>
	/// Gets or sets optional end date - job won't run after this date.
	/// </summary>
	/// <value>The current <see cref="EndDate"/> value.</value>
	public DateTimeOffset? EndDate { get; set; }

	/// <summary>
	/// Determines if the job should run at the specified time.
	/// </summary>
	/// <param name="time"> The time to check. </param>
	/// <returns> True if the job should run; otherwise, false. </returns>
	public bool ShouldRunAt(DateTimeOffset time)
	{
		if (!IsEnabled)
		{
			return false;
		}

		if (StartDate.HasValue && time < StartDate.Value)
		{
			return false;
		}

		if (EndDate.HasValue && time > EndDate.Value)
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Updates the job statistics after a run.
	/// </summary>
	/// <param name="success"> Whether the run was successful. </param>
	/// <param name="error"> Error message if the run failed. </param>
	public void UpdateRunStatistics(bool success, string? error = null)
	{
		LastRunUtc = DateTimeOffset.UtcNow;
		RunCount++;

		if (success)
		{
			LastError = null;
		}
		else
		{
			FailureCount++;
			LastError = error;
		}
	}
}
