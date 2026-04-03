// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Core;

namespace Excalibur.Jobs.DataProcessing;

/// <summary>
/// Represents the configuration for the Data Processing Job.
/// </summary>
/// <remarks>
/// Inherits from <see cref="JobOptions" /> to include common job configuration properties such as <see cref="JobOptions.CronSchedule" />,
/// <see cref="JobOptions.JobName" />, and <see cref="JobOptions.JobGroup" />. Can be extended in the future to include additional
/// configuration specific to data processing jobs.
/// </remarks>
public sealed class DataProcessingJobOptions : JobOptions;
