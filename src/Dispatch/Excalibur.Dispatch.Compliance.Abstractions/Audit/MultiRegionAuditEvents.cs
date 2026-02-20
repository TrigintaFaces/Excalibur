// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Factory methods for creating multi-region disaster recovery audit events.
/// </summary>
/// <remarks>
/// <para>
/// Multi-region operations generate audit events for:
/// </para>
/// <list type="bullet">
///   <item><description>Failover events (automatic and manual)</description></item>
///   <item><description>Failback events</description></item>
///   <item><description>RPO threshold breaches</description></item>
///   <item><description>Replication status changes</description></item>
/// </list>
/// <para>
/// These events support SOC 2 compliance by providing evidence of:
/// </para>
/// <list type="bullet">
///   <item><description>CC6.6 - Encryption key availability and recovery</description></item>
///   <item><description>CC7.2 - System availability monitoring</description></item>
///   <item><description>CC9.1 - Business continuity planning</description></item>
/// </list>
/// </remarks>
public static class MultiRegionAuditEvents
{
	/// <summary>
	/// Creates an audit event for a failover operation.
	/// </summary>
	/// <param name="sourceRegion">The region failing from (primary).</param>
	/// <param name="targetRegion">The region failing to (secondary).</param>
	/// <param name="reason">The reason for failover.</param>
	/// <param name="isAutomatic">Whether the failover was triggered automatically.</param>
	/// <param name="actorId">The actor who initiated the failover (for manual) or "System" (for automatic).</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the failover.</returns>
	public static AuditEvent FailoverInitiated(
		string sourceRegion,
		string targetRegion,
		string reason,
		bool isAutomatic,
		string actorId,
		string? correlationId = null)
	{
		return new AuditEvent
		{
			EventId = $"failover-{Guid.NewGuid():N}",
			EventType = AuditEventType.Security,
			Action = "FailoverInitiated",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = actorId,
			ActorType = isAutomatic ? "System" : "User",
			ResourceId = $"{sourceRegion}->{targetRegion}",
			ResourceType = "MultiRegionKeyProvider",
			Reason = reason,
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["sourceRegion"] = sourceRegion,
				["targetRegion"] = targetRegion,
				["isAutomatic"] = isAutomatic.ToString(),
				["reason"] = reason
			}
		};
	}

	/// <summary>
	/// Creates an audit event for a failback operation.
	/// </summary>
	/// <param name="sourceRegion">The region failing back from (secondary).</param>
	/// <param name="targetRegion">The region failing back to (primary).</param>
	/// <param name="reason">The reason for failback.</param>
	/// <param name="actorId">The actor who initiated the failback.</param>
	/// <param name="keysSynchronized">Number of keys synchronized during failback.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the failback.</returns>
	public static AuditEvent FailbackCompleted(
		string sourceRegion,
		string targetRegion,
		string reason,
		string actorId,
		int keysSynchronized,
		string? correlationId = null)
	{
		return new AuditEvent
		{
			EventId = $"failback-{Guid.NewGuid():N}",
			EventType = AuditEventType.Security,
			Action = "FailbackCompleted",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = actorId,
			ActorType = "User",
			ResourceId = $"{sourceRegion}->{targetRegion}",
			ResourceType = "MultiRegionKeyProvider",
			Reason = reason,
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["sourceRegion"] = sourceRegion,
				["targetRegion"] = targetRegion,
				["keysSynchronized"] = keysSynchronized.ToString(),
				["reason"] = reason
			}
		};
	}

	/// <summary>
	/// Creates an audit event for an RPO threshold breach.
	/// </summary>
	/// <param name="currentLag">The current replication lag.</param>
	/// <param name="threshold">The configured RPO threshold.</param>
	/// <param name="activeRegion">The currently active region.</param>
	/// <param name="pendingKeys">Number of keys pending replication.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the RPO threshold breach.</returns>
	public static AuditEvent RpoThresholdBreached(
		TimeSpan currentLag,
		TimeSpan threshold,
		string activeRegion,
		int pendingKeys,
		string? correlationId = null)
	{
		return new AuditEvent
		{
			EventId = $"rpo-breach-{Guid.NewGuid():N}",
			EventType = AuditEventType.Compliance,
			Action = "RpoThresholdBreached",
			Outcome = AuditOutcome.Pending,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "System",
			ActorType = "System",
			ResourceId = activeRegion,
			ResourceType = "MultiRegionKeyProvider",
			Reason = $"Replication lag ({currentLag.TotalMinutes:F1}m) exceeded RPO target ({threshold.TotalMinutes:F1}m)",
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["currentLagMinutes"] = currentLag.TotalMinutes.ToString("F2"),
				["thresholdMinutes"] = threshold.TotalMinutes.ToString("F2"),
				["activeRegion"] = activeRegion,
				["pendingKeys"] = pendingKeys.ToString()
			}
		};
	}

	/// <summary>
	/// Creates an audit event for a replication sync completion.
	/// </summary>
	/// <param name="sourceRegion">The source region of the replication.</param>
	/// <param name="targetRegion">The target region of the replication.</param>
	/// <param name="keyCount">Number of keys synchronized.</param>
	/// <param name="duration">Duration of the sync operation.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the sync completion.</returns>
	public static AuditEvent ReplicationSyncCompleted(
		string sourceRegion,
		string targetRegion,
		int keyCount,
		TimeSpan duration,
		string? correlationId = null)
	{
		return new AuditEvent
		{
			EventId = $"replication-{Guid.NewGuid():N}",
			EventType = AuditEventType.Security,
			Action = "ReplicationSyncCompleted",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "System",
			ActorType = "System",
			ResourceId = $"{sourceRegion}->{targetRegion}",
			ResourceType = "MultiRegionKeyProvider",
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["sourceRegion"] = sourceRegion,
				["targetRegion"] = targetRegion,
				["keyCount"] = keyCount.ToString(),
				["durationMs"] = duration.TotalMilliseconds.ToString("F0")
			}
		};
	}

	/// <summary>
	/// Creates an audit event for a region health status change.
	/// </summary>
	/// <param name="regionId">The region whose health changed.</param>
	/// <param name="previousStatus">The previous health status.</param>
	/// <param name="currentStatus">The current health status.</param>
	/// <param name="consecutiveFailures">Number of consecutive health check failures.</param>
	/// <param name="errorMessage">Error message if unhealthy.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the health status change.</returns>
	public static AuditEvent RegionHealthChanged(
		string regionId,
		bool previousStatus,
		bool currentStatus,
		int consecutiveFailures,
		string? errorMessage = null,
		string? correlationId = null)
	{
		var statusChange = (previousStatus, currentStatus) switch
		{
			(true, false) => "Healthy->Unhealthy",
			(false, true) => "Unhealthy->Healthy",
			_ => "Unknown"
		};

		return new AuditEvent
		{
			EventId = $"health-{Guid.NewGuid():N}",
			EventType = AuditEventType.System,
			Action = "RegionHealthChanged",
			Outcome = currentStatus ? AuditOutcome.Success : AuditOutcome.Error,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "System",
			ActorType = "System",
			ResourceId = regionId,
			ResourceType = "MultiRegionKeyProvider",
			Reason = statusChange,
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["regionId"] = regionId,
				["previousStatus"] = previousStatus ? "Healthy" : "Unhealthy",
				["currentStatus"] = currentStatus ? "Healthy" : "Unhealthy",
				["consecutiveFailures"] = consecutiveFailures.ToString(),
				["errorMessage"] = errorMessage ?? string.Empty
			}
		};
	}
}
