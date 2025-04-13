namespace Excalibur.Jobs;

/// <summary>
///     Represents the configuration settings for a scheduled job.
/// </summary>
/// <remarks> This class defines the properties required to configure and manage a job, including scheduling, thresholds, and enabling/disabling. </remarks>
public class JobConfig : IJobConfig
{
	/// <inheritdoc />
	public string JobName { get; init; }

	/// <inheritdoc />
	public string JobGroup { get; init; }

	/// <inheritdoc />
	public string CronSchedule { get; init; }

	/// <inheritdoc />
	public TimeSpan DegradedThreshold { get; init; }

	/// <inheritdoc />
	public bool Disabled { get; init; }

	/// <inheritdoc />
	public TimeSpan UnhealthyThreshold { get; init; }
}
