// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS transport health checks.
/// </summary>
public static class AwsHealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds a health check that probes Amazon SQS connectivity.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name. Default is "aws-sqs".</param>
	/// <param name="failureStatus">The failure status. Default is <see langword="null"/> (context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddAwsSqsHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "aws-sqs",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<AwsSqsHealthChecker>(sp),
			failureStatus,
			tags));
	}

	/// <summary>
	/// Adds a health check that probes Amazon SNS connectivity.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name. Default is "aws-sns".</param>
	/// <param name="failureStatus">The failure status. Default is <see langword="null"/> (context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddAwsSnsHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "aws-sns",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<AwsSnsHealthChecker>(sp),
			failureStatus,
			tags));
	}
}
