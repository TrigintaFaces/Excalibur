// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Observability.Aws;

/// <summary>
/// Provides integration between Dispatch telemetry and AWS X-Ray tracing.
/// </summary>
/// <remarks>
/// <para>
/// This interface bridges the Dispatch <see cref="System.Diagnostics.ActivitySource"/>
/// and <see cref="System.Diagnostics.Metrics.Meter"/> with AWS X-Ray segments
/// and CloudWatch metrics.
/// </para>
/// </remarks>
public interface IAwsTracingIntegration
{
	/// <summary>
	/// Configures the AWS X-Ray integration, registering activity listeners
	/// that propagate Dispatch traces to X-Ray segments.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous configuration operation.</returns>
	Task ConfigureXRayAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Configures the CloudWatch metrics integration, registering meter listeners
	/// that publish Dispatch metrics to CloudWatch.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous configuration operation.</returns>
	Task ConfigureCloudWatchMetricsAsync(CancellationToken cancellationToken);
}
