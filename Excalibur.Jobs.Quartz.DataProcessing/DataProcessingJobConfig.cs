namespace Excalibur.Jobs.Quartz.DataProcessing;

/// <summary>
///     Represents the configuration for the Data Processing Job.
/// </summary>
/// <remarks>
///     Inherits from <see cref="JobConfig" /> to include common job configuration properties such as <see cref="JobConfig.CronSchedule" />,
///     <see cref="JobConfig.JobName" />, and <see cref="JobConfig.JobGroup" />. Can be extended in the future to include additional
///     configuration specific to data processing jobs.
/// </remarks>
public class DataProcessingJobConfig : JobConfig
{
}
