// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a security audit event stored in Elasticsearch.
/// </summary>
public sealed class SecurityAuditEvent
{
	/// <inheritdoc/>
	public string EventId { get; set; } = string.Empty;

	/// <inheritdoc/>
	public DateTimeOffset Timestamp { get; set; }

	/// <inheritdoc/>
	public SecurityEventType EventType { get; set; }

	/// <inheritdoc/>
	public SecurityEventSeverity Severity { get; set; }

	/// <inheritdoc/>
	public string Source { get; set; } = string.Empty;

	/// <inheritdoc/>
	public string? UserId { get; set; }

	/// <inheritdoc/>
	public string? SourceIpAddress { get; set; }

	/// <inheritdoc/>
	public string? UserAgent { get; set; }

	/// <inheritdoc/>
	public Dictionary<string, object> Details { get; set; } = [];

	/// <inheritdoc/>
	public string IntegrityHash { get; set; } = string.Empty;
}
