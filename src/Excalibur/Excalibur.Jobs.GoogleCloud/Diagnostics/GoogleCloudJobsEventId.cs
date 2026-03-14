// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Jobs.GoogleCloud.Diagnostics;

/// <summary>
/// Event IDs for Google Cloud Scheduler Job Provider (146300-146399).
/// </summary>
internal static class GoogleCloudJobsEventId
{
	/// <summary>Google Cloud Scheduler job created successfully.</summary>
	public const int GoogleCloudSchedulerJobCreated = 146300;

	/// <summary>Google Cloud Scheduler job creation failed.</summary>
	public const int GoogleCloudSchedulerJobCreationFailed = 146301;

	/// <summary>Google Cloud Scheduler job deleted successfully.</summary>
	public const int GoogleCloudSchedulerJobDeleted = 146302;

	/// <summary>Google Cloud Scheduler job not found for deletion.</summary>
	public const int GoogleCloudSchedulerJobNotFound = 146303;

	/// <summary>Google Cloud Scheduler job deletion failed.</summary>
	public const int GoogleCloudSchedulerJobDeletionFailed = 146304;
}
