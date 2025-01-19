namespace Excalibur.Jobs;

/// <summary>
///     Represents the configuration settings for a scheduled job.
/// </summary>
/// <remarks> This class defines the properties required to configure and manage a job, including scheduling, thresholds, and enabling/disabling. </remarks>
public class JobConfig : IJobConfig
{
	/// <inheritdoc />
	/// &gt;
	public string JobName { get; init; }

	/// <inheritdoc />
	/// &gt;
	public string JobGroup { get; init; }

	/// <inheritdoc />
	/// &gt;
	public string CronSchedule { get; init; }

	/// <inheritdoc />
	/// &gt;
	public TimeSpan DegradedThreshold { get; init; }

	/// <inheritdoc />
	/// &gt;
	public bool Disabled { get; init; }

	/// <inheritdoc />
	/// &gt;
	public TimeSpan UnhealthyThreshold { get; init; }
}
