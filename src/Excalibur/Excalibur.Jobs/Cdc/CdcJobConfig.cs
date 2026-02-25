// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

using Excalibur.Data.SqlServer.Cdc;
using Excalibur.Jobs.Core;

namespace Excalibur.Jobs.Cdc;

/// <summary>
/// Represents the configuration for the CDC (Change Data Capture) Job.
/// </summary>
/// <remarks>
/// Inherits from <see cref="JobConfig" /> to include common job configuration properties such as <see cref="JobConfig.CronSchedule" />,
/// <see cref="JobConfig.JobName" />, and <see cref="JobConfig.JobGroup" />. Can be extended in the future to include additional
/// configuration specific to CDC (Change Data Capture) jobs.
/// </remarks>
public sealed class CdcJobConfig : JobConfig
{
	/// <summary>
	/// Gets the list of database configurations required for CDC processing.
	/// </summary>
	/// <value>
	/// The list of database configurations required for CDC processing.
	/// </value>
	[Required]
	public required Collection<DatabaseConfig> DatabaseConfigs { get; init; } = [];
}
