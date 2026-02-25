// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.Aws;

/// <summary>
/// Configuration options for the AWS CloudWatch audit log exporter.
/// </summary>
/// <remarks>
/// <para>
/// Uses the CloudWatch Logs PutLogEvents API to send audit events
/// to a specified log group and stream.
/// </para>
/// <para>
/// Authentication uses the standard AWS credential chain (environment variables,
/// IAM roles, profile-based credentials).
/// </para>
/// </remarks>
public sealed class AwsAuditOptions
{
	/// <summary>
	/// Gets or sets the CloudWatch Logs log group name.
	/// </summary>
	[Required]
	public required string LogGroupName { get; set; }

	/// <summary>
	/// Gets or sets the AWS region (e.g., "us-east-1").
	/// </summary>
	[Required]
	public required string Region { get; set; }

	/// <summary>
	/// Gets or sets the CloudWatch Logs stream name.
	/// </summary>
	/// <remarks>
	/// If not specified, defaults to "dispatch-audit-{MachineName}".
	/// </remarks>
	public string? StreamName { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of events per PutLogEvents call.
	/// </summary>
	/// <remarks>
	/// CloudWatch Logs has a limit of 10,000 events per PutLogEvents request.
	/// Default is 500 events per batch for optimal performance.
	/// </remarks>
	public int BatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets the CloudWatch Logs service endpoint URL override.
	/// </summary>
	/// <remarks>
	/// Optional. Use this for VPC endpoints or custom endpoints.
	/// </remarks>
	public string? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retries.
	/// </summary>
	public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the HTTP request timeout.
	/// </summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
