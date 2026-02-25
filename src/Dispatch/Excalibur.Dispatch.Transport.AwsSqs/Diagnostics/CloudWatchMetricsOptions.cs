// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Configuration options for the AWS CloudWatch metrics export bridge.
/// </summary>
/// <remarks>
/// <para>
/// Configures the bridge between OpenTelemetry metrics and AWS CloudWatch
/// <c>PutMetricData</c> API. Metrics are buffered and published at the
/// configured interval to the specified CloudWatch namespace.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsCloudWatchMetricsExporter(options =>
/// {
///     options.Namespace = "MyApp/Dispatch";
///     options.Region = "us-east-1";
///     options.PublishInterval = TimeSpan.FromSeconds(60);
/// });
/// </code>
/// </example>
public sealed class CloudWatchMetricsOptions
{
	/// <summary>
	/// Gets or sets the CloudWatch namespace for published metrics.
	/// </summary>
	/// <remarks>
	/// CloudWatch namespaces group related metrics. Custom namespaces must not
	/// start with <c>AWS/</c> (reserved for AWS service metrics).
	/// </remarks>
	/// <value>The CloudWatch namespace.</value>
	[Required]
	public string Namespace { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the AWS region for CloudWatch API calls.
	/// </summary>
	/// <remarks>
	/// If not specified, the region is resolved from the default AWS credential chain
	/// (environment variables, instance profile, etc.).
	/// </remarks>
	/// <value>The AWS region. Default is <c>null</c> (use default).</value>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the interval between metric publish operations.
	/// </summary>
	/// <remarks>
	/// Metrics are buffered and published to CloudWatch at this interval.
	/// CloudWatch accepts data points with timestamps up to 2 weeks in the past
	/// and 2 hours in the future.
	/// </remarks>
	/// <value>The publish interval. Default is 60 seconds.</value>
	public TimeSpan PublishInterval { get; set; } = TimeSpan.FromSeconds(60);
}
