// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Jobs.Aws.Diagnostics;

/// <summary>
/// Event IDs for AWS EventBridge Scheduler Job Provider (146200-146299).
/// </summary>
internal static class AwsJobsEventId
{
	/// <summary>AWS EventBridge schedule created successfully.</summary>
	public const int AwsSchedulerScheduleCreated = 146200;

	/// <summary>AWS EventBridge schedule creation failed.</summary>
	public const int AwsSchedulerScheduleCreationFailed = 146201;

	/// <summary>AWS EventBridge schedule deleted successfully.</summary>
	public const int AwsSchedulerScheduleDeleted = 146202;

	/// <summary>AWS EventBridge schedule not found for deletion.</summary>
	public const int AwsSchedulerScheduleNotFound = 146203;

	/// <summary>AWS EventBridge schedule deletion failed.</summary>
	public const int AwsSchedulerScheduleDeletionFailed = 146204;
}
