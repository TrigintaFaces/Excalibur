// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.AuditLogging.Elasticsearch;

/// <summary>
/// Event IDs for Elasticsearch audit exporter diagnostics (93460-93479).
/// </summary>
public static class ElasticsearchAuditLoggingEventId
{
	/// <summary>Elasticsearch event forwarded.</summary>
	public const int EventForwarded = 93460;

	/// <summary>Elasticsearch batch forwarded.</summary>
	public const int BatchForwarded = 93461;

	/// <summary>Elasticsearch forward failed (status response).</summary>
	public const int ForwardFailedStatus = 93465;

	/// <summary>Elasticsearch forward retried.</summary>
	public const int ForwardRetried = 93466;

	/// <summary>Elasticsearch health check failed.</summary>
	public const int HealthCheckFailed = 93467;

	/// <summary>Elasticsearch forward failed (HTTP error).</summary>
	public const int ForwardFailedHttpError = 93468;

	/// <summary>Elasticsearch forward failed (timeout).</summary>
	public const int ForwardFailedTimeout = 93469;

	/// <summary>Elasticsearch forward failed (batch chunk).</summary>
	public const int ForwardFailedBatchChunk = 93470;
}
