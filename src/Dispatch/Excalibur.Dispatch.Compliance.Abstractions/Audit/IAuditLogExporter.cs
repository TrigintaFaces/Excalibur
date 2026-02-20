// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides audit event export capabilities to external SIEM and logging platforms.
/// </summary>
/// <remarks>
/// <para>
/// Implementations export audit events to external systems such as:
/// - Splunk (HTTP Event Collector)
/// - Azure Sentinel (Log Analytics)
/// - Datadog (Log Management)
/// - Elastic Security
/// </para>
/// <para>
/// Exporters support both real-time and batch modes to optimize throughput
/// while minimizing latency for critical security events.
/// </para>
/// </remarks>
public interface IAuditLogExporter
{
	/// <summary>
	/// Gets the name of this exporter (e.g., "Splunk", "Sentinel", "Datadog").
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Exports a single audit event in real-time.
	/// </summary>
	/// <param name="auditEvent">The audit event to export.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The result of the export operation.</returns>
	Task<AuditExportResult> ExportAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Exports multiple audit events in a batch.
	/// </summary>
	/// <param name="auditEvents">The audit events to export.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The result of the batch export operation.</returns>
	Task<AuditExportBatchResult> ExportBatchAsync(
		IReadOnlyList<AuditEvent> auditEvents,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks the health and connectivity of the export destination.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The health check result.</returns>
	Task<AuditExporterHealthResult> CheckHealthAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Result of a single audit event export operation.
/// </summary>
public sealed record AuditExportResult
{
	/// <summary>
	/// Gets a value indicating whether the export was successful.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the event ID that was exported.
	/// </summary>
	public required string EventId { get; init; }

	/// <summary>
	/// Gets the timestamp when the export completed.
	/// </summary>
	public required DateTimeOffset ExportedAt { get; init; }

	/// <summary>
	/// Gets the destination-specific acknowledgment ID (if applicable).
	/// </summary>
	public string? AckId { get; init; }

	/// <summary>
	/// Gets the error message if the export failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets a value indicating whether the error is transient and the export can be retried.
	/// </summary>
	public bool IsTransientError { get; init; }
}

/// <summary>
/// Result of a batch audit event export operation.
/// </summary>
public sealed record AuditExportBatchResult
{
	/// <summary>
	/// Gets the total number of events in the batch.
	/// </summary>
	public required int TotalCount { get; init; }

	/// <summary>
	/// Gets the number of events successfully exported.
	/// </summary>
	public required int SuccessCount { get; init; }

	/// <summary>
	/// Gets the number of events that failed to export.
	/// </summary>
	public required int FailedCount { get; init; }

	/// <summary>
	/// Gets the timestamp when the batch export completed.
	/// </summary>
	public required DateTimeOffset ExportedAt { get; init; }

	/// <summary>
	/// Gets the IDs of events that failed to export.
	/// </summary>
	public IReadOnlyList<string>? FailedEventIds { get; init; }

	/// <summary>
	/// Gets error messages for failed events (keyed by event ID).
	/// </summary>
	public IReadOnlyDictionary<string, string>? Errors { get; init; }

	/// <summary>
	/// Gets a value indicating whether all exports succeeded.
	/// </summary>
	public bool AllSucceeded => SuccessCount == TotalCount;
}

/// <summary>
/// Result of an exporter health check.
/// </summary>
public sealed record AuditExporterHealthResult
{
	/// <summary>
	/// Gets a value indicating whether the exporter is healthy.
	/// </summary>
	public required bool IsHealthy { get; init; }

	/// <summary>
	/// Gets the name of the exporter.
	/// </summary>
	public required string ExporterName { get; init; }

	/// <summary>
	/// Gets the endpoint being checked.
	/// </summary>
	public string? Endpoint { get; init; }

	/// <summary>
	/// Gets the latency of the health check in milliseconds.
	/// </summary>
	public long? LatencyMs { get; init; }

	/// <summary>
	/// Gets the timestamp when the health check was performed.
	/// </summary>
	public required DateTimeOffset CheckedAt { get; init; }

	/// <summary>
	/// Gets the error message if the health check failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets additional diagnostic information.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Diagnostics { get; init; }
}
