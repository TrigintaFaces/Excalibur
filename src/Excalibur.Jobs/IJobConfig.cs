namespace Excalibur.Jobs;

/// <summary>
///     Represents the configuration settings for a job, including scheduling, health thresholds, and metadata.
/// </summary>
public interface IJobConfig
{
	/// <summary>
	///     Gets the name of the job.
	/// </summary>
	/// <value> A string representing the job's name. </value>
	string JobName { get; init; }

	/// <summary>
	///     Gets the group name to which the job belongs.
	/// </summary>
	/// <value> A string representing the job group name. </value>
	string JobGroup { get; init; }

	/// <summary>
	///     Gets the cron expression that defines the job's schedule.
	/// </summary>
	/// <value> A string representing the cron schedule. </value>
	/// <remarks>
	///     Cron expressions are used to specify job execution times in a concise format.
	///     Example: "0 0 * * *" for running a job every day at midnight.
	/// </remarks>
	string CronSchedule { get; init; }

	/// <summary>
	///     Gets the threshold duration after which the job's state is considered degraded.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the degraded threshold. </value>
	/// <remarks>
	///     Use this threshold to determine when a job's performance or behavior warrants attention but does not yet indicate failure.
	/// </remarks>
	TimeSpan DegradedThreshold { get; init; }

	/// <summary>
	///     Gets a value indicating whether the job is disabled.
	/// </summary>
	/// <value> <c> true </c> if the job is disabled; otherwise, <c> false </c>. </value>
	/// <remarks> When set to <c> true </c>, the job will not be executed. </remarks>
	bool Disabled { get; init; }

	/// <summary>
	///     Gets the threshold duration after which the job's state is considered unhealthy.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the unhealthy threshold. </value>
	/// <remarks> Use this threshold to determine when a job has failed or is in a critical state requiring immediate action. </remarks>
	TimeSpan UnhealthyThreshold { get; init; }
}
