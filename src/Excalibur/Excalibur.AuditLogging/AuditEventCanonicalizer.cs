// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Compliance;

namespace Excalibur.AuditLogging;

/// <summary>
/// Produces the deterministic canonical byte representation of an <see cref="AuditEvent"/>'s
/// integrity-covered fields, for input to <see cref="IAuditIntegrityStrategy"/>.
/// </summary>
/// <remarks>
/// This is the AuditLogging-sink record canonicalizer (it knows <see cref="AuditEvent"/>); it delegates
/// the unambiguous, length-prefixed, version-stamped encoding to the shared
/// <see cref="AuditRecordCanonicalizer"/> primitive. <see cref="AuditEvent.TenantId"/> is included, so a
/// link's keyed MAC binds the tenant — cross-tenant chain-splicing fails verification without a separate
/// tenant-seeded genesis (qa71t5: genesis = <see langword="null"/> prior tag).
/// <para>
/// Public so every <c>IAuditStore</c> backend (across the AuditLogging provider packages) canonicalizes
/// <see cref="AuditEvent"/> identically — a shared contract, replacing the former public <c>AuditHasher</c>.
/// </para>
/// </remarks>
public static class AuditEventCanonicalizer
{
	/// <summary>
	/// Canonicalizes an audit event's integrity-covered fields to deterministic bytes.
	/// </summary>
	/// <param name="auditEvent">The audit event to canonicalize.</param>
	/// <returns>The canonical byte representation (version-stamped, length-prefixed).</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="auditEvent"/> is null.</exception>
	public static byte[] Canonicalize(AuditEvent auditEvent)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		// Fixed integrity-covered fields, in a stable order. Nulls are preserved (the primitive marks
		// absent-vs-empty distinctly); culture-invariant rendering for the numeric/enum fields.
		var fields = new List<string?>(32)
		{
			auditEvent.EventId,
			((int)auditEvent.EventType).ToString(CultureInfo.InvariantCulture),
			auditEvent.Action,
			((int)auditEvent.Outcome).ToString(CultureInfo.InvariantCulture),
			auditEvent.Timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
			auditEvent.ActorId,
			auditEvent.ActorType,
			auditEvent.ResourceId,
			auditEvent.ResourceType,
			auditEvent.ResourceClassification?.ToString(),
			auditEvent.TenantId,
			auditEvent.ApplicationName,
			auditEvent.CorrelationId,
			auditEvent.SessionId,
			auditEvent.IpAddress,
			auditEvent.UserAgent,
			auditEvent.Reason,
		};

		// Metadata, deterministically ordered by key; key and value appended as separate length-prefixed
		// fields so distinct metadata sets cannot collide.
		if (auditEvent.Metadata is { Count: > 0 })
		{
			foreach (var kvp in auditEvent.Metadata.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal))
			{
				fields.Add(kvp.Key);
				fields.Add(kvp.Value);
			}
		}

		return AuditRecordCanonicalizer.Canonicalize([.. fields]);
	}
}
