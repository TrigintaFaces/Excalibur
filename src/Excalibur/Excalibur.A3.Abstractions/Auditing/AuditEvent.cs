// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Auditing;

/// <summary>
/// Minimal in-memory implementation of <see cref="IAuditEvent" />.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuditEvent" /> class.
/// </remarks>
/// <param name="timestampUtc"> UTC occurrence time. </param>
/// <param name="tenantId"> Tenant id. </param>
/// <param name="actorId"> Actor id. </param>
/// <param name="action"> Action name. </param>
/// <param name="resource"> Target resource. </param>
/// <param name="outcome"> Outcome string. </param>
/// <param name="correlationId"> Optional correlation id. </param>
/// <param name="attributes"> Optional attributes. </param>
public sealed class AuditEvent(
	DateTimeOffset timestampUtc,
	string tenantId,
	string actorId,
	string action,
	string resource,
	string outcome,
	string? correlationId = null,
	IReadOnlyDictionary<string, string>? attributes = null) : IAuditEvent
{
	/// <inheritdoc />
	public DateTimeOffset TimestampUtc { get; } = timestampUtc;

	/// <inheritdoc />
	public string? CorrelationId { get; } = correlationId;

	/// <inheritdoc />
	public string TenantId { get; } = tenantId;

	/// <inheritdoc />
	public string ActorId { get; } = actorId;

	/// <inheritdoc />
	public string Action { get; } = action;

	/// <inheritdoc />
	public string Resource { get; } = resource;

	/// <inheritdoc />
	public string Outcome { get; } = outcome;

	/// <inheritdoc />
	public IReadOnlyDictionary<string, string>? Attributes { get; } = attributes;
}
