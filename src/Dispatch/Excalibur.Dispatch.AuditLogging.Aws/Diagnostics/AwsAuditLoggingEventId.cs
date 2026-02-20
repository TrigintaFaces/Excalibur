// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.AuditLogging.Aws;

/// <summary>
/// Event IDs for AWS CloudWatch audit exporter diagnostics (93480-93499).
/// </summary>
public static class AwsAuditLoggingEventId
{
	/// <summary>AWS CloudWatch event forwarded.</summary>
	public const int EventForwarded = 93480;

	/// <summary>AWS CloudWatch batch forwarded.</summary>
	public const int BatchForwarded = 93481;

	/// <summary>AWS CloudWatch forward failed (status response).</summary>
	public const int ForwardFailedStatus = 93485;

	/// <summary>AWS CloudWatch forward retried.</summary>
	public const int ForwardRetried = 93486;

	/// <summary>AWS CloudWatch health check failed.</summary>
	public const int HealthCheckFailed = 93487;

	/// <summary>AWS CloudWatch forward failed (HTTP error).</summary>
	public const int ForwardFailedHttpError = 93488;

	/// <summary>AWS CloudWatch forward failed (timeout).</summary>
	public const int ForwardFailedTimeout = 93489;

	/// <summary>AWS CloudWatch forward failed (batch chunk).</summary>
	public const int ForwardFailedBatchChunk = 93490;
}
