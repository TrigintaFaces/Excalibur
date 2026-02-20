// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.AuditLogging.Sentinel;

/// <summary>
/// Event IDs for Azure Sentinel audit exporter diagnostics (93440-93459).
/// </summary>
public static class SentinelAuditLoggingEventId
{
	/// <summary>Azure Sentinel event forwarded.</summary>
	public const int EventForwarded = 93440;

	/// <summary>Azure Sentinel batch forwarded.</summary>
	public const int BatchForwarded = 93441;

	/// <summary>Azure Sentinel forward failed (status response).</summary>
	public const int ForwardFailedStatus = 93445;

	/// <summary>Azure Sentinel forward retried.</summary>
	public const int ForwardRetried = 93446;

	/// <summary>Azure Sentinel health check failed.</summary>
	public const int HealthCheckFailed = 93447;

	/// <summary>Azure Sentinel forward failed (HTTP error).</summary>
	public const int ForwardFailedHttpError = 93448;

	/// <summary>Azure Sentinel forward failed (timeout).</summary>
	public const int ForwardFailedTimeout = 93449;

	/// <summary>Azure Sentinel forward failed (batch chunk).</summary>
	public const int ForwardFailedBatchChunk = 93450;
}
