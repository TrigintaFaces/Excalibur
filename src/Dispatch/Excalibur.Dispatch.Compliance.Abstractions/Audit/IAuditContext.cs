// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Scoped audit context available within message handlers for emitting
/// domain-aware audit observations.
/// </summary>
/// <remarks>
/// <para>
/// Inherits pipeline context (correlation, actor, tenant) automatically.
/// Handlers only provide the condition, message, and severity — the framework
/// supplies all contextual data.
/// </para>
/// <para>
/// Registered as <b>scoped</b> in DI and initialized by
/// <c>AuditContextMiddleware</c> before handler execution.
/// </para>
/// </remarks>
public interface IAuditContext
{
	/// <summary>
	/// Records an audit assertion if the condition is true.
	/// No-op if condition is false (zero overhead for passing assertions).
	/// </summary>
	/// <param name="condition">The domain condition observed.</param>
	/// <param name="message">Human-readable description of what was observed.</param>
	/// <param name="eventType">Classification of the audit event.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The audit event ID if recorded, <see langword="null"/> if condition was false.</returns>
	Task<AuditEventId?> AssertAsync(
		bool condition,
		string message,
		AuditEventType eventType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Unconditionally records a custom audit observation.
	/// Use for events that do not depend on a boolean condition.
	/// </summary>
	/// <param name="message">Human-readable description.</param>
	/// <param name="eventType">Classification of the audit event.</param>
	/// <param name="outcome">The outcome to record.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The audit event ID.</returns>
	Task<AuditEventId> ObserveAsync(
		string message,
		AuditEventType eventType,
		AuditOutcome outcome,
		CancellationToken cancellationToken);

	/// <summary>
	/// Sets additional metadata for subsequent assertions/observations in this scope.
	/// </summary>
	/// <param name="key">The metadata key.</param>
	/// <param name="value">The metadata value.</param>
	/// <returns>This context for fluent chaining.</returns>
	IAuditContext WithMetadata(string key, string value);

	/// <summary>
	/// Associates the audit context with a specific resource (e.g., aggregate ID).
	/// </summary>
	/// <param name="resourceId">The resource identifier.</param>
	/// <param name="resourceType">The resource type name.</param>
	/// <returns>This context for fluent chaining.</returns>
	IAuditContext ForResource(string resourceId, string resourceType);
}
