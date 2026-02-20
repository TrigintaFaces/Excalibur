// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents an auditable event in the system.
/// </summary>
/// <remarks>
/// <para>
/// Audit events form a hash-chained, tamper-evident log for:
/// - SOC2 compliance evidence
/// - Security incident investigation
/// - Data access tracking
/// - Regulatory reporting
/// </para>
/// <para> Audit events are immutable once created and should never contain sensitive data values (only references/identifiers). </para>
/// </remarks>
public sealed record AuditEvent
{
	/// <summary>
	/// Gets the unique identifier for this audit event.
	/// </summary>
	public required string EventId { get; init; }

	/// <summary>
	/// Gets the type/category of this audit event.
	/// </summary>
	public required AuditEventType EventType { get; init; }

	/// <summary>
	/// Gets the action that was performed (e.g., "Read", "Create", "Update", "Delete", "Login").
	/// </summary>
	public required string Action { get; init; }

	/// <summary>
	/// Gets the outcome of the audited operation.
	/// </summary>
	public required AuditOutcome Outcome { get; init; }

	/// <summary>
	/// Gets the timestamp when the event occurred.
	/// </summary>
	public required DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets the identifier of the actor (user, service, system) who performed the action.
	/// </summary>
	public required string ActorId { get; init; }

	/// <summary>
	/// Gets the type of actor (e.g., "User", "Service", "System").
	/// </summary>
	public string? ActorType { get; init; }

	/// <summary>
	/// Gets the identifier of the resource being accessed or modified.
	/// </summary>
	public string? ResourceId { get; init; }

	/// <summary>
	/// Gets the type of resource (e.g., "Customer", "Order", "Configuration").
	/// </summary>
	public string? ResourceType { get; init; }

	/// <summary>
	/// Gets the data classification level of the accessed resource.
	/// </summary>
	public DataClassification? ResourceClassification { get; init; }

	/// <summary>
	/// Gets the tenant identifier for multi-tenant isolation.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the correlation ID linking related audit events.
	/// </summary>
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets the session ID associated with this event.
	/// </summary>
	public string? SessionId { get; init; }

	/// <summary>
	/// Gets the IP address from which the action originated.
	/// </summary>
	public string? IpAddress { get; init; }

	/// <summary>
	/// Gets the user agent or client application identifier.
	/// </summary>
	public string? UserAgent { get; init; }

	/// <summary>
	/// Gets the reason or justification for the action.
	/// </summary>
	public string? Reason { get; init; }

	/// <summary>
	/// Gets additional metadata as key-value pairs. Must not contain sensitive data values.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Metadata { get; init; }

	/// <summary>
	/// Gets the hash of the previous audit event in the chain. Null for the first event in the chain.
	/// </summary>
	public string? PreviousEventHash { get; init; }

	/// <summary>
	/// Gets the hash of this audit event (set by the audit store).
	/// </summary>
	public string? EventHash { get; init; }
}
