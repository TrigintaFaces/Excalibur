// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.AuditLogging.Datadog;

/// <summary>
/// Event IDs for Datadog audit exporter diagnostics (93420-93439).
/// </summary>
public static class DatadogAuditLoggingEventId
{
	/// <summary>Datadog event forwarded.</summary>
	public const int EventForwarded = 93420;

	/// <summary>Datadog batch forwarded.</summary>
	public const int BatchForwarded = 93421;

	/// <summary>Datadog forward failed (status response).</summary>
	public const int ForwardFailedStatus = 93425;

	/// <summary>Datadog forward retried.</summary>
	public const int ForwardRetried = 93426;

	/// <summary>Datadog health check failed.</summary>
	public const int HealthCheckFailed = 93427;

	/// <summary>Datadog forward failed (HTTP error).</summary>
	public const int ForwardFailedHttpError = 93428;

	/// <summary>Datadog forward failed (timeout).</summary>
	public const int ForwardFailedTimeout = 93429;

	/// <summary>Datadog forward failed (batch chunk).</summary>
	public const int ForwardFailedBatchChunk = 93430;
}
