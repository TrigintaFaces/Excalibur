// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Auditing;

/// <summary>
/// Represents an immutable, provider-neutral audit event.
/// </summary>
public interface IAuditEvent
{
	/// <summary>
	/// Gets the UTC timestamp when the event occurred.
	/// </summary>
	/// <value>The UTC timestamp of the audit event.</value>
	DateTimeOffset TimestampUtc { get; }

	/// <summary>
	/// Gets the optional correlation identifier for tracing across services.
	/// </summary>
	/// <value>The correlation ID, or <see langword="null"/> if not available.</value>
	string? CorrelationId { get; }

	/// <summary>
	/// Gets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <value>The tenant identifier.</value>
	string TenantId { get; }

	/// <summary>
	/// Gets the actor identifier (user/service) responsible for the action.
	/// </summary>
	/// <value>The identifier of the actor who performed the action.</value>
	string ActorId { get; }

	/// <summary>
	/// Gets the canonical action name (e.g., "CreateOrder", "DeleteMessage").
	/// </summary>
	/// <value>The canonical name of the action performed.</value>
	string Action { get; }

	/// <summary>
	/// Gets the resource affected by the action (type/id or URI).
	/// </summary>
	/// <value>The resource identifier or URI.</value>
	string Resource { get; }

	/// <summary>
	/// Gets the outcome of the action (e.g., "Success", "Denied", "Failed").
	/// </summary>
	/// <value>The outcome of the action.</value>
	string Outcome { get; }

	/// <summary>
	/// Gets the optional additional context as key/value pairs.
	/// </summary>
	/// <value>A dictionary of additional attributes, or <see langword="null"/> if not available.</value>
	IReadOnlyDictionary<string, string>? Attributes { get; }
}

// Intentionally no implementation type in this file to satisfy analyzer rules.
