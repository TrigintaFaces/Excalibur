// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Factory methods for creating provider migration audit events.
/// </summary>
/// <remarks>
/// <para>
/// Provider migration operations generate audit events for:
/// </para>
/// <list type="bullet">
///   <item><description>Data re-encryption operations (algorithm/key changes)</description></item>
///   <item><description>Provider migration completion</description></item>
///   <item><description>Decryption migration completion</description></item>
/// </list>
/// <para>
/// These events support SOC 2 compliance by providing evidence of:
/// </para>
/// <list type="bullet">
///   <item><description>CC6.6 - Encryption key management and rotation</description></item>
///   <item><description>CC7.2 - System change monitoring</description></item>
/// </list>
/// </remarks>
public static class ProviderMigrationAuditEvents
{
	/// <summary>
	/// Creates an audit event for a data re-encryption operation.
	/// </summary>
	/// <param name="sourceProvider">The source encryption provider being migrated from.</param>
	/// <param name="targetProvider">The target encryption provider being migrated to.</param>
	/// <param name="sourceAlgorithm">The encryption algorithm used by the source provider.</param>
	/// <param name="targetAlgorithm">The encryption algorithm used by the target provider.</param>
	/// <param name="sourceKeyId">The key identifier used by the source provider.</param>
	/// <param name="targetKeyId">The key identifier used by the target provider.</param>
	/// <param name="resourceId">The identifier of the resource being re-encrypted.</param>
	/// <param name="resourceType">The type of resource being re-encrypted.</param>
	/// <param name="actorId">The actor who initiated the re-encryption.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the data re-encryption.</returns>
	public static AuditEvent DataReEncrypted(
		string sourceProvider,
		string targetProvider,
		string sourceAlgorithm,
		string targetAlgorithm,
		string sourceKeyId,
		string targetKeyId,
		string resourceId,
		string resourceType,
		string actorId,
		string? correlationId = null)
	{
		return new AuditEvent
		{
			EventId = $"reencrypt-{Guid.NewGuid():N}",
			EventType = AuditEventType.Security,
			Action = "DataReEncrypted",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = actorId,
			ActorType = "System",
			ResourceId = resourceId,
			ResourceType = resourceType,
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["sourceProvider"] = sourceProvider,
				["targetProvider"] = targetProvider,
				["sourceAlgorithm"] = sourceAlgorithm,
				["targetAlgorithm"] = targetAlgorithm,
				["sourceKeyId"] = sourceKeyId,
				["targetKeyId"] = targetKeyId
			}
		};
	}

	/// <summary>
	/// Creates an audit event for a provider migration completion.
	/// </summary>
	/// <param name="sourceProvider">The source provider that was migrated from.</param>
	/// <param name="targetProvider">The target provider that was migrated to.</param>
	/// <param name="migratedCount">The number of records successfully migrated.</param>
	/// <param name="failedCount">The number of records that failed to migrate.</param>
	/// <param name="skippedCount">The number of records that were skipped.</param>
	/// <param name="duration">The total duration of the migration.</param>
	/// <param name="actorId">The actor who initiated the migration.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the provider migration completion.</returns>
	public static AuditEvent ProviderMigrationCompleted(
		string sourceProvider,
		string targetProvider,
		long migratedCount,
		long failedCount,
		long skippedCount,
		TimeSpan duration,
		string actorId,
		string? correlationId = null)
	{
		var outcome = failedCount > 0 ? AuditOutcome.Failure : AuditOutcome.Success;

		return new AuditEvent
		{
			EventId = $"provider-migration-{Guid.NewGuid():N}",
			EventType = AuditEventType.Security,
			Action = "ProviderMigrationCompleted",
			Outcome = outcome,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = actorId,
			ActorType = "System",
			ResourceId = $"{sourceProvider}->{targetProvider}",
			ResourceType = "EncryptionProvider",
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["sourceProvider"] = sourceProvider,
				["targetProvider"] = targetProvider,
				["migratedCount"] = migratedCount.ToString(),
				["failedCount"] = failedCount.ToString(),
				["skippedCount"] = skippedCount.ToString(),
				["totalCount"] = (migratedCount + failedCount + skippedCount).ToString(),
				["durationMs"] = duration.TotalMilliseconds.ToString("F0")
			}
		};
	}

	/// <summary>
	/// Creates an audit event for a decryption migration completion.
	/// </summary>
	/// <param name="sourceProvider">The provider from which data was decrypted.</param>
	/// <param name="recordCount">The number of records processed.</param>
	/// <param name="successCount">The number of records successfully decrypted.</param>
	/// <param name="failedCount">The number of records that failed to decrypt.</param>
	/// <param name="duration">The total duration of the decryption migration.</param>
	/// <param name="actorId">The actor who initiated the decryption migration.</param>
	/// <param name="reason">The reason for the decryption migration.</param>
	/// <param name="correlationId">Optional correlation ID for related events.</param>
	/// <returns>An audit event representing the decryption migration completion.</returns>
	public static AuditEvent DecryptionMigrationCompleted(
		string sourceProvider,
		long recordCount,
		long successCount,
		long failedCount,
		TimeSpan duration,
		string actorId,
		string? reason = null,
		string? correlationId = null)
	{
		var outcome = failedCount > 0 ? AuditOutcome.Failure : AuditOutcome.Success;

		return new AuditEvent
		{
			EventId = $"decryption-migration-{Guid.NewGuid():N}",
			EventType = AuditEventType.Security,
			Action = "DecryptionMigrationCompleted",
			Outcome = outcome,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = actorId,
			ActorType = "System",
			ResourceId = sourceProvider,
			ResourceType = "EncryptionProvider",
			Reason = reason,
			CorrelationId = correlationId,
			Metadata = new Dictionary<string, string>
			{
				["sourceProvider"] = sourceProvider,
				["recordCount"] = recordCount.ToString(),
				["successCount"] = successCount.ToString(),
				["failedCount"] = failedCount.ToString(),
				["successRate"] = recordCount > 0
					? ((double)successCount / recordCount * 100).ToString("F2") + "%"
					: "N/A",
				["durationMs"] = duration.TotalMilliseconds.ToString("F0")
			}
		};
	}
}
