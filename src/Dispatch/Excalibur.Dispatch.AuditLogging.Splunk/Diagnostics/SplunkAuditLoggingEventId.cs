// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Event IDs for Splunk audit exporter diagnostics (93400-93499).
/// </summary>
public static class SplunkAuditLoggingEventId
{
	/// <summary>Splunk event forwarded.</summary>
	public const int EventForwarded = 93401;

	/// <summary>Splunk batch forwarded.</summary>
	public const int BatchForwarded = 93402;

	/// <summary>Splunk forward failed (status response).</summary>
	public const int ForwardFailedStatus = 93405;

	/// <summary>Splunk forward failed (HTTP error).</summary>
	public const int ForwardFailedHttpError = 93408;

	/// <summary>Splunk forward failed (timeout).</summary>
	public const int ForwardFailedTimeout = 93409;

	/// <summary>Splunk forward failed (batch chunk).</summary>
	public const int ForwardFailedBatchChunk = 93410;

	/// <summary>Splunk forward retried.</summary>
	public const int ForwardRetried = 93406;

	/// <summary>Splunk health check failed.</summary>
	public const int HealthCheckFailed = 93407;
}
