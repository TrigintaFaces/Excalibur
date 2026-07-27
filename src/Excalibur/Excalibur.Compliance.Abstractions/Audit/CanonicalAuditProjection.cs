// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

namespace Excalibur.Compliance;

/// <summary>
/// Single source of truth for the canonical audit-field <b>names</b>, <b>order</b>, and <b>selection</b>
/// that every audit sink emits — so adding or renaming a canonical field is one edit here, not one silent
/// edit per sink.
/// </summary>
/// <remarks>
/// <para>
/// This owns the canonical field <em>schema</em> for the <b>wire</b> representation (what sinks serialize).
/// It deliberately does <b>not</b> own value <em>rendering</em> beyond the shared culture-invariant string
/// form: a sink applies its own backend formatting on top (e.g. an object field vs a string field, or a
/// backend-specific field name) where it genuinely differs.
/// </para>
/// <para>
/// <b>Hash independence.</b> The tamper-evidence hash input is owned separately by
/// <c>AuditEventCanonicalizer</c> (in <c>Excalibur.AuditLogging</c>) and is <b>not</b> derived from this
/// projection — the hashed field set, order, and rendering (integer enum casts, unix-millisecond timestamp)
/// are frozen there and are intentionally distinct from the wire rendering. Changing this wire projection
/// never perturbs the hash.
/// </para>
/// <para>
/// <b>Per-sink fields.</b> Three fields are intentionally not in the shared core because sinks treat them
/// differently, and are exposed via dedicated members instead:
/// <list type="bullet">
/// <item><description><see cref="TimestampField"/> — sinks emit the timestamp with backend-specific typing
/// and placement (a typed <c>DateTimeOffset</c>, an ISO-8601 string, a HEC epoch, a log-entry timestamp).</description></item>
/// <item><description><b>ApplicationName</b> — an Elasticsearch/OpenSearch index-tag sink-option
/// (<c>application_name</c>); the other sinks do not expose it. Owned here so the omission is explicit and
/// cannot silently re-drift, but emitted only by those sinks.</description></item>
/// <item><description><b>Metadata</b> — a variable-length dictionary, emitted as a nested object/field set
/// by each sink (see <see cref="MetadataField"/>).</description></item>
/// </list>
/// </para>
/// </remarks>
public static class CanonicalAuditProjection
{
	/// <summary>The canonical field name for the timestamp (per-sink typed/placed; see <see cref="TimestampField"/>).</summary>
	public const string TimestampFieldName = "timestamp";

	/// <summary>The canonical field name for the Elasticsearch/OpenSearch application-name index tag.</summary>
	public const string ApplicationNameFieldName = "application_name";

	/// <summary>The canonical field name for the metadata object (see <see cref="MetadataField"/>).</summary>
	public const string MetadataFieldName = "metadata";

	/// <summary>
	/// Projects the shared, ordered <b>core</b> canonical fields common to every sink — the string-valued
	/// fields whose name, order, and rendering are identical across backends. Timestamp, application name,
	/// and metadata are handled per-sink (see the type remarks).
	/// </summary>
	/// <param name="auditEvent">The audit event to project.</param>
	/// <returns>The ordered core canonical fields (name, culture-invariant string value or <see langword="null"/>).</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="auditEvent"/> is <see langword="null"/>.</exception>
	public static IReadOnlyList<(string Name, string? Value)> ProjectCore(AuditEvent auditEvent)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		return
		[
			("event_id", auditEvent.EventId),
			("event_type", auditEvent.EventType.ToString()),
			("action", auditEvent.Action),
			("outcome", auditEvent.Outcome.ToString()),
			("actor_id", auditEvent.ActorId),
			("actor_type", auditEvent.ActorType),
			("resource_id", auditEvent.ResourceId),
			("resource_type", auditEvent.ResourceType),
			("resource_classification", auditEvent.ResourceClassification?.ToString()),
			("tenant_id", auditEvent.TenantId),
			("correlation_id", auditEvent.CorrelationId),
			("session_id", auditEvent.SessionId),
			("ip_address", auditEvent.IpAddress),
			("user_agent", auditEvent.UserAgent),
			("reason", auditEvent.Reason),
			("event_hash", auditEvent.EventHash),
		];
	}

	/// <summary>
	/// Gets the timestamp field rendered as an ISO-8601 round-trip (<c>"O"</c>) string, for sinks that
	/// serialize the timestamp as a string field. Sinks emitting a typed timestamp use
	/// <see cref="AuditEvent.Timestamp"/> directly.
	/// </summary>
	/// <param name="auditEvent">The audit event.</param>
	/// <returns>The timestamp field.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="auditEvent"/> is <see langword="null"/>.</exception>
	public static (string Name, string? Value) TimestampField(AuditEvent auditEvent)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);
		return (TimestampFieldName, auditEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// Gets the per-event metadata as the canonical metadata field, or <see langword="null"/> when the
	/// event carries no metadata. Every sink emits per-event metadata (the omission of it on any sink is
	/// accidental drift, not a supported variation).
	/// </summary>
	/// <param name="auditEvent">The audit event.</param>
	/// <returns>The metadata, or <see langword="null"/> when none is present.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="auditEvent"/> is <see langword="null"/>.</exception>
	public static IReadOnlyDictionary<string, string>? MetadataField(AuditEvent auditEvent)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);
		return auditEvent.Metadata is { Count: > 0 } ? auditEvent.Metadata : null;
	}
}
