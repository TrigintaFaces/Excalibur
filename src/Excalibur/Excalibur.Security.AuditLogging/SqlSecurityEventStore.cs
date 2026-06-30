// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;

namespace Excalibur.Security;

/// <summary>
/// An <see cref="ISecurityEventStore"/> that bridges security events onto the existing
/// <see cref="IAuditStore"/> (e.g. <c>Excalibur.AuditLogging.SqlServer</c>), giving security
/// events durable, tamper-evident SQL persistence through the audit-store abstraction without
/// duplicating any SQL/Dapper/hash-chain machinery (: wire the advertised seam by composing,
/// never fork a parallel store).
/// </summary>
/// <remarks>
/// <para>
/// <b>Round-trip fidelity (documented boundary).</b> The mapping carries the audit-relevant
/// who/what/when/where of a <see cref="SecurityEvent"/> losslessly:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="SecurityEvent.Id"/> ↔ <see cref="AuditEvent.EventId"/>.</description></item>
/// <item><description><see cref="SecurityEvent.EventType"/> ↔ <see cref="AuditEvent.Action"/> (the enum name, parsed back exactly).</description></item>
/// <item><description><see cref="SecurityEvent.Timestamp"/>, <see cref="SecurityEvent.CorrelationId"/>, <see cref="SecurityEvent.UserId"/> (→ <see cref="AuditEvent.ActorId"/>).</description></item>
/// <item><description><see cref="SecurityEvent.SourceIp"/> → <see cref="AuditEvent.IpAddress"/>, <see cref="SecurityEvent.UserAgent"/> → <see cref="AuditEvent.UserAgent"/>, <see cref="SecurityEvent.Description"/> → <see cref="AuditEvent.Reason"/> — purpose-built dedicated columns (subject to the audit pipeline's PII masking), <b>not</b> the free-form metadata bag.</description></item>
/// <item><description><see cref="SecurityEvent.Severity"/> and <see cref="SecurityEvent.MessageType"/> → <see cref="AuditEvent.Metadata"/> (non-sensitive reference values only).</description></item>
/// </list>
/// <para>
/// <b>Intentionally not carried:</b> <see cref="SecurityEvent.AdditionalData"/> (arbitrary
/// <c>object?</c> forensic payload) has no compliant home in <see cref="AuditEvent"/> — the
/// free-form <see cref="AuditEvent.Metadata"/> is contractually "references/identifiers only, no
/// sensitive values." Full forensic persistence (IP/UA/payload with encryption-at-rest + retention)
/// is a dedicated security-event store, tracked separately as <c></c>. This bridge honestly
/// closes the audit-relevant portion of the advertised durable-SQL seam.
/// </para>
/// </remarks>
internal sealed class SqlSecurityEventStore : ISecurityEventStore
{
	// Non-sensitive reference keys in the free-form audit metadata bag. SourceIp/UserAgent/AdditionalData
	// are NEVER written here (the compliance contract bars sensitive values from the catch-all dictionary).
	internal const string SeverityMetadataKey = "security.severity";
	internal const string MessageTypeMetadataKey = "security.messageType";

	// Sentinel recorded in the required AuditEvent.ActorId when a SecurityEvent has no UserId, so the
	// reverse mapping can restore UserId == null rather than an empty/placeholder actor.
	internal const string UnknownActor = "(unknown)";

	private readonly IAuditStore _auditStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlSecurityEventStore"/> class.
	/// </summary>
	/// <param name="auditStore">The underlying audit store (e.g. the SQL Server audit store) that provides durable, hash-chained persistence.</param>
	public SqlSecurityEventStore(IAuditStore auditStore)
	{
		ArgumentNullException.ThrowIfNull(auditStore);
		_auditStore = auditStore;
	}

	/// <inheritdoc/>
	public async Task StoreEventsAsync(IEnumerable<SecurityEvent> events, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(events);

		foreach (var securityEvent in events)
		{
			_ = await _auditStore.StoreAsync(ToAuditEvent(securityEvent), cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task<IEnumerable<SecurityEvent>> QueryEventsAsync(
		SecurityEventQuery query,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		var auditEvents = await _auditStore
			.QueryAsync(ToAuditQuery(query), cancellationToken)
			.ConfigureAwait(false);

		var results = auditEvents.Select(ToSecurityEvent);

		// MinimumSeverity has no native audit column (severity lives in the metadata bag) → filter here.
		if (query.MinimumSeverity is { } minSeverity)
		{
			results = results.Where(e => e.Severity >= minSeverity);
		}

		return results.ToList();
	}

	private static AuditEvent ToAuditEvent(SecurityEvent securityEvent)
	{
		var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			[SeverityMetadataKey] = securityEvent.Severity.ToString(),
		};

		if (!string.IsNullOrEmpty(securityEvent.MessageType))
		{
			metadata[MessageTypeMetadataKey] = securityEvent.MessageType;
		}

		return new AuditEvent
		{
			EventId = securityEvent.Id.ToString(),
			EventType = MapToAuditEventType(securityEvent.EventType),
			Action = securityEvent.EventType.ToString(),
			Outcome = MapToAuditOutcome(securityEvent.EventType),
			Timestamp = securityEvent.Timestamp,
			ActorId = string.IsNullOrEmpty(securityEvent.UserId) ? UnknownActor : securityEvent.UserId,
			CorrelationId = securityEvent.CorrelationId?.ToString(),
			IpAddress = securityEvent.SourceIp,
			UserAgent = securityEvent.UserAgent,
			Reason = securityEvent.Description,
			Metadata = metadata,
		};
	}

	private static SecurityEvent ToSecurityEvent(AuditEvent auditEvent)
	{
		var severity = SecuritySeverity.Low;
		if (auditEvent.Metadata is not null
			&& auditEvent.Metadata.TryGetValue(SeverityMetadataKey, out var severityText)
			&& Enum.TryParse<SecuritySeverity>(severityText, out var parsedSeverity))
		{
			severity = parsedSeverity;
		}

		var eventType = Enum.TryParse<SecurityEventType>(auditEvent.Action, out var parsedEventType)
			? parsedEventType
			: default;

		Guid? correlationId = Guid.TryParse(auditEvent.CorrelationId, out var parsedCorrelation)
			? parsedCorrelation
			: null;

		return new SecurityEvent
		{
			Id = Guid.TryParse(auditEvent.EventId, out var id) ? id : Guid.Empty,
			Timestamp = auditEvent.Timestamp,
			EventType = eventType,
			Description = auditEvent.Reason ?? string.Empty,
			Severity = severity,
			CorrelationId = correlationId,
			UserId = string.Equals(auditEvent.ActorId, UnknownActor, StringComparison.Ordinal) ? null : auditEvent.ActorId,
			SourceIp = auditEvent.IpAddress,
			UserAgent = auditEvent.UserAgent,
			MessageType = auditEvent.Metadata is not null
				&& auditEvent.Metadata.TryGetValue(MessageTypeMetadataKey, out var messageType)
					? messageType
					: null,
			// AdditionalData intentionally not restored — see class remarks (bd-8f1l09).
		};
	}

	private static AuditQuery ToAuditQuery(SecurityEventQuery query) =>
		new()
		{
			StartDate = query.StartTime,
			EndDate = query.EndTime,
			ActorId = query.UserId,
			IpAddress = query.SourceIp,
			CorrelationId = query.CorrelationId?.ToString(),
			Action = query.EventType?.ToString(),
			MaxResults = query.MaxResults,
		};

	// Best-effort semantic categorization for native audit-store queryability. SecurityEvent.EventType
	// itself round-trips losslessly via AuditEvent.Action, so this mapping need not be reversible.
	private static AuditEventType MapToAuditEventType(SecurityEventType eventType) =>
		eventType switch
		{
			SecurityEventType.AuthenticationSuccess or SecurityEventType.AuthenticationFailure => AuditEventType.Authentication,
			SecurityEventType.AuthorizationSuccess or SecurityEventType.AuthorizationFailure => AuditEventType.Authorization,
			SecurityEventType.ConfigurationChange => AuditEventType.ConfigurationChange,
			SecurityEventType.AuditLogAccess => AuditEventType.Compliance,
			_ => AuditEventType.Security,
		};

	private static AuditOutcome MapToAuditOutcome(SecurityEventType eventType) =>
		eventType switch
		{
			SecurityEventType.AuthenticationSuccess or SecurityEventType.AuthorizationSuccess => AuditOutcome.Success,
			SecurityEventType.AuthorizationFailure or SecurityEventType.RateLimitExceeded => AuditOutcome.Denied,
			_ => AuditOutcome.Failure,
		};
}
