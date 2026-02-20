// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Inbox;

/// <summary>
/// Internal document model for Elasticsearch inbox serialization.
/// </summary>
internal sealed class ElasticsearchInboxDocument
{
	public string MessageId { get; set; } = string.Empty;
	public string HandlerType { get; set; } = string.Empty;
	public string MessageType { get; set; } = string.Empty;
	public string? PayloadBase64 { get; set; }
	public Dictionary<string, object>? Metadata { get; set; }
	public DateTimeOffset ReceivedAt { get; set; }
	public DateTimeOffset? ProcessedAt { get; set; }
	public int Status { get; set; }
	public string? LastError { get; set; }
	public int RetryCount { get; set; }
	public DateTimeOffset? LastAttemptAt { get; set; }
	public string? CorrelationId { get; set; }
	public string? TenantId { get; set; }
	public string? Source { get; set; }
}
