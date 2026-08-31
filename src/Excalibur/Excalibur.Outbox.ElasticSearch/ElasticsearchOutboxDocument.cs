// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.ElasticSearch;

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

	// Consumer-supplied routing fields — persisted so they round-trip on reload (a dropped routing field is
	// silent consumer-data loss).
	public string? PartitionKey { get; set; }
	public string? GroupKey { get; set; }
	public string? TargetTransports { get; set; }
	public bool IsMultiTransport { get; set; }

	// Lease (visibility-timeout) columns. A claimed message is LEASED while its status stays Staged —
	// per the OutboxStatus contract, concurrent delivery is guarded by these fields, not by a status flip.
	// A null/expired LeaseExpiresAt means the message is claimable; a terminal transition clears both.
	public DateTimeOffset? LeaseExpiresAt { get; set; }
	public string? LeasedBy { get; set; }

	// Failure-anchored re-claim floor. Stamped at the failure instant (never derived from the lease, which
	// a never-claimed message does not have), and read by the claim query: the message is excluded until
	// this instant passes, then becomes claimable again so a failure is never a silent terminal drop.
	public DateTimeOffset? NextAttemptAt { get; set; }

	public string? LastError { get; set; }
	public DateTimeOffset? ScheduledAt { get; set; }
	public DateTimeOffset? SentAt { get; set; }
	public DateTimeOffset? LastAttemptAt { get; set; }
	public Dictionary<string, object>? Headers { get; set; }
}
