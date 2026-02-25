// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Observability.Aws;

/// <summary>
/// Configuration options for AWS observability integration.
/// </summary>
/// <remarks>
/// <para>
/// Bridges Dispatch telemetry to AWS X-Ray segments and CloudWatch metrics.
/// Uses the standard AWS credential chain for authentication.
/// </para>
/// </remarks>
public sealed class AwsObservabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable AWS X-Ray tracing integration.
	/// </summary>
	public bool EnableXRay { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable CloudWatch metrics publishing.
	/// </summary>
	public bool EnableCloudWatchMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets the sampling rate for X-Ray traces (0.0 to 1.0).
	/// </summary>
	/// <remarks>
	/// Default is 0.05 (5%). Set to 1.0 for development/debugging.
	/// </remarks>
	[Range(0.0, 1.0)]
	public double SamplingRate { get; set; } = 0.05;

	/// <summary>
	/// Gets or sets the service name used in X-Ray segments and CloudWatch dimensions.
	/// </summary>
	[Required]
	public required string ServiceName { get; set; }

	/// <summary>
	/// Gets or sets the AWS region for CloudWatch metrics.
	/// </summary>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the CloudWatch namespace for custom metrics.
	/// </summary>
	public string MetricsNamespace { get; set; } = "Dispatch/Custom";

	/// <summary>
	/// Gets or sets the X-Ray daemon endpoint override.
	/// </summary>
	/// <remarks>
	/// Default is "127.0.0.1:2000". Override for custom daemon configurations.
	/// </remarks>
	public string? XRayDaemonEndpoint { get; set; }
}
