// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging;

/// <summary>
/// Provides SHA-256 hash computation for audit events with chain linking.
/// </summary>
/// <remarks>
/// <para>
/// Audit events are hash-chained for tamper detection:
/// - Each event hash includes the previous event's hash
/// - Hash includes all immutable event fields
/// - Enables verification of entire audit trail integrity
/// </para>
/// </remarks>
public static class AuditHasher
{
	/// <summary>
	/// Computes the SHA-256 hash for an audit event, linking it to the previous event's hash.
	/// </summary>
	/// <param name="auditEvent">The audit event to hash.</param>
	/// <param name="previousHash">The hash of the previous event in the chain. Null for the first event.</param>
	/// <returns>The Base64-encoded SHA-256 hash of the event.</returns>
	public static string ComputeHash(AuditEvent auditEvent, string? previousHash)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		var hashInput = BuildHashInput(auditEvent, previousHash);
		var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));

		return Convert.ToBase64String(hashBytes);
	}

	/// <summary>
	/// Verifies that an event's hash matches its expected value.
	/// </summary>
	/// <param name="auditEvent">The audit event to verify.</param>
	/// <param name="previousHash">The hash of the previous event in the chain.</param>
	/// <returns>True if the event hash is valid; otherwise, false.</returns>
	public static bool VerifyHash(AuditEvent auditEvent, string? previousHash)
	{
		if (auditEvent?.EventHash is null)
		{
			return false;
		}

		var computedHash = ComputeHash(auditEvent, previousHash);
		return string.Equals(computedHash, auditEvent.EventHash, StringComparison.Ordinal);
	}

	/// <summary>
	/// Computes the genesis hash for the first event in a chain.
	/// </summary>
	/// <param name="tenantId">Optional tenant ID for multi-tenant isolation.</param>
	/// <param name="chainInitTime">The initialization timestamp for the chain.</param>
	/// <returns>The Base64-encoded SHA-256 genesis hash.</returns>
	public static string ComputeGenesisHash(string? tenantId, DateTimeOffset chainInitTime)
	{
		var genesisInput = $"GENESIS:{tenantId ?? "default"}:{chainInitTime:O}";
		var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(genesisInput));

		return Convert.ToBase64String(hashBytes);
	}

	private static string BuildHashInput(AuditEvent auditEvent, string? previousHash)
	{
		// Build a deterministic string representation of all immutable event fields
		// Order matters for hash consistency
		var sb = new StringBuilder(512);

		_ = sb.Append("EVENT:");
		_ = sb.Append(auditEvent.EventId);
		_ = sb.Append('|');
		_ = sb.Append((int)auditEvent.EventType);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.Action);
		_ = sb.Append('|');
		_ = sb.Append((int)auditEvent.Outcome);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.Timestamp.ToUnixTimeMilliseconds());
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.ActorId);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.ActorType ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.ResourceId ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.ResourceType ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.ResourceClassification?.ToString() ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.TenantId ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.CorrelationId ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.SessionId ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.IpAddress ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.UserAgent ?? string.Empty);
		_ = sb.Append('|');
		_ = sb.Append(auditEvent.Reason ?? string.Empty);
		_ = sb.Append('|');

		// Include metadata in a deterministic way
		if (auditEvent.Metadata is { Count: > 0 })
		{
			var sortedMetadata = auditEvent.Metadata
				.OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

			foreach (var kvp in sortedMetadata)
			{
				AppendLengthPrefixed(sb, kvp.Key);
				_ = sb.Append('=');
				AppendLengthPrefixed(sb, kvp.Value ?? string.Empty);
				_ = sb.Append(';');
			}
		}

		_ = sb.Append('|');
		_ = sb.Append("PREV:");
		_ = sb.Append(previousHash ?? "GENESIS");

		return sb.ToString();
	}

	private static void AppendLengthPrefixed(StringBuilder sb, string value)
	{
		_ = sb.Append(value.Length.ToString(CultureInfo.InvariantCulture));
		_ = sb.Append(':');
		_ = sb.Append(value);
	}
}
