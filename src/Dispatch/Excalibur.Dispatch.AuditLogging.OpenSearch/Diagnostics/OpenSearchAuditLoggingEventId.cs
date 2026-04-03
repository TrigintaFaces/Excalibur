// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.AuditLogging.OpenSearch;

/// <summary>
/// Event IDs for OpenSearch audit exporter diagnostics (93530-93549).
/// </summary>
public static class OpenSearchAuditLoggingEventId
{
    /// <summary>OpenSearch event forwarded.</summary>
    public const int EventForwarded = 93530;

    /// <summary>OpenSearch batch forwarded.</summary>
    public const int BatchForwarded = 93531;

    /// <summary>OpenSearch forward failed (status response).</summary>
    public const int ForwardFailedStatus = 93535;

    /// <summary>OpenSearch forward retried.</summary>
    public const int ForwardRetried = 93536;

    /// <summary>OpenSearch health check failed.</summary>
    public const int HealthCheckFailed = 93537;

    /// <summary>OpenSearch forward failed (HTTP error).</summary>
    public const int ForwardFailedHttpError = 93538;

    /// <summary>OpenSearch forward failed (timeout).</summary>
    public const int ForwardFailedTimeout = 93539;

    /// <summary>OpenSearch forward failed (batch chunk).</summary>
    public const int ForwardFailedBatchChunk = 93540;

    // Sink event IDs (93541-93549)

    /// <summary>OpenSearch sink event written.</summary>
    public const int SinkEventWritten = 93541;

    /// <summary>OpenSearch sink write failed (status response).</summary>
    public const int SinkWriteFailed = 93542;

    /// <summary>OpenSearch sink write failed (HTTP error).</summary>
    public const int SinkHttpError = 93543;

    /// <summary>OpenSearch sink write failed (timeout).</summary>
    public const int SinkTimeout = 93544;

    /// <summary>OpenSearch sink write retried.</summary>
    public const int SinkRetried = 93545;
}
