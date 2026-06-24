// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// Exception thrown when an audit event cannot be durably persisted to the underlying audit store.
/// </summary>
/// <remarks>
/// <para>
/// Audit logging is <b>fail-closed</b>: when the audit store fails, the failure is surfaced to the caller
/// as this exception rather than being masked behind a success-shaped result. A compliance audit trail that
/// silently drops events is worse than one that fails loudly, because callers would otherwise treat an
/// unrecorded event as durably stored.
/// </para>
/// <para>
/// Callers that require availability over durability (fail-open) must catch this exception explicitly and
/// apply their own policy (for example, queueing the event for later retry). The framework does not silently
/// swallow audit-store failures.
/// </para>
/// </remarks>
public sealed class AuditPersistenceException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuditPersistenceException"/> class.
	/// </summary>
	public AuditPersistenceException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditPersistenceException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public AuditPersistenceException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditPersistenceException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The underlying audit-store failure.</param>
	public AuditPersistenceException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the identifier of the audit event that failed to persist, when available.
	/// </summary>
	/// <value>The <c>EventId</c> of the unsaved audit event, or <see langword="null"/> if not known.</value>
	public string? EventId { get; init; }
}
