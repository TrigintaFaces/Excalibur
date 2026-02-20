// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.AuditLogging.GoogleCloud;

/// <summary>
/// Event IDs for Google Cloud Logging audit exporter diagnostics (93500-93519).
/// </summary>
public static class GoogleCloudAuditLoggingEventId
{
	/// <summary>Google Cloud event forwarded.</summary>
	public const int EventForwarded = 93510;

	/// <summary>Google Cloud batch forwarded.</summary>
	public const int BatchForwarded = 93511;

	/// <summary>Google Cloud forward failed (status response).</summary>
	public const int ForwardFailedStatus = 93515;

	/// <summary>Google Cloud forward retried.</summary>
	public const int ForwardRetried = 93516;

	/// <summary>Google Cloud health check failed.</summary>
	public const int HealthCheckFailed = 93517;

	/// <summary>Google Cloud forward failed (HTTP error).</summary>
	public const int ForwardFailedHttpError = 93518;

	/// <summary>Google Cloud forward failed (timeout).</summary>
	public const int ForwardFailedTimeout = 93519;

	/// <summary>Google Cloud forward failed (batch chunk).</summary>
	public const int ForwardFailedBatchChunk = 93520;
}
