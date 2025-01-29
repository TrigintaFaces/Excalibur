using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

using Excalibur.DataAccess.SqlServer.Cdc;

namespace Excalibur.Jobs.Quartz.Cdc;

/// <summary>
///     Represents the configuration for the CDC (Change Data Capture) Job.
/// </summary>
/// <remarks>
///     Inherits from <see cref="JobConfig" /> to include common job configuration properties such as <see cref="JobConfig.CronSchedule" />,
///     <see cref="JobConfig.JobName" />, and <see cref="JobConfig.JobGroup" />. Can be extended in the future to include additional
///     configuration specific to CDC (Change Data Capture) jobs.
/// </remarks>
public class CdcJobConfig : JobConfig
{
	/// <summary>
	///     Gets the list of database configurations required for CDC processing.
	/// </summary>
	[Required]
	public required Collection<DatabaseConfig> DatabaseConfigs { get; init; } = [];
}
