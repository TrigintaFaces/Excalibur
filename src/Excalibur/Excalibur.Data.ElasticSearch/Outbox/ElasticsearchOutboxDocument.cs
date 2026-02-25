// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Outbox;

/// <summary>
/// Internal document model for Elasticsearch outbox serialization.
/// </summary>
internal sealed class ElasticsearchOutboxDocument
{
	public string Id { get; set; } = string.Empty;
	public string MessageType { get; set; } = string.Empty;
	public string? PayloadBase64 { get; set; }
	public string Destination { get; set; } = string.Empty;
	public DateTimeOffset CreatedAt { get; set; }
	public int Status { get; set; }
	public int Priority { get; set; }
	public int RetryCount { get; set; }
	public string? CorrelationId { get; set; }
	public string? CausationId { get; set; }
	public string? TenantId { get; set; }
	public string? LastError { get; set; }
	public DateTimeOffset? ScheduledAt { get; set; }
	public DateTimeOffset? SentAt { get; set; }
	public DateTimeOffset? LastAttemptAt { get; set; }
	public Dictionary<string, string>? Headers { get; set; }
}
