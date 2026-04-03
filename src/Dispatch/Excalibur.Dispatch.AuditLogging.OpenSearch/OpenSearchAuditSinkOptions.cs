// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.OpenSearch;

/// <summary>
/// Configuration options for the OpenSearch audit sink.
/// </summary>
/// <remarks>
/// <para>
/// The audit sink writes individual audit events to OpenSearch in real-time
/// using the Bulk API for efficient indexing.
/// </para>
/// <para>
/// Supports cluster deployments via <see cref="NodeUrls"/> with round-robin
/// load balancing across nodes. For single-node or load-balanced setups,
/// use <see cref="OpenSearchUrl"/> instead.
/// </para>
/// </remarks>
public sealed class OpenSearchAuditSinkOptions
{
    /// <summary>
    /// Gets or sets the OpenSearch node URLs for cluster connectivity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provide one or more node URLs. Requests are distributed across nodes
    /// using round-robin for load balancing and fault tolerance.
    /// </para>
    /// <para>
    /// If both <see cref="NodeUrls"/> and <see cref="OpenSearchUrl"/> are set,
    /// <see cref="NodeUrls"/> takes precedence.
    /// </para>
    /// <para>
    /// Example: ["https://os-node1:9200", "https://os-node2:9200", "https://os-node3:9200"]
    /// </para>
    /// </remarks>
    public List<string>? NodeUrls { get; set; }

    /// <summary>
    /// Gets or sets the OpenSearch base URL (single-node convenience).
    /// </summary>
    /// <remarks>
    /// <para>
    /// For cluster deployments, prefer <see cref="NodeUrls"/> with multiple node addresses.
    /// This property is a convenience for single-node or load-balanced endpoint setups.
    /// </para>
    /// <para>
    /// Example: "https://my-cluster.os.example.com:9200"
    /// </para>
    /// </remarks>
    public string? OpenSearchUrl { get; set; }

    /// <summary>
    /// Gets or sets the application name that produced the audit events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored as an <c>application_name</c> field on every indexed document.
    /// Essential for shared clusters where multiple applications write to
    /// the same index pattern -- enables filtering by source application
    /// in OpenSearch Dashboards queries.
    /// </para>
    /// </remarks>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the index name prefix for audit documents.
    /// </summary>
    /// <remarks>
    /// Documents are indexed to "{IndexPrefix}-{yyyy.MM.dd}" for time-based partitioning.
    /// </remarks>
    public string IndexPrefix { get; set; } = "dispatch-audit";

    /// <summary>
    /// Gets or sets the refresh policy for index operations.
    /// </summary>
    /// <remarks>
    /// Valid values: "true" (immediate), "wait_for" (wait until refreshed), "false" (no refresh).
    /// Default is "false" for optimal performance.
    /// </remarks>
    public string RefreshPolicy { get; set; } = "false";

    /// <summary>
    /// Gets or sets the optional API key for authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retries.
    /// </summary>
    /// <remarks>
    /// Actual delay uses exponential backoff: baseDelay * 2^(attempt-1).
    /// </remarks>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the HTTP request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the resolved node URLs, preferring <see cref="NodeUrls"/> over <see cref="OpenSearchUrl"/>.
    /// </summary>
    internal IReadOnlyList<string> GetResolvedNodeUrls()
    {
        if (NodeUrls is { Count: > 0 })
        {
            return NodeUrls;
        }

        if (!string.IsNullOrWhiteSpace(OpenSearchUrl))
        {
            return [OpenSearchUrl];
        }

        return [];
    }
}
