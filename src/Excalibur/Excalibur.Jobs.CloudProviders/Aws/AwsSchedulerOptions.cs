// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.CloudProviders.Aws;

/// <summary>
/// Configuration options for AWS EventBridge Scheduler integration.
/// </summary>
public sealed class AwsSchedulerOptions
{
	/// <summary>
	/// Gets or sets the ARN of the target resource (e.g., Lambda function, SQS queue).
	/// </summary>
	/// <value> The target ARN for job execution. </value>
	public required string TargetArn { get; set; }

	/// <summary>
	/// Gets or sets the ARN of the IAM role that EventBridge Scheduler assumes to invoke the target.
	/// </summary>
	/// <value> The execution role ARN. </value>
	public required string ExecutionRoleArn { get; set; }

	/// <summary>
	/// Gets or sets the time zone for schedule expressions.
	/// </summary>
	/// <value> The time zone. Defaults to "UTC". </value>
	public string TimeZone { get; set; } = "UTC";

	/// <summary>
	/// Gets or sets the schedule group name for organizing related schedules.
	/// </summary>
	/// <value> The schedule group name. Defaults to "default". </value>
	public string ScheduleGroup { get; set; } = "default";

	/// <summary>
	/// Gets or sets the maximum age of a request that EventBridge Scheduler sends to a target.
	/// </summary>
	/// <value> The maximum age in seconds. Defaults to 86400 (24 hours). </value>
	public int MaximumEventAgeInSeconds { get; set; } = 86400;

	/// <summary>
	/// Gets or sets the maximum number of times to retry when the target returns an error.
	/// </summary>
	/// <value> The retry policy maximum retry attempts. Defaults to 3. </value>
	public int RetryPolicyMaximumRetryAttempts { get; set; } = 3;
}
