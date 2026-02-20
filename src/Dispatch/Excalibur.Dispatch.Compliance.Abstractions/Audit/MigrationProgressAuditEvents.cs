// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Severity levels for plaintext data detection alerts.
/// </summary>
public enum PlaintextSeverity
{
	/// <summary>
	/// Low severity - data is classified as non-sensitive.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Medium severity - data may contain sensitive information.
	/// </summary>
	Medium = 1,

	/// <summary>
	/// High severity - data contains sensitive or PII information.
	/// </summary>
	High = 2,

	/// <summary>
	/// Critical severity - data contains highly sensitive information (financial, health, etc.).
	/// </summary>
	Critical = 3
}

/// <summary>
/// Factory methods for creating migration progress tracking audit events.
/// </summary>
/// <remarks>
/// <para>
/// Migration progress is tracked for observability via:
/// </para>
/// <list type="bullet">
///   <item><description>Plaintext data detection alerts</description></item>
///   <item><description>Data migration queue events</description></item>
///   <item><description>Migration completion events</description></item>
///   <item><description>Batch-level progress updates</description></item>
/// </list>
/// <para>
/// These events support SOC 2 compliance by providing evidence of:
/// </para>
/// <list type="bullet">
///   <item><description>CC6.6 - Data protection monitoring</description></item>
///   <item><description>CC7.2 - System availability and change monitoring</description></item>
/// </list>
/// </remarks>
public static class MigrationProgressAuditEvents
{
	/// <summary>
	/// Creates an audit event when unencrypted/plaintext data is detected.
	/// </summary>
	/// <param name="resourceType">The type of resource containing plaintext data.</param>
	/// <param name="count">The number of plaintext records detected.</param>
	/// <param name="severity">The severity level of the detection.</param>
	/// <param name="detectionMethod">The method used to detect plaintext (e.g., "scan", "migration", "audit").</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the plaintext data detection.</returns>
	public static AuditEvent PlaintextDataDetected(
		string resourceType,
		long count,
		PlaintextSeverity severity,
		string detectionMethod,
		string? correlationId = null)
	{
		var outcome = severity >= PlaintextSeverity.High
			? AuditOutcome.Error
			: AuditOutcome.Pending;

		return new AuditEvent
		{
			EventId = $"plaintext-detected-{Guid.NewGuid():N}",
			EventType = AuditEventType.Compliance,
			Action = "PlaintextDataDetected",
			Outcome = outcome,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "System",
			ActorType = "System",
			ResourceType = resourceType,
			Reason = $"Detected {count} records with unencrypted data",
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["resourceType"] = resourceType,
				["count"] = count.ToString(),
				["severity"] = severity.ToString(),
				["detectionMethod"] = detectionMethod
			}
		};
	}

	/// <summary>
	/// Creates an audit event when data is queued for migration.
	/// </summary>
	/// <param name="batchId">The unique identifier for the migration batch.</param>
	/// <param name="resourceType">The type of resource being migrated.</param>
	/// <param name="recordCount">The number of records queued for migration.</param>
	/// <param name="estimatedSizeBytes">The estimated size of the data in bytes.</param>
	/// <param name="sourceProvider">The source encryption provider.</param>
	/// <param name="targetProvider">The target encryption provider.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the migration queue operation.</returns>
	public static AuditEvent DataMigrationQueued(
		string batchId,
		string resourceType,
		long recordCount,
		long estimatedSizeBytes,
		string sourceProvider,
		string targetProvider,
		string? correlationId = null)
	{
		return new AuditEvent
		{
			EventId = $"migration-queued-{Guid.NewGuid():N}",
			EventType = AuditEventType.Security,
			Action = "DataMigrationQueued",
			Outcome = AuditOutcome.Pending,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "System",
			ActorType = "System",
			ResourceId = batchId,
			ResourceType = resourceType,
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["batchId"] = batchId,
				["resourceType"] = resourceType,
				["recordCount"] = recordCount.ToString(),
				["estimatedSizeBytes"] = estimatedSizeBytes.ToString(),
				["estimatedSizeMB"] = (estimatedSizeBytes / 1024.0 / 1024.0).ToString("F2"),
				["sourceProvider"] = sourceProvider,
				["targetProvider"] = targetProvider
			}
		};
	}

	/// <summary>
	/// Creates an audit event when a data migration operation completes.
	/// </summary>
	/// <param name="batchId">The unique identifier for the migration batch.</param>
	/// <param name="resourceType">The type of resource that was migrated.</param>
	/// <param name="totalRecords">The total number of records in the migration.</param>
	/// <param name="successCount">The number of records successfully migrated.</param>
	/// <param name="failedCount">The number of records that failed to migrate.</param>
	/// <param name="skippedCount">The number of records that were skipped.</param>
	/// <param name="duration">The total duration of the migration.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the migration completion.</returns>
	public static AuditEvent DataMigrationCompleted(
		string batchId,
		string resourceType,
		long totalRecords,
		long successCount,
		long failedCount,
		long skippedCount,
		TimeSpan duration,
		string? correlationId = null)
	{
		var outcome = failedCount > 0
			? (failedCount == totalRecords ? AuditOutcome.Failure : AuditOutcome.Error)
			: AuditOutcome.Success;

		return new AuditEvent
		{
			EventId = $"migration-completed-{Guid.NewGuid():N}",
			EventType = AuditEventType.Security,
			Action = "DataMigrationCompleted",
			Outcome = outcome,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "System",
			ActorType = "System",
			ResourceId = batchId,
			ResourceType = resourceType,
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["batchId"] = batchId,
				["resourceType"] = resourceType,
				["totalRecords"] = totalRecords.ToString(),
				["successCount"] = successCount.ToString(),
				["failedCount"] = failedCount.ToString(),
				["skippedCount"] = skippedCount.ToString(),
				["successRate"] = totalRecords > 0
					? ((double)successCount / totalRecords * 100).ToString("F2") + "%"
					: "N/A",
				["durationMs"] = duration.TotalMilliseconds.ToString("F0"),
				["throughputPerSecond"] = duration.TotalSeconds > 0
					? (totalRecords / duration.TotalSeconds).ToString("F2")
					: "N/A"
			}
		};
	}

	/// <summary>
	/// Creates an audit event for batch-level migration progress updates.
	/// </summary>
	/// <param name="batchId">The unique identifier for the migration batch.</param>
	/// <param name="currentBatch">The current batch number being processed.</param>
	/// <param name="totalBatches">The total number of batches in the migration.</param>
	/// <param name="processedRecords">The number of records processed so far.</param>
	/// <param name="totalRecords">The total number of records to be processed.</param>
	/// <param name="elapsedTime">The elapsed time since the migration started.</param>
	/// <param name="estimatedTimeRemaining">The estimated time remaining for the migration.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the batch progress.</returns>
	public static AuditEvent MigrationBatchProgress(
		string batchId,
		int currentBatch,
		int totalBatches,
		long processedRecords,
		long totalRecords,
		TimeSpan elapsedTime,
		TimeSpan? estimatedTimeRemaining = null,
		string? correlationId = null)
	{
		var progressPercentage = totalRecords > 0
			? (double)processedRecords / totalRecords * 100
			: 0;

		return new AuditEvent
		{
			EventId = $"migration-progress-{Guid.NewGuid():N}",
			EventType = AuditEventType.System,
			Action = "MigrationBatchProgress",
			Outcome = AuditOutcome.Pending,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "System",
			ActorType = "System",
			ResourceId = batchId,
			ResourceType = "MigrationBatch",
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["batchId"] = batchId,
				["currentBatch"] = currentBatch.ToString(),
				["totalBatches"] = totalBatches.ToString(),
				["processedRecords"] = processedRecords.ToString(),
				["totalRecords"] = totalRecords.ToString(),
				["progressPercentage"] = progressPercentage.ToString("F2"),
				["elapsedMs"] = elapsedTime.TotalMilliseconds.ToString("F0"),
				["estimatedRemainingMs"] = estimatedTimeRemaining?.TotalMilliseconds.ToString("F0") ?? "N/A"
			}
		};
	}
}
