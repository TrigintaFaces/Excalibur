// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS EventBridge scheduler.
/// </summary>
public class EventBridgeSchedulerOptions
{
	/// <summary>
	/// Gets or sets the AWS region.
	/// </summary>
	/// <value>
	/// The AWS region.
	/// </value>
	public string Region { get; set; } = "us-east-1";

	/// <summary>
	/// Gets or sets the role ARN for EventBridge.
	/// </summary>
	/// <value>
	/// The role ARN for EventBridge.
	/// </value>
	public string? RoleArn { get; set; }

	/// <summary>
	/// Gets or sets the schedule group name.
	/// </summary>
	/// <value>
	/// The schedule group name.
	/// </value>
	public string ScheduleGroupName { get; set; } = "default";

	/// <summary>
	/// Gets or sets the target ARN.
	/// </summary>
	/// <value>
	/// The target ARN.
	/// </value>
	public string? TargetArn { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retries.
	/// </summary>
	/// <value>
	/// The maximum number of retries.
	/// </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the schedule expression time zone.
	/// </summary>
	/// <value>
	/// The time zone for schedule expressions. Defaults to UTC.
	/// </value>
	public string ScheduleTimeZone { get; set; } = "UTC";

	/// <summary>
	/// Gets or sets the dead letter queue ARN.
	/// </summary>
	/// <value>
	/// The dead letter queue ARN.
	/// </value>
	public string? DeadLetterQueueArn { get; set; }
}
